module internal FSharp.Grpc.Tools.DecodeGen

open Google.Protobuf.Reflection
open Fabulous.AST
open Fantomas.Core.SyntaxOak
open type Fabulous.AST.Ast
open FSharp.Grpc.Tools.TypeMapping
open FSharp.Grpc.Tools.WireFormat

/// Default literal for a scalar/enum field in decode (map key/value defaults).
let scalarDefault (field: FieldDescriptorProto) =
    match field.Type with
    | FieldDescriptorProto.Types.Type.String -> "\"\""
    | FieldDescriptorProto.Types.Type.Bool -> "false"
    | FieldDescriptorProto.Types.Type.Message -> $"{simpleTypeName field.TypeName}.empty"
    | FieldDescriptorProto.Types.Type.Int64
    | FieldDescriptorProto.Types.Type.Sint64
    | FieldDescriptorProto.Types.Type.Sfixed64 -> "0L"
    | FieldDescriptorProto.Types.Type.Uint64
    | FieldDescriptorProto.Types.Type.Fixed64 -> "0UL"
    | FieldDescriptorProto.Types.Type.Uint32
    | FieldDescriptorProto.Types.Type.Fixed32 -> "0u"
    | _ -> "0"

/// Classify field variable declarations for decode.
type FieldVar =
    | MutableScalar of fieldName: string * varName: string * defaultExpr: string
    | MutableList of fieldName: string * varName: string
    | MutableMap of fieldName: string * varName: string

/// Default value for a mutable scalar field declaration in decode.
let fieldDefault (field: FieldDescriptorProto) =
    match field.Type with
    | FieldDescriptorProto.Types.Type.Double -> "0.0"
    | FieldDescriptorProto.Types.Type.Float -> "0.0f"
    | FieldDescriptorProto.Types.Type.Bytes -> "Array.empty"
    | FieldDescriptorProto.Types.Type.Enum -> "LanguagePrimitives.EnumOfValue 0"
    | _ -> scalarDefault field

/// Generate decode match clauses for all cases of a oneof group.
let decodeOneofClauses (msg: DescriptorProto) (idx: int) =
    let decl = msg.OneofDecl.[idx]
    let duName = oneofTypeName msg.Name decl.Name
    let duFieldName = toPascalCase decl.Name

    let oneofCases =
        msg.Field
        |> Seq.filter (fun of' -> of'.HasOneofIndex && not of'.Proto3Optional && of'.OneofIndex = idx)
        |> Seq.sortBy (fun of' -> of'.Number)
        |> Seq.toList

    oneofCases
    |> List.map (fun c ->
        let caseName = toPascalCase c.Name
        let rm = readMethod c

        if c.Type = FieldDescriptorProto.Types.Type.Message then
            let msgTypeName = simpleTypeName c.TypeName

            MatchClauseExpr(
                ConstantPat(Constant($"{c.Number}")),
                CompExprBodyExpr(
                    [ LetOrUseExpr(Use("subInput", E "input.ReadBytes().CreateCodedInput()"))
                      OtherExpr(
                          LongIdentSetExpr(
                              $"_{duFieldName}",
                              E $"Some({duName}.{caseName}({msgTypeName}.decodeFrom subInput))"
                          )
                      ) ]
                )
            )
        else
            let castExpr =
                if c.Type = FieldDescriptorProto.Types.Type.Enum then
                    $"LanguagePrimitives.EnumOfValue(input.{rm}())"
                else
                    $"input.{rm}()"

            MatchClauseExpr(
                ConstantPat(Constant($"{c.Number}")),
                LongIdentSetExpr($"_{duFieldName}", E $"Some({duName}.{caseName}({castExpr}))")
            ))

/// Generate decode match clause for a map field.
let decodeMapClause (msg: DescriptorProto) (f: FieldDescriptorProto) (fname: string) (fieldNum: int) =
    let entry = (tryGetMapEntry msg f).Value
    let keyField = entry.Field.[0]
    let valueField = entry.Field.[1]
    let keyRm = readMethod keyField
    let keyDefault = scalarDefault keyField
    let valueDefault = scalarDefault valueField

    let valueClause =
        if valueField.Type = FieldDescriptorProto.Types.Type.Message then
            let msgTypeName = simpleTypeName valueField.TypeName

            MatchClauseExpr(
                ConstantPat(Constant("2")),
                CompExprBodyExpr(
                    [ LetOrUseExpr(Use("subInput", E "entryInput.ReadBytes().CreateCodedInput()"))
                      OtherExpr(LongIdentSetExpr("mapValue", E $"{msgTypeName}.decodeFrom subInput")) ]
                )
            )
        else
            let valueRm = readMethod valueField

            let valueCast =
                if valueField.Type = FieldDescriptorProto.Types.Type.Enum then
                    "LanguagePrimitives.EnumOfValue("
                else
                    ""

            let valueCastEnd =
                if valueField.Type = FieldDescriptorProto.Types.Type.Enum then
                    ")"
                else
                    ""

            MatchClauseExpr(
                ConstantPat(Constant("2")),
                LongIdentSetExpr("mapValue", E $"{valueCast}entryInput.{valueRm}(){valueCastEnd}")
            )

    let entryClauses =
        [ MatchClauseExpr(ConstantPat(Constant("1")), LongIdentSetExpr("key", E $"entryInput.{keyRm}()"))
          valueClause
          MatchClauseExpr(ConstantPat(Constant("_")), E "entryInput.SkipLastField()") ]

    MatchClauseExpr(
        ConstantPat(Constant($"{fieldNum}")),
        CompExprBodyExpr(
            [ LetOrUseExpr(Use("entryInput", E "input.ReadBytes().CreateCodedInput()"))
              LetOrUseExpr(Value("key", E keyDefault).toMutable ())
              LetOrUseExpr(Value("mapValue", E valueDefault).toMutable ())
              LetOrUseExpr(Value("entryTag", E "entryInput.ReadTag()").toMutable ())
              OtherExpr(
                  WhileExpr(
                      E "entryTag <> 0u",
                      CompExprBodyExpr(
                          [ OtherExpr(
                                MatchExpr(E "Google.Protobuf.WireFormat.GetTagFieldNumber(entryTag)", entryClauses)
                            )
                            OtherExpr(LongIdentSetExpr("entryTag", E "entryInput.ReadTag()")) ]
                      )
                  )
              )
              OtherExpr(E $"_{fname} <- Map.add key mapValue _{fname}") ]
        )
    )

/// Generate decode match clause for a repeated field.
let decodeRepeatedClause (f: FieldDescriptorProto) (fname: string) (fieldNum: int) =
    if f.Type = FieldDescriptorProto.Types.Type.Message then
        let msgTypeName = simpleTypeName f.TypeName

        MatchClauseExpr(
            ConstantPat(Constant($"{fieldNum}")),
            CompExprBodyExpr(
                [ LetOrUseExpr(Use("subInput", E "input.ReadBytes().CreateCodedInput()"))
                  OtherExpr(E $"_{fname} <- {msgTypeName}.decodeFrom subInput :: _{fname}") ]
            )
        )
    elif f.Type = FieldDescriptorProto.Types.Type.String then
        MatchClauseExpr(ConstantPat(Constant($"{fieldNum}")), E $"_{fname} <- input.ReadString() :: _{fname}")
    elif f.Type = FieldDescriptorProto.Types.Type.Bytes then
        MatchClauseExpr(
            ConstantPat(Constant($"{fieldNum}")),
            E $"_{fname} <- input.ReadBytes().ToByteArray() :: _{fname}"
        )
    else
        let rm = readMethod f

        let castExpr =
            if f.Type = FieldDescriptorProto.Types.Type.Enum then
                $"LanguagePrimitives.EnumOfValue(input.{rm}())"
            else
                $"input.{rm}()"

        let packedCastExpr =
            if f.Type = FieldDescriptorProto.Types.Type.Enum then
                $"LanguagePrimitives.EnumOfValue(packedInput.{rm}())"
            else
                $"packedInput.{rm}()"

        MatchClauseExpr(
            ConstantPat(Constant($"{fieldNum}")),
            CompExprBodyExpr(
                [ LetOrUseExpr(Value("wt", E "Google.Protobuf.WireFormat.GetTagWireType(tag)"))
                  OtherExpr(
                      IfThenElseExpr(
                          E "wt = Google.Protobuf.WireFormat.WireType.LengthDelimited",
                          CompExprBodyExpr(
                              [ LetOrUseExpr(Use("packedInput", E "input.ReadBytes().CreateCodedInput()"))
                                OtherExpr(
                                    WhileExpr(
                                        E "not packedInput.IsAtEnd",
                                        E $"_{fname} <- {packedCastExpr} :: _{fname}"
                                    )
                                ) ]
                          ),
                          E $"_{fname} <- {castExpr} :: _{fname}"
                      )
                  ) ]
            )
        )

/// Generate decode match clause for a proto3 optional field.
let decodeOptionalClause (f: FieldDescriptorProto) (fname: string) (fieldNum: int) =
    let rm = readMethod f

    if f.Type = FieldDescriptorProto.Types.Type.Message then
        let msgTypeName = simpleTypeName f.TypeName

        MatchClauseExpr(
            ConstantPat(Constant($"{fieldNum}")),
            CompExprBodyExpr(
                [ LetOrUseExpr(Use("subInput", E "input.ReadBytes().CreateCodedInput()"))
                  OtherExpr(LongIdentSetExpr($"_{fname}", E $"Some({msgTypeName}.decodeFrom subInput)")) ]
            )
        )
    elif f.Type = FieldDescriptorProto.Types.Type.Enum then
        MatchClauseExpr(
            ConstantPat(Constant($"{fieldNum}")),
            LongIdentSetExpr($"_{fname}", E $"Some(LanguagePrimitives.EnumOfValue(input.{rm}()))")
        )
    else
        MatchClauseExpr(ConstantPat(Constant($"{fieldNum}")), LongIdentSetExpr($"_{fname}", E $"Some(input.{rm}())"))

/// Generate decode match clause for a (non-optional, non-repeated) message field.
let decodeMessageClause (f: FieldDescriptorProto) (fname: string) (fieldNum: int) =
    let msgTypeName = simpleTypeName f.TypeName

    match Map.tryFind f.TypeName wellKnownTypeMap with
    | Some wktType ->
        MatchClauseExpr(
            ConstantPat(Constant($"{fieldNum}")),
            CompExprBodyExpr(
                [ LetOrUseExpr(Use("subInput", E "input.ReadBytes().CreateCodedInput()"))
                  LetOrUseExpr(Value("msg", E $"{wktType}()"))
                  OtherExpr(E "msg.MergeFrom(subInput)")
                  OtherExpr(LongIdentSetExpr($"_{fname}", E "Some(msg)")) ]
            )
        )
    | None ->
        match Map.tryFind f.TypeName wrapperTypeMap with
        | Some _ ->
            let wrapperClassName = simpleTypeName f.TypeName

            MatchClauseExpr(
                ConstantPat(Constant($"{fieldNum}")),
                CompExprBodyExpr(
                    [ LetOrUseExpr(Use("subInput", E "input.ReadBytes().CreateCodedInput()"))
                      LetOrUseExpr(Value("wrapper", E $"Google.Protobuf.WellKnownTypes.{wrapperClassName}()"))
                      OtherExpr(E "wrapper.MergeFrom(subInput)")
                      OtherExpr(LongIdentSetExpr($"_{fname}", E "Some(wrapper.Value)")) ]
                )
            )
        | None ->
            MatchClauseExpr(
                ConstantPat(Constant($"{fieldNum}")),
                CompExprBodyExpr(
                    [ LetOrUseExpr(Use("subInput", E "input.ReadBytes().CreateCodedInput()"))
                      OtherExpr(LongIdentSetExpr($"_{fname}", E $"Some({msgTypeName}.decodeFrom subInput)")) ]
                )
            )

/// Generate decode match clause for a plain scalar field.
let decodeScalarClause (f: FieldDescriptorProto) (fname: string) (fieldNum: int) =
    let rm = readMethod f

    if f.Type = FieldDescriptorProto.Types.Type.Enum then
        MatchClauseExpr(
            ConstantPat(Constant($"{fieldNum}")),
            LongIdentSetExpr($"_{fname}", E $"LanguagePrimitives.EnumOfValue(input.{rm}())")
        )
    elif f.Type = FieldDescriptorProto.Types.Type.Bytes then
        MatchClauseExpr(
            ConstantPat(Constant($"{fieldNum}")),
            LongIdentSetExpr($"_{fname}", E $"input.{rm}().ToByteArray()")
        )
    else
        MatchClauseExpr(ConstantPat(Constant($"{fieldNum}")), LongIdentSetExpr($"_{fname}", E $"input.{rm}()"))

/// Generate decodeFrom function as AST widget (reads from existing CodedInputStream).
let generateDecodeFromAST (msg: DescriptorProto) =
    let mutable emittedOneofs = Set.empty
    let fieldVars = ResizeArray<FieldVar>()
    let clauses = ResizeArray<WidgetBuilder<MatchClauseNode>>()

    for f in msg.Field |> Seq.sortBy (fun f -> f.Number) do
        let fname = toPascalCase f.Name
        let fieldNum = f.Number

        if f.HasOneofIndex && not f.Proto3Optional then
            let idx = f.OneofIndex

            if not (emittedOneofs |> Set.contains idx) then
                emittedOneofs <- emittedOneofs |> Set.add idx
                let decl = msg.OneofDecl.[idx]
                let duFieldName = toPascalCase decl.Name
                fieldVars.Add(MutableScalar(duFieldName, $"_{duFieldName}", "None"))
                clauses.AddRange(decodeOneofClauses msg idx)
        elif tryGetMapEntry msg f |> Option.isSome then
            fieldVars.Add(MutableMap(fname, $"_{fname}"))
            clauses.Add(decodeMapClause msg f fname fieldNum)
        elif f.Label = FieldDescriptorProto.Types.Label.Repeated then
            fieldVars.Add(MutableList(fname, $"_{fname}"))
            clauses.Add(decodeRepeatedClause f fname fieldNum)
        elif f.Proto3Optional then
            fieldVars.Add(MutableScalar(fname, $"_{fname}", "None"))
            clauses.Add(decodeOptionalClause f fname fieldNum)
        elif f.Type = FieldDescriptorProto.Types.Type.Message then
            fieldVars.Add(MutableScalar(fname, $"_{fname}", "None"))
            clauses.Add(decodeMessageClause f fname fieldNum)
        else
            fieldVars.Add(MutableScalar(fname, $"_{fname}", fieldDefault f))
            clauses.Add(decodeScalarClause f fname fieldNum)

    clauses.Add(MatchClauseExpr(ConstantPat(Constant("_")), E "input.SkipLastField()"))

    // Build variable declarations
    let varDecls =
        fieldVars
        |> Seq.map (fun fv ->
            match fv with
            | MutableScalar(_, vn, defaultExpr) -> LetOrUseExpr(Value(vn, E defaultExpr).toMutable ())
            | MutableList(_, vn) -> LetOrUseExpr(Value(vn, E "[]").toMutable ())
            | MutableMap(_, vn) -> LetOrUseExpr(Value(vn, E "Map.empty").toMutable ()))
        |> Seq.toList

    // Build final record fields
    let recordFields =
        fieldVars
        |> Seq.map (fun fv ->
            match fv with
            | MutableScalar(fn, vn, _) -> RecordFieldExpr(fn, E vn)
            | MutableList(fn, vn) -> RecordFieldExpr(fn, E $"List.rev {vn}")
            | MutableMap(fn, vn) -> RecordFieldExpr(fn, E vn))
        |> Seq.toList

    let bodyExprs =
        [ yield! varDecls
          yield LetOrUseExpr(Value("tag", E "input.ReadTag()").toMutable ())
          yield
              OtherExpr(
                  WhileExpr(
                      E "tag <> 0u",
                      CompExprBodyExpr(
                          [ OtherExpr(
                                MatchExpr(E "Google.Protobuf.WireFormat.GetTagFieldNumber(tag)", clauses |> Seq.toList)
                            )
                            OtherExpr(LongIdentSetExpr("tag", E "input.ReadTag()")) ]
                      )
                  )
              )
          yield OtherExpr(RecordExpr(recordFields)) ]

    Function(
        "decodeFrom",
        ParenPat(ParameterPat("input", LongIdent("Google.Protobuf.CodedInputStream"))),
        CompExprBodyExpr(bodyExprs),
        LongIdent(msg.Name)
    )

/// Generate thin decode wrapper that creates a CodedInputStream and delegates to decodeFrom.
let generateDecodeAST (msg: DescriptorProto) =
    Function(
        "decode",
        ParenPat(ParameterPat("data", LongIdent("byte array"))),
        CompExprBodyExpr(
            [ LetOrUseExpr(Use("input", E "new Google.Protobuf.CodedInputStream(data)"))
              OtherExpr(E "decodeFrom input") ]
        ),
        LongIdent(msg.Name)
    )

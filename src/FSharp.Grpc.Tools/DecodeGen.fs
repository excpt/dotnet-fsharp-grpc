module internal FSharp.Grpc.Tools.DecodeGen

open Google.Protobuf.Reflection
open Fabulous.AST
open Fantomas.Core.SyntaxOak
open type Fabulous.AST.Ast
open FSharp.Grpc.Tools.TypeMapping
open FSharp.Grpc.Tools.FieldClass
open FSharp.Grpc.Tools.WireFormat

/// Default literal for a scalar/enum field in decode (map key/value defaults).
let scalarDefault (currentMsgFqn: string) (field: FieldDescriptorProto) =
    match field.Type with
    | FieldDescriptorProto.Types.Type.String -> "\"\""
    | FieldDescriptorProto.Types.Type.Bool -> "false"
    | FieldDescriptorProto.Types.Type.Message -> $"{qualifiedRef currentMsgFqn field.TypeName}.empty"
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
let fieldDefault (currentMsgFqn: string) (field: FieldDescriptorProto) =
    match field.Type with
    | FieldDescriptorProto.Types.Type.Double -> "0.0"
    | FieldDescriptorProto.Types.Type.Float -> "0.0f"
    | FieldDescriptorProto.Types.Type.Bytes -> "Array.empty"
    | FieldDescriptorProto.Types.Type.Enum -> "LanguagePrimitives.EnumOfValue 0"
    | _ -> scalarDefault currentMsgFqn field

/// Generate decode match clauses for all cases of a oneof group.
let decodeOneofClauses (currentMsgFqn: string) (msg: DescriptorProto) (idx: int) =
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

        if c.Type = FieldDescriptorProto.Types.Type.Message then
            let msgTypeName = qualifiedRef currentMsgFqn c.TypeName

            let buildExpr =
                match Map.tryFind c.TypeName wellKnownTypeMap with
                | Some wktType ->
                    CompExprBodyExpr(
                        [ LetOrUseExpr(Use("subInput", E "input.ReadBytes().CreateCodedInput()"))
                          LetOrUseExpr(Value("msg", E $"{wktType}()"))
                          OtherExpr(E "msg.MergeFrom(subInput)")
                          OtherExpr(LongIdentSetExpr($"_{duFieldName}", E $"Some({duName}.{caseName}(msg))")) ]
                    )
                | None ->
                    match Map.tryFind c.TypeName wrapperTypeMap with
                    | Some _ ->
                        let wrapperClassName = simpleTypeName c.TypeName

                        CompExprBodyExpr(
                            [ LetOrUseExpr(Use("subInput", E "input.ReadBytes().CreateCodedInput()"))
                              LetOrUseExpr(Value("wrapper", E $"Google.Protobuf.WellKnownTypes.{wrapperClassName}()"))
                              OtherExpr(E "wrapper.MergeFrom(subInput)")
                              OtherExpr(
                                  LongIdentSetExpr($"_{duFieldName}", E $"Some({duName}.{caseName}(wrapper.Value))")
                              ) ]
                        )
                    | None ->
                        CompExprBodyExpr(
                            [ LetOrUseExpr(Use("subInput", E "input.ReadBytes().CreateCodedInput()"))
                              OtherExpr(
                                  LongIdentSetExpr(
                                      $"_{duFieldName}",
                                      E $"Some({duName}.{caseName}({msgTypeName}.decodeFrom subInput))"
                                  )
                              ) ]
                        )

            MatchClauseExpr(ConstantPat(Constant($"{c.Number}")), buildExpr)
        else
            let readCall = readExpr "input" c

            MatchClauseExpr(
                ConstantPat(Constant($"{c.Number}")),
                LongIdentSetExpr($"_{duFieldName}", E $"Some({duName}.{caseName}({readCall}))")
            ))

/// Generate decode match clause for a map field.
let decodeMapClause
    (currentMsgFqn: string)
    (msg: DescriptorProto)
    (f: FieldDescriptorProto)
    (fname: string)
    (fieldNum: int)
    =
    let entry = (tryGetMapEntry msg f).Value
    let keyField = entry.Field.[0]
    let valueField = entry.Field.[1]
    let keyDefault = scalarDefault currentMsgFqn keyField
    let valueDefault = scalarDefault currentMsgFqn valueField

    let valueClause =
        if valueField.Type = FieldDescriptorProto.Types.Type.Message then
            let msgTypeName = qualifiedRef currentMsgFqn valueField.TypeName

            MatchClauseExpr(
                ConstantPat(Constant("2")),
                CompExprBodyExpr(
                    [ LetOrUseExpr(Use("subInput", E "entryInput.ReadBytes().CreateCodedInput()"))
                      OtherExpr(LongIdentSetExpr("mapValue", E $"{msgTypeName}.decodeFrom subInput")) ]
                )
            )
        else
            MatchClauseExpr(
                ConstantPat(Constant("2")),
                LongIdentSetExpr("mapValue", E(readExpr "entryInput" valueField))
            )

    let entryClauses =
        [ MatchClauseExpr(ConstantPat(Constant("1")), LongIdentSetExpr("key", E(readExpr "entryInput" keyField)))
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
let decodeRepeatedClause (currentMsgFqn: string) (f: FieldDescriptorProto) (fname: string) (fieldNum: int) =
    if f.Type = FieldDescriptorProto.Types.Type.Message then
        let msgTypeName = qualifiedRef currentMsgFqn f.TypeName

        MatchClauseExpr(
            ConstantPat(Constant($"{fieldNum}")),
            CompExprBodyExpr(
                [ LetOrUseExpr(Use("subInput", E "input.ReadBytes().CreateCodedInput()"))
                  OtherExpr(E $"_{fname} <- {msgTypeName}.decodeFrom subInput :: _{fname}") ]
            )
        )
    elif
        f.Type = FieldDescriptorProto.Types.Type.String
        || f.Type = FieldDescriptorProto.Types.Type.Bytes
    then
        let readCall = readExpr "input" f

        MatchClauseExpr(ConstantPat(Constant($"{fieldNum}")), E $"_{fname} <- {readCall} :: _{fname}")
    else
        let unpackedRead = readExpr "input" f
        let packedRead = readExpr "packedInput" f

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
                                    WhileExpr(E "not packedInput.IsAtEnd", E $"_{fname} <- {packedRead} :: _{fname}")
                                ) ]
                          ),
                          E $"_{fname} <- {unpackedRead} :: _{fname}"
                      )
                  ) ]
            )
        )

/// Generate decode match clause for a (non-optional, non-repeated) message field.
let decodeMessageClause (currentMsgFqn: string) (f: FieldDescriptorProto) (fname: string) (fieldNum: int) =
    let msgTypeName = qualifiedRef currentMsgFqn f.TypeName

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

/// Generate decode match clause for a proto3 optional field.
let decodeOptionalClause (currentMsgFqn: string) (f: FieldDescriptorProto) (fname: string) (fieldNum: int) =
    if f.Type = FieldDescriptorProto.Types.Type.Message then
        decodeMessageClause currentMsgFqn f fname fieldNum
    else
        let readCall = readExpr "input" f

        MatchClauseExpr(ConstantPat(Constant($"{fieldNum}")), LongIdentSetExpr($"_{fname}", E $"Some({readCall})"))

/// Generate decode match clause for a plain scalar field.
let decodeScalarClause (f: FieldDescriptorProto) (fname: string) (fieldNum: int) =
    MatchClauseExpr(ConstantPat(Constant($"{fieldNum}")), LongIdentSetExpr($"_{fname}", E(readExpr "input" f)))

/// Generate decodeFrom function as AST widget (reads from existing CodedInputStream).
let generateDecodeFromAST (currentMsgFqn: string) (msg: DescriptorProto) =
    let fieldVars = ResizeArray<FieldVar>()
    let clauses = ResizeArray<WidgetBuilder<MatchClauseNode>>()

    for kind in classifyFields msg do
        match kind with
        | OneofLeader(idx, decl, _) ->
            let duFieldName = toPascalCase decl.Name
            fieldVars.Add(MutableScalar(duFieldName, $"_{duFieldName}", "None"))
            clauses.AddRange(decodeOneofClauses currentMsgFqn msg idx)
        | MapEntry(f, _) ->
            let fname = toPascalCase f.Name
            fieldVars.Add(MutableMap(fname, $"_{fname}"))
            clauses.Add(decodeMapClause currentMsgFqn msg f fname f.Number)
        | Repeated f ->
            let fname = toPascalCase f.Name
            fieldVars.Add(MutableList(fname, $"_{fname}"))
            clauses.Add(decodeRepeatedClause currentMsgFqn f fname f.Number)
        | Optional f ->
            let fname = toPascalCase f.Name
            fieldVars.Add(MutableScalar(fname, $"_{fname}", "None"))
            clauses.Add(decodeOptionalClause currentMsgFqn f fname f.Number)
        | Message f ->
            let fname = toPascalCase f.Name
            fieldVars.Add(MutableScalar(fname, $"_{fname}", "None"))
            clauses.Add(decodeMessageClause currentMsgFqn f fname f.Number)
        | Scalar f ->
            let fname = toPascalCase f.Name
            fieldVars.Add(MutableScalar(fname, $"_{fname}", fieldDefault currentMsgFqn f))
            clauses.Add(decodeScalarClause f fname f.Number)

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

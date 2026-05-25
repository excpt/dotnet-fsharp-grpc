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

/// Read a length-delimited sub-region's length prefix and bind `endVar` to the absolute
/// stream position of its end (`length + position-after-prefix`), then run `body`. Reads
/// the sub-region in place from `input` — no intermediate `byte[]` copy and no second
/// `CodedInputStream` — replacing the old `input.ReadBytes().CreateCodedInput()` pattern.
/// The `int64 (ReadLength()) + Position` order matters: `ReadLength` advances past the
/// prefix before `Position` is read, so the sum is the body's end, not its start.
let private withEndPos (endVar: string) body =
    [ yield LetOrUseExpr(Value(endVar, E "int64 (input.ReadLength()) + input.Position"))
      yield! body ]

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
            let buildExpr =
                match classifyMessageRef currentMsgFqn c.TypeName with
                | WellKnown wktType ->
                    CompExprBodyExpr(
                        [ LetOrUseExpr(Value("msg", E $"{wktType}()"))
                          OtherExpr(E "input.ReadMessage(msg)")
                          OtherExpr(LongIdentSetExpr($"_{duFieldName}", E $"Some({duName}.{caseName}(msg))")) ]
                    )
                | Wrapper(_, wrapperClassName) ->
                    CompExprBodyExpr(
                        [ LetOrUseExpr(Value("wrapper", E $"Google.Protobuf.WellKnownTypes.{wrapperClassName}()"))
                          OtherExpr(E "input.ReadMessage(wrapper)")
                          OtherExpr(LongIdentSetExpr($"_{duFieldName}", E $"Some({duName}.{caseName}(wrapper.Value))")) ]
                    )
                | PlainMessage msgTypeName ->
                    CompExprBodyExpr(
                        withEndPos
                            "endPos"
                            [ LetOrUseExpr(Value("msg", E $"{msgTypeName}.decodeFrom input endPos"))
                              OtherExpr(LongIdentSetExpr($"_{duFieldName}", E $"Some({duName}.{caseName}(msg))")) ]
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
                    withEndPos
                        "vEndPos"
                        [ LetOrUseExpr(Value("v", E $"{msgTypeName}.decodeFrom input vEndPos"))
                          OtherExpr(LongIdentSetExpr("mapValue", E "v")) ]
                )
            )
        else
            MatchClauseExpr(ConstantPat(Constant("2")), LongIdentSetExpr("mapValue", E(readExpr "input" valueField)))

    let entryClauses =
        [ MatchClauseExpr(ConstantPat(Constant("1")), LongIdentSetExpr("key", E(readExpr "input" keyField)))
          valueClause
          MatchClauseExpr(ConstantPat(Constant("_")), E "input.SkipLastField()") ]

    MatchClauseExpr(
        ConstantPat(Constant($"{fieldNum}")),
        CompExprBodyExpr(
            withEndPos
                "endPos"
                [ LetOrUseExpr(Value("key", E keyDefault).toMutable ())
                  LetOrUseExpr(Value("mapValue", E valueDefault).toMutable ())
                  OtherExpr(
                      WhileExpr(
                          E "input.Position < endPos",
                          MatchExpr(E "Google.Protobuf.WireFormat.GetTagFieldNumber(input.ReadTag())", entryClauses)
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
                withEndPos
                    "endPos"
                    [ LetOrUseExpr(Value("msg", E $"{msgTypeName}.decodeFrom input endPos"))
                      OtherExpr(E $"_{fname} <- msg :: _{fname}") ]
            )
        )
    elif
        f.Type = FieldDescriptorProto.Types.Type.String
        || f.Type = FieldDescriptorProto.Types.Type.Bytes
    then
        let readCall = readExpr "input" f

        MatchClauseExpr(ConstantPat(Constant($"{fieldNum}")), E $"_{fname} <- {readCall} :: _{fname}")
    else
        let read = readExpr "input" f

        MatchClauseExpr(
            ConstantPat(Constant($"{fieldNum}")),
            CompExprBodyExpr(
                [ LetOrUseExpr(Value("wt", E "Google.Protobuf.WireFormat.GetTagWireType(tag)"))
                  OtherExpr(
                      IfThenElseExpr(
                          E "wt = Google.Protobuf.WireFormat.WireType.LengthDelimited",
                          CompExprBodyExpr(
                              withEndPos
                                  "endPos"
                                  [ OtherExpr(
                                        WhileExpr(E "input.Position < endPos", E $"_{fname} <- {read} :: _{fname}")
                                    ) ]
                          ),
                          E $"_{fname} <- {read} :: _{fname}"
                      )
                  ) ]
            )
        )

/// Generate decode match clause for a (non-optional, non-repeated) message field.
let decodeMessageClause (currentMsgFqn: string) (f: FieldDescriptorProto) (fname: string) (fieldNum: int) =
    match classifyMessageRef currentMsgFqn f.TypeName with
    | WellKnown wktType ->
        MatchClauseExpr(
            ConstantPat(Constant($"{fieldNum}")),
            CompExprBodyExpr(
                [ LetOrUseExpr(Value("msg", E $"{wktType}()"))
                  OtherExpr(E "input.ReadMessage(msg)")
                  OtherExpr(LongIdentSetExpr($"_{fname}", E "Some(msg)")) ]
            )
        )
    | Wrapper(_, wrapperClassName) ->
        MatchClauseExpr(
            ConstantPat(Constant($"{fieldNum}")),
            CompExprBodyExpr(
                [ LetOrUseExpr(Value("wrapper", E $"Google.Protobuf.WellKnownTypes.{wrapperClassName}()"))
                  OtherExpr(E "input.ReadMessage(wrapper)")
                  OtherExpr(LongIdentSetExpr($"_{fname}", E "Some(wrapper.Value)")) ]
            )
        )
    | PlainMessage msgTypeName ->
        MatchClauseExpr(
            ConstantPat(Constant($"{fieldNum}")),
            CompExprBodyExpr(
                withEndPos
                    "endPos"
                    [ LetOrUseExpr(Value("msg", E $"{msgTypeName}.decodeFrom input endPos"))
                      OtherExpr(LongIdentSetExpr($"_{fname}", E "Some(msg)")) ]
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

    // Read fields until `input.Position` reaches `endPos` (the sub-message's byte
    // boundary). Bounding by position rather than `IsAtEnd`/a `0u` tag lets a nested
    // message be parsed in place from the parent stream — no buffer copy, no second
    // `CodedInputStream`. The top-level `decode` passes the full buffer length.
    let bodyExprs =
        [ yield! varDecls
          yield
              OtherExpr(
                  WhileExpr(
                      E "input.Position < endPos",
                      CompExprBodyExpr(
                          [ LetOrUseExpr(Value("tag", E "input.ReadTag()"))
                            OtherExpr(
                                MatchExpr(E "Google.Protobuf.WireFormat.GetTagFieldNumber(tag)", clauses |> Seq.toList)
                            ) ]
                      )
                  )
              )
          yield OtherExpr(RecordExpr(recordFields)) ]

    Function(
        "decodeFrom",
        [ ParenPat(ParameterPat("input", LongIdent("Google.Protobuf.CodedInputStream")))
          ParenPat(ParameterPat("endPos", LongIdent("int64"))) ],
        CompExprBodyExpr(bodyExprs),
        LongIdent(msg.Name)
    )

/// Generate thin decode wrapper that creates a CodedInputStream and delegates to decodeFrom,
/// bounding the read by the full buffer length.
let generateDecodeAST (msg: DescriptorProto) =
    Function(
        "decode",
        ParenPat(ParameterPat("data", LongIdent("byte array"))),
        CompExprBodyExpr(
            [ LetOrUseExpr(Use("input", E "new Google.Protobuf.CodedInputStream(data)"))
              OtherExpr(E "decodeFrom input (int64 data.Length)") ]
        ),
        LongIdent(msg.Name)
    )

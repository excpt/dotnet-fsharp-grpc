module internal FSharp.Grpc.Tools.JsonDecodeGen

open Google.Protobuf.Reflection
open Fabulous.AST
open Fantomas.Core.SyntaxOak
open type Fabulous.AST.Ast
open FSharp.Grpc.Tools.TypeMapping
open FSharp.Grpc.Tools.FieldClass
open FSharp.Grpc.Tools.WireFormat
open FSharp.Grpc.Tools.DecodeGen

/// JSON read expression for a scalar value from a JsonElement.
let private jsonReadExpr (currentMsgFqn: string) (elemExpr: string) (f: FieldDescriptorProto) =
    match f.Type with
    | FieldDescriptorProto.Types.Type.String -> $"{elemExpr}.GetString()"
    | FieldDescriptorProto.Types.Type.Bool -> $"{elemExpr}.GetBoolean()"
    | FieldDescriptorProto.Types.Type.Int32
    | FieldDescriptorProto.Types.Type.Sint32
    | FieldDescriptorProto.Types.Type.Sfixed32 -> $"{elemExpr}.GetInt32()"
    | FieldDescriptorProto.Types.Type.Uint32
    | FieldDescriptorProto.Types.Type.Fixed32 -> $"{elemExpr}.GetUInt32()"
    | FieldDescriptorProto.Types.Type.Int64
    | FieldDescriptorProto.Types.Type.Sint64
    | FieldDescriptorProto.Types.Type.Sfixed64 ->
        $"if {elemExpr}.ValueKind = System.Text.Json.JsonValueKind.String then int64 ({elemExpr}.GetString()) else {elemExpr}.GetInt64()"
    | FieldDescriptorProto.Types.Type.Uint64
    | FieldDescriptorProto.Types.Type.Fixed64 ->
        $"if {elemExpr}.ValueKind = System.Text.Json.JsonValueKind.String then uint64 ({elemExpr}.GetString()) else {elemExpr}.GetUInt64()"
    | FieldDescriptorProto.Types.Type.Double ->
        $"if {elemExpr}.ValueKind = System.Text.Json.JsonValueKind.String then (match {elemExpr}.GetString() with | \"NaN\" -> System.Double.NaN | \"Infinity\" -> System.Double.PositiveInfinity | \"-Infinity\" -> System.Double.NegativeInfinity | s -> float s) else {elemExpr}.GetDouble()"
    | FieldDescriptorProto.Types.Type.Float ->
        $"if {elemExpr}.ValueKind = System.Text.Json.JsonValueKind.String then (match {elemExpr}.GetString() with | \"NaN\" -> System.Single.NaN | \"Infinity\" -> System.Single.PositiveInfinity | \"-Infinity\" -> System.Single.NegativeInfinity | s -> float32 s) else float32 ({elemExpr}.GetDouble())"
    | FieldDescriptorProto.Types.Type.Bytes -> $"System.Convert.FromBase64String({elemExpr}.GetString())"
    | FieldDescriptorProto.Types.Type.Enum ->
        $"{qualifiedRef currentMsgFqn f.TypeName}.fromJsonName(if {elemExpr}.ValueKind = System.Text.Json.JsonValueKind.Number then string ({elemExpr}.GetInt32()) else {elemExpr}.GetString())"
    | _ -> $"{elemExpr}.GetInt32()"

/// Parse expression for map keys from string.
let private jsonParseKeyExpr (keyField: FieldDescriptorProto) (keyExpr: string) =
    match keyField.Type with
    | FieldDescriptorProto.Types.Type.String -> keyExpr
    | FieldDescriptorProto.Types.Type.Bool -> $"System.Boolean.Parse({keyExpr})"
    | FieldDescriptorProto.Types.Type.Int32
    | FieldDescriptorProto.Types.Type.Sint32
    | FieldDescriptorProto.Types.Type.Sfixed32 -> $"int {keyExpr}"
    | FieldDescriptorProto.Types.Type.Uint32
    | FieldDescriptorProto.Types.Type.Fixed32 -> $"uint32 {keyExpr}"
    | FieldDescriptorProto.Types.Type.Int64
    | FieldDescriptorProto.Types.Type.Sint64
    | FieldDescriptorProto.Types.Type.Sfixed64 -> $"int64 {keyExpr}"
    | FieldDescriptorProto.Types.Type.Uint64
    | FieldDescriptorProto.Types.Type.Fixed64 -> $"uint64 {keyExpr}"
    | _ -> keyExpr

/// Generate decodeJsonElement function as AST widget.
let generateDecodeJsonElementAST (currentMsgFqn: string) (msg: DescriptorProto) =
    let fieldVars = ResizeArray<FieldVar>()
    let clauses = ResizeArray<WidgetBuilder<MatchClauseNode>>()

    for kind in classifyFields msg do
        match kind with
        | OneofLeader(_, decl, oneofCases) ->
            let duName = oneofTypeName msg.Name decl.Name
            let duFieldName = toPascalCase decl.Name
            fieldVars.Add(MutableScalar(duFieldName, $"_{duFieldName}", "None"))

            for c in oneofCases do
                let caseName = toPascalCase c.Name
                let caseJsonName = c.JsonName

                let patterns =
                    if caseJsonName <> c.Name then
                        [ caseJsonName; c.Name ]
                    else
                        [ caseJsonName ]

                for pat in patterns do
                    if c.Type = FieldDescriptorProto.Types.Type.Message then
                        let msgTypeName = qualifiedRef currentMsgFqn c.TypeName

                        match Map.tryFind c.TypeName wellKnownTypeMap with
                        | Some wktType ->
                            clauses.Add(
                                MatchClauseExpr(
                                    ConstantPat(Constant($"\"{pat}\"")),
                                    LongIdentSetExpr(
                                        $"_{duFieldName}",
                                        E
                                            $"Some({duName}.{caseName}(Google.Protobuf.JsonParser.Default.Parse<{wktType}>(prop.Value.GetRawText())))"
                                    )
                                )
                            )
                        | None ->
                            match Map.tryFind c.TypeName wrapperTypeMap with
                            | Some wrapperScalar ->
                                let readExpr =
                                    match wrapperScalar with
                                    | "string" -> "prop.Value.GetString()"
                                    | "bool" -> "prop.Value.GetBoolean()"
                                    | "float" -> "prop.Value.GetDouble()"
                                    | "float32" -> "float32 (prop.Value.GetDouble())"
                                    | "int64" ->
                                        "if prop.Value.ValueKind = System.Text.Json.JsonValueKind.String then int64 (prop.Value.GetString()) else prop.Value.GetInt64()"
                                    | "uint64" ->
                                        "if prop.Value.ValueKind = System.Text.Json.JsonValueKind.String then uint64 (prop.Value.GetString()) else prop.Value.GetUInt64()"
                                    | "int" -> "prop.Value.GetInt32()"
                                    | "uint32" -> "prop.Value.GetUInt32()"
                                    | "byte array" -> "System.Convert.FromBase64String(prop.Value.GetString())"
                                    | _ -> failwith $"unhandled wrapper scalar type: {wrapperScalar}"

                                clauses.Add(
                                    MatchClauseExpr(
                                        ConstantPat(Constant($"\"{pat}\"")),
                                        LongIdentSetExpr($"_{duFieldName}", E $"Some({duName}.{caseName}({readExpr}))")
                                    )
                                )
                            | None ->
                                clauses.Add(
                                    MatchClauseExpr(
                                        ConstantPat(Constant($"\"{pat}\"")),
                                        LongIdentSetExpr(
                                            $"_{duFieldName}",
                                            E $"Some({duName}.{caseName}({msgTypeName}.decodeJsonElement prop.Value))"
                                        )
                                    )
                                )
                    else
                        let readExpr = jsonReadExpr currentMsgFqn "prop.Value" c

                        clauses.Add(
                            MatchClauseExpr(
                                ConstantPat(Constant($"\"{pat}\"")),
                                LongIdentSetExpr($"_{duFieldName}", E $"Some({duName}.{caseName}({readExpr}))")
                            )
                        )
        | MapEntry(f, entry) ->
            let fname = toPascalCase f.Name
            let jsonName = f.JsonName
            let keyField = entry.Field.[0]
            let valueField = entry.Field.[1]
            fieldVars.Add(MutableMap(fname, $"_{fname}"))

            let patterns =
                if jsonName <> f.Name then
                    [ jsonName; f.Name ]
                else
                    [ jsonName ]

            let keyParseExpr = jsonParseKeyExpr keyField "entry.Name"

            for pat in patterns do
                if valueField.Type = FieldDescriptorProto.Types.Type.Message then
                    let msgTypeName = qualifiedRef currentMsgFqn valueField.TypeName

                    match Map.tryFind valueField.TypeName wellKnownTypeMap with
                    | Some wktType ->
                        let valueExpr =
                            $"Google.Protobuf.JsonParser.Default.Parse<{wktType}>(entry.Value.GetRawText())"

                        clauses.Add(
                            MatchClauseExpr(
                                ConstantPat(Constant($"\"{pat}\"")),
                                ForEachDoExpr(
                                    "entry",
                                    E "prop.Value.EnumerateObject()",
                                    E $"_{fname} <- Map.add ({keyParseExpr}) ({valueExpr}) _{fname}"
                                )
                            )
                        )
                    | None ->
                        clauses.Add(
                            MatchClauseExpr(
                                ConstantPat(Constant($"\"{pat}\"")),
                                ForEachDoExpr(
                                    "entry",
                                    E "prop.Value.EnumerateObject()",
                                    E
                                        $"_{fname} <- Map.add ({keyParseExpr}) ({msgTypeName}.decodeJsonElement entry.Value) _{fname}"
                                )
                            )
                        )
                else
                    let readExpr = jsonReadExpr currentMsgFqn "entry.Value" valueField

                    clauses.Add(
                        MatchClauseExpr(
                            ConstantPat(Constant($"\"{pat}\"")),
                            ForEachDoExpr(
                                "entry",
                                E "prop.Value.EnumerateObject()",
                                E $"_{fname} <- Map.add ({keyParseExpr}) ({readExpr}) _{fname}"
                            )
                        )
                    )
        | Repeated f ->
            let fname = toPascalCase f.Name
            let jsonName = f.JsonName
            fieldVars.Add(MutableList(fname, $"_{fname}"))

            let patterns =
                if jsonName <> f.Name then
                    [ jsonName; f.Name ]
                else
                    [ jsonName ]

            for pat in patterns do
                if f.Type = FieldDescriptorProto.Types.Type.Message then
                    let msgTypeName = qualifiedRef currentMsgFqn f.TypeName

                    match Map.tryFind f.TypeName wellKnownTypeMap with
                    | Some wktType ->
                        clauses.Add(
                            MatchClauseExpr(
                                ConstantPat(Constant($"\"{pat}\"")),
                                ForEachDoExpr(
                                    "item",
                                    E "prop.Value.EnumerateArray()",
                                    E
                                        $"_{fname} <- Google.Protobuf.JsonParser.Default.Parse<{wktType}>(item.GetRawText()) :: _{fname}"
                                )
                            )
                        )
                    | None ->
                        clauses.Add(
                            MatchClauseExpr(
                                ConstantPat(Constant($"\"{pat}\"")),
                                ForEachDoExpr(
                                    "item",
                                    E "prop.Value.EnumerateArray()",
                                    E $"_{fname} <- {msgTypeName}.decodeJsonElement item :: _{fname}"
                                )
                            )
                        )
                else
                    let readExpr = jsonReadExpr currentMsgFqn "item" f

                    clauses.Add(
                        MatchClauseExpr(
                            ConstantPat(Constant($"\"{pat}\"")),
                            ForEachDoExpr(
                                "item",
                                E "prop.Value.EnumerateArray()",
                                E $"_{fname} <- {readExpr} :: _{fname}"
                            )
                        )
                    )
        | Optional f ->
            let fname = toPascalCase f.Name
            let jsonName = f.JsonName
            fieldVars.Add(MutableScalar(fname, $"_{fname}", "None"))

            let patterns =
                if jsonName <> f.Name then
                    [ jsonName; f.Name ]
                else
                    [ jsonName ]

            for pat in patterns do
                if f.Type = FieldDescriptorProto.Types.Type.Message then
                    let msgTypeName = qualifiedRef currentMsgFqn f.TypeName

                    match Map.tryFind f.TypeName wellKnownTypeMap with
                    | Some wktType ->
                        clauses.Add(
                            MatchClauseExpr(
                                ConstantPat(Constant($"\"{pat}\"")),
                                LongIdentSetExpr(
                                    $"_{fname}",
                                    E
                                        $"Some(Google.Protobuf.JsonParser.Default.Parse<{wktType}>(prop.Value.GetRawText()))"
                                )
                            )
                        )
                    | None ->
                        clauses.Add(
                            MatchClauseExpr(
                                ConstantPat(Constant($"\"{pat}\"")),
                                LongIdentSetExpr($"_{fname}", E $"Some({msgTypeName}.decodeJsonElement prop.Value)")
                            )
                        )
                else
                    let readExpr = jsonReadExpr currentMsgFqn "prop.Value" f

                    clauses.Add(
                        MatchClauseExpr(
                            ConstantPat(Constant($"\"{pat}\"")),
                            LongIdentSetExpr($"_{fname}", E $"Some({readExpr})")
                        )
                    )
        | Message f ->
            let fname = toPascalCase f.Name
            let jsonName = f.JsonName
            fieldVars.Add(MutableScalar(fname, $"_{fname}", "None"))

            let patterns =
                if jsonName <> f.Name then
                    [ jsonName; f.Name ]
                else
                    [ jsonName ]

            let msgTypeName = qualifiedRef currentMsgFqn f.TypeName

            for pat in patterns do
                match Map.tryFind f.TypeName wellKnownTypeMap with
                | Some wktType ->
                    clauses.Add(
                        MatchClauseExpr(
                            ConstantPat(Constant($"\"{pat}\"")),
                            LongIdentSetExpr(
                                $"_{fname}",
                                E $"Some(Google.Protobuf.JsonParser.Default.Parse<{wktType}>(prop.Value.GetRawText()))"
                            )
                        )
                    )
                | None ->
                    match Map.tryFind f.TypeName wrapperTypeMap with
                    | Some wrapperScalar ->
                        let readExpr =
                            match wrapperScalar with
                            | "string" -> "prop.Value.GetString()"
                            | "bool" -> "prop.Value.GetBoolean()"
                            | "float" -> "prop.Value.GetDouble()"
                            | "float32" -> "float32 (prop.Value.GetDouble())"
                            | "int64" ->
                                "if prop.Value.ValueKind = System.Text.Json.JsonValueKind.String then int64 (prop.Value.GetString()) else prop.Value.GetInt64()"
                            | "uint64" ->
                                "if prop.Value.ValueKind = System.Text.Json.JsonValueKind.String then uint64 (prop.Value.GetString()) else prop.Value.GetUInt64()"
                            | "int" -> "prop.Value.GetInt32()"
                            | "uint32" -> "prop.Value.GetUInt32()"
                            | "byte array" -> "System.Convert.FromBase64String(prop.Value.GetString())"
                            | _ -> failwith $"unhandled wrapper scalar type: {wrapperScalar}"

                        clauses.Add(
                            MatchClauseExpr(
                                ConstantPat(Constant($"\"{pat}\"")),
                                LongIdentSetExpr($"_{fname}", E $"Some({readExpr})")
                            )
                        )
                    | None ->
                        clauses.Add(
                            MatchClauseExpr(
                                ConstantPat(Constant($"\"{pat}\"")),
                                LongIdentSetExpr($"_{fname}", E $"Some({msgTypeName}.decodeJsonElement prop.Value)")
                            )
                        )
        | Scalar f ->
            let fname = toPascalCase f.Name
            let jsonName = f.JsonName
            fieldVars.Add(MutableScalar(fname, $"_{fname}", fieldDefault currentMsgFqn f))

            let patterns =
                if jsonName <> f.Name then
                    [ jsonName; f.Name ]
                else
                    [ jsonName ]

            let readExpr = jsonReadExpr currentMsgFqn "prop.Value" f

            for pat in patterns do
                clauses.Add(
                    MatchClauseExpr(ConstantPat(Constant($"\"{pat}\"")), LongIdentSetExpr($"_{fname}", E readExpr))
                )

    clauses.Add(MatchClauseExpr(ConstantPat(Constant("_")), E "()"))

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
          yield
              OtherExpr(
                  ForEachDoExpr("prop", E "element.EnumerateObject()", MatchExpr(E "prop.Name", clauses |> Seq.toList))
              )
          yield OtherExpr(RecordExpr(recordFields)) ]

    Function(
        "decodeJsonElement",
        ParenPat(ParameterPat("element", LongIdent("System.Text.Json.JsonElement"))),
        CompExprBodyExpr(bodyExprs),
        LongIdent(msg.Name)
    )

/// Generate decodeJson function as AST widget.
let generateDecodeJsonAST (msg: DescriptorProto) =
    Function(
        "decodeJson",
        ParenPat(ParameterPat("json", LongIdent("string"))),
        CompExprBodyExpr(
            [ LetOrUseExpr(Use("doc", E "System.Text.Json.JsonDocument.Parse(json)"))
              OtherExpr(E "decodeJsonElement doc.RootElement") ]
        ),
        LongIdent(msg.Name)
    )

module internal Grpc.FSharp.Tools.JsonDecodeGen

open Google.Protobuf.Reflection
open Fabulous.AST
open Fantomas.Core.SyntaxOak
open type Fabulous.AST.Ast
open Grpc.FSharp.Tools.TypeMapping
open Grpc.FSharp.Tools.WireFormat
open Grpc.FSharp.Tools.DecodeGen

/// JSON read expression for a scalar value from a JsonElement.
let private jsonReadExpr (elemExpr: string) (f: FieldDescriptorProto) =
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
        $"{simpleTypeName f.TypeName}.fromJsonName(if {elemExpr}.ValueKind = System.Text.Json.JsonValueKind.Number then string ({elemExpr}.GetInt32()) else {elemExpr}.GetString())"
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
let generateDecodeJsonElementAST (msg: DescriptorProto) =
    let mutable emittedOneofs = Set.empty
    let fieldVars = ResizeArray<FieldVar>()
    let clauses = ResizeArray<WidgetBuilder<MatchClauseNode>>()

    for f in msg.Field |> Seq.sortBy (fun f -> f.Number) do
        let fname = toPascalCase f.Name
        let jsonName = f.JsonName

        if f.HasOneofIndex && not f.Proto3Optional then
            let idx = f.OneofIndex

            if not (emittedOneofs |> Set.contains idx) then
                emittedOneofs <- emittedOneofs |> Set.add idx
                let decl = msg.OneofDecl.[idx]
                let duName = oneofTypeName msg.Name decl.Name
                let duFieldName = toPascalCase decl.Name
                fieldVars.Add(MutableScalar(duFieldName, $"_{duFieldName}", "None"))

                let oneofCases =
                    msg.Field
                    |> Seq.filter (fun of' -> of'.HasOneofIndex && not of'.Proto3Optional && of'.OneofIndex = idx)
                    |> Seq.sortBy (fun of' -> of'.Number)
                    |> Seq.toList

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
                            let msgTypeName = simpleTypeName c.TypeName

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
                            let readExpr = jsonReadExpr "prop.Value" c

                            clauses.Add(
                                MatchClauseExpr(
                                    ConstantPat(Constant($"\"{pat}\"")),
                                    LongIdentSetExpr($"_{duFieldName}", E $"Some({duName}.{caseName}({readExpr}))")
                                )
                            )
        elif tryGetMapEntry msg f |> Option.isSome then
            let entry = (tryGetMapEntry msg f).Value
            let keyField = entry.Field.[0]
            let valueField = entry.Field.[1]
            let keyType = resolveElemType keyField
            let valueType = resolveElemType valueField

            fieldVars.Add(DictionaryVar(fname, $"_{fname}", keyType, valueType))

            let patterns =
                if jsonName <> f.Name then
                    [ jsonName; f.Name ]
                else
                    [ jsonName ]

            let keyParseExpr = jsonParseKeyExpr keyField "entry.Name"

            for pat in patterns do
                if valueField.Type = FieldDescriptorProto.Types.Type.Message then
                    let msgTypeName = simpleTypeName valueField.TypeName

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
                                    E $"_{fname}.[{keyParseExpr}] <- {valueExpr}"
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
                                    E $"_{fname}.[{keyParseExpr}] <- {msgTypeName}.decodeJsonElement entry.Value"
                                )
                            )
                        )
                else
                    let readExpr = jsonReadExpr "entry.Value" valueField

                    clauses.Add(
                        MatchClauseExpr(
                            ConstantPat(Constant($"\"{pat}\"")),
                            ForEachDoExpr(
                                "entry",
                                E "prop.Value.EnumerateObject()",
                                E $"_{fname}.[{keyParseExpr}] <- {readExpr}"
                            )
                        )
                    )
        elif f.Label = FieldDescriptorProto.Types.Label.Repeated then
            let elemType = resolveElemType f
            fieldVars.Add(ResizeArrayVar(fname, $"_{fname}", elemType))

            let patterns =
                if jsonName <> f.Name then
                    [ jsonName; f.Name ]
                else
                    [ jsonName ]

            for pat in patterns do
                if f.Type = FieldDescriptorProto.Types.Type.Message then
                    let msgTypeName = simpleTypeName f.TypeName

                    match Map.tryFind f.TypeName wellKnownTypeMap with
                    | Some wktType ->
                        clauses.Add(
                            MatchClauseExpr(
                                ConstantPat(Constant($"\"{pat}\"")),
                                ForEachDoExpr(
                                    "item",
                                    E "prop.Value.EnumerateArray()",
                                    E
                                        $"_{fname}.Add(Google.Protobuf.JsonParser.Default.Parse<{wktType}>(item.GetRawText()))"
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
                                    E $"_{fname}.Add({msgTypeName}.decodeJsonElement item)"
                                )
                            )
                        )
                else
                    let readExpr = jsonReadExpr "item" f

                    clauses.Add(
                        MatchClauseExpr(
                            ConstantPat(Constant($"\"{pat}\"")),
                            ForEachDoExpr("item", E "prop.Value.EnumerateArray()", E $"_{fname}.Add({readExpr})")
                        )
                    )
        elif f.Proto3Optional then
            fieldVars.Add(MutableScalar(fname, $"_{fname}", "None"))

            let patterns =
                if jsonName <> f.Name then
                    [ jsonName; f.Name ]
                else
                    [ jsonName ]

            for pat in patterns do
                if f.Type = FieldDescriptorProto.Types.Type.Message then
                    let msgTypeName = simpleTypeName f.TypeName

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
                    let readExpr = jsonReadExpr "prop.Value" f

                    clauses.Add(
                        MatchClauseExpr(
                            ConstantPat(Constant($"\"{pat}\"")),
                            LongIdentSetExpr($"_{fname}", E $"Some({readExpr})")
                        )
                    )
        elif f.Type = FieldDescriptorProto.Types.Type.Message then
            fieldVars.Add(MutableScalar(fname, $"_{fname}", "None"))

            let patterns =
                if jsonName <> f.Name then
                    [ jsonName; f.Name ]
                else
                    [ jsonName ]

            let msgTypeName = simpleTypeName f.TypeName

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
                            | _ -> "prop.Value.GetInt32()"

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
        else
            fieldVars.Add(MutableScalar(fname, $"_{fname}", fieldDefault f))

            let patterns =
                if jsonName <> f.Name then
                    [ jsonName; f.Name ]
                else
                    [ jsonName ]

            let readExpr = jsonReadExpr "prop.Value" f

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
            | ResizeArrayVar(_, vn, elemType) -> LetOrUseExpr(Value(vn, E $"ResizeArray<{elemType}>()"))
            | DictionaryVar(_, vn, kt, vt) ->
                LetOrUseExpr(Value(vn, E $"System.Collections.Generic.Dictionary<{kt}, {vt}>()")))
        |> Seq.toList

    // Build final record fields
    let recordFields =
        fieldVars
        |> Seq.map (fun fv ->
            match fv with
            | MutableScalar(fn, vn, _) -> RecordFieldExpr(fn, E vn)
            | ResizeArrayVar(fn, vn, _) -> RecordFieldExpr(fn, E $"Seq.toList {vn}")
            | DictionaryVar(fn, vn, _, _) ->
                RecordFieldExpr(fn, E $"{vn} |> Seq.map (fun kvp -> kvp.Key, kvp.Value) |> Map.ofSeq"))
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

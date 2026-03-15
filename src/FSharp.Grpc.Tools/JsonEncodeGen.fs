module internal FSharp.Grpc.Tools.JsonEncodeGen

open Google.Protobuf.Reflection
open Fabulous.AST
open Fantomas.Core.SyntaxOak
open type Fabulous.AST.Ast
open FSharp.Grpc.Tools.TypeMapping
open FSharp.Grpc.Tools.WireFormat

/// JSON write expression for a single scalar value (not wrapped in property name).
let private jsonWriteValueExpr (valueExpr: string) (f: FieldDescriptorProto) =
    match f.Type with
    | FieldDescriptorProto.Types.Type.String -> $"writer.WriteStringValue({valueExpr})"
    | FieldDescriptorProto.Types.Type.Bool -> $"writer.WriteBooleanValue({valueExpr})"
    | FieldDescriptorProto.Types.Type.Int32
    | FieldDescriptorProto.Types.Type.Sint32
    | FieldDescriptorProto.Types.Type.Sfixed32 -> $"writer.WriteNumberValue({valueExpr})"
    | FieldDescriptorProto.Types.Type.Uint32
    | FieldDescriptorProto.Types.Type.Fixed32 -> $"writer.WriteNumberValue({valueExpr})"
    | FieldDescriptorProto.Types.Type.Int64
    | FieldDescriptorProto.Types.Type.Sint64
    | FieldDescriptorProto.Types.Type.Sfixed64 -> $"writer.WriteStringValue(string {valueExpr})"
    | FieldDescriptorProto.Types.Type.Uint64
    | FieldDescriptorProto.Types.Type.Fixed64 -> $"writer.WriteStringValue(string {valueExpr})"
    | FieldDescriptorProto.Types.Type.Double ->
        $"if System.Double.IsNaN({valueExpr}) then writer.WriteStringValue(\"NaN\") elif System.Double.IsPositiveInfinity({valueExpr}) then writer.WriteStringValue(\"Infinity\") elif System.Double.IsNegativeInfinity({valueExpr}) then writer.WriteStringValue(\"-Infinity\") else writer.WriteNumberValue({valueExpr})"
    | FieldDescriptorProto.Types.Type.Float ->
        $"if System.Single.IsNaN({valueExpr}) then writer.WriteStringValue(\"NaN\") elif System.Single.IsPositiveInfinity({valueExpr}) then writer.WriteStringValue(\"Infinity\") elif System.Single.IsNegativeInfinity({valueExpr}) then writer.WriteStringValue(\"-Infinity\") else writer.WriteNumberValue(float {valueExpr})"
    | FieldDescriptorProto.Types.Type.Bytes -> $"writer.WriteStringValue(System.Convert.ToBase64String({valueExpr}))"
    | FieldDescriptorProto.Types.Type.Enum ->
        $"writer.WriteStringValue({simpleTypeName f.TypeName}.toJsonName {valueExpr})"
    | _ -> $"writer.WriteNumberValue({valueExpr})"

/// JSON write expression with property name for a scalar field.
let private jsonWritePropertyExpr (jsonName: string) (valueExpr: string) (f: FieldDescriptorProto) =
    match f.Type with
    | FieldDescriptorProto.Types.Type.String -> $"writer.WriteString(\"{jsonName}\", {valueExpr})"
    | FieldDescriptorProto.Types.Type.Bool -> $"writer.WriteBoolean(\"{jsonName}\", {valueExpr})"
    | FieldDescriptorProto.Types.Type.Int32
    | FieldDescriptorProto.Types.Type.Sint32
    | FieldDescriptorProto.Types.Type.Sfixed32 -> $"writer.WriteNumber(\"{jsonName}\", {valueExpr})"
    | FieldDescriptorProto.Types.Type.Uint32
    | FieldDescriptorProto.Types.Type.Fixed32 -> $"writer.WriteNumber(\"{jsonName}\", {valueExpr})"
    | FieldDescriptorProto.Types.Type.Int64
    | FieldDescriptorProto.Types.Type.Sint64
    | FieldDescriptorProto.Types.Type.Sfixed64 -> $"writer.WriteString(\"{jsonName}\", string {valueExpr})"
    | FieldDescriptorProto.Types.Type.Uint64
    | FieldDescriptorProto.Types.Type.Fixed64 -> $"writer.WriteString(\"{jsonName}\", string {valueExpr})"
    | FieldDescriptorProto.Types.Type.Bytes ->
        $"writer.WriteString(\"{jsonName}\", System.Convert.ToBase64String({valueExpr}))"
    | FieldDescriptorProto.Types.Type.Enum ->
        $"writer.WriteString(\"{jsonName}\", {simpleTypeName f.TypeName}.toJsonName {valueExpr})"
    | _ -> $"writer.WriteNumber(\"{jsonName}\", {valueExpr})"

/// Generate writeJsonTo function as AST widget.
let generateWriteJsonToAST (msg: DescriptorProto) =
    let mutable emittedOneofs = Set.empty
    let stmts = ResizeArray<WidgetBuilder<ComputationExpressionStatement>>()

    stmts.Add(OtherExpr(E "writer.WriteStartObject()"))

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

                let oneofCases =
                    msg.Field
                    |> Seq.filter (fun of' -> of'.HasOneofIndex && not of'.Proto3Optional && of'.OneofIndex = idx)
                    |> Seq.sortBy (fun of' -> of'.Number)
                    |> Seq.toList

                let clauses =
                    oneofCases
                    |> List.map (fun c ->
                        let caseName = toPascalCase c.Name
                        let caseJsonName = c.JsonName

                        if c.Type = FieldDescriptorProto.Types.Type.Message then
                            let msgTypeName = simpleTypeName c.TypeName

                            match Map.tryFind c.TypeName wellKnownTypeMap with
                            | Some _ ->
                                MatchClauseExpr(
                                    ConstantPat(Constant($"{duName}.{caseName} v")),
                                    CompExprBodyExpr(
                                        [ OtherExpr(E $"writer.WritePropertyName(\"{caseJsonName}\")")
                                          OtherExpr(
                                              E
                                                  $"writer.WriteRawValue(Google.Protobuf.JsonFormatter.Default.Format(v))"
                                          ) ]
                                    )
                                )
                            | None ->
                                MatchClauseExpr(
                                    ConstantPat(Constant($"{duName}.{caseName} v")),
                                    CompExprBodyExpr(
                                        [ OtherExpr(E $"writer.WritePropertyName(\"{caseJsonName}\")")
                                          OtherExpr(E $"{msgTypeName}.writeJsonTo writer v") ]
                                    )
                                )
                        else
                            MatchClauseExpr(
                                ConstantPat(Constant($"{duName}.{caseName} v")),
                                E(jsonWritePropertyExpr caseJsonName "v" c)
                            ))

                stmts.Add(
                    OtherExpr(
                        MatchExpr(
                            E $"value.{duFieldName}",
                            [ MatchClauseExpr(
                                  ConstantPat(Constant("Some oneofValue")),
                                  MatchExpr(E "oneofValue", clauses)
                              )
                              MatchClauseExpr(ConstantPat(Constant("None")), E "()") ]
                        )
                    )
                )
        elif tryGetMapEntry msg f |> Option.isSome then
            let entry = (tryGetMapEntry msg f).Value
            let valueField = entry.Field.[1]

            if valueField.Type = FieldDescriptorProto.Types.Type.Message then
                let msgTypeName = simpleTypeName valueField.TypeName

                match Map.tryFind valueField.TypeName wellKnownTypeMap with
                | Some _ ->
                    stmts.Add(
                        OtherExpr(
                            IfThenExpr(
                                E $"not (Map.isEmpty value.{fname})",
                                CompExprBodyExpr(
                                    [ OtherExpr(E $"writer.WriteStartObject(\"{jsonName}\")")
                                      OtherExpr(
                                          ForEachDoExpr(
                                              "kvp",
                                              E $"value.{fname}",
                                              CompExprBodyExpr(
                                                  [ OtherExpr(E "writer.WritePropertyName(string kvp.Key)")
                                                    OtherExpr(
                                                        E
                                                            "writer.WriteRawValue(Google.Protobuf.JsonFormatter.Default.Format(kvp.Value))"
                                                    ) ]
                                              )
                                          )
                                      )
                                      OtherExpr(E "writer.WriteEndObject()") ]
                                )
                            )
                        )
                    )
                | None ->
                    stmts.Add(
                        OtherExpr(
                            IfThenExpr(
                                E $"not (Map.isEmpty value.{fname})",
                                CompExprBodyExpr(
                                    [ OtherExpr(E $"writer.WriteStartObject(\"{jsonName}\")")
                                      OtherExpr(
                                          ForEachDoExpr(
                                              "kvp",
                                              E $"value.{fname}",
                                              CompExprBodyExpr(
                                                  [ OtherExpr(E "writer.WritePropertyName(string kvp.Key)")
                                                    OtherExpr(E $"{msgTypeName}.writeJsonTo writer kvp.Value") ]
                                              )
                                          )
                                      )
                                      OtherExpr(E "writer.WriteEndObject()") ]
                                )
                            )
                        )
                    )
            else
                stmts.Add(
                    OtherExpr(
                        IfThenExpr(
                            E $"not (Map.isEmpty value.{fname})",
                            CompExprBodyExpr(
                                [ OtherExpr(E $"writer.WriteStartObject(\"{jsonName}\")")
                                  OtherExpr(
                                      ForEachDoExpr(
                                          "kvp",
                                          E $"value.{fname}",
                                          CompExprBodyExpr(
                                              [ OtherExpr(E "writer.WritePropertyName(string kvp.Key)")
                                                OtherExpr(E(jsonWriteValueExpr "kvp.Value" valueField)) ]
                                          )
                                      )
                                  )
                                  OtherExpr(E "writer.WriteEndObject()") ]
                            )
                        )
                    )
                )
        elif f.Label = FieldDescriptorProto.Types.Label.Repeated then
            if f.Type = FieldDescriptorProto.Types.Type.Message then
                let msgTypeName = simpleTypeName f.TypeName

                match Map.tryFind f.TypeName wellKnownTypeMap with
                | Some _ ->
                    stmts.Add(
                        OtherExpr(
                            IfThenExpr(
                                E $"not (List.isEmpty value.{fname})",
                                CompExprBodyExpr(
                                    [ OtherExpr(E $"writer.WriteStartArray(\"{jsonName}\")")
                                      OtherExpr(
                                          ForEachDoExpr(
                                              "item",
                                              E $"value.{fname}",
                                              E
                                                  "writer.WriteRawValue(Google.Protobuf.JsonFormatter.Default.Format(item))"
                                          )
                                      )
                                      OtherExpr(E "writer.WriteEndArray()") ]
                                )
                            )
                        )
                    )
                | None ->
                    stmts.Add(
                        OtherExpr(
                            IfThenExpr(
                                E $"not (List.isEmpty value.{fname})",
                                CompExprBodyExpr(
                                    [ OtherExpr(E $"writer.WriteStartArray(\"{jsonName}\")")
                                      OtherExpr(
                                          ForEachDoExpr(
                                              "item",
                                              E $"value.{fname}",
                                              E $"{msgTypeName}.writeJsonTo writer item"
                                          )
                                      )
                                      OtherExpr(E "writer.WriteEndArray()") ]
                                )
                            )
                        )
                    )
            else
                stmts.Add(
                    OtherExpr(
                        IfThenExpr(
                            E $"not (List.isEmpty value.{fname})",
                            CompExprBodyExpr(
                                [ OtherExpr(E $"writer.WriteStartArray(\"{jsonName}\")")
                                  OtherExpr(ForEachDoExpr("item", E $"value.{fname}", E(jsonWriteValueExpr "item" f)))
                                  OtherExpr(E "writer.WriteEndArray()") ]
                            )
                        )
                    )
                )
        elif f.Proto3Optional then
            if f.Type = FieldDescriptorProto.Types.Type.Message then
                let msgTypeName = simpleTypeName f.TypeName

                match Map.tryFind f.TypeName wellKnownTypeMap with
                | Some _ ->
                    stmts.Add(
                        OtherExpr(
                            MatchExpr(
                                E $"value.{fname}",
                                [ MatchClauseExpr(
                                      ConstantPat(Constant("Some v")),
                                      CompExprBodyExpr(
                                          [ OtherExpr(E $"writer.WritePropertyName(\"{jsonName}\")")
                                            OtherExpr(
                                                E
                                                    "writer.WriteRawValue(Google.Protobuf.JsonFormatter.Default.Format(v))"
                                            ) ]
                                      )
                                  )
                                  MatchClauseExpr(ConstantPat(Constant("None")), E "()") ]
                            )
                        )
                    )
                | None ->
                    stmts.Add(
                        OtherExpr(
                            MatchExpr(
                                E $"value.{fname}",
                                [ MatchClauseExpr(
                                      ConstantPat(Constant("Some v")),
                                      CompExprBodyExpr(
                                          [ OtherExpr(E $"writer.WritePropertyName(\"{jsonName}\")")
                                            OtherExpr(E $"{msgTypeName}.writeJsonTo writer v") ]
                                      )
                                  )
                                  MatchClauseExpr(ConstantPat(Constant("None")), E "()") ]
                            )
                        )
                    )
            elif f.Type = FieldDescriptorProto.Types.Type.Double then
                stmts.Add(
                    OtherExpr(
                        MatchExpr(
                            E $"value.{fname}",
                            [ MatchClauseExpr(
                                  ConstantPat(Constant("Some v")),
                                  E
                                      $"if System.Double.IsNaN(v) then writer.WriteString(\"{jsonName}\", \"NaN\") elif System.Double.IsPositiveInfinity(v) then writer.WriteString(\"{jsonName}\", \"Infinity\") elif System.Double.IsNegativeInfinity(v) then writer.WriteString(\"{jsonName}\", \"-Infinity\") else writer.WriteNumber(\"{jsonName}\", v)"
                              )
                              MatchClauseExpr(ConstantPat(Constant("None")), E "()") ]
                        )
                    )
                )
            elif f.Type = FieldDescriptorProto.Types.Type.Float then
                stmts.Add(
                    OtherExpr(
                        MatchExpr(
                            E $"value.{fname}",
                            [ MatchClauseExpr(
                                  ConstantPat(Constant("Some v")),
                                  E
                                      $"if System.Single.IsNaN(v) then writer.WriteString(\"{jsonName}\", \"NaN\") elif System.Single.IsPositiveInfinity(v) then writer.WriteString(\"{jsonName}\", \"Infinity\") elif System.Single.IsNegativeInfinity(v) then writer.WriteString(\"{jsonName}\", \"-Infinity\") else writer.WriteNumber(\"{jsonName}\", float v)"
                              )
                              MatchClauseExpr(ConstantPat(Constant("None")), E "()") ]
                        )
                    )
                )
            else
                stmts.Add(
                    OtherExpr(
                        MatchExpr(
                            E $"value.{fname}",
                            [ MatchClauseExpr(ConstantPat(Constant("Some v")), E(jsonWritePropertyExpr jsonName "v" f))
                              MatchClauseExpr(ConstantPat(Constant("None")), E "()") ]
                        )
                    )
                )
        elif f.Type = FieldDescriptorProto.Types.Type.Message then
            let msgTypeName = simpleTypeName f.TypeName

            match Map.tryFind f.TypeName wellKnownTypeMap with
            | Some _ ->
                stmts.Add(
                    OtherExpr(
                        MatchExpr(
                            E $"value.{fname}",
                            [ MatchClauseExpr(
                                  ConstantPat(Constant("Some v")),
                                  CompExprBodyExpr(
                                      [ OtherExpr(E $"writer.WritePropertyName(\"{jsonName}\")")
                                        OtherExpr(
                                            E "writer.WriteRawValue(Google.Protobuf.JsonFormatter.Default.Format(v))"
                                        ) ]
                                  )
                              )
                              MatchClauseExpr(ConstantPat(Constant("None")), E "()") ]
                        )
                    )
                )
            | None ->
                match Map.tryFind f.TypeName wrapperTypeMap with
                | Some wrapperScalar ->
                    let writeExpr =
                        match wrapperScalar with
                        | "string" -> $"writer.WriteString(\"{jsonName}\", v)"
                        | "bool" -> $"writer.WriteBoolean(\"{jsonName}\", v)"
                        | "float" -> $"writer.WriteNumber(\"{jsonName}\", v)"
                        | "float32" -> $"writer.WriteNumber(\"{jsonName}\", float v)"
                        | "int64" -> $"writer.WriteString(\"{jsonName}\", string v)"
                        | "uint64" -> $"writer.WriteString(\"{jsonName}\", string v)"
                        | "byte array" -> $"writer.WriteString(\"{jsonName}\", System.Convert.ToBase64String(v))"
                        | _ -> $"writer.WriteNumber(\"{jsonName}\", v)"

                    stmts.Add(
                        OtherExpr(
                            MatchExpr(
                                E $"value.{fname}",
                                [ MatchClauseExpr(ConstantPat(Constant("Some v")), E writeExpr)
                                  MatchClauseExpr(ConstantPat(Constant("None")), E "()") ]
                            )
                        )
                    )
                | None ->
                    stmts.Add(
                        OtherExpr(
                            MatchExpr(
                                E $"value.{fname}",
                                [ MatchClauseExpr(
                                      ConstantPat(Constant("Some v")),
                                      CompExprBodyExpr(
                                          [ OtherExpr(E $"writer.WritePropertyName(\"{jsonName}\")")
                                            OtherExpr(E $"{msgTypeName}.writeJsonTo writer v") ]
                                      )
                                  )
                                  MatchClauseExpr(ConstantPat(Constant("None")), E "()") ]
                            )
                        )
                    )
        elif f.Type = FieldDescriptorProto.Types.Type.Double then
            stmts.Add(
                OtherExpr(
                    IfThenExpr(
                        E $"value.{fname} <> 0.0",
                        E
                            $"if System.Double.IsNaN(value.{fname}) then writer.WriteString(\"{jsonName}\", \"NaN\") elif System.Double.IsPositiveInfinity(value.{fname}) then writer.WriteString(\"{jsonName}\", \"Infinity\") elif System.Double.IsNegativeInfinity(value.{fname}) then writer.WriteString(\"{jsonName}\", \"-Infinity\") else writer.WriteNumber(\"{jsonName}\", value.{fname})"
                    )
                )
            )
        elif f.Type = FieldDescriptorProto.Types.Type.Float then
            stmts.Add(
                OtherExpr(
                    IfThenExpr(
                        E $"value.{fname} <> 0.0f",
                        E
                            $"if System.Single.IsNaN(value.{fname}) then writer.WriteString(\"{jsonName}\", \"NaN\") elif System.Single.IsPositiveInfinity(value.{fname}) then writer.WriteString(\"{jsonName}\", \"Infinity\") elif System.Single.IsNegativeInfinity(value.{fname}) then writer.WriteString(\"{jsonName}\", \"-Infinity\") else writer.WriteNumber(\"{jsonName}\", float value.{fname})"
                    )
                )
            )
        else
            let check = defaultCheckExpr fname f

            stmts.Add(OtherExpr(IfThenExpr(E check, E(jsonWritePropertyExpr jsonName $"value.{fname}" f))))

    stmts.Add(OtherExpr(E "writer.WriteEndObject()"))

    Function(
        "writeJsonTo",
        [ ParenPat(ParameterPat("writer", LongIdent("System.Text.Json.Utf8JsonWriter")))
          ParenPat(ParameterPat("value", LongIdent(msg.Name))) ],
        CompExprBodyExpr(stmts),
        LongIdent("unit")
    )

/// Generate encodeJson function as AST widget.
let generateEncodeJsonAST (msg: DescriptorProto) =
    Function(
        "encodeJson",
        ParenPat(ParameterPat("value", LongIdent(msg.Name))),
        CompExprBodyExpr(
            [ LetOrUseExpr(Use("bufferWriter", E "new System.IO.MemoryStream()"))
              LetOrUseExpr(Use("writer", E "new System.Text.Json.Utf8JsonWriter(bufferWriter)"))
              OtherExpr(E "writeJsonTo writer value")
              OtherExpr(E "writer.Flush()")
              OtherExpr(E "System.Text.Encoding.UTF8.GetString(bufferWriter.ToArray())") ]
        ),
        LongIdent("string")
    )

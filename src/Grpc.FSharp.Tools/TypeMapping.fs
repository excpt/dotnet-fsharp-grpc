module internal Grpc.FSharp.Tools.TypeMapping

open Google.Protobuf.Reflection
open Fabulous.AST
open type Fabulous.AST.Ast

/// Convert "snake_case" or "SCREAMING_SNAKE_CASE" to "PascalCase".
let toPascalCase (name: string) =
    name.Split('_')
    |> Array.map (fun part ->
        if part.Length = 0 then
            ""
        else
            (string (System.Char.ToUpperInvariant(part.[0])))
            + part.[1..].ToLowerInvariant())
    |> System.String.Concat

/// Extract the simple type name from a fully-qualified proto type name
/// (e.g. ".example.Status" -> "Status").
let simpleTypeName (typeName: string) =
    match typeName.LastIndexOf('.') with
    | -1 -> typeName
    | i -> typeName.[i + 1 ..]

/// Check if a nested type is a synthetic map entry.
let isMapEntry (nested: DescriptorProto) =
    not (isNull nested.Options) && nested.Options.MapEntry

/// Build a set of map entry type names for a message.
let mapEntryTypes (msg: DescriptorProto) =
    msg.NestedType
    |> Seq.filter isMapEntry
    |> Seq.map (fun n -> n.Name)
    |> Set.ofSeq

/// Find the map entry nested type for a field, if it points to one.
let tryGetMapEntry (msg: DescriptorProto) (field: FieldDescriptorProto) =
    if field.Type <> FieldDescriptorProto.Types.Type.Message then
        None
    else
        let entryName = simpleTypeName field.TypeName
        msg.NestedType |> Seq.tryFind (fun n -> n.Name = entryName && isMapEntry n)

/// Well-known type mappings: fully-qualified proto type name -> F# type.
let wellKnownTypeMap =
    Map.ofList
        [ ".google.protobuf.Timestamp", "Google.Protobuf.WellKnownTypes.Timestamp"
          ".google.protobuf.Duration", "Google.Protobuf.WellKnownTypes.Duration"
          ".google.protobuf.Any", "Google.Protobuf.WellKnownTypes.Any" ]

/// Wrapper type mappings: fully-qualified proto type name -> unwrapped F# scalar type.
let wrapperTypeMap =
    Map.ofList
        [ ".google.protobuf.DoubleValue", "float"
          ".google.protobuf.FloatValue", "float32"
          ".google.protobuf.Int64Value", "int64"
          ".google.protobuf.UInt64Value", "uint64"
          ".google.protobuf.Int32Value", "int"
          ".google.protobuf.UInt32Value", "uint32"
          ".google.protobuf.BoolValue", "bool"
          ".google.protobuf.StringValue", "string"
          ".google.protobuf.BytesValue", "byte array" ]

/// Convert a proto scalar/enum/message type to a base F# type string.
let baseTypeName (field: FieldDescriptorProto) =
    match field.Type with
    | FieldDescriptorProto.Types.Type.Double -> "float"
    | FieldDescriptorProto.Types.Type.Float -> "float32"
    | FieldDescriptorProto.Types.Type.Int64
    | FieldDescriptorProto.Types.Type.Sint64
    | FieldDescriptorProto.Types.Type.Sfixed64 -> "int64"
    | FieldDescriptorProto.Types.Type.Uint64
    | FieldDescriptorProto.Types.Type.Fixed64 -> "uint64"
    | FieldDescriptorProto.Types.Type.Int32
    | FieldDescriptorProto.Types.Type.Sint32
    | FieldDescriptorProto.Types.Type.Sfixed32 -> "int"
    | FieldDescriptorProto.Types.Type.Uint32
    | FieldDescriptorProto.Types.Type.Fixed32 -> "uint32"
    | FieldDescriptorProto.Types.Type.Bool -> "bool"
    | FieldDescriptorProto.Types.Type.String -> "string"
    | FieldDescriptorProto.Types.Type.Bytes -> "byte array"
    | FieldDescriptorProto.Types.Type.Enum -> simpleTypeName field.TypeName
    | FieldDescriptorProto.Types.Type.Message ->
        match Map.tryFind field.TypeName wellKnownTypeMap with
        | Some t -> t
        | None ->
            match Map.tryFind field.TypeName wrapperTypeMap with
            | Some t -> t
            | None -> simpleTypeName field.TypeName
    | _ -> "obj"

/// Resolve the full F# type for a field, accounting for repeated, map, optional, and message option.
let resolveFieldType (msg: DescriptorProto) (field: FieldDescriptorProto) =
    match tryGetMapEntry msg field with
    | Some entry ->
        let keyType = baseTypeName entry.Field.[0]
        let valueType = baseTypeName entry.Field.[1]
        LongIdent($"Map<{keyType}, {valueType}>")
    | None ->
        let baseTy = baseTypeName field

        if field.Label = FieldDescriptorProto.Types.Label.Repeated then
            LongIdent($"{baseTy} list")
        elif field.Proto3Optional then
            LongIdent($"{baseTy} option")
        elif field.Type = FieldDescriptorProto.Types.Type.Message then
            LongIdent($"{baseTy} option")
        else
            LongIdent(baseTy)

/// Convert a proto package name to an F# namespace (e.g. "example.users" -> "Example.Users").
let toNamespace (package: string) =
    if System.String.IsNullOrEmpty(package) then
        None
    else
        package.Split('.') |> Array.map toPascalCase |> String.concat "." |> Some

/// Convert an EnumDescriptorProto to an F# enum widget.
let enumToEnum (enum: EnumDescriptorProto) =
    Enum(enum.Name) {
        for v in enum.Value do
            EnumCase(toPascalCase v.Name, Int(v.Number))
    }

/// Build a DU name from message name + oneof name (e.g. "Payment" + "method" -> "PaymentMethod").
let oneofTypeName (msgName: string) (oneofName: string) = msgName + toPascalCase oneofName

/// Generate a companion module for an enum with toJsonName/fromJsonName.
let enumJsonModule (enum: EnumDescriptorProto) =
    let toJsonClauses =
        [ for v in enum.Value do
              MatchClauseExpr(ConstantPat(Constant($"{enum.Name}.{toPascalCase v.Name}")), ConstantExpr(String(v.Name)))
          MatchClauseExpr(ConstantPat(Constant("_")), ConstantExpr(Constant($"string (int value)"))) ]

    let fromJsonClauses =
        [ for v in enum.Value do
              MatchClauseExpr(
                  ConstantPat(Constant($"\"{v.Name}\"")),
                  ConstantExpr(Constant($"{enum.Name}.{toPascalCase v.Name}"))
              )
          MatchClauseExpr(
              ConstantPat(Constant("_")),
              ConstantExpr(
                  Constant(
                      "match System.Int32.TryParse(name) with | true, v -> LanguagePrimitives.EnumOfValue v | _ -> LanguagePrimitives.EnumOfValue 0"
                  )
              )
          ) ]

    Module(enum.Name) {
        Function(
            "toJsonName",
            ParenPat(ParameterPat("value", LongIdent(enum.Name))),
            MatchExpr(ConstantExpr(Constant("value")), toJsonClauses),
            LongIdent("string")
        )

        Function(
            "fromJsonName",
            ParenPat(ParameterPat("name", LongIdent("string"))),
            MatchExpr(ConstantExpr(Constant("name")), fromJsonClauses),
            LongIdent(enum.Name)
        )
    }

/// Convert a oneof group to a discriminated union.
let oneofToUnion (msgName: string) (oneofDecl: OneofDescriptorProto) (fields: FieldDescriptorProto seq) =
    let duName = oneofTypeName msgName oneofDecl.Name

    Union(duName) {
        for f in fields |> Seq.sortBy (fun f -> f.Number) do
            UnionCase(toPascalCase f.Name, [ Field(f.JsonName, LongIdent(baseTypeName f)) ])
    }

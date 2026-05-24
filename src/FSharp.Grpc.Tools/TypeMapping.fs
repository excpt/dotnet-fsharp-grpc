module internal FSharp.Grpc.Tools.TypeMapping

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

/// Split a fully-qualified proto type name into its type segments,
/// dropping the leading dot and the package portion (lowercase-starting parts).
/// E.g. ".example.users.User" -> [| "User" |];
///      ".pkg.Outer.Inner" -> [| "Outer"; "Inner" |].
let private typeSegments (typeName: string) =
    typeName.TrimStart('.').Split('.')
    |> Array.skipWhile (fun p -> p.Length > 0 && System.Char.IsLower p.[0])

/// Build the F# namespace-relative path for a proto type, inserting `Types`
/// between nesting levels so nested types live under `Parent.Types.Inner`
/// (mirrors C# protobuf codegen). E.g. ".pkg.Outer.Inner" -> "Outer.Types.Inner",
/// ".pkg.User" -> "User", ".pkg.A.B.C" -> "A.Types.B.Types.C".
let typePath (typeName: string) =
    typeSegments typeName |> String.concat ".Types."

/// Build the reference path the generator should emit for a type, picking the
/// shortest form that resolves in the current scope. Three cases:
///   - target nested inside current msg -> "Types.X..." (relative; in scope)
///   - target is a sibling under the same parent -> bare leaf name
///     (parent's Types module is the surrounding scope)
///   - everything else -> absolute `typePath`
///
/// E.g. with currentMsgFqn ".pkg.Outer":
///   ".pkg.Outer.Inner" -> "Types.Inner"        (own nested)
///   ".pkg.Outer.Inner.Deep" -> "Types.Inner.Types.Deep"
///   ".pkg.User" -> "User"                       (sibling at namespace level)
///   ".pkg.Org.Dept" -> "Org.Types.Dept"         (cross-message nested)
///
/// E.g. with currentMsgFqn ".pkg.Outer.Inner" (deeply nested):
///   ".pkg.Outer.OtherInner" -> "OtherInner"     (sibling under Outer.Types)
let qualifiedRef (currentMsgFqn: string) (targetTypeName: string) =
    let prefix = currentMsgFqn + "."

    if targetTypeName.StartsWith(prefix) then
        let rest = targetTypeName.Substring(prefix.Length)
        "Types." + (rest.Split('.') |> String.concat ".Types.")
    else
        let parentOf (fqn: string) =
            match fqn.LastIndexOf('.') with
            | -1 -> ""
            | i -> fqn.Substring(0, i)

        let currentParent = parentOf currentMsgFqn
        let targetParent = parentOf targetTypeName

        if currentParent <> "" && currentParent = targetParent then
            // Sibling — the leaf name resolves in the current surrounding scope.
            targetTypeName.Substring(currentParent.Length + 1)
        else
            typePath targetTypeName

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

/// JSON write call for an unwrapped wrapper value as a named property. Picks the right
/// `Utf8JsonWriter.Write*` overload for each F# scalar in `wrapperTypeMap`. Strings,
/// 64-bit ints and bytes go through the string-typed JSON overloads per the proto
/// JSON spec; everything else is a JSON number.
let jsonWriteWrapperExpr (jsonName: string) (valueExpr: string) (wrapperScalar: string) : string =
    match wrapperScalar with
    | "string" -> $"writer.WriteString(\"{jsonName}\", {valueExpr})"
    | "bool" -> $"writer.WriteBoolean(\"{jsonName}\", {valueExpr})"
    | "float" -> $"writer.WriteNumber(\"{jsonName}\", {valueExpr})"
    | "float32" -> $"writer.WriteNumber(\"{jsonName}\", float {valueExpr})"
    | "int" -> $"writer.WriteNumber(\"{jsonName}\", {valueExpr})"
    | "uint32" -> $"writer.WriteNumber(\"{jsonName}\", {valueExpr})"
    | "int64" -> $"writer.WriteString(\"{jsonName}\", string {valueExpr})"
    | "uint64" -> $"writer.WriteString(\"{jsonName}\", string {valueExpr})"
    | "byte array" -> $"writer.WriteString(\"{jsonName}\", System.Convert.ToBase64String({valueExpr}))"
    | _ -> failwith $"unhandled wrapper scalar type: {wrapperScalar}"

/// JSON read expression for an unwrapped wrapper value from a `JsonElement` source.
/// `sourceExpr` is the JsonElement-typed source (e.g. `"prop.Value"`); the returned
/// string is an F# expression that yields the unwrapped scalar. 64-bit ints accept both
/// JSON string and number per the proto JSON spec.
let jsonReadWrapperExpr (sourceExpr: string) (wrapperScalar: string) : string =
    match wrapperScalar with
    | "string" -> $"{sourceExpr}.GetString()"
    | "bool" -> $"{sourceExpr}.GetBoolean()"
    | "float" -> $"{sourceExpr}.GetDouble()"
    | "float32" -> $"float32 ({sourceExpr}.GetDouble())"
    | "int64" ->
        $"if {sourceExpr}.ValueKind = System.Text.Json.JsonValueKind.String then int64 ({sourceExpr}.GetString()) else {sourceExpr}.GetInt64()"
    | "uint64" ->
        $"if {sourceExpr}.ValueKind = System.Text.Json.JsonValueKind.String then uint64 ({sourceExpr}.GetString()) else {sourceExpr}.GetUInt64()"
    | "int" -> $"{sourceExpr}.GetInt32()"
    | "uint32" -> $"{sourceExpr}.GetUInt32()"
    | "byte array" -> $"System.Convert.FromBase64String({sourceExpr}.GetString())"
    | _ -> failwith $"unhandled wrapper scalar type: {wrapperScalar}"

/// What a proto message-typed field actually refers to. Three categories handled
/// distinctly by every generator:
///   - `WellKnown` — a Google.Protobuf well-known type with native serialisation
///   - `Wrapper`   — a proto3 wrapper (StringValue, Int32Value, …) that we unwrap to a scalar
///   - `PlainMessage` — any other generated message
///
/// Caller behaviour today: only singular and oneof positions unwrap wrappers. In
/// repeated/map/optional positions the JSON generators still treat wrappers as
/// plain messages — see the wkt-only `Map.tryFind` branches in JsonEncodeGen.fs /
/// JsonDecodeGen.fs. Worth a follow-up if we want wrapper unwrapping there too.
type MessageKind =
    | WellKnown of fsType: string
    | Wrapper of scalarFsType: string * className: string
    | PlainMessage of fsTypeRef: string

/// Classify a message-typed field's `TypeName` against the WKT/wrapper maps and fall
/// through to a `qualifiedRef`-resolved plain message. Replaces the nested
/// `Map.tryFind … wellKnownTypeMap`/`Map.tryFind … wrapperTypeMap` cascade.
let classifyMessageRef (currentMsgFqn: string) (typeName: string) : MessageKind =
    match Map.tryFind typeName wellKnownTypeMap with
    | Some t -> WellKnown t
    | None ->
        match Map.tryFind typeName wrapperTypeMap with
        | Some scalar -> Wrapper(scalar, simpleTypeName typeName)
        | None -> PlainMessage(qualifiedRef currentMsgFqn typeName)

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
    | FieldDescriptorProto.Types.Type.Enum -> typePath field.TypeName
    | FieldDescriptorProto.Types.Type.Message ->
        match Map.tryFind field.TypeName wellKnownTypeMap with
        | Some t -> t
        | None ->
            match Map.tryFind field.TypeName wrapperTypeMap with
            | Some t -> t
            | None -> typePath field.TypeName
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

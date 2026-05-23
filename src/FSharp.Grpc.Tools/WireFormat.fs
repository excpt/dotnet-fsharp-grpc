module internal FSharp.Grpc.Tools.WireFormat

open Google.Protobuf.Reflection
open Fabulous.AST
open Fantomas.Core.SyntaxOak
open type Fabulous.AST.Ast
open FSharp.Grpc.Tools.TypeMapping

/// Get the default value expression for a field in the `empty` record.
let defaultValueExpr (msg: DescriptorProto) (field: FieldDescriptorProto) =
    match tryGetMapEntry msg field with
    | Some _ -> ConstantExpr(Constant("Map.empty"))
    | None ->
        if field.Label = FieldDescriptorProto.Types.Label.Repeated then
            ConstantExpr(Constant("[]"))
        elif field.Proto3Optional then
            ConstantExpr(Constant("None"))
        else
            match field.Type with
            | FieldDescriptorProto.Types.Type.Double -> ConstantExpr(Constant("0.0"))
            | FieldDescriptorProto.Types.Type.Float -> ConstantExpr(Constant("0.0f"))
            | FieldDescriptorProto.Types.Type.Int64
            | FieldDescriptorProto.Types.Type.Sint64
            | FieldDescriptorProto.Types.Type.Sfixed64 -> ConstantExpr(Constant("0L"))
            | FieldDescriptorProto.Types.Type.Uint64
            | FieldDescriptorProto.Types.Type.Fixed64 -> ConstantExpr(Constant("0UL"))
            | FieldDescriptorProto.Types.Type.Int32
            | FieldDescriptorProto.Types.Type.Sint32
            | FieldDescriptorProto.Types.Type.Sfixed32 -> ConstantExpr(Int(0))
            | FieldDescriptorProto.Types.Type.Uint32
            | FieldDescriptorProto.Types.Type.Fixed32 -> ConstantExpr(Constant("0u"))
            | FieldDescriptorProto.Types.Type.Bool -> ConstantExpr(Constant("false"))
            | FieldDescriptorProto.Types.Type.String -> ConstantExpr(String(""))
            | FieldDescriptorProto.Types.Type.Bytes -> ConstantExpr(Constant("Array.empty"))
            | FieldDescriptorProto.Types.Type.Enum -> ConstantExpr(Constant("LanguagePrimitives.EnumOfValue 0"))
            | FieldDescriptorProto.Types.Type.Message -> ConstantExpr(Constant("None"))
            | FieldDescriptorProto.Types.Type.Group -> failwith "proto2 groups are not supported"
            | _ -> failwith $"unknown field type: %A{field.Type}"

/// Get the default value expression for a oneof DU field.
let oneofDefaultExpr (_duName: string) = ConstantExpr(Constant("None"))

/// Proto wire type string for a field.
let wireType (field: FieldDescriptorProto) =
    match field.Type with
    | FieldDescriptorProto.Types.Type.Double
    | FieldDescriptorProto.Types.Type.Fixed64
    | FieldDescriptorProto.Types.Type.Sfixed64 -> "Fixed64"
    | FieldDescriptorProto.Types.Type.Float
    | FieldDescriptorProto.Types.Type.Fixed32
    | FieldDescriptorProto.Types.Type.Sfixed32 -> "Fixed32"
    | FieldDescriptorProto.Types.Type.String
    | FieldDescriptorProto.Types.Type.Bytes
    | FieldDescriptorProto.Types.Type.Message -> "LengthDelimited"
    | FieldDescriptorProto.Types.Type.Int32
    | FieldDescriptorProto.Types.Type.Int64
    | FieldDescriptorProto.Types.Type.Uint32
    | FieldDescriptorProto.Types.Type.Uint64
    | FieldDescriptorProto.Types.Type.Sint32
    | FieldDescriptorProto.Types.Type.Sint64
    | FieldDescriptorProto.Types.Type.Bool
    | FieldDescriptorProto.Types.Type.Enum -> "Varint"
    | FieldDescriptorProto.Types.Type.Group -> failwith "proto2 groups are not supported"
    | _ -> failwith $"unknown field type: %A{field.Type}"

/// Bare CodedOutputStream write method name for a scalar field.
/// Internal — call sites should use `writeExpr` which wraps the value correctly.
let private writeMethodName (field: FieldDescriptorProto) =
    match field.Type with
    | FieldDescriptorProto.Types.Type.Double -> "WriteDouble"
    | FieldDescriptorProto.Types.Type.Float -> "WriteFloat"
    | FieldDescriptorProto.Types.Type.Int64 -> "WriteInt64"
    | FieldDescriptorProto.Types.Type.Uint64 -> "WriteUInt64"
    | FieldDescriptorProto.Types.Type.Int32 -> "WriteInt32"
    | FieldDescriptorProto.Types.Type.Fixed64 -> "WriteFixed64"
    | FieldDescriptorProto.Types.Type.Fixed32 -> "WriteFixed32"
    | FieldDescriptorProto.Types.Type.Bool -> "WriteBool"
    | FieldDescriptorProto.Types.Type.String -> "WriteString"
    | FieldDescriptorProto.Types.Type.Bytes -> "WriteBytes"
    | FieldDescriptorProto.Types.Type.Uint32 -> "WriteUInt32"
    | FieldDescriptorProto.Types.Type.Enum -> "WriteInt32"
    | FieldDescriptorProto.Types.Type.Sfixed32 -> "WriteSFixed32"
    | FieldDescriptorProto.Types.Type.Sfixed64 -> "WriteSFixed64"
    | FieldDescriptorProto.Types.Type.Sint32 -> "WriteSInt32"
    | FieldDescriptorProto.Types.Type.Sint64 -> "WriteSInt64"
    | FieldDescriptorProto.Types.Type.Message ->
        failwith "writeMethodName called for Message type — messages use length-delimited encoding"
    | FieldDescriptorProto.Types.Type.Group -> failwith "proto2 groups are not supported"
    | _ -> failwith $"unknown field type: %A{field.Type}"

/// Bare CodedInputStream read method name for a scalar field.
/// Internal — call sites should use `readExpr` which wraps the result correctly.
let private readMethodName (field: FieldDescriptorProto) =
    match field.Type with
    | FieldDescriptorProto.Types.Type.Double -> "ReadDouble"
    | FieldDescriptorProto.Types.Type.Float -> "ReadFloat"
    | FieldDescriptorProto.Types.Type.Int64 -> "ReadInt64"
    | FieldDescriptorProto.Types.Type.Uint64 -> "ReadUInt64"
    | FieldDescriptorProto.Types.Type.Int32 -> "ReadInt32"
    | FieldDescriptorProto.Types.Type.Fixed64 -> "ReadFixed64"
    | FieldDescriptorProto.Types.Type.Fixed32 -> "ReadFixed32"
    | FieldDescriptorProto.Types.Type.Bool -> "ReadBool"
    | FieldDescriptorProto.Types.Type.String -> "ReadString"
    | FieldDescriptorProto.Types.Type.Bytes -> "ReadBytes"
    | FieldDescriptorProto.Types.Type.Uint32 -> "ReadUInt32"
    | FieldDescriptorProto.Types.Type.Enum -> "ReadInt32"
    | FieldDescriptorProto.Types.Type.Sfixed32 -> "ReadSFixed32"
    | FieldDescriptorProto.Types.Type.Sfixed64 -> "ReadSFixed64"
    | FieldDescriptorProto.Types.Type.Sint32 -> "ReadSInt32"
    | FieldDescriptorProto.Types.Type.Sint64 -> "ReadSInt64"
    | FieldDescriptorProto.Types.Type.Message ->
        failwith "readMethodName called for Message type — messages use length-delimited decoding"
    | FieldDescriptorProto.Types.Type.Group -> failwith "proto2 groups are not supported"
    | _ -> failwith $"unknown field type: %A{field.Type}"

/// Full write call for a scalar/enum/bytes field: e.g. `output.WriteInt32(int v)`,
/// `output.WriteBytes(Google.Protobuf.ByteString.CopyFrom(v))`. Callers don't need to
/// cast enums or wrap bytes — the helper handles type-specific quirks.
let writeExpr (output: string) (valueExpr: string) (field: FieldDescriptorProto) =
    let wm = writeMethodName field

    let arg =
        match field.Type with
        | FieldDescriptorProto.Types.Type.Enum -> $"int {valueExpr}"
        | FieldDescriptorProto.Types.Type.Bytes -> $"Google.Protobuf.ByteString.CopyFrom({valueExpr})"
        | _ -> valueExpr

    $"{output}.{wm}({arg})"

/// Full read expression for a scalar/enum/bytes field, producing the F#-typed value:
/// e.g. `input.ReadInt32()`, `input.ReadBytes().ToByteArray()`,
/// `LanguagePrimitives.EnumOfValue(input.ReadInt32())`.
let readExpr (input: string) (field: FieldDescriptorProto) =
    let rm = readMethodName field
    let raw = $"{input}.{rm}()"

    match field.Type with
    | FieldDescriptorProto.Types.Type.Enum -> $"LanguagePrimitives.EnumOfValue({raw})"
    | FieldDescriptorProto.Types.Type.Bytes -> $"{raw}.ToByteArray()"
    | _ -> raw

/// Zero-value check expression for proto3 default value omission.
let defaultCheckExpr (fieldName: string) (field: FieldDescriptorProto) =
    match field.Type with
    | FieldDescriptorProto.Types.Type.String -> $"value.{fieldName} <> \"\""
    | FieldDescriptorProto.Types.Type.Bool -> $"value.{fieldName}"
    | FieldDescriptorProto.Types.Type.Bytes -> $"value.{fieldName}.Length > 0"
    | FieldDescriptorProto.Types.Type.Enum -> $"int value.{fieldName} <> 0"
    | FieldDescriptorProto.Types.Type.Double -> $"value.{fieldName} <> 0.0"
    | FieldDescriptorProto.Types.Type.Float -> $"value.{fieldName} <> 0.0f"
    | FieldDescriptorProto.Types.Type.Int64
    | FieldDescriptorProto.Types.Type.Sint64
    | FieldDescriptorProto.Types.Type.Sfixed64 -> $"value.{fieldName} <> 0L"
    | FieldDescriptorProto.Types.Type.Uint64
    | FieldDescriptorProto.Types.Type.Fixed64 -> $"value.{fieldName} <> 0UL"
    | FieldDescriptorProto.Types.Type.Uint32
    | FieldDescriptorProto.Types.Type.Fixed32 -> $"value.{fieldName} <> 0u"
    | FieldDescriptorProto.Types.Type.Int32
    | FieldDescriptorProto.Types.Type.Sint32
    | FieldDescriptorProto.Types.Type.Sfixed32 -> $"value.{fieldName} <> 0"
    | FieldDescriptorProto.Types.Type.Message ->
        failwith "defaultCheckExpr called for Message type — messages use option-based presence"
    | FieldDescriptorProto.Types.Type.Group -> failwith "proto2 groups are not supported"
    | _ -> failwith $"unknown field type: %A{field.Type}"

/// Shorthand: create an Expr from a raw code fragment (identifier, member access, method call, etc.)
let E (s: string) = ConstantExpr(Constant(s))

/// Compute-size expression for a scalar field value (excluding tag).
/// Callers don't need to cast enums or wrap bytes — the helper handles type-specific quirks.
let computeSizeExpr (valueExpr: string) (field: FieldDescriptorProto) =
    match field.Type with
    | FieldDescriptorProto.Types.Type.Double -> "8"
    | FieldDescriptorProto.Types.Type.Float -> "4"
    | FieldDescriptorProto.Types.Type.Fixed64
    | FieldDescriptorProto.Types.Type.Sfixed64 -> "8"
    | FieldDescriptorProto.Types.Type.Fixed32
    | FieldDescriptorProto.Types.Type.Sfixed32 -> "4"
    | FieldDescriptorProto.Types.Type.Bool -> "1"
    | FieldDescriptorProto.Types.Type.Int32 -> $"Google.Protobuf.CodedOutputStream.ComputeInt32Size({valueExpr})"
    | FieldDescriptorProto.Types.Type.Int64 -> $"Google.Protobuf.CodedOutputStream.ComputeInt64Size({valueExpr})"
    | FieldDescriptorProto.Types.Type.Uint32 -> $"Google.Protobuf.CodedOutputStream.ComputeUInt32Size({valueExpr})"
    | FieldDescriptorProto.Types.Type.Uint64 -> $"Google.Protobuf.CodedOutputStream.ComputeUInt64Size({valueExpr})"
    | FieldDescriptorProto.Types.Type.Sint32 -> $"Google.Protobuf.CodedOutputStream.ComputeSInt32Size({valueExpr})"
    | FieldDescriptorProto.Types.Type.Sint64 -> $"Google.Protobuf.CodedOutputStream.ComputeSInt64Size({valueExpr})"
    | FieldDescriptorProto.Types.Type.String -> $"Google.Protobuf.CodedOutputStream.ComputeStringSize({valueExpr})"
    | FieldDescriptorProto.Types.Type.Bytes ->
        $"Google.Protobuf.CodedOutputStream.ComputeBytesSize(Google.Protobuf.ByteString.CopyFrom({valueExpr}))"
    | FieldDescriptorProto.Types.Type.Enum -> $"Google.Protobuf.CodedOutputStream.ComputeInt32Size(int {valueExpr})"
    | FieldDescriptorProto.Types.Type.Message ->
        failwith "computeSizeExpr called for Message type — messages compute size via nested writeTo"
    | FieldDescriptorProto.Types.Type.Group -> failwith "proto2 groups are not supported"
    | _ -> failwith $"unknown field type: %A{field.Type}"

/// size <- size + exprString
let addToSize (exprString: string) = E $"size <- size + {exprString}"

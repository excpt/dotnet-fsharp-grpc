# Messages

Generate F# types from `.proto` files with binary and JSON serialization.

## Step 1: Create a project

```bash
dotnet new console -lang F# -n MyApp
cd MyApp
```

## Step 2: Install the package

```bash
dotnet add package Grpc.FSharp.Tools.Codegen
```

## Step 3: Add a proto file

```bash
mkdir protos
```

Create `protos/messages.proto`:

```protobuf
syntax = "proto3";
package myapp;

message Todo {
  string id = 1;
  string title = 2;
  bool done = 3;
}
```

## Step 4: Register it in your project file

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net8.0</TargetFramework>
    </PropertyGroup>

    <ItemGroup>
        <Compile Include="Program.fs"/>
    </ItemGroup>

    <ItemGroup>
        <Protobuf Include="protos/messages.proto"/>
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="Grpc.FSharp.Tools.Codegen"/>
    </ItemGroup>

</Project>
```

## Step 5: Use the generated types

```fsharp
open Myapp

let todo =
    { Todo.empty with
        Id = "TODO-001"
        Title = "Learn Grpc.FSharp"
        Done = true }

// Binary
let bytes = Todo.encode todo
let decoded = Todo.decode bytes

// JSON
let json = Todo.encodeJson todo
// {"id":"TODO-001","title":"Learn Grpc.FSharp","done":true}
let fromJson = Todo.decodeJson json

// Structural equality
printfn "Roundtrip OK: %b" (todo = decoded)
```

## Step 6: Build and run

```bash
dotnet run
```

The code generator runs automatically during build. Generated files appear in `obj/protos/` and are compiled before your code.

---

## Generated API

Every proto message becomes an F# record with a companion module:

| Function | Signature | Description |
|---|---|---|
| `empty` | `T` | Zero-value instance for `{ T.empty with ... }` |
| `encode` | `T -> byte array` | Binary serialization |
| `decode` | `byte array -> T` | Binary deserialization |
| `encodeJson` | `T -> string` | JSON serialization |
| `decodeJson` | `string -> T` | JSON deserialization |
| `writeJsonTo` | `Utf8JsonWriter -> T -> unit` | Streaming JSON write |
| `decodeJsonElement` | `JsonElement -> T` | Parse from `JsonElement` |
| `computeSize` | `T -> int` | Serialized byte size |
| `writeTo` | `CodedOutputStream -> T -> unit` | Low-level binary write |

Each enum gets a companion module with `toJsonName` / `fromJsonName`.

---

## Type mapping

### Scalars

| Proto type | F# type | Default |
|---|---|---|
| `double` | `float` | `0.0` |
| `float` | `float32` | `0.0f` |
| `int32`, `sint32`, `sfixed32` | `int` | `0` |
| `int64`, `sint64`, `sfixed64` | `int64` | `0L` |
| `uint32`, `fixed32` | `uint32` | `0u` |
| `uint64`, `fixed64` | `uint64` | `0UL` |
| `bool` | `bool` | `false` |
| `string` | `string` | `""` |
| `bytes` | `byte array` | `Array.empty` |

### Composite types

| Proto type | F# type | Default |
|---|---|---|
| `message` field | `TypeName option` | `None` |
| `optional` scalar | `scalar option` | `None` |
| `repeated T` | `T list` | `[]` |
| `map<K, V>` | `Map<K, V>` | `Map.empty` |
| `enum` | F# enum | `LanguagePrimitives.EnumOfValue 0` |
| `oneof` group | Discriminated union `option` | `None` |

### Well-known types

| Proto type | F# type |
|---|---|
| `google.protobuf.Timestamp` | `Google.Protobuf.WellKnownTypes.Timestamp option` |
| `google.protobuf.Duration` | `Google.Protobuf.WellKnownTypes.Duration option` |
| `google.protobuf.Any` | `Google.Protobuf.WellKnownTypes.Any option` |
| `google.protobuf.StringValue` | `string option` |
| `google.protobuf.Int32Value` | `int option` |
| `google.protobuf.BoolValue` | `bool option` |
| *(other wrappers)* | `scalar option` |

### Naming conventions

| Proto | F# |
|---|---|
| `package my.app;` | `namespace My.App` |
| `message MyMessage` | `type MyMessage = { ... }` |
| `field_name` | `FieldName` (PascalCase) |
| `STATUS_ACTIVE` | `StatusActive` (PascalCase) |
| `oneof method` | `type {Msg}Method = \| ...` (DU) |

---

## Examples

### Enums

```protobuf
enum Status {
  STATUS_UNKNOWN  = 0;
  STATUS_ACTIVE   = 1;
  STATUS_INACTIVE = 2;
}

message Account {
  string name = 1;
  Status status = 2;
}
```

```fsharp
let account = { Account.empty with Name = "alice"; Status = Status.StatusActive }
let json = Account.encodeJson account
// {"name":"alice","status":"STATUS_ACTIVE"}
```

### Oneof (discriminated unions)

```protobuf
message Payment {
  string id = 1;
  oneof method {
    string credit_card = 2;
    string bank_account = 3;
  }
}
```

```fsharp
let payment =
    { Payment.empty with
        Id = "PAY-001"
        Method = Some (PaymentMethod.CreditCard "4111-1111-1111-1111") }

match payment.Method with
| None -> printfn "No payment method"
| Some (PaymentMethod.CreditCard card) -> printfn "Card: %s" card
| Some (PaymentMethod.BankAccount acct) -> printfn "Bank: %s" acct
```

### Repeated fields

```protobuf
message WithRepeated {
  repeated string tags = 1;
  repeated int32 scores = 2;
}
```

```fsharp
let data =
    { WithRepeated.empty with
        Tags = [ "protobuf"; "fsharp" ]
        Scores = [ 100; 95; 87 ] }
```

### Map fields

```protobuf
message WithMap {
  map<string, string> labels = 1;
}
```

```fsharp
let data =
    { WithMap.empty with
        Labels = Map.ofList [ "env", "prod"; "region", "eu-west-1" ] }
```

### Nested messages and optional fields

```protobuf
message Address {
  string street = 1;
  string city = 2;
}

message User {
  string name = 1;
  Address home_address = 2;
  optional string nickname = 3;
}
```

```fsharp
let user =
    { User.empty with
        Name = "Alice"
        HomeAddress = Some { Address.empty with Street = "123 Main St"; City = "Springfield" }
        Nickname = Some "ally" }

match user.HomeAddress with
| Some addr -> printfn "%s, %s" addr.Street addr.City
| None -> printfn "No address"
```

---

## JSON mapping rules

- **Field names**: camelCase on encode. Both camelCase and snake_case accepted on decode.
- **Default values**: Fields at their zero value are omitted from JSON output.
- **int64/uint64**: Serialized as JSON strings (JavaScript precision).
- **bytes**: Base64-encoded strings.
- **enum**: Proto string names (e.g. `"STATUS_ACTIVE"`). Integer fallback on decode.
- **float/double**: NaN and Infinity as `"NaN"`, `"Infinity"`, `"-Infinity"`.
- **Well-known types**: Timestamp, Duration, Any use `Google.Protobuf.JsonFormatter`/`JsonParser`.
- **Wrapper types**: Serialized as the unwrapped scalar value.

---

## MSBuild options

### Multiple proto files

```xml
<ItemGroup>
    <Protobuf Include="protos/**/*.proto"/>
</ItemGroup>
```

### Custom output directory

```xml
<PropertyGroup>
    <GrpcFSharp_OutputDir>$(BaseIntermediateOutputPath)generated/</GrpcFSharp_OutputDir>
</PropertyGroup>
```

### Custom protoc path

```xml
<PropertyGroup>
    <GrpcFSharp_ProtocFullPath>/usr/local/bin/protoc</GrpcFSharp_ProtocFullPath>
</PropertyGroup>
```

### CLI usage

```bash
dotnet grpc-fsharp-codegen [--protoc <path>] [--proto-path <dir>]... [--output-dir <dir>] <file.proto>...
```

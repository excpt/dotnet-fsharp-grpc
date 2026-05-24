# Tutorial

A short end-to-end walk-through: install the package, declare a proto, build, and use
the generated types.

## 1. Create a project

```bash
dotnet new console -lang F# -n HelloProto
cd HelloProto
dotnet add package FSharp.Grpc.Tools.Codegen
```

## 2. Add a proto file

Create `protos/person.proto`:

```protobuf
syntax = "proto3";
package example;

message Person {
  string name = 1;
  int32 age = 2;
}
```

Register it in `HelloProto.fsproj`:

```xml
<ItemGroup>
    <Protobuf Include="protos/person.proto"/>
</ItemGroup>
```

## 3. Build

```bash
dotnet build
```

The MSBuild target invokes `protoc`, then the F# generator. Generated files land in
`obj/` and are compiled before your own code.

## 4. Use the generated types

```fsharp
open Example

let alice = { Person.empty with Name = "Alice"; Age = 30 }

let bytes = Person.encode alice
let decoded = Person.decode bytes

let json = Person.encodeJson alice  // {"name":"Alice","age":30}
```

## Next steps

- [Messages](messages.md) — type mapping, JSON, examples.
- [gRPC Server](grpc-server.md) — handler signatures, all streaming patterns.
- [gRPC Client](grpc-client.md) — client creation, all streaming patterns.

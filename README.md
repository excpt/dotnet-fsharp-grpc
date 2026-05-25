# FSharp.Grpc

F# code generation from `.proto` files. Generates F# records, enums, and discriminated unions with binary and JSON serialization. Generates gRPC server and client stubs.

## Documentation

- [Messages](docs/messages.md) — setup, type mapping, JSON, examples
- [gRPC Server](docs/grpc-server.md) — setup, handler signatures, all streaming patterns, testing
- [gRPC Client](docs/grpc-client.md) — setup, client creation, all streaming patterns

## Overview

```protobuf
syntax = "proto3";
package example;

message Person {
  string name = 1;
  int32 age = 2;
}
```

```fsharp
open Example

let alice = { Person.empty with Name = "Alice"; Age = 30 }

let bytes = Person.encode alice
let decoded = Person.decode bytes

let json = Person.encodeJson alice
// {"name":"Alice","age":30}
```

## Getting started

```bash
dotnet new console -lang F# -n MyApp
cd MyApp
dotnet add package FSharp.Grpc.Tools.Codegen
```

Add a proto file and register it in your `.fsproj`:

```xml
<ItemGroup>
    <Protobuf Include="protos/person.proto"/>
</ItemGroup>
```

Build and use the generated types:

```bash
dotnet build
```

```fsharp
open Example

let request = { Person.empty with Name = "World"; Age = 30 }
let bytes = Person.encode request
let json = Person.encodeJson request
```

## Features

| Proto feature | F# representation |
|---|---|
| Scalar fields | Record fields |
| `message` fields | `TypeName option` |
| `optional` scalars | `scalar option` |
| `repeated` fields | `type list` |
| `map<K, V>` fields | `Map<K, V>` |
| `enum` | F# enum + companion module |
| `oneof` | Discriminated union |
| Well-known types | `Google.Protobuf.WellKnownTypes` |
| Wrapper types | Unwrapped `option` |
| Binary serialization | `encode` / `decode` |
| JSON serialization | `encodeJson` / `decodeJson` |
| gRPC services | Server + client stubs |

## gRPC services

Set `GrpcServices` metadata to generate server and client stubs:

```xml
<Protobuf Include="protos/greeter.proto" GrpcServices="Both"/>
```

### Server

```fsharp
open Greeter
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection

let handlers: GreeterHandlers =
    { SayHello =
        fun req ctx ->
            task { return { HelloReply.empty with Message = $"Hello {req.Name}!" } } }

let builder = WebApplication.CreateBuilder()
builder.Services.AddGrpc() |> ignore
builder.Services.AddSingleton(GreeterService(handlers)) |> ignore
let app = builder.Build()
app.MapGrpcService<GreeterService>() |> ignore
app.Run()
```

### Client

```fsharp
open Greeter

let run () = task {
    let client = GreeterClient.create "https://localhost:5001"
    let! reply = client.SayHello { HelloRequest.empty with Name = "World" }
    printfn "%s" reply.Message
}
```

Supported patterns: unary, server streaming, client streaming, bidirectional streaming.

| `GrpcServices` value | Messages | Server stubs | Client stubs |
|---|---|---|---|
| `None` (default) | Yes | No | No |
| `Server` | Yes | Yes | No |
| `Client` | Yes | No | Yes |
| `Both` | Yes | Yes | Yes |

## Wire compatibility

Generated code produces the same wire format as C#, Go, Java, Python, and other protobuf implementations. Tested with 50 cross-language serialization tests and 10 cross-language gRPC tests (F# server + C# client, C# server + F# client).

## Benchmarks

Apple M3 Max, .NET 10.0:

### Binary

**Person** (2 fields):

| Method | Mean | Allocated |
|---|---:|---:|
| C# Encode | 23.26 ns | 112 B |
| F# Encode | 22.27 ns | 112 B |
| C# Decode | 38.94 ns | 256 B |
| F# Decode | 26.86 ns | 248 B |

**ScalarTypes** (15 fields):

| Method | Mean | Allocated |
|---|---:|---:|
| C# Encode | 111.0 ns | 200 B |
| F# Encode | 168.8 ns | 264 B |
| C# Decode | 129.2 ns | 424 B |
| F# Decode | 139.1 ns | 448 B |

**UserProfile** (enum, nested message, repeated, maps, oneof):

| Method | Mean | Allocated |
|---|---:|---:|
| C# Encode | 446.9 ns | 496 B |
| F# Encode | 442.3 ns | 504 B |
| C# Decode | 616.2 ns | 2,640 B |
| F# Decode | 546.0 ns | 2,752 B |

**UserProfile, high cardinality** (1,000 repeated ints + 100 nested messages — exercises the sub-message decode path):

| Method | Mean | Allocated |
|---|---:|---:|
| C# Decode | 6.79 µs | 24.95 KB |
| F# Decode | 12.72 µs | 82.3 KB |

### JSON

**Person** (2 fields):

| Method | Mean | Allocated |
|---|---:|---:|
| C# JSON Encode | 124.39 ns | 568 B |
| F# JSON Encode | 99.51 ns | 944 B |
| C# JSON Decode | 202.54 ns | 560 B |
| F# JSON Decode | 186.27 ns | 216 B |

**ScalarTypes** (15 fields):

| Method | Mean | Allocated |
|---|---:|---:|
| C# JSON Encode | 1,233.0 ns | 3.2 KB |
| F# JSON Encode | 659.9 ns | 6.91 KB |
| C# JSON Decode | 1,938.9 ns | 3.45 KB |
| F# JSON Decode | 1,129.7 ns | 1.27 KB |

**UserProfile** (enum, nested message, repeated, maps, oneof):

| Method | Mean | Allocated |
|---|---:|---:|
| C# JSON Encode | 1,704.0 ns | 4.56 KB |
| F# JSON Encode | 931.8 ns | 7.69 KB |
| C# JSON Decode | 2,712.5 ns | 6.42 KB |
| F# JSON Decode | 2,153.9 ns | 4.28 KB |

## Requirements

- .NET 8.0 SDK or later
- `protoc` is resolved automatically from the `Grpc.Tools` NuGet package

## Limitations

- Proto3 only. Proto2 syntax is not supported.
- The deprecated `group` field type is rejected.

## License

MIT. See [LICENSE](LICENSE).

---

Developed with [Claude Code](https://claude.ai/claude-code).

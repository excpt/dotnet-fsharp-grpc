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
| C# Encode | 23.12 ns | 112 B |
| F# Encode | 23.45 ns | 112 B |
| C# Decode | 40.01 ns | 256 B |
| F# Decode | 28.97 ns | 248 B |

**ScalarTypes** (15 fields):

| Method | Mean | Allocated |
|---|---:|---:|
| C# Encode | 117.4 ns | 200 B |
| F# Encode | 171.3 ns | 264 B |
| C# Decode | 128.1 ns | 424 B |
| F# Decode | 138.7 ns | 448 B |

**UserProfile** (enum, nested message, repeated, maps, oneof):

| Method | Mean | Allocated |
|---|---:|---:|
| C# Encode | 453.6 ns | 496 B |
| F# Encode | 442.6 ns | 504 B |
| C# Decode | 622.5 ns | 2,640 B |
| F# Decode | 829.0 ns | 5,200 B |

### JSON

**Person** (2 fields):

| Method | Mean | Allocated |
|---|---:|---:|
| C# JSON Encode | 130.54 ns | 568 B |
| F# JSON Encode | 99.97 ns | 944 B |
| C# JSON Decode | 207.10 ns | 560 B |
| F# JSON Decode | 186.04 ns | 216 B |

**ScalarTypes** (15 fields):

| Method | Mean | Allocated |
|---|---:|---:|
| C# JSON Encode | 1,241.1 ns | 3.2 KB |
| F# JSON Encode | 677.5 ns | 6.91 KB |
| C# JSON Decode | 1,953.7 ns | 3.45 KB |
| F# JSON Decode | 1,124.1 ns | 1.27 KB |

**UserProfile** (enum, nested message, repeated, maps, oneof):

| Method | Mean | Allocated |
|---|---:|---:|
| C# JSON Encode | 1,670.5 ns | 4.56 KB |
| F# JSON Encode | 965.6 ns | 7.69 KB |
| C# JSON Decode | 2,791.8 ns | 6.42 KB |
| F# JSON Decode | 2,185.8 ns | 4.28 KB |

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

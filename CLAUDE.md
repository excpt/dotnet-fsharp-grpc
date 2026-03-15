# CLAUDE.md

## Project

F# code generator for Protocol Buffers. Generates F# records, enums, discriminated unions, binary/JSON serialization, and gRPC server/client stubs from `.proto` files.

Single NuGet package: `FSharp.Grpc.Tools.Codegen`. No runtime libraries — generated code depends only on `Google.Protobuf` and `Grpc.Core.Api`.

## Development

### Build and test

```bash
dotnet build
dotnet test
```

Always run `dotnet test` at the solution root. Do not run individual test projects.

### Formatting

All F# code is formatted with `dotnet fantomas`. All C# code is formatted with `dotnet format`. Settings are in `.editorconfig`.

```bash
dotnet fantomas --check .   # check F# formatting
dotnet fantomas .           # fix F# formatting
dotnet format --verify-no-changes  # check C# formatting
dotnet format               # fix C# formatting
```

CI enforces F# formatting on every PR.

### TDD workflow

Write tests first, then implement. The expected workflow:

1. Add or update expected output files in `tests/FSharp.Grpc.Tools.Tests/expected/`
2. Add or update test cases in the corresponding test file
3. Run `dotnet test` — tests fail
4. Implement the feature in `src/FSharp.Grpc.Tools/`
5. Run `dotnet test` — tests pass

For gRPC integration tests, write the test in `GrpcIntegrationTests.fs` first, then update the hand-written generated code to match.

### Benchmarks

Benchmarks are in `benchmarks/FSharp.Grpc.Benchmarks/`. Binary and JSON benchmarks compare F# generated code against C#'s `Google.Protobuf`.

```bash
dotnet run --project benchmarks/FSharp.Grpc.Benchmarks/ -c Release -f net10.0
```

Results go to `BenchmarkDotNet.Artifacts/results/` (gitignored).

## Architecture

### Source projects

- `src/FSharp.Grpc.Tools/` — code generation library (proto → F# AST → formatted code)
- `src/FSharp.Grpc.Tools.Codegen/` — CLI tool + MSBuild targets (the NuGet package)

### Code generation

The code generator uses Fabulous.AST to construct F# syntax trees, formatted by Fantomas. Key modules in `src/FSharp.Grpc.Tools/`:

- `TypeMapping.fs` — proto type → F# type mapping
- `WireFormat.fs` — wire format constants
- `ComputeSizeGen.fs` — generate `computeSize` functions
- `WriteToGen.fs` — generate `writeTo` functions
- `DecodeGen.fs` — generate `decode` functions
- `JsonEncodeGen.fs` — generate JSON encode functions
- `JsonDecodeGen.fs` — generate JSON decode functions
- `ServiceGen.fs` — generate gRPC server and client stubs
- `ProtoToAst.fs` — orchestrate code generation

#### AST vs constant expressions

Use Fabulous.AST builders (`Record`, `Module`, `Function`, `Field`, `Value`, `TypeDefn`, `Member`, etc.) for code structure: types, modules, function signatures, attributes, open statements.

Use `ConstantExpr(Constant("..."))` for expression bodies where Fabulous.AST lacks expression-level builders (method calls, record construction, lambda expressions). Fantomas formats these strings correctly.

Prefer AST builders when possible. Fall back to `ConstantExpr` only for expression bodies.

### Test projects

- `tests/FSharp.Grpc.Tools.Tests/` — codegen unit tests. Expected output in `expected/*.fs.expected`. Tests compile proto files with `protoc`, run the code generator, and compare output character-by-character.
- `tests/FSharp.Grpc.AspNetCore.Tests/` — gRPC integration tests. Real gRPC server + client via ASP.NET Core TestHost. Hand-written generated code for Greeter and TestService (all 4 streaming patterns).
- `tests/FSharp.Grpc.Integration.Tests.FSharp/` — F# interop type library. Hand-written F# types matching `interop.proto` for cross-language testing.
- `tests/FSharp.Grpc.Integration.Tests/` — C# cross-language serialization tests. 50 tests verifying byte-exact wire compatibility between F# and C# protobuf.
- `tests/FSharp.Grpc.GrpcCrossLang.Tests.FSharp/` — F# gRPC types for cross-language testing.
- `tests/FSharp.Grpc.GrpcCrossLang.Tests/` — C# cross-language gRPC tests. 10 tests: F# server + C# client and C# server + F# client, all 4 streaming patterns.

Total: 107 tests.

### Hand-written generated code

Several test projects contain hand-written `.fs` files that match what the code generator produces. When changing the generated code pattern, update these files to match:

- `tests/FSharp.Grpc.AspNetCore.Tests/` — `Greeter.{Messages,Server,Client}.fs`, `TestService.{Messages,Server,Client}.fs`
- `tests/FSharp.Grpc.Integration.Tests.FSharp/` — `Greeter.{Messages,Server,Client}.fs`, `TestService.{Messages,Server,Client}.fs`
- `tests/FSharp.Grpc.GrpcCrossLang.Tests.FSharp/` — `CrossLang.{Messages,Server,Client}.fs`
- `tests/FSharp.Grpc.Tools.Tests/expected/` — `*.fs.expected` files

### Generated code patterns

**Messages**: F# record + companion module with `empty`, `encode`, `decode`, `encodeJson`, `decodeJson`.

**Server**: Handler record type (`{Service}Handlers`) + service class (`{Service}Service`) with `[<BindServiceMethod>]`.

**Client**: Client record type (`{Service}Client`) with one field per RPC method + companion module with `create`, `fromChannel`, `fromInvoker`.

**Marshalling**: Inlined directly as `Marshaller(System.Func<_, _>(T.encode), System.Func<_, _>(T.decode))`. No runtime library.

## Conventions

- F# is the primary language. C# is only used in test and benchmark projects.
- Proto3 only. Proto2 is not supported.
- Generated code uses fully-qualified type names to avoid hidden `open` dependencies.
- F# compilation order matters. Messages compile before server stubs, server stubs before client stubs, generated code before user code.
- Use `{ T.empty with ... }` pattern for constructing records.
- `option` for absent values: message fields, optional scalars, oneof groups.
- `list` for repeated fields. `Map` for map fields. F# enum for proto enums. Discriminated union for oneof.

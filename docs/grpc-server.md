# gRPC Server

Generate F# gRPC server stubs from `.proto` service definitions. Handlers are F# functions passed via a record type.

## Step 1: Create a project

```bash
dotnet new web -lang F# -n MyServer
cd MyServer
```

## Step 2: Install packages

```bash
dotnet add package Grpc.FSharp.Tools.Codegen
dotnet add package Grpc.AspNetCore.Server
```

## Step 3: Define your service

Create `protos/greeter.proto`:

```protobuf
syntax = "proto3";
package greeter;

message HelloRequest {
  string name = 1;
}

message HelloReply {
  string message = 1;
}

service Greeter {
  rpc SayHello (HelloRequest) returns (HelloReply);
}
```

## Step 4: Configure your project file

Set `GrpcServices="Server"` on the `<Protobuf>` item:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
    </PropertyGroup>

    <ItemGroup>
        <Compile Include="Program.fs"/>
    </ItemGroup>

    <ItemGroup>
        <Protobuf Include="protos/greeter.proto" GrpcServices="Server"/>
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="Grpc.FSharp.Tools.Codegen"/>
        <PackageReference Include="Grpc.AspNetCore.Server"/>
    </ItemGroup>

</Project>
```

## Step 5: Implement handlers and start the server

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

## Step 6: Build and run

```bash
dotnet run
```

---

## Generated code

For each `service` in the proto, the generator produces:

**Handler record** — one field per RPC method:

```fsharp
type GreeterHandlers =
    { SayHello: HelloRequest -> ServerCallContext -> Task<HelloReply> }
```

**Service class** — wired into ASP.NET Core via `[<BindServiceMethod>]`:

```fsharp
[<BindServiceMethod(typeof<GreeterService>, "BindService")>]
type GreeterService(handlers: GreeterHandlers) = ...
```

Construct it with your handlers record, register with `MapGrpcService<T>()`, done.

---

## All RPC patterns

### Unary

```protobuf
rpc SayHello (HelloRequest) returns (HelloReply);
```

```fsharp
SayHello = fun req ctx ->
    task { return { HelloReply.empty with Message = $"Hello {req.Name}!" } }
```

### Server streaming

```protobuf
rpc ServerStream (Request) returns (stream Item);
```

```fsharp
ServerStream = fun req stream ctx ->
    task {
        for i in 1..3 do
            do! stream.WriteAsync({ StreamItem.empty with Value = i })
    }
```

### Client streaming

```protobuf
rpc ClientStream (stream Item) returns (Summary);
```

```fsharp
ClientStream = fun stream ctx ->
    task {
        let mutable count = 0
        let mutable total = 0
        while! stream.MoveNext(CancellationToken.None) do
            count <- count + 1
            total <- total + stream.Current.Value
        return { Summary.empty with Count = count; Total = total }
    }
```

### Bidirectional streaming

```protobuf
rpc BidiStream (stream Item) returns (stream Item);
```

```fsharp
BidiStream = fun reqStream resStream ctx ->
    task {
        while! reqStream.MoveNext(CancellationToken.None) do
            do! resStream.WriteAsync(
                { StreamItem.empty with Value = reqStream.Current.Value * 2 })
    }
```

---

## Handler signatures

| Pattern | Signature |
|---|---|
| Unary | `Request -> ServerCallContext -> Task<Response>` |
| Server streaming | `Request -> IServerStreamWriter<Response> -> ServerCallContext -> Task` |
| Client streaming | `IAsyncStreamReader<Request> -> ServerCallContext -> Task<Response>` |
| Bidi streaming | `IAsyncStreamReader<Request> -> IServerStreamWriter<Response> -> ServerCallContext -> Task` |

---

## GrpcServices metadata

| Value | Messages | Server stubs | Client stubs |
|---|---|---|---|
| `None` (default) | Yes | No | No |
| `Server` | Yes | Yes | No |
| `Client` | Yes | No | Yes |
| `Both` | Yes | Yes | Yes |

---

## Testing with TestHost

Use `Microsoft.AspNetCore.TestHost` for in-process testing:

```fsharp
let startServer (service: 'T when 'T : not struct) = task {
    let builder = WebApplication.CreateBuilder()
    builder.WebHost.UseTestServer() |> ignore
    builder.Services.AddGrpc() |> ignore
    builder.Services.AddSingleton<'T>(service) |> ignore
    let app = builder.Build()
    app.MapGrpcService<'T>() |> ignore
    do! app.StartAsync()

    let handler = app.GetTestServer().CreateHandler()
    let channel =
        GrpcChannel.ForAddress("http://localhost", GrpcChannelOptions(HttpHandler = handler))
    return (app, channel.CreateCallInvoker())
}
```

```fsharp
[<Fact>]
let ``unary roundtrip`` () = task {
    let handlers: GreeterHandlers =
        { SayHello = fun req ctx ->
            task { return { HelloReply.empty with Message = $"Hello {req.Name}" } } }

    let! (app, invoker) = startServer (GreeterService(handlers))
    try
        let client = GreeterClient.fromInvoker invoker
        let! reply = client.SayHello { HelloRequest.empty with Name = "World" }
        reply.Message |> should equal "Hello World"
    finally
        app.StopAsync().Wait()
}
```

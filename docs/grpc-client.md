# gRPC Client

Generate F# gRPC client stubs from `.proto` service definitions. Each service becomes a record type with factory functions.

## Step 1: Create a project

```bash
dotnet new console -lang F# -n MyClient
cd MyClient
```

## Step 2: Install packages

```bash
dotnet add package FSharp.Grpc.Tools.Codegen
dotnet add package Grpc.Net.Client
```

## Step 3: Define your service

Use the same `.proto` file as the server. Create `protos/greeter.proto`:

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

Set `GrpcServices="Client"` on the `<Protobuf>` item:

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
        <Protobuf Include="protos/greeter.proto" GrpcServices="Client"/>
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="FSharp.Grpc.Tools.Codegen"/>
        <PackageReference Include="Grpc.Net.Client"/>
    </ItemGroup>

</Project>
```

## Step 5: Create a client and call the service

```fsharp
open Greeter

let run () = task {
    let client = GreeterClient.create "https://localhost:5001"
    let! reply = client.SayHello { HelloRequest.empty with Name = "World" }
    printfn "%s" reply.Message
}

run().Wait()
```

## Step 6: Build and run

```bash
dotnet run
```

---

## Generated code

For each `service` in the proto, the generator produces a record type and a companion module:

```fsharp
type GreeterClient =
    { SayHello: HelloRequest -> Task<HelloReply> }

module GreeterClient =
    let create (address: string) : GreeterClient = ...
    let fromChannel (channel: GrpcChannel) : GreeterClient = ...
    let fromInvoker (invoker: CallInvoker) : GreeterClient = ...
```

---

## Creating a client

Three ways, from simplest to most flexible:

```fsharp
// From an address (most common)
let client = GreeterClient.create "https://localhost:5001"

// From a channel (custom configuration)
let channel = GrpcChannel.ForAddress("https://localhost:5001", GrpcChannelOptions(...))
let client = GreeterClient.fromChannel channel

// From a CallInvoker (testing with TestHost)
let client = GreeterClient.fromInvoker invoker
```

---

## All RPC patterns

### Unary

```protobuf
rpc SayHello (HelloRequest) returns (HelloReply);
```

Record field: `SayHello: HelloRequest -> Task<HelloReply>`

```fsharp
let! reply = client.SayHello { HelloRequest.empty with Name = "World" }
```

### Server streaming

```protobuf
rpc ServerStream (Request) returns (stream Item);
```

Record field: `ServerStream: Request -> AsyncServerStreamingCall<Item>`

```fsharp
let call = client.ServerStream { PingRequest.empty with Message = "go" }
while! call.ResponseStream.MoveNext(CancellationToken.None) do
    printfn "Got: %d" call.ResponseStream.Current.Value
```

### Client streaming

```protobuf
rpc ClientStream (stream Item) returns (Summary);
```

Record field: `ClientStream: unit -> AsyncClientStreamingCall<Item, Summary>`

```fsharp
let call = client.ClientStream()
for v in [ 10; 20; 30 ] do
    do! call.RequestStream.WriteAsync({ StreamItem.empty with Value = v })
do! call.RequestStream.CompleteAsync()
let! summary = call.ResponseAsync
```

### Bidirectional streaming

```protobuf
rpc BidiStream (stream Item) returns (stream Item);
```

Record field: `BidiStream: unit -> AsyncDuplexStreamingCall<Item, Item>`

```fsharp
let call = client.BidiStream()
for v in [ 1; 2; 3 ] do
    do! call.RequestStream.WriteAsync({ StreamItem.empty with Value = v })
do! call.RequestStream.CompleteAsync()
while! call.ResponseStream.MoveNext(CancellationToken.None) do
    printfn "Got: %d" call.ResponseStream.Current.Value
```

---

## Client record field types

| Pattern | Record field type |
|---|---|
| Unary | `Request -> Task<Response>` |
| Server streaming | `Request -> AsyncServerStreamingCall<Response>` |
| Client streaming | `unit -> AsyncClientStreamingCall<Request, Response>` |
| Bidi streaming | `unit -> AsyncDuplexStreamingCall<Request, Response>` |

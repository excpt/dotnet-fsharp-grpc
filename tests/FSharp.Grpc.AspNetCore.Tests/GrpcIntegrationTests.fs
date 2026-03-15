module FSharp.Grpc.AspNetCore.Tests.GrpcIntegrationTests

open System.Threading
open System.Threading.Tasks
open Xunit
open FsUnit.Xunit
open Grpc.Core
open Grpc.Net.Client
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection

// ---------------------------------------------------------
// Test server helper
// ---------------------------------------------------------

/// Start a test gRPC server with the given service and return a CallInvoker.
let private startServer<'TService when 'TService: not struct> (service: 'TService) =
    task {
        let builder = WebApplication.CreateBuilder()
        builder.WebHost.UseTestServer() |> ignore
        builder.Services.AddGrpc() |> ignore
        builder.Services.AddSingleton<'TService>(service) |> ignore
        let app = builder.Build()
        app.MapGrpcService<'TService>() |> ignore
        do! app.StartAsync()

        let handler = app.GetTestServer().CreateHandler()

        let channel =
            GrpcChannel.ForAddress("http://localhost", GrpcChannelOptions(HttpHandler = handler))

        return (app, channel.CreateCallInvoker())
    }

// ---------------------------------------------------------
// Greeter service (unary)
// ---------------------------------------------------------

[<Fact>]
let ``unary gRPC roundtrip`` () =
    task {
        let handlers: Greeter.GreeterHandlers =
            { SayHello =
                fun req ctx ->
                    task {
                        return
                            { Greeter.HelloReply.empty with
                                Message = $"Hello {req.Name}" }
                    } }

        let! (app, invoker) = startServer (Greeter.GreeterService(handlers))

        try
            let client = Greeter.GreeterClient.fromInvoker invoker

            let! reply =
                client.SayHello
                    { Greeter.HelloRequest.empty with
                        Name = "World" }

            reply.Message |> should equal "Hello World"
        finally
            app.StopAsync().Wait()
    }

[<Fact>]
let ``unary gRPC with empty request`` () =
    task {
        let handlers: Greeter.GreeterHandlers =
            { SayHello =
                fun req ctx ->
                    task {
                        return
                            { Greeter.HelloReply.empty with
                                Message = $"Hello {req.Name}" }
                    } }

        let! (app, invoker) = startServer (Greeter.GreeterService(handlers))

        try
            let client = Greeter.GreeterClient.fromInvoker invoker
            let! reply = client.SayHello Greeter.HelloRequest.empty

            reply.Message |> should equal "Hello "
        finally
            app.StopAsync().Wait()
    }

// ---------------------------------------------------------
// TestService (all 4 streaming patterns)
// ---------------------------------------------------------

[<Fact>]
let ``server streaming gRPC roundtrip`` () =
    task {
        let handlers: Testservice.TestServiceHandlers =
            { Ping = fun _ _ -> task { return Testservice.PingReply.empty }
              ServerStream =
                fun req stream ctx ->
                    task {
                        for i in 1..3 do
                            do!
                                stream.WriteAsync(
                                    { Testservice.StreamItem.empty with
                                        Value = i }
                                )
                    }
              ClientStream = fun _ _ -> task { return Testservice.Summary.empty }
              BidiStream = fun _ _ _ -> Task.CompletedTask }

        let! (app, invoker) = startServer (Testservice.TestServiceService(handlers))

        try
            let client = Testservice.TestServiceClient.fromInvoker invoker

            let call =
                client.ServerStream
                    { Testservice.PingRequest.empty with
                        Message = "go" }

            let items = ResizeArray()

            while! call.ResponseStream.MoveNext(CancellationToken.None) do
                items.Add(call.ResponseStream.Current)

            items.Count |> should equal 3
            items.[0].Value |> should equal 1
            items.[1].Value |> should equal 2
            items.[2].Value |> should equal 3
        finally
            app.StopAsync().Wait()
    }

[<Fact>]
let ``client streaming gRPC roundtrip`` () =
    task {
        let handlers: Testservice.TestServiceHandlers =
            { Ping = fun _ _ -> task { return Testservice.PingReply.empty }
              ServerStream = fun _ _ _ -> Task.CompletedTask
              ClientStream =
                fun stream ctx ->
                    task {
                        let mutable count = 0
                        let mutable total = 0

                        while! stream.MoveNext(CancellationToken.None) do
                            count <- count + 1
                            total <- total + stream.Current.Value

                        return
                            { Testservice.Summary.empty with
                                Count = count
                                Total = total }
                    }
              BidiStream = fun _ _ _ -> Task.CompletedTask }

        let! (app, invoker) = startServer (Testservice.TestServiceService(handlers))

        try
            let client = Testservice.TestServiceClient.fromInvoker invoker
            let call = client.ClientStream()

            for v in [ 10; 20; 30 ] do
                do!
                    call.RequestStream.WriteAsync(
                        { Testservice.StreamItem.empty with
                            Value = v }
                    )

            do! call.RequestStream.CompleteAsync()
            let! summary = call.ResponseAsync

            summary.Count |> should equal 3
            summary.Total |> should equal 60
        finally
            app.StopAsync().Wait()
    }

[<Fact>]
let ``bidi streaming gRPC roundtrip`` () =
    task {
        let handlers: Testservice.TestServiceHandlers =
            { Ping = fun _ _ -> task { return Testservice.PingReply.empty }
              ServerStream = fun _ _ _ -> Task.CompletedTask
              ClientStream = fun _ _ -> task { return Testservice.Summary.empty }
              BidiStream =
                fun reqStream resStream ctx ->
                    task {
                        while! reqStream.MoveNext(CancellationToken.None) do
                            // Echo back with value doubled
                            do!
                                resStream.WriteAsync(
                                    { Testservice.StreamItem.empty with
                                        Value = reqStream.Current.Value * 2 }
                                )
                    } }

        let! (app, invoker) = startServer (Testservice.TestServiceService(handlers))

        try
            let client = Testservice.TestServiceClient.fromInvoker invoker
            let call = client.BidiStream()

            // Write requests
            for v in [ 1; 2; 3 ] do
                do!
                    call.RequestStream.WriteAsync(
                        { Testservice.StreamItem.empty with
                            Value = v }
                    )

            do! call.RequestStream.CompleteAsync()

            // Read responses
            let items = ResizeArray()

            while! call.ResponseStream.MoveNext(CancellationToken.None) do
                items.Add(call.ResponseStream.Current)

            items.Count |> should equal 3
            items.[0].Value |> should equal 2
            items.[1].Value |> should equal 4
            items.[2].Value |> should equal 6
        finally
            app.StopAsync().Wait()
    }

[<Fact>]
let ``unary ping roundtrip`` () =
    task {
        let handlers: Testservice.TestServiceHandlers =
            { Ping =
                fun req ctx ->
                    task {
                        return
                            { Testservice.PingReply.empty with
                                Message = $"pong: {req.Message}" }
                    }
              ServerStream = fun _ _ _ -> Task.CompletedTask
              ClientStream = fun _ _ -> task { return Testservice.Summary.empty }
              BidiStream = fun _ _ _ -> Task.CompletedTask }

        let! (app, invoker) = startServer (Testservice.TestServiceService(handlers))

        try
            let client = Testservice.TestServiceClient.fromInvoker invoker

            let! reply =
                client.Ping
                    { Testservice.PingRequest.empty with
                        Message = "hello" }

            reply.Message |> should equal "pong: hello"
        finally
            app.StopAsync().Wait()
    }

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using CrossLangCSharp;

using Xunit;

using Fs = Crosslang;

namespace FSharp.Grpc.GrpcCrossLang.Tests;

/// <summary>
/// Tests F# gRPC server with C# gRPC client.
/// Verifies that a server using F#-generated service stubs (custom marshalling)
/// can be called by a standard C# gRPC client (Grpc.Tools-generated stubs).
/// </summary>
public class FSharpServerCSharpClientTests
{
    [Fact]
    public async Task Unary_FSharpServer_CSharpClient()
    {
        var service = Fs.Helpers.createEchoService();
        var (app, invoker) = await TestHelpers.StartServer(service);

        try
        {
            var client = new CrossLangService.CrossLangServiceClient(invoker);
            var reply = await client.SayHelloAsync(new HelloRequest { Name = "World" });

            Assert.Equal("Hello World", reply.Message);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Unary_EmptyRequest_FSharpServer_CSharpClient()
    {
        var service = Fs.Helpers.createEchoService();
        var (app, invoker) = await TestHelpers.StartServer(service);

        try
        {
            var client = new CrossLangService.CrossLangServiceClient(invoker);
            var reply = await client.SayHelloAsync(new HelloRequest());

            Assert.Equal("Hello ", reply.Message);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task ServerStreaming_FSharpServer_CSharpClient()
    {
        var service = Fs.Helpers.createEchoService();
        var (app, invoker) = await TestHelpers.StartServer(service);

        try
        {
            var client = new CrossLangService.CrossLangServiceClient(invoker);
            using var call = client.ServerStream(new HelloRequest { Name = "stream" });

            var items = new List<int>();
            while (await call.ResponseStream.MoveNext(CancellationToken.None))
            {
                items.Add(call.ResponseStream.Current.Value);
            }

            Assert.Equal(3, items.Count);
            Assert.Equal(new[] { 1, 2, 3 }, items.ToArray());
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task ClientStreaming_FSharpServer_CSharpClient()
    {
        var service = Fs.Helpers.createEchoService();
        var (app, invoker) = await TestHelpers.StartServer(service);

        try
        {
            var client = new CrossLangService.CrossLangServiceClient(invoker);
            using var call = client.ClientStream();

            foreach (var v in new[] { 10, 20, 30 })
            {
                await call.RequestStream.WriteAsync(new StreamItem { Value = v });
            }

            await call.RequestStream.CompleteAsync();
            var summary = await call;

            Assert.Equal(3, summary.Count);
            Assert.Equal(60, summary.Total);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task BidiStreaming_FSharpServer_CSharpClient()
    {
        var service = Fs.Helpers.createEchoService();
        var (app, invoker) = await TestHelpers.StartServer(service);

        try
        {
            var client = new CrossLangService.CrossLangServiceClient(invoker);
            using var call = client.BidiStream();

            foreach (var v in new[] { 1, 2, 3 })
            {
                await call.RequestStream.WriteAsync(new StreamItem { Value = v });
            }

            await call.RequestStream.CompleteAsync();

            var items = new List<int>();
            while (await call.ResponseStream.MoveNext(CancellationToken.None))
            {
                items.Add(call.ResponseStream.Current.Value);
            }

            Assert.Equal(3, items.Count);
            Assert.Equal(new[] { 2, 4, 6 }, items.ToArray());
        }
        finally
        {
            await app.StopAsync();
        }
    }
}

using System.Threading.Tasks;

using Xunit;

using Fs = Crosslang;

namespace Grpc.FSharp.GrpcCrossLang.Tests;

/// <summary>
/// Tests C# gRPC server with F# gRPC client.
/// Verifies that a server using standard Grpc.Tools-generated stubs
/// can be called by an F#-generated gRPC client (custom marshalling).
/// </summary>
public class CSharpServerFSharpClientTests
{
    [Fact]
    public async Task Unary_CSharpServer_FSharpClient()
    {
        var service = new CSharpCrossLangServiceImpl();
        var (app, invoker) = await TestHelpers.StartServer(service);

        try
        {
            var message = await Fs.Helpers.sayHelloAsync(invoker, "World");
            Assert.Equal("Hello World", message);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Unary_EmptyRequest_CSharpServer_FSharpClient()
    {
        var service = new CSharpCrossLangServiceImpl();
        var (app, invoker) = await TestHelpers.StartServer(service);

        try
        {
            var message = await Fs.Helpers.sayHelloAsync(invoker, "");
            Assert.Equal("Hello ", message);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task ServerStreaming_CSharpServer_FSharpClient()
    {
        var service = new CSharpCrossLangServiceImpl();
        var (app, invoker) = await TestHelpers.StartServer(service);

        try
        {
            var items = await Fs.Helpers.serverStreamCollect(invoker, "stream");
            Assert.Equal(new[] { 1, 2, 3 }, items);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task ClientStreaming_CSharpServer_FSharpClient()
    {
        var service = new CSharpCrossLangServiceImpl();
        var (app, invoker) = await TestHelpers.StartServer(service);

        try
        {
            var (count, total) = await Fs.Helpers.clientStreamSend(invoker, new[] { 10, 20, 30 });
            Assert.Equal(3, count);
            Assert.Equal(60, total);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task BidiStreaming_CSharpServer_FSharpClient()
    {
        var service = new CSharpCrossLangServiceImpl();
        var (app, invoker) = await TestHelpers.StartServer(service);

        try
        {
            var items = await Fs.Helpers.bidiStreamDoubled(invoker, new[] { 1, 2, 3 });
            Assert.Equal(new[] { 2, 4, 6 }, items);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}

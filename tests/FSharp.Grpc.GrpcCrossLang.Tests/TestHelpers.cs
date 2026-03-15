using System.Threading.Tasks;

using Grpc.Core;
using Grpc.Net.Client;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FSharp.Grpc.GrpcCrossLang.Tests;

internal static class TestHelpers
{
    public static async Task<(WebApplication App, CallInvoker Invoker)> StartServer<TService>(
        TService service) where TService : class
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGrpc();
        builder.Services.AddSingleton(service);
        var app = builder.Build();
        app.MapGrpcService<TService>();
        await app.StartAsync();

        var handler = app.GetTestServer().CreateHandler();
        var channel = GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions { HttpHandler = handler });

        return (app, channel.CreateCallInvoker());
    }
}

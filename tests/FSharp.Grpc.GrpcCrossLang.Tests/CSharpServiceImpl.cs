using System.Threading;
using System.Threading.Tasks;

using CrossLangCSharp;

using Grpc.Core;

namespace FSharp.Grpc.GrpcCrossLang.Tests;

/// <summary>
/// C# gRPC server implementation using standard Grpc.Tools-generated base class.
/// Used for testing: C# server + F# client.
/// </summary>
public class CSharpCrossLangServiceImpl : CrossLangService.CrossLangServiceBase
{
    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        return Task.FromResult(new HelloReply { Message = $"Hello {request.Name}" });
    }

    public override async Task ServerStream(
        HelloRequest request,
        IServerStreamWriter<StreamItem> responseStream,
        ServerCallContext context)
    {
        for (int i = 1; i <= 3; i++)
        {
            await responseStream.WriteAsync(new StreamItem { Value = i });
        }
    }

    public override async Task<Summary> ClientStream(
        IAsyncStreamReader<StreamItem> requestStream,
        ServerCallContext context)
    {
        int count = 0, total = 0;

        while (await requestStream.MoveNext(context.CancellationToken))
        {
            count++;
            total += requestStream.Current.Value;
        }

        return new Summary { Count = count, Total = total };
    }

    public override async Task BidiStream(
        IAsyncStreamReader<StreamItem> requestStream,
        IServerStreamWriter<StreamItem> responseStream,
        ServerCallContext context)
    {
        while (await requestStream.MoveNext(context.CancellationToken))
        {
            await responseStream.WriteAsync(new StreamItem { Value = requestStream.Current.Value * 2 });
        }
    }
}

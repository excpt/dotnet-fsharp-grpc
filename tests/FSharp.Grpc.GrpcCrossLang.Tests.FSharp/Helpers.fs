namespace Crosslang

open System.Threading
open System.Threading.Tasks
open Grpc.Core

/// Helper functions for cross-language testing.
/// Wraps F# generated server/client code in C#-friendly APIs.
module Helpers =

    /// Create an F# gRPC server that echoes back greetings,
    /// streams 1..3, aggregates client streams, and doubles bidi values.
    let createEchoService () : CrossLangServiceService =
        let handlers: CrossLangServiceHandlers =
            { SayHello =
                fun req ctx ->
                    task {
                        return
                            { HelloReply.empty with
                                Message = $"Hello {req.Name}" }
                    }
              ServerStream =
                fun req stream ctx ->
                    task {
                        for i in 1..3 do
                            do! stream.WriteAsync({ StreamItem.empty with Value = i })
                    }
              ClientStream =
                fun stream ctx ->
                    task {
                        let mutable count = 0
                        let mutable total = 0

                        while! stream.MoveNext(CancellationToken.None) do
                            count <- count + 1
                            total <- total + stream.Current.Value

                        return
                            { Summary.empty with
                                Count = count
                                Total = total }
                    }
              BidiStream =
                fun reqStream resStream ctx ->
                    task {
                        while! reqStream.MoveNext(CancellationToken.None) do
                            do!
                                resStream.WriteAsync(
                                    { StreamItem.empty with
                                        Value = reqStream.Current.Value * 2 }
                                )
                    } }

        CrossLangServiceService(handlers)

    /// Call F# client SayHello and return the reply message.
    let sayHelloAsync (invoker: CallInvoker) (name: string) : Task<string> =
        task {
            let client = CrossLangServiceClient.fromInvoker invoker
            let! reply = client.SayHello { HelloRequest.empty with Name = name }
            return reply.Message
        }

    /// Call F# client ServerStream and collect all streamed values.
    let serverStreamCollect (invoker: CallInvoker) (message: string) : Task<int array> =
        task {
            let client = CrossLangServiceClient.fromInvoker invoker

            let call =
                client.ServerStream
                    { HelloRequest.empty with
                        Name = message }

            let items = ResizeArray()

            while! call.ResponseStream.MoveNext(CancellationToken.None) do
                items.Add(call.ResponseStream.Current.Value)

            return items.ToArray()
        }

    /// Call F# client ClientStream, send values, return (count, total).
    let clientStreamSend (invoker: CallInvoker) (values: int array) : Task<struct (int * int)> =
        task {
            let client = CrossLangServiceClient.fromInvoker invoker
            let call = client.ClientStream()

            for v in values do
                do! call.RequestStream.WriteAsync({ StreamItem.empty with Value = v })

            do! call.RequestStream.CompleteAsync()
            let! summary = call.ResponseAsync
            return struct (summary.Count, summary.Total)
        }

    /// Call F# client BidiStream, send values, collect doubled responses.
    let bidiStreamDoubled (invoker: CallInvoker) (values: int array) : Task<int array> =
        task {
            let client = CrossLangServiceClient.fromInvoker invoker
            let call = client.BidiStream()

            for v in values do
                do! call.RequestStream.WriteAsync({ StreamItem.empty with Value = v })

            do! call.RequestStream.CompleteAsync()
            let items = ResizeArray()

            while! call.ResponseStream.MoveNext(CancellationToken.None) do
                items.Add(call.ResponseStream.Current.Value)

            return items.ToArray()
        }

namespace Crosslang

open Grpc.Core
open Grpc.Net.Client
open System.Threading.Tasks

type CrossLangServiceClient =
    { SayHello: HelloRequest -> Task<HelloReply>
      ServerStream: HelloRequest -> AsyncServerStreamingCall<StreamItem>
      ClientStream: unit -> AsyncClientStreamingCall<StreamItem, Summary>
      BidiStream: unit -> AsyncDuplexStreamingCall<StreamItem, StreamItem> }

module CrossLangServiceClient =
    let private sayHelloMethod =
        Method<HelloRequest, HelloReply>(
            MethodType.Unary,
            "crosslang.CrossLangService",
            "SayHello",
            Marshaller(System.Func<_, _>(HelloRequest.encode), System.Func<_, _>(HelloRequest.decode)),
            Marshaller(System.Func<_, _>(HelloReply.encode), System.Func<_, _>(HelloReply.decode))
        )

    let private serverStreamMethod =
        Method<HelloRequest, StreamItem>(
            MethodType.ServerStreaming,
            "crosslang.CrossLangService",
            "ServerStream",
            Marshaller(System.Func<_, _>(HelloRequest.encode), System.Func<_, _>(HelloRequest.decode)),
            Marshaller(System.Func<_, _>(StreamItem.encode), System.Func<_, _>(StreamItem.decode))
        )

    let private clientStreamMethod =
        Method<StreamItem, Summary>(
            MethodType.ClientStreaming,
            "crosslang.CrossLangService",
            "ClientStream",
            Marshaller(System.Func<_, _>(StreamItem.encode), System.Func<_, _>(StreamItem.decode)),
            Marshaller(System.Func<_, _>(Summary.encode), System.Func<_, _>(Summary.decode))
        )

    let private bidiStreamMethod =
        Method<StreamItem, StreamItem>(
            MethodType.DuplexStreaming,
            "crosslang.CrossLangService",
            "BidiStream",
            Marshaller(System.Func<_, _>(StreamItem.encode), System.Func<_, _>(StreamItem.decode)),
            Marshaller(System.Func<_, _>(StreamItem.encode), System.Func<_, _>(StreamItem.decode))
        )

    let fromInvoker (invoker: CallInvoker) : CrossLangServiceClient =
        { SayHello = fun request -> invoker.AsyncUnaryCall(sayHelloMethod, null, CallOptions(), request).ResponseAsync
          ServerStream =
            fun request -> invoker.AsyncServerStreamingCall(serverStreamMethod, null, CallOptions(), request)
          ClientStream = fun () -> invoker.AsyncClientStreamingCall(clientStreamMethod, null, CallOptions())
          BidiStream = fun () -> invoker.AsyncDuplexStreamingCall(bidiStreamMethod, null, CallOptions()) }

    let fromChannel (channel: GrpcChannel) : CrossLangServiceClient =
        fromInvoker (channel.CreateCallInvoker())

    let create (address: string) : CrossLangServiceClient =
        fromChannel (GrpcChannel.ForAddress(address))

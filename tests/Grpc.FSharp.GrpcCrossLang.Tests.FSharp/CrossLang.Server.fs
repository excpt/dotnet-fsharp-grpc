namespace Crosslang

open Grpc.Core
open System.Threading.Tasks

type CrossLangServiceHandlers =
    { SayHello: HelloRequest -> ServerCallContext -> Task<HelloReply>
      ServerStream: HelloRequest -> IServerStreamWriter<StreamItem> -> ServerCallContext -> Task
      ClientStream: IAsyncStreamReader<StreamItem> -> ServerCallContext -> Task<Summary>
      BidiStream: IAsyncStreamReader<StreamItem> -> IServerStreamWriter<StreamItem> -> ServerCallContext -> Task }

[<BindServiceMethod(typeof<CrossLangServiceService>, "BindService")>]
type CrossLangServiceService(handlers: CrossLangServiceHandlers) =
    static member private sayHelloMethod =
        Method<HelloRequest, HelloReply>(
            MethodType.Unary,
            "crosslang.CrossLangService",
            "SayHello",
            Marshaller(System.Func<_, _>(HelloRequest.encode), System.Func<_, _>(HelloRequest.decode)),
            Marshaller(System.Func<_, _>(HelloReply.encode), System.Func<_, _>(HelloReply.decode))
        )

    static member private serverStreamMethod =
        Method<HelloRequest, StreamItem>(
            MethodType.ServerStreaming,
            "crosslang.CrossLangService",
            "ServerStream",
            Marshaller(System.Func<_, _>(HelloRequest.encode), System.Func<_, _>(HelloRequest.decode)),
            Marshaller(System.Func<_, _>(StreamItem.encode), System.Func<_, _>(StreamItem.decode))
        )

    static member private clientStreamMethod =
        Method<StreamItem, Summary>(
            MethodType.ClientStreaming,
            "crosslang.CrossLangService",
            "ClientStream",
            Marshaller(System.Func<_, _>(StreamItem.encode), System.Func<_, _>(StreamItem.decode)),
            Marshaller(System.Func<_, _>(Summary.encode), System.Func<_, _>(Summary.decode))
        )

    static member private bidiStreamMethod =
        Method<StreamItem, StreamItem>(
            MethodType.DuplexStreaming,
            "crosslang.CrossLangService",
            "BidiStream",
            Marshaller(System.Func<_, _>(StreamItem.encode), System.Func<_, _>(StreamItem.decode)),
            Marshaller(System.Func<_, _>(StreamItem.encode), System.Func<_, _>(StreamItem.decode))
        )

    member _.Handlers = handlers
    abstract SayHello: request: HelloRequest * context: ServerCallContext -> Task<HelloReply>
    default _.SayHello(request: HelloRequest, context: ServerCallContext) = handlers.SayHello request context

    abstract ServerStream:
        request: HelloRequest * responseStream: IServerStreamWriter<StreamItem> * context: ServerCallContext -> Task

    default _.ServerStream
        (request: HelloRequest, responseStream: IServerStreamWriter<StreamItem>, context: ServerCallContext)
        =
        handlers.ServerStream request responseStream context

    abstract ClientStream: requestStream: IAsyncStreamReader<StreamItem> * context: ServerCallContext -> Task<Summary>

    default _.ClientStream(requestStream: IAsyncStreamReader<StreamItem>, context: ServerCallContext) =
        handlers.ClientStream requestStream context

    abstract BidiStream:
        requestStream: IAsyncStreamReader<StreamItem> *
        responseStream: IServerStreamWriter<StreamItem> *
        context: ServerCallContext ->
            Task

    default _.BidiStream
        (
            requestStream: IAsyncStreamReader<StreamItem>,
            responseStream: IServerStreamWriter<StreamItem>,
            context: ServerCallContext
        ) =
        handlers.BidiStream requestStream responseStream context

    static member BindService(binder: ServiceBinderBase, service: CrossLangServiceService) =
        binder.AddMethod(
            CrossLangServiceService.sayHelloMethod,
            UnaryServerMethod<HelloRequest, HelloReply>(fun req ctx -> service.SayHello(req, ctx))
        )

        binder.AddMethod(
            CrossLangServiceService.serverStreamMethod,
            ServerStreamingServerMethod<HelloRequest, StreamItem>(fun req stream ctx ->
                service.ServerStream(req, stream, ctx))
        )

        binder.AddMethod(
            CrossLangServiceService.clientStreamMethod,
            ClientStreamingServerMethod<StreamItem, Summary>(fun stream ctx -> service.ClientStream(stream, ctx))
        )

        binder.AddMethod(
            CrossLangServiceService.bidiStreamMethod,
            DuplexStreamingServerMethod<StreamItem, StreamItem>(fun reqStream resStream ctx ->
                service.BidiStream(reqStream, resStream, ctx))
        )

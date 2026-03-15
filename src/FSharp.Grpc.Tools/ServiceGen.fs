module FSharp.Grpc.Tools.ServiceGen

open System
open Google.Protobuf.Reflection
open FSharp.Grpc.Tools.TypeMapping
open Fabulous.AST
open Fantomas.Core.SyntaxOak
open type Fabulous.AST.Ast

/// Convert PascalCase to camelCase.
let private toCamelCase (name: string) =
    if String.IsNullOrEmpty(name) then
        name
    else
        (string (Char.ToLowerInvariant(name.[0]))) + name.[1..]

/// Resolve proto type name to simple F# type name.
let private resolveType (typeName: string) = simpleTypeName typeName

/// Determine the gRPC MethodType string.
let private methodType (m: MethodDescriptorProto) =
    match m.ClientStreaming, m.ServerStreaming with
    | false, false -> "MethodType.Unary"
    | false, true -> "MethodType.ServerStreaming"
    | true, false -> "MethodType.ClientStreaming"
    | true, true -> "MethodType.DuplexStreaming"

/// Full gRPC service name on the wire.
let private fullServiceName (file: FileDescriptorProto) (svc: ServiceDescriptorProto) =
    if String.IsNullOrEmpty(file.Package) then
        svc.Name
    else
        $"{file.Package}.{svc.Name}"

/// Generate handler function type annotation for a method.
let private handlerType (m: MethodDescriptorProto) =
    let req = resolveType m.InputType
    let res = resolveType m.OutputType

    match m.ClientStreaming, m.ServerStreaming with
    | false, false -> $"{req} -> ServerCallContext -> Task<{res}>"
    | false, true -> $"{req} -> IServerStreamWriter<{res}> -> ServerCallContext -> Task"
    | true, false -> $"IAsyncStreamReader<{req}> -> ServerCallContext -> Task<{res}>"
    | true, true -> $"IAsyncStreamReader<{req}> -> IServerStreamWriter<{res}> -> ServerCallContext -> Task"

/// Generate the delegate type for BindService.
let private delegateType (m: MethodDescriptorProto) =
    let req = resolveType m.InputType
    let res = resolveType m.OutputType

    match m.ClientStreaming, m.ServerStreaming with
    | false, false -> $"UnaryServerMethod<{req}, {res}>"
    | false, true -> $"ServerStreamingServerMethod<{req}, {res}>"
    | true, false -> $"ClientStreamingServerMethod<{req}, {res}>"
    | true, true -> $"DuplexStreamingServerMethod<{req}, {res}>"

/// Generate the client return type for a method.
let private clientReturnType (m: MethodDescriptorProto) =
    let req = resolveType m.InputType
    let res = resolveType m.OutputType

    match m.ClientStreaming, m.ServerStreaming with
    | false, false -> $"AsyncUnaryCall<{res}>"
    | false, true -> $"AsyncServerStreamingCall<{res}>"
    | true, false -> $"AsyncClientStreamingCall<{req}, {res}>"
    | true, true -> $"AsyncDuplexStreamingCall<{req}, {res}>"


/// Generate the CallInvoker method name for a client call.
let private clientInvokerMethod (m: MethodDescriptorProto) =
    match m.ClientStreaming, m.ServerStreaming with
    | false, false -> "AsyncUnaryCall"
    | false, true -> "AsyncServerStreamingCall"
    | true, false -> "AsyncClientStreamingCall"
    | true, true -> "AsyncDuplexStreamingCall"

/// Generate the client record field type for a method.
let private clientFieldType (m: MethodDescriptorProto) =
    let req = resolveType m.InputType
    let res = resolveType m.OutputType

    match m.ClientStreaming, m.ServerStreaming with
    | false, false -> $"{req} -> Task<{res}>"
    | false, true -> $"{req} -> AsyncServerStreamingCall<{res}>"
    | true, false -> $"unit -> AsyncClientStreamingCall<{req}, {res}>"
    | true, true -> $"unit -> AsyncDuplexStreamingCall<{req}, {res}>"

/// Generate the lambda expression for a fromInvoker record field.
let private fromInvokerFieldExpr (m: MethodDescriptorProto) =
    let funcName = toCamelCase m.Name
    let bindingName = $"{funcName}Method"
    let invokerMethod = clientInvokerMethod m

    match m.ClientStreaming, m.ServerStreaming with
    | false, false ->
        $"fun request -> invoker.{invokerMethod}({bindingName}, null, CallOptions(), request).ResponseAsync"
    | false, true -> $"fun request -> invoker.{invokerMethod}({bindingName}, null, CallOptions(), request)"
    | true, false
    | true, true -> $"fun () -> invoker.{invokerMethod}({bindingName}, null, CallOptions())"

/// Build the fromInvoker body expression string.
let private fromInvokerBody (methods: MethodDescriptorProto list) =
    methods
    |> List.map (fun m -> $"{m.Name} = {fromInvokerFieldExpr m}")
    |> String.concat "\n          "
    |> fun fields -> $"{{ {fields} }}"

/// Build a Method<,> descriptor expression string.
let private methodDescriptorExpr (file: FileDescriptorProto) (svc: ServiceDescriptorProto) (m: MethodDescriptorProto) =
    let req = resolveType m.InputType
    let res = resolveType m.OutputType
    let svcFullName = fullServiceName file svc

    $"Method<{req}, {res}>({methodType m}, \"{svcFullName}\", \"{m.Name}\", Marshaller(System.Func<_, _>({req}.encode), System.Func<_, _>({req}.decode)), Marshaller(System.Func<_, _>({res}.encode), System.Func<_, _>({res}.decode)))"

/// Build the abstract member parameter list for a method.
let private abstractParams (m: MethodDescriptorProto) =
    let req = resolveType m.InputType
    let res = resolveType m.OutputType

    match m.ClientStreaming, m.ServerStreaming with
    | false, false -> [ ("request", LongIdent req); ("context", LongIdent "ServerCallContext") ]
    | false, true ->
        [ ("request", LongIdent req)
          ("responseStream", LongIdent $"IServerStreamWriter<{res}>")
          ("context", LongIdent "ServerCallContext") ]
    | true, false ->
        [ ("requestStream", LongIdent $"IAsyncStreamReader<{req}>")
          ("context", LongIdent "ServerCallContext") ]
    | true, true ->
        [ ("requestStream", LongIdent $"IAsyncStreamReader<{req}>")
          ("responseStream", LongIdent $"IServerStreamWriter<{res}>")
          ("context", LongIdent "ServerCallContext") ]

/// Build the abstract member return type for a method.
let private abstractReturnType (m: MethodDescriptorProto) =
    let res = resolveType m.OutputType

    match m.ClientStreaming, m.ServerStreaming with
    | false, false -> LongIdent $"Task<{res}>"
    | false, true -> LongIdent "Task"
    | true, false -> LongIdent $"Task<{res}>"
    | true, true -> LongIdent "Task"

/// Build the default method parameter pattern for a method.
let private defaultParamPat (m: MethodDescriptorProto) =
    let req = resolveType m.InputType
    let res = resolveType m.OutputType

    let pats =
        match m.ClientStreaming, m.ServerStreaming with
        | false, false ->
            [ ParameterPat("request", LongIdent req)
              ParameterPat("context", LongIdent "ServerCallContext") ]
        | false, true ->
            [ ParameterPat("request", LongIdent req)
              ParameterPat("responseStream", LongIdent $"IServerStreamWriter<{res}>")
              ParameterPat("context", LongIdent "ServerCallContext") ]
        | true, false ->
            [ ParameterPat("requestStream", LongIdent $"IAsyncStreamReader<{req}>")
              ParameterPat("context", LongIdent "ServerCallContext") ]
        | true, true ->
            [ ParameterPat("requestStream", LongIdent $"IAsyncStreamReader<{req}>")
              ParameterPat("responseStream", LongIdent $"IServerStreamWriter<{res}>")
              ParameterPat("context", LongIdent "ServerCallContext") ]

    ParenPat(TuplePat(pats))

/// Generate the default method body that forwards to the handler.
let private defaultMethodBody (m: MethodDescriptorProto) (fieldName: string) =
    match m.ClientStreaming, m.ServerStreaming with
    | false, false -> $"handlers.{fieldName} request context"
    | false, true -> $"handlers.{fieldName} request responseStream context"
    | true, false -> $"handlers.{fieldName} requestStream context"
    | true, true -> $"handlers.{fieldName} requestStream responseStream context"

/// Build a delegate lambda expression string for BindService.
let private delegateLambda (m: MethodDescriptorProto) =
    match m.ClientStreaming, m.ServerStreaming with
    | false, false -> $"fun req ctx -> service.{m.Name}(req, ctx)"
    | false, true -> $"fun req stream ctx -> service.{m.Name}(req, stream, ctx)"
    | true, false -> $"fun stream ctx -> service.{m.Name}(stream, ctx)"
    | true, true -> $"fun reqStream resStream ctx -> service.{m.Name}(reqStream, resStream, ctx)"

/// Build the BindService body expression string.
let private bindServiceBody (serviceName: string) (methods: MethodDescriptorProto list) =
    methods
    |> List.map (fun m ->
        let bindingName = $"{toCamelCase m.Name}Method"
        $"binder.AddMethod({serviceName}.{bindingName}, {delegateType m}({delegateLambda m}))")
    |> String.concat "\n        "

/// Generate server code for all services in a file.
let generateServer (file: FileDescriptorProto) : WidgetBuilder<Oak> =
    // Collect all service-related widgets
    let serviceWidgets =
        [ for svc in file.Service do
              let handlersName = $"{svc.Name}Handlers"
              let serviceName = $"{svc.Name}Service"
              let methods = svc.Method |> Seq.toList

              // Handler record type
              yield
                  Choice1Of2(
                      Record(handlersName) {
                          for m in methods do
                              Field(m.Name, LongIdent(handlerType m))
                      }
                  )

              // Service class with attribute
              yield
                  Choice2Of2(
                      (TypeDefn(serviceName, Constructor(ParameterPat("handlers", LongIdent handlersName))) {
                          // Method descriptors (static member private)
                          for m in methods do
                              let bindingName = $"{toCamelCase m.Name}Method"

                              Member(bindingName, ConstantExpr(Constant(methodDescriptorExpr file svc m)))
                                  .toStatic()
                                  .toPrivate ()

                          // Handlers member
                          Member("_.Handlers", ConstantExpr(Constant("handlers")))

                          // Abstract + default method pairs
                          for m in methods do
                              AbstractMember(m.Name, abstractParams m, abstractReturnType m, true)

                              Default(
                                  $"_.{m.Name}",
                                  defaultParamPat m,
                                  ConstantExpr(Constant(defaultMethodBody m m.Name))
                              )

                          // BindService static method
                          Member(
                              "BindService",
                              ParenPat(
                                  TuplePat(
                                      [ ParameterPat("binder", LongIdent "ServiceBinderBase")
                                        ParameterPat("service", LongIdent serviceName) ]
                                  )
                              ),
                              ConstantExpr(Constant(bindServiceBody serviceName methods))
                          )
                              .toStatic ()
                      })
                          .attribute (
                              Attribute(
                                  "BindServiceMethod",
                                  ParenExpr(ConstantExpr(Constant($"typeof<{serviceName}>, \"BindService\"")))
                              )
                          )
                  ) ]

    match toNamespace file.Package with
    | Some ns ->
        Oak() {
            Namespace(ns) {
                Open("Grpc.Core")

                Open("System.Threading.Tasks")

                for w in serviceWidgets do
                    match w with
                    | Choice1Of2 r -> r
                    | Choice2Of2 t -> t
            }
        }
    | None ->
        Oak() {
            AnonymousModule() {
                Open("Grpc.Core")

                Open("System.Threading.Tasks")

                for w in serviceWidgets do
                    match w with
                    | Choice1Of2 r -> r
                    | Choice2Of2 t -> t
            }
        }

/// Generate client code for all services in a file.
let generateClient (file: FileDescriptorProto) : WidgetBuilder<Oak> =
    // Collect record types and companion modules for each service
    let clientWidgets =
        [ for svc in file.Service do
              let clientName = $"{svc.Name}Client"
              let methods = svc.Method |> Seq.toList

              // Client record type
              yield
                  Choice1Of2(
                      Record(clientName) {
                          for m in methods do
                              Field(m.Name, LongIdent(clientFieldType m))
                      }
                  )

              // Companion module
              yield
                  Choice2Of2(
                      Module(clientName) {
                          // Private method descriptors
                          for m in methods do
                              let bindingName = $"{toCamelCase m.Name}Method"

                              Value(bindingName, ConstantExpr(Constant(methodDescriptorExpr file svc m))).toPrivate ()

                          // fromInvoker: CallInvoker -> Client
                          Function(
                              "fromInvoker",
                              [ ParenPat(ParameterPat("invoker", LongIdent "CallInvoker")) ],
                              ConstantExpr(Constant(fromInvokerBody methods)),
                              LongIdent clientName
                          )

                          // fromChannel: GrpcChannel -> Client
                          Function(
                              "fromChannel",
                              [ ParenPat(ParameterPat("channel", LongIdent "GrpcChannel")) ],
                              ConstantExpr(Constant("fromInvoker (channel.CreateCallInvoker())")),
                              LongIdent clientName
                          )

                          // create: string -> Client
                          Function(
                              "create",
                              [ ParenPat(ParameterPat("address", LongIdent "string")) ],
                              ConstantExpr(Constant("fromChannel (GrpcChannel.ForAddress(address))")),
                              LongIdent clientName
                          )
                      }
                  ) ]

    match toNamespace file.Package with
    | Some ns ->
        Oak() {
            Namespace(ns) {
                Open("Grpc.Core")

                Open("Grpc.Net.Client")
                Open("System.Threading.Tasks")

                for w in clientWidgets do
                    match w with
                    | Choice1Of2 r -> r
                    | Choice2Of2 m -> m
            }
        }
    | None ->
        Oak() {
            AnonymousModule() {
                Open("Grpc.Core")

                Open("Grpc.Net.Client")
                Open("System.Threading.Tasks")

                for w in clientWidgets do
                    match w with
                    | Choice1Of2 r -> r
                    | Choice2Of2 m -> m
            }
        }

module Grpc.FSharp.Tools.Tests.ServiceGenTests

open System
open System.Diagnostics
open System.IO
open Xunit
open FsUnit.Xunit
open Google.Protobuf
open Google.Protobuf.Reflection
open Grpc.FSharp.Tools.ServiceGen
open Grpc.FSharp.Tools.ProtocResolver
open Fabulous.AST

// ---------------------------------------------------------
// Helpers
// ---------------------------------------------------------

let private expectedDir =
    let asmDir =
        Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)

    Path.Combine(asmDir, "expected")

let private assertMatchesExpected (name: string) (actual: string) =
    let path = Path.Combine(expectedDir, $"{name}.fs.expected")
    let expected = File.ReadAllText(path).ReplaceLineEndings("\n")
    actual.ReplaceLineEndings("\n") |> should equal expected

let private protosDir =
    let asmDir =
        Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)

    Path.Combine(asmDir, "protos")

let private findProtocOrFail () =
    findProtoc ()
    |> Option.defaultWith (fun () -> failwith "protoc not found — install Grpc.Tools NuGet package")

let private wellKnownTypesPath () =
    findWellKnownTypesPath ()
    |> Option.defaultWith (fun () -> failwith "well-known types path not found — install Grpc.Tools NuGet package")

/// Compile a .proto file to a FileDescriptorProto using protoc.
let private compileProto (protoPath: string) =
    let protoc = findProtocOrFail ()
    let wktPath = wellKnownTypesPath ()
    let tmpDescriptor = Path.GetTempFileName()

    try
        let psi = ProcessStartInfo(protoc)

        psi.Arguments <-
            $"--descriptor_set_out=\"{tmpDescriptor}\" --proto_path=\"{Path.GetDirectoryName protoPath}\" --proto_path=\"{wktPath}\" \"{Path.GetFileName protoPath}\""

        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false

        use proc = Process.Start(psi)
        let stderr = proc.StandardError.ReadToEnd()
        proc.WaitForExit()

        if proc.ExitCode <> 0 then
            failwith $"protoc failed (exit {proc.ExitCode}): {stderr}"

        let bytes = File.ReadAllBytes(tmpDescriptor)
        let descriptorSet = FileDescriptorSet.Parser.ParseFrom(bytes)
        descriptorSet.File.[0]
    finally
        if File.Exists tmpDescriptor then
            File.Delete tmpDescriptor

let private protoToServer (protoFileName: string) =
    Path.Combine(protosDir, protoFileName)
    |> compileProto
    |> generateServer
    |> Gen.mkOak
    |> Gen.run

let private protoToClient (protoFileName: string) =
    Path.Combine(protosDir, protoFileName)
    |> compileProto
    |> generateClient
    |> Gen.mkOak
    |> Gen.run

// ---------------------------------------------------------
// Server generation tests
// ---------------------------------------------------------

[<Fact>]
let ``simple unary service generates server stubs`` () =
    protoToServer "service_simple.proto"
    |> assertMatchesExpected "ServiceSimpleServer"

[<Fact>]
let ``all streaming patterns generate correct server stubs`` () =
    protoToServer "service_all_patterns.proto"
    |> assertMatchesExpected "ServiceAllPatternsServer"

// ---------------------------------------------------------
// Client generation tests
// ---------------------------------------------------------

[<Fact>]
let ``simple unary service generates client stubs`` () =
    protoToClient "service_simple.proto"
    |> assertMatchesExpected "ServiceSimpleClient"

[<Fact>]
let ``all streaming patterns generate correct client stubs`` () =
    protoToClient "service_all_patterns.proto"
    |> assertMatchesExpected "ServiceAllPatternsClient"

// ---------------------------------------------------------
// Structural verification tests
// ---------------------------------------------------------

[<Fact>]
let ``server code contains BindServiceMethod attribute`` () =
    let code = protoToServer "service_simple.proto"

    code
    |> should haveSubstring "[<BindServiceMethod(typeof<GreeterService>, \"BindService\")>]"

[<Fact>]
let ``server code contains correct wire service name`` () =
    let code = protoToServer "service_simple.proto"
    code |> should haveSubstring "\"greeter.Greeter\""

[<Fact>]
let ``server code contains correct method name`` () =
    let code = protoToServer "service_simple.proto"
    code |> should haveSubstring "\"SayHello\""

[<Fact>]
let ``server code uses correct MethodType for unary`` () =
    let code = protoToServer "service_simple.proto"
    code |> should haveSubstring "MethodType.Unary"

[<Fact>]
let ``server code uses correct MethodType for all patterns`` () =
    let code = protoToServer "service_all_patterns.proto"
    code |> should haveSubstring "MethodType.Unary"
    code |> should haveSubstring "MethodType.ServerStreaming"
    code |> should haveSubstring "MethodType.ClientStreaming"
    code |> should haveSubstring "MethodType.DuplexStreaming"

[<Fact>]
let ``server code references correct encode/decode functions`` () =
    let code = protoToServer "service_simple.proto"
    code |> should haveSubstring "HelloRequest.encode"
    code |> should haveSubstring "HelloRequest.decode"
    code |> should haveSubstring "HelloReply.encode"
    code |> should haveSubstring "HelloReply.decode"

[<Fact>]
let ``client code contains correct return types for all patterns`` () =
    let code = protoToClient "service_all_patterns.proto"
    // Record field types
    code |> should haveSubstring "PingRequest -> Task<PingReply>"

    code
    |> should haveSubstring "PingRequest -> AsyncServerStreamingCall<StreamItem>"

    code
    |> should haveSubstring "unit -> AsyncClientStreamingCall<StreamItem, Summary>"

    code
    |> should haveSubstring "unit -> AsyncDuplexStreamingCall<StreamItem, StreamItem>"

[<Fact>]
let ``client method descriptors match server`` () =
    let server = protoToServer "service_simple.proto"
    let client = protoToClient "service_simple.proto"
    // Both should have same wire service name and method name
    server |> should haveSubstring "\"greeter.Greeter\""
    client |> should haveSubstring "\"greeter.Greeter\""
    server |> should haveSubstring "\"SayHello\""
    client |> should haveSubstring "\"SayHello\""

[<Fact>]
let ``proto without services generates empty server code`` () =
    let code = protoToServer "person.proto"
    // Should have opens but no service types
    code |> should haveSubstring "open Grpc.Core"
    code |> should not' (haveSubstring "type ")

[<Fact>]
let ``proto without services generates empty client code`` () =
    let code = protoToClient "person.proto"
    code |> should haveSubstring "open Grpc.Core"
    code |> should not' (haveSubstring "module ")

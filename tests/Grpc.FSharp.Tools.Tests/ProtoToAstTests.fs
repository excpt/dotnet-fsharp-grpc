module Grpc.FSharp.Tools.Tests.ProtoToAstTests

open System
open System.Diagnostics
open System.IO
open Xunit
open FsUnit.Xunit
open Google.Protobuf
open Google.Protobuf.Reflection
open Grpc.FSharp.Tools.ProtoToAst
open Grpc.FSharp.Tools.ProtocResolver
open Fabulous.AST

// ---------------------------------------------------------
// Helpers
// ---------------------------------------------------------

let private generateFSharp (file: FileDescriptorProto) =
    file |> generate |> Gen.mkOak |> Gen.run

let private expectedDir =
    let asmDir =
        Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)

    Path.Combine(asmDir, "expected")

let private assertValidFSharpSyntax (code: string) =
    Fantomas.Core.CodeFormatter.IsValidFSharpCodeAsync(false, code)
    |> Async.RunSynchronously
    |> should be True

let private assertMatchesExpected (name: string) (actual: string) =
    assertValidFSharpSyntax actual
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

let private protoToFSharp (protoFileName: string) =
    Path.Combine(protosDir, protoFileName) |> compileProto |> generateFSharp

// ---------------------------------------------------------
// Tests
// ---------------------------------------------------------

[<Fact>]
let ``proto message generates F# record`` () =
    protoToFSharp "person.proto" |> assertMatchesExpected "PersonMessage"

[<Fact>]
let ``all scalar field types map to correct F# types`` () =
    protoToFSharp "scalar_types.proto" |> assertMatchesExpected "ScalarTypes"

[<Fact>]
let ``proto without package generates anonymous module`` () =
    protoToFSharp "no_package.proto" |> assertMatchesExpected "NoPackage"

[<Fact>]
let ``multiple messages generate multiple records`` () =
    protoToFSharp "multiple_messages.proto"
    |> assertMatchesExpected "MultipleMessages"

[<Fact>]
let ``enum type and enum field generate F# enum and typed record field`` () =
    protoToFSharp "enum_field.proto" |> assertMatchesExpected "EnumField"

[<Fact>]
let ``message field references another record type`` () =
    protoToFSharp "message_field.proto" |> assertMatchesExpected "MessageField"

[<Fact>]
let ``nested package maps to dotted F# namespace`` () =
    protoToFSharp "nested_package.proto" |> assertMatchesExpected "NestedPackage"

[<Fact>]
let ``nested message is flattened to top-level record`` () =
    protoToFSharp "nested_message.proto" |> assertMatchesExpected "NestedMessage"

[<Fact>]
let ``oneof generates discriminated union and typed record field`` () =
    protoToFSharp "oneof.proto" |> assertMatchesExpected "Oneof"

[<Fact>]
let ``repeated fields generate list types`` () =
    protoToFSharp "repeated_fields.proto" |> assertMatchesExpected "RepeatedFields"

[<Fact>]
let ``map fields generate Map types`` () =
    protoToFSharp "map_fields.proto" |> assertMatchesExpected "MapFields"

[<Fact>]
let ``proto3 optional fields generate option types`` () =
    protoToFSharp "optional_fields.proto" |> assertMatchesExpected "OptionalFields"

[<Fact>]
let ``message field generates option type`` () =
    protoToFSharp "message_ref.proto" |> assertMatchesExpected "MessageRef"

[<Fact>]
let ``complex proto with all features`` () =
    protoToFSharp "complex.proto" |> assertMatchesExpected "Complex"

[<Fact>]
let ``proto importing another file resolves cross-file types`` () =
    protoToFSharp "imports.proto" |> assertMatchesExpected "Imports"

[<Fact>]
let ``well-known types map to idiomatic F# types`` () =
    protoToFSharp "wellknown.proto" |> assertMatchesExpected "WellKnown"

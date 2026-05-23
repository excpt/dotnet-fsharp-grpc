module FSharp.Grpc.Tools.Tests.ProtoToAstTests

open System
open System.Diagnostics
open System.IO
open Xunit
open FsUnit.Xunit
open Google.Protobuf
open Google.Protobuf.Reflection
open FSharp.Grpc.Tools.ProtoToAst
open FSharp.Grpc.Tools.ProtocResolver
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

/// Snapshots that legitimately reference types from a sibling proto file
/// (which has its own generated .fs). Real consumers compile those side-by-side;
/// to type-check the snapshot in isolation we stub the dependency here.
let private typeCheckPrelude =
    Map.ofList
        [
          // Imports.fs.expected has `open Shared` and references Money — Money
          // lives in shared.proto's generated output (namespace Shared). Stub it.
          "Imports",
          """namespace Shared

type Money = { AmountMicros: int64; Currency: int }

module Money =
    let computeSize (_: Money) : int = 0
    let writeTo (_: Google.Protobuf.CodedOutputStream) (_: Money) : unit = ()
    let decodeFrom (_: Google.Protobuf.CodedInputStream) : Money = { AmountMicros = 0L; Currency = 0 }
    let writeJsonTo (_: System.Text.Json.Utf8JsonWriter) (_: Money) : unit = ()
    let decodeJsonElement (_: System.Text.Json.JsonElement) : Money = { AmountMicros = 0L; Currency = 0 }
""" ]

/// Snapshots whose generated source lacks a top-level namespace/module declaration.
/// Fix #7 removed the previous "NoPackage" entry; the generator now emits
/// `namespace {FileName}` for protos without a package.
let private wrapInModule: Set<string> = Set.empty

/// Snapshots skipped from type-check because of known generator bugs that can't
/// be worked around with a prelude. The snapshot-equality test still runs, so
/// regressions in the *current* (buggy) output are still caught.
let private typeCheckSkip =
    Map.ofList
        [
          // Tracked in issue #13 (nested-type flattening doesn't disambiguate
          // same-name types in different parents). NestedCollision.proto's purpose
          // is to pin the FQN-keyed forward-ref detection (`namespace Test`
          // without `rec` proves no false-positive); the flattening collision is
          // orthogonal — both `Foo.Inner` and `Bar.Inner` emit as `type Inner`
          // at namespace scope, producing a duplicate type definition.
          "NestedCollision", "see issue #13: nested-type flattening name collision" ]

let private assertTypeChecks (name: string) (actual: string) =
    match Map.tryFind name typeCheckSkip with
    | Some _reason -> ()
    | None ->
        let source =
            match Map.tryFind name typeCheckPrelude with
            | Some prelude -> prelude + "\n" + actual
            | None ->
                if Set.contains name wrapInModule then
                    $"module {name}\n\n{actual}"
                else
                    actual

        match SnapshotTypeCheck.typeCheck name source with
        | None -> ()
        | Some errors -> Assert.Fail($"Generated F# for {name}.fs.expected does not type-check:\n{errors}")

let private assertMatchesExpected (name: string) (actual: string) =
    assertValidFSharpSyntax actual
    let path = Path.Combine(expectedDir, $"{name}.fs.expected")
    let expected = File.ReadAllText(path).ReplaceLineEndings("\n")
    actual.ReplaceLineEndings("\n") |> should equal expected
    assertTypeChecks name actual

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
let ``proto without package generates namespace from file name`` () =
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
let ``oneof with message-typed cases generates length-delimited decode (issue #4)`` () =
    protoToFSharp "oneof_messages.proto" |> assertMatchesExpected "OneofMessages"

[<Fact>]
let ``oneof with well-known and wrapper types generates correct serialization`` () =
    protoToFSharp "oneof_wellknown.proto" |> assertMatchesExpected "OneofWellKnown"

[<Fact>]
let ``proto3 optional message field generates option type`` () =
    protoToFSharp "optional_message.proto"
    |> assertMatchesExpected "OptionalMessage"

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
let ``nested types with same simple name in different parents do not collide in forward-ref detection`` () =
    // Regression: positionByFqn must be keyed on fully-qualified names. With a
    // simple-name key, both `Foo.Inner` and `Bar.Inner` would collide
    // (last-write-wins), and `Foo.localRef: Inner` would resolve to Bar.Inner's
    // position — flagging a self-targeting back-ref as a forward ref and
    // emitting an unnecessary `namespace rec`. The expected snapshot has plain
    // `namespace test` (no `rec`); a regression would emit `rec`.
    protoToFSharp "nested_collision.proto"
    |> assertMatchesExpected "NestedCollision"

[<Fact>]
let ``proto importing another file resolves cross-file types`` () =
    protoToFSharp "imports.proto" |> assertMatchesExpected "Imports"

[<Fact>]
let ``well-known types map to idiomatic F# types`` () =
    protoToFSharp "wellknown.proto" |> assertMatchesExpected "WellKnown"

// ---------------------------------------------------------
// Sanity check for the type-check helper itself: confirm it
// rejects code with a deliberate type error. Guards against
// the helper silently passing everything (which would make
// the snapshot type-check verification useless).
// ---------------------------------------------------------

[<Fact>]
let ``type-check helper rejects code with type errors`` () =
    let badCode =
        """namespace Test
type Foo = { X: int }
module Foo =
    let bad: string = 42
"""

    match SnapshotTypeCheck.typeCheck "TypeErrorSmoke" badCode with
    | Some _ -> ()
    | None -> Assert.Fail("type-check helper failed to catch int-to-string assignment")

[<Fact>]
let ``type-check helper accepts valid code`` () =
    let goodCode =
        """namespace Test
type Foo = { X: int }
module Foo =
    let make (v: int) : Foo = { X = v }
"""

    match SnapshotTypeCheck.typeCheck "TypeOkSmoke" goodCode with
    | None -> ()
    | Some errors -> Assert.Fail($"type-check helper rejected valid code:\n{errors}")

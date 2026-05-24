module FSharp.Grpc.Tools.Tests.ProtocResolverTests

open Xunit
open FsUnit.Xunit
open FSharp.Grpc.Tools.ProtocResolver

// pickLatestVersionDir works on absolute paths returned by Directory.GetDirectories,
// so the tests pass paths with a common prefix — only the last segment varies.
let private dirs (names: string list) =
    names |> List.map (fun n -> $"/cache/grpc.tools/{n}")

[<Fact>]
let ``picks 2.10.0 over 2.9.0 (lexicographic sort would invert this)`` () =
    dirs [ "2.9.0"; "2.10.0" ]
    |> pickLatestVersionDir
    |> should equal (Some "/cache/grpc.tools/2.10.0")

[<Fact>]
let ``picks the numerically highest version across a realistic Grpc.Tools range`` () =
    dirs [ "2.55.0"; "2.71.0"; "2.9.0"; "2.10.1"; "2.60.0" ]
    |> pickLatestVersionDir
    |> should equal (Some "/cache/grpc.tools/2.71.0")

[<Fact>]
let ``prefers release over pre-release at the same version`` () =
    dirs [ "2.71.0-pre1"; "2.71.0" ]
    |> pickLatestVersionDir
    |> should equal (Some "/cache/grpc.tools/2.71.0")

[<Fact>]
let ``picks pre-release when it is the only candidate at the highest version`` () =
    dirs [ "2.70.0"; "2.71.0-pre1" ]
    |> pickLatestVersionDir
    |> should equal (Some "/cache/grpc.tools/2.71.0-pre1")

[<Fact>]
let ``ignores directories whose names are not numeric SemVer`` () =
    dirs [ "2.10.0"; "garbage"; ".staging"; "2.71.0" ]
    |> pickLatestVersionDir
    |> should equal (Some "/cache/grpc.tools/2.71.0")

[<Fact>]
let ``returns None when nothing parses`` () =
    dirs [ "garbage"; ".staging" ] |> pickLatestVersionDir |> should equal None

[<Fact>]
let ``returns None on an empty sequence`` () =
    Seq.empty<string> |> pickLatestVersionDir |> should equal None

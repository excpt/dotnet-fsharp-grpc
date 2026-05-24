module FSharp.Grpc.Tools.Tests.ProgramTests

open Xunit
open FsUnit.Xunit
open FSharp.Grpc.Tools.Pipeline
open FSharp.Grpc.Tools.Codegen.Program

let private expectOk (result: Result<'a, string>) : 'a =
    match result with
    | Ok v -> v
    | Error e -> failwithf "expected Ok, got Error %s" e

let private expectErr (result: Result<'a, string>) : string =
    match result with
    | Error e -> e
    | Ok v -> failwithf "expected Error, got Ok %A" v

[<Fact>]
let ``parseGrpcServices accepts each known value`` () =
    parseGrpcServices "None" |> expectOk |> should equal GrpcServices.None
    parseGrpcServices "Server" |> expectOk |> should equal Server
    parseGrpcServices "Client" |> expectOk |> should equal Client
    parseGrpcServices "Both" |> expectOk |> should equal Both

[<Fact>]
let ``parseGrpcServices returns Error for a typo, naming the bad value`` () =
    let msg = parseGrpcServices "Bot" |> expectErr
    msg |> should haveSubstring "Bot"
    msg |> should haveSubstring "None"
    msg |> should haveSubstring "Server"
    msg |> should haveSubstring "Client"
    msg |> should haveSubstring "Both"

[<Fact>]
let ``parseGrpcServices is case-sensitive — lowercase is rejected`` () =
    parseGrpcServices "server" |> expectErr |> ignore

[<Fact>]
let ``parseArgs surfaces a --grpc-services typo through Errors`` () =
    let args = parseArgs [| "--grpc-services"; "Bot"; "foo.proto" |]
    args.Errors |> List.length |> should equal 1
    args.Errors.[0] |> should haveSubstring "Bot"

[<Fact>]
let ``parseArgs leaves Errors empty for a valid --grpc-services value`` () =
    let args = parseArgs [| "--grpc-services"; "Server"; "foo.proto" |]
    args.Errors |> should be Empty
    args.DefaultServices |> should equal Server

[<Fact>]
let ``parsePositional returns Ok with default services when no suffix is given`` () =
    let spec = parsePositional Server "foo.proto" |> expectOk
    spec.File |> should equal "foo.proto"
    spec.Services |> should equal Server

[<Fact>]
let ``parsePositional returns Ok with default services when suffix is empty`` () =
    let spec = parsePositional Client "foo.proto|" |> expectOk
    spec.File |> should equal "foo.proto"
    spec.Services |> should equal Client

[<Fact>]
let ``parsePositional parses a valid per-file services suffix`` () =
    let spec = parsePositional GrpcServices.None "foo.proto|Both" |> expectOk
    spec.File |> should equal "foo.proto"
    spec.Services |> should equal Both

[<Fact>]
let ``parsePositional returns Error when the per-file services suffix is a typo`` () =
    let msg = parsePositional GrpcServices.None "foo.proto|Bot" |> expectErr
    msg |> should haveSubstring "Bot"

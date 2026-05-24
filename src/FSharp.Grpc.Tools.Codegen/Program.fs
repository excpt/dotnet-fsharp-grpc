module FSharp.Grpc.Tools.Codegen.Program

open System.IO
open FSharp.Grpc.Tools.ProtocResolver
open FSharp.Grpc.Tools.Pipeline

type Args =
    { Protoc: string option
      ProtoPaths: string list
      OutputDir: string
      Positionals: string list
      DefaultServices: GrpcServices
      Errors: string list }

let parseGrpcServices (value: string) : Result<GrpcServices, string> =
    match value with
    | "None" -> Ok None
    | "Server" -> Ok Server
    | "Client" -> Ok Client
    | "Both" -> Ok Both
    | _ -> Error $"unknown GrpcServices value '{value}'. Expected one of: None, Server, Client, Both."

// Positional args use "<path>|<services>" to attach per-file services.
// If the suffix is absent or empty, the global --grpc-services default applies.
// '|' is safe — no platform's path syntax uses it.
let parsePositional (defaultServices: GrpcServices) (raw: string) : Result<ProtoSpec, string> =
    let idx = raw.IndexOf('|')

    if idx < 0 then
        Ok
            { File = raw
              Services = defaultServices }
    else
        let file = raw.Substring(0, idx)
        let svc = raw.Substring(idx + 1)

        if svc = "" then
            Ok
                { File = file
                  Services = defaultServices }
        else
            parseGrpcServices svc
            |> Result.map (fun services -> { File = file; Services = services })

let parseArgs (argv: string array) =
    let mutable args =
        { Protoc = Option.None
          ProtoPaths = []
          OutputDir = "."
          Positionals = []
          DefaultServices = None
          Errors = [] }

    let mutable i = 0

    while i < argv.Length do
        match argv.[i] with
        | "--protoc" when i + 1 < argv.Length ->
            args <- { args with Protoc = Some argv.[i + 1] }
            i <- i + 2
        | "--proto-path"
        | "-I" when i + 1 < argv.Length ->
            args <-
                { args with
                    ProtoPaths = args.ProtoPaths @ [ argv.[i + 1] ] }

            i <- i + 2
        | "--output-dir"
        | "-o" when i + 1 < argv.Length ->
            args <- { args with OutputDir = argv.[i + 1] }
            i <- i + 2
        | "--grpc-services" when i + 1 < argv.Length ->
            args <-
                match parseGrpcServices argv.[i + 1] with
                | Ok v -> { args with DefaultServices = v }
                | Error e ->
                    { args with
                        Errors = args.Errors @ [ e ] }

            i <- i + 2
        | arg when not (arg.StartsWith("-")) ->
            args <-
                { args with
                    Positionals = args.Positionals @ [ arg ] }

            i <- i + 1
        | _ -> i <- i + 1

    args

[<EntryPoint>]
let main argv =
    let args = parseArgs argv

    let positionalResults =
        args.Positionals |> List.map (parsePositional args.DefaultServices)

    let positionalErrors =
        positionalResults
        |> List.choose (function
            | Error e -> Some e
            | _ -> Option.None)

    let allErrors = args.Errors @ positionalErrors

    if List.isEmpty args.Positionals then
        eprintfn
            "Usage: grpc-fsharp-codegen [--protoc <path>] [--proto-path <dir>]... [--output-dir <dir>] [--grpc-services None|Server|Client|Both] <file.proto[|Services]>..."

        1
    elif not (List.isEmpty allErrors) then
        for e in allErrors do
            eprintfn "%s" e

        1
    else
        let protocPath =
            match args.Protoc with
            | Some p -> Some p
            | Option.None -> findProtoc ()

        match protocPath with
        | Option.None ->
            eprintfn "Could not find protoc. Install Grpc.Tools NuGet or pass --protoc <path>."
            1
        | Some protoc ->
            let wktPaths =
                match findWellKnownTypesPath () with
                | Some p -> [ p ]
                | Option.None ->
                    let protocDir = Path.GetDirectoryName(protoc)
                    let candidate = Path.Combine(protocDir, "..", "..", "build", "native", "include")

                    if Directory.Exists candidate then
                        [ Path.GetFullPath candidate ]
                    else
                        []

            let protos =
                positionalResults
                |> List.choose (function
                    | Ok p -> Some p
                    | _ -> Option.None)

            let protoFileDirs =
                protos
                |> List.map (fun s -> Path.GetFullPath(Path.GetDirectoryName(s.File)))
                |> List.distinct

            let allProtoPaths = args.ProtoPaths @ wktPaths @ protoFileDirs |> List.distinct

            // Pipeline resolves files via --proto-path, so strip directory from each spec
            let protosForPipeline =
                protos
                |> List.map (fun s ->
                    { s with
                        File = Path.GetFileName(s.File) })

            let runResult =
                run
                    { Protoc = protoc
                      ProtoPaths = allProtoPaths
                      OutputDir = args.OutputDir
                      Protos = protosForPipeline }

            match runResult with
            | Error msg ->
                eprintfn "%s" msg
                1
            | Ok result ->
                for p in result.MessageFiles @ result.StubFiles do
                    printfn "%s" p

                printfn "%s" result.ManifestPath
                0

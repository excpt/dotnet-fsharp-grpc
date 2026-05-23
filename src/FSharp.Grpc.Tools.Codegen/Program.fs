module FSharp.Grpc.Tools.Codegen.Program

open System.IO
open FSharp.Grpc.Tools.ProtocResolver
open FSharp.Grpc.Tools.Pipeline

type private Args =
    { Protoc: string option
      ProtoPaths: string list
      OutputDir: string
      ProtoFiles: string list
      GrpcServices: GrpcServices }

let private parseGrpcServices =
    function
    | "Server" -> Server
    | "Client" -> Client
    | "Both" -> Both
    | _ -> None

let private parseArgs (argv: string array) =
    let mutable args =
        { Protoc = Option.None
          ProtoPaths = []
          OutputDir = "."
          ProtoFiles = []
          GrpcServices = None }

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
                { args with
                    GrpcServices = parseGrpcServices argv.[i + 1] }

            i <- i + 2
        | arg when not (arg.StartsWith("-")) ->
            args <-
                { args with
                    ProtoFiles = args.ProtoFiles @ [ arg ] }

            i <- i + 1
        | _ -> i <- i + 1

    args

[<EntryPoint>]
let main argv =
    let args = parseArgs argv

    if List.isEmpty args.ProtoFiles then
        eprintfn
            "Usage: grpc-fsharp-codegen [--protoc <path>] [--proto-path <dir>]... [--output-dir <dir>] [--grpc-services None|Server|Client|Both] <file.proto>..."

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

            let protoFileDirs =
                args.ProtoFiles
                |> List.map (fun f -> Path.GetFullPath(Path.GetDirectoryName(f)))
                |> List.distinct

            let allProtoPaths = args.ProtoPaths @ wktPaths @ protoFileDirs |> List.distinct
            let protoFileNames = args.ProtoFiles |> List.map Path.GetFileName

            let result =
                run
                    { Protoc = protoc
                      ProtoPaths = allProtoPaths
                      OutputDir = args.OutputDir
                      ProtoFiles = protoFileNames
                      GrpcServices = args.GrpcServices }

            for p in result.MessageFiles @ result.StubFiles do
                printfn "%s" p

            printfn "%s" result.ManifestPath
            0

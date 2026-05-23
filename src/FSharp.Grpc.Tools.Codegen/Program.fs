module FSharp.Grpc.Tools.Codegen.Program

open System.IO
open FSharp.Grpc.Tools.ProtocResolver
open FSharp.Grpc.Tools.Pipeline

type private Args =
    { Protoc: string option
      ProtoPaths: string list
      OutputDir: string
      Positionals: string list
      DefaultServices: GrpcServices }

let private parseGrpcServices =
    function
    | "Server" -> Server
    | "Client" -> Client
    | "Both" -> Both
    | _ -> None

// Positional args use "<path>|<services>" to attach per-file services.
// If the suffix is absent or empty, the global --grpc-services default applies.
// '|' is safe — no platform's path syntax uses it.
let private parsePositional (defaultServices: GrpcServices) (raw: string) : ProtoSpec =
    let idx = raw.IndexOf('|')

    if idx < 0 then
        { File = raw
          Services = defaultServices }
    else
        let file = raw.Substring(0, idx)
        let svc = raw.Substring(idx + 1)

        let services = if svc = "" then defaultServices else parseGrpcServices svc

        { File = file; Services = services }

let private parseArgs (argv: string array) =
    let mutable args =
        { Protoc = Option.None
          ProtoPaths = []
          OutputDir = "."
          Positionals = []
          DefaultServices = None }

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
                    DefaultServices = parseGrpcServices argv.[i + 1] }

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

    if List.isEmpty args.Positionals then
        eprintfn
            "Usage: grpc-fsharp-codegen [--protoc <path>] [--proto-path <dir>]... [--output-dir <dir>] [--grpc-services None|Server|Client|Both] <file.proto[|Services]>..."

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

            let protos = args.Positionals |> List.map (parsePositional args.DefaultServices)

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

            let result =
                run
                    { Protoc = protoc
                      ProtoPaths = allProtoPaths
                      OutputDir = args.OutputDir
                      Protos = protosForPipeline }

            for p in result.MessageFiles @ result.StubFiles do
                printfn "%s" p

            printfn "%s" result.ManifestPath
            0

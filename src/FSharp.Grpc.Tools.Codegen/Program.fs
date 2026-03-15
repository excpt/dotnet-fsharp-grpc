module FSharp.Grpc.Tools.Codegen.Program

open System
open System.Diagnostics
open System.IO
open Google.Protobuf.Reflection
open FSharp.Grpc.Tools.ProtoToAst
open FSharp.Grpc.Tools.ProtocResolver
open FSharp.Grpc.Tools.ServiceGen
open Fabulous.AST

/// Compile .proto files to a FileDescriptorSet using protoc.
let private compileProtos (protocPath: string) (protoPaths: string list) (protoFiles: string list) =
    let tmpDescriptor = Path.GetTempFileName()

    try
        let protoPathArgs =
            protoPaths |> List.map (fun p -> $"--proto_path=\"{p}\"") |> String.concat " "

        let fileArgs = protoFiles |> List.map (fun f -> $"\"{f}\"") |> String.concat " "

        let psi = ProcessStartInfo(protocPath)
        psi.Arguments <- $"--descriptor_set_out=\"{tmpDescriptor}\" --include_imports {protoPathArgs} {fileArgs}"
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false

        use proc = Process.Start(psi)
        let stderr = proc.StandardError.ReadToEnd()
        proc.WaitForExit()

        if proc.ExitCode <> 0 then
            eprintfn "protoc failed (exit %d): %s" proc.ExitCode stderr
            None
        else
            let bytes = File.ReadAllBytes(tmpDescriptor)
            Some(FileDescriptorSet.Parser.ParseFrom(bytes))
    finally
        if File.Exists tmpDescriptor then
            File.Delete tmpDescriptor

type GrpcServices =
    | None
    | Server
    | Client
    | Both

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
        // Resolve protoc
        let protocPath =
            match args.Protoc with
            | Some p -> Some p
            | Option.None -> findProtoc ()

        match protocPath with
        | Option.None ->
            eprintfn "Could not find protoc. Install Grpc.Tools NuGet or pass --protoc <path>."
            1
        | Some protoc ->

            // Build proto paths: user-provided + WKT + proto file directories
            let wktPaths =
                match findWellKnownTypesPath () with
                | Some p -> [ p ]
                | Option.None ->
                    // Try deriving WKT path from protoc location
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

            // Resolve proto file paths relative to proto paths
            let protoFileNames = args.ProtoFiles |> List.map (fun f -> Path.GetFileName(f))

            // Ensure output dir exists
            Directory.CreateDirectory(args.OutputDir) |> ignore

            // Compile and generate
            match compileProtos protoc allProtoPaths protoFileNames with
            | Option.None -> 1
            | Some descriptorSet ->
                let inputNames = args.ProtoFiles |> List.map Path.GetFileName |> Set.ofList

                for file in descriptorSet.File do
                    // Only generate for the requested files, not their imports
                    if inputNames |> Set.contains file.Name then
                        // Always generate messages
                        let code = file |> generate |> Gen.mkOak |> Gen.run
                        let baseName = Path.GetFileNameWithoutExtension(file.Name)
                        let outputPath = Path.Combine(args.OutputDir, baseName + ".generated.fs")
                        File.WriteAllText(outputPath, code)
                        // Print to stdout for MSBuild to capture
                        printfn "%s" outputPath

                        // Conditionally generate server stubs
                        if
                            (args.GrpcServices = Server || args.GrpcServices = Both)
                            && file.Service.Count > 0
                        then
                            let serverCode = generateServer file |> Gen.mkOak |> Gen.run
                            let serverPath = Path.Combine(args.OutputDir, baseName + ".server.generated.fs")
                            File.WriteAllText(serverPath, serverCode)
                            printfn "%s" serverPath

                        // Conditionally generate client stubs
                        if
                            (args.GrpcServices = Client || args.GrpcServices = Both)
                            && file.Service.Count > 0
                        then
                            let clientCode = generateClient file |> Gen.mkOak |> Gen.run
                            let clientPath = Path.Combine(args.OutputDir, baseName + ".client.generated.fs")
                            File.WriteAllText(clientPath, clientCode)
                            printfn "%s" clientPath

                0

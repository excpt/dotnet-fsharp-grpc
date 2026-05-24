module FSharp.Grpc.Tools.Pipeline

open System.Diagnostics
open System.IO
open Google.Protobuf.Reflection
open FSharp.Grpc.Tools.ProtoToAst
open FSharp.Grpc.Tools.ServiceGen
open Fabulous.AST

type GrpcServices =
    | None
    | Server
    | Client
    | Both

type ProtoSpec =
    { File: string; Services: GrpcServices }

type Options =
    { Protoc: string
      ProtoPaths: string list
      OutputDir: string
      Protos: ProtoSpec list }

type Result =
    { MessageFiles: string list
      StubFiles: string list
      ManifestPath: string }

let private runProtoc (protocPath: string) (protoPaths: string list) (protoFiles: string list) =
    let tmp = Path.GetTempFileName()

    try
        let psi = ProcessStartInfo(protocPath)
        psi.ArgumentList.Add($"--descriptor_set_out={tmp}")
        psi.ArgumentList.Add("--include_imports")

        for p in protoPaths do
            psi.ArgumentList.Add($"--proto_path={p}")

        for f in protoFiles do
            psi.ArgumentList.Add(f)

        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false

        use proc = Process.Start(psi)
        let stderr = proc.StandardError.ReadToEnd()
        proc.WaitForExit()

        if proc.ExitCode <> 0 then
            Error $"protoc failed (exit {proc.ExitCode}): {stderr}"
        else
            File.ReadAllBytes(tmp) |> FileDescriptorSet.Parser.ParseFrom |> Ok
    finally
        if File.Exists tmp then
            File.Delete tmp

let private writeManifest (outputDir: string) (orderedPaths: string list) =
    let manifestPath = Path.Combine(outputDir, "FSharpGrpc.GeneratedFiles.props")

    let lines =
        seq {
            "<Project>"
            "  <ItemGroup>"

            for p in orderedPaths do
                $"    <Compile Include=\"{p}\" />"

            "  </ItemGroup>"
            "</Project>"
        }

    File.WriteAllLines(manifestPath, lines)
    manifestPath

let run (opts: Options) : Result =
    let protoFiles = opts.Protos |> List.map (fun s -> s.File)

    match runProtoc opts.Protoc opts.ProtoPaths protoFiles with
    | Error msg -> failwith msg
    | Ok descriptorSet ->
        Directory.CreateDirectory(opts.OutputDir) |> ignore

        let servicesByName =
            opts.Protos
            |> List.map (fun s -> Path.GetFileName s.File, s.Services)
            |> Map.ofList

        let messages = ResizeArray<string>()
        let stubs = ResizeArray<string>()

        // protoc emits descriptor files in topological order (deps before dependents)
        for file in descriptorSet.File do
            match Map.tryFind file.Name servicesByName with
            | Option.None -> ()
            | Some services ->
                let baseName = Path.GetFileNameWithoutExtension(file.Name)

                let msgPath = Path.Combine(opts.OutputDir, baseName + ".generated.fs")
                File.WriteAllText(msgPath, file |> generate |> Gen.mkOak |> Gen.run)
                messages.Add(msgPath)

                let wantsServer = services = Server || services = Both
                let wantsClient = services = Client || services = Both

                if wantsServer && file.Service.Count > 0 then
                    let p = Path.Combine(opts.OutputDir, baseName + ".server.generated.fs")
                    File.WriteAllText(p, generateServer file |> Gen.mkOak |> Gen.run)
                    stubs.Add(p)

                if wantsClient && file.Service.Count > 0 then
                    let p = Path.Combine(opts.OutputDir, baseName + ".client.generated.fs")
                    File.WriteAllText(p, generateClient file |> Gen.mkOak |> Gen.run)
                    stubs.Add(p)

        let messageList = messages |> List.ofSeq
        let stubList = stubs |> List.ofSeq
        let manifest = writeManifest opts.OutputDir (messageList @ stubList)

        { MessageFiles = messageList
          StubFiles = stubList
          ManifestPath = manifest }

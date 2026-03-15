module Grpc.FSharp.Tools.ProtocResolver

open System
open System.IO
open System.Runtime.InteropServices

/// Resolve the protoc binary from the Grpc.Tools NuGet cache.
let findProtoc () =
    let nugetDir =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget",
            "packages",
            "grpc.tools"
        )

    if not (Directory.Exists nugetDir) then
        None
    else
        let version =
            Directory.GetDirectories(nugetDir)
            |> Array.sortDescending
            |> Array.tryHead
            |> Option.map Path.GetFileName

        match version with
        | None -> None
        | Some ver ->
            let platform =
                if RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
                    "macosx"
                elif RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
                    "linux"
                elif RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
                    "windows"
                else
                    "unknown"

            let archCandidates =
                match RuntimeInformation.OSArchitecture with
                | Architecture.Arm64 -> [ "arm64"; "x64" ]
                | Architecture.X64 -> [ "x64" ]
                | Architecture.X86 -> [ "x86" ]
                | _ -> []

            let exe =
                if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
                    "protoc.exe"
                else
                    "protoc"

            archCandidates
            |> List.map (fun arch -> Path.Combine(nugetDir, ver, "tools", $"{platform}_{arch}", exe))
            |> List.tryFind File.Exists

/// Resolve the well-known types include path from Grpc.Tools NuGet cache.
let findWellKnownTypesPath () =
    let nugetDir =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget",
            "packages",
            "grpc.tools"
        )

    if not (Directory.Exists nugetDir) then
        None
    else
        Directory.GetDirectories(nugetDir)
        |> Array.sortDescending
        |> Array.tryHead
        |> Option.map (fun dir -> Path.Combine(dir, "build", "native", "include"))
        |> Option.filter Directory.Exists

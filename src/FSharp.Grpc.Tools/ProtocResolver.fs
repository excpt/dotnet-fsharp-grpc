module FSharp.Grpc.Tools.ProtocResolver

open System
open System.IO
open System.Runtime.InteropServices
open NuGet.Versioning

/// Pick the newest version directory from a sequence of NuGet package version paths.
/// Parses each directory name with `NuGetVersion` for full SemVer 2.0 semantics —
/// 2.10.0 ranks above 2.9.0, releases rank above pre-releases at the same version,
/// and pre-release identifiers compare alphanumerically. Directory names that aren't
/// valid NuGet versions are ignored.
let pickLatestVersionDir (dirs: string seq) : string option =
    let tryParse (name: string) : NuGetVersion option =
        let mutable v = Unchecked.defaultof<NuGetVersion>
        if NuGetVersion.TryParse(name, &v) then Some v else None

    dirs
    |> Seq.choose (fun dir -> tryParse (Path.GetFileName(dir)) |> Option.map (fun v -> v, dir))
    |> Seq.sortByDescending fst
    |> Seq.tryHead
    |> Option.map snd

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
            |> pickLatestVersionDir
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
        |> pickLatestVersionDir
        |> Option.map (fun dir -> Path.Combine(dir, "build", "native", "include"))
        |> Option.filter Directory.Exists

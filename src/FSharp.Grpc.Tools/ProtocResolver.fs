module FSharp.Grpc.Tools.ProtocResolver

open System
open System.IO
open System.Runtime.InteropServices

/// Pick the newest version directory from a sequence of NuGet package version paths.
/// Compares as SemVer (major.minor.patch) so 2.10.0 ranks above 2.9.0. Releases rank
/// above pre-releases at the same version. Directories whose names don't parse as
/// numeric SemVer are ignored.
let pickLatestVersionDir (dirs: string seq) : string option =
    let parseVersion (name: string) =
        let core, hasPre =
            match name.IndexOf('-') with
            | -1 -> name, false
            | i -> name.Substring(0, i), true

        let parts = core.Split('.')

        let nums =
            parts
            |> Array.choose (fun s ->
                match Int32.TryParse(s) with
                | true, n -> Some n
                | _ -> None)

        if nums.Length = parts.Length && nums.Length > 0 then
            let major = nums.[0]
            let minor = if nums.Length > 1 then nums.[1] else 0
            let patch = if nums.Length > 2 then nums.[2] else 0
            Some(major, minor, patch, not hasPre)
        else
            None

    dirs
    |> Seq.choose (fun dir -> Path.GetFileName(dir) |> parseVersion |> Option.map (fun v -> v, dir))
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

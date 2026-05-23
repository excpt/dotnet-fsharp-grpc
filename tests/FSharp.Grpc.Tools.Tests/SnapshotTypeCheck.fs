/// Type-checks snapshot outputs using FSharp.Compiler.Service. The existing
/// `Fantomas.Core.CodeFormatter.IsValidFSharpCodeAsync` only validates parsing —
/// a generator bug that emits `ComputeInt32Size(v)` for `v: Currency` parses
/// fine but won't actually type-check. This helper catches that bug class at
/// test time instead of at consumer build time.
module FSharp.Grpc.Tools.Tests.SnapshotTypeCheck

open System
open System.IO
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics

let private checker = lazy FSharpChecker.Create()

/// Locations of every assembly the snapshots need.
let private referenceAssemblies =
    [ typeof<obj>.Assembly.Location // System.Private.CoreLib (BCL)
      typeof<System.Uri>.Assembly.Location // System
      typeof<System.Linq.Enumerable>.Assembly.Location // System.Linq
      typeof<System.Text.Json.JsonElement>.Assembly.Location // System.Text.Json
      typeof<Google.Protobuf.CodedOutputStream>.Assembly.Location // Google.Protobuf
      typeof<Grpc.Core.Marshaller<int>>.Assembly.Location // Grpc.Core.Api
      typeof<FSharp.Collections.List<int>>.Assembly.Location ] // FSharp.Core
    |> List.distinct

/// Collect every .dll sitting next to System.Private.CoreLib so the compiler
/// can find the rest of the BCL surface (System.Runtime, System.Collections,
/// System.ObjectModel, ...) without us listing each one.
let private bclDir = Path.GetDirectoryName(typeof<obj>.Assembly.Location)

let private bclDlls =
    Directory.GetFiles(bclDir, "*.dll")
    |> Array.filter (fun p ->
        let n = Path.GetFileName(p)

        n.StartsWith("System.") || n = "netstandard.dll" || n = "mscorlib.dll")
    |> Array.toList

/// Type-check the given F# source. Returns None on success, Some errorReport on
/// failure. Diagnostics-style: only "error"-severity issues fail; warnings pass.
let typeCheck (sourceName: string) (source: string) : string option =
    let tmpDir = Path.Combine(Path.GetTempPath(), $"snap-{Guid.NewGuid():N}")
    Directory.CreateDirectory(tmpDir) |> ignore
    let srcPath = Path.Combine(tmpDir, $"{sourceName}.fs")
    File.WriteAllText(srcPath, source)

    try
        let refs = bclDlls @ referenceAssemblies |> List.distinct
        let outDll = Path.Combine(tmpDir, $"{sourceName}.dll")

        let args =
            [| yield "fsc.exe"
               yield "--target:library"
               yield "--noframework"
               yield "--simpleresolution"
               yield "--nowarn:3186" // suppress "unused" warnings on snapshot artefacts
               yield $"-o:{outDll}"
               for r in refs do
                   yield $"-r:{r}"
               yield srcPath |]

        let diagnostics, _exitCode = checker.Value.Compile(args) |> Async.RunSynchronously

        let errors =
            diagnostics
            |> Array.filter (fun d -> d.Severity = FSharpDiagnosticSeverity.Error)

        if Array.isEmpty errors then
            None
        else
            errors
            |> Array.map (fun d ->
                $"  {Path.GetFileName d.Range.FileName}({d.Range.StartLine},{d.Range.StartColumn}): {d.Message}")
            |> String.concat "\n"
            |> Some
    finally
        try
            Directory.Delete(tmpDir, true)
        with _ ->
            ()

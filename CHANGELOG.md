# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

Releases are cut with [versionize](https://github.com/versionize/versionize),
which generates the version heading + tag. Bodies are written by hand —
no Conventional-Commits convention is enforced in this repo, so
auto-grouping wouldn't capture anything useful.


<a name="0.1.7"></a>
## [0.1.7](https://www.github.com/excpt/dotnet-fsharp-grpc/releases/tag/v0.1.7) (2026-05-24)

### Fixed
- `protoc` resolver picks the newest cached `Grpc.Tools` by SemVer instead of lexicographic sort — `2.10.0` previously ranked below `2.9.0` (#15).
- `protoc` invocation builds its argument list via `ProcessStartInfo.ArgumentList`, so proto paths containing special characters (e.g. a literal `"`) no longer break the command (#15).
- `--grpc-services` rejects unknown values with a non-zero exit and an error message, instead of silently emitting no stubs (#15).
- protoc failures surface as a clean error + non-zero exit rather than an unhandled exception (#15).
- NuGet package `Authors` metadata corrected (#15).

### Changed
- Generated map `computeSize`/`writeTo` code emits multi-line `for` loops instead of single-line `Map.iter` lambdas — more readable generated output. Partially reverts the 0.1.2 `for..in → Map.iter` change for the map path (#20).
- Internal codegen refactors, no output change: single-pass field classification (`FieldClass`, #16), message-ref classification (`MessageKind`, #17), centralised wrapper-scalar JSON dispatch, and a widget-built `BindService` body (#20).

### Added
- `CONTRIBUTING.md` and `docs/tutorial.md` (#15).
- FsCheck property-based cross-language round-trip tests, complementing the hand-written suite (#20).
- Backfilled CHANGELOG bodies for every prior release (#20).
- Codegen tests skip gracefully (rather than fail) when `Grpc.Tools` isn't cached locally (#20).


<a name="0.1.6"></a>
## [0.1.6](https://www.github.com/excpt/dotnet-fsharp-grpc/releases/tag/v0.1.6) (2026-05-23)

### Added
- Nested types stay nested under a `Types` submodule, mirroring C# protobuf codegen (#13).
- `open` emitted for cross-package type references (#6).
- `namespace rec` emitted when a file has intra-file forward references (#8).
- `namespace {FileName}` emitted for protos without a package (#7).

### Fixed
- Oneof codegen + wire-helper centralisation (#4).
- Loud failure on unhandled wrapper scalar types in JSON generators.
- Forward-ref detection keyed by fully-qualified name.

### Changed
- Snapshot type-check workarounds reference their tracking issues.


<a name="0.1.5"></a>
## [0.1.5](https://www.github.com/excpt/dotnet-fsharp-grpc/releases/tag/v0.1.5) (2026-05-23)

### Added
- Per-item `GrpcServices` selection from `<Protobuf>` items in the consuming `.fsproj`.


<a name="0.1.4"></a>
## [0.1.4](https://www.github.com/excpt/dotnet-fsharp-grpc/releases/tag/v0.1.4) (2026-05-23)

### Fixed
- Codegen file ordering — manifest now emits files in topological order so
  generated dependencies compile before dependents (#1).


<a name="0.1.3"></a>
## [0.1.3](https://www.github.com/excpt/dotnet-fsharp-grpc/releases/tag/v0.1.3) (2026-03-22)

### Fixed
- NuGet package bundles all dependencies; added `RollForward` so the CLI tool
  works on newer .NET runtimes than its build target.

### Added
- `llms.txt` and `llms-full.txt` for LLM-friendly project metadata.
- Updated benchmark results in `README`.


<a name="0.1.2"></a>
## [0.1.2](https://www.github.com/excpt/dotnet-fsharp-grpc/releases/tag/v0.1.2) (2026-03-22)

### Added
- `decodeFrom` for streaming deserialisation; nested messages now use
  `CreateCodedInput` for sub-message decoding.

### Changed
- Decode codegen swaps `ResizeArray`/`Dictionary` for list prepend + `Map.add`
  (functional, fewer allocations).
- `computeSize` and `writeTo` codegen replaces `for..in` with `List.iter` /
  `Map.iter`.
- Updated benchmark results in `README`.


<a name="0.1.1"></a>
## [0.1.1](https://www.github.com/excpt/dotnet-fsharp-grpc/releases/tag/v0.1.1) (2026-03-15)

### Fixed
- Symbol package error in NuGet publish; `README` now included in published packages.


<a name="0.1.0"></a>
## [0.1.0](https://www.github.com/excpt/dotnet-fsharp-grpc/releases/tag/v0.1.0) (2026-03-15)

Initial public release.

- F# code generation from `.proto` files (records, enums, discriminated unions).
- Binary serialisation (`encode` / `decode`) and JSON serialisation
  (`encodeJson` / `decodeJson`).
- gRPC server and client stub generation.
- MSBuild integration via the `FSharp.Grpc.Tools.Codegen` NuGet package.

# Contributing

Thanks for your interest in contributing to FSharp.Grpc.

## Build and test

```bash
dotnet build
dotnet test
```

Always run `dotnet test` at the solution root. Don't run individual test projects.

## Formatting

F# code is formatted with [Fantomas](https://github.com/fsprojects/fantomas).
C# code is formatted with `dotnet format`. Settings live in `.editorconfig`.

```bash
dotnet fantomas --check .          # check F# formatting
dotnet fantomas .                  # fix F# formatting
dotnet format --verify-no-changes  # check C# formatting
dotnet format                      # fix C# formatting
```

CI enforces F# formatting on every PR.

## Workflow

This project follows TDD:

1. Add or update expected output in `tests/FSharp.Grpc.Tools.Tests/expected/`.
2. Add or update test cases in the corresponding test file.
3. Run `dotnet test` — tests fail.
4. Implement the change in `src/FSharp.Grpc.Tools/`.
5. Run `dotnet test` — tests pass.

For gRPC integration tests, write the test in `GrpcIntegrationTests.fs` first, then
update the hand-written generated code to match.

## Pull requests

- Keep PRs focused — one concern per PR.
- Run `dotnet test` and `dotnet fantomas --check .` locally before pushing.
- Reference the relevant issue in the PR description.

## Architecture

See [`CLAUDE.md`](CLAUDE.md) for an architectural overview of the codebase, including
the generator pipeline, key modules, and the layout of the test projects.

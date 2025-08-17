# Repository Guidelines

## Project Structure & Modules
- `src/ListMmf`: Core library (`net9.0`, unsafe allowed) under `BruSoftware.ListMmf`.
- `src/ListMmfTests`: xUnit test suite with FluentAssertions and Coverlet.
- `src/ListMmfBenchmarks`: BenchmarkDotNet console app (`net9.0-windows`).
- `src/ListMmf/docs`: Internal design and project notes.
- `ListMmf.sln`: Solution entry point.

## Build, Test, and Development
- Restore: `dotnet restore`
- Build: `dotnet build ListMmf.sln -c Release`
- Test (with results): `dotnet test src/ListMmfTests -c Release --logger "trx;LogFileName=test-results.trx"`
- Coverage (Coverlet collector): `dotnet test src/ListMmfTests -c Release --collect:"XPlat Code Coverage"`
- Pack (NuGet): `dotnet pack src/ListMmf/ListMmf.csproj -c Release -o ./artifacts`
- Benchmarks (Windows only): `dotnet run -c Release --project src/ListMmfBenchmarks`

Note: The library and tests require a 64-bit process; ensure you run on x64.

## Coding Style & Naming
- Indentation: 4 spaces; file-scoped namespaces; explicit access modifiers.
- Naming: PascalCase for public types/members; camelCase for locals/params; UPPER_SNAKE_CASE for constants.
- C#: `net9.0`, `LangVersion=preview`. Favor `Span<T>`/`ReadOnlySpan<T>` and avoid allocations on hot paths.
- Unsafe: Allowed where justified for performance; keep usage minimal and well-contained.

## Testing Guidelines
- Frameworks: xUnit + FluentAssertions; coverage via `coverlet.collector`.
- Test naming: `<TypeName>Tests.cs`; methods use `[Fact]`/`[Theory]` with clear Arrange-Act-Assert.
- Run: `dotnet test -c Release` (optionally add `--collect:"XPlat Code Coverage"`). Ensure tests work on Windows and Linux.

## Commit & Pull Requests
- Commits: Imperative mood, present tense; concise subject. Optional type prefix (e.g., `fix:`, `docs:`) seen in history.
  - Example: `fix: correct view count handling` or `docs: improve API documentation`.
- PRs: Include purpose, linked issues, and scope. Require green CI, updated tests/docs (`src/ListMmf/docs` if internals change), and any perf notes for hot paths. Keep changes minimal and focused.

## Security & Configuration Tips
- Concurrency: One writer, multiple readers; avoid patterns that introduce global locks.
- Files: Use unique, explicit paths in samples/tests to prevent collisions.
- Platform: Benchmarks target Windows; core library and tests are cross-platform but x64-only.

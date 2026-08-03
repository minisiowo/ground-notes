# Agent Guide: tests/GroundNotes.Tests

## Scope
- This guide applies to `tests/GroundNotes.Tests` and descendants.
- This is the xUnit and Avalonia headless test project for `src/GroundNotes`.
- Keep tests near the behavior they cover and prefer focused regressions over broad end-to-end coverage.

## Harness
- `AvaloniaTestApplication.cs` supplies the assembly-wide headless Skia app. Tests that construct controls, windows, editor views, bitmaps, or use the UI dispatcher should use `[AvaloniaFact]`/`[AvaloniaTheory]`; ordinary service and parser tests use xUnit `[Fact]`/`[Theory]`.
- `xunit.runner.json` disables test-collection parallelism. Preserve this because tests share Avalonia application resources, dispatcher state, static markdown diagnostics, and timing-sensitive filesystem behavior.
- Flush queued rendering or background work with `Dispatcher.UIThread.RunJobs` at the priority used by the production path before asserting layout, visual lines, or deferred previews.

## Conventions
- Use descriptive names in the existing `MethodOrFeature_ExpectedBehavior` style.
- Use unique temp directories and clean them through `IDisposable`; `Helpers/TempDirectoryFixture.cs` is the shared minimal fixture. Never write to repository paths, the user's notes folder, or the real settings location.
- Reuse `Helpers` and the focused local fakes already kept beside larger fixtures such as `MainViewModelTests` before adding shared infrastructure.
- Do not make real OpenAI or network calls; use `Helpers/FakeHttpMessageHandler.cs` or equivalent local fakes.

## Where to Add Coverage
- Repository, filesystem, parsing, rename/delete, timestamps: `NotesRepositoryTests` and related service tests.
- Search/list and main workflow state: `MainViewModelTests`.
- AI chat and prompt services: `ChatViewModelTests` and `OpenAi*ServiceTests`.
- Markdown/editor/image-preview behavior: the relevant `Markdown*` tests and editor controller tests.

## Hazards
- Repository and mutation tests intentionally exercise filename migration, collision handling, frontmatter preservation, timestamps, and stale-save conflicts. Assert both returned models and on-disk paths/content when changing these contracts.
- Rendering tests may mutate `Application.Current.Resources` or `MarkdownDiagnostics`; restore resources in `finally`, reset shared counters around assertions, dispose controllers/providers, and close shown windows.
- Watcher and asynchronous view-model tests are timing-sensitive. Prefer the existing `TaskCompletionSource`/bounded polling patterns over unbounded waits; do not replace deterministic hooks with a real user-directory watcher.

## Validation
- Target one class with `dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --filter "FullyQualifiedName~ClassName"`.
- Use `--no-build` only after a successful build that includes your latest changes.

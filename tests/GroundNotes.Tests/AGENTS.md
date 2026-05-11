# Agent Guide: tests/GroundNotes.Tests

## Local Scope
- This is the xUnit test project for `src/GroundNotes`.
- Keep tests near the behavior they cover and prefer focused regressions over broad end-to-end coverage.

## Test Patterns
- Use descriptive names in the existing `MethodOrFeature_ExpectedBehavior` style.
- Use temp directories for filesystem tests and clean them up reliably; do not write to repo paths or user note folders.
- Reuse existing helpers and local fakes before adding new test infrastructure.
- Do not make real OpenAI or network calls; use `Helpers/FakeHttpMessageHandler.cs` or equivalent local fakes.
- Avalonia/editor rendering tests should follow the existing `EnsureApplication()` pattern and flush `Dispatcher.UIThread` jobs when needed.

## Where to Add Coverage
- Repository, filesystem, parsing, rename/delete, timestamps: `NotesRepositoryTests` and related service tests.
- Search/list and main workflow state: `MainViewModelTests`.
- AI chat and prompt services: `ChatViewModelTests` and `OpenAi*ServiceTests`.
- Markdown/editor/image-preview behavior: the relevant `Markdown*` tests and editor controller tests.

## Validation
- Target one class with `dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --filter "FullyQualifiedName~ClassName"`.
- Use `--no-build` only after a successful build that includes your latest changes.

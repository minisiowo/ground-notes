# Markdown Wrap Indent Investigation

## Problem

Wrapped markdown list/task lines still align incorrectly in the editor. The desired behavior was a hanging indent so continuation lines would start under the item text, not back near the left edge.

This was tested against real screenshots from the app. The screenshots remained the source of truth, and the attempted fixes did not produce the intended result.

## What Was Attempted

1. Parser-side continuation support in `MarkdownLineParser`
   - Added ordered-list text range support.
   - Added a helper to calculate a continuation prefix length for list items.

2. Shared markdown analysis service
   - Added `MarkdownLineAnalysisService` to share line analysis and fence tracking.
   - Refactored `MarkdownColorizingTransformer` to consume that service.

3. Shared editor host / theme changes
   - Rewired `EditorHostController` to own the shared analysis service and colorizer.
   - Changed `EditorThemeController` wrap settings to try inherited wrap indentation.

4. Built-in wrap indentation attempts
   - Tried a fixed `WordWrapIndentation` value.
   - Tried a measured `WordWrapIndentation` based on the rendered width of `- [ ] `.

## What Did Not Work

- The custom markdown-aware wrap-indent approach did not change the actual visible wrap alignment in the GUI.
- The built-in `WordWrapIndentation` approach also did not produce the expected hanging indent in real screenshots.
- The screenshot evidence showed that the continuation line still started too far to the left.

## Files Touched During The Failed Attempt

- `src/GroundNotes/Views/EditorThemeController.cs`
- `src/GroundNotes/Views/EditorHostController.cs`
- `src/GroundNotes/Editors/MarkdownColorizingTransformer.cs`
- `src/GroundNotes/Editors/MarkdownLineParser.cs`
- `src/GroundNotes/Editors/MarkdownLineAnalysisService.cs`
- `tests/GroundNotes.Tests/MarkdownLineParserTests.cs`
- `tests/GroundNotes.Tests/MarkdownLineAnalysisServiceTests.cs`

## Recommended Future Follow-Up

Before changing repo code again, inspect AvaloniaEdit's real wrap/paragraph layout path more deeply and verify the exact supported hook with a minimal reproduction. The assumptions used in this attempt about which APIs control wrap indentation were not reliable enough.

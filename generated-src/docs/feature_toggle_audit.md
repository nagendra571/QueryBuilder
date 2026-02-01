# Feature Toggle Audit Notes

Date: 2026-02-01

## Scope
- Feature: `NewAppearance` (compact UI)
- Locations: `_Layout.cshtml`, `theme.compact.css`, `Home/Index.cshtml`, feature flag service.

## Findings
- Compact theme CSS is fully scoped under `.theme-compact` and is conditionally loaded only when enabled.
- `_Layout.cshtml` evaluates the toggle once and applies the body class + optional CSS include.
- `Home/Index.cshtml` uses the same flag to swap compact vs legacy markup.
- Compact-only utility classes (`btn-compact`, `card-compact`, `table-compact`, `input-compact`) are defined only under `.theme-compact`, so they are inert in legacy mode.

## Gaps Found & Fixes Applied
- Added DB-backed feature flags with config fallback and short TTL caching.
- Centralized toggle evaluation via `IFeatureFlagService` (layout and home use the service).
- Ensured compact styles are scoped under `.theme-compact` (no unscoped overrides).
 - Added rowversion-based concurrency on feature flag updates.

## Why This Matches Requirements
- Legacy mode: `theme.compact.css` is not loaded and `.theme-compact` is absent, so compact overrides do not apply.
- New mode: `.theme-compact` + `theme.compact.css` enabled through the feature flag service.
- Toggle is centralized and consistent across the app.
 - Cache TTL is 45 seconds; updates invalidate cache immediately for fast convergence.

# Counter Visualization Plan

## Current Architecture Notes
- Editor: `Views/Visualizations/Create.cshtml` renders tabs and preview; type switch toggles tabs and preview panel.
- Preview data: editor JS loads latest execution via `QueryExecutionPreviewService` endpoints and custom preview endpoints (e.g., `/visualizations/chart-preview`).
- Config persistence: `Visualization.ConfigJson` stored; `VisualizationsController` serializes per type.
- Rendering:
  - Query view: `Views/Visualizations/Details.cshtml` uses `window.queryBuilderViz` + `visualizations.js`.
  - Dashboards/public: `Views/Dashboards/View.cshtml`, `Views/Public/Dashboard*.cshtml`, auto refresh via `auto-refresh.js`.

## Config Schema (proposal)
```json
{
  "type": "counter",
  "general": {
    "label": "",
    "countRows": false,
    "valueColumn": null,
    "valueRow": 1,
    "targetColumn": null,
    "targetRow": 1
  },
  "format": {
    "numberFormat": "0,0",
    "showTarget": true,
    "prefix": "",
    "suffix": "",
    "positiveColor": "green",
    "negativeColor": "red"
  }
}
```

## Data/Preview Computation
- Build a pure `CounterVisualizationDataBuilder` that accepts config + columns + rows and returns a render model:
  - resolved value/target (string + numeric when parseable)
  - status flags, error/warning messages
  - display text (formatted)
- Validation:
  - if `countRows` true → value = row count; ignore value column/row
  - else require `valueColumn` + `valueRow` (1-based index within row count)
  - if `targetColumn` provided → validate `targetRow` (1-based), else target optional
- Formatting: use `numberFormat` + prefix/suffix; fallback to raw text for non-numeric.
- Color rule: if target numeric and value numeric:
  - value >= target → positiveColor
  - value < target → negativeColor

## Files/Areas to Update
- Domain: `VisualizationType` enum add `Counter`.
- Models:
  - `CounterVisualizationConfig`, `CounterVisualizationRenderModel`.
  - Update `VisualizationEditViewModel`, `VisualizationDetailsViewModel`, `DashboardViewModel` to include counter config/render.
- Services:
  - `CounterVisualizationDataBuilder` + tests.
- Controllers:
  - `VisualizationsController`: include Counter in type list, config serialization/deserialization, preview endpoint `/visualizations/counter-preview`, use builder in Details + Data.
  - `DashboardsController` + `PublicController`: build counter render for widgets and auto-refresh payload.
- Views:
  - `Views/Visualizations/Create.cshtml`: add Counter tabs (General, Format) + preview container; add hidden `CounterConfigJson`.
  - `Views/Visualizations/Details.cshtml`: render counter view (not canvas) + pass counter payload to JS.
  - Dashboard/Public views: include counter config/render in widget payload.
- JS:
  - Editor: `wwwroot/js/counter-visualization-editor.js` for UI binding + live preview.
  - Renderer: extend `wwwroot/js/visualizations.js` (or add new renderer) to render counter in preview/details/dashboard.
  - Auto refresh: `wwwroot/js/auto-refresh.js` to route to counter renderer.

## Tests
- `CounterVisualizationDataBuilderTests`: row index mapping, countRows, missing columns/rows, formatting, color rule.
- Config serialization test.
- Controller/preview test for `/visualizations/counter-preview`.

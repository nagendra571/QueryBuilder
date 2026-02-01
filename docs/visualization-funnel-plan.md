# Funnel Visualization Plan

## Current Architecture Notes
- Editor: `Views/Visualizations/Create.cshtml` renders per-type tabs/sections and preview panel; type switch toggles sections.
- Preview data: editor JS loads latest execution results via `/queries/{id}/executions/latest` and custom preview endpoints (e.g., `/visualizations/chart-preview`, `/visualizations/counter-preview`).
- Config persistence: `Visualization.ConfigJson` serialized/deserialized in `VisualizationsController`.
- Rendering:
  - Query view: `Views/Visualizations/Details.cshtml` + `wwwroot/js/visualizations.js`.
  - Dashboards/public: `Views/Dashboards/View.cshtml`, `Views/Public/Dashboard*.cshtml`, refresh via `wwwroot/js/auto-refresh.js`.

## Config Schema (proposal)
```json
{
  "type": "funnel",
  "stepColumn": "",
  "stepDisplayName": "Steps",
  "valueColumn": "",
  "valueDisplayName": "Value",
  "autoSort": false,
  "sortByColumn": "",
  "sortDirection": "desc",
  "treatNullAsZero": true
}
```

## Computation Rules
- Build rows: `stepLabel` from `stepColumn`, `value` numeric parse of `valueColumn` (null/NaN => 0).
- Sorting: if `autoSort`, sort by `sortByColumn` numeric desc (fallback to `valueColumn`); else preserve order.
- `maxValue` = max(values). If max=0, percentMax = 0 for all.
- `percentMax` = value / maxValue * 100.
- `percentPrevious`: for first row = 100. Else if prevValue==0 => 0 (or “—” in UI); else value/prevValue*100.
- Limit preview rows to first 50.

## Files to Touch
- Domain: add `VisualizationType.Funnel`.
- Models: `FunnelVisualizationConfig`, `FunnelVisualizationRenderModel`, `FunnelPreviewRequest`.
- Services: `FunnelVisualizationDataBuilder` + tests.
- Controllers: `VisualizationsController` (config + preview endpoint), `DashboardsController`, `PublicController` (render payload).
- Views: `Views/Visualizations/Create.cshtml` (Funnel editor section + preview), `Views/Visualizations/Details.cshtml`, dashboard/public views.
- JS: new `funnel-visualization-editor.js`, extend `visualizations.js` for funnel rendering, update `auto-refresh.js`.
- CSS: funnel table + bars styling in `wwwroot/css/site.css`.

## Tests
- Unit tests for computation (percentMax/percentPrevious, sort, null handling, max=0).
- Config serialization test.
- Controller preview test for `/visualizations/funnel-preview`.

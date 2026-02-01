# Chart Visualization Plan

## Goals
- Add a single "Chart" visualization type with editor tabs: General, X Axis, Y Axis, Series, Colors, Data Labels.
- Use latest stored execution results for live preview (no query re-run), same as table editor.
- Persist chart config in `Visualization.ConfigJson`, reload it, and render in query view + dashboards.

## Config JSON schema (Chart)
```json
{
  "type": "chart",
  "general": {
    "chartType": "bar",
    "xColumn": "recorddate",
    "yColumns": ["salesunits","revenue"],
    "groupBy": "region",
    "errorsColumn": null,
    "showLegend": true,
    "stacking": "disabled",
    "normalizeToPercent": false,
    "nullAsZero": true
  },
  "xAxis": {
    "scale": "auto",
    "name": "",
    "sortValues": true,
    "reverseOrder": false,
    "showLabels": true
  },
  "yAxis": {
    "left": { "scale": "linear", "name": "", "min": null, "max": null, "reverse": false },
    "right": { "scale": "linear", "name": "", "min": null, "max": null, "reverse": false }
  },
  "series": [
    { "key": "revenue|region=East", "label": "East Revenue", "axis": "left", "type": "bar", "zIndex": 1 }
  ],
  "colors": {
    "revenue|region=East": "blue"
  },
  "dataLabels": {
    "enabled": false,
    "numberFormat": "0,0.00",
    "percentFormat": "0[.]00%",
    "dateTimeFormat": "DD/MM/YY HH:mm",
    "labelTemplate": "auto"
  }
}
```

## Data builder (core engine)
Create a pure C# service that transforms execution results + chart config into render data:
- Inputs: columns, rows, `ChartVisualizationConfig`.
- Output: `ChartVisualizationRenderModel` with labels + series datasets + warnings.
- Rules:
  - Group-by behavior and stable series keys.
  - Sorting/reversing by X values.
  - Null handling and `nullAsZero`.
  - Normalize to percent per X bucket.
  - Errors column stored (tooltip support placeholder).

## Chart.js mapping (frontend)
Use render model + config to build Chart.js options:
- General: legend display, stacking, chart type mapping, percent normalization display.
- X Axis: scale type mapping (auto detect), label display.
- Y Axis: left/right axes, min/max, reverse, grid on right disabled.
- Series: per-series type overrides and axis assignment.
- Colors: palette mapping applied per series key.
- Data labels: enable plugin if available; basic formatting using Intl.

## Editor UX
- Tabs: General, X Axis, Y Axis, Series, Colors, Data Labels.
- General tab controls for chart type, X, Y (multi), group by, errors, legend, stacking, normalization, null-as-zero.
- Inline validation hints (missing X/Y).
- Live preview updates on every change.

## Endpoints / data flow
- Editor preview: new endpoint to build chart render data from latest execution result + posted config.
- Query view / dashboard: server builds chart render data from stored config + execution results and serializes to JS.
- Auto-refresh: extend refresh payload to include chart render data for charts.

## Files to change (high level)
- Models: new `ChartVisualizationConfig` and render models.
- Services: `ChartVisualizationDataBuilder` (pure), unit tests.
- Controllers: VisualizationsController, DashboardsController, PublicController (render + refresh payloads).
- Views: Visualizations/Create, Visualizations/Details, Dashboards/View, Public/Dashboard*, Visualization preview partial.
- JS: new chart editor JS; update chart renderer to accept render model; update auto-refresh for chart payloads.
- Tests: data builder, config serialization, series key mapping; integration/controller test.
```

(() => {
  const mapRows = (columns, rows) => {
    const index = Object.create(null);
    columns.forEach((name, i) => {
      index[name] = i;
    });
    return { rows, index };
  };

  const toNumber = (value) => {
    if (value === null || value === undefined || value === "") return null;
    const num = Number(value);
    return Number.isFinite(num) ? num : null;
  };

  const palette = [
    "#2563eb",
    "#16a34a",
    "#f59e0b",
    "#ef4444",
    "#8b5cf6",
    "#14b8a6",
    "#f97316",
    "#0ea5e9"
  ];

  const buildSeries = (config) => {
    const { rows, index } = mapRows(config.columns || [], config.rows || []);
    const xIndex = index[config.xColumn];
    const yIndex = index[config.yColumn];
    const labelIndex = index[config.labelColumn];
    const valueIndex = index[config.valueColumn];
    const rangeStartIndex = index[config.rangeStartColumn];
    const rangeEndIndex = index[config.rangeEndColumn];
    const yColumns = Array.isArray(config.yColumns) && config.yColumns.length > 0
      ? config.yColumns
      : (config.yColumn ? [config.yColumn] : []);

    if (config.type === "Pie") {
      const labels = [];
      const data = [];
      rows.forEach((row) => {
        labels.push(row[labelIndex] ?? "");
        data.push(toNumber(row[valueIndex]) ?? 0);
      });
      return { labels, datasets: [{ label: config.valueColumn || "Value", data }] };
    }

    const useFloatingBars = config.type === "FloatingBar" || config.useFloatingBars === true;
    const useHorizontalBars = config.type === "HorizontalBar" || config.useHorizontalBars === true;

    if (useFloatingBars) {
      const labels = [];
      const data = [];
      rows.forEach((row) => {
        labels.push(row[xIndex] ?? "");
        const start = toNumber(row[rangeStartIndex]) ?? 0;
        const end = toNumber(row[rangeEndIndex]) ?? 0;
        data.push([start, end]);
      });
      return {
        labels,
        datasets: [{
          label: config.rangeEndColumn || "Range",
          data,
          borderColor: palette[0],
          backgroundColor: palette[0] + "55"
        }]
      };
    }

    if (config.type === "Line" || config.type === "Bar") {
      const labels = [];
      rows.forEach((row) => {
        labels.push(row[xIndex] ?? "");
      });

      const datasets = yColumns.map((col, i) => {
        const colIndex = index[col];
        const data = rows.map((row) => toNumber(row[colIndex]) ?? 0);
        const lineOptions = config.type === "Line" ? getLineDatasetOptions(config.lineInterpolationMode) : {};
        return {
          label: col,
          data,
          borderColor: palette[i % palette.length],
          backgroundColor: config.type === "Bar"
            ? palette[i % palette.length] + "55"
            : palette[i % palette.length] + "33",
          ...lineOptions
        };
      });
      return { labels, datasets };
    }

    return { labels: [], datasets: [] };
  };

  const renderChart = (canvasId, config) => {
    const canvas = document.getElementById(canvasId);
    if (!canvas || typeof Chart === "undefined") return;

    if (config.type === "Table") {
      return;
    }

    const series = buildSeries(config);
    const chartType = config.type === "Line" ? "line" : config.type === "Pie" ? "pie" : "bar";
    const useHorizontalBars = config.type === "HorizontalBar" || config.useHorizontalBars === true;
    const useStackedBars = config.useStackedBars === true && chartType === "bar";

    window.queryBuilderViz = window.queryBuilderViz || {};
    window.queryBuilderViz.instances = window.queryBuilderViz.instances || {};
    const existing = window.queryBuilderViz.instances[canvasId];
    if (existing) {
      existing.data.labels = series.labels;
      existing.data.datasets = series.datasets;
      existing.options.plugins.legend.display = config.showLegend !== false;
      existing.options.indexAxis = chartType === "bar" && useHorizontalBars ? "y" : "x";
      if (chartType === "pie") {
        existing.options.scales = {};
      } else {
        existing.options.scales = {
          x: { stacked: useStackedBars },
          y: { stacked: useStackedBars, ticks: { precision: 0 } }
        };
      }
      existing.update();
      return;
    }

    const chart = new Chart(canvas, {
      type: chartType,
      data: {
        labels: series.labels,
        datasets: series.datasets
      },
      options: {
        responsive: true,
        plugins: {
          legend: {
            display: config.showLegend !== false
          }
        },
        indexAxis: chartType === "bar" && useHorizontalBars ? "y" : "x",
        scales: chartType === "pie"
          ? {}
          : {
              x: {
                stacked: useStackedBars
              },
              y: {
                stacked: useStackedBars,
                ticks: { precision: 0 }
              }
            }
      }
    });
    window.queryBuilderViz.instances[canvasId] = chart;
  };

  window.queryBuilderViz = window.queryBuilderViz || {};
  window.queryBuilderViz.renderChart = renderChart;

  if (window.queryBuilderViz?.preview) {
    renderChart("vizPreview", window.queryBuilderViz.preview);
  }

  if (window.queryBuilderViz?.details) {
    renderChart("vizChart", window.queryBuilderViz.details);
  }

  if (Array.isArray(window.queryBuilderViz?.multi)) {
    window.queryBuilderViz.multi.forEach((item) => {
      renderChart(item.canvasId, item);
    });
  }

  function getLineDatasetOptions(mode) {
    const normalized = (mode || "default").toLowerCase();
    if (normalized === "monotone") {
      return { cubicInterpolationMode: "monotone", tension: 0.4 };
    }
    if (normalized === "linear") {
      return { cubicInterpolationMode: "default", tension: 0 };
    }
    return {};
  }
})();

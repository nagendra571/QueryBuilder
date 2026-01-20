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

  const getChartColors = () => {
    const style = getComputedStyle(document.documentElement);
    const colors = [];
    for (let i = 1; i <= 8; i++) {
      const color = style.getPropertyValue(`--chart-color-${i}`).trim();
      if (color) {
        colors.push(color);
      }
    }
    return colors.length > 0 ? colors : [
      "#2563eb",
      "#16a34a",
      "#f59e0b",
      "#ef4444",
      "#8b5cf6",
      "#14b8a6",
      "#f97316",
      "#0ea5e9"
    ];
  };

  const palette = getChartColors();

  const buildSeries = (config) => {
    const { rows, index } = mapRows(config.columns || [], config.rows || []);
    const xIndex = index[config.xColumn];
    const yIndex = index[config.yColumn];
    const labelIndex = index[config.labelColumn];
    const valueIndex = index[config.valueColumn];
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
      return { labels, datasets: [{ label: config.valueColumn || "Value", data, backgroundColor: palette }] };
    }

    if (config.type === "Line" || config.type === "Bar") {
      const labels = [];
      rows.forEach((row) => {
        labels.push(row[xIndex] ?? "");
      });

      const datasets = yColumns.map((col, i) => {
        const colIndex = index[col];
        const data = rows.map((row) => toNumber(row[colIndex]) ?? 0);
        return {
          label: col,
          data,
          borderColor: palette[i % palette.length],
          backgroundColor: config.type === "Bar"
            ? palette[i % palette.length]
            : palette[i % palette.length] + "33"
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

    new Chart(canvas, {
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
        scales: chartType === "pie"
          ? {}
          : {
              y: {
                ticks: { precision: 0 }
              }
            }
      }
    });
  };

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
})();

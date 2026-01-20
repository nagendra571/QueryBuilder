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

  const buildSeries = (config) => {
    const { rows, index } = mapRows(config.columns || [], config.rows || []);
    const xIndex = index[config.xColumn];
    const yIndex = index[config.yColumn];
    const labelIndex = index[config.labelColumn];
    const valueIndex = index[config.valueColumn];

    if (config.type === "Pie") {
      const labels = [];
      const data = [];
      rows.forEach((row) => {
        labels.push(row[labelIndex] ?? "");
        data.push(toNumber(row[valueIndex]) ?? 0);
      });
      return { labels, data };
    }

    if (config.type === "Line" || config.type === "Bar") {
      const labels = [];
      const data = [];
      rows.forEach((row) => {
        labels.push(row[xIndex] ?? "");
        data.push(toNumber(row[yIndex]) ?? 0);
      });
      return { labels, data };
    }

    return { labels: [], data: [] };
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
        datasets: [
          {
            label: config.yColumn || config.valueColumn || "Value",
            data: series.data,
            borderColor: "#0d6efd",
            backgroundColor: chartType === "pie"
              ? ["#0d6efd", "#198754", "#ffc107", "#dc3545", "#6f42c1", "#20c997", "#fd7e14"]
              : "rgba(13, 110, 253, 0.25)"
          }
        ]
      },
      options: {
        responsive: true,
        plugins: {
          legend: {
            display: chartType === "pie"
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

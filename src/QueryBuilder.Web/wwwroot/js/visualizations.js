(() => {
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

  if (typeof Chart !== "undefined" && typeof ChartDataLabels !== "undefined") {
    Chart.register(ChartDataLabels);
  }

  const colorMap = {
    automatic: null,
    blue: "#2563eb",
    red: "#ef4444",
    green: "#16a34a",
    purple: "#8b5cf6",
    cyan: "#06b6d4",
    orange: "#f97316",
    "light blue": "#38bdf8",
    teal: "#14b8a6",
    yellow: "#f59e0b",
    gray: "#64748b"
  };

  const normalizeType = (value, fallback) => {
    if (!value) return fallback;
    return String(value).trim().toLowerCase();
  };

  const toChartType = (value) => {
    const type = normalizeType(value, "bar");
    if (type === "area") return "line";
    if (type === "scatter") return "scatter";
    if (type === "bubble") return "bubble";
    if (type === "pie") return "pie";
    return type;
  };

  const resolveAxisType = (value, fallback) => {
    const type = normalizeType(value, fallback || "category");
    if (type === "datetime") return "time";
    if (type === "logarithmic") return "logarithmic";
    if (type === "linear") return "linear";
    return "category";
  };

  const resolveColor = (key, index, config) => {
    const colors = config?.colors || {};
    const selection = colors[key];
    if (selection) {
      const normalized = String(selection).toLowerCase();
      if (colorMap[normalized]) {
        return colorMap[normalized];
      }
    }
    return palette[index % palette.length];
  };

  const colorWithAlpha = (hex, alpha) => {
    if (!hex) return hex;
    const normalized = hex.replace("#", "");
    const r = parseInt(normalized.substring(0, 2), 16);
    const g = parseInt(normalized.substring(2, 4), 16);
    const b = parseInt(normalized.substring(4, 6), 16);
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
  };

  const formatNumber = (value, format) => {
    if (value === null || value === undefined || Number.isNaN(value)) return "";
    const formatString = format || "0,0.00";
    const decimals = formatString.includes(".")
      ? formatString.split(".")[1].replace(/[^0]/g, "").length
      : 0;
    const formatter = new Intl.NumberFormat(undefined, {
      minimumFractionDigits: decimals,
      maximumFractionDigits: decimals
    });
    return formatter.format(value);
  };

  const formatDateTime = (value, format) => {
    if (!value) return "";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return String(value);
    if (!format) return date.toLocaleString();
    const pad = (num) => String(num).padStart(2, "0");
    return format
      .replace(/YYYY/g, date.getFullYear())
      .replace(/YY/g, String(date.getFullYear()).slice(-2))
      .replace(/MM/g, pad(date.getMonth() + 1))
      .replace(/DD/g, pad(date.getDate()))
      .replace(/HH/g, pad(date.getHours()))
      .replace(/mm/g, pad(date.getMinutes()))
      .replace(/ss/g, pad(date.getSeconds()));
  };

  const buildLabelTemplate = (template, ctx, value) => {
    if (!template || template === "auto") {
      return value;
    }
    return String(template)
      .replace(/{{\s*value\s*}}/gi, value)
      .replace(/{{\s*label\s*}}/gi, ctx.label || "")
      .replace(/{{\s*series\s*}}/gi, ctx.dataset?.label || "");
  };

  const buildDatasets = (render, config) => {
    const chartType = normalizeType(render?.chartType, "bar");
    const generalType = normalizeType(config?.general?.chartType, chartType);

    return (render?.series || []).map((series, index) => {
      const seriesType = normalizeType(series.type, generalType);
      const chartSeriesType = toChartType(seriesType);
      const color = resolveColor(series.key, index, config);
      const isArea = seriesType === "area";
      const dataset = {
        label: series.label || series.key,
        data: series.data || [],
        type: chartSeriesType,
        yAxisID: series.axis === "right" ? "y1" : "y",
        order: Number.isFinite(series.zIndex) ? series.zIndex : index
      };

      if (chartSeriesType === "line") {
        dataset.borderColor = color;
        dataset.backgroundColor = colorWithAlpha(color, isArea ? 0.25 : 0.2);
        if (isArea) {
          dataset.fill = true;
        }
      } else if (chartSeriesType === "bar") {
        dataset.backgroundColor = colorWithAlpha(color, 0.5);
        dataset.borderColor = color;
      } else if (chartSeriesType === "scatter" || chartSeriesType === "bubble") {
        dataset.backgroundColor = colorWithAlpha(color, 0.6);
        dataset.borderColor = color;
      } else if (chartSeriesType === "pie") {
        const groupBy = config?.general?.groupBy;
        const sliceColors = (render.labels || []).map((label, sliceIndex) => {
          if (groupBy) {
            const key = `${series.key}|${groupBy}=${label}`;
            const selected = config?.colors?.[key];
            if (selected && colorMap[selected.toLowerCase()]) {
              return colorMap[selected.toLowerCase()];
            }
          }
          return palette[sliceIndex % palette.length];
        });
        dataset.backgroundColor = sliceColors;
        dataset.borderColor = "#ffffff";
      }

      return dataset;
    });
  };

  const buildScales = (render, config) => {
    const stacking = normalizeType(config?.general?.stacking, "disabled") === "stack";
    const normalizeToPercent = config?.general?.normalizeToPercent === true;
    const showLabels = config?.xAxis?.showLabels !== false;
    const xScaleType = resolveAxisType(
      render?.resolvedXAxisScale || config?.xAxis?.scale,
      "category"
    );

    const xAxis = {
      type: xScaleType,
      stacked: stacking,
      ticks: { display: showLabels },
      reverse: config?.xAxis?.reverseOrder === true,
      title: {
        display: !!config?.xAxis?.name,
        text: config?.xAxis?.name || ""
      }
    };

    const left = config?.yAxis?.left || {};
    const right = config?.yAxis?.right || {};

    const yAxis = {
      type: resolveAxisType(left.scale, "linear"),
      stacked: stacking,
      reverse: left.reverse === true,
      title: {
        display: !!left.name,
        text: left.name || ""
      }
    };
    if (Number.isFinite(left.min)) yAxis.min = left.min;
    if (Number.isFinite(left.max)) yAxis.max = left.max;
    if (normalizeToPercent && !Number.isFinite(left.max)) yAxis.max = 100;

    const yAxisRight = {
      type: resolveAxisType(right.scale, "linear"),
      stacked: stacking,
      position: "right",
      reverse: right.reverse === true,
      grid: { drawOnChartArea: false },
      title: {
        display: !!right.name,
        text: right.name || ""
      }
    };
    if (Number.isFinite(right.min)) yAxisRight.min = right.min;
    if (Number.isFinite(right.max)) yAxisRight.max = right.max;

    return { x: xAxis, y: yAxis, y1: yAxisRight };
  };

  const buildDatalabels = (config) => {
    const dataLabels = config?.dataLabels || {};
    const enabled = dataLabels.enabled === true;
    if (!enabled) return { display: false };

    return {
      display: true,
      anchor: "end",
      align: "top",
      formatter: (value, ctx) => {
        let formatted = value;
        if (value && typeof value === "object" && typeof value.y === "number") {
          value = value.y;
        }
        if (typeof value === "number") {
          const usePercent = config?.general?.normalizeToPercent === true;
          const format = usePercent
            ? dataLabels.percentFormat || "0[.]00%"
            : dataLabels.numberFormat || "0,0.00";
          if (usePercent) {
            const formatter = new Intl.NumberFormat(undefined, {
              style: "percent",
              minimumFractionDigits: format.includes(".") ? format.split(".")[1].length : 0,
              maximumFractionDigits: format.includes(".") ? format.split(".")[1].length : 0
            });
            formatted = formatter.format(value / 100);
          } else {
            formatted = formatNumber(value, format);
          }
        } else if (typeof value === "string") {
          formatted = formatDateTime(value, dataLabels.dateTimeFormat || "");
        }
        const label = buildLabelTemplate(dataLabels.labelTemplate, ctx, formatted);
        return label;
      }
    };
  };

  const buildChartConfig = (render, config) => {
    const chartType = toChartType(config?.general?.chartType || render?.chartType || "bar");
    const datasets = buildDatasets(render, config);

    const options = {
      responsive: true,
      plugins: {
        legend: { display: config?.general?.showLegend !== false }
      }
    };

    if (typeof ChartDataLabels !== "undefined") {
      options.plugins.datalabels = buildDatalabels(config);
    }

    if (chartType !== "pie") {
      options.scales = buildScales(render, config);
    }

    return {
      type: chartType,
      data: {
        labels: render?.labels || [],
        datasets
      },
      options
    };
  };

  const showMessage = (canvas, message) => {
    const host = canvas?.parentElement;
    if (!host) return;
    let messageEl = host.querySelector(".viz-chart-message");
    if (!messageEl) {
      messageEl = document.createElement("div");
      messageEl.className = "viz-chart-message text-muted";
      host.appendChild(messageEl);
    }
    messageEl.textContent = message;
    canvas.classList.add("d-none");
  };

  const clearMessage = (canvas) => {
    const host = canvas?.parentElement;
    if (!host) return;
    const messageEl = host.querySelector(".viz-chart-message");
    if (messageEl) messageEl.remove();
    canvas.classList.remove("d-none");
  };

  const renderChart = (canvasId, payload) => {
    const canvas = document.getElementById(canvasId);
    if (!canvas || typeof Chart === "undefined") return;

    if (!payload || payload.type === "Table" || payload.type === "Counter") {
      return;
    }

    const render = payload.render || payload.chart || payload;
    const config = payload.config || payload.chartConfig || payload;
    if (!render) {
      showMessage(canvas, "No chart data available.");
      return;
    }

    const errors = render.errors || [];
    const message = render?.message;

    if (errors.length > 0 || message) {
      showMessage(canvas, message || errors[0] || "Unable to render chart.");
      return;
    }

    clearMessage(canvas);

    const chartConfig = buildChartConfig(render, config);
    window.queryBuilderViz = window.queryBuilderViz || {};
    window.queryBuilderViz.instances = window.queryBuilderViz.instances || {};
    const existing = window.queryBuilderViz.instances[canvasId];
    if (existing) {
      if (existing.config.type !== chartConfig.type) {
        existing.destroy();
        delete window.queryBuilderViz.instances[canvasId];
      } else {
        existing.data.labels = chartConfig.data.labels;
        existing.data.datasets = chartConfig.data.datasets;
        existing.options = chartConfig.options;
        existing.update();
        return;
      }
    }

    const chart = new Chart(canvas, chartConfig);
    window.queryBuilderViz.instances[canvasId] = chart;
  };

  const resolveCounterColor = (value) => {
    if (!value) return null;
    const normalized = String(value).toLowerCase();
    return colorMap[normalized] || value;
  };

  const formatCounterValue = (valueNumber, valueRaw, format, prefix, suffix) => {
    let output = "";
    if (typeof valueNumber === "number" && !Number.isNaN(valueNumber)) {
      output = formatNumber(valueNumber, format || "0,0");
    } else if (valueRaw !== null && valueRaw !== undefined) {
      output = String(valueRaw);
    } else {
      output = "";
    }
    return `${prefix || ""}${output}${suffix || ""}`;
  };

  const ensureCounterStructure = (container) => {
    if (!container.querySelector(".counter-value")) {
      container.innerHTML = "";
      const valueEl = document.createElement("div");
      valueEl.className = "counter-value";
      valueEl.dataset.counterValue = "true";
      const targetEl = document.createElement("div");
      targetEl.className = "counter-target";
      targetEl.dataset.counterTarget = "true";
      const labelEl = document.createElement("div");
      labelEl.className = "counter-label";
      labelEl.dataset.counterLabel = "true";
      container.appendChild(valueEl);
      container.appendChild(targetEl);
      container.appendChild(labelEl);
    }
  };

  const showCounterMessage = (container, message) => {
    container.innerHTML = "";
    const messageEl = document.createElement("div");
    messageEl.className = "counter-message text-muted";
    messageEl.textContent = message;
    container.appendChild(messageEl);
  };

  const renderCounter = (containerId, payload) => {
    const container = document.getElementById(containerId);
    if (!container) return;

    if (!payload || payload.type === "Table") {
      return;
    }

    const render = payload.render || payload.counter || payload;
    const config = payload.config || payload.counterConfig || payload;
    if (!render) {
      showCounterMessage(container, "No counter data available.");
      return;
    }

    const errors = render.errors || [];
    const message = render.message;
    if (errors.length > 0 || message) {
      showCounterMessage(container, message || errors[0] || "Unable to render counter.");
      return;
    }

    ensureCounterStructure(container);
    const valueEl = container.querySelector("[data-counter-value]");
    const targetEl = container.querySelector("[data-counter-target]");
    const labelEl = container.querySelector("[data-counter-label]");
    if (!valueEl || !targetEl || !labelEl) return;

    const format = config?.format || {};
    const general = config?.general || {};
    const valueText = formatCounterValue(
      render.valueNumber,
      render.valueRaw,
      format.numberFormat,
      format.prefix,
      format.suffix
    );
    valueEl.textContent = valueText || "—";

    let targetText = "";
    const showTarget = format.showTarget !== false;
    if (showTarget && (render.targetRaw !== null && render.targetRaw !== undefined)) {
      const formattedTarget = formatCounterValue(
        render.targetNumber,
        render.targetRaw,
        format.numberFormat,
        format.prefix,
        format.suffix
      );
      targetText = formattedTarget ? `(${formattedTarget})` : "";
    }

    if (targetText) {
      targetEl.textContent = targetText;
      targetEl.classList.remove("d-none");
    } else {
      targetEl.textContent = "";
      targetEl.classList.add("d-none");
    }

    labelEl.textContent = general.label || render.label || "";
    labelEl.classList.toggle("d-none", !labelEl.textContent);

    let color = null;
    if (typeof render.valueNumber === "number" && typeof render.targetNumber === "number") {
      color = render.valueNumber >= render.targetNumber
        ? resolveCounterColor(format.positiveColor)
        : resolveCounterColor(format.negativeColor);
    }
    valueEl.style.color = color || "";
  };

  window.queryBuilderViz = window.queryBuilderViz || {};
  window.queryBuilderViz.renderChart = renderChart;
  window.queryBuilderViz.renderCounter = renderCounter;

  const renderVisualization = (targetId, payload) => {
    if (payload?.type === "Counter") {
      renderCounter(targetId, payload);
      return;
    }
    renderChart(targetId, payload);
  };

  if (window.queryBuilderViz?.preview) {
    renderVisualization("vizPreview", window.queryBuilderViz.preview);
  }

  if (window.queryBuilderViz?.details) {
    const targetId = window.queryBuilderViz.details?.type === "Counter" ? "vizCounter" : "vizChart";
    renderVisualization(targetId, window.queryBuilderViz.details);
  }

  if (Array.isArray(window.queryBuilderViz?.multi)) {
    window.queryBuilderViz.multi.forEach((item) => {
      renderVisualization(item.canvasId, item);
    });
  }
})();

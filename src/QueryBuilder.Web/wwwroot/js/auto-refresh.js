(() => {
  const config = window.autoRefreshConfig;
  if (!config) {
    return;
  }

  const timers = new Map();
  const locks = new Map();
  const visibleWidgets = new Set();
  let isPageVisible = document.visibilityState === "visible";

  const toIntervalMs = (seconds) => {
    if (!seconds || Number.isNaN(seconds)) return null;
    return seconds * 1000;
  };

  const renderTable = (container, columns, rows) => {
    const table = document.createElement("table");
    table.className = "table table-sm table-hover mb-0";
    const thead = document.createElement("thead");
    const headRow = document.createElement("tr");
    columns.forEach((col) => {
      const th = document.createElement("th");
      th.textContent = col;
      headRow.appendChild(th);
    });
    thead.appendChild(headRow);
    table.appendChild(thead);

    const tbody = document.createElement("tbody");
    if (!rows || rows.length === 0) {
      const tr = document.createElement("tr");
      const td = document.createElement("td");
      td.colSpan = columns.length || 1;
      td.className = "text-muted";
      td.textContent = "No rows returned.";
      tr.appendChild(td);
      tbody.appendChild(tr);
    } else {
      rows.forEach((row) => {
        const tr = document.createElement("tr");
        row.forEach((cell) => {
          const td = document.createElement("td");
          td.textContent = cell ?? "";
          tr.appendChild(td);
        });
        tbody.appendChild(tr);
      });
    }
    table.appendChild(tbody);
    container.innerHTML = "";
    container.appendChild(table);
  };

  const showError = (container, message) => {
    let footer = container.querySelector(".auto-refresh-warning");
    if (!footer) {
      footer = document.createElement("div");
      footer.className = "auto-refresh-warning text-warning small mt-2";
      container.appendChild(footer);
    }
    footer.textContent = message;
  };

  const clearError = (container) => {
    const footer = container.querySelector(".auto-refresh-warning");
    if (footer) {
      footer.remove();
    }
  };

  const refreshWidget = async (widget) => {
    if (!isPageVisible || !widget.autoRefreshEnabled) {
      return;
    }

    if (locks.get(widget.key)) {
      return;
    }

    const container = document.querySelector(`[data-widget-id="${widget.widgetId}"]`) ||
      document.getElementById("viz-details-container");
    if (!container) {
      return;
    }

    if (widget.mode === "dashboard" || widget.mode.startsWith("public")) {
      if (!visibleWidgets.has(widget.widgetId)) {
        return;
      }
    }

    locks.set(widget.key, true);
    try {
      const resolver = window.dashboardParameterResolver;
      const queryString = typeof resolver === "function" ? resolver(widget.widgetId) : "";
      const url = queryString ? `${widget.endpoint}?${queryString}` : widget.endpoint;
      const response = await fetch(url, { credentials: "same-origin" });
      if (!response.ok) {
        showError(container, "Auto refresh failed.");
        return;
      }
      const payload = await response.json();
      if (!payload.success) {
        showError(container, payload.errorMessage || "Auto refresh failed.");
        return;
      }

      clearError(container);
      const body = container.querySelector(`[data-widget-body="${widget.widgetId}"]`) || container;
      if (widget.visualizationType === "Table") {
        renderTable(body, payload.columns || [], payload.rows || []);
      } else if (window.queryBuilderViz) {
        const canvasId = widget.canvasId || "vizChart";
        window.queryBuilderViz.details = window.queryBuilderViz.details || {};
        const baseConfig = widget.chartConfig || window.queryBuilderViz.details;
        const merged = {
          ...baseConfig,
          columns: payload.columns || [],
          rows: payload.rows || []
        };
        const render = window.queryBuilderViz?.renderChart;
        if (typeof render === "function") {
          render(canvasId, merged);
        }
      }
    } catch (err) {
      showError(container, "Auto refresh failed.");
    } finally {
      locks.set(widget.key, false);
    }
  };

  const schedule = (widget) => {
    const intervalMs = toIntervalMs(widget.autoRefreshInterval);
    if (!intervalMs) {
      return;
    }
    const jitter = Math.floor(intervalMs * 0.1 * Math.random());
    const tick = async () => {
      await refreshWidget(widget);
      timers.set(widget.key, setTimeout(tick, intervalMs));
    };
    timers.set(widget.key, setTimeout(tick, jitter));
  };

  const clearAll = () => {
    timers.forEach((timer) => clearTimeout(timer));
    timers.clear();
    locks.clear();
  };

  document.addEventListener("visibilitychange", () => {
    isPageVisible = document.visibilityState === "visible";
  });

  if (config.mode === "dashboard" || config.mode.startsWith("public")) {
    const observer = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        const widgetId = Number(entry.target.dataset.widgetId);
        if (!widgetId) return;
        if (entry.isIntersecting) {
          visibleWidgets.add(widgetId);
        } else {
          visibleWidgets.delete(widgetId);
        }
      });
    }, { threshold: 0.1 });

    document.querySelectorAll("[data-widget-id]").forEach((node) => observer.observe(node));
  }

  const widgets = [];
  if (config.mode === "visualization") {
    widgets.push({
      key: `viz-${config.visualizationId}`,
      widgetId: config.visualizationId,
      visualizationType: document.getElementById("viz-details-container")?.dataset?.visualizationType || "Line",
      autoRefreshEnabled: config.autoRefreshEnabled,
      autoRefreshInterval: config.autoRefreshInterval,
      endpoint: config.endpoint,
      mode: "visualization",
      canvasId: "vizChart",
      chartConfig: window.queryBuilderViz?.details
    });
  } else {
    config.widgets.forEach((widget) => {
      widgets.push({
        key: `widget-${widget.widgetId}`,
        widgetId: widget.widgetId,
        visualizationType: widget.visualizationType,
        autoRefreshEnabled: widget.autoRefreshEnabled,
        autoRefreshInterval: widget.autoRefreshInterval,
        endpoint: widget.endpoint,
        mode: config.mode,
        canvasId: `viz-${widget.widgetId}`,
        chartConfig: window.queryBuilderViz?.multi?.find(item => item.canvasId === `viz-${widget.widgetId}`)
      });
    });
  }

  widgets.forEach((widget) => {
    if (widget.autoRefreshEnabled && widget.autoRefreshInterval) {
      schedule(widget);
    }
  });

  window.addEventListener("beforeunload", clearAll);
})();

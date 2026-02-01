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

  const renderTable = (container, columns, rows, config, allowUnsafeHtml) => {
    if (window.queryBuilderTable?.render) {
      window.queryBuilderTable.render(container, {
        columns,
        rows,
        config: config || {},
        allowUnsafeHtml: allowUnsafeHtml === true
      });
      return;
    }
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
    footer.innerHTML = "";
    if (Array.isArray(message)) {
      if (message.length === 1) {
        footer.textContent = message[0];
        return;
      }
      const list = document.createElement("ul");
      list.className = "mb-0 ps-3";
      message.forEach((item) => {
        const li = document.createElement("li");
        li.textContent = item;
        list.appendChild(li);
      });
      footer.appendChild(list);
      return;
    }
    footer.textContent = message;
  };

  const clearError = (container) => {
    const footer = container.querySelector(".auto-refresh-warning");
    if (footer) {
      footer.remove();
    }
  };

  const buildErrorMessages = (payload, fallback) => {
    if (payload && payload.type === "ParameterValidationError" && Array.isArray(payload.errors)) {
      const messages = payload.errors
        .map((error) => {
          if (!error) return null;
          const message = error.message || "is invalid.";
          if (error.parameter) {
            return `Parameter ${error.parameter} ${message}`;
          }
          return message;
        })
        .filter(Boolean);
      if (messages.length > 0) {
        return messages;
      }
    }
    if (payload && payload.message) {
      return [payload.message];
    }
    if (payload && payload.errorMessage) {
      return [payload.errorMessage];
    }
    return [fallback];
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
      let payload = null;
      try {
        payload = await response.json();
      } catch (err) {
        payload = null;
      }
      if (!response.ok) {
        showError(container, buildErrorMessages(payload, "Auto refresh failed."));
        return;
      }
      if (!payload || !payload.success) {
        showError(container, buildErrorMessages(payload, "Auto refresh failed."));
        return;
      }

      clearError(container);
      const body = container.querySelector(`[data-widget-body="${widget.widgetId}"]`) || container;
      if (widget.visualizationType === "Table") {
        renderTable(body, payload.columns || [], payload.rows || [], widget.tableConfig, widget.allowUnsafeHtml);
      } else if (widget.visualizationType === "Counter") {
        const hostId = widget.canvasId || "vizCounter";
        const baseConfig = widget.counterConfig || window.queryBuilderViz?.details || {};
        const render = window.queryBuilderViz?.renderCounter;
        const counterRender = payload.counter || baseConfig.render || null;
        const counterConfig = baseConfig.config || baseConfig;
        if (typeof render === "function") {
          render(hostId, {
            type: widget.visualizationType,
            config: counterConfig,
            render: counterRender
          });
        }
      } else if (widget.visualizationType === "Funnel") {
        const hostId = widget.canvasId || "vizFunnel";
        const baseConfig = widget.funnelConfig || window.queryBuilderViz?.details || {};
        const render = window.queryBuilderViz?.renderFunnel;
        const funnelRender = payload.funnel || baseConfig.render || null;
        const funnelConfig = baseConfig.config || baseConfig;
        if (typeof render === "function") {
          render(hostId, {
            type: widget.visualizationType,
            config: funnelConfig,
            render: funnelRender
          });
        }
      } else if (window.queryBuilderViz) {
        const canvasId = widget.canvasId || "vizChart";
        const baseConfig = widget.chartConfig || window.queryBuilderViz.details || {};
        const render = window.queryBuilderViz?.renderChart;
        const chartRender = payload.chart || baseConfig.render || null;
        const chartConfig = baseConfig.config || baseConfig;
        if (typeof render === "function") {
          render(canvasId, {
            type: widget.visualizationType,
            config: chartConfig,
            render: chartRender
          });
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
    const vizType = document.getElementById("viz-details-container")?.dataset?.visualizationType || "Line";
    widgets.push({
      key: `viz-${config.visualizationId}`,
      widgetId: config.visualizationId,
      visualizationType: vizType,
      autoRefreshEnabled: config.autoRefreshEnabled,
      autoRefreshInterval: config.autoRefreshInterval,
      endpoint: config.endpoint,
      mode: "visualization",
      canvasId: vizType === "Counter" ? "vizCounter" : vizType === "Funnel" ? "vizFunnel" : "vizChart",
      chartConfig: window.queryBuilderViz?.details,
      counterConfig: window.queryBuilderViz?.details,
      funnelConfig: window.queryBuilderViz?.details,
      tableConfig: window.queryBuilderTable?.details?.config,
      allowUnsafeHtml: window.queryBuilderTable?.details?.allowUnsafeHtml === true
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
        chartConfig: window.queryBuilderViz?.multi?.find(item => item.canvasId === `viz-${widget.widgetId}`),
        tableConfig: widget.tableConfig,
        allowUnsafeHtml: widget.allowUnsafeHtml === true
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

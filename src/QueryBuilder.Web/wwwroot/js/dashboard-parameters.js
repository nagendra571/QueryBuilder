(() => {
  const state = window.dashboardParameters;
  const config = window.dashboardParameterConfig;
  if (!state || !config) return;

  const getValue = (input) => {
    if (!input) return "";
    if (input.type === "checkbox") {
      return input.checked ? "true" : "false";
    }
    return (input.value || "").trim();
  };

  const readDashboardControls = () => {
    const values = new URLSearchParams();
    const useDashboard = document.getElementById("useDashboardFiltersToggle");
    if (useDashboard && !useDashboard.checked) {
      return values;
    }
    document.querySelectorAll("[data-dashboard-control='true']").forEach((input) => {
      const value = getValue(input);
      if (value) {
        values.append(input.name, value);
      }
    });
    return values;
  };

  const readInlineControls = (widgetId) => {
    const values = new URLSearchParams();
    const container = document.querySelector(`[data-widget-id='${widgetId}']`);
    if (!container) return values;
    container.querySelectorAll("[data-inline-control='true']").forEach((input) => {
      const value = getValue(input);
      if (value) {
        values.append(input.name, value);
      }
    });
    return values;
  };

  const buildQueryString = (widgetId) => {
    const params = readDashboardControls();
    const inline = readInlineControls(widgetId);
    inline.forEach((value, key) => params.append(key, value));
    return params.toString();
  };

  const renderTable = (container, columns, rows) => {
    const table = document.createElement("table");
    table.className = "table table-sm table-hover mb-0";
    const thead = document.createElement("thead");
    const headRow = document.createElement("tr");
    (columns || []).forEach((col) => {
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
    let footer = container.querySelector(".param-apply-warning");
    if (!footer) {
      footer = document.createElement("div");
      footer.className = "param-apply-warning text-warning small mt-2";
      container.appendChild(footer);
    }
    footer.textContent = message;
  };

  const clearError = (container) => {
    const footer = container.querySelector(".param-apply-warning");
    if (footer) {
      footer.remove();
    }
  };

  const refreshWidget = async (widget) => {
    const queryString = buildQueryString(widget.widgetId || widget.id);
    const url = queryString ? `${widget.endpoint}?${queryString}` : widget.endpoint;
    const options = config.mode === "public" ? {} : { credentials: "same-origin" };
    const response = await fetch(url, options);
    const container = document.querySelector(`[data-widget-id='${widget.widgetId || widget.id}']`);
    if (!container) return;

    if (!response.ok) {
      showError(container, "Refresh failed.");
      return;
    }

    const payload = await response.json();
    if (!payload.success) {
      showError(container, payload.errorMessage || "Refresh failed.");
      return;
    }

    clearError(container);
    const body = container.querySelector(`[data-widget-body="${widget.widgetId || widget.id}"]`) || container;
    if (widget.visualizationType === "Table") {
      renderTable(body, payload.columns || [], payload.rows || []);
      return;
    }

    const canvasId = `viz-${widget.widgetId || widget.id}`;
    const baseConfig = window.queryBuilderViz?.multi?.find(item => item.canvasId === canvasId) || {};
    const render = window.queryBuilderViz?.renderChart;
    if (typeof render === "function") {
      render(canvasId, {
        ...baseConfig,
        columns: payload.columns || [],
        rows: payload.rows || []
      });
    }
  };

  const applyButton = document.getElementById("dashboardApplyBtn");
  if (applyButton) {
    applyButton.addEventListener("click", async () => {
      applyButton.disabled = true;
      applyButton.textContent = "Applying...";
      try {
        for (const widget of config.widgets) {
          await refreshWidget(widget);
        }
      } finally {
        applyButton.disabled = false;
        applyButton.textContent = "Apply";
      }
    });
  }

  const toggle = document.getElementById("useDashboardFiltersToggle");
  if (toggle) {
    const controls = document.querySelectorAll("[data-dashboard-control='true']");
    const updateControls = () => {
      controls.forEach((input) => {
        input.disabled = !toggle.checked;
      });
    };
    toggle.addEventListener("change", updateControls);
    updateControls();
  }

  window.dashboardParameterResolver = (widgetId) => buildQueryString(widgetId);

  const renderInlineControls = (widgetId, container) => {
    const widgetParams = state.widgetParameters?.find(p => p.widgetId === widgetId);
    if (!widgetParams || !Array.isArray(widgetParams.parameters)) {
      return;
    }

    const mappingMap = new Map();
    (widgetParams.mappings || []).forEach((mapping) => {
      mappingMap.set(mapping.queryParamKey?.toLowerCase(), mapping);
    });

    const inlineParams = widgetParams.parameters.filter((param) => {
      const mapping = mappingMap.get(param.name?.toLowerCase());
      return !mapping || mapping.bindingType === "InlineControl";
    });

    if (inlineParams.length === 0) {
      return;
    }

    const wrapper = document.createElement("div");
    wrapper.className = "dashboard-inline-params";

    inlineParams.forEach((param) => {
      const field = document.createElement("div");
      field.className = "dashboard-inline-field";
      const label = document.createElement("label");
      label.className = "form-label";
      label.textContent = param.title || param.name;
      field.appendChild(label);

      const defaultValue = param.defaultValue || "";
      const inlineKey = (suffix) => `${widgetId}:${param.name}${suffix ? "." + suffix : ""}`;
      const inlineValue = (suffix) => state.parameterState?.inlineValues?.[inlineKey(suffix)] || defaultValue;

      const createInput = (type, name, value) => {
        const input = document.createElement("input");
        input.type = type;
        input.name = name;
        input.value = value || "";
        input.className = "form-control form-control-sm";
        input.setAttribute("data-inline-control", "true");
        return input;
      };

      if (param.type === "Dropdown") {
        const select = document.createElement("select");
        select.name = `wp_${widgetId}_${param.name}`;
        select.className = "form-select form-select-sm";
        select.setAttribute("data-inline-control", "true");
        const options = param.options || [];
        select.appendChild(new Option("Select", ""));
        options.forEach((opt) => {
          const option = new Option(opt.label, opt.value);
          if (opt.value === inlineValue()) {
            option.selected = true;
          }
          select.appendChild(option);
        });
        field.appendChild(select);
      } else if (param.type === "DateRange" || param.type === "DateTimeRange") {
        const wrap = document.createElement("div");
        wrap.className = "d-flex gap-2";
        const startType = param.type === "DateRange" ? "date" : "datetime-local";
        const endType = startType;
        wrap.appendChild(createInput(startType, `wp_${widgetId}_${param.name}_start`, inlineValue("start")));
        wrap.appendChild(createInput(endType, `wp_${widgetId}_${param.name}_end`, inlineValue("end")));
        field.appendChild(wrap);
      } else {
        const inputType = param.type === "Number" ? "number"
          : param.type === "Date" ? "date"
          : param.type === "DateTime" ? "datetime-local"
          : "text";
        field.appendChild(createInput(inputType, `wp_${widgetId}_${param.name}`, inlineValue()));
      }

      wrapper.appendChild(field);
    });

    container.appendChild(wrapper);
  };

  window.dashboardParameters.renderInlineControls = renderInlineControls;
})();

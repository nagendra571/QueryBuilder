(() => {
  const state = window.dashboardBuilder;
  if (!state) return;

  const grid = GridStack.init({ margin: 8, cellHeight: 80, float: true });
  const gridEl = document.querySelector(".grid-stack");
  const querySelect = document.getElementById("widgetQuerySelect");
  const visualizationSelect = document.getElementById("widgetVisualizationSelect");
  const paramTableBody = document.querySelector("#widgetParamTable tbody");
  const confirmAddBtn = document.getElementById("confirmAddWidgetBtn");
  const addWidgetModal = document.getElementById("addWidgetModal");
  const mappingModal = document.getElementById("widgetMappingModal");
  const mappingNew = document.getElementById("mappingNew");
  const mappingExisting = document.getElementById("mappingExisting");
  const mappingWidget = document.getElementById("mappingWidget");
  const mappingStatic = document.getElementById("mappingStatic");
  const mappingNewFields = document.getElementById("mappingNewFields");
  const mappingExistingFields = document.getElementById("mappingExistingFields");
  const mappingStaticFields = document.getElementById("mappingStaticFields");
  const mappingNewKey = document.getElementById("mappingNewKey");
  const mappingNewTitle = document.getElementById("mappingNewTitle");
  const mappingExistingSelect = document.getElementById("mappingExistingSelect");
  const mappingQuickCreateBtn = document.getElementById("mappingQuickCreateBtn");
  const mappingStaticInput = document.getElementById("mappingStaticInput");
  const saveMappingBtn = document.getElementById("saveMappingBtn");
  const saveBtn = document.getElementById("saveLayoutBtn");
  const tokenInput = document.querySelector("input[name='__RequestVerificationToken']");
  const pickerState = {
    queries: [],
    visualizations: [],
    parameters: [],
    queryId: null
  };
  let activeMappingRow = null;
  const bindingTypeMap = {
    DashboardControlKey: 1,
    InlineControl: 2,
    StaticValue: 3
  };

  const addWidget = (widget) => {
    const content = document.createElement("div");
    content.className = "grid-stack-item-content dashboard-widget";
    const header = document.createElement("div");
    header.className = "dashboard-widget-header";
    const title = document.createElement("div");
    title.className = "dashboard-widget-title";
    title.textContent = widget.visualizationName;
    const meta = document.createElement("span");
    meta.className = "badge bg-light text-dark border";
    meta.textContent = widget.visualizationType;
    const actions = document.createElement("div");
    actions.className = "dashboard-widget-actions";
    if (state.editMode) {
      const removeBtn = document.createElement("button");
      removeBtn.type = "button";
      removeBtn.className = "dashboard-widget-action dashboard-widget-remove";
      removeBtn.title = "Remove widget";
      removeBtn.setAttribute("data-action", "remove");
      removeBtn.innerHTML = '<i class="bi bi-x"></i>';
      actions.appendChild(removeBtn);
    }
    actions.appendChild(meta);
    header.appendChild(title);
    header.appendChild(actions);

    const body = document.createElement("div");
    body.className = "dashboard-widget-body";
    const hint = document.createElement("div");
    hint.className = "dashboard-widget-hint";
    hint.textContent = "Drag to reposition · Resize from edges";
    body.appendChild(hint);

    content.appendChild(header);
    content.appendChild(body);

    const item = grid.addWidget({
      x: widget.x,
      y: widget.y,
      w: widget.width || 4,
      h: widget.height || 4,
      content: content.outerHTML
    });
    item.setAttribute("data-visualization-id", widget.visualizationId);
    if (widget.id) {
      item.setAttribute("data-widget-id", widget.id);
    }

    const removeButton = item.querySelector("[data-action='remove']");
    if (removeButton) {
      removeButton.addEventListener("click", (event) => {
        event.preventDefault();
        grid.removeWidget(item);
      });
    }
  };

  state.widgets.forEach((w) => addWidget({
    id: w.id,
    visualizationId: w.visualizationId,
    visualizationName: w.visualizationName,
    visualizationType: w.visualizationType,
    x: w.x,
    y: w.y,
    width: w.width,
    height: w.height
  }));

  const renderQueryOptions = () => {
    if (!querySelect) return;
    querySelect.innerHTML = '<option value="">Select query</option>';
    pickerState.queries.forEach((query) => {
      const option = document.createElement("option");
      option.value = query.id;
      option.textContent = query.name;
      querySelect.appendChild(option);
    });
  };

  const renderVisualizationOptions = (queryId) => {
    if (!visualizationSelect) return;
    visualizationSelect.innerHTML = '<option value="">Select visualization</option>';
    const visuals = queryId
      ? pickerState.visualizations.filter((v) => v.queryId === Number(queryId))
      : pickerState.visualizations;
    visuals.forEach((visualization) => {
      const option = document.createElement("option");
      option.value = visualization.id;
      option.textContent = `${visualization.name} (${visualization.type})`;
      visualizationSelect.appendChild(option);
    });
  };

  const renderParameterTable = (parameters) => {
    if (!paramTableBody) return;
    paramTableBody.innerHTML = "";

    if (!parameters || parameters.length === 0) {
      paramTableBody.innerHTML = '<tr class="text-muted"><td colspan="4">No parameters for this visualization.</td></tr>';
      return;
    }

    parameters.forEach((param) => {
      const row = document.createElement("tr");
      row.dataset.paramKey = param.name;
      row.dataset.paramType = param.type;
      row.dataset.bindingKey = "InlineControl";
      row.innerHTML = `
        <td>${param.title || param.name}</td>
        <td><code>${param.name}</code></td>
        <td class="param-source-cell"><span class="badge bg-light text-dark border">Widget</span></td>
        <td class="param-value-cell">
          <div class="d-flex align-items-center justify-content-between gap-2">
            <span class="text-muted small">Inline control</span>
            <button class="btn btn-sm btn-outline-secondary param-edit-btn" type="button">Edit</button>
          </div>
        </td>
      `;
      paramTableBody.appendChild(row);
    });
  };

  const loadPickerData = async () => {
    try {
      const response = await fetch("/Dashboards/VisualizationPickerData");
      const data = await response.json();
      if (!response.ok || !data.success) {
        return;
      }
      pickerState.queries = data.queries || [];
      pickerState.visualizations = data.visualizations || [];
      renderQueryOptions();
      renderVisualizationOptions();
    } catch {
    }
  };

  const loadVisualizationParameters = async (visualizationId) => {
    if (!visualizationId) {
      pickerState.parameters = [];
      pickerState.queryId = null;
      renderParameterTable([]);
      return;
    }

    try {
      const response = await fetch(`/Dashboards/VisualizationParameters?visualizationId=${encodeURIComponent(visualizationId)}`);
      const data = await response.json();
      if (!response.ok || !data.success) {
        renderParameterTable([]);
        return;
      }
      pickerState.parameters = data.parameters || [];
      pickerState.queryId = data.queryId;
      renderParameterTable(pickerState.parameters);
    } catch {
      renderParameterTable([]);
    }
  };

  addWidgetModal?.addEventListener("show.bs.modal", () => {
    if (!pickerState.queries.length) {
      loadPickerData();
    }
    if (visualizationSelect) {
      visualizationSelect.value = "";
    }
    if (querySelect) {
      querySelect.value = "";
    }
    renderVisualizationOptions();
    renderParameterTable([]);
    if (confirmAddBtn) {
      confirmAddBtn.disabled = true;
    }
  });

  querySelect?.addEventListener("change", () => {
    renderVisualizationOptions(querySelect.value);
    if (visualizationSelect) {
      visualizationSelect.value = "";
    }
    renderParameterTable([]);
    if (confirmAddBtn) {
      confirmAddBtn.disabled = true;
    }
  });

  visualizationSelect?.addEventListener("change", () => {
    const id = visualizationSelect.value;
    if (!id) {
      renderParameterTable([]);
      if (confirmAddBtn) {
        confirmAddBtn.disabled = true;
      }
      return;
    }
    confirmAddBtn && (confirmAddBtn.disabled = false);
    loadVisualizationParameters(id);
  });

  const updateMappingSummary = (row) => {
    const sourceCell = row.querySelector(".param-source-cell");
    const valueCell = row.querySelector(".param-value-cell");
    if (!sourceCell || !valueCell) return;

    const bindingKey = row.dataset.bindingKey || "InlineControl";
    let sourceLabel = "Widget";
    let valueLabel = "Inline control";
    if (bindingKey === "DashboardControlKey") {
      sourceLabel = row.dataset.createControl === "true" ? "Dashboard (new)" : "Dashboard";
      valueLabel = row.dataset.controlTitle || row.dataset.controlKey || "Dashboard control";
    } else if (bindingKey === "StaticValue") {
      sourceLabel = "Static";
      if (row.dataset.paramType === "DateRange" || row.dataset.paramType === "DateTimeRange") {
        const start = row.dataset.staticStart || "";
        const end = row.dataset.staticEnd || "";
        valueLabel = start && end ? `${start} → ${end}` : "Range value";
      } else {
        valueLabel = row.dataset.staticValue || "Static value";
      }
    }

    sourceCell.innerHTML = `<span class="badge bg-light text-dark border">${sourceLabel}</span>`;
    valueCell.innerHTML = `
      <div class="d-flex align-items-center justify-content-between gap-2">
        <span class="text-muted small">${valueLabel}</span>
        <button class="btn btn-sm btn-outline-secondary param-edit-btn" type="button">Edit</button>
      </div>
    `;
  };

  const resetMappingModal = () => {
    if (mappingNew) mappingNew.checked = false;
    if (mappingExisting) mappingExisting.checked = false;
    if (mappingWidget) mappingWidget.checked = true;
    if (mappingStatic) mappingStatic.checked = false;
    mappingNewFields?.classList.add("d-none");
    mappingExistingFields?.classList.add("d-none");
    mappingStaticFields?.classList.add("d-none");
    if (mappingNewKey) mappingNewKey.value = "";
    if (mappingNewTitle) mappingNewTitle.value = "";
    if (mappingExistingSelect) mappingExistingSelect.innerHTML = "";
    if (mappingStaticInput) mappingStaticInput.innerHTML = "";
  };

  const renderExistingControls = () => {
    if (!mappingExistingSelect) return;
    mappingExistingSelect.innerHTML = "";
    const controls = state.parameterControls || [];
    if (!controls.length) {
      const option = document.createElement("option");
      option.value = "";
      option.textContent = "No controls yet";
      mappingExistingSelect.appendChild(option);
      return;
    }
    const empty = document.createElement("option");
    empty.value = "";
    empty.textContent = "Select control";
    mappingExistingSelect.appendChild(empty);
    controls.forEach((control) => {
      const option = document.createElement("option");
      option.value = control.key;
      option.textContent = control.title || control.key;
      mappingExistingSelect.appendChild(option);
    });
  };

  mappingQuickCreateBtn?.addEventListener("click", () => {
    if (!mappingNewKey || !mappingNewTitle || !mappingExistingSelect) return;
    const key = mappingNewKey.value.trim();
    if (!key) return;
    const title = mappingNewTitle.value.trim() || key;

    if (!state.parameterControls) {
      state.parameterControls = [];
    }

    if (!state.parameterControls.some((control) => control.key.toLowerCase() === key.toLowerCase())) {
      state.parameterControls.push({ key, title, type: activeMappingRow?.dataset.paramType || "Text" });
    }

    renderExistingControls();
    mappingExistingSelect.value = key;
    mappingExisting.checked = true;
    showMappingFields("existing");
  });

  const renderStaticInput = (type, startValue, endValue, value) => {
    if (!mappingStaticInput) return;
    mappingStaticInput.innerHTML = "";
    if (type === "DateRange" || type === "DateTimeRange") {
      const inputType = type === "DateRange" ? "date" : "datetime-local";
      mappingStaticInput.innerHTML = `
        <div class="d-flex gap-2">
          <input class="form-control" id="mappingStaticStart" type="${inputType}" value="${startValue || ""}" />
          <input class="form-control" id="mappingStaticEnd" type="${inputType}" value="${endValue || ""}" />
        </div>
      `;
      return;
    }
    const inputType = type === "Number" ? "number" : type === "Date" ? "date" : type === "DateTime" ? "datetime-local" : "text";
    mappingStaticInput.innerHTML = `<input class="form-control" id="mappingStaticValue" type="${inputType}" value="${value || ""}" />`;
  };

  const showMappingFields = (mode) => {
    mappingNewFields?.classList.toggle("d-none", mode !== "new");
    mappingExistingFields?.classList.toggle("d-none", mode !== "existing");
    mappingStaticFields?.classList.toggle("d-none", mode !== "static");
  };

  const openMappingModal = (row) => {
    if (!mappingModal) return;
    activeMappingRow = row;
    resetMappingModal();
    renderExistingControls();
    const bindingKey = row.dataset.bindingKey || "InlineControl";
    if (bindingKey === "DashboardControlKey") {
      if (row.dataset.createControl === "true") {
        mappingNew.checked = true;
        mappingNewFields?.classList.remove("d-none");
        if (mappingNewKey) mappingNewKey.value = row.dataset.controlKey || "";
        if (mappingNewTitle) mappingNewTitle.value = row.dataset.controlTitle || "";
      } else {
        mappingExisting.checked = true;
        mappingExistingFields?.classList.remove("d-none");
        if (mappingExistingSelect) mappingExistingSelect.value = row.dataset.controlKey || "";
      }
    } else if (bindingKey === "StaticValue") {
      mappingStatic.checked = true;
      mappingStaticFields?.classList.remove("d-none");
      renderStaticInput(row.dataset.paramType, row.dataset.staticStart, row.dataset.staticEnd, row.dataset.staticValue);
    } else {
      mappingWidget.checked = true;
    }

    const modal = bootstrap.Modal.getOrCreateInstance(mappingModal);
    modal.show();
  };

  paramTableBody?.addEventListener("click", (event) => {
    const target = event.target;
    if (!target.classList.contains("param-edit-btn")) return;
    const row = target.closest("tr");
    if (!row) return;
    openMappingModal(row);
  });

  [mappingNew, mappingExisting, mappingWidget, mappingStatic].forEach((radio) => {
    radio?.addEventListener("change", () => {
      if (mappingNew?.checked) {
        showMappingFields("new");
      } else if (mappingExisting?.checked) {
        showMappingFields("existing");
      } else if (mappingStatic?.checked) {
        showMappingFields("static");
      } else {
        showMappingFields("widget");
      }
    });
  });

  saveMappingBtn?.addEventListener("click", () => {
    if (!activeMappingRow) return;
    const row = activeMappingRow;
    const paramType = row.dataset.paramType;
    row.dataset.createControl = "false";
    row.dataset.controlKey = "";
    row.dataset.controlTitle = "";
    row.dataset.staticValue = "";
    row.dataset.staticStart = "";
    row.dataset.staticEnd = "";

    if (mappingNew?.checked) {
      const key = mappingNewKey?.value?.trim() || "";
      if (!key) return;
      row.dataset.bindingKey = "DashboardControlKey";
      row.dataset.createControl = "true";
      row.dataset.controlKey = key;
      row.dataset.controlTitle = mappingNewTitle?.value?.trim() || key;
    } else if (mappingExisting?.checked) {
      const key = mappingExistingSelect?.value || "";
      if (!key) return;
      const selected = mappingExistingSelect?.selectedOptions?.[0];
      row.dataset.bindingKey = "DashboardControlKey";
      row.dataset.createControl = "false";
      row.dataset.controlKey = key;
      row.dataset.controlTitle = selected?.textContent || key;
    } else if (mappingStatic?.checked) {
      row.dataset.bindingKey = "StaticValue";
      if (paramType === "DateRange" || paramType === "DateTimeRange") {
        row.dataset.staticStart = document.getElementById("mappingStaticStart")?.value || "";
        row.dataset.staticEnd = document.getElementById("mappingStaticEnd")?.value || "";
      } else {
        row.dataset.staticValue = document.getElementById("mappingStaticValue")?.value || "";
      }
    } else {
      row.dataset.bindingKey = "InlineControl";
    }

    updateMappingSummary(row);
    const modal = bootstrap.Modal.getInstance(mappingModal);
    modal?.hide();
  });

  const getAntiForgeryToken = () =>
    tokenInput?.value || document.querySelector("input[name='__RequestVerificationToken']")?.value || "";

  confirmAddBtn?.addEventListener("click", async () => {
    const visualizationId = Number(visualizationSelect?.value);
    if (!visualizationId) return;

    const mappings = [];
    (paramTableBody?.querySelectorAll("tr") || []).forEach((row) => {
      const paramKey = row.dataset.paramKey;
      if (!paramKey) return;
      const bindingKey = row.dataset.bindingKey || "InlineControl";
      const bindingType = bindingTypeMap[bindingKey] || bindingTypeMap.InlineControl;
      const mapping = {
        queryId: pickerState.queryId,
        queryParamKey: paramKey,
        bindingType
      };

      if (bindingKey === "DashboardControlKey") {
        mapping.controlKey = row.dataset.controlKey || "";
        mapping.controlTitle = row.dataset.controlTitle || "";
        mapping.createControl = row.dataset.createControl === "true";
      } else if (bindingKey === "StaticValue") {
        if (row.dataset.paramType === "DateRange" || row.dataset.paramType === "DateTimeRange") {
          mapping.staticValueStart = row.dataset.staticStart || "";
          mapping.staticValueEnd = row.dataset.staticEnd || "";
        } else {
          mapping.staticValue = row.dataset.staticValue || "";
        }
      }
      mappings.push(mapping);
    });

    const response = await fetch("/Dashboards/AddWidgetFromPicker", {
      method: "POST",
      credentials: "same-origin",
      headers: {
        "Content-Type": "application/json",
        "RequestVerificationToken": getAntiForgeryToken()
      },
      body: JSON.stringify({
        dashboardId: state.dashboardId,
        visualizationId,
        mappings
      })
    });

    if (!response.ok) {
      alert("Failed to add widget.");
      return;
    }

    const data = await response.json();
    if (!data.success) {
      alert("Failed to add widget.");
      return;
    }

    addWidget(data.widget);
    const modalInstance = bootstrap.Modal.getInstance(addWidgetModal);
    modalInstance?.hide();
  });

  saveBtn?.addEventListener("click", async () => {
    const items = [];
    grid.engine.nodes.forEach((node) => {
      const el = node.el;
      items.push({
        id: el?.getAttribute("data-widget-id")
          ? Number(el.getAttribute("data-widget-id"))
          : null,
        visualizationId: Number(el?.getAttribute("data-visualization-id")),
        x: node.x,
        y: node.y,
        width: node.w,
        height: node.h
      });
    });

    const response = await fetch(`/Dashboards/SaveLayout/${state.dashboardId}`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "RequestVerificationToken": tokenInput?.value || ""
      },
      body: JSON.stringify({ widgets: items })
    });

    if (response.ok) {
      saveBtn.textContent = "Saved";
      setTimeout(() => {
        saveBtn.textContent = "Save Layout";
      }, 1500);
    } else {
      alert("Failed to save layout.");
    }
  });
})();

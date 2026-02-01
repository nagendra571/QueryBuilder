(() => {
  const configInput = document.getElementById("FunnelConfigJson");
  const typeSelect = document.getElementById("Type");
  const queryIdInput = document.getElementById("QueryId");
  const executionInput = document.getElementById("LatestExecutionId");
  const previewEmpty = document.getElementById("funnelPreviewEmpty");
  const previewErrors = document.getElementById("funnelPreviewErrors");
  const previewWarnings = document.getElementById("funnelPreviewWarnings");
  const previewLoading = document.getElementById("funnelPreviewLoading");
  const previewDisplay = document.getElementById("funnelPreviewDisplay");

  const stepColumnSelect = document.getElementById("funnelStepColumn");
  const stepDisplayInput = document.getElementById("funnelStepDisplayName");
  const valueColumnSelect = document.getElementById("funnelValueColumn");
  const valueDisplayInput = document.getElementById("funnelValueDisplayName");
  const autoSortToggle = document.getElementById("funnelAutoSort");
  const sortBySelect = document.getElementById("funnelSortBy");

  const stepColumnError = document.getElementById("funnelStepColumnError");
  const valueColumnError = document.getElementById("funnelValueColumnError");

  if (!configInput || !typeSelect) {
    return;
  }

  let funnelConfig = parseConfig(configInput.value);
  let previewTimer = null;

  const setConfigValue = () => {
    configInput.value = JSON.stringify(funnelConfig);
  };

  const normalizeConfig = () => {
    funnelConfig.type = "funnel";
    funnelConfig.stepDisplayName = funnelConfig.stepDisplayName || "Steps";
    funnelConfig.valueDisplayName = funnelConfig.valueDisplayName || "Value";
    if (typeof funnelConfig.autoSort !== "boolean") {
      funnelConfig.autoSort = false;
    }
    funnelConfig.sortDirection = funnelConfig.sortDirection || "desc";
    if (typeof funnelConfig.treatNullAsZero !== "boolean") {
      funnelConfig.treatNullAsZero = true;
    }
  };

  const applyConfigToUi = () => {
    normalizeConfig();
    if (stepColumnSelect) stepColumnSelect.value = funnelConfig.stepColumn || "";
    if (stepDisplayInput) stepDisplayInput.value = funnelConfig.stepDisplayName || "Steps";
    if (valueColumnSelect) valueColumnSelect.value = funnelConfig.valueColumn || "";
    if (valueDisplayInput) valueDisplayInput.value = funnelConfig.valueDisplayName || "Value";
    if (autoSortToggle) autoSortToggle.checked = funnelConfig.autoSort === true;
    if (sortBySelect) sortBySelect.value = funnelConfig.sortByColumn || funnelConfig.valueColumn || "";
    updateSortState();
  };

  const readUiToConfig = () => {
    funnelConfig.stepColumn = stepColumnSelect?.value || null;
    funnelConfig.stepDisplayName = stepDisplayInput?.value || "Steps";
    funnelConfig.valueColumn = valueColumnSelect?.value || null;
    funnelConfig.valueDisplayName = valueDisplayInput?.value || "Value";
    funnelConfig.autoSort = autoSortToggle?.checked ?? false;
    funnelConfig.sortByColumn = sortBySelect?.value || funnelConfig.valueColumn || null;
    funnelConfig.sortDirection = "desc";
    funnelConfig.treatNullAsZero = true;
  };

  const updateSortState = () => {
    if (!sortBySelect) return;
    sortBySelect.disabled = autoSortToggle?.checked !== true;
  };

  const updateValidation = () => {
    const hasStep = !!funnelConfig.stepColumn;
    const hasValue = !!funnelConfig.valueColumn;
    if (stepColumnError) stepColumnError.classList.toggle("d-none", hasStep);
    if (valueColumnError) valueColumnError.classList.toggle("d-none", hasValue);
  };

  const schedulePreview = () => {
    if (typeSelect.value !== "Funnel") return;
    if (previewTimer) {
      clearTimeout(previewTimer);
    }
    previewTimer = setTimeout(loadPreview, 200);
  };

  const loadPreview = async () => {
    readUiToConfig();
    setConfigValue();
    updateValidation();

    if (previewEmpty) previewEmpty.classList.add("d-none");
    if (previewErrors) previewErrors.classList.add("d-none");
    if (previewWarnings) previewWarnings.classList.add("d-none");
    if (previewLoading) previewLoading.classList.remove("d-none");

    const queryId = Number(queryIdInput?.value || 0);
    if (!queryId) {
      if (previewLoading) previewLoading.classList.add("d-none");
      if (previewEmpty) previewEmpty.classList.remove("d-none");
      return;
    }

    const executionId = Number(executionInput?.value || 0) || null;
    const token = document.querySelector('input[name="__RequestVerificationToken"]');
    try {
      const response = await fetch("/visualizations/funnel-preview", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "RequestVerificationToken": token?.value || ""
        },
        body: JSON.stringify({
          queryId,
          executionId,
          config: funnelConfig
        })
      });

      const payload = await response.json();
      if (!response.ok || payload.success !== true) {
        if (Array.isArray(payload?.errors) && payload.errors.length > 0 && previewErrors) {
          previewErrors.textContent = payload.errors.join(" ");
          previewErrors.classList.remove("d-none");
        } else if (payload?.errorMessage && previewErrors) {
          previewErrors.textContent = payload.errorMessage;
          previewErrors.classList.remove("d-none");
        } else if (previewEmpty) {
          previewEmpty.classList.remove("d-none");
        }
        return;
      }

      if (Array.isArray(payload.warnings) && payload.warnings.length > 0 && previewWarnings) {
        previewWarnings.textContent = payload.warnings.join(" ");
        previewWarnings.classList.remove("d-none");
      }

      if (window.queryBuilderViz?.renderFunnel && previewDisplay) {
        window.queryBuilderViz.renderFunnel(previewDisplay.id, {
          type: "Funnel",
          config: funnelConfig,
          render: payload.render
        });
      }
    } catch (err) {
      if (previewEmpty) previewEmpty.classList.remove("d-none");
    } finally {
      if (previewLoading) previewLoading.classList.add("d-none");
    }
  };

  const bindInputs = () => {
    [
      stepColumnSelect,
      stepDisplayInput,
      valueColumnSelect,
      valueDisplayInput,
      autoSortToggle,
      sortBySelect
    ].forEach((element) => {
      if (!element) return;
      const event = element.tagName === "SELECT" || element.type === "checkbox" ? "change" : "input";
      element.addEventListener(event, () => {
        if (element === autoSortToggle) {
          updateSortState();
        }
        if (element === valueColumnSelect && sortBySelect && !sortBySelect.value) {
          sortBySelect.value = valueColumnSelect.value;
        }
        schedulePreview();
      });
    });
  };

  const updateColumnOptions = (select, columns, includeEmpty) => {
    if (!select) return;
    const current = select.value;
    select.innerHTML = "";
    if (includeEmpty) {
      const empty = document.createElement("option");
      empty.value = "";
      empty.textContent = "Choose column...";
      select.appendChild(empty);
    }
    columns.forEach((column) => {
      const option = document.createElement("option");
      option.value = column;
      option.textContent = column;
      option.selected = current === column;
      select.appendChild(option);
    });
  };

  const loadLatestColumns = async () => {
    const queryId = Number(queryIdInput?.value || 0);
    if (!queryId) return;
    try {
      const executionId = Number(executionInput?.value || 0) || "";
      const response = await fetch(`/queries/${encodeURIComponent(queryId)}/executions/latest?executionId=${encodeURIComponent(executionId)}&maxRows=1`);
      const payload = await response.json();
      if (!response.ok || payload.success !== true) {
        return;
      }
      const cols = Array.isArray(payload.columns) ? payload.columns.map((c) => c.name || c) : [];
      if (!cols.length) return;
      updateColumnOptions(stepColumnSelect, cols, true);
      updateColumnOptions(valueColumnSelect, cols, true);
      updateColumnOptions(sortBySelect, cols, true);
    } catch (err) {
      return;
    }
  };

  function parseConfig(value) {
    if (!value) return {};
    try {
      return JSON.parse(value) || {};
    } catch (err) {
      return {};
    }
  }

  const init = () => {
    normalizeConfig();
    applyConfigToUi();
    setConfigValue();
    bindInputs();
    loadLatestColumns();
    if (typeSelect.value === "Funnel") {
      loadPreview();
    }
  };

  typeSelect?.addEventListener("change", () => {
    if (typeSelect.value === "Funnel") {
      loadPreview();
    }
  });

  init();
})();

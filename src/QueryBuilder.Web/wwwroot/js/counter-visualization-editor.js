(() => {
  const configInput = document.getElementById("CounterConfigJson");
  const typeSelect = document.getElementById("Type");
  const queryIdInput = document.getElementById("QueryId");
  const executionInput = document.getElementById("LatestExecutionId");
  const previewEmpty = document.getElementById("counterPreviewEmpty");
  const previewErrors = document.getElementById("counterPreviewErrors");
  const previewWarnings = document.getElementById("counterPreviewWarnings");
  const previewLoading = document.getElementById("counterPreviewLoading");
  const previewDisplay = document.getElementById("counterPreviewDisplay");

  const labelInput = document.getElementById("counterLabel");
  const valueColumnSelect = document.getElementById("counterValueColumn");
  const valueRowInput = document.getElementById("counterValueRow");
  const targetColumnSelect = document.getElementById("counterTargetColumn");
  const targetRowInput = document.getElementById("counterTargetRow");
  const countRowsToggle = document.getElementById("counterCountRows");

  const numberFormatInput = document.getElementById("counterNumberFormat");
  const showTargetToggle = document.getElementById("counterShowTarget");
  const prefixInput = document.getElementById("counterPrefix");
  const suffixInput = document.getElementById("counterSuffix");
  const positiveColorSelect = document.getElementById("counterPositiveColor");
  const negativeColorSelect = document.getElementById("counterNegativeColor");

  const valueColumnError = document.getElementById("counterValueColumnError");
  const valueRowError = document.getElementById("counterValueRowError");

  if (!configInput || !typeSelect) {
    return;
  }

  let counterConfig = parseConfig(configInput.value);
  let previewTimer = null;

  const setConfigValue = () => {
    configInput.value = JSON.stringify(counterConfig);
  };

  const normalizeConfig = () => {
    counterConfig.type = "counter";
    counterConfig.general = counterConfig.general || {};
    counterConfig.format = counterConfig.format || {};
    if (typeof counterConfig.general.countRows !== "boolean") {
      counterConfig.general.countRows = false;
    }
    counterConfig.general.valueRow = counterConfig.general.valueRow || 1;
    counterConfig.general.targetRow = counterConfig.general.targetRow || 1;
    if (typeof counterConfig.format.showTarget !== "boolean") {
      counterConfig.format.showTarget = true;
    }
    counterConfig.format.numberFormat = counterConfig.format.numberFormat || "0,0";
    counterConfig.format.prefix = counterConfig.format.prefix || "";
    counterConfig.format.suffix = counterConfig.format.suffix || "";
    counterConfig.format.positiveColor = counterConfig.format.positiveColor || "green";
    counterConfig.format.negativeColor = counterConfig.format.negativeColor || "red";
  };

  const applyConfigToUi = () => {
    normalizeConfig();
    if (labelInput) labelInput.value = counterConfig.general.label || "";
    if (valueColumnSelect) valueColumnSelect.value = counterConfig.general.valueColumn || "";
    if (valueRowInput) valueRowInput.value = counterConfig.general.valueRow || 1;
    if (targetColumnSelect) targetColumnSelect.value = counterConfig.general.targetColumn || "";
    if (targetRowInput) targetRowInput.value = counterConfig.general.targetRow || 1;
    if (countRowsToggle) countRowsToggle.checked = counterConfig.general.countRows === true;
    if (numberFormatInput) numberFormatInput.value = counterConfig.format.numberFormat || "0,0";
    if (showTargetToggle) showTargetToggle.checked = counterConfig.format.showTarget !== false;
    if (prefixInput) prefixInput.value = counterConfig.format.prefix || "";
    if (suffixInput) suffixInput.value = counterConfig.format.suffix || "";
    if (positiveColorSelect) positiveColorSelect.value = counterConfig.format.positiveColor || "green";
    if (negativeColorSelect) negativeColorSelect.value = counterConfig.format.negativeColor || "red";
    updateCountRowsState();
  };

  const readUiToConfig = () => {
    counterConfig.general.label = labelInput?.value || "";
    counterConfig.general.valueColumn = valueColumnSelect?.value || null;
    counterConfig.general.valueRow = parseNullableInt(valueRowInput?.value) || 1;
    counterConfig.general.targetColumn = targetColumnSelect?.value || null;
    counterConfig.general.targetRow = parseNullableInt(targetRowInput?.value) || 1;
    counterConfig.general.countRows = countRowsToggle?.checked ?? false;
    counterConfig.format.numberFormat = numberFormatInput?.value || "0,0";
    counterConfig.format.showTarget = showTargetToggle?.checked ?? true;
    counterConfig.format.prefix = prefixInput?.value || "";
    counterConfig.format.suffix = suffixInput?.value || "";
    counterConfig.format.positiveColor = positiveColorSelect?.value || "green";
    counterConfig.format.negativeColor = negativeColorSelect?.value || "red";
  };

  const parseNullableInt = (value) => {
    if (value === null || value === undefined || value === "") return null;
    const parsed = Number(value);
    return Number.isFinite(parsed) ? Math.trunc(parsed) : null;
  };

  const updateCountRowsState = () => {
    const isCountRows = countRowsToggle?.checked === true;
    if (valueColumnSelect) valueColumnSelect.disabled = isCountRows;
    if (valueRowInput) valueRowInput.disabled = isCountRows;
  };

  const updateValidation = () => {
    const isCountRows = counterConfig.general.countRows === true;
    const hasValueColumn = !!counterConfig.general.valueColumn;
    const hasRow = (counterConfig.general.valueRow || 0) >= 1;
    if (valueColumnError) valueColumnError.classList.toggle("d-none", isCountRows || hasValueColumn);
    if (valueRowError) valueRowError.classList.toggle("d-none", isCountRows || hasRow);
  };

  const schedulePreview = () => {
    if (typeSelect.value !== "Counter") return;
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
      const response = await fetch("/visualizations/counter-preview", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "RequestVerificationToken": token?.value || ""
        },
        body: JSON.stringify({
          queryId,
          executionId,
          config: counterConfig
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

      if (window.queryBuilderViz?.renderCounter && previewDisplay) {
        window.queryBuilderViz.renderCounter(previewDisplay.id, {
          type: "Counter",
          config: counterConfig,
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
      labelInput,
      valueColumnSelect,
      valueRowInput,
      targetColumnSelect,
      targetRowInput,
      countRowsToggle,
      numberFormatInput,
      showTargetToggle,
      prefixInput,
      suffixInput,
      positiveColorSelect,
      negativeColorSelect
    ].forEach((element) => {
      if (!element) return;
      const event = element.tagName === "SELECT" || element.type === "checkbox" ? "change" : "input";
      element.addEventListener(event, () => {
        if (element === countRowsToggle) {
          updateCountRowsState();
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
      updateColumnOptions(valueColumnSelect, cols, true);
      updateColumnOptions(targetColumnSelect, cols, true);
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
    if (typeSelect.value === "Counter") {
      loadPreview();
    }
  };

  typeSelect?.addEventListener("change", () => {
    if (typeSelect.value === "Counter") {
      loadPreview();
    }
  });

  init();
})();

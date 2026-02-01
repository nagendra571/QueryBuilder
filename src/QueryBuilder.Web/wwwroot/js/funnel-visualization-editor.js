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
  const valueBarColorSelect = document.getElementById("funnelValueBarColor");
  const prevBarColorSelect = document.getElementById("funnelPrevBarColor");
  const headerTextColorSelect = document.getElementById("funnelHeaderTextColor");
  const autoColorStepsToggle = document.getElementById("funnelAutoColorSteps");
  const valueBarHeightInput = document.getElementById("funnelValueBarHeight");
  const prevBarHeightInput = document.getElementById("funnelPrevBarHeight");
  const barRadiusInput = document.getElementById("funnelBarRadius");
  const showValueBarToggle = document.getElementById("funnelShowValueBar");
  const showPrevBarToggle = document.getElementById("funnelShowPrevBar");
  const valueNumberFormatInput = document.getElementById("funnelValueNumberFormat");
  const nullHandlingSelect = document.getElementById("funnelNullHandling");
  const percentPrecisionInput = document.getElementById("funnelPercentPrecision");
  const capPercentPreviousInput = document.getElementById("funnelCapPercentPrevious");
  const showPercentSignToggle = document.getElementById("funnelShowPercentSign");
  const topNInput = document.getElementById("funnelTopN");
  const includeOthersToggle = document.getElementById("funnelIncludeOthers");
  const aggregateStepsToggle = document.getElementById("funnelAggregateSteps");
  const aggregationSelect = document.getElementById("funnelAggregation");

  const stepColumnError = document.getElementById("funnelStepColumnError");
  const valueColumnError = document.getElementById("funnelValueColumnError");

  if (!configInput || !typeSelect) {
    return;
  }

  let funnelConfig = parseConfig(configInput.value);
  let previewTimer = null;
  const stepPalette = [
    "teal",
    "blue",
    "green",
    "orange",
    "red",
    "purple",
    "cyan",
    "gray"
  ];

  const setConfigValue = () => {
    configInput.value = JSON.stringify(funnelConfig);
  };

  const readNumber = (element, fallback) => {
    const value = Number(element?.value);
    return Number.isFinite(value) ? value : fallback;
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
    funnelConfig.format = funnelConfig.format || {};
    normalizeFormat(funnelConfig.format);
  };

  const normalizeFormat = (format) => {
    format.valueBarColor = format.valueBarColor || "teal";
    format.previousBarColor = format.previousBarColor || "gray";
    format.valueBarHeight = Number.isFinite(format.valueBarHeight) ? format.valueBarHeight : 22;
    format.previousBarHeight = Number.isFinite(format.previousBarHeight) ? format.previousBarHeight : 18;
    format.barRadius = Number.isFinite(format.barRadius) ? format.barRadius : 3;
    if (typeof format.showValueBar !== "boolean") format.showValueBar = true;
    if (typeof format.showPreviousBar !== "boolean") format.showPreviousBar = true;
    format.valueNumberFormat = format.valueNumberFormat || "0,0";
    format.percentPrecision = Number.isFinite(format.percentPrecision) ? format.percentPrecision : 2;
    if (typeof format.showPercentSign !== "boolean") format.showPercentSign = true;
    format.capPercentPrevious = Number.isFinite(format.capPercentPrevious) ? format.capPercentPrevious : 250;
    format.topN = Number.isFinite(format.topN) ? format.topN : 0;
    if (typeof format.includeOthers !== "boolean") format.includeOthers = true;
    if (typeof format.aggregateSteps !== "boolean") format.aggregateSteps = true;
    format.aggregation = format.aggregation || "sum";
    format.nullHandling = format.nullHandling || (funnelConfig.treatNullAsZero ? "zero" : "skip");
    if (typeof format.autoColorByStep !== "boolean") format.autoColorByStep = false;
    format.stepColors = format.stepColors || {};
  };

  const applyConfigToUi = () => {
    normalizeConfig();
    if (stepColumnSelect) stepColumnSelect.value = funnelConfig.stepColumn || "";
    if (stepDisplayInput) stepDisplayInput.value = funnelConfig.stepDisplayName || "Steps";
    if (valueColumnSelect) valueColumnSelect.value = funnelConfig.valueColumn || "";
    if (valueDisplayInput) valueDisplayInput.value = funnelConfig.valueDisplayName || "Value";
    if (autoSortToggle) autoSortToggle.checked = funnelConfig.autoSort === true;
    if (sortBySelect) sortBySelect.value = funnelConfig.sortByColumn || funnelConfig.valueColumn || "";
    if (valueBarColorSelect) valueBarColorSelect.value = funnelConfig.format.valueBarColor || "teal";
    if (prevBarColorSelect) prevBarColorSelect.value = funnelConfig.format.previousBarColor || "gray";
    if (headerTextColorSelect) headerTextColorSelect.value = funnelConfig.format.headerTextColor || "";
    if (autoColorStepsToggle) autoColorStepsToggle.checked = funnelConfig.format.autoColorByStep === true;
    if (valueBarHeightInput) valueBarHeightInput.value = funnelConfig.format.valueBarHeight ?? 22;
    if (prevBarHeightInput) prevBarHeightInput.value = funnelConfig.format.previousBarHeight ?? 18;
    if (barRadiusInput) barRadiusInput.value = funnelConfig.format.barRadius ?? 3;
    if (showValueBarToggle) showValueBarToggle.checked = funnelConfig.format.showValueBar !== false;
    if (showPrevBarToggle) showPrevBarToggle.checked = funnelConfig.format.showPreviousBar !== false;
    if (valueNumberFormatInput) valueNumberFormatInput.value = funnelConfig.format.valueNumberFormat || "0,0";
    if (nullHandlingSelect) nullHandlingSelect.value = funnelConfig.format.nullHandling || "zero";
    if (percentPrecisionInput) percentPrecisionInput.value = funnelConfig.format.percentPrecision ?? 2;
    if (capPercentPreviousInput) capPercentPreviousInput.value = funnelConfig.format.capPercentPrevious ?? 250;
    if (showPercentSignToggle) showPercentSignToggle.checked = funnelConfig.format.showPercentSign !== false;
    if (topNInput) topNInput.value = funnelConfig.format.topN ?? 0;
    if (includeOthersToggle) includeOthersToggle.checked = funnelConfig.format.includeOthers !== false;
    if (aggregateStepsToggle) aggregateStepsToggle.checked = funnelConfig.format.aggregateSteps !== false;
    if (aggregationSelect) aggregationSelect.value = funnelConfig.format.aggregation || "sum";
    updateSortState();
    updateFormatState();
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
    funnelConfig.format = funnelConfig.format || {};
    funnelConfig.format.valueBarColor = valueBarColorSelect?.value || "teal";
    funnelConfig.format.previousBarColor = prevBarColorSelect?.value || "gray";
    funnelConfig.format.headerTextColor = headerTextColorSelect?.value || null;
    funnelConfig.format.autoColorByStep = autoColorStepsToggle?.checked ?? false;
    funnelConfig.format.valueBarHeight = readNumber(valueBarHeightInput, 22);
    funnelConfig.format.previousBarHeight = readNumber(prevBarHeightInput, 18);
    funnelConfig.format.barRadius = readNumber(barRadiusInput, 3);
    funnelConfig.format.showValueBar = showValueBarToggle?.checked ?? true;
    funnelConfig.format.showPreviousBar = showPrevBarToggle?.checked ?? true;
    funnelConfig.format.valueNumberFormat = valueNumberFormatInput?.value || "0,0";
    funnelConfig.format.nullHandling = nullHandlingSelect?.value || "zero";
    funnelConfig.format.percentPrecision = readNumber(percentPrecisionInput, 2);
    funnelConfig.format.capPercentPrevious = readNumber(capPercentPreviousInput, 250);
    funnelConfig.format.showPercentSign = showPercentSignToggle?.checked ?? true;
    funnelConfig.format.topN = readNumber(topNInput, 0);
    funnelConfig.format.includeOthers = includeOthersToggle?.checked ?? true;
    funnelConfig.format.aggregateSteps = aggregateStepsToggle?.checked ?? true;
    funnelConfig.format.aggregation = aggregationSelect?.value || "sum";
    funnelConfig.format.stepColors = funnelConfig.format.stepColors || {};
  };

  const updateSortState = () => {
    if (!sortBySelect) return;
    sortBySelect.disabled = autoSortToggle?.checked !== true;
  };

  const updateFormatState = () => {
    if (aggregationSelect) aggregationSelect.disabled = aggregateStepsToggle?.checked !== true;
    if (valueBarColorSelect) valueBarColorSelect.disabled = autoColorStepsToggle?.checked === true;
    if (includeOthersToggle) {
      const topNValue = readNumber(topNInput, 0);
      includeOthersToggle.disabled = topNValue <= 0;
    }
  };

  const buildStepColorMap = (rows) => {
    if (!Array.isArray(rows) || rows.length === 0) return;
    funnelConfig.format.stepColors = funnelConfig.format.stepColors || {};
    rows.forEach((row, index) => {
      const step = row?.stepLabel;
      if (!step || funnelConfig.format.stepColors[step]) return;
      funnelConfig.format.stepColors[step] = stepPalette[index % stepPalette.length];
    });
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

      if (funnelConfig.format?.autoColorByStep) {
        buildStepColorMap(payload.render?.rows);
        setConfigValue();
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
      sortBySelect,
      valueBarColorSelect,
      prevBarColorSelect,
      headerTextColorSelect,
      autoColorStepsToggle,
      valueBarHeightInput,
      prevBarHeightInput,
      barRadiusInput,
      showValueBarToggle,
      showPrevBarToggle,
      valueNumberFormatInput,
      nullHandlingSelect,
      percentPrecisionInput,
      capPercentPreviousInput,
      showPercentSignToggle,
      topNInput,
      includeOthersToggle,
      aggregateStepsToggle,
      aggregationSelect
    ].forEach((element) => {
      if (!element) return;
      const event = element.tagName === "SELECT" || element.type === "checkbox" ? "change" : "input";
      element.addEventListener(event, () => {
        if (element === autoSortToggle) {
          updateSortState();
        }
        if (element === aggregateStepsToggle || element === autoColorStepsToggle) {
          updateFormatState();
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

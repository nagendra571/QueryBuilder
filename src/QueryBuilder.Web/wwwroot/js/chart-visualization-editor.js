(() => {
  const configInput = document.getElementById("ChartConfigJson");
  const typeSelect = document.getElementById("Type");
  const queryIdInput = document.getElementById("QueryId");
  const executionInput = document.getElementById("LatestExecutionId");
  const chartPreviewEmpty = document.getElementById("chartPreviewEmpty");
  const chartPreviewErrors = document.getElementById("chartPreviewErrors");
  const chartPreviewWarnings = document.getElementById("chartPreviewWarnings");
  const chartPreviewLoading = document.getElementById("chartPreviewLoading");

  const chartTypeSelect = document.getElementById("chartTypeSelect");
  const xColumnSelect = document.getElementById("chartXColumnSelect");
  const yColumnsSelect = document.getElementById("chartYColumnsSelect");
  const groupBySelect = document.getElementById("chartGroupBySelect");
  const errorsSelect = document.getElementById("chartErrorsSelect");
  const showLegendToggle = document.getElementById("chartShowLegend");
  const stackingSelect = document.getElementById("chartStackingSelect");
  const normalizeToggle = document.getElementById("chartNormalizeToPercent");
  const nullAsZeroToggle = document.getElementById("chartNullAsZero");
  const xAxisScale = document.getElementById("chartXAxisScale");
  const xAxisName = document.getElementById("chartXAxisName");
  const sortValuesToggle = document.getElementById("chartSortValues");
  const reverseOrderToggle = document.getElementById("chartReverseOrder");
  const showLabelsToggle = document.getElementById("chartShowLabels");
  const yAxisLeftScale = document.getElementById("chartYAxisLeftScale");
  const yAxisLeftName = document.getElementById("chartYAxisLeftName");
  const yAxisLeftMin = document.getElementById("chartYAxisLeftMin");
  const yAxisLeftMax = document.getElementById("chartYAxisLeftMax");
  const yAxisLeftReverse = document.getElementById("chartYAxisLeftReverse");
  const yAxisRightScale = document.getElementById("chartYAxisRightScale");
  const yAxisRightName = document.getElementById("chartYAxisRightName");
  const yAxisRightMin = document.getElementById("chartYAxisRightMin");
  const yAxisRightMax = document.getElementById("chartYAxisRightMax");
  const yAxisRightReverse = document.getElementById("chartYAxisRightReverse");
  const seriesTableBody = document.querySelector("#chartSeriesTable tbody");
  const seriesEmpty = document.getElementById("chartSeriesEmpty");
  const colorsList = document.getElementById("chartColorsList");
  const colorsEmpty = document.getElementById("chartColorsEmpty");
  const dataLabelsEnabled = document.getElementById("chartDataLabelsEnabled");
  const dataLabelsNumber = document.getElementById("chartDataLabelsNumberFormat");
  const dataLabelsPercent = document.getElementById("chartDataLabelsPercentFormat");
  const dataLabelsDate = document.getElementById("chartDataLabelsDateFormat");
  const dataLabelsTemplate = document.getElementById("chartDataLabelsTemplate");
  const xColumnError = document.getElementById("chartXColumnError");
  const yColumnsError = document.getElementById("chartYColumnsError");

  if (!configInput || !typeSelect) {
    return;
  }

  const colorOptions = [
    { value: "automatic", label: "Automatic" },
    { value: "blue", label: "Blue" },
    { value: "red", label: "Red" },
    { value: "green", label: "Green" },
    { value: "purple", label: "Purple" },
    { value: "cyan", label: "Cyan" },
    { value: "orange", label: "Orange" },
    { value: "light blue", label: "Light blue" },
    { value: "teal", label: "Teal" },
    { value: "yellow", label: "Yellow" },
    { value: "gray", label: "Gray" }
  ];

  let chartConfig = parseConfig(configInput.value);
  let previewTimer = null;
  let latestRender = null;

  const setConfigValue = () => {
    configInput.value = JSON.stringify(chartConfig);
  };

  const normalizeConfig = () => {
    chartConfig.type = chartConfig.type || "chart";
    chartConfig.general = chartConfig.general || {};
    chartConfig.general.chartType = chartConfig.general.chartType || "bar";
    chartConfig.general.yColumns = Array.isArray(chartConfig.general.yColumns)
      ? chartConfig.general.yColumns
      : [];
    chartConfig.general.stacking = chartConfig.general.stacking || "disabled";
    if (typeof chartConfig.general.showLegend !== "boolean") {
      chartConfig.general.showLegend = true;
    }
    if (typeof chartConfig.general.nullAsZero !== "boolean") {
      chartConfig.general.nullAsZero = true;
    }
    chartConfig.xAxis = chartConfig.xAxis || {};
    chartConfig.xAxis.scale = chartConfig.xAxis.scale || "auto";
    if (typeof chartConfig.xAxis.sortValues !== "boolean") chartConfig.xAxis.sortValues = true;
    if (typeof chartConfig.xAxis.showLabels !== "boolean") chartConfig.xAxis.showLabels = true;
    chartConfig.yAxis = chartConfig.yAxis || {};
    chartConfig.yAxis.left = chartConfig.yAxis.left || { scale: "linear" };
    chartConfig.yAxis.right = chartConfig.yAxis.right || { scale: "linear" };
    chartConfig.series = Array.isArray(chartConfig.series) ? chartConfig.series : [];
    chartConfig.colors = chartConfig.colors || {};
    chartConfig.dataLabels = chartConfig.dataLabels || {};
    if (typeof chartConfig.dataLabels.enabled !== "boolean") {
      chartConfig.dataLabels.enabled = false;
    }
  };

  const applyConfigToUi = () => {
    normalizeConfig();
    if (chartTypeSelect) chartTypeSelect.value = chartConfig.general.chartType || "bar";
    if (xColumnSelect) xColumnSelect.value = chartConfig.general.xColumn || "";
    if (groupBySelect) groupBySelect.value = chartConfig.general.groupBy || "";
    if (errorsSelect) errorsSelect.value = chartConfig.general.errorsColumn || "";
    if (showLegendToggle) showLegendToggle.checked = chartConfig.general.showLegend !== false;
    if (stackingSelect) stackingSelect.value = chartConfig.general.stacking || "disabled";
    if (normalizeToggle) normalizeToggle.checked = chartConfig.general.normalizeToPercent === true;
    if (nullAsZeroToggle) nullAsZeroToggle.checked = chartConfig.general.nullAsZero !== false;
    if (xAxisScale) xAxisScale.value = chartConfig.xAxis.scale || "auto";
    if (xAxisName) xAxisName.value = chartConfig.xAxis.name || "";
    if (sortValuesToggle) sortValuesToggle.checked = chartConfig.xAxis.sortValues !== false;
    if (reverseOrderToggle) reverseOrderToggle.checked = chartConfig.xAxis.reverseOrder === true;
    if (showLabelsToggle) showLabelsToggle.checked = chartConfig.xAxis.showLabels !== false;
    if (yAxisLeftScale) yAxisLeftScale.value = chartConfig.yAxis.left?.scale || "linear";
    if (yAxisLeftName) yAxisLeftName.value = chartConfig.yAxis.left?.name || "";
    if (yAxisLeftMin) yAxisLeftMin.value = chartConfig.yAxis.left?.min ?? "";
    if (yAxisLeftMax) yAxisLeftMax.value = chartConfig.yAxis.left?.max ?? "";
    if (yAxisLeftReverse) yAxisLeftReverse.checked = chartConfig.yAxis.left?.reverse === true;
    if (yAxisRightScale) yAxisRightScale.value = chartConfig.yAxis.right?.scale || "linear";
    if (yAxisRightName) yAxisRightName.value = chartConfig.yAxis.right?.name || "";
    if (yAxisRightMin) yAxisRightMin.value = chartConfig.yAxis.right?.min ?? "";
    if (yAxisRightMax) yAxisRightMax.value = chartConfig.yAxis.right?.max ?? "";
    if (yAxisRightReverse) yAxisRightReverse.checked = chartConfig.yAxis.right?.reverse === true;
    if (dataLabelsEnabled) dataLabelsEnabled.checked = chartConfig.dataLabels.enabled === true;
    if (dataLabelsNumber) dataLabelsNumber.value = chartConfig.dataLabels.numberFormat || "0,0.00";
    if (dataLabelsPercent) dataLabelsPercent.value = chartConfig.dataLabels.percentFormat || "0[.]00%";
    if (dataLabelsDate) dataLabelsDate.value = chartConfig.dataLabels.dateTimeFormat || "DD/MM/YY HH:mm";
    if (dataLabelsTemplate) dataLabelsTemplate.value = chartConfig.dataLabels.labelTemplate || "auto";
    syncMultiSelect(yColumnsSelect, chartConfig.general.yColumns);
  };

  const syncMultiSelect = (select, values) => {
    if (!select) return;
    const valueSet = new Set(values || []);
    Array.from(select.options).forEach((opt) => {
      opt.selected = valueSet.has(opt.value);
    });
  };

  const readUiToConfig = () => {
    chartConfig.general.chartType = chartTypeSelect?.value || "bar";
    chartConfig.general.xColumn = xColumnSelect?.value || null;
    chartConfig.general.groupBy = groupBySelect?.value || null;
    chartConfig.general.errorsColumn = errorsSelect?.value || null;
    chartConfig.general.showLegend = showLegendToggle?.checked ?? true;
    chartConfig.general.stacking = stackingSelect?.value || "disabled";
    chartConfig.general.normalizeToPercent = normalizeToggle?.checked ?? false;
    chartConfig.general.nullAsZero = nullAsZeroToggle?.checked ?? true;
    chartConfig.general.yColumns = yColumnsSelect
      ? Array.from(yColumnsSelect.selectedOptions).map((opt) => opt.value)
      : [];
    chartConfig.xAxis.scale = xAxisScale?.value || "auto";
    chartConfig.xAxis.name = xAxisName?.value || "";
    chartConfig.xAxis.sortValues = sortValuesToggle?.checked ?? true;
    chartConfig.xAxis.reverseOrder = reverseOrderToggle?.checked ?? false;
    chartConfig.xAxis.showLabels = showLabelsToggle?.checked ?? true;
    chartConfig.yAxis.left = chartConfig.yAxis.left || {};
    chartConfig.yAxis.left.scale = yAxisLeftScale?.value || "linear";
    chartConfig.yAxis.left.name = yAxisLeftName?.value || "";
    chartConfig.yAxis.left.min = parseNullableNumber(yAxisLeftMin?.value);
    chartConfig.yAxis.left.max = parseNullableNumber(yAxisLeftMax?.value);
    chartConfig.yAxis.left.reverse = yAxisLeftReverse?.checked ?? false;
    chartConfig.yAxis.right = chartConfig.yAxis.right || {};
    chartConfig.yAxis.right.scale = yAxisRightScale?.value || "linear";
    chartConfig.yAxis.right.name = yAxisRightName?.value || "";
    chartConfig.yAxis.right.min = parseNullableNumber(yAxisRightMin?.value);
    chartConfig.yAxis.right.max = parseNullableNumber(yAxisRightMax?.value);
    chartConfig.yAxis.right.reverse = yAxisRightReverse?.checked ?? false;
    chartConfig.dataLabels.enabled = dataLabelsEnabled?.checked ?? false;
    chartConfig.dataLabels.numberFormat = dataLabelsNumber?.value || "";
    chartConfig.dataLabels.percentFormat = dataLabelsPercent?.value || "";
    chartConfig.dataLabels.dateTimeFormat = dataLabelsDate?.value || "";
    chartConfig.dataLabels.labelTemplate = dataLabelsTemplate?.value || "auto";
  };

  const parseNullableNumber = (value) => {
    if (value === null || value === undefined || value === "") return null;
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  };

  const updateValidation = () => {
    const isPie = chartConfig.general.chartType === "pie";
    const hasX = !!chartConfig.general.xColumn || (!!chartConfig.general.groupBy && isPie);
    const hasY = chartConfig.general.yColumns && chartConfig.general.yColumns.length > 0;
    if (xColumnError) xColumnError.classList.toggle("d-none", hasX || isPie);
    if (yColumnsError) yColumnsError.classList.toggle("d-none", hasY);
  };

  const renderSeriesTable = (render) => {
    if (!seriesTableBody) return;
    seriesTableBody.innerHTML = "";
    const series = render?.series || [];
    if (!series.length) {
      if (seriesEmpty) seriesEmpty.classList.remove("d-none");
      return;
    }
    if (seriesEmpty) seriesEmpty.classList.add("d-none");

    const seriesByKey = new Map(chartConfig.series.map((s) => [s.key, s]));
    chartConfig.series = series.map((item) => {
      const existing = seriesByKey.get(item.key) || {};
      return {
        key: item.key,
        label: existing.label || item.label || item.key,
        axis: existing.axis || item.axis || "left",
        type: existing.type || item.type || chartConfig.general.chartType,
        zIndex: Number.isFinite(existing.zIndex) ? existing.zIndex : item.zIndex
      };
    });

    chartConfig.series.forEach((seriesItem) => {
      const row = document.createElement("tr");

      const zCell = document.createElement("td");
      const zInput = document.createElement("input");
      zInput.type = "number";
      zInput.className = "form-control form-control-sm";
      zInput.value = seriesItem.zIndex ?? "";
      zInput.addEventListener("input", () => {
        const parsed = Number(zInput.value);
        seriesItem.zIndex = Number.isFinite(parsed) ? parsed : null;
        setConfigValue();
        schedulePreview();
      });
      zCell.appendChild(zInput);
      row.appendChild(zCell);

      const leftCell = document.createElement("td");
      const leftRadio = document.createElement("input");
      leftRadio.type = "radio";
      leftRadio.name = `axis-${seriesItem.key}`;
      leftRadio.checked = seriesItem.axis !== "right";
      leftRadio.addEventListener("change", () => {
        if (leftRadio.checked) {
          seriesItem.axis = "left";
          setConfigValue();
          schedulePreview();
        }
      });
      leftCell.appendChild(leftRadio);
      row.appendChild(leftCell);

      const rightCell = document.createElement("td");
      const rightRadio = document.createElement("input");
      rightRadio.type = "radio";
      rightRadio.name = `axis-${seriesItem.key}`;
      rightRadio.checked = seriesItem.axis === "right";
      rightRadio.addEventListener("change", () => {
        if (rightRadio.checked) {
          seriesItem.axis = "right";
          setConfigValue();
          schedulePreview();
        }
      });
      rightCell.appendChild(rightRadio);
      row.appendChild(rightCell);

      const labelCell = document.createElement("td");
      const labelInput = document.createElement("input");
      labelInput.type = "text";
      labelInput.className = "form-control form-control-sm";
      labelInput.value = seriesItem.label || "";
      labelInput.addEventListener("input", () => {
        seriesItem.label = labelInput.value;
        setConfigValue();
        schedulePreview();
      });
      labelCell.appendChild(labelInput);
      row.appendChild(labelCell);

      const typeCell = document.createElement("td");
      const typeSelect = document.createElement("select");
      typeSelect.className = "form-select form-select-sm";
      ["line", "bar", "area", "pie", "scatter", "bubble", "heatmap", "box"].forEach((type) => {
        const option = document.createElement("option");
        option.value = type;
        option.textContent = type.charAt(0).toUpperCase() + type.slice(1);
        option.selected = (seriesItem.type || chartConfig.general.chartType) === type;
        typeSelect.appendChild(option);
      });
      typeSelect.addEventListener("change", () => {
        seriesItem.type = typeSelect.value;
        setConfigValue();
        schedulePreview();
      });
      typeCell.appendChild(typeSelect);
      row.appendChild(typeCell);

      seriesTableBody.appendChild(row);
    });

    setConfigValue();
  };

  const renderColors = (render) => {
    if (!colorsList) return;
    colorsList.innerHTML = "";
    const series = render?.series || [];
    if (!series.length) {
      if (colorsEmpty) colorsEmpty.classList.remove("d-none");
      return;
    }
    if (colorsEmpty) colorsEmpty.classList.add("d-none");

    const groupBy = chartConfig.general.groupBy;
    if (groupBy) {
      const groups = new Map();
      series.forEach((item) => {
        const groupValue = extractGroupValue(item.key, groupBy);
        if (!groupValue) return;
        if (!groups.has(groupValue)) {
          groups.set(groupValue, []);
        }
        groups.get(groupValue).push(item);
      });

      if (groups.size === 0 && Array.isArray(render?.labels) && render.labels.length > 0) {
        const baseKey = series[0]?.key || "";
        render.labels.forEach((label) => {
          const key = baseKey ? `${baseKey}|${groupBy}=${label}` : `${groupBy}=${label}`;
          groups.set(label, [{ key }]);
        });
      }

      groups.forEach((items, groupValue) => {
        const row = document.createElement("div");
        row.className = "d-flex align-items-center justify-content-between";

        const label = document.createElement("div");
        label.textContent = groupValue;

        const select = document.createElement("select");
        select.className = "form-select form-select-sm";
        colorOptions.forEach((opt) => {
          const option = document.createElement("option");
          option.value = opt.value;
          option.textContent = opt.label;
          const current = chartConfig.colors?.[items[0]?.key];
          option.selected = (current || "automatic") === opt.value;
          select.appendChild(option);
        });
        select.addEventListener("change", () => {
          chartConfig.colors = chartConfig.colors || {};
          items.forEach((seriesItem) => {
            if (select.value === "automatic") {
              delete chartConfig.colors[seriesItem.key];
            } else {
              chartConfig.colors[seriesItem.key] = select.value;
            }
          });
          setConfigValue();
          schedulePreview();
        });

        row.appendChild(label);
        row.appendChild(select);
        colorsList.appendChild(row);
      });
      return;
    }

    series.forEach((item) => {
      const row = document.createElement("div");
      row.className = "d-flex align-items-center justify-content-between";

      const label = document.createElement("div");
      label.textContent = item.label || item.key;

      const select = document.createElement("select");
      select.className = "form-select form-select-sm";
      colorOptions.forEach((opt) => {
        const option = document.createElement("option");
        option.value = opt.value;
        option.textContent = opt.label;
        const current = chartConfig.colors?.[item.key];
        option.selected = (current || "automatic") === opt.value;
        select.appendChild(option);
      });
      select.addEventListener("change", () => {
        chartConfig.colors = chartConfig.colors || {};
        if (select.value === "automatic") {
          delete chartConfig.colors[item.key];
        } else {
          chartConfig.colors[item.key] = select.value;
        }
        setConfigValue();
        schedulePreview();
      });

      row.appendChild(label);
      row.appendChild(select);
      colorsList.appendChild(row);
    });
  };

  const extractGroupValue = (key, groupBy) => {
    if (!key || !groupBy) return null;
    const parts = String(key).split("|");
    const groupPart = parts.find((part) => part.startsWith(`${groupBy}=`));
    if (!groupPart) return null;
    return groupPart.split("=").slice(1).join("=");
  };

  const schedulePreview = () => {
    if (typeSelect.value !== "Chart") return;
    if (previewTimer) {
      clearTimeout(previewTimer);
    }
    previewTimer = setTimeout(loadPreview, 200);
  };

  const loadPreview = async () => {
    readUiToConfig();
    setConfigValue();
    updateValidation();

    if (chartPreviewEmpty) chartPreviewEmpty.classList.add("d-none");
    if (chartPreviewErrors) chartPreviewErrors.classList.add("d-none");
    if (chartPreviewWarnings) chartPreviewWarnings.classList.add("d-none");
    if (chartPreviewLoading) chartPreviewLoading.classList.remove("d-none");

    const queryId = Number(queryIdInput?.value || 0);
    if (!queryId) {
      if (chartPreviewLoading) chartPreviewLoading.classList.add("d-none");
      if (chartPreviewEmpty) chartPreviewEmpty.classList.remove("d-none");
      return;
    }

    const executionId = Number(executionInput?.value || 0) || null;
    const token = document.querySelector('input[name="__RequestVerificationToken"]');
    try {
      const response = await fetch("/visualizations/chart-preview", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "RequestVerificationToken": token?.value || ""
        },
        body: JSON.stringify({
          queryId,
          executionId,
          config: chartConfig
        })
      });

      const payload = await response.json();
      if (!response.ok || payload.success !== true) {
        if (chartPreviewLoading) chartPreviewLoading.classList.add("d-none");
        if (Array.isArray(payload?.errors) && payload.errors.length > 0 && chartPreviewErrors) {
          chartPreviewErrors.textContent = payload.errors.join(" ");
          chartPreviewErrors.classList.remove("d-none");
        } else if (payload?.errorMessage && chartPreviewErrors) {
          chartPreviewErrors.textContent = payload.errorMessage;
          chartPreviewErrors.classList.remove("d-none");
        } else if (chartPreviewEmpty) {
          chartPreviewEmpty.classList.remove("d-none");
        }
        return;
      }

      latestRender = payload.render;
      renderSeriesTable(latestRender);
      renderColors(latestRender);
      if (chartPreviewLoading) chartPreviewLoading.classList.add("d-none");

      if (Array.isArray(payload.warnings) && payload.warnings.length > 0 && chartPreviewWarnings) {
        chartPreviewWarnings.textContent = payload.warnings.join(" ");
        chartPreviewWarnings.classList.remove("d-none");
      }

      if (window.queryBuilderViz?.renderChart) {
        window.queryBuilderViz.renderChart("vizPreview", {
          type: "Chart",
          config: chartConfig,
          render: latestRender
        });
      }
    } catch (err) {
      if (chartPreviewLoading) chartPreviewLoading.classList.add("d-none");
      if (chartPreviewEmpty) chartPreviewEmpty.classList.remove("d-none");
    }
  };

  const bindInputs = () => {
    [
      chartTypeSelect,
      xColumnSelect,
      yColumnsSelect,
      groupBySelect,
      errorsSelect,
      showLegendToggle,
      stackingSelect,
      normalizeToggle,
      nullAsZeroToggle,
      xAxisScale,
      xAxisName,
      sortValuesToggle,
      reverseOrderToggle,
      showLabelsToggle,
      yAxisLeftScale,
      yAxisLeftName,
      yAxisLeftMin,
      yAxisLeftMax,
      yAxisLeftReverse,
      yAxisRightScale,
      yAxisRightName,
      yAxisRightMin,
      yAxisRightMax,
      yAxisRightReverse,
      dataLabelsEnabled,
      dataLabelsNumber,
      dataLabelsPercent,
      dataLabelsDate,
      dataLabelsTemplate
    ].forEach((element) => {
      if (!element) return;
      const event = element.tagName === "SELECT" || element.type === "checkbox" ? "change" : "input";
      element.addEventListener(event, schedulePreview);
    });
  };

  function parseConfig(value) {
    if (!value) return {};
    try {
      return JSON.parse(value) || {};
    } catch (err) {
      return {};
    }
  }

  const updateColumnOptions = (select, columns, includeEmpty) => {
    if (!select) return;
    const current = select.multiple
      ? Array.from(select.selectedOptions).map((opt) => opt.value)
      : [select.value];
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
      option.selected = current.includes(column);
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
      updateColumnOptions(xColumnSelect, cols, true);
      updateColumnOptions(groupBySelect, cols, true);
      updateColumnOptions(errorsSelect, cols, true);
      updateColumnOptions(yColumnsSelect, cols, false);
      syncMultiSelect(yColumnsSelect, chartConfig.general.yColumns);
    } catch (err) {
      return;
    }
  };

  const init = () => {
    normalizeConfig();
    applyConfigToUi();
    setConfigValue();
    bindInputs();
    loadLatestColumns();
    if (typeSelect.value === "Chart") {
      loadPreview();
    }
  };

  typeSelect?.addEventListener("change", () => {
    if (typeSelect.value === "Chart") {
      loadPreview();
    }
  });

  init();
})();

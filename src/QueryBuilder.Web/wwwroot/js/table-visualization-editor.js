(() => {
  const configInput = document.getElementById("TableConfigJson");
  const executionInput = document.getElementById("LatestExecutionId");
  const typeSelect = document.getElementById("Type");
  const columnsList = document.getElementById("tableColumnsList");
  const searchInput = document.getElementById("tableColumnSearch");
  const emptyState = document.getElementById("tableColumnsEmptyState");
  const noMatchState = document.getElementById("tableColumnsNoMatch");
  const gridPageSize = document.getElementById("tableGridPageSize");
  const previewHost = document.getElementById("tableVizPreview");
  const previewTableHost = document.getElementById("tablePreviewTable");
  const previewStatus = document.getElementById("tablePreviewStatus");
  const previewInfo = document.getElementById("tablePreviewInfo");
  const previewLoading = document.getElementById("tablePreviewLoading");
  const previewPrev = document.getElementById("tablePreviewPrev");
  const previewNext = document.getElementById("tablePreviewNext");
  const previewPageSize = document.getElementById("tablePreviewPageSize");
  const previewSearch = document.getElementById("tablePreviewSearch");
  const previewSearchWrap = document.getElementById("tablePreviewSearchWrap");

  if (!configInput || !columnsList || !previewHost || !typeSelect) {
    console.debug("Table editor: missing required elements", {
      hasConfigInput: !!configInput,
      hasColumnsList: !!columnsList,
      hasPreviewHost: !!previewHost,
      hasTypeSelect: !!typeSelect
    });
    return;
  }

  console.debug("Table editor: init");

  const allowHtmlPreview = previewHost.dataset.allowHtmlPreview === "true";
  const displayTypes = [
    { value: "text", label: "Text" },
    { value: "number", label: "Number" },
    { value: "datetime", label: "Date/Time" },
    { value: "boolean", label: "Boolean" },
    { value: "link", label: "Link" },
    { value: "image", label: "Image" },
    { value: "json", label: "JSON" }
  ];

  let columns = [];
  let rows = [];
  let tableConfig = parseConfig(configInput.value);
  let lastLoadKey = "";
  let page = 1;
  let pageSize = 25;
  let hasNext = false;
  let previewSearchTerm = "";
  let previewSearchTimer = null;

  const setConfigValue = () => {
    configInput.value = JSON.stringify(tableConfig);
  };

  const ensureGridDefaults = () => {
    tableConfig.grid = tableConfig.grid || {};
    if (!Number.isFinite(tableConfig.grid.pageSize)) {
      tableConfig.grid.pageSize = 25;
    }
  };

  const ensureColumnsDefaults = () => {
    tableConfig.columns = tableConfig.columns || {};
    const normalized = {};
    columns.forEach((name, index) => {
      const existing = tableConfig.columns[name];
      normalized[name] = existing || buildDefaultColumn(inferDisplayAs(rows, index));
    });
    tableConfig.columns = normalized;
  };

  const syncColumn = (columnName, updater) => {
    const column = tableConfig.columns[columnName] || buildDefaultColumn("text");
    updater(column);
    tableConfig.columns[columnName] = column;
    setConfigValue();
    renderPreview();
  };

  // Root cause: table editor depended on a single executionId-only endpoint that often returned
  // empty results for older executions without stored ResultJson, and the UI rendered before
  // data arrived (no loading state), leaving Columns/Preview blank.
  const renderColumns = () => {
    columnsList.innerHTML = "";
    if (!columns.length) {
      return;
    }

    columns.forEach((columnName) => {
      const columnConfig = tableConfig.columns[columnName] || buildDefaultColumn("text");
      const card = document.createElement("div");
      card.className = "viz-col-card";
      card.dataset.columnName = columnName.toLowerCase();

      const header = document.createElement("div");
      header.className = "viz-col-card__header";

      header.appendChild(renderVisibilityToggle(columnName, columnConfig));
      header.appendChild(renderAlignmentControls(columnName, columnConfig));
      header.appendChild(renderToggle("Use for search", columnConfig.useForSearch, (checked) => {
        syncColumn(columnName, (column) => { column.useForSearch = checked; });
      }));
      card.appendChild(header);

      const body = document.createElement("div");
      body.className = "viz-col-card__body";
      body.appendChild(renderDisplayAs(columnName, columnConfig));
      body.appendChild(renderTypeSpecific(columnName, columnConfig));

      card.appendChild(body);
      columnsList.appendChild(card);
    });
  };

  const applySearchFilter = () => {
    if (!searchInput) return;
    const term = searchInput.value.trim().toLowerCase();
    let visibleCount = 0;
    columnsList.querySelectorAll(".viz-col-card").forEach((item) => {
      const name = item.dataset.columnName || "";
      const show = !term || name.includes(term);
      item.classList.toggle("d-none", !show);
      if (show) visibleCount += 1;
    });
    if (noMatchState) {
      noMatchState.classList.toggle("d-none", visibleCount > 0 || columns.length === 0);
    }
  };

  const renderAlignmentControls = (columnName, columnConfig) => {
    const wrapper = document.createElement("div");
    wrapper.className = "mb-2";

    const label = document.createElement("div");
    label.className = "form-label mb-1";
    label.textContent = "Alignment";
    wrapper.appendChild(label);

    const group = document.createElement("div");
    group.className = "btn-group btn-group-sm";
    group.role = "group";

    ["left", "center", "right"].forEach((alignment) => {
      const id = `align-${columnName}-${alignment}`;
      const input = document.createElement("input");
      input.type = "radio";
      input.className = "btn-check";
      input.name = `align-${columnName}`;
      input.id = id;
      input.checked = (columnConfig.alignment || "left") === alignment;
      input.addEventListener("change", () => {
        if (input.checked) {
          syncColumn(columnName, (column) => { column.alignment = alignment; });
        }
      });

      const labelBtn = document.createElement("label");
      labelBtn.className = "btn btn-outline-secondary";
      labelBtn.setAttribute("for", id);
      labelBtn.textContent = alignment.charAt(0).toUpperCase() + alignment.slice(1);

      group.appendChild(input);
      group.appendChild(labelBtn);
    });

    wrapper.appendChild(group);
    return wrapper;
  };

  const renderVisibilityToggle = (columnName, columnConfig) => {
    const wrapper = document.createElement("div");
    wrapper.className = "d-flex align-items-center gap-2 mb-2";
    const id = `visible-${columnName}`;

    const input = document.createElement("input");
    input.type = "checkbox";
    input.className = "form-check-input m-0";
    input.id = id;
    input.checked = columnConfig.isVisible !== false;
    input.setAttribute("aria-label", "Show column");
    input.addEventListener("change", () => {
      syncColumn(columnName, (column) => { column.isVisible = input.checked; });
    });

    const label = document.createElement("label");
    label.className = "viz-col-card__title mb-0";
    label.setAttribute("for", id);
    label.textContent = columnName;

    wrapper.appendChild(input);
    wrapper.appendChild(label);
    return wrapper;
  };

  const renderDisplayAs = (columnName, columnConfig) => {
    const wrapper = document.createElement("div");
    wrapper.className = "mb-2";

    const label = document.createElement("label");
    label.className = "form-label";
    label.textContent = "Display as";
    wrapper.appendChild(label);

    const select = document.createElement("select");
    select.className = "form-select";
    displayTypes.forEach((opt) => {
      const option = document.createElement("option");
      option.value = opt.value;
      option.textContent = opt.label;
      option.selected = (columnConfig.displayAs || "text") === opt.value;
      select.appendChild(option);
    });
    select.addEventListener("change", () => {
      const value = select.value;
      syncColumn(columnName, (column) => {
        column.displayAs = value;
        if (value === "number" && !column.numberFormat) {
          column.numberFormat = "0,0";
        }
        if (value === "boolean") {
          column.falseText = column.falseText || "False";
          column.trueText = column.trueText || "True";
        }
        column.alignment = value === "number" ? "right" : value === "boolean" ? "center" : "left";
      });
      renderColumns();
      applySearchFilter();
    });
    wrapper.appendChild(select);
    return wrapper;
  };

  const renderTypeSpecific = (columnName, columnConfig) => {
    const wrapper = document.createElement("div");
    wrapper.className = "pt-2";
    const displayAs = columnConfig.displayAs || "text";

    if (displayAs === "number") {
      wrapper.appendChild(renderTextInput("Number format", columnConfig.numberFormat || "", (value) => {
        syncColumn(columnName, (column) => { column.numberFormat = value; });
      }));
      return wrapper;
    }

    if (displayAs === "text") {
      wrapper.appendChild(renderToggle("Allow HTML content", columnConfig.allowHtml === true, (checked) => {
        syncColumn(columnName, (column) => { column.allowHtml = checked; });
      }));
      wrapper.appendChild(renderToggle("Highlight links", columnConfig.highlightLinks === true, (checked) => {
        syncColumn(columnName, (column) => { column.highlightLinks = checked; });
      }));
      return wrapper;
    }

    if (displayAs === "datetime") {
      wrapper.appendChild(renderTextInput("Date/Time format", columnConfig.dateTimeFormat || "", (value) => {
        syncColumn(columnName, (column) => { column.dateTimeFormat = value; });
      }));
      return wrapper;
    }

    if (displayAs === "boolean") {
      wrapper.appendChild(renderTextInput("Value for false", columnConfig.falseText || "", (value) => {
        syncColumn(columnName, (column) => { column.falseText = value; });
      }));
      wrapper.appendChild(renderTextInput("Value for true", columnConfig.trueText || "", (value) => {
        syncColumn(columnName, (column) => { column.trueText = value; });
      }));
      return wrapper;
    }

    if (displayAs === "link") {
      wrapper.appendChild(renderTextInput("URL template", columnConfig.urlTemplate || "", (value) => {
        syncColumn(columnName, (column) => { column.urlTemplate = value; });
      }));
      wrapper.appendChild(renderTextInput("Text template", columnConfig.textTemplate || "", (value) => {
        syncColumn(columnName, (column) => { column.textTemplate = value; });
      }));
      wrapper.appendChild(renderTextInput("Title template", columnConfig.titleTemplate || "", (value) => {
        syncColumn(columnName, (column) => { column.titleTemplate = value; });
      }));
      wrapper.appendChild(renderToggle("Open in new tab", columnConfig.openInNewTab === true, (checked) => {
        syncColumn(columnName, (column) => { column.openInNewTab = checked; });
      }));
      return wrapper;
    }

    if (displayAs === "image") {
      wrapper.appendChild(renderTextInput("URL template", columnConfig.urlTemplate || "", (value) => {
        syncColumn(columnName, (column) => { column.urlTemplate = value; });
      }));
      wrapper.appendChild(renderNumberInput("Width", columnConfig.imageWidth, (value) => {
        syncColumn(columnName, (column) => { column.imageWidth = value; });
      }));
      wrapper.appendChild(renderNumberInput("Height", columnConfig.imageHeight, (value) => {
        syncColumn(columnName, (column) => { column.imageHeight = value; });
      }));
      wrapper.appendChild(renderTextInput("Title template", columnConfig.titleTemplate || "", (value) => {
        syncColumn(columnName, (column) => { column.titleTemplate = value; });
      }));
      return wrapper;
    }

    return wrapper;
  };

  const renderToggle = (labelText, value, onChange) => {
    const wrapper = document.createElement("div");
    wrapper.className = "form-check mb-2";
    const input = document.createElement("input");
    input.type = "checkbox";
    input.className = "form-check-input";
    input.checked = value;
    input.addEventListener("change", () => onChange(input.checked));
    const label = document.createElement("label");
    label.className = "form-check-label";
    label.textContent = labelText;
    wrapper.appendChild(input);
    wrapper.appendChild(label);
    return wrapper;
  };

  const renderTextInput = (labelText, value, onChange) => {
    const wrapper = document.createElement("div");
    wrapper.className = "mb-2";
    const label = document.createElement("label");
    label.className = "form-label";
    label.textContent = labelText;
    const input = document.createElement("input");
    input.type = "text";
    input.className = "form-control";
    input.value = value;
    input.addEventListener("input", () => onChange(input.value));
    wrapper.appendChild(label);
    wrapper.appendChild(input);
    return wrapper;
  };

  const renderNumberInput = (labelText, value, onChange) => {
    const wrapper = document.createElement("div");
    wrapper.className = "mb-2";
    const label = document.createElement("label");
    label.className = "form-label";
    label.textContent = labelText;
    const input = document.createElement("input");
    input.type = "number";
    input.className = "form-control";
    input.value = value ?? "";
    input.addEventListener("input", () => {
      const parsed = input.value === "" ? null : Number(input.value);
      onChange(Number.isFinite(parsed) ? parsed : null);
    });
    wrapper.appendChild(label);
    wrapper.appendChild(input);
    return wrapper;
  };

  const renderPreview = () => {
    if (typeSelect.value !== "Table") return;
    if (!previewTableHost) return;
    previewTableHost.innerHTML = "";
    if (!columns.length) {
      if (previewSearchWrap) {
        previewSearchWrap.classList.add("d-none");
      }
      if (previewSearch) {
        previewSearch.value = "";
      }
      previewSearchTerm = "";
      previewTableHost.innerHTML = "<div class='text-muted'>Run the query to preview table results.</div>";
      return;
    }

    const visibleColumns = [];
    const visibleIndexes = [];
    const searchableIndexes = [];
    columns.forEach((name, index) => {
      const config = tableConfig.columns[name] || buildDefaultColumn("text");
      if (config.isVisible === false) {
        return;
      }
      visibleColumns.push(name);
      visibleIndexes.push(index);
      if (config.useForSearch) {
        searchableIndexes.push(index);
      }
    });

    if (!visibleColumns.length) {
      if (previewSearchWrap) {
        previewSearchWrap.classList.add("d-none");
      }
      if (previewSearch) {
        previewSearch.value = "";
      }
      previewSearchTerm = "";
      previewTableHost.innerHTML = "<div class='text-muted'>No columns selected.</div>";
      return;
    }

    const searchEnabled = searchableIndexes.length > 0;
    if (previewSearchWrap) {
      previewSearchWrap.classList.toggle("d-none", !searchEnabled);
    }
    if (previewSearch && !searchEnabled && previewSearchTerm) {
      previewSearchTerm = "";
      previewSearch.value = "";
    }

    const table = document.createElement("table");
    table.className = "table table-sm table-hover mb-0";
    const thead = document.createElement("thead");
    const headRow = document.createElement("tr");
    visibleColumns.forEach((name) => {
      const th = document.createElement("th");
      th.textContent = name;
      headRow.appendChild(th);
    });
    thead.appendChild(headRow);
    table.appendChild(thead);

    const tbody = document.createElement("tbody");
    const pageSize = Math.max(1, Number(tableConfig.grid?.pageSize || 25));
    const filteredRows = applyPreviewSearch(rows, searchableIndexes, previewSearchTerm);
    const slice = filteredRows.slice(0, pageSize);
    if (slice.length === 0) {
      const tr = document.createElement("tr");
      const td = document.createElement("td");
      td.colSpan = visibleColumns.length || 1;
      td.className = "text-muted";
      td.textContent = filteredRows.length ? "No rows returned." : "No matching rows.";
      tr.appendChild(td);
      tbody.appendChild(tr);
    } else {
      slice.forEach((row) => {
        const tr = document.createElement("tr");
        visibleColumns.forEach((name, position) => {
          const index = visibleIndexes[position];
          const config = tableConfig.columns[name] || buildDefaultColumn("text");
          const td = document.createElement("td");
          td.classList.add(resolveAlignmentClass(config.alignment));
          renderCell(td, config, row, index);
          tr.appendChild(td);
        });
        tbody.appendChild(tr);
      });
    }
    table.appendChild(tbody);

    const wrapper = document.createElement("div");
    wrapper.className = "table-responsive";
    wrapper.appendChild(table);
    previewTableHost.appendChild(wrapper);
  };

  const applyPreviewSearch = (dataRows, searchableIndexes, term) => {
    const normalized = String(term || "").trim().toLowerCase();
    if (!normalized || !searchableIndexes.length) {
      return dataRows;
    }
    return dataRows.filter((row) => searchableIndexes.some((index) => {
      const value = index < row.length ? row[index] : null;
      return String(value ?? "").toLowerCase().includes(normalized);
    }));
  };

  const renderCell = (cell, columnConfig, row, index) => {
    const raw = index < row.length ? row[index] : null;
    const displayAs = columnConfig.displayAs || "text";
    if (raw === null || raw === undefined) {
      cell.textContent = "";
      return;
    }
    if (displayAs === "number") {
      const num = Number(raw);
      cell.textContent = Number.isFinite(num)
        ? formatNumber(num, columnConfig.numberFormat)
        : String(raw);
      return;
    }
    if (displayAs === "datetime") {
      cell.textContent = formatDateTime(raw, columnConfig.dateTimeFormat);
      return;
    }
    if (displayAs === "boolean") {
      cell.textContent = resolveBoolean(raw, columnConfig.trueText, columnConfig.falseText);
      return;
    }
    if (displayAs === "link") {
      const href = applyTemplate(columnConfig.urlTemplate || String(raw), row, raw);
      const text = applyTemplate(columnConfig.textTemplate || String(raw), row, raw);
      const title = applyTemplate(columnConfig.titleTemplate || "", row, raw);
      const link = document.createElement("a");
      link.href = href;
      link.textContent = text || href;
      if (title) link.title = title;
      if (columnConfig.openInNewTab) {
        link.target = "_blank";
        link.rel = "noopener noreferrer";
      }
      cell.appendChild(link);
      return;
    }
    if (displayAs === "image") {
      const src = applyTemplate(columnConfig.urlTemplate || String(raw), row, raw);
      const img = document.createElement("img");
      img.src = src;
      if (columnConfig.imageWidth) img.width = columnConfig.imageWidth;
      if (columnConfig.imageHeight) img.height = columnConfig.imageHeight;
      const title = applyTemplate(columnConfig.titleTemplate || "", row, raw);
      if (title) img.title = title;
      img.className = "img-fluid";
      cell.appendChild(img);
      return;
    }
    if (displayAs === "json") {
      cell.textContent = formatJson(raw);
      return;
    }

    if (columnConfig.highlightLinks === true) {
      const link = detectLink(raw);
      if (link) {
        const a = document.createElement("a");
        a.href = link;
        a.textContent = raw;
        a.target = "_blank";
        a.rel = "noopener noreferrer";
        cell.appendChild(a);
        return;
      }
    }

    if (columnConfig.allowHtml === true && allowHtmlPreview) {
      cell.innerHTML = String(raw);
      return;
    }

    cell.textContent = String(raw);
  };

  const resolveAlignmentClass = (alignment) => {
    if (alignment === "center") return "text-center";
    if (alignment === "right") return "text-end";
    return "text-start";
  };

  const formatNumber = (value, format) => {
    const formatString = format || "0,0";
    const decimals = formatString.includes(".")
      ? formatString.split(".")[1].length
      : 0;
    const formatter = new Intl.NumberFormat(undefined, {
      minimumFractionDigits: decimals,
      maximumFractionDigits: decimals
    });
    return formatter.format(value);
  };

  const formatDateTime = (value, format) => {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return String(value);
    if (!format) return date.toLocaleString();
    const pad = (num) => String(num).padStart(2, "0");
    return format
      .replace(/yyyy/g, date.getFullYear())
      .replace(/MM/g, pad(date.getMonth() + 1))
      .replace(/dd/g, pad(date.getDate()))
      .replace(/HH/g, pad(date.getHours()))
      .replace(/mm/g, pad(date.getMinutes()))
      .replace(/ss/g, pad(date.getSeconds()));
  };

  const resolveBoolean = (value, trueText, falseText) => {
    const normalized = String(value).toLowerCase();
    const isTrue = normalized === "true" || normalized === "1" || normalized === "yes";
    return isTrue ? (trueText || "True") : (falseText || "False");
  };

  const formatJson = (value) => {
    if (typeof value === "object") {
      return JSON.stringify(value, null, 2);
    }
    try {
      const parsed = JSON.parse(value);
      return JSON.stringify(parsed, null, 2);
    } catch (err) {
      return String(value);
    }
  };

  const detectLink = (value) => {
    if (!value) return null;
    const match = String(value).match(/https?:\/\/[^\s]+/i);
    return match ? match[0] : null;
  };

  const applyTemplate = (template, row, value) => {
    return String(template).replace(/{{\s*([^}]+)\s*}}/g, (match, token) => {
      const key = token.trim();
      if (key.toLowerCase() === "value") return String(value ?? "");
      const index = columns.findIndex((col) => col.toLowerCase() === key.toLowerCase());
      if (index >= 0) {
        return row[index] ?? "";
      }
      return "";
    });
  };

  const buildDefaultColumn = (displayAs) => ({
    isVisible: true,
    alignment: displayAs === "number" ? "right" : displayAs === "boolean" ? "center" : "left",
    useForSearch: displayAs === "text" || displayAs === "json",
    displayAs,
    numberFormat: displayAs === "number" ? "0,0" : undefined,
    allowHtml: false,
    highlightLinks: false,
    dateTimeFormat: "",
    falseText: "False",
    trueText: "True",
    urlTemplate: "",
    textTemplate: "",
    titleTemplate: "",
    openInNewTab: false,
    imageWidth: null,
    imageHeight: null
  });

  const inferDisplayAs = (rowsData, columnIndex) => {
    const values = rowsData
      .map((row) => (columnIndex < row.length ? row[columnIndex] : null))
      .filter((value) => value !== null && value !== undefined && value !== "");
    if (!values.length) return "text";
    if (values.every(isBoolean)) return "boolean";
    if (values.every(isNumber)) return "number";
    if (values.every(isDateTime)) return "datetime";
    return "text";
  };

  const isBoolean = (value) => {
    const normalized = String(value).toLowerCase();
    return normalized === "true" || normalized === "false" || normalized === "0" || normalized === "1";
  };

  const isNumber = (value) => {
    return Number.isFinite(Number(value));
  };

  const isDateTime = (value) => {
    return !Number.isNaN(Date.parse(value));
  };

  function parseConfig(value) {
    if (!value) return { type: "table", columns: {}, grid: { pageSize: 25 } };
    try {
      const parsed = JSON.parse(value);
      return parsed || { type: "table", columns: {}, grid: { pageSize: 25 } };
    } catch (err) {
      return { type: "table", columns: {}, grid: { pageSize: 25 } };
    }
  }

  const updateGridInput = () => {
    if (!gridPageSize) return;
    gridPageSize.value = tableConfig.grid?.pageSize ?? 25;
  };

  const bindGridInput = () => {
    if (!gridPageSize) return;
    gridPageSize.addEventListener("input", () => {
      const value = Number(gridPageSize.value);
      tableConfig.grid.pageSize = Number.isFinite(value) && value > 0 ? value : 25;
      setConfigValue();
      renderPreview();
    });
  };

  const updateUiState = (hasData, message) => {
    if (!emptyState) return;
    if (hasData) {
      emptyState.classList.add("d-none");
      emptyState.textContent = "Run the query to configure columns.";
      return;
    }
    emptyState.classList.remove("d-none");
    emptyState.textContent = message || "Run the query to configure columns.";
  };

  const loadExecutionPreview = async () => {
    const queryId = Number(document.getElementById("QueryId")?.value || 0);
    const executionId = Number(executionInput?.value || 0);
    console.debug("Table editor: load preview", { queryId, executionId });
    if (!queryId) {
      updateUiState(false, "Run the query to configure columns.");
      showPreviewError("Query is missing. Save and run the query first.");
      return;
    }

    const loadKey = `${queryId}:${executionId || "latest"}:${page}:${pageSize}`;
    if (lastLoadKey === loadKey && columns.length > 0) {
      return;
    }
    lastLoadKey = loadKey;
    if (previewLoading) {
      previewLoading.textContent = "Loading preview...";
    }
    if (columnsList) {
      columnsList.innerHTML = "<div class='text-muted'>Loading columns...</div>";
    }

    try {
      const url = `/queries/${encodeURIComponent(queryId)}/executions/latest/results?page=${page}&pageSize=${pageSize}${executionId ? `&executionId=${encodeURIComponent(executionId)}` : ""}`;
      const response = await fetch(url);
      const payload = await response.json();
      console.debug("Table editor: preview response", { status: response.status, payload });
      if (!response.ok || payload.success !== true) {
        updateUiState(false, payload?.errorMessage || "Run the query to configure columns.");
        showPreviewError(payload?.errorMessage || "No execution results available.");
        return;
      }

      const normalized = normalizePayload(payload);
      columns = normalized.columns;
      rows = normalized.rows;
      hasNext = payload.hasNext === true || normalized.rows.length === pageSize;
      ensureGridDefaults();
      ensureColumnsDefaults();
      setConfigValue();
      renderColumns();
      applySearchFilter();
      updateGridInput();
      updateUiState(columns.length > 0);
      updatePreviewControls(payload);
      renderPreview();
    } catch (err) {
      updateUiState(false, "Run the query to configure columns.");
      showPreviewError("Unable to load preview data.");
    }
  };

  const init = () => {
    const storedSize = Number(localStorage.getItem("vizEditor.pageSize"));
    pageSize = allowedPageSize(storedSize) ? storedSize : 25;
    if (previewPageSize) {
      previewPageSize.value = String(pageSize);
    }
    ensureGridDefaults();
    setConfigValue();
    renderColumns();
    updateGridInput();
    bindGridInput();
    searchInput?.addEventListener("input", applySearchFilter);
    previewSearch?.addEventListener("input", () => {
      if (previewSearchTimer) {
        clearTimeout(previewSearchTimer);
      }
      previewSearchTimer = setTimeout(() => {
        previewSearchTerm = previewSearch.value.trim().toLowerCase();
        renderPreview();
      }, 200);
    });
    typeSelect?.addEventListener("change", () => {
      if (typeSelect.value === "Table") {
        loadExecutionPreview();
      } else {
        renderPreview();
      }
    });
    if (typeSelect.value === "Table") {
      updateUiState(false);
      loadExecutionPreview();
    }

    previewPageSize?.addEventListener("change", () => {
      const value = Number(previewPageSize.value);
      pageSize = allowedPageSize(value) ? value : 25;
      localStorage.setItem("vizEditor.pageSize", String(pageSize));
      page = 1;
      loadExecutionPreview();
    });

    previewPrev?.addEventListener("click", () => {
      if (page <= 1) return;
      page -= 1;
      loadExecutionPreview();
    });

    previewNext?.addEventListener("click", () => {
      if (!hasNext) return;
      page += 1;
      loadExecutionPreview();
    });
  };

  const showPreviewError = (message) => {
    if (previewTableHost) {
      previewTableHost.innerHTML = `<div class="text-muted">${message}</div>`;
    }
  };

  const normalizePayload = (payload) => {
    const rawColumns = Array.isArray(payload.columns) ? payload.columns : [];
    const normalizedColumns = rawColumns.map((col) =>
      typeof col === "string" ? { name: col } : col
    );
    const columnNames = normalizedColumns.map((col) => col.name);
    const rawRows = Array.isArray(payload.rows) ? payload.rows : [];
    const normalizedRows = rawRows.map((row) => {
      if (Array.isArray(row)) {
        return row;
      }
      if (row && typeof row === "object") {
        return columnNames.map((name) => row[name] ?? null);
      }
      return [];
    });
    return { columns: columnNames, rows: normalizedRows };
  };

  const updatePreviewControls = (payload) => {
    if (previewLoading) previewLoading.textContent = "";
    if (previewStatus) previewStatus.textContent = `Page ${page}`;
    if (previewPrev) previewPrev.disabled = page <= 1;
    if (previewNext) previewNext.disabled = !hasNext;
    if (previewInfo) {
      if (payload.rowCount) {
        previewInfo.textContent = `${payload.rowCount} rows`;
      } else {
        previewInfo.textContent = hasNext ? "More rows available" : "";
      }
    }
  };

  const allowedPageSize = (value) => {
    return [25, 50, 100, 250, 500].includes(value);
  };

  init();
})();

(() => {
  const renderTable = (container, options) => {
    if (!container || !options) return;
    const columns = Array.isArray(options.columns) ? options.columns : [];
    const rows = Array.isArray(options.rows) ? options.rows : [];
    const config = options.config || {};
    const allowUnsafeHtml = options.allowUnsafeHtml === true;
    const pageSize = Math.max(1, Number(config.grid?.pageSize || 25));

    let page = 1;
    let totalPages = Math.max(1, Math.ceil(rows.length / pageSize));
    let searchTimer = null;

    const getColumnConfig = (name) => config.columns?.[name] || buildDefaultColumn("text");
    const getSearchTerm = () => String(container.dataset.searchTerm || "").trim().toLowerCase();
    const setSearchTerm = (term) => {
      if (term) {
        container.dataset.searchTerm = term;
      } else {
        delete container.dataset.searchTerm;
      }
    };

    const ensureSearchBar = (searchableIndexes) => {
      const existing = container.querySelector(".qb-table-search");
      if (!searchableIndexes.length) {
        if (existing) {
          existing.remove();
        }
        setSearchTerm("");
        return null;
      }

      const searchWrap = existing || document.createElement("div");
      if (!existing) {
        searchWrap.className = "qb-table-search mb-2";
        const input = document.createElement("input");
        input.type = "search";
        input.className = "form-control form-control-sm";
        input.placeholder = "Search...";
        input.setAttribute("aria-label", "Search table");
        searchWrap.appendChild(input);
        container.prepend(searchWrap);
      }

      const input = searchWrap.querySelector("input");
      if (input) {
        input.value = getSearchTerm();
        if (!input.dataset.bound) {
          input.dataset.bound = "true";
          input.addEventListener("input", () => {
            const term = input.value.trim().toLowerCase();
            setSearchTerm(term);
            if (searchTimer) {
              clearTimeout(searchTimer);
            }
            searchTimer = setTimeout(renderTableSection, 200);
          });
        }
      }

      return searchWrap;
    };

    const ensureTableWrap = () => {
      let tableWrap = container.querySelector(".qb-table-wrap");
      if (!tableWrap) {
        tableWrap = document.createElement("div");
        tableWrap.className = "qb-table-wrap";
        container.appendChild(tableWrap);
      }
      return tableWrap;
    };

    const filterRows = (dataRows, searchableIndexes) => {
      const term = getSearchTerm();
      if (!term || !searchableIndexes.length) {
        return dataRows;
      }
      return dataRows.filter((row) => searchableIndexes.some((index) => {
        const value = index < row.length ? row[index] : null;
        return String(value ?? "").toLowerCase().includes(term);
      }));
    };

    const renderTableSection = () => {
      const columnMeta = columns.map((name, index) => ({
        name,
        index,
        config: getColumnConfig(name)
      }));
      const visibleColumns = columnMeta.filter((col) => col.config.isVisible !== false);
      const searchableIndexes = visibleColumns
        .filter((col) => col.config.useForSearch)
        .map((col) => col.index);

      if (columns.length === 0) {
        setSearchTerm("");
        container.innerHTML = "<div class='text-muted'>No columns to display.</div>";
        return;
      }

      if (!visibleColumns.length) {
        if (container.querySelector(".qb-table-search")) {
          container.querySelector(".qb-table-search").remove();
        }
        setSearchTerm("");
        const tableWrap = ensureTableWrap();
        tableWrap.innerHTML = "<div class='text-muted'>No columns selected.</div>";
        return;
      }

      ensureSearchBar(searchableIndexes);
      const tableWrap = ensureTableWrap();
      tableWrap.innerHTML = "";

      const table = document.createElement("table");
      table.className = "table table-sm table-hover table-compact mb-0";
      const thead = document.createElement("thead");
      const headRow = document.createElement("tr");
      visibleColumns.forEach((column) => {
        const th = document.createElement("th");
        th.textContent = column.name;
        headRow.appendChild(th);
      });
      thead.appendChild(headRow);
      table.appendChild(thead);

      const tbody = document.createElement("tbody");
      const filteredRows = filterRows(rows, searchableIndexes);
      totalPages = Math.max(1, Math.ceil(filteredRows.length / pageSize));
      page = Math.min(page, totalPages);
      const start = (page - 1) * pageSize;
      const slice = filteredRows.slice(start, start + pageSize);

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
          visibleColumns.forEach((column) => {
            const columnConfig = column.config;
            const td = document.createElement("td");
            td.classList.add(resolveAlignmentClass(columnConfig.alignment));
            renderCell(td, columnConfig, row, column.index, columns, allowUnsafeHtml);
            tr.appendChild(td);
          });
          tbody.appendChild(tr);
        });
      }
      table.appendChild(tbody);

      const wrapper = document.createElement("div");
      wrapper.className = "table-responsive";
      wrapper.appendChild(table);
      tableWrap.appendChild(wrapper);

      if (filteredRows.length > pageSize) {
        tableWrap.appendChild(renderPagination(totalPages));
      }
    };

    const renderPagination = (pages) => {
      const wrapper = document.createElement("div");
      wrapper.className = "d-flex justify-content-between align-items-center mt-2";
      const info = document.createElement("div");
      info.className = "text-muted small";
      info.textContent = `Page ${page} of ${pages}`;

      const controls = document.createElement("div");
      controls.className = "btn-group btn-group-sm";
      const prev = document.createElement("button");
      prev.type = "button";
      prev.className = "btn btn-outline-secondary";
      prev.textContent = "Prev";
      prev.disabled = page <= 1;
      prev.addEventListener("click", () => {
        page = Math.max(1, page - 1);
        renderTableSection();
      });
      const next = document.createElement("button");
      next.type = "button";
      next.className = "btn btn-outline-secondary";
      next.textContent = "Next";
      next.disabled = page >= pages;
      next.addEventListener("click", () => {
        page = Math.min(pages, page + 1);
        renderTableSection();
      });

      controls.appendChild(prev);
      controls.appendChild(next);
      wrapper.appendChild(info);
      wrapper.appendChild(controls);
      return wrapper;
    };

    renderTableSection();
  };

  const renderCell = (cell, columnConfig, row, index, columns, allowUnsafeHtml) => {
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
      const href = applyTemplate(columnConfig.urlTemplate || String(raw), row, columns, raw);
      const text = applyTemplate(columnConfig.textTemplate || String(raw), row, columns, raw);
      const title = applyTemplate(columnConfig.titleTemplate || "", row, columns, raw);
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
      const src = applyTemplate(columnConfig.urlTemplate || String(raw), row, columns, raw);
      const img = document.createElement("img");
      img.src = src;
      if (columnConfig.imageWidth) img.width = columnConfig.imageWidth;
      if (columnConfig.imageHeight) img.height = columnConfig.imageHeight;
      const title = applyTemplate(columnConfig.titleTemplate || "", row, columns, raw);
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

    if (columnConfig.allowHtml === true && allowUnsafeHtml) {
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

  const applyTemplate = (template, row, columns, value) => {
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

  window.queryBuilderTable = window.queryBuilderTable || {};
  window.queryBuilderTable.render = renderTable;
})();

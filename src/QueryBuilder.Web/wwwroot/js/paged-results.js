(() => {
  const allowedSizes = [25, 50, 100, 250, 500];

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
    return { columnNames, rows: normalizedRows };
  };

  const renderSimpleTable = (host, columns, rows) => {
    host.innerHTML = "";
    if (!columns.length) {
      host.innerHTML = "<div class='text-muted'>No columns to display.</div>";
      return;
    }
    const table = document.createElement("table");
    table.className = "table table-sm table-striped table-compact mb-0";
    const thead = document.createElement("thead");
    const headRow = document.createElement("tr");
    columns.forEach((name) => {
      const th = document.createElement("th");
      th.textContent = name;
      headRow.appendChild(th);
    });
    thead.appendChild(headRow);
    table.appendChild(thead);

    const tbody = document.createElement("tbody");
    if (!rows.length) {
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
        columns.forEach((_, index) => {
          const td = document.createElement("td");
          td.textContent = row[index] ?? "";
          tr.appendChild(td);
        });
        tbody.appendChild(tr);
      });
    }
    table.appendChild(tbody);
    const wrapper = document.createElement("div");
    wrapper.className = "table-responsive";
    wrapper.appendChild(table);
    host.appendChild(wrapper);
  };

  const resolveEndpoint = (container) => {
    const template = container.dataset.endpointTemplate;
    if (!template) {
      return container.dataset.endpoint || "";
    }
    const queryId = container.dataset.queryId;
    const executionId = container.dataset.executionId;
    if (!queryId || !executionId) {
      return "";
    }
    return template
      .replace("{queryId}", encodeURIComponent(queryId))
      .replace("{executionId}", encodeURIComponent(executionId));
  };

  const initContainer = (container) => {
    const storageKey = container.dataset.storageKey || "pageSize";
    const pageSizeSelect = container.querySelector("[data-page-size]");
    const prevBtn = container.querySelector("[data-page-prev]");
    const nextBtn = container.querySelector("[data-page-next]");
    const statusEl = container.querySelector("[data-page-status]");
    const infoEl = container.querySelector("[data-page-info]");
    const loadingEl = container.querySelector("[data-page-loading]");
    const tableHost = container.querySelector("[data-page-table]");

    const storedSize = Number(localStorage.getItem(storageKey));
    let pageSize = allowedSizes.includes(storedSize) ? storedSize : 25;
    let page = 1;
    let hasNext = false;

    if (pageSizeSelect) {
      pageSizeSelect.value = String(pageSize);
    }

    const updateControls = () => {
      if (prevBtn) prevBtn.disabled = page <= 1;
      if (nextBtn) nextBtn.disabled = !hasNext;
      if (statusEl) statusEl.textContent = `Page ${page}`;
    };

    const showMessage = (message) => {
      if (loadingEl) loadingEl.textContent = message;
      if (tableHost) tableHost.innerHTML = "";
    };

    const loadPage = async () => {
      const endpoint = resolveEndpoint(container);
      if (!endpoint) {
        showMessage("Run the query to view results.");
        return;
      }
      if (loadingEl) loadingEl.textContent = "Loading results...";
      if (tableHost) tableHost.innerHTML = "";

      try {
        const url = `${endpoint}?page=${page}&pageSize=${pageSize}`;
        const response = await fetch(url);
        const payload = await response.json();
        if (!response.ok || payload.success !== true) {
          showMessage(payload?.errorMessage || "No results available.");
          return;
        }

        const normalized = normalizePayload(payload);
        hasNext = payload.hasNext === true || normalized.rows.length === pageSize;
        updateControls();

        if (infoEl) {
          if (payload.rowCount) {
            infoEl.textContent = `${payload.rowCount} rows`;
          } else {
            infoEl.textContent = hasNext ? "More rows available" : "";
          }
        }

        if (loadingEl) loadingEl.textContent = "";
        const configJson = container.dataset.tableConfig;
        if (configJson && window.queryBuilderTable?.render) {
          const config = JSON.parse(configJson);
          window.queryBuilderTable.render(tableHost, {
            columns: normalized.columnNames,
            rows: normalized.rows,
            config,
            allowUnsafeHtml: container.dataset.allowUnsafeHtml === "true"
          });
        } else {
          renderSimpleTable(tableHost, normalized.columnNames, normalized.rows);
        }
      } catch (err) {
        showMessage("Unable to load results.");
      }
    };

    pageSizeSelect?.addEventListener("change", () => {
      const value = Number(pageSizeSelect.value);
      pageSize = allowedSizes.includes(value) ? value : 25;
      localStorage.setItem(storageKey, String(pageSize));
      page = 1;
      if (tableHost) {
        delete tableHost.dataset.searchTerm;
      }
      loadPage();
    });

    prevBtn?.addEventListener("click", () => {
      if (page <= 1) return;
      page -= 1;
      loadPage();
    });

    nextBtn?.addEventListener("click", () => {
      if (!hasNext) return;
      page += 1;
      loadPage();
    });

    updateControls();
    loadPage();
  };

  document.querySelectorAll("[data-paged-table='true']").forEach(initContainer);
})();

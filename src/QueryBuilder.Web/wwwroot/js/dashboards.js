(() => {
  const state = window.dashboardBuilder;
  if (!state) return;

  const grid = GridStack.init({ margin: 8, cellHeight: 80, float: true });
  const gridEl = document.querySelector(".grid-stack");
  const select = document.getElementById("vizSelect");
  const addBtn = document.getElementById("addWidgetBtn");
  const saveBtn = document.getElementById("saveLayoutBtn");
  const tokenInput = document.querySelector("input[name='__RequestVerificationToken']");

  const checkEmpty = () => {
    if (grid.engine.nodes.length === 0) {
      gridEl.classList.add("grid-stack-empty");
      gridEl.innerHTML = `
        <div class="text-center text-muted p-5">
            <i class="bi bi-kanban" style="font-size: 3rem;"></i>
            <p class="mt-3">This dashboard is empty. Add a visualization to get started.</p>
        </div>
      `;
    } else {
      gridEl.classList.remove("grid-stack-empty");
      const emptyEl = gridEl.querySelector(".text-center");
      if(emptyEl) emptyEl.remove();
    }
  };

  const addWidget = (widget) => {
    checkEmpty();
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
        checkEmpty();
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

  checkEmpty();

  addBtn?.addEventListener("click", () => {
    const selectedId = Number(select.value);
    if (!selectedId) return;

    const visualization = state.visualizations.find((v) => v.id === selectedId);
    if (!visualization) return;

    addWidget({
      visualizationId: visualization.id,
      visualizationName: visualization.name,
      visualizationType: visualization.type,
      x: 0,
      y: 0,
      width: 4,
      height: 4
    });
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

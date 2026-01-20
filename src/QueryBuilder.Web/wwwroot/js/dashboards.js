(() => {
  const state = window.dashboardBuilder;
  if (!state) return;

  const grid = GridStack.init({ margin: 8, cellHeight: 80, float: true });
  const gridEl = document.querySelector(".grid-stack");
  const select = document.getElementById("vizSelect");
  const addBtn = document.getElementById("addWidgetBtn");
  const saveBtn = document.getElementById("saveLayoutBtn");
  const tokenInput = document.querySelector("input[name='__RequestVerificationToken']");

  const addWidget = (widget) => {
    const content = document.createElement("div");
    content.className = "grid-stack-item-content card";
    const body = document.createElement("div");
    body.className = "card-body";
    const title = document.createElement("div");
    title.className = "fw-semibold";
    title.textContent = widget.visualizationName;
    const meta = document.createElement("div");
    meta.className = "text-muted small";
    meta.textContent = widget.visualizationType;
    body.appendChild(title);
    body.appendChild(meta);
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

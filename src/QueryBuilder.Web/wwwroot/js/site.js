// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
(() => {
  const body = document.body;
  const toggle = document.getElementById("sidebarToggle");
  if (!toggle) return;

  const applyState = (collapsed) => {
    body.classList.toggle("sidebar-collapsed", collapsed);
    const icon = toggle.querySelector(".sidebar-toggle-icon");
    const label = toggle.querySelector(".sidebar-label");
    if (icon) {
      icon.className = collapsed
        ? "sidebar-toggle-icon bi bi-chevron-right"
        : "sidebar-toggle-icon bi bi-chevron-left";
    }
    if (label) {
      label.textContent = collapsed ? "Expand" : "Collapse";
    }
  };

  const stored = localStorage.getItem("qb.sidebar.collapsed");
  applyState(stored === "1");

  toggle.addEventListener("click", () => {
    const collapsed = !body.classList.contains("sidebar-collapsed");
    localStorage.setItem("qb.sidebar.collapsed", collapsed ? "1" : "0");
    applyState(collapsed);
  });

  document.querySelectorAll(".sidebar .nav-link").forEach((link) => {
    const label = link.querySelector(".sidebar-label");
    if (label && !link.title) {
      link.title = label.textContent || "";
    }
  });
})();

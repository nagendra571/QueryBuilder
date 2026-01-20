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

(() => {
  const seed = document.getElementById("toastSeed");
  const container = document.getElementById("toastContainer");
  if (!seed || !container) return;

  const message = seed.getAttribute("data-toast-message");
  const level = seed.getAttribute("data-toast-level") || "info";
  if (!message) return;

  const toast = document.createElement("div");
  toast.className = `toast align-items-center text-bg-${level} border-0`;
  toast.setAttribute("role", "alert");
  toast.setAttribute("aria-live", "assertive");
  toast.setAttribute("aria-atomic", "true");

  toast.innerHTML = `
    <div class="d-flex">
      <div class="toast-body">${message}</div>
      <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
    </div>
  `;

  container.appendChild(toast);
  const bsToast = new bootstrap.Toast(toast, { delay: 3500 });
  bsToast.show();
})();

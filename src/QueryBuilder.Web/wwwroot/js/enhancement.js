// enhancement.js
(function () {
    'use strict';

    // Sidebar toggle
    const sidebarToggle = document.getElementById('sidebarToggle');
    if (sidebarToggle) {
        sidebarToggle.addEventListener('click', function () {
            document.body.classList.toggle('sidebar-collapsed');
        });
    }
})();

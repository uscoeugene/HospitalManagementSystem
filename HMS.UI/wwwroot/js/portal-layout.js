document.addEventListener("DOMContentLoaded", function () {
    const body = document.body;
    const sidebar = document.getElementById("portalSidebar");
    const openButtons = document.querySelectorAll("[data-sidebar-toggle]");
    const closeButtons = document.querySelectorAll("[data-sidebar-close]");

    if (!sidebar) {
        return;
    }

    const openSidebar = function () {
        sidebar.classList.add("show");
        body.classList.add("sidebar-open");
    };

    const closeSidebar = function () {
        sidebar.classList.remove("show");
        body.classList.remove("sidebar-open");
    };

    openButtons.forEach(function (button) {
        button.addEventListener("click", openSidebar);
    });

    closeButtons.forEach(function (button) {
        button.addEventListener("click", closeSidebar);
    });

    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape") {
            closeSidebar();
        }
    });

    window.addEventListener("resize", function () {
        if (window.innerWidth >= 992) {
            closeSidebar();
        }
    });
});

document.addEventListener("DOMContentLoaded", function () {
    function appendQueryParams(url, extraQuery) {
        var resolved = new URL(url, window.location.origin);
        if (extraQuery) {
            var extra = new URLSearchParams(extraQuery);
            extra.forEach(function (value, key) {
                if (value === null || value === undefined || value === "") {
                    return;
                }
                resolved.searchParams.set(key, value);
            });
        }
        return resolved.toString();
    }

    function getAjaxTarget(element) {
        var targetSelector = element.getAttribute("data-ajax-target");
        if (!targetSelector) {
            return null;
        }

        return {
            selector: targetSelector,
            element: document.querySelector(targetSelector)
        };
    }

    function getAjaxSwapMode(element) {
        return element.getAttribute("data-ajax-swap") || "outerHTML";
    }

    function getAjaxExtraQuery(element) {
        return element.getAttribute("data-ajax-extra-query") || "";
    }

    async function swapAjaxContent(sourceElement, url, options) {
        var targetInfo = getAjaxTarget(sourceElement);
        if (!targetInfo || !targetInfo.element) {
            window.location.href = url;
            return;
        }

        var displayUrl = url;
        var requestUrl = appendQueryParams(url, getAjaxExtraQuery(sourceElement));
        var target = targetInfo.element;
        var swapMode = getAjaxSwapMode(sourceElement);

        target.setAttribute("aria-busy", "true");
        target.classList.add("ajax-panel-busy");

        try {
            var response = await fetch(requestUrl, {
                method: options.method || "GET",
                headers: {
                    "X-Requested-With": "XMLHttpRequest",
                    "X-Ajax-Panel": targetInfo.selector
                },
                body: options.body || null
            });

            if (!response.ok) {
                throw new Error("Request failed");
            }

            var html = await response.text();
            if (swapMode === "innerHTML") {
                target.innerHTML = html;
            } else {
                target.outerHTML = html;
            }

            if (window.history && window.history.replaceState) {
                window.history.replaceState({}, "", displayUrl);
            }
        } catch (error) {
            console.warn(error);
            window.location.href = displayUrl;
        } finally {
            var currentTarget = document.querySelector(targetInfo.selector);
            if (currentTarget) {
                currentTarget.removeAttribute("aria-busy");
                currentTarget.classList.remove("ajax-panel-busy");
            }
        }
    }

    var body = document.body;
    var sidebar = document.getElementById("portalSidebar");
    var toggles = document.querySelectorAll("[data-sidebar-toggle]");
    var closers = document.querySelectorAll("[data-sidebar-close]");
    var isDesktop = window.innerWidth >= 992;

    function setDesktopState(collapsed, pinned) {
        body.classList.toggle("sidebar-collapsed", collapsed);
        body.classList.toggle("sidebar-pinned", pinned);
        body.classList.remove("sidebar-open");
        if (sidebar) {
            sidebar.classList.remove("show");
            sidebar.classList.remove("sidebar-hover");
        }
    }

    function openSidebar() {
        body.classList.add("sidebar-open");
        if (sidebar) {
            sidebar.classList.add("show");
        }
    }

    function closeSidebar() {
        body.classList.remove("sidebar-open");
        if (sidebar) {
            sidebar.classList.remove("show");
        }
    }

    if (isDesktop) {
        setDesktopState(true, false);
    }

    toggles.forEach(function (toggle) {
        toggle.addEventListener("click", function () {
            if (window.innerWidth >= 992) {
                if (body.classList.contains("sidebar-pinned")) {
                    setDesktopState(true, false);
                } else {
                    setDesktopState(false, true);
                }
                return;
            }

            if (body.classList.contains("sidebar-open")) {
                closeSidebar();
            } else {
                openSidebar();
            }
        });
    });

    closers.forEach(function (closer) {
        closer.addEventListener("click", function () {
            closeSidebar();
        });
    });

    if (sidebar) {
        sidebar.addEventListener("mouseenter", function () {
            if (window.innerWidth >= 992 && body.classList.contains("sidebar-collapsed") && !body.classList.contains("sidebar-pinned")) {
                sidebar.classList.add("sidebar-hover");
            }
        });

        sidebar.addEventListener("mouseleave", function () {
            if (window.innerWidth >= 992) {
                sidebar.classList.remove("sidebar-hover");
            }
        });
    }

    window.addEventListener("resize", function () {
        if (window.innerWidth >= 992) {
            closeSidebar();
            if (!body.classList.contains("sidebar-pinned")) {
                body.classList.add("sidebar-collapsed");
            }
        } else {
            body.classList.remove("sidebar-collapsed", "sidebar-pinned");
            if (sidebar) {
                sidebar.classList.remove("sidebar-hover");
            }
        }
    });

    var sectionSelects = document.querySelectorAll("[data-chart-section-select]");
    sectionSelects.forEach(function (select) {
        select.addEventListener("change", function () {
            var targetId = this.value;
            if (!targetId) {
                return;
            }

            var trigger = document.querySelector('[data-bs-toggle="tab"][data-bs-target="#' + targetId + '"]');
            if (!trigger && window.bootstrap && bootstrap.Tab) {
                trigger = document.querySelector('[data-bs-target="#' + targetId + '"]');
            }

            if (trigger && window.bootstrap && bootstrap.Tab) {
                bootstrap.Tab.getOrCreateInstance(trigger).show();
            }
        });
    });

    var tabScrollButtons = document.querySelectorAll("[data-chart-tabs-scroll]");
    tabScrollButtons.forEach(function (button) {
        button.addEventListener("click", function () {
            var direction = this.getAttribute("data-chart-tabs-scroll");
            var wrap = this.closest(".chart-tabs-wrap");
            var scrollHost = wrap ? wrap.querySelector(".chart-tabs-scroll") : document.querySelector(".chart-tabs-scroll");
            if (!scrollHost) {
                return;
            }

            var amount = Math.max(180, Math.floor(scrollHost.clientWidth * 0.7));
            var targetLeft = scrollHost.scrollLeft + (direction === "prev" ? -amount : amount);
            if (direction === "prev") {
                scrollHost.scrollTo({ left: targetLeft, behavior: "smooth" });
            } else if (direction === "next") {
                scrollHost.scrollTo({ left: targetLeft, behavior: "smooth" });
            }
        });
    });

    document.addEventListener("submit", function (event) {
        var form = event.target.closest("form[data-ajax-target]");
        if (!form) {
            return;
        }

        event.preventDefault();

        var method = (form.getAttribute("method") || "get").toUpperCase();
        if (method !== "GET") {
            form.submit();
            return;
        }

        var formData = new FormData(form);
        var url = new URL(form.action || window.location.href, window.location.origin);
        formData.forEach(function (value, key) {
            if (value !== null && value !== undefined) {
                url.searchParams.set(key, value);
            }
        });

        swapAjaxContent(form, url.toString(), { method: "GET" });
    });

    document.addEventListener("click", function (event) {
        var link = event.target.closest("a[data-ajax-target]");
        if (!link || link.getAttribute("href") === "#" || link.closest(".disabled")) {
            return;
        }

        event.preventDefault();
        swapAjaxContent(link, link.href, { method: "GET" });
    });
});

// Global Popup System (Replacement for SweetAlert)
window.showSidePopup = function (
    element,
    title,
    onConfirm,
    icon = "fa-question-circle",
    confirmText = "Confirm",
    iconColor = "var(--nc-primary)",
    customContent = "",
    btnExtraClass = "",
    extraClass = "",
    hideArrow = false,
) {

    const isAlreadyOpen = element?.classList.contains("ut-popup-active-trigger");
    window.closePopups();
    if (!element || isAlreadyOpen) return;

    // Elevate trigger button above overlay via portal (handled in showSidePopup)
    element.classList.add("ut-popup-active-trigger");

    const overlay = document.getElementById("ncGlobalOverlay");
    if (overlay) overlay.classList.add("show");

    const rect = element.getBoundingClientRect();

    // Create Visual Portal (Trigger Clone) to bypass stacking context issues
    const portal = document.createElement("div");
    portal.id = "utPortalTrigger";
    portal.className = "ut-visual-portal";
    const clone = element.cloneNode(true);

    // Copy computed styles to ensure appearance is identical
    const style = window.getComputedStyle(element);
    portal.style.position = "absolute";
    portal.style.top = `${rect.top + window.scrollY}px`;
    portal.style.left = `${rect.left + window.scrollX}px`;
    portal.style.width = `${rect.width}px`;
    portal.style.height = `${rect.height}px`;
    portal.style.zIndex = "1050";
    portal.style.pointerEvents = "none";
    portal.style.transition = "none";

    portal.appendChild(clone);
    element.classList.add("ut-trigger-hidden");
    document.body.appendChild(portal);

    const popup = document.createElement("div");
    popup.className = `ut-side-popup`; // Don't add extraClass/animation yet
    popup.style.position = "absolute";
    popup.style.left = "-9999px";
    popup.style.top = "-9999px";
    popup.style.visibility = "hidden";
    popup.style.display = "block";
    const isWide = rect.width > 200 || (extraClass && extraClass.includes("ut-popup-dropdown")); // Force dropdown logic for specific types
    const showOnLeft = rect.left > window.innerWidth / 2;
    const showOnTop = rect.bottom > window.innerHeight * 0.7; // If in bottom 30%, show on top

    // Initial classes (will refine isWide positioning later)
    popup.className = `ut-side-popup ${extraClass || ""} ${isWide ? (showOnTop ? "ut-popup-top" : "ut-popup-bottom") : showOnLeft ? "ut-popup-left" : "ut-popup-right"}`;

    popup.innerHTML = `
        <div class="ut-popup-content">
            <div class="ut-popup-header"><i class="fa ${icon}" style="color: ${iconColor}"></i><span>${title}</span></div>
            ${customContent
            ? customContent
            : `
                <div class="ut-popup-actions">
                    <button class="ut-popup-btn-cancel">Cancel</button>
                    <button class="ut-popup-btn-confirm ${btnExtraClass || ""}">${confirmText}</button>
                </div>
            `
        }
        </div>
        ${hideArrow ? "" : '<div class="ut-popup-arrow"></div>'}
    `;


    // Add unique identifier if provided via extraClass for re-render targeting
    if (extraClass && extraClass.includes("clear")) popup.setAttribute("data-popup-type", "clear");
    if (extraClass && extraClass.includes("save")) popup.setAttribute("data-popup-type", "save");

    document.body.appendChild(popup);
    // Reveal and Position accurately in the next frame to allow layout to settle
    requestAnimationFrame(() => {
        const popupWidth = popup.offsetWidth;
        const popupHeight = popup.offsetHeight;
        const scrollY = window.scrollY;

        if (isWide) {
            if (showOnTop) {
                popup.style.top = `${rect.top + scrollY - popupHeight - 12}px`;
            } else {
                popup.style.top = `${rect.bottom + scrollY + 12}px`;
            }
            popup.style.transform = "none";

            if (extraClass && extraClass.includes("ut-popup-dropdown")) {
                // Dropdown alignment (Left or Right based on screen half)
                if (showOnLeft) {
                    popup.style.left = `${rect.right - popupWidth}px`;
                } else {
                    popup.style.left = `${rect.left}px`;
                }
            } else {
                // Standard wide modal centering
                popup.style.left = `${rect.left + rect.width / 2 - popupWidth / 2}px`;
            }
        } else if (customContent) {
            popup.style.top = `${rect.bottom + scrollY + 12}px`;
            popup.style.transform = "none";
            if (showOnLeft) {
                popup.style.left = `${rect.right - popupWidth}px`;
            } else {
                popup.style.left = `${rect.left}px`;
            }
        } else {
            // Standard siding for narrow triggers
            popup.style.top = `${rect.top + scrollY + rect.height / 2}px`;
            if (showOnLeft) {
                popup.style.left = `${rect.left - 12}px`;
            } else {
                popup.style.left = `${rect.right + 12}px`;
            }
        }

        popup.style.visibility = "visible";
        popup.style.opacity = "1";
    });

    popup.querySelector(".ut-popup-btn-cancel")?.addEventListener("click", () => {
        element.classList.remove("ut-popup-active-trigger");
        element.classList.remove("ut-trigger-hidden");
        window.closePopups();
    });
    popup.querySelector(".ut-popup-btn-confirm")?.addEventListener("click", () => {
        element.classList.remove("ut-popup-active-trigger");
        element.classList.remove("ut-trigger-hidden");
        onConfirm();
        window.closePopups();
    });

    setTimeout(() => {
        const handleOutside = (e) => {
            if (popup.parentNode && !popup.contains(e.target) && e.target !== element) {
                element.classList.remove("ut-popup-active-trigger");
                element.classList.remove("ut-trigger-hidden");
                window.closePopups();
                document.removeEventListener("mousedown", handleOutside);
            }
        };
        document.addEventListener("mousedown", handleOutside);
    }, 10);
};

window.closePopups = function () {
    document.querySelectorAll(".ut-filter-popup, .ut-diff-modal").forEach((p) => (p.style.display = "none"));
    document.querySelectorAll(".ut-side-popup").forEach((p) => p.remove());
    document.querySelectorAll(".ut-popup-active-trigger").forEach((el) => el.classList.remove("ut-popup-active-trigger"));
    document.querySelectorAll(".ut-has-popup").forEach((el) => el.classList.remove("ut-has-popup"));

    const overlay = document.getElementById("ncGlobalOverlay");
    if (overlay) overlay.classList.remove("show");

    // Cleanup Visual Portal
    document.getElementById("utPortalTrigger")?.remove();
    document.getElementById("ncModalPortalTrigger")?.remove();
    document.querySelectorAll(".ut-trigger-hidden").forEach(el => el.classList.remove("ut-trigger-hidden"));
};


// Global delete confirmation
window.confirmDelete = function (id, url, onDeleted, element = null) {
    if (!element) return; // side-popup needs an anchor

    window.showSidePopup(
        element,
        "Terminate Listing?",
        () => {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            fetch(url, {
                method: "POST",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded",
                    "X-Requested-With": "XMLHttpRequest",
                    RequestVerificationToken: token,
                },
                body: new URLSearchParams({ id: id }),
            }).then((response) => {
                if (response.ok) {
                    window.showToast("Record has been deleted.", "success");
                    if (onDeleted) onDeleted();
                } else {
                    window.showToast("Failed to delete record.", "error");
                }
            });
        },
        "fa-trash",
        "Terminate",
        "var(--nc-error)",
        "",
        "btn-danger",
    );
};

// Global ban confirmation
window.confirmBan = function (formElement) {
    const button = formElement.querySelector("button");
    const isBanned = button.classList.contains("enable");
    const action = isBanned ? "unban" : "ban";
    const type = formElement.getAttribute("data-type") || "employee";

    window.showSidePopup(
        button,
        `${action.charAt(0).toUpperCase() + action.slice(1)} ${type}?`,
        () => {
            formElement.submit();
        },
        isBanned ? "fa-circle-check" : "fa-ban",
        `Yes, ${action}`,
        isBanned ? "var(--nc-primary)" : "var(--nc-error)",
        "",
        isBanned ? "btn-success" : "btn-danger",
    );
};

// Global restore confirmation
window.confirmRestore = function (id, url, onRestored, element = null) {
    if (!element) return;

    window.showSidePopup(
        element,
        "Restore Record?",
        () => {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            fetch(url, {
                method: "POST",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded",
                    "X-Requested-With": "XMLHttpRequest",
                    RequestVerificationToken: token,
                },
                body: new URLSearchParams({ id: id }),
            }).then((response) => {
                if (response.ok) {
                    window.showToast("Record has been restored.", "success");
                    if (onRestored) onRestored();
                } else {
                    window.showToast("Failed to restore record.", "error");
                }
            });
        },
        "fa-undo",
        "Restore",
        "var(--nc-success)",
        "",
        "btn-success",
    );
};
// Global Search Highlighting Utility
// Global Search Highlighting Utility (String-based - use for plain text only)
window.ncHighlight = function (text, query) {
    if (!query || !text) return text;
    const str = String(text);
    const escaped = query.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    const regex = new RegExp(`(${escaped})(?![^<]*>)`, "gi");
    return str.replace(regex, '<mark class="ut-highlight">$1</mark>');
};

// Global Search Highlighting Utility (DOM-based - Preserves Icons)
window.ncHighlightElement = function (element, query) {
    if (!query || !element) return;

    // Normalize query
    const term = query.trim().toLowerCase();
    if (!term) return;

    const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT, null, false);
    const nodes = [];
    while (walker.nextNode()) nodes.push(walker.currentNode);

    const escaped = term.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

    // Handle numeric matching: allow "1500" to match "1,500"
    // Also handle partial numbers "15" -> "1,5"
    let numberRegexPattern = escaped;
    if (/^\d+$/.test(term)) {
        const withCommas = term.replace(/(\d)(?=(\d{3})+(?!\d))/g, "$1,");
        numberRegexPattern = `(${escaped}|${withCommas.replace(/,/g, ",?")})`;
    }

    const regex = new RegExp(numberRegexPattern, "gi");

    nodes.forEach((node) => {
        const text = node.nodeValue;
        if (regex.test(text)) {
            const span = document.createElement("span");
            // Set innerHTML using the string-based highlight logic
            // But we use textContent here because we ARE the text node
            span.innerHTML = text.replace(regex, '<mark class="ut-highlight">$&</mark>');
            node.parentNode.replaceChild(span, node);
        }
    });
};

// Mobile Side Menu Setup
document.addEventListener("DOMContentLoaded", () => {
    const mobileMenuToggle = document.getElementById("ncMobileMenuToggle");
    const mobileMenuClose = document.getElementById("ncMobileMenuClose");
    const mobileMenu = document.getElementById("ncMobileMenu");
    const mobileMenuOverlay = document.getElementById("ncMobileMenuOverlay");

    function toggleMobileMenu() {
        const isShowing = mobileMenu.classList.contains("show");
        if (isShowing) {
            mobileMenu.classList.remove("show");
            mobileMenuOverlay.classList.remove("show");
            document.body.style.overflow = "";
        } else {
            mobileMenu.classList.add("show");
            mobileMenuOverlay.classList.add("show");
            document.body.style.overflow = "hidden";
        }
    }

    if (mobileMenuToggle) mobileMenuToggle.addEventListener("click", toggleMobileMenu);
    if (mobileMenuClose) mobileMenuClose.addEventListener("click", toggleMobileMenu);
    if (mobileMenuOverlay) mobileMenuOverlay.addEventListener("click", toggleMobileMenu);
});

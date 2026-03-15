/**
 * NightCord State Preservation
 * Handles scroll positions and carousel indices to improve back navigation.
 */

const StatePreservation = {
    // Current page URL serving as key
    getPageKey: () => window.location.pathname + window.location.search,

    saveScroll: () => {
        sessionStorage.setItem(`scroll_${StatePreservation.getPageKey()}`, window.scrollY);
    },

    restoreScroll: () => {
        const navigationEntries = performance.getEntriesByType("navigation");
        const isBackForward = navigationEntries.length > 0 && navigationEntries[0].type === "back_forward";

        if (!isBackForward) return;

        const saved = sessionStorage.getItem(`scroll_${StatePreservation.getPageKey()}`);
        if (saved !== null) {
            const top = parseInt(saved);
            // Immediate attempt
            window.scrollTo({ top, behavior: "instant" });
            // Micro-task fallback for dynamic layouts
            setTimeout(() => {
                window.scrollTo({ top, behavior: "instant" });
            }, 0);
        }
    },

    saveCarouselIndex: (key, index) => {
        sessionStorage.setItem(key, index);
    },

    restoreCarouselIndex: (carouselEl, key) => {
        const saved = sessionStorage.getItem(key);
        if (saved !== null && carouselEl) {
            const index = parseInt(saved);
            const items = carouselEl.querySelectorAll(".carousel-item");
            if (items[index]) {
                items.forEach((i) => i.classList.remove("active"));
                items[index].classList.add("active");
                return index;
            }
        }
        return null;
    },

    init: () => {
        // Restore scroll on load
        StatePreservation.restoreScroll();

        // Save scroll on scroll (debounced slightly)
        let scrollTimeout;
        window.addEventListener(
            "scroll",
            () => {
                clearTimeout(scrollTimeout);
                scrollTimeout = setTimeout(StatePreservation.saveScroll, 150);
            },
            { passive: true },
        );

        // Save on navigate away
        window.addEventListener("beforeunload", StatePreservation.saveScroll);

        // Force instant scroll for same-page anchor links
        window.addEventListener("hashchange", () => {
            const target = document.querySelector(window.location.hash);
            if (target) {
                target.scrollIntoView({ behavior: "instant" });
            }
        });
    },
};

document.addEventListener("DOMContentLoaded", StatePreservation.init);
window.StatePreservation = StatePreservation;

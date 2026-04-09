/**
 * NcFloatingInput.js
 * A modern Web Component for the SIS-style floating input system.
 * Usage: <nc-floating-input label="Username" icon="fa-user" bg="#000">...</nc-floating-input>
 */

class NcFloatingInput extends HTMLElement {
    constructor() {
        super();
        this._initialized = false;
    }

    connectedCallback() {
        if (this._initialized) return;
        this.render();
        this.setupListeners();
        this._initialized = true;
    }

    static get observedAttributes() {
        return ["label", "icon", "bg"];
    }

    attributeChangedCallback(name, oldVal, newVal) {
        if (!this._initialized) return;
        if (name === "bg") this.style.setProperty("--nc-input-bg", newVal);
        else this.render();
    }

    render() {
        // Find internal control first
        const control = this.querySelector("input, textarea, select");
        if (!control) return;

        // Automatically detect parent background if not explicitly provided via 'bg' attribute
        let finalBg = "";
        const bgAttr = this.getAttribute("bg");
        if (bgAttr) {
            finalBg = bgAttr;
        } else if (this.parentElement) {
            finalBg = this.getParentBg(this.parentElement);
        }

        // Apply background and autofill fix to the COMPONENT itself (the field box)
        if (finalBg) {
            this.style.setProperty("--nc-input-bg", finalBg);
            // Autofill fix
            control.style.boxShadow = `0 0 0px 1000px ${finalBg} inset`;
        }

        const labelText = this.getAttribute("label") || "";
        const iconClass = this.getAttribute("icon") || "";

        // Ensure control has necessary class
        control.classList.add("nc-floating-control");

        // Setup Label
        let label = this.querySelector(".nc-floating-label");
        if (!label) {
            label = document.createElement("label");
            label.className = "nc-floating-label";
            this.appendChild(label);
        }

        // Link label to control
        if (!control.id) {
            control.id = "nc-input-" + Math.random().toString(36).substr(2, 9);
        }
        label.setAttribute("for", control.id);

        // Setup Label Content (Icon + Text)
        label.innerHTML = "";
        if (iconClass) {
            const icon = document.createElement("i");
            icon.className = `${iconClass} me-1`;
            icon.style.flexShrink = "0";
            label.appendChild(icon);
        }
        const textSpan = document.createElement("span");
        textSpan.className = "nc-label-text";
        textSpan.textContent = ` ${labelText}`;
        label.appendChild(textSpan);

        // Setup Password Toggle (if type="password")
        if (control.type === "password" || control.getAttribute("type") === "password") {
            this.setupPasswordToggle(control);
        }

        // Set data-label for validation engine
        control.setAttribute("data-label", labelText);

        // Inject Component-Specific Styles (Inside)
        this.injectInternalStyles();

        // Initial state check
        this.updateState(control);
    }

    injectInternalStyles() {
        if (this.querySelector("#nc-internal-styles")) return;
        const style = document.createElement("style");
        style.id = "nc-internal-styles";
        style.textContent = `
            nc-floating-input .nc-error-popup {
                left: 0 !important;
                width: 100% !important;
                transform: translateY(10px) scale(0.98) !important;
                transform-origin: center bottom !important;
                box-sizing: border-box !important;
            }
            nc-floating-input .nc-error-popup.show,
            nc-floating-input:hover .nc-error-popup {
                opacity: 1 !important;
                transform: translateY(0) scale(1) !important;
            }
        `;
        this.appendChild(style);
    }

    setupListeners() {
        const control = this.querySelector("input, textarea, select");
        if (!control) return;

        control.addEventListener("input", () => this.updateState(control));
        control.addEventListener("focus", () => this.classList.add("nc-is-focused"));
        control.addEventListener("blur", () => {
            this.classList.remove("nc-is-focused");
            this.updateState(control);
        });
    }

    setupPasswordToggle(control) {
        let toggle = this.querySelector(".nc-password-toggle");
        if (!toggle) {
            toggle = document.createElement("button");
            toggle.type = "button";
            toggle.className = "nc-password-toggle";
            toggle.innerHTML = '<i class="fa fa-eye"></i>';
            toggle.tabIndex = -1;
            this.appendChild(toggle);
        }

        toggle.onclick = (e) => {
            e.preventDefault();
            const isPassword = control.type === "password";
            control.type = isPassword ? "text" : "password";
            toggle.innerHTML = `<i class="fa fa-eye${isPassword ? "-slash" : ""}"></i>`;
        };
    }

    updateState(control) {
        const hasValue = control.value.trim().length > 0;
        if (hasValue) {
            this.classList.add("nc-has-content");
        } else {
            this.classList.remove("nc-has-content");
        }
    }

    getParentBg(el) {
        if (!el || el === document.body) {
            const bodyBg = getComputedStyle(document.body).backgroundColor;
            return this.solidify(bodyBg);
        }
        const bg = getComputedStyle(el).backgroundColor;
        // Check for non-transparent backgrounds
        if (bg && bg !== "transparent" && bg !== "rgba(0, 0, 0, 0)" && !bg.includes("rgba(0,0,0,0)")) {
            return this.solidify(bg);
        }
        return this.getParentBg(el.parentElement);
    }

    solidify(bg) {
        if (!bg) return bg;
        // If it's rgba, convert to rgb to make it opaque
        if (bg.startsWith("rgba")) {
            const parts = bg.match(/[\d.]+/g);
            if (parts && parts.length >= 3) {
                return `rgb(${parts[0]}, ${parts[1]}, ${parts[2]})`;
            }
        }
        return bg;
    }
}

customElements.define("nc-floating-input", NcFloatingInput);

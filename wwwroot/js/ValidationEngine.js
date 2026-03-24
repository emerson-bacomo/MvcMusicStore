/**
 * ValidationEngine.js
 * Real-time validation and floating label management inspired by SIS.
 */

window.ValidationEngine = {
    init() {
        this.observeDOM();
        this.initializeAll();
    },

    observeDOM() {
        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                mutation.addedNodes.forEach((node) => {
                    if (node.nodeType === 1) { // Element
                        if (node.classList.contains('nc-floating-control') || node.classList.contains('nc-pd-floating-control')) this.initControl(node);
                        node.querySelectorAll('.nc-floating-control, .nc-pd-floating-control').forEach(el => this.initControl(el));
                    }
                });
            });
        });
        observer.observe(document.body, { childList: true, subtree: true });
    },

    initializeAll() {
        document.querySelectorAll('.nc-floating-control, .nc-pd-floating-control').forEach(el => this.initControl(el));
    },

    initControl(el) {
        if (el._ncInitialized) return;
        el._ncInitialized = true;

        // Ensure placeholder is set for :placeholder-shown CSS selector
        if (!el.getAttribute('placeholder')) el.setAttribute('placeholder', ' ');

        // Input event for real-time validation
        el.addEventListener('input', () => {
            this.validate(el);
            if (el.tagName.toLowerCase() === 'textarea') this.autoResize(el);
        });

        // Password toggle initialization
        if (el.type === 'password') this.initPasswordToggle(el);
    },

    validate(el) {
        let error = "";
        const value = el.value.trim();
        const labelText = el.getAttribute('data-label') || "Field";

        // 1. Required Check
        if (el.hasAttribute('data-val-required') && value === "") {
            error = el.getAttribute('data-val-required') || `${labelText} is required.`;
        }

        // 2. Email Check
        if (!error && el.type === 'email' && value !== "") {
            const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
            if (!emailRegex.test(value)) {
                error = "Please enter a valid email address.";
            }
        }

        // 3. Length Checks
        if (!error && value !== "") {
            const minLen = el.getAttribute('data-val-length-min');
            const maxLen = el.getAttribute('data-val-length-max');
            if (minLen && value.length < parseInt(minLen)) {
                error = el.getAttribute('data-val-length') || `${labelText} must be at least ${minLen} characters.`;
            } else if (maxLen && value.length > parseInt(maxLen)) {
                error = el.getAttribute('data-val-length') || `${labelText} cannot exceed ${maxLen} characters.`;
            }
        }

        // 4. Custom Checks (e.g., Numeric)
        if (!error && el.getAttribute('data-type') === 'numeric' && value !== "") {
            if (isNaN(parseFloat(value))) {
                error = "Must be a valid number.";
            }
        }

        // 5. Compare Check (Confirm Password)
        if (!error && el.hasAttribute('data-compare')) {
            const targetId = el.getAttribute('data-compare');
            const targetEl = document.querySelector(targetId);
            if (targetEl && value !== targetEl.value.trim()) {
                error = el.getAttribute('data-val-equalto') || "Does not match.";
            }
        }

        // 6. Password Strength Check
        if (!error && (el.getAttribute('data-type') === 'password-strength' || el.classList.contains('nc-password-strength')) && value !== "") {
            if (!/[a-z]/.test(value)) error = "Lowercase character required.";
            else if (!/[A-Z]/.test(value)) error = "Uppercase character required.";
            else if (!/[0-9]/.test(value)) error = "Numeric character required.";
            else if (!/[^a-zA-Z0-9]/.test(value)) error = "Special character required.";
        }

        this.setError(el, error);
        return !error;
    },

    setError(el, message) {
        const label = el.parentElement.querySelector('.nc-floating-label, .nc-pd-floating-label');
        if (!label) return;

        let errorInline = label.querySelector('.nc-error-inline');
        let errorPopup = label.querySelector('.nc-error-popup');

        if (message) {
            el.classList.add('is-invalid');
            if (!errorInline) {
                errorInline = document.createElement('span');
                errorInline.className = 'nc-error-inline';
                label.appendChild(errorInline);
            }
            if (!errorPopup) {
                errorPopup = document.createElement('div');
                errorPopup.className = 'nc-error-popup';
                label.appendChild(errorPopup);
            }

            // Map to concise message for inline display
            let concise = "invalid";
            const msg = message.toLowerCase();
            
            if (msg.includes("lowercase")) concise = "no lowercase";
            else if (msg.includes("uppercase")) concise = "no uppercase";
            else if (msg.includes("numeric")) concise = "no number";
            else if (msg.includes("special")) concise = "no symbol";
            else if (msg.includes("required")) concise = "required";
            else if (msg.includes("email")) concise = "invalid";
            else if (msg.includes("at least")) concise = "too short";
            else if (msg.includes("cannot exceed")) concise = "too long";
            else if (msg.includes("number")) concise = "invalid number";
            else if (msg.includes("match")) concise = "mismatch";

            errorInline.textContent = ` (${concise})`; // Enclose in parentheses
            errorPopup.textContent = message;
        } else {
            el.classList.remove('is-invalid');
            if (errorInline) errorInline.remove();
            if (errorPopup) errorPopup.remove();
        }
    },

    autoResize(el) {
        el.style.height = 'auto';
        el.style.height = (el.scrollHeight) + 'px';
    },

    initPasswordToggle(el) {
        const wrapper = el.parentElement;
        if (!wrapper.classList.contains('nc-password-wrapper')) return;

        if (wrapper.querySelector('.nc-password-toggle')) return;

        const toggle = document.createElement('button');
        toggle.type = 'button';
        toggle.className = 'nc-password-toggle';
        toggle.innerHTML = '<i class="fa fa-eye"></i>';
        toggle.title = "Show Password";
        
        toggle.addEventListener('click', () => {
            const isPassword = el.type === 'password';
            el.type = isPassword ? 'text' : 'password';
            toggle.innerHTML = isPassword ? '<i class="fa fa-eye-slash"></i>' : '<i class="fa fa-eye"></i>';
            toggle.title = isPassword ? "Hide Password" : "Show Password";
        });

        wrapper.appendChild(toggle);
    }
};

// Initialize on DOM ready
document.addEventListener('DOMContentLoaded', () => window.ValidationEngine.init());

/**
 * ValidationEngine.js
 * Real-time validation and floating label management inspired by SIS.
 */

window.ValidationEngine = {
    init() {
        this._initDebouncedEmailCheck();
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
        document.querySelectorAll('input, select, textarea').forEach(el => {
            if (el.classList.contains('nc-floating-control') || el.classList.contains('nc-pd-floating-control')) {
                this.initControl(el);
            }
        });
    },

    initControl(el) {
        if (el._ncInitialized) return;
        el._ncInitialized = true;

        // Ensure placeholder is set for :placeholder-shown CSS selector
        if (!el.getAttribute('placeholder')) el.setAttribute('placeholder', ' ');

        // Input event for real-time validation
        el.addEventListener('input', () => {
            if (el.type === 'email' && el.value.trim().length > 0) {
                this.debouncedEmailCheck(el);
            } else {
                this.validate(el);
            }
            if (el.tagName.toLowerCase() === 'textarea') this.autoResize(el);
        });

        // Password toggle initialization
        if (el.type === 'password') {
            this.initPasswordToggle(el);
            if (el.classList.contains('nc-password-strength')) {
                this.initPasswordRequirements(el);
            }
        }
    },

    validate(el) {
        const value = el.value.trim();
        const errors = [];

        // 1. Required Check
        if (el.hasAttribute('required') && value === "") {
            errors.push(el.getAttribute('data-val-required') || "This field is required.");
        }

        // 2. Email Format Check
        if (value !== "" && (el.type === 'email' || el.getAttribute('data-type') === 'email')) {
            const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
            if (!emailRegex.test(value)) {
                errors.push("Please enter a valid email address.");
            }
        }

        // 3. Min Length Check
        const minLength = el.getAttribute('data-val-length-min') || el.getAttribute('minlength');
        if (value !== "" && minLength && value.length < parseInt(minLength)) {
            errors.push(`Must be at least ${minLength} characters.`);
        }

        // 4. Max Length Check
        const maxLength = el.getAttribute('data-val-length-max') || el.getAttribute('maxlength');
        if (value !== "" && maxLength && value.length > parseInt(maxLength)) {
            errors.push(`Cannot exceed ${maxLength} characters.`);
        }

        // 5. Compare Check (Confirm Password)
        if (el.hasAttribute('data-compare')) {
            const targetId = el.getAttribute('data-compare');
            const targetEl = document.querySelector(targetId);
            if (targetEl && value !== targetEl.value.trim()) {
                errors.push(el.getAttribute('data-val-equalto') || "Does not match.");
            }
        }

        // 6. Password Strength Check
        if ((el.getAttribute('data-type') === 'password-strength' || el.classList.contains('nc-password-strength')) && value !== "") {
            if (!/[a-z]/.test(value)) errors.push("Lowercase character required.");
            if (!/[A-Z]/.test(value)) errors.push("Uppercase character required.");
            if (!/[0-9]/.test(value)) errors.push("Numeric character required.");
            if (!/[^a-zA-Z0-9]/.test(value)) errors.push("Special character required.");
        }

        let finalError = "";
        if (errors.length > 1) {
            finalError = errors.map(e => "• " + e.replace(/^•\s*/, "")).join("\n");
        } else if (errors.length === 1) {
            finalError = errors[0];
        }

        this.setError(el, finalError);
        return errors.length === 0;
    },

    setError(el, message) {
        // Support both floating labels and standard labels
        const label = el.parentElement.querySelector('.nc-floating-label, .nc-pd-floating-label, .nc-label');
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
            if (msg.includes("•")) concise = "invalid";
            else if (msg.includes("taken")) concise = "already taken";
            else if (msg.includes("required")) concise = "required";
            else if (msg.includes("at least")) concise = "too short";
            else if (msg.includes("cannot exceed")) concise = "too long";
            else if (msg.includes("number")) concise = "invalid";
            else if (msg.includes("match")) concise = "mismatch";
            else if (msg.includes("character required")) concise = "invalid";

            errorInline.textContent = ` (${concise})`; // Enclose in parentheses
            errorPopup.textContent = message;
        } else {
            el.classList.remove('is-invalid');
            if (errorInline) errorInline.remove();
            if (errorPopup) errorPopup.remove();
        }
    },

    debounce(func, wait) {
        let timeout;
        return function (...args) {
            const context = this;
            clearTimeout(timeout);
            timeout = setTimeout(() => func.apply(context, args), wait);
        };
    },

    debouncedEmailCheck: null,

    _initDebouncedEmailCheck() {
        if (this.debouncedEmailCheck) return;
        this.debouncedEmailCheck = this.debounce(async (el) => {
            // Use window.ValidationEngine explicitly for callback context safety
            const engine = window.ValidationEngine;

            // Basic validation first
            if (!engine.validate(el)) return;

            const emailAtStart = el.value.trim();
            console.log(el.getAttribute('data-ajax-url'));
            const ajaxUrl = el.getAttribute('data-ajax-url') || '/account/is-email-available';

            try {
                const response = await fetch(`${ajaxUrl}?email=${encodeURIComponent(emailAtStart)}`);
                if (!response.ok) throw new Error("Server error");

                const isAvailable = await response.json();

                // Stale check: if user typed more while we were fetching, ignore this result
                if (el.value.trim() !== emailAtStart) return;

                if (!isAvailable) {
                    engine.setError(el, "Email is already taken.");
                } else {
                    // Re-validate to clear any previous error
                    engine.validate(el);
                }
            } catch (err) {
                console.error("Email uniqueness check failed", err);
            }
        }, 500);
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
    },

    initPasswordRequirements(el) {
        const wrapper = el.parentElement;
        if (!wrapper) return;

        // Create popup if it doesn't exist
        let popup = wrapper.querySelector('.nc-password-requirements-popup');
        if (!popup) {
            popup = document.createElement('div');
            popup.className = 'nc-password-requirements-popup';
            popup.innerHTML = `
                <div class="nc-password-requirement-item" data-req="length">
                    <i class="fa fa-circle-o"></i> <span>At least 6 characters</span>
                </div>
                <div class="nc-password-requirement-item" data-req="uppercase">
                    <i class="fa fa-circle-o"></i> <span>One uppercase letter (A-Z)</span>
                </div>
                <div class="nc-password-requirement-item" data-req="lowercase">
                    <i class="fa fa-circle-o"></i> <span>One lowercase letter (a-z)</span>
                </div>
                <div class="nc-password-requirement-item" data-req="digit">
                    <i class="fa fa-circle-o"></i> <span>One numeric digit (0-9)</span>
                </div>
            `;
            wrapper.appendChild(popup);
        }

        const items = {
            length: popup.querySelector('[data-req="length"]'),
            uppercase: popup.querySelector('[data-req="uppercase"]'),
            lowercase: popup.querySelector('[data-req="lowercase"]'),
            digit: popup.querySelector('[data-req="digit"]')
        };

        const updateRequirements = () => {
            const val = el.value;

            // Length check
            const isLengthOk = val.length >= 6;
            this._toggleReq(items.length, isLengthOk);

            // Uppercase check
            const isUpperOk = /[A-Z]/.test(val);
            this._toggleReq(items.uppercase, isUpperOk);

            // Lowercase check
            const isLowerOk = /[a-z]/.test(val);
            this._toggleReq(items.lowercase, isLowerOk);

            // Digit check
            const isDigitOk = /[0-9]/.test(val);
            this._toggleReq(items.digit, isDigitOk);
        };

        el.addEventListener('focus', () => {
            updateRequirements();
            popup.classList.add('show');
        });

        el.addEventListener('blur', () => {
            popup.classList.remove('show');
        });

        el.addEventListener('input', updateRequirements);
    },

    _toggleReq(item, isOk) {
        if (!item) return;
        const icon = item.querySelector('i');
        if (isOk) {
            item.classList.add('success');
            icon.className = 'fa fa-check-circle';
        } else {
            item.classList.remove('success');
            icon.className = 'fa fa-circle-o';
        }
    }
};

// Initialize on DOM ready
document.addEventListener('DOMContentLoaded', () => window.ValidationEngine.init());

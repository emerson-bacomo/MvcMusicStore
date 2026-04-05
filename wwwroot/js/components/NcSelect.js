/**
 * NcSelect.js
 * A modern Web Component for a custom themed select dropdown.
 * Solves the scrollbar border-radius clipping issue via a nested container.
 * 
 * Usage:
 * <nc-select id="mySelect" name="topic" placeholder="Select a topic...">
 *     <div slot="options">
 *         <div class="nc-option" data-value="1">Option 1</div>
 *         <div class="nc-option" data-value="2">Option 2</div>
 *     </div>
 * </nc-select>
 */

class NcSelect extends HTMLElement {
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
        return ['placeholder', 'name', 'value'];
    }

    attributeChangedCallback(name, oldVal, newVal) {
        if (!this._initialized) return;
        if (name === 'value') {
            this.updateSelectedDisplay(newVal);
        } else {
            this.render();
        }
    }

    render() {
        // Save existing children (options) before wiping innerHTML
        const optionsContainer = this.querySelector('[slot="options"]');
        const optionsHtml = optionsContainer ? optionsContainer.innerHTML : "";
        
        const placeholder = this.getAttribute('placeholder') || "Select an option...";
        const name = this.getAttribute('name') || "";
        const value = this.getAttribute('value') || "";

        // Component Structure (No Shadow DOM, so we use a real div for slotting)
        this.innerHTML = `
            <div class="nc-select-container">
                <div class="nc-select-trigger">
                    <span class="nc-select-selected-text">${placeholder}</span>
                    <i class="fa fa-chevron-down nc-select-arrow"></i>
                </div>
                <div class="nc-select-dropdown">
                    <div class="nc-select-options-clip">
                        <div class="nc-select-options-list">
                            ${optionsHtml}
                        </div>
                    </div>
                </div>
                <input type="hidden" name="${name}" value="${value}" class="nc-select-input">
            </div>
        `;

        if (value) {
            this.updateSelectedDisplay(value);
        }
    }

    setupListeners() {
        const container = this.querySelector('.nc-select-container');
        const trigger = this.querySelector('.nc-select-trigger');
        const optionsList = this.querySelector('.nc-select-options-list');
        const input = this.querySelector('.nc-select-input');
        const selectedText = this.querySelector('.nc-select-selected-text');

        // Toggle Open
        trigger.addEventListener('click', (e) => {
            container.classList.toggle('open');
            e.stopPropagation();
        });

        // Option Selection (delegated)
        this.addEventListener('click', (e) => {
            const option = e.target.closest('.nc-option');
            if (option) {
                const val = option.getAttribute('data-value');
                const text = option.textContent;
                
                input.value = val;
                selectedText.textContent = text;
                container.classList.remove('open');

                // Update selected class
                this.querySelectorAll('.nc-option').forEach(opt => opt.classList.remove('selected'));
                option.classList.add('selected');

                // Dispatch change event
                this.dispatchEvent(new CustomEvent('change', { 
                    detail: { value: val, text: text },
                    bubbles: true 
                }));
            }
        });

        // Close on outside click
        document.addEventListener('click', () => {
            container.classList.remove('open');
        });
    }

    get value() {
        return this.querySelector('.nc-select-input')?.value || "";
    }

    set value(val) {
        this.updateSelectedDisplay(val);
    }

    updateSelectedDisplay(value) {
        const option = this.querySelector(`.nc-option[data-value="${value}"]`);
        const selectedText = this.querySelector('.nc-select-selected-text');
        const input = this.querySelector('.nc-select-input');
        
        if (option) {
            selectedText.textContent = option.textContent;
            if (input) input.value = value;
            this.querySelectorAll('.nc-option').forEach(opt => opt.classList.remove('selected'));
            option.classList.add('selected');
        }
    }
}

customElements.define('nc-select', NcSelect);

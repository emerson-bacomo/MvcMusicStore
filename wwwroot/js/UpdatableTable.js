export default class UpdatableTable {
    constructor(container, config) {
        this.container = typeof container === 'string' ? document.querySelector(container) : container;
        this.request = config.request || {};
        this.fieldDefinitions = config.fieldDefinitions || {};
        this.onSave = config.onSave;
        this.label = config.label || '';

        this.data = config.initialData || null;
        this.rows = new Map();
        this.localChanges = new Map();
        this.invalidCells = new Map(); // Map of "rowId:colId" -> errorMessage
        
        // Granular storage prefix (e.g., 'product')
        this.storagePrefix = config.storagePrefix || (this.label || '').toLowerCase().replace(/\s+/g, '-').replace(/ies$/, 'y').replace(/s$/, '') || 'item';
        
        this.searchQuery = '';
        this.sortConfig = { key: null, direction: 'none' };
        this.statusFilters = {
            active: true,
            deleted: false
        };
        this.filters = {
            visibleColumns: new Set()
        };
        
        // If initial data is provided, populate rows immediately
        if (this.data) {
            this.rows = new Map(Object.entries(this.data.rows || {}));
            if (this.filters.visibleColumns.size === 0) {
                this.data.columns.forEach(col => {
                    if (!col.hidden) this.filters.visibleColumns.add(col.id);
                });
            }
        }

        this.handleCellChange = this.handleCellChange.bind(this);
        this.saveChanges = this.saveChanges.bind(this);
        this.handleSearch = this.handleSearch.bind(this);
        this.toggleFilterMenu = this.toggleFilterMenu.bind(this);
        this.handleSort = this.handleSort.bind(this);

        this.renderContainer();
        this.parseUrlFilters(); // Sync URL params before fetching
        if (!this.data) {
            this.fetchData();
        } else {
            this.loadFromLocalStorage(); // Load after rows are populated if data is initial
            this.renderTable();
            this.updateControls();
        }
        this.initEventListeners();
    }

    saveToLocalStorage() {
        for (const [id, changes] of this.localChanges) {
            localStorage.setItem(`${this.storagePrefix}-${id}`, JSON.stringify(changes));
        }
    }

    loadFromLocalStorage() {
        try {
            const prefix = `${this.storagePrefix}-`;
            for (let i = 0; i < localStorage.length; i++) {
                const key = localStorage.key(i);
                if (key.startsWith(prefix)) {
                    const id = key.substring(prefix.length);
                    // Only load if the row actually exists in current data or we want to persist it anyway
                    const saved = localStorage.getItem(key);
                    if (saved) {
                        const rowChanges = JSON.parse(saved);
                        this.localChanges.set(id, rowChanges);
                        // Re-validate loaded changes
                        for (const colId of Object.keys(rowChanges)) {
                            this.validateCell(id, colId, rowChanges[colId], false);
                        }
                    }
                }
            }
        } catch (e) {
            console.warn("UpdatableTable: Failed to load from localStorage", e);
        }
    }

    clearLocalStorage() {
        const prefix = `${this.storagePrefix}-`;
        const keysToRemove = [];
        for (let i = 0; i < localStorage.length; i++) {
            const key = localStorage.key(i);
            if (key.startsWith(prefix)) keysToRemove.push(key);
        }
        keysToRemove.forEach(k => localStorage.removeItem(k));
    }

    parseUrlFilters() {
        const params = new URLSearchParams(window.location.search);
        params.forEach((value, key) => {
            if (key === 'status') {
                const statuses = value.split(',');
                this.statusFilters.active = statuses.includes('active');
                this.statusFilters.deleted = statuses.includes('deleted');
            } else {
                this.filters[key] = value;
            }
        });
    }

    renderContainer() {
        this.container.innerHTML = `
            <div class="ut-header-row">
                ${this.label ? `<h1 class="ut-header">${this.label}</h1>` : '<div></div>'}
                <div class="ut-top-controls">
                    <div class="ut-search-wrapper">
                        <i class="fa fa-search ut-search-icon"></i>
                        <input type="text" class="ut-search-input" placeholder="Search ${this.label.toLowerCase() || 'items'}...">
                    </div>
                    <div class="ut-filter-container">
                        <button class="ut-filter-btn" title="Filters">
                            <i class="fa fa-filter"></i>
                        </button>
                        <div class="ut-filter-popup">
                            <div class="ut-filter-section">
                                <h4>Record Status</h4>
                                <div class="ut-status-filters">
                                    <label class="ut-checkbox-label">
                                        <input type="checkbox" id="ut-filter-active" ${this.statusFilters.active ? 'checked' : ''}> 
                                        <span>Active</span> <span class="ut-count-badge active-count">0</span>
                                    </label>
                                    <label class="ut-checkbox-label">
                                        <input type="checkbox" id="ut-filter-deleted" ${this.statusFilters.deleted ? 'checked' : ''}> 
                                        <span>Deleted</span> <span class="ut-count-badge deleted-count">0</span>
                                    </label>
                                </div>
                            </div>
                            <div class="ut-filter-section ut-dropdown-filters">
                                <h4>Filters</h4>
                                <div class="ut-dropdown-filter-list"></div>
                            </div>
                            <div class="ut-filter-section">
                                <h4>Columns</h4>
                                <div class="ut-column-toggles"></div>
                            </div>
                        </div>
                    </div>
                    <button class="ut-save-btn" style="display: none;">Save Changes</button>
                </div>
            </div>
            <div class="ut-wrapper">
                <div class="ut-table-container">
                    <table class="ut-table nc-table">
                        <thead></thead>
                        <tbody></tbody>
                    </table>
                </div>
            </div>
        `;

        // Use delegated listener
        this.container.addEventListener('click', (e) => {
            if (e.target.closest('.ut-save-btn')) {
                this.showSaveDiffModal();
            }
        });

        this.container.querySelector('.ut-search-input').addEventListener('input', this.handleSearch);
        this.container.querySelector('.ut-filter-btn').addEventListener('click', (e) => {
            e.stopPropagation();
            this.toggleFilterMenu();
        });

        const activeCheck = this.container.querySelector('#ut-filter-active');
        const deletedCheck = this.container.querySelector('#ut-filter-deleted');

        activeCheck.addEventListener('change', (e) => {
            this.statusFilters.active = e.target.checked;
            this.updateUrl();
            this.renderTable();
        });
        deletedCheck.addEventListener('change', (e) => {
            this.statusFilters.deleted = e.target.checked;
            this.updateUrl();
            this.renderTable();
        });
    }

    initEventListeners() {
        document.addEventListener('click', (e) => {
            // Filter popup click outside
            const filterPopup = this.container.querySelector('.ut-filter-popup');
            const filterBtn = this.container.querySelector('.ut-filter-btn');
            if (filterPopup && filterPopup.style.display === 'block' && !filterPopup.contains(e.target) && !filterBtn.contains(e.target)) {
                filterPopup.style.display = 'none';
            }

            // Revert popup click outside
            const revertPopups = this.container.querySelectorAll('.ut-revert-popup');
            revertPopups.forEach(popup => {
                if (!popup.contains(e.target)) popup.remove();
            });
        });
    }

    handleSearch(e) {
        this.searchQuery = e.target.value.toLowerCase();
        this.renderTable();
    }

    toggleFilterMenu() {
        const popup = this.container.querySelector('.ut-filter-popup');
        popup.style.display = popup.style.display === 'block' ? 'none' : 'block';
        if (popup.style.display === 'block') {
            this.renderDropdownFilters();
            this.renderColumnToggles();
        }
    }

    renderDropdownFilters() {
        const container = this.container.querySelector('.ut-dropdown-filter-list');
        if (!this.data || !container) return;

        container.innerHTML = '';
        this.data.columns.forEach(col => {
            if (col.type === 'select' && col.options) {
                const section = document.createElement('div');
                section.className = 'ut-filter-item-dropdown';
                const label = this.fieldDefinitions[col.id]?.label || this.toLabelCase(col.id);
                
                section.innerHTML = `
                    <label style="display:block; font-size:0.75rem; color:var(--nc-text-muted); margin-bottom:0.25rem;">${label}</label>
                    <select class="ut-input ut-select" style="padding:0.3rem 0.5rem; height:auto; background:rgba(255,255,255,0.05); border:1px solid var(--nc-border); width:100%; border-radius:4px;">
                        <option value="">All ${label}s</option>
                        ${Array.from(col.options).map(opt => `<option value="${opt.value}" ${String(this.filters[col.id]) === String(opt.value) ? 'selected' : ''}>${opt.label}</option>`).join('')}
                    </select>
                `;

                section.querySelector('select').addEventListener('change', (e) => {
                    this.filters[col.id] = e.target.value;
                    this.updateUrl();
                    this.renderTable();
                });
                container.appendChild(section);
            }
        });
    }

    updateUrl() {
        const url = new URL(window.location);
        Object.entries(this.filters).forEach(([key, value]) => {
            if (key === 'visibleColumns') return;
            if (value && value !== 'false') {
                url.searchParams.set(key, value);
            } else {
                url.searchParams.delete(key);
            }
        });

        const statusValues = [];
        if (this.statusFilters.active) statusValues.push('active');
        if (this.statusFilters.deleted) statusValues.push('deleted');
        if (statusValues.length > 0 && statusValues.length < 2) {
            url.searchParams.set('status', statusValues.join(','));
        } else {
            url.searchParams.delete('status');
        }

        window.history.replaceState({}, '', url);
    }

    renderColumnToggles() {
        const container = this.container.querySelector('.ut-column-toggles');
        if (!this.data) return;

        container.innerHTML = '';
        this.data.columns.forEach(col => {
            if (col.id === 'recordStatus' || col.id === 'id' || col.id === 'actions' || (col.hidden && !this.filters.visibleColumns.has(col.id))) return;

            const label = this.fieldDefinitions[col.id]?.label || this.toLabelCase(col.id);
            const div = document.createElement('div');
            div.className = 'ut-column-toggle-item';
            div.innerHTML = `
                <label>
                    <input type="checkbox" data-col-id="${col.id}" ${this.filters.visibleColumns.has(col.id) ? 'checked' : ''}>
                    ${label}
                </label>
            `;
            div.querySelector('input').addEventListener('change', (e) => {
                if (e.target.checked) this.filters.visibleColumns.add(col.id);
                else this.filters.visibleColumns.delete(col.id);
                this.renderTable();
            });
            container.appendChild(div);
        });
    }

    handleSort(colId) {
        if (this.sortConfig.key === colId) {
            if (this.sortConfig.direction === 'asc') this.sortConfig.direction = 'desc';
            else if (this.sortConfig.direction === 'desc') {
                this.sortConfig.direction = 'none';
                this.sortConfig.key = null;
            }
        } else {
            this.sortConfig.key = colId;
            this.sortConfig.direction = 'asc';
        }
        this.renderTable();
    }

    async fetchData() {
        try {
            let response;
            if (this.request.fetchFn) {
                response = await this.request.fetchFn(this.request);
            } else if (this.request.url) {
                const res = await fetch(this.request.url, {
                    method: this.request.type || 'GET',
                    headers: { 'Content-Type': 'application/json' }
                });
                response = await res.json();
            } else {
                response = { columns: [], rows: {} };
            }

            this.data = response;
            this.rows = new Map(Object.entries(response.rows || {}));
            this.localChanges.clear();
            this.invalidCells.clear();

            if (this.filters.visibleColumns.size === 0) {
                this.data.columns.forEach(col => {
                    if (!col.hidden) this.filters.visibleColumns.add(col.id);
                });
            }

            this.renderTable();
            this.loadFromLocalStorage(); // Load after data is fetched and rows Map is populated
            this.renderTable(); // Re-render to show indicators
            this.updateControls();
        } catch (error) {
            console.error("UpdatableTable: Error fetching data", error);
        }
    }

    getProcessedRows() {
        let rowsArray = Array.from(this.rows.entries()).map(([id, data]) => ({ id, ...data }));

        // Count for filters
        this.activeCount = rowsArray.filter(r => r.recordStatus !== 'Deleted').length;
        this.deletedCount = rowsArray.filter(r => r.recordStatus === 'Deleted').length;

        // Apply Status Filter
        rowsArray = rowsArray.filter(r => {
            const isDeleted = r.recordStatus === 'Deleted';
            if (isDeleted) return this.statusFilters.deleted;
            return this.statusFilters.active;
        });

        if (this.searchQuery) {
            rowsArray = rowsArray.filter(row => {
                return Object.values(row).some(val => {
                    if (typeof val === 'string' || typeof val === 'number') {
                        return String(val).toLowerCase().includes(this.searchQuery);
                    }
                    if (val && typeof val === 'object' && val.name) {
                        return val.name.toLowerCase().includes(this.searchQuery);
                    }
                    return false;
                });
            });
        }

        // URL / Column Filters
        Object.entries(this.filters).forEach(([key, filterVal]) => {
            if (key === 'visibleColumns') return;
            if (filterVal) {
                rowsArray = rowsArray.filter(row => {
                    const cellVal = row[key];
                    if (cellVal === undefined) return true; // Col might not exist in row
                    // Handle objects (like {id, name}) or primitives
                    if (cellVal && typeof cellVal === 'object') {
                        return String(cellVal.id || cellVal.value || '') === String(filterVal);
                    }
                    return String(cellVal) === String(filterVal);
                });
            }
        });

        if (this.sortConfig.key && this.sortConfig.direction !== 'none') {
            const key = this.sortConfig.key;
            const dir = this.sortConfig.direction === 'asc' ? 1 : -1;
            rowsArray.sort((a, b) => {
                let v1 = a[key];
                let v2 = b[key];
                if (v1 && typeof v1 === 'object') v1 = v1.name || v1.label || '';
                if (v2 && typeof v2 === 'object') v2 = v2.name || v2.label || '';
                if (v1 < v2) return -1 * dir;
                if (v1 > v2) return 1 * dir;
                return 0;
            });
        }

        return rowsArray;
    }

    renderTable() {
        if (!this.data) return;

        const thead = this.container.querySelector('thead');
        const tbody = this.container.querySelector('tbody');

        let theadHtml = '<tr>';
        this.data.columns.forEach(col => {
            if (col.id === 'recordStatus' || (col.hidden && !this.filters.visibleColumns.has(col.id))) return;
            if (!this.filters.visibleColumns.has(col.id) && col.id !== 'actions') return;

            const label = this.fieldDefinitions[col.id]?.label || this.toLabelCase(col.id);
            const isSorting = this.sortConfig.key === col.id;
            let sortIcon = '<i class="fa fa-sort ut-sort-ghost"></i>';
            if (isSorting) {
                sortIcon = this.sortConfig.direction === 'asc' ? '<i class="fa fa-sort-up"></i>' : '<i class="fa fa-sort-down"></i>';
            }
            
            theadHtml += `
                <th data-col-id="${col.id}" class="ut-th ${col.id !== 'actions' ? 'ut-sortable' : ''}" 
                    style="width: ${col.id === 'actions' ? '1%' : (col.widthPercentage || 'auto')}; min-width: ${col.id === 'actions' ? 'auto' : (col.widthMinimum || 'auto')}; ${col.id === 'actions' ? 'text-align: right; white-space: nowrap;' : ''}">
                    <div class="ut-th-content" style="${col.id === 'actions' ? 'justify-content: flex-end;' : ''}">
                        <span>${label}</span>
                        ${col.id !== 'actions' ? `<span class="ut-sort-icon">${sortIcon}</span>` : ''}
                    </div>
                </th>
            `;
        });
        theadHtml += '</tr>';
        thead.innerHTML = theadHtml;

        thead.querySelectorAll('.ut-sortable').forEach(th => {
            th.addEventListener('click', () => this.handleSort(th.dataset.colId));
        });

        tbody.innerHTML = '';
        const processedRows = this.getProcessedRows();
        
        if (processedRows.length === 0) {
            tbody.innerHTML = '<tr><td colspan="100%" class="ut-empty-msg">No records found matching your criteria.</td></tr>';
            return;
        }

        processedRows.forEach(rowData => {
            const rowId = rowData.id;
            const tr = document.createElement('tr');
            tr.dataset.rowId = rowId;
            tr.className = rowData.recordStatus === 'Deleted' ? 'ut-deleted-row' : '';

            this.data.columns.forEach(col => {
                if (col.id === 'recordStatus' || (col.hidden && !this.filters.visibleColumns.has(col.id))) return;
                if (!this.filters.visibleColumns.has(col.id) && col.id !== 'actions') return;

                const td = document.createElement('td');
                td.dataset.colId = col.id;
                const isUpdatable = col.updatable && this.data.updateRequest && rowData.recordStatus !== 'Deleted';
                this.renderCellContent(td, rowId, col, rowData, isUpdatable);
                tr.appendChild(td);
            });
            tbody.appendChild(tr);
        });

        this.updateFilterCounts();
    }

    updateFilterCounts() {
        const activeBadge = this.container.querySelector('.active-count');
        const deletedBadge = this.container.querySelector('.deleted-count');
        if (activeBadge) activeBadge.textContent = this.activeCount || 0;
        if (deletedBadge) deletedBadge.textContent = this.deletedCount || 0;
    }

    renderCellContent(containerElement, rowId, col, rowData, isUpdatable) {
        const localRow = this.localChanges.get(rowId) || {};
        const isChanged = localRow.hasOwnProperty(col.id);
        const value = isChanged ? localRow[col.id] : rowData[col.id];
        const isInvalid = this.invalidCells.has(`${rowId}:${col.id}`);

        const wrapper = document.createElement('div');
        wrapper.className = 'ut-cell-wrapper';
        containerElement.appendChild(wrapper);

        if (!isUpdatable) {
            const fieldDef = this.fieldDefinitions[col.id];
            if (fieldDef && fieldDef.renderContent) {
                const content = fieldDef.renderContent(value, col, rowData);
                if (typeof content === 'string') wrapper.innerHTML = this.searchQuery ? this.highlight(content) : content;
                else if (content instanceof Node) wrapper.appendChild(content);
            } else {
                wrapper.innerHTML = `<span>${this.formatValue(value, col.id, true)}</span>`;
            }
        } else {
            const fieldDef = this.fieldDefinitions[col.id];
            if (fieldDef && fieldDef.renderInput) {
                const inputElement = fieldDef.renderInput(value, col, rowData);
                inputElement.addEventListener('input', (e) => this.handleCellChange(rowId, col.id, e.target.value));
                wrapper.appendChild(inputElement);
            } else if (col.type === 'select' && col.options) {
                const select = document.createElement('select');
                select.className = 'ut-input ut-select' + (isChanged ? ' ut-input-changed' : '');
                col.options.forEach(opt => {
                    const option = document.createElement('option');
                    option.value = opt.value;
                    option.textContent = opt.label;
                    option.selected = String(opt.value) === String(value);
                    select.appendChild(option);
                });
                select.addEventListener('change', (e) => this.handleCellChange(rowId, col.id, e.target.value));
                wrapper.appendChild(select);
            } else {
                const input = document.createElement('input');
                input.type = 'text';
                input.value = value || '';
                input.className = 'ut-input' + (isChanged ? ' ut-input-changed' : '');
                input.addEventListener('input', (e) => this.handleCellChange(rowId, col.id, e.target.value));
                wrapper.appendChild(input);
            }
        }

        this.renderIndicators(wrapper, rowId, col.id, isChanged, isInvalid);
    }

    renderIndicators(wrapper, rowId, colId, isChanged, isInvalid) {
        let stack = wrapper.querySelector('.ut-indicator-stack');
        if (stack) stack.remove();

        if (isChanged || isInvalid) {
            stack = document.createElement('div');
            stack.className = 'ut-indicator-stack';
            
            if (isChanged) {
                const changeIn = document.createElement('span');
                changeIn.className = 'ut-indicator ut-change-indicator';
                changeIn.title = 'Value changed. Click to revert.';
                changeIn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    this.showRevertConfirm(rowId, colId, changeIn);
                });
                stack.appendChild(changeIn);
            }

            if (isInvalid) {
                const invalidIn = document.createElement('span');
                invalidIn.className = 'ut-indicator ut-invalid-indicator';
                invalidIn.title = this.invalidCells.get(`${rowId}:${colId}`) || 'Invalid input.';
                stack.appendChild(invalidIn);
            }

            wrapper.appendChild(stack);
        }
    }

    showRevertConfirm(rowId, colId, anchorElement) {
        const existing = document.querySelector('.ut-revert-popup');
        if (existing) existing.remove();

        const popup = document.createElement('div');
        popup.className = 'ut-revert-popup ut-filter-popup'; // reuse animation
        popup.style.display = 'block';
        popup.innerHTML = `
            <span class="ut-revert-title">Revert Cell?</span>
            <div class="ut-revert-actions">
                <button class="ut-revert-yes">Yes</button>
                <button class="ut-revert-no">No</button>
            </div>
        `;
        
        anchorElement.parentElement.appendChild(popup);

        popup.querySelector('.ut-revert-yes').onclick = (e) => {
            e.stopPropagation();
            this.revertCell(rowId, colId);
            popup.remove();
        };
        popup.querySelector('.ut-revert-no').onclick = (e) => {
            e.stopPropagation();
            popup.remove();
        };
        
        // Prevent closing filter popup if inside
        popup.addEventListener('click', e => e.stopPropagation());
    }

    revertCell(rowId, colId) {
        const rowChanges = this.localChanges.get(rowId);
        if (rowChanges) {
            delete rowChanges[colId];
            if (Object.keys(rowChanges).length === 0)            this.localChanges.delete(rowId);
            this.invalidCells.delete(`${rowId}:${colId}`);
            this.saveToLocalStorage();
            this.renderTable();
            this.updateControls();
        }
    }

    validateCell(rowId, colId, newValue, updateUI = true) {
        const colDef = this.data?.columns.find(c => c.id === colId);
        const rules = colDef?.validation;
        let isValid = true;
        let errorMessage = '';

        if (rules) {
            const valStr = newValue !== null && newValue !== undefined ? String(newValue).trim() : '';
            if (rules.required && valStr === '') {
                isValid = false;
                errorMessage = rules.requiredMsg || 'Required field cannot be empty.';
            }
            if (isValid && rules.minLength && valStr.length < rules.minLength) {
                isValid = false;
                errorMessage = rules.minLengthMsg || `Minimum length is ${rules.minLength}.`;
            }
            if (isValid && rules.maxLength && valStr.length > rules.maxLength) {
                isValid = false;
                errorMessage = rules.maxLengthMsg || `Maximum length is ${rules.maxLength}.`;
            }
            if (isValid && rules.min !== undefined && Number(newValue) < rules.min) {
                isValid = false;
                errorMessage = rules.rangeMsg || `Minimum value is ${rules.min}.`;
            }
            if (isValid && rules.max !== undefined && Number(newValue) > rules.max) {
                isValid = false;
                errorMessage = rules.rangeMsg || `Maximum value is ${rules.max}.`;
            }
        } else {
            const isRequired = true; // Default to required if no rules? Or maybe not.
            // Let's assume default behavior is what was there
            isValid = !isRequired || (newValue !== null && newValue !== undefined && String(newValue).trim() !== '');
            if (!isValid) errorMessage = 'Required field cannot be empty.';
        }

        if (isValid) {
            this.invalidCells.delete(`${rowId}:${colId}`);
        } else {
            this.invalidCells.set(`${rowId}:${colId}`, errorMessage);
        }

        if (updateUI) {
            const tr = this.container.querySelector(`tr[data-row-id="${rowId}"]`);
            if (tr) {
                const td = tr.querySelector(`td[data-col-id="${colId}"]`);
                const wrapper = td?.querySelector('.ut-cell-wrapper');
                if (wrapper) {
                    const rowChanges = this.localChanges.get(rowId) || {};
                    const isChanged = rowChanges.hasOwnProperty(colId);
                    this.renderIndicators(wrapper, rowId, colId, isChanged, !isValid);
                }
            }
        }
        return isValid;
    }

    handleCellChange(rowId, colId, newValue) {
        let rowChanges = this.localChanges.get(rowId);
        if (!rowChanges) {
            rowChanges = {};
            this.localChanges.set(rowId, rowChanges);
        }

        const originalRow = this.rows.get(rowId) || {};
        
        // ── Manual Reset Logic (Update localChanges before validation for UI sync) ──────────────────────
        if (String(originalRow[colId] ?? '') === String(newValue ?? '')) {
            delete rowChanges[colId];
            if (Object.keys(rowChanges).length === 0) this.localChanges.delete(rowId);
        } else {
            rowChanges[colId] = newValue;
        }

        // ── Validation (will use updated localChanges for isChanged state) ──────────────────────────────
        this.validateCell(rowId, colId, newValue, true);

        this.saveToLocalStorage();
        this.updateControls();
        
        const tr = this.container.querySelector(`tr[data-row-id="${rowId}"]`);
        const td = tr?.querySelector(`td[data-col-id="${colId}"]`);
        const wrapper = td?.querySelector('.ut-cell-wrapper');
        const input = td?.querySelector('.ut-input');
        
        if (wrapper) {
            const isChanged = rowChanges.hasOwnProperty(colId);
            const isInvalid = this.invalidCells.has(`${rowId}:${colId}`);
            if (input) {
                input.classList.toggle('ut-input-changed', isChanged);
                input.classList.toggle('ut-input-invalid', isInvalid);
            }
            this.renderIndicators(wrapper, rowId, colId, isChanged, isInvalid);
        }
    }

    updateControls() {
        const saveBtn = this.container.querySelector('.ut-save-btn');
        const hasChanges = this.localChanges.size > 0;
        const hasErrors = this.invalidCells.size > 0;

        if (hasChanges && this.data?.updateRequest) {
            saveBtn.style.display = 'inline-block';
            saveBtn.disabled = hasErrors;
            saveBtn.textContent = hasErrors ? 'Fix Errors' : `Save Changes (${this.localChanges.size} rows)`;
            saveBtn.style.opacity = hasErrors ? '0.5' : '1';
        } else {
            saveBtn.style.display = 'none';
        }
    }

    showSaveDiffModal() {
        if (this.invalidCells.size > 0) return;

        const modal = document.createElement('div');
        modal.className = 'ut-modal-overlay';
        
        let diffHtml = '<table class="ut-diff-table"><tr><th>Product</th><th>Field</th><th>Old</th><th>New</th></tr>';
        
        for (const [id, changes] of this.localChanges) {
            const row = this.rows.get(id);
            // Fallback chain for row name
            const name = row.name || (row.photo && row.photo.name) || (row.productInfo && row.productInfo.name) || `Record #${id}`;
            for (const [colId, newVal] of Object.entries(changes)) {
                const label = this.fieldDefinitions[colId]?.label || this.toLabelCase(colId);
                diffHtml += `
                    <tr>
                        <td>${name}</td>
                        <td>${label}</td>
                        <td class="ut-diff-old">${this.formatValue(row[colId], colId)}</td>
                        <td class="ut-diff-new">${this.formatValue(newVal, colId)}</td>
                    </tr>
                `;
            }
        }
        diffHtml += '</table>';

        modal.innerHTML = `
            <div class="ut-modal-content">
                <h2 class="ut-header">Confirm Changes</h2>
                <p>Please review the changes before saving to the database.</p>
                <div style="max-height: 400px; overflow-y: auto;">
                    ${diffHtml}
                </div>
                <div class="ut-modal-actions">
                    <button class="btn-nc-outline ut-modal-cancel">Cancel</button>
                    <button class="btn-nc-primary ut-modal-save">Confirm and Save</button>
                </div>
            </div>
        `;

        document.body.appendChild(modal);

        modal.querySelector('.ut-modal-cancel').onclick = () => modal.remove();
        modal.querySelector('.ut-modal-save').onclick = () => {
            modal.remove();
            this.saveChanges();
        };
    }

    async saveChanges() {
        if (!this.data || !this.data.updateRequest || this.invalidCells.size > 0) return;
        const changesToSave = {};
        for (const [id, changes] of this.localChanges) {
            changesToSave[id] = changes;
        }

        try {
            const res = await fetch(this.data.updateRequest, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ changes: changesToSave })
            });
            if (!res.ok) throw new Error("Save failed");
            
            // Notification logic here (Phase 6b: Toast)
            if (typeof window.showToast === 'function') {
                window.showToast("Changes saved successfully!", "success");
            }

            if (this.onSave) this.onSave();
            this.clearLocalStorage();
            await this.fetchData();
        } catch (e) {
            if (typeof window.showToast === 'function') {
                window.showToast("Failed to save changes.", "error");
            } else {
                alert("Failed to save changes.");
            }
        }
    }

    highlight(text) {
        if (!this.searchQuery) return text;
        const escaped = this.searchQuery.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
        const regex = new RegExp(`(${escaped})`, 'gi');
        return text.toString().replace(regex, '<mark class="ut-highlight">$1</mark>');
    }

    toLabelCase(str) {
        if (!str) return '';
        const result = str.replace(/([A-Z])/g, " $1");
        return result.charAt(0).toUpperCase() + result.slice(1);
    }

    formatValue(val, colId = null, useHighlight = false) {
        if (val === null || val === undefined) return '';
        let str = '';
        if (typeof val === 'object') str = val.name || val.label || val.value || JSON.stringify(val);
        else {
            // Check if it's a select column and find the label
            const col = colId ? this.data.columns.find(c => c.id === colId) : null;
            if (col && col.type === 'select' && col.options) {
                const option = col.options.find(o => String(o.value) === String(val));
                str = option ? option.label : String(val);
            } else {
                str = String(val);
            }
        }
        
        return (useHighlight && this.searchQuery) ? this.highlight(str) : str;
    }
}

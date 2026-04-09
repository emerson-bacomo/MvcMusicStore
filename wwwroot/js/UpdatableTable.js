export default class UpdatableTable {
    constructor(container, config) {
        this.container = typeof container === "string" ? document.querySelector(container) : container;
        this.visibleFields = new Set();
        this.stickyFields = new Map(); // colId -> 'left' | 'right' | null
        this.request = config.request || {};
        this.fieldDefinitions = config.fieldDefinitions || {};
        this.onSave = config.onSave;
        this.label = config.label || "";
        this.customFilters = config.customFilters || [];
        this.onFilter = config.onFilter;

        this.data = config.initialData || null;
        this.rows = new Map();
        this.localChanges = new Map();
        this.invalidCells = new Map();
        this.isSaving = false;

        // Granular storage prefix (e.g., 'product')
        this.storagePrefix =
            config.storagePrefix ||
            (this.label || "").toLowerCase().replace(/\s+/g, "-").replace(/ies$/, "y").replace(/s$/, "") ||
            "item";

        this.searchQuery = "";
        this.defaultSortConfig = config.sortConfig || { key: "id", direction: "desc" };
        this.sortConfig = { ...this.defaultSortConfig };
        this.currentPage = 1;
        this.pageSize = config.pageSize || 25;
        this.statusFilters = {
            active: true,
            deleted: false,
        };
        this.filters = {
            visibleColumns: new Set(),
        };

        // If initial data is provided, populate rows immediately
        if (this.data) {
            // Deep copy to ensure original data stays original
            this.rows = new Map(Object.entries(JSON.parse(JSON.stringify(this.data.rows || {}))));
            if (this.filters.visibleColumns.size === 0) {
                this.data.columns.forEach((col) => {
                    if (!col.hidden) this.filters.visibleColumns.add(col.id);
                });
            }
        }

        this.handleCellChange = this.handleCellChange.bind(this);
        this.saveChanges = this.saveChanges.bind(this);
        this.handleSearch = this.handleSearch.bind(this);
        this.toggleFilterMenu = this.toggleFilterMenu.bind(this);
        this.handleSort = this.handleSort.bind(this);
        this.updateMarkerPositions = this.updateMarkerPositions.bind(this);
        this.markers = new Map();

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

        // Register globally for access from string-based event handlers (e.g., onclick)
        const containerId = this.container.id || this.container.getAttribute("id");
        if (containerId) {
            window.updatableTables = window.updatableTables || {};
            window.updatableTables[containerId] = this;
        }

        window.addEventListener("resize", () => {
            this.calculateStickyOffsets();
            this.updateMarkerPositions();
        });
        window.addEventListener("scroll", this.updateMarkerPositions, { passive: true });
    }

    saveToLocalStorage() {
        if (this.isSaving || !this.storagePrefix) return;
        // 1. Save Row Changes
        for (const [id, changes] of this.localChanges) {
            localStorage.setItem(`${this.storagePrefix}-row-${id}`, JSON.stringify(changes));
        }

        // 2. Save UI Preferences
        const uiPrefs = {
            ...this.filters,
            visibleColumns: Array.from(this.filters.visibleColumns),
            stickyFields: Array.from(this.stickyFields.entries()),
            statusFilters: this.statusFilters,
            sortConfig: this.sortConfig,
        };
        localStorage.setItem(`${this.storagePrefix}-ui-prefs`, JSON.stringify(uiPrefs));
        sessionStorage.setItem(`${this.storagePrefix}-ui-prefs`, JSON.stringify(uiPrefs));
    }

    loadFromLocalStorage() {
        try {
            const prefix = `${this.storagePrefix}-`;

            // 1. Load UI Preferences (from SessionStorage first, then fall back to LocalStorage)
            const savedPrefs =
                sessionStorage.getItem(`${this.storagePrefix}-ui-prefs`) ||
                localStorage.getItem(`${this.storagePrefix}-ui-prefs`);
            if (savedPrefs) {
                const state = JSON.parse(savedPrefs);
                if (state.visibleColumns) this.filters.visibleColumns = new Set(state.visibleColumns);
                if (state.stickyFields) this.stickyFields = new Map(state.stickyFields);
                if (state.statusFilters) this.statusFilters = state.statusFilters;
                if (state.sortConfig) this.sortConfig = state.sortConfig;

                // Load other filter values
                Object.keys(state).forEach((key) => {
                    if (!["visibleColumns", "stickyFields", "statusFilters", "sortConfig"].includes(key)) {
                        this.filters[key] = state[key];
                    }
                });
            }

            // Default Actions to sticky right if not set
            if (!this.stickyFields.has("actions")) {
                this.stickyFields.set("actions", "right");
            }

            // 2. Load Row Changes
            const rowPrefix = `${this.storagePrefix}-row-`;
            for (let i = 0; i < localStorage.length; i++) {
                const key = localStorage.key(i);
                if (key && key.startsWith(rowPrefix)) {
                    const id = key.substring(rowPrefix.length);
                    const saved = localStorage.getItem(key);
                    if (saved) {
                        const rowChanges = JSON.parse(saved);
                        this.localChanges.set(id, rowChanges);
                        for (const colId of Object.keys(rowChanges)) {
                            this.validateCell(id, colId, rowChanges[colId], false);
                        }
                    }
                }
            }

            // 3. Cleanup Legacy Data
            this.cleanupLegacyData();
        } catch (e) {
            console.warn("UpdatableTable: Failed to load from localStorage", e);
        }
    }

    cleanupLegacyData() {
        if (!this.data) return;
        const validColIds = new Set(this.data.columns.map((c) => c.id));
        const customFilterIds = new Set((this.customFilters || []).map((cf) => cf.id));

        // Cleanup Filter Dropdowns (preserve custom filter ids)
        Object.keys(this.filters).forEach((k) => {
            if (k !== "visibleColumns" && !validColIds.has(k) && !customFilterIds.has(k)) {
                delete this.filters[k];
            }
        });

        // Cleanup Row Changes
        for (const [id, changes] of this.localChanges) {
            let changed = false;
            Object.keys(changes).forEach((colId) => {
                if (!validColIds.has(colId)) {
                    delete changes[colId];
                    changed = true;
                }
            });
            if (changed) {
                if (Object.keys(changes).length === 0) {
                    this.localChanges.delete(id);
                    localStorage.removeItem(`${this.storagePrefix}-${id}`);
                } else {
                    localStorage.setItem(`${this.storagePrefix}-${id}`, JSON.stringify(changes));
                }
            }
        }
    }

    clearLocalStorage() {
        if (!this.storagePrefix) return;

        // Remove all row-specific entries for this prefix
        const rowPrefix = `${this.storagePrefix}-row-`;
        const keysToRemove = [];
        for (let i = 0; i < localStorage.length; i++) {
            const key = localStorage.key(i);
            if (key && key.startsWith(rowPrefix)) {
                keysToRemove.push(key);
            }
        }
        keysToRemove.forEach((k) => localStorage.removeItem(k));

        this.localChanges.clear();
        this.invalidCells.clear();
        this.updateControls();
    }

    parseUrlFilters() {
        const params = new URLSearchParams(window.location.search);
        params.forEach((value, key) => {
            if (key === "status") {
                if (value === "active") {
                    this.statusFilters.active = true;
                    this.statusFilters.deleted = false;
                } else if (value === "deleted") {
                    this.statusFilters.active = false;
                    this.statusFilters.deleted = true;
                } else if (value === "all") {
                    this.statusFilters.active = true;
                    this.statusFilters.deleted = true;
                }
            } else if (key === "q") {
                this.searchQuery = value.toLowerCase();
                const searchInput = this.container.querySelector(".ut-search-input");
                if (searchInput) searchInput.value = value;
            } else if (key === "sort") {
                this.sortConfig.key = value;
            } else if (key === "dir") {
                this.sortConfig.direction = value;
            } else if (key === "page") {
                this.currentPage = parseInt(value, 10) || 1;
            } else {
                this.filters[key] = value;
            }
        });

        // URL is authoritative for custom filters and column-based select filters.
        // If a key is NOT in the URL, clear it — prevents stale sessionStorage values from
        // persisting when navigating to a page without that param (e.g. "View All Logs").
        (this.customFilters || []).forEach((cf) => {
            if (!params.has(cf.id)) delete this.filters[cf.id];
        });
        if (this.data) {
            this.data.columns.forEach((col) => {
                const fDef = this.fieldDefinitions[col.id] || {};
                const colType = col.type || fDef.type;
                if (colType === "select" && !params.has(col.id)) delete this.filters[col.id];
            });
        }
    }

    renderContainer() {
        this.container.innerHTML = `
            <div class="ut-header-row">
                ${this.label ? `<h1 class="ut-header">${this.label}</h1>` : "<div></div>"}
                    <div class="ut-top-controls" style="display: flex; align-items: center; gap: 0.75rem;">
                        <div class="ut-search-wrapper">
                            <i class="fa fa-search ut-search-icon"></i>
                            <input type="text" class="ut-search-input" placeholder="Search ${this.label.toLowerCase() || "items"}...">
                        </div>
                        <div class="ut-filter-container">
                        <button class="ut-filter-btn" title="Filters">
                            <i class="fa fa-filter"></i>
                        </button>
                        <div class="ut-filter-popup">
                            <div class="ut-dropdown-filter-list"></div>
                            <div class="ut-filter-separator ut-dropdown-separator" style="display:none;"></div>
                            <div class="ut-filter-section">
                                <h4>Columns Visibility and Pin</h4>
                                <div class="ut-column-toggles"></div>
                            </div>
                        </div>
                    </div>
                    <div class="ut-action-container" style="position: relative; display: none;">
                        <button class="ut-clear-btn" style="display:none;">
                            <i class="fa fa-undo me-2"></i> Clear Changes
                        </button>
                        <div class="ut-clear-popup ut-diff-modal" style="display:none;"></div>
                    </div>
                    <div class="ut-action-container" style="position: relative; display: none;">
                        <button class="ut-save-btn" style="display:none;">
                            <i class="fa fa-save me-2"></i> Save Changes
                        </button>
                        <div class="ut-save-popup ut-diff-modal" style="display:none;"></div>
                    </div>
                </div>
            </div>
            <div class="ut-wrapper">
                <div class="ut-table-container">
                    <table class="ut-table">
                        <thead></thead>
                        <tbody></tbody>
                    </table>
                </div>
            </div>
        `;

        this.container.querySelector(".ut-search-input").addEventListener("input", this.handleSearch);
        this.container.querySelector(".ut-filter-btn").addEventListener("click", (e) => {
            e.stopPropagation();
            this.toggleFilterMenu();
        });
    }

    initEventListeners() {
        this.container.addEventListener("click", (e) => {
            if (e.target.closest(".ut-save-btn")) {
                this.showSaveDiffModal();
            }
            if (e.target.closest(".ut-clear-btn")) {
                this.showClearDiffModal();
            }
        });

        document.addEventListener("click", (e) => {
            const filterPopup = this.container.querySelector(".ut-filter-popup");
            const filterBtn = this.container.querySelector(".ut-filter-btn");
            const overlay = document.getElementById("ncGlobalOverlay");

            if (overlay && e.target === overlay) {
                if (filterPopup) filterPopup.style.display = "none";
                this.container.querySelectorAll(".ut-diff-modal").forEach((m) => (m.style.display = "none"));
                overlay.classList.remove("show");
                this.container
                    .querySelectorAll(".ut-popup-active-trigger")
                    .forEach((b) => b.classList.remove("ut-popup-active-trigger"));
                return;
            }

            let anyPopupOpen = false;

            if (
                filterPopup &&
                filterPopup.style.display === "block" &&
                !filterPopup.contains(e.target) &&
                !filterBtn.contains(e.target)
            ) {
                filterPopup.style.display = "none";
                if (filterBtn) filterBtn.classList.remove("ut-popup-active-trigger");
            } else if (filterPopup && filterPopup.style.display === "block") {
                anyPopupOpen = true;
            }

            // Diff modals click outside
            const diffModals = this.container.querySelectorAll(".ut-diff-modal");
            diffModals.forEach((modal) => {
                const btn = modal.parentElement.querySelector("button");
                if (modal.style.display === "block" && !modal.contains(e.target) && (!btn || !btn.contains(e.target))) {
                    modal.style.display = "none";
                    if (btn) btn.classList.remove("ut-popup-active-trigger");
                } else if (modal.style.display === "block") {
                    anyPopupOpen = true;
                }
            });

            if (document.querySelector(".ut-side-popup")) {
                anyPopupOpen = true;
            }

            if (!anyPopupOpen && overlay) {
                overlay.classList.remove("show");
                this.container.classList.remove("ut-has-popup");
                this.container
                    .querySelectorAll(".ut-popup-active-trigger")
                    .forEach((b) => b.classList.remove("ut-popup-active-trigger"));
            }

            // Revert popup click outside
            const revertPopups = this.container.querySelectorAll(".ut-revert-popup");
            revertPopups.forEach((popup) => {
                if (!popup.contains(e.target)) popup.remove();
            });
        });
    }

    handleSearch(e) {
        this.searchQuery = (e.target.value || "").toLowerCase();
        this.updateUrl();
        this.currentPage = 1;
        this.renderTable();
    }

    toggleFilterMenu() {
        // Close other popups
        this.container.querySelectorAll(".ut-diff-modal").forEach((m) => (m.style.display = "none"));
        this.container.querySelectorAll(".ut-popup-active-trigger").forEach((b) => b.classList.remove("ut-popup-active-trigger"));

        const popup = this.container.querySelector(".ut-filter-popup");
        const overlay = document.getElementById("ncGlobalOverlay");
        const btn = this.container.querySelector(".ut-filter-btn");

        popup.style.display = popup.style.display === "block" ? "none" : "block";
        if (popup.style.display === "block") {
            if (overlay) overlay.classList.add("show");
            this.container.classList.add("ut-has-popup");
            if (btn) btn.classList.add("ut-popup-active-trigger");
            this.renderDropdownFilters();
            this.renderColumnToggles();
            this.updateFilterCounts();
        } else {
            if (overlay) overlay.classList.remove("show");
            this.container.classList.remove("ut-has-popup");
            if (btn) btn.classList.remove("ut-popup-active-trigger");
        }
    }

    renderDropdownFilters() {
        const container = this.container.querySelector(".ut-dropdown-filter-list");
        if (!container) return;

        container.innerHTML = "";
        let hasFilters = false;

        const renderSelectSection = (filterId, label, options, currentValue) => {
            const section = document.createElement("div");
            section.className = "ut-filter-item-dropdown";

            section.innerHTML = `
                <label style="display:block; font-size:0.75rem; color:var(--nc-text-muted); margin-bottom:0.25rem;">${label}</label>
                <select class="ut-input ut-select" style="padding:0.4rem 0.6rem; height:auto; background:rgba(255,255,255,0.05); border:1px solid var(--nc-border); width:100%; border-radius:4px;">
                    <option value="">All ${label}</option>
                    ${options
                        .map(
                            (opt) =>
                                `<option value="${opt.value}" ${String(currentValue) === String(opt.value) ? "selected" : ""}>${opt.label}</option>`,
                        )
                        .join("")}
                </select>
            `;

            section.querySelector("select").addEventListener("change", (e) => {
                if (e.target.value) this.filters[filterId] = e.target.value;
                else delete this.filters[filterId];
                this.saveToLocalStorage();
                this.updateUrl();
                this.currentPage = 1;
                this.renderTable();
                this.calculateStickyOffsets();
            });
            return section;
        };

        if (this.hasRecordStatus) {
            hasFilters = true;
            const statusSection = document.createElement("div");
            statusSection.className = "ut-filter-item-dropdown";
            statusSection.innerHTML = `
                <label style="display:block; font-size:0.75rem; color:var(--nc-text-muted); margin-bottom:0.25rem;">Record Status</label>
                <label class="ut-checkbox-label">
                    <input type="checkbox" id="ut-filter-active" ${this.statusFilters.active ? "checked" : ""}>
                    <span>Active</span>
                    <span class="ut-count-badge active-count">${this.activeCount || 0}</span>
                </label>
                <label class="ut-checkbox-label">
                    <input type="checkbox" id="ut-filter-deleted" ${this.statusFilters.deleted ? "checked" : ""}>
                    <span>Deleted</span>
                    <span class="ut-count-badge deleted-count">${this.deletedCount || 0}</span>
                </label>
            `;
            statusSection.querySelector("#ut-filter-active").addEventListener("change", (e) => {
                this.statusFilters.active = e.target.checked;
                this.saveToLocalStorage();
                this.updateUrl();
                this.currentPage = 1;
                this.renderTable();
            });
            statusSection.querySelector("#ut-filter-deleted").addEventListener("change", (e) => {
                this.statusFilters.deleted = e.target.checked;
                this.saveToLocalStorage();
                this.updateUrl();
                this.currentPage = 1;
                this.renderTable();
            });
            container.appendChild(statusSection);
        }

        // 1. Column-based select filters (server columns, with fieldDefinitions as fallback for type/options)
        if (this.data) {
            this.data.columns.forEach((col) => {
                const fDef = this.fieldDefinitions[col.id] || {};
                const colType = col.type || fDef.type;
                const colOptions = col.options || fDef.options;
                if (colType === "select" && colOptions?.length) {
                    hasFilters = true;
                    const label = fDef.label || col.label || this.toLabelCase(col.id);
                    // If the column ID doesn't already end in 'Id', use the 'Id' suffix for the filter key
                    // to match common backend parameter naming conventions (e.g. brand -> brandId)
                    const filterKey = col.id.toLowerCase().endsWith("id") ? col.id : col.id + "Id";
                    container.appendChild(renderSelectSection(filterKey, label, Array.from(colOptions), this.filters[filterKey]));
                }
            });
        }

        // 2. Custom filters defined in config (e.g. userId -> Full Name)
        (this.customFilters || []).forEach((cf) => {
            if (!cf.id || !cf.options?.length) return;
            hasFilters = true;
            const label = cf.label || this.toLabelCase(cf.id);
            container.appendChild(renderSelectSection(cf.id, label, cf.options, this.filters[cf.id]));
        });

        if (!hasFilters) {
            container.style.display = "none";
            const dropSep = this.container.querySelector(".ut-dropdown-separator");
            if (dropSep) dropSep.style.display = "none";
        } else {
            container.style.display = "flex";
            container.style.flexDirection = "column";
            container.style.gap = "0.75rem";
            const dropSep = this.container.querySelector(".ut-dropdown-separator");
            if (dropSep) dropSep.style.display = "";
        }
    }

    updateUrl() {
        const url = new URL(window.location);
        const coreParams = ["sort", "dir", "page", "status", "q", "_"];

        // 1. Identify what SHOULD be in the URL
        const activeFilterKeys = Object.keys(this.filters).filter((k) => k !== "visibleColumns" && this.filters[k]);
        const customFilterKeys = (this.customFilters || []).map((cf) => cf.id);

        // 2. Clear out any parameters that are no longer active or relevant.
        // We delete anything that isn't a core param (sort, page, etc) and isn't currently an active filter.
        Array.from(url.searchParams.keys()).forEach((key) => {
            if (!coreParams.includes(key) && !activeFilterKeys.includes(key)) {
                url.searchParams.delete(key);
            }
        });

        // 3. Sync current filters to URL
        activeFilterKeys.forEach((key) => {
            url.searchParams.set(key, this.filters[key]);
        });

        if (this.searchQuery) {
            url.searchParams.set("q", this.searchQuery);
        } else {
            url.searchParams.delete("q");
        }

        if (this.statusFilters.active && this.statusFilters.deleted) {
            url.searchParams.set("status", "all");
        } else if (this.statusFilters.active) {
            url.searchParams.set("status", "active");
        } else if (this.statusFilters.deleted) {
            url.searchParams.set("status", "deleted");
        } else {
            url.searchParams.delete("status");
        }

        if (this.sortConfig.key && this.sortConfig.direction !== "none") {
            url.searchParams.set("sort", this.sortConfig.key);
            url.searchParams.set("dir", this.sortConfig.direction);
        } else {
            url.searchParams.delete("sort");
            url.searchParams.delete("dir");
        }

        if (this.currentPage > 1) {
            url.searchParams.set("page", this.currentPage);
        } else {
            url.searchParams.delete("page");
        }

        window.history.replaceState({}, "", url);
        this.saveToLocalStorage();
        if (this.onFilter) this.onFilter(this.filters, this.searchQuery, this.statusFilters);
    }

    renderColumnToggles() {
        const container = this.container.querySelector(".ut-column-toggles");
        if (!this.data) return;

        container.innerHTML = "";
        this.data.columns.forEach((col) => {
            if (col.id === "recordStatus" || col.id === "id" || (col.hidden && !this.filters.visibleColumns.has(col.id))) return;

            const isSticky = this.stickyFields.has(col.id);
            const stickySide = this.stickyFields.get(col.id);
            const pinIconClass = isSticky ? (stickySide === "left" ? "fa-rotate-90" : "fa-rotate-270") : "";
            const pinColor = isSticky ? "var(--nc-primary)" : "inherit";

            const colItem = document.createElement("div");
            colItem.className = "ut-column-toggle-item";
            colItem.style.display = "flex";
            colItem.style.alignItems = "center";
            colItem.style.justifyContent = "space-between";
            colItem.style.marginBottom = "0.25rem"; // Reduced from 0.5rem

            colItem.innerHTML = `
                <label class="ut-checkbox-label" style="flex: 1; margin-bottom: 0;">
                    <input type="checkbox" ${this.filters.visibleColumns.has(col.id) ? "checked" : ""} data-col-id="${col.id}">
                    <span>${col.label || this.fieldDefinitions[col.id]?.label || this.toLabelCase(col.id)}</span>
                </label>
                <button class="ut-pin-btn" data-col-id="${col.id}" title="Pin Column" 
                        style="background: none; border: none; cursor: pointer; padding: 4px; color: ${pinColor}; transition: all 0.2s;">
                    <i class="fa fa-thumbtack ${pinIconClass}"></i>
                </button>
            `;

            colItem.querySelector('input[type="checkbox"]').addEventListener("change", (e) => {
                if (e.target.checked) this.filters.visibleColumns.add(col.id);
                else this.filters.visibleColumns.delete(col.id);
                this.saveToLocalStorage();
                this.currentPage = 1;
                this.renderTable();
                this.calculateStickyOffsets();
                this.renderColumnToggles();
            });

            colItem.querySelector(".ut-pin-btn").addEventListener("click", (e) => {
                e.stopPropagation(); // BUGFIX: Prevent closing the filter popup
                const current = this.stickyFields.get(col.id);
                if (!current) this.stickyFields.set(col.id, "left");
                else if (current === "left") this.stickyFields.set(col.id, "right");
                else this.stickyFields.delete(col.id);

                this.saveToLocalStorage();
                this.renderTable();
                this.calculateStickyOffsets();
                this.renderColumnToggles();
            });

            container.appendChild(colItem);
        });
    }

    handleSort(colId) {
        if (this.sortConfig.key === colId) {
            if (this.sortConfig.direction === "asc") this.sortConfig.direction = "desc";
            else if (this.sortConfig.direction === "desc") {
                this.sortConfig = { ...this.defaultSortConfig, direction: "none" };
            }
        } else {
            this.sortConfig.key = colId;
            this.sortConfig.direction = "asc";
        }
        this.updateUrl();
        this.renderTable();
    }

    resetFilters() {
        this.searchQuery = "";
        const visibleCols = this.filters.visibleColumns;
        this.filters = { visibleColumns: visibleCols };
        this.statusFilters = { active: true, deleted: false };

        const searchInput = this.container.querySelector(".ut-search-input");
        if (searchInput) searchInput.value = "";

        this.saveToLocalStorage();
        this.updateUrl();
        this.currentPage = 1;

        // If the table was filtered by the backend (URL params), we must re-fetch the full dataset.
        // fetchData() internally calls renderTable() and updateControls().
        this.fetchData();
    }

    async fetchData() {
        try {
            // Collect IDs with local changes to ensure they are fetched even if filtered out
            const localChangeIds = Array.from(this.localChanges.keys());
            const urlAttr = this.request.url || "";
            const baseUrl = urlAttr.split("?")[0];

            // Construct fetch URL using CURRENT window location search (which has been synced by updateUrl)
            // plus internal-only params like includeIds and a cache buster.
            const params = new URLSearchParams(window.location.search);
            if (localChangeIds.length > 0) params.set("includeIds", localChangeIds.join(","));
            params.set("_", Date.now());

            const finalUrl = `${baseUrl}?${params.toString()}`;

            let response;
            if (this.request.fetchFn) {
                const fetchParams = Object.fromEntries(params.entries());
                response = await this.request.fetchFn({ ...this.request, ...fetchParams, includeIds: localChangeIds });
            } else if (urlAttr) {
                const res = await fetch(finalUrl, {
                    method: this.request.type || "GET",
                    headers: { "Content-Type": "application/json" },
                });
                response = await res.json();
            } else {
                response = { columns: [], rows: {} };
            }

            this.data = response;
            const fetchedRows = response.rows || {};
            const fetchedRowIds = new Set(Object.values(fetchedRows).map((r) => String(r.id)));
            // Universal row Map population
            const rowsArray = Array.isArray(fetchedRows) ? fetchedRows : Object.values(fetchedRows);
            this.rows = new Map(rowsArray.map((r) => [String(r.id), r]));

            // Sync: Remove local changes for records that no longer exist in the backend
            if (localChangeIds.length > 0) {
                for (const id of localChangeIds) {
                    if (!fetchedRowIds.has(id)) {
                        this.localChanges.delete(id);
                        localStorage.removeItem(`${this.storagePrefix}-row-${id}`);
                    }
                }
            }

            if (this.filters.visibleColumns.size === 0) {
                this.data.columns.forEach((col) => {
                    if (!col.hidden) this.filters.visibleColumns.add(col.id);
                });
            }

            this.loadFromLocalStorage(); // Load after data is fetched and rows Map is populated
            this.parseUrlFilters(); // If URL has filters, they should override localStorage

            this.updateUrl(); // Sync URL with our final filters
            this.renderTable(); // Re-render to show indicators and apply filters
            this.updateControls();
        } catch (error) {
            console.error("UpdatableTable: Error fetching data", error);
        }
    }

    getProcessedRows() {
        const allRowsArray = Array.from(this.rows.entries()).map(([id, data]) => {
            const changes = this.localChanges.get(id) || {};
            return { id, ...data, ...changes };
        });

        // 1. Exclude "Extra" rows immediately (they are only for diffing/sync)
        let processed = allRowsArray.filter((r) => !r._isExtra);

        if (processed.length > 0) {
            this.hasRecordStatus = !!processed[0].recordStatus;
            if (this.hasRecordStatus) {
                // Update counts (before status filters) based on non-extra rows
                this.activeCount = processed.filter((r) => r.recordStatus !== "Deleted").length;
                this.deletedCount = processed.filter((r) => r.recordStatus === "Deleted").length;

                // 2. Status Filter
                processed = processed.filter((r) => {
                    const isDeleted = r.recordStatus === "Deleted";
                    return isDeleted ? this.statusFilters.deleted : this.statusFilters.active;
                });
            }
        }

        // 3. Search Filter (applies to merged local values)
        if (this.searchQuery) {
            processed = processed.filter((row) => {
                // Search against all properties, using formatValue where applicable
                return Object.entries(row).some(([key, val]) => {
                    if (val == null) return false;

                    const col = this.data.columns.find((c) => c.id === key);
                    const displayStr = col ? this.formatValue(val, col.id) : String(val);
                    const displayStrLower = displayStr.toLowerCase();

                    return displayStrLower.includes(this.searchQuery);
                });
            });
        }

        // 4. URL / Column Filters
        Object.entries(this.filters).forEach(([key, filterVal]) => {
            if (key === "visibleColumns") return;
            if (filterVal) {
                processed = processed.filter((row) => {
                    // Try to find the cell value:
                    // 1. Precise match: row['brandId']
                    // 2. ID Mapping: row['brand'] if key is 'brandId'
                    let cellVal = row[key];
                    if (cellVal === undefined && key.toLowerCase().endsWith("id")) {
                        const baseKey = key.substring(0, key.length - 2);
                        cellVal = row[baseKey];
                    }

                    if (cellVal === undefined) return true; // Col might not exist in row
                    // Handle objects (like {id, name}) or primitives
                    if (cellVal && typeof cellVal === "object") {
                        return String(cellVal.id || cellVal.value || "") === String(filterVal);
                    }
                    return String(cellVal) === String(filterVal);
                });
            }
        });

        let sortKey = this.sortConfig.key;
        let sortDir = this.sortConfig.direction;

        if (!sortKey || sortDir === "none") {
            sortKey = this.defaultSortConfig?.key || "id";
            sortDir = this.defaultSortConfig?.direction || "desc";
        }

        if (sortKey && sortDir !== "none") {
            const key = sortKey;
            const dir = sortDir === "asc" ? 1 : -1;
            processed.sort((a, b) => {
                let v1 = a[key + "Label"] !== undefined ? a[key + "Label"] : a[key];
                let v2 = b[key + "Label"] !== undefined ? b[key + "Label"] : b[key];
                if (v1 && typeof v1 === "object") v1 = v1.name || v1.label || "";
                if (v2 && typeof v2 === "object") v2 = v2.name || v2.label || "";
                if (v1 < v2) return -1 * dir;
                if (v1 > v2) return 1 * dir;
                return 0;
            });
        }

        return processed;
    }

    renderTable() {
        if (!this.data) {
            return;
        }

        this.clearMarkers();

        const thead = this.container.querySelector("thead");
        const tbody = this.container.querySelector("tbody");

        let theadHtml = "<tr>";
        this.data.columns.forEach((col) => {
            if (col.id === "recordStatus" || (col.hidden && !this.filters.visibleColumns.has(col.id))) return;
            if (!this.filters.visibleColumns.has(col.id) && col.id !== "actions") return;

            const label = this.fieldDefinitions[col.id]?.label || this.toLabelCase(col.id);
            const isSorting = this.sortConfig.key === col.id && this.sortConfig.direction !== "none";
            let sortIcon = '<i class="fa fa-sort ut-sort-ghost"></i>';
            if (isSorting) {
                sortIcon =
                    this.sortConfig.direction === "asc" ? '<i class="fa fa-sort-up"></i>' : '<i class="fa fa-sort-down"></i>';
            }

            const isColUpdatable = this.fieldDefinitions[col.id]?.hasOwnProperty("updatable")
                ? this.fieldDefinitions[col.id].updatable
                : col.updatable;

            const isUpdatableHeader = col.id !== "actions" && isColUpdatable;
            const penIcon = isUpdatableHeader
                ? '<i class="fa fa-pen nc-text-primary me-2" style="font-size: 0.7rem; opacity: 0.7;"></i>'
                : "";

            let stickyClass = "";
            if (this.stickyFields.has(col.id)) {
                stickyClass = `ut-sticky-${this.stickyFields.get(col.id)}`;
            }
            const fDef = this.fieldDefinitions[col.id] || {};
            const cellClass = fDef.cellClass || "";
            const widthMinimum = fDef.widthMinimum || "";
            const widthMaximum = fDef.widthMaximum || "";
            const widthPercentage = fDef.widthPercentage || "";
            const hasFitClass = cellClass.includes("ut-min-w-fit");

            let thWidthStyle = "auto";
            if (col.id === "actions" || hasFitClass) {
                thWidthStyle = "1%";
            } else if (widthPercentage) {
                thWidthStyle = widthPercentage;
            }

            let thMinWidthStyle = widthMinimum || "auto";
            let thMaxWidthStyle = widthMaximum ? `max-width: ${widthMaximum};` : "";

            theadHtml += `
                <th data-col-id="${col.id}" class="ut-th ${col.id !== "actions" ? "ut-sortable" : ""} ${stickyClass} ${cellClass}" 
                    style="width: ${thWidthStyle}; min-width: ${thMinWidthStyle}; ${thMaxWidthStyle} ${col.id === "actions" ? "text-align: left; white-space: nowrap;" : ""}">
                    <div class="ut-cell-content">
                        ${penIcon}
                        <span>${label}</span>
                        ${col.id !== "actions" ? `<span class="ut-sort-icon" style="margin-left: 6px; font-size: 0.85em;">${sortIcon}</span>` : ""}
                    </div>
                </th>
            `;
        });
        theadHtml += "</tr>";
        thead.innerHTML = theadHtml;

        thead.querySelectorAll(".ut-sortable").forEach((th) => {
            th.addEventListener("click", () => this.handleSort(th.dataset.colId));
        });

        tbody.innerHTML = "";
        const processedRows = this.getProcessedRows();
        if (processedRows.length === 0) {
            const urlParams = new URLSearchParams(window.location.search);
            // Check for any parameter that isn't purely about pagination or sorting
            const coreParams = ["sort", "dir", "page", "_"]; // _ is for cache busting
            const hasUrlFilters = Array.from(urlParams.keys()).some((k) => !coreParams.includes(k));

            const hasActiveFilters =
                this.searchQuery.trim() !== "" ||
                Object.keys(this.filters).some((k) => k !== "visibleColumns" && this.filters[k]) ||
                !this.statusFilters.active ||
                this.statusFilters.deleted ||
                hasUrlFilters;

            const containerId = this.container.id || this.container.getAttribute("id");

            if (!hasActiveFilters) {
                const itemType = (this.label || this.storagePrefix || "table").toLowerCase();
                tbody.innerHTML = `
                    <tr>
                        <td colspan="100%" class="ut-empty-state-cell">
                            <div class="ut-empty-state">
                                <i class="fa fa-folder-open mb-3"></i>
                                <h3>No data available</h3>
                                <p>There are currently no items to show in the ${itemType} table.</p>
                            </div>
                        </td>
                    </tr>
                `;
            } else {
                tbody.innerHTML = `
                    <tr>
                        <td colspan="100%" class="ut-empty-state-cell">
                            <div class="ut-empty-state">
                                <i class="fa fa-search-minus mb-3"></i>
                                <h3>No results found</h3>
                                <p>We couldn't find any results matching your filters or search query.</p>
                                <button type="button" class="btn-nc-primary mt-3 d-inline-flex align-items-center" style="width: auto; padding: 1rem 2rem; font-size: 0.9rem;" onclick="window.updatableTables['${containerId}'].resetFilters()">
                                    <i class="fa fa-undo me-2"></i> Reset All Filters
                                </button>
                            </div>
                        </td>
                    </tr>
                `;
            }
            this.renderPagination(0);
            return;
        }

        const startIndex = (this.currentPage - 1) * this.pageSize;
        const endIndex = startIndex + this.pageSize;
        const paginatedRows = processedRows.slice(startIndex, endIndex);

        paginatedRows.forEach((rowData) => {
            const rowId = String(rowData.id);
            const tr = document.createElement("tr");
            tr.dataset.rowId = rowId;
            const isDeleted = rowData.recordStatus === "Deleted";
            const rowClasses = [];
            if (isDeleted) rowClasses.push("ut-deleted-row");
            if (rowData._isNew) rowClasses.push("ut-row-new");
            tr.className = rowClasses.join(" ");

            if (isDeleted) tr.dataset.deleted = "true";

            this.data.columns.forEach((col) => {
                if (col.id === "recordStatus" || (col.hidden && !this.filters.visibleColumns.has(col.id))) return;
                if (!this.filters.visibleColumns.has(col.id) && col.id !== "actions") return;

                const td = document.createElement("td");
                td.dataset.colId = col.id;

                const stickyClass = this.stickyFields.has(col.id) ? `ut-sticky-${this.stickyFields.get(col.id)}` : "";
                const fDef = this.fieldDefinitions[col.id] || {};
                const cellClass = fDef.cellClass || "";
                const cellStyle = fDef.cellStyle || "";
                const widthMinimum = fDef.widthMinimum || "";
                const widthMaximum = fDef.widthMaximum || "";
                const hasFitClass = cellClass.includes("ut-min-w-fit");

                if (stickyClass) td.classList.add(stickyClass);
                if (cellClass) {
                    cellClass
                        .split(" ")
                        .filter((c) => c.trim())
                        .forEach((cls) => td.classList.add(cls));
                }
                if (cellStyle) td.style.cssText += cellStyle;

                if (widthMinimum) td.style.minWidth = widthMinimum;
                if (widthMaximum) td.style.maxWidth = widthMaximum;
                if (hasFitClass || col.id === "actions") td.style.width = "1%";

                const isColUpdatable = this.fieldDefinitions[col.id]?.hasOwnProperty("updatable")
                    ? this.fieldDefinitions[col.id].updatable
                    : col.updatable;

                if (isColUpdatable && col.id !== "actions") {
                    td.classList.add("ut-updatable-cell");
                }

                const isUpdatable = isColUpdatable && this.data.updateRequest && rowData.recordStatus !== "Deleted";

                // Full-height inner div — this is the border target for pinned columns
                const cellContent = document.createElement("div");
                cellContent.className = "ut-cell-content";
                // Mark non-action cells in deleted rows so CSS can reduce their opacity
                if (isDeleted && col.id !== "actions") cellContent.classList.add("ut-deleted-content");
                td.appendChild(cellContent);

                if (fDef.currency) {
                    td.classList.add("ut-has-currency");
                }

                this.renderCellContent(cellContent, rowId, col, rowData, isUpdatable);

                const tooltip = fDef.renderTooltip ? fDef.renderTooltip(rowData[col.id], col, rowData) : "";
                if (tooltip) {
                    td.setAttribute("title", tooltip);
                }

                tr.appendChild(td);
            });
            tbody.appendChild(tr);

            if (rowData._isNew) {
                this.createMarker(rowId, tr);
            }
        });

        this.updateMarkerPositions();

        this.updateFilterCounts();
        this.renderPagination(processedRows.length);
    }

    renderPagination(totalRows) {
        let paginationContainer = this.container.querySelector(".ut-pagination");
        if (!paginationContainer) {
            paginationContainer = document.createElement("div");
            this.container.querySelector(".ut-wrapper").appendChild(paginationContainer);
        }

        const totalPages = Math.ceil(totalRows / this.pageSize) || 1;
        if (this.currentPage > totalPages) {
            this.currentPage = totalPages;
            this.renderTable();
            return;
        }

        if (totalPages <= 1) {
            paginationContainer.style.display = "none";
            return;
        }
        paginationContainer.style.display = "flex";
        paginationContainer.className = "ut-pagination d-flex justify-content-between align-items-center mt-4 px-3 pb-3";

        let startPage = Math.max(1, this.currentPage - 2);
        let endPage = Math.min(totalPages, this.currentPage + 2);

        let paginationHtml = `
            <li class="page-item ${this.currentPage === 1 ? "disabled" : ""}">
                <a class="page-link ut-page-prev" href="javascript:void(0)" tabindex="-1" style="background: var(--nc-bg-card); border-color: var(--nc-border); color: var(--nc-text-primary);">
                    <i class="fa fa-angle-left"></i>
                </a>
            </li>
        `;

        if (startPage > 1) {
            paginationHtml += `<li class="page-item"><a class="page-link ut-page-num" data-page="1" href="javascript:void(0)" style="background: var(--nc-bg-card); border-color: var(--nc-border); color: var(--nc-text-primary);">1</a></li>`;
            if (startPage > 2) {
                paginationHtml += `<li class="page-item disabled"><span class="page-link" style="background: var(--nc-bg-card); border-color: var(--nc-border); color: var(--nc-text-muted);">...</span></li>`;
            }
        }

        for (let i = startPage; i <= endPage; i++) {
            if (i === this.currentPage) {
                paginationHtml += `<li class="page-item active"><span class="page-link" style="background: var(--nc-primary); border-color: var(--nc-primary); color: #fff;">${i}</span></li>`;
            } else {
                paginationHtml += `<li class="page-item"><a class="page-link ut-page-num" data-page="${i}" href="javascript:void(0)" style="background: var(--nc-bg-card); border-color: var(--nc-border); color: var(--nc-text-primary);">${i}</a></li>`;
            }
        }

        if (endPage < totalPages) {
            if (endPage < totalPages - 1) {
                paginationHtml += `<li class="page-item disabled"><span class="page-link" style="background: var(--nc-bg-card); border-color: var(--nc-border); color: var(--nc-text-muted);">...</span></li>`;
            }
            paginationHtml += `<li class="page-item"><a class="page-link ut-page-num" data-page="${totalPages}" href="javascript:void(0)" style="background: var(--nc-bg-card); border-color: var(--nc-border); color: var(--nc-text-primary);">${totalPages}</a></li>`;
        }

        paginationHtml += `
            <li class="page-item ${this.currentPage === totalPages ? "disabled" : ""}">
                <a class="page-link ut-page-next" href="javascript:void(0)" tabindex="-1" style="background: var(--nc-bg-card); border-color: var(--nc-border); color: var(--nc-text-primary);">
                    <i class="fa fa-angle-right"></i>
                </a>
            </li>
        `;

        let pageInputHtml = "";
        if (totalPages > 5) {
            pageInputHtml = `
            <div class="ms-3 d-flex align-items-center">
                <span class="text-muted small me-2">Go to:</span>
                <input type="number" class="form-control form-control-sm ut-page-input" min="1" max="${totalPages}" placeholder="${this.currentPage}" style="width: 60px; background: var(--nc-bg-card); border-color: var(--nc-border); color: var(--nc-text-primary);">
            </div>
            `;
        }

        paginationContainer.innerHTML = `
            <div class="nc-text-muted" style="font-size: 0.85rem;">
                Showing page <strong>${this.currentPage}</strong> of <strong>${totalPages}</strong>
            </div>
            <div class="d-flex align-items-center">
                <nav aria-label="Table pagination">
                    <ul class="pagination pagination-sm mb-0">
                        ${paginationHtml}
                    </ul>
                </nav>
                ${pageInputHtml}
            </div>
        `;

        const prevBtn = paginationContainer.querySelector(".ut-page-prev");
        const nextBtn = paginationContainer.querySelector(".ut-page-next");
        const numBtns = paginationContainer.querySelectorAll(".ut-page-num");
        const pageInput = paginationContainer.querySelector(".ut-page-input");

        if (prevBtn) {
            prevBtn.addEventListener("click", () => {
                if (this.currentPage > 1) {
                    this.currentPage--;
                    this.updateUrl();
                    this.renderTable();
                }
            });
        }
        if (nextBtn) {
            nextBtn.addEventListener("click", () => {
                if (this.currentPage < totalPages) {
                    this.currentPage++;
                    this.updateUrl();
                    this.renderTable();
                }
            });
        }
        numBtns.forEach((btn) => {
            btn.addEventListener("click", (e) => {
                this.currentPage = parseInt(e.currentTarget.dataset.page, 10);
                this.updateUrl();
                this.renderTable();
            });
        });
        if (pageInput) {
            pageInput.addEventListener("keydown", (e) => {
                if (e.key === "Enter") {
                    let page = parseInt(pageInput.value, 10);
                    if (!isNaN(page) && page >= 1 && page <= totalPages) {
                        this.currentPage = page;
                        this.updateUrl();
                        this.renderTable();
                    }
                }
            });
        }
    }

    updateFilterCounts() {
        const counts = { active: this.activeCount || 0, deleted: this.deletedCount || 0 };
        this.container.querySelectorAll(".active-count").forEach((el) => (el.textContent = counts.active));
        this.container.querySelectorAll(".deleted-count").forEach((el) => (el.textContent = counts.deleted));
        // Apply sticky offsets
        this.calculateStickyOffsets();
    }

    calculateStickyOffsets() {
        // Clear old classes
        this.container.querySelectorAll(".ut-stuck-left, .ut-stuck-right").forEach((el) => {
            el.classList.remove("ut-stuck-left", "ut-stuck-right");
        });

        const leftSticky = Array.from(this.container.querySelectorAll(".ut-sticky-left"));
        const rightSticky = Array.from(this.container.querySelectorAll(".ut-sticky-right"));

        // Group by column ID if multiple cells per column
        const leftCols = Array.from(new Set(leftSticky.map((el) => el.getAttribute("data-col-id"))));
        const rightCols = Array.from(new Set(rightSticky.map((el) => el.getAttribute("data-col-id")))).reverse();

        let leftOffset = 0;
        leftCols.forEach((colId) => {
            const cells = this.container.querySelectorAll(`[data-col-id="${colId}"].ut-sticky-left`);
            const width = cells[0]?.offsetWidth || 0;
            cells.forEach((cell) => {
                cell.style.left = `${leftOffset}px`;
                if (colId === leftCols[leftCols.length - 1]) cell.classList.add("ut-stuck-left");
            });
            leftOffset += width;
        });

        let rightOffset = 0;
        rightCols.forEach((colId) => {
            const cells = this.container.querySelectorAll(`[data-col-id="${colId}"].ut-sticky-right`);
            const width = cells[0]?.offsetWidth || 0;
            cells.forEach((cell) => {
                cell.style.right = `${rightOffset}px`;
                if (colId === rightCols[rightCols.length - 1]) cell.classList.add("ut-stuck-right");
            });
            rightOffset += width;
        });

        // Set up scroll listener (idempotent - remove old one first)
        const tableContainer = this.container.querySelector(".ut-table-container");
        if (!tableContainer) return;

        if (this._scrollListener) {
            tableContainer.removeEventListener("scroll", this._scrollListener);
        }

        const updateBorderOnScroll = () => {
            if (!tableContainer) return;

            const containerRect = tableContainer.getBoundingClientRect();

            // Left-pinned: only show border on the innermost left column when scrolled
            this.container.querySelectorAll(".ut-border-left").forEach((el) => {
                el.classList.remove("ut-border-left");
            });

            if (leftCols.length > 0) {
                const lastLeftColId = leftCols[leftCols.length - 1];
                const thCell = this.container.querySelector(`th[data-col-id="${lastLeftColId}"]`);
                if (thCell) {
                    const cellRect = thCell.getBoundingClientRect();
                    const isLeftScrolled = cellRect.left - containerRect.left < 1 && tableContainer.scrollLeft > 0;
                    if (isLeftScrolled) {
                        this.container
                            .querySelectorAll(`[data-col-id="${lastLeftColId}"] .ut-cell-content`)
                            .forEach((el) => el.classList.add("ut-border-left"));
                    }
                }
            }

            // Right-pinned: only show border on the innermost right column when not fully scrolled
            this.container.querySelectorAll(".ut-border-right").forEach((el) => {
                el.classList.remove("ut-border-right");
            });

            if (rightCols.length > 0) {
                const lastRightColId = rightCols[rightCols.length - 1];
                const thCell = this.container.querySelector(`th[data-col-id="${lastRightColId}"]`);
                if (thCell) {
                    const isRightScrolled =
                        tableContainer.scrollLeft < tableContainer.scrollWidth - tableContainer.offsetWidth - 1;
                    if (isRightScrolled) {
                        this.container
                            .querySelectorAll(`[data-col-id="${lastRightColId}"] .ut-cell-content`)
                            .forEach((el) => el.classList.add("ut-border-right"));
                    }
                }
            }
        };

        this._scrollListener = () => {
            updateBorderOnScroll();
            this.updateMarkerPositions();
        };
        tableContainer.addEventListener("scroll", this._scrollListener, { passive: true });
        // Trigger once on render to set initial state
        updateBorderOnScroll();
    }

    createMarker(rowId, tr) {
        const marker = document.createElement("div");
        marker.className = "ut-floating-indicator";
        marker.title = "New unseen log";
        document.body.appendChild(marker);
        this.markers.set(rowId, { marker, tr });
    }

    updateMarkerPositions() {
        if (this.markers.size === 0) return;

        const containerRect = this.container.querySelector(".ut-wrapper").getBoundingClientRect();
        const tableContainer = this.container.querySelector(".ut-table-container");
        const tableRect = tableContainer.getBoundingClientRect();

        this.markers.forEach(({ marker, tr }, rowId) => {
            const rowRect = tr.getBoundingClientRect();

            // Check if row is within the visible area of the table container (vertical clipping)
            const isVisibleVertically = rowRect.top < tableRect.bottom && rowRect.bottom > tableRect.top;

            if (!isVisibleVertically) {
                marker.style.display = "none";
                return;
            }

            marker.style.display = "block";
            const mWidth = marker.offsetWidth || 8;
            const mHeight = marker.offsetHeight || 8;
            marker.style.top = `${rowRect.top + rowRect.height / 2 - mHeight / 2 + window.scrollY}px`;
            // Center horizontally on the left border (exactly half-in, half-out)
            marker.style.left = `${containerRect.left + window.scrollX - mWidth / 2}px`;
        });
    }

    clearMarkers() {
        this.markers.forEach(({ marker }) => marker.remove());
        this.markers.clear();
    }
    renderCellContent(containerElement, rowId, col, rowData, isUpdatable) {
        containerElement.innerHTML = "";
        const localRow = this.localChanges.get(rowId) || {};
        const fieldDef = this.fieldDefinitions[col.id];

        // Restore currency overlay if needed
        if (fieldDef?.currency) {
            const currencyOverlay = document.createElement("span");
            currencyOverlay.className = "ut-currency-overlay";
            currencyOverlay.innerHTML = typeof fieldDef.currency === "string" ? fieldDef.currency : "&#8369;";
            containerElement.appendChild(currencyOverlay);
            // Ensure classes are correct on the parent <td>
            const td = containerElement.parentElement;
            if (td) td.classList.add("ut-has-currency");
        }

        // Priority: Active Edit > Local Changes > Server Data
        let value = rowData[col.id];
        if (localRow.hasOwnProperty(col.id)) {
            value = localRow[col.id];
        }
        if (this.activeEdit && this.activeEdit.rowId === rowId && this.activeEdit.colId === col.id) {
            value = this.activeEdit.newValue;
        }

        const isChanged = !!(
            localRow.hasOwnProperty(col.id) ||
            (this.activeEdit && this.activeEdit.rowId === rowId && this.activeEdit.colId === col.id && this.activeEdit.isChanged)
        );
        const isInvalid = this.invalidCells.has(`${rowId}:${col.id}`);

        if (!isUpdatable) {
            const span = document.createElement("span");
            span.className = "ut-display-span";

            if (fieldDef && fieldDef.renderContent) {
                const content = fieldDef.renderContent(value, col, rowData);
                if (typeof content === "string") span.innerHTML = this.searchQuery ? this.highlight(content) : content;
                else if (content instanceof Node) span.appendChild(content);
            } else {
                span.innerHTML = this.formatValue(value, col.id, true);
            }

            // Apply per-column span styling from fieldDefinitions
            if (fieldDef?.spanClass) span.classList.add(fieldDef.spanClass);
            if (fieldDef?.spanStyle) span.style.cssText += fieldDef.spanStyle;

            if (col.id === "price") {
                span.classList.add("price-span");
                span.style.color = "white";
            }
            containerElement.appendChild(span);
        } else {
            // 1. Create Display Span
            const span = document.createElement("span");
            span.className = `ut-editable-span ${isChanged ? "ut-span-changed" : ""} ${isInvalid ? "ut-span-invalid" : ""}`;

            // Use custom renderContent if available for the editable span too
            if (fieldDef && fieldDef.renderContent) {
                const content = fieldDef.renderContent(value, col, rowData);
                if (typeof content === "string") span.innerHTML = this.searchQuery ? this.highlight(content) : content;
                else if (content instanceof Node) span.appendChild(content);
            } else {
                span.innerHTML = this.formatValue(value, col.id, true);
            }

            // Apply per-column span styling from fieldDefinitions
            if (fieldDef?.spanClass) span.classList.add(fieldDef.spanClass);
            if (fieldDef?.spanStyle) span.style.cssText += fieldDef.spanStyle;

            if (col.id === "price") {
                span.classList.add("price-span");
                span.style.color = "white"; // Force white text for price
            }

            // 2. Create Input/Select
            let input;
            if (fieldDef && fieldDef.renderInput) {
                input = fieldDef.renderInput(value, col, rowData);
            } else if (col.type === "select" && col.options) {
                input = document.createElement("select");
                input.className = "ut-input ut-select";
                col.options.forEach((opt) => {
                    const option = document.createElement("option");
                    option.value = opt.value;
                    option.textContent = opt.label;
                    option.selected = String(opt.value) === String(value);
                    input.appendChild(option);
                });
            } else {
                input = document.createElement("input");
                const isNumeric =
                    col?.isNumeric || fieldDef?.currency || col?.currency || col.id === "price" || col.id === "stock";
                input.type = isNumeric ? "number" : "text";
                if (isNumeric) input.step = "any";
                input.value = value === null || value === undefined ? "" : value;
                input.className = "ut-input";
            }

            input.classList.add("ut-editable-input");
            if (isChanged) input.classList.add("ut-input-changed");
            if (isInvalid) input.classList.add("ut-input-invalid");
            input.style.display = "none";

            containerElement.appendChild(span);
            containerElement.appendChild(input);

            // 3. Event Listeners for Toggling
            span.addEventListener("click", () => {
                span.style.display = "none";
                input.style.display = "block";
                setTimeout(() => {
                    input.focus();
                    if (input.tagName === "INPUT") input.select();
                    if (input.tagName === "SELECT") {
                        // Attempt to open the dropdown
                        const event = new MouseEvent("mousedown", { bubbles: true });
                        input.dispatchEvent(event);
                    }
                }, 10);
            });

            input.addEventListener("blur", () => {
                input.style.display = "none";
                span.style.display = "flex"; // Use flex to maintain vertical centering

                // Refresh span content
                if (fieldDef && fieldDef.renderContent) {
                    const content = fieldDef.renderContent(input.value, col, rowData);
                    if (typeof content === "string") span.innerHTML = this.searchQuery ? this.highlight(content) : content;
                    else {
                        span.innerHTML = "";
                        span.appendChild(content);
                    }
                } else {
                    span.innerHTML = this.formatValue(input.value, col.id, true);
                }

                // Update tooltip: error prioritized over custom tooltip
                const error = this.invalidCells.get(`${rowId}:${col.id}`);
                span.classList.toggle("ut-span-invalid", !!error);
                if (error) {
                    span.title = error;
                } else if (fieldDef && fieldDef.renderTooltip) {
                    span.title = fieldDef.renderTooltip(input.value, col, rowData);
                } else {
                    span.title = "";
                }
            });

            input.addEventListener("input", (e) => {
                this.validateCell(rowId, col.id, e.target.value);
            });

            input.addEventListener("change", (e) => {
                this.handleCellChange(rowId, col.id, e.target.value);
            });
        }

        this.renderIndicators(containerElement, rowId, col.id, isChanged, isInvalid);

        const input = containerElement.querySelector(".ut-input");
        if (input) {
            input.classList.toggle("ut-input-changed", isChanged);
            input.classList.toggle("ut-input-invalid", isInvalid);
        }
    }

    renderIndicators(containerElement, rowId, colId, isChanged, isInvalid) {
        let stack = containerElement.querySelector(".ut-indicator-stack");
        if (stack) stack.remove();

        if (isChanged || isInvalid) {
            stack = document.createElement("div");
            stack.className = "ut-indicator-stack";

            // Always apply left-border position for consistency across all updatable cells
            stack.classList.add("ut-indicator-left-border");

            if (isChanged) {
                const changeIn = document.createElement("span");
                changeIn.className = "ut-indicator ut-change-indicator";
                changeIn.title = "Value changed. Click to revert.";
                changeIn.addEventListener("click", (e) => {
                    e.stopPropagation();
                    this.showRevertConfirm(rowId, colId, changeIn);
                });
                stack.appendChild(changeIn);
            }

            if (isInvalid) {
                const invalidIn = document.createElement("span");
                invalidIn.className = "ut-indicator ut-invalid-indicator";
                invalidIn.title = this.invalidCells.get(`${rowId}:${colId}`) || "Invalid input.";
                stack.appendChild(invalidIn);
            }

            containerElement.appendChild(stack);
        }
    }

    showRevertConfirm(rowId, colId, anchorElement) {
        const originalRow = this.rows.get(rowId) || {};
        const originalValue = this.formatValue(originalRow[colId], colId);

        window.showSidePopup(
            anchorElement,
            `Revert to "${originalValue}"?`,
            () => {
                this.revertCell(rowId, colId);
                if (typeof window.showToast === "function") window.showToast("Cell reverted.", "success");
            },
            "fa-undo",
            "Revert",
            "var(--nc-primary)",
        );
    }

    revertCell(rowId, colId) {
        const rowChanges = this.localChanges.get(rowId);
        if (rowChanges) {
            delete rowChanges[colId];
            if (Object.keys(rowChanges).length === 0) this.localChanges.delete(rowId);
            this.invalidCells.delete(`${rowId}:${colId}`);
            if (this.activeEdit && this.activeEdit.rowId === rowId && this.activeEdit.colId === colId) {
                this.activeEdit = null;
            }
            this.saveToLocalStorage();
            this.renderTable();
            this.updateControls();
        }
    }

    validateCell(rowId, colId, newValue, updateUI = true) {
        const colDef = this.data?.columns.find((c) => c.id === colId);
        const rules = colDef?.validation;
        let isValid = true;
        let errorMessage = "";

        const valStr = newValue !== null && newValue !== undefined ? String(newValue).trim() : "";
        const num = parseFloat(valStr);

        // 1. Check for explicit rules
        if (rules) {
            if (rules.required && valStr === "") {
                isValid = false;
                errorMessage = rules.requiredMsg || "Required field cannot be empty.";
            } else if (valStr !== "") {
                if (rules.min !== undefined && num < rules.min) {
                    isValid = false;
                    errorMessage = rules.minMsg || `Value must be at least ${rules.min}.`;
                } else if (rules.max !== undefined && num > rules.max) {
                    isValid = false;
                    errorMessage = rules.maxMsg || `Value must be at most ${rules.max}.`;
                }
            }
        }

        // 2. Fallback/Enforced Validation for Price and Stock (Numeric/Currency)
        if (isValid) {
            const isNumeric =
                colDef?.isNumeric ||
                colId === "price" ||
                colId === "stock" ||
                colDef?.currency ||
                this.fieldDefinitions[colId]?.currency;
            if (isNumeric) {
                if (valStr === "") {
                    isValid = false;
                    errorMessage = "Value cannot be empty.";
                } else if (isNaN(num)) {
                    isValid = false;
                    errorMessage = "Value must be a valid number.";
                } else if (num < 0) {
                    isValid = false;
                    errorMessage = "Value cannot be negative.";
                }
            }
        }

        if (isValid) {
            this.invalidCells.delete(`${rowId}:${colId}`);
        } else {
            this.invalidCells.set(`${rowId}:${colId}`, errorMessage);
        }

        if (updateUI) {
            const td = this.container.querySelector(`tr[data-row-id="${rowId}"] td[data-col-id="${colId}"]`);
            if (td) {
                const cellContent = td.querySelector(".ut-cell-content");
                if (cellContent) {
                    const input = cellContent.querySelector(".ut-editable-input");
                    const span = cellContent.querySelector(".ut-editable-span");
                    if (input) {
                        input.classList.toggle("ut-input-invalid", !isValid);
                        input.title = isValid ? "" : errorMessage;
                    }
                    if (span) {
                        span.classList.toggle("ut-span-invalid", !isValid);
                    }

                    // Real-time "Changed" indicator: compare newValue with original table row data
                    const originalValue = (this.rows.get(rowId) || {})[colId];
                    const isChanged = String(newValue) !== String(originalValue);

                    // Update activeEdit tracking for global button visibility
                    this.activeEdit = { rowId, colId, isChanged, newValue };

                    this.renderIndicators(cellContent, rowId, colId, isChanged, !isValid);

                    // If stock changes, also update the status column in the same row
                    if (colId === "stock") {
                        const statusTd = td.parentElement.querySelector('td[data-col-id="status"]');
                        if (statusTd) {
                            const statusContent = statusTd.querySelector(".ut-cell-content");
                            if (statusContent) {
                                const statusCol = this.data.columns.find((c) => c.id === "status");
                                if (statusCol) {
                                    // Temporarily override the row's stock for status rendering
                                    const rowDataCopy = {
                                        ...this.rows.get(rowId),
                                        ...(this.localChanges.get(rowId) || {}),
                                        stock: newValue,
                                    };
                                    this.renderCellContent(statusContent, rowId, statusCol, rowDataCopy, false);
                                }
                            }
                        }
                    }
                }
            }
        }
        this.updateControls();

        return isValid;
    }

    handleCellChange(rowId, colId, newValue) {
        let rowChanges = this.localChanges.get(rowId);
        if (!rowChanges) {
            rowChanges = {};
            this.localChanges.set(rowId, rowChanges);
        }

        const rowData = this.rows.get(rowId) || {};
        const originalValue = rowData[colId];

        const normOriginal = originalValue === null || originalValue === undefined ? "" : String(originalValue);
        let normNew = newValue === null || newValue === undefined ? "" : String(newValue);

        // Auto-round currency fields before comparison and storage
        const fDef = this.fieldDefinitions[colId] || {};
        const col = this.data?.columns.find((c) => c.id === colId);
        if (fDef.currency || col?.currency) {
            const num = parseFloat(newValue);
            if (!isNaN(num)) {
                newValue = Number(num.toFixed(2));
                normNew = String(newValue);
            }
        }

        if (normOriginal === normNew) {
            delete rowChanges[colId];
            if (Object.keys(rowChanges).length === 0) this.localChanges.delete(rowId);
        } else {
            rowChanges[colId] = newValue;
        }

        this.validateCell(rowId, colId, newValue, true);
        this.activeEdit = null;
        this.saveToLocalStorage();
        this.updateControls();

        // Localized UI Update
        const tr = this.container.querySelector(`tr[data-row-id="${rowId}"]`);
        const td = tr?.querySelector(`td[data-col-id="${colId}"]`);
        const span = td?.querySelector(".ut-editable-span");
        const input = td?.querySelector(".ut-editable-input");

        if (span) {
            const isChanged = rowChanges.hasOwnProperty(colId);
            const isInvalid = this.invalidCells.has(`${rowId}:${colId}`);

            span.classList.toggle("ut-span-changed", isChanged);
            span.classList.toggle("ut-span-invalid", isInvalid);
            if (input) {
                input.classList.toggle("ut-input-changed", isChanged);
                input.classList.toggle("ut-input-invalid", isInvalid);
            }

            // Refresh span content
            const fieldDef = this.fieldDefinitions[colId];
            if (fieldDef && fieldDef.renderContent) {
                const content = fieldDef.renderContent(newValue, colId, rowData);
                if (typeof content === "string") span.innerHTML = this.searchQuery ? this.highlight(content) : content;
                else {
                    span.innerHTML = "";
                    span.appendChild(content);
                }
            } else {
                span.innerHTML = this.formatValue(newValue, colId, true);
            }
        }
    }

    updateControls() {
        const saveBtn = this.container.querySelector(".ut-save-btn");
        const clearBtn = this.container.querySelector(".ut-clear-btn");
        const hasLocalChanges = this.localChanges.size > 0;
        const hasPendingChange = this.activeEdit && this.activeEdit.isChanged;
        const hasChanges = hasLocalChanges || hasPendingChange;
        const hasErrors = this.invalidCells.size > 0;

        if (hasChanges && this.data?.updateRequest) {
            let totalPropertyChanges = 0;
            for (const [rowId, changes] of this.localChanges.entries()) {
                totalPropertyChanges += Object.keys(changes).length;
            }

            // Include current active edit if it's dirty and not yet committed to localChanges
            if (hasPendingChange) {
                const alreadyInLocal = this.localChanges.get(this.activeEdit.rowId)?.hasOwnProperty(this.activeEdit.colId);
                if (!alreadyInLocal) {
                    totalPropertyChanges++;
                }
            }

            saveBtn.style.display = "inline-flex";
            saveBtn.disabled = hasErrors;
            const btnText = hasErrors ? `<span class="ut-btn-text-error">Fix Errors</span>` : `Save Changes`;
            saveBtn.innerHTML = `<i class="fa fa-save me-2"></i> ${btnText} (${totalPropertyChanges})`;
            saveBtn.parentElement.style.display = "inline-flex";

            clearBtn.style.display = "inline-flex";
            clearBtn.parentElement.style.display = "inline-flex";
        } else {
            saveBtn.parentElement.style.display = "none";
            clearBtn.parentElement.style.display = "none";
            // Hide popups
            this.container.querySelectorAll(".ut-diff-modal").forEach((m) => (m.style.display = "none"));
        }
    }

    showClearDiffModal() {
        if (this.localChanges.size === 0) return;

        // Close other popups
        const filterPopup = this.container.querySelector(".ut-filter-popup");
        if (filterPopup) filterPopup.style.display = "none";

        const popup = this.container.querySelector(".ut-clear-popup");
        if (popup.style.display === "block") {
            popup.style.display = "none";
            return;
        }

        const entityName = this.storagePrefix ? this.storagePrefix.charAt(0).toUpperCase() + this.storagePrefix.slice(1) : "Item";
        let diffHtml = `<table class="ut-diff-table"><tr><th>${entityName}</th><th>Field</th><th>Original</th><th>Current</th><th></th></tr>`;
        for (const [id, changes] of this.localChanges) {
            const row = this.rows.get(id);
            if (!row) continue;
            const name = row.name || `Record #${id}`;
            const imgData = row.image || {};
            const imgHtml = imgData.image ? `<img src="${imgData.image}" class="ut-diff-thumb" />` : "";
            let firstForRow = true;
            for (const [colId, newVal] of Object.entries(changes)) {
                const label = this.fieldDefinitions[colId]?.label || this.toLabelCase(colId);
                diffHtml += `
                    <tr class="ut-diff-data-row ${!firstForRow ? "ut-diff-row-internal" : ""}" data-row-id="${id}">
                        <td class="ut-diff-product-cell">
                            ${
                                firstForRow
                                    ? `
                                <div class="ut-diff-product-info">
                                    ${imgHtml}
                                    <span class="ut-diff-product-name" title="${name}">${name}</span>
                                </div>
                            `
                                    : ""
                            }
                        </td>
                        <td class="ut-diff-field-cell">${label}</td>
                        <td class="ut-diff-old">${this.formatValue(row[colId], colId)}</td>
                        <td class="ut-diff-new">${this.formatValue(newVal, colId)}</td>
                        <td>
                            <button class="ut-revert-cell-btn" title="Revert" onclick="window.updatableTables['${this.container.id}'].removeDiffRow(this, '${id}', '${colId}')">
                                <i class="fa fa-undo"></i>
                            </button>
                        </td>
                    </tr>
                `;
                firstForRow = false;
            }
        }
        diffHtml += "</table>";

        popup.innerHTML = `
            <div class="ut-popup-header"><i class="fa fa-undo" style="color:var(--nc-error)"></i><span>Clear Changes</span></div>
            <div class="ut-diff-container" style="max-height: 350px; overflow-y: auto; overflow-x: auto; margin-bottom: 1rem;">
                ${diffHtml}
            </div>
            <div class="ut-modal-actions">
                <button class="ut-popup-btn-cancel">Cancel</button>
                <button class="ut-popup-btn-confirm btn-danger">Clear All</button>
            </div>
        `;

        popup.style.display = "block";
        const overlay = document.getElementById("ncGlobalOverlay");
        const triggerBtn = this.container.querySelector(".ut-clear-btn");
        if (overlay) overlay.classList.add("show");
        this.container.classList.add("ut-has-popup");
        if (triggerBtn) triggerBtn.classList.add("ut-popup-active-trigger");

        popup.querySelector(".ut-popup-btn-cancel").onclick = () => {
            popup.style.display = "none";
            if (overlay) overlay.classList.remove("show");
            this.container.classList.remove("ut-has-popup");
            if (triggerBtn) triggerBtn.classList.remove("ut-popup-active-trigger");
        };
        popup.querySelector(".ut-popup-btn-confirm").onclick = () => {
            this.performClearAll();
            popup.style.display = "none";
            if (overlay) overlay.classList.remove("show");
            this.container.classList.remove("ut-has-popup");
            if (triggerBtn) triggerBtn.classList.remove("ut-popup-active-trigger");
        };
    }

    async performClearAll() {
        this.localChanges = new Map();
        this.invalidCells = new Map();
        this.activeEdit = null;

        this.clearLocalStorage();

        // Hard Reset: Rebuild container structure and table
        this.renderContainer();
        this.currentPage = 1;
        this.renderTable();
        this.updateControls();

        if (typeof window.showToast === "function") {
            window.showToast("All changes cleared.", "info");
        }
    }

    showSaveDiffModal() {
        if (this.localChanges.size === 0 || this.invalidCells.size > 0) return;

        // Close other popups
        const filterPopup = this.container.querySelector(".ut-filter-popup");
        if (filterPopup) filterPopup.style.display = "none";

        const popup = this.container.querySelector(".ut-save-popup");
        if (popup.style.display === "block") {
            popup.style.display = "none";
            return;
        }

        const entityName = this.storagePrefix ? this.storagePrefix.charAt(0).toUpperCase() + this.storagePrefix.slice(1) : "Item";
        let diffHtml = `<table class="ut-diff-table"><tr><th>${entityName}</th><th>Field</th><th>Original</th><th>New</th></tr>`;
        for (const [id, changes] of this.localChanges) {
            const row = this.rows.get(id);
            if (!row) continue;
            const name = row.name || `Record #${id}`;
            const imgData = row.image || {};
            const imgHtml = imgData.image ? `<img src="${imgData.image}" class="ut-diff-thumb" />` : "";
            const changeEntries = Object.entries(changes);
            const totalChanges = changeEntries.length;

            changeEntries.forEach(([colId, newVal], index) => {
                const label = this.fieldDefinitions[colId]?.label || this.toLabelCase(colId);
                const isFirst = index === 0;
                const isLast = index === totalChanges - 1;
                const hasMultiple = totalChanges > 1;

                let rowClass = "ut-diff-data-row";
                if (hasMultiple) {
                    if (!isFirst) rowClass += " ut-diff-row-no-top";
                    if (!isLast) rowClass += " ut-diff-row-no-bottom";
                }

                diffHtml += `
                    <tr class="${rowClass}" data-row-id="${id}">
                        <td class="ut-diff-product-cell">
                            ${
                                isFirst
                                    ? `
                                <div class="ut-diff-product-info">
                                    ${imgHtml}
                                    <span class="ut-diff-product-name" title="${name}">${name}</span>
                                </div>
                            `
                                    : ""
                            }
                        </td>
                        <td class="ut-diff-field-cell">${label}</td>
                        <td class="ut-diff-old">${this.formatValue(row[colId], colId)}</td>
                        <td class="ut-diff-new">${this.formatValue(newVal, colId)}</td>
                    </tr>
                `;
            });
        }
        diffHtml += "</table>";

        popup.innerHTML = `
            <div class="ut-popup-header"><i class="fa fa-save" style="color:var(--nc-primary)"></i><span>Confirm Changes</span></div>
            <div class="ut-diff-container" style="max-height: 350px; overflow-y: auto; overflow-x: auto; margin-bottom: 1rem;">
                ${diffHtml}
            </div>
            <div class="ut-modal-actions">
                <button class="ut-popup-btn-cancel">Cancel</button>
                <button class="ut-popup-btn-confirm">Save All</button>
            </div>
        `;

        popup.style.display = "block";
        const overlay = document.getElementById("ncGlobalOverlay");
        const triggerBtn = this.container.querySelector(".ut-save-btn");
        if (overlay) overlay.classList.add("show");
        this.container.classList.add("ut-has-popup");
        if (triggerBtn) triggerBtn.classList.add("ut-popup-active-trigger");

        popup.querySelector(".ut-popup-btn-cancel").onclick = () => {
            popup.style.display = "none";
            if (overlay) overlay.classList.remove("show");
            this.container.classList.remove("ut-has-popup");
            if (triggerBtn) triggerBtn.classList.remove("ut-popup-active-trigger");
        };
        popup.querySelector(".ut-popup-btn-confirm").onclick = () => {
            this.saveChanges();
            popup.style.display = "none";
            if (overlay) overlay.classList.remove("show");
            this.container.classList.remove("ut-has-popup");
            if (triggerBtn) triggerBtn.classList.remove("ut-popup-active-trigger");
        };
    }

    async saveChanges() {
        if (!this.data || !this.data.updateRequest || this.invalidCells.size > 0) return;
        const changesToSave = {};
        for (const [id, changes] of this.localChanges) {
            changesToSave[id] = changes;
        }

        this.isSaving = true;
        try {
            const res = await fetch(this.data.updateRequest, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest",
                    RequestVerificationToken: document.querySelector('input[name="__RequestVerificationToken"]')?.value || "",
                },
                body: JSON.stringify({ changes: changesToSave }),
            });
            if (!res.ok) throw new Error("Save failed");

            if (typeof window.showToast === "function") {
                window.showToast("Changes saved successfully!", "success");
            }

            this.clearLocalStorage();
            if (this.onSave) {
                this.onSave();
            } else {
                await this.fetchData();
            }
        } catch (e) {
            if (typeof window.showToast === "function") {
                window.showToast("Failed to save changes.", "error");
            }
        } finally {
            this.isSaving = false;
        }
    }

    highlight(text) {
        return window.ncHighlight(text, this.searchQuery);
    }

    toLabelCase(str) {
        if (!str) return "";
        // Strip technical "Id" or "id" suffixes from the display label
        let cleanStr = str;
        if (str.toLowerCase().endsWith("id") && str.length > 2) {
            cleanStr = str.slice(0, -2);
        }
        const result = cleanStr.replace(/([A-Z])/g, " $1");
        return result.charAt(0).toUpperCase() + result.slice(1).trim();
    }

    formatValue(val, colId = null, useHighlight = false) {
        if (val === null || val === undefined) return "";
        let str = "";

        // UTC to Local Conversion for Date/Time fields
        const isDateColumn =
            colId &&
            (colId.toLowerCase().includes("date") ||
                colId.toLowerCase().includes("time") ||
                colId.toLowerCase().includes("timestamp") ||
                this.fieldDefinitions[colId]?.type === "date");

        if (isDateColumn && val) {
            try {
                const date = new Date(val);
                if (!isNaN(date.getTime())) {
                    // Check if the original string was UTC (ends with Z or has no offset but intended as UTC)
                    // ASP.NET JSON usually serializes as ISO 8601 UTC
                    str = date.toLocaleString(undefined, {
                        year: "numeric",
                        month: "short",
                        day: "numeric",
                        hour: "2-digit",
                        minute: "2-digit",
                        second: "2-digit",
                    });
                } else {
                    str = String(val);
                }
            } catch (e) {
                str = String(val);
            }
        } else if (typeof val === "object") {
            str = val.name || val.label || val.value || JSON.stringify(val);
        } else {
            const col = colId ? this.data.columns.find((c) => c.id === colId) : null;
            const fDef = colId ? this.fieldDefinitions[colId] : null;

            if (col && col.type === "select" && col.options) {
                const option = col.options.find((o) => String(o.value) === String(val));
                str = option ? option.label : String(val);
            } else {
                const isCurrency = col?.currency || fDef?.currency || false;
                const minDecimals = isCurrency ? 2 : 0;

                if (typeof val === "number") {
                    str = val.toLocaleString("en-US", { minimumFractionDigits: minDecimals, maximumFractionDigits: 2 });
                } else if (!isNaN(val) && val !== "" && val !== null) {
                    const n = parseFloat(val);
                    str = n.toLocaleString("en-US", { minimumFractionDigits: minDecimals, maximumFractionDigits: 2 });
                } else {
                    str = String(val);
                }
            }
        }
        return useHighlight && this.searchQuery ? this.highlight(str) : str;
    }

    confirmRestore(rowId, url, element) {
        const entityName = this.storagePrefix
            ? this.storagePrefix.charAt(0).toUpperCase() + this.storagePrefix.slice(1)
            : "Record";
        window.showSidePopup(
            element,
            `Restore ${entityName}?`,
            () => {
                if (element) element.classList.remove("ut-popup-active-trigger");
                this.container.classList.remove("ut-has-popup");
                const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
                fetch(url, {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/x-www-form-urlencoded",
                        "X-Requested-With": "XMLHttpRequest",
                        RequestVerificationToken: token,
                    },
                    body: new URLSearchParams({ id: rowId }),
                }).then((r) => {
                    if (r.ok) {
                        window.showToast(`${entityName} restored`, "success");
                        this.localChanges.delete(rowId);
                        localStorage.removeItem(`${this.storagePrefix}-row-${rowId}`);
                        this.saveToLocalStorage();
                        this.fetchData();
                    } else {
                        window.showToast("Failed to restore record", "error");
                    }
                });
            },
            "fa-undo",
            "Restore",
            "var(--nc-success)",
            "",
            "btn-success",
        );
    }

    confirmDelete(rowId, url, element) {
        const entityName = this.storagePrefix
            ? this.storagePrefix.charAt(0).toUpperCase() + this.storagePrefix.slice(1)
            : "Record";
        window.showSidePopup(
            element,
            `Delete ${entityName}?`,
            () => {
                if (element) element.classList.remove("ut-popup-active-trigger");
                this.container.classList.remove("ut-has-popup");
                const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
                fetch(url, {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/x-www-form-urlencoded",
                        "X-Requested-With": "XMLHttpRequest",
                        RequestVerificationToken: token,
                    },
                    body: new URLSearchParams({ id: rowId }),
                }).then((r) => {
                    if (r.ok) {
                        window.showToast(`${entityName} deleted`, "success");
                        this.localChanges.delete(rowId);
                        localStorage.removeItem(`${this.storagePrefix}-row-${rowId}`);
                        this.saveToLocalStorage();
                        this.fetchData();
                    } else {
                        window.showToast("Failed to delete record", "error");
                    }
                });
            },
            "fa-trash",
            "Delete",
            "var(--nc-error)",
            "",
            "btn-danger",
        );
    }

    removeDiffRow(btn, rowId, colId) {
        // Revert the actual data
        this.revertCell(rowId, colId);

        // Re-render the appropriate modal
        const isClearModal = document.querySelector(".ut-side-popup[data-popup-type='clear']");
        if (isClearModal) {
            this.showClearDiffModal();
        } else {
            this.showSaveDiffModal();
        }

        // Check if table is totally empty (handled by re-render logic implicitly if localChanges empty)
        if (this.localChanges.size === 0) {
            const popup = document.querySelector(".ut-side-popup");
            if (popup) popup.remove();
        }
    }
}

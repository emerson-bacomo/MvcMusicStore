class ProductEditor {
    constructor(containerId) {
        this.container = document.getElementById(containerId);
        if (!this.container) return;

        this.productId = this.container.dataset.productId;
        this.form = document.getElementById("inPlaceEditForm");
        this.storageKey = `product-edit-${this.productId}`;

        this.originalState = this.captureState();
        this.changedFields = new Set();
        this.isInfiniteScrolling = false;
        this.currentImgIndex = 0;
        this.dragSrcGalleryEl = null;

        this.init();
    }

    init() {
        this.setupEventListeners();
        this.setupCarousel();
        this.setupDragDrop();
        this.loadFromLocalStorage();
        this.checkChanges();

        // URL State Sync
        if (window.location.hash === "#edit") {
            this.setEditMode(true);
        }
        window.addEventListener("hashchange", () => {
            const shouldEdit = window.location.hash === "#edit";
            if (shouldEdit !== this.container.classList.contains("nc-edit-mode")) {
                this.setEditMode(shouldEdit);
            }
        });

        // Initial auto-resize for textareas
        this.form.querySelectorAll("textarea").forEach((ta) => this.autoSize(ta));
    }

    captureState() {
        const formData = new FormData(this.form);
        const state = { fields: {}, images: this.getImagesState() };
        for (let [key, value] of formData.entries()) {
            if (key !== "__RequestVerificationToken" && !key.includes("productImages") && key !== "bannerImage") {
                state.fields[key] = value;
            }
        }
        return state;
    }

    getImagesState() {
        return {
            primary: document.getElementById("primaryImageInput").value,
            order: document.getElementById("imageOrderInput").value,
            deleted: document.getElementById("deletedImagesInput").value,
            bannerUrl: document.getElementById("bannerUrlInput")?.value || "",
        };
    }

    setupEventListeners() {
        // Mode Switches
        document.getElementById("enterEditMode")?.addEventListener("click", () => this.setEditMode(true));
        document.getElementById("exitEditMode")?.addEventListener("click", () => this.setEditMode(false));

        // Form Tracking
        this.form.querySelectorAll("input, select, textarea").forEach((input) => {
            input.addEventListener("input", (e) => this.handleInputChange(e.target));
            input.addEventListener("change", (e) => this.handleInputChange(e.target));
        });

        // Save / Clear
        document.getElementById("saveEditorChangesGutter")?.addEventListener("click", (e) => {
            e.stopPropagation();
            this.saveChanges(e.currentTarget);
        });
        document.getElementById("saveEditorChanges")?.addEventListener("click", (e) => this.saveChanges(e.currentTarget));

        document.getElementById("clearEditorChangesGutter")?.addEventListener("click", (e) => {
            e.stopPropagation();
            this.clearChanges(e.currentTarget);
        });
        document.getElementById("clearEditorChanges")?.addEventListener("click", (e) => this.clearChanges(e.currentTarget));


        // Click outside to close popups
        document.addEventListener("click", (e) => {
            if (e.target.id === "ncGlobalOverlay") {
                this.closeLocalizedPopups();
                return;
            }

            if (!e.target.closest(".nc-localized-popup") && !e.target.closest(".nc-gutter-btn") && !e.target.closest(".nc-changed-indicator")) {
                this.closeLocalizedPopups();
            }
        });


        // Image Handling
        document.getElementById("newImageUrlInput")?.addEventListener("keypress", (e) => {
            if (e.key === "Enter") {
                e.preventDefault();
                this.addImageUrl();
            }
        });

        // Click to edit spans
        this.container.querySelectorAll(".nc-editable-span").forEach((span) => {
            span.addEventListener("click", () => {
                if (!this.container.classList.contains("nc-edit-mode")) return;
                const wrapper = span.closest(".nc-editable-wrapper");
                if (wrapper) {
                    wrapper.classList.add("nc-is-editing");
                    const input = wrapper.querySelector("input, select, textarea");
                    if (input) {
                        input.focus();
                        if (input.tagName === "TEXTAREA") this.autoSize(input);
                    }
                }
            });
        });

        // Focus out to close inline edit
        this.form.addEventListener("focusout", (e) => {
            const wrapper = e.target.closest(".nc-editable-wrapper");
            if (wrapper) {
                setTimeout(() => {
                    if (!wrapper.contains(document.activeElement)) {
                        wrapper.classList.remove("nc-is-editing");
                        this.updateSpan(wrapper);
                    }
                }, 150);
            }
        });
    }

    setEditMode(active) {
        if (active) {
            this.container.classList.add("nc-edit-mode");
            if (window.location.hash !== "#edit") {
                history.replaceState(null, null, "#edit");
            }
            this.form.querySelectorAll("textarea").forEach((ta) => this.autoSize(ta));
        } else {
            this.container.classList.remove("nc-edit-mode");
            if (window.location.hash === "#edit") {
                history.replaceState(null, null, " ");
            }
        }
    }

    handleInputChange(input) {
        const field = input.name;
        if (!field) return;

        if (input.tagName === "TEXTAREA") this.autoSize(input);

        const val = input.type === "checkbox" ? (input.checked ? "true" : "false") : input.value;
        this.trackFieldChange(field, val);
        
        this.validateField(input);
        this.saveToLocalStorage();
    }

    trackFieldChange(field, value) {
        if (!field) return;
        
        const input = this.form.querySelector(`[name="${field}"]`);
        if (!input) return;

        const initial = input.dataset.initialValue;
        let current = value !== undefined ? String(value) : (input.type === "checkbox" ? (input.checked ? "true" : "false") : String(input.value));
        
        let isChanged = current !== String(initial);

        // Normalize numeric comparison for Price to avoid formatting mismatches (e.g. "1500" vs "1500.00")
        if (field === "Price") {
            const pInitial = parseFloat(initial || 0);
            const pCurrent = parseFloat(current || 0);
            isChanged = Math.abs(pInitial - pCurrent) > 0.001;
        }

        const wrapper = input.closest(".nc-editable-wrapper");
        const indicator = document.getElementById(`indicator-${field}`);

        if (isChanged) {
            this.changedFields.add(field);
            if (wrapper) wrapper.classList.add("nc-changed");
            if (indicator) indicator.style.display = "block";
        } else {
            this.changedFields.delete(field);
            if (wrapper) wrapper.classList.remove("nc-changed");
            if (indicator) indicator.style.display = "none";
        }

        this.checkChanges();
        this.saveToLocalStorage();
    }

    validateField(input) {
        if (!window.ValidationEngine) return;

        const field = input.name;
        const val = input.value;

        // Use ValidationEngine for standard data-val attributes (Name length, etc.)
        const isBaseValid = window.ValidationEngine.validate(input);
        if (!isBaseValid) return;

        // Custom Numeric check for Price
        if (field === "Price" && parseFloat(val) <= 0) {
            window.ValidationEngine.setError(input, "Price must be greater than 0.");
            return;
        }

        // Categories/Brands (if not handled by data-val-required)
        if (["CategoryId", "BrandId"].includes(field) && !val) {
            window.ValidationEngine.setError(input, `${field} is required.`);
            return;
        }

        // Banner validation
        if (field === "IsBanner" || field === "BannerDescription" || field === "BannerImageUrl") {
            const isBanner = this.form.querySelector('[name="IsBanner"]').checked;
            const bannerDesc = this.form.querySelector('[name="BannerDescription"]');
            const bannerUrl = this.form.querySelector('[name="BannerImageUrl"]');

            if (isBanner) {
                if (!bannerDesc?.value.trim())
                    window.ValidationEngine.setError(bannerDesc, "Banner description is required when featured.");
                else window.ValidationEngine.setError(bannerDesc, "");

                if (!bannerUrl?.value.trim())
                    window.ValidationEngine.setError(bannerUrl, "Banner image is required when featured.");
                else window.ValidationEngine.setError(bannerUrl, "");
            } else {
                if (bannerDesc) window.ValidationEngine.setError(bannerDesc, "");
                if (bannerUrl) window.ValidationEngine.setError(bannerUrl, "");
            }
        } else {
            // Already handled by isBaseValid, but ensure no double errors
        }
    }

    validateForm() {
        let isValid = true;

        // 1. Check images count
        const activeImages = this.container.querySelectorAll(".nc-gallery-item:not(.nc-to-delete)");
        if (activeImages.length === 0) {
            window.showToast("At least one product image is required.", "error");
            isValid = false;
        }

        // 2. Check Banner requirements
        const isBanner = this.form.querySelector('[name="IsBanner"]').checked;
        if (isBanner) {
            const bannerDesc = this.form.querySelector('[name="BannerDescription"]').value.trim();
            const bannerUrl = this.form.querySelector('[name="BannerImageUrl"]').value.trim();
            if (!bannerDesc || !bannerUrl) {
                window.showToast("Banner featured products require a description and image.", "error");
                isValid = false;
            }
        }

        // 3. Trigger all field validations
        this.form.querySelectorAll("input, select, textarea").forEach((input) => {
            this.validateField(input);
            if (input.classList.contains("is-invalid")) {
                isValid = false;
                // Force show the popup container for 2 seconds
                const wrapper = input.closest(".nc-pd-floating-group");
                if (wrapper) {
                    wrapper.classList.add("nc-show-all-errors");
                    setTimeout(() => wrapper.classList.remove("nc-show-all-errors"), 2000);
                }
            }
        });

        return isValid;
    }

    checkChanges() {
        const count = this.changedFields.size;
        const indicatorBadge = document.getElementById("changesIndicator");
        const saveBar = document.getElementById("editorSaveBar");
        const gutterSave = document.getElementById("saveEditorChangesGutter");
        const gutterRevert = document.getElementById("clearEditorChangesGutter");

        if (indicatorBadge) {
            indicatorBadge.textContent = count;
            indicatorBadge.style.display = count > 0 ? "flex" : "none";
        }

        if (saveBar) saveBar.style.display = count > 0 ? "block" : "none";
        
        if (gutterSave) {
            // Disabled but visible is often better for UX stability
            gutterSave.classList.toggle("opacity-50", count === 0);
            gutterSave.disabled = (count === 0);
        }
        if (gutterRevert) {
            gutterRevert.style.display = count > 0 ? "flex" : "none";
        }
    }

    autoSize(el) {
        if (!el || el.tagName !== "TEXTAREA") return;
        el.style.height = "auto";
        el.style.height = el.scrollHeight + "px";
    }

    updateSpan(wrapper) {
        const span = wrapper.querySelector(".nc-editable-span");
        const input = wrapper.querySelector("input, select, textarea");
        if (!span || !input) return;

        if (input.tagName === "SELECT") {
            span.textContent = input.options[input.selectedIndex]?.text || "None";
        } else if (input.type === "checkbox") {
            span.textContent = input.checked ? "Featured" : "Not Featured";
            span.className = `badge-nc ${input.checked ? "bg-success" : "bg-secondary"} nc-editable-span`;
        } else if (input.name === "Price") {
            span.textContent = parseFloat(input.value || 0).toLocaleString(undefined, { minimumFractionDigits: 2 });
        } else if (input.name === "Stock") {
            span.textContent = `${input.value || 0} available`;
        } else {
            span.textContent = input.value || (wrapper.dataset.field === "Description" ? "No description" : "None");
        }
    }

    // --- Image Management ---
    setupCarousel() {
        const carouselEl = document.getElementById("productCarousel");
        if (!carouselEl) return;

        this.bsCarousel = bootstrap.Carousel.getOrCreateInstance(carouselEl, { interval: false });

        // Sync gallery highlights IMMEDIATELY when carousel starts sliding (not after)
        carouselEl.addEventListener("slide.bs.carousel", (e) => {
            const idx = e.to;
            this.currentImgIndex = idx;
            document.querySelectorAll(".nc-image-gallery .nc-gallery-item").forEach((item, i) => {
                item.classList.toggle("active", i === idx);
            });
        });

        // Also sync when clicking gallery items directly
        document.querySelectorAll(".nc-image-gallery .nc-gallery-item").forEach((item, i) => {
            item.addEventListener("click", () => {
                if (this.container.classList.contains("nc-edit-mode")) return;
                this.toImage(i); // Trigger the carousel to slide
                document.querySelectorAll(".nc-image-gallery .nc-gallery-item").forEach((el, j) => {
                    el.classList.toggle("active", j === i);
                });
            });
        });
    }

    toImage(idx) {
        this.bsCarousel?.to(idx);
    }

    carouselNext() {
        this.bsCarousel?.next();
    }

    carouselPrev() {
        this.bsCarousel?.prev();
    }

    setupDragDrop() {
        ["dropZone", "bannerDropZone"].forEach((id) => {
            const el = document.getElementById(id);
            if (!el) return;
            el.addEventListener("dragover", (e) => {
                e.preventDefault();
                el.classList.add("drag-over");
            });
            ["dragleave", "drop"].forEach((ev) => el.addEventListener(ev, () => el.classList.remove("drag-over")));
            el.addEventListener("drop", (e) => {
                e.preventDefault();
                if (id === "dropZone") this.handleFileSelect({ target: { files: e.dataTransfer.files } });
                else this.handleBannerSelect({ target: { files: e.dataTransfer.files } });
            });
        });

        document.querySelectorAll(".nc-gallery-item").forEach((it) => this.setupGalleryDragAndDrop(it));
    }

    setupGalleryDragAndDrop(item) {
        item.setAttribute("draggable", "true");
        item.addEventListener("dragstart", (e) => {
            if (!this.container.classList.contains("nc-edit-mode")) {
                e.preventDefault();
                return;
            }
            this.dragSrcGalleryEl = item;
            e.dataTransfer.effectAllowed = "move";
            item.style.opacity = "0.4";
        });

        item.addEventListener("dragover", (e) => {
            e.preventDefault();
            if (this.dragSrcGalleryEl && this.dragSrcGalleryEl !== item) {
                const container = item.parentNode;
                const items = Array.from(container.querySelectorAll(".nc-gallery-item"));
                const srcIdx = items.indexOf(this.dragSrcGalleryEl);
                const tarIdx = items.indexOf(item);
                if (srcIdx < tarIdx) container.insertBefore(this.dragSrcGalleryEl, item.nextSibling);
                else container.insertBefore(this.dragSrcGalleryEl, item);
            }
        });

        item.addEventListener("dragend", () => {
            item.style.opacity = "1";
            this.updateImageOrder();
            this.changedFields.add("Images");
            this.checkChanges();
        });
    }

    handleFileSelect(e) {
        const files = e.target.files;
        if (!files) return;
        Array.from(files).forEach((file) => {
            const reader = new FileReader();
            reader.onload = (ev) => this.appendTempImage(ev.target.result);
            reader.readAsDataURL(file);
        });
    }

    appendTempImage(src) {
        const galleryContainer = document.getElementById("galleryContainer");
        if (galleryContainer) galleryContainer.classList.remove("d-none");

        const gallery = document.getElementById("imageGallery");
        const item = document.createElement("div");
        item.className = "nc-gallery-item";
        item.dataset.url = src;

        // Add imageUrls input ONLY for remote URLs (not data URLs from local files)
        const isRemote = src.startsWith("http") || src.startsWith("/");
        const hiddenInput = isRemote ? `<input type="hidden" name="imageUrls" value="${src}" />` : "";

        item.innerHTML = `
            <img src="${src}" alt="New Image" />
            <div class="nc-gallery-status-icon nc-editable"><i class="fa fa-image"></i></div>
            ${hiddenInput}
            <div class="nc-editable nc-item-actions">
                <button type="button" class="nc-item-btn btn-thumbnail" onclick="editor.setPrimaryImage(this)"><i class="fa fa-image"></i></button>
                <button type="button" class="nc-item-btn btn-danger" onclick="editor.removeTempImage(this)"><i class="fa fa-trash"></i></button>
            </div>
        `;
        const isFirst = gallery.querySelectorAll(".nc-gallery-item").length === 0;
        if (isFirst) {
            item.classList.add("is-thumbnail");
            const btn = item.querySelector(".btn-thumbnail");
            if (btn) btn.classList.add("active");
            document.getElementById("primaryImageInput").value = src;
        }

        gallery.appendChild(item);
        this.setupGalleryDragAndDrop(item);
        this.updateImageOrder();
        this.refreshCarousel();
        this.changedFields.add("Images");
        this.checkChanges();
    }

    removeTempImage(btn) {
        btn.closest(".nc-gallery-item").remove();
        this.updateImageOrder();
        this.refreshCarousel();
        this.changedFields.add("Images");
        this.checkChanges();
    }

    addImageUrl() {
        const input = document.getElementById("newImageUrlInput");
        const url = input.value.trim();
        if (!url) return;
        this.appendTempImage(url);
        input.value = "";
    }

    setPrimaryImage(btn) {
        const item = btn.closest(".nc-gallery-item");
        document.querySelectorAll(".btn-thumbnail").forEach((b) => b.classList.remove("active"));
        btn.classList.add("active");

        document.querySelectorAll(".nc-gallery-item").forEach((it) => it.classList.remove("is-thumbnail"));
        item.classList.add("is-thumbnail");
        document.getElementById("primaryImageInput").value = item.dataset.url;
        this.changedFields.add("PrimaryImage");
        this.checkGalleryChanges();
        this.checkChanges();
        this.saveToLocalStorage();
    }

    toggleRemoveImage(btn) {
        const item = btn.closest(".nc-gallery-item");
        const url = item.dataset.url;
        item.classList.toggle("nc-to-delete");

        const deletedInput = document.getElementById("deletedImagesInput");
        const deleted = JSON.parse(deletedInput.value || "[]");

        if (item.classList.contains("nc-to-delete")) {
            item.style.opacity = "0.3";
            if (!deleted.includes(url)) deleted.push(url);
        } else {
            item.style.opacity = "1";
            const idx = deleted.indexOf(url);
            if (idx !== -1) deleted.splice(idx, 1);
        }

        deletedInput.value = JSON.stringify(deleted);
        this.updateImageOrder();
        this.refreshCarousel();
        this.changedFields.add("Images");
        this.checkChanges();
    }

    updateImageOrder() {
        const urls = Array.from(document.querySelectorAll(".nc-image-gallery .nc-gallery-item:not(.nc-to-delete)")).map((it) => it.dataset.url);
        document.getElementById("imageOrderInput").value = urls.join(",");
        this.checkGalleryChanges();
        this.saveToLocalStorage();
    }

    checkGalleryChanges() {
        const oldOrder = (this.originalState.images.order || "").split(",").filter(x => x);
        const newOrder = (document.getElementById("imageOrderInput").value || "").split(",").filter(x => x);
        const oldPrimary = this.originalState.images.primary;
        const newPrimary = document.getElementById("primaryImageInput").value;

        const orderChanged = JSON.stringify(oldOrder) !== JSON.stringify(newOrder);
        const primaryChanged = oldPrimary !== newPrimary;

        const galleryInd = document.getElementById("indicator-Gallery");
        if (orderChanged || primaryChanged) {
            this.changedFields.add("Images");
            if (galleryInd) galleryInd.style.display = "block";
        } else {
            this.changedFields.delete("Images");
            if (galleryInd) galleryInd.style.display = "none";
        }
        this.checkChanges();
    }

    refreshCarousel() {
        const track = document.getElementById("carouselTrack");
        if (!track) return;

        this.images = Array.from(document.querySelectorAll(".nc-gallery-item:not(.nc-to-delete)")).map((it) => it.dataset.url);

        track.innerHTML = "";
        if (this.images.length === 0) {
            track.innerHTML = `<div class="carousel-item active h-100"><img src="https://placehold.co/800x600/1a1f2c/7babdd?text=No+Image" class="nc-main-image h-100 w-100" style="object-fit: cover;" /></div>`;
        } else {
            this.images.forEach((url, i) => {
                const slide = document.createElement("div");
                slide.className = `carousel-item h-100 ${i === 0 ? "active" : ""}`;
                slide.dataset.url = url;
                slide.innerHTML = `<div class="d-flex align-items-center justify-content-center h-100"><img src="${url}" class="nc-main-image h-100 w-100" style="object-fit: cover;" onclick="window.openFullscreen(this.src)" /></div>`;
                track.appendChild(slide);
            });
        }

        // Re-init Bootstrap instance if structure changed
        this.bsCarousel?.dispose();
        this.setupCarousel();
    }

    updateMainImage(url) {
        const main = document.getElementById("mainProductImage"); // Legacy or current
        if (main) main.src = url;
    }

    handleBannerSelect(e) {
        const file = e.target.files[0];
        if (!file) return;
        const reader = new FileReader();
        reader.onload = (ev) => {
            const preview = document.getElementById("currentBannerPreviewEdit");
            if (preview) {
                document.getElementById("bannerPreviewContainerEdit").style.display = "block";
                preview.src = ev.target.result;
            }
            document.getElementById("bannerUrlInput").value = ev.target.result;
            this.handleInputChange(document.getElementById("bannerUrlInput"));
        };
        reader.readAsDataURL(file);
    }

    // --- State Management ---
    revertAll() {
        for (const field of this.changedFields) {
            this.revertField(field, false);
        }
        this.changedFields.clear();
        this.checkChanges();
        localStorage.removeItem(this.storageKey);
        
        if (typeof window.showToast === "function") {
            window.showToast("All changes cleared.", "info");
        }
    }

    saveToLocalStorage() {
        const state = {
            fields: {},
            changed: Array.from(this.changedFields),
        };
        this.form.querySelectorAll("input, select, textarea").forEach((input) => {
            if (input.name) {
                state.fields[input.name] = input.type === "checkbox" ? input.checked : input.value;
            }
        });
        localStorage.setItem(this.storageKey, JSON.stringify(state));
    }

    loadFromLocalStorage() {
        const saved = localStorage.getItem(this.storageKey);
        if (!saved) return;
        const state = JSON.parse(saved);
        for (const [name, val] of Object.entries(state.fields)) {
            const input = this.form.querySelector(`[name="${name}"]`);
            if (input) {
                if (input.type === "checkbox") input.checked = val === true;
                else input.value = val;

                const wrapper = input.closest(".nc-editable-wrapper");
                if (wrapper) this.updateSpan(wrapper);
            }
        }
        state.changed?.forEach((f) => {
            this.changedFields.add(f);
            const input = this.form.querySelector(`[name="${f}"]`);
            if (input) {
                const wrapper = input.closest(".nc-editable-wrapper");
                if (wrapper) wrapper.classList.add("nc-changed");
                const indicator = document.getElementById(`indicator-${f}`);
                if (indicator) indicator.style.display = "block";
            }
        });
        this.syncGalleryWithInputs();
        this.checkChanges();
    }

    syncGalleryWithInputs() {
        const orderValue = document.getElementById("imageOrderInput").value;
        const primaryValue = document.getElementById("primaryImageInput").value;
        const deletedValue = document.getElementById("deletedImagesInput").value;
        if (!orderValue) return;

        const gallery = document.getElementById("imageGallery");
        if (!gallery) return;

        const order = orderValue.split(",").filter(x => x);
        const deleted = JSON.parse(deletedValue || "[]");

        // 1. Re-sort gallery items in DOM
        const items = Array.from(gallery.querySelectorAll(".nc-gallery-item"));
        items.sort((a, b) => {
            const idxA = order.indexOf(a.dataset.url);
            const idxB = order.indexOf(b.dataset.url);
            if (idxA === -1 && idxB === -1) return 0;
            if (idxA === -1) return 1;
            if (idxB === -1) return -1;
            return idxA - idxB;
        });
        items.forEach(item => gallery.appendChild(item));

        // 2. Update classes (active/thumbnail/deleted)
        gallery.querySelectorAll(".nc-gallery-item").forEach(item => {
            const url = item.dataset.url;
            
            // Primary
            const isPrimary = url === primaryValue;
            item.classList.toggle("is-thumbnail", isPrimary);
            const thumbBtn = item.querySelector(".btn-thumbnail");
            if (thumbBtn) thumbBtn.classList.toggle("active", isPrimary);

            // Deleted
            const isDeleted = deleted.includes(url);
            item.classList.toggle("nc-to-delete", isDeleted);
            item.style.opacity = isDeleted ? "0.3" : "1";
        });

        this.refreshCarousel();
        this.checkGalleryChanges();
    }

    // --- New Localized Popups ---
    showLocalizedPopup(type, title, icon, onConfirm, confirmText, content, triggerEl, confirmClass = "btn-nc-primary", hideArrow = false) {

        const customContent = `
            <div class="ut-diff-container">
                ${content}
            </div>
            <div class="ut-modal-actions">
                <button type="button" class="ut-popup-btn-cancel">Cancel</button>
                <button type="button" class="ut-popup-btn-confirm ${confirmClass}">${confirmText}</button>
            </div>
        `;

        window.showSidePopup(
            triggerEl,
            title,
            async () => {
                // Handle processing state if needed
                const popup = document.querySelector(".ut-side-popup[style*='visibility: visible']");
                const confirmBtn = popup?.querySelector(".ut-popup-btn-confirm");
                if (confirmBtn) {
                    confirmBtn.innerHTML = '<i class="fa fa-spinner fa-spin me-2"></i> Processing...';
                    confirmBtn.disabled = true;
                }
                await onConfirm();
                // window.closePopups() is called by showSidePopup after this
            },
            icon,
            confirmText,
            "var(--nc-primary)",
            customContent,
            confirmClass,
            "nc-localized-popup",
            hideArrow
        );

    }



    revertGallery() {
        this.changedFields.delete("Images");
        this.changedFields.delete("PrimaryImage");
        document.getElementById("primaryImageInput").value = this.originalState.images.primary;
        document.getElementById("imageOrderInput").value = this.originalState.images.order;
        document.getElementById("deletedImagesInput").value = this.originalState.images.deleted;
        this.syncGalleryWithInputs();
        this.saveToLocalStorage();
    }

    closeLocalizedPopups() {
        window.closePopups();
    }

    handleRowRevert(btn, id, isGallery = false) {
        if (isGallery) {
            this.revertGallery();
        } else {
            this.revertField(id, true);
        }

        const row = btn.closest("tr");
        const tbody = row?.parentNode;
        if (row) row.remove();

        // If no more changes in diff table, close popup
        if (tbody && tbody.querySelectorAll("tr").length === 0) {
            this.closeLocalizedPopups();
        }
    }





    showRevertPopup(field, label, el) {
        const input = this.form.querySelector(`[name="${field}"]`);
        if (!input) return;

        const originalVal = this.originalState.fields[field] || "(Empty)";
        const currentVal = input.type === "checkbox" ? (input.checked ? "Featured" : "Not Featured") : input.value || "(Empty)";

        let diffHtml = `
            <table class="ut-diff-table">
                <tr><th>Field</th><th>Original</th><th>Current</th></tr>
                <tr>
                    <td class="ut-diff-field-cell">${label}</td>
                    <td class="ut-diff-new">${originalVal}</td>
                    <td class="ut-diff-old">${currentVal}</td>
                </tr>
            </table>
        `;

        this.showLocalizedPopup(
            "clear",
            `Revert ${label}?`,
            "fa-undo",
            async () => {
                this.revertField(field);
            },
            "Revert Field",
            diffHtml,
            el,
            "btn-danger"
        );

    }

    revertField(field, refresh = true) {
        const input = this.form.querySelector(`[name="${field}"]`);
        if (!input) return;

        const initial = this.originalState.fields[field];
        if (input.type === "checkbox") {
            input.checked = initial === "true" || initial === true;
        } else {
            input.value = initial || "";
        }

        this.handleInputChange(input);
        const wrapper = input.closest(".nc-editable-wrapper");
        if (wrapper) this.updateSpan(wrapper);
        
        if (refresh) {
            this.checkChanges();
            this.saveToLocalStorage();
        }
    }


    renderDiffTable(isRevert = false) {
        const diff = this.calculateDiff(isRevert);
        if (diff.length === 0) return "<p class='text-center py-3 opacity-50'>No changes detected.</p>";

        let html = '<div class="ut-diff-table-wrapper"><table class="ut-diff-table">';
        html += `<thead><tr><th>Property</th><th>Original</th><th>Current</th>${isRevert ? "<th></th>" : ""}</tr></thead>`;
        html += "<tbody>";

        diff.forEach((d) => {

            if (d.type === "gallery") {
                html += `<tr><td>${d.field}</td><td colspan="2">`;

                // Show Gallery Changes specifically
                const allUrls = Array.from(new Set([...d.oldOrder, ...d.newOrder]));
                allUrls.forEach((url, idx) => {
                    const oldIdx = d.oldOrder.indexOf(url);
                    const newIdx = d.newOrder.indexOf(url);
                    const isRemoved = newIdx === -1;
                    const isAdded = oldIdx === -1;
                    const isMoved = !isRemoved && !isAdded && oldIdx !== newIdx;
                    const isPrimary = url === d.newPrimary;
                    const wasPrimary = url === d.oldPrimary;
                    const primaryChanged = isPrimary !== wasPrimary;

                    if (isRemoved || isAdded || isMoved || primaryChanged) {
                        html += `
                            <div class="ut-gallery-diff-item ${isRemoved ? "opacity-30" : ""}">
                                <img src="${url}" class="ut-gallery-diff-img" />
                                <div class="ut-gallery-diff-info">
                                    <div class="d-flex justify-content-between align-items-center">
                                        <span class="ut-gallery-diff-pos">
                                            ${isAdded ? "New Image" : isRemoved ? "Removed" : `<i class="fa fa-arrows-alt-h mx-1"></i> ${oldIdx + 1} → ${newIdx + 1}`}
                                        </span>
                                        ${isPrimary ? '<span class="ut-gallery-diff-tag">Thumbnail</span>' : ""}
                                    </div>
                                    <div class="opacity-50" style="font-size: 0.6rem; word-break: break-all;">${url.split("/").pop()}</div>
                                </div>
                            </div>
                        `;
                    }
                });

                html += "</td>";
                html += isRevert ? `<td><button type="button" class="ut-revert-cell-btn" title="Revert Gallery" onclick="editor.handleRowRevert(this, 'Images', true)"><i class="fa fa-undo"></i></button></td>` : "";
                html += "</tr>";


            } else {
                html += `
                    <tr>
                        <td class="ut-diff-field-cell">${d.field}</td>
                        <td class="ut-diff-old">${d.old}</td>
                        <td class="ut-diff-new">${d.new}</td>
                        ${isRevert ? `<td><button type="button" class="ut-revert-cell-btn" title="Revert" onclick="editor.handleRowRevert(this, '${d.id}', false)"><i class="fa fa-undo"></i></button></td>` : ""}
                    </tr>

                `;
            }
        });

        html += "</tbody></table></div>";
        return html;
    }

    calculateDiff(isRevert = false) {
        const diff = [];
        const labels = {
            Name: "Product Name",
            Price: "Price",
            Description: "Description",
            BrandId: "Brand",
            CategoryId: "Category",
            IsBanner: "Featured Status",
            BannerDescription: "Banner Text",
            BannerImageUrl: "Banner Image URL",
        };

        this.changedFields.forEach((field) => {
            if (field === "Images" || field === "PrimaryImage" || field === "imageOrder") return; // Handled separately

            const input = this.form.querySelector(`[name="${field}"]`);
            if (input) {
                const label = labels[field] || field;
                let originalVal = this.originalState.fields[field] || "(None)";
                let currentVal = input.type === "checkbox" ? (input.checked ? "True" : "False") : input.value || "(Empty)";

                if (field === "Price") {
                    originalVal = "₱" + parseFloat(originalVal || 0).toLocaleString(undefined, { minimumFractionDigits: 2 });
                    currentVal = "₱" + parseFloat(currentVal || 0).toLocaleString(undefined, { minimumFractionDigits: 2 });
                }

                diff.push({
                    id: field,
                    field: label,
                    old: isRevert ? currentVal : originalVal,
                    new: isRevert ? originalVal : currentVal,
                });
            }
        });

        if (this.changedFields.has("Images") || this.changedFields.has("PrimaryImage")) {
            const oldOrder = (this.originalState.images.order || "").split(",").filter((x) => x);
            const newOrder = (document.getElementById("imageOrderInput").value || "").split(",").filter((x) => x);
            const oldPrimary = this.originalState.images.primary;
            const newPrimary = document.getElementById("primaryImageInput").value;

            diff.push({
                field: "Gallery Changes",
                type: "gallery",
                oldOrder: isRevert ? newOrder : oldOrder,
                newOrder: isRevert ? oldOrder : newOrder,
                oldPrimary: isRevert ? newPrimary : oldPrimary,
                newPrimary: isRevert ? oldPrimary : newPrimary,
            });
        }

        return diff;
    }

    clearChanges(triggerEl) {
        if (this.changedFields.size === 0) {
            window.showToast("No changes to clear.", "info");
            return;
        }


        const content = this.renderDiffTable(true);
        this.showLocalizedPopup(
            "clear",
            "Revert All Changes?",
            "fa-undo",
            async () => {
                localStorage.removeItem(this.storageKey);
                location.reload();
            },
            "Revert All",
            content,
            triggerEl,
            "btn-danger",
            true
        );

    }

    async saveChanges(triggerEl) {

        if (!this.validateForm()) return;

        if (this.changedFields.size === 0) {
            window.showToast("No changes to save.", "info");
            return;
        }

        const content = this.renderDiffTable(false);
        this.showLocalizedPopup(
            "save",
            "Confirm Product Updates",
            "fa-save",
            async () => {
                try {
                    const formData = new FormData(this.form);
                    const response = await fetch(`/Products/UpdateDetails/${this.productId}`, {
                        method: "POST",
                        body: formData,
                        headers: { "X-Requested-With": "XMLHttpRequest" },
                    });
                    const result = await response.json();
                    if (result.success) {
                        localStorage.removeItem(this.storageKey);
                        window.showToast(result.message || "Product updated successfully.");
                        setTimeout(() => location.reload(), 1000);
                    } else {
                        let errorMsg = result.message || "Save failed.";
                        if (result.errors && result.errors.length > 0) {
                            errorMsg += " " + result.errors.join(" ");
                        }
                        window.showToast(errorMsg, "error");
                    }
                } catch (err) {
                    window.showToast("An error occurred during save.", "error");
                }
            },
            "Apply Changes",
            content,
            triggerEl,
            "btn-nc-primary",
            true
        );

    }
}

// Global Fullscreen Helper
window.openFullscreen = function (url) {
    const overlay = document.getElementById("fullscreenOverlay");
    const img = document.getElementById("fullscreenImg");
    if (overlay && img) {
        img.src = url;
        overlay.style.display = "flex";
        document.body.style.overflow = "hidden";
    }
};

window.closeFullscreen = function () {
    const overlay = document.getElementById("fullscreenOverlay");
    if (overlay) {
        overlay.style.display = "none";
        document.body.style.overflow = "";
    }
};

// Initialize
document.addEventListener("DOMContentLoaded", () => {
    window.editor = new ProductEditor("productDetailContainer");
});

class ProductEditor {
    constructor(containerId) {
        this.container = document.getElementById(containerId);
        if (!this.container) return;

        this.productId = this.container.dataset.productId;
        this.isCreate = this.container.dataset.isCreate === "true";
        this.form = document.getElementById("inPlaceEditForm");
        this.storageKey = this.isCreate ? "product-create" : `product-edit-${this.productId}`;

        this.originalState = this.isCreate
            ? { fields: {}, images: { order: "", deleted: "", bannerUrl: "" } }
            : this.captureState();
        this.changedFields = new Set();
        this.suppressTracking = false;
        this.isInfiniteScrolling = false;
        this.currentImgIndex = 0;
        this.dragSrcGalleryEl = null;

        this.init();

        // Disable browser's automatic scroll restoration to previous positions
        if ("scrollRestoration" in history) {
            history.scrollRestoration = "manual";
        }

        // Ensure reset to top on load/init to avoid weird browser scroll memory or focus jumps.
        // Using a slightly longer timeout (100ms) to ensure it runs AFTER the browser
        // has finished the initial layout, images have started loading, and autofocus is handled.
        setTimeout(() => {
            window.scrollTo(0, 0);
            document.documentElement.scrollTop = 0;
            document.body.scrollTop = 0;
        }, 100);
    }

    init() {
        this.setupEventListeners();
        this.setupCarousel();
        this.setupDragDrop();

        this.loadFromLocalStorage();
        this.checkChanges();

        // URL State Sync
        if (this.isCreate) {
            // Create page is always in edit mode
            this.container.classList.add("nc-edit-mode");
        } else {
            if (window.location.hash === "#edit") {
                this.setEditMode(true);
            }
            window.addEventListener("hashchange", () => {
                const shouldEdit = window.location.hash === "#edit";
                if (shouldEdit !== this.container.classList.contains("nc-edit-mode")) {
                    this.setEditMode(shouldEdit);
                }
            });
        }

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
            order: document.getElementById("imageOrderInput")?.value || "",
            deleted: document.getElementById("deletedImagesInput")?.value || "",
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

            if (
                !e.target.closest(".nc-localized-popup") &&
                !e.target.closest(".nc-gutter-btn") &&
                !e.target.closest(".nc-changed-indicator")
            ) {
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

        // Global Input listener for interaction tracking
        this.form.addEventListener("input", (e) => {
            if (e.target.tagName === "INPUT" || e.target.tagName === "TEXTAREA" || e.target.tagName === "SELECT") {
                e.target._hasInteracted = true;
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

        if (input.tagName === "TEXTAREA") {
            this.autoSize(input);
            // In a CSS grid stack, the invisible span dictates the height floor.
            // By updating it immediately, the parent grid shrinks when text is deleted.
            const wrapper = input.closest(".nc-editable-wrapper");
            if (wrapper) this.updateSpan(wrapper);
        }

        const val = input.type === "checkbox" ? (input.checked ? "true" : "false") : input.value;
        this.trackFieldChange(field, val);

        this.validateField(input);
        this.saveToLocalStorage();
        this.validateCustomFieldRealtime(field, false);
    }

    trackFieldChange(field, value) {
        if (!field || this.suppressTracking) return;

        const input = this.form.querySelector(`[name="${field}"]`);
        if (!input) return;

        const initial = input.dataset.initialValue;
        let current =
            value !== undefined
                ? String(value)
                : input.type === "checkbox"
                  ? input.checked
                      ? "true"
                      : "false"
                  : String(input.value);

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
        this.validateCustomFieldRealtime(field, false);
    }

    validateField(input) {
        if (!window.ValidationEngine) return;

        const field = input.name;
        const val = input.value;

        // Fields handled by validateCustomFieldRealtime should skip base validateField logic
        // to prevent duplicate tooltips and conflicting messages.
        if (
            ["Price", "Stock", "CategoryId", "BrandId", "BannerDescription", "BannerImageUrl", "IsBanner", "Gallery"].includes(
                field,
            ) ||
            input.id === "newImageUrlInput" ||
            input.id === "bannerUrlInput"
        ) {
            return;
        }

        // Use ValidationEngine for standard data-val attributes (Name length, etc.)
        window.ValidationEngine.validate(input);
    }

    validateCustomFieldRealtime(field, isSubmit = false) {
        const knownFields = [
            "Name",
            "Price",
            "Stock",
            "CategoryId",
            "BrandId",
            "BannerDescription",
            "BannerImageUrl",
            "IsBanner",
            "Gallery",
        ];
        if (!knownFields.includes(field)) return true;

        const input = this.form.querySelector(`[name="${field}"]`) || document.getElementById(field);
        let el = input;
        if (field === "BannerImageUrl") el = document.getElementById("bannerUrlInput");

        if (el) el._hasInteracted = true;

        let isInvalid = false;
        let errMsg = "";
        let popupId = "";
        let anchorSelector = ".position-relative";

        if (field === "BannerDescription" || field === "BannerImageUrl" || field === "IsBanner") {
            const isBanner = this.form.querySelector(`[name="IsBanner"]`).checked;

            const desc = this.form.querySelector(`[name="BannerDescription"]`);
            if (desc) {
                if (isSubmit || desc._hasInteracted) {
                    let descInvalid = isBanner && !desc.value.trim();
                    if (descInvalid && isSubmit) window.showToast("Banner description is required.", "error");
                    this._applyCustomError(desc, descInvalid, "Required when featured.", "banner-desc-err", ".nc-input-edit");
                    if (descInvalid) isInvalid = true;
                } else if (!isBanner) {
                    // Always clear error if banner is unchecked, even if not interacted
                    this._applyCustomError(desc, false, "", "banner-desc-err", ".nc-input-edit");
                }
            }

            const url = document.getElementById("bannerUrlInput");
            const bDropZone = document.getElementById("bannerDropZone");
            if (url) {
                if (isSubmit || url._hasInteracted) {
                    let urlInvalid = isBanner && !url.value.trim();
                    if (urlInvalid && isSubmit) window.showToast("Banner image URL is required.", "error");
                    this._applyCustomError(url, urlInvalid, "Required when featured.", "banner-url-err", ".nc-url-input-group");
                    if (bDropZone) {
                        this._applyCustomError(
                            bDropZone,
                            urlInvalid,
                            "Required when featured.",
                            "banner-drop-err",
                            "#bannerDropZone",
                        );
                    }
                    if (urlInvalid) isInvalid = true;
                } else if (!isBanner) {
                    // Always clear error if banner is unchecked
                    this._applyCustomError(url, false, "", "banner-url-err", ".nc-url-input-group");
                    if (bDropZone) this._applyCustomError(bDropZone, false, "", "banner-drop-err", "#bannerDropZone");
                }
            }
            return !isInvalid;
        }

        if (field === "Name") {
            const val = el.value.trim();
            const min = parseInt(el.getAttribute("minlength") || el.dataset.valLengthMin || "3");
            const max = parseInt(el.getAttribute("maxlength") || el.dataset.valLengthMax || "50");

            if (!val) {
                isInvalid = true;
                errMsg = el.dataset.valRequired || "Product Name is required.";
            } else if (val.length < min) {
                isInvalid = true;
                errMsg = `Name must be at least ${min} characters.`;
            } else if (val.length > max) {
                isInvalid = true;
                errMsg = `Name cannot exceed ${max} characters.`;
            }

            popupId = "name-err";
            anchorSelector = ".nc-pd-floating-group";
            if (isInvalid && isSubmit) window.showToast(errMsg, "error");
        } else if (field === "Price") {
            if (!el.value) {
                isInvalid = true;
                errMsg = "Price is required.";
            } else if (parseFloat(el.value) < 0) {
                isInvalid = true;
                errMsg = "Value cannot be negative.";
            }
            popupId = "price-err";
            anchorSelector = ".nc-input-edit";
            if (isInvalid && isSubmit) window.showToast(errMsg, "error");
        } else if (field === "Stock") {
            if (!el) return true;
            if (!el.value) {
                isInvalid = true;
                errMsg = "Stock is required.";
            } else if (parseInt(el.value) < 0) {
                isInvalid = true;
                errMsg = "Value cannot be negative.";
            }
            popupId = "stock-err";
            anchorSelector = ".nc-input-edit";
            if (isInvalid && isSubmit) window.showToast(errMsg, "error");
        } else if (field === "CategoryId") {
            el = this.container.querySelector('select[name="CategoryId"]');
            isInvalid = !el || !el.value;
            errMsg = "Category is required.";
            popupId = "cat-select-err";
            anchorSelector = ".nc-pd-category-select";
            if (isInvalid && isSubmit) window.showToast(errMsg, "error");
        } else if (field === "BrandId") {
            el = this.container.querySelector('select[name="BrandId"]');
            isInvalid = !el || !el.value;
            errMsg = "Brand is required.";
            popupId = "brand-select-err";
            anchorSelector = ".nc-pd-brand-select";
            if (isInvalid && isSubmit) window.showToast(errMsg, "error");
        } else if (field === "Gallery") {
            const activeImages = this.container.querySelectorAll(".nc-gallery-item:not(.nc-to-delete)");
            isInvalid = activeImages.length === 0;
            if (isInvalid) {
                if (isSubmit) window.showToast("At least one product image is required.", "error");

                const dropZone = this.container.querySelector("#dropZone");
                if (dropZone) {
                    if (isSubmit || dropZone._hasInteracted) {
                        this._applyCustomError(
                            dropZone,
                            true,
                            "At least one product image is required.",
                            "img-drop-err",
                            "#dropZone",
                        );
                        const newUrlInput = this.container.querySelector("#newImageUrlInput");
                        if (newUrlInput) {
                            this._applyCustomError(
                                newUrlInput,
                                true,
                                "At least one product image is required.",
                                "img-url-err",
                                ".nc-url-input-group",
                            );
                        }
                    }
                }
                return false;
            } else {
                const dropZone = this.container.querySelector("#dropZone");
                if (dropZone) {
                    this._applyCustomError(dropZone, false, "", "img-drop-err", "#dropZone");
                }
                const newUrlInput = this.container.querySelector("#newImageUrlInput");
                if (newUrlInput) {
                    this._applyCustomError(newUrlInput, false, "", "img-url-err", ".nc-url-input-group");
                }
                return true;
            }
        }

        if (el && (isSubmit || el._hasInteracted)) {
            this._applyCustomError(el, isInvalid, errMsg, popupId, anchorSelector);
        }
        return !isInvalid;
    }

    _applyCustomError(input, isInvalid, errMsg, popupId, anchorSelector) {
        if (!input) return;

        // Clear any existing autohide timer for this specific input
        if (input._invalidTimer) {
            clearTimeout(input._invalidTimer);
            input._invalidTimer = null;
        }

        const anchor = anchorSelector ? input.closest(anchorSelector) || input.parentElement : input.parentElement;
        if (!anchor) return;

        const pid = popupId || input.id + "-err";
        let existing = document.getElementById(pid);

        input.classList.toggle("is-invalid", isInvalid);
        anchor.classList.toggle("is-invalid", isInvalid);

        if (isInvalid) {
            if (!existing) {
                existing = document.createElement("div");
                existing.id = pid;
                existing.className = "nc-error-popup nc-error-popup-portal";
                document.body.appendChild(existing);
            }
            existing.innerText = errMsg;

            // Positioning Logic: Use Absolute Body Coordinates
            const rect = anchor.getBoundingClientRect();
            const scrollY = window.pageYOffset || document.documentElement.scrollTop;
            const scrollX = window.pageXOffset || document.documentElement.scrollLeft;

            // Set styles with high priority to ensure they aren't hijacked
            existing.style.setProperty("left", `${rect.left + scrollX + rect.width / 2}px`, "important");

            if (pid === "img-drop-err" || pid.includes("inside")) {
                existing.style.setProperty("top", `${rect.top + scrollY + rect.height / 2}px`, "important");
                existing.classList.add("nc-error-popup--inside");
            } else {
                existing.style.setProperty("top", `${rect.top + scrollY}px`, "important");
                existing.classList.remove("nc-error-popup--inside");
            }

            // Force reflow and show
            existing.offsetHeight;
            existing.classList.add("show");

            // Hover Reveal Logic: Since popups are portaled to the body,
            // CSS :hover rules can't reach them. We use JS listeners instead.
            if (!anchor._hasErrorHoverListeners) {
                anchor.addEventListener("mouseenter", () => {
                    const e = document.getElementById(pid);
                    if (e && input.classList.contains("is-invalid")) e.classList.add("show");
                });
                anchor.addEventListener("mouseleave", () => {
                    const e = document.getElementById(pid);
                    if (e && !input._invalidTimer) e.classList.remove("show");
                });
                anchor._hasErrorHoverListeners = true;
            }

            // Autohide Logic: Fade out after 4 seconds
            input._invalidTimer = setTimeout(() => {
                const e = document.getElementById(pid);
                if (e) e.classList.remove("show");
                input._invalidTimer = null;
            }, 4000);
        } else if (existing) {
            existing.classList.remove("show");
            // Delay removal from body to allow transition to finish
            setTimeout(() => {
                if (existing && !existing.classList.contains("show")) {
                    const e = document.getElementById(pid);
                    if (e) e.remove();
                }
            }, 500);
        }
    }

    validateForm() {
        let isValid = true;

        // Run all custom field validations — each one applies its own popup via _applyCustomError
        const allCustomFields = [
            "Gallery",
            "Name",
            "Price",
            "Stock",
            "CategoryId",
            "BrandId",
            "BannerDescription",
            "BannerImageUrl",
        ];
        allCustomFields.forEach((f) => {
            if (!this.validateCustomFieldRealtime(f, true)) isValid = false;
        });

        // Flash anchors for custom-field popups that are now in the DOM.
        // Each entry maps a known popup id → its containing anchor selector/element.
        const knownPopups = [
            { popupId: "name-err", anchorSel: ".nc-pd-floating-group" },
            { popupId: "price-err", anchorSel: ".nc-input-edit" },
            { popupId: "stock-err", anchorSel: ".nc-input-edit" },
            { popupId: "cat-select-err", anchorSel: ".nc-pd-category-select-wrapper" },
            { popupId: "brand-select-err", anchorSel: ".nc-input-edit" },
            { popupId: "img-drop-err", anchorEl: document.getElementById("dropZone") },
            { popupId: "img-url-err", anchorSel: ".nc-url-input-group" },
        ];
        knownPopups.forEach(({ popupId, anchorSel, anchorEl }) => {
            const popup = document.getElementById(popupId);
            if (!popup) return; // not in DOM means no error for this field
            const anchor = anchorEl || (anchorSel ? popup.closest(anchorSel) || this.container.querySelector(anchorSel) : null);
            if (!anchor) return;
            anchor.classList.add("nc-show-all-errors");
            setTimeout(() => anchor.classList.remove("nc-show-all-errors"), 4000);
        });

        // Also run ValidationEngine for standard floating inputs (Name length, email, etc.)
        this.form.querySelectorAll("input, select, textarea").forEach((input) => {
            this.validateField(input);
            if (input.classList.contains("is-invalid")) {
                isValid = false;
                const wrapper = input.closest(".nc-pd-floating-group, nc-floating-input, .nc-floating-group");
                if (wrapper) {
                    wrapper.classList.add("nc-show-all-errors");
                    setTimeout(() => wrapper.classList.remove("nc-show-all-errors"), 4000);
                }
            }
        });

        if (!isValid) {
            window.showToast("Please fix the validation errors before saving.", "error");
        }

        return isValid;
    }

    checkChanges() {
        if (this.isCreate) return;
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
            gutterSave.disabled = count === 0;
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
                el._hasInteracted = true;

                // Smart online image drag-and-drop URL extraction
                const plainUrl = e.dataTransfer.getData("text/uri-list") || e.dataTransfer.getData("text/plain");
                const htmlStr = e.dataTransfer.getData("text/html");
                let extractedUrl = "";

                if (htmlStr) {
                    // Extract src attribute from dropped HTML image tag
                    const match = htmlStr.match(/src\s*=\s*"([^"]+)"/i);
                    if (match && match[1]) {
                        extractedUrl = match[1];
                    }
                }

                if (!extractedUrl && plainUrl && (plainUrl.startsWith("http") || plainUrl.startsWith("data:image"))) {
                    extractedUrl = plainUrl;
                }

                if (extractedUrl) {
                    if (id === "dropZone") {
                        this.appendTempImage(extractedUrl);
                    } else if (id === "bannerDropZone") {
                        document.getElementById("bannerUrlInput").value = extractedUrl;
                        this.handleInputChange(document.getElementById("bannerUrlInput"));
                        const preview = document.getElementById("currentBannerPreviewEdit");
                        if (preview) {
                            preview.src = extractedUrl;
                            document.getElementById("bannerPreviewContainerEdit").style.display = "block";
                        }
                    }
                    return;
                }

                // Fallback to local files
                if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
                    if (id === "dropZone") this.handleFileSelect({ target: { files: e.dataTransfer.files } });
                    else this.handleBannerSelect({ target: { files: e.dataTransfer.files } });
                }
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
            const dropZone = document.getElementById("dropZone");
            if (dropZone) dropZone._hasInteracted = true;
            this.updateImageOrder();
            this.changedFields.add("Images");
            this.checkChanges();
        });
    }

    handleFileSelect(e) {
        const files = e.target.files;
        if (!files) return;

        const dropZone = document.getElementById("dropZone");
        if (dropZone) dropZone._hasInteracted = true;

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
            ${hiddenInput}
            <div class="nc-editable nc-item-actions">
                <button type="button" class="nc-item-btn btn-edit" onclick="editor.editImageUrl(this)" title="Edit URL"><i class="fa fa-link"></i></button>
                <button type="button" class="nc-item-btn btn-danger" onclick="editor.removeTempImage(this)"><i class="fa fa-trash"></i></button>
            </div>
        `;

        gallery.appendChild(item);
        this.setupGalleryDragAndDrop(item);
        this.updateImageOrder();
        this.refreshCarousel();
        this.changedFields.add("Images");
        this.checkChanges();
        this.saveToLocalStorage();
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
        if (!url) {
            window.showToast("Please enter a valid image URL.", "error");
            this._applyCustomError(input, true, "Please enter a valid image URL.", "img-url-err", ".nc-url-input-group");
            return;
        }

        const dropZone = document.getElementById("dropZone");
        if (dropZone) dropZone._hasInteracted = true;

        this.appendTempImage(url);
        input.value = "";
    }

    editImageUrl(btn) {
        if (!btn) return;
        const item = btn.closest(".nc-gallery-item");
        if (!item) return;

        const img = item.querySelector("img");
        if (!img) return;

        const url = img.src;

        const modalEl = document.getElementById("editImageUrlModal");
        if (!modalEl) {
            console.error("[ProductEditor] editImageUrlModal not found!");
            return;
        }

        const input = modalEl.querySelector("#editImageUrlInput");
        if (input) input.value = url;

        // Cleanup previous listeners to prevent multiple triggers
        const confirmBtn = modalEl.querySelector("#confirmEditImageUrl");
        const newConfirmBtn = confirmBtn.cloneNode(true);
        confirmBtn.parentNode.replaceChild(newConfirmBtn, confirmBtn);

        const modal = new bootstrap.Modal(modalEl);

        newConfirmBtn.addEventListener("click", async () => {
            const newUrl = input.value?.trim();
            if (!newUrl) {
                if (window.showToast) window.showToast("Please enter a valid URL", "error");
                return;
            }

            // Update the image and the item data
            img.src = newUrl;
            item.dataset.url = newUrl;

            // Sync with backend hidden inputs
            // We search for ANY hidden input that might be storing the URL for this item
            const existingInput = item.querySelector('input[name="existingImages"]');
            const newUrlInput = item.querySelector('input[name="imageUrls"]');
            
            if (existingInput) {
                console.log("[ProductEditor] Updating existingImages input value");
                existingInput.value = newUrl;
            } else if (newUrlInput) {
                console.log("[ProductEditor] Updating imageUrls input value");
                newUrlInput.value = newUrl;
            } else {
                console.log("[ProductEditor] Creating new imageUrls input");
                const newInput = document.createElement("input");
                newInput.type = "hidden";
                newInput.name = "imageUrls";
                newInput.value = newUrl;
                item.appendChild(newInput);
            }

            // Sync state
            this.updateImageOrder();
            this.refreshCarousel();
            this.changedFields.add("Images");
            this.checkChanges();
            this.saveToLocalStorage();

            modal.hide();
            if (window.showToast) window.showToast("Image URL updated", "success");
        });

        modal.show();
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
        const urls = Array.from(document.querySelectorAll(".nc-image-gallery .nc-gallery-item:not(.nc-to-delete)")).map(
            (it) => it.dataset.url,
        );
        document.getElementById("imageOrderInput").value = urls.join("|");
        this.checkGalleryChanges();
        this.saveToLocalStorage();
        this.validateCustomFieldRealtime("Gallery", false);
    }

    checkGalleryChanges() {
        const oldOrder = (this.originalState.images.order || "").split("|").filter((x) => x);
        const newOrder = (document.getElementById("imageOrderInput").value || "").split("|").filter((x) => x);

        const orderChanged = JSON.stringify(oldOrder) !== JSON.stringify(newOrder);

        const galleryInd = document.getElementById("indicator-Gallery");
        if (orderChanged) {
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

                // Handle banner preview update
                if (name === "BannerImageUrl" && val) {
                    const preview = document.getElementById("currentBannerPreviewEdit");
                    const container = document.getElementById("bannerPreviewContainerEdit");
                    if (preview) preview.src = val;
                    if (container) container.style.display = "block";
                }
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
        const deletedValue = document.getElementById("deletedImagesInput").value;
        if (!orderValue) return;

        const gallery = document.getElementById("imageGallery");
        if (!gallery) return;

        const order = orderValue.split("|").filter((x) => x);
        const deleted = JSON.parse(deletedValue || "[]");

        // Unhide container if we have images
        const galleryContainer = document.getElementById("galleryContainer");
        if (galleryContainer && order.length > 0) {
            galleryContainer.classList.remove("d-none");
        }

        // 1. Identify missing items and create them
        const existingUrls = Array.from(gallery.querySelectorAll(".nc-gallery-item")).map((it) => it.dataset.url);
        order.forEach((url) => {
            if (!existingUrls.includes(url)) {
                // Determine if it was a remote URL or a data URL (local drop)
                const isRemote = url.startsWith("http") || url.startsWith("/");
                const hiddenInput = isRemote ? `<input type="hidden" name="imageUrls" value="${url}" />` : "";

                const item = document.createElement("div");
                item.className = "nc-gallery-item";
                item.dataset.url = url;

                // For newly added items being re-synced, deletion should always be total removal
                const removeAction = "editor.removeTempImage(this)";

                item.innerHTML = `
                    <img src="${url}" alt="Image" />
                    ${hiddenInput}
                    <div class="nc-editable nc-item-actions">
                        <button type="button" class="nc-item-btn btn-edit" onclick="editor.editImageUrl(this)" title="Edit URL"><i class="fa fa-link"></i></button>
                        <button type="button" class="nc-item-btn btn-danger" onclick="editor.removeTempImage(this)" title="Remove"><i class="fa fa-trash"></i></button>
                    </div>
                `;
                gallery.appendChild(item);
                this.setupGalleryDragAndDrop(item);
            }
        });

        // 2. Re-sort gallery items in DOM
        const items = Array.from(gallery.querySelectorAll(".nc-gallery-item"));
        items.sort((a, b) => {
            const idxA = order.indexOf(a.dataset.url);
            const idxB = order.indexOf(b.dataset.url);
            if (idxA === -1 && idxB === -1) return 0;
            if (idxA === -1) return 1;
            if (idxB === -1) return -1;
            return idxA - idxB;
        });
        items.forEach((item) => gallery.appendChild(item));

        // 3. Update classes (active/deleted)
        gallery.querySelectorAll(".nc-gallery-item").forEach((item) => {
            const url = item.dataset.url;

            // Only gray out if ORINGIAL image from backend is deleted.
            // New images should just be removed from DOM (not in deleted list anyway usually)
            const isOriginal = (this.originalState.images.order || "").split("|").includes(url);
            const isDeleted = isOriginal && deleted.includes(url);

            item.classList.toggle("nc-to-delete", isDeleted);
            item.style.opacity = isDeleted ? "0.3" : "1";
        });

        this.refreshCarousel();
        this.checkGalleryChanges();
    }

    // --- New Localized Popups ---
    showLocalizedPopup(
        type,
        title,
        icon,
        onConfirm,
        confirmText,
        content,
        triggerEl,
        confirmClass = "btn-nc-primary",
        hideArrow = false,
    ) {
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
            hideArrow,
        );
    }

    revertGallery() {
        this.changedFields.delete("Images");
        this._galleryOrderChanged = false;
        this._galleryThumbnailChanged = false;
        document.getElementById("imageOrderInput").value = this.originalState.images.order;
        document.getElementById("deletedImagesInput").value = this.originalState.images.deleted;
        this.syncGalleryWithInputs();
        this.saveToLocalStorage();
        this.validateCustomFieldRealtime(field, false);
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

    forceSubmitCreate() {
        if (!this.validateForm()) return;

        // Ensure image order is synced before submit
        this.updateImageOrder();

        const btn = document.getElementById("forceSaveEditorButton");
        if (btn) {
            btn.innerHTML = '<i class="fa fa-spinner fa-spin me-2"></i> Creating...';
            btn.disabled = true;
        }

        this.form.submit();
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
            "btn-danger",
        );
    }

    revertField(field, refresh = true) {
        const input = this.form.querySelector(`[name="${field}"]`);
        if (!input) return;

        const initial = this.originalState.fields[field];

        this.suppressTracking = true;
        if (input.type === "checkbox") {
            input.checked = initial === "true" || initial === true;
        } else {
            input.value = initial || "";
        }

        this.handleInputChange(input);
        const wrapper = input.closest(".nc-editable-wrapper");
        if (wrapper) this.updateSpan(wrapper);
        this.suppressTracking = false;

        if (refresh) {
            // Reset interaction state on revert so error doesn't immediately show if original is also "invalid" (like 0)
            input._hasInteracted = false;

            this.checkChanges();
            this.saveToLocalStorage();

            // Explicitly clear errors on revert by forcing an isValid=false call to the custom error applier
            // We use the same anchor logic as validateCustomFieldRealtime
            let anchor = ".nc-input-edit";
            let popId = "";
            if (field === "Price") popId = "price-err";
            else if (field === "Stock") popId = "stock-err";
            else if (field === "CategoryId") {
                popId = "cat-select-err";
                anchor = ".nc-pd-category-select-wrapper";
            } else if (field === "BrandId") {
                popId = "brand-select-err";
                anchor = ".nc-input-edit";
            } else if (field === "BannerDescription") {
                popId = "banner-desc-err";
                anchor = ".nc-input-edit";
            } else if (field === "BannerImageUrl") {
                popId = "banner-url-err";
                anchor = ".nc-url-input-group";
            }

            this._applyCustomError(input, false, "", popId, anchor);
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
                html += `<tr><td class="ut-diff-field-cell">${d.field}</td>`;

                // Render Original Gallery
                let oldHtml = '<div class="d-flex flex-wrap gap-2">';
                if (d.oldOrder.length === 0) {
                    oldHtml += '<span class="opacity-50" style="font-size: 0.85rem;">Empty</span>';
                }
                d.oldOrder.forEach((url, idx) => {
                    oldHtml += `
                        <div class="position-relative" style="width: 60px; height: 60px; border-radius: 6px; overflow: hidden; border: 1px solid var(--nc-border);">
                            <img src="${url}" class="w-100 h-100" style="object-fit: cover;" />
                            <div class="position-absolute top-0 start-0 bg-dark text-white px-1" style="font-size: 0.65rem; border-bottom-right-radius: 4px; opacity: 0.9">${idx + 1}</div>
                        </div>
                    `;
                });
                oldHtml += "</div>";

                // Render Current Gallery including removed ones but marked
                let newHtml = '<div class="d-flex flex-wrap gap-2">';
                if (d.newOrder.length === 0 && d.oldOrder.length === 0) {
                    newHtml += '<span class="opacity-50" style="font-size: 0.85rem;">Empty</span>';
                }

                // Show new/reordered
                d.newOrder.forEach((url, idx) => {
                    const isNew = !d.oldOrder.includes(url);
                    const oldIdx = d.oldOrder.indexOf(url);
                    const isMoved = !isNew && oldIdx !== idx;

                    let borderStyle = "1px solid var(--nc-border)";
                    if (isNew) {
                        borderStyle = "2px solid #81c784";
                    } else if (isMoved) {
                        borderStyle = "2px solid #ffb74d";
                    }

                    newHtml += `
                        <div class="position-relative" style="width: 60px; height: 60px; border-radius: 6px; overflow: hidden; border: ${borderStyle};">
                            <img src="${url}" class="w-100 h-100" style="object-fit: cover;" />
                            <div class="position-absolute top-0 start-0 bg-dark text-white px-1" style="font-size: 0.65rem; border-bottom-right-radius: 4px; opacity: 0.9">${idx + 1}</div>
                        </div>
                    `;
                });

                // Show deleted images
                d.oldOrder.forEach((url) => {
                    if (!d.newOrder.includes(url)) {
                        newHtml += `
                            <div class="position-relative" style="width: 60px; height: 60px; border-radius: 6px; overflow: hidden; border: 2px solid #e57373; opacity: 0.4;">
                                <img src="${url}" class="w-100 h-100" style="object-fit: cover; filter: grayscale(100%);" />
                                <div class="position-absolute top-0 start-0 bg-danger text-white px-1" style="font-size: 0.65rem; border-bottom-right-radius: 4px; opacity: 0.9"><i class="fa fa-times"></i></div>
                            </div>
                        `;
                    }
                });
                newHtml += "</div>";

                html += `<td class="ut-diff-old" style="vertical-align: top;">${oldHtml}</td>`;
                html += `<td class="ut-diff-new" style="vertical-align: top;">${newHtml}</td>`;

                html += isRevert
                    ? `<td><button type="button" class="revert-cell-btn" title="Revert Gallery" onclick="window.editor.handleRowRevert(this, 'Images', true)"><i class="fa fa-undo"></i></button></td>`
                    : "";
                html += "</tr>";
            } else {
                html += `
                    <tr>
                        <td class="ut-diff-field-cell">${d.field}</td>
                        <td class="ut-diff-old">${d.old}</td>
                        <td class="ut-diff-new">${d.new}</td>
                        ${isRevert ? `<td><button type="button" class="revert-cell-btn" title="Revert" onclick="window.editor.handleRowRevert(this, '${d.id}', false)"><i class="fa fa-undo"></i></button></td>` : ""}
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
            if (field === "Images" || field === "imageOrder") return;

            const input = this.form.querySelector(`[name="${field}"]`);
            if (input) {
                const label = labels[field] || field;
                let originalVal = this.originalState.fields[field] || "(None)";
                let currentVal = input.type === "checkbox" ? (input.checked ? "True" : "False") : input.value || "(Empty)";

                if (field === "Price") {
                    const currency = window.ncConfig?.currency;
                    originalVal = currency + parseFloat(originalVal || 0).toLocaleString(undefined, { minimumFractionDigits: 2 });
                    currentVal = currency + parseFloat(currentVal || 0).toLocaleString(undefined, { minimumFractionDigits: 2 });
                }

                diff.push({
                    id: field,
                    field: label,
                    old: isRevert ? currentVal : originalVal,
                    new: isRevert ? originalVal : currentVal,
                });
            }
        });

        if (this.changedFields.has("Images")) {
            const oldOrder = (this.originalState.images.order || "").split("|").filter((x) => x);
            const newOrder = (document.getElementById("imageOrderInput").value || "").split("|").filter((x) => x);

            diff.push({
                field: "Gallery",
                type: "gallery",
                oldOrder: isRevert ? newOrder : oldOrder,
                newOrder: isRevert ? oldOrder : newOrder,
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
            true,
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
                        setTimeout(() => {
                            if (window.location.hash === "#edit") {
                                history.replaceState(null, null, " ");
                            }
                            location.reload();
                        }, 1000);
                    } else {
                        let errorMsg = result.message || "Save failed.";
                        if (result.errors && result.errors.length > 0) {
                            errorMsg += " " + result.errors.join(" ");
                        }
                        window.showToast(errorMsg, "error");
                    }
                } catch (err) {
                    console.error("[ProductEditor] Save Error:", err);
                    window.showToast("An error occurred during save: " + err.message, "error");
                }
            },
            "Apply Changes",
            content,
            triggerEl,
            "btn-nc-primary",
            true,
        );
    }

    async forceSubmitCreate() {
        if (!this.validateForm()) return;
        this.updateImageOrder();
        
        const btn = document.getElementById("forceSaveEditorButton") || document.querySelector(".save-modal-btn");
        let oldHtml = "";
        if (btn) {
            oldHtml = btn.innerHTML;
            btn.innerHTML = '<i class="fa fa-spinner fa-spin me-2"></i> Creating...';
            btn.disabled = true;
        }

        try {
            const formData = new FormData(this.form);
            const response = await fetch(this.form.action, {
                method: "POST",
                body: formData,
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            const result = await response.json();
            if (result.success) {
                localStorage.removeItem(this.storageKey);
                window.showToast(result.message || "Product created successfully.");
                setTimeout(() => {
                    window.location.href = `/Products/Details/${result.id}`;
                }, 1000);
            } else {
                let errorMsg = result.message || "Creation failed.";
                if (result.errors && result.errors.length > 0) {
                    errorMsg += " " + result.errors.join(" ");
                }
                window.showToast(errorMsg, "error");
                if (btn) {
                    btn.innerHTML = oldHtml;
                    btn.disabled = false;
                }
            }
        } catch (err) {
            console.error("[ProductEditor] Create Error:", err);
            window.showToast("An error occurred during creation: " + err.message, "error");
            if (btn) {
                btn.innerHTML = oldHtml;
                btn.disabled = false;
            }
        }
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

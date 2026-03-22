class ProductEditor {
    constructor(containerId) {
        this.container = document.getElementById(containerId);
        this.productId = this.container.dataset.productId;
        this.form = document.getElementById('inPlaceEditForm');
        this.saveBar = document.getElementById('editorSaveBar');
        this.storageKey = `products-${this.productId}`;
        
        this.originalState = this.captureState();
        this.currentState = JSON.parse(JSON.stringify(this.originalState));
        
        this.init();
    }

    init() {
        this.loadFromLocalStorage();
        this.setupEventListeners();
        this.setupDragAndDrop();
        this.checkChanges();
    }

    captureState() {
        const data = new FormData(this.form);
        const state = {
            fields: {},
            images: this.getCurrentImages()
        };
        for (let [key, value] of data.entries()) {
            if (key !== '__RequestVerificationToken' && key !== 'productImages' && key !== 'existingImages') {
                state.fields[key] = value;
            }
        }
        return state;
    }

    getCurrentImages() {
        return Array.from(this.container.querySelectorAll('.nc-gallery-item:not(.is-removed)')).map(item => ({
            url: item.dataset.url,
            isPrimary: item.querySelector('.btn-star').classList.contains('active')
        }));
    }

    setupEventListeners() {
        // Toggle Modes
        document.getElementById('enterEditMode').onclick = () => this.setEditMode(true);
        document.getElementById('exitEditMode').onclick = () => this.setEditMode(false);

        // Form Changes
        this.form.addEventListener('input', () => this.handleFieldChange());
        this.form.addEventListener('change', () => this.handleFieldChange());

        // Save/Clear
        document.getElementById('saveEditorChanges').onclick = () => this.saveChanges();
        document.getElementById('clearEditorChanges').onclick = () => this.clearChanges();
        
        // Image Upload
        const dropZone = document.getElementById('dropZone');
        const fileInput = document.getElementById('fileInput');
        dropZone.onclick = () => fileInput.click();
        fileInput.onchange = () => this.handleFiles(fileInput.files);
        
        dropZone.addEventListener('dragover', (e) => { e.preventDefault(); dropZone.classList.add('dragover'); });
        dropZone.addEventListener('dragleave', () => dropZone.classList.remove('dragover'));
        dropZone.addEventListener('drop', (e) => {
            e.preventDefault();
            dropZone.classList.remove('dragover');
            this.handleFiles(e.dataTransfer.files);
        });
    }

    setEditMode(active) {
        if (active) {
            this.container.classList.add('nc-edit-mode');
        } else {
            this.container.classList.remove('nc-edit-mode');
        }
    }

    handleFieldChange() {
        this.checkChanges();
        this.saveToLocalStorage();
    }

    checkChanges() {
        const currentState = this.captureState();
        let hasChanged = JSON.stringify(this.originalState) !== JSON.stringify(currentState);
        
        // Also check for new local files
        if (document.getElementById('fileInput').files.length > 0) hasChanged = true;

        this.saveBar.style.display = hasChanged ? 'block' : 'none';
        
        // Update indicators (similar to UpdatableTable)
        this.updateIndicators(currentState);
    }

    updateIndicators(current) {
        // Find inputs that differ from original
        for (const [key, val] of Object.entries(current.fields)) {
            const input = this.form.querySelector(`[name="${key}"]`);
            if (!input) continue;
            
            const isChanged = String(this.originalState.fields[key]) !== String(val);
            if (isChanged) {
                input.classList.add('ut-changed');
            } else {
                input.classList.remove('ut-changed');
            }
        }
    }

    // --- Image Management ---
    addImageUrl() {
        const input = document.getElementById('newImageUrlInput');
        const url = input.value.trim();
        if (!url) return;
        
        this.appendImagePreview(url, false);
        input.value = '';
        this.handleFieldChange();
    }

    handleFiles(files) {
        Array.from(files).forEach(file => {
            if (!file.type.startsWith('image/')) return;
            const reader = new FileReader();
            reader.onload = (e) => {
                this.appendImagePreview(e.target.result, true);
                this.handleFieldChange();
            };
            reader.readAsDataURL(file);
        });
    }

    appendImagePreview(url, isLocal) {
        const gallery = document.getElementById('imageGallery');
        const item = document.createElement('div');
        item.className = 'nc-gallery-item active';
        item.dataset.url = url;
        
        const hiddenInput = isLocal ? '' : `<input type="hidden" name="imageUrls" value="${url}" />`;
        
        item.innerHTML = `
            <img src="${url}" />
            ${hiddenInput}
            <div class="nc-editable nc-item-actions">
                <button type="button" class="nc-item-btn btn-star" onclick="editor.setPrimaryImage(this)" title="Set Primary"><i class="fa fa-star"></i></button>
                <button type="button" class="nc-item-btn btn-edit" onclick="editor.editImageUrl(this)" title="Edit URL"><i class="fa fa-pen"></i></button>
                <button type="button" class="nc-item-btn btn-danger" onclick="editor.toggleRemoveImage(this)" title="Remove"><i class="fa fa-times"></i></button>
            </div>
        `;
        
        gallery.appendChild(item);
        document.getElementById('galleryContainer').classList.remove('d-none');
        this.setupDragAndDrop();
        this.updateMainImage(url);
    }

    setPrimaryImage(btn) {
        const item = btn.closest('.nc-gallery-item');
        const url = item.dataset.url;
        
        this.container.querySelectorAll('.btn-star').forEach(b => b.classList.remove('active'));
        this.container.querySelectorAll('.nc-primary-indicator').forEach(i => i.remove());
        
        btn.classList.add('active');
        document.getElementById('primaryImageInput').value = url;
        
        const indicator = document.createElement('span');
        indicator.className = 'nc-primary-indicator nc-read-only';
        indicator.innerText = 'P';
        item.appendChild(indicator);
        
        this.handleFieldChange();
    }

    editImageUrl(btn) {
        const item = btn.closest('.nc-gallery-item');
        const oldUrl = item.dataset.url;
        
        // Use global showSidePopup but lift the whole item
        window.showSidePopup(
            item, 
            "Edit Image URL", 
            () => {
                const newUrl = document.getElementById('popupImageUrlInput').value.trim();
                if (newUrl && newUrl !== oldUrl) {
                    item.dataset.url = newUrl;
                    item.querySelector('img').src = newUrl;
                    const hidden = item.querySelector('input[name="existingImages"], input[name="imageUrls"]');
                    if (hidden) {
                        hidden.value = newUrl;
                        hidden.name = "imageUrls"; // Convert to new URL if edited
                    }
                    if (document.getElementById('primaryImageInput').value === oldUrl) {
                        document.getElementById('primaryImageInput').value = newUrl;
                    }
                    this.handleFieldChange();
                }
            },
            "fa-link",
            "Update",
            "var(--nc-primary)",
            `<div style="padding: 1rem;">
                <input type="text" id="popupImageUrlInput" class="nc-input" value="${oldUrl}" style="width: 300px;" />
            </div>`,
            "btn-nc-primary"
        );
    }

    toggleRemoveImage(btn) {
        const item = btn.closest('.nc-gallery-item');
        const url = item.dataset.url;
        const isRemoved = item.classList.toggle('is-removed');
        
        const hidden = item.querySelector('input[type="hidden"]');
        if (hidden) hidden.disabled = isRemoved;
        
        const deletedInput = document.getElementById('deletedImagesInput');
        let deleted = JSON.parse(deletedInput.value);
        
        if (isRemoved) {
            if (url.startsWith('/uploads/') && !deleted.includes(url)) deleted.push(url);
        } else {
            deleted = deleted.filter(u => u !== url);
        }
        deletedInput.value = JSON.stringify(deleted);
        
        this.handleFieldChange();
    }

    updateMainImage(url) {
        const main = document.getElementById('mainProductImage');
        main.src = url;
        this.container.querySelectorAll('.nc-gallery-item').forEach(i => {
            i.classList.toggle('active', i.dataset.url === url);
        });
    }

    // --- Reordering ---
    setupDragAndDrop() {
        const items = this.container.querySelectorAll('.nc-gallery-item');
        let dragSrcEl = null;

        items.forEach(item => {
            item.draggable = true;
            item.ondragstart = (e) => {
                if (!this.container.classList.contains('nc-edit-mode')) { e.preventDefault(); return; }
                dragSrcEl = item;
                e.dataTransfer.effectAllowed = 'move';
                item.classList.add('is-dragging');
            };
            item.ondragover = (e) => {
                e.preventDefault();
                if (dragSrcEl !== item) {
                    const container = item.parentNode;
                    const all = Array.from(container.children);
                    const srcIdx = all.indexOf(dragSrcEl);
                    const tarIdx = all.indexOf(item);
                    if (srcIdx < tarIdx) container.insertBefore(dragSrcEl, item.nextSibling);
                    else container.insertBefore(dragSrcEl, item);
                }
                return false;
            };
            item.ondragend = () => {
                item.classList.remove('is-dragging');
                this.updateOrderInput();
                this.handleFieldChange();
            };
        });
    }

    updateOrderInput() {
        const urls = Array.from(this.container.querySelectorAll('.nc-gallery-item:not(.is-removed)')).map(i => i.dataset.url);
        document.getElementById('imageOrderInput').value = urls.join(',');
    }

    // --- Local Storage ---
    saveToLocalStorage() {
        const state = this.captureState();
        localStorage.setItem(this.storageKey, JSON.stringify(state));
    }

    loadFromLocalStorage() {
        const saved = localStorage.getItem(this.storageKey);
        if (!saved) return;
        
        const state = JSON.parse(saved);
        // Apply fields
        for (const [key, val] of Object.entries(state.fields)) {
            const input = this.form.querySelector(`[name="${key}"]`);
            if (input) {
                if (input.type === 'checkbox') input.checked = val === 'true';
                else input.value = val;
            }
        }
        // Images are complex, for now let's just focus on fields for the MVP of local storage
        // Full image sync requires more logic for local blobs
    }

    clearChanges() {
        localStorage.removeItem(this.storageKey);
        location.reload(); // Simplest way to revert everything
    }

    // --- Save Logic ---
    async saveChanges() {
        const diff = this.calculateDiff();
        if (diff.length === 0 && document.getElementById('fileInput').files.length === 0) {
            window.showToast("No changes to save.", "info");
            return;
        }

        // Show Diff Modal (Custom implementation)
        this.showDiffModal(diff);
    }

    calculateDiff() {
        const current = this.captureState();
        const diff = [];
        
        for (const [key, val] of Object.entries(current.fields)) {
            if (String(this.originalState.fields[key]) !== String(val)) {
                diff.push({ field: key, old: this.originalState.fields[key], new: val });
            }
        }
        
        if (JSON.stringify(this.originalState.images) !== JSON.stringify(current.images)) {
            diff.push({ field: 'Images', old: 'Modified', new: 'Updated' });
        }
        
        return diff;
    }

    showDiffModal(diff) {
        const content = `
            <div class="ut-diff-container" style="max-height: 400px; overflow-y: auto;">
                <table class="ut-diff-table">
                    <thead><tr><th>Property</th><th>Original</th><th>New</th></tr></thead>
                    <tbody>
                        ${diff.map(d => `
                            <tr>
                                <td>${d.field}</td>
                                <td class="ut-diff-old">${d.old}</td>
                                <td class="ut-diff-new">${d.new}</td>
                            </tr>
                        `).join('')}
                    </tbody>
                </table>
            </div>
        `;

        window.showSidePopup(
            document.getElementById('saveEditorChanges'),
            "Confirm Changes",
            async () => {
                const formData = new FormData(this.form);
                const response = await fetch(`/Products/UpdateDetails/${this.productId}`, {
                    method: 'POST',
                    body: formData,
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                const result = await response.json();
                if (result.success) {
                    localStorage.removeItem(this.storageKey);
                    window.showToast(result.message, "success");
                    setTimeout(() => location.reload(), 1000);
                } else {
                    window.showToast(result.message || "Save failed.", "error");
                    if (result.errors) console.error(result.errors);
                }
            },
            "fa-save",
            "Commit Changes",
            "var(--nc-primary)",
            content,
            "btn-nc-primary",
            "ut-diff-modal"
        );
    }
}

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    window.editor = new ProductEditor('productDetailContainer');
});

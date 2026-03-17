/* Toast.js */
function showToast(message, type = 'info') {
    let container = document.querySelector('.toast-container');
    if (!container) {
        container = document.createElement('div');
        container.className = 'toast-container';
        document.body.appendChild(container);
    }

    const toast = document.createElement('div');
    toast.className = `nc-toast nc-toast-${type}`;
    
    let icon = 'fa-info-circle';
    if (type === 'success') icon = 'fa-check-circle';
    if (type === 'error') icon = 'fa-exclamation-circle';

    toast.innerHTML = `
        <i class="fa ${icon} nc-toast-icon"></i>
        <div class="nc-toast-content">${message}</div>
        <button class="nc-toast-close">&times;</button>
    `;

    container.appendChild(toast);

    // Animate in
    setTimeout(() => toast.classList.add('show'), 10);

    const closeBtn = toast.querySelector('.nc-toast-close');
    const removeToast = () => {
        toast.classList.remove('show');
        setTimeout(() => toast.remove(), 400);
    };

    closeBtn.onclick = removeToast;

    // Auto remove
    setTimeout(removeToast, 5000);
}

// Global exposure
window.showToast = showToast;

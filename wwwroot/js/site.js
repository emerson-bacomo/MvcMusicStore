// Global delete confirmation
window.confirmDelete = function(id, url, onDeleted) {
    Swal.fire({
        title: 'Are you sure?',
        text: "This action cannot be undone!",
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#e57373',
        cancelButtonColor: 'var(--nc-border)',
        confirmButtonText: 'Yes, delete it!',
        background: 'var(--nc-bg-card)',
        color: 'var(--nc-text-primary)'
    }).then((result) => {
        if (result.isConfirmed) {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                    'X-Requested-With': 'XMLHttpRequest',
                    'RequestVerificationToken': token
                },
                body: new URLSearchParams({ id: id })
            }).then(response => {
                if (response.ok) {
                    Swal.fire({
                        title: 'Deleted!',
                        text: 'Record has been deleted.',
                        icon: 'success',
                        timer: 1500,
                        showConfirmButton: false,
                        background: 'var(--nc-bg-card)',
                        color: 'var(--nc-text-primary)'
                    });
                    if (onDeleted) onDeleted();
                } else {
                    Swal.fire('Error', 'Failed to delete record.', 'error');
                }
            });
        }
    });
};

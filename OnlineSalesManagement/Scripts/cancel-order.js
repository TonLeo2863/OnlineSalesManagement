(function () {
    'use strict';
    var element = document.getElementById('cancelOrderModal');
    if (!element) return;
    var pending = null, sending = false, modal = new bootstrap.Modal(element);
    var confirm = document.getElementById('confirmCancelOrder');
    document.querySelectorAll('[data-cancel-order]').forEach(function (form) {
        form.addEventListener('submit', function (event) {
            event.preventDefault();
            if (sending) return;
            pending = form;
            document.getElementById('cancelOrderCode').textContent = form.dataset.cancelOrder;
            confirm.disabled = false;
            modal.show();
        });
    });
    element.addEventListener('hidden.bs.modal', function () { if (!sending) pending = null; });
    confirm.addEventListener('click', function () {
        if (!pending || sending) return;
        sending = true; confirm.disabled = true;
        pending.querySelector('button[type="submit"]').disabled = true;
        pending.submit();
    });
})();

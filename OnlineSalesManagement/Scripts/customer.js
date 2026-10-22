(function () {
    'use strict';
    var busy = false;
    var cartRevision = 0;
    var toastElement = document.getElementById('shopToast');
    function notify(message, error) {
        if (!toastElement) return;
        document.getElementById('shopToastMessage').textContent = message;
        toastElement.classList.toggle('shop-toast-error', !!error);
        toastElement.setAttribute('role', error ? 'alert' : 'status');
        if (window.bootstrap && window.bootstrap.Toast) {
            window.bootstrap.Toast.getOrCreateInstance(toastElement, { delay: 3000, autohide: true }).show();
        } else { toastElement.classList.add('show'); }
    }
    function syncCart(data) {
        if (typeof data.version === 'string') {
            cartRevision++;
            document.querySelectorAll('[data-add-cart] input[name="version"]').forEach(function (input) { input.value = data.version; });
        }
        if (Number.isInteger(data.count) && data.count >= 0) {
            document.querySelectorAll('[data-cart-count]').forEach(function (badge) {
                badge.textContent = data.count;
                badge.setAttribute('aria-label', data.count + ' sản phẩm trong giỏ');
            });
        }
    }
    function fallbackImage(image) {
        if (image.dataset.productImage === undefined || !image.dataset.placeholder) return;
        if (image.getAttribute('src') !== image.dataset.placeholder) image.setAttribute('src', image.dataset.placeholder);
    }
    document.addEventListener('error', function (event) {
        if (event.target.tagName === 'IMG') fallbackImage(event.target);
    }, true);
    document.querySelectorAll('img[data-product-image]').forEach(function (image) {
        if (image.complete && image.naturalWidth === 0) fallbackImage(image);
    });
    document.addEventListener('submit', async function (event) {
        var form = event.target;
        if (!form.matches('[data-add-cart]')) return;
        event.preventDefault();
        if (busy || !form.reportValidity()) return;
        busy = true;
        var buttons = Array.from(document.querySelectorAll('[data-add-cart] button[type="submit"]'));
        var previous = buttons.map(function (button) { return button.disabled; });
        buttons.forEach(function (button) { button.disabled = true; });
        form.setAttribute('aria-busy', 'true');
        var controller = new AbortController();
        var timeout = setTimeout(function () { controller.abort(); }, 30000);
        try {
            var response = await fetch(form.action, {
                method: 'POST', credentials: 'same-origin', signal: controller.signal,
                headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
                body: new URLSearchParams(new FormData(form))
            });
            if (response.redirected || response.status === 401) throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại để mua hàng.');
            if (!(response.headers.get('content-type') || '').includes('application/json'))
                throw new Error('Phiên biểu mẫu không còn hợp lệ. Vui lòng tải lại trang và thử lại.');
            var data = await response.json();
            syncCart(data);
            if (!response.ok || !data.success) throw new Error(data.message || 'Chưa thể thêm sản phẩm. Vui lòng kiểm tra giỏ hàng.');
            notify(data.message || ('Đã thêm ' + data.productName + ' vào giỏ hàng'), false);
        } catch (error) {
            notify(error.name === 'TypeError' || error.name === 'AbortError'
                ? 'Chưa nhận được xác nhận từ máy chủ. Vui lòng kiểm tra giỏ hàng trước khi thử lại.'
                : error.message, true);
        } finally {
            clearTimeout(timeout);
            buttons.forEach(function (button, index) {
                button.disabled = button.id === 'detailAdd' ? button.dataset.available !== 'true' : previous[index];
            });
            form.removeAttribute('aria-busy');
            busy = false;
        }
    });
    var modal = document.getElementById('productModal');
    if (modal) {
        var detailRequest;
        var detailGeneration = 0;
        var status = document.getElementById('productModalStatus');
        var content = document.getElementById('productModalContent');
        var add = document.getElementById('detailAdd');
        var quantity = document.getElementById('detailQuantity');
        modal.addEventListener('show.bs.modal', async function (event) {
            var trigger = event.relatedTarget;
            if (!trigger || !trigger.dataset.detailsUrl) return;
            var generation = ++detailGeneration;
            var cartRevisionAtRequest = cartRevision;
            if (detailRequest) detailRequest.abort();
            detailRequest = new AbortController();
            content.hidden = true;
            add.disabled = true;
            add.dataset.available = 'false';
            quantity.value = 1;
            status.textContent = 'Đang tải sản phẩm…';
            try {
                var response = await fetch(trigger.dataset.detailsUrl, { credentials: 'same-origin', signal: detailRequest.signal, headers: { 'Accept': 'application/json' } });
                if (!(response.headers.get('content-type') || '').includes('application/json')) throw new Error('Không thể tải chi tiết. Vui lòng tải lại trang.');
                var data = await response.json();
                if (generation !== detailGeneration) return;
                if (!response.ok || !data.success) throw new Error(data.message || 'Sản phẩm không còn kinh doanh.');
                var p = data.product;
                ['Name', 'Sku', 'Category', 'Unit', 'Description', 'Price'].forEach(function (field) {
                    document.getElementById('detail' + field).textContent = p[field.toLowerCase()] || (field === 'Description' ? 'Chưa có mô tả cho sản phẩm này.' : '');
                });
                var image = document.getElementById('detailImage');
                image.alt = p.name;
                image.src = p.imageUrl;
                document.getElementById('detailProductId').value = p.id;
                document.getElementById('detailStock').textContent = p.stock > 0 ? 'Số lượng còn lại: ' + p.stock : 'Hết hàng';
                quantity.max = Math.min(1000, Math.max(1, p.stock));
                quantity.disabled = p.stock <= 0 || !data.signedIn;
                add.dataset.available = p.stock > 0 && data.signedIn ? 'true' : 'false';
                add.disabled = add.dataset.available !== 'true' || busy;
                document.getElementById('detailLogin').hidden = !!data.signedIn;
                // A delayed modal response must not overwrite a newer POST result.
                if (cartRevision === cartRevisionAtRequest) syncCart(data);
                content.hidden = false;
                status.textContent = p.stock > 0 ? 'Thông tin sản phẩm được cập nhật từ cửa hàng.' : 'Sản phẩm hiện đã hết hàng.';
            } catch (error) {
                if (generation === detailGeneration && error.name !== 'AbortError') status.textContent = error.message || 'Không thể tải chi tiết sản phẩm. Vui lòng thử lại.';
            }
        });
        modal.addEventListener('hidden.bs.modal', function () {
            detailGeneration++;
            if (detailRequest) detailRequest.abort();
            add.disabled = true;
            add.dataset.available = 'false';
            content.hidden = true;
        });
    }
}());

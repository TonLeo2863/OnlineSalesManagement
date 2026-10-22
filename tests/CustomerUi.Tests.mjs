import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFile } from 'node:fs/promises';

// Behavioral tests for our script with a small DOM/fetch adapter, not browser QA.
const source = await readFile(new URL('../OnlineSalesManagement/Scripts/customer.js', import.meta.url), 'utf8');
function element(properties = {}) {
    const attributes = {};
    const classes = new Set();
    return Object.assign({ value: '', textContent: '', disabled: false, hidden: false, dataset: {},
        listeners: {}, classList: { toggle(name, on) { on ? classes.add(name) : classes.delete(name); }, add(name) { classes.add(name); } },
        setAttribute(name, value) { attributes[name] = String(value); }, getAttribute(name) { return attributes[name]; },
        removeAttribute(name) { delete attributes[name]; }, addEventListener(name, fn) { this.listeners[name] = fn; }
    }, properties);
}
function fixture() {
    const ids = Object.fromEntries(['shopToast', 'shopToastMessage', 'productModal', 'productModalStatus', 'productModalContent', 'detailAdd', 'detailQuantity', 'detailLogin', 'detailImage', 'detailProductId', 'detailStock', 'detailName', 'detailSku', 'detailCategory', 'detailUnit', 'detailDescription', 'detailPrice'].map(id => [id, element({ id })]));
    ids.detailAdd.disabled = true;
    const versions = [element({ value: 'v1' }), element({ value: 'v1' })];
    const badge = element({ textContent: '0' });
    const submitButton = element();
    const buttons = [submitButton, ids.detailAdd];
    const listeners = {};
    const calls = [];
    const toasts = [];
    const timers = new Map();
    let respond = async () => { throw new Error('Set fetch response in test'); };
    const form = element({ action: '/Store/AddToCart', matches: selector => selector === '[data-add-cart]', reportValidity: () => true });
    const context = { document: { getElementById: id => ids[id], addEventListener: (name, fn) => { listeners[name] = fn; },
        querySelectorAll(selector) {
            if (selector.includes('input[name="version"]')) return versions;
            if (selector.includes('button[type="submit"]')) return buttons;
            if (selector === '[data-cart-count]') return [badge];
            return [];
        } }, window: { bootstrap: { Toast: { getOrCreateInstance(el, options) { return { show() { toasts.push({ message: ids.shopToastMessage.textContent, options, role: el.getAttribute('role') }); } }; } } } },
        fetch: async (...args) => { calls.push(args); return respond(...args); },
        AbortController, URLSearchParams,
        FormData: class { *[Symbol.iterator]() { yield ['__RequestVerificationToken', 'csrf']; yield ['version', versions[0].value]; yield ['productId', '7']; yield ['quantity', '1']; } },
        setTimeout: (fn, ms) => { const id = timers.size + 1; timers.set(id, { fn, ms }); return id; }, clearTimeout: id => timers.delete(id)
    };
    vm.runInNewContext(source, context, { filename: 'customer.js' });
    return { ids, versions, badge, buttons, calls, toasts, timers, listeners, form,
        respond(fn) { respond = fn; }, submit() { return listeners.submit({ target: form, preventDefault() {} }); },
        show(url = '/Store/ProductDetails/7') { return ids.productModal.listeners['show.bs.modal']({ relatedTarget: { dataset: { detailsUrl: url } } }); },
        hide() { ids.productModal.listeners['hidden.bs.modal'](); }
    };
}
function response(data, status = 200, mime = 'application/json') { return { ok: status >= 200 && status < 300, status, redirected: false, headers: { get: () => mime }, json: async () => data }; }
function product(overrides = {}) { return { success: true, signedIn: true, version: 'v3', product: { id: 7, name: 'Sản phẩm', sku: 'SKU7', category: 'Danh mục', unit: 'Cái', description: '<img onerror=bad>', price: '100.000 ₫', stock: 8, imageUrl: '/Content/product-placeholder.svg' }, ...overrides }; }

test('AJAX sends CSRF once during rapid submit; toast and badge wait for server success', async () => {
    const f = fixture(); let resolve;
    f.respond(() => new Promise(done => { resolve = done; }));
    const first = f.submit();
    assert.equal(f.calls.length, 1); assert.equal(f.buttons[0].disabled, true); assert.equal(f.toasts.length, 0);
    await f.submit(); assert.equal(f.calls.length, 1);
    const request = f.calls[0][1];
    assert.equal(request.method, 'POST'); assert.equal(request.credentials, 'same-origin');
    assert.equal(request.headers['X-Requested-With'], 'XMLHttpRequest');
    assert.equal(request.body.get('__RequestVerificationToken'), 'csrf');
    resolve(response({ success: true, message: 'Đã thêm sản phẩm vào giỏ hàng', version: 'v2', count: 3 }));
    await first;
    assert.equal(f.badge.textContent, 3); assert.ok(f.versions.every(v => v.value === 'v2'));
    assert.equal(f.toasts[0].options.delay, 3000); assert.equal(f.toasts[0].role, 'status');
    assert.equal(f.buttons[0].disabled, false); assert.equal(f.ids.detailAdd.disabled, true);
    assert.equal(f.timers.size, 0);
});
test('server conflict is an error toast and synchronizes authoritative version/count', async () => {
    const f = fixture(); f.respond(async () => response({ success: false, message: 'Không đủ hàng', count: 2, version: 'fresh' }, 409));
    await f.submit(); assert.equal(f.badge.textContent, 2); assert.equal(f.versions[0].value, 'fresh');
    assert.equal(f.toasts[0].message, 'Không đủ hàng'); assert.equal(f.toasts[0].role, 'alert');
    assert.equal(f.buttons[0].disabled, false);
});
test('expired login and non-JSON CSRF failure never show success', async () => {
    const f = fixture(); f.respond(async () => response({ success: false }, 401));
    await f.submit(); assert.match(f.toasts[0].message, /đăng nhập/); assert.equal(f.badge.textContent, '0');
    f.respond(async () => response(null, 500, 'text/html'));
    await f.submit(); assert.match(f.toasts[1].message, /biểu mẫu/); assert.equal(f.toasts[1].role, 'alert');
});
test('network ambiguity tells customer to check cart without automatic retry', async () => {
    const f = fixture(); f.respond(async () => { throw new TypeError('offline'); });
    await f.submit(); assert.equal(f.calls.length, 1); assert.match(f.toasts[0].message, /kiểm tra giỏ hàng/); assert.equal(f.badge.textContent, '0');
});
test('modal uses server data as text, limits quantity and disables out-of-stock purchase', async () => {
    const f = fixture(); const data = product(); f.respond(async () => response(data));
    await f.show(); assert.equal(f.ids.detailName.textContent, 'Sản phẩm');
    assert.equal(f.ids.detailDescription.textContent, '<img onerror=bad>');
    assert.equal(f.ids.detailQuantity.max, 8); assert.equal(f.ids.detailAdd.disabled, false);
    data.product.stock = 0; await f.show();
    assert.equal(f.ids.detailAdd.disabled, true); assert.equal(f.ids.detailQuantity.disabled, true); assert.equal(f.ids.detailStock.textContent, 'Hết hàng');
});
test('anonymous modal permits reading but requires login to buy', async () => {
    const f = fixture(); f.respond(async () => response(product({ signedIn: false, version: null })));
    await f.show(); assert.equal(f.ids.productModalContent.hidden, false); assert.equal(f.ids.detailAdd.disabled, true); assert.equal(f.ids.detailLogin.hidden, false);
});
test('discontinued modal reports server error and cannot submit', async () => {
    const f = fixture(); f.respond(async () => response({ success: false, message: 'Sản phẩm không còn kinh doanh.' }, 404));
    await f.show(); assert.equal(f.ids.productModalContent.hidden, true); assert.equal(f.ids.detailAdd.disabled, true); assert.match(f.ids.productModalStatus.textContent, /không còn kinh doanh/);
});
test('closing modal aborts request and discards delayed response', async () => {
    const f = fixture(); let resolve; f.respond(() => new Promise(done => { resolve = done; }));
    const pending = f.show(); f.hide(); assert.equal(f.calls[0][1].signal.aborted, true);
    resolve(response(product())); await pending;
    assert.equal(f.ids.productModalContent.hidden, true); assert.equal(f.ids.detailAdd.disabled, true);
});
test('a late response for previous product cannot replace a newer modal selection', async () => {
    const f = fixture(); let resolve; f.respond(() => new Promise(done => { resolve = done; }));
    const old = f.show('/Store/ProductDetails/7');
    const data = product(); data.product.name = 'Mới'; f.respond(async () => response(data));
    await f.show('/Store/ProductDetails/8'); resolve(response(product())); await old;
    assert.equal(f.ids.detailName.textContent, 'Mới');
});
test('broken image falls back once to the shared placeholder', () => {
    const f = fixture(); const image = element({ tagName: 'IMG', dataset: { productImage: '', placeholder: '/placeholder.svg' } });
    image.setAttribute('src', '/missing.jpg'); f.listeners.error({ target: image });
    assert.equal(image.getAttribute('src'), '/placeholder.svg'); f.listeners.error({ target: image }); assert.equal(image.getAttribute('src'), '/placeholder.svg');
});
test('a modal response started before a successful add cannot restore the old cart version', async () => {
    const f = fixture(); let resolve;
    f.respond(() => new Promise(done => { resolve = done; }));
    const pendingModal = f.show();
    f.respond(async () => response({ success: true, message: 'Đã thêm', version: 'after-post', count: 2 }));
    await f.submit();
    resolve(response(product({ version: 'before-post' }))); await pendingModal;
    assert.equal(f.versions[0].value, 'after-post'); assert.equal(f.badge.textContent, 2);
    assert.equal(f.ids.detailAdd.disabled, false);
});

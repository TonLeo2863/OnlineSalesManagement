import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
const script = name => readFileSync(new URL('../OnlineSalesManagement/Views/' + name + '/Index.cshtml', import.meta.url), 'utf8').match(/<script>([\s\S]*?)<\/script>/)[1];
const element = () => ({ listeners: {}, value: '', hidden: false, textContent: '', addEventListener(event, fn) { this.listeners[event] = fn; } });
let assertions = 0;
function check(value, message) { assert.ok(value, message); console.log('PASS: ' + message); assertions++; }
{
 const ids = Object.fromEntries(['roleConfirmation', 'roleConfirmationText', 'confirmRole', 'cancelRole'].map(id => [id, element()]));
 const dialog = ids.roleConfirmation;
 dialog.showModal = () => { dialog.open = true; }; dialog.close = () => { dialog.open = false; };
 let submits = 0;
 const form = Object.assign(element(), { dataset: { email: '<account@example.test>', roles: 'KhachHang' }, elements: { role: { value: 'Kho' } }, submit() { submits++; } });
 vm.runInNewContext(script('AdminUsers'), { document: { getElementById: id => ids[id], querySelectorAll: () => [form] } });
 let prevented = false;
 const start = () => form.listeners.submit({ preventDefault() { prevented = true; } });
 start(); check(prevented && dialog.open && submits === 0, 'role save opens confirmation without submitting');
 check(ids.roleConfirmationText.textContent.includes(form.dataset.email) && ids.roleConfirmationText.textContent.includes('Kho') && ids.roleConfirmationText.textContent.includes('KhachHang'), 'confirmation shows selected account and role as plain text');
 ids.cancelRole.listeners.click(); check(!dialog.open && submits === 0, 'cancel never submits');
 start(); dialog.listeners.cancel(); ids.confirmRole.listeners.click.call(ids.confirmRole);
 check(submits === 0, 'Escape cancellation clears pending form');
 start(); ids.confirmRole.listeners.click.call(ids.confirmRole);
 check(submits === 1 && ids.confirmRole.disabled, 'confirm submits selected form and disables repeat click');
}
for (const page of ['Employees', 'Customers', 'Suppliers']) {
 const ids = Object.fromEntries(['quickSearch', 'activeFilter', 'resultCount', 'emptyResults'].map(id => [id, element()]));
 const rows = [ { textContent: 'NV01 Nguyễn Đăng', dataset: { active: 'true' } }, { textContent: 'NV02 Trần An', dataset: { active: 'false' } } ];
 vm.runInNewContext(script(page), { document: { getElementById: id => ids[id], querySelectorAll: () => rows } });
 check(rows.every(row => !row.hidden), page + ': initial records visible');
 ids.quickSearch.value = 'nguyen dang'; ids.quickSearch.listeners.input();
 check(!rows[0].hidden && rows[1].hidden, page + ': accent-insensitive Vietnamese search');
 ids.activeFilter.value = 'false'; ids.activeFilter.listeners.change();
 check(rows.every(row => row.hidden) && !ids.emptyResults.hidden, page + ': combined status filter and empty state');
 ids.quickSearch.value = ''; ids.quickSearch.listeners.input();
 check(rows[0].hidden && !rows[1].hidden, page + ': inactive records remain searchable');
}
console.log('TOTAL ADMIN UI PASS: ' + assertions);

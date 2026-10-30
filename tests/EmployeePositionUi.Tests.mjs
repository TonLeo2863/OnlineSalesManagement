import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
const script=readFileSync(new URL('../OnlineSalesManagement/Views/Shared/_EmployeePosition.cshtml',import.meta.url),'utf8').match(/<script>([\s\S]*?)<\/script>/)[1];
const choice={value:'__other__',addEventListener(event,fn){this.change=fn;}},custom={},input={value:'Legacy position'};
vm.runInNewContext(script,{document:{getElementById:id=>({positionChoice:choice,customPosition:custom,Position:input})[id]}});
assert.ok(!custom.hidden && !input.disabled && input.required);console.log('PASS: Other exposes required custom position');
choice.value='Known';choice.change();assert.ok(custom.hidden && input.disabled);console.log('PASS: known choice disables free input');
choice.value='__other__';choice.change();assert.equal(input.value,'Legacy position');console.log('PASS: toggling dropdown never erases legacy position text');

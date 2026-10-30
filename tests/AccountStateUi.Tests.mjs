import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
const source=readFileSync(new URL('../OnlineSalesManagement/Views/AdminUsers/Index.cshtml',import.meta.url),'utf8').match(/<script>([\s\S]*?)<\/script>/)[1].split('})();')[1]+'})();';
const nodes=[];
function node(){const n={events:{},children:[],addEventListener(k,f){this.events[k]=f;},setAttribute(){},append(...x){this.children.push(...x);},showModal(){this.open=true;},close(){this.open=false;}};nodes.push(n);return n;}
let submitted=0;const form=node();form.dataset={account:'customer@example.test'};form.elements={suspended:{value:'true'},reason:{value:'Safety reason'},confirmed:{value:'false'}};form.submit=()=>submitted++;
vm.runInNewContext(source,{document:{createElement:node,body:{append(){}},querySelectorAll:()=>[form]}});
const dialog=nodes[1],cancel=nodes[3],confirm=nodes[4];
let blocked=false;const start=()=>form.events.submit({preventDefault(){blocked=true;}});
start();assert.ok(blocked && dialog.open && submitted===0);console.log('PASS: suspension opens confirmation without submitting');
cancel.events.click();assert.ok(!dialog.open && submitted===0 && form.elements.confirmed.value==='false');console.log('PASS: cancel preserves false confirmation and does not submit');
start();dialog.events.cancel();confirm.events.click();assert.equal(submitted,0);console.log('PASS: Escape clears pending suspension');
start();confirm.events.click();assert.ok(submitted===1 && form.elements.confirmed.value==='true');console.log('PASS: explicit confirm marks approved request and submits once');
confirm.events.click();assert.equal(submitted,1);console.log('PASS: repeated confirm cannot repeat submission');

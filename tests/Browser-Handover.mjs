import fs from 'node:fs';
import path from 'node:path';

const base = process.env.DACN_BROWSER_BASE;
const out = process.env.DACN_BROWSER_OUTPUT;
if (!/^http:\/\/localhost:\d+$/.test(base || '') || !process.env.DACN_BROWSER_DATABASE?.startsWith('Dacn05_Test_'))
  throw new Error('Browser runner requires the isolated fixture supplied by Run-UiTests.');
const target = (await (await fetch(`http://127.0.0.1:${process.env.DACN_BROWSER_PORT}/json/list`)).json()).find(t => t.type === 'page');
const socket = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((resolve, reject) => { socket.onopen = resolve; socket.onerror = reject; });
let sequence = 0, count = 0;
const pending = new Map();
socket.onmessage = ({data}) => {
  const message = JSON.parse(data);
  if (message.id && pending.has(message.id)) {
    const {resolve, reject, timer} = pending.get(message.id); clearTimeout(timer); pending.delete(message.id);
    message.error ? reject(new Error(JSON.stringify(message.error))) : resolve(message.result);
  }
};
function call(method, params = {}) {
  return new Promise((resolve, reject) => {
    const id = ++sequence;
    const timer = setTimeout(() => { pending.delete(id); reject(new Error('CDP timeout: ' + method)); }, 30000);
    pending.set(id, {resolve, reject, timer}); socket.send(JSON.stringify({id, method, params}));
  });
}
async function evaluate(expression) {
  const value = await call('Runtime.evaluate', {expression, returnByValue:true, awaitPromise:true});
  if (value.exceptionDetails) throw new Error(value.exceptionDetails.text);
  return value.result.value;
}
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
async function waitFor(expression) {
  for (let i=0; i<100; i++) { try { if (await evaluate(expression)) return; } catch {} await pause(100); }
  throw new Error('Browser navigation/form did not reach expected state.');
}
async function navigate(route) {
  await call('Page.navigate', {url:base + route});
  await waitFor(`document.readyState==='complete' && location.pathname===${JSON.stringify(route.split('?')[0])}`);
}
function check(ok, label) { if (!ok) throw new Error('FAIL BROWSER: '+label); console.log('PASS BROWSER: '+label); count++; }
async function submit(fields, expectedPath) {
  await evaluate(`(() => { const fields=${JSON.stringify(fields)}; for (const [name,value] of Object.entries(fields)) {
    const input=document.querySelector('[name="'+name+'"]'); if(!input) throw new Error('Missing input '+name);
    if(input.type==='checkbox') input.checked=!!value; else input.value=String(value);
    input.dispatchEvent(new Event('input',{bubbles:true})); input.dispatchEvent(new Event('change',{bubbles:true}));
  } document.querySelector('[name="'+Object.keys(fields)[0]+'"]').form.requestSubmit(); })()`);
  await waitFor(`document.readyState==='complete' && location.pathname===${JSON.stringify(expectedPath)}`);
}
async function screenshot(name) {
  const shot = await call('Page.captureScreenshot', {format:'png',captureBeyondViewport:true});
  fs.writeFileSync(path.join(out, name+'.png'), Buffer.from(shot.data,'base64'));
}
try {
  await call('Page.enable'); await call('Runtime.enable');
  await call('Emulation.setDeviceMetricsOverride',{width:1280,height:900,deviceScaleFactor:1,mobile:false});
  await navigate('/Account/Login');
  await submit({Email:'Admin@ui.example.test',Password:process.env.DACN_UI_TEST_PASSWORD}, '/');
  check(await evaluate(`document.body.innerText.includes('Doanh thu ghi nhận')`),'Admin logs in through browser form');
  const fixtures = [
    {controller:'Customers',key:'Code',name:'FullName',fields:{Code:'BR-C',FullName:'Browser Customer',PhoneNumber:'0901234567',IsActive:true}},
    {controller:'Suppliers',key:'Code',name:'Name',fields:{Code:'BR-S',Name:'Browser Supplier',PhoneNumber:'0901234567',IsActive:true}},
    {controller:'Employees',key:'Code',name:'FullName',fields:{Code:'BR-E',FullName:'Browser Employee',PhoneNumber:'0901234567',HireDate:'2026-10-21',IsActive:true}},
    {controller:'Products',key:'Sku',name:'Name',fields:{Sku:'BR-P',Name:'Browser Product',Unit:'Cai',CostPrice:'100',SalePrice:'200',ReorderLevel:'1',IsActive:true}}
  ];
  for (const item of fixtures) {
    const root='/'+item.controller;
    await navigate(root+'/Create');
    if (item.controller==='Products') item.fields.CategoryId=await evaluate(`document.querySelector('[name="CategoryId"] option').value`);
    await submit(item.fields, root);
    check(await evaluate(`document.body.innerText.includes(${JSON.stringify(item.fields[item.key])})`), item.controller+' Create and Read through browser');
    const editPath = await evaluate(`(() => { const row=[...document.querySelectorAll('tr')].find(r=>r.innerText.includes(${JSON.stringify(item.fields[item.key])})); return row && row.querySelector('a[href*="/Edit/"]')?.getAttribute('href'); })()`);
    check(!!editPath,item.controller+' persisted row has Edit action');
    await navigate(editPath);
    const edited = {...item.fields,[item.name]:item.fields[item.name]+' Updated'};
    await submit(edited,root);
    check(await evaluate(`document.body.innerText.includes(${JSON.stringify(edited[item.name])})`), item.controller+' Update persisted');
    await screenshot(item.controller.toLowerCase()+'-updated');
    if (item.controller==='Products') {
      await navigate(editPath);
      check(!(await evaluate(`!!document.querySelector('[name="QuantityInStock"]')`)), 'Product form cannot edit stock');
    }
  }
  const order = process.env.DACN_BROWSER_ORDER;
  for (const [name,route] of [['invoice','/SalesOrders/Invoice/'+order],['revenue','/Reports'],['purchases','/Reports/Purchases'],['sales','/Reports/Sales'],['inventory','/InventoryReports']]) {
    await navigate(route);
    await call('Emulation.setEmulatedMedia',{media:'print'});
    const landscape = name==='purchases' || name==='sales';
    await call('Emulation.setDeviceMetricsOverride',{width:landscape?1123:794,height:landscape?794:1123,deviceScaleFactor:1,mobile:false});
    check(await evaluate(`!document.body.innerText.includes('Server Error') && !!document.querySelector('table')`),name+' print page has table without server error');
    const pdf = await call('Page.printToPDF',{printBackground:true,preferCSSPageSize:true,paperWidth:8.2677165,paperHeight:11.6929134,displayHeaderFooter:false});
    const bytes=Buffer.from(pdf.data,'base64'); fs.writeFileSync(path.join(out,name+'-a4.pdf'),bytes);
    const boxes=[...bytes.toString('latin1').matchAll(/\/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]/g)];
    check(boxes.length>0 && boxes.every(m=>Math.abs(+m[1]-(landscape?841.89:595.28))<2 && Math.abs(+m[2]-(landscape?595.28:841.89))<2),name+' PDF pages use A4 '+(landscape?'landscape':'portrait'));
    await screenshot(name+'-print');
    await call('Emulation.setEmulatedMedia',{media:''});
  }
  fs.writeFileSync(path.join(out,'browser-result.json'),JSON.stringify({status:'PASS',checks:count,database:process.env.DACN_BROWSER_DATABASE,utc:new Date().toISOString(),limitations:['No physical printer test','No cart or notification implementation found','No fixture records deleted']},null,2));
  console.log('TOTAL BROWSER PASS: '+count);
} catch (error) {
  try {
    await screenshot('browser-failure');
    fs.writeFileSync(path.join(out,'failure-visible-text.txt'),await evaluate('location.pathname+"\\n"+document.body.innerText'));
  } catch {}
  throw error;
} finally { socket.close(); }

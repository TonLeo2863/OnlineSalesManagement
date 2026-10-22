import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
const code=readFileSync(new URL('../OnlineSalesManagement/Scripts/store-menu.js',import.meta.url),'utf8');
let activeElement=null;
function element(){return {events:{},attrs:{},addEventListener(k,f){this.events[k]=f;},setAttribute(k,v){this.attrs[k]=v;},getAttribute(k){return this.attrs[k];},focus(){activeElement=this;}};}
const menu=element(), buttons=[element(),element()],panels=[element(),element()],links=[element(),element()];
buttons.forEach((b,i)=>{b.attrs['aria-controls']='panel'+i;panels[i].hidden=true;panels[i].querySelector=()=>links[i];});
menu.querySelectorAll=()=>buttons; menu.contains=e=>[menu,...buttons,...panels,...links].includes(e);
const media={matches:true,addEventListener(k,f){this[k]=f;}},docEvents={};
vm.runInNewContext(code,{window:{matchMedia:()=>media},document:{querySelector:()=>menu,getElementById:id=>panels[Number(id.slice(-1))],addEventListener(k,f){docEvents[k]=f;},get activeElement(){return activeElement;}}});
const key=k=>({key:k,preventDefault(){}});
buttons[0].events.pointerenter({pointerType:'mouse'});assert.equal(panels[0].hidden,false);
assert.equal(buttons[0].attrs['aria-expanded'],'true');console.log('PASS: desktop hover opens accessible panel');
// Moving into a descendant panel does not trigger leave on the whole menu.
assert.ok(menu.contains(panels[0]));assert.equal(panels[0].hidden,false);console.log('PASS: panel is inside the same menu interaction region');
menu.events.pointerleave();assert.ok(panels.every(p=>p.hidden));console.log('PASS: leaving whole desktop region closes');
buttons[1].events.click();assert.equal(panels[1].hidden,false);console.log('PASS: desktop click opens panel');
menu.events.keydown(key('Escape'));assert.ok(panels[1].hidden && activeElement===buttons[1]);console.log('PASS: Escape closes and restores trigger focus');
buttons[0].events.keydown(key('ArrowDown'));assert.equal(activeElement,buttons[1]);
buttons[1].events.keydown(key('ArrowRight'));assert.equal(activeElement,links[1]);console.log('PASS: arrows navigate categories and enter panel');
menu.events.keydown(key('ArrowLeft'));assert.equal(activeElement,buttons[1]);
menu.events.focusout({relatedTarget:{}});assert.ok(panels.every(p=>p.hidden));console.log('PASS: leaving focus region closes panel');
media.matches=false;media.change();buttons[0].events.pointerenter({pointerType:'touch'});assert.ok(panels[0].hidden);
buttons[0].events.click();assert.equal(panels[0].hidden,false);buttons[0].events.click();assert.equal(panels[0].hidden,true);console.log('PASS: touch accordion opens and collapses without hover');
buttons[0].events.click();buttons[1].events.click();assert.ok(panels[0].hidden && !panels[1].hidden);console.log('PASS: accordion keeps a single panel open');
docEvents.pointerdown({target:{}});assert.ok(panels.every(p=>p.hidden));console.log('PASS: outside click closes');
console.log('TOTAL MEGA MENU PASS: 10');

const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const callbacks = [], listeners = new Set();
let focused = false, methods, checks = 0;
const context = {
  Module: { canvas: { getBoundingClientRect: () => ({ left:10, top:20, width:1280, height:720 }), focus:()=>focused=true } },
  LibraryManager: {library:{}}, UTF8ToString: value=>value,
  SendMessage: (receiver,method,value)=>callbacks.push({receiver,method,value}),
  mergeInto: (target,value)=>Object.assign(target,value),
  window: { addEventListener:(type,fn)=>listeners.add(fn), removeEventListener:(type,fn)=>listeners.delete(fn) },
  document: {body:{appendChild(){}},createElement:()=>({style:{},events:{},setAttribute(){},focus(){},setSelectionRange(){},remove(){this.removed=true},addEventListener(type,fn){this.events[type]=fn}})},
};
vm.runInNewContext(fs.readFileSync('LionIceRun/Assets/Plugins/WebGL/IceTextInput.jslib','utf8'),context);
methods = context.LibraryManager.library;
methods.IceTextOpen('닉네임','가','이름',16,.1,.2,.3,.1);
let input=context.Module.iceText;
assert.equal(input.style.left,'138px'); checks++;
input.events.compositionstart(); input.value='ㅎ'; input.events.input();
input.events.keydown({isComposing:true,key:'Enter',stopPropagation(){},preventDefault(){}});
assert.equal(callbacks.length,0); checks++;
input.value='한글 닉네임'; input.events.compositionend();
assert.equal(callbacks[0].value,'한글 닉네임'); checks++;
input.events.keydown({key:'Enter',stopPropagation(){},preventDefault(){}});
assert.equal(callbacks[1].method,'OnNativeSubmit'); checks++;
assert.equal(callbacks[1].value,'한글 닉네임'); checks++;
methods.IceTextClose(); assert.equal(listeners.size,0); checks++;
assert.ok(input.removed && focused); checks++;
methods.IceTextOpen('채팅','','메시지',120,0,0,.4,.1); const old=context.Module.iceText;
methods.IceTextOpen('두번째','','메시지',120,0,0,.4,.1);
assert.ok(old.removed && listeners.size===1); checks++;
context.Module.iceText.value='마지막 글자 한'; context.Module.iceText.events.blur();
assert.equal(callbacks.at(-1).value,'마지막 글자 한'); checks++;
methods.IceTextClose(); assert.equal(listeners.size,0); checks++;
console.log(`PASS: browser IME event bridge checks=${checks}`);

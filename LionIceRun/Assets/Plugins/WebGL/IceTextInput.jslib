mergeInto(LibraryManager.library, {
  IceTextOpen: function(receiver, value, hint, limit, x, y, w, h) {
    if (Module.iceText) Module.iceText.iceDispose();
    var target = UTF8ToString(receiver), input = document.createElement('input');
    input.type = 'text'; input.value = UTF8ToString(value); input.placeholder = UTF8ToString(hint);
    if (limit > 0) input.maxLength = limit;
    input.autocomplete = 'off'; input.spellcheck = false;
    input.setAttribute('aria-label', input.placeholder || '텍스트 입력');
    input.style.cssText = 'position:fixed;z-index:2147483647;box-sizing:border-box;border:3px solid #293b47;border-radius:6px;background:#ffffff;color:#293b47;padding:8px 16px;outline:none;font-family:system-ui,sans-serif;font-weight:600';
    var canvas = Module.canvas, reposition = function() {
      var r = canvas.getBoundingClientRect();
      input.style.left = (r.left + x * r.width) + 'px'; input.style.top = (r.top + y * r.height) + 'px';
      input.style.width = (w * r.width) + 'px'; input.style.height = (h * r.height) + 'px';
      input.style.fontSize = Math.max(16, Math.min(26, h * r.height * .42)) + 'px';
    };
    reposition();
    var composing = false, sent = false;
    var send = function(method) { if (!sent) SendMessage(target, method, input.value); };
    input.addEventListener('compositionstart', function() { composing = true; });
    input.addEventListener('compositionend', function() { composing = false; send('OnNativeText'); });
    input.addEventListener('input', function() { if (!composing) send('OnNativeText'); });
    input.addEventListener('keydown', function(e) {
      e.stopPropagation();
      if (e.isComposing || composing || e.keyCode === 229) return;
      if (e.key === 'Enter') { e.preventDefault(); send('OnNativeSubmit'); sent = true; }
      else if (e.key === 'Escape') { e.preventDefault(); send('OnNativeBlur'); sent = true; }
    });
    input.addEventListener('keyup', function(e) { e.stopPropagation(); });
    input.addEventListener('blur', function() { send('OnNativeBlur'); sent = true; });
    window.addEventListener('resize', reposition);
    input.iceDispose = function() { sent = true; window.removeEventListener('resize', reposition); input.remove(); };
    Module.iceText = input; document.body.appendChild(input); input.focus();
    input.setSelectionRange(input.value.length, input.value.length);
  },
  IceTextClose: function() {
    if (Module.iceText) { Module.iceText.iceDispose(); Module.iceText = null; }
    if (Module.canvas) Module.canvas.focus();
  }
});

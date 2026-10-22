(function () {
    'use strict';
    var menu = document.querySelector('[data-mega-menu]');
    if (!menu) return;
    var triggers = Array.from(menu.querySelectorAll('.store-category-trigger'));
    var desktop = window.matchMedia('(min-width: 992px)');
    var active = null;
    function close() {
        triggers.forEach(function (button) {
            button.setAttribute('aria-expanded', 'false');
            document.getElementById(button.getAttribute('aria-controls')).hidden = true;
        });
        active = null;
    }
    function open(button) {
        close(); active = button;
        button.setAttribute('aria-expanded', 'true');
        document.getElementById(button.getAttribute('aria-controls')).hidden = false;
    }
    triggers.forEach(function (button, index) {
        button.addEventListener('pointerenter', function (event) { if (desktop.matches && event.pointerType !== 'touch') open(button); });
        button.addEventListener('click', function () {
            // Hover already opens desktop panels; clicking keeps them open.
            if (active === button && !desktop.matches) close(); else open(button);
        });
        button.addEventListener('keydown', function (event) {
            if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
                event.preventDefault(); triggers[(index + (event.key === 'ArrowDown' ? 1 : triggers.length - 1)) % triggers.length].focus();
            }
            if (event.key === 'ArrowRight') { event.preventDefault(); open(button); document.getElementById(button.getAttribute('aria-controls')).querySelector('a').focus(); }
        });
    });
    menu.addEventListener('pointerleave', function () { if (desktop.matches) close(); });
    menu.addEventListener('focusout', function (event) { if (!menu.contains(event.relatedTarget)) close(); });
    menu.addEventListener('keydown', function (event) {
        if ((event.key === 'Escape' || event.key === 'ArrowLeft') && active) { event.preventDefault(); var previous = active; close(); previous.focus(); }
    });
    document.addEventListener('pointerdown', function (event) { if (!menu.contains(event.target)) close(); });
    desktop.addEventListener('change', close);
})();

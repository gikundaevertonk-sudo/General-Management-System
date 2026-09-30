// Hover layer for the dashboard's sales trend: a crosshair, a dot on the line and a tooltip.
// The chart is drawn on the server; this only reads the data-* values each hit area carries.
(function () {
    document.querySelectorAll('[data-chart="trend"]').forEach(function (wrap) {
        var svg = wrap.querySelector('svg');
        var tip = wrap.querySelector('.chart-tooltip');
        var cross = svg.querySelector('.chart-crosshair');
        var dot = svg.querySelector('.chart-dot');
        var vb = svg.viewBox.baseVal;

        function show(hit) {
            var x = parseFloat(hit.dataset.x), y = parseFloat(hit.dataset.y);
            cross.setAttribute('x1', x); cross.setAttribute('x2', x);
            dot.setAttribute('cx', x); dot.setAttribute('cy', y);
            svg.classList.add('is-hovering');

            tip.innerHTML = '';
            var label = document.createElement('div'); label.className = 'chart-tooltip-label'; label.textContent = hit.dataset.label;
            var value = document.createElement('div'); value.className = 'chart-tooltip-value'; value.textContent = hit.dataset.value;
            var note = document.createElement('div'); note.className = 'chart-tooltip-note'; note.textContent = hit.dataset.note;
            tip.append(label, value, note);
            tip.hidden = false;

            // viewBox units to pixels, then keep the tooltip inside the card.
            var box = svg.getBoundingClientRect();
            var px = x / vb.width * box.width, py = y / vb.height * box.height;
            var left = px + 12;
            if (left + tip.offsetWidth > box.width) left = px - tip.offsetWidth - 12;
            tip.style.left = Math.max(0, left) + 'px';
            tip.style.top = Math.max(0, py - tip.offsetHeight / 2) + 'px';
        }

        function hide() { svg.classList.remove('is-hovering'); tip.hidden = true; }

        svg.querySelectorAll('.chart-hit').forEach(function (hit) {
            hit.addEventListener('mouseenter', function () { show(hit); });
            hit.addEventListener('touchstart', function () { show(hit); }, { passive: true });
        });
        svg.addEventListener('mouseleave', hide);
    });
})();

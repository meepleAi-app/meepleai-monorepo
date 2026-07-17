/**
 * Report builder per il tool mockup↔live compare (#2999).
 * `buildReportHtml` è puro (nessun I/O) → unit-testabile.
 */

/** Escape minimale per testo iniettato in HTML. */
export function escapeHtml(s) {
  return String(s)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;');
}

function renderPair(e) {
  const vp = `${e.viewport.width}×${e.viewport.height}`;
  const intent = e.designIntent ? `<span class="intent">${escapeHtml(e.designIntent)}</span>` : '';
  const mockup = e.mockupDataUri
    ? `<img class="layer mockup" src="${e.mockupDataUri}" alt="mockup ${escapeHtml(e.id)}" />`
    : `<div class="live-error">⚠ mockup capture failed<br /><code>${escapeHtml(e.mockupError ?? 'unknown')}</code></div>`;
  const live = e.liveDataUri
    ? `<img class="layer live" src="${e.liveDataUri}" alt="live ${escapeHtml(e.id)}" />`
    : `<div class="live-error">⚠ live capture failed<br /><code>${escapeHtml(e.liveError ?? 'unknown')}</code></div>`;
  // Slider ha senso solo se ENTRAMBE le catture esistono.
  const slider =
    e.mockupDataUri && e.liveDataUri
      ? `<input type="range" min="0" max="100" value="50" class="slider"
         data-pair-id="${escapeHtml(e.id)}" aria-label="reveal live over mockup" />`
      : '';
  return `
  <section class="pair" data-pair="${escapeHtml(e.id)}">
    <header>
      <h2>${escapeHtml(e.label)} ${intent}</h2>
      <p class="meta"><code>${escapeHtml(e.route)}</code> · ${vp}</p>
    </header>
    <div class="compare" data-mode="overlay">
      <div class="stage">
        ${mockup}
        <div class="live-wrap" data-pair-id="${escapeHtml(e.id)}">${live}</div>
      </div>
      ${slider}
      <button class="toggle" data-pair-id="${escapeHtml(e.id)}">side-by-side ⇄ overlay</button>
    </div>
  </section>`;
}

export function buildReportHtml(entries) {
  const body = entries.map(renderPair).join('\n');
  const css = `
    :root { color-scheme: light dark; }
    body { margin: 0; font: 14px/1.5 system-ui, sans-serif; background: #f7f3ee; color: #1a1a1a; }
    header.top { padding: 16px 24px; border-bottom: 1px solid #ccc; }
    .pair { padding: 24px; border-bottom: 1px solid #ddd; }
    .pair h2 { margin: 0 0 4px; font-size: 16px; }
    .intent { font-size: 11px; padding: 2px 6px; border-radius: 6px; background: #e5dccb; margin-left: 8px; }
    .meta { margin: 0 0 12px; color: #666; }
    .stage { position: relative; max-width: 1200px; border: 1px solid #bbb; overflow: hidden; }
    .layer { display: block; width: 100%; height: auto; }
    .live-wrap { position: absolute; inset: 0; overflow: hidden; }
    .compare[data-mode="overlay"] .live-wrap { width: 50%; }
    .compare[data-mode="sbs"] .stage { display: none; }
    .slider { width: 100%; max-width: 1200px; margin: 8px 0; }
    .toggle { font: inherit; padding: 4px 10px; cursor: pointer; }
    .live-error { padding: 40px; text-align: center; color: #a00; background: #fdd; }
    @media (prefers-color-scheme: dark) { body { background: #1a1a1a; color: #eee; } }
  `;
  const js = `
    document.querySelectorAll('.slider').forEach(function (s) {
      s.addEventListener('input', function () {
        var wrap = document.querySelector('.live-wrap[data-pair-id="' + s.dataset.pairId + '"]');
        if (wrap) wrap.style.width = s.value + '%';
      });
    });
    document.querySelectorAll('.toggle').forEach(function (b) {
      b.addEventListener('click', function () {
        var c = b.closest('.compare');
        c.dataset.mode = c.dataset.mode === 'overlay' ? 'sbs' : 'overlay';
      });
    });
  `;
  return `<!doctype html>
<html lang="it"><head><meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>Mockup↔Live Compare (#2999)</title>
<style>${css}</style></head>
<body>
<header class="top"><strong>Mockup↔Live Compare</strong> — ${entries.length} coppie · confronto manuale (#2999)</header>
${body}
<script>${js}</script>
</body></html>`;
}

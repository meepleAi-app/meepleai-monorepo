/**
 * Page Catalog — HTML Generator
 *
 * Reads manifest.json and produces a self-contained index.html
 * with card grid, sidebar filters, search, and lightbox.
 * Zero external dependencies.
 *
 * Usage:
 *   npx tsx docs/pages/generate.ts
 */

import * as fs from 'fs';
import * as path from 'path';

import { config, type Manifest } from './config';

// ============================================================================
// Badge Colors
// ============================================================================

const GROUP_COLORS: Record<string, string> = {
  public: '#22c55e',
  auth: '#3b82f6',
  authenticated: 'hsl(25,95%,38%)',
  admin: '#a855f7',
  chat: '#14b8a6',
  dev: '#6b7280',
  docs: '#6b7280',
  join: '#6b7280',
  offline: '#6b7280',
  other: '#6b7280',
};

const STATUS_COLORS: Record<string, string> = {
  ok: '#22c55e',
  error: '#ef4444',
  timeout: '#f59e0b',
  skipped: '#6b7280',
  pending: '#6b7280',
};

// ============================================================================
// HTML Generation
// ============================================================================

function generateHTML(manifest: Manifest): string {
  const groups = [...new Set(manifest.pages.map(p => p.group))].sort();

  const cardsHTML = manifest.pages.map((page, i) => {
    const screenshotRelPath = `../${page.screenshot}`;
    const statusColor = STATUS_COLORS[page.status] || '#6b7280';
    const groupColor = GROUP_COLORS[page.group] || '#6b7280';
    const hasScreenshot = page.status === 'ok' || page.status === 'error';

    return `
    <div class="card" data-group="${esc(page.group)}" data-status="${esc(page.status)}"
         data-title="${esc(page.title)}" data-route="${esc(page.route)}"
         data-description="${esc(page.description)}" data-index="${i}"
         ${hasScreenshot ? `onclick="openLightbox(${i})"` : ''}>
      <div class="card-image">
        ${hasScreenshot
          ? `<img src="${esc(screenshotRelPath)}" alt="${esc(page.title)}" loading="lazy" />`
          : `<div class="card-placeholder">${page.status.toUpperCase()}</div>`
        }
      </div>
      <div class="card-body">
        <div class="card-title">${escHTML(page.title)}</div>
        <div class="card-route">${escHTML(page.route)}</div>
        <div class="card-meta">
          <span class="badge" style="background:${groupColor}">${page.group}</span>
          <span class="status-dot" style="background:${statusColor}" title="${page.status}"></span>
        </div>
      </div>
    </div>`;
  }).join('\n');

  const sidebarHTML = groups.map(g => {
    const count = manifest.pages.filter(p => p.group === g).length;
    const color = GROUP_COLORS[g] || '#6b7280';
    return `<label class="filter-option">
      <input type="radio" name="group" value="${g}" />
      <span class="filter-dot" style="background:${color}"></span>
      ${g} <span class="filter-count">(${count})</span>
    </label>`;
  }).join('\n');

  const pagesJSON = JSON.stringify(manifest.pages.map(p => ({
    title: p.title,
    route: p.route,
    description: p.description,
    group: p.group,
    status: p.status,
    screenshot: '../' + p.screenshot,
  })));

  return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>MeepleAI Page Catalog</title>
<style>
  * { margin: 0; padding: 0; box-sizing: border-box; }
  body { font-family: system-ui, -apple-system, sans-serif; background: #f8fafc; color: #1e293b; }
  .layout { display: flex; min-height: 100vh; }
  .sidebar { width: 220px; background: #fff; border-right: 1px solid #e2e8f0; padding: 20px; position: fixed; height: 100vh; overflow-y: auto; }
  .main { margin-left: 220px; flex: 1; padding: 24px; }
  .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; flex-wrap: wrap; gap: 12px; }
  .header h1 { font-size: 24px; font-weight: 700; }
  .search { padding: 8px 12px; border: 1px solid #e2e8f0; border-radius: 8px; width: 300px; font-size: 14px; }
  .search:focus { outline: none; border-color: hsl(25,95%,38%); box-shadow: 0 0 0 3px hsla(25,95%,38%,0.1); }
  .counter { font-size: 13px; color: #64748b; margin-bottom: 16px; }
  .grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; }
  @media (max-width: 1200px) { .grid { grid-template-columns: repeat(2, 1fr); } }
  @media (max-width: 768px) { .sidebar { display: none; } .main { margin-left: 0; } .grid { grid-template-columns: 1fr; } }
  .card { background: #fff; border-radius: 12px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.08); cursor: pointer; transition: transform 0.15s, box-shadow 0.15s; }
  .card:hover { transform: translateY(-2px); box-shadow: 0 4px 12px rgba(0,0,0,0.12); }
  .card[data-status="skipped"], .card[data-status="pending"] { opacity: 0.6; cursor: default; }
  .card-image { aspect-ratio: 16/10; overflow: hidden; background: #f1f5f9; }
  .card-image img { width: 100%; height: 100%; object-fit: cover; object-position: top; }
  .card-placeholder { display: flex; align-items: center; justify-content: center; height: 100%; font-size: 12px; color: #94a3b8; font-weight: 600; letter-spacing: 1px; }
  .card-body { padding: 12px; }
  .card-title { font-size: 14px; font-weight: 600; margin-bottom: 2px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  .card-route { font-size: 12px; color: #64748b; margin-bottom: 8px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; font-family: monospace; }
  .card-meta { display: flex; align-items: center; gap: 8px; }
  .badge { font-size: 11px; color: #fff; padding: 2px 8px; border-radius: 4px; font-weight: 600; }
  .status-dot { width: 8px; height: 8px; border-radius: 50%; }
  .sidebar h2 { font-size: 14px; font-weight: 700; margin-bottom: 16px; text-transform: uppercase; letter-spacing: 1px; color: #64748b; }
  .filter-option { display: flex; align-items: center; gap: 8px; padding: 6px 0; cursor: pointer; font-size: 14px; }
  .filter-option input { display: none; }
  .filter-option.active { font-weight: 600; }
  .filter-dot { width: 10px; height: 10px; border-radius: 50%; }
  .filter-count { color: #94a3b8; font-size: 12px; }
  .sidebar-stats { margin-top: 24px; padding-top: 16px; border-top: 1px solid #e2e8f0; }
  .sidebar-stats div { font-size: 13px; color: #64748b; margin-bottom: 4px; }
  .sidebar-stats span { font-weight: 600; color: #1e293b; }
  .lightbox { display: none; position: fixed; inset: 0; background: rgba(0,0,0,0.9); z-index: 1000; align-items: center; justify-content: center; flex-direction: column; }
  .lightbox.active { display: flex; }
  .lightbox img { max-width: 90vw; max-height: 80vh; border-radius: 8px; box-shadow: 0 8px 32px rgba(0,0,0,0.5); }
  .lightbox-info { color: #fff; text-align: center; margin-top: 16px; }
  .lightbox-title { font-size: 18px; font-weight: 600; }
  .lightbox-route { font-size: 14px; color: #94a3b8; font-family: monospace; margin-top: 4px; }
  .lightbox-nav { position: absolute; top: 50%; font-size: 48px; color: #fff; cursor: pointer; user-select: none; padding: 20px; opacity: 0.7; }
  .lightbox-nav:hover { opacity: 1; }
  .lightbox-prev { left: 20px; }
  .lightbox-next { right: 20px; }
  .lightbox-close { position: absolute; top: 20px; right: 30px; font-size: 36px; color: #fff; cursor: pointer; opacity: 0.7; }
  .lightbox-close:hover { opacity: 1; }
  .lightbox-counter { font-size: 13px; color: #64748b; margin-top: 8px; }
  .footer { text-align: center; padding: 24px; color: #94a3b8; font-size: 13px; margin-top: 40px; border-top: 1px solid #e2e8f0; }
</style>
</head>
<body>

<div class="layout">
  <aside class="sidebar">
    <h2>Filters</h2>
    <label class="filter-option active">
      <input type="radio" name="group" value="all" checked />
      <span class="filter-dot" style="background:#1e293b"></span>
      All <span class="filter-count">(${manifest.stats.total})</span>
    </label>
    ${sidebarHTML}
    <div class="sidebar-stats">
      <div>Captured: <span>${manifest.stats.captured}</span></div>
      <div>Errors: <span>${manifest.stats.errors}</span></div>
      <div>Timeouts: <span>${manifest.stats.timeouts}</span></div>
      <div>Skipped: <span>${manifest.stats.skipped}</span></div>
    </div>
  </aside>

  <main class="main">
    <div class="header">
      <h1>MeepleAI Page Catalog</h1>
      <input type="text" class="search" placeholder="Search pages..." id="search" />
    </div>
    <div class="counter" id="counter">Showing ${manifest.stats.total} of ${manifest.stats.total} pages</div>
    <div class="grid" id="grid">
      ${cardsHTML}
    </div>
    <div class="footer">Generated: ${manifest.generatedAt.split('T')[0]} &mdash; MeepleAI Page Catalog</div>
  </main>
</div>

<div class="lightbox" id="lightbox">
  <span class="lightbox-close" onclick="closeLightbox()">&times;</span>
  <span class="lightbox-nav lightbox-prev" onclick="navLightbox(-1)">&#8249;</span>
  <span class="lightbox-nav lightbox-next" onclick="navLightbox(1)">&#8250;</span>
  <img id="lightbox-img" src="" alt="" />
  <div class="lightbox-info">
    <div class="lightbox-title" id="lightbox-title"></div>
    <div class="lightbox-route" id="lightbox-route"></div>
    <div class="lightbox-counter" id="lightbox-counter"></div>
  </div>
</div>

<script>
  var pages = ${pagesJSON};
  var currentFilter = 'all';
  var currentSearch = '';
  var lightboxIndex = -1;
  var visibleIndices = [];
  var grid = document.getElementById('grid');
  var counter = document.getElementById('counter');
  var cards = grid.querySelectorAll('.card');
  var lightbox = document.getElementById('lightbox');
  var totalPages = ${manifest.stats.total};

  function updateView() {
    var shown = 0;
    visibleIndices = [];
    cards.forEach(function(card, i) {
      var group = card.dataset.group;
      var title = card.dataset.title.toLowerCase();
      var route = card.dataset.route.toLowerCase();
      var desc = card.dataset.description.toLowerCase();
      var search = currentSearch.toLowerCase();
      var matchGroup = currentFilter === 'all' || group === currentFilter;
      var matchSearch = !search || title.indexOf(search) !== -1 || route.indexOf(search) !== -1 || desc.indexOf(search) !== -1;
      if (matchGroup && matchSearch) {
        card.style.display = '';
        shown++;
        visibleIndices.push(i);
      } else {
        card.style.display = 'none';
      }
    });
    counter.textContent = 'Showing ' + shown + ' of ' + totalPages + ' pages';
  }

  document.querySelectorAll('.filter-option').forEach(function(opt) {
    opt.addEventListener('click', function() {
      document.querySelectorAll('.filter-option').forEach(function(o) { o.classList.remove('active'); });
      opt.classList.add('active');
      currentFilter = opt.querySelector('input').value;
      updateView();
    });
  });

  document.getElementById('search').addEventListener('input', function(e) {
    currentSearch = e.target.value;
    updateView();
  });

  function openLightbox(idx) {
    lightboxIndex = idx;
    var p = pages[idx];
    document.getElementById('lightbox-img').src = p.screenshot;
    document.getElementById('lightbox-title').textContent = p.title;
    document.getElementById('lightbox-route').textContent = p.route;
    var posInVisible = visibleIndices.indexOf(idx);
    document.getElementById('lightbox-counter').textContent = (posInVisible + 1) + ' / ' + visibleIndices.length;
    lightbox.classList.add('active');
  }

  function closeLightbox() {
    lightbox.classList.remove('active');
  }

  function navLightbox(dir) {
    var pos = visibleIndices.indexOf(lightboxIndex);
    var next = pos + dir;
    if (next >= 0 && next < visibleIndices.length) {
      openLightbox(visibleIndices[next]);
    }
  }

  document.addEventListener('keydown', function(e) {
    if (!lightbox.classList.contains('active')) return;
    if (e.key === 'Escape') closeLightbox();
    if (e.key === 'ArrowLeft') navLightbox(-1);
    if (e.key === 'ArrowRight') navLightbox(1);
  });

  lightbox.addEventListener('click', function(e) {
    if (e.target === lightbox) closeLightbox();
  });

  visibleIndices = Array.from({length: pages.length}, function(_, i) { return i; });
</script>

</body>
</html>`;
}

// ============================================================================
// Utilities
// ============================================================================

function escHTML(str: string): string {
  return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

function esc(str: string): string {
  return str.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

// ============================================================================
// Main
// ============================================================================

function main(): void {
  console.log('\uD83D\uDD28 MeepleAI Page Catalog \u2014 HTML Generator\n');

  if (!fs.existsSync(config.manifestPath)) {
    console.error('\u274C Manifest not found. Run capture first: npm run docs:capture');
    process.exit(1);
  }

  const manifest: Manifest = JSON.parse(fs.readFileSync(config.manifestPath, 'utf-8'));
  console.log(`   Pages: ${manifest.stats.total}`);
  console.log(`   Captured: ${manifest.stats.captured}`);

  const html = generateHTML(manifest);

  fs.mkdirSync(config.distDir, { recursive: true });
  const outputPath = path.join(config.distDir, 'index.html');
  fs.writeFileSync(outputPath, html);

  console.log(`\n\u2705 Generated: ${outputPath}`);
  console.log(`   Open in browser or run: npx serve ${path.relative(process.cwd(), config.distDir)}`);
}

main();

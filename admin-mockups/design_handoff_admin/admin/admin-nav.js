/* admin-nav.js — Shared admin sidebar nav.
   Call after DOMContentLoaded with: renderAdminNav('active-id').
   IDs reflect file basename (sp5-admin-overview → 'overview', sp5-editor → 'editor'). */

const ADMIN_NAV = [
  { group: 'Admin Console', items: [
    { id: 'overview',      em: '📊', label: 'Overview',         href: 'sp5-admin-overview.html',     count: '' },
    { id: 'users',         em: '👥', label: 'Utenti',           href: 'sp5-admin-users.html',        count: '1.2k' },
    { id: 'content',       em: '📦', label: 'Content',          href: 'sp5-admin-content.html',      count: '14' },
    { id: 'ai',            em: '🤖', label: 'AI / RAG',         href: 'sp5-admin-ai.html',           count: '' },
    { id: 'kb',            em: '📚', label: 'Knowledge Base',   href: 'sp5-admin-kb.html',           count: '247' },
    { id: 'kb-upload',     em: '⤴️',  label: 'KB Upload',        href: 'sp5-kb-upload-flow.html',     count: '' },
    { id: 'catalog',       em: '🔄', label: 'Catalog ingest',   href: 'sp5-admin-catalog.html',      count: '' },
    { id: 'config',        em: '⚙️',  label: 'Config / Flags',   href: 'sp5-admin-config.html',       count: '' },
    { id: 'monitor',       em: '📈', label: 'Monitor',          href: 'sp5-admin-monitor.html',      count: '' },
    { id: 'notifications', em: '📨', label: 'Notifications',    href: 'sp5-admin-notifications.html', count: '' },
  ]},
  { group: 'Power-User Tools', items: [
    { id: 'editor',         em: '✏️',  label: 'Editor',            href: 'sp5-editor.html',           count: '' },
    { id: 'pipeline',       em: '🔗', label: 'Pipeline Builder',  href: 'sp5-pipeline-builder.html', count: '' },
    { id: 'n8n',            em: '⚡', label: 'n8n Integrations', href: 'sp5-n8n.html',              count: '' },
    { id: 'upload',         em: '📤', label: 'Upload avanzato',  href: 'sp5-upload-advanced.html',  count: '3' },
    { id: 'play-records',   em: '🎲', label: 'Play Records',     href: 'sp5-play-records.html',     count: '' },
    { id: 'versions',       em: '🔢', label: 'Versions',         href: 'sp5-versions.html',         count: '' },
    { id: 'private-games',  em: '🔒', label: 'Private Games',    href: 'sp5-private-games.html',    count: '' },
    { id: 'dev-tools',      em: '🛠️',  label: 'Dev Tools',        href: 'sp5-dev-tools.html',        count: '' },
  ]},
  { group: 'Platform & Operations', items: [
    { id: 'infra',          em: '🖥️',  label: 'Infrastructure',    href: 'sp5-admin-infra.html',      count: '' },
    { id: 'database-sync',  em: '🗄️',  label: 'Database Sync',     href: 'sp5-admin-database-sync.html', count: '' },
    { id: 'providers',      em: '🧠',  label: 'LLM Providers',     href: 'sp5-admin-providers.html',  count: '' },
    { id: 'emergency',      em: '⛔', label: 'Emergency',         href: 'sp5-admin-emergency.html',  count: '' },
    { id: 'budget',         em: '💰', label: 'Budget & Cost',     href: 'sp5-admin-budget.html',     count: '' },
    { id: 'secrets',        em: '🔐', label: 'Secrets Vault',     href: 'sp5-admin-secrets.html',    count: '' },
    { id: 'alerts',         em: '🔔', label: 'Alerting',          href: 'sp5-admin-alerts.html',     count: '' },
  ]},
  { group: 'AI Tooling & Data Quality', items: [
    { id: 'mechanic-extractor', em: '⚙️', label: 'Mechanic Extractor', href: 'sp5-admin-mechanic-extractor.html', count: '' },
    { id: 'sandbox',        em: '🧪', label: 'Agent Sandbox',     href: 'sp5-admin-sandbox.html',    count: '' },
    { id: 'ab-testing',     em: '🆎', label: 'A/B Testing',       href: 'sp5-admin-ab-testing.html', count: '' },
    { id: 'prompts',        em: '📝', label: 'Prompt Management',  href: 'sp5-admin-prompts.html',    count: '' },
    { id: 'rag-backup',     em: '💾', label: 'RAG Backup',        href: 'sp5-admin-rag-backup.html', count: '' },
    { id: 'integrations',   em: '🔌', label: 'Integrations',      href: 'sp5-admin-integrations.html', count: '' },
  ]},
];

function renderAdminNav(activeId) {
  const html = `
    <aside class="admin-nav" aria-label="Admin navigation">
      <div class="admin-nav-brand">
        <div class="admin-nav-brand-mark">M</div>
        <span class="admin-nav-brand-name">MeepleAI</span>
        <span class="admin-nav-brand-tag">Admin</span>
      </div>

      ${ADMIN_NAV.map(g => `
        <div class="admin-nav-group">${g.group}</div>
        ${g.items.map(i => `
          <a class="admin-nav-item ${i.id === activeId ? 'active' : ''}" href="${i.href}">
            <span class="em" aria-hidden="true">${i.em}</span>
            <span>${i.label}</span>
            ${i.count ? `<span class="count">${i.count}</span>` : ''}
          </a>
        `).join('')}
      `).join('')}

      <div class="admin-nav-foot">
        <span class="badge">SuperAdmin</span>
        <span>Aaron</span>
      </div>
    </aside>
  `;
  const container = document.querySelector('[data-admin-nav-mount]');
  if (container) container.innerHTML = html;
}

/* Theme auto from system + manual toggle persisted */
(function initAdminTheme() {
  const saved = localStorage.getItem('mai-admin-theme');
  if (saved) {
    document.documentElement.setAttribute('data-theme', saved);
  } else {
    // Admin prefers DARK as per brief
    document.documentElement.setAttribute('data-theme', 'dark');
  }
})();

function toggleAdminTheme() {
  const cur = document.documentElement.getAttribute('data-theme');
  const next = cur === 'dark' ? 'light' : 'dark';
  document.documentElement.setAttribute('data-theme', next);
  localStorage.setItem('mai-admin-theme', next);
}

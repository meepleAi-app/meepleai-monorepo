/* MeepleAI Nav Prototype — /agents (index) + /agents/[id] (detail).
   sp4-agents-index + sp4-agent-detail. */

const AG_CHIPS = [{ k: 'all', lb: 'Tutti' }, { k: 'Rules expert', lb: 'Rules expert' }, { k: 'Strategy', lb: 'Strategy' }, { k: 'Arbiter', lb: 'Arbiter' }];
const AG_TABS = [{ k: 'mine', lb: 'Tuoi' }, { k: 'community', lb: 'Community' }];

function AgentCard({ a, onNav }) {
  const g = a.gameId ? window.DS.byId[a.gameId] : null;
  return h('div', { className: 'agent-card', onClick: () => onNav('/agents/' + a.id) },
    h('span', { className: 'agent-av' }, '🤖'),
    h('div', { className: 'agent-name' }, a.title),
    h('div', { className: 'agent-game' }, g ? g.title : 'Multi-game'),
    h('div', { className: 'agent-stats' },
      h('span', null, '💬 ' + a.invocations),
      h('span', null, window.GN.agentAccuracy(a) + '% acc'),
      h('span', null, a.avgLatency)),
    h('span', { className: 'agent-cat' }, window.GN.agentCategory(a)));
}

function AgentsIndex({ state, onOpen, onNav }) {
  const [chip, setChip] = React.useState('all');
  const [tab, setTab] = React.useState('mine');

  if (state === 'loading') return h('div', null, h('div', { className: 'section-label', style: { marginTop: 0 } }, 'Agents', h('span', { className: 'ln' }), h(Gap, { cat: 'GAP-STATE', loc: 'Agents · loading', note: 'skeleton non nel mockup' })), h('div', { className: 'lib-grid' }, [0, 1, 2, 3].map(i => h('div', { key: i }, h(SkeletonCard, { hgt: 150 })))));
  if (state === 'error') return h('div', null, h(ErrorState, { title: 'Agents non disponibili', msg: 'Non riesco a caricare gli agent. Riprova.' }), h('div', { style: { marginTop: 'var(--s-3)' } }, h(Gap, { cat: 'GAP-STATE', loc: 'Agents · error', note: 'stato error non coperto dal mockup' })));
  if (state === 'empty') return h('div', null, h(EmptyState, { entity: 'agent', icon: '🤖', title: 'Nessun agent', desc: 'Non hai ancora creato agent. Crea il tuo primo esperto di regole.', cta: '+ Crea il tuo primo agent' }), h('div', { style: { textAlign: 'center' } }, h(Gap, { cat: 'GAP-STATE', loc: 'Agents · empty', note: 'stato empty non coperto dal mockup' })));

  const offline = state === 'offline';
  let list = window.DS.agents.filter(a => tab === 'mine' ? a.id !== 'a-universal' : a.id === 'a-universal');
  if (chip !== 'all') list = list.filter(a => window.GN.agentCategory(a) === chip);

  return h('div', null,
    offline ? h(OfflineBar, { note: 'agent dalla cache · ultimo sync 4h fa' }) : null,
    h('div', { className: 'subtab-row' }, AG_TABS.map(t => h('button', { key: t.k, className: 'subtab agent', 'data-active': tab === t.k, onClick: () => setTab(t.k) }, t.lb))),
    h('div', { className: 'filter-row' }, AG_CHIPS.map(c => h('button', { key: c.k, className: 'filter-chip e-agent', 'data-active': chip === c.k, onClick: () => setChip(c.k) }, c.lb))),
    list.length ? h('div', { className: 'lib-grid' + (offline ? ' cached' : '') }, list.map(a => h(AgentCard, { key: a.id, a, onNav })))
      : h('div', { className: 'dr-empty-note', style: { marginTop: 'var(--s-4)' } }, 'Nessun agent in questo filtro.')
  );
}

const AGD_TABS = [{ k: 'overview', lb: 'Overview' }, { k: 'kb', lb: 'Knowledge base' }, { k: 'chats', lb: 'Recent chats' }, { k: 'settings', lb: 'Settings' }];

function AgentDetail({ id, state, onOpen, onNav }) {
  const [tab, setTab] = React.useState('overview');
  const a = window.DS.byId[id];

  if (state === 'loading') return h('div', null, h('div', { className: 'gd-hero' }, h(SkeletonCard, { hgt: 180 }), h('div', null, h(SkeletonLine, { w: 50, hgt: 28 }), h(SkeletonLine, { w: 60 }))), h('div', { style: { marginTop: 'var(--s-3)' } }, h(Gap, { cat: 'GAP-STATE', loc: 'Agent detail · loading', note: 'skeleton non nel mockup' })));
  if (!a || a.type !== 'agent' || state === 'error') {
    return h('div', { className: 'state-block e-agent' }, h('div', { className: 'sb-icon' }, '🔍'),
      h('h2', null, a ? 'Errore di caricamento' : 'Agent non trovato'),
      h('p', null, a ? 'Riprova tra poco.' : 'L\u2019id "' + id + '" non corrisponde a nessun agent.'),
      h('button', { className: 'sb-cta', style: { background: 'hsl(var(--c-agent))' }, onClick: () => onNav('/agents') }, '← Torna ad Agents'));
  }

  const offline = state === 'offline';
  const g = a.gameId ? window.DS.byId[a.gameId] : null;
  const kbs = g ? window.DS.kbs.filter(k => k.gameId === g.id) : [];
  const chats = window.DS.chats.filter(c => c.agentId === a.id);

  const body = {
    overview: h('div', { className: 'gd-body' },
      h('p', null, a.title + ' è un agent ' + window.GN.agentCategory(a).toLowerCase() + ' per ' + (g ? g.title : 'più giochi') + '. Strategia ' + a.strategy + ', modello ' + a.model + '.'),
      h('div', { className: 'dr-sec-label' }, 'Capabilities', h('span', { className: 'ln' })),
      h('div', { className: 'gd-mech' }, ['Spiega regole', 'Risolve dispute', 'Cita il manuale', 'Suggerisce strategie'].map((c, i) => h('span', { key: i, className: 'gd-tag' }, c))),
      h('div', { className: 'dr-sec-label' }, 'Prompt config (preview)', h('span', { className: 'ln' })),
      h('pre', { className: 'prompt-preview' }, 'role: ' + window.GN.agentCategory(a) + '\nmodel: ' + a.model + '\nstrategy: ' + a.strategy + '\ntemperature: 0.3\nkb_docs: ' + a.docs)),
    kb: h('div', null,
      kbs.length ? h('div', { className: 'dr-list' }, kbs.map(k => h('div', { className: 'dr-list-row', key: k.id, onClick: () => onOpen('entity', k.id) },
        h('span', { className: 'sess-cover e-kb', style: { background: k.cover } }, '📄'),
        h('span', { className: 'dlr-stack' }, h('span', { className: 'dlr-name' }, k.title), h('span', { className: 'dlr-sub' }, k.subtitle)),
        h('span', { className: 'chev' }, '›')))) : h('div', { className: 'dr-empty-note' }, 'Nessun documento collegato.'),
      h('div', { style: { display: 'flex', gap: 'var(--s-3)', alignItems: 'center', marginTop: 'var(--s-3)' } },
        h('button', { className: 'btn ghost' }, '+ Aggiungi documento'), h(Gap, { cat: 'GAP-CTA', loc: 'Agent · KB', note: 'upload documento TBD' }))),
    chats: chats.length ? h('div', { className: 'dr-list' }, chats.map(c => h('div', { className: 'dr-list-row', key: c.id, onClick: () => onOpen('entity', c.id) },
        h('span', { className: 'sess-cover e-chat', style: { background: c.cover } }, '💬'),
        h('span', { className: 'dlr-stack' }, h('span', { className: 'dlr-name' }, c.title), h('span', { className: 'dlr-sub' }, c.msgCount + ' messaggi · ' + c.lastAt)),
        h('span', { className: 'chev' }, '›')))) : h('div', { className: 'dr-empty-note' }, 'Nessun thread recente.'),
    settings: h('div', { className: 'gd-body' },
      h('p', null, 'Configurazione agent (solo owner).'),
      h('div', { className: 'wz-field' }, h('label', null, 'Temperature'), h('input', { className: 'wz-input', defaultValue: '0.3' })),
      h('div', { className: 'wz-field' }, h('label', null, 'System prompt'), h('textarea', { className: 'wz-input', rows: 3, defaultValue: 'Sei un esperto di ' + (g ? g.title : 'giochi da tavolo') + '…' })),
      h('div', { style: { marginTop: 'var(--s-2)' } }, h(Gap, { cat: 'GAP-CTA', loc: 'Agent · settings', note: 'salvataggio config TBD' }))),
  }[tab];

  return h('div', null,
    offline ? h(OfflineBar, { note: 'agent dalla cache' }) : null,
    h('div', { className: 'gd-hero' },
      h('div', { className: 'gd-cover', style: { background: a.cover } }, '🤖'),
      h('div', { className: 'gd-info' },
        h('div', { className: 'gd-badges' }, h('span', { className: 'agent-cat' }, window.GN.agentCategory(a)), h('span', { className: 'gd-status', style: { background: 'var(--bg-muted)', color: 'var(--text-sec)' } }, a.badge)),
        h('h1', { className: 'gd-title' }, a.title),
        h('div', { className: 'gd-pub' }, 'Esperto di ', g ? h('button', { className: 'game-link', onClick: () => onOpen('entity', g.id) }, g.title) : 'più giochi', ' · ' + a.model),
        h('div', { className: 'gd-tags' }, h('span', { className: 'gd-tag' }, '💬 ' + a.invocations + ' chat'), h('span', { className: 'gd-tag' }, window.GN.agentAccuracy(a) + '% accuracy'), h('span', { className: 'gd-tag' }, '⚡ ' + a.avgLatency)),
        h('div', { className: 'gd-cta-stack' },
          h('button', { className: 'gd-cta', style: { background: 'hsl(var(--c-agent))' } }, 'Inizia chat'),
          h(Gap, { cat: 'GAP-CTA', loc: 'Agent · Inizia chat', note: 'navigate /chat/[threadId] TBD' })))),
    h('div', { className: 'gd-tabs' }, AGD_TABS.map(t => h('button', { key: t.k, className: 'gd-tab agent', 'data-active': tab === t.k, onClick: () => setTab(t.k) }, t.lb))),
    body
  );
}

Object.assign(window, { AgentsIndex, AgentDetail });

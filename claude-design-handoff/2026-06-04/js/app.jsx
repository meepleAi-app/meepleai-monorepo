/* MeepleAI Nav Prototype — root app: sidebar nav, topbar, 5-way state toggle,
   theme persistence, entity drawer host, and the GAP audit slide-over. */
const { useState: useS, useEffect: useE, useCallback: useCb } = React;

const NAV = [
  { route: '/dashboard',   label: 'Dashboard',   ico: '\uD83C\uDFE0', ent: 'game',    built: true },
  { route: '/library',     label: 'Library',     ico: '\uD83D\uDCDA', ent: 'game',    built: true },
  { route: '/games',       label: 'Games',       ico: '\uD83C\uDFB2', ent: 'game',    built: false,
    pattern: 'Indice giochi. Ma /library mostra già i giochi posseduti — confine /games vs /library indefinito.',
    note: '/games vs /library semantics TBD — superfici sovrapposte',
    sidebarGap: { cat: 'GAP-ROUTE', note: '/games vs /library semantics TBD' } },
  { route: '/sessions',    label: 'Sessions',    ico: '\uD83C\uDFAF', ent: 'session', built: true },
  { route: '/agents',      label: 'Agents',      ico: '\uD83E\uDD16', ent: 'agent',   built: true },
  { route: '/discover',    label: 'Discover',    ico: '\uD83E\uDDED', ent: 'toolkit', built: true },
  { route: '/game-nights', label: 'Game Nights', ico: '\uD83C\uDF89', ent: 'event',   built: true },
];
const AUTH = { route: '/login', label: 'Auth', ico: '\uD83D\uDD11', ent: 'kb', built: false,
  pattern: 'auth-flow.html · login / register / reset (JSX twin assente nel bundle)' };

function StateToggle({ value, onChange }) {
  const states = [
    { k: 'default', lb: 'default' }, { k: 'empty', lb: 'empty' }, { k: 'loading', lb: 'loading' },
    { k: 'error', lb: 'error' }, { k: 'offline', lb: 'offline' },
  ];
  return h('div', { className: 'state-toggle', role: 'tablist', 'aria-label': 'Stato schermo' },
    states.map(s => h('button', {
      key: s.k, 'data-s': s.k, 'data-active': value === s.k, onClick: () => onChange(s.k),
    }, h('span', { className: 'sdot' }), s.lb))
  );
}

function NavItem({ item, active, onClick }) {
  return h('button', {
    className: 'nav-item ' + (ENT_CLASS[item.ent] || '') + (active ? ' active' : ''),
    onClick, title: item.route,
  },
    h('span', { className: 'ico' }, item.ico),
    h('span', null, item.label),
    item.sidebarGap ? h('span', { className: 'nav-gap' }, h(Gap, { cat: item.sidebarGap.cat, mini: true, loc: 'Sidebar · ' + item.label, note: item.sidebarGap.note })) : null
  );
}

function Sidebar({ route, onNav, open, onLogout }) {
  return h('aside', { className: 'sidebar' + (open ? ' open' : '') },
    h('div', { className: 'brand' },
      h('div', { className: 'brand-mark' }, 'M'),
      h('div', null,
        h('div', { className: 'brand-name' }, 'MeepleAI'),
        h('div', { className: 'brand-sub' }, 'Nav Prototype')
      )
    ),
    h('div', { className: 'nav-group-label' }, 'Navigazione'),
    NAV.map(it => h(NavItem, { key: it.route, item: it, active: route === it.route, onClick: () => onNav(it.route) })),
    h('div', { className: 'nav-group-label' }, 'Account'),
    h('button', { className: 'nav-item e-kb', onClick: onLogout, title: 'Logout' },
      h('span', { className: 'ico' }, '🚪'), h('span', null, 'Logout')),
    h('div', { className: 'sb-spacer' }),
    h('div', { className: 'sb-foot' },
      'QA fixture · token-driven',
      h('br'), 'fonte: SP4-entity-desktop.md'
    )
  );
}

function GapPanel({ open, onClose }) {
  const gaps = useGaps();
  const { panelRef, scrimRef } = useSlideAnim(open, 250);
  const byCat = Object.keys(GAP_CATS).map(cat => ({ cat, items: gaps.filter(g => g.cat === cat) }));
  return h(React.Fragment, null,
    h('div', { ref: scrimRef, className: 'drawer-scrim' + (open ? ' open' : ''), onClick: onClose, style: { zIndex: 'var(--z-overlay)' } }),
    h('aside', { ref: panelRef, className: 'gap-panel' + (open ? ' open' : '') },
      h('div', { className: 'gp-head' },
        h('h2', null, '\u26A0 Gap audit'),
        h('span', { className: 'pip-more' }, gaps.length + ' totali'),
        h('span', { style: { flex: 1 } }),
        h('button', { className: 'dr-close', onClick: onClose }, '\u2715')
      ),
      h('div', { className: 'gp-body' },
        h('p', { style: { fontSize: 'var(--fs-sm)', color: 'var(--text-sec)', lineHeight: 1.5, margin: '0 0 var(--s-2)' } },
          'Gap rilevati nella vista corrente, classificati nelle 5 categorie del brief. Si aggiornano in base allo schermo e allo stato attivi.'),
        byCat.map(({ cat, items }) => h('div', { key: cat },
          h('div', { className: 'gap-cat-head' }, '[' + cat + ']', h('span', { className: 'cnt' }, items.length), h('span', { style: { color: 'var(--text-muted)', textTransform: 'none', letterSpacing: 0 } }, '· ' + GAP_CATS[cat])),
          items.length
            ? items.map(it => h('div', { className: 'gap-row', key: it.id },
                h('div', { className: 'gr-loc' }, it.loc),
                it.note ? h('div', { className: 'gr-note' }, it.note) : null))
            : h('div', { style: { fontSize: 'var(--fs-xs)', color: 'var(--text-muted)', fontFamily: 'var(--f-mono)', padding: '0 0 var(--s-2)' } }, 'nessuno in questa vista')
        ))
      )
    )
  );
}

function Stub({ item }) {
  return h('div', null,
    h('div', { className: 'stub' },
      h('div', { className: 'stub-ico' }, item.ico),
      h('h2', null, item.label),
      h('span', { className: 'stub-route' }, item.route),
      h('p', null, 'Schermo non ancora costruito in questa iterazione. Pattern previsto dal brief:'),
      h('p', { style: { color: 'var(--text)', fontWeight: 'var(--fw-semi)' } }, item.pattern || '—'),
      item.note ? h('div', null, h(Gap, { cat: 'GAP-ROUTE', loc: item.label + ' (' + item.route + ')', note: item.note })) : null
    )
  );
}

function App() {
  const [route, setRoute] = useS(() => (location.hash || '#/dashboard').replace('#', '') || '/dashboard');
  const [states, setStates] = useS({});
  const [stack, setStack] = useS([]);
  const [gapOpen, setGapOpen] = useS(false);
  const [sbOpen, setSbOpen] = useS(false);
  const [loggedIn, setLoggedIn] = useS(() => localStorage.getItem('mai-simfirst') !== '1');
  const [simFirst, setSimFirst] = useS(() => localStorage.getItem('mai-simfirst') === '1');
  const [authMode, setAuthMode] = useS('login');
  const [authState, setAuthState] = useS('default');
  const [onboarding, setOnboarding] = useS(false);
  const [logoutAsk, setLogoutAsk] = useS(false);
  const [theme, setTheme] = useS(() => {
    const stored = localStorage.getItem('mai-theme');
    if (stored) return stored;
    return matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  });

  useE(() => { document.documentElement.dataset.theme = theme; localStorage.setItem('mai-theme', theme); }, [theme]);
  useE(() => {
    const onHash = () => setRoute((location.hash || '#/dashboard').replace('#', '') || '/dashboard');
    window.addEventListener('hashchange', onHash);
    return () => window.removeEventListener('hashchange', onHash);
  }, []);

  const nav = useCb((r) => { location.hash = r; setSbOpen(false); }, []);
  const openRoot = useCb((kind, id, ctx) => setStack([{ kind, id, ctx }]), []);
  const pushDrawer = useCb((kind, id, ctx) => setStack(s => [...s, { kind, id, ctx }]), []);
  const popDrawer = useCb(() => setStack(s => s.slice(0, -1)), []);
  const closeDrawer = useCb(() => setStack([]), []);
  // Close the whole drawer stack AND navigate full-page (game-name tap inside a
  // GameNight drawer → /games/[id], avoiding recursive Game Detail ⇄ drawer).
  const navFromDrawer = useCb((r) => { setStack([]); location.hash = r; }, []);
  const [showAdd, setShowAdd] = useS(false);

  const toggleSimFirst = useCb(() => setSimFirst(v => { const nv = !v; localStorage.setItem('mai-simfirst', nv ? '1' : '0'); if (nv) { setLoggedIn(false); setAuthMode('login'); } else { setLoggedIn(true); } return nv; }), []);
  const finishOnboarding = useCb(() => { setOnboarding(false); setLoggedIn(true); setSimFirst(false); localStorage.setItem('mai-simfirst', '0'); location.hash = '/dashboard'; }, []);

  // Onboarding wizard: fullscreen, no app shell.
  if (onboarding) {
    return h(React.Fragment, null,
      h(Onboarding, { state: authState, onFinish: finishOnboarding }),
      h(AuthDevBar, { state: authState, setState: setAuthState, onGap: () => setGapOpen(true), theme, setTheme }),
      h(GapPanel, { open: gapOpen, onClose: () => setGapOpen(false) }));
  }
  // Not logged in: auth modal over dark backdrop, no app shell.
  if (!loggedIn) {
    return h(React.Fragment, null,
      h(AuthModal, { mode: authMode, setMode: setAuthMode, state: authState,
        onLogin: () => { setLoggedIn(true); location.hash = '/dashboard'; },
        onRegister: () => { setOnboarding(true); } }),
      h(AuthDevBar, { state: authState, setState: setAuthState, onGap: () => setGapOpen(true), theme, setTheme }),
      h(GapPanel, { open: gapOpen, onClose: () => setGapOpen(false) }));
  }

  const state = states[route] || 'default';
  const setState = (s) => setStates(prev => ({ ...prev, [route]: s }));
  const segs = route.split('/').filter(Boolean);
  const sec = segs[0];

  // Live mode: immersive, bypasses the sidebar shell entirely.
  if (sec === 'game-nights' && segs[1] && segs[2] === 'live') {
    return h(React.Fragment, null,
      h(GameNightLive, { id: segs[1], state, setState, onExit: () => nav('/game-nights/' + segs[1]), onGap: () => setGapOpen(true) }),
      h(DrawerStack, { stack, onPush: pushDrawer, onPop: popDrawer, onClose: closeDrawer, onNavigate: navFromDrawer }),
      h(GapPanel, { open: gapOpen, onClose: () => setGapOpen(false) })
    );
  }

  const isGameDetail = sec === 'games' && !!segs[1];
  const isAgentDetail = sec === 'agents' && !!segs[1];
  const isGnNew = sec === 'game-nights' && segs[1] === 'new';
  const isGnDetail = sec === 'game-nights' && segs[1] && segs[1] !== 'new';
  const isGnIndex = sec === 'game-nights' && !segs[1];
  const navActive = sec === 'games' ? '/games' : (sec === 'game-nights' ? '/game-nights' : (sec === 'agents' ? '/agents' : route));
  const item = [...NAV, AUTH].find(n => n.route === navActive) || { route: navActive, label: prettyRoute(navActive), built: false, pattern: 'Route fuori dal set di 8 voci principali.', note: 'destinazione non costruita in questo prototipo' };

  let built = item.built, titleText = item.label, screen;
  if (isGameDetail) { built = true; titleText = (DS.byId[segs[1]] || {}).title || 'Gioco'; screen = h(GameDetail, { id: segs[1], state, onOpen: openRoot, onNav: nav }); }
  else if (isGnNew) { built = true; titleText = 'Nuova Game Night'; screen = h(GameNightWizard, { state, onNav: nav }); }
  else if (isGnDetail) { built = true; titleText = (window.GN.nightById[segs[1]] || {}).name || 'Game Night'; screen = h(GameNightDetail, { id: segs[1], state, onOpen: openRoot, onNav: nav }); }
  else if (isGnIndex) { built = true; titleText = 'Game Nights'; screen = h(GameNightsIndex, { state, onOpen: openRoot, onNav: nav }); }
  else if (isAgentDetail) { built = true; titleText = (DS.byId[segs[1]] || {}).title || 'Agent'; screen = h(AgentDetail, { id: segs[1], state, onOpen: openRoot, onNav: nav }); }
  else if (route === '/agents') { screen = h(AgentsIndex, { state, onOpen: openRoot, onNav: nav }); }
  else if (route === '/sessions') { screen = h(SessionsIndex, { state, onOpen: openRoot, onNav: nav }); }
  else if (route === '/discover') { screen = h(Discover, { state, onNav: nav }); }
  else if (route === '/dashboard') { screen = h(Dashboard, { state, onOpen: openRoot }); }
  else if (route === '/library') { screen = h(Library, { state, onOpen: openRoot, onNav: nav }); }
  else { screen = h(Stub, { item }); }

  return h(React.Fragment, null,
    h('div', { className: 'app' },
      h(Sidebar, { route: navActive, onNav: nav, open: sbOpen, onLogout: () => setLogoutAsk(true) }),
      h('div', { className: 'main' },
        h('header', { className: 'topbar' },
          h('button', { className: 'icon-btn', onClick: () => setSbOpen(o => !o), style: { display: 'none' }, 'aria-label': 'Menu' }, '\u2630'),
          h('div', { className: 'tb-title' },
            h('h1', null, titleText, route === '/sessions' ? h('span', { className: 'title-help', title: 'Vista cross-GameNight. Per le partite di una specifica serata, vai a Game Nights.' }, '?') : null),
            h('span', { className: 'tb-route' }, route + (built ? '' : ' · stub'))
          ),
          h('span', { className: 'tb-spacer' }),
          h('button', { className: 'sim-toggle' + (simFirst ? ' on' : ''), onClick: toggleSimFirst, title: 'Dev: simula primo accesso (mostra login)' }, h('span', { className: 'sim-dot' }), 'Simulate first visit'),
          built ? h(StateToggle, { value: state, onChange: setState }) : null,
          route === '/library' ? h('button', { className: 'btn primary e-game', onClick: () => setShowAdd(true), title: 'Aggiungi gioco' }, '+ Gioco') : null,
          isGnIndex ? h('button', { className: 'btn primary e-event', onClick: () => nav('/game-nights/new'), title: 'Nuova Game Night' }, '+ Nuova Game Night') : null,
          route === '/agents' ? h('span', { style: { display: 'inline-flex', alignItems: 'center', gap: 'var(--s-2)' } }, h('button', { className: 'btn primary e-agent' }, '+ Crea agent'), h(Gap, { cat: 'GAP-CTA', loc: 'Agents · Crea agent', note: 'agent creation flow TBD', mini: true })) : null,
          h('button', { className: 'gap-pill', onClick: () => setGapOpen(true), title: 'Apri gap audit' },
            '\u26A0 GAP', h('span', { className: 'n' }, h(GapCount))),
          h('button', { className: 'icon-btn', onClick: () => setTheme(t => t === 'dark' ? 'light' : 'dark'), title: 'Tema' },
            theme === 'dark' ? '\u2600' : '\u263E')
        ),
        h('main', { className: 'content' },
          h('div', { className: 'content-wrap' }, screen)
        )
      )
    ),
    h(DrawerStack, { stack, onPush: pushDrawer, onPop: popDrawer, onClose: closeDrawer, onNavigate: navFromDrawer }),
    h(GapPanel, { open: gapOpen, onClose: () => setGapOpen(false) }),
    showAdd ? h(AddGameModal, { onClose: () => setShowAdd(false) }) : null,
    logoutAsk ? h('div', { className: 'modal-scrim open', onClick: () => setLogoutAsk(false) },
      h('div', { className: 'modal', onClick: (e) => e.stopPropagation() },
        h('div', { className: 'modal-head' }, h('h2', null, 'Vuoi disconnetterti?'), h('button', { className: 'dr-close', onClick: () => setLogoutAsk(false) }, '\u2715')),
        h('div', { className: 'modal-body' },
          h('p', { style: { color: 'var(--text-sec)', fontSize: 'var(--fs-base)' } }, 'Tornerai alla schermata di accesso. Utile per ri-testare il flow.'),
          h('div', { className: 'dr-cta-row' },
            h('button', { className: 'btn primary e-kb', onClick: () => { setLogoutAsk(false); setLoggedIn(false); setAuthMode('login'); setAuthState('default'); } }, 'Disconnetti'),
            h('button', { className: 'btn ghost', onClick: () => setLogoutAsk(false) }, 'Annulla'))))) : null
  );
}

function AuthDevBar({ state, setState, onGap, theme, setTheme }) {
  return h('div', { className: 'auth-devbar' },
    h(StateToggle, { value: state, onChange: setState }),
    h('button', { className: 'gap-pill', onClick: onGap, title: 'Gap audit' }, '\u26A0 GAP', h('span', { className: 'n' }, h(GapCount))),
    h('button', { className: 'icon-btn', onClick: () => setTheme(t => t === 'dark' ? 'light' : 'dark'), title: 'Tema' }, theme === 'dark' ? '\u2600' : '\u263E')
  );
}

function prettyRoute(r) {
  return r.replace('/', '').split('-').map(w => w ? w[0].toUpperCase() + w.slice(1) : w).join(' ') || 'Home';
}

function AddGameModal({ onClose }) {
  return h('div', { className: 'modal-scrim open', onClick: onClose },
    h('div', { className: 'modal', onClick: (e) => e.stopPropagation() },
      h('div', { className: 'modal-head' },
        h('h2', null, 'Aggiungi gioco'),
        h('button', { className: 'dr-close', onClick: onClose }, '\u2715')),
      h('div', { className: 'modal-body' },
        h('p', { style: { color: 'var(--text-sec)', fontSize: 'var(--fs-base)', lineHeight: 1.5 } },
          'Da dove arriva il gioco? Import da BoardGameGeek o inserimento manuale: il flusso non è ancora definito.'),
        h('div', { style: { marginTop: 'var(--s-3)' } },
          h(Gap, { cat: 'GAP-CTA', loc: 'Library · Aggiungi gioco', note: 'flow BGG vs manuale TBD', block: true })),
        h('div', { className: 'dr-cta-row' },
          h('button', { className: 'btn primary e-game' }, 'Cerca su BGG'),
          h('button', { className: 'btn ghost' }, 'Inserisci manuale')))
    )
  );
}

function GapCount() {
  const gaps = useGaps();
  return gaps.length;
}

/* responsive: reveal hamburger under 920px */
const mq = matchMedia('(max-width: 920px)');
function applyMq() {
  document.querySelectorAll('.topbar .icon-btn[aria-label="Menu"]').forEach(b => { b.style.display = mq.matches ? 'inline-flex' : 'none'; });
}
mq.addEventListener('change', applyMq);

ReactDOM.createRoot(document.getElementById('root')).render(h(App));
setTimeout(applyMq, 50);

/* MeepleAI Nav Prototype — /game-nights/new wizard (sp7-game-night-create).
   3 steps: quando e dove · invita player · giochi. Sticky progress header with
   cancel-confirm. On submit creates an in-memory planned GameNight and routes
   to its detail page. */
const { useState: useStateW } = React;

function GuestModal({ onAdd, onClose }) {
  const [name, setName] = useStateW('');
  return h('div', { className: 'modal-scrim open', onClick: onClose },
    h('div', { className: 'modal', onClick: (e) => e.stopPropagation() },
      h('div', { className: 'modal-head' }, h('h2', null, 'Aggiungi guest'), h('button', { className: 'dr-close', onClick: onClose }, '✕')),
      h('div', { className: 'modal-body' },
        h('p', { style: { color: 'var(--text-sec)', fontSize: 'var(--fs-sm)', marginBottom: 'var(--s-3)' } }, 'Un guest è un Player senza account (non collegato a un User).'),
        h('input', { className: 'wz-input', placeholder: 'Nome guest', value: name, onChange: (e) => setName(e.target.value), autoFocus: true }),
        h('div', { className: 'dr-cta-row' },
          h('button', { className: 'btn primary e-event', disabled: !name.trim(), onClick: () => { if (name.trim()) { onAdd(name.trim()); onClose(); } } }, 'Aggiungi'),
          h('button', { className: 'btn ghost', onClick: onClose }, 'Annulla'))))
  );
}

function CancelModal({ onConfirm, onClose }) {
  return h('div', { className: 'modal-scrim open', onClick: onClose },
    h('div', { className: 'modal', onClick: (e) => e.stopPropagation() },
      h('div', { className: 'modal-head' }, h('h2', null, 'Abbandonare?'), h('button', { className: 'dr-close', onClick: onClose }, '✕')),
      h('div', { className: 'modal-body' },
        h('p', { style: { color: 'var(--text-sec)', fontSize: 'var(--fs-base)' } }, 'Vuoi abbandonare? Le modifiche verranno perse.'),
        h('div', { className: 'dr-cta-row' },
          h('button', { className: 'btn primary e-event', onClick: onConfirm }, 'Abbandona'),
          h('button', { className: 'btn ghost', onClick: onClose }, 'Continua'))))
  );
}

function GameNightWizard({ state, onNav }) {
  const [step, setStep] = useStateW(1);
  const tomorrow = (() => { const d = new Date(); d.setDate(d.getDate() + 1); return d.toISOString().slice(0, 10); })();
  const [date, setDate] = useStateW(tomorrow);
  const [time, setTime] = useStateW('20:00');
  const [loc, setLoc] = useStateW('');
  const [note, setNote] = useStateW('');
  const [pids, setPids] = useStateW([]);
  const [gids, setGids] = useStateW([]);
  const [pcFilter, setPcFilter] = useStateW(false);
  const [showGuest, setShowGuest] = useStateW(false);
  const [showCancel, setShowCancel] = useStateW(false);
  const [search, setSearch] = useStateW('');

  if (state === 'error') {
    return h('div', null, h(ErrorState, { title: 'Impossibile creare la serata', msg: 'Qualcosa è andato storto nel salvataggio. Riprova.', primary: 'Riprova', secondary: 'Annulla' }));
  }

  const totalPlayers = pids.length + 1; // + me
  const addPid = (id) => setPids(p => p.includes(id) ? p : [...p, id]);
  const rmPid = (id) => setPids(p => p.filter(x => x !== id));
  const toggleGid = (id) => setGids(g => g.includes(id) ? g.filter(x => x !== id) : [...g, id]);
  const compatible = (g) => { const [mn, mx] = window.GN.parsePlayers(g.players); return totalPlayers >= mn && totalPlayers <= mx; };

  const submit = () => {
    const id = window.GN.addNight({ name: loc || 'Nuova serata', location: loc || '—',
      dateLabel: 'Sab ' + date.slice(8) + ' giu', time, host: window.GN.meId,
      playerIds: [window.GN.meId, ...pids], gameIds: gids, note });
    onNav('/game-nights/' + id);
  };

  const step1ok = date && loc.trim();
  const step2ok = pids.length >= 1;
  const step3ok = gids.length >= 1;

  return h('div', { className: 'wizard' },
    h('div', { className: 'wz-head' },
      h('div', { className: 'wz-progress' },
        [1, 2, 3].map(s => h('div', { key: s, className: 'wz-step' + (s === step ? ' active' : '') + (s < step ? ' done' : '') },
          h('span', { className: 'wz-num' }, s < step ? '✓' : s), h('span', null, ['Quando e dove', 'Invita player', 'Giochi'][s - 1])))),
      h('span', { className: 'wz-count' }, 'Step ' + step + '/3'),
      h('button', { className: 'dr-close', onClick: () => setShowCancel(true), title: 'Annulla' }, '✕')),

    step === 1 ? h('div', { className: 'wz-body' },
      h('div', { className: 'wz-field' }, h('label', null, 'Data'), h('input', { className: 'wz-input', type: 'date', value: date, onChange: (e) => setDate(e.target.value) })),
      h('div', { className: 'wz-field' }, h('label', null, 'Ora'), h('input', { className: 'wz-input', type: 'time', value: time, onChange: (e) => setTime(e.target.value) })),
      h('div', { className: 'wz-field' }, h('label', null, 'Location'),
        h('input', { className: 'wz-input', list: 'wz-locs', placeholder: 'Dove giocate?', value: loc, onChange: (e) => setLoc(e.target.value) }),
        h('datalist', { id: 'wz-locs' }, window.GN.savedLocations.map(l => h('option', { key: l, value: l })))),
      h('div', { className: 'wz-field' }, h('label', null, 'Note (opzionale)'), h('textarea', { className: 'wz-input', rows: 2, value: note, onChange: (e) => setNote(e.target.value) }))) : null,

    step === 2 ? h('div', { className: 'wz-body' },
      pids.length ? h('div', { className: 'wz-chips' },
        pids.map(id => { const p = window.GN.playerById[id]; return h('span', { key: id, className: 'wz-chip' }, p.initials, ' ', p.title, !p.linked ? h('span', { className: 'pbadge guest' }, 'Guest') : null, h('button', { className: 'wz-chip-x', onClick: () => rmPid(id) }, '✕')); })) : null,
      h('input', { className: 'wz-input', placeholder: 'Cerca friend o aggiungi guest', value: search, onChange: (e) => setSearch(e.target.value) }),
      h('div', { className: 'wz-friends' },
        window.GN.friendsList.filter(p => p.title.toLowerCase().includes(search.toLowerCase())).map(p => {
          const added = pids.includes(p.id);
          const risky = p.id === 'p-andrea'; // historic non-confirmed RSVP
          return h('button', { key: p.id, className: 'wz-friend' + (added ? ' added' : ''), onClick: () => added ? rmPid(p.id) : addPid(p.id) },
            h('span', { className: 'avatar sm', style: { background: p.cover, color: '#fff' } }, p.initials),
            h('span', { className: 'wz-friend-name' }, p.title),
            risky && added ? h(Gap, { cat: 'GAP-CTA', mini: true, loc: 'Wizard · invito ' + p.title, note: 'RSVP storico non confermato — side-effect invito TBD' }) : null,
            h('span', { className: 'wz-friend-add' }, added ? '✓' : '+'));
        })),
      h('button', { className: 'btn ghost', onClick: () => setShowGuest(true), style: { marginTop: 'var(--s-3)' } }, '+ Aggiungi guest'),
      h('div', { className: 'wz-hint' }, totalPlayers + '/9 giocatori (incluso te) · min 2, max 8 + te')) : null,

    step === 3 ? h('div', { className: 'wz-body' },
      h('div', { className: 'section-label', style: { marginTop: 0 } }, 'Suggested for tonight', h('span', { className: 'ln' }), h(Gap, { cat: 'GAP-ENTITY', mini: true, loc: 'Wizard · suggeriti', note: 'algoritmo di suggerimento opaco (fixture)' })),
      h('div', { className: 'wz-game-rail' },
        window.DS.games.filter(g => g.status !== 'wishlist' && compatible(g)).slice(0, 4).map(g => h('button', { key: g.id, className: 'wz-game' + (gids.includes(g.id) ? ' on' : ''), onClick: () => toggleGid(g.id) },
          h('span', { className: 'wz-game-cover', style: { background: g.cover } }, g.coverEmoji), h('span', { className: 'wz-game-name' }, g.title), h('span', { className: 'wz-game-sub' }, g.players)))),
      h('div', { className: 'section-label' }, 'Tutta la library', h('span', { className: 'ln' }),
        h('button', { className: 'filter-chip e-game', 'data-active': pcFilter, onClick: () => setPcFilter(v => !v) }, 'Solo compatibili ' + totalPlayers + 'p')),
      h('div', { className: 'wz-lib-grid' },
        window.DS.games.filter(g => !pcFilter || compatible(g)).map(g => h('button', { key: g.id, className: 'wz-game sm' + (gids.includes(g.id) ? ' on' : ''), onClick: () => toggleGid(g.id) },
          h('span', { className: 'wz-game-cover sm', style: { background: g.cover } }, g.coverEmoji), h('span', { className: 'wz-game-name' }, g.title)))) ) : null,

    // sticky footer
    h('div', { className: 'wz-foot' },
      step === 3 && gids.length ? h('div', { className: 'wz-selected' },
        h('span', { className: 'wz-selected-lb' }, 'Games della serata:'),
        gids.map(id => { const g = window.DS.byId[id]; return h('span', { key: id, className: 'wz-chip' }, g.coverEmoji + ' ' + g.title, h('button', { className: 'wz-chip-x', onClick: () => toggleGid(id) }, '✕')); })) : null,
      h('div', { className: 'wz-foot-btns' },
        step > 1 ? h('button', { className: 'btn ghost', onClick: () => setStep(s => s - 1) }, '‹ Indietro') : h('span'),
        step < 3
          ? h('button', { className: 'btn primary e-event', disabled: step === 1 ? !step1ok : !step2ok, onClick: () => setStep(s => s + 1) }, 'Avanti ›')
          : h('button', { className: 'btn primary e-event', disabled: !step3ok, onClick: submit }, 'Crea Game Night'))),

    showGuest ? h(GuestModal, { onClose: () => setShowGuest(false), onAdd: (name) => addPid(window.GN.addGuest(name)) }) : null,
    showCancel ? h(CancelModal, { onClose: () => setShowCancel(false), onConfirm: () => onNav('/game-nights') }) : null
  );
}

Object.assign(window, { GameNightWizard });

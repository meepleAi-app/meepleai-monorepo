/* MeepleAI SP4 · Power Grid — DATA  (window.PG  +  window.SkeletonData shim)
   /sessions/[id]/live — dataset game-specific letto da power-grid.json.

   Espone:
     window.SkeletonData = { STATES }   (shim: i renderer dello skeleton e
                                          skeleton-parts.jsx leggono STATES qui)
     window.PG = { game, agent, RES, SUPPLY, roster, PLANTS, market,
                   resourceMarket, network, phases, phaseState, turnOrder(),
                   ds (shape per i renderer polimorfici), counterWidgets,
                   auction, chat, suggestions, events, summary }

   MECCANICA CHIAVE (da power-grid.json): l'ordine di turno si RICALCOLA ogni
   round per città possedute (leader = più città) e si RIBALTA nelle fasi
   2-Auction e 3-Resources (leader gioca ULTIMO). Fasi 4-Building e
   5-Bureaucracy in ordine normale. 3 Step di gioco (1/2/3).

   Colori risorse: verbatim da power-grid.json. Tutto il resto = token CSS. */

(function () {
  // ─── shim per lo skeleton: i 5 stati canonici ─────────────────────────────
  window.SkeletonData = window.SkeletonData || {
    STATES: [
      { id: 'default', lb: 'Default' },
      { id: 'empty',   lb: 'Empty' },
      { id: 'loading', lb: 'Loading' },
      { id: 'error',   lb: 'Error' },
      { id: 'sse',     lb: 'SSE ✕' },
    ],
  };

  // ─── gioco + agente ───────────────────────────────────────────────────────
  const game = {
    id: 'power-grid', title: 'Power Grid', emoji: '⚡',
    cover: 'linear-gradient(135deg, hsl(45,80%,46%), hsl(28,82%,42%))',
    meta: '2-6 giocatori · ~120 min · Germania (base)',
  };
  const agent = { id: 'pg-agent', name: 'Power Grid Expert', emoji: '⚡', latency: '0.7s' };

  // ─── risorse (colori da power-grid.json) ──────────────────────────────────
  // glyph distinti + icona per leggibilità (coal/oil sono due slate vicini).
  const RES = {
    coal:    { id: 'coal',    lb: 'Carbone', glyph: 'C', icon: '⬛', color: '#2c3e50', dark: '#e8eef3', max: 24 },
    oil:     { id: 'oil',     lb: 'Petrolio',glyph: 'O', icon: '🛢', color: '#34495e', dark: '#e8eef3', max: 24 },
    garbage: { id: 'garbage', lb: 'Rifiuti', glyph: 'G', icon: '🗑', color: '#7d6608', dark: '#fdf5d0', max: 24 },
    uranium: { id: 'uranium', lb: 'Uranio',  glyph: 'U', icon: '☢', color: '#1e8449', dark: '#e6fff0', max: 12 },
  };
  const RES_ORDER = ['coal', 'oil', 'garbage', 'uranium'];

  // supply counters (Elektro / città / powered) — colori da JSON
  const SUPPLY = {
    elektro: { lb: 'Elektro',  glyph: 'E', color: '#f1c40f', dark: '#3a2c00' },
    city:    { lb: 'Città',    glyph: 'C', color: '#9b59b6', dark: '#fff' },
    powered: { lb: 'Alimentate', glyph: '⚡', color: '#f39c12', dark: '#3a2300' },
  };

  // ─── catalogo centrale: power plants ──────────────────────────────────────
  // n = numero (= puntata minima). res = combustibile(i) o 'eco'. cons = unità
  // consumate per attivare. cap = città alimentate. hybrid = brucia uno fra res.
  const PLANTS = {
    p03: { n: 3,  res: ['oil'],          cons: 2, cap: 1, eco: false },
    p04: { n: 4,  res: ['coal'],         cons: 2, cap: 1, eco: false },
    p06: { n: 6,  res: ['garbage'],      cons: 1, cap: 1, eco: false },
    p11: { n: 11, res: ['uranium'],      cons: 1, cap: 2, eco: false },
    p13: { n: 13, res: [],               cons: 0, cap: 1, eco: true  },
    p15: { n: 15, res: ['coal', 'oil'],  cons: 2, cap: 3, hybrid: true },
    p18: { n: 18, res: [],               cons: 0, cap: 2, eco: true  },
    p19: { n: 19, res: ['garbage'],      cons: 2, cap: 3, eco: false },
    p20: { n: 20, res: ['coal'],         cons: 3, cap: 5, eco: false },
    p21: { n: 21, res: ['coal', 'oil'],  cons: 2, cap: 4, hybrid: true },
    p23: { n: 23, res: ['uranium'],      cons: 1, cap: 3, eco: false },
    p25: { n: 25, res: [],               cons: 0, cap: 4, eco: true  },
    p27: { n: 27, res: ['oil'],          cons: 3, cap: 7, eco: false },
    p31: { n: 31, res: ['uranium'],      cons: 1, cap: 4, eco: false },
    p36: { n: 36, res: ['coal', 'oil'],  cons: 3, cap: 6, hybrid: true },
    p42: { n: 42, res: [],               cons: 0, cap: 6, eco: true  },
  };

  // mercato visibile: 8 carte ordinate per numero crescente.
  // currentMarket = comprabili (riga alta) · futureMarket = preview (riga bassa)
  const market = {
    current: ['p13', 'p15', 'p18', 'p20'],
    future:  ['p21', 'p23', 'p25', 'p27'],
  };

  // ─── mercato risorse: 4 colonne con scarsità ──────────────────────────────
  // per ogni risorsa, lista scaffali dal più caro (top) al più economico.
  // available = token ancora acquistabile. Il "next price" = scaffale
  // economico disponibile più basso. Più scarso ⇒ next price più alto.
  function buildShelves(prices, perPrice, boughtCheap) {
    // prices: array prezzi crescenti; perPrice: token per prezzo (1 o 3)
    // boughtCheap: quanti token (dai più economici) già acquistati
    const slots = [];
    prices.forEach(price => { for (let i = 0; i < perPrice; i++) slots.push({ price, available: true }); });
    for (let i = 0; i < boughtCheap && i < slots.length; i++) slots[i].available = false;
    return slots;
  }
  const resourceMarket = {
    coal:    { res: 'coal',    shelves: buildShelves([1,2,3,4,5,6,7,8], 3, 7) },   // next 3
    oil:     { res: 'oil',     shelves: buildShelves([1,2,3,4,5,6,7,8], 3, 10) },  // next 4
    garbage: { res: 'garbage', shelves: buildShelves([1,2,3,4,5,6,7,8], 3, 4) },   // next 2
    uranium: { res: 'uranium', shelves: buildShelves([1,2,3,4,5,6,7,8,10,12,14,16], 1, 7) }, // next 10
  };
  const nextPrice = (res) => {
    const av = resourceMarket[res].shelves.filter(s => s.available);
    return av.length ? av[0].price : null;
  };
  const remaining = (res) => resourceMarket[res].shelves.filter(s => s.available).length;

  // ─── roster (5 giocatori) — cities pilota l'ordine di turno ───────────────
  // cities = città su mappa · powered = alimentate questo round · store =
  // risorse in stock · plants = power plant possedute (max 3) con fuel caricato.
  const roster = [
    { id: 'p-marco', name: 'Marco', hue: 262, elektro: 22, cities: 14, powered: 13,
      store: { coal: 4, oil: 1, garbage: 0, uranium: 0 },
      plants: [ { ...PLANTS.p20, loaded: { coal: 3 } }, { ...PLANTS.p11, loaded: { uranium: 1 } }, { ...PLANTS.p15, loaded: { coal: 1, oil: 1 } } ] },
    { id: 'p-anna', name: 'Anna', hue: 350, elektro: 41, cities: 12, powered: 12,
      store: { coal: 2, oil: 5, garbage: 1, uranium: 0 },
      plants: [ { ...PLANTS.p21, loaded: { coal: 2 } }, { ...PLANTS.p19, loaded: { garbage: 2 } }, { ...PLANTS.p06, loaded: { garbage: 0 } } ] },
    { id: 'p-luca', name: 'Luca', hue: 174, elektro: 18, cities: 11, powered: 10,
      store: { coal: 1, oil: 0, garbage: 0, uranium: 2 },
      plants: [ { ...PLANTS.p23, loaded: { uranium: 1 } }, { ...PLANTS.p13, loaded: {} }, { ...PLANTS.p04, loaded: { coal: 1 } } ] },
    { id: 'p-sara', name: 'Sara', hue: 38, elektro: 55, cities: 9, powered: 9,
      store: { coal: 0, oil: 3, garbage: 0, uranium: 0 },
      plants: [ { ...PLANTS.p18, loaded: {} }, { ...PLANTS.p03, loaded: { oil: 2 } } ] },
    { id: 'p-dario', name: 'Dario', hue: 200, elektro: 30, cities: 7, powered: 7,
      store: { coal: 2, oil: 2, garbage: 1, uranium: 0 },
      plants: [ { ...PLANTS.p25, loaded: {} }, { ...PLANTS.p04, loaded: { coal: 2 } } ] },
  ];
  const byId = (id) => roster.find(p => p.id === id);

  // ─── fasi + stato di gioco ────────────────────────────────────────────────
  const phases = [
    { id: 'order',   n: 1, lb: 'Ordine giocatori', short: 'Ordine', reverse: false, desc: 'Ricalcolo per città possedute', icon: '🔢' },
    { id: 'auction', n: 2, lb: 'Asta centrali',    short: 'Asta',   reverse: true,  desc: '1-2 centrali per turno · puntate', icon: '🔨' },
    { id: 'resource',n: 3, lb: 'Acquisto risorse', short: 'Risorse',reverse: true,  desc: 'Prezzo di mercato con scarsità', icon: '🛢' },
    { id: 'build',   n: 4, lb: 'Costruzione',      short: 'Build',  reverse: false, desc: 'Posa nuove città sulla rete', icon: '🏗' },
    { id: 'bureau',  n: 5, lb: 'Burocrazia',       short: 'Burocr.',reverse: false, desc: 'Alimenta → reddito + restock + step', icon: '⚙' },
  ];
  // default: Round 5, Step 2, fase attiva = 2 (Asta) → ordine RIBALTATO
  const phaseState = { round: 5, step: 2, phaseIndex: 1, activePhaseId: 'auction' };

  // ─── ordine di turno (ricalcolo + reverse) ───────────────────────────────
  // base = per città decrescenti (leader primo). reverse ⇒ leader ultimo.
  const baseOrder = [...roster].sort((a, b) => b.cities - a.cities || b.powered - a.powered);
  const turnOrder = (phaseId) => {
    const ph = phases.find(p => p.id === (phaseId || phaseState.activePhaseId)) || phases[1];
    const ord = ph.reverse ? [...baseOrder].reverse() : baseOrder;
    return { order: ord, reverse: ph.reverse, phase: ph };
  };

  // ─── asta in corso (fase 2) ───────────────────────────────────────────────
  // plant a turno: p18 (eco, cap 2). Ordine reverse ⇒ inizia Dario.
  const auction = {
    plantId: 'p18',
    minBid: 18,
    highBid: 23,
    highBidderId: 'p-anna',
    nominatorId: 'p-dario',
    inAuction: ['p-dario', 'p-sara', 'p-luca', 'p-anna', 'p-marco'],
    passed: ['p-dario'],
    awaitingId: 'p-sara', // a chi tocca rilanciare/passare
    boughtThisPhase: 0, max: 1,
  };

  // ─── shape per i RENDERER POLIMORFICI dello skeleton ──────────────────────
  // SCORING — ScoringPanelRenderer (branch Points): score = città alimentate.
  const scoringCats = [
    { id: 'cities-powered', label: 'Città alimentate (finale)', computation: 'Count',  description: 'Condizione di vittoria primaria: più città alimentate nella Burocrazia finale. Fine partita quando un giocatore raggiunge 17 città costruite.' },
    { id: 'elektro-remaining', label: 'Elektro (spareggio 1)', computation: 'Sum',     description: 'Più Elektro residui vince i pareggi sulle città alimentate.' },
    { id: 'plants-capacity', label: 'Capacità centrali (spareggio 2)', computation: 'Custom', description: 'Se ancora in pareggio, somma della capacità città delle centrali possedute.' },
  ];
  const plantsCapOf = (p) => p.plants.reduce((s, pl) => s + pl.cap, 0);
  const scoringBreakdown = {};
  roster.forEach(p => { scoringBreakdown[p.id] = {
    'cities-powered': p.powered, 'elektro-remaining': p.elektro, 'plants-capacity': plantsCapOf(p),
  }; });

  // TURN — TurnIndicatorRenderer (branch Sequential): le 5 fasi del round.
  // meta segnala la natura reverse-aware (lo strip custom porta la logica).
  const turnShape = {
    turnOrderType: 'Sequential',
    phases: phases.map(p => `${p.n}. ${p.lb}`),
    direction: 'custom-reverse',
    turnActions: ['auction-bid', 'auction-pass', 'buy-resources', 'build-city', 'power-cities', 'discard-plant'],
  };

  // ds = oggetto unico consumato dai renderer skeleton (data.scoring/turn/...)
  const ds = {
    game, agent,
    session: { elapsed: '1h 12min' },
    players: baseOrder.map(p => ({ id: p.id, name: p.name, hue: p.hue, score: p.powered, turnDelta: 0 })),
    scoring: { scoreType: 'Points', categories: scoringCats },
    breakdown: scoringBreakdown,
    meta: 'Fasi del round · ordine reverse-aware',
    turn: turnShape,
    turnState: { phaseIndex: phaseState.phaseIndex, round: phaseState.round },
    chat: [
      { id: 'c1', from: 'user',  t: '15:02', text: 'Se Anna alimenta 12 città ma ne ha 14, come funziona il reddito?' },
      { id: 'c2', from: 'agent', t: '15:02', text: 'Il reddito dipende dalle città ALIMENTATE, non da quelle possedute. Anna sceglie quante alimentarne in base alle risorse: con 12 alimentate incassa 82 Elektro dalla tabella reddito.', citations: ['Power Grid §6', 'Income chart'] },
      { id: 'c3', from: 'user',  t: '15:08', text: 'Perché Marco è ultimo nell\u2019asta se è in testa?' },
      { id: 'c4', from: 'agent', t: '15:08', text: 'Esatto: nelle fasi Asta e Risorse l\u2019ordine si ribalta. Chi guida per città gioca per ULTIMO, così i giocatori in svantaggio scelgono centrali e comprano risorse prima.', citations: ['Power Grid §3'] },
    ],
    suggestions: ['Tabella reddito', 'Quando scatta lo Step 3?', 'Costo connessione'],
    events: [
      { kind: 'turn-change', who: 'p-dario', t: '15:09', text: '<strong>Dario</strong> mette all\u2019asta la centrale <strong>#18</strong> (eco, 2 città)' },
      { kind: 'custom',      who: 'p-anna',  t: '15:09', text: '<strong>Anna</strong> rilancia a <strong>23 Elektro</strong>' },
      { kind: 'custom',      who: 'p-dario', t: '15:10', text: '<strong>Dario</strong> passa sulla centrale #18' },
      { kind: 'score-update',who: 'p-marco', t: '15:04', text: 'Burocrazia round 4: <strong>Marco</strong> alimenta 13 città → +€90 reddito' },
      { kind: 'game-start',  who: null,      t: '14:01', text: 'Partita avviata · Germania (base) · 5 giocatori · Step 1' },
    ],
  };

  // WIDGET — ToolkitRenderer: i CounterTools del JSON (dadi/timer esclusi).
  const counterWidgets = [
    { id: 'w-elektro', type: 'ResourceManager', isEnabled: true, displayOrder: 1,
      config: { shared: false, resources: [
        { label: 'Elektro · Marco', value: 22, max: 999 },
        { label: 'Città alimentate', value: 13, max: 17 },
        { label: 'Uranio (scorta)', value: 0, max: 12, danger: true },
      ] } },
    { id: 'w-score', type: 'ScoreTracker', isEnabled: true, displayOrder: 2, config: {} },
    { id: 'w-turn',  type: 'TurnManager',  isEnabled: true, displayOrder: 3, config: { phaseBased: true } },
    { id: 'w-note',  type: 'NoteManager',  isEnabled: false, displayOrder: 4, config: { text: '' } },
    { id: 'w-rand',  type: 'RandomGenerator', isEnabled: false, displayOrder: 5, config: { name: 'Dadi' } },
  ];

  // ─── network map (Germania stilizzata · layout astratto/placeholder) ──────
  // coords in viewBox 0..100. owner = id giocatore o null (non costruita).
  // I nomi sono reali (base game sud); le posizioni sono uno SCHEMA, non
  // geografia reale — segnaposto per la mappa di rilascio.
  const network = {
    region: 'Germania · Sud-Ovest (base)',
    nodes: [
      { id: 'essen',     name: 'Essen',      x: 10, y: 13, owner: 'p-anna',  lp: 'top' },
      { id: 'dortmund',  name: 'Dortmund',   x: 27, y: 8,  owner: 'p-anna',  lp: 'top' },
      { id: 'koln',      name: 'Köln',       x: 9,  y: 30, owner: 'p-luca',  lp: 'left' },
      { id: 'aachen',    name: 'Aachen',     x: 6,  y: 45, owner: 'p-luca',  lp: 'top' },
      { id: 'kassel',    name: 'Kassel',     x: 45, y: 17, owner: 'p-anna',  lp: 'top' },
      { id: 'trier',     name: 'Trier',      x: 11, y: 61, owner: 'p-dario', lp: 'left' },
      { id: 'wiesbaden', name: 'Wiesbaden',  x: 27, y: 47, owner: 'p-marco', lp: 'left' },
      { id: 'frankfurt', name: 'Frankfurt',  x: 41, y: 36, owner: 'p-marco', lp: 'top' },
      { id: 'wurzburg',  name: 'Würzburg',   x: 59, y: 37, owner: 'p-sara',  lp: 'top' },
      { id: 'mannheim',  name: 'Mannheim',   x: 33, y: 63, owner: 'p-marco', lp: 'right' },
      { id: 'stuttgart', name: 'Stuttgart',  x: 46, y: 76, owner: 'p-sara',  lp: 'bottom' },
      { id: 'nurnberg',  name: 'Nürnberg',   x: 67, y: 55, owner: 'p-sara',  lp: 'right' },
      { id: 'regensburg',name: 'Regensburg', x: 82, y: 61, owner: null,      lp: 'bottom' },
      { id: 'augsburg',  name: 'Augsburg',   x: 61, y: 79, owner: null,      lp: 'right' },
      { id: 'munchen',   name: 'München',    x: 78, y: 86, owner: null,      lp: 'bottom' },
    ],
    edges: [
      ['essen','dortmund',5], ['essen','koln',10], ['koln','aachen',5], ['koln','dortmund',10],
      ['aachen','trier',20], ['trier','wiesbaden',20], ['koln','wiesbaden',20],
      ['wiesbaden','frankfurt',5], ['frankfurt','kassel',15], ['dortmund','kassel',20],
      ['frankfurt','mannheim',15], ['wiesbaden','mannheim',10], ['mannheim','stuttgart',10],
      ['frankfurt','wurzburg',15], ['kassel','wurzburg',20], ['wurzburg','nurnberg',10],
      ['stuttgart','wurzburg',15], ['nurnberg','augsburg',20], ['stuttgart','augsburg',15],
      ['augsburg','munchen',5], ['nurnberg','regensburg',10], ['regensburg','munchen',10],
    ],
    // città raggiungibili a costo minore dal current player (Marco) — highlight
    reachableBy: { 'p-marco': ['augsburg', 'wurzburg'] },
    connectCosts: [5, 10, 15, 20],
  };

  // ─── SUMMARY (post-game) ──────────────────────────────────────────────────
  // Stato finale: Step 3, round 14, Marco raggiunge 17ª città → burocrazia
  // finale. Vince chi alimenta più città; Elektro = spareggio.
  const summary = {
    rounds: 14, step: 3, endTrigger: 'Marco costruisce la 17ª città',
    // standings ordinati per esito finale (powered desc, elektro desc)
    standings: [
      { id: 'p-anna',  name: 'Anna',  hue: 350, powered: 17, cities: 17, elektro: 18, plants: 3, maxCap: 6, plantsCap: 13, rank: 1, winner: true },
      { id: 'p-marco', name: 'Marco', hue: 262, powered: 17, cities: 18, elektro: 9,  plants: 3, maxCap: 7, plantsCap: 15, rank: 2 },
      { id: 'p-sara',  name: 'Sara',  hue: 38,  powered: 15, cities: 16, elektro: 41, plants: 3, maxCap: 6, plantsCap: 14, rank: 3 },
      { id: 'p-luca',  name: 'Luca',  hue: 174, powered: 14, cities: 15, elektro: 12, plants: 3, maxCap: 4, plantsCap: 10, rank: 4 },
      { id: 'p-dario', name: 'Dario', hue: 200, powered: 12, cities: 13, elektro: 27, plants: 2, maxCap: 6, plantsCap: 11, rank: 5 },
    ],
    tiebreak: { winnerId: 'p-anna', runnerUpId: 'p-marco', note: 'Anna e Marco pari a 17 città alimentate · vince Anna per Elektro residui (18 vs 9).' },
    steps: [
      { step: 1, round: 1,  trigger: 'Inizio partita', detail: 'Mercato 8 centrali (4+4) · mappa base · max 1 centrale/città.', icon: '①' },
      { step: 2, round: 5,  trigger: '1º giocatore raggiunge 7 città', detail: 'Si può costruire come 2º occupante su una città (costo +). Carta più bassa rimossa.', icon: '②' },
      { step: 3, round: 11, trigger: 'Carta "Step 3" pescata dal mazzo', detail: 'Mercato unico di 6 centrali (no future) · 3º occupante ammesso · fine ravvicinata.', icon: '③' },
    ],
    stats: {
      // bid medio per fascia di centrale + spesa risorse per giocatore + meta
      avgBidByTier: [
        { tier: 'Centrali ≤15', avg: 14 },
        { tier: 'Centrali 16-30', avg: 24 },
        { tier: 'Centrali 31+', avg: 39 },
      ],
      spendByPlayer: [
        { id: 'p-anna',  name: 'Anna',  hue: 350, plants: 184, resources: 132, connections: 211 },
        { id: 'p-marco', name: 'Marco', hue: 262, plants: 203, resources: 158, connections: 226 },
        { id: 'p-sara',  name: 'Sara',  hue: 38,  plants: 171, resources: 119, connections: 198 },
        { id: 'p-luca',  name: 'Luca',  hue: 174, plants: 149, resources: 141, connections: 176 },
        { id: 'p-dario', name: 'Dario', hue: 200, plants: 138, resources: 104, connections: 162 },
      ],
      facts: [
        { lb: 'Round totali', v: '14' },
        { lb: 'Centrali messe all\u2019asta', v: '31' },
        { lb: 'Step 3 al round', v: '11' },
        { lb: 'Risorsa più scarsa', v: 'Uranio' },
      ],
    },
  };

  window.PG = {
    game, agent, RES, RES_ORDER, SUPPLY, PLANTS, market, resourceMarket,
    nextPrice, remaining, roster, byId, phases, phaseState, baseOrder, turnOrder,
    auction, ds, counterWidgets, network, plantsCapOf, summary,
  };
})();

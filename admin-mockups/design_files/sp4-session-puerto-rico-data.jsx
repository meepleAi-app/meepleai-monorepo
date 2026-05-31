/* MeepleAI SP4 · Puerto Rico — DATASET  (window.PR)
   Game-specific data layer for the Puerto Rico extension of the universal
   session skeleton. Pure static fixtures — no game simulation.

   ── What this feeds ────────────────────────────────────────────────────────
   • Skeleton reuse  : PR.ds drives the reused TopBar · ChatAgentPanel ·
                       ActionLogTimeline (same prop shape as the skeleton).
   • Polymorphic     : PR.scoring (scoreType=Points) + PR.breakdown feed the
     renderers          reused ScoringPanelRenderer; PR.turn/PR.turnState feed
                       TurnIndicatorRenderer (Sequential branch / phase stepper
                       over the role sequence); PR.counterWidgets feed
                       ToolkitRenderer (the JSON CounterTools, mapped).
   • PR-specific UI  : PR.roster (per-player mats) · PR.roles · PR.shared
                       (galleons / trading house / plantations / colonist ship)
                       · PR.summary (post-game breakdown).

   ── Assumptions (flagged per _common.md) ───────────────────────────────────
   • Default variant = 4 players (3-5 supported; Prospector 2 shown disabled,
     it only exists at 5p). Round 4, current role-phase = Captain.
   • Goods colors are the raw hex values from puerto-rico.json — the ONE
     sanctioned exception to "tokens-only", per the brief.
   • Numbers are believable, not a faithful rules engine.                       */

(function () {
  // ─── Goods (colors verbatim from puerto-rico.json CounterTools) ───────────
  const GOODS = [
    { id: 'corn',    lb: 'Mais',     color: '#f4d03f', dark: '#1a1407' },
    { id: 'indigo',  lb: 'Indaco',   color: '#2980b9', dark: '#fff'    },
    { id: 'sugar',   lb: 'Zucchero', color: '#ecf0f1', dark: '#2b1f12' },
    { id: 'tobacco', lb: 'Tabacco',  color: '#7d6608', dark: '#fff'    },
    { id: 'coffee',  lb: 'Caffè',    color: '#6e2c00', dark: '#fff'    },
  ];
  const GOOD = Object.fromEntries(GOODS.map(g => [g.id, g]));
  const SUPPLY = { // non-goods counters from JSON
    doubloon: { lb: 'Dobloni',  color: '#f1c40f', dark: '#1a1407' },
    colonist: { lb: 'Colono',   color: '#8b6914', dark: '#fff'    },
    vp:       { lb: 'PV',       color: '#9b59b6', dark: '#fff'    },
    quarry:   { lb: 'Cava',     color: '#8a8d8f', dark: '#fff'    },
  };

  // ─── Role cards (8; Prospector 2 only at 5 players) ───────────────────────
  const ROLES = [
    { id: 'settler',   lb: 'Colono',      icon: '🌱', effect: 'Prendi 1 tessera piantagione', priv: 'Privilegio: può prendere una cava', e: 'toolkit' },
    { id: 'mayor',     lb: 'Sindaco',     icon: '🏛️', effect: 'Distribuisci i nuovi coloni', priv: 'Privilegio: +1 colono dalla nave', e: 'player' },
    { id: 'builder',   lb: 'Costruttore', icon: '🔨', effect: 'Costruisci 1 edificio',        priv: 'Privilegio: sconto di 1 doblone',  e: 'game' },
    { id: 'craftsman', lb: 'Artigiano',   icon: '⚒️', effect: 'Tutti producono merci',         priv: 'Privilegio: +1 merce extra',       e: 'kb' },
    { id: 'trader',    lb: 'Mercante',    icon: '⚖️', effect: 'Vendi 1 merce alla casa',       priv: 'Privilegio: +1 doblone sul prezzo', e: 'chat' },
    { id: 'captain',   lb: 'Capitano',    icon: '⛵', effect: 'Spedisci merci sui galeoni',    priv: 'Privilegio: +1 PV alla 1ª spedizione', e: 'session' },
    { id: 'prospector1', lb: 'Cercatore I', icon: '💰', effect: 'Prendi 1 doblone (solo tu)',   priv: 'Nessuna azione per gli altri',     e: 'agent' },
    { id: 'prospector2', lb: 'Cercatore II', icon: '💰', effect: 'Prendi 1 doblone (solo tu)',  priv: 'Solo nella partita a 5 giocatori', e: 'agent', fivePlayerOnly: true },
  ];

  // ─── Plantation / building factories ──────────────────────────────────────
  const pl = (t, c = false) => ({ t, c });                 // tile type + colonist?
  // building: name, kind small|prod|large, printed VP, colonist slots, filled
  const bd = (n, kind, vp, slots, filled) => ({ n, kind, vp, slots, filled });

  // ─── Roster — 4 players, full mats ────────────────────────────────────────
  // hue drives avatar; entity-aligned hues reused from the palette.
  const roster = [
    {
      id: 'p-marco', name: 'Marco', hue: 262, governor: true, current: true,
      score: 34, turnDelta: 4, role: 'captain',
      mat: {
        doubloons: 4, colonistsAvail: 1, vp: 34,
        storehouse: { corn: 1, indigo: 0, sugar: 2, tobacco: 1, coffee: 0 },
        plantations: [
          pl('corn', true), pl('corn', true), pl('indigo', true), pl('indigo', false),
          pl('sugar', true), pl('tobacco', true), pl('quarry', true), pl('quarry', false),
          pl('coffee', false),
        ],
        buildings: [
          bd('Indaco (pic.)', 'prod', 1, 1, 1), bd('Zuccherificio', 'prod', 2, 3, 2),
          bd('Tabacco', 'prod', 3, 3, 2), bd('Mercato grande', 'small', 2, 1, 1),
          bd('Ospizio', 'small', 2, 1, 0), bd('Guild Hall', 'large', 4, 1, 1),
          bd('Fortezza', 'large', 4, 1, 1),
        ],
      },
    },
    {
      id: 'p-anna', name: 'Anna', hue: 350, role: 'settler',
      score: 30, turnDelta: 0,
      mat: {
        doubloons: 2, colonistsAvail: 0, vp: 30,
        storehouse: { corn: 0, indigo: 3, sugar: 1, tobacco: 0, coffee: 1 },
        plantations: [
          pl('indigo', true), pl('indigo', true), pl('indigo', true), pl('sugar', true),
          pl('sugar', false), pl('corn', true), pl('coffee', true), pl('quarry', false),
        ],
        buildings: [
          bd('Indaco', 'prod', 2, 3, 3), bd('Zucchero (pic.)', 'prod', 1, 1, 1),
          bd('Caffè', 'prod', 3, 2, 1), bd('Magazzino piccolo', 'small', 1, 1, 1),
          bd('Porto', 'small', 3, 1, 1), bd('Residence', 'large', 4, 1, 1),
        ],
      },
    },
    {
      id: 'p-luca', name: 'Luca', hue: 174, role: 'craftsman',
      score: 27, turnDelta: 0,
      mat: {
        doubloons: 5, colonistsAvail: 2, vp: 27,
        storehouse: { corn: 2, indigo: 0, sugar: 0, tobacco: 3, coffee: 1 },
        plantations: [
          pl('tobacco', true), pl('tobacco', true), pl('corn', true), pl('corn', false),
          pl('coffee', true), pl('quarry', true), pl('quarry', true),
        ],
        buildings: [
          bd('Tabacco', 'prod', 3, 3, 3), bd('Caffè', 'prod', 3, 2, 2),
          bd('Fabbrica', 'small', 3, 1, 1), bd('Università', 'small', 3, 1, 0),
          bd('City Hall', 'large', 4, 1, 1),
        ],
      },
    },
    {
      id: 'p-sara', name: 'Sara', hue: 38, role: 'builder',
      score: 22, turnDelta: 0,
      mat: {
        doubloons: 3, colonistsAvail: 0, vp: 22,
        storehouse: { corn: 1, indigo: 1, sugar: 2, tobacco: 0, coffee: 0 },
        plantations: [
          pl('corn', true), pl('sugar', true), pl('sugar', true), pl('indigo', true),
          pl('corn', false), pl('quarry', false),
        ],
        buildings: [
          bd('Zuccherificio', 'prod', 2, 3, 2), bd('Indaco (pic.)', 'prod', 1, 1, 1),
          bd('Mercato piccolo', 'small', 1, 1, 1), bd('Ufficio', 'small', 2, 1, 0),
        ],
      },
    },
  ];

  // ─── Scoring (feeds reused ScoringPanelRenderer · Points branch) ──────────
  // categories mapped 1:1 from puerto-rico.json ScoringTemplate.Categories,
  // computation values (Sum/Count/Custom) preserved so ComputationBadge renders.
  const scoring = {
    scoreType: 'Points',
    categories: [
      { id: 'buildings-vp',        label: 'Edifici (PV stampati)', computation: 'Sum',    weight: 1, description: 'PV stampati su ogni edificio posseduto (1-4 per edificio).' },
      { id: 'shipped-goods-vp',    label: 'Merci spedite',          computation: 'Count',  weight: 1, description: '1 chip PV per merce spedita su un galeone in fase Capitano.' },
      { id: 'large-building-bonus',label: 'Bonus edifici grandi',   computation: 'Custom', weight: 1, description: 'Bonus di fine partita degli edifici viola (Guild Hall, City Hall, Residence, Fortezza, Custom House…).' },
    ],
  };
  // live breakdown (sums == roster.score)
  const breakdown = {
    'p-marco': { 'buildings-vp': 14, 'shipped-goods-vp': 12, 'large-building-bonus': 8 },
    'p-anna':  { 'buildings-vp': 13, 'shipped-goods-vp': 12, 'large-building-bonus': 5 },
    'p-luca':  { 'buildings-vp': 12, 'shipped-goods-vp': 9,  'large-building-bonus': 6 },
    'p-sara':  { 'buildings-vp': 11, 'shipped-goods-vp': 9,  'large-building-bonus': 2 },
  };

  // ─── Turn (feeds reused TurnIndicatorRenderer · Sequential) ───────────────
  // phase stepper walks the role sequence of the round; activeIndex = role
  // currently being executed (Captain).
  const rolePhases = ROLES.filter(r => !r.fivePlayerOnly).map(r => r.lb);
  const turn = {
    turnOrderType: 'Sequential',
    phases: rolePhases,
    direction: 'clockwise',
    turnActions: ['choose-role', 'execute-role-action', 'all-others-clockwise', 'take-privilege', 'pass-governor'],
  };
  const turnState = { phaseIndex: 5 /* Captain */ };

  // this round's role selections (governor Marco first, clockwise)
  // chosen roles carry the chooser; unchosen roles accrue doubloons.
  const roleState = {
    round: 4,
    activeRole: 'captain',
    chosenBy: { captain: 'p-marco', settler: 'p-anna', craftsman: 'p-luca', builder: 'p-sara' },
    doubloonsOn: { mayor: 2, trader: 1, prospector1: 3, prospector2: 0 },
  };

  // ─── Shared state (PR-specific board) ─────────────────────────────────────
  const shared = {
    galleons: [
      { id: 'g1', cap: 4, good: 'corn',   loaded: 4 },        // full
      { id: 'g2', cap: 6, good: 'coffee', loaded: 3 },        // partial
      { id: 'g3', cap: 7, good: null,     loaded: 0 },        // empty
    ],
    tradingHouse: {
      slots: [
        { good: 'indigo', price: 1 },
        { good: 'sugar',  price: 2 },
        null, null,
      ],
      basePrice: { corn: 0, indigo: 1, sugar: 2, tobacco: 3, coffee: 4 },
    },
    plantations: { // available face-up tiles + 1 face-down + quarry stack
      faceUp: ['corn', 'indigo', 'indigo', 'sugar', 'tobacco'],
      faceDown: 1,
      quarryStack: 4,
    },
    buildingSupply: [
      { n: 'Caffè',          kind: 'prod',  vp: 3, cost: 6, left: 2 },
      { n: 'Università',     kind: 'small', vp: 3, cost: 8, left: 1 },
      { n: 'Porto',          kind: 'small', vp: 3, cost: 8, left: 2 },
      { n: 'Custom House',   kind: 'large', vp: 4, cost: 10, left: 1 },
      { n: 'Wharf',          kind: 'small', vp: 3, cost: 9, left: 2 },
    ],
    colonistShip: { incoming: 4, supply: 17, onShip: 4 },
  };

  // counter widgets — JSON CounterTools mapped to a ResourceManager widget so
  // the reused ToolkitRenderer can render PR's per-player counters in galleries.
  const counterWidgets = [
    {
      id: 'w-goods', type: 'ResourceManager', isEnabled: true, displayOrder: 0,
      config: {
        shared: false,
        resources: [
          { label: 'Dobloni', value: 4, max: 54 },
          { label: 'Coloni',  value: 1, max: 30 },
          { label: 'PV chip', value: 34, max: 75 },
          { label: 'Mais',    value: 1, max: 20 },
          { label: 'Indaco',  value: 0, max: 20 },
        ],
      },
    },
    {
      id: 'w-score', type: 'ScoreTracker', isEnabled: true, displayOrder: 1,
      config: {},
    },
    {
      id: 'w-turn', type: 'TurnManager', isEnabled: true, displayOrder: 2,
      config: { phaseBased: true },
    },
    // disabled — PR excludes dice + timer (per JSON ExcludedTools)
    { id: 'w-dice',  type: 'RandomGenerator', isEnabled: false, displayOrder: 3, config: { name: 'Dadi (esclusi)' } },
    { id: 'w-note',  type: 'NoteManager', isEnabled: false, displayOrder: 4, config: {} },
    { id: 'w-board', type: 'Whiteboard', isEnabled: false, displayOrder: 5, config: {} },
  ];

  // ─── ds — drives reused skeleton chrome (TopBar / ChatAgent / ActionLog) ──
  const ds = {
    game: {
      title: 'Puerto Rico', emoji: '🏝️',
      cover: 'linear-gradient(135deg, hsl(28,80%,52%), hsl(190,55%,42%))',
      meta: '3-5 giocatori · ~120 min · Round 4',
    },
    agent: { name: 'Esperto Puerto Rico', emoji: '🤖', latency: 'Risponde in 1-2s' },
    session: { elapsed: '1h 02m' },
    players: roster, // carries name/hue/score/turnDelta + .mat
    // scoring + breakdown + turn so the polymorphic renderers can read `ds`
    scoring, breakdown, turn, turnState, meta: 'Sequenza ruoli · Round 4',
    chat: [
      { id: 'c1', from: 'user',  t: '15:18', text: 'Posso spedire merci di tipi diversi sullo stesso galeone?' },
      { id: 'c2', from: 'agent', t: '15:18', text: 'No — ogni galeone trasporta un solo tipo di merce. Una nave già caricata con caffè accetta solo altro caffè finché non viene svuotata a fine fase.', citations: ['Puerto Rico §Capitano'] },
      { id: 'c3', from: 'user',  t: '15:24', text: 'Il Cercatore d’oro fa agire anche gli altri giocatori?' },
      { id: 'c4', from: 'agent', t: '15:24', text: 'No: il Cercatore è l’unico ruolo senza azione condivisa. Solo chi lo sceglie prende 1 doblone dalla banca.', citations: ['Puerto Rico §Ruoli', 'FAQ #4'] },
    ],
    suggestions: ['Bonus Guild Hall?', 'Prezzi casa commerciale', 'Quando finisce la partita?'],
    events: [
      { kind: 'game-start', who: null,      t: '14:16', text: 'Partita avviata · <strong>4 giocatori</strong> · governatore <strong>Marco</strong>' },
      { kind: 'turn-change', who: 'p-anna', t: '15:05', text: 'sceglie <strong>Colono</strong> · prende una piantagione di indaco' },
      { kind: 'turn-change', who: 'p-luca', t: '15:09', text: 'sceglie <strong>Artigiano</strong> · tutti producono merci' },
      { kind: 'score-update',who: 'p-luca', t: '15:10', text: 'produce <strong>3 tabacco</strong> + 1 caffè (privilegio)' },
      { kind: 'turn-change', who: 'p-sara', t: '15:13', text: 'sceglie <strong>Costruttore</strong> · costruisce <strong>Ufficio</strong> (–2 dobloni)' },
      { kind: 'turn-change', who: 'p-marco',t: '15:17', text: 'sceglie <strong>Capitano</strong> · apre la fase di spedizione' },
      { kind: 'custom',      who: 'p-marco',t: '15:18', text: 'spedisce <strong>4 mais</strong> sul galeone da 4 · <strong>+4 PV</strong>' },
    ],
    notes: 'Marco controlla 2 cave → costruzioni scontate.\nAnna engine indaco (3 tessere + edificio pieno).\nLuca siede su 5 dobloni: occhio al Costruttore prossimo round.',
    photos: [
      { lb: 'Plancia T20', g: 'linear-gradient(135deg, hsl(30,55%,55%), hsl(150,45%,40%))' },
      { lb: 'Casa comm.',  g: 'linear-gradient(135deg, hsl(200,55%,55%), hsl(255,45%,45%))' },
    ],
  };

  // ─── Summary (post-game) ──────────────────────────────────────────────────
  // shipped goods counts per player (sum == shippedVP)
  const shipped = {
    'p-marco': { corn: 6, coffee: 4, sugar: 3, tobacco: 2, indigo: 1 }, // 16
    'p-anna':  { indigo: 6, sugar: 5, corn: 4, coffee: 2, tobacco: 1 }, // 18
    'p-luca':  { coffee: 5, tobacco: 4, corn: 3, sugar: 2, indigo: 0 }, // 14
    'p-sara':  { corn: 5, sugar: 4, indigo: 3, tobacco: 2, coffee: 2 }, // 16
  };
  // building VP lists (sum == buildingVP)
  const buildingVP = {
    'p-marco': [['Indaco (pic.)', 1], ['Zuccherificio', 2], ['Tabacco', 3], ['Mercato grande', 2], ['Ospizio', 2], ['Guild Hall', 4], ['Fortezza', 4]], // 18
    'p-anna':  [['Indaco', 2], ['Zucchero (pic.)', 1], ['Caffè', 3], ['Magazzino piccolo', 1], ['Porto', 3], ['Università', 3], ['Residence', 4]],         // 17
    'p-luca':  [['Tabacco', 3], ['Caffè', 3], ['Fabbrica', 3], ['Università', 3], ['City Hall', 4]],                                                        // 16
    'p-sara':  [['Zuccherificio', 2], ['Indaco (pic.)', 1], ['Mercato grande', 2], ['Ufficio', 2], ['Fabbrica', 3], ['Custom House', 4]],                   // 14 -> bump
  };
  // large building end-game bonuses (sum == largeBonus)
  const largeBonus = {
    'p-marco': [
      { n: 'Guild Hall', rule: '+1 PV per edificio di produzione (×2 se grande)', value: 9 },
      { n: 'Fortezza',   rule: '+1 PV ogni 3 coloni piazzati', value: 5 },
    ], // 14
    'p-anna': [
      { n: 'Residence', rule: '+PV per spazi piantagione occupati (9→4, 10→5, 11→6, 12→7)', value: 10 },
    ], // 10
    'p-luca': [
      { n: 'City Hall', rule: '+1 PV per edificio viola (non di produzione)', value: 11 },
    ], // 11
    'p-sara': [
      { n: 'Custom House', rule: '+1 PV ogni 4 chip PV da spedizione', value: 7 },
    ], // 7
  };
  // round-by-round role recap (governor rotates clockwise)
  const recap = [
    { round: 1, governor: 'p-marco', picks: [['p-marco', 'settler'], ['p-anna', 'builder'], ['p-luca', 'craftsman'], ['p-sara', 'mayor']] },
    { round: 2, governor: 'p-anna',  picks: [['p-anna', 'craftsman'], ['p-luca', 'builder'], ['p-sara', 'trader'], ['p-marco', 'captain']] },
    { round: 3, governor: 'p-luca',  picks: [['p-luca', 'settler'], ['p-sara', 'craftsman'], ['p-marco', 'builder'], ['p-anna', 'captain']] },
    { round: 4, governor: 'p-sara',  picks: [['p-sara', 'mayor'], ['p-marco', 'craftsman'], ['p-anna', 'trader'], ['p-luca', 'captain']] },
    { round: 5, governor: 'p-marco', picks: [['p-marco', 'captain'], ['p-anna', 'settler'], ['p-luca', 'builder'], ['p-sara', 'prospector1']] },
  ];
  // per-player stats
  const stats = {
    'p-marco': { vpPerMin: 0.45, topRole: 'builder', topRoleCount: 3, shippedTot: 16, builtTot: 7, colonists: 11 },
    'p-anna':  { vpPerMin: 0.42, topRole: 'captain', topRoleCount: 3, shippedTot: 18, builtTot: 7, colonists: 10 },
    'p-luca':  { vpPerMin: 0.38, topRole: 'craftsman', topRoleCount: 4, shippedTot: 14, builtTot: 5, colonists: 9 },
    'p-sara':  { vpPerMin: 0.35, topRole: 'craftsman', topRoleCount: 3, shippedTot: 16, builtTot: 6, colonists: 8 },
  };
  // final totals (building + shipped + large)
  const finalScore = {};
  roster.forEach(p => {
    const b = buildingVP[p.id].reduce((s, x) => s + x[1], 0);
    const sh = Object.values(shipped[p.id]).reduce((s, x) => s + x, 0);
    const lg = largeBonus[p.id].reduce((s, x) => s + x.value, 0);
    finalScore[p.id] = { building: b, shipped: sh, large: lg, total: b + sh + lg };
  });
  const endTrigger = 'Esaurimento dei chip PV durante la fase Capitano (round 5).';

  const summary = { shipped, buildingVP, largeBonus, recap, stats, finalScore, endTrigger, durationMin: 108 };

  window.PR = {
    GOODS, GOOD, SUPPLY, ROLES, roster, scoring, breakdown,
    turn, turnState, roleState, rolePhases, shared, counterWidgets, ds, summary,
  };
})();

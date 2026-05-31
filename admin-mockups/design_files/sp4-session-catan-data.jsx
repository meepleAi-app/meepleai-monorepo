/* MeepleAI SP4 · Coloni di Catan — DATASET  (window.CATAN)
   Game-specific data for the Catan extension of the universal session skeleton.

   Also provides a SHIM for window.SkeletonData.STATES so the skeleton parts
   (sp4-session-skeleton-parts.jsx) can resolve the canonical-state selector
   without loading the full skeleton dataset (skeleton-data is built for the
   generic demo). Load AFTER sp4-parts-common.jsx / skeleton-renderers and
   BEFORE skeleton-parts.

   Source of truth for the toolkit shape: catan.json (Coloni di Catan, base game,
   3-4 players). Resource colors are taken verbatim from catan.json — the ONLY
   non-token colors allowed by the brief. Everything else uses tokens.css vars. */

(function () {
  // ─── canonical-state selector shim (5 stati) ──────────────────────────────
  if (!window.SkeletonData) window.SkeletonData = {};
  if (!window.SkeletonData.STATES) {
    window.SkeletonData.STATES = [
      { id: 'default', lb: 'Default' },
      { id: 'empty',   lb: 'Empty' },
      { id: 'loading', lb: 'Loading' },
      { id: 'error',   lb: 'Error' },
      { id: 'sse',     lb: 'SSE-disc' },
    ];
  }

  // ─── resource / terrain palette (verbatim from catan.json) ────────────────
  // each: label, board terrain name, tile color, number-token text color,
  //       a darker stripe for the light terrain "texture" pattern.
  const RES = {
    wood:  { id: 'wood',  lb: 'Legno',  res: 'Legno',  terrain: 'Foresta',  letter: 'L', icon: '🌲', color: '#3f8f56', stripe: '#36794a', ink: '#ffffff' },
    brick: { id: 'brick', lb: 'Argilla',res: 'Argilla',terrain: 'Collina',  letter: 'A', icon: '🧱', color: '#c0584a', stripe: '#a8493d', ink: '#ffffff' },
    sheep: { id: 'sheep', lb: 'Lana',   res: 'Lana',   terrain: 'Pascolo',  letter: 'P', icon: '🐑', color: '#a3c95e', stripe: '#92b850', ink: '#1f2a0e' },
    wheat: { id: 'wheat', lb: 'Grano',  res: 'Grano',  terrain: 'Campo',    letter: 'G', icon: '🌾', color: '#e7b53d', stripe: '#d4a32c', ink: '#3a2a06' },
    ore:   { id: 'ore',   lb: 'Minerale',res:'Minerale',terrain:'Montagna', letter: 'M', icon: '⛰️', color: '#8a93a0', stripe: '#79828f', ink: '#ffffff' },
    desert:{ id: 'desert',lb: 'Deserto',res: '—',      terrain: 'Deserto',  letter: '·', icon: '🏜️', color: '#d9c08a', stripe: '#cbae73', ink: '#5a4423' },
  };
  // ordered list of the 5 producing resources (bank / hand / trade order)
  const RES_ORDER = ['wood', 'brick', 'sheep', 'wheat', 'ore'];

  // ─── players (4) — Catan piece colors + avatar hue ────────────────────────
  // pc = piece color (settlements/cities/roads); pcInk = outline ink.
  const PLAYERS = [
    { id: 'marco', name: 'Marco', hue: 4,   pc: '#cf4036', pcInk: '#fff', vp: 8, turnDelta: 1, current: true,
      hand: { wood: 2, brick: 1, sheep: 0, wheat: 3, ore: 2 },
      pieces: { settlements: 3, cities: 2, roads: 8 },     // BUILT
      dev: { held: 2, knightsPlayed: 1, vpCards: 1 },
      badges: { longestRoad: true, largestArmy: false }, longestRoadLen: 6, knights: 1 },
    { id: 'anna', name: 'Anna', hue: 212, pc: '#2f72c4', pcInk: '#fff', vp: 7, turnDelta: 0,
      hand: { wood: 1, brick: 2, sheep: 3, wheat: 0, ore: 1 },
      pieces: { settlements: 3, cities: 1, roads: 5 },
      dev: { held: 1, knightsPlayed: 3, vpCards: 0 },
      badges: { longestRoad: false, largestArmy: true }, longestRoadLen: 4, knights: 3 },
    { id: 'luca', name: 'Luca', hue: 32,  pc: '#e0902a', pcInk: '#3a2606', vp: 5, turnDelta: 0,
      hand: { wood: 0, brick: 1, sheep: 1, wheat: 1, ore: 0 },
      pieces: { settlements: 3, cities: 1, roads: 6 },
      dev: { held: 0, knightsPlayed: 1, vpCards: 0 },
      badges: { longestRoad: false, largestArmy: false }, longestRoadLen: 5, knights: 1 },
    { id: 'sara', name: 'Sara', hue: 45,  pc: '#e9e3d4', pcInk: '#4a3a1e', vp: 4, turnDelta: 0,
      hand: { wood: 3, brick: 0, sheep: 2, wheat: 1, ore: 1 },
      pieces: { settlements: 4, cities: 0, roads: 4 },
      dev: { held: 1, knightsPlayed: 0, vpCards: 0 },
      badges: { longestRoad: false, largestArmy: false }, longestRoadLen: 3, knights: 0 },
  ];
  // derived "score" field used by the skeleton ScoringPanelRenderer (public VP)
  PLAYERS.forEach(p => { p.score = p.vp; });

  const TOTAL_PIECES = { settlements: 5, cities: 4, roads: 15 };

  // ─── hex board (19 tiles, flat-top, columns 3·4·5·4·3) ────────────────────
  // col 0..4 (left→right); rowInCol top→bottom. terrain + number token.
  // Distribution: 4 wood · 4 sheep · 4 wheat · 3 brick · 3 ore · 1 desert.
  // Numbers: full standard set 2·3·3·4·4·5·5·6·6·8·8·9·9·10·10·11·11·12.
  const HEXES = [
    // col 0
    { id: 'h0',  col: 0, row: 0, t: 'ore',   n: 10 },
    { id: 'h1',  col: 0, row: 1, t: 'sheep', n: 2 },
    { id: 'h2',  col: 0, row: 2, t: 'wood',  n: 9 },
    // col 1
    { id: 'h3',  col: 1, row: 0, t: 'wheat', n: 12 },
    { id: 'h4',  col: 1, row: 1, t: 'brick', n: 6 },
    { id: 'h5',  col: 1, row: 2, t: 'sheep', n: 4 },
    { id: 'h6',  col: 1, row: 3, t: 'brick', n: 10 },
    // col 2 (center)
    { id: 'h7',  col: 2, row: 0, t: 'wheat', n: 9 },
    { id: 'h8',  col: 2, row: 1, t: 'wood',  n: 11 },
    { id: 'h9',  col: 2, row: 2, t: 'desert', n: null },
    { id: 'h10', col: 2, row: 3, t: 'ore',   n: 3 },
    { id: 'h11', col: 2, row: 4, t: 'wheat', n: 8 },
    // col 3
    { id: 'h12', col: 3, row: 0, t: 'wood',  n: 8 },
    { id: 'h13', col: 3, row: 1, t: 'sheep', n: 3 },
    { id: 'h14', col: 3, row: 2, t: 'brick', n: 4 },
    { id: 'h15', col: 3, row: 3, t: 'wheat', n: 5 },
    // col 4
    { id: 'h16', col: 4, row: 0, t: 'wood',  n: 5 },
    { id: 'h17', col: 4, row: 1, t: 'ore',   n: 6 },
    { id: 'h18', col: 4, row: 2, t: 'sheep', n: 11 },
  ];
  const COL_HEIGHTS = [3, 4, 5, 4, 3];

  // robber position (moved off the desert after a 7 earlier in the game)
  const ROBBER_HEX = 'h6';

  // ─── pieces on the board ──────────────────────────────────────────────────
  // settlements/cities reference a hex + corner index (0=E, going clockwise in
  // screen space: 0 E, 1 SE, 2 SW, 3 W, 4 NW, 5 NE). roads reference hex + edge
  // index (0 SE-edge … rotating). Curated for a believable network; exact graph
  // connectivity is illustrative.
  const SETTLEMENTS = [
    { p: 'marco', hex: 'h2',  corner: 5 },
    { p: 'marco', hex: 'h8',  corner: 4 },
    { p: 'marco', hex: 'h12', corner: 5 },
    { p: 'anna',  hex: 'h7',  corner: 0 },
    { p: 'anna',  hex: 'h11', corner: 1 },
    { p: 'anna',  hex: 'h18', corner: 4 },
    { p: 'luca',  hex: 'h5',  corner: 2 },
    { p: 'luca',  hex: 'h13', corner: 0 },
    { p: 'luca',  hex: 'h10', corner: 2 },
    { p: 'sara',  hex: 'h1',  corner: 4 },
    { p: 'sara',  hex: 'h3',  corner: 5 },
    { p: 'sara',  hex: 'h16', corner: 5 },
    { p: 'sara',  hex: 'h14', corner: 1 },
  ];
  const CITIES = [
    { p: 'marco', hex: 'h4',  corner: 0 },
    { p: 'marco', hex: 'h0',  corner: 1 },
    { p: 'anna',  hex: 'h15', corner: 2 },
    { p: 'luca',  hex: 'h17', corner: 3 },
  ];
  // roads: hex + edge (0..5 starting at NE edge going clockwise)
  const ROADS = [
    // Marco — the longest road (top arc)
    { p: 'marco', hex: 'h2', edge: 5 }, { p: 'marco', hex: 'h2', edge: 0 },
    { p: 'marco', hex: 'h8', edge: 4 }, { p: 'marco', hex: 'h8', edge: 5 },
    { p: 'marco', hex: 'h4', edge: 0 }, { p: 'marco', hex: 'h4', edge: 1 },
    { p: 'marco', hex: 'h0', edge: 1 }, { p: 'marco', hex: 'h12', edge: 5 },
    // Anna
    { p: 'anna', hex: 'h7', edge: 0 }, { p: 'anna', hex: 'h11', edge: 1 },
    { p: 'anna', hex: 'h11', edge: 0 }, { p: 'anna', hex: 'h15', edge: 2 },
    { p: 'anna', hex: 'h18', edge: 4 },
    // Luca
    { p: 'luca', hex: 'h5', edge: 2 }, { p: 'luca', hex: 'h10', edge: 2 },
    { p: 'luca', hex: 'h13', edge: 0 }, { p: 'luca', hex: 'h17', edge: 3 },
    { p: 'luca', hex: 'h13', edge: 3 }, { p: 'luca', hex: 'h14', edge: 4 },
    // Sara
    { p: 'sara', hex: 'h1', edge: 4 }, { p: 'sara', hex: 'h3', edge: 5 },
    { p: 'sara', hex: 'h16', edge: 5 }, { p: 'sara', hex: 'h14', edge: 1 },
  ];

  // ─── ports (harbors) on the coastline ─────────────────────────────────────
  // type: 'generic' (3:1) or a resource id (2:1). anchored to a hex edge.
  const PORTS = [
    { type: 'generic', hex: 'h0',  edge: 4, ratio: '3:1' },
    { type: 'wheat',   hex: 'h3',  edge: 5, ratio: '2:1' },
    { type: 'ore',     hex: 'h16', edge: 0, ratio: '2:1' },
    { type: 'generic', hex: 'h18', edge: 1, ratio: '3:1' },
    { type: 'wood',    hex: 'h2',  edge: 2, ratio: '2:1' },
    { type: 'brick',   hex: 'h10', edge: 3, ratio: '2:1' },
    { type: 'sheep',   hex: 'h11', edge: 2, ratio: '2:1' },
    { type: 'generic', hex: 'h15', edge: 1, ratio: '3:1' },
  ];
  // ports actually owned/usable by each player (settlement on a port vertex)
  const PLAYER_PORTS = {
    marco: [{ type: 'generic', ratio: '3:1' }, { type: 'wood', ratio: '2:1' }],
    anna:  [{ type: 'wheat', ratio: '2:1' }],
    luca:  [{ type: 'brick', ratio: '2:1' }],
    sara:  [{ type: 'generic', ratio: '3:1' }],
  };

  // ─── dice ─────────────────────────────────────────────────────────────────
  // last roll = 8 → highlights producing hexes (h11 wheat, h12 wood); robber on
  // h6 blocks nothing for an 8. History newest-first.
  const DICE = {
    last: [5, 3],            // sum 8
    history: [8, 6, 11, 4, 9, 7, 5, 8, 10, 3, 6, 9],
  };

  // ─── shared bank ──────────────────────────────────────────────────────────
  const BANK = { wood: 11, brick: 13, sheep: 14, wheat: 9, ore: 12 };  // /19 each
  const DEV_DECK = { remaining: 18, total: 25 };
  // dev card breakdown of the full deck (for Build/Dev reference)
  const DEV_TYPES = [
    { id: 'knight',     lb: 'Cavaliere',       icon: '⚔️', total: 14, e: 'event',   desc: 'Sposta il ladro e ruba 1 carta. 3 cavalieri = Armata più grande.' },
    { id: 'vp',         lb: 'Punto Vittoria',  icon: '⭐', total: 5,  e: 'toolkit', desc: '+1 PV, tenuta segreta fino alla vittoria.' },
    { id: 'road',       lb: 'Costruzione strade', icon: '🛤️', total: 2, e: 'session', desc: 'Costruisci 2 strade gratis.' },
    { id: 'plenty',     lb: 'Anno dell\u2019abbondanza', icon: '🎁', total: 2, e: 'kb', desc: 'Prendi 2 risorse a scelta dalla banca.' },
    { id: 'monopoly',   lb: 'Monopolio',       icon: '💰', total: 2,  e: 'agent',   desc: 'Tutti ti danno tutte le carte di una risorsa.' },
  ];

  // ─── build costs reference ────────────────────────────────────────────────
  const BUILD_COSTS = [
    { id: 'road',       lb: 'Strada',       icon: '🛤️', cost: { wood: 1, brick: 1 }, vp: 0, e: 'session', note: 'Connette i tuoi insediamenti. 5+ = Strada più lunga.' },
    { id: 'settlement', lb: 'Insediamento', icon: '🏠', cost: { wood: 1, brick: 1, sheep: 1, wheat: 1 }, vp: 1, e: 'game', note: '1 PV. Produce su 1 risorsa per vertice.' },
    { id: 'city',       lb: 'Città',        icon: '🏛️', cost: { wheat: 2, ore: 3 }, vp: 2, e: 'player', note: '2 PV. Sostituisce un insediamento, doppia produzione.' },
    { id: 'dev',        lb: 'Carta sviluppo', icon: '🃏', cost: { sheep: 1, wheat: 1, ore: 1 }, vp: 0, e: 'toolkit', note: 'Pesca dal mazzo: cavaliere, PV, progresso.' },
  ];

  // ─── scoring template (from catan.json ScoringTemplate) ───────────────────
  const SCORING = {
    scoreType: 'Points',
    defaultUnit: 'points',
    categories: [
      { id: 'settlements',  label: 'Insediamenti',     computation: 'Count',  weight: 1, description: '1 PV per insediamento (max 5).' },
      { id: 'cities',       label: 'Città',            computation: 'Count',  weight: 2, description: '2 PV per città (max 4).' },
      { id: 'longest-road', label: 'Strada più lunga', computation: 'Custom', weight: 2, description: '2 PV a chi ha la strada più lunga ≥5 segmenti.' },
      { id: 'largest-army', label: 'Armata più grande', computation: 'Custom', weight: 2, description: '2 PV a chi ha l\u2019armata più grande ≥3 cavalieri.' },
      { id: 'vp-cards',     label: 'Carte PV',         computation: 'Sum',    weight: 1, description: '1 PV per carta sviluppo Punto Vittoria (segreta).' },
    ],
    target: 10,
  };
  // breakdown = VP contributed per category (sums to public VP). vp-cards kept
  // secret in live view → shown as hidden; revealed in summary.
  const BREAKDOWN = {
    marco: { settlements: 2, cities: 4, 'longest-road': 2, 'largest-army': 0, 'vp-cards': 1 },
    anna:  { settlements: 3, cities: 2, 'longest-road': 0, 'largest-army': 2, 'vp-cards': 0 },
    luca:  { settlements: 3, cities: 2, 'longest-road': 0, 'largest-army': 0, 'vp-cards': 0 },
    sara:  { settlements: 4, cities: 0, 'longest-road': 0, 'largest-army': 0, 'vp-cards': 0 },
  };

  // ─── turn template (from catan.json TurnTemplate) ─────────────────────────
  const TURN = {
    turnOrderType: 'RoundRobin',
    phases: ['Tira i dadi', 'Commercio', 'Costruisci / sviluppa'],
    turnActions: ['roll-dice', 'trade-with-player', 'trade-with-bank', 'build-road', 'build-settlement', 'build-city', 'buy-dev-card', 'play-dev-card', 'end-turn'],
    direction: 'clockwise',
    rounds: '\u221e',          // open-ended: first to 10 VP
    turnsPerRound: null,
  };
  const TURN_STATE = {
    activePlayerId: 'marco',
    round: 8,
    turnInRound: 1,
    phaseIndex: 2,             // currently in Build / develop
    actionIndex: 5,           // build-city
  };

  // ─── trade panel ──────────────────────────────────────────────────────────
  const TRADE = {
    // incoming offers from other players to the active player (Marco)
    incoming: [
      { id: 'o1', from: 'anna', give: { sheep: 2 }, want: { ore: 1 }, note: 'Mi serve minerale per la città' },
      { id: 'o2', from: 'luca', give: { brick: 1, sheep: 1 }, want: { wheat: 1 }, note: '' },
    ],
    // outgoing offer drafted by the active player
    outgoing: { give: { wheat: 1 }, want: { brick: 1 }, to: 'all', status: 'open' },
  };

  // ─── action log (events) — newest first ───────────────────────────────────
  const EVENTS = [
    { kind: 'score-update', who: 'marco', t: '19:42', text: '<strong>Marco</strong> costruisce una <strong>città</strong> su Foresta-9 → <strong>8 PV</strong>' },
    { kind: 'custom',       who: 'marco', t: '19:41', text: 'Banca: <strong>2 grano + 3 minerale</strong> spesi per la città' },
    { kind: 'turn-change',  who: 'marco', t: '19:40', text: 'Inizia il turno di <strong>Marco</strong> · Round 8' },
    { kind: 'custom',       who: 'anna',  t: '19:38', text: '<strong>Anna</strong> gioca un <strong>Cavaliere</strong> → Armata più grande (3) 🛡️' },
    { kind: 'custom',       who: 'anna',  t: '19:38', text: 'Ladro spostato su <strong>Collina-10</strong>, ruba 1 carta a Luca' },
    { kind: 'custom',       who: 'sara',  t: '19:35', text: 'Scambio <strong>Sara → Luca</strong>: 1 legno ⇄ 1 minerale' },
    { kind: 'custom',       who: 'luca',  t: '19:33', text: '<strong>Luca</strong> tira <strong>7</strong> · scarto carte > 7 · sposta il ladro' },
    { kind: 'score-update', who: 'luca',  t: '19:32', text: '<strong>Luca</strong> costruisce un <strong>insediamento</strong> → 5 PV' },
    { kind: 'turn-change',  who: 'luca',  t: '19:30', text: 'Inizia il turno di <strong>Luca</strong>' },
    { kind: 'game-start',   who: null,    t: '18:18', text: 'Partita avviata · 4 giocatori · disposizione iniziale completata' },
  ];

  // ─── chat with the agent (reused ChatAgentPanel shape) ────────────────────
  const CHAT = [
    { id: 'c1', from: 'user',  t: '19:36', text: 'Se costruisco la città su Foresta-9, perdo la strada più lunga?' },
    { id: 'c2', from: 'agent', t: '19:36', text: 'No: costruire una città non rimuove strade. Mantieni i tuoi 6 segmenti consecutivi, quindi conservi il bonus Strada più lunga (+2 PV).', citations: ['Catan §Costruzione', 'FAQ #7'] },
    { id: 'c3', from: 'user',  t: '19:41', text: 'Con la città arrivo a 8 PV?' },
    { id: 'c4', from: 'agent', t: '19:41', text: '2 insediamenti (2) + 2 città (4) + Strada più lunga (2) = 8 PV pubblici. Con la tua carta PV segreta saresti a 9 — ti manca 1 punto per vincere.', citations: ['Catan §Punteggio'] },
  ];
  const SUGGESTIONS = ['Costo della città?', 'Chi ha l\u2019Armata più grande?', 'Probabilità del 6 e 8'];

  // ─── session / game header (TopBar reads ds.game / ds.session) ────────────
  const GAME = {
    id: 'catan', title: 'Coloni di Catan', emoji: '🎲',
    cover: 'linear-gradient(135deg, hsl(28,70%,52%), hsl(150,45%,40%))',
    meta: 'Euro · trading · 3-4 giocatori · ~80 min',
  };
  const SESSION = { id: 'catan-7', elapsed: '1h 24min', startedAt: '18:18', turn: 29, round: 8 };

  const AGENT = { id: 'a-catan', name: 'Esperto Catan', emoji: '🤖', latency: '~0.7s' };

  const PHOTOS = [
    { lb: 'tavolo', g: 'linear-gradient(135deg,#3f8f56,#e7b53d)' },
    { lb: 'plance', g: 'linear-gradient(135deg,#2f72c4,#8a93a0)' },
    { lb: 'dadi',   g: 'linear-gradient(135deg,#cf4036,#e0902a)' },
  ];

  // ─── stats (for the summary) ──────────────────────────────────────────────
  const STATS = {
    totalTurns: 29, totalRounds: 8, duration: '1h 24min',
    diceCounts: { 2: 3, 3: 8, 4: 9, 5: 11, 6: 14, 7: 13, 8: 15, 9: 10, 10: 8, 11: 6, 12: 2 },
    diceTotal: 99,
    trades: { marco: 7, anna: 9, luca: 6, sara: 4 },
    longestProductionRun: { number: 8, count: 5, hint: '5 volte di fila ha prodotto l\u20198' },
    robberMoves: 13,
    biggestHand: { player: 'sara', n: 9, hint: 'mano più grande mai tenuta' },
  };

  // ─── assembled "ds" object — shape consumed by skeleton parts/renderers ───
  const ds = {
    game: GAME,
    session: SESSION,
    agent: AGENT,
    players: PLAYERS,
    scoring: SCORING,
    breakdown: BREAKDOWN,
    turn: TURN,
    turnState: TURN_STATE,
    chat: CHAT,
    suggestions: SUGGESTIONS,
    events: EVENTS,
    notes: 'Setup: Marco rosso (alto/sx), Anna blu (dx), Luca arancio (centro/basso), Sara bianco (sx). Porto 2:1 legno conteso. Occhio al 7 di Luca.',
    photos: PHOTOS,
  };

  window.CATAN = {
    RES, RES_ORDER, PLAYERS, TOTAL_PIECES,
    HEXES, COL_HEIGHTS, ROBBER_HEX, SETTLEMENTS, CITIES, ROADS, PORTS, PLAYER_PORTS,
    DICE, BANK, DEV_DECK, DEV_TYPES, BUILD_COSTS,
    SCORING, BREAKDOWN, TURN, TURN_STATE, TRADE,
    EVENTS, CHAT, SUGGESTIONS, GAME, SESSION, AGENT, PHOTOS, STATS,
    ds,
  };
})();

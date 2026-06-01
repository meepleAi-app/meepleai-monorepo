/* MeepleAI SP4 · Codenames — DATASET  (window.CN)
   Game-specific data feeding both the reused skeleton renderers and the new
   Codenames widgets. Pure data — no React, no token helpers.

   Also bootstraps window.SkeletonData.STATES (the canonical 5 states) so the
   shared skeleton parts can read it at module-eval time.

   SCENARIOS (switchable in the live view):
     mid      — tight mid-game, Red leads 6/9 vs Blue 3/8, Red guessing OCEAN:3
     match    — match point Blu: Blue 7/8 (1 left), Blue guessing, high tension
     assassin — game over: Red revealed the assassin → instant loss, Blue wins
   The key card (which word is which colour) is FIXED across scenarios — only
   the revealed tiles / clue / phase / log differ. */

(function () {
  // ─── canonical states ─────────────────────────────────────────────────────
  window.SkeletonData = window.SkeletonData || {};
  window.SkeletonData.STATES = [
    { id: 'default', lb: 'Default' },
    { id: 'empty',   lb: 'Empty' },
    { id: 'loading', lb: 'Loading' },
    { id: 'error',   lb: 'Error' },
    { id: 'sse',     lb: 'SSE ✕' },
  ];

  const TEAM = {
    red:  { id: 'red',  name: 'Rossi', entity: 'event', total: 9, spymasterId: 'p-marco', operativeIds: ['p-anna', 'p-luca'] },
    blue: { id: 'blue', name: 'Blu',   entity: 'chat',  total: 8, spymasterId: 'p-sara',  operativeIds: ['p-giulia', 'p-dario'] },
  };

  // ─── roster ───────────────────────────────────────────────────────────────
  const ROSTER = [
    { id: 'p-marco',  name: 'Marco',  hue: 5,   team: 'red',  role: 'spymaster' },
    { id: 'p-anna',   name: 'Anna',   hue: 16,  team: 'red',  role: 'operative' },
    { id: 'p-luca',   name: 'Luca',   hue: 352, team: 'red',  role: 'operative' },
    { id: 'p-sara',   name: 'Sara',   hue: 218, team: 'blue', role: 'spymaster' },
    { id: 'p-giulia', name: 'Giulia', hue: 226, team: 'blue', role: 'operative' },
    { id: 'p-dario',  name: 'Dario',  hue: 206, team: 'blue', role: 'operative' },
  ];
  const byId = (id) => ROSTER.find((p) => p.id === id);

  // ─── fixed key card · 25 words in grid order ──────────────────────────────
  const KEYS = [
    ['APPLE', 'neutral'], ['OCEAN', 'red'],   ['KNIGHT', 'blue'],   ['ROBOT', 'red'],   ['MAPLE', 'neutral'],
    ['SHADOW', 'assassin'], ['COMET', 'red'],  ['ANCHOR', 'blue'],  ['TIGER', 'red'],   ['PRISM', 'neutral'],
    ['CASTLE', 'blue'],   ['VOLCANO', 'red'],  ['FEATHER', 'neutral'], ['ENGINE', 'blue'], ['NOMAD', 'red'],
    ['GLACIER', 'blue'],  ['ORCHID', 'neutral'], ['BISHOP', 'blue'], ['ROCKET', 'red'],  ['CACTUS', 'neutral'],
    ['LANTERN', 'red'],   ['FALCON', 'blue'],  ['MARBLE', 'neutral'], ['THUNDER', 'red'], ['COMPASS', 'blue'],
  ];
  const makeBoard = (rev) => KEYS.map(([word, key]) => ({ word, key, revealed: !!rev[word], revealedBy: rev[word] || null }));
  const countFound = (board, team) => board.filter((c) => c.key === team && c.revealed).length;
  const buildTeams = (board, currentTeam) => {
    const mk = (id) => {
      const found = countFound(board, id);
      return { ...TEAM[id], found, remaining: TEAM[id].total - found, current: currentTeam === id };
    };
    return { red: mk('red'), blue: mk('blue') };
  };
  const buildRanking = (teams) => {
    const arr = [
      { id: 'red',  name: 'Rossi', hue: 5,   t: teams.red },
      { id: 'blue', name: 'Blu',   hue: 218, t: teams.blue },
    ];
    arr.sort((a, b) => (a.t.remaining - b.t.remaining) || (b.t.found - a.t.found));
    return arr.map((x, i) => ({ id: x.id, name: x.name, hue: x.hue, rank: i + 1,
      sub: `${x.t.found} / ${x.t.total} agenti · ${x.t.remaining} rimasti` }));
  };

  const cR = 'hsl(var(--c-event))', cB = 'hsl(var(--c-chat))';

  // ─── chat (shared) ────────────────────────────────────────────────────────
  const CHAT = [
    { id: 'q1', from: 'user',  t: '07:55', text: 'Se tocchiamo l\u2019assassino perdiamo subito?' },
    { id: 'q2', from: 'agent', t: '07:55', text: 'Sì: rivelare la tessera assassino fa perdere immediatamente la squadra che la tocca, a prescindere dagli agenti già trovati.', citations: ['Codenames §Fine partita'] },
    { id: 'q3', from: 'user',  t: '08:10', text: 'Quante tessere possiamo provare con un indizio da 3?' },
    { id: 'q4', from: 'agent', t: '08:10', text: 'Fino al numero dell\u2019indizio più uno: con «3» potete tentare fino a 4 tessere, finché indovinate agenti della vostra squadra.', citations: ['Codenames §Indizi', 'FAQ #4'] },
  ];
  const SUGGESTIONS = ['Indizio non valido?', 'Possiamo passare?', 'Regola assassino'];

  // optional timers (codenames.json · community variant) — guess timer active
  const timers = {
    clue:  { id: 'clue',  label: 'Indizio',   duration: 120, warn: 30, enabled: true, active: false, remaining: 120 },
    guess: { id: 'guess', label: 'Tentativo', duration: 60,  warn: 15, enabled: true, active: true,  remaining: 41 },
  };

  // ══════════════════════════════════════════════════════════════════════════
  // SCENARIOS
  // ══════════════════════════════════════════════════════════════════════════
  const REV_MID = { OCEAN: 'red', ROBOT: 'red', COMET: 'red', TIGER: 'red', NOMAD: 'red', THUNDER: 'red', CASTLE: 'blue', ENGINE: 'blue', BISHOP: 'blue', MAPLE: 'red' };
  const REV_MATCH = { ...REV_MID, KNIGHT: 'blue', ANCHOR: 'blue', GLACIER: 'blue', FALCON: 'blue', ORCHID: 'blue' };
  const REV_ASSN = { ...REV_MID, SHADOW: 'red' };

  // — mid clues —
  const CLUES_MID = [
    { id: 'cl1', team: 'red',  word: 'METAL',  number: 3, correct: 2, attempts: 3, outcome: 'partial', spymaster: 'p-marco', note: 'finito su civile' },
    { id: 'cl2', team: 'blue', word: 'ROYAL',  number: 1, correct: 1, attempts: 1, outcome: 'clean',   spymaster: 'p-sara' },
    { id: 'cl3', team: 'red',  word: 'ANIMAL', number: 3, correct: 3, attempts: 3, outcome: 'clean',   spymaster: 'p-marco', best: true },
    { id: 'cl4', team: 'blue', word: 'SPACE',  number: 2, correct: 2, attempts: 2, outcome: 'clean',   spymaster: 'p-sara' },
    { id: 'cl5', team: 'red',  word: 'OCEAN',  number: 3, correct: 1, attempts: 1, outcome: 'active',  spymaster: 'p-marco' },
  ];
  const CLUES_MATCH = [
    { id: 'm1', team: 'red',  word: 'METAL',   number: 3, correct: 2, attempts: 3, outcome: 'partial', spymaster: 'p-marco' },
    { id: 'm2', team: 'blue', word: 'ROYAL',   number: 1, correct: 1, attempts: 1, outcome: 'clean',   spymaster: 'p-sara' },
    { id: 'm3', team: 'red',  word: 'ANIMAL',  number: 3, correct: 3, attempts: 3, outcome: 'clean',   spymaster: 'p-marco', best: true },
    { id: 'm4', team: 'blue', word: 'HARBOR',  number: 3, correct: 2, attempts: 3, outcome: 'partial', spymaster: 'p-sara', note: 'finito su civile' },
    { id: 'm5', team: 'red',  word: 'OCEAN',   number: 3, correct: 3, attempts: 3, outcome: 'clean',   spymaster: 'p-marco' },
    { id: 'm6', team: 'blue', word: 'NORDIC',  number: 3, correct: 3, attempts: 3, outcome: 'clean',   spymaster: 'p-sara' },
    { id: 'm7', team: 'blue', word: 'SAILOR',  number: 2, correct: 1, attempts: 1, outcome: 'active',  spymaster: 'p-sara' },
  ];
  const CLUES_ASSN = [
    { id: 'a1', team: 'red',  word: 'METAL',  number: 3, correct: 2, attempts: 3, outcome: 'partial', spymaster: 'p-marco' },
    { id: 'a2', team: 'blue', word: 'ROYAL',  number: 1, correct: 1, attempts: 1, outcome: 'clean',   spymaster: 'p-sara' },
    { id: 'a3', team: 'red',  word: 'ANIMAL', number: 3, correct: 3, attempts: 3, outcome: 'clean',   spymaster: 'p-marco', best: true },
    { id: 'a4', team: 'blue', word: 'SPACE',  number: 2, correct: 2, attempts: 2, outcome: 'clean',   spymaster: 'p-sara' },
    { id: 'a5', team: 'red',  word: 'OCEAN',  number: 3, correct: 1, attempts: 2, outcome: 'miss',    spymaster: 'p-marco', note: 'rivelato l\u2019assassino' },
  ];

  const EVENTS_MID = [
    { kind: 'turn-change',  who: 'p-marco',  t: 'ora',   text: 'Indizio dato — <strong>OCEAN : 3</strong>' },
    { kind: 'score-update', who: 'p-anna',   t: '08:01', text: `Rivela <strong>OCEAN</strong> · agente <strong style="color:${cR}">Rosso</strong> ✓` },
    { kind: 'turn-change',  who: 'p-sara',   t: '07:30', text: 'Indizio dato — <strong>SPACE : 2</strong>' },
    { kind: 'score-update', who: 'p-giulia', t: '07:12', text: `Rivela <strong>ENGINE</strong> · agente <strong style="color:${cB}">Blu</strong> ✓` },
    { kind: 'custom',       who: 'p-anna',   t: '05:42', text: 'Rivela <strong>MAPLE</strong> · civile — il turno passa al Blu' },
    { kind: 'game-start',   who: null,       t: '00:00', text: `Partita avviata · il <strong style="color:${cR}">Rosso</strong> muove per primo (9 agenti)` },
  ];
  const EVENTS_MATCH = [
    { kind: 'turn-change',  who: 'p-sara',   t: 'ora',   text: 'Indizio dato — <strong>SAILOR : 2</strong>' },
    { kind: 'score-update', who: 'p-giulia', t: '11:48', text: `Rivela <strong>FALCON</strong> · agente <strong style="color:${cB}">Blu</strong> ✓ — manca 1 agente!` },
    { kind: 'score-update', who: 'p-dario',  t: '11:20', text: `Rivela <strong>GLACIER</strong> · agente <strong style="color:${cB}">Blu</strong> ✓` },
    { kind: 'turn-change',  who: 'p-marco',  t: '10:05', text: 'Indizio dato — <strong>OCEAN : 3</strong> · 3/3 pieno' },
    { kind: 'custom',       who: 'p-giulia', t: '09:10', text: 'Rivela <strong>ORCHID</strong> · civile — il turno passa al Rosso' },
    { kind: 'game-start',   who: null,       t: '00:00', text: `Partita avviata · il <strong style="color:${cR}">Rosso</strong> muove per primo` },
  ];
  const EVENTS_ASSN = [
    { kind: 'custom',       who: 'p-luca',   t: 'ora',   text: `💀 Rivela <strong>SHADOW</strong> · <strong style="color:${cR}">ASSASSINO</strong> — sconfitta immediata del Rosso` },
    { kind: 'score-update', who: 'p-anna',   t: '08:01', text: `Rivela <strong>OCEAN</strong> · agente <strong style="color:${cR}">Rosso</strong> ✓` },
    { kind: 'turn-change',  who: 'p-marco',  t: '07:50', text: 'Indizio dato — <strong>OCEAN : 3</strong>' },
    { kind: 'score-update', who: 'p-giulia', t: '07:12', text: `Rivela <strong>ENGINE</strong> · agente <strong style="color:${cB}">Blu</strong> ✓` },
    { kind: 'game-start',   who: null,       t: '00:00', text: `Partita avviata · il <strong style="color:${cR}">Rosso</strong> muove per primo` },
  ];

  const baseGame = { id: 'codenames', title: 'Codenames', emoji: '🕵️',
    cover: 'linear-gradient(135deg, hsl(350,72%,48%), hsl(220,72%,46%))',
    meta: '2 squadre · 4–8+ giocatori · ~15 min · deduzione' };
  const scoringTemplate = {
    scoreType: 'Ranking',
    categories: [
      { id: 'agents-found',     label: 'Agenti rivelati dalla squadra', computation: 'Count',  weight: 1, description: 'Prima squadra a rivelare tutti i propri agenti vince.' },
      { id: 'assassin-touched', label: 'Assassino rivelato',            computation: 'Custom', weight: 0, description: 'Sconfitta immediata per chi rivela l\u2019assassino (1 di 25).' },
    ],
  };
  const turnTemplate = {
    turnOrderType: 'Sequential',
    phases: ['Spymaster Rosso', 'Operativi Rossi', 'Spymaster Blu', 'Operativi Blu'],
    rounds: null, turnsPerRound: null, turnActions: ['give-clue', 'guess-tile', 'end-turn'], direction: 'none',
  };

  // 4-phase team-alternating descriptors (drives PhaseBanner)
  const PHASES = [
    { id: 'r-clue',  team: 'red',  short: 'Spymaster Rosso', desc: 'Lo Spymaster rosso dà un indizio', kind: 'clue' },
    { id: 'r-guess', team: 'red',  short: 'Operativi Rossi', desc: 'Gli operativi rossi indovinano',   kind: 'guess' },
    { id: 'b-clue',  team: 'blue', short: 'Spymaster Blu',   desc: 'Lo Spymaster blu dà un indizio',    kind: 'clue' },
    { id: 'b-guess', team: 'blue', short: 'Operativi Blu',   desc: 'Gli operativi blu indovinano',      kind: 'guess' },
  ];

  function buildScenario(cfg) {
    const board = makeBoard(cfg.rev);
    const teams = buildTeams(board, cfg.currentTeam);
    const ranking = buildRanking(teams);
    const ds = {
      game: baseGame, session: { elapsed: cfg.elapsed }, players: ROSTER,
      agent: { emoji: '🕵️', name: 'Arbitro Codenames', latency: '~0.5s' },
      chat: CHAT, suggestions: SUGGESTIONS, events: cfg.events,
      meta: 'Classifica squadre · race condition', scoring: scoringTemplate, ranking,
      turn: turnTemplate, turnState: { phaseIndex: cfg.phaseState.phaseIndex },
    };
    return { id: cfg.id, label: cfg.label, sub: cfg.sub, tone: cfg.tone,
      board, teams, ranking, clue: cfg.clue, phaseState: cfg.phaseState,
      clues: cfg.clues, events: cfg.events, gameOver: cfg.gameOver || null, ds };
  }

  const SCENARIOS = {
    mid: buildScenario({
      id: 'mid', label: 'Mid-game · Rosso avanti', sub: 'Rosso 6/9 · Blu 3/8 · indizio attivo', tone: 'event',
      rev: REV_MID, currentTeam: 'red', elapsed: '08:24', phaseState: { round: 3, phaseIndex: 1 },
      clue: { team: 'red', word: 'OCEAN', number: 3, spymaster: 'p-marco', guessesMade: 1, allowed: 4, remaining: 3 },
      clues: CLUES_MID, events: EVENTS_MID,
    }),
    match: buildScenario({
      id: 'match', label: 'Match point · Blu', sub: 'Blu 7/8 · manca 1 agente!', tone: 'chat',
      rev: REV_MATCH, currentTeam: 'blue', elapsed: '12:03', phaseState: { round: 6, phaseIndex: 3 },
      clue: { team: 'blue', word: 'SAILOR', number: 2, spymaster: 'p-sara', guessesMade: 1, allowed: 3, remaining: 2 },
      clues: CLUES_MATCH, events: EVENTS_MATCH,
    }),
    assassin: buildScenario({
      id: 'assassin', label: 'Sconfitta · Assassino', sub: 'Il Rosso ha toccato l\u2019assassino', tone: 'event',
      rev: REV_ASSN, currentTeam: 'red', elapsed: '08:36', phaseState: { round: 4, phaseIndex: 1 },
      clue: null, clues: CLUES_ASSN, events: EVENTS_ASSN,
      gameOver: { result: 'assassin', loser: 'red', winner: 'blue', icon: '💀',
        title: 'Sconfitta · Assassino', cause: 'Il Rosso ha rivelato l\u2019assassino (SHADOW)',
        detail: 'Sconfitta immediata. Vince la Squadra Blu.' },
    }),
  };
  const SCN_ORDER = ['mid', 'match', 'assassin'];

  // ─── defaults (mid) exposed top-level for galleries & component defaults ──
  const MID = SCENARIOS.mid;

  // ─── SUMMARY (post-game) ──────────────────────────────────────────────────
  const finalBoard = MID.board.map((c) => ({ ...c, revealed: true, revealedBy: c.revealedBy || c.key }));
  const summary = {
    winner: 'red', cause: 'Ha rivelato tutti i 9 agenti', causeKind: 'agents',
    duration: '14:38', rounds: 7, assassinAvoided: true,
    teams: { red: { ...TEAM.red, found: 9, remaining: 0 }, blue: { ...TEAM.blue, found: 6, remaining: 2 } },
    clues: [
      { id: 's1', team: 'red',  word: 'METAL',  number: 3, correct: 2, attempts: 3, outcome: 'partial', spymaster: 'p-marco' },
      { id: 's2', team: 'blue', word: 'ROYAL',  number: 1, correct: 1, attempts: 1, outcome: 'clean',   spymaster: 'p-sara' },
      { id: 's3', team: 'red',  word: 'ANIMAL', number: 3, correct: 3, attempts: 3, outcome: 'clean',   spymaster: 'p-marco', best: true },
      { id: 's4', team: 'blue', word: 'SPACE',  number: 2, correct: 2, attempts: 2, outcome: 'clean',   spymaster: 'p-sara' },
      { id: 's5', team: 'red',  word: 'OCEAN',  number: 3, correct: 3, attempts: 3, outcome: 'clean',   spymaster: 'p-marco' },
      { id: 's6', team: 'blue', word: 'METAL',  number: 2, correct: 1, attempts: 2, outcome: 'partial', spymaster: 'p-sara', worst: true },
      { id: 's7', team: 'red',  word: 'STONE',  number: 1, correct: 1, attempts: 1, outcome: 'clean',   spymaster: 'p-marco' },
    ],
    stats: { avgTurn: '1:13', missRate: 0.18, assassinTouched: 0, clueDensity: 2.1, tilesRevealed: 18, neutralsHit: 3 },
    finalBoard,
  };

  window.CN = {
    TEAM, ROSTER, byId, KEYS, makeBoard, buildTeams, PHASES,
    SCENARIOS, SCN_ORDER,
    // defaults (mid scenario) — used by galleries & component prop fallbacks
    BOARD: MID.board, teams: MID.teams, currentClue: MID.clue, current: CLUES_MID[CLUES_MID.length - 1],
    phaseState: MID.phaseState, CLUES: MID.clues, EVENTS: MID.events, ds: MID.ds,
    timers,
    summary, COLS: 5, ROWS: 5,
  };
})();

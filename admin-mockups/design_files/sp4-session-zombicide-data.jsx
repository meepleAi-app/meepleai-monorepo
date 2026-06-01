/* MeepleAI SP4 · Zombicide: Green Horde — DATA  (window.ZOM  +  window.SkeletonData shim)
   /sessions/[id]/live — dataset game-specific letto da zombicide-green-horde.json.

   Espone:
     window.SkeletonData = { STATES }   (shim: skeleton-renderers + skeleton-parts
                                          leggono STATES qui)
     window.ZOM = { game, agent, levelOf, LEVELS, DANGER, ZTYPES, ZORDER, EQUIP,
                    survivors, byId, board, objectives, phases, phaseState,
                    combat, weaponsRef, spawnDeck, mapTiles, ds (shape per i
                    renderer polimorfici), counterWidgets, chat, suggestions,
                    events, summary }

   MECCANICA CHIAVE (da zombicide-green-horde.json): co-op a missione (BinaryWin —
   nessun punteggio, vittoria/sconfitta condivisa). Per-survivor: ferite (0-2, 2 =
   morto, carta flippata), XP che pilota i livelli skill (Blu 0-6 → Giallo 7-18 →
   Arancione 19-42 → Rosso 43+), punti azione (3 base + bonus skill), equipaggiamento
   (2 mani + corpo + zaino). Plancia: conteggi zombie per tipo. Round in 3 fasi:
   Giocatori (orario) → Zombie attivano → Finale (spawn per Danger Level).
   Combat: D6, colpo 4+ (modificato dall'arma). Danger Level = survivor con XP più alto.

   Colori zombie/skill: verbatim da zombicide-green-horde.json / mappati su token.
   Tutto il resto = var di tokens.css. */

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
    id: 'zombicide-gh', title: 'Zombicide: Green Horde', emoji: '🧟',
    cover: 'linear-gradient(135deg, hsl(140,42%,24%), hsl(96,38%,16%))',
    meta: '1-6 giocatori · ~60-90 min · co-op dungeon-crawler',
  };
  const agent = { id: 'zom-agent', name: 'Zombicide GH Expert', emoji: '🧟', latency: '0.6s' };

  // ─── livelli skill (soglie XP da JSON) · color-coded per livello ──────────
  // Blu → --c-info · Giallo → --c-warning · Arancione → --c-game · Rosso → --c-danger
  const LEVELS = [
    { id: 'blue',   e: 'info',    lb: 'Blu',       min: 0,  max: 6,  range: '0-6' },
    { id: 'yellow', e: 'warning', lb: 'Giallo',    min: 7,  max: 18, range: '7-18' },
    { id: 'orange', e: 'game',    lb: 'Arancione', min: 19, max: 42, range: '19-42' },
    { id: 'red',    e: 'danger',  lb: 'Rosso',     min: 43, max: 99, range: '43+' },
  ];
  const levelOf = (xp) => xp >= 43 ? 'red' : xp >= 19 ? 'orange' : xp >= 7 ? 'yellow' : 'blue';
  const levelMeta = (id) => LEVELS.find(l => l.id === id) || LEVELS[0];
  const levelIndex = (id) => LEVELS.findIndex(l => l.id === id);

  // ─── Danger Level (aggressività spawn · determinato dal survivor con XP top) ─
  const DANGER = [
    { id: 'blue',   e: 'info',    lb: 'Blu',       spawn: 'Spawn base · pochi zombie' },
    { id: 'yellow', e: 'warning', lb: 'Giallo',    spawn: 'Spawn rinforzati' },
    { id: 'orange', e: 'game',    lb: 'Arancione', spawn: 'Orde · più Fatty/Runner' },
    { id: 'red',    e: 'danger',  lb: 'Rosso',     spawn: 'Orde massime · Abominio possibile' },
  ];

  // ─── tipi zombie (colori verbatim da zombicide-green-horde.json) ──────────
  const ZTYPES = {
    walker:      { id: 'walker',      lb: 'Walker',      glyph: 'W', color: '#27ae60', actions: 1, dmg: 1, note: 'Mossa base · 1 attività' },
    orc:         { id: 'orc',         lb: 'Walker Orc',  glyph: 'O', color: '#1e8a4e', actions: 1, dmg: 1, note: 'Più resistente del Walker' },
    runner:      { id: 'runner',      lb: 'Runner',      glyph: 'R', color: '#16a085', actions: 2, dmg: 1, note: '2 attività · velocissimo' },
    fatty:       { id: 'fatty',       lb: 'Fatty',       glyph: 'F', color: '#15795a', actions: 1, dmg: 1, note: 'Serve danno 2 · genera Walker' },
    abomination: { id: 'abomination', lb: 'Abomination', glyph: 'A', color: '#7d3c98', actions: 1, dmg: 1, note: 'Solo danno 3 lo uccide' },
    necromancer: { id: 'necromancer', lb: 'Necromancer', glyph: 'N', color: '#5b2c6f', actions: 1, dmg: 0, note: 'Fugge e apre nuovi spawn' },
  };
  const ZORDER = ['walker', 'orc', 'runner', 'fatty', 'abomination', 'necromancer'];

  // ─── catalogo equipaggiamento ─────────────────────────────────────────────
  // type: melee · ranged · armor · item. range in tessere. dice/dmg per attacco.
  const EQUIP = {
    katana:       { id: 'katana',       name: 'Katana',           type: 'melee',  glyph: '🗡', range: '0',   dice: 2, dmg: 1, accent: 'danger' },
    fireaxe:      { id: 'fireaxe',      name: 'Ascia da fuoco',   type: 'melee',  glyph: '🪓', range: '0',   dice: 2, dmg: 1, accent: 'danger' },
    sword:        { id: 'sword',        name: 'Spada',            type: 'melee',  glyph: '⚔', range: '0',   dice: 1, dmg: 1, accent: 'danger' },
    pistol:       { id: 'pistol',       name: 'Pistola',          type: 'ranged', glyph: '•', range: '0-1', dice: 1, dmg: 1, accent: 'session' },
    sawedoff:     { id: 'sawedoff',     name: 'Canne mozze',      type: 'ranged', glyph: '═', range: '0-1', dice: 1, dmg: 2, accent: 'session', area: true },
    crossbow:     { id: 'crossbow',     name: 'Balestra',         type: 'ranged', glyph: '↟', range: '0-3', dice: 1, dmg: 2, accent: 'session' },
    handcrossbow: { id: 'handcrossbow', name: 'Balestra a mano',  type: 'ranged', glyph: '↑', range: '0-1', dice: 2, dmg: 1, accent: 'session' },
    longbow:      { id: 'longbow',      name: 'Arco lungo',       type: 'ranged', glyph: ')', range: '0-3', dice: 1, dmg: 1, accent: 'session', reload: true },
    riot:         { id: 'riot',         name: 'Antisommossa',     type: 'armor',  glyph: '🛡', save: '4+',  accent: 'kb' },
    plate:        { id: 'plate',        name: 'Armatura piastre', type: 'armor',  glyph: '🛡', save: '3+',  accent: 'kb' },
    molotov:      { id: 'molotov',      name: 'Molotov',          type: 'item',   glyph: '🔥', range: '0-1', dmg: 'fuoco', accent: 'event' },
    torch:        { id: 'torch',        name: 'Torcia',           type: 'item',   glyph: '🔦', accent: 'agent' },
    water:        { id: 'water',        name: 'Acqua',            type: 'item',   glyph: '💧', accent: 'tool' },
  };
  const eq = (id) => EQUIP[id] || { id, name: id, type: 'item', glyph: '?', accent: 'tool' };
  const EQUIP_SLOTS = [
    { id: 'rightHand', lb: 'Mano dx' },
    { id: 'leftHand',  lb: 'Mano sx' },
    { id: 'body',      lb: 'Corpo' },
    { id: 'backpack',  lb: 'Zaino' },
  ];

  // ─── skill tree helper: ogni survivor ha 4 livelli (Blu/Giallo/Aran/Rosso) ─
  // status derivato dall'XP: 'done' (livello raggiunto, chosen highlighted),
  // 'available' (livello appena raggiunto, da scegliere), 'locked' (futuro, greyed).
  const treeStatus = (survivorLevel, rowLevel) => {
    const a = levelIndex(survivorLevel), b = levelIndex(rowLevel);
    return b < a ? 'done' : b === a ? 'available' : 'locked';
  };

  // ─── survivors (4 · co-op) — XP pilota livello e Danger Level ──────────────
  // tree[].chosen = skill ottenuto · options = alternative a quel livello.
  const survivors = [
    {
      id: 's-bruno', name: 'Bruno', player: 'Marco', hue: 8, klass: 'Berserker',
      xp: 47, wounds: 1, apBase: 3, apBonus: 0, zone: 'Cattedrale · tile 03b',
      equip: { rightHand: 'katana', leftHand: 'pistol', body: 'riot', backpack: 'molotov' },
      kills: { walker: 14, orc: 5, runner: 3, fatty: 2, abomination: 1, necromancer: 0 },
      tree: [
        { level: 'blue',   chosen: '+1 Azione Mischia', options: ['+1 Azione Mischia'] },
        { level: 'yellow', chosen: 'Carica',            options: ['Carica', '+1 dado Mischia', 'Robusto'] },
        { level: 'orange', chosen: '+1 dado: Mischia',  options: ['+1 dado: Mischia', 'Spaccaossa', 'Berserk'] },
        { level: 'red',    chosen: 'Furia',             options: ['Furia', 'Esecuzione'] },
      ],
    },
    {
      id: 's-elena', name: 'Elena', player: 'Anna', hue: 282, klass: 'Heroine',
      xp: 31, wounds: 0, apBase: 3, apBonus: 1, zone: 'Sagrato · tile 01a',
      equip: { rightHand: 'crossbow', leftHand: 'handcrossbow', body: 'plate', backpack: 'water' },
      kills: { walker: 11, orc: 2, runner: 4, fatty: 1, abomination: 0, necromancer: 2 },
      tree: [
        { level: 'blue',   chosen: '+1 Azione',          options: ['+1 Azione'] },
        { level: 'yellow', chosen: 'Mira',               options: ['Mira', '+1 dado: Distanza', 'Scattante'] },
        { level: 'orange', chosen: '+1 Azione Movimento', options: ['+1 Azione Movimento', 'Cecchino', 'Ambidestra'] },
        { level: 'red',    chosen: null,                 options: ['Leader nato', 'Tiratore scelto'] },
      ],
    },
    {
      id: 's-tomas', name: 'Tomas', player: 'Luca', hue: 150, klass: 'Tracker',
      xp: 14, wounds: 0, apBase: 3, apBonus: 0, zone: 'Vicolo · tile 02c',
      equip: { rightHand: 'longbow', leftHand: null, body: null, backpack: 'torch' },
      kills: { walker: 6, orc: 1, runner: 2, fatty: 0, abomination: 0, necromancer: 1 },
      tree: [
        { level: 'blue',   chosen: '+1 Free Move',  options: ['+1 Free Move'] },
        { level: 'yellow', chosen: null,            options: ['Furtivo', '+1 dado: Distanza', 'Fortunato'] },
        { level: 'orange', chosen: null,            options: ['Cecchino', 'Ranger', 'Lince'] },
        { level: 'red',    chosen: null,            options: ['Tiratore scelto', 'Leader nato'] },
      ],
    },
    {
      id: 's-greta', name: 'Greta', player: 'Sara', hue: 40, klass: 'Hunter',
      xp: 5, wounds: 1, apBase: 3, apBonus: 0, zone: 'Cripta · tile 04b',
      equip: { rightHand: 'sawedoff', leftHand: null, body: 'riot', backpack: 'molotov' },
      kills: { walker: 4, orc: 0, runner: 1, fatty: 1, abomination: 0, necromancer: 0 },
      tree: [
        { level: 'blue',   chosen: '+1 dado: Distanza', options: ['+1 dado: Distanza'] },
        { level: 'yellow', chosen: null,                options: ['Robusto', 'Carica', 'Slegato'] },
        { level: 'orange', chosen: null,                options: ['Spaccaossa', 'Devastante', 'Berserk'] },
        { level: 'red',    chosen: null,                options: ['Furia', 'Esecuzione'] },
      ],
    },
  ];
  survivors.forEach(s => {
    s.level = levelOf(s.xp);
    s.ap = s.apBase + s.apBonus;
    s.dead = s.wounds >= 2;
    s.killTotal = Object.values(s.kills).reduce((a, b) => a + b, 0);
    s.tree.forEach(row => { row.status = treeStatus(s.level, row.level); });
  });
  const byId = (id) => survivors.find(s => s.id === id);
  const topSurvivor = [...survivors].sort((a, b) => b.xp - a.xp)[0];
  const dangerLevel = topSurvivor.level; // Danger Level = livello del survivor con XP top

  // ─── stato plancia (conteggi zombie per tipo) ─────────────────────────────
  const board = {
    walker: 14, orc: 6, runner: 4, fatty: 3, abomination: 1, necromancer: 2,
  };
  const zombieTotal = ZORDER.reduce((s, t) => s + (board[t] || 0), 0);

  // ─── obiettivi scenario (checklist) ───────────────────────────────────────
  const objectives = [
    { id: 'o1', label: 'Salva i 3 civili nascosti', done: false, progress: '2/3' },
    { id: 'o2', label: 'Uccidi tutti i Necromancer', done: false, progress: '0/2' },
    { id: 'o3', label: 'Apri il portone della cripta', done: true },
    { id: 'o4', label: 'Raggiungi il punto di estrazione', done: false },
  ];

  // ─── round in 3 fasi ──────────────────────────────────────────────────────
  const phases = [
    { id: 'players', n: 1, lb: 'Fase Giocatori', short: 'Giocatori', desc: 'Ogni survivor spende i punti azione, in senso orario', icon: '🧍' },
    { id: 'zombies', n: 2, lb: 'Fase Zombie',    short: 'Zombie',    desc: 'Gli zombie esistenti si attivano per tipo', icon: '🧟' },
    { id: 'end',     n: 3, lb: 'Fase Finale',    short: 'Finale',    desc: 'Spawn per Danger Level · pesca carte spawn', icon: '⚙' },
  ];
  const phaseState = { round: 6, phaseIndex: 0, activePhaseId: 'players', dir: 'clockwise' };

  // ─── combat dice (D6 · colpo 4+ · modificato dall'arma) ───────────────────
  const combat = {
    diceType: 'D6', dice: 6, threshold: 4,
    weaponId: 'crossbow',
    lastRoll: [5, 2, 6, 4, 1, 3], // hit dove ≥ threshold
  };
  const weaponsRef = [
    { id: 'pistol',   lb: 'Pistola',     range: '0-1', dice: 1, hit: '4+', note: '' },
    { id: 'sawedoff', lb: 'Canne mozze', range: '0-1', dice: 1, hit: '4+', note: 'area' },
    { id: 'crossbow', lb: 'Balestra',    range: '0-3', dice: 1, hit: '4+', note: 'danno 2' },
    { id: 'longbow',  lb: 'Arco lungo',  range: '0-3', dice: 1, hit: '4+', note: 'ricarica' },
    { id: 'katana',   lb: 'Katana',      range: '0',   dice: 2, hit: '4+', note: 'mischia' },
  ];

  // ─── spawn deck (carte rimaste · pescate in Fase Finale per Danger Level) ──
  const spawnDeck = {
    remaining: 18, total: 40, drawnThisRound: 0,
    byDanger: [
      { level: 'blue',   draws: 1 },
      { level: 'yellow', draws: 1 },
      { level: 'orange', draws: 2 },
      { level: 'red',    draws: 2 },
    ],
    note: 'In Danger Rosso ogni spawn point pesca 2 carte · Abominio possibile.',
  };

  // ─── map tiles (snapshot schematico · placeholder, non geografia reale) ────
  const mapTiles = {
    scenario: 'Scenario · "Caccia al Necromante" (GH base)',
    cols: 3, rows: 2,
    cells: [
      { id: '01a', name: 'Sagrato',   survivors: ['s-elena'], z: { walker: 2 },            objective: false, spawn: false },
      { id: '02c', name: 'Vicolo',    survivors: ['s-tomas'], z: { runner: 2, walker: 1 }, objective: false, spawn: true },
      { id: '03b', name: 'Cattedrale',survivors: ['s-bruno'], z: { walker: 5, fatty: 1 },  objective: true,  spawn: false },
      { id: '04b', name: 'Cripta',    survivors: ['s-greta'], z: { walker: 3, abomination: 1 }, objective: true, spawn: true },
      { id: '05a', name: 'Cortile',   survivors: [],          z: { orc: 4, necromancer: 1 }, objective: false, spawn: true },
      { id: '06d', name: 'Estrazione',survivors: [],          z: { necromancer: 1 },        objective: true,  spawn: false, exit: true },
    ],
  };

  // ══════════════════════════════════════════════════════════════════════════
  // ds = oggetto consumato dai RENDERER POLIMORFICI dello skeleton
  // ══════════════════════════════════════════════════════════════════════════
  const playersShape = survivors.map(s => ({ id: s.id, name: s.name, hue: s.hue, score: s.xp, turnDelta: 0 }));

  // SCORING — branch BinaryWin (co-op · nessun punteggio · esito condiviso)
  const scoring = {
    scoreType: 'BinaryWin',
    collective: {
      goalLabel: 'Obiettivi missione', goalValue: 1, goalMax: 4, goalHint: 'completati',
      failLabel: 'Survivor a terra', failValue: 1, failMax: 4, failHint: 'tutti morti = sconfitta',
    },
    categories: [
      { id: 'mission-success', label: 'Obiettivi missione completati', computation: 'Custom', weight: 1,  description: 'Specifici dello scenario: salva civili, uccidi i Necromancer, raggiungi l\u2019estrazione.' },
      { id: 'survivors-alive', label: 'Survivor vivi a fine partita',   computation: 'Count',  weight: 1,  description: 'Alcuni scenari richiedono tutti i survivor vivi; altri tollerano perdite.' },
      { id: 'total-xp',        label: 'XP totali guadagnati (info)',     computation: 'Sum',    weight: 0,  description: 'XP cumulati: pilotano la progressione skill e il Danger Level, non il punteggio.' },
    ],
  };

  // TURN — branch Sequential (le 3 fasi del round)
  const turnShape = {
    turnOrderType: 'Sequential',
    phases: phases.map(p => `${p.n}. ${p.lb}`),
    direction: 'clockwise',
    turnActions: ['move', 'attack-melee', 'attack-ranged', 'open-door', 'search', 'trade', 'objective-action', 'use-skill'],
  };

  const ds = {
    game, agent,
    session: { elapsed: '47 min' },
    players: playersShape,
    scoring,
    meta: 'Fasi del round · co-op a missione',
    turn: turnShape,
    turnState: { phaseIndex: phaseState.phaseIndex, round: phaseState.round },
    chat: [
      { id: 'c1', from: 'user',  t: '15:31', text: 'Bruno è adiacente a un Abominio: la mia Katana lo uccide?' },
      { id: 'c2', from: 'agent', t: '15:31', text: 'No. L\u2019Abominio si elimina solo con danno 3. La Katana fa danno 1: serve un\u2019arma da danno 3 (es. Molotov, lanciafiamme) o effetti che cumulano danno.', citations: ['Zombicide GH §Zombi', 'FAQ #4'] },
      { id: 'c3', from: 'user',  t: '15:38', text: 'A che distanza spara la balestra di Elena?' },
      { id: 'c4', from: 'agent', t: '15:38', text: 'La Balestra ha gittata 0-3 tessere e fa danno 2 per colpo. A distanza 0 puoi colpire la tua stessa zona solo se non ci sono survivor (rischio di amici a terra).', citations: ['Zombicide GH §Armi'] },
    ],
    suggestions: ['Come si attiva un Runner?', 'Soglia colpo a distanza', 'Spawn con Danger Rosso'],
    events: [
      { kind: 'turn-change', who: 's-bruno', t: '15:40', text: '<strong>Bruno</strong> entra in <strong>Cattedrale</strong> e attacca in mischia: <strong>3 colpi</strong> → 3 Walker eliminati' },
      { kind: 'custom',      who: 's-elena', t: '15:39', text: '<strong>Elena</strong> abbatte un <strong>Necromancer</strong> con la balestra (danno 2)' },
      { kind: 'score-update',who: 's-bruno', t: '15:39', text: '<strong>Bruno</strong> sale a <strong>47 XP</strong> → livello <strong>Rosso</strong> · Danger Level → Rosso' },
      { kind: 'custom',      who: 's-greta', t: '15:36', text: '<strong>Greta</strong> subisce 1 ferita da un Fatty nella Cripta (1/2)' },
      { kind: 'game-start',  who: null,      t: '14:53', text: 'Scenario avviato · "Caccia al Necromante" · 4 survivor · Danger Blu' },
    ],
  };

  // WIDGET — ToolkitRenderer: i CounterTools / DiceTools del JSON
  const counterWidgets = [
    { id: 'w-zombies', type: 'ResourceManager', isEnabled: true, displayOrder: 1,
      config: { shared: true, resources: [
        { label: 'Walker', value: board.walker, max: 99 },
        { label: 'Runner', value: board.runner, max: 30 },
        { label: 'Fatty', value: board.fatty, max: 20 },
        { label: 'Abomination', value: board.abomination, max: 5, danger: true },
        { label: 'Necromancer', value: board.necromancer, max: 5, danger: true },
      ] } },
    { id: 'w-survivor', type: 'ResourceManager', isEnabled: true, displayOrder: 2,
      config: { shared: false, resources: [
        { label: 'Ferite · Bruno', value: 1, max: 2, danger: true },
        { label: 'XP · Bruno', value: 47, max: 99 },
        { label: 'Punti azione', value: 3, max: 6 },
      ] } },
    { id: 'w-xp',   type: 'ScoreTracker',    isEnabled: true,  displayOrder: 3, config: {} },
    { id: 'w-turn', type: 'TurnManager',     isEnabled: true,  displayOrder: 4, config: { phaseBased: true } },
    { id: 'w-dice', type: 'RandomGenerator', isEnabled: true,  displayOrder: 5, config: { name: 'Dadi combattimento', quantity: 6, faces: ['1', '2', '3', '4', '5', '6'], last: '5·2·6·4·1·3' } },
    { id: 'w-note', type: 'NoteManager',     isEnabled: false, displayOrder: 6, config: { text: '' } },
  ];

  // ══════════════════════════════════════════════════════════════════════════
  // SUMMARY (post-partita) — esito VITTORIA (missione completata, 1 caduto)
  // ══════════════════════════════════════════════════════════════════════════
  const summary = {
    result: 'victory', // 'victory' | 'defeat'
    mission: 'Caccia al Necromante',
    rounds: 9, turns: 31,
    survivorsAlive: 3, survivorsTotal: 4,
    defeatCause: 'Tutti i survivor a terra', // usato solo se result === 'defeat'
    objectives: [
      { id: 'o1', label: 'Salva i 3 civili nascosti', done: true },
      { id: 'o2', label: 'Uccidi tutti i Necromancer', done: true },
      { id: 'o3', label: 'Apri il portone della cripta', done: true },
      { id: 'o4', label: 'Raggiungi il punto di estrazione', done: true },
    ],
    // stato finale per survivor (skill tree finale, livello, kill, ferite, armi)
    survivors: [
      { id: 's-bruno', name: 'Bruno', player: 'Marco', hue: 8,   klass: 'Berserker', xp: 58, level: 'red',    wounds: 1, dead: false,
        kills: { walker: 18, orc: 7, runner: 4, fatty: 3, abomination: 1, necromancer: 0 }, weapons: ['Katana', 'Pistola', 'Molotov'] },
      { id: 's-elena', name: 'Elena', player: 'Anna', hue: 282, klass: 'Heroine',   xp: 49, level: 'red',    wounds: 0, dead: false,
        kills: { walker: 14, orc: 3, runner: 6, fatty: 1, abomination: 0, necromancer: 4 }, weapons: ['Balestra', 'Balestra a mano'] },
      { id: 's-tomas', name: 'Tomas', player: 'Luca', hue: 150, klass: 'Tracker',   xp: 27, level: 'orange', wounds: 1, dead: false,
        kills: { walker: 9, orc: 2, runner: 3, fatty: 1, abomination: 0, necromancer: 1 }, weapons: ['Arco lungo', 'Torcia'] },
      { id: 's-greta', name: 'Greta', player: 'Sara', hue: 40,  klass: 'Hunter',    xp: 12, level: 'yellow', wounds: 2, dead: true,
        kills: { walker: 6, orc: 0, runner: 2, fatty: 1, abomination: 0, necromancer: 0 }, weapons: ['Canne mozze', 'Molotov'] },
    ],
    // zombie uccisi totali per tipo (istogramma)
    zombieKills: { walker: 47, orc: 12, runner: 15, fatty: 6, abomination: 1, necromancer: 5 },
    // transizioni Danger Level per round
    dangerByRound: [
      { round: 1, level: 'blue' }, { round: 2, level: 'blue' }, { round: 3, level: 'yellow' },
      { round: 4, level: 'yellow' }, { round: 5, level: 'orange' }, { round: 6, level: 'orange' },
      { round: 7, level: 'orange' }, { round: 8, level: 'red' }, { round: 9, level: 'red' },
    ],
    stats: {
      facts: [
        { lb: 'Round totali', v: '9' },
        { lb: 'Turni totali', v: '31' },
        { lb: 'Tiri medi / combat', v: '4.2' },
        { lb: 'Zombie eliminati', v: '86' },
      ],
      avgXpPerTurn: '7.8',
      mostUsedWeapons: [
        { name: 'Katana', uses: 22, e: 'danger' },
        { name: 'Balestra', uses: 19, e: 'session' },
        { name: 'Arco lungo', uses: 14, e: 'session' },
        { name: 'Canne mozze', uses: 9, e: 'event' },
      ],
    },
  };

  window.ZOM = {
    game, agent, levelOf, levelMeta, levelIndex, LEVELS, DANGER, ZTYPES, ZORDER, EQUIP, eq, EQUIP_SLOTS,
    survivors, byId, topSurvivor, dangerLevel, board, zombieTotal, objectives,
    phases, phaseState, combat, weaponsRef, spawnDeck, mapTiles,
    ds, counterWidgets, summary,
  };
})();

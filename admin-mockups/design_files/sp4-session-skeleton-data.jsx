/* MeepleAI SP4 — Generic Session Skeleton · DATA
   Game-AGNOSTIC runtime fixtures. Nothing here is hard-wired into the
   renderers — every value is shaped like what the Toolkit/Toolbox API would
   return (see toolkit.schemas.ts): AiScoringTemplateSuggestion,
   AiTurnTemplateSuggestion, ToolkitDashboardDto.widgets[].

   Two live datasets prove the same skeleton adapts to opposite genres:
     A · Wingspan  — engine builder · Points · RoundRobin · 4 round
     B · Paleo     — co-op         · BinaryWin · Simultaneous · no score

   Plus GALLERY fixtures that exercise every other switch branch the two
   datasets don't hit (Ranking/Objectives · Sequential/Realtime/None/Custom).

   Exposes window.SkeletonData. */

(function () {
  // ─── DATASET A · WINGSPAN (engine builder) ───────────────────────────────
  const wingspan = {
    game: {
      id: 'wingspan', title: 'Wingspan', emoji: '🦜',
      cover: 'linear-gradient(135deg, hsl(165,55%,42%), hsl(205,60%,38%))',
      meta: '1–5 giocatori · ~70 min', complexity: 2.4,
    },
    session: { id: 'wing-3', elapsed: '1:24:06', startedAt: '23 apr · 20:41' },
    agent: { name: 'Wingspan Coach', emoji: '🦜', latency: 'Risponde in 1–2s' },
    players: [
      { id: 'p-marco', name: 'Marco', hue: 262, score: 87, turnDelta: 16, presence: 'host' },
      { id: 'p-anna',  name: 'Anna',  hue: 350, score: 82, turnDelta: 0,  presence: 'online' },
      { id: 'p-luca',  name: 'Luca',  hue: 174, score: 74, turnDelta: 0,  presence: 'online' },
      { id: 'p-sara',  name: 'Sara',  hue: 38,  score: 61, turnDelta: 0,  presence: 'away' },
    ],
    // AiScoringTemplateSuggestion → scoreType "Points"
    scoring: {
      scoreType: 'Points', defaultUnit: 'punti',
      dimensions: ['Uccelli', 'Carte bonus', 'Obiettivi round', 'Uova', 'Cibo', 'Carte infilate'],
      categories: [
        { id: 'birds',  label: 'Uccelli',         computation: 'Sum',       weight: 1, description: 'Punti variabili stampati su ogni carta uccello (1–9).' },
        { id: 'bonus',  label: 'Carte bonus',      computation: 'Sum',       weight: 1, description: 'Punti variabili per obiettivo bonus personale.' },
        { id: 'goals',  label: 'Obiettivi round',  computation: 'RankBased', weight: 1, description: '5/2/1 o 4/2/1 punti per round in base al rank.' },
        { id: 'eggs',   label: 'Uova',             computation: 'Count',     weight: 1, description: '1 punto per uovo deposto.' },
        { id: 'cached', label: 'Cibo',             computation: 'Count',     weight: 1, description: '1 punto per cibo accumulato su una carta.' },
        { id: 'tucked', label: 'Carte infilate',   computation: 'Count',     weight: 1, description: '1 punto per carta infilata sotto un uccello.' },
      ],
    },
    breakdown: {
      'p-marco': { birds: 31, bonus: 16, goals: 11, eggs: 18, cached: 6, tucked: 5 },
      'p-anna':  { birds: 34, bonus: 9,  goals: 13, eggs: 14, cached: 7, tucked: 5 },
      'p-luca':  { birds: 28, bonus: 7,  goals: 9,  eggs: 16, cached: 9, tucked: 5 },
      'p-sara':  { birds: 22, bonus: 5,  goals: 8,  eggs: 13, cached: 8, tucked: 5 },
    },
    // AiTurnTemplateSuggestion → turnOrderType "RoundRobin"
    turn: {
      turnOrderType: 'RoundRobin',
      phases: ['Round 1', 'Round 2', 'Round 3', 'Round 4'],
      rounds: 4, turnsPerRound: [8, 7, 6, 5],
      turnActions: ['Gioca uccello', 'Cerca cibo', 'Deponi uova', 'Pesca carte'],
      direction: 'clockwise',
    },
    turnState: { round: 2, turnInRound: 3, activePlayerId: 'p-marco', actionIndex: 1 },
    // ToolkitDashboardDto.widgets[]
    widgets: [
      { id: 'w1', type: 'RandomGenerator', isEnabled: true,  displayOrder: 0, config: { name: 'Dadi mangiatoia', faces: ['🐛', '🌱', '🍒', '🐟', '🐭', '✦'], quantity: 5, last: '🌱 🐛 🐟 ✦ 🌱' } },
      { id: 'w2', type: 'ResourceManager', isEnabled: true,  displayOrder: 1, config: { perPlayer: true, resources: [{ label: 'Uova', value: 3, max: 6 }, { label: 'Cibo', value: 5, max: 99 }, { label: 'Cubi azione', value: 6, max: 8 }] } },
      { id: 'w3', type: 'ScoreTracker',    isEnabled: true,  displayOrder: 2, config: {} },
      { id: 'w4', type: 'NoteManager',     isEnabled: true,  displayOrder: 3, config: { text: 'Marco apertura aggressiva sul forest.\nWetland con 3 uccelli a turno 8.' } },
      { id: 'w5', type: 'TurnManager',     isEnabled: false, displayOrder: 4, config: {} },
      { id: 'w6', type: 'Whiteboard',      isEnabled: false, displayOrder: 5, config: {} },
    ],
    events: [
      { t: '14:51', who: 'p-marco', kind: 'score-update', text: 'gioca <b>Aquila reale</b> nel forest · +9 punti' },
      { t: '14:49', who: 'p-anna',  kind: 'turn-change',   text: 'completa l\u2019obiettivo di round <b>uova in nido</b>' },
      { t: '14:45', who: 'p-luca',  kind: 'custom',        text: 'attiva potere a catena · pesca 3 carte' },
      { t: '14:41', who: 'p-sara',  kind: 'score-update',  text: 'deposita <b>4 uova</b> sulla wetland' },
      { t: '14:32', who: null,      kind: 'game-start',    text: 'inizio <b>Round 2</b> · 7 turni rimasti' },
    ],
    chat: [
      { id: 'c1', from: 'user',  t: '14:51', text: 'Quanto vale la wetland a fine partita?' },
      { id: 'c2', from: 'agent', t: '14:51', text: 'La wetland dà punti tramite gli uccelli giocati e le uova deposte sopra — non ha un bonus di colonna proprio.', citations: ['Wingspan §4.2'] },
      { id: 'c3', from: 'user',  t: '14:58', text: 'I bird con uova multiple come si contano?' },
      { id: 'c4', from: 'agent', t: '14:58', text: 'Ogni uovo deposto vale 1 punto a fine partita, indipendentemente dal massimo del bird.', citations: ['Wingspan §6', 'FAQ #12'] },
    ],
    suggestions: ['Punteggio bonus card?', 'Regola wetland', 'Tucked cards'],
    notes: 'Marco apertura aggressiva sul forest.\nWetland con 3 uccelli a turno 8.\n→ Valutare bonus card "Eggs in nest" a fine partita.',
    photos: [
      { lb: 'Tableau T8', g: 'linear-gradient(135deg, hsl(85,60%,55%), hsl(180,50%,40%))' },
      { lb: 'Wetland',    g: 'linear-gradient(135deg, hsl(200,60%,55%), hsl(230,50%,40%))' },
    ],
    connections: [
      { type: 'game', label: 'Wingspan' }, { type: 'player', label: '4 giocatori' },
      { type: 'agent', label: 'Coach' }, { type: 'kb', label: '2 doc' },
      { type: 'chat', label: '8 msg' }, { type: 'event', label: '12 eventi' },
    ],
    // ── summary-only ──
    summary: {
      outcome: 'win',
      result: { kind: 'winner', playerId: 'p-marco', headline: 'Marco vince', sub: '87 punti · margine +5' },
      duration: '1h 24min', date: '23 apr 2026', rounds: '4 round · 26 turni',
    },
  };

  // ─── DATASET B · PALEO (co-op, simultaneous, BinaryWin) ───────────────────
  const paleo = {
    game: {
      id: 'paleo', title: 'Paleo', emoji: '🦣',
      cover: 'linear-gradient(135deg, hsl(28,48%,40%), hsl(8,42%,26%))',
      meta: '1–4 giocatori · co-op · ~60 min', complexity: 3.2,
    },
    session: { id: 'paleo-1', elapsed: '0:47:12', startedAt: '30 mag · 21:10' },
    agent: { name: 'Paleo Guida', emoji: '🦣', latency: 'Risponde in 1–2s' },
    players: [
      { id: 'p-giulia', name: 'Giulia', hue: 142, current: true, presence: 'host' },
      { id: 'p-davide', name: 'Davide', hue: 200, current: true, presence: 'online' },
      { id: 'p-elena',  name: 'Elena',  hue: 320, current: true, presence: 'online' },
    ],
    // scoreType "BinaryWin" — collective, no points
    scoring: {
      scoreType: 'BinaryWin', defaultUnit: 'esito',
      dimensions: ['Condizione di vittoria', 'Pitture rupestri'],
      collective: {
        goalLabel: 'Pittura rupestre', goalValue: 3, goalMax: 5, goalHint: 'impronte',
        failLabel: 'Teschi', failValue: 2, failMax: 5, failHint: 'su 5 = sconfitta',
      },
      categories: [
        { id: 'cave',    label: 'Pittura rupestre completata', computation: 'Count',  weight: 1,  description: 'Completa la pittura (5 impronte) per vincere.' },
        { id: 'skulls',  label: 'Teschi accumulati',           computation: 'Count',  weight: -1, description: '5 teschi = sconfitta immediata per la tribù.' },
        { id: 'extinct', label: 'Tribù estinta',               computation: 'Custom', weight: 0,  description: 'Tutti i membri morti = sconfitta collettiva.' },
      ],
    },
    breakdown: null,
    // turnOrderType "Simultaneous"
    turn: {
      turnOrderType: 'Simultaneous',
      phases: ['Mattina', 'Giorno', 'Notte'],
      rounds: null, turnsPerRound: null,
      turnActions: ['Esplora', 'Caccia', 'Costruisci', 'Sviluppa', 'Riposa'],
      direction: 'none',
    },
    turnState: { phaseIndex: 1 },
    widgets: [
      { id: 'w1', type: 'ResourceManager', isEnabled: true,  displayOrder: 0, config: { shared: true, resources: [{ label: 'Legno', value: 6, max: 30 }, { label: 'Pietra', value: 4, max: 30 }, { label: 'Cibo', value: 8, max: 30 }, { label: 'Teschi', value: 2, max: 5, danger: true }, { label: 'Membri tribù', value: 4, max: 8 }] } },
      { id: 'w2', type: 'TurnManager',     isEnabled: true,  displayOrder: 1, config: { phaseBased: true } },
      { id: 'w3', type: 'NoteManager',     isEnabled: true,  displayOrder: 2, config: { text: 'Non spendere cibo prima della Notte.\nElena tiene la carta "fuoco".' } },
      { id: 'w4', type: 'Whiteboard',      isEnabled: true,  displayOrder: 3, config: {} },
      { id: 'w5', type: 'RandomGenerator', isEnabled: false, displayOrder: 4, config: {} },
      { id: 'w6', type: 'ScoreTracker',    isEnabled: false, displayOrder: 5, config: {} },
    ],
    events: [
      { t: '21:54', who: null,      kind: 'custom',       text: 'la tribù aggiunge la <b>3ª impronta</b> alla pittura' },
      { t: '21:50', who: 'p-elena', kind: 'score-update', text: 'subisce un <b>teschio</b> dalla carta notte · 2/5' },
      { t: '21:46', who: 'p-davide',kind: 'turn-change',  text: 'caccia il mammut · +3 cibo condiviso' },
      { t: '21:40', who: 'p-giulia',kind: 'custom',       text: 'sblocca abilità <b>lancia</b> per la tribù' },
      { t: '21:10', who: null,      kind: 'game-start',   text: 'inizio partita · fase <b>Mattina</b>' },
    ],
    chat: [
      { id: 'c1', from: 'user',  t: '21:48', text: 'Se finiamo le carte del mazzo notte cosa succede?' },
      { id: 'c2', from: 'agent', t: '21:48', text: 'Esaurire il mazzo notte non è una sconfitta diretta: si risolve la fine del giorno e si controllano le condizioni di fine partita.', citations: ['Paleo §Notte'] },
      { id: 'c3', from: 'user',  t: '21:52', text: 'Il teschio si può rimuovere?' },
      { id: 'c4', from: 'agent', t: '21:52', text: 'No, i teschi sono permanenti. Al 5° teschio la tribù perde immediatamente, qualunque sia lo stato della pittura.', citations: ['Paleo §Sconfitta'] },
    ],
    suggestions: ['Condizioni di fine?', 'Carte missione', 'Gestione fame'],
    notes: 'Non spendere cibo prima della Notte.\nElena tiene la carta "fuoco".\n→ Servono 2 impronte ancora, attenzione ai teschi.',
    photos: [
      { lb: 'Pittura', g: 'linear-gradient(135deg, hsl(28,50%,45%), hsl(8,45%,30%))' },
    ],
    connections: [
      { type: 'game', label: 'Paleo' }, { type: 'player', label: '3 · co-op' },
      { type: 'agent', label: 'Guida' }, { type: 'kb', label: '1 doc' },
      { type: 'chat', label: '6 msg' }, { type: 'event', label: '9 eventi' },
    ],
    summary: {
      outcome: 'win',
      result: { kind: 'collective', won: true, headline: 'Tribù sopravvissuta', sub: 'Pittura completata · 5/5 impronte · 3 teschi' },
      duration: '1h 02min', date: '30 mag 2026', rounds: 'Co-op · 11 giorni',
    },
  };

  // ─── GALLERY FIXTURES — branches the 2 datasets don't exercise ────────────
  // Scoring: Ranking + Objectives (Points/BinaryWin already covered above).
  const scoringFixtures = {
    Ranking: {
      scoring: { scoreType: 'Ranking', defaultUnit: 'posizione', categories: [] },
      players: [
        { id: 'r1', name: 'Bea',   hue: 25,  score: 0 },
        { id: 'r2', name: 'Tariq', hue: 195, score: 0 },
        { id: 'r3', name: 'Noa',   hue: 262, score: 0 },
        { id: 'r4', name: 'Kai',   hue: 142, score: 0 },
      ],
      ranking: [
        { id: 'r2', name: 'Tariq', hue: 195, rank: 1, sub: 'in testa di 2 case' },
        { id: 'r1', name: 'Bea',   hue: 25,  rank: 2, sub: 'a –2' },
        { id: 'r4', name: 'Kai',   hue: 142, rank: 3, sub: 'a –5' },
        { id: 'r3', name: 'Noa',   hue: 262, rank: 4, sub: 'ultimo' },
      ],
      meta: 'Gara · giro 3 / 5',
    },
    Objectives: {
      scoring: { scoreType: 'Objectives', defaultUnit: 'obiettivo', categories: [] },
      objectives: [
        { id: 'o1', label: 'Costruisci 3 avamposti',     done: true },
        { id: 'o2', label: 'Esplora la regione nord',     done: true },
        { id: 'o3', label: 'Recupera l\u2019artefatto',   done: false, progress: '2 / 3 frammenti' },
        { id: 'o4', label: 'Nessun membro perso',         done: false, progress: 'a rischio' },
        { id: 'o5', label: 'Completa entro il giorno 12', done: false, progress: 'giorno 9' },
      ],
      meta: 'Campagna · capitolo 4',
    },
  };

  // Turn: Sequential / Realtime / None / Custom (RoundRobin+Simultaneous covered).
  const turnFixtures = {
    Sequential: {
      turn: { turnOrderType: 'Sequential', phases: ['Spirito', 'Crescita', 'Veloce', 'Invasori', 'Lento'], turnActions: [], direction: 'team' },
      turnState: { phaseIndex: 2 },
      meta: 'Fasi a squadra · turno 5',
    },
    Realtime: {
      turn: { turnOrderType: 'Realtime', phases: ['Tempo reale'], turnActions: [], direction: 'none' },
      turnState: {},
      meta: 'Nessun turno · simultaneo continuo',
    },
    None: {
      turn: { turnOrderType: 'None', phases: [], turnActions: [], direction: 'none' },
      turnState: {},
      meta: 'Gioco libero',
    },
    Custom: {
      turn: { turnOrderType: 'Custom', phases: ['Setup', 'Azioni', 'Mercato', 'Pulizia'], turnActions: [], direction: 'free' },
      turnState: { phaseIndex: 1 },
      meta: 'Sequenza custom dal toolkit',
    },
  };

  // Widget: one representative config per type for the dispatcher gallery.
  const widgetFixtures = [
    { id: 'g-rng', type: 'RandomGenerator', isEnabled: true, displayOrder: 0, config: { name: 'Dado d20', faces: ['1','2','…','20'], quantity: 1, last: '🎲 14' } },
    { id: 'g-trn', type: 'TurnManager',     isEnabled: true, displayOrder: 1, config: {} },
    { id: 'g-sco', type: 'ScoreTracker',    isEnabled: true, displayOrder: 2, config: {} },
    { id: 'g-res', type: 'ResourceManager', isEnabled: true, displayOrder: 3, config: { shared: true, resources: [{ label: 'Oro', value: 7, max: 20 }, { label: 'Legno', value: 3, max: 20 }] } },
    { id: 'g-not', type: 'NoteManager',     isEnabled: true, displayOrder: 4, config: { text: 'Promemoria di partita…' } },
    { id: 'g-whb', type: 'Whiteboard',      isEnabled: true, displayOrder: 5, config: {} },
  ];

  window.SkeletonData = {
    DATASETS: { wingspan, paleo },
    ORDER: ['wingspan', 'paleo'],
    scoringFixtures, turnFixtures, widgetFixtures,
    STATES: [
      { id: 'default', lb: 'Default' }, { id: 'empty', lb: 'Empty' },
      { id: 'loading', lb: 'Loading' }, { id: 'error', lb: 'Error' }, { id: 'sse', lb: 'SSE-off' },
    ],
  };
})();

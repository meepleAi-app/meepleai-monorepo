/* MeepleAI SP4 · Paleo — DATASET  (window.PaleoData · alias PD)
   Game-specific data for the co-op session "Paleo" (preistorico, simultaneo,
   1–4 giocatori, ~45–60 min). Feeds both the reused skeleton renderers
   (TopBar · ChatAgentPanel · ActionLogTimeline · ScoringPanelRenderer ·
   TurnIndicatorRenderer) and the Paleo-specific parts/flavor.

   Also defines window.SkeletonData.STATES (the 5 canonical states) since the
   generic skeleton-data module is not part of this delivery.

   Shape of `ds` mirrors window.SkeletonData.DATASETS.* exactly:
     game · session · players · agent · chat · suggestions · events ·
     notes · photos · scoring (BinaryWin) · turn (Simultaneous) · turnState. */

(function () {
  // ─── 5 canonical states (id 'sse' = sse-disconnect) ──────────────────────
  const STATES = [
    { id: 'default', lb: 'Default' },
    { id: 'empty',   lb: 'Empty' },
    { id: 'loading', lb: 'Loading' },
    { id: 'error',   lb: 'Error' },
    { id: 'sse',     lb: 'SSE off' },
  ];
  window.SkeletonData = Object.assign(window.SkeletonData || {}, { STATES });

  // ─── human players (co-op · 3 al tavolo) ─────────────────────────────────
  const PLAYERS = [
    { id: 'p-marco', name: 'Marco', hue: 262, you: true },
    { id: 'p-anna',  name: 'Anna',  hue: 350 },
    { id: 'p-luca',  name: 'Luca',  hue: 174 },
  ];
  const byPlayer = id => PLAYERS.find(p => p.id === id);

  // ─── skill catalogue (asimmetrico per membro) ────────────────────────────
  const SKILLS = {
    hunt:    { id: 'hunt',    lb: 'Caccia',       glyph: '🏹', e: 'event',   desc: 'Affronta gli animali e raccoglie cibo e pelli.' },
    gather:  { id: 'gather',  lb: 'Raccolta',     glyph: '🌿', e: 'toolkit', desc: 'Bacche e legna dai boschi senza rischi.' },
    craft:   { id: 'craft',   lb: 'Artigianato',  glyph: '🔨', e: 'game',    desc: 'Costruisce attrezzi, leve e strumenti.' },
    explore: { id: 'explore', lb: 'Esplorazione', glyph: '🧭', e: 'session', desc: 'Scopre nuove tessere e incontri misteriosi.' },
    heal:    { id: 'heal',    lb: 'Cura',          glyph: '✚', e: 'kb',      desc: 'Guarisce i feriti e tiene lontana la malattia.' },
  };

  // ─── tribe members (meeple) · 4 vivi · 1 perso al Giorno 2 ───────────────
  const MEMBERS = [
    { id: 'm1', name: 'Ugar',  owner: 'p-marco', skill: 'hunt',    status: 'away',    place: 'Caccia al bisonte', xp: 2 },
    { id: 'm2', name: 'Mela',  owner: 'p-anna',  skill: 'gather',  status: 'home',    place: 'Accampamento',      xp: 3 },
    { id: 'm3', name: 'Tok',   owner: 'p-luca',  skill: 'craft',   status: 'away',    place: 'Officina',          xp: 1 },
    { id: 'm4', name: 'Brun',  owner: 'p-marco', skill: 'explore', status: 'wounded', place: 'Accampamento',      xp: 2 },
    { id: 'm5', name: 'Lia',   owner: 'p-luca',  skill: 'heal',    status: 'dead',    place: '—', xp: 1, diedDay: 2, cause: 'Tigre dai denti a sciabola' },
  ];
  const aliveMembers = MEMBERS.filter(m => m.status !== 'dead');

  // ─── collective tribe state (shared counters) ────────────────────────────
  const tribe = {
    wood: 6, stone: 4, food: 9, knowledge: 7,
    painting: 3, paintingMax: 5,   // impronte rupestri 0–5  (5 = vittoria)
    skulls: 2, skullsMax: 5,       // teschi 0–5  (5 = sconfitta · tribù estinta)
    membersAlive: aliveMembers.length, membersMax: 8,
    members: MEMBERS,
  };

  // ─── day / phase cycle ───────────────────────────────────────────────────
  const PHASES = [
    { id: 'morning', n: 1, short: 'Mattina', glyph: '🌅', desc: 'Pesca carte dal mazzo e scegli le azioni in mano segreta.' },
    { id: 'day',     n: 2, short: 'Giorno',  glyph: '☀️', desc: 'Rivela e risolvi le azioni di tutti simultaneamente.' },
    { id: 'night',   n: 3, short: 'Notte',   glyph: '🌙', desc: 'I membri tornano alla tribù · nutri tutti · controlla le condizioni di fine.' },
  ];
  const day = { day: 3, totalDays: 6, phaseIndex: 1, phases: PHASES }; // index 1 = Giorno (reveal)

  // ─── cards & decks ───────────────────────────────────────────────────────
  const MISSIONS = [
    { id: 'fire',    icon: '🔥', lb: 'Domina il fuoco',     progress: 2, goal: 3, hint: 'legna + scintilla' },
    { id: 'hut',     icon: '🛖', lb: 'Costruisci la capanna', progress: 1, goal: 2, hint: 'serve molta pietra' },
    { id: 'totem',   icon: '🗿', lb: 'Erigi il totem',       progress: 0, goal: 4, hint: 'rituale di tribù' },
  ];
  const cards = {
    actionDeck: 28,
    missionDeck: 9,
    missionBreakdown: [
      { lb: 'Sopravvivenza', n: 4, e: 'toolkit' },
      { lb: 'Sapienza',      n: 3, e: 'kb' },
      { lb: 'Pericolo',      n: 2, e: 'event' },
    ],
    encounters: [
      { id: 'e1', lb: 'Grotta',    icon: '🕳' },
      { id: 'e2', lb: 'Foresta',   icon: '🌲' },
      { id: 'e3', lb: 'Notturno',  icon: '🌑' },
    ],
    discard: 14,
    missions: MISSIONS,
  };

  // ─── per-player hands & ready status (fascia) ────────────────────────────
  // current view = Marco (you). Le mani altrui sono segrete (face-down + conteggio).
  const ACTIONS = {
    hunt:    { id: 'hunt',    lb: 'Caccia',     icon: '🏹', e: 'event' },
    explore: { id: 'explore', lb: 'Esplora',    icon: '🧭', e: 'session' },
    craft:   { id: 'craft',   lb: 'Costruisci', icon: '🔨', e: 'game' },
    develop: { id: 'develop', lb: 'Sviluppa',   icon: '📜', e: 'kb' },
    rest:    { id: 'rest',    lb: 'Riposa',     icon: '🌿', e: 'toolkit' },
  };
  const hands = {
    youId: 'p-marco',
    seats: [
      { playerId: 'p-marco', count: 4, ready: true,  chosen: 'hunt',
        hand: [
          { id: 'h1', action: 'hunt',    title: 'Bisonte',    cost: '2 forza', e: 'event' },
          { id: 'h2', action: 'explore', title: 'Cresta',     cost: '—',       e: 'session' },
          { id: 'h3', action: 'craft',   title: 'Ascia',      cost: '1 legna 1 pietra', e: 'game' },
          { id: 'h4', action: 'develop', title: 'Pittura',    cost: '1 sapere', e: 'kb' },
        ] },
      { playerId: 'p-anna', count: 5, ready: true,  chosen: 'develop', hand: null },
      { playerId: 'p-luca', count: 4, ready: false, chosen: null,      hand: null },
    ],
  };

  // ─── action reveal (fase Giorno · simultaneo · auto-resolve in ordine) ───
  const reveal = {
    phase: 'Giorno',
    resolved: false,
    cards: [
      { playerId: 'p-marco', action: 'hunt',    priority: 1, title: 'Caccia · Bisonte',
        outcome: { kind: 'gain', icon: '🍖', text: '+3 cibo · +1 sapere', e: 'game' } },
      { playerId: 'p-anna',  action: 'develop', priority: 2, title: 'Sviluppa · Pittura rupestre',
        outcome: { kind: 'paint', icon: '✋', text: '+1 impronta (3 → 4)', e: 'kb' } },
      { playerId: 'p-luca',  action: 'craft',   priority: 3, title: 'Costruisci · Ascia di pietra',
        outcome: { kind: 'gain', icon: '🪓', text: 'Attrezzo: +1 forza permanente', e: 'toolkit' } },
      { playerId: 'p-marco', action: 'explore', priority: 4, title: 'Esplora · Cresta rocciosa',
        outcome: { kind: 'skull', icon: '💀', text: 'Incontro fallito · +1 teschio (2 → 3)', e: 'event' } },
    ],
  };

  // ─── skill tree (asimmetrico per membro · per la tab Skills) ─────────────
  const skillTree = aliveMembers.map(m => ({
    member: m,
    nodes: ({
      hunt:    [{ lb: 'Lancia',    done: true }, { lb: 'Trappola',  done: true }, { lb: 'Branco',    done: false }],
      gather:  [{ lb: 'Cesto',     done: true }, { lb: 'Sentiero',  done: true }, { lb: 'Orto',      done: false }],
      craft:   [{ lb: 'Martello',  done: true }, { lb: 'Leva',      done: false }, { lb: 'Ruota',    done: false }],
      explore: [{ lb: 'Mappa',     done: true }, { lb: 'Bussola',   done: false }, { lb: 'Vetta',    done: false }],
    })[m.skill] || [],
  }));

  // ─── skeleton dataset (`ds`) — same shape as DATASETS.* ──────────────────
  const ds = {
    game: {
      title: 'Paleo', emoji: '🦣',
      cover: 'linear-gradient(135deg, hsl(28,55%,32%), hsl(18,40%,18%))',
      meta: 'Co-op · 1–4 giocatori · 45–60 min · Giorno 3/6',
    },
    session: { elapsed: '38:12' },
    players: PLAYERS,
    agent: { emoji: '🦣', name: 'Paleo · Guida', latency: '~1.1s' },
    chat: [
      { id: 'c1', from: 'user',  t: '17:21', text: 'Se un membro è ferito può ancora agire di Giorno?' },
      { id: 'c2', from: 'agent', t: '17:21', text: 'Un membro ferito resta all\u2019accampamento e non parte: non gioca azioni finché non viene curato con un\u2019azione "Cura". Torna disponibile alla Notte successiva.', citations: ['Paleo §Notte', 'FAQ ferite'] },
      { id: 'c3', from: 'user',  t: '17:29', text: 'Quanti teschi mancano alla sconfitta?' },
      { id: 'c4', from: 'agent', t: '17:29', text: 'Al 5° teschio l\u2019intera tribù si estingue e la partita è persa. Ora siete a 2/5 — un margine di 3.', citations: ['Paleo §Fine partita'] },
    ],
    suggestions: ['Come completo la pittura?', 'Regola della fame', 'Incontro Notturno'],
    events: [
      { kind: 'game-start',  who: null,       t: '16:43', text: 'Partita avviata · <strong>Giorno 1</strong> · tribù di 5 membri.' },
      { kind: 'custom',      who: 'p-luca',   t: '17:02', text: '<strong>Lia</strong> uccisa da una <strong>tigre dai denti a sciabola</strong> · +1 teschio.' },
      { kind: 'score-update',who: 'p-anna',   t: '17:14', text: 'Completata la <strong>2ª impronta</strong> della pittura rupestre.' },
      { kind: 'turn-change', who: null,       t: '17:18', text: 'Inizio <strong>Giorno 3</strong> · fase Mattina · carte pescate.' },
      { kind: 'score-update',who: 'p-marco',  t: '17:24', text: 'Trovata <strong>sorgente d\u2019acqua</strong> · +2 cibo alla tribù.' },
      { kind: 'custom',      who: 'p-marco',  t: '17:31', text: 'Esplorazione fallita sulla cresta · <strong>+1 teschio</strong> (2/5).' },
    ],
    notes: 'Priorità: completare il fuoco (manca 1) prima della Notte. Tenere 3 cibo di riserva per la fame. Brun va curato o salta il prossimo Giorno.',
    photos: [
      { lb: 'Tavolo G2', g: 'linear-gradient(135deg, hsl(28,50%,40%), hsl(160,35%,30%))' },
      { lb: 'Pittura',   g: 'linear-gradient(135deg, hsl(20,60%,55%), hsl(35,55%,42%))' },
    ],

    // BinaryWin scoring (consumed by ScoringPanelRenderer)
    scoring: {
      scoreType: 'BinaryWin',
      collective: {
        goalLabel: 'Pittura rupestre', goalValue: 3, goalMax: 5, goalHint: 'impronte',
        failLabel: 'Teschi', failValue: 2, failMax: 5, failHint: 'estinzione tribù',
      },
      categories: [
        { id: 'cave-painting',  label: 'Pittura completata (5 impronte)', computation: 'Count',  weight: 1,  description: 'Completa la pittura rupestre (5 impronte) per vincere.' },
        { id: 'skulls',         label: 'Teschi accumulati',                computation: 'Count',  weight: -1, description: '5 teschi = sconfitta immediata per tutta la tribù.' },
        { id: 'tribe-extinct',  label: 'Tribù estinta (tutti morti)',      computation: 'Custom', weight: 0,  description: 'Condizione di sconfitta — tutti i membri morti.' },
      ],
    },

    // Simultaneous turn (consumed by TurnIndicatorRenderer)
    turn: { turnOrderType: 'Simultaneous', phases: ['Mattina', 'Giorno', 'Notte'] },
    turnState: { phaseIndex: 1 },
  };

  // ══════════════════════════════════════════════════════════════════════════
  // SUMMARY datasets (post-game) — victory / defeat-skulls / defeat-extinct
  // ══════════════════════════════════════════════════════════════════════════
  const journey = [
    { member: MEMBERS[0], turns: 6, kills: 3, wounds: 1, end: 'alive' },
    { member: MEMBERS[1], turns: 6, kills: 0, wounds: 0, end: 'alive' },
    { member: MEMBERS[2], turns: 6, kills: 1, wounds: 2, end: 'alive' },
    { member: MEMBERS[3], turns: 6, kills: 1, wounds: 3, end: 'alive' },
    { member: MEMBERS[4], turns: 2, kills: 0, wounds: 1, end: 'dead' },
  ];
  const timeline = [
    { day: 1, icon: '🎲', e: 'session', text: 'Partita avviata · tribù di 5 membri.' },
    { day: 2, icon: '💀', e: 'event',   text: 'Lia uccisa dalla tigre · primo teschio.' },
    { day: 2, icon: '🏹', e: 'event',   text: 'Ugar apprende la Trappola (caccia).' },
    { day: 3, icon: '✋', e: 'kb',      text: '2ª e 3ª impronta della pittura completate.' },
    { day: 4, icon: '🔥', e: 'toolkit', text: 'Fuoco domato · missione completata.' },
    { day: 5, icon: '🛖', e: 'game',    text: 'Capanna costruita · riparo per la Notte.' },
    { day: 6, icon: '🖐', e: 'toolkit', text: '5ª impronta dipinta · pittura completata!' },
  ];
  const cardFreq = [
    { lb: 'Caccia',     icon: '🏹', n: 11, e: 'event' },
    { lb: 'Raccolta',   icon: '🌿', n: 14, e: 'toolkit' },
    { lb: 'Esplora',    icon: '🧭', n: 9,  e: 'session' },
    { lb: 'Costruisci', icon: '🔨', n: 7,  e: 'game' },
    { lb: 'Sviluppa',   icon: '📜', n: 6,  e: 'kb' },
  ];
  const summary = {
    base: {
      days: 6, totalDays: 6,
      knowledge: 13, resourcesGained: 71, resourcesPerDay: 11.8, syncRate: 0.72,
      missionsDone: 4, missionsTotal: 6, encounterSurvival: 0.81,
      journey, timeline, cardFreq,
    },
    outcomes: {
      victory: {
        kind: 'victory', title: 'Vittoria', emoji: '🖐',
        sub: 'La pittura rupestre è completa — 5 impronte sulla parete.',
        cause: 'Pittura rupestre completata al Giorno 6 con 4 membri vivi e 3 teschi.',
        painting: 5, skulls: 3, membersAlive: 4,
      },
      'defeat-skulls': {
        kind: 'defeat', title: 'Sconfitta', emoji: '💀',
        sub: 'Cinque teschi: la tribù si è estinta nel buio.',
        cause: '5° teschio accumulato al Giorno 5 — soglia di estinzione raggiunta.',
        painting: 3, skulls: 5, membersAlive: 2,
      },
      'defeat-extinct': {
        kind: 'defeat', title: 'Sconfitta', emoji: '🦴',
        sub: 'Nessun membro è sopravvissuto alla notte.',
        cause: 'Tutti i membri della tribù morti al Giorno 4 — tribù estinta.',
        painting: 2, skulls: 4, membersAlive: 0,
      },
    },
  };

  window.PaleoData = {
    STATES, PLAYERS, byPlayer, SKILLS, ACTIONS, MEMBERS, aliveMembers,
    tribe, day, PHASES, cards, hands, reveal, skillTree, ds, summary,
  };
})();

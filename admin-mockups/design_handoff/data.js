/* MeepleAI — shared dataset for the deliverable.
   Fake data with coherent cross-references between entities.
   Attached to window.DS for all pages to read. */

window.DS = (function () {

  // Entity color map (HSL triplets - must match tokens.css)
  const EC = {
    game:    { h: 25,  s: 95, l: 45, em: '🎲', lb: 'Game' },
    player:  { h: 262, s: 83, l: 58, em: '👤', lb: 'Player' },
    session: { h: 240, s: 60, l: 55, em: '🎯', lb: 'Session' },
    agent:   { h: 38,  s: 92, l: 50, em: '🤖', lb: 'Agent' },
    kb:      { h: 174, s: 60, l: 40, em: '📄', lb: 'KB' },
    chat:    { h: 220, s: 80, l: 55, em: '💬', lb: 'Chat' },
    event:   { h: 350, s: 89, l: 60, em: '📅', lb: 'Event' },
    toolkit: { h: 142, s: 70, l: 45, em: '🧰', lb: 'Toolkit' },
    tool:    { h: 195, s: 80, l: 50, em: '🔧', lb: 'Tool' },
  };

  // Helper for cover gradients
  const grad = (h, s) => `linear-gradient(135deg, hsl(${h},${s}%,55%), hsl(${h},${s-20}%,30%) 60%, hsl(${(h+30)%360},${s-10}%,40%))`;

  // ── GAMES ────────────────────────────────────────────
  const games = [
    { id: 'g-azul', type: 'game', title: 'Azul', publisher: 'Plan B Games', year: 2017,
      author: 'Michael Kiesling', players: '2–4', duration: '30–45m', weight: 1.77, rating: 7.8, stars: 4,
      cover: grad(200, 70), coverEmoji: '🔷', status: 'owned', badge: 'In Libreria',
      kbState: 'indexed', totalPlays: 23, winRate: 0.65, avgScore: 72 },
    { id: 'g-catan', type: 'game', title: 'I Coloni di Catan', publisher: 'Kosmos', year: 1995,
      author: 'Klaus Teuber', players: '3–4', duration: '60–120m', weight: 2.33, rating: 7.1, stars: 4,
      cover: grad(30, 80), coverEmoji: '🌾', status: 'owned', badge: 'Posseduto',
      kbState: 'indexed', totalPlays: 41, winRate: 0.48, avgScore: 9 },
    { id: 'g-wingspan', type: 'game', title: 'Wingspan', publisher: 'Stonemaier', year: 2019,
      author: 'Elizabeth Hargrave', players: '1–5', duration: '40–70m', weight: 2.42, rating: 8.1, stars: 5,
      cover: grad(85, 45), coverEmoji: '🦜', status: 'owned', badge: 'Top 10',
      kbState: 'indexed', totalPlays: 17, winRate: 0.59, avgScore: 89 },
    { id: 'g-brass', type: 'game', title: 'Brass: Birmingham', publisher: 'Roxley', year: 2018,
      author: 'Wallace · Lopiano', players: '2–4', duration: '60–120m', weight: 3.91, rating: 8.6, stars: 5,
      cover: grad(220, 40), coverEmoji: '🏭', status: 'owned', badge: 'Preferito',
      kbState: 'partial', totalPlays: 8, winRate: 0.38, avgScore: 132 },
    { id: 'g-gloomhaven', type: 'game', title: 'Gloomhaven', publisher: 'Cephalofair', year: 2017,
      author: 'Isaac Childres', players: '1–4', duration: '90–150m', weight: 3.90, rating: 8.7, stars: 5,
      cover: grad(0, 35), coverEmoji: '⚔️', status: 'wishlist', badge: 'Wishlist',
      kbState: 'initial', totalPlays: 0, winRate: 0, avgScore: 0 },
    { id: 'g-arknova', type: 'game', title: 'Ark Nova', publisher: 'Feuerland', year: 2021,
      author: 'Mathias Wigge', players: '1–4', duration: '90–150m', weight: 3.72, rating: 8.5, stars: 5,
      cover: grad(130, 50), coverEmoji: '🦒', status: 'owned', badge: 'Nuovo',
      kbState: 'partial', totalPlays: 3, winRate: 0.33, avgScore: 118 },
    { id: 'g-spirit', type: 'game', title: 'Spirit Island', publisher: 'GMT', year: 2017,
      author: 'R. Eric Reuss', players: '1–4', duration: '90–120m', weight: 4.08, rating: 8.3, stars: 5,
      cover: grad(210, 40), coverEmoji: '🌋', status: 'owned', badge: 'Heavy',
      kbState: 'partial', totalPlays: 5, winRate: 0.60, avgScore: 0 },
    { id: 'g-7wonders', type: 'game', title: '7 Wonders Duel', publisher: 'Repos', year: 2015,
      author: 'Cathala · Bauza', players: '2', duration: '30m', weight: 2.23, rating: 8.0, stars: 4,
      cover: grad(40, 60), coverEmoji: '🏛️', status: 'owned', badge: 'Duello',
      kbState: 'indexed', totalPlays: 31, winRate: 0.55, avgScore: 64 },
  ];

  // ── PLAYERS ──────────────────────────────────────────
  const players = [
    { id: 'p-marco', type: 'player', title: 'Marco R.', subtitle: 'Membro Gen 2025 · 89 partite',
      cover: grad(262, 60), coverEmoji: '👤', initials: 'MR', color: 262,
      status: 'active', badge: 'Pro', totalWins: 47, totalSessions: 89, winRate: 0.528, fav: 'Azul' },
    { id: 'p-sara', type: 'player', title: 'Sara T.', subtitle: 'Membro Mar 2024 · 142 partite',
      cover: grad(320, 55), coverEmoji: '👤', initials: 'ST', color: 320,
      status: 'active', badge: 'Veterana', totalWins: 71, totalSessions: 142, winRate: 0.500, fav: 'Wingspan' },
    { id: 'p-luca', type: 'player', title: 'Luca B.', subtitle: 'Membro Giu 2025 · 54 partite',
      cover: grad(180, 50), coverEmoji: '👤', initials: 'LB', color: 180,
      status: 'active', badge: 'Rising', totalWins: 22, totalSessions: 54, winRate: 0.407, fav: 'Catan' },
    { id: 'p-giulia', type: 'player', title: 'Giulia M.', subtitle: 'Membro Feb 2026 · 12 partite',
      cover: grad(10, 60), coverEmoji: '👤', initials: 'GM', color: 10,
      status: 'active', badge: 'Nuova', totalWins: 4, totalSessions: 12, winRate: 0.333, fav: 'Brass' },
    { id: 'p-andrea', type: 'player', title: 'Andrea P.', subtitle: 'Membro Ott 2024 · 78 partite',
      cover: grad(100, 45), coverEmoji: '👤', initials: 'AP', color: 100,
      status: 'idle', badge: 'Casual', totalWins: 31, totalSessions: 78, winRate: 0.397, fav: 'Ark Nova' },
  ];

  // ── SESSIONS ─────────────────────────────────────────
  const sessions = [
    { id: 's-azul-live', type: 'session', title: 'Serata Azul', subtitle: 'In corso · Turno 3/5',
      cover: grad(240, 55), coverEmoji: '🎯', status: 'inprogress', badge: 'In Corso',
      state: 'live', turn: '3/5', code: 'AZL-7X2K', gameId: 'g-azul',
      playerIds: ['p-marco', 'p-sara', 'p-luca', 'p-giulia'], toolCount: 3, duration: '45m' },
    { id: 's-wing-042', type: 'session', title: 'Wingspan · Domenica', subtitle: 'Conclusa · 5 giocatori',
      cover: grad(85, 45), coverEmoji: '🦜', status: 'completed', badge: 'Conclusa',
      state: 'done', turn: '10/10', code: 'WNG-042X', gameId: 'g-wingspan',
      playerIds: ['p-sara', 'p-marco', 'p-giulia', 'p-luca', 'p-andrea'], toolCount: 2, duration: '1h 42m', winner: 'Sara T.' },
    { id: 's-brass-041', type: 'session', title: 'Brass Martedì', subtitle: 'Conclusa · 4 giocatori',
      cover: grad(220, 40), coverEmoji: '🏭', status: 'completed', badge: 'Conclusa',
      state: 'done', turn: '18/18', code: 'BRS-041', gameId: 'g-brass',
      playerIds: ['p-marco', 'p-sara', 'p-luca'], toolCount: 3, duration: '2h 15m', winner: 'Marco R.' },
    { id: 's-catan-pause', type: 'session', title: 'Catan Epico', subtitle: 'In pausa · Turno 12',
      cover: grad(30, 80), coverEmoji: '🌾', status: 'paused', badge: 'In Pausa',
      state: 'paused', turn: '12/?', code: 'CTN-9K3', gameId: 'g-catan',
      playerIds: ['p-andrea', 'p-sara', 'p-giulia'], toolCount: 2, duration: '1h 20m' },
    { id: 's-arknova-setup', type: 'session', title: 'Ark Nova Venerdì', subtitle: 'Setup · In attesa',
      cover: grad(130, 50), coverEmoji: '🦒', status: 'setup', badge: 'Setup',
      state: 'setup', turn: '0/?', code: 'ARK-NEW', gameId: 'g-arknova',
      playerIds: ['p-marco', 'p-sara'], toolCount: 1, duration: '0m' },
    { id: 's-agricola-arch', type: 'session', title: 'Agricola · Archivio', subtitle: 'Archiviata · Gen 2026',
      cover: grad(40, 40), coverEmoji: '🚜', status: 'archived', badge: 'Archivio',
      state: 'done', turn: '14/14', code: 'AGR-ARC', gameId: null,
      playerIds: ['p-marco', 'p-andrea'], toolCount: 1, duration: '2h 10m', winner: 'Andrea P.' },
  ];

  // ── AGENTS ───────────────────────────────────────────
  const agents = [
    { id: 'a-azul-rules', type: 'agent', title: 'Azul Rules Expert', subtitle: 'RAG · GPT-4o-mini · Attivo',
      cover: grad(38, 80), coverEmoji: '🤖', status: 'active', badge: 'Attivo',
      strategy: 'hybrid-rag', model: 'GPT-4o-mini', gameId: 'g-azul',
      docs: 3, invocations: 342, avgLatency: '1.2s' },
    { id: 'a-catan-coach', type: 'agent', title: 'Catan Coach', subtitle: 'RAG · Claude · Attivo',
      cover: grad(30, 75), coverEmoji: '🤖', status: 'active', badge: 'Attivo',
      strategy: 'rag-citations', model: 'Claude Haiku', gameId: 'g-catan',
      docs: 2, invocations: 187, avgLatency: '0.9s' },
    { id: 'a-brass-expert', type: 'agent', title: 'Brass Helper', subtitle: 'RAG · GPT-4o · Attivo',
      cover: grad(220, 60), coverEmoji: '🤖', status: 'active', badge: 'Attivo',
      strategy: 'hybrid-rag', model: 'GPT-4o', gameId: 'g-brass',
      docs: 4, invocations: 95, avgLatency: '1.5s' },
    { id: 'a-universal', type: 'agent', title: 'Game Night Host', subtitle: 'Multi-game · GPT-4o',
      cover: grad(60, 70), coverEmoji: '🤖', status: 'idle', badge: 'Idle',
      strategy: 'router', model: 'GPT-4o', gameId: null,
      docs: 12, invocations: 1203, avgLatency: '2.1s' },
    { id: 'a-wing-rules', type: 'agent', title: 'Wingspan Rules', subtitle: 'RAG · Claude · Attivo',
      cover: grad(85, 60), coverEmoji: '🤖', status: 'active', badge: 'Attivo',
      strategy: 'rag-citations', model: 'Claude Sonnet', gameId: 'g-wingspan',
      docs: 2, invocations: 230, avgLatency: '1.1s' },
  ];

  // ── KB DOCS ──────────────────────────────────────────
  const kbs = [
    { id: 'kb-azul-ita', type: 'kb', title: 'azul-regole-ita.pdf', subtitle: '2.4 MB · 12 pag · Indicizzato',
      cover: grad(174, 55), coverEmoji: '📄', status: 'indexed', badge: 'Indicizzato',
      pages: 12, size: '2.4 MB', chunks: 47, embedding: 'e5-base 768d', gameId: 'g-azul' },
    { id: 'kb-azul-eng', type: 'kb', title: 'azul-rules-en.pdf', subtitle: '1.8 MB · 10 pag · Indicizzato',
      cover: grad(174, 55), coverEmoji: '📄', status: 'indexed', badge: 'Indicizzato',
      pages: 10, size: '1.8 MB', chunks: 38, embedding: 'e5-base 768d', gameId: 'g-azul' },
    { id: 'kb-azul-faq', type: 'kb', title: 'azul-faq.md', subtitle: '120 KB · 4 pag · Indicizzato',
      cover: grad(174, 55), coverEmoji: '📄', status: 'indexed', badge: 'Indicizzato',
      pages: 4, size: '120 KB', chunks: 14, embedding: 'e5-base 768d', gameId: 'g-azul' },
    { id: 'kb-catan-ita', type: 'kb', title: 'catan-regole.pdf', subtitle: '3.1 MB · 18 pag · Indicizzato',
      cover: grad(174, 55), coverEmoji: '📄', status: 'indexed', badge: 'Indicizzato',
      pages: 18, size: '3.1 MB', chunks: 62, embedding: 'e5-base 768d', gameId: 'g-catan' },
    { id: 'kb-brass-proc', type: 'kb', title: 'brass-rulebook.pdf', subtitle: '6.2 MB · 32 pag · In elaborazione',
      cover: grad(174, 55), coverEmoji: '📄', status: 'processing', badge: 'Processing',
      pages: 32, size: '6.2 MB', chunks: 0, embedding: null, gameId: 'g-brass' },
    { id: 'kb-wingspan', type: 'kb', title: 'wingspan-rules.pdf', subtitle: '4.8 MB · 24 pag · Indicizzato',
      cover: grad(174, 55), coverEmoji: '📄', status: 'indexed', badge: 'Indicizzato',
      pages: 24, size: '4.8 MB', chunks: 89, embedding: 'e5-base 768d', gameId: 'g-wingspan' },
    { id: 'kb-gloom-failed', type: 'kb', title: 'gloomhaven-scenarios.pdf', subtitle: '48 MB · Failed (troppo grande)',
      cover: grad(174, 55), coverEmoji: '📄', status: 'failed', badge: 'Failed',
      pages: 0, size: '48 MB', chunks: 0, embedding: null, gameId: 'g-gloomhaven' },
  ];

  // ── CHATS ────────────────────────────────────────────
  const chats = [
    { id: 'c-azul-rules', type: 'chat', title: 'Come si gioca ad Azul?', subtitle: 'Azul Rules Expert · 12 msg',
      cover: grad(220, 60), coverEmoji: '💬', status: 'active', badge: 'Attiva',
      msgCount: 12, lastAt: '5 min fa', agentId: 'a-azul-rules', gameId: 'g-azul' },
    { id: 'c-azul-score', type: 'chat', title: 'Come calcolo i bonus di riga?', subtitle: 'Azul Rules Expert · 4 msg',
      cover: grad(220, 60), coverEmoji: '💬', status: 'active', badge: 'Attiva',
      msgCount: 4, lastAt: '2 ore fa', agentId: 'a-azul-rules', gameId: 'g-azul' },
    { id: 'c-catan-strat', type: 'chat', title: 'Strategia inizio partita Catan', subtitle: 'Catan Coach · 18 msg',
      cover: grad(220, 60), coverEmoji: '💬', status: 'active', badge: 'Attiva',
      msgCount: 18, lastAt: 'Ieri', agentId: 'a-catan-coach', gameId: 'g-catan' },
    { id: 'c-brass-ind', type: 'chat', title: 'Link industry obbligatorio?', subtitle: 'Brass Helper · 6 msg',
      cover: grad(220, 60), coverEmoji: '💬', status: 'archived', badge: 'Archiviata',
      msgCount: 6, lastAt: '3 giorni fa', agentId: 'a-brass-expert', gameId: 'g-brass' },
    { id: 'c-wing-food', type: 'chat', title: 'Azione "Guadagna cibo"?', subtitle: 'Wingspan Rules · 8 msg',
      cover: grad(220, 60), coverEmoji: '💬', status: 'active', badge: 'Attiva',
      msgCount: 8, lastAt: 'Stamattina', agentId: 'a-wing-rules', gameId: 'g-wingspan' },
  ];

  // ── EVENTS ───────────────────────────────────────────
  const events = [
    { id: 'e-marco-serata', type: 'event', title: 'Serata da Marco', subtitle: '15 Mar · 19:00 · Casa Marco',
      cover: grad(350, 65), coverEmoji: '🎉', status: 'inprogress', badge: 'Confermato',
      date: '15 Mar 2026', time: '19:00–23:00', location: '📍 Casa di Marco',
      participantIds: ['p-marco', 'p-sara', 'p-luca', 'p-giulia', 'p-andrea'],
      gameIds: ['g-azul', 'g-wingspan', 'g-7wonders'], confirmed: 3, pending: 2 },
    { id: 'e-club-night', type: 'event', title: 'Game Night Club', subtitle: '22 Mar · 20:00 · Pub',
      cover: grad(280, 55), coverEmoji: '🍻', status: 'inprogress', badge: 'Confermato',
      date: '22 Mar 2026', time: '20:00–00:00', location: '📍 The Dragon Pub',
      participantIds: ['p-marco', 'p-sara', 'p-andrea'],
      gameIds: ['g-brass', 'g-spirit'], confirmed: 2, pending: 1 },
    { id: 'e-torneo', type: 'event', title: 'Torneo 7 Wonders', subtitle: '5 Apr · 14:00 · Ludoteca',
      cover: grad(45, 70), coverEmoji: '🏆', status: 'setup', badge: 'Setup',
      date: '5 Apr 2026', time: '14:00–19:00', location: '📍 Ludoteca Centrale',
      participantIds: ['p-marco', 'p-sara', 'p-luca', 'p-giulia', 'p-andrea'],
      gameIds: ['g-7wonders'], confirmed: 4, pending: 1 },
    { id: 'e-archive', type: 'event', title: 'Capodanno Ludico', subtitle: '31 Dic · Conclusa',
      cover: grad(0, 50), coverEmoji: '🎊', status: 'completed', badge: 'Conclusa',
      date: '31 Dic 2025', time: '20:00–02:00', location: '📍 Casa Giulia',
      participantIds: ['p-marco', 'p-sara', 'p-luca', 'p-giulia', 'p-andrea'],
      gameIds: ['g-azul', 'g-brass', 'g-catan', 'g-wingspan'], confirmed: 5, pending: 0 },
  ];

  // ── TOOLKITS ─────────────────────────────────────────
  const toolkits = [
    { id: 'tk-azul-v2', type: 'toolkit', title: 'Azul Toolkit v2', subtitle: 'Pubblicato · 3 strumenti',
      cover: grad(142, 60), coverEmoji: '🧰', status: 'active', badge: 'Pubblicato',
      version: 2, toolCount: 3, useCount: 12, gameId: 'g-azul', owner: 'p-marco' },
    { id: 'tk-catan-base', type: 'toolkit', title: 'Catan Essentials', subtitle: 'Pubblicato · 4 strumenti',
      cover: grad(30, 70), coverEmoji: '🧰', status: 'active', badge: 'Pubblicato',
      version: 1, toolCount: 4, useCount: 28, gameId: 'g-catan', owner: 'p-sara' },
    { id: 'tk-brass-pro', type: 'toolkit', title: 'Brass Pro Tools', subtitle: 'Draft · 5 strumenti',
      cover: grad(220, 50), coverEmoji: '🧰', status: 'setup', badge: 'Draft',
      version: 1, toolCount: 5, useCount: 2, gameId: 'g-brass', owner: 'p-marco' },
    { id: 'tk-generic', type: 'toolkit', title: 'Universal Game Night', subtitle: 'Pubblicato · 6 strumenti',
      cover: grad(280, 55), coverEmoji: '🧰', status: 'active', badge: 'Pubblicato',
      version: 3, toolCount: 6, useCount: 67, gameId: null, owner: 'p-sara' },
  ];

  // ── TOOLS ────────────────────────────────────────────
  const tools = [
    { id: 't-timer', type: 'tool', title: 'Timer Turno Azul', subtitle: 'Timer · 5:00 · Per giocatore',
      cover: grad(195, 70), coverEmoji: '⏱️', status: 'active', badge: 'Attivo',
      kind: 'timer', config: { duration: '5:00', perPlayer: true, warning: '30s' }, uses: 23, toolkitId: 'tk-azul-v2' },
    { id: 't-score-azul', type: 'tool', title: 'Punteggi Azul', subtitle: 'Counter · Righe+Col+Set',
      cover: grad(195, 70), coverEmoji: '🔢', status: 'active', badge: 'Attivo',
      kind: 'counter', config: { formula: 'rows+cols+sets' }, uses: 35, toolkitId: 'tk-azul-v2' },
    { id: 't-deck', type: 'tool', title: 'Mazzo Tessere', subtitle: 'Deck · 100 carte · Shuffle',
      cover: grad(195, 70), coverEmoji: '🃏', status: 'active', badge: 'Attivo',
      kind: 'deck', config: { cards: 100, shuffle: true }, uses: 12, toolkitId: 'tk-azul-v2' },
    { id: 't-dice', type: 'tool', title: 'Dado Catan', subtitle: 'Dice · 2d6 · Storico',
      cover: grad(195, 70), coverEmoji: '🎲', status: 'active', badge: 'Attivo',
      kind: 'dice', config: { dice: '2d6', history: true }, uses: 89, toolkitId: 'tk-catan-base' },
    { id: 't-tracker', type: 'tool', title: 'Risorse Brass', subtitle: 'Tracker · Carbone/Ferro/Birra',
      cover: grad(195, 70), coverEmoji: '📊', status: 'active', badge: 'Attivo',
      kind: 'tracker', config: { resources: ['Coal', 'Iron', 'Beer'] }, uses: 8, toolkitId: 'tk-brass-pro' },
  ];

  // ── ALL ──────────────────────────────────────────────
  const all = [
    ...games, ...players, ...sessions, ...agents,
    ...kbs, ...chats, ...events, ...toolkits, ...tools,
  ];
  const byId = Object.fromEntries(all.map(e => [e.id, e]));

  // ── HOME FEED / ACTIVITY ─────────────────────────────
  const activity = [
    { id: 'ac1', at: '10 min fa', who: 'Marco R.', what: 'ha avviato', ref: 's-azul-live', kind: 'session' },
    { id: 'ac2', at: '2h fa', who: 'Sara T.', what: 'ha chiesto',  ref: 'c-wing-food', kind: 'chat' },
    { id: 'ac3', at: 'Ieri', who: 'Luca B.', what: 'ha vinto',     ref: 's-wing-042', kind: 'session' },
    { id: 'ac4', at: 'Ieri', who: 'Sistema', what: 'ha indicizzato', ref: 'kb-wingspan', kind: 'kb' },
    { id: 'ac5', at: '2g fa', who: 'Giulia M.', what: 'si è unita', ref: 'e-marco-serata', kind: 'event' },
    { id: 'ac6', at: '3g fa', who: 'Marco R.', what: 'ha pubblicato', ref: 'tk-azul-v2', kind: 'toolkit' },
    { id: 'ac7', at: '4g fa', who: 'Andrea P.', what: 'ha aggiunto', ref: 'g-arknova', kind: 'game' },
    { id: 'ac8', at: '1s fa', who: 'Sara T.', what: 'ha archiviato', ref: 'c-brass-ind', kind: 'chat' },
  ];

  // Color helper for entity
  const color = (type, a = 1) => {
    const e = EC[type] || EC.game;
    return a === 1 ? `hsl(${e.h},${e.s}%,${e.l}%)` : `hsla(${e.h},${e.s}%,${e.l}%,${a})`;
  };

  return { EC, games, players, sessions, agents, kbs, chats, events, toolkits, tools, all, byId, activity, color, grad };
})();

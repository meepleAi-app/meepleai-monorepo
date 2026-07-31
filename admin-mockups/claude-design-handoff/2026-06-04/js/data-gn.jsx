/* MeepleAI Nav Prototype — derived domain layer (corrected model).
   GameNight = top-level social event wrapper (planned | in-progress | completed),
   holding players[] (User-linked or Guest) and sessions[] (single games played
   inside the night). data.js `events` are the GameNight source; `sessions` are
   the games. The GameNight→Session link is NOT in data.js, so it's synthesized
   here from event.gameIds + game.avgScore + plausible MVPs (flagged in-product).
   Dates are normalized relative to "today = gio 4 giu 2026". */
window.GN = (function () {
  const D = window.DS;
  const meId = 'p-marco'; // current user identity (owner/RSVP context)

  // Players: data.js players are User-linked; add one Guest (Anna) to exercise
  // the guest path and the tagging-vs-RSVP tension.
  const players = [
    ...D.players.map(p => ({ ...p, linked: true,
      bio: p.subtitle, library: D.games.filter(g => g.status !== 'wishlist').slice(0, 4).map(g => g.id) })),
    { id: 'p-anna', type: 'player', title: 'Anna G.', name: 'Anna G.', initials: 'AG', color: 330,
      cover: D.grad(330, 55), coverEmoji: '👤', linked: false, badge: 'Guest', fav: 'Codenames',
      totalSessions: 0, totalWins: 0, winRate: 0, subtitle: 'Guest · nessun account' },
  ];
  const playerById = Object.fromEntries(players.map(p => [p.id, p]));
  const me = playerById[meId];
  const pname = (id) => (playerById[id] || {}).title || id;
  const savedLocations = ['Casa Marco', 'Casa Anna', 'Ludoteca Centrale', 'The Dragon Pub'];
  const friendsList = players.filter(p => p.linked && p.id !== meId);

  // Relational stats vs ME (synthesized; only meaningful for User-linked peers).
  const relational = (p) => {
    if (!p.linked) return null;
    if (p.id === meId) return { self: true };
    const together = Math.max(3, Math.round((p.totalSessions || 0) * 0.35));
    const winsVsMe = Math.round(together * (0.4 + (p.color % 30) / 100));
    return {
      together,
      winRateVsMe: Math.round((winsVsMe / together) * 100),
      common: [p.fav, 'Azul'].filter((v, i, a) => v && a.indexOf(v) === i).slice(0, 2),
    };
  };

  const gid = (id) => D.byId[id];
  const gtitle = (id) => (gid(id) || {}).title || id;

  // GameNights — ordered fields per dashboard need.
  const nights = [
    { id: 'gn-marco', name: 'Casa Marco', status: 'in-progress', dateLabel: 'Oggi · gio 4 giu', month: 'Giugno 2026',
      dateSort: 0, location: 'Casa di Marco', host: 'p-marco', time: '20:00',
      playerIds: ['p-marco', 'p-sara', 'p-luca', 'p-giulia'], confirmed: 3, pending: 1,
      gameIds: ['g-azul', 'g-wingspan'], autoPromoted: true,
      sessions: [
        { n: 1, gameId: 'g-azul', mvp: 'p-marco', score: 72, result: 'MVP Marco', createdAt: '21:15', startedAt: '21:15', completedAt: '21:42' },
        { n: 2, gameId: 'g-wingspan', live: true, result: 'in corso · turno 4', createdAt: '22:30', startedAt: '22:30', completedAt: null },
      ] },
    { id: 'gn-club', name: 'Game Night Club', status: 'planned', dateLabel: 'Sab 6 giu', month: 'Giugno 2026',
      dateSort: 2, location: 'The Dragon Pub', host: 'p-sara', time: '20:30',
      playerIds: ['p-marco', 'p-sara', 'p-andrea', 'p-anna'], confirmed: 2, pending: 2,
      taggedPending: 'p-anna', gameIds: ['g-brass', 'g-spirit'],
      sessions: [] },
    { id: 'gn-torneo', name: 'Torneo 7 Wonders', status: 'planned', dateLabel: 'Sab 13 giu', month: 'Giugno 2026',
      dateSort: 9, location: 'Ludoteca Centrale', host: 'p-marco', time: '14:00',
      playerIds: ['p-marco', 'p-sara', 'p-luca', 'p-giulia', 'p-andrea'], confirmed: 4, pending: 1,
      gameIds: ['g-7wonders'], sessions: [] },

    { id: 'gn-giulia', name: 'Casa Giulia', status: 'completed', dateLabel: 'Dom 1 giu', month: 'Giugno 2026',
      dateSort: -3, location: 'Casa di Giulia', host: 'p-giulia', time: '19:00',
      playerIds: ['p-marco', 'p-sara', 'p-luca', 'p-giulia', 'p-andrea'], confirmed: 5, pending: 0,
      mvp: 'p-sara', gameIds: ['g-wingspan', 'g-azul', 'g-7wonders'], cachedAgo: '3h',
      sessions: [
        { n: 1, gameId: 'g-wingspan', mvp: 'p-sara', score: 92, result: 'MVP Sara · 92 pt' },
        { n: 2, gameId: 'g-azul', mvp: 'p-marco', score: 72, result: 'MVP Marco · 72 pt' },
        { n: 3, gameId: 'g-7wonders', mvp: 'p-sara', score: 64, result: 'MVP Sara · 64 pt' },
      ] },
    { id: 'gn-brass', name: 'Brass Night', status: 'completed', dateLabel: 'Mar 27 mag', month: 'Maggio 2026',
      dateSort: -8, location: 'Casa di Marco', host: 'p-marco', time: '20:00',
      playerIds: ['p-marco', 'p-sara', 'p-luca'], confirmed: 3, pending: 0,
      mvp: 'p-marco', gameIds: ['g-brass', 'g-catan'], cachedAgo: '9d',
      sessions: [
        { n: 1, gameId: 'g-brass', mvp: 'p-marco', score: 132, result: 'MVP Marco · 132 pt' },
        { n: 2, gameId: 'g-catan', mvp: 'p-luca', score: 10, result: 'MVP Luca · 10 pt' },
      ] },
    { id: 'gn-wing', name: 'Domenica Wingspan', status: 'completed', dateLabel: 'Dom 18 mag', month: 'Maggio 2026',
      dateSort: -17, location: 'Casa di Sara', host: 'p-sara', time: '18:30',
      playerIds: ['p-sara', 'p-marco', 'p-giulia', 'p-luca', 'p-andrea'], confirmed: 5, pending: 0,
      mvp: 'p-anna', gameIds: ['g-wingspan'], cachedAgo: '17d',
      sessions: [
        { n: 1, gameId: 'g-wingspan', mvp: 'p-anna', score: 88, result: 'MVP Anna (guest) · 88 pt' },
      ] },
  ];
  const nightById = Object.fromEntries(nights.map(n => [n.id, n]));
  const upcoming = nights.filter(n => n.status !== 'completed').sort((a, b) => a.dateSort - b.dateSort);
  const recent = nights.filter(n => n.status === 'completed').sort((a, b) => b.dateSort - a.dateSort);

  // Suggestions — owned games, treated as "not played in last 30d".
  const suggestions = D.games.filter(g => g.status !== 'wishlist').map(g => g.id);

  // Friends feed — informational only.
  const friends = [
    { playerId: 'p-sara', action: 'ha programmato', kind: 'gn', refId: 'gn-torneo', at: '2h fa' },
    { playerId: 'p-luca', action: 'ha loggato', kind: 'game', refId: 'g-arknova', at: '5h fa' },
    { playerId: 'p-andrea', action: 'ha loggato', kind: 'game', refId: 'g-brass', at: 'ieri' },
  ];

  // All atomic sessions of a single game, across every GameNight (newest first).
  function sessionsForGame(gameId) {
    const out = [];
    nights.forEach(n => (n.sessions || []).forEach(s => {
      if (s.gameId === gameId) out.push({
        nightId: n.id, nightName: n.name, date: n.dateLabel, dateSort: n.dateSort,
        n: s.n, mvp: s.mvp, score: s.score, result: s.result, live: s.live,
        players: n.playerIds, status: s.live ? 'live' : 'completed',
      });
    }));
    return out.sort((a, b) => b.dateSort - a.dateSort);
  }
  // Most-recent play date (for "Played" sort); -Infinity if never played.
  function lastPlayedSort(gameId) {
    const ss = sessionsForGame(gameId);
    return ss.length ? ss[0].dateSort : -1e9;
  }

  // Ownership / invitation relative to the current user.
  const isMine = (n) => n.host === meId;
  const amInvited = (n) => (n.playerIds || []).includes(meId) && n.host !== meId;

  // Per-player RSVP (synthesized — data.js only has aggregate counts).
  function perPlayerRsvp(n, pid) {
    if (n.status === 'completed') return 'confirmed';
    if (pid === n.host) return 'confirmed';
    if (pid === n.taggedPending) return 'pending';
    if (n.declined && n.declined.includes(pid)) return 'declined';
    return 'confirmed';
  }
  const sessionTime = (i) => ['21:15', '22:30', '23:45', '00:50', '01:30'][i] || '—';

  // Parse a "2–4" / "1–5" / "2" players string into [min, max].
  function parsePlayers(str) {
    const m = String(str || '').replace(/[–—]/g, '-').match(/(\d+)(?:-(\d+))?/);
    if (!m) return [1, 99];
    return [parseInt(m[1], 10), parseInt(m[2] || m[1], 10)];
  }

  // ── Live invariant: at most ONE live session per GameNight ──
  const liveSession = (n) => (n && n.sessions || []).find(s => s.live) || null;
  const pad2 = (x) => String(x).padStart(2, '0');
  const nowHHMM = () => { const d = new Date(); return pad2(d.getHours()) + ':' + pad2(d.getMinutes()); };
  const durMin = (a, b) => { if (!a || !b) return null; const p = (t) => { const [h, m] = t.split(':').map(Number); return h * 60 + m; }; let d = p(b) - p(a); if (d < 0) d += 1440; return d; };
  // Persistent draft sessions (created from the new-session modal, Live OFF).
  function addDraftSession(nightId, gameId) {
    const n = nightById[nightId]; if (!n) return null;
    n.drafts = n.drafts || [];
    const num = (n.sessions ? n.sessions.length : 0) + n.drafts.length + 1;
    const draft = { n: num, gameId, status: 'draft', createdAt: nowHHMM(), startedAt: null, completedAt: null };
    n.drafts.push(draft);
    return draft;
  }
  function completeDraft(nightId, num, res) {
    const n = nightById[nightId]; if (!n || !n.drafts) return;
    const d = n.drafts.find(x => x.n === num);
    if (d) Object.assign(d, { status: 'completed', completedAt: nowHHMM() }, res);
  }
  function terminateLive(nightId, scores) {
    const n = nightById[nightId]; const s = liveSession(n); if (!s) return;
    s.live = false; s.completedAt = nowHHMM();
    if (scores) { const ids = Object.keys(scores); ids.sort((a, b) => scores[b] - scores[a]); const mvp = ids[0]; if (mvp !== undefined) { s.mvp = mvp; s.score = scores[mvp]; s.result = 'MVP ' + pname(mvp) + ' · ' + scores[mvp] + ' pt'; } }
    else if (!s.result) { s.result = 'Conclusa'; }
  }

  // Flatten every atomic session/draft across all GameNights (for /sessions).
  function statusOf(s) { return s.live ? 'live' : (s.status === 'draft' ? 'draft' : 'completed'); }
  function allSessions() {
    const out = [];
    nights.forEach(n => {
      [...(n.sessions || []), ...(n.drafts || [])].forEach(s => out.push({
        nightId: n.id, nightName: n.name, nightDate: n.dateLabel, host: n.host, mine: n.host === meId,
        invited: (n.playerIds || []).includes(meId) && n.host !== meId,
        gameId: s.gameId, n: s.n, status: statusOf(s), createdAt: s.createdAt || '00:00',
        startedAt: s.startedAt || null, completedAt: s.completedAt || null,
        mvp: s.mvp || null, score: s.score, result: s.result || '', playerIds: n.playerIds,
      }));
    });
    return out;
  }
  function agentCategory(a) {
    if (a.strategy === 'router') return 'Arbiter';
    if (a.strategy === 'rag-citations') return 'Strategy';
    return 'Rules expert';
  }
  function agentAccuracy(a) { return 84 + ((a.invocations || 0) % 14); }

  // In-memory create for the wizard. Pushes a planned night and refreshes maps.
  let seq = 1;
  function addNight(rec) {
    const id = 'gn-new-' + (seq++);
    const n = Object.assign({ id, status: 'planned', month: 'Giugno 2026', dateSort: 1,
      confirmed: (rec.playerIds || []).length, pending: 0, sessions: [] }, rec);
    nights.push(n);
    API.nightById[id] = n;
    API.upcoming = nights.filter(x => x.status !== 'completed').sort((a, b) => a.dateSort - b.dateSort);
    API.recent = nights.filter(x => x.status === 'completed').sort((a, b) => b.dateSort - a.dateSort);
    return id;
  }
  let gseq = 1;
  function addGuest(name) {
    const id = 'p-guest-' + (gseq++);
    const init = (name || 'GG').split(/\s+/).map(w => w[0]).join('').slice(0, 2).toUpperCase();
    const p = { id, type: 'player', title: name || 'Guest', name: name || 'Guest', initials: init,
      cover: D.grad((gseq * 70) % 360, 55), coverEmoji: '👤', linked: false, badge: 'Guest',
      totalSessions: 0, totalWins: 0, winRate: 0, subtitle: 'Guest · nessun account' };
    players.push(p); playerById[id] = p;
    return id;
  }

  const API = { me, meId, players, playerById, pname, relational, nights, nightById, upcoming, recent,
    suggestions, friends, friendsList, savedLocations, gtitle, sessionsForGame, lastPlayedSort,
    isMine, amInvited, perPlayerRsvp, sessionTime, addNight, addGuest, parsePlayers,
    liveSession, addDraftSession, completeDraft, terminateLive, nowHHMM, durMin,
    allSessions, agentCategory, agentAccuracy };
  return API;
})();

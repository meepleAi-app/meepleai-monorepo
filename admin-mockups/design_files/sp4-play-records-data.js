/* MeepleAI SP4 — Play records fixture extension.
   Estende window.DS (definito in data.js) con:
   - DS.playRecords  : array record partita (id 'pr1'..'pr9' con stati misti)
   - DS.stats        : aggregati statistiche per /play-records/stats
   - DS.byId         : aggiunto entry per ciascun pr* + player extra 'p-anna'

   NON modifica data.js (condiviso da 10+ mockup non-play-records).
   Include negli HTML SP4 dopo data.js e prima del mockup .jsx.

   Shape record (reverse-engineering da -index/-detail/-stats/pr-form-core):
     id          : 'pr*'                         (string)
     game        : 'g-*'  -> DS.byId[game]       (string, FK games)
     date        : '17 mag 2026'                 (display)
     duration    : '2h 15m'                      (display, KPI)
     status      : 'completed'|'inprogress'|'planned'  (filter chips)
     outcome     : 'won'|'lost'|'tie'|null       (only completed)
     playerCount : number                        (badge "👥 N giocatori")
     when        : 'Domenica sera'               (sotto-titolo)
     turn        : '7/10'                        (solo inprogress)
     hasChat     : boolean
     chatCount   : number                        (when hasChat)
     notes       : string                        (sezione note)
     scores      : [{ playerId:'p-*', name, score:number|null, winner:bool }]

   Shape stats:
     totals       : { plays, hoursPlayed, games, winRate }
     favoriteGame : 'g-*'  -> DS.byId[favoriteGame]
     mostPlayed   : [{ game:'g-*', plays:number }]
     winByGame    : [{ game:'g-*', played:number, won:number }]
     user         : 'Marco R.'                   (DesktopNav avatar label)
*/
(function () {
  if (!window.DS) {
    console.error('[sp4-play-records-data] window.DS not loaded. Include data.js first.');
    return;
  }
  const DS = window.DS;

  // ── Player extra usato da pr-form-core (PREFILL Wingspan) ─────────
  // pr-form-core PREFILL referenzia 'p-anna' che NON esiste in DS.players.
  // Lo aggiungiamo a DS.byId per evitare il fallback "?.title" undefined.
  if (!DS.byId['p-anna']) {
    DS.byId['p-anna'] = {
      id: 'p-anna', type: 'player', title: 'Anna F.',
      subtitle: 'Membro Set 2025 · 38 partite',
      initials: 'AF', color: 200, status: 'active',
      totalWins: 14, totalSessions: 38, winRate: 0.368, fav: 'Wingspan',
    };
  }

  // ── PLAY RECORDS ──────────────────────────────────────────────────
  // pr1  : Wingspan · Marco vince (cover hero / vittoria)         [completed]
  // pr2  : Azul · Sara vince (recente, list/grid)                 [completed]
  // pr3  : Catan · in corso, turno 7/10                           [inprogress]
  // pr4  : Brass · Marco vince (mid-week)                         [completed]
  // pr5  : 7 Wonders · pareggio 64-64                             [completed,tie]
  // pr6  : Wingspan · Andrea vince (replay)                       [completed]
  // pr7  : Ark Nova · Luca perde (outcome:'lost')                 [completed]
  // pr8  : Azul · in corso, turno 3/5                             [inprogress]
  // pr9  : Wingspan · pianificata (no scores ancora)              [planned]
  const playRecords = [
    {
      id: 'pr1', game: 'g-wingspan',
      date: '17 mag 2026', duration: '1h 42m',
      status: 'completed', outcome: 'won', playerCount: 4,
      when: 'Domenica sera', turn: null,
      hasChat: true, chatCount: 12,
      notes: 'Partita combattuta fino all\'ultimo turno. Marco chiude con un mega-combo wetland e ribalta il vantaggio di Sara grazie ai bonus di fine round.',
      scores: [
        { playerId: 'p-marco',  name: 'Marco R.',  score: 89, winner: true  },
        { playerId: 'p-sara',   name: 'Sara T.',   score: 76, winner: false },
        { playerId: 'p-luca',   name: 'Luca B.',   score: 64, winner: false },
        { playerId: 'p-giulia', name: 'Giulia M.', score: 58, winner: false },
      ],
    },
    {
      id: 'pr2', game: 'g-azul',
      date: '15 mag 2026', duration: '38m',
      status: 'completed', outcome: 'won', playerCount: 3,
      when: 'Venerdì pomeriggio', turn: null,
      hasChat: true, chatCount: 4,
      notes: 'Sara chiude tutte le colonne, bonus +21.',
      scores: [
        { playerId: 'p-sara',  name: 'Sara T.',  score: 82, winner: true  },
        { playerId: 'p-marco', name: 'Marco R.', score: 68, winner: false },
        { playerId: 'p-luca',  name: 'Luca B.',  score: 55, winner: false },
      ],
    },
    {
      id: 'pr3', game: 'g-catan',
      date: '14 mag 2026', duration: '1h 20m',
      status: 'inprogress', outcome: null, playerCount: 4,
      when: 'In pausa · ripresa stasera', turn: '7/10',
      hasChat: true, chatCount: 6,
      notes: 'Marco ha appena costruito la 4ª città, Sara contende il porto.',
      scores: [
        { playerId: 'p-marco',  name: 'Marco R.',  score: 7, winner: false },
        { playerId: 'p-sara',   name: 'Sara T.',   score: 6, winner: false },
        { playerId: 'p-andrea', name: 'Andrea P.', score: 5, winner: false },
        { playerId: 'p-giulia', name: 'Giulia M.', score: 4, winner: false },
      ],
    },
    {
      id: 'pr4', game: 'g-brass',
      date: '11 mag 2026', duration: '2h 15m',
      status: 'completed', outcome: 'won', playerCount: 3,
      when: 'Martedì', turn: null,
      hasChat: false, chatCount: 0,
      notes: 'Strategia ferro early-game premiata.',
      scores: [
        { playerId: 'p-marco', name: 'Marco R.', score: 142, winner: true  },
        { playerId: 'p-sara',  name: 'Sara T.',  score: 128, winner: false },
        { playerId: 'p-luca',  name: 'Luca B.',  score: 111, winner: false },
      ],
    },
    {
      id: 'pr5', game: 'g-7wonders',
      date: '9 mag 2026', duration: '32m',
      status: 'completed', outcome: 'tie', playerCount: 2,
      when: 'Sabato pomeriggio', turn: null,
      hasChat: true, chatCount: 2,
      notes: 'Pareggio perfetto al tie-break: stessa scienza, stesso militare.',
      scores: [
        { playerId: 'p-marco', name: 'Marco R.', score: 64, winner: true  },
        { playerId: 'p-sara',  name: 'Sara T.',  score: 64, winner: true  },
      ],
    },
    {
      id: 'pr6', game: 'g-wingspan',
      date: '5 mag 2026', duration: '1h 28m',
      status: 'completed', outcome: 'lost', playerCount: 4,
      when: 'Domenica · rivincita', turn: null,
      hasChat: true, chatCount: 8,
      notes: 'Andrea domina con strategia praterie.',
      scores: [
        { playerId: 'p-andrea', name: 'Andrea P.', score: 94, winner: true  },
        { playerId: 'p-marco',  name: 'Marco R.',  score: 81, winner: false },
        { playerId: 'p-sara',   name: 'Sara T.',   score: 78, winner: false },
        { playerId: 'p-giulia', name: 'Giulia M.', score: 62, winner: false },
      ],
    },
    {
      id: 'pr7', game: 'g-arknova',
      date: '1 mag 2026', duration: '2h 40m',
      status: 'completed', outcome: 'lost', playerCount: 3,
      when: 'Festivo', turn: null,
      hasChat: false, chatCount: 0,
      notes: 'Luca chiude la conservazione globale al penultimo turno.',
      scores: [
        { playerId: 'p-luca',   name: 'Luca B.',   score: 118, winner: true  },
        { playerId: 'p-marco',  name: 'Marco R.',  score: 104, winner: false },
        { playerId: 'p-andrea', name: 'Andrea P.', score:  97, winner: false },
      ],
    },
    {
      id: 'pr8', game: 'g-azul',
      date: 'oggi · 14:32', duration: '12m',
      status: 'inprogress', outcome: null, playerCount: 2,
      when: 'Live ora', turn: '3/5',
      hasChat: false, chatCount: 0,
      notes: '',
      scores: [
        { playerId: 'p-marco', name: 'Marco R.', score: 28, winner: false },
        { playerId: 'p-luca',  name: 'Luca B.',  score: 22, winner: false },
      ],
    },
    {
      id: 'pr9', game: 'g-wingspan',
      date: '24 mag 2026', duration: '—',
      status: 'planned', outcome: null, playerCount: 4,
      when: 'Sabato sera · pianificata', turn: null,
      hasChat: false, chatCount: 0,
      notes: '',
      scores: [
        { playerId: 'p-marco',  name: 'Marco R.',  score: null, winner: false },
        { playerId: 'p-sara',   name: 'Sara T.',   score: null, winner: false },
        { playerId: 'p-luca',   name: 'Luca B.',   score: null, winner: false },
        { playerId: 'p-giulia', name: 'Giulia M.', score: null, winner: false },
      ],
    },
  ];

  // ── STATS aggregates ──────────────────────────────────────────────
  // totals.plays: somma totale partite registrate dell'utente (mock 89)
  // hoursPlayed: somma durate (display "142h")
  // games: numero distinto giochi giocati (mock 9, leggermente > tipologie DS.games)
  // winRate: 0..1 (47/89 ≈ 0.528)
  // favoriteGame: id da DS.games (Wingspan = top votes/scores)
  // mostPlayed: top giochi per # partite
  // winByGame: stats per gioco (played, won) — il mockup calcola won/played %
  // user: label utente in DesktopNav (avatar "M" + nome)
  const stats = {
    user: 'Marco R.',
    totals: {
      plays: 89,
      hoursPlayed: 142,
      games: 9,
      winRate: 0.528,
    },
    favoriteGame: 'g-wingspan',
    mostPlayed: [
      { game: 'g-catan',    plays: 21 },
      { game: 'g-azul',     plays: 17 },
      { game: 'g-wingspan', plays: 14 },
      { game: 'g-7wonders', plays: 12 },
      { game: 'g-brass',    plays:  9 },
      { game: 'g-arknova',  plays:  7 },
    ],
    winByGame: [
      { game: 'g-wingspan', played: 14, won: 9 },
      { game: 'g-azul',     played: 17, won: 11 },
      { game: 'g-brass',    played:  9, won: 4 },
      { game: 'g-7wonders', played: 12, won: 6 },
      { game: 'g-catan',    played: 21, won: 8 },
      { game: 'g-arknova',  played:  7, won: 2 },
    ],
  };

  // ── Mount on DS + byId entries for pr* ────────────────────────────
  DS.playRecords = playRecords;
  DS.stats = stats;
  playRecords.forEach(r => { DS.byId[r.id] = r; });
})();

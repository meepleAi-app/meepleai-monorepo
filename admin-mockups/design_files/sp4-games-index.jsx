/* MeepleAI SP4 wave 1 — Schermata #1 / 5
   Route: /games
   File: admin-mockups/design_files/sp4-games-index.{html,jsx}
   Pattern desktop: Hero + body grid

   Hero compatto: titolo + stats library (N giochi · M partite · X agenti)
   + filtri (search, status, complexity, players, year, designer) + sort.
   Body: grid 4-col MeepleCard variante `grid` (cover gradient prominente
   + ConnectionChipStrip footer). Click card → /games/[id].

   Stati: default (24+), empty library, loading skeleton, filtered empty.
   Mobile 375 → 2-col compact. Desktop 1440 → 4-col.

   Componenti v2 nuovi flaggati:
   - LibraryHero          → apps/web/src/components/ui/v2/library-hero/
   - GameFilters          → apps/web/src/components/ui/v2/game-filters/
   - MeepleCardGameGrid   → variante `grid` di MeepleCard (già v1, qui usata pixel-perfect)
   - ConnectionChipStrip  → ✅ già in produzione (PR #549/#552)
   - EmptyLibrary         → apps/web/src/components/ui/v2/empty-library/

   Riusi pixel-perfect: ConnectionChipStrip footer (verticale ridotto: agent, kb, session, chat).
*/

const { useState, useEffect, useMemo } = React;
const DS = window.DS;

// ─── ENTITY HSL HELPER (replica spec) ─────────────────
const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.game;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

// ─── DATASET ESTESO PER L'INDEX ─────────────────────
// Espando i 8 giochi base da DS a 24+ con varianti realistiche.
const GAMES_INDEX = [
  ...DS.games,
  { id: 'g-everdell', type: 'game', title: 'Everdell', publisher: 'Starling Games', year: 2018,
    author: 'James A. Wilson', players: '1–4', duration: '40–80m', weight: 2.81, rating: 8.0, stars: 4,
    cover: DS.grad(120, 55), coverEmoji: '🌳', status: 'owned', badge: 'Posseduto',
    agentCount: 1, kbCount: 1, sessionCount: 4, chatCount: 2, totalPlays: 4 },
  { id: 'g-terraforming', type: 'game', title: 'Terraforming Mars', publisher: 'Stronghold', year: 2016,
    author: 'Jacob Fryxelius', players: '1–5', duration: '90–120m', weight: 3.24, rating: 8.4, stars: 5,
    cover: DS.grad(15, 65), coverEmoji: '🚀', status: 'owned', badge: 'Heavy',
    agentCount: 2, kbCount: 3, sessionCount: 11, chatCount: 6, totalPlays: 11 },
  { id: 'g-scythe', type: 'game', title: 'Scythe', publisher: 'Stonemaier', year: 2016,
    author: 'Jamey Stegmaier', players: '1–5', duration: '90–115m', weight: 3.43, rating: 8.2, stars: 5,
    cover: DS.grad(45, 50), coverEmoji: '⚙️', status: 'owned', badge: 'Posseduto',
    agentCount: 1, kbCount: 2, sessionCount: 6, chatCount: 3, totalPlays: 6 },
  { id: 'g-carcassonne', type: 'game', title: 'Carcassonne', publisher: 'HiG', year: 2000,
    author: 'Klaus-Jürgen Wrede', players: '2–5', duration: '30–45m', weight: 1.91, rating: 7.4, stars: 4,
    cover: DS.grad(90, 50), coverEmoji: '🏰', status: 'owned', badge: 'Classico',
    agentCount: 1, kbCount: 1, sessionCount: 19, chatCount: 1, totalPlays: 19 },
  { id: 'g-ticket', type: 'game', title: 'Ticket to Ride', publisher: 'Days of Wonder', year: 2004,
    author: 'Alan R. Moon', players: '2–5', duration: '30–60m', weight: 1.84, rating: 7.4, stars: 4,
    cover: DS.grad(355, 60), coverEmoji: '🚂', status: 'owned', badge: 'Family',
    agentCount: 1, kbCount: 1, sessionCount: 28, chatCount: 0, totalPlays: 28 },
  { id: 'g-pandemic', type: 'game', title: 'Pandemic Legacy S.1', publisher: 'Z-Man', year: 2015,
    author: 'M. Leacock · R. Daviau', players: '2–4', duration: '60m', weight: 2.83, rating: 8.5, stars: 5,
    cover: DS.grad(160, 45), coverEmoji: '🦠', status: 'owned', badge: 'Legacy',
    agentCount: 0, kbCount: 1, sessionCount: 12, chatCount: 1, totalPlays: 12 },
  { id: 'g-root', type: 'game', title: 'Root', publisher: 'Leder Games', year: 2018,
    author: 'Cole Wehrle', players: '2–4', duration: '60–90m', weight: 3.79, rating: 8.0, stars: 4,
    cover: DS.grad(75, 60), coverEmoji: '🦊', status: 'wishlist', badge: 'Wishlist',
    agentCount: 0, kbCount: 0, sessionCount: 0, chatCount: 0, totalPlays: 0 },
  { id: 'g-dune', type: 'game', title: 'Dune: Imperium', publisher: 'Dire Wolf', year: 2020,
    author: 'Paul Dennen', players: '1–4', duration: '60–120m', weight: 3.07, rating: 8.4, stars: 5,
    cover: DS.grad(35, 70), coverEmoji: '🏜️', status: 'owned', badge: 'Top 10',
    agentCount: 2, kbCount: 2, sessionCount: 9, chatCount: 4, totalPlays: 9 },
  { id: 'g-cascadia', type: 'game', title: 'Cascadia', publisher: 'AEG', year: 2021,
    author: 'Randy Flynn', players: '1–4', duration: '30–45m', weight: 1.84, rating: 7.9, stars: 4,
    cover: DS.grad(110, 55), coverEmoji: '🦌', status: 'owned', badge: 'Light',
    agentCount: 0, kbCount: 1, sessionCount: 7, chatCount: 0, totalPlays: 7 },
  { id: 'g-lost-ruins', type: 'game', title: 'Lost Ruins of Arnak', publisher: 'CGE', year: 2020,
    author: 'M. Suchý · M. Suchý', players: '1–4', duration: '30–120m', weight: 2.85, rating: 8.0, stars: 4,
    cover: DS.grad(50, 55), coverEmoji: '🗿', status: 'owned', badge: 'Posseduto',
    agentCount: 1, kbCount: 2, sessionCount: 5, chatCount: 2, totalPlays: 5 },
  { id: 'g-marvel', type: 'game', title: 'Marvel Champions', publisher: 'FFG', year: 2019,
    author: 'M. Boucher · N. French', players: '1–4', duration: '45–90m', weight: 2.55, rating: 7.9, stars: 4,
    cover: DS.grad(0, 70), coverEmoji: '🦸', status: 'owned', badge: 'LCG',
    agentCount: 1, kbCount: 4, sessionCount: 14, chatCount: 5, totalPlays: 14 },
  { id: 'g-paleo', type: 'game', title: 'Paleo', publisher: 'Hans im Glück', year: 2020,
    author: 'Peter Rustemeyer', players: '1–4', duration: '45–60m', weight: 2.74, rating: 7.7, stars: 4,
    cover: DS.grad(25, 50), coverEmoji: '🦴', status: 'wishlist', badge: 'Wishlist',
    agentCount: 0, kbCount: 0, sessionCount: 0, chatCount: 0, totalPlays: 0 },
  { id: 'g-castles', type: 'game', title: 'Castles of Burgundy', publisher: 'Ravensburger', year: 2011,
    author: 'Stefan Feld', players: '2–4', duration: '60–90m', weight: 2.96, rating: 8.1, stars: 5,
    cover: DS.grad(280, 45), coverEmoji: '🏯', status: 'owned', badge: 'Classico',
    agentCount: 1, kbCount: 1, sessionCount: 8, chatCount: 1, totalPlays: 8 },
  { id: 'g-quacks', type: 'game', title: 'Quacks of Quedlinburg', publisher: 'Schmidt', year: 2018,
    author: 'Wolfgang Warsch', players: '2–4', duration: '45m', weight: 1.97, rating: 7.7, stars: 4,
    cover: DS.grad(305, 55), coverEmoji: '🧪', status: 'owned', badge: 'Family',
    agentCount: 0, kbCount: 1, sessionCount: 6, chatCount: 0, totalPlays: 6 },
  { id: 'g-orleans', type: 'game', title: 'Orléans', publisher: 'dlp games', year: 2014,
    author: 'Reiner Stockhausen', players: '2–4', duration: '90m', weight: 3.04, rating: 7.9, stars: 4,
    cover: DS.grad(195, 45), coverEmoji: '⛪', status: 'wishlist', badge: 'Wishlist',
    agentCount: 0, kbCount: 0, sessionCount: 0, chatCount: 0, totalPlays: 0 },
  { id: 'g-blood', type: 'game', title: 'Blood Rage', publisher: 'CMON', year: 2015,
    author: 'Eric M. Lang', players: '2–4', duration: '60–90m', weight: 2.85, rating: 7.9, stars: 4,
    cover: DS.grad(355, 55), coverEmoji: '⚔️', status: 'owned', badge: 'Posseduto',
    agentCount: 0, kbCount: 1, sessionCount: 4, chatCount: 0, totalPlays: 4 },
];

// I primi 8 (DS.games) hanno già le connection counts derivate dal dataset
// Le derivo on the fly per coerenza:
const enrich = g => ({
  ...g,
  agentCount: g.agentCount ?? DS.agents.filter(a => a.gameId === g.id).length,
  kbCount: g.kbCount ?? DS.kbs.filter(k => k.gameId === g.id).length,
  sessionCount: g.sessionCount ?? DS.sessions.filter(s => s.gameId === g.id).length,
  chatCount: g.chatCount ?? DS.chats.filter(c => c.gameId === g.id).length,
});
const ALL_GAMES = GAMES_INDEX.map(enrich);

// ═══════════════════════════════════════════════════════
// ─── STARS ──────────────────────────────────────────
const Stars = ({ value = 0, size = 11 }) => {
  const full = Math.floor(value);
  return (
    <span style={{ display:'inline-flex', gap: 1, color:'hsl(var(--c-agent))', fontSize: size }}>
      {[0,1,2,3,4].map(i => <span key={i} aria-hidden="true">{i < full ? '★' : '☆'}</span>)}
    </span>
  );
};

// ═══════════════════════════════════════════════════════
// ─── CONNECTIONCHIPSTRIP (footer card MeepleCard, prod) ─
// ═══════════════════════════════════════════════════════
// /* v2: ConnectionChipStrip — ✅ in produzione (PR #549/#552) */
const ConnectionChipStrip = ({ connections, compact }) => {
  const visible = connections.filter(c => !c.isEmpty);
  if (visible.length === 0) {
    return (
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
        fontWeight: 600, letterSpacing:'.04em',
      }}>Nessuna connessione</div>
    );
  }
  return (
    <div style={{ display:'flex', gap: 4, flexWrap:'wrap' }}>
      {visible.map(c => {
        const ec = DS.EC[c.entityType];
        return (
          <span key={c.entityType} style={{
            display:'inline-flex', alignItems:'center', gap: 3,
            padding: compact ? '2px 6px' : '3px 7px',
            borderRadius: 999,
            background: entityHsl(c.entityType, 0.1),
            color: entityHsl(c.entityType),
            fontFamily:'var(--f-display)', fontSize: compact ? 10 : 11, fontWeight: 700,
            lineHeight: 1.2,
          }}>
            <span aria-hidden="true" style={{ fontSize: compact ? 10 : 11 }}>{ec.em}</span>
            <span style={{ fontFamily:'var(--f-mono)', fontVariantNumeric:'tabular-nums' }}>{c.count}</span>
          </span>
        );
      })}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── MEEPLECARD (variante `grid` per game) ──────────
// ═══════════════════════════════════════════════════════
// /* v2: MeepleCard.grid — ✅ in produzione */
const GameCardGrid = ({ game, compact }) => {
  const connections = [
    { entityType:'agent', count: game.agentCount, isEmpty: game.agentCount === 0 },
    { entityType:'kb', count: game.kbCount, isEmpty: game.kbCount === 0 },
    { entityType:'session', count: game.sessionCount, isEmpty: game.sessionCount === 0 },
    { entityType:'chat', count: game.chatCount, isEmpty: game.chatCount === 0 },
  ];
  return (
    <article tabIndex={0}
      className="mai-card-game"
      style={{
        background:'var(--bg-card)',
        border:'1px solid var(--border)',
        borderRadius:'var(--r-lg)',
        overflow:'hidden',
        cursor:'pointer',
        transition:'transform var(--dur-sm) var(--ease-out), border-color var(--dur-sm), box-shadow var(--dur-sm)',
        display:'flex', flexDirection:'column',
      }}>
      {/* Cover */}
      <div style={{
        position:'relative',
        aspectRatio: compact ? '4 / 3' : '5 / 3',
        background: game.cover,
        display:'flex', alignItems:'center', justifyContent:'center',
        overflow:'hidden',
      }}>
        <span aria-hidden="true" style={{
          fontSize: compact ? 50 : 60,
          filter:'drop-shadow(0 4px 12px rgba(0,0,0,.35))',
        }}>{game.coverEmoji}</span>
        {/* status badge top-left */}
        {game.badge && (
          <span style={{
            position:'absolute', top: 8, left: 8,
            padding:'3px 8px', borderRadius:'var(--r-pill)',
            background:'rgba(0,0,0,.5)', color:'#fff',
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
            textTransform:'uppercase', letterSpacing:'.06em',
            backdropFilter:'blur(4px)',
          }}>{game.badge}</span>
        )}
        {/* entity chip top-right */}
        <span style={{
          position:'absolute', top: 8, right: 8,
          padding:'3px 7px', borderRadius:'var(--r-pill)',
          background: entityHsl('game', 0.95), color:'#fff',
          fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
          textTransform:'uppercase', letterSpacing:'.06em',
          display:'inline-flex', alignItems:'center', gap: 3,
        }}>
          <span aria-hidden="true">🎲</span>Game
        </span>
        {/* gradient overlay bottom */}
        <div style={{
          position:'absolute', inset:'auto 0 0 0', height:'40%',
          background:'linear-gradient(180deg, transparent 0%, rgba(0,0,0,.35) 100%)',
        }}/>
      </div>
      {/* Body */}
      <div style={{
        padding: compact ? 10 : 14,
        display:'flex', flexDirection:'column', gap: compact ? 6 : 8, flex: 1,
      }}>
        <div>
          <h3 style={{
            fontFamily:'var(--f-display)', fontWeight: 800,
            fontSize: compact ? 13 : 15, lineHeight: 1.15,
            color:'var(--text)', margin:'0 0 3px',
            display:'-webkit-box', WebkitLineClamp: 1, WebkitBoxOrient:'vertical', overflow:'hidden',
          }}>{game.title}</h3>
          <div style={{
            display:'flex', alignItems:'center', gap: 5,
            fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
            fontWeight: 600,
          }}>
            <Stars value={game.stars} size={9}/>
            <span>{game.rating}</span>
            <span aria-hidden="true">·</span>
            <span>{game.players}</span>
            <span aria-hidden="true">·</span>
            <span>{game.duration.split('–')[0]}{game.duration.includes('–') ? '+' : ''}</span>
          </div>
        </div>
        {/* Footer ConnectionChipStrip */}
        <div style={{ marginTop:'auto' }}>
          <ConnectionChipStrip connections={connections} compact={compact}/>
        </div>
      </div>
    </article>
  );
};

// ═══════════════════════════════════════════════════════
// ─── LIBRARY HERO ───────────────────────────────────
// ═══════════════════════════════════════════════════════
// /* v2: LibraryHero → apps/web/src/components/ui/v2/library-hero/ */
const LibraryHero = ({ stats, compact }) => (
  <header style={{
    padding: compact ? '20px 16px 14px' : '32px 32px 18px',
    background:`linear-gradient(180deg, ${entityHsl('game', 0.06)} 0%, transparent 100%)`,
    borderBottom:'1px solid var(--border-light)',
  }}>
    <div style={{
      display:'flex', alignItems:'center', gap: 6, marginBottom: 10,
    }}>
      <span style={{
        display:'inline-flex', alignItems:'center', gap: 5,
        padding:'3px 9px', borderRadius:'var(--r-pill)',
        background: entityHsl('game', 0.14),
        color: entityHsl('game'),
        fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
        textTransform:'uppercase', letterSpacing:'.08em',
      }}><span aria-hidden="true">🎲</span>Games · /games</span>
    </div>
    <h1 style={{
      fontFamily:'var(--f-display)', fontWeight: 800,
      fontSize: compact ? 24 : 34, letterSpacing:'-.02em', lineHeight: 1.05,
      color:'var(--text)', margin:'0 0 6px',
    }}>La tua libreria di giochi</h1>
    <p style={{
      color:'var(--text-sec)', fontSize: compact ? 13 : 14, lineHeight: 1.55,
      margin:'0 0 14px', maxWidth: 620,
    }}>
      Esplora, filtra e apri i tuoi giochi. Ogni card mostra le connessioni
      attive: agenti, KB, partite, chat.
    </p>
    {/* Stats inline */}
    <div style={{
      display:'flex', flexWrap:'wrap', gap: compact ? 14 : 22,
      fontFamily:'var(--f-mono)', fontSize: compact ? 11 : 12,
    }}>
      {stats.map(s => (
        <div key={s.label} style={{ display:'flex', flexDirection:'column', gap: 2 }}>
          <span style={{
            color:'var(--text-muted)', fontSize: 9, fontWeight: 700,
            textTransform:'uppercase', letterSpacing:'.08em',
          }}>{s.label}</span>
          <span style={{
            color:'var(--text)', fontSize: compact ? 18 : 22, fontWeight: 800,
            fontFamily:'var(--f-display)', fontVariantNumeric:'tabular-nums',
            lineHeight: 1,
          }}>{s.value}<span style={{ fontSize: 11, color:'var(--text-muted)', fontWeight: 600, marginLeft: 4 }}>{s.unit}</span></span>
        </div>
      ))}
    </div>
  </header>
);

// ═══════════════════════════════════════════════════════
// ─── GAME FILTERS ───────────────────────────────────
// ═══════════════════════════════════════════════════════
// /* v2: GameFilters → apps/web/src/components/ui/v2/game-filters/ */
const STATUS_OPTS = [
  { id:'all', label:'Tutti' },
  { id:'owned', label:'Posseduti' },
  { id:'wishlist', label:'Wishlist' },
  { id:'played', label:'Giocati' },
];
const SORT_OPTS = [
  { id:'last-played', label:'Ultima partita' },
  { id:'rating', label:'Rating' },
  { id:'title', label:'Titolo A-Z' },
  { id:'year', label:'Anno' },
];

const GameFilters = ({
  q, setQ, status, setStatus, sort, setSort,
  view, setView, compact, count,
}) => (
  <div style={{
    padding: compact ? '12px 16px' : '14px 32px',
    display:'flex', flexDirection: compact ? 'column' : 'row',
    alignItems: compact ? 'stretch' : 'center',
    gap: compact ? 10 : 12,
    borderBottom:'1px solid var(--border-light)',
    background:'var(--bg)',
    position:'sticky', top: 0, zIndex: 10,
    backdropFilter:'blur(12px)',
  }}>
    {/* Search */}
    <label style={{
      display:'flex', alignItems:'center', gap: 8,
      flex: compact ? 'none' : '1 1 280px', maxWidth: compact ? 'none' : 360,
      padding:'8px 12px',
      background:'var(--bg-card)',
      border:'1px solid var(--border)',
      borderRadius:'var(--r-md)',
    }}>
      <span aria-hidden="true" style={{ color:'var(--text-muted)' }}>🔍</span>
      <input
        type="text" value={q} onChange={e=>setQ(e.target.value)}
        placeholder="Cerca per titolo, autore, anno…"
        style={{
          flex: 1, border:'none', outline:'none', background:'transparent',
          color:'var(--text)', fontFamily:'var(--f-body)', fontSize: 13,
          minWidth: 0,
        }}
      />
      {q && (
        <button type="button" onClick={()=>setQ('')} aria-label="Pulisci"
          style={{
            border:'none', background:'transparent', color:'var(--text-muted)',
            cursor:'pointer', fontSize: 13, padding: 0,
          }}>✕</button>
      )}
    </label>

    {/* Status segmented */}
    <div role="tablist" style={{
      display:'inline-flex',
      background:'var(--bg-muted)',
      borderRadius:'var(--r-md)',
      padding: 2,
      flexShrink: 0,
    }}>
      {STATUS_OPTS.map(o => (
        <button key={o.id} role="tab" aria-selected={status === o.id}
          onClick={()=>setStatus(o.id)}
          style={{
            padding: compact ? '6px 9px' : '6px 12px',
            border:'none',
            background: status === o.id ? 'var(--bg-card)' : 'transparent',
            color: status === o.id ? entityHsl('game') : 'var(--text-sec)',
            fontFamily:'var(--f-display)',
            fontSize: compact ? 11 : 12, fontWeight: 700,
            borderRadius: 'var(--r-sm)',
            cursor:'pointer', whiteSpace:'nowrap',
            boxShadow: status === o.id ? 'var(--shadow-xs)' : 'none',
            transition:'all var(--dur-sm) var(--ease-out)',
          }}>{o.label}</button>
      ))}
    </div>

    <div style={{ flex: 1 }}/>

    {/* Sort */}
    <label style={{
      display:'inline-flex', alignItems:'center', gap: 6,
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      fontWeight: 600,
    }}>
      <span style={{ textTransform:'uppercase', letterSpacing:'.06em' }}>Ordina</span>
      <select value={sort} onChange={e=>setSort(e.target.value)}
        style={{
          padding:'6px 10px', borderRadius:'var(--r-sm)',
          border:'1px solid var(--border)', background:'var(--bg-card)',
          color:'var(--text)', fontFamily:'var(--f-display)',
          fontSize: 12, fontWeight: 700, cursor:'pointer',
        }}>
        {SORT_OPTS.map(o => <option key={o.id} value={o.id}>{o.label}</option>)}
      </select>
    </label>

    {/* View toggle (only desktop) */}
    {!compact && (
      <div role="group" aria-label="Vista" style={{
        display:'inline-flex',
        background:'var(--bg-muted)', borderRadius:'var(--r-sm)', padding: 2,
      }}>
        {[
          { id:'grid', icon:'▦' },
          { id:'list', icon:'☰' },
        ].map(v => (
          <button key={v.id} aria-pressed={view === v.id}
            onClick={()=>setView(v.id)}
            aria-label={v.id}
            style={{
              padding:'6px 10px',
              background: view === v.id ? 'var(--bg-card)' : 'transparent',
              border:'none', color: view === v.id ? entityHsl('game') : 'var(--text-muted)',
              fontSize: 14, cursor:'pointer',
              borderRadius: 'var(--r-xs)',
              boxShadow: view === v.id ? 'var(--shadow-xs)' : 'none',
            }}>{v.icon}</button>
        ))}
      </div>
    )}

    {/* Count chip (compact only) */}
    {compact && (
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
        fontWeight: 700, letterSpacing:'.04em',
      }}>{count} giochi</div>
    )}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── EMPTY STATES ───────────────────────────────────
// /* v2: EmptyLibrary → apps/web/src/components/ui/v2/empty-library/ */
const EmptyLibrary = ({ kind, onAction, compact }) => {
  const cfg = {
    library: {
      icon:'🎲',
      t:'La tua libreria è vuota',
      sub:'Aggiungi il primo gioco per iniziare a tracciare partite, caricare regolamenti e creare agenti AI esperti.',
      cta:'Aggiungi un gioco',
    },
    filtered: {
      icon:'🔎',
      t:'Nessun risultato',
      sub:'Nessun gioco corrisponde ai filtri attuali. Prova a modificare la ricerca o azzera i filtri.',
      cta:'Azzera filtri',
    },
  }[kind];
  return (
    <div style={{
      gridColumn:'1 / -1',
      display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center',
      padding: compact ? '40px 20px' : '64px 32px',
      background:'var(--bg-card)',
      border:'1px dashed var(--border-strong)',
      borderRadius:'var(--r-xl)',
    }}>
      <div style={{
        width: compact ? 64 : 80, height: compact ? 64 : 80,
        borderRadius:'50%',
        background: entityHsl('game', 0.12), color: entityHsl('game'),
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: compact ? 32 : 40, marginBottom: 16,
      }} aria-hidden="true">{cfg.icon}</div>
      <h3 style={{
        fontFamily:'var(--f-display)', fontSize: compact ? 16 : 19, fontWeight: 800,
        color:'var(--text)', margin:'0 0 6px',
      }}>{cfg.t}</h3>
      <p style={{
        fontSize: 13, color:'var(--text-muted)', maxWidth: 360,
        margin:'0 0 18px', lineHeight: 1.55,
      }}>{cfg.sub}</p>
      <button type="button" onClick={onAction}
        style={{
          padding:'10px 18px', borderRadius:'var(--r-md)',
          background: entityHsl('game'), color:'#fff',
          border:'none', fontFamily:'var(--f-display)',
          fontSize: 13, fontWeight: 800, cursor:'pointer',
          boxShadow:`0 4px 14px ${entityHsl('game', 0.35)}`,
        }}>+ {cfg.cta}</button>
    </div>
  );
};

// ─── ERROR STATE ─────────────────────────────────────
const ErrorState = ({ onRetry, compact }) => (
  <div style={{
    gridColumn:'1 / -1',
    padding: compact ? '32px 20px' : '48px 32px',
    background:'hsl(var(--c-danger) / .08)',
    border:'1px solid hsl(var(--c-danger) / .3)',
    borderRadius:'var(--r-xl)',
    display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center',
  }}>
    <div style={{
      width: 56, height: 56, borderRadius:'50%',
      background:'hsl(var(--c-danger) / .15)', color:'hsl(var(--c-danger))',
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 26, marginBottom: 12,
    }} aria-hidden="true">⚠</div>
    <h3 style={{
      fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
      color:'var(--text)', margin:'0 0 4px',
    }}>Impossibile caricare la libreria</h3>
    <p style={{
      fontSize: 12.5, color:'var(--text-muted)', maxWidth: 360,
      margin:'0 0 16px',
    }}>Si è verificato un errore di rete. Verifica la connessione e riprova.</p>
    <button type="button" onClick={onRetry}
      style={{
        padding:'8px 16px', borderRadius:'var(--r-md)',
        background:'hsl(var(--c-danger))', color:'#fff',
        border:'none', fontFamily:'var(--f-display)',
        fontSize: 12, fontWeight: 800, cursor:'pointer',
      }}>↻ Riprova</button>
  </div>
);

// ─── SKELETON ────────────────────────────────────────
const SkeletonCard = ({ compact }) => (
  <div style={{
    background:'var(--bg-card)',
    border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)',
    overflow:'hidden',
  }}>
    <div className="mai-shimmer" style={{
      aspectRatio: compact ? '4 / 3' : '5 / 3',
      background:'var(--bg-muted)',
    }}/>
    <div style={{ padding: compact ? 10 : 14, display:'flex', flexDirection:'column', gap: 6 }}>
      <div className="mai-shimmer" style={{ height: 14, width:'72%', borderRadius: 4, background:'var(--bg-muted)' }}/>
      <div className="mai-shimmer" style={{ height: 10, width:'52%', borderRadius: 4, background:'var(--bg-muted)' }}/>
      <div style={{ display:'flex', gap: 4, marginTop: 4 }}>
        {[0,1,2].map(i => (
          <div key={i} className="mai-shimmer" style={{ height: 16, width: 28 + i*4, borderRadius: 999, background:'var(--bg-muted)' }}/>
        ))}
      </div>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── PAGE BODY ──────────────────────────────────────
// ═══════════════════════════════════════════════════════
const filterAndSort = (list, q, status, sort) => {
  let out = list;
  if (q) {
    const needle = q.toLowerCase();
    out = out.filter(g =>
      g.title.toLowerCase().includes(needle) ||
      (g.author || '').toLowerCase().includes(needle) ||
      String(g.year).includes(needle)
    );
  }
  if (status !== 'all') {
    if (status === 'played') out = out.filter(g => (g.totalPlays || 0) > 0);
    else out = out.filter(g => g.status === status);
  }
  out = [...out].sort((a, b) => {
    if (sort === 'rating') return b.rating - a.rating;
    if (sort === 'title') return a.title.localeCompare(b.title);
    if (sort === 'year') return b.year - a.year;
    return (b.totalPlays || 0) - (a.totalPlays || 0); // last-played proxy
  });
  return out;
};

const GamesIndexBody = ({ stateOverride, compact }) => {
  const [q, setQ] = useState('');
  const [status, setStatus] = useState('all');
  const [sort, setSort] = useState('last-played');
  const [view, setView] = useState('grid');

  // Force filter scenarios
  useEffect(() => {
    if (stateOverride === 'filtered-empty') {
      setQ('xyznotfound');
    } else {
      setQ('');
      setStatus('all');
    }
  }, [stateOverride]);

  const isLoading = stateOverride === 'loading';
  const isEmpty = stateOverride === 'empty';
  const isError = stateOverride === 'error';
  const isFilteredEmpty = stateOverride === 'filtered-empty';

  const sourceList = isEmpty ? [] : ALL_GAMES;
  const filtered = useMemo(() => filterAndSort(sourceList, q, status, sort),
    [sourceList, q, status, sort]);

  const stats = [
    { label:'Giochi', value: ALL_GAMES.length, unit:'' },
    { label:'Partite', value: ALL_GAMES.reduce((s,g) => s + (g.totalPlays || 0), 0), unit:'' },
    { label:'Agenti', value: ALL_GAMES.reduce((s,g) => s + g.agentCount, 0), unit:'' },
    { label:'KB Docs', value: ALL_GAMES.reduce((s,g) => s + g.kbCount, 0), unit:'' },
  ];

  return (
    <>
      <LibraryHero stats={stats} compact={compact}/>
      {!isError && !isEmpty && (
        <GameFilters
          q={q} setQ={setQ}
          status={status} setStatus={setStatus}
          sort={sort} setSort={setSort}
          view={view} setView={setView}
          compact={compact}
          count={filtered.length}
        />
      )}

      <div style={{
        padding: compact ? '14px 16px 24px' : '24px 32px 64px',
      }}>
        {isError && <ErrorState compact={compact}/>}

        {isEmpty && <EmptyLibrary kind="library" compact={compact}/>}

        {!isError && !isEmpty && isLoading && (
          <div style={{
            display:'grid',
            gridTemplateColumns: compact ? 'repeat(2, 1fr)' : 'repeat(4, 1fr)',
            gap: compact ? 10 : 16,
          }}>
            {Array.from({ length: compact ? 6 : 12 }).map((_, i) => (
              <SkeletonCard key={i} compact={compact}/>
            ))}
          </div>
        )}

        {!isError && !isEmpty && !isLoading && (
          filtered.length === 0 ? (
            <EmptyLibrary kind="filtered" compact={compact}
              onAction={() => { setQ(''); setStatus('all'); }}/>
          ) : view === 'grid' || compact ? (
            <div style={{
              display:'grid',
              gridTemplateColumns: compact ? 'repeat(2, 1fr)' : 'repeat(4, 1fr)',
              gap: compact ? 10 : 16,
            }}>
              {filtered.map(g => <GameCardGrid key={g.id} game={g} compact={compact}/>)}
            </div>
          ) : (
            // Desktop list view
            <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
              {filtered.map(g => <GameRow key={g.id} game={g}/>)}
            </div>
          )
        )}
      </div>
    </>
  );
};

// ─── GAME ROW (list view, desktop) ──────────────────
const GameRow = ({ game }) => {
  const connections = [
    { entityType:'agent', count: game.agentCount, isEmpty: game.agentCount === 0 },
    { entityType:'kb', count: game.kbCount, isEmpty: game.kbCount === 0 },
    { entityType:'session', count: game.sessionCount, isEmpty: game.sessionCount === 0 },
    { entityType:'chat', count: game.chatCount, isEmpty: game.chatCount === 0 },
  ];
  return (
    <article tabIndex={0} className="mai-card-game" style={{
      display:'grid', gridTemplateColumns:'80px 2fr 1fr 1.2fr',
      gap: 14, alignItems:'center',
      padding: 12,
      background:'var(--bg-card)',
      border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)',
      cursor:'pointer',
    }}>
      <div style={{
        width: 80, height: 60,
        background: game.cover, borderRadius:'var(--r-md)',
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 30,
      }}>{game.coverEmoji}</div>
      <div>
        <div style={{
          fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 15,
          color:'var(--text)', marginBottom: 3,
        }}>{game.title}</div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
          fontWeight: 600,
        }}>
          {game.author} · {game.year} · {game.players} pl · {game.duration}
        </div>
      </div>
      <div style={{ display:'flex', alignItems:'center', gap: 8 }}>
        <Stars value={game.stars}/>
        <span style={{
          fontFamily:'var(--f-mono)', fontSize: 12, fontWeight: 700, color:'var(--text-sec)',
        }}>{game.rating}</span>
        <span style={{
          padding:'2px 8px', borderRadius:'var(--r-pill)',
          background: 'var(--bg-muted)',
          color:'var(--text-muted)',
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
        }}>{game.totalPlays || 0} partite</span>
      </div>
      <ConnectionChipStrip connections={connections}/>
    </article>
  );
};

// ═══════════════════════════════════════════════════════
// ─── DESKTOP / MOBILE SHELLS ─────────────────────────
// ═══════════════════════════════════════════════════════
const DesktopNav = ({ active = 'games' }) => {
  const items = [
    { id:'home', label:'Home' },
    { id:'library', label:'Libreria' },
    { id:'games', label:'Giochi' },
    { id:'agents', label:'Agenti' },
    { id:'sessions', label:'Sessioni' },
    { id:'toolkit', label:'Toolkit' },
  ];
  return (
    <div style={{
      display:'flex', alignItems:'center', gap: 14,
      padding:'10px 32px',
      background:'var(--glass-bg)', backdropFilter:'blur(12px)',
      borderBottom:'1px solid var(--border)',
    }}>
      <div style={{ display:'flex', alignItems:'center', gap: 9 }}>
        <div style={{
          width: 26, height: 26, borderRadius: 7,
          background:'linear-gradient(135deg, hsl(var(--c-game)), hsl(var(--c-event)))',
          color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
          fontWeight: 800, fontSize: 13, fontFamily:'var(--f-display)',
        }}>M</div>
        <span style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 14 }}>MeepleAI</span>
      </div>
      <div style={{ display:'flex', alignItems:'center', gap: 2, marginLeft: 18 }}>
        {items.map(it => (
          <a key={it.id} href="#" onClick={e=>e.preventDefault()}
            aria-current={it.id === active ? 'page' : undefined}
            style={{
              padding:'6px 12px', borderRadius:'var(--r-md)',
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700,
              color: it.id === active ? entityHsl('game') : 'var(--text-sec)',
              background: it.id === active ? entityHsl('game', 0.1) : 'transparent',
              textDecoration:'none',
            }}>{it.label}</a>
        ))}
      </div>
      <div style={{ flex: 1 }}/>
      <button type="button" style={{
        padding:'7px 14px', borderRadius:'var(--r-md)',
        background: entityHsl('game'), color:'#fff',
        border:'none', fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
        cursor:'pointer', boxShadow:`0 3px 10px ${entityHsl('game', 0.35)}`,
      }}>+ Nuovo gioco</button>
    </div>
  );
};

const DesktopScreen = ({ stateOverride }) => (
  <div style={{ minHeight: 720, background:'var(--bg)' }}>
    <DesktopNav/>
    <GamesIndexBody stateOverride={stateOverride}/>
  </div>
);

const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}>
    <span style={{ fontFamily:'var(--f-mono)' }}>14:32</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);
const PhoneTopNav = () => (
  <div style={{
    display:'flex', alignItems:'center', gap: 9,
    padding:'10px 14px', borderBottom:'1px solid var(--border)',
    background:'var(--bg)',
  }}>
    <button aria-label="Indietro" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'transparent', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 14, cursor:'pointer',
    }}>←</button>
    <div style={{ flex: 1, fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 700, textAlign:'center' }}>
      Giochi
    </div>
    <button aria-label="Aggiungi" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background: entityHsl('game'), border:'none',
      color:'#fff', fontSize: 16, cursor:'pointer', fontWeight: 800,
    }}>＋</button>
  </div>
);
const MobileBottomBar = () => {
  const tabs = [
    { id:'home', icon:'⌂', label:'Home' },
    { id:'search', icon:'🔍', label:'Cerca' },
    { id:'library', icon:'📚', label:'Libreria', active: true },
    { id:'chat', icon:'💬', label:'Chat' },
    { id:'profile', icon:'👤', label:'Profilo' },
  ];
  return (
    <div style={{
      position:'absolute', bottom: 0, left: 0, right: 0,
      display:'flex',
      padding:'8px 10px 12px',
      background:'var(--glass-bg)', backdropFilter:'blur(14px)',
      borderTop:'1px solid var(--border)',
      zIndex: 5,
    }}>
      {tabs.map(t => (
        <button key={t.id} style={{
          flex: 1,
          display:'flex', flexDirection:'column', alignItems:'center', gap: 2,
          padding:'4px 0',
          background:'transparent', border:'none',
          color: t.active ? entityHsl('game') : 'var(--text-muted)',
          fontFamily:'var(--f-display)', fontSize: 9, fontWeight: 700,
          cursor:'pointer',
        }}>
          <span aria-hidden="true" style={{ fontSize: 16 }}>{t.icon}</span>
          <span>{t.label}</span>
        </button>
      ))}
    </div>
  );
};
const MobileScreen = ({ stateOverride }) => (
  <>
    <PhoneSbar/>
    <div style={{ flex: 1, overflowY:'auto', position:'relative', background:'var(--bg)' }}>
      <PhoneTopNav/>
      <div style={{ paddingBottom: 70 }}>
        <GamesIndexBody stateOverride={stateOverride} compact/>
      </div>
      <MobileBottomBar/>
    </div>
  </>
);

const PhoneShell = ({ label, desc, children }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 10 }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}</div>
    <div className="phone">{children}</div>
    {desc && <div style={{
      fontSize: 11, color:'var(--text-muted)', maxWidth: 340,
      textAlign:'center', lineHeight: 1.55,
    }}>{desc}</div>}
  </div>
);

const DesktopFrame = ({ label, desc, children }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 12, width:'100%' }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}</div>
    <div style={{
      width:'100%', maxWidth: 1280,
      borderRadius:'var(--r-xl)', border:'1px solid var(--border)',
      background:'var(--bg)', overflow:'hidden',
      boxShadow:'var(--shadow-lg)',
    }}>
      <div style={{
        display:'flex', alignItems:'center', gap: 8,
        padding:'9px 14px',
        background:'var(--bg-muted)',
        borderBottom:'1px solid var(--border)',
        fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      }}>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-event))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-warning))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-toolkit))' }}/>
        <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/games</span>
      </div>
      {children}
    </div>
    {desc && <div style={{
      fontSize: 11, color:'var(--text-muted)', maxWidth: 720,
      textAlign:'center', lineHeight: 1.55,
    }}>{desc}</div>}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── ROOT ────────────────────────────────────────────
const MOBILE_STATES = [
  { id:'default', label:'01 · Default', desc:'24+ giochi, grid 2-col compact, ConnectionChipStrip nei footer card.' },
  { id:'empty', label:'02 · Empty library', desc:'Illustrazione 🎲 + CTA "Aggiungi un gioco" entity-color.' },
  { id:'loading', label:'03 · Loading', desc:'6 skeleton cards shimmer (cover gray + 2 line + 3 chip pills).' },
  { id:'filtered-empty', label:'04 · Filtered empty', desc:'Query "xyznotfound" → empty state filtrato + "Azzera filtri".' },
  { id:'error', label:'05 · Error', desc:'Banner danger inline + retry CTA. Hero/filtri restano visibili.' },
];

function ThemeToggle() {
  const [dark, setDark] = useState(false);
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
  }, [dark]);
  return (
    <button onClick={() => setDark(d => !d)} className="theme-toggle"
      aria-label={dark ? 'Tema chiaro' : 'Tema scuro'}>
      <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span>
      <span>{dark ? 'Dark' : 'Light'}</span>
    </button>
  );
}

function App() {
  return (
    <div className="stage">
      <ThemeToggle/>
      <div className="stage-wrap">
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
          textTransform:'uppercase', letterSpacing:'.08em', marginBottom: 8,
        }}>SP4 wave 1 · #1/5 · Hero + body grid</div>
        <h1>Games index — /games</h1>
        <p className="lead">
          Hero compatto + filtri sticky + grid 4-col MeepleCard variante <strong>grid</strong>.
          Footer card: <strong>ConnectionChipStrip</strong> (PR #549/#552, riuso pixel-perfect).
          5 stati mobile (default / empty / loading / filtered-empty / error) e 4 desktop.
        </p>

        <div className="section-label">Mobile · 375 — 5 stati</div>
        <div className="phones-grid">
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc}>
              <MobileScreen stateOverride={s.id}/>
            </PhoneShell>
          ))}
        </div>

        <div className="section-label">Desktop · 1440 — 4 stati</div>
        <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
          <DesktopFrame label="06 · Desktop · Default"
            desc="Hero stats inline, filtri sticky, grid 4-col. 24 giochi reali (Catan, Wingspan, Brass…) con ConnectionChipStrip per agent/kb/session/chat in ogni footer.">
            <DesktopScreen stateOverride="default"/>
          </DesktopFrame>
          <DesktopFrame label="07 · Desktop · Empty library"
            desc="Stato 'first-run': nessun gioco. Filtri nascosti. Empty state full-width entity-color con CTA primario.">
            <DesktopScreen stateOverride="empty"/>
          </DesktopFrame>
          <DesktopFrame label="08 · Desktop · Loading"
            desc="12 skeleton cards (4-col × 3 row) con shimmer. Hero e filtri visibili e interattivi (skeleton-aware).">
            <DesktopScreen stateOverride="loading"/>
          </DesktopFrame>
          <DesktopFrame label="09 · Desktop · Filtered empty"
            desc="Query 'xyznotfound' → empty filtrato (icona 🔎) con CTA 'Azzera filtri'. Hero e filtri restano. Sort/view ancora interagibili.">
            <DesktopScreen stateOverride="filtered-empty"/>
          </DesktopFrame>
        </div>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim {
          0%   { background-position: -200% 0; }
          100% { background-position:  200% 0; }
        }
        .mai-shimmer {
          background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important;
          background-size: 200% 100% !important;
          animation: mai-shimmer-anim 1.4s linear infinite;
        }
        .phones-grid {
          display: grid;
          grid-template-columns: repeat(auto-fill, minmax(380px, 1fr));
          gap: var(--s-7);
          align-items: start;
        }
        .stage h1 { font-size: var(--fs-3xl); }
        .stage .lead { line-height: var(--lh-relax); }
        .phone > div::-webkit-scrollbar { display: none; }
        .mai-card-game {
          outline: none;
        }
        .mai-card-game:hover {
          transform: translateY(-2px);
          border-color: hsl(var(--c-game) / .4) !important;
          box-shadow: var(--shadow-md);
        }
        .mai-card-game:focus-visible {
          outline: 2px solid hsl(var(--c-game));
          outline-offset: 2px;
        }
        select:focus-visible, input:focus-visible, button:focus-visible, a:focus-visible {
          outline: 2px solid hsl(var(--c-game));
          outline-offset: 2px;
        }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);

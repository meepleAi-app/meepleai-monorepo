/* MeepleAI SP3 — Shared Games (public catalog index)
   Route target: /shared-games
   Vetrina pubblica del SharedGameCatalog. Hero leggero + filter bar sticky +
   grid MeepleCard entity=game variant=grid. Sidebar desktop "Top contributors".
   4 stati: default (24+ giochi) · loading (skeleton 3×3) · empty-search ·
   filtered-empty (con CTA reset filtri).

   Componenti v2 nuovi:
   - SharedGamesFilters (search + chip filters + sort)
   - ContributorsSidebar (desktop only)
   Riusa: Banner (#1), MeepleCard primitive (qui implementata in variante "grid"
   coerente col pattern esistente: cover gradient, rating, badge "N tk · M ag").

   Caricato da sp3-shared-games.html via Babel standalone. */

const { useState, useEffect, useMemo, useRef } = React;
const DS = window.DS;

// ─── Estendiamo il dataset DS con metadata public-catalog ─────────
// (toolkit/agent counts derivati o mockati per i giochi che non li hanno;
//  i conteggi sono coerenti con DS.toolkits / DS.agents / DS.kbs)
const buildPublicGames = () => {
  const tkByGame = {};   DS.toolkits.forEach(t => { if (t.gameId) tkByGame[t.gameId] = (tkByGame[t.gameId]||0)+1; });
  const agByGame = {};   DS.agents.forEach(a => { if (a.gameId) agByGame[a.gameId] = (agByGame[a.gameId]||0)+1; });
  const kbByGame = {};   DS.kbs.forEach(k => { if (k.gameId) kbByGame[k.gameId] = (kbByGame[k.gameId]||0)+1; });

  // Metadata extra per filtro (genere + complexity bucket)
  const extra = {
    'g-azul':       { genre:'Astratto',   complexity:'leggero',  newWeek: 4 },
    'g-catan':      { genre:'Famiglia',   complexity:'medio',    newWeek: 0 },
    'g-wingspan':   { genre:'Engine',     complexity:'medio',    newWeek: 2 },
    'g-brass':      { genre:'Economico',  complexity:'pesante',  newWeek: 1 },
    'g-gloomhaven': { genre:'Avventura',  complexity:'pesante',  newWeek: 0 },
    'g-arknova':    { genre:'Engine',     complexity:'pesante',  newWeek: 5 },
    'g-spirit':     { genre:'Coop',       complexity:'pesante',  newWeek: 0 },
    'g-7wonders':   { genre:'Drafting',   complexity:'leggero',  newWeek: 3 },
  };

  return DS.games.map(g => ({
    ...g,
    tkCount: tkByGame[g.id] || 0,
    agCount: agByGame[g.id] || 0,
    kbCount: kbByGame[g.id] || 0,
    contribCount: (tkByGame[g.id]||0) * 2 + (agByGame[g.id]||0),
    ...(extra[g.id] || { genre:'Misto', complexity:'medio', newWeek: 0 }),
  }));
};

// Aggiungiamo qualche gioco extra (italiani realistici) per arrivare a 24+
const EXTRA_GAMES = [
  { id:'g-carcassonne', title:'Carcassonne', publisher:'Hans im Glück', year:2000, author:'Klaus-Jürgen Wrede',
    players:'2–5', duration:'30–45m', weight:1.92, rating:7.4, stars:4, coverEmoji:'🏰',
    cover: DS.grad(45, 60), genre:'Famiglia', complexity:'leggero',
    tkCount:3, agCount:2, kbCount:2, newWeek:1, contribCount:8 },
  { id:'g-ticket', title:'Ticket to Ride Europe', publisher:'Days of Wonder', year:2005, author:'Alan R. Moon',
    players:'2–5', duration:'30–60m', weight:1.92, rating:7.5, stars:4, coverEmoji:'🚂',
    cover: DS.grad(0, 70), genre:'Famiglia', complexity:'leggero',
    tkCount:2, agCount:1, kbCount:1, newWeek:0, contribCount:5 },
  { id:'g-everdell', title:'Everdell', publisher:'Starling', year:2018, author:'James A. Wilson',
    players:'1–4', duration:'40–80m', weight:2.81, rating:8.2, stars:5, coverEmoji:'🌲',
    cover: DS.grad(110, 50), genre:'Engine', complexity:'medio',
    tkCount:4, agCount:2, kbCount:3, newWeek:2, contribCount:10 },
  { id:'g-terra', title:'Terraforming Mars', publisher:'FryxGames', year:2016, author:'Jacob Fryxelius',
    players:'1–5', duration:'120m', weight:3.24, rating:8.4, stars:5, coverEmoji:'🪐',
    cover: DS.grad(15, 65), genre:'Engine', complexity:'pesante',
    tkCount:5, agCount:3, kbCount:4, newWeek:6, contribCount:13 },
  { id:'g-scythe', title:'Scythe', publisher:'Stonemaier', year:2016, author:'Jamey Stegmaier',
    players:'1–5', duration:'90–115m', weight:3.43, rating:8.2, stars:5, coverEmoji:'⚙️',
    cover: DS.grad(180, 40), genre:'Strategico', complexity:'pesante',
    tkCount:3, agCount:2, kbCount:2, newWeek:0, contribCount:8 },
  { id:'g-root', title:'Root', publisher:'Leder Games', year:2018, author:'Cole Wehrle',
    players:'2–4', duration:'60–90m', weight:3.79, rating:8.1, stars:5, coverEmoji:'🦊',
    cover: DS.grad(140, 55), genre:'Asimmetrico', complexity:'pesante',
    tkCount:2, agCount:2, kbCount:2, newWeek:1, contribCount:6 },
  { id:'g-nemesis', title:'Nemesis', publisher:'Awaken Realms', year:2018, author:'Adam Kwapiński',
    players:'1–5', duration:'90–180m', weight:3.69, rating:7.9, stars:4, coverEmoji:'👽',
    cover: DS.grad(280, 45), genre:'Avventura', complexity:'pesante',
    tkCount:1, agCount:1, kbCount:2, newWeek:0, contribCount:3 },
  { id:'g-cascadia', title:'Cascadia', publisher:'Flatout', year:2021, author:'Randy Flynn',
    players:'1–4', duration:'30–45m', weight:1.84, rating:7.8, stars:4, coverEmoji:'🦌',
    cover: DS.grad(95, 50), genre:'Astratto', complexity:'leggero',
    tkCount:3, agCount:1, kbCount:1, newWeek:4, contribCount:7 },
  { id:'g-vinhos', title:'Viticulture EE', publisher:'Stonemaier', year:2015, author:'Jamey Stegmaier',
    players:'1–6', duration:'45–90m', weight:2.94, rating:8.2, stars:5, coverEmoji:'🍇',
    cover: DS.grad(330, 50), genre:'Worker', complexity:'medio',
    tkCount:2, agCount:1, kbCount:2, newWeek:0, contribCount:5 },
  { id:'g-cosmic', title:'Cosmic Encounter', publisher:'FFG', year:2008, author:'Eberle · Olotka',
    players:'3–5', duration:'60–120m', weight:2.59, rating:7.5, stars:4, coverEmoji:'🛸',
    cover: DS.grad(260, 60), genre:'Asimmetrico', complexity:'medio',
    tkCount:1, agCount:1, kbCount:1, newWeek:0, contribCount:3 },
  { id:'g-pandemic', title:'Pandemic Legacy S1', publisher:'Z-Man', year:2015, author:'Daviau · Leacock',
    players:'2–4', duration:'60m', weight:2.83, rating:8.5, stars:5, coverEmoji:'🦠',
    cover: DS.grad(160, 50), genre:'Coop', complexity:'medio',
    tkCount:4, agCount:2, kbCount:3, newWeek:2, contribCount:11 },
  { id:'g-orleans', title:'Orléans', publisher:'dlp Games', year:2014, author:'Reiner Stockhausen',
    players:'2–4', duration:'90m', weight:3.07, rating:7.9, stars:4, coverEmoji:'🏰',
    cover: DS.grad(50, 55), genre:'Bag-build', complexity:'medio',
    tkCount:2, agCount:1, kbCount:1, newWeek:0, contribCount:4 },
  { id:'g-pax', title:'Pax Pamir 2nd Ed.', publisher:'Wehrlegig', year:2019, author:'Cole Wehrle',
    players:'1–5', duration:'45–120m', weight:3.49, rating:8.1, stars:5, coverEmoji:'🐪',
    cover: DS.grad(35, 60), genre:'Politico', complexity:'pesante',
    tkCount:1, agCount:1, kbCount:1, newWeek:1, contribCount:3 },
  { id:'g-kingdomino', title:'Kingdomino', publisher:'Blue Orange', year:2016, author:'Bruno Cathala',
    players:'2–4', duration:'15–25m', weight:1.21, rating:7.4, stars:4, coverEmoji:'👑',
    cover: DS.grad(75, 65), genre:'Famiglia', complexity:'leggero',
    tkCount:2, agCount:1, kbCount:1, newWeek:3, contribCount:5 },
  { id:'g-sherlock', title:'Sherlock Holmes Consulting', publisher:'Space Cowboys', year:2017, author:'AA.VV.',
    players:'1–8', duration:'60–120m', weight:3.05, rating:7.7, stars:4, coverEmoji:'🔍',
    cover: DS.grad(20, 35), genre:'Investigativo', complexity:'medio',
    tkCount:1, agCount:1, kbCount:2, newWeek:0, contribCount:3 },
  { id:'g-blood', title:'Blood Rage', publisher:'CMON', year:2015, author:'Eric M. Lang',
    players:'2–4', duration:'60–90m', weight:2.91, rating:7.9, stars:4, coverEmoji:'⚔️',
    cover: DS.grad(355, 60), genre:'Strategico', complexity:'medio',
    tkCount:2, agCount:1, kbCount:1, newWeek:0, contribCount:4 },
];

const ALL_GAMES = [
  ...buildPublicGames(),
  ...EXTRA_GAMES,
].sort((a, b) => (b.rating || 0) - (a.rating || 0));

// Filter chips definition
const FILTER_CHIPS = [
  { id:'with-toolkit', label:'Con toolkit',   icon:'🧰', e:'toolkit', test: g => g.tkCount > 0 },
  { id:'with-agent',   label:'Con agente',    icon:'🤖', e:'agent',   test: g => g.agCount > 0 },
  { id:'top-rated',    label:'Top rated',     icon:'⭐', e:'game',    test: g => (g.rating || 0) >= 8 },
  { id:'new',          label:'Nuovi',         icon:'✨', e:'event',   test: g => g.newWeek >= 2 },
];

// ─── BANNER (riusato da #1) ───────────────────────────────
const Banner = ({ tone='info', children, action }) => {
  const colors = { success:'c-toolkit', error:'c-danger', warning:'c-warning', info:'c-info' };
  const c = colors[tone];
  const icon = tone === 'success' ? '✓' : tone === 'error' ? '✕' : tone === 'warning' ? '!' : 'ℹ';
  return (
    <div role={tone === 'error' ? 'alert' : 'status'} style={{
      display:'flex', alignItems:'center', gap: 10,
      padding:'10px 12px', borderRadius:'var(--r-md)',
      background:`hsl(var(--${c}) / .1)`,
      border:`1px solid hsl(var(--${c}) / .25)`,
      color:`hsl(var(--${c}))`,
      fontSize: 12, fontWeight: 600,
    }}>
      <span aria-hidden="true" style={{
        fontSize: 13, lineHeight: 1, flexShrink: 0,
        width: 18, height: 18, borderRadius:'50%',
        background:`hsl(var(--${c}) / .18)`,
        display:'inline-flex', alignItems:'center', justifyContent:'center',
      }}>{icon}</span>
      <span style={{ flex: 1, lineHeight: 1.45 }}>{children}</span>
      {action}
    </div>
  );
};

// ─── ENTITY PIP (round, riusabile) ───────────────────────
const EntityPip = ({ type='game', size = 22, label }) => {
  const ec = DS.EC[type] || DS.EC.game;
  return (
    <span aria-label={label} title={label} style={{
      display:'inline-flex', alignItems:'center', justifyContent:'center',
      width: size, height: size, borderRadius:'50%',
      background: `hsl(${ec.h} ${ec.s}% ${ec.l}% / .14)`,
      color: `hsl(${ec.h} ${ec.s}% ${ec.l}%)`,
      fontSize: size * 0.5,
      flexShrink: 0,
    }}>{ec.em}</span>
  );
};

// ─── ENTITY CHIP (count + emoji) ─────────────────────────
const EntityChip = ({ type='game', count, label }) => {
  const ec = DS.EC[type] || DS.EC.game;
  return (
    <span style={{
      display:'inline-flex', alignItems:'center', gap: 4,
      padding:'2px 7px', borderRadius:'var(--r-sm)',
      background:`hsl(${ec.h} ${ec.s}% ${ec.l}% / .12)`,
      color:`hsl(${ec.h} ${ec.s}% ${ec.l}%)`,
      fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
      whiteSpace:'nowrap',
    }}>
      <span aria-hidden="true">{ec.em}</span>
      <span>{count}</span>
      {label && <span style={{ opacity:.7, fontWeight:600 }}>{label}</span>}
    </span>
  );
};

// ─── STAR RATING ─────────────────────────────────────────
const Stars = ({ value = 0 }) => {
  const full = Math.floor(value);
  const half = value - full >= 0.5;
  return (
    <span style={{ display:'inline-flex', gap: 1, color:'hsl(var(--c-agent))', fontSize: 11 }}>
      {[0,1,2,3,4].map(i => (
        <span key={i} aria-hidden="true">
          {i < full ? '★' : (i === full && half ? '⯨' : '☆')}
        </span>
      ))}
    </span>
  );
};

// ─── MEEPLECARD entity=game variant=grid ─────────────────
// Pattern coerente con l'esistente: cover gradient hero, body con
// titolo/meta, footer con rating + entity chips.
const MeepleCardGame = ({ game, compact }) => (
  <article tabIndex={0} aria-label={`${game.title} — gioco condiviso`} style={{
    background:'var(--bg-card)',
    border:'1px solid var(--border)',
    borderRadius:'var(--r-xl)',
    overflow:'hidden',
    display:'flex', flexDirection:'column',
    boxShadow:'var(--shadow-xs)',
    transition:'transform var(--dur-sm) var(--ease-out), box-shadow var(--dur-sm), border-color var(--dur-sm)',
    cursor:'pointer',
  }}
  onMouseEnter={e => { e.currentTarget.style.transform='translateY(-2px)'; e.currentTarget.style.boxShadow='var(--shadow-md)'; e.currentTarget.style.borderColor='hsl(var(--c-game) / .35)'; }}
  onMouseLeave={e => { e.currentTarget.style.transform=''; e.currentTarget.style.boxShadow='var(--shadow-xs)'; e.currentTarget.style.borderColor='var(--border)'; }}
  >
    {/* Cover */}
    <div style={{
      height: compact ? 96 : 116,
      background: game.cover,
      position:'relative',
      display:'flex', alignItems:'center', justifyContent:'center',
    }}>
      <span aria-hidden="true" style={{
        fontSize: compact ? 36 : 44,
        filter:'drop-shadow(0 3px 8px rgba(0,0,0,.25))',
      }}>{game.coverEmoji}</span>
      {game.newWeek >= 2 && (
        <span style={{
          position:'absolute', top: 8, right: 8,
          padding:'3px 7px', borderRadius:'var(--r-pill)',
          background:'hsl(var(--c-event))', color:'#fff',
          fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
          textTransform:'uppercase', letterSpacing:'.08em',
          boxShadow:'0 2px 6px hsl(var(--c-event) / .35)',
        }}>+{game.newWeek}</span>
      )}
    </div>
    {/* Body */}
    <div style={{
      padding: compact ? '10px 12px 11px' : '12px 14px 13px',
      display:'flex', flexDirection:'column', gap: 6, flex: 1,
    }}>
      <div style={{
        display:'flex', alignItems:'flex-start', justifyContent:'space-between', gap: 8,
      }}>
        <h3 style={{
          fontFamily:'var(--f-display)', fontWeight: 700,
          fontSize: compact ? 13 : 14, color:'var(--text)',
          letterSpacing:'-.01em', margin: 0, lineHeight: 1.2,
        }}>{game.title}</h3>
        <div style={{ display:'flex', alignItems:'center', gap: 4, flexShrink:0 }}>
          <Stars value={game.rating / 2}/>
        </div>
      </div>
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
        letterSpacing:'.03em',
      }}>
        {game.year} · {game.players} pl · {game.duration}
      </div>
      <div style={{ display:'flex', flexWrap:'wrap', gap: 4, marginTop: 'auto', paddingTop: 6 }}>
        {game.tkCount > 0 && <EntityChip type="toolkit" count={game.tkCount} label={game.tkCount === 1 ? 'tk' : 'tk'}/>}
        {game.agCount > 0 && <EntityChip type="agent" count={game.agCount} label={game.agCount === 1 ? 'ag' : 'ag'}/>}
        {game.kbCount > 0 && <EntityChip type="kb" count={game.kbCount}/>}
      </div>
    </div>
  </article>
);

// ─── SKELETON CARD ───────────────────────────────────────
const SkeletonCard = ({ delay = 0 }) => (
  <div style={{
    background:'var(--bg-card)',
    border:'1px solid var(--border)',
    borderRadius:'var(--r-xl)',
    overflow:'hidden',
    display:'flex', flexDirection:'column',
  }}>
    <div className="mai-shimmer" style={{ height: 116, background:'var(--bg-muted)' }}/>
    <div style={{ padding:'12px 14px 13px', display:'flex', flexDirection:'column', gap: 7 }}>
      <div className="mai-shimmer" style={{ height: 13, width:'70%', background:'var(--bg-muted)', borderRadius: 4 }}/>
      <div className="mai-shimmer" style={{ height: 10, width:'52%', background:'var(--bg-muted)', borderRadius: 4 }}/>
      <div style={{ display:'flex', gap: 5, marginTop: 4 }}>
        <div className="mai-shimmer" style={{ height: 16, width: 36, background:'var(--bg-muted)', borderRadius: 4 }}/>
        <div className="mai-shimmer" style={{ height: 16, width: 36, background:'var(--bg-muted)', borderRadius: 4 }}/>
      </div>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── SHAREDGAMESFILTERS (NUOVO COMPONENTE V2) ─────────
// ═══════════════════════════════════════════════════════
const SharedGamesFilters = ({
  query, onQuery, activeChips, onToggleChip,
  genre, onGenre, sort, onSort,
  resultCount, totalCount,
  variant = 'desktop',
}) => {
  const isMobile = variant === 'mobile';

  return (
    <div style={{
      position:'sticky', top: isMobile ? 0 : 56, zIndex: 4,
      background:'var(--glass-bg)', backdropFilter:'blur(14px)',
      borderBottom:'1px solid var(--border)',
      padding: isMobile ? '12px 14px 10px' : '14px 24px 12px',
      display:'flex', flexDirection:'column', gap: 10,
    }}>
      {/* Riga 1 — search + sort */}
      <div style={{ display:'flex', alignItems:'center', gap: 8 }}>
        <div style={{ position:'relative', flex: 1 }}>
          <span aria-hidden="true" style={{
            position:'absolute', left: 11, top:'50%', transform:'translateY(-50%)',
            color:'var(--text-muted)', fontSize: 14, pointerEvents:'none',
          }}>🔍</span>
          <input
            type="search"
            value={query}
            onChange={e => onQuery(e.target.value)}
            placeholder="Cerca per titolo, autore, editore…"
            aria-label="Cerca giochi"
            style={{
              width:'100%', padding:'9px 12px 9px 34px',
              borderRadius:'var(--r-md)',
              border:'1.5px solid var(--border)',
              background:'var(--bg-card)',
              color:'var(--text)', fontSize: 13, outline:'none',
              fontFamily:'var(--f-body)', boxSizing:'border-box',
              transition:'border-color var(--dur-sm)',
            }}
            onFocus={e => e.target.style.borderColor='hsl(var(--c-game))'}
            onBlur={e => e.target.style.borderColor='var(--border)'}
          />
        </div>
        {!isMobile && (
          <select
            value={genre} onChange={e => onGenre(e.target.value)}
            aria-label="Filtra per genere"
            style={{
              padding:'9px 12px',
              borderRadius:'var(--r-md)',
              border:'1.5px solid var(--border)',
              background:'var(--bg-card)', color:'var(--text)',
              fontSize: 12, fontFamily:'var(--f-body)', fontWeight: 600,
              cursor:'pointer', outline:'none',
            }}>
            <option value="">Tutti i generi</option>
            {['Astratto','Famiglia','Engine','Economico','Strategico','Coop','Avventura','Drafting','Asimmetrico','Politico','Worker','Bag-build','Investigativo'].map(g => (
              <option key={g} value={g}>{g}</option>
            ))}
          </select>
        )}
        <select
          value={sort} onChange={e => onSort(e.target.value)}
          aria-label="Ordina"
          style={{
            padding:'9px 12px',
            borderRadius:'var(--r-md)',
            border:'1.5px solid var(--border)',
            background:'var(--bg-card)', color:'var(--text)',
            fontSize: 12, fontFamily:'var(--f-body)', fontWeight: 600,
            cursor:'pointer', outline:'none',
          }}>
          <option value="rating">⭐ Top rated</option>
          <option value="contrib">🧰 Più toolkit</option>
          <option value="new">✨ Più recenti</option>
          <option value="title">A–Z</option>
        </select>
      </div>

      {/* Riga 2 — chips */}
      <div style={{ display:'flex', alignItems:'center', gap: 8, flexWrap:'wrap' }}>
        <div style={{
          display:'flex', gap: 6, flexWrap:'wrap', flex: 1,
          overflowX: isMobile ? 'auto' : 'visible',
          paddingBottom: isMobile ? 2 : 0,
          scrollbarWidth:'none',
        }}>
          {FILTER_CHIPS.map(chip => {
            const active = activeChips.includes(chip.id);
            const ec = DS.EC[chip.e] || DS.EC.game;
            return (
              <button
                key={chip.id}
                type="button"
                onClick={() => onToggleChip(chip.id)}
                aria-pressed={active}
                style={{
                  display:'inline-flex', alignItems:'center', gap: 5,
                  padding:'5px 10px', borderRadius:'var(--r-pill)',
                  border: active
                    ? `1.5px solid hsl(${ec.h} ${ec.s}% ${ec.l}%)`
                    : '1px solid var(--border)',
                  background: active
                    ? `hsl(${ec.h} ${ec.s}% ${ec.l}% / .14)`
                    : 'var(--bg-card)',
                  color: active ? `hsl(${ec.h} ${ec.s}% ${ec.l}%)` : 'var(--text-sec)',
                  fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 700,
                  whiteSpace:'nowrap', cursor:'pointer',
                  transition:'all var(--dur-sm) var(--ease-out)',
                }}>
                <span aria-hidden="true">{chip.icon}</span>
                <span>{chip.label}</span>
                {active && <span aria-hidden="true" style={{ marginLeft: 2 }}>✕</span>}
              </button>
            );
          })}
        </div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
          fontWeight: 700, letterSpacing:'.04em', textTransform:'uppercase',
          whiteSpace:'nowrap', flexShrink: 0,
        }}>
          {resultCount === totalCount
            ? `${totalCount} giochi`
            : `${resultCount} di ${totalCount}`}
        </div>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── CONTRIBUTORSSIDEBAR (NUOVO COMPONENTE V2) ────────
// ═══════════════════════════════════════════════════════
const ContributorsSidebar = () => {
  // Top contributors — derivati da DS.players
  const contribs = DS.players
    .map(p => ({ ...p, contribs: p.totalSessions + (p.totalWins * 2) }))
    .sort((a,b) => b.contribs - a.contribs)
    .slice(0, 5);

  return (
    <aside aria-label="Top contributor" style={{
      position:'sticky', top: 116, alignSelf:'start',
      background:'var(--bg-card)',
      border:'1px solid var(--border)',
      borderRadius:'var(--r-xl)',
      padding:'18px 16px',
      boxShadow:'var(--shadow-xs)',
    }}>
      <div style={{
        display:'flex', alignItems:'center', gap: 7,
        marginBottom: 14, paddingBottom: 10,
        borderBottom:'1px solid var(--border-light)',
      }}>
        <EntityPip type="player" size={22}/>
        <h3 style={{
          fontFamily:'var(--f-display)', fontWeight: 700, fontSize: 13,
          color:'var(--text)', margin: 0, letterSpacing:'-.01em',
        }}>Top contributor</h3>
      </div>

      <ol style={{ listStyle:'none', padding: 0, margin: 0, display:'flex', flexDirection:'column', gap: 10 }}>
        {contribs.map((p, i) => (
          <li key={p.id} style={{
            display:'flex', alignItems:'center', gap: 10,
          }}>
            <span style={{
              width: 18, textAlign:'right',
              fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 800,
              color: i === 0 ? 'hsl(var(--c-game))' : 'var(--text-muted)',
            }}>#{i+1}</span>
            <span style={{
              width: 32, height: 32, borderRadius:'50%',
              background:`hsl(${p.color} 60% 55%)`,
              color:'#fff',
              display:'inline-flex', alignItems:'center', justifyContent:'center',
              fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 11,
              flexShrink: 0,
              boxShadow:'inset 0 1px 0 rgba(255,255,255,.15)',
            }} aria-hidden="true">{p.initials}</span>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{
                fontFamily:'var(--f-display)', fontWeight: 700, fontSize: 12,
                color:'var(--text)', whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
              }}>{p.title}</div>
              <div style={{
                fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
                letterSpacing:'.04em', textTransform:'uppercase',
              }}>
                {p.totalSessions} partite · fav: {p.fav}
              </div>
            </div>
          </li>
        ))}
      </ol>

      <div style={{
        marginTop: 14, paddingTop: 12,
        borderTop:'1px solid var(--border-light)',
        display:'flex', flexDirection:'column', gap: 8,
      }}>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
          letterSpacing:'.08em', textTransform:'uppercase', fontWeight: 700,
        }}>// Stats community</div>
        <div style={{ display:'grid', gridTemplateColumns:'1fr 1fr', gap: 8 }}>
          <div>
            <div style={{ fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800, color:'hsl(var(--c-toolkit))' }}>
              {DS.toolkits.length * 3 + 18}
            </div>
            <div style={{ fontSize: 10, color:'var(--text-muted)' }}>toolkit pubbl.</div>
          </div>
          <div>
            <div style={{ fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800, color:'hsl(var(--c-agent))' }}>
              {DS.agents.length * 2 + 12}
            </div>
            <div style={{ fontSize: 10, color:'var(--text-muted)' }}>agenti attivi</div>
          </div>
        </div>
      </div>
    </aside>
  );
};

// ─── HERO PUBLIC (compact) ───────────────────────────────
const SharedGamesHero = ({ compact }) => (
  <section style={{
    padding: compact ? '24px 16px 20px' : '40px 24px 28px',
    textAlign: compact ? 'left' : 'center',
    position:'relative',
    background: compact ? 'transparent'
      : 'radial-gradient(80% 60% at 50% 0%, hsl(var(--c-toolkit) / .07), transparent 70%)',
  }}>
    <div style={{
      display:'inline-flex', alignItems:'center', gap: 6,
      padding:'4px 10px', borderRadius:'var(--r-pill)',
      background:'hsl(var(--c-toolkit) / .14)',
      color:'hsl(var(--c-toolkit))',
      fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
      textTransform:'uppercase', letterSpacing:'.08em',
      marginBottom: 12,
    }}>
      <span style={{ width: 6, height: 6, borderRadius:'50%', background:'hsl(var(--c-toolkit))' }}/>
      Catalogo community
    </div>
    <h1 style={{
      fontFamily:'var(--f-display)', fontWeight: 800,
      fontSize: compact ? 22 : 32,
      letterSpacing:'-.02em', lineHeight: 1.1,
      color:'var(--text)', margin:'0 0 8px',
    }}>
      Giochi <span style={{
        background:'linear-gradient(95deg, hsl(var(--c-game)), hsl(var(--c-toolkit)))',
        WebkitBackgroundClip:'text', WebkitTextFillColor:'transparent',
        backgroundClip:'text', color:'transparent',
      }}>condivisi</span> dalla community
    </h1>
    <p style={{
      fontFamily:'var(--f-body)', fontSize: compact ? 12 : 14,
      color:'var(--text-sec)', lineHeight: 1.55,
      margin: 0, maxWidth: compact ? '100%' : 540,
      marginLeft: compact ? 0 : 'auto', marginRight: compact ? 0 : 'auto',
    }}>
      Toolkit, agenti AI e knowledge base pubblicati da boardgamer come te.
      Esplora liberamente — accedi quando vuoi installare.
    </p>
  </section>
);

// ─── FILTER + SEARCH HOOK ────────────────────────────────
const useFilteredGames = (override) => {
  const [query, setQuery] = useState('');
  const [activeChips, setActiveChips] = useState([]);
  const [genre, setGenre] = useState('');
  const [sort, setSort] = useState('rating');

  // Override forza dataset (per stati empty/empty-search)
  useEffect(() => {
    if (override === 'empty-search') { setQuery('terraform'); setGenre(''); setActiveChips([]); }
    else if (override === 'filtered-empty') { setQuery(''); setActiveChips(['with-toolkit','with-agent','top-rated','new']); setGenre('Investigativo'); }
    else { setQuery(''); setActiveChips([]); setGenre(''); }
  }, [override]);

  const toggleChip = (id) => setActiveChips(c => c.includes(id) ? c.filter(x=>x!==id) : [...c, id]);
  const reset = () => { setQuery(''); setActiveChips([]); setGenre(''); };

  const filtered = useMemo(() => {
    let res = ALL_GAMES.slice();
    if (query) {
      const q = query.toLowerCase();
      res = res.filter(g =>
        g.title.toLowerCase().includes(q) ||
        (g.author||'').toLowerCase().includes(q) ||
        (g.publisher||'').toLowerCase().includes(q)
      );
    }
    if (genre) res = res.filter(g => g.genre === genre);
    activeChips.forEach(id => {
      const chip = FILTER_CHIPS.find(c => c.id === id);
      if (chip) res = res.filter(chip.test);
    });
    if (sort === 'rating')   res.sort((a,b) => (b.rating||0) - (a.rating||0));
    if (sort === 'contrib')  res.sort((a,b) => b.contribCount - a.contribCount);
    if (sort === 'new')      res.sort((a,b) => b.newWeek - a.newWeek);
    if (sort === 'title')    res.sort((a,b) => a.title.localeCompare(b.title));

    // Override forzato per "empty-search" — query "terraform" lascia 1 match,
    // ma vogliamo 0 per stato empty: filtriamo come se non ci fosse match.
    if (override === 'empty-search') return [];
    if (override === 'filtered-empty') return [];
    return res;
  }, [query, genre, activeChips, sort, override]);

  return { query, setQuery, activeChips, toggleChip, genre, setGenre, sort, setSort, filtered, reset };
};

// ─── EMPTY STATE ─────────────────────────────────────────
const EmptyState = ({ kind, onReset, query }) => {
  const isSearch = kind === 'empty-search';
  return (
    <div style={{
      gridColumn:'1 / -1',
      display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center',
      padding:'48px 24px',
    }}>
      <div style={{
        width: 72, height: 72, borderRadius:'50%',
        background: isSearch ? 'hsl(var(--c-info) / .12)' : 'hsl(var(--c-warning) / .12)',
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 32, marginBottom: 16,
      }} aria-hidden="true">{isSearch ? '🔎' : '🎲'}</div>
      <h3 style={{
        fontFamily:'var(--f-display)', fontWeight: 700, fontSize: 17,
        color:'var(--text)', margin:'0 0 6px',
      }}>
        {isSearch ? 'Nessun gioco trovato' : 'Nessun match con i filtri attuali'}
      </h3>
      <p style={{
        fontSize: 13, color:'var(--text-muted)', lineHeight: 1.55,
        maxWidth: 380, margin:'0 0 20px',
      }}>
        {isSearch
          ? <>La ricerca per <strong style={{ color:'var(--text)' }}>"{query || 'terraform'}"</strong> non ha prodotto risultati. Prova con un titolo diverso o rimuovi i filtri.</>
          : <>Hai applicato troppi filtri insieme. Rimuovili tutti per vedere il catalogo completo.</>}
      </p>
      <button
        type="button"
        onClick={onReset}
        style={{
          padding:'9px 16px',
          borderRadius:'var(--r-md)',
          border:'1.5px solid hsl(var(--c-game))',
          background:'transparent',
          color:'hsl(var(--c-game))',
          fontFamily:'var(--f-display)', fontWeight: 700, fontSize: 13,
          cursor:'pointer',
          transition:'all var(--dur-sm) var(--ease-out)',
        }}
        onMouseEnter={e => { e.currentTarget.style.background='hsl(var(--c-game) / .1)'; }}
        onMouseLeave={e => { e.currentTarget.style.background='transparent'; }}
      >
        ↺ Reset filtri
      </button>
    </div>
  );
};

// ─── ERROR STATE ─────────────────────────────────────────
const ErrorState = ({ onRetry }) => (
  <div style={{
    gridColumn:'1 / -1',
    display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center',
    padding:'48px 24px',
  }}>
    <div style={{
      width: 72, height: 72, borderRadius:'50%',
      background:'hsl(var(--c-danger) / .12)',
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 32, marginBottom: 16,
    }} aria-hidden="true">🛑</div>
    <h3 style={{
      fontFamily:'var(--f-display)', fontWeight: 700, fontSize: 17,
      color:'var(--text)', margin:'0 0 6px',
    }}>Impossibile caricare il catalogo</h3>
    <p style={{
      fontSize: 13, color:'var(--text-muted)', lineHeight: 1.55,
      maxWidth: 380, margin:'0 0 18px',
    }}>
      Il servizio è temporaneamente non disponibile. Riprova tra qualche secondo.
    </p>
    <button
      type="button"
      onClick={onRetry}
      style={{
        padding:'9px 16px',
        borderRadius:'var(--r-md)',
        border:'none',
        background:'hsl(var(--c-game))',
        color:'#fff',
        fontFamily:'var(--f-display)', fontWeight: 700, fontSize: 13,
        cursor:'pointer',
        boxShadow:'0 3px 10px hsl(var(--c-game) / .3)',
      }}
    >Riprova</button>
  </div>
);

// ─── GRID (default | loading | empty | error) ────────────
const SharedGamesGrid = ({ state, games, onReset, query, columns = 3, compact }) => {
  const gridStyle = {
    display:'grid',
    gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))`,
    gap: compact ? 10 : 14,
  };

  if (state === 'loading') {
    return (
      <div style={gridStyle}>
        {Array.from({ length: columns * 3 }).map((_, i) => (
          <SkeletonCard key={i}/>
        ))}
      </div>
    );
  }
  if (state === 'error') {
    return <div style={gridStyle}><ErrorState onRetry={onReset}/></div>;
  }
  if (games.length === 0) {
    return <div style={gridStyle}><EmptyState kind={state} onReset={onReset} query={query}/></div>;
  }

  return (
    <div style={gridStyle}>
      {games.map(g => <MeepleCardGame key={g.id} game={g} compact={compact}/>)}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── MOBILE 375 SCREEN ───────────────────────────────
// ═══════════════════════════════════════════════════════
const PhoneSbar = () => (
  <div className="phone-sbar">
    <span style={{ fontFamily:'var(--f-mono)' }}>14:32</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);

const PhoneTopNav = () => (
  <div style={{
    display:'flex', alignItems:'center', gap: 9,
    padding:'10px 14px',
    borderBottom:'1px solid var(--border)',
    background:'var(--bg)',
  }}>
    <div style={{
      width: 24, height: 24, borderRadius: 7,
      background:'linear-gradient(135deg, hsl(var(--c-game)), hsl(var(--c-event)))',
      color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
      fontWeight: 800, fontSize: 12, fontFamily:'var(--f-display)',
    }}>M</div>
    <span style={{
      fontFamily:'var(--f-display)', fontWeight: 700, fontSize: 13, color:'var(--text)',
    }}>MeepleAI</span>
    <div style={{ flex: 1 }}/>
    <button aria-label="Accedi" style={{
      padding:'5px 12px', borderRadius:'var(--r-md)',
      background:'transparent', border:'1px solid var(--border)',
      color:'var(--text)', fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 700,
      cursor:'pointer',
    }}>Accedi</button>
  </div>
);

const SharedGamesMobileScreen = ({ stateOverride }) => {
  const f = useFilteredGames(stateOverride);
  const isLoading = stateOverride === 'loading';
  const isError   = stateOverride === 'error';

  return (
    <>
      <PhoneSbar/>
      <div style={{
        flex: 1, overflowY:'auto', scrollbarWidth:'none',
        background:'var(--bg)', display:'flex', flexDirection:'column',
      }}>
        <PhoneTopNav/>
        <SharedGamesHero compact/>
        <SharedGamesFilters
          query={f.query} onQuery={f.setQuery}
          activeChips={f.activeChips} onToggleChip={f.toggleChip}
          genre={f.genre} onGenre={f.setGenre}
          sort={f.sort} onSort={f.setSort}
          resultCount={f.filtered.length}
          totalCount={ALL_GAMES.length}
          variant="mobile"
        />
        <div style={{ padding:'14px 14px 22px' }}>
          <SharedGamesGrid
            state={isLoading ? 'loading' : isError ? 'error' : stateOverride}
            games={f.filtered}
            onReset={f.reset}
            query={f.query}
            columns={2}
            compact
          />

          {/* Paginator (default only) */}
          {!isLoading && !isError && f.filtered.length > 0 && (
            <div style={{
              marginTop: 18, paddingTop: 16,
              borderTop:'1px solid var(--border-light)',
              textAlign:'center',
            }}>
              <button style={{
                padding:'9px 18px', borderRadius:'var(--r-md)',
                background:'var(--bg-muted)', border:'1px solid var(--border)',
                color:'var(--text)', fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700,
                cursor:'pointer',
              }}>Carica altri ({Math.max(0, ALL_GAMES.length - f.filtered.length + 8)})</button>
            </div>
          )}
        </div>
      </div>
    </>
  );
};

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

// ═══════════════════════════════════════════════════════
// ─── DESKTOP 1440 SCREEN ─────────────────────────────
// ═══════════════════════════════════════════════════════
const DesktopNav = () => (
  <div style={{
    position:'sticky', top: 0, zIndex: 5,
    display:'flex', alignItems:'center', gap: 14,
    padding:'12px 32px',
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
      <span style={{ fontFamily:'var(--f-display)', fontWeight: 700, fontSize: 14, color:'var(--text)' }}>MeepleAI</span>
    </div>
    <div style={{ flex: 1, display:'flex', gap: 6, marginLeft: 22 }}>
      {[
        { l:'Prodotto' }, { l:'Prezzi' },
        { l:'Catalogo', active:true }, { l:'Chi siamo' }, { l:'Contatti' },
      ].map(it => (
        <a key={it.l} href="#" onClick={e=>e.preventDefault()} style={{
          padding:'6px 12px', borderRadius:'var(--r-md)',
          color: it.active ? 'hsl(var(--c-game))' : 'var(--text-sec)',
          background: it.active ? 'hsl(var(--c-game) / .12)' : 'transparent',
          fontSize: 13, fontWeight: it.active ? 700 : 600, fontFamily:'var(--f-display)',
        }}>{it.l}</a>
      ))}
    </div>
    <a href="#" onClick={e=>e.preventDefault()} style={{
      padding:'6px 12px', borderRadius:'var(--r-md)',
      color:'var(--text-sec)', fontSize: 13, fontWeight: 700, fontFamily:'var(--f-display)',
      border:'1px solid var(--border)',
    }}>Accedi</a>
    <a href="#" onClick={e=>e.preventDefault()} style={{
      padding:'7px 14px', borderRadius:'var(--r-md)',
      color:'#fff', fontSize: 13, fontWeight: 700, fontFamily:'var(--f-display)',
      background:'hsl(var(--c-game))',
      boxShadow:'0 3px 10px hsl(var(--c-game) / .3)',
    }}>Inizia gratis</a>
  </div>
);

const SharedGamesDesktopScreen = ({ stateOverride }) => {
  const f = useFilteredGames(stateOverride);
  const isLoading = stateOverride === 'loading';
  const isError   = stateOverride === 'error';

  return (
    <div>
      <DesktopNav/>
      <SharedGamesHero/>
      <SharedGamesFilters
        query={f.query} onQuery={f.setQuery}
        activeChips={f.activeChips} onToggleChip={f.toggleChip}
        genre={f.genre} onGenre={f.setGenre}
        sort={f.sort} onSort={f.setSort}
        resultCount={f.filtered.length}
        totalCount={ALL_GAMES.length}
        variant="desktop"
      />
      <div style={{
        display:'grid', gridTemplateColumns:'1fr 268px', gap: 28,
        padding:'24px 32px 48px',
      }}>
        {/* Main grid */}
        <div>
          <SharedGamesGrid
            state={isLoading ? 'loading' : isError ? 'error' : stateOverride}
            games={f.filtered}
            onReset={f.reset}
            query={f.query}
            columns={3}
          />
          {!isLoading && !isError && f.filtered.length > 0 && (
            <div style={{
              marginTop: 28, paddingTop: 18,
              borderTop:'1px solid var(--border-light)',
              display:'flex', alignItems:'center', justifyContent:'space-between',
            }}>
              <div style={{
                fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
                letterSpacing:'.04em',
              }}>Pag. 1 di 3</div>
              <div style={{ display:'flex', gap: 6 }}>
                <button disabled style={{
                  width: 32, height: 32, borderRadius:'var(--r-md)',
                  background:'var(--bg-muted)', border:'1px solid var(--border)',
                  color:'var(--text-muted)', fontFamily:'var(--f-mono)', fontSize: 14,
                  cursor:'not-allowed',
                }} aria-label="Pagina precedente">←</button>
                {[1,2,3].map(p => (
                  <button key={p} style={{
                    width: 32, height: 32, borderRadius:'var(--r-md)',
                    background: p === 1 ? 'hsl(var(--c-game))' : 'var(--bg-card)',
                    border:'1px solid var(--border)',
                    color: p === 1 ? '#fff' : 'var(--text)',
                    fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700,
                    cursor:'pointer',
                  }} aria-label={`Pagina ${p}`} aria-current={p === 1 ? 'page' : undefined}>{p}</button>
                ))}
                <button style={{
                  width: 32, height: 32, borderRadius:'var(--r-md)',
                  background:'var(--bg-card)', border:'1px solid var(--border)',
                  color:'var(--text)', fontFamily:'var(--f-mono)', fontSize: 14,
                  cursor:'pointer',
                }} aria-label="Pagina successiva">→</button>
              </div>
            </div>
          )}
        </div>
        {/* Sidebar */}
        <ContributorsSidebar/>
      </div>
    </div>
  );
};

// ─── DESKTOP FRAME (browser chrome) ──────────────────────
const DesktopFrame = ({ children, label, desc }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 12, width:'100%' }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}</div>
    <div style={{
      width: '100%', maxWidth: 1280,
      borderRadius:'var(--r-xl)',
      border:'1px solid var(--border)',
      background:'var(--bg)',
      overflow:'hidden',
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
        <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/shared-games</span>
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
// ─── ROOT — STAGE ────────────────────────────────────
// ═══════════════════════════════════════════════════════
const MOBILE_STATES = [
  { id: 'default',         label: '01 · Default',         desc: '24 giochi caricati, ordinati per rating. Filtri tutti off.' },
  { id: 'loading',         label: '02 · Loading',         desc: 'Skeleton 2×3 con shimmer animato; filtri rimangono interattivi.' },
  { id: 'empty-search',    label: '03 · Empty search',    desc: 'Query "terraform" senza risultati — illustration + CTA reset.' },
  { id: 'filtered-empty',  label: '04 · Filtered empty',  desc: 'Tutti i 4 chip + genere "Investigativo" combinati: zero match.' },
];

function ThemeToggle() {
  const [dark, setDark] = useState(false);
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
  }, [dark]);
  return (
    <button
      onClick={() => setDark(d => !d)}
      className="theme-toggle"
      aria-label={dark ? 'Passa a tema chiaro' : 'Passa a tema scuro'}>
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
        }}>SP3 · Public Secondary · #2</div>
        <h1>Shared Games — Catalog Index</h1>
        <p className="lead">
          <code style={{ background:'var(--bg-muted)', padding:'2px 7px', borderRadius:'var(--r-sm)', fontSize: 12 }}>/shared-games</code>
          {' · '}24 giochi catalogati con counts toolkit/agent/kb derivati da
          {' '}<code style={{ background:'var(--bg-muted)', padding:'1px 6px', borderRadius:'var(--r-sm)', fontSize: 11 }}>data.js</code>.
          Hero leggero + filter bar sticky + grid responsive (3-col desktop, 2-col mobile).
          Componenti v2 nuovi: <strong>SharedGamesFilters</strong>, <strong>ContributorsSidebar</strong>.
          Riusa <strong>MeepleCard</strong> primitive (entity=game variante grid) e <strong>Banner</strong> (#1).
        </p>

        <div className="section-label">Mobile · 375 — 4 stati</div>
        <div className="phones-grid">
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc}>
              <SharedGamesMobileScreen stateOverride={s.id}/>
            </PhoneShell>
          ))}
        </div>

        <div className="section-label">Desktop · 1440 — main grid + contributors sidebar</div>
        <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
          <DesktopFrame label="05 · Desktop · Default"
            desc="Grid 3-col, sidebar 268px sticky con top contributors da DS.players + stats community. Pagination minimal in fondo.">
            <SharedGamesDesktopScreen stateOverride="default"/>
          </DesktopFrame>
          <DesktopFrame label="06 · Desktop · Loading"
            desc="Skeleton 3×3 con shimmer; sidebar visibile (i contributors caricano in parallelo, qui semplificato).">
            <SharedGamesDesktopScreen stateOverride="loading"/>
          </DesktopFrame>
          <DesktopFrame label="07 · Desktop · Filtered empty"
            desc="Tutti e 4 i filtri attivi + genere niche: empty state con CTA reset. Nota i chip evidenziati nella filter bar.">
            <SharedGamesDesktopScreen stateOverride="filtered-empty"/>
          </DesktopFrame>
        </div>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim {
          0%   { background-position: -200% 0; }
          100% { background-position:  200% 0; }
        }
        .mai-shimmer {
          background: linear-gradient(
            90deg,
            var(--bg-muted) 0%,
            var(--bg-hover) 50%,
            var(--bg-muted) 100%
          ) !important;
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
        article:focus-visible {
          outline: 2px solid hsl(var(--c-game));
          outline-offset: 2px;
        }
        button:focus-visible, a:focus-visible, input:focus-visible, select:focus-visible {
          outline: 2px solid hsl(var(--c-game));
          outline-offset: 2px;
        }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);

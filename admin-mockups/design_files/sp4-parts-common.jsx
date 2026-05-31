/* MeepleAI SP4 — Shared primitives reconstructed from the design system.
   Exposes window.MAI: entityHsl helper, fake data, and the reuse components
   referenced across the sessions cluster:
     OutcomeBadge · ScoringInline · ConnectionChipStripFooter · MeepleCard
   (originals live in sp4-sessions-index.jsx — faithfully re-derived from tokens.css). */

(function () {
  // ─── entity helper ───────────────────────────────────
  // tokens.css stores HSL triplets, so alpha composition is trivial.
  const entityHsl = (e, a) =>
    a != null ? `hsl(var(--c-${e}) / ${a})` : `hsl(var(--c-${e}))`;

  const EMOJI = {
    game: '🎲', player: '👤', session: '🎯', agent: '🤖', kb: '📄',
    chat: '💬', event: '🎉', toolkit: '🧰', tool: '🔧',
  };

  // ─── fake dataset (Wingspan night #3, 4 IT players) ──
  const GAME = { id: 'wingspan', title: 'Wingspan', emoji: '🦜', cover:
    'linear-gradient(135deg, hsl(165,55%,42%), hsl(205,60%,38%))' };

  // hue per player → derives avatar bg; ordered by final score
  const ROSTER = [
    { id: 'p-marco', name: 'Marco', hue: 262, score: 87, delta: +16, turnDelta: +16, current: true },
    { id: 'p-anna',  name: 'Anna',  hue: 350, score: 82, delta: +8,  turnDelta: 0 },
    { id: 'p-luca',  name: 'Luca',  hue: 174, score: 74, delta: +4,  turnDelta: 0 },
    { id: 'p-sara',  name: 'Sara',  hue: 38,  score: 61, delta: +2,  turnDelta: 0 },
  ];

  // Wingspan scoring categories (columns of the breakdown table)
  const CATS = [
    { id: 'birds',   lb: 'Uccelli',   em: '🐦' },
    { id: 'bonus',   lb: 'Bonus',     em: '🎯' },
    { id: 'eotr',    lb: 'Fine round',em: '🏁' },
    { id: 'eggs',    lb: 'Uova',      em: '🥚' },
    { id: 'cached',  lb: 'Cibo',      em: '🌰' },
    { id: 'tucked',  lb: 'Carte',     em: '🪶' },
  ];
  // per-player per-category points → rows sum to score
  const BREAKDOWN = {
    'p-marco': { birds: 31, bonus: 16, eotr: 11, eggs: 18, cached: 6, tucked: 5 },
    'p-anna':  { birds: 34, bonus: 9,  eotr: 13, eggs: 14, cached: 7, tucked: 5 },
    'p-luca':  { birds: 28, bonus: 7,  eotr: 9,  eggs: 16, cached: 9, tucked: 5 },
    'p-sara':  { birds: 22, bonus: 5,  eotr: 8,  eggs: 13, cached: 8, tucked: 5 },
  };

  const CHAT = [
    { id: 'c1', from: 'user',  t: '14:51', text: 'Quanto vale la wetland a fine partita?' },
    { id: 'c2', from: 'agent', t: '14:51', text: 'La wetland (riga blu) dà punti tramite gli uccelli giocati e le uova deposte sopra di essi — non ha un bonus di colonna proprio.', citations: ['Wingspan §4.2'] },
    { id: 'c3', from: 'user',  t: '14:58', text: 'I bird con uova multiple come si contano?' },
    { id: 'c4', from: 'agent', t: '14:58', text: 'Ogni uovo deposto su un uccello vale 1 punto a fine partita, indipendentemente dal numero massimo di uova del bird.', citations: ['Wingspan §6', 'FAQ #12'] },
  ];
  const CHAT_SUGGESTIONS = ['Punteggio bonus card?', 'Regola wetland', 'Tucked cards'];

  // connection-bar related entities for this session
  const CONNECTIONS = [
    { type: 'game',    label: 'Wingspan' },
    { type: 'player',  label: '4 player', count: 4 },
    { type: 'agent',   label: 'Wingspan Coach' },
    { type: 'kb',      label: '2 doc' },
    { type: 'chat',    label: '8 msg' },
    { type: 'event',   label: '12 eventi' },
  ];

  // ─── OutcomeBadge ─────────────────────────────────────
  // status pill for a session outcome. Win→toolkit, live→session,
  // abandoned→event, tie→toolkit (with handshake), loss→player.
  const OUTCOME = {
    win:       { lb: 'Vittoria',    em: '🏆', e: 'toolkit' },
    live:      { lb: 'In corso',    em: '●',  e: 'session', pulse: true },
    paused:    { lb: 'In pausa',    em: '⏸',  e: 'session' },
    tie:       { lb: 'Pareggio',    em: '🤝', e: 'toolkit' },
    abandoned: { lb: 'Abbandonata', em: '⏹',  e: 'event' },
    loss:      { lb: 'Sconfitta',   em: '—',  e: 'player' },
  };
  const OutcomeBadge = ({ status = 'win', size = 'md' }) => {
    const o = OUTCOME[status] || OUTCOME.win;
    const sm = size === 'sm';
    return (
      <span style={{
        display: 'inline-flex', alignItems: 'center', gap: sm ? 4 : 5,
        padding: sm ? '2px 8px' : '3px 10px', borderRadius: 'var(--r-pill)',
        background: entityHsl(o.e, 0.12), color: entityHsl(o.e),
        border: `1px solid ${entityHsl(o.e, 0.3)}`,
        fontFamily: 'var(--f-mono)', fontSize: sm ? 9 : 10, fontWeight: 800,
        textTransform: 'uppercase', letterSpacing: '.06em', whiteSpace: 'nowrap',
      }}>
        <span aria-hidden="true" className={o.pulse ? 'mai-pulse-dot' : undefined}
          style={o.pulse ? { fontSize: sm ? 8 : 9 } : undefined}>{o.em}</span>
        {o.lb}
      </span>
    );
  };

  // ─── ScoringInline ────────────────────────────────────
  // compact player avatar + score chip used in lists / footers.
  const ScoringInline = ({ player, rank, leader, size = 'md' }) => {
    const sm = size === 'sm';
    const av = sm ? 22 : 26;
    return (
      <div style={{ display: 'inline-flex', alignItems: 'center', gap: sm ? 6 : 8 }}>
        <div style={{
          width: av, height: av, borderRadius: '50%',
          background: `linear-gradient(135deg, hsl(${player.hue},70%,64%), hsl(${player.hue},58%,44%))`,
          color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontFamily: 'var(--f-display)', fontSize: sm ? 11 : 12.5, fontWeight: 800,
          border: leader ? `2px solid ${entityHsl('toolkit')}` : 'none', flexShrink: 0,
        }}>{player.name[0]}</div>
        <div style={{ display: 'flex', flexDirection: 'column', lineHeight: 1.1 }}>
          <span style={{
            fontFamily: 'var(--f-display)', fontSize: sm ? 12 : 13, fontWeight: 800,
            color: 'var(--text)',
          }}>{player.name}{rank != null && (
            <span style={{ color: 'var(--text-muted)', fontFamily: 'var(--f-mono)', fontSize: 9.5, marginLeft: 5 }}>{rank}°</span>
          )}</span>
        </div>
        <span style={{
          marginLeft: sm ? 2 : 4,
          fontFamily: 'var(--f-display)', fontSize: sm ? 15 : 18, fontWeight: 800,
          color: leader ? entityHsl('toolkit') : 'var(--text)',
          fontVariantNumeric: 'tabular-nums',
        }}>{player.score}</span>
      </div>
    );
  };

  // ─── ConnectionChipStripFooter ───────────────────────
  // horizontal scroll of entity chips for related entities; a footer
  // variant of the connection bar. onScrollTo lets pips jump to sections.
  const ConnectionChipStripFooter = ({ items = CONNECTIONS, onJump, compact }) => (
    <div className="mai-cb-scroll" style={{
      display: 'flex', gap: 6, overflowX: 'auto', padding: compact ? '8px 12px' : '10px 16px',
      borderTop: '1px solid var(--border)', background: 'var(--bg-card)',
      alignItems: 'center',
    }}>
      <span style={{
        fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800, color: 'var(--text-muted)',
        textTransform: 'uppercase', letterSpacing: '.08em', flexShrink: 0, marginRight: 2,
      }}>Collegati</span>
      {items.map((it) => (
        <button key={it.type} type="button" onClick={() => onJump && onJump(it.type)} style={{
          display: 'inline-flex', alignItems: 'center', gap: 5, flexShrink: 0,
          padding: '4px 10px', borderRadius: 'var(--r-pill)',
          background: entityHsl(it.type, 0.1), color: entityHsl(it.type),
          border: `1px solid ${entityHsl(it.type, 0.28)}`,
          fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 700, cursor: 'pointer',
        }}>
          <span aria-hidden="true">{EMOJI[it.type]}</span>{it.label}
        </button>
      ))}
    </div>
  );

  // ─── MeepleCard ──────────────────────────────────────
  // session summary card (game cover + title + meta + outcome).
  const MeepleCard = ({ title = 'Serata #3', subtitle = 'Wingspan · 4 giocatori',
    status = 'win', meta = '23 apr · 1h 24min', onClick, compact }) => (
    <button type="button" onClick={onClick} style={{
      display: 'flex', alignItems: 'center', gap: 12, width: '100%', textAlign: 'left',
      padding: compact ? 10 : 12, borderRadius: 'var(--r-lg)',
      background: 'var(--bg-card)', border: '1px solid var(--border)',
      boxShadow: 'var(--shadow-sm)', cursor: onClick ? 'pointer' : 'default',
    }}>
      <div style={{
        width: compact ? 44 : 52, height: compact ? 56 : 64, borderRadius: 'var(--r-md)',
        background: GAME.cover, flexShrink: 0, display: 'flex',
        alignItems: 'center', justifyContent: 'center', fontSize: compact ? 20 : 24,
        boxShadow: 'var(--shadow-xs)',
      }} aria-hidden="true">{GAME.emoji}</div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ fontFamily: 'var(--f-display)', fontSize: compact ? 14 : 15.5, fontWeight: 800, color: 'var(--text)' }}>{title}</div>
        <div style={{ fontFamily: 'var(--f-body)', fontSize: 12, color: 'var(--text-sec)', fontWeight: 600, marginBottom: 6 }}>{subtitle}</div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <OutcomeBadge status={status} size="sm" />
          <span style={{ fontFamily: 'var(--f-mono)', fontSize: 9.5, color: 'var(--text-muted)', fontWeight: 700 }}>{meta}</span>
        </div>
      </div>
    </button>
  );

  // ─── shared state-panel helpers (empty/loading/error/sse) ──
  const StateBlock = ({ icon, title, body, action, tone = 'muted' }) => {
    const border = tone === 'error' ? `1px solid ${entityHsl('event', 0.3)}` : '1px dashed var(--border-strong)';
    const bg = tone === 'error' ? entityHsl('event', 0.06) : 'var(--bg-card)';
    return (
      <div style={{
        margin: 'auto', maxWidth: 280, textAlign: 'center',
        padding: '28px 20px', borderRadius: 'var(--r-lg)', background: bg, border,
        display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6,
      }}>
        <div style={{ fontSize: 30 }} aria-hidden="true">{icon}</div>
        <div style={{ fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 800, color: 'var(--text)' }}>{title}</div>
        {body && <div style={{ fontFamily: 'var(--f-body)', fontSize: 12, color: 'var(--text-muted)', lineHeight: 1.5 }}>{body}</div>}
        {action}
      </div>
    );
  };

  const Shimmer = ({ h = 40, r = 'var(--r-md)', style }) => (
    <div className="mai-shimmer" style={{ height: h, borderRadius: r, background: 'var(--bg-muted)', ...style }} />
  );

  const SseBanner = ({ where = 'scores' }) => (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 8,
      padding: '8px 12px', borderRadius: 'var(--r-md)', margin: 12,
      background: entityHsl('agent', 0.1), border: `1px solid ${entityHsl('agent', 0.3)}`,
      fontFamily: 'var(--f-mono)', fontSize: 10.5, fontWeight: 700, color: entityHsl('agent'),
    }}>
      <span aria-hidden="true" className="mai-pulse-dot">◐</span>
      <span style={{ flex: 1 }}><strong>Stream interrotto</strong> · dati {where} non aggiornati · retry 5s…</span>
      <button type="button" style={{
        padding: '3px 9px', borderRadius: 'var(--r-pill)', flexShrink: 0,
        background: entityHsl('agent'), color: '#1a1208', border: 'none',
        fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800, cursor: 'pointer',
        textTransform: 'uppercase', letterSpacing: '.05em',
      }}>↻ Ora</button>
    </div>
  );

  window.MAI = {
    entityHsl, EMOJI, GAME, ROSTER, CATS, BREAKDOWN, CHAT, CHAT_SUGGESTIONS, CONNECTIONS,
    OutcomeBadge, ScoringInline, ConnectionChipStripFooter, MeepleCard,
    StateBlock, Shimmer, SseBanner,
  };
})();

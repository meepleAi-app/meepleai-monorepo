/* MeepleAI SP4 · Paleo — FLAVOR ATOMS  (window.PaleoFlavor · alias PF)
   Small, reusable visual atoms for the Paleo session, built ONLY from
   tokens.css variables. Mirrors the role of window.PGFlavor for Power Grid.

     mono · disp                — type helpers
     MiniAvatar                 — player avatar (owner hue)
     Meeple                     — tribe-member token (skill + owner + status)
     SkillBadge / SkillGlyph    — skill icon
     ResourceCounter / Token    — wood · stone · food (+ knowledge)
     KnowledgeBadge
     HandPrint / CavePainting   — pittura rupestre · impronte (red-ochre)
     Skull / SkullCluster       — teschi (danger)
     DayMoon                    — sun/moon phase indicator
     ReadyBadge · ActionPill · CardBack · MissionRow

   NOTE — intentional token deviation: the cave-painting ochre
   (--paleo-ochre) is a warm red-brown sampled BETWEEN --c-game (25°) and
   --c-event (350°). The DS has no ochre token; the brief explicitly asks for
   "hand prints in red-ocra", so it is declared once here as a derived accent
   (same warm family, no grey, no blue). */

(function () {
  const M = window.MAI;
  const PD = window.PaleoData;
  const eHsl = M.entityHsl;

  const mono = (size, weight, color) => ({ fontFamily: 'var(--f-mono)', fontSize: size, fontWeight: weight, color });
  const disp = (size, weight, color) => ({ fontFamily: 'var(--f-display)', fontSize: size, fontWeight: weight, color });

  // cave-painting ochre (derived warm accent, not grey/blue — see header note)
  const OCHRE = 'hsl(14, 62%, 47%)';
  const OCHRE_DK = 'hsl(14, 55%, 36%)';

  // resource meta — wood→toolkit · stone→neutral warm · food→game · knowledge→kb
  const RES = {
    wood:  { lb: 'Legno',  e: 'toolkit', glyph: '🪵', color: eHsl('toolkit'), tint: eHsl('toolkit', 0.12), ring: eHsl('toolkit', 0.3) },
    stone: { lb: 'Pietra', e: null,      glyph: '🪨', color: 'var(--text-sec)', tint: 'var(--bg-muted)', ring: 'var(--border-strong)' },
    food:  { lb: 'Cibo',   e: 'game',    glyph: '🍖', color: eHsl('game'),    tint: eHsl('game', 0.12),    ring: eHsl('game', 0.3) },
  };

  // ─── player avatar ────────────────────────────────────────────────────────
  const MiniAvatar = ({ p, size = 24, ring, dim }) => (
    <div aria-hidden="true" title={p.name} style={{
      width: size, height: size, borderRadius: '50%', flexShrink: 0,
      background: `linear-gradient(135deg, hsl(${p.hue},70%,64%), hsl(${p.hue},58%,44%))`,
      color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
      ...disp(size * 0.44, 800), border: ring ? `2px solid ${eHsl('session')}` : '2px solid var(--bg-card)',
      opacity: dim ? 0.4 : 1,
    }}>{p.name[0]}</div>
  );

  // ─── meeple token ───────────────────────────────────────────────────────
  const MEEPLE_PATH = 'M32 4a9 9 0 0 0-9 9 9 9 0 0 0 4.6 7.8C20 22 14 26 9 31c-3 3-1 8 3 8h7c-2 5-4 11-5 17-.6 4 2.6 7 6.6 7h22.8c4 0 7.2-3 6.6-7-1-6-3-12-5-17h7c4 0 6-5 3-8-5-5-11-9-18.6-10.2A9 9 0 0 0 41 13a9 9 0 0 0-9-9z';
  const Meeple = ({ member, size = 34 }) => {
    const owner = PD.byPlayer(member.owner);
    const dead = member.status === 'dead';
    const wounded = member.status === 'wounded';
    const fill = dead ? 'var(--text-muted)' : `hsl(${owner.hue},62%,52%)`;
    return (
      <span title={`${member.name} · ${owner.name}`} aria-hidden="true" style={{ position: 'relative', width: size, height: size, flexShrink: 0, display: 'inline-flex', opacity: dead ? 0.5 : 1 }}>
        <svg viewBox="0 0 64 64" width={size} height={size} style={{ display: 'block' }}>
          <path d={MEEPLE_PATH} fill={fill} stroke="rgba(0,0,0,.28)" strokeWidth="1.5" />
        </svg>
        {wounded && <span style={{ position: 'absolute', top: -3, right: -3, width: size * 0.42, height: size * 0.42, borderRadius: '50%', background: eHsl('event'), color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: size * 0.26, border: '1.5px solid var(--bg-card)' }}>✚</span>}
        {dead && <span style={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: size * 0.5 }}>💀</span>}
      </span>
    );
  };

  // ─── skill glyph / badge ──────────────────────────────────────────────────
  const SkillGlyph = ({ skill, size = 18 }) => {
    const s = PD.SKILLS[skill]; if (!s) return null;
    return (
      <span title={`${s.lb} — ${s.desc}`} aria-label={s.lb} style={{
        width: size, height: size, borderRadius: 'var(--r-sm)', flexShrink: 0,
        background: eHsl(s.e, 0.16), border: `1px solid ${eHsl(s.e, 0.32)}`,
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontSize: size * 0.6,
      }}>{s.glyph}</span>
    );
  };
  const SkillBadge = ({ skill }) => {
    const s = PD.SKILLS[skill]; if (!s) return null;
    return (
      <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, padding: '2px 8px 2px 3px', borderRadius: 'var(--r-pill)', background: eHsl(s.e, 0.1), border: `1px solid ${eHsl(s.e, 0.28)}` }}>
        <SkillGlyph skill={skill} size={16} /><span style={{ ...disp(10.5, 800, eHsl(s.e)) }}>{s.lb}</span>
      </span>
    );
  };

  // ─── resource token + counter ─────────────────────────────────────────────
  const ResourceToken = ({ kind, size = 16 }) => {
    const r = RES[kind];
    return <span aria-hidden="true" style={{ width: size, height: size, borderRadius: kind === 'stone' ? '40%' : 'var(--r-sm)', background: r.tint, border: `1px solid ${r.ring}`, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontSize: size * 0.66, flexShrink: 0 }}>{r.glyph}</span>;
  };
  const ResourceCounter = ({ kind, value, big }) => {
    const r = RES[kind];
    return (
      <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', gap: 4, padding: big ? '10px 11px' : '7px 9px', borderRadius: 'var(--r-md)', background: r.tint, border: `1px solid ${r.ring}` }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
          <span aria-hidden="true" style={{ fontSize: big ? 16 : 13 }}>{r.glyph}</span>
          <span style={{ ...mono(big ? 9.5 : 8.5, 800, r.color), textTransform: 'uppercase', letterSpacing: '.06em' }}>{r.lb}</span>
        </div>
        <span style={{ ...disp(big ? 26 : 19, 800, r.color === 'var(--text-sec)' ? 'var(--text)' : r.color), fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{value}</span>
      </div>
    );
  };
  const KnowledgeBadge = ({ count, big }) => (
    <div style={{ display: 'inline-flex', alignItems: 'center', gap: 6, padding: big ? '7px 11px' : '5px 9px', borderRadius: 'var(--r-pill)', background: eHsl('kb', 0.12), border: `1px solid ${eHsl('kb', 0.3)}` }}>
      <span aria-hidden="true" style={{ fontSize: big ? 16 : 13 }}>📜</span>
      <span style={{ ...disp(big ? 17 : 14, 800, eHsl('kb')), fontVariantNumeric: 'tabular-nums' }}>{count}</span>
      <span style={{ ...mono(8.5, 800, eHsl('kb')), textTransform: 'uppercase', letterSpacing: '.05em' }}>sapere</span>
    </div>
  );

  // ─── cave painting · hand prints ──────────────────────────────────────────
  const HAND_PATH = 'M32 60c-7 0-13-5-15-12-1-4-2-9-2-13l-2-9c-.5-2 .8-4 2.8-4.3 1.8-.3 3.5 1 3.9 2.8l1 4.7V14c0-2 1.7-3.6 3.7-3.6S32 12 32 14v13V9c0-2 1.7-3.6 3.7-3.6S39.4 7 39.4 9v18V12.5c0-2 1.7-3.6 3.7-3.6s3.6 1.6 3.6 3.6V31l1.4-5.8c.5-1.9 2.4-3 4.2-2.5 1.8.5 2.9 2.4 2.5 4.2l-2.3 12.4c-.6 4-1.6 8-3.2 11C45.6 56.4 39.4 60 32 60z';
  const HandPrint = ({ filled, size = 30 }) => (
    <span aria-hidden="true" style={{ display: 'inline-flex' }}>
      <svg viewBox="0 0 64 64" width={size} height={size} style={{ display: 'block' }}>
        <path d={HAND_PATH}
          fill={filled ? OCHRE : 'transparent'}
          stroke={filled ? OCHRE_DK : 'var(--border-strong)'}
          strokeWidth={filled ? 1.5 : 2}
          strokeDasharray={filled ? 'none' : '3 3'}
          style={{ opacity: filled ? 1 : 0.55 }} />
      </svg>
    </span>
  );
  // parchment panel of N hand prints (0..max filled)
  const CavePainting = ({ value, max = 5, size = 34, compact }) => (
    <div role="img"
      aria-valuetext={`${value} di ${max} impronte`} aria-label="Progresso pittura rupestre"
      style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: compact ? 4 : 8,
        padding: compact ? '8px 10px' : '12px 14px', borderRadius: 'var(--r-md)',
        background: 'linear-gradient(135deg, hsl(38,44%,82%), hsl(30,38%,72%))',
        border: `1px solid ${OCHRE_DK}`, boxShadow: 'inset 0 1px 6px rgba(60,30,10,.18)',
      }}>
      {Array.from({ length: max }).map((_, i) => <HandPrint key={i} filled={i < value} size={size} />)}
    </div>
  );

  // ─── skulls ─────────────────────────────────────────────────────────────
  const Skull = ({ on, size = 26, peril }) => (
    <span aria-hidden="true" style={{
      width: size, height: size, borderRadius: '50%', flexShrink: 0,
      display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontSize: size * 0.62,
      background: on ? eHsl('event', 0.16) : 'var(--bg-sunken)',
      border: `1.5px solid ${on ? eHsl('event', peril ? 0.7 : 0.45) : 'var(--border-strong)'}`,
      boxShadow: on && peril ? `0 0 0 3px ${eHsl('event', 0.18)}` : 'none',
      filter: on ? 'none' : 'grayscale(1) opacity(.4)',
    }} className={on && peril ? 'mai-pulse-dot' : undefined}>💀</span>
  );
  const SkullCluster = ({ value, max = 5, size = 26, compact }) => {
    const peril = value >= max - 1;
    return (
      <div aria-live="polite" aria-label={`${value} di ${max} teschi`} style={{
        display: 'flex', alignItems: 'center', gap: compact ? 4 : 6,
        padding: compact ? '6px 9px' : '9px 12px', borderRadius: 'var(--r-md)',
        background: peril ? eHsl('event', 0.1) : eHsl('event', 0.05),
        border: `1px solid ${eHsl('event', peril ? 0.45 : 0.25)}`,
      }}>
        {Array.from({ length: max }).map((_, i) => <Skull key={i} on={i < value} size={size} peril={peril && i < value} />)}
        {peril && <span style={{ ...mono(8.5, 800, eHsl('event')), textTransform: 'uppercase', letterSpacing: '.05em', marginLeft: 2 }}>pericolo</span>}
      </div>
    );
  };

  // ─── sun / moon phase indicator ────────────────────────────────────────────
  const DayMoon = ({ phaseId, size = 34 }) => {
    const map = { morning: { g: '🌅', e: 'agent' }, day: { g: '☀️', e: 'game' }, night: { g: '🌙', e: 'session' } };
    const m = map[phaseId] || map.day;
    return (
      <span aria-hidden="true" style={{ width: size, height: size, borderRadius: '50%', background: eHsl(m.e, 0.14), border: `1px solid ${eHsl(m.e, 0.32)}`, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontSize: size * 0.55, flexShrink: 0 }}>{m.g}</span>
    );
  };

  // ─── ready badge · action pill · card back · mission row ───────────────────
  const ReadyBadge = ({ ready }) => (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 4, padding: '2px 8px', borderRadius: 'var(--r-pill)', whiteSpace: 'nowrap',
      background: eHsl(ready ? 'toolkit' : 'agent', 0.12), border: `1px solid ${eHsl(ready ? 'toolkit' : 'agent', 0.3)}`,
      ...mono(8.5, 800, eHsl(ready ? 'toolkit' : 'agent')), textTransform: 'uppercase', letterSpacing: '.05em',
    }}>
      <span aria-hidden="true" className={ready ? undefined : 'mai-pulse-dot'}>{ready ? '✓' : '◐'}</span>{ready ? 'Pronto' : 'In pensiero'}
    </span>
  );
  const ActionPill = ({ action, compact }) => {
    const a = PD.ACTIONS[action]; if (!a) return null;
    return (
      <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5, padding: compact ? '3px 8px 3px 6px' : '4px 10px 4px 7px', borderRadius: 'var(--r-pill)', background: eHsl(a.e, 0.12), border: `1px solid ${eHsl(a.e, 0.3)}` }}>
        <span aria-hidden="true" style={{ fontSize: compact ? 12 : 13 }}>{a.icon}</span>
        <span style={{ ...disp(compact ? 10.5 : 11.5, 800, eHsl(a.e)) }}>{a.lb}</span>
      </span>
    );
  };
  const CardBack = ({ w = 44, h = 60, label = '?', e = 'session' }) => (
    <div aria-hidden="true" style={{
      width: w, height: h, borderRadius: 'var(--r-sm)', flexShrink: 0,
      background: `repeating-linear-gradient(135deg, ${eHsl(e, 0.18)}, ${eHsl(e, 0.18)} 5px, ${eHsl(e, 0.08)} 5px, ${eHsl(e, 0.08)} 10px)`,
      border: `1.5px solid ${eHsl(e, 0.4)}`, display: 'flex', alignItems: 'center', justifyContent: 'center',
      ...disp(w * 0.4, 800, eHsl(e)),
    }}>{label}</div>
  );
  const MissionRow = ({ mission }) => {
    const pct = Math.round((mission.progress / mission.goal) * 100);
    const done = mission.progress >= mission.goal;
    return (
      <div style={{ display: 'flex', alignItems: 'center', gap: 9, padding: '8px 10px', borderRadius: 'var(--r-md)', background: done ? eHsl('toolkit', 0.08) : 'var(--bg-muted)', border: `1px solid ${done ? eHsl('toolkit', 0.3) : 'var(--border-light)'}` }}>
        <span aria-hidden="true" style={{ fontSize: 17, flexShrink: 0 }}>{mission.icon}</span>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 6 }}>
            <span style={{ ...disp(12, 800, 'var(--text)'), whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{mission.lb}</span>
            <div style={{ flex: 1 }} />
            <span style={{ ...mono(9.5, 800, done ? eHsl('toolkit') : 'var(--text-sec)') }}>{mission.progress}/{mission.goal}</span>
          </div>
          <div style={{ height: 5, borderRadius: 'var(--r-pill)', background: 'var(--bg-sunken)', overflow: 'hidden', marginTop: 4 }}>
            <div style={{ width: `${pct}%`, height: '100%', borderRadius: 'var(--r-pill)', background: done ? eHsl('toolkit') : OCHRE }} />
          </div>
        </div>
      </div>
    );
  };

  window.PaleoFlavor = {
    mono, disp, OCHRE, OCHRE_DK, RES,
    MiniAvatar, Meeple, SkillGlyph, SkillBadge,
    ResourceToken, ResourceCounter, KnowledgeBadge,
    HandPrint, CavePainting, Skull, SkullCluster,
    DayMoon, ReadyBadge, ActionPill, CardBack, MissionRow,
  };
})();

/* MeepleAI SP4 · Codenames — FLAVOR ATOMS  (window.CNFlavor)
   Small reusable bits + type helpers, all from tokens.css.
   Reads window.MAI (entityHsl) + window.CN (data). */

(function () {
  const M = window.MAI;
  const CN = window.CN;
  const eHsl = M.entityHsl;

  // type helpers (mirror PGFlavor.mono/disp)
  const mono = (size, weight, color) => ({ fontFamily: 'var(--f-mono)', fontSize: size, fontWeight: weight, color });
  const disp = (size, weight, color) => ({ fontFamily: 'var(--f-display)', fontSize: size, fontWeight: weight, color });

  const teamEntity = (team) => (team === 'red' ? 'event' : 'chat');
  const teamName   = (team) => (team === 'red' ? 'Rossi' : 'Blu');

  // ─── key-card swatch colors (the "real" identity of a tile) ───────────────
  // assassin uses the deepest ink; neutral uses a warm beige from the palette.
  const KEY = {
    red:      { e: 'event', label: 'Agente Rosso',  swatch: 'hsl(var(--c-event))' },
    blue:     { e: 'chat',  label: 'Agente Blu',     swatch: 'hsl(var(--c-chat))' },
    neutral:  { e: null,    label: 'Civile',         swatch: 'var(--bg-sunken)' },
    assassin: { e: null,    label: 'Assassino',      swatch: '#0f0c08' },
  };

  // ─── RoleAvatar — player chip; spymaster gets a distinguishing badge ──────
  const RoleAvatar = ({ p, size = 26, ring, showRole, dim }) => {
    const spy = p.role === 'spymaster';
    const e = teamEntity(p.team);
    return (
      <span style={{ position: 'relative', display: 'inline-flex', flexShrink: 0, opacity: dim ? 0.5 : 1 }}>
        <span aria-hidden="true" style={{
          width: size, height: size, borderRadius: '50%',
          background: `linear-gradient(135deg, hsl(${p.hue},72%,62%), hsl(${p.hue},60%,42%))`,
          color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
          ...disp(size * 0.44, 800), border: ring ? `2px solid ${eHsl(e)}` : '2px solid var(--bg-card)',
        }}>{p.name[0]}</span>
        {spy && (
          <span title="Spymaster" aria-label="Spymaster" style={{
            position: 'absolute', bottom: -3, right: -4, width: size * 0.5, height: size * 0.5, minWidth: 13, minHeight: 13,
            borderRadius: '50%', background: eHsl(e), color: '#fff', border: '1.5px solid var(--bg-card)',
            display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: Math.max(7, size * 0.26),
          }}>🕶</span>
        )}
        {showRole && (
          <span style={{ ...mono(7.5, 800, eHsl(e)), position: 'absolute', top: -7, left: '50%', transform: 'translateX(-50%)', whiteSpace: 'nowrap', textTransform: 'uppercase', letterSpacing: '.04em' }}>
            {spy ? 'SPY' : 'OP'}
          </span>
        )}
      </span>
    );
  };

  // ─── RoleTag — text role pill ─────────────────────────────────────────────
  const RoleTag = ({ role, team }) => {
    const e = teamEntity(team);
    const spy = role === 'spymaster';
    return (
      <span style={{
        display: 'inline-flex', alignItems: 'center', gap: 3, padding: '1px 7px', borderRadius: 'var(--r-pill)',
        background: eHsl(e, 0.12), border: `1px solid ${eHsl(e, 0.3)}`, ...mono(8.5, 800, eHsl(e)),
        textTransform: 'uppercase', letterSpacing: '.05em', whiteSpace: 'nowrap',
      }}>
        <span aria-hidden="true">{spy ? '🕶' : '🔎'}</span>{spy ? 'Spymaster' : 'Operativo'}
      </span>
    );
  };

  // ─── ClueChip — "WORD : N" with team color ────────────────────────────────
  const ClueChip = ({ word, number, team, big, dim }) => {
    const e = teamEntity(team);
    return (
      <span style={{
        display: 'inline-flex', alignItems: 'center', gap: big ? 9 : 6, padding: big ? '6px 12px' : '3px 9px',
        borderRadius: 'var(--r-md)', background: eHsl(e, dim ? 0.06 : 0.12), border: `1px solid ${eHsl(e, dim ? 0.2 : 0.34)}`,
        opacity: dim ? 0.8 : 1,
      }}>
        <span style={{ ...disp(big ? 18 : 12.5, 800, 'var(--text)'), letterSpacing: '.04em', textTransform: 'uppercase' }}>{word}</span>
        <span aria-hidden="true" style={{ ...mono(big ? 14 : 10, 700, eHsl(e)) }}>:</span>
        <span style={{
          width: big ? 26 : 18, height: big ? 26 : 18, borderRadius: 'var(--r-sm)', flexShrink: 0,
          background: eHsl(e), color: '#fff', display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          ...disp(big ? 14 : 10.5, 800), fontVariantNumeric: 'tabular-nums',
        }}>{number}</span>
      </span>
    );
  };

  // ─── GuessPips — N+1 slots: filled = correct, hollow = remaining, dashed = bonus
  const GuessPips = ({ made, number, team }) => {
    const e = teamEntity(team);
    const slots = [];
    for (let i = 0; i < number; i++) slots.push(i < made ? 'done' : 'open');
    slots.push(made > number ? 'done' : 'bonus');
    return (
      <div role="img" aria-label={`${made} indovinati su ${number} (+1 bonus)`} style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
        {slots.map((kind, i) => (
          <span key={i} aria-hidden="true" style={{
            width: 13, height: 13, borderRadius: '50%', flexShrink: 0,
            background: kind === 'done' ? eHsl(e) : 'transparent',
            border: kind === 'done' ? 'none' : kind === 'bonus' ? `1.5px dashed ${eHsl(e, 0.6)}` : `1.5px solid ${eHsl(e, 0.4)}`,
          }} />
        ))}
      </div>
    );
  };

  // ─── KeyCell — small swatch used in the Spymaster key-card mini grid ──────
  const KeyCell = ({ keyType, revealed, size = 'auto' }) => {
    const k = KEY[keyType] || KEY.neutral;
    const isDark = keyType === 'assassin';
    return (
      <span aria-hidden="true" style={{
        aspectRatio: '1', width: size === 'auto' ? '100%' : size, borderRadius: 'var(--r-xs)',
        background: k.e ? eHsl(k.e, revealed ? 1 : 0.85) : k.swatch,
        border: `1px solid ${isDark ? 'rgba(255,255,255,.18)' : k.e ? eHsl(k.e) : 'var(--border-strong)'}`,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 9, color: '#fff', opacity: revealed ? 0.45 : 1,
      }}>{isDark ? '💀' : ''}</span>
    );
  };

  // ─── TimerChip — countdown with warning state · aria-live ─────────────────
  const fmt = (s) => `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
  const TimerChip = ({ timer, compact }) => {
    if (!timer || !timer.enabled) return null;
    const warn = timer.remaining <= timer.warn;
    const e = !timer.active ? null : warn ? 'event' : 'session';
    const fg = e ? eHsl(e) : 'var(--text-muted)';
    return (
      <span role="timer" aria-live="polite" aria-label={`${timer.label}: ${fmt(timer.remaining)}${timer.active ? '' : ' · in pausa'}`}
        style={{
          display: 'inline-flex', alignItems: 'center', gap: 5, padding: compact ? '2px 8px' : '4px 10px',
          borderRadius: 'var(--r-pill)', background: e ? eHsl(e, 0.1) : 'var(--bg-muted)',
          border: `1px solid ${e ? eHsl(e, 0.32) : 'var(--border)'}`, whiteSpace: 'nowrap',
        }}>
        <span aria-hidden="true" className={timer.active && warn ? 'mai-pulse-dot' : undefined} style={{ fontSize: compact ? 10 : 11, color: fg }}>{timer.active ? '⏱' : '⏸'}</span>
        <span style={{ ...mono(compact ? 9 : 10, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.04em' }}>{timer.label}</span>
        <span style={{ ...mono(compact ? 11 : 12.5, 800, fg), fontVariantNumeric: 'tabular-nums' }}>{timer.active ? fmt(timer.remaining) : `${timer.duration}s`}</span>
      </span>
    );
  };

  // ─── OutcomeBadge for a clue result ───────────────────────────────────────
  const CLUE_OUTCOME = {
    clean:   { e: 'toolkit', lb: 'Pieno',     icon: '✓' },
    partial: { e: 'agent',   lb: 'Parziale',  icon: '◑' },
    miss:    { e: 'event',   lb: 'Mancato',   icon: '✕' },
    active:  { e: 'session', lb: 'In corso',  icon: '●', pulse: true },
  };
  const ClueOutcome = ({ outcome }) => {
    const o = CLUE_OUTCOME[outcome] || CLUE_OUTCOME.clean;
    return (
      <span style={{
        display: 'inline-flex', alignItems: 'center', gap: 4, padding: '1px 8px', borderRadius: 'var(--r-pill)',
        background: eHsl(o.e, 0.12), border: `1px solid ${eHsl(o.e, 0.3)}`, ...mono(8.5, 800, eHsl(o.e)),
        textTransform: 'uppercase', letterSpacing: '.04em', whiteSpace: 'nowrap',
      }}>
        <span aria-hidden="true" className={o.pulse ? 'mai-pulse-dot' : undefined}>{o.icon}</span>{o.lb}
      </span>
    );
  };

  window.CNFlavor = {
    mono, disp, teamEntity, teamName, KEY,
    RoleAvatar, RoleTag, ClueChip, GuessPips, KeyCell, TimerChip, ClueOutcome, CLUE_OUTCOME, fmt,
  };
})();

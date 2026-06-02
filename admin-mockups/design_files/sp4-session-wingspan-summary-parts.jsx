/* MeepleAI SP4 wave 2 — Session summary PARTS — window.SummaryParts
   Reconstructed primitives consumed by the original sp4-session-summary.jsx:
     eH · FINAL · ROSTER
     SummaryHeroPodium · KpiGrid · ScoringBreakdownTable · ConnectionBar
   Faithful to tokens.css; data shared via window.MAI. */

(function () {
  const { useState } = React;
  const M = window.MAI;
  const eH = M.entityHsl;

  const FINAL = M.ROSTER;            // already ordered by score desc
  const total = (id) => Object.values(M.BREAKDOWN[id]).reduce((a, b) => a + b, 0);

  // ─── SummaryHeroPodium ───────────────────────────────
  const Confetti = () => (
    <div aria-hidden="true" style={{ position: 'absolute', inset: 0, overflow: 'hidden', pointerEvents: 'none' }}>
      {Array.from({ length: 18 }).map((_, i) => {
        const ents = ['session', 'toolkit', 'game', 'event', 'chat', 'agent'];
        const c = ents[i % ents.length];
        return (
          <span key={i} className="mai-confetti-piece" style={{
            position: 'absolute', top: 0, left: `${(i * 5.5 + 4) % 100}%`,
            width: 7, height: 11, borderRadius: 2, background: eH(c),
            animationDelay: `${(i % 6) * 0.18}s`,
          }} />
        );
      })}
    </div>
  );

  const Avatar = ({ p, size, crown, tie }) => (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4 }}>
      {crown && <span style={{ fontSize: size > 70 ? 24 : 18 }} aria-hidden="true">👑</span>}
      <div style={{ position: 'relative' }}>
        <div style={{
          width: size, height: size, borderRadius: '50%',
          background: `linear-gradient(135deg, hsl(${p.hue},70%,66%), hsl(${p.hue},58%,44%))`,
          color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontFamily: 'var(--f-display)', fontSize: size * 0.4, fontWeight: 800,
          border: crown ? `3px solid ${eH('toolkit')}` : 'none', boxShadow: 'var(--shadow-md)',
        }}>{p.name[0]}</div>
        {tie && <span style={{
          position: 'absolute', top: -2, right: -2, width: 18, height: 18, borderRadius: '50%',
          background: eH('toolkit'), color: '#fff', fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800,
          display: 'flex', alignItems: 'center', justifyContent: 'center', border: '2px solid var(--bg-card)',
        }}>T</span>}
      </div>
      <div style={{ fontFamily: 'var(--f-display)', fontSize: size > 70 ? 15 : 13, fontWeight: 800, color: 'var(--text)' }}>{p.name}</div>
      <div style={{ fontFamily: 'var(--f-display)', fontSize: crown ? (size > 70 ? 30 : 22) : 18, fontWeight: 800, color: crown ? eH('toolkit') : 'var(--text)', fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{p.score}</div>
    </div>
  );

  const SummaryHeroPodium = ({ variant = 'default', compact }) => {
    const pad = compact ? '20px 14px' : '28px 24px';
    if (variant === 'abandoned') {
      return (
        <div style={{ padding: pad, background: eH('event', 0.06), borderBottom: `1px solid ${eH('event', 0.2)}` }}>
          <M.OutcomeBadge status="abandoned" />
          <h2 style={{ fontFamily: 'var(--f-display)', fontSize: compact ? 20 : 26, fontWeight: 800, color: 'var(--text)', margin: '10px 0 4px', letterSpacing: '-.02em' }}>Partita interrotta</h2>
          <p style={{ fontFamily: 'var(--f-body)', fontSize: 12.5, color: 'var(--text-sec)', margin: '0 0 14px' }}>Sospesa al turno 12 / 18 · stato salvato automaticamente.</p>
          <div style={{ display: 'flex', gap: 7, flexWrap: 'wrap' }}>
            <button type="button" style={{ padding: '9px 15px', borderRadius: 'var(--r-md)', background: eH('session'), color: '#fff', border: 'none', fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800, cursor: 'pointer' }}>▶ Riprendi</button>
            <button type="button" style={{ padding: '9px 15px', borderRadius: 'var(--r-md)', background: 'transparent', color: eH('event'), border: `1px solid ${eH('event', 0.4)}`, fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800, cursor: 'pointer' }}>Archivia</button>
          </div>
        </div>
      );
    }
    if (variant === 'solo') {
      const p = FINAL[0];
      return (
        <div style={{ padding: pad, position: 'relative', background: `radial-gradient(circle at 50% 0%, ${eH('toolkit', 0.12)} 0%, transparent 60%)`, borderBottom: '1px solid var(--border)', textAlign: 'center' }}>
          {!compact && <Confetti />}
          <div style={{ position: 'relative', zIndex: 1, display: 'inline-flex', flexDirection: 'column', alignItems: 'center' }}>
            <M.OutcomeBadge status="win" />
            <div style={{ marginTop: 12 }}><Avatar p={p} size={compact ? 70 : 96} crown /></div>
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)', fontWeight: 700, marginTop: 8 }}>Solo run · record personale</div>
          </div>
        </div>
      );
    }
    const tied = variant === 'tied';
    return (
      <div style={{ padding: pad, position: 'relative', background: `radial-gradient(circle at 30% 0%, ${eH('session', 0.1)} 0%, transparent 55%), radial-gradient(circle at 80% 10%, ${eH('toolkit', 0.08)} 0%, transparent 50%)`, borderBottom: '1px solid var(--border)' }}>
        {!compact && <Confetti />}
        <div style={{ position: 'relative', zIndex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 14 }}>
          {tied
            ? <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, padding: '3px 10px', borderRadius: 'var(--r-pill)', background: eH('toolkit', 0.14), color: eH('toolkit'), fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, textTransform: 'uppercase', letterSpacing: '.06em' }}>🤝 Pareggio · {FINAL[0].name} e {FINAL[1].name}</span>
            : <M.OutcomeBadge status="win" />}
          <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'center', gap: compact ? 14 : 26 }}>
            <Avatar p={FINAL[1]} size={compact ? 52 : 70} crown={tied} tie={tied} />
            <Avatar p={FINAL[0]} size={compact ? 70 : 96} crown tie={tied} />
            <Avatar p={FINAL[2]} size={compact ? 52 : 70} />
          </div>
          {/* 4th place row */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '7px 14px', borderRadius: 'var(--r-pill)', background: 'var(--bg-card)', border: '1px solid var(--border)' }}>
            <M.ScoringInline player={FINAL[3]} rank={4} size="sm" />
          </div>
        </div>
      </div>
    );
  };

  // ─── KpiGrid ─────────────────────────────────────────
  const KPI = [
    { e: 'session', em: '⏱', lb: 'Durata', v: '1h 24m' },
    { e: 'session', em: '🔄', lb: 'Turni', v: '18' },
    { e: 'toolkit', em: '🏆', lb: 'Top score', v: '87' },
    { e: 'player',  em: '📊', lb: 'Margine', v: '+5' },
    { e: 'tool',    em: '⚡', lb: 'Media/turno', v: '38s' },
    { e: 'event',   em: '🎉', lb: 'Eventi', v: '12' },
  ];
  const KpiGrid = ({ compact }) => (
    <div>
      <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 10 }}>Statistiche partita</div>
      <div style={{ display: 'grid', gridTemplateColumns: compact ? 'repeat(2, 1fr)' : 'repeat(3, 1fr)', gap: 8 }}>
        {KPI.map(k => (
          <div key={k.lb} style={{ padding: '12px 14px', borderRadius: 'var(--r-lg)', background: 'var(--bg-card)', border: '1px solid var(--border)', display: 'flex', alignItems: 'center', gap: 10 }}>
            <div style={{ width: 34, height: 34, borderRadius: 'var(--r-md)', background: eH(k.e, 0.12), display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 16, flexShrink: 0 }} aria-hidden="true">{k.em}</div>
            <div>
              <div style={{ fontFamily: 'var(--f-display)', fontSize: 20, fontWeight: 800, color: 'var(--text)', fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{k.v}</div>
              <div style={{ fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.04em', marginTop: 2 }}>{k.lb}</div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );

  // ─── ScoringBreakdownTable ───────────────────────────
  const ScoringBreakdownTable = ({ compact }) => (
    <div>
      <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 10 }}>Breakdown punteggi</div>
      <div className="mai-cb-scroll" style={{ overflowX: 'auto', border: '1px solid var(--border)', borderRadius: 'var(--r-lg)', background: 'var(--bg-card)' }}>
        <table style={{ borderCollapse: 'collapse', width: '100%', minWidth: 460 }}>
          <thead>
            <tr>
              <th style={thCell('left')}>Giocatore</th>
              {M.CATS.map(c => <th key={c.id} style={thCell()}><span aria-hidden="true">{c.em}</span></th>)}
              <th style={{ ...thCell(), color: eH('toolkit') }}>Tot</th>
            </tr>
          </thead>
          <tbody>
            {FINAL.map((p, i) => (
              <tr key={p.id} style={{ background: i === 0 ? eH('toolkit', 0.05) : 'transparent' }}>
                <td style={tdCell('left')}>
                  <div style={{ display: 'inline-flex', alignItems: 'center', gap: 7 }}>
                    <span style={{ width: 22, height: 22, borderRadius: '50%', background: `linear-gradient(135deg, hsl(${p.hue},70%,64%), hsl(${p.hue},58%,44%))`, color: '#fff', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 800 }}>{p.name[0]}</span>
                    <span style={{ fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800, color: 'var(--text)' }}>{p.name}{i === 0 && ' 🏆'}</span>
                  </div>
                </td>
                {M.CATS.map(c => <td key={c.id} style={tdCell()}>{M.BREAKDOWN[p.id][c.id]}</td>)}
                <td style={{ ...tdCell(), fontWeight: 800, color: i === 0 ? eH('toolkit') : 'var(--text)' }}>{total(p.id)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
  function thCell(align) {
    return { padding: '9px 12px', textAlign: align || 'center', fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 800, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.04em', borderBottom: '1px solid var(--border)', whiteSpace: 'nowrap', position: 'sticky', top: 0, background: 'var(--bg-card)' };
  }
  function tdCell(align) {
    return { padding: '9px 12px', textAlign: align || 'center', fontFamily: align === 'left' ? 'var(--f-display)' : 'var(--f-mono)', fontSize: 12, fontWeight: 700, color: 'var(--text-sec)', borderBottom: '1px solid var(--border-light)', fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' };
  }

  // ─── ConnectionBar ───────────────────────────────────
  const SECTION_OF = { game: 'section-session', player: 'section-player', agent: 'section-agent', kb: 'section-kb', chat: 'section-chat', event: 'section-event' };
  const ConnectionBar = ({ compact }) => {
    const jump = (type) => {
      const el = document.getElementById(SECTION_OF[type]);
      if (el) {
        const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        el.scrollIntoView({ behavior: reduce ? 'auto' : 'smooth', block: 'start' });
      }
    };
    return <M.ConnectionChipStripFooter items={M.CONNECTIONS} onJump={jump} compact={compact} />;
  };

  window.SummaryParts = { eH, FINAL, ROSTER: M.ROSTER, SummaryHeroPodium, KpiGrid, ScoringBreakdownTable, ConnectionBar };
})();

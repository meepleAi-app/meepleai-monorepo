/* MeepleAI SP4 — /play-records/[id] · DETAIL
   Route: /play-records/[id]
   File: admin-mockups/design_files/sp4-play-records-detail.{html,jsx}
   Modello: sp4-session-summary — Hero podio + ConnectionBar + classifica + dettaglio punteggi + note.
   Entity dominante: session 🎯 (240 60% 55%). Tone: festoso ma elegante.
*/
const { useState, useEffect } = React;
const DS = window.DS;

const eHs = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.session;
  return alpha !== undefined ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})` : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

// Record principale (Wingspan, 4 giocatori, Marco vince)
const REC = DS.byId['pr1'];
const REC_GAME = DS.byId[REC.game];

// ranking helper
const ranked = (scores) => [...scores]
  .map((s, i) => ({ ...s, _orig: i }))
  .sort((a, b) => (Number(b.score) ?? -Infinity) - (Number(a.score) ?? -Infinity));

const pColor = (playerId, fallbackHue = 240) => {
  const p = DS.byId[playerId];
  return p ? p.color : fallbackHue;
};
const initialsOf = (s) => {
  const p = DS.byId[s.playerId];
  return p ? p.initials : s.name.slice(0, 2).toUpperCase();
};

// ─── SECTION TITLE ─────────────────────────────────────
const SectionTitle = ({ em, children, extra }) => (
  <h2 style={{ fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800, color:'var(--text)', margin:'0 0 10px', display:'inline-flex', alignItems:'center', gap: 7 }}>
    <span aria-hidden="true">{em}</span>{children}
    {extra && <span style={{ fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 700, color:'var(--text-muted)' }}>{extra}</span>}
  </h2>
);

// ─── HERO PODIUM ───────────────────────────────────────
const Confetti = ({ show }) => {
  if (!show) return null;
  const pieces = Array.from({ length: 22 });
  const colors = [eHs('session'), eHs('toolkit'), eHs('game'), eHs('event'), eHs('agent')];
  return (
    <div aria-hidden="true" style={{ position:'absolute', inset: 0, overflow:'hidden', pointerEvents:'none', zIndex: 1 }}>
      {pieces.map((_, i) => (
        <span key={i} className="mai-confetti-piece" style={{
          position:'absolute', top: 0, left: `${(i * 4.6) % 100}%`,
          width: 7, height: 11, borderRadius: 2, background: colors[i % colors.length],
          animationDelay: `${(i % 7) * 0.12}s`,
        }}/>
      ))}
    </div>
  );
};

const HeroPodium = ({ record, variant, compact }) => {
  const game = DS.byId[record.game];
  const isTie = variant === 'tied';
  const isInProgress = variant === 'inprogress';
  const isPlanned = variant === 'planned';
  const rs = ranked(record.scores);

  // outcome banner content
  if (isPlanned) {
    return (
      <div style={{ padding: compact ? '22px 16px' : '32px 32px', background:`linear-gradient(135deg, ${eHs('event', 0.12)}, ${eHs('session', 0.1)})`, borderBottom:'1px solid var(--border-light)', position:'relative', overflow:'hidden' }}>
        <div style={{ position:'relative', display:'flex', alignItems:'center', gap: 14, flexWrap:'wrap' }}>
          <div style={{ width: compact ? 56 : 68, height: compact ? 56 : 68, borderRadius:'var(--r-lg)', background: game?.cover, display:'flex', alignItems:'center', justifyContent:'center', fontSize: compact ? 28 : 36 }} aria-hidden="true">{game?.coverEmoji}</div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <span style={{ display:'inline-flex', alignItems:'center', gap: 5, padding:'2px 8px', borderRadius:'var(--r-pill)', background: eHs('event', 0.14), color: eHs('event'), fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800, textTransform:'uppercase', letterSpacing:'.08em', border:`1px solid ${eHs('event', 0.3)}` }}>📅 Pianificata</span>
            <h1 style={{ fontFamily:'var(--f-display)', fontSize: compact ? 22 : 30, fontWeight: 800, letterSpacing:'-.02em', margin:'6px 0 2px', color:'var(--text)' }}>{game?.title}</h1>
            <div style={{ fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-sec)', fontWeight: 700 }}>{record.date} · 👥 {record.playerCount} giocatori</div>
          </div>
          <a href="sp4-play-records-edit.html" style={{ padding:'10px 16px', borderRadius:'var(--r-md)', background: eHs('session'), color:'#fff', fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, boxShadow:`0 4px 14px ${eHs('session', 0.4)}` }}>▶ Avvia partita</a>
        </div>
      </div>
    );
  }

  const top3 = rs.slice(0, 3);
  const podiumOrder = top3.length >= 3 ? [top3[1], top3[0], top3[2]] : top3.length === 2 ? [top3[1], top3[0]] : [top3[0]];

  return (
    <div style={{ padding: compact ? '20px 16px 18px' : '28px 32px 24px', background:`radial-gradient(circle at 50% -10%, ${eHs('session', 0.18)} 0%, transparent 60%), var(--bg)`, borderBottom:'1px solid var(--border-light)', position:'relative', overflow:'hidden' }}>
      <Confetti show={!isInProgress}/>
      <div style={{ position:'relative', zIndex: 2 }}>
        {/* badge + title */}
        <div style={{ textAlign:'center', marginBottom: compact ? 14 : 18 }}>
          {isInProgress ? (
            <span className="mai-pulse" style={{ display:'inline-flex', alignItems:'center', gap: 5, padding:'3px 10px', borderRadius:'var(--r-pill)', background: eHs('session', 0.14), color: eHs('session'), fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800, textTransform:'uppercase', letterSpacing:'.08em', border:`1px solid ${eHs('session', 0.3)}` }}><span style={{ width: 6, height: 6, borderRadius:'50%', background: eHs('session') }}/>In corso · turno {record.turn}</span>
          ) : isTie ? (
            <span style={{ display:'inline-flex', alignItems:'center', gap: 5, padding:'3px 10px', borderRadius:'var(--r-pill)', background: eHs('toolkit', 0.16), color: eHs('toolkit'), fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800, textTransform:'uppercase', letterSpacing:'.08em', border:`1px solid ${eHs('toolkit', 0.3)}` }}>🤝 Pareggio</span>
          ) : (
            <span style={{ display:'inline-flex', alignItems:'center', gap: 5, padding:'3px 10px', borderRadius:'var(--r-pill)', background: eHs('toolkit', 0.16), color: eHs('toolkit'), fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800, textTransform:'uppercase', letterSpacing:'.08em', border:`1px solid ${eHs('toolkit', 0.3)}` }}>🏆 Vittoria</span>
          )}
          <h1 style={{ fontFamily:'var(--f-display)', fontSize: compact ? 22 : 30, fontWeight: 800, letterSpacing:'-.02em', margin:'8px 0 2px', color:'var(--text)' }}>
            {isInProgress ? `${game?.title} in corso` : isTie ? `Pareggio a ${rs[0].score} punti` : `${rs[0].name} vince ${game?.title}`}
          </h1>
          <div style={{ fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-sec)', fontWeight: 700 }}>{record.date} · ⏱ {record.duration} · 👥 {record.playerCount} giocatori</div>
        </div>

        {/* podium */}
        <div style={{ display:'flex', justifyContent:'center', alignItems:'flex-end', gap: compact ? 12 : 22 }}>
          {podiumOrder.map((s) => {
            const place = rs.indexOf(s) + 1;
            const isW = place === 1 && !isInProgress;
            const sharedWin = isTie && s.score === rs[0].score;
            const sz = (place === 1) ? (compact ? 64 : 88) : (compact ? 48 : 64);
            return (
              <div key={s._orig} style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 4 }}>
                {(isW || sharedWin) && <span style={{ fontSize: compact ? 16 : 22 }} aria-hidden="true">👑</span>}
                <div style={{
                  width: sz, height: sz, borderRadius:'50%',
                  background:`linear-gradient(135deg, hsl(${pColor(s.playerId)}, 70%, 62%), hsl(${pColor(s.playerId)}, 60%, 42%))`,
                  color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
                  fontFamily:'var(--f-display)', fontSize: place === 1 ? (compact ? 22 : 30) : (compact ? 16 : 22), fontWeight: 800,
                  border: (isW || sharedWin) ? `3px solid ${eHs('toolkit')}` : `2px solid var(--bg-card)`,
                  boxShadow: (isW || sharedWin) ? `0 6px 20px ${eHs('toolkit', 0.35)}` : 'var(--shadow-sm)',
                  position:'relative',
                }}>
                  {initialsOf(s)}
                  {!isInProgress && (
                    <span style={{ position:'absolute', bottom:-6, right:-6, width: 22, height: 22, borderRadius:'50%', background:'var(--bg-card)', border:`2px solid ${place===1 ? eHs('toolkit') : 'var(--border-strong)'}`, color: place===1 ? eHs('toolkit') : 'var(--text-sec)', display:'flex', alignItems:'center', justifyContent:'center', fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800 }}>{place}</span>
                  )}
                </div>
                <div style={{ fontFamily:'var(--f-display)', fontSize: place === 1 ? (compact ? 14 : 16) : (compact ? 12 : 13), fontWeight: 800, color:'var(--text)' }}>{s.name}</div>
                <div style={{ fontFamily:'var(--f-mono)', fontSize: place === 1 ? (compact ? 18 : 24) : (compact ? 13 : 16), fontWeight: 800, color: (isW || sharedWin) ? eHs('toolkit') : 'var(--text-sec)', fontVariantNumeric:'tabular-nums', lineHeight: 1 }}>{s.score === null ? '—' : s.score}</div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};

// ─── CONNECTION BAR (pips) ─────────────────────────────
const ConnectionBar = ({ record, compact }) => {
  const game = DS.byId[record.game];
  const pips = [
    { entity:'game', label: game?.title },
    { entity:'player', label: `${record.playerCount} giocatori` },
    ...(record.hasChat ? [{ entity:'chat', label: `${record.chatCount} messaggi` }] : [{ entity:'chat', label:'Nessuna chat', empty:true }]),
    { entity:'event', label: record.date },
  ];
  return (
    <div className="mai-cb-scroll" style={{ display:'flex', alignItems:'center', gap: 6, padding: compact ? '10px 16px' : '12px 32px', background:'var(--bg-card)', borderBottom:'1px solid var(--border-light)', overflowX:'auto' }}>
      <span style={{ fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800, color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em', flexShrink: 0, marginRight: 2 }}>Collegamenti</span>
      {pips.map((p, i) => (
        <span key={i} style={{
          display:'inline-flex', alignItems:'center', gap: 5, padding:'4px 10px', borderRadius:'var(--r-pill)',
          background: p.empty ? 'transparent' : eHs(p.entity, 0.1),
          border: p.empty ? `1px dashed ${eHs(p.entity, 0.4)}` : `1px solid ${eHs(p.entity, 0.22)}`,
          color: eHs(p.entity), fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 700,
          whiteSpace:'nowrap', flexShrink: 0, opacity: p.empty ? 0.6 : 1, cursor:'pointer',
        }}><span aria-hidden="true">{DS.EC[p.entity].em}</span>{p.label}</span>
      ))}
    </div>
  );
};

// ─── KPI GRID ──────────────────────────────────────────
const KpiGrid = ({ record, compact }) => {
  const nums = record.scores.map(s => Number(s.score)).filter(n => !isNaN(n));
  const max = nums.length ? Math.max(...nums) : 0;
  const avg = nums.length ? Math.round(nums.reduce((a, b) => a + b, 0) / nums.length) : 0;
  const spread = nums.length ? max - Math.min(...nums) : 0;
  const kpis = [
    { icon:'⏱', label:'Durata', value: record.duration, entity:'event' },
    { icon:'🏆', label:'Punteggio top', value: max, entity:'session' },
    { icon:'📊', label:'Media', value: avg, entity:'player' },
    { icon:'↔', label:'Distacco', value: spread, entity:'game' },
  ];
  return (
    <section>
      <SectionTitle em="📊">Statistiche partita</SectionTitle>
      <div style={{ display:'grid', gridTemplateColumns: compact ? 'repeat(2, 1fr)' : 'repeat(4, 1fr)', gap: compact ? 8 : 12 }}>
        {kpis.map(k => (
          <div key={k.label} style={{ padding: compact ? '10px 12px' : '14px 16px', background:'var(--bg-card)', border:'1px solid var(--border)', borderRadius:'var(--r-md)', display:'flex', alignItems:'center', gap: 10 }}>
            <div style={{ width: compact ? 32 : 38, height: compact ? 32 : 38, borderRadius:'var(--r-sm)', background: eHs(k.entity, 0.12), color: eHs(k.entity), display:'flex', alignItems:'center', justifyContent:'center', fontSize: compact ? 15 : 18, flexShrink: 0 }} aria-hidden="true">{k.icon}</div>
            <div style={{ minWidth: 0 }}>
              <div style={{ color:'var(--text-muted)', fontSize: 9, fontWeight: 700, fontFamily:'var(--f-mono)', textTransform:'uppercase', letterSpacing:'.08em' }}>{k.label}</div>
              <div style={{ color:'var(--text)', fontSize: compact ? 17 : 21, fontWeight: 800, fontFamily:'var(--f-display)', fontVariantNumeric:'tabular-nums', lineHeight: 1.1 }}>{k.value}</div>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
};

// ─── CLASSIFICA (ranking list) ─────────────────────────
const Classifica = ({ record, compact }) => {
  const rs = ranked(record.scores);
  const max = Math.max(...rs.map(s => Number(s.score) || 0), 1);
  return (
    <section>
      <SectionTitle em="🏅" extra={`· ${rs.length} giocatori`}>Classifica</SectionTitle>
      <div style={{ background:'var(--bg-card)', border:'1px solid var(--border)', borderRadius:'var(--r-lg)', overflow:'hidden' }}>
        {rs.map((s, i) => {
          const place = i + 1;
          const isW = place === 1 && record.status === 'completed';
          const p = DS.byId[s.playerId];
          const pct = Math.round(((Number(s.score) || 0) / max) * 100);
          return (
            <div key={s._orig} style={{ display:'flex', alignItems:'center', gap: 12, padding:'12px 14px', borderBottom: i < rs.length - 1 ? '1px solid var(--border-light)' : 'none', background: isW ? eHs('session', 0.05) : 'transparent' }}>
              <span style={{ width: 24, fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800, color: isW ? eHs('toolkit') : 'var(--text-muted)', textAlign:'center', flexShrink: 0 }}>{place === 1 ? '🥇' : place === 2 ? '🥈' : place === 3 ? '🥉' : place}</span>
              <span style={{ width: 38, height: 38, borderRadius:'50%', background:`hsl(${pColor(s.playerId)}, 60%, 55%)`, color:'#fff', display:'flex', alignItems:'center', justifyContent:'center', fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 13, flexShrink: 0, border:'2px solid var(--bg-card)' }}>{initialsOf(s)}</span>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontFamily:'var(--f-display)', fontSize: 13.5, fontWeight: 800, color:'var(--text)', display:'flex', alignItems:'center', gap: 6 }}>{p ? p.title : s.name}{isW && <span style={{ fontFamily:'var(--f-mono)', fontSize: 8.5, fontWeight: 800, color: eHs('toolkit'), background: eHs('toolkit', 0.14), padding:'1px 6px', borderRadius:'var(--r-pill)', textTransform:'uppercase', letterSpacing:'.06em' }}>Vincitore</span>}</div>
                {/* score bar */}
                <div style={{ marginTop: 5, height: 6, borderRadius:'var(--r-pill)', background:'var(--bg-muted)', overflow:'hidden' }}>
                  <div style={{ width: `${pct}%`, height:'100%', borderRadius:'var(--r-pill)', background: isW ? eHs('toolkit') : eHs('session') }}/>
                </div>
              </div>
              <span style={{ fontFamily:'var(--f-mono)', fontSize: 20, fontWeight: 800, color: isW ? eHs('toolkit') : 'var(--text-sec)', fontVariantNumeric:'tabular-nums', flexShrink: 0 }}>{s.score === null ? '—' : s.score}</span>
            </div>
          );
        })}
      </div>
    </section>
  );
};

// ─── DETTAGLIO PUNTEGGI ────────────────────────────────
const ScoringBreakdown = ({ record, compact }) => {
  // categorie sintetiche per Wingspan-like games (mock realistico)
  const categories = ['Uccelli', 'Bonus', 'Fine round', 'Uova', 'Cibo cache', 'Carte tucked'];
  const rs = ranked(record.scores);
  // genera split deterministico per ogni giocatore che somma ~ score
  const splitFor = (total) => {
    const weights = [0.32, 0.2, 0.18, 0.14, 0.09, 0.07];
    let remaining = total, out = [];
    weights.forEach((w, i) => {
      const v = i === weights.length - 1 ? remaining : Math.round(total * w);
      out.push(Math.max(0, v)); remaining -= v;
    });
    return out;
  };
  return (
    <section>
      <SectionTitle em="🔢">Dettaglio punteggi</SectionTitle>
      <div style={{ overflowX:'auto', background:'var(--bg-card)', border:'1px solid var(--border)', borderRadius:'var(--r-lg)' }} className="mai-cb-scroll">
        <table style={{ width:'100%', minWidth: compact ? 460 : 'auto', borderCollapse:'collapse', fontFamily:'var(--f-mono)', fontSize: 12 }}>
          <thead>
            <tr style={{ borderBottom:'1px solid var(--border)' }}>
              <th style={{ textAlign:'left', padding:'10px 14px', fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800, color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.06em' }}>Giocatore</th>
              {categories.map(c => <th key={c} style={{ textAlign:'center', padding:'10px 8px', fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800, color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.04em', whiteSpace:'nowrap' }}>{c}</th>)}
              <th style={{ textAlign:'center', padding:'10px 14px', fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800, color: eHs('session'), textTransform:'uppercase', letterSpacing:'.06em' }}>Tot</th>
            </tr>
          </thead>
          <tbody>
            {rs.map((s, i) => {
              const split = splitFor(Number(s.score) || 0);
              const isW = i === 0 && record.status === 'completed';
              return (
                <tr key={s._orig} style={{ borderBottom: i < rs.length - 1 ? '1px solid var(--border-light)' : 'none', background: isW ? eHs('session', 0.04) : 'transparent' }}>
                  <td style={{ padding:'10px 14px', display:'flex', alignItems:'center', gap: 8 }}>
                    <span style={{ width: 26, height: 26, borderRadius:'50%', background:`hsl(${pColor(s.playerId)}, 60%, 55%)`, color:'#fff', display:'flex', alignItems:'center', justifyContent:'center', fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 10, flexShrink: 0 }}>{initialsOf(s)}</span>
                    <span style={{ fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800, color: isW ? eHs('session') : 'var(--text)', whiteSpace:'nowrap' }}>{s.name}{isW && ' 🏆'}</span>
                  </td>
                  {split.map((v, ci) => <td key={ci} style={{ textAlign:'center', padding:'10px 8px', color:'var(--text-sec)', fontVariantNumeric:'tabular-nums' }}>{v}</td>)}
                  <td style={{ textAlign:'center', padding:'10px 14px', fontWeight: 800, color: isW ? eHs('session') : 'var(--text)', fontVariantNumeric:'tabular-nums' }}>{s.score}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );
};

// ─── NOTE ──────────────────────────────────────────────
const Notes = ({ record, empty }) => (
  <section>
    <SectionTitle em="📝">Note</SectionTitle>
    {empty ? (
      <div style={{ padding:'22px 16px', borderRadius:'var(--r-lg)', background:'var(--bg-card)', border:'1px dashed var(--border-strong)', textAlign:'center' }}>
        <div style={{ fontSize: 28, marginBottom: 6 }} aria-hidden="true">📝</div>
        <div style={{ fontFamily:'var(--f-display)', fontSize: 13.5, fontWeight: 800, color:'var(--text)', marginBottom: 3 }}>Nessuna nota</div>
        <a href="sp4-play-records-edit.html" style={{ fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700, color: eHs('session') }}>+ Aggiungi una nota</a>
      </div>
    ) : (
      <div style={{ padding:'14px 16px', borderRadius:'var(--r-lg)', background:'var(--bg-card)', border:'1px solid var(--border)', borderLeft:`3px solid ${eHs('session')}` }}>
        <p style={{ margin: 0, fontFamily:'var(--f-body)', fontSize: 13.5, lineHeight: 1.6, color:'var(--text-sec)' }}>{record.notes}</p>
      </div>
    )}
  </section>
);

// ─── BODY ──────────────────────────────────────────────
const DetailBody = ({ stateOverride, variant, compact }) => {
  if (stateOverride === 'loading') {
    return (
      <div style={{ padding: compact ? 14 : 24, display:'flex', flexDirection:'column', gap: 14 }}>
        <div className="mai-shimmer" style={{ height: compact ? 200 : 260, borderRadius:'var(--r-xl)', background:'var(--bg-muted)' }}/>
        <div className="mai-shimmer" style={{ height: 90, borderRadius:'var(--r-lg)', background:'var(--bg-muted)' }}/>
        <div className="mai-shimmer" style={{ height: 220, borderRadius:'var(--r-lg)', background:'var(--bg-muted)' }}/>
      </div>
    );
  }
  if (stateOverride === 'error') {
    return (
      <div style={{ padding:'40px 24px', textAlign:'center', margin: compact ? 14 : 24, background:'hsl(var(--c-danger) / .06)', border:'1px solid hsl(var(--c-danger) / .25)', borderRadius:'var(--r-xl)' }}>
        <div style={{ fontSize: 32, marginBottom: 8 }} aria-hidden="true">⚠</div>
        <h3 style={{ fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800, margin:'0 0 4px', color:'var(--text)' }}>Partita non trovata</h3>
        <p style={{ fontSize: 12.5, color:'var(--text-muted)', margin:'0 0 14px' }}>Impossibile caricare il dettaglio della partita.</p>
        <button type="button" style={{ padding:'8px 14px', borderRadius:'var(--r-md)', background:'hsl(var(--c-danger))', color:'#fff', border:'none', fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800, cursor:'pointer' }}>↻ Riprova</button>
      </div>
    );
  }

  const record = variant === 'planned' ? DS.byId['pr9']
    : variant === 'inprogress' ? DS.byId['pr3']
    : variant === 'tied' ? DS.byId['pr5']
    : REC;
  const emptyNotes = stateOverride === 'empty-notes';

  return (
    <div style={{ padding: compact ? '14px 14px 32px' : '24px 32px 64px', display:'flex', flexDirection:'column', gap: compact ? 18 : 24, maxWidth: 1080, margin:'0 auto', width:'100%' }}>
      <KpiGrid record={record} compact={compact}/>
      <Classifica record={record} compact={compact}/>
      {variant !== 'planned' && <ScoringBreakdown record={record} compact={compact}/>}
      <Notes record={record} empty={emptyNotes}/>
      {variant !== 'inprogress' && variant !== 'planned' && (
        <section style={{ padding:'16px 18px', borderRadius:'var(--r-xl)', background:`linear-gradient(135deg, ${eHs('game', 0.1)}, ${eHs('session', 0.1)})`, border:`1px solid ${eHs('game', 0.3)}`, display:'flex', alignItems:'center', justifyContent:'space-between', gap: 14, flexWrap:'wrap' }}>
          <div style={{ flex: 1, minWidth: 200 }}>
            <h3 style={{ fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800, color:'var(--text)', margin:'0 0 3px' }}>🎲 Pronti per la rivincita?</h3>
            <p style={{ fontFamily:'var(--f-body)', fontSize: 12.5, color:'var(--text-sec)', margin: 0 }}>Stessi giocatori e gioco.</p>
          </div>
          <a href="sp4-play-records-new.html" style={{ padding:'10px 16px', borderRadius:'var(--r-md)', background: eHs('session'), color:'#fff', fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, boxShadow:`0 4px 14px ${eHs('session', 0.4)}` }}>▶ Registra rivincita</a>
        </section>
      )}
    </div>
  );
};

// ─── FRAMES ────────────────────────────────────────────
const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}><span>15:33</span><div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">88%</span></div></div>
);

const TopNav = ({ record, compact }) => {
  const game = DS.byId[record.game];
  return (
    <div style={{ display:'flex', alignItems:'center', gap: 10, padding: compact ? '8px 12px' : '10px 24px', background:'var(--glass-bg)', backdropFilter:'blur(12px)', borderBottom:'1px solid var(--border)' }}>
      <a href="sp4-play-records-index.html" aria-label="Indietro" style={{ width: 30, height: 30, borderRadius:'var(--r-md)', background:'var(--bg-card)', border:'1px solid var(--border)', color:'var(--text-sec)', fontSize: 13, fontWeight: 800, display:'flex', alignItems:'center', justifyContent:'center' }}>←</a>
      <div style={{ display:'inline-flex', alignItems:'center', gap: 5, padding:'2px 8px', borderRadius:'var(--r-pill)', background: eHs('game', 0.1), color: eHs('game'), border:`1px solid ${eHs('game', 0.3)}`, fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800, textTransform:'uppercase', letterSpacing:'.05em' }}>{game?.coverEmoji} {game?.title}</div>
      <div style={{ flex: 1 }}/>
      <a href="sp4-play-records-edit.html" style={{ padding:'6px 12px', borderRadius:'var(--r-md)', background:'var(--bg-card)', border:'1px solid var(--border)', color:'var(--text-sec)', fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 700 }}>✎ Modifica</a>
    </div>
  );
};

const recordFor = (variant) => variant === 'planned' ? DS.byId['pr9'] : variant === 'inprogress' ? DS.byId['pr3'] : variant === 'tied' ? DS.byId['pr5'] : REC;

const PhoneScreen = ({ stateOverride, variant }) => {
  const record = recordFor(variant);
  return (
    <>
      <PhoneSbar/>
      <div style={{ flex: 1, overflow:'auto', display:'flex', flexDirection:'column', background:'var(--bg)' }}>
        <TopNav record={record} compact/>
        {!stateOverride && <><HeroPodium record={record} variant={variant} compact/><ConnectionBar record={record} compact/></>}
        <DetailBody stateOverride={stateOverride} variant={variant} compact/>
      </div>
    </>
  );
};

const DesktopFrameInner = ({ stateOverride, variant }) => {
  const record = recordFor(variant);
  return (
    <div style={{ display:'flex', flexDirection:'column', minHeight: 720, background:'var(--bg)' }}>
      <TopNav record={record}/>
      {!stateOverride && <><HeroPodium record={record} variant={variant}/><ConnectionBar record={record}/></>}
      <DetailBody stateOverride={stateOverride} variant={variant}/>
    </div>
  );
};

const PhoneShell = ({ label, desc, children }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 10 }}>
    <div style={{ fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)', textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700 }}>{label}</div>
    <div className="phone">{children}</div>
    {desc && <div style={{ fontSize: 11, color:'var(--text-muted)', maxWidth: 340, textAlign:'center', lineHeight: 1.55 }}>{desc}</div>}
  </div>
);

const DesktopFrame = ({ label, desc, dark, children }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 12, width:'100%' }} data-theme={dark ? 'dark' : undefined}>
    <div style={{ fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)', textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700 }}>{label}{dark && <span style={{ color: eHs('session'), marginLeft: 6 }}>· dark</span>}</div>
    <div className="desktop-frame">
      <div className="desktop-bar"><span className="traffic"/><span className="traffic"/><span className="traffic"/><span className="url">meepleai.app/play-records/pr1</span></div>
      {children}
    </div>
    {desc && <div style={{ fontSize: 11, color:'var(--text-muted)', maxWidth: 720, textAlign:'center', lineHeight: 1.55 }}>{desc}</div>}
  </div>
);

const MOBILE_STATES = [
  { id:'m1', variant:'default', label:'01 · Default · vittoria', desc:'Hero podio 3-place con confetti + corona toolkit 1°. ConnectionBar pip. KPI + classifica + dettaglio + note + rivincita.' },
  { id:'m2', variant:'tied', label:'02 · Pareggio', desc:'Banner "🤝 Pareggio" + 2 corone shared 1°. Hero score-based title.' },
  { id:'m3', variant:'inprogress', label:'03 · In corso', desc:'Badge "In corso · turno" pulsante, no confetti, no rivincita. Classifica provvisoria.' },
  { id:'m4', variant:'planned', label:'04 · Pianificata (empty)', desc:'Pre-partita: hero ridotto entity-event + CTA Avvia. Nessun punteggio ancora.' },
  { id:'m5', variant:'default', state:'empty-notes', label:'05 · Note vuote', desc:'Sezione note in empty-state + CTA Aggiungi nota.' },
  { id:'m6', state:'loading', label:'06 · Loading', desc:'Skeleton hero 200 + KPI + tabella shimmer.' },
  { id:'m7', state:'error', label:'07 · Error', desc:'Partita non trovata + CTA Riprova in danger color.' },
];

function ThemeToggle() {
  const [dark, setDark] = useState(false);
  useEffect(() => { document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light'); }, [dark]);
  return (
    <button onClick={() => setDark(d => !d)} className="theme-toggle" aria-label={dark ? 'Tema chiaro' : 'Tema scuro'}>
      <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span><span>{dark ? 'Dark' : 'Light'}</span>
    </button>
  );
}

function App() {
  return (
    <div className="stage">
      <ThemeToggle/>
      <div className="stage-wrap">
        <div style={{ fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em', marginBottom: 8 }}>SP4 · /play-records · Detail 🏆</div>
        <h1>Play records — /play-records/[id]</h1>
        <p className="lead">
          Pagina dettaglio partita. <strong>Hero podio 3-place</strong> con confetti CSS-only + ConnectionBar pip cliccabili.
          Body: KPI grid · <strong>classifica</strong> (rank/avatar/punti + barra) · <strong>dettaglio punteggi</strong> (tabella per-categoria) · note · rivincita.
          Varianti: vittoria · pareggio · in corso · pianificata. Tone festoso ma elegante, prefers-reduced-motion rispettato.
        </p>

        <div className="section-label">Mobile · 375 — 7 stati</div>
        <div className="phones-grid">
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc}>
              <PhoneScreen stateOverride={s.state} variant={s.variant || 'default'}/>
            </PhoneShell>
          ))}
        </div>

        <div className="section-label">Desktop · 1280 — 5 stati chiave</div>
        <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
          <DesktopFrame label="08 · Desktop · Default vittoria" desc="Container 1080. Podio hero 88/64 + corona. ConnectionBar. KPI 4-col + classifica + tabella dettaglio + note + rivincita.">
            <DesktopFrameInner variant="default"/>
          </DesktopFrame>
          <DesktopFrame label="09 · Desktop · Pareggio" desc="2 corone shared 1°, titolo score-based.">
            <DesktopFrameInner variant="tied"/>
          </DesktopFrame>
          <DesktopFrame label="10 · Desktop · Dark mode" dark desc="Confetti glow + accent entity-session/toolkit verificati in dark.">
            <DesktopFrameInner variant="default"/>
          </DesktopFrame>
          <DesktopFrame label="11 · Desktop · In corso" desc="Badge live, classifica provvisoria, no rivincita.">
            <DesktopFrameInner variant="inprogress"/>
          </DesktopFrame>
          <DesktopFrame label="12 · Desktop · Pianificata (empty)" desc="Pre-partita, hero entity-event + CTA Avvia, nessun punteggio.">
            <DesktopFrameInner variant="planned"/>
          </DesktopFrame>
        </div>
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);

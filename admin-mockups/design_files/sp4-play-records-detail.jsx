/* MeepleAI SP4 — /play-records/[id] · DETAIL
   Route: /play-records/[id]  (read-only display dettaglio partita)
   File: admin-mockups/design_files/sp4-play-records-detail.{html,jsx}
   Modello: sp4-session-summary — Hero podio + ConnectionBar + classifica + dettaglio punteggi + note + foto.
   Entity dominante: session 🎯 (240 60% 55%). Tone: festoso ma elegante.

   ── Stati canonici (G7 SessionStateRenderer, PR 2357) ──────────────
   Export per-stato (anchor #state-NN-* nell'HTML gallery):
     State01_Default  → state-01-default   (dettaglio completo as-shipped)
     State02_Empty    → state-02-empty     (record minimale · dati core only · null parziali)
     State03_Loading  → state-03-loading   (skeleton primitives · aria-busy)
     State04_Error    → state-04-error     (banner alert · retry · torna lista · dismiss)
   state-05-sse → SKIPPED: il detail NON è SSE-driven (fetch-once read-only). Vedi nota in fondo.

   FREEZE: zero hex/hsl numerico hardcoded per gli entity color → solo token --c-*
   via eHs() (abbreviazione di entityHsl). Esente: color:'#fff' su background eHs (pattern .e-bg).
*/
const { useState, useEffect } = React;
const DS = window.DS;

// eHs(entity, alpha?) — risolve SEMPRE sui token CSS (--c-*), così il colore
// segue automaticamente light/dark ([data-theme]) ed è FREEZE-clean (nessun
// valore hsl numerico hardcoded nel sorgente del mockup).
const eHs = (entity, alpha) =>
  alpha === undefined ? `hsl(var(--c-${entity}))` : `hsl(var(--c-${entity}) / ${alpha})`;

// ── ranking + player helpers ───────────────────────────
const ranked = (scores) => [...scores]
  .map((s, i) => ({ ...s, _orig: i }))
  .sort((a, b) => {
    const av = a.score === null ? -Infinity : Number(a.score);
    const bv = b.score === null ? -Infinity : Number(b.score);
    return bv - av;
  });

const pColor = (playerId, fallbackHue = 240) => {
  const p = DS.byId[playerId];
  return p ? p.color : fallbackHue; // hue identità giocatore (dato DS, non entity-token)
};
const initialsOf = (s) => {
  const p = DS.byId[s.playerId];
  return p ? p.initials : s.name.slice(0, 2).toUpperCase();
};
const playerTitle = (s) => {
  const p = DS.byId[s.playerId];
  return p ? p.title : s.name;
};

// #2496 — Asse A invariante #16: roster player mix (User-linked + guest).
// Reads the `kind` flag injected in sp4-play-records-data.js; defaults to 'user'
// for the 5 core players in data.js (Marco/Sara/Luca/Giulia/Andrea).
const playerKind = (s) => {
  const p = DS.byId[s.playerId];
  return (p && p.kind) || 'user';
};

// Tiny pill badge — distinguishes account-linked players ("Tu") from guest tags ("Ospite").
// Sits next to the player title in Classifica + Hero podium so the roster mix is read at-a-glance.
const PlayerKindBadge = ({ kind, small }) => {
  const isGuest = kind === 'guest';
  const label = isGuest ? 'Ospite' : 'Account';
  const ent = isGuest ? null : 'player';
  return (
    <span
      title={isGuest ? 'Giocatore ospite — non collegato a un account' : 'Giocatore con account collegato'}
      style={{
        fontFamily: 'var(--f-mono)',
        fontSize: small ? 8 : 9,
        fontWeight: 800,
        letterSpacing: '.06em',
        textTransform: 'uppercase',
        padding: small ? '1px 5px' : '2px 7px',
        borderRadius: 'var(--r-pill)',
        color: isGuest ? 'var(--text-sec)' : eHs(ent),
        background: isGuest ? 'var(--bg-muted)' : eHs(ent, 0.12),
        border: isGuest ? '1px solid var(--border-light)' : `1px solid ${eHs(ent, 0.25)}`,
        whiteSpace: 'nowrap',
      }}
    >
      {label}
    </span>
  );
};

// ── Record demo ─────────────────────────────────────────
const REC = DS.byId['pr1'];
const REGISTRANT = (DS.stats && DS.stats.user) || 'Marco R.';

// Default (as-shipped): record completo + foto allegata + meta registrazione.
const REC_DEFAULT = {
  ...REC,
  photo: { caption: 'Tavolo a fine partita', emoji: '📸' },
  registeredOn: '17 mag 2026 · 22:14',
  registeredBy: REGISTRANT,
};

// Empty (record minimale): stesso pr1 ma foto:null + notes:'' + 2 score null + nessun winner.
const REC_EMPTY = {
  ...REC,
  photo: null,
  notes: '',
  registeredOn: '17 mag 2026 · 22:05',
  registeredBy: REGISTRANT,
  scores: REC.scores.map((s, i) => i >= 2 ? { ...s, score: null, winner: false } : { ...s, winner: false }),
};

// ── Score value (null → "—" + aria) ────────────────────
const ScoreVal = ({ value, style }) =>
  (value === null || value === undefined)
    ? <span aria-label="Punteggio non registrato" style={style}>—</span>
    : <span style={style}>{value}</span>;

// ── SECTION TITLE ──────────────────────────────────────
const SectionTitle = ({ em, children, extra }) => (
  <h2 style={{ fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800, color:'var(--text)', margin:'0 0 10px', display:'inline-flex', alignItems:'center', gap: 7 }}>
    <span aria-hidden="true">{em}</span>{children}
    {extra && <span style={{ fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 700, color:'var(--text-muted)' }}>{extra}</span>}
  </h2>
);

// ── Confetti (CSS-only, decorativo) ────────────────────
const Confetti = () => {
  const pieces = Array.from({ length: 20 });
  const ents = ['session', 'toolkit', 'game', 'event', 'player'];
  return (
    <div aria-hidden="true" style={{ position:'absolute', inset: 0, overflow:'hidden', pointerEvents:'none', zIndex: 1 }}>
      {pieces.map((_, i) => (
        <span key={i} className="sp4-confetti" style={{
          position:'absolute', top: 0, left: `${(i * 5.1) % 100}%`,
          width: 7, height: 11, borderRadius: 2, background: eHs(ents[i % ents.length]),
          animationDelay: `${(i % 7) * 0.16}s`,
        }}/>
      ))}
    </div>
  );
};

// ── HERO PODIUM ────────────────────────────────────────
const HeroPodium = ({ record, compact, minimal }) => {
  const game = DS.byId[record.game];
  const rs = ranked(record.scores);
  const top3 = rs.slice(0, 3);
  const podiumOrder = top3.length >= 3 ? [top3[1], top3[0], top3[2]] : top3.length === 2 ? [top3[1], top3[0]] : [top3[0]];

  return (
    <div aria-label={minimal ? 'Esito partita non disponibile' : 'Vincitore partita'} style={{ padding: compact ? '20px 16px 18px' : '28px 32px 24px', background:`radial-gradient(circle at 50% -10%, ${eHs('session', 0.18)} 0%, transparent 60%), var(--bg)`, borderBottom:'1px solid var(--border-light)', position:'relative', overflow:'hidden' }}>
      {!minimal && <Confetti/>}
      <div style={{ position:'relative', zIndex: 2 }}>
        <div style={{ textAlign:'center', marginBottom: compact ? 14 : 18 }}>
          {minimal ? (
            <span style={{ display:'inline-flex', alignItems:'center', gap: 5, padding:'3px 10px', borderRadius:'var(--r-pill)', background: eHs('session', 0.12), color: eHs('session'), fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800, textTransform:'uppercase', letterSpacing:'.08em', border:`1px solid ${eHs('session', 0.3)}` }}>ℹ️ Esito non disponibile</span>
          ) : (
            <span style={{ display:'inline-flex', alignItems:'center', gap: 5, padding:'3px 10px', borderRadius:'var(--r-pill)', background: eHs('toolkit', 0.16), color: eHs('toolkit'), fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800, textTransform:'uppercase', letterSpacing:'.08em', border:`1px solid ${eHs('toolkit', 0.3)}` }}>🏆 Vittoria</span>
          )}
          <h1 style={{ fontFamily:'var(--f-display)', fontSize: compact ? 22 : 30, fontWeight: 800, letterSpacing:'-.02em', margin:'13px 0 3px', color:'var(--text)' }}>
            {minimal ? game?.title : `${rs[0].name} vince ${game?.title}`}
          </h1>
          <div style={{ fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-sec)', fontWeight: 700 }}>{record.date} · ⏱ {record.duration} · 👥 {record.playerCount} giocatori</div>
        </div>

        <div style={{ display:'flex', justifyContent:'center', alignItems:'flex-end', gap: compact ? 12 : 22 }}>
          {podiumOrder.map((s) => {
            const place = rs.indexOf(s) + 1;
            const isW = place === 1 && !minimal;
            const neutral = minimal && place === 1;
            const sz = (place === 1) ? (compact ? 64 : 88) : (compact ? 48 : 64);
            return (
              <div key={s._orig} style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 4 }}>
                {isW && <span style={{ fontSize: compact ? 16 : 22 }} aria-hidden="true">👑</span>}
                <div style={{
                  width: sz, height: sz, borderRadius:'50%',
                  background: neutral ? 'var(--bg-muted)' : `linear-gradient(135deg, hsl(${pColor(s.playerId)}, 70%, 62%), hsl(${pColor(s.playerId)}, 60%, 42%))`,
                  color: neutral ? 'var(--text-muted)' : '#fff', display:'flex', alignItems:'center', justifyContent:'center',
                  fontFamily:'var(--f-display)', fontSize: place === 1 ? (compact ? 22 : 30) : (compact ? 16 : 22), fontWeight: 800,
                  border: isW ? `3px solid ${eHs('toolkit')}` : neutral ? '2px dashed var(--border-strong)' : '2px solid var(--bg-card)',
                  boxShadow: isW ? `0 6px 20px ${eHs('toolkit', 0.35)}` : 'var(--shadow-sm)',
                  position:'relative',
                }}>
                  {neutral ? '—' : initialsOf(s)}
                  <span style={{ position:'absolute', bottom:-6, right:-6, width: 22, height: 22, borderRadius:'50%', background:'var(--bg-card)', border:`2px solid ${isW ? eHs('toolkit') : 'var(--border-strong)'}`, color: isW ? eHs('toolkit') : 'var(--text-sec)', display:'flex', alignItems:'center', justifyContent:'center', fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800 }}>{place}</span>
                </div>
                <div style={{ fontFamily:'var(--f-display)', fontSize: place === 1 ? (compact ? 14 : 16) : (compact ? 12 : 13), fontWeight: 800, color:'var(--text)' }}>{neutral ? '—' : s.name}</div>
                {/* #2496 Asse A inv #16 — podium roster mix badge (small variant for the tighter podium layout) */}
                {!neutral && <PlayerKindBadge kind={playerKind(s)} small/>}
                <ScoreVal value={neutral ? null : s.score} style={{ fontFamily:'var(--f-mono)', fontSize: place === 1 ? (compact ? 18 : 24) : (compact ? 13 : 16), fontWeight: 800, color: isW ? eHs('toolkit') : 'var(--text-sec)', fontVariantNumeric:'tabular-nums', lineHeight: 1 }}/>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};

// ── INFO BANNER (empty · record minimale) ──────────────
const InfoBanner = ({ compact }) => (
  <div role="status" aria-live="polite" style={{
    display:'flex', alignItems:'center', gap: 10,
    padding: compact ? '11px 14px' : '12px 18px',
    background: eHs('session', 0.06), borderLeft:`4px solid ${eHs('session', 0.45)}`,
    border:'1px solid var(--border-light)', borderLeftWidth: 4, borderRadius:'var(--r-md)',
  }}>
    <span aria-hidden="true" style={{ fontSize: compact ? 16 : 18, lineHeight: 1 }}>ℹ️</span>
    <div style={{ fontFamily:'var(--f-display)', fontSize: compact ? 12.5 : 13.5, fontWeight: 700, color:'var(--text-sec)' }}>
      Record minimale <span style={{ color:'var(--text-muted)' }}>· alcuni dati non sono stati registrati</span>
    </div>
  </div>
);

// ── CONNECTION BAR (pips game/event/session/player) ────
const ConnectionBar = ({ record, compact }) => {
  const game = DS.byId[record.game];
  const pips = [
    { entity:'game',    label: game?.title },
    { entity:'player',  label: `${record.playerCount} giocatori` },
    { entity:'session', label: record.when || 'Partita' },
    { entity:'event',   label: record.date },
  ];
  return (
    <div className="mai-cb-scroll" style={{ display:'flex', alignItems:'center', gap: 6, padding: compact ? '10px 16px' : '12px 32px', background:'var(--bg-card)', borderBottom:'1px solid var(--border-light)', overflowX: compact ? 'visible' : 'auto', flexWrap: compact ? 'wrap' : 'nowrap' }}>
      <span style={{ fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800, color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em', flexShrink: 0, marginRight: 2 }}>Collegamenti</span>
      {pips.map((p, i) => (
        <span key={i} style={{
          display:'inline-flex', alignItems:'center', gap: 5, padding:'4px 10px', borderRadius:'var(--r-pill)',
          background: eHs(p.entity, 0.1), border:`1px solid ${eHs(p.entity, 0.22)}`,
          color: eHs(p.entity), fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 700,
          whiteSpace:'nowrap', flexShrink: 0,
        }}><span aria-hidden="true">{DS.EC[p.entity].em}</span>{p.label}</span>
      ))}
    </div>
  );
};

// ── CLASSIFICA (role=list · podium 1°/2°/3°/4°) ────────
const Classifica = ({ record, compact, minimal }) => {
  const rs = ranked(record.scores);
  const nums = rs.map(s => s.score === null ? 0 : Number(s.score));
  const max = Math.max(...nums, 1);
  return (
    <section>
      <SectionTitle em="🏅" extra={`· ${rs.length} giocatori`}>Classifica</SectionTitle>
      <div role="list" style={{ background:'var(--bg-card)', border:'1px solid var(--border)', borderRadius:'var(--r-lg)', overflow:'hidden' }}>
        {rs.map((s, i) => {
          const place = i + 1;
          const isNull = s.score === null;
          const isW = place === 1 && !minimal && !isNull;
          const pct = isNull ? 0 : Math.round((Number(s.score) / max) * 100);
          return (
            <div role="listitem" key={s._orig} style={{ display:'flex', alignItems:'center', gap: 12, padding:'12px 14px', borderBottom: i < rs.length - 1 ? '1px solid var(--border-light)' : 'none', background: isW ? eHs('session', 0.05) : 'transparent', opacity: isNull ? 0.62 : 1 }}>
              <span style={{ width: 24, fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800, color: isW ? eHs('toolkit') : 'var(--text-muted)', textAlign:'center', flexShrink: 0 }} aria-hidden="true">{place === 1 ? '🥇' : place === 2 ? '🥈' : place === 3 ? '🥉' : place}</span>
              <span style={{ width: 38, height: 38, borderRadius:'50%', background:`hsl(${pColor(s.playerId)}, 60%, 55%)`, color:'#fff', display:'flex', alignItems:'center', justifyContent:'center', fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 13, flexShrink: 0, border:'2px solid var(--bg-card)' }} aria-hidden="true">{initialsOf(s)}</span>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontFamily:'var(--f-display)', fontSize: 13.5, fontWeight: 800, color:'var(--text)', display:'flex', alignItems:'center', gap: 6, flexWrap:'wrap' }}>
                  {playerTitle(s)}
                  {/* #2496 Asse A inv #16 — User/Guest badge so the roster mix is visible per row */}
                  <PlayerKindBadge kind={playerKind(s)}/>
                  {isW && <span style={{ fontFamily:'var(--f-mono)', fontSize: 8.5, fontWeight: 800, color: eHs('toolkit'), background: eHs('toolkit', 0.14), padding:'1px 6px', borderRadius:'var(--r-pill)', textTransform:'uppercase', letterSpacing:'.06em' }}>Vincitore</span>}
                </div>
                <div style={{ marginTop: 5, height: 6, borderRadius:'var(--r-pill)', background:'var(--bg-muted)', overflow:'hidden' }}>
                  <div style={{ width: `${pct}%`, height:'100%', borderRadius:'var(--r-pill)', background: isW ? eHs('toolkit') : eHs('session') }}/>
                </div>
              </div>
              <ScoreVal value={s.score} style={{ fontFamily:'var(--f-mono)', fontSize: 20, fontWeight: 800, color: isW ? eHs('toolkit') : 'var(--text-sec)', fontVariantNumeric:'tabular-nums', flexShrink: 0 }}/>
            </div>
          );
        })}
      </div>
    </section>
  );
};

// ── DETTAGLIO PUNTEGGI (tabellare) ─────────────────────
const ScoringBreakdown = ({ record, compact }) => {
  const categories = ['Uccelli', 'Bonus', 'Fine round', 'Uova', 'Cibo cache', 'Carte tucked'];
  const rs = ranked(record.scores);
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
              const isNull = s.score === null;
              const split = isNull ? null : splitFor(Number(s.score));
              const isW = i === 0 && !isNull && record.status === 'completed';
              return (
                <tr key={s._orig} style={{ borderBottom: i < rs.length - 1 ? '1px solid var(--border-light)' : 'none', background: isW ? eHs('session', 0.04) : 'transparent', opacity: isNull ? 0.6 : 1 }}>
                  <td style={{ padding:'10px 14px' }}>
                    <span style={{ display:'inline-flex', alignItems:'center', gap: 8 }}>
                      <span style={{ width: 26, height: 26, borderRadius:'50%', background:`hsl(${pColor(s.playerId)}, 60%, 55%)`, color:'#fff', display:'flex', alignItems:'center', justifyContent:'center', fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 10, flexShrink: 0 }} aria-hidden="true">{initialsOf(s)}</span>
                      <span style={{ fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800, color: isW ? eHs('session') : 'var(--text)', whiteSpace:'nowrap' }}>{s.name}{isW && ' 🏆'}</span>
                    </span>
                  </td>
                  {categories.map((c, ci) => (
                    <td key={ci} style={{ textAlign:'center', padding:'10px 8px', color:'var(--text-sec)', fontVariantNumeric:'tabular-nums' }}>{isNull ? <span aria-label="Punteggio non registrato">—</span> : split[ci]}</td>
                  ))}
                  <td style={{ textAlign:'center', padding:'10px 14px', fontWeight: 800, color: isW ? eHs('session') : 'var(--text)', fontVariantNumeric:'tabular-nums' }}><ScoreVal value={s.score}/></td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );
};

// ── NOTE ───────────────────────────────────────────────
const Notes = ({ record, empty }) => (
  <section>
    <SectionTitle em="📝">Note</SectionTitle>
    {empty ? (
      <div style={{ padding:'22px 16px', borderRadius:'var(--r-lg)', background:'var(--bg-card)', border:'1px dashed var(--border-strong)', textAlign:'center' }}>
        <div style={{ fontSize: 28, marginBottom: 6 }} aria-hidden="true">📝</div>
        <div style={{ fontFamily:'var(--f-display)', fontSize: 13.5, fontWeight: 800, color:'var(--text)', marginBottom: 5 }}>Nessuna nota</div>
        <a href="sp4-play-records-edit.html" style={{ fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700, color: eHs('session'), opacity: .8 }}>+ Aggiungi una nota</a>
      </div>
    ) : (
      <div style={{ padding:'14px 16px', borderRadius:'var(--r-lg)', background:'var(--bg-card)', border:'1px solid var(--border)', borderLeft:`3px solid ${eHs('session')}` }}>
        <p style={{ margin: 0, fontFamily:'var(--f-body)', fontSize: 13.5, lineHeight: 1.6, color:'var(--text-sec)' }}>{record.notes}</p>
      </div>
    )}
  </section>
);

// ── FOTO ───────────────────────────────────────────────
const PhotoSection = ({ record, empty, compact }) => (
  <section>
    <SectionTitle em="📷">Foto</SectionTitle>
    {empty ? (
      <div style={{ padding:'22px 16px', borderRadius:'var(--r-lg)', background:'var(--bg-card)', border:'1px dashed var(--border-strong)', textAlign:'center' }}>
        <div style={{ fontSize: 28, marginBottom: 6 }} aria-hidden="true">📷</div>
        <div style={{ fontFamily:'var(--f-display)', fontSize: 13.5, fontWeight: 800, color:'var(--text)', marginBottom: 5 }}>Nessuna foto allegata</div>
        <a href="sp4-play-records-edit.html" style={{ fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700, color: eHs('session'), opacity: .8 }}>+ Aggiungi foto</a>
      </div>
    ) : (
      <figure style={{ margin: 0, borderRadius:'var(--r-lg)', overflow:'hidden', border:'1px solid var(--border)', background:'var(--bg-card)' }}>
        <div role="img" aria-label={`Foto della partita: ${record.photo.caption}`} style={{ height: compact ? 150 : 200, display:'flex', alignItems:'center', justifyContent:'center', fontSize: compact ? 44 : 58, background:`linear-gradient(135deg, ${eHs('session', 0.14)}, ${eHs('game', 0.12)})` }}>
          <span aria-hidden="true">{DS.byId[record.game]?.coverEmoji || '🎲'}</span>
        </div>
        <figcaption style={{ display:'flex', alignItems:'center', gap: 7, padding:'9px 14px', fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)', fontWeight: 700 }}>
          <span aria-hidden="true">{record.photo.emoji}</span>{record.photo.caption}
        </figcaption>
      </figure>
    )}
  </section>
);

// ── META FOOTER (data registrazione · registratore) ────
const MetaFooter = ({ record, compact }) => (
  <div style={{ display:'flex', alignItems:'center', flexWrap:'wrap', gap: compact ? '6px 14px' : '6px 22px', padding: compact ? '14px 16px' : '16px 32px', borderTop:'1px solid var(--border-light)', background:'var(--bg-card)' }}>
    <span style={{ display:'inline-flex', alignItems:'center', gap: 6, fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)', fontWeight: 700 }}>
      <span aria-hidden="true" style={{ color: eHs('event') }}>📅</span>Registrata il <strong style={{ color:'var(--text-sec)' }}>{record.registeredOn}</strong>
    </span>
    <span style={{ display:'inline-flex', alignItems:'center', gap: 6, fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)', fontWeight: 700 }}>
      <span aria-hidden="true" style={{ color: eHs('player') }}>👤</span>da <strong style={{ color:'var(--text-sec)' }}>{record.registeredBy}</strong>
    </span>
    <span style={{ marginLeft:'auto', fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)', fontWeight: 700, letterSpacing:'.04em' }}>#{record.id}</span>
  </div>
);

// ── TOP NAV (in-app chrome) ────────────────────────────
const TopNav = ({ record, compact, loading }) => {
  const game = record ? DS.byId[record.game] : null;
  return (
    <div style={{ display:'flex', alignItems:'center', gap: 10, padding: compact ? '8px 12px' : '10px 24px', background:'var(--glass-bg)', backdropFilter:'blur(12px)', borderBottom:'1px solid var(--border)' }}>
      <a href="sp4-play-records-index.html" aria-label="Torna alla lista partite" style={{ width: 30, height: 30, borderRadius:'var(--r-md)', background:'var(--bg-card)', border:'1px solid var(--border)', color:'var(--text-sec)', fontSize: 13, fontWeight: 800, display:'flex', alignItems:'center', justifyContent:'center', flexShrink: 0 }}>←</a>
      {loading ? (
        <span aria-hidden="true" className="skel" style={{ width: compact ? 96 : 132, height: 20, borderRadius:'var(--r-pill)', background: eHs('session', 0.08) }}/>
      ) : game ? (
        <div style={{ display:'inline-flex', alignItems:'center', gap: 5, padding:'2px 8px', borderRadius:'var(--r-pill)', background: eHs('game', 0.1), color: eHs('game'), border:`1px solid ${eHs('game', 0.3)}`, fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800, textTransform:'uppercase', letterSpacing:'.05em' }}>{game.coverEmoji} {game.title}</div>
      ) : (
        <span style={{ fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 800, color:'var(--text-sec)' }}>Dettaglio partita</span>
      )}
      <div style={{ flex: 1 }}/>
      <a href="sp4-play-records-edit.html" style={{ padding:'6px 12px', borderRadius:'var(--r-md)', background:'var(--bg-card)', border:'1px solid var(--border)', color:'var(--text-sec)', fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 700, flexShrink: 0 }}>✎ Modifica</a>
    </div>
  );
};

const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}>
    <span>15:33</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">88%</span></div>
  </div>
);

const Stack = ({ children, gap = 20 }) => (
  <div style={{ display:'flex', flexDirection:'column', gap }}>{children}</div>
);

// ── DETAIL VIEW (responsive: mobile stack / desktop 2-col) ──
// Mobile 375 : hero → connection bar → [info] → classifica → punteggi → note → foto → meta
// Desktop 1440: hero + connection bar full-width; 2-col → [classifica] | [punteggi · note · foto]; meta full-width.
const DetailView = ({ record, compact, minimal, emptyMedia, infoBanner }) => {
  const classifica = <Classifica record={record} compact={compact} minimal={minimal}/>;
  const scoring = <ScoringBreakdown record={record} compact={compact}/>;
  const notes = <Notes record={record} empty={emptyMedia}/>;
  const photo = <PhotoSection record={record} empty={emptyMedia} compact={compact}/>;

  if (compact) {
    return (
      <div style={{ flex: 1, overflowY:'auto', minHeight: 0 }}>
        <HeroPodium record={record} compact minimal={minimal}/>
        <ConnectionBar record={record} compact/>
        <div style={{ padding:'14px 14px 24px', display:'flex', flexDirection:'column', gap: 16 }}>
          {infoBanner && <InfoBanner compact/>}
          {classifica}{scoring}{notes}{photo}
        </div>
        <MetaFooter record={record} compact/>
      </div>
    );
  }
  return (
    <>
      <HeroPodium record={record} minimal={minimal}/>
      <ConnectionBar record={record}/>
      <div style={{ padding:'24px 32px 12px', maxWidth: 1280, margin:'0 auto', width:'100%' }}>
        {infoBanner && <div style={{ marginBottom: 20 }}><InfoBanner/></div>}
        <div style={{ display:'grid', gridTemplateColumns:'minmax(0, 1fr) minmax(0, 1fr)', gap: 28, alignItems:'start' }}>
          <Stack>{classifica}</Stack>
          <Stack>{scoring}{notes}{photo}</Stack>
        </div>
      </div>
      <MetaFooter record={record}/>
    </>
  );
};

// ── SKELETON PRIMITIVES (loading) ──────────────────────
const SkelRect = ({ w, h, r, style }) => (
  <div aria-hidden="true" className="skel" style={{ width: w, height: h, borderRadius: r || 'var(--r-sm)', background: eHs('session', 0.08), flexShrink: 0, ...style }}/>
);

const SkelClassificaRow = () => (
  <div aria-hidden="true" style={{ display:'flex', alignItems:'center', gap: 12, padding:'12px 14px', borderBottom:'1px solid var(--border-light)' }}>
    <SkelRect w={38} h={38} r="var(--r-pill)"/>
    <div style={{ flex: 1, display:'flex', flexDirection:'column', gap: 7 }}>
      <SkelRect w="52%" h={13} r="var(--r-xs)"/>
      <SkelRect w="100%" h={6} r="var(--r-pill)"/>
    </div>
    <SkelRect w={34} h={20} r="var(--r-xs)"/>
  </div>
);

// ── STATO 01 · DEFAULT ─────────────────────────────────
// state-01-default — dettaglio completo as-shipped (Marco vince).
const State01_Default = ({ compact }) => (
  <div style={{ flex: 1, display:'flex', flexDirection:'column', background:'var(--bg)', minHeight: 0, overflow: compact ? 'hidden' : 'visible' }}>
    <TopNav record={REC_DEFAULT} compact={compact}/>
    <DetailView record={REC_DEFAULT} compact={compact}/>
  </div>
);

// ── STATO 02 · EMPTY ───────────────────────────────────
// state-02-empty — record minimale: foto/note assenti, 2 score null, nessun winner.
const State02_Empty = ({ compact }) => (
  <div style={{ flex: 1, display:'flex', flexDirection:'column', background:'var(--bg)', minHeight: 0, overflow: compact ? 'hidden' : 'visible' }}>
    <TopNav record={REC_EMPTY} compact={compact}/>
    <DetailView record={REC_EMPTY} compact={compact} minimal emptyMedia infoBanner/>
  </div>
);

// ── STATO 03 · LOADING ─────────────────────────────────
// state-03-loading — skeleton primitives. aria-busy sul wrapper, skeleton aria-hidden,
// screen-reader span. Pulse 0.4→0.8→0.4 (2s) via .skel; reduced-motion → snap.
const State03_Loading = ({ compact }) => {
  const body = (
    <>
      {/* Header skeleton (podio rect + game pip) */}
      <div style={{ padding: compact ? '20px 16px' : '28px 32px', borderBottom:'1px solid var(--border-light)', background:`radial-gradient(circle at 50% -10%, ${eHs('session', 0.08)} 0%, transparent 60%), var(--bg)`, display:'flex', flexDirection:'column', alignItems:'center', gap: 12 }}>
        <SkelRect w={120} h={18} r="var(--r-pill)"/>
        <SkelRect w={compact ? '70%' : 300} h={compact ? 24 : 30} r="var(--r-md)"/>
        <div style={{ display:'flex', alignItems:'flex-end', gap: compact ? 12 : 22, marginTop: 8 }}>
          <SkelRect w={compact ? 48 : 64} h={compact ? 48 : 64} r="var(--r-pill)"/>
          <SkelRect w={compact ? 64 : 88} h={compact ? 64 : 88} r="var(--r-pill)"/>
          <SkelRect w={compact ? 48 : 64} h={compact ? 48 : 64} r="var(--r-pill)"/>
        </div>
      </div>
      {/* ConnectionBar skeleton (4 chip) */}
      <div style={{ display:'flex', gap: 6, padding: compact ? '10px 16px' : '12px 32px', background:'var(--bg-card)', borderBottom:'1px solid var(--border-light)' }}>
        {[0,1,2,3].map(i => <SkelRect key={i} w={compact ? 64 : 96} h={26} r="var(--r-pill)"/>)}
      </div>
    </>
  );
  const classificaSkel = (
    <div>
      <SkelRect w={120} h={14} r="var(--r-xs)" style={{ marginBottom: 10 }}/>
      <div style={{ background:'var(--bg-card)', border:'1px solid var(--border)', borderRadius:'var(--r-lg)', overflow:'hidden' }}>
        {[0,1,2,3].map(i => <SkelClassificaRow key={i}/>)}
      </div>
    </div>
  );
  const notesSkel = (
    <div>
      <SkelRect w={70} h={14} r="var(--r-xs)" style={{ marginBottom: 10 }}/>
      <div style={{ padding:'14px 16px', background:'var(--bg-card)', border:'1px solid var(--border)', borderRadius:'var(--r-lg)', display:'flex', flexDirection:'column', gap: 8 }}>
        <SkelRect w="100%" h={11} r="var(--r-xs)"/>
        <SkelRect w="92%" h={11} r="var(--r-xs)"/>
        <SkelRect w="64%" h={11} r="var(--r-xs)"/>
      </div>
    </div>
  );
  const photoSkel = (
    <div>
      <SkelRect w={64} h={14} r="var(--r-xs)" style={{ marginBottom: 10 }}/>
      <SkelRect w="100%" h={compact ? 150 : 200} r="var(--r-lg)"/>
    </div>
  );
  const srSpan = (
    <span style={{ position:'absolute', width: 1, height: 1, padding: 0, margin: -1, overflow:'hidden', clip:'rect(0 0 0 0)', whiteSpace:'nowrap', border: 0 }}>
      Caricamento dettaglio partita…
    </span>
  );

  return (
    <div aria-busy="true" style={{ flex: 1, display:'flex', flexDirection:'column', background:'var(--bg)', minHeight: 0, overflow: compact ? 'hidden' : 'visible' }}>
      <TopNav loading compact={compact}/>
      {srSpan}
      {body}
      {compact ? (
        <div style={{ padding:'14px 14px 24px', display:'flex', flexDirection:'column', gap: 16 }}>
          {classificaSkel}{notesSkel}{photoSkel}
        </div>
      ) : (
        <div style={{ padding:'24px 32px 40px', maxWidth: 1280, margin:'0 auto', width:'100%', display:'grid', gridTemplateColumns:'minmax(0, 1fr) minmax(0, 1fr)', gap: 28, alignItems:'start' }}>
          <Stack>{classificaSkel}</Stack>
          <Stack>{notesSkel}{photoSkel}</Stack>
        </div>
      )}
    </div>
  );
};

// ── STATO 04 · ERROR ───────────────────────────────────
// state-04-error — banner alert + retry + area vuota (torna lista) + dismiss.
const State04_Error = ({ compact }) => (
  <div style={{ flex: 1, display:'flex', flexDirection:'column', background:'var(--bg)', minHeight: 0, overflow: compact ? 'hidden' : 'visible' }}>
    <TopNav record={null} compact={compact}/>

    {/* Banner errore full-width */}
    <div role="alert" style={{ display:'flex', alignItems:'flex-start', gap: compact ? 10 : 14, padding: compact ? '12px 16px' : '14px 32px', background: eHs('event', 0.08), borderLeft:`4px solid ${eHs('event', 0.6)}`, borderBottom:'1px solid var(--border-light)' }}>
      <span aria-hidden="true" style={{ fontSize: compact ? 18 : 20, lineHeight: 1.3 }}>⚠️</span>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ fontFamily:'var(--f-display)', fontSize: compact ? 13 : 14.5, fontWeight: 800, color:'var(--text)' }}>Impossibile caricare il dettaglio della partita</div>
        <div style={{ fontFamily:'var(--f-body)', fontSize: 12, color:'var(--text-sec)', marginTop: 2, fontWeight: 500 }}>Record non trovato (404) · Verifica la connessione e riprova</div>
      </div>
      <button type="button" aria-label="Riprova caricamento dettaglio partita" style={{ padding:'7px 14px', borderRadius:'var(--r-md)', background:'transparent', color: eHs('event'), border:`1px solid ${eHs('event', 0.5)}`, fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer', display:'inline-flex', alignItems:'center', gap: 5, whiteSpace:'nowrap', flexShrink: 0 }}><span aria-hidden="true">↻</span>Riprova</button>
    </div>

    {/* Area vuota */}
    <div style={{ flex: 1, padding: compact ? '28px 16px' : '56px 32px', display:'flex', alignItems:'flex-start', justifyContent:'center', overflowY:'auto' }}>
      <div style={{ display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center', maxWidth: 380 }}>
        <div aria-hidden="true" style={{ width: 56, height: 56, borderRadius:'50%', background:'var(--bg-muted)', display:'flex', alignItems:'center', justifyContent:'center', fontSize: 26, marginBottom: 12 }}>🃏</div>
        <div style={{ fontFamily:'var(--f-display)', fontSize: 14.5, fontWeight: 800, color:'var(--text-sec)', marginBottom: 6 }}>Nessun dato disponibile</div>
        <a href="sp4-play-records-index.html" aria-label="Torna alla lista partite" style={{ fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, color: eHs('session'), display:'inline-flex', alignItems:'center', gap: 5 }}><span aria-hidden="true">←</span>Torna alla lista partite</a>
      </div>
    </div>

    {/* Footer dismiss */}
    <div style={{ padding: compact ? '12px 16px' : '14px 32px', borderTop:'1px solid var(--border-light)', display:'flex', justifyContent:'center' }}>
      <button type="button" style={{ background:'transparent', border:'none', color:'var(--text-muted)', fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 700, cursor:'pointer', textDecoration:'underline', textUnderlineOffset: 3 }}>Chiudi</button>
    </div>
  </div>
);

// ── STATO 05 · SSE — SKIPPED ───────────────────────────
// Il detail /play-records/[id] NON è SSE-driven: è una read-only fetch-once
// (nessuna sottoscrizione eventi live). Nessun State05_SSE renderizzato
// (cfr. G7 SessionStateRenderer: lo stato `sse` si applica solo alle view con
// streaming live). Ghost link disabled in nav → "05 · SSE · skip".

// ═══════════════════════════════════════════════════════
// ── GALLERY — frames + nav + sections ──────────────────
// ═══════════════════════════════════════════════════════
const MobileFrame = ({ children }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 8 }}>
    <div className="frame-tag">Mobile · 375</div>
    <div className="phone">
      <PhoneSbar/>
      <div style={{ flex: 1, overflow:'hidden', display:'flex', flexDirection:'column', background:'var(--bg)' }}>{children}</div>
    </div>
  </div>
);

const DesktopFrame = ({ children }) => (
  <div style={{ display:'flex', flexDirection:'column', gap: 8, flex: 1, minWidth: 0 }}>
    <div className="frame-tag">Desktop · 1440</div>
    <div className="desktop-frame">
      <div className="desktop-bar">
        <span className="traffic"/><span className="traffic"/><span className="traffic"/>
        <span className="url">meepleai.app/play-records/pr1</span>
      </div>
      <div style={{ display:'flex', flexDirection:'column', minHeight: 660, background:'var(--bg)' }}>{children}</div>
    </div>
  </div>
);

// 2 viewport per stato (Mobile 375 + Desktop 1440). Tema light/dark globale via
// <html data-theme> (toggle in nav) — token dark scoping su :root, niente wrapper annidati.
const StateMatrix = ({ Comp }) => (
  <div className="matrix">
    <div className="matrix-row">
      <MobileFrame><Comp compact/></MobileFrame>
      <DesktopFrame><Comp/></DesktopFrame>
    </div>
  </div>
);

const STATES = [
  { id:'state-01-default', num:'01', title:'Default', sub:'Dettaglio completo as-shipped: hero podio (Marco vince) + ConnectionBar + classifica 1°–4° + dettaglio punteggi tabellare + note + foto allegata + meta footer. Stato base, invariato.', Comp: State01_Default },
  { id:'state-02-empty',   num:'02', title:'Empty',   sub:'Record minimale: hero con vincitore "—", banner info role="status", classifica con score parziali (null → "—"), note e foto in empty-state con CTA dimmed.', Comp: State02_Empty },
  { id:'state-03-loading', num:'03', title:'Loading', sub:'Fetch dettaglio in corso: skeleton header (podio + pip) + ConnectionBar + classifica + note + foto. aria-busy, skeleton aria-hidden, pulse 2s (snap con reduced-motion).', Comp: State03_Loading },
  { id:'state-04-error',   num:'04', title:'Error',   sub:'Errore fetch (404 / rete): banner role="alert" + Riprova, area vuota con link "Torna alla lista partite", footer "Chiudi".', Comp: State04_Error },
];

const NAV = [
  { id:'state-01-default', label:'01 · Default' },
  { id:'state-02-empty',   label:'02 · Empty' },
  { id:'state-03-loading', label:'03 · Loading' },
  { id:'state-04-error',   label:'04 · Error' },
];

function ThemeToggle() {
  const initial = (() => { try { return localStorage.getItem('sp4-pr-theme') === 'dark'; } catch (e) { return false; } })();
  const [dark, setDark] = useState(initial);
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
    try { localStorage.setItem('sp4-pr-theme', dark ? 'dark' : 'light'); } catch (e) {}
  }, [dark]);
  return (
    <button type="button" className="theme-toggle" onClick={() => setDark(d => !d)} aria-pressed={dark}
      aria-label={dark ? 'Passa a tema chiaro' : 'Passa a tema scuro'}>
      <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span><span>{dark ? 'Dark' : 'Light'}</span>
    </button>
  );
}

function GalleryNav() {
  return (
    <nav className="gallery-nav" aria-label="Stati canonici">
      <div className="gallery-nav-brand"><span aria-hidden="true">🎯</span> SP4 · /play-records/[id]</div>
      <div className="gallery-nav-links">
        {NAV.map(n => <a key={n.id} href={`#${n.id}`}>{n.label}</a>)}
      </div>
      <a className="gallery-nav-ghost" href="#state-05-sse-skipped" aria-disabled="true" title="state-05-sse: skipped — detail non SSE-driven (fetch-once read-only)">05 · SSE · skip</a>
      <ThemeToggle/>
    </nav>
  );
}

function StateSection({ id, num, title, sub, Comp }) {
  return (
    <section id={id} className="state-section" data-screen-label={id}>
      <header className="state-head">
        <div className="state-num">{num}</div>
        <div className="state-head-text">
          <h2>{title}</h2>
          <p>{sub}</p>
        </div>
        <code className="state-anchor">#{id}</code>
      </header>
      <StateMatrix Comp={Comp}/>
    </section>
  );
}

function App() {
  return (
    <div className="gallery">
      <GalleryNav/>
      <div className="gallery-body">
        <header className="gallery-intro">
          <div className="kicker">SP4 · /play-records/[id] · Detail 🏆 — canonical states</div>
          <h1>Dettaglio partita — Stati canonici</h1>
          <p className="lead">
            Vista read-only dettaglio partita allineata al pattern <strong>G7 SessionStateRenderer</strong> (PR 2357).
            4 stati canonici × viewport mobile&nbsp;375 / desktop&nbsp;1440 (8 frame), × tema light/dark via toggle = 16 combinazioni.
            Entity dominante <strong>session 🎯</strong>; colori esclusivamente da token <code>--c-*</code> via <code>eHs()</code>.
            Lo stato <code>state-05-sse</code> è intenzionalmente <strong>saltato</strong> (detail non SSE-driven, fetch-once).
          </p>
        </header>

        {STATES.map(s => <StateSection key={s.id} {...s}/>)}
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);

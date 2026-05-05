/* MeepleAI SP4 wave 2 — Schermata #3/3 FINALE — main
   Achievements + Diary + Photos + Chat highlights + ShareCard + PlayAgain
*/
const { useState: us, useEffect: ue, useMemo: um } = React;
const SP = window.SummaryParts;
const eHs = SP.eH;

const ACHIEVEMENTS = [
  { id:'a1', em:'🦅', name:'Master birder', desc:'15+ birds in single round', unlocked: true },
  { id:'a2', em:'🌿', name:'Habitat king', desc:'7+ in single column', unlocked: true },
  { id:'a3', em:'⚡', name:'Speed run', desc:'Turno < 30s medio', unlocked: true },
  { id:'a4', em:'🥚', name:'Egg layer', desc:'25+ eggs deposte', unlocked: false, lockText:'Servono 25 uova' },
  { id:'a5', em:'🎯', name:'Bonus hunter', desc:'5+ bonus card completate', unlocked: false, lockText:'5+ bonus card' },
  { id:'a6', em:'🌍', name:'World traveler', desc:'Giocato 10+ giochi diversi', unlocked: false, lockText:'10 giochi distinti' },
];

const DIARY = [
  { turn: 1, events: [
    { t:'13:08', kind:'event', em:'▶', text:'Sessione iniziata · setup completato' },
    { t:'13:09', kind:'tool', em:'🎲', text:'Marco ha tirato 4 (mano iniziale)' },
  ]},
  { turn: 8, events: [
    { t:'14:05', kind:'score', em:'🐦', text:'Anna ha giocato Bald Eagle (Forest +12)' },
    { t:'14:08', kind:'photo', em:'📷', text:'Foto tableau aggiunta' },
  ]},
  { turn: 12, events: [
    { t:'14:38', kind:'event', em:'⏸', text:'Pausa 14:32–14:38 (6m)' },
    { t:'14:43', kind:'score', em:'🏆', text:'Anna ha guadagnato +8 punti (Habitat)' },
    { t:'14:51', kind:'agent', em:'🤖', text:'Agente: "Considera la wetland..."' },
    { t:'14:58', kind:'chat', em:'💬', text:'Marco: "Quanto valgono i bird con uova multiple?"' },
  ]},
  { turn: 18, events: [
    { t:'15:30', kind:'score', em:'🏆', text:'Marco ha completato bonus card "Eggs in nest" (+16)' },
    { t:'15:32', kind:'event', em:'⏹', text:'Sessione terminata · score finalizzati' },
  ]},
];

const eventColor = (k) => ({ score:'session', tool:'tool', agent:'agent', chat:'chat', photo:'kb', event:'event' }[k] || 'session');

// ═══════════════════════════════════════════════════════
const SectionTitle = ({ em, children, action }) => (
  <div style={{
    display:'flex', alignItems:'center', justifyContent:'space-between',
    marginBottom: 10, gap: 8, flexWrap:'wrap',
  }}>
    <h2 style={{
      fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
      color:'var(--text)', margin: 0,
      display:'inline-flex', alignItems:'center', gap: 7,
    }}>
      <span aria-hidden="true">{em}</span>{children}
    </h2>
    {action}
  </div>
);

// ─── ACHIEVEMENTS ────────────────────────────────────
const Achievements = ({ empty, compact }) => (
  <section id="section-achievements">
    <SectionTitle em="🏅">Achievements <span style={{ fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 700, color:'var(--text-muted)' }}>· 3 / 6 sbloccati</span></SectionTitle>
    {empty ? (
      <div style={{
        padding:'24px', borderRadius:'var(--r-lg)',
        background:'var(--bg-card)', border:'1px dashed var(--border-strong)',
        textAlign:'center',
      }}>
        <div style={{ fontSize: 32, marginBottom: 8 }} aria-hidden="true">🏅</div>
        <div style={{ fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, color:'var(--text)', marginBottom: 3 }}>Nessun achievement questa partita</div>
        <div style={{ fontSize: 12, color:'var(--text-muted)' }}>Riprova per sbloccare nuovi badge!</div>
      </div>
    ) : (
      <div style={{
        display:'grid',
        gridTemplateColumns: compact ? 'repeat(2, 1fr)' : 'repeat(3, 1fr)',
        gap: 8,
      }}>
        {ACHIEVEMENTS.map(a => (
          <div key={a.id} title={a.unlocked ? a.desc : a.lockText} style={{
            padding:'10px 12px', borderRadius:'var(--r-md)',
            background: a.unlocked ? eHs('toolkit', 0.06) : 'var(--bg-muted)',
            border: a.unlocked ? `1px solid ${eHs('toolkit', 0.3)}` : '1px dashed var(--border)',
            display:'flex', alignItems:'center', gap: 9,
            opacity: a.unlocked ? 1 : 0.45,
          }}>
            <div style={{
              width: 36, height: 36, borderRadius:'50%',
              background: a.unlocked ? eHs('toolkit', 0.18) : 'var(--bg-hover)',
              display:'flex', alignItems:'center', justifyContent:'center',
              fontSize: 18, flexShrink: 0,
            }} aria-hidden="true">{a.unlocked ? a.em : '🔒'}</div>
            <div style={{ minWidth: 0 }}>
              <div style={{
                fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800,
                color: a.unlocked ? eHs('toolkit') : 'var(--text-muted)',
              }}>{a.name}</div>
              <div style={{ fontFamily:'var(--f-mono)', fontSize: 9.5, color:'var(--text-muted)', fontWeight: 700 }}>
                {a.unlocked ? a.desc : a.lockText}
              </div>
            </div>
          </div>
        ))}
      </div>
    )}
  </section>
);

// ─── DIARY TIMELINE ──────────────────────────────────
const Diary = () => {
  const [filter, setFilter] = us('all');
  const [expanded, setExpanded] = us({ 1: true });
  const filters = [
    { id:'all', lb:'Tutti' }, { id:'score', lb:'Score' },
    { id:'event', lb:'Eventi' }, { id:'chat', lb:'Chat' }, { id:'photo', lb:'Foto' },
  ];
  return (
    <section id="section-event">
      <SectionTitle em="📜">Diario partita</SectionTitle>
      <div style={{ display:'flex', gap: 4, marginBottom: 10, flexWrap:'wrap' }}>
        {filters.map(f => (
          <button key={f.id} type="button" onClick={() => setFilter(f.id)}
            aria-pressed={filter===f.id}
            style={{
              padding:'4px 10px', borderRadius:'var(--r-pill)',
              background: filter===f.id ? eHs('session', 0.14) : 'var(--bg-card)',
              border: filter===f.id ? `1px solid ${eHs('session', 0.4)}` : '1px solid var(--border)',
              color: filter===f.id ? eHs('session') : 'var(--text-sec)',
              fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 700, cursor:'pointer',
            }}>{f.lb}</button>
        ))}
      </div>
      <div style={{
        background:'var(--bg-card)', border:'1px solid var(--border)',
        borderRadius:'var(--r-lg)', overflow:'hidden',
      }}>
        {DIARY.map((t, idx) => {
          const open = expanded[t.turn];
          const filtered = filter === 'all' ? t.events : t.events.filter(e => e.kind === filter);
          if (filter !== 'all' && filtered.length === 0) return null;
          return (
            <div key={t.turn} style={{
              borderBottom: idx < DIARY.length - 1 ? '1px solid var(--border-light)' : 'none',
            }}>
              <button type="button" onClick={() => setExpanded(s => ({ ...s, [t.turn]: !s[t.turn] }))}
                style={{
                  width:'100%', padding:'10px 14px',
                  background:'transparent', border:'none', cursor:'pointer',
                  display:'flex', alignItems:'center', gap: 10,
                  color:'var(--text)',
                }}>
                <span style={{
                  padding:'2px 8px', borderRadius:'var(--r-pill)',
                  background: eHs('session', 0.12),
                  color: eHs('session'),
                  fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
                  textTransform:'uppercase', letterSpacing:'.06em',
                }}>Turno {t.turn}</span>
                <span style={{ fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)', fontWeight: 700 }}>
                  {filtered.length} eventi
                </span>
                <span style={{ flex: 1 }}/>
                <span aria-hidden="true" style={{ fontSize: 12, color:'var(--text-muted)' }}>{open ? '▴' : '▾'}</span>
              </button>
              {open && (
                <div style={{
                  padding:'4px 14px 14px 14px', position:'relative',
                }}>
                  <div aria-hidden="true" style={{
                    position:'absolute', left: 24, top: 4, bottom: 14,
                    width: 2, background:`${eHs('session', 0.2)}`,
                  }}/>
                  <div style={{ display:'flex', flexDirection:'column', gap: 6 }}>
                    {filtered.map((e, ei) => {
                      const ec = eventColor(e.kind);
                      return (
                        <div key={ei} style={{ display:'flex', gap: 9, paddingLeft: 4, position:'relative' }}>
                          <div style={{
                            width: 20, height: 20, borderRadius:'50%',
                            background: eHs(ec, 0.14),
                            border: `2px solid ${eHs(ec, 0.4)}`,
                            display:'flex', alignItems:'center', justifyContent:'center',
                            fontSize: 9, flexShrink: 0, zIndex: 1,
                          }} aria-hidden="true">{e.em}</div>
                          <div style={{ flex: 1, padding:'3px 0' }}>
                            <span style={{ fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800, color: eHs(ec), marginRight: 6, textTransform:'uppercase', letterSpacing:'.06em' }}>{e.t}</span>
                            <span style={{ fontFamily:'var(--f-body)', fontSize: 12, color:'var(--text-sec)', fontWeight: 600 }}>{e.text}</span>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              )}
            </div>
          );
        })}
      </div>
    </section>
  );
};

// ─── PHOTOS GALLERY ──────────────────────────────────
const PhotoGallery = ({ empty, compact }) => (
  <section id="section-kb">
    <SectionTitle em="📷">Foto della partita {!empty && <span style={{ fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 700, color:'var(--text-muted)' }}>· 3</span>}</SectionTitle>
    {empty ? (
      <div style={{
        padding:'30px 16px', borderRadius:'var(--r-lg)',
        background:'var(--bg-card)', border:'1px dashed var(--border-strong)',
        textAlign:'center',
      }}>
        <div style={{ fontSize: 36, marginBottom: 8 }} aria-hidden="true">📷</div>
        <div style={{ fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, color:'var(--text)', marginBottom: 3 }}>Nessuna foto — la prossima volta!</div>
        <button type="button" style={{
          marginTop: 8, padding:'7px 14px', borderRadius:'var(--r-md)',
          background:'transparent', color: eHs('kb'),
          border:`1px dashed ${eHs('kb', 0.4)}`,
          fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700, cursor:'pointer',
        }}>+ Aggiungi foto</button>
      </div>
    ) : (
      <div style={{
        display:'grid',
        gridTemplateColumns: compact ? 'repeat(2, 1fr)' : 'repeat(4, 1fr)',
        gap: 8,
      }}>
        {[
          { lb:'Tableau T8', g:'linear-gradient(135deg, hsl(85,60%,55%), hsl(180,50%,40%))' },
          { lb:'Wetland', g:'linear-gradient(135deg, hsl(200,60%,55%), hsl(230,50%,40%))' },
          { lb:'Final scores', g:'linear-gradient(135deg, hsl(40,70%,60%), hsl(10,60%,45%))' },
        ].map((p, i) => (
          <button key={i} type="button" style={{
            aspectRatio:'1', borderRadius:'var(--r-md)',
            background: p.g, border:'1px solid var(--border)',
            position:'relative', overflow:'hidden', cursor:'pointer',
            padding: 0,
          }}>
            <span style={{
              position:'absolute', bottom: 6, left: 6,
              fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
              color:'#fff', background:'rgba(0,0,0,.45)',
              padding:'2px 6px', borderRadius:'var(--r-pill)',
            }}>{p.lb}</span>
          </button>
        ))}
        <button type="button" style={{
          aspectRatio:'1', borderRadius:'var(--r-md)',
          background:'transparent', border:'1px dashed var(--border-strong)',
          color:'var(--text-muted)', fontSize: 22, cursor:'pointer',
          display:'flex', alignItems:'center', justifyContent:'center',
        }} aria-label="Aggiungi foto">+</button>
      </div>
    )}
  </section>
);

// ─── CHAT HIGHLIGHTS ─────────────────────────────────
const ChatHighlights = ({ empty }) => {
  const [open, setOpen] = us(true);
  return (
    <section id="section-chat">
      <SectionTitle em="💬"
        action={<button type="button" onClick={() => setOpen(o => !o)}
          style={{
            padding:'4px 10px', borderRadius:'var(--r-pill)',
            background:'var(--bg-card)', border:'1px solid var(--border)',
            color:'var(--text-sec)',
            fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700, cursor:'pointer',
          }}>{open ? '▴ Comprimi' : '▾ Espandi'}</button>}>
        Chat highlights {!empty && <span style={{ fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 700, color:'var(--text-muted)' }}>· 3 di 8</span>}
      </SectionTitle>
      {empty ? (
        <div style={{ padding:'20px', borderRadius:'var(--r-lg)', background:'var(--bg-card)', border:'1px dashed var(--border-strong)', textAlign:'center', fontSize: 12.5, color:'var(--text-muted)' }}>
          Nessuna chat con l'agente in questa partita.
        </div>
      ) : open && (
        <div style={{ background:'var(--bg-card)', border:'1px solid var(--border)', borderRadius:'var(--r-lg)', padding: 12, display:'flex', flexDirection:'column', gap: 10 }}>
          {[
            { from:'user', t:'14:58', text:'Quanto valgono i bird con uova multiple?' },
            { from:'agent', t:'15:02', text:'I bird con simbolo uovo nell\'angolo valgono 1 punto bonus per ogni uovo deposto.', citations:['Wingspan §4.2', 'FAQ #12'] },
            { from:'user', t:'15:08', text:'Posso giocare un bird sopra il limite di habitat?' },
          ].map((m, i) => {
            const isAgent = m.from === 'agent';
            return (
              <div key={i} style={{ alignSelf: isAgent ? 'flex-start' : 'flex-end', maxWidth:'88%' }}>
                <div style={{
                  padding:'7px 11px', borderRadius:'var(--r-lg)',
                  background: isAgent ? eHs('agent', 0.1) : eHs('chat', 0.12),
                  border: `1px solid ${isAgent ? eHs('agent', 0.25) : eHs('chat', 0.25)}`,
                  borderTopLeftRadius: isAgent ? 4 : 'var(--r-lg)',
                  borderTopRightRadius: isAgent ? 'var(--r-lg)' : 4,
                  fontFamily:'var(--f-body)', fontSize: 12.5, fontWeight: 500,
                  color:'var(--text)', lineHeight: 1.45,
                }}>{m.text}</div>
                <div style={{ display:'flex', gap: 4, alignItems:'center', marginTop: 3, padding:'0 4px', flexWrap:'wrap' }}>
                  <span style={{ fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)', fontWeight: 700 }}>{m.t}</span>
                  {m.citations?.map(c => (
                    <span key={c} style={{
                      padding:'1px 5px', borderRadius:'var(--r-pill)',
                      background: eHs('kb', 0.1), color: eHs('kb'),
                      border:`1px solid ${eHs('kb', 0.25)}`,
                      fontFamily:'var(--f-mono)', fontSize: 8.5, fontWeight: 800,
                    }}>📄 {c}</span>
                  ))}
                </div>
              </div>
            );
          })}
          <button type="button" style={{
            alignSelf:'center', marginTop: 4,
            padding:'5px 12px', borderRadius:'var(--r-pill)',
            background: eHs('chat', 0.1), color: eHs('chat'),
            border:`1px solid ${eHs('chat', 0.3)}`,
            fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
            cursor:'pointer', textTransform:'uppercase', letterSpacing:'.05em',
          }}>Vedi thread completo →</button>
        </div>
      )}
    </section>
  );
};

// ─── SHARE CARD ──────────────────────────────────────
const ShareCard = ({ compact }) => {
  const [previewDark, setPreviewDark] = us(false);
  return (
    <section>
      <SectionTitle em="📤"
        action={
          <div role="radiogroup" style={{
            display:'inline-flex', borderRadius:'var(--r-md)',
            border:'1px solid var(--border)', background:'var(--bg-card)',
            overflow:'hidden',
          }}>
            {['light','dark'].map(t => {
              const active = (t === 'dark') === previewDark;
              return (
                <button key={t} type="button" onClick={() => setPreviewDark(t === 'dark')}
                  aria-checked={active}
                  style={{
                    padding:'4px 10px',
                    background: active ? eHs('session', 0.14) : 'transparent',
                    color: active ? eHs('session') : 'var(--text-muted)',
                    border:'none', cursor:'pointer',
                    fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
                    textTransform:'uppercase', letterSpacing:'.06em',
                  }}>{t}</button>
              );
            })}
          </div>
        }>Share card preview</SectionTitle>
      <div style={{
        background: previewDark ? '#0f0c1e' : '#fff',
        borderRadius:'var(--r-lg)',
        border: `2px solid ${eHs('session', 0.3)}`,
        overflow:'hidden',
        position:'relative',
        aspectRatio: compact ? '1' : '4/3',
        maxWidth: compact ? '100%' : 600,
        margin:'0 auto',
        padding: compact ? '20px 16px' : '32px 28px',
        backgroundImage: previewDark
          ? `radial-gradient(circle at 20% 0%, ${eHs('session', 0.3)} 0%, transparent 60%), radial-gradient(circle at 80% 100%, ${eHs('toolkit', 0.18)} 0%, transparent 50%)`
          : `radial-gradient(circle at 20% 0%, ${eHs('session', 0.18)} 0%, transparent 60%), radial-gradient(circle at 80% 100%, ${eHs('toolkit', 0.1)} 0%, transparent 50%)`,
      }}>
        {/* Game watermark */}
        <div style={{
          position:'absolute', top: 14, right: 16,
          fontSize: compact ? 32 : 56, opacity: 0.18,
        }} aria-hidden="true">🦜</div>

        <div style={{ position:'relative', zIndex: 1, height:'100%', display:'flex', flexDirection:'column', justifyContent:'space-between' }}>
          <div>
            <div style={{
              display:'inline-flex', alignItems:'center', gap: 5,
              padding:'2px 8px', borderRadius:'var(--r-pill)',
              background: eHs('toolkit', 0.18),
              color: eHs('toolkit'),
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              textTransform:'uppercase', letterSpacing:'.1em',
            }}>🏆 Vittoria</div>
            <h3 style={{
              fontFamily:'var(--f-display)', fontSize: compact ? 18 : 26, fontWeight: 800,
              color: previewDark ? '#fff' : '#0f0c1e',
              margin:'8px 0 2px', letterSpacing:'-.02em',
            }}>Marco vince Wingspan</h3>
            <div style={{ fontFamily:'var(--f-mono)', fontSize: compact ? 10 : 11, color: previewDark ? 'rgba(255,255,255,.7)' : 'var(--text-muted)', fontWeight: 700 }}>
              23 apr 2026 · 1h 24min · 4 giocatori
            </div>
          </div>

          {/* Mini podio */}
          <div style={{ display:'flex', justifyContent:'space-around', alignItems:'flex-end', gap: 8, marginTop: 12 }}>
            {[SP.FINAL[1], SP.FINAL[0], SP.FINAL[2]].map((p, idx) => {
              const place = idx === 1 ? 1 : (idx === 0 ? 2 : 3);
              const isW = place === 1;
              const sz = isW ? (compact ? 38 : 56) : (compact ? 30 : 44);
              return (
                <div key={p.id} style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 3 }}>
                  {isW && <span style={{ fontSize: compact ? 14 : 20 }}>🏆</span>}
                  <div style={{
                    width: sz, height: sz, borderRadius:'50%',
                    background:`linear-gradient(135deg, hsl(${p.color},70%,65%), hsl(${p.color},60%,42%))`,
                    color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
                    fontFamily:'var(--f-display)', fontSize: isW ? (compact ? 17 : 24) : (compact ? 13 : 18), fontWeight: 800,
                    border: isW ? `2px solid ${eHs('toolkit')}` : 'none',
                  }}>{p.name[0]}</div>
                  <div style={{
                    fontFamily:'var(--f-display)', fontSize: isW ? (compact ? 18 : 26) : (compact ? 13 : 18), fontWeight: 800,
                    color: isW ? eHs('toolkit') : (previewDark ? '#fff' : '#0f0c1e'),
                    fontVariantNumeric:'tabular-nums', lineHeight: 1,
                  }}>{p.score}</div>
                </div>
              );
            })}
          </div>

          <div style={{
            display:'flex', alignItems:'center', justifyContent:'space-between',
            marginTop: 10,
          }}>
            <div style={{
              fontFamily:'var(--f-display)', fontSize: compact ? 10 : 12, fontWeight: 800,
              color: previewDark ? 'rgba(255,255,255,.5)' : 'var(--text-muted)',
              display:'inline-flex', alignItems:'center', gap: 4,
            }}>
              <span style={{
                width: 14, height: 14, borderRadius: 4,
                background:`linear-gradient(135deg, ${eHs('game')}, ${eHs('event')})`,
                color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
                fontSize: 9, fontWeight: 800,
              }}>M</span>
              MeepleAI
            </div>
            <div style={{ fontFamily:'var(--f-mono)', fontSize: 9, color: previewDark ? 'rgba(255,255,255,.4)' : 'var(--text-muted)', fontWeight: 700 }}>
              meepleai.app
            </div>
          </div>
        </div>
      </div>

      {/* Share buttons */}
      <div style={{ display:'flex', gap: 6, marginTop: 12, flexWrap:'wrap', justifyContent:'center' }}>
        {[
          { lb:'Twitter', em:'𝕏' },
          { lb:'Instagram', em:'📸' },
          { lb:'WhatsApp', em:'💬' },
          { lb:'Copia link', em:'🔗' },
          { lb:'Download PNG', em:'⬇' },
        ].map(b => (
          <button key={b.lb} type="button" style={{
            padding:'7px 12px', borderRadius:'var(--r-md)',
            background:'var(--bg-card)', border:`1px solid ${eHs('session', 0.3)}`,
            color:'var(--text-sec)',
            fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 700,
            cursor:'pointer', display:'inline-flex', alignItems:'center', gap: 4,
          }}><span aria-hidden="true">{b.em}</span>{b.lb}</button>
        ))}
      </div>
    </section>
  );
};

// ─── PLAY AGAIN ──────────────────────────────────────
const PlayAgain = () => (
  <section style={{
    padding:'18px 20px', borderRadius:'var(--r-xl)',
    background:`linear-gradient(135deg, ${eHs('game', 0.1)} 0%, ${eHs('session', 0.1)} 100%)`,
    border:`1px solid ${eHs('game', 0.3)}`,
    display:'flex', alignItems:'center', justifyContent:'space-between',
    gap: 14, flexWrap:'wrap',
  }}>
    <div style={{ flex: 1, minWidth: 200 }}>
      <h3 style={{ fontFamily:'var(--f-display)', fontSize: 17, fontWeight: 800, color:'var(--text)', margin:'0 0 3px', letterSpacing:'-.01em' }}>
        🎲 Pronti per la rivincita?
      </h3>
      <p style={{ fontFamily:'var(--f-body)', fontSize: 12.5, color:'var(--text-sec)', margin: 0 }}>
        Stessi giocatori e gioco — agente già caricato.
      </p>
    </div>
    <div style={{ display:'flex', gap: 7, flexWrap:'wrap' }}>
      <button type="button" style={{
        padding:'10px 16px', borderRadius:'var(--r-md)',
        background: eHs('session'), color:'#fff', border:'none',
        fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, cursor:'pointer',
        boxShadow:`0 4px 14px ${eHs('session', 0.4)}`,
        display:'inline-flex', alignItems:'center', gap: 5,
      }}><span aria-hidden="true">▶</span>Riavvia con stessi player</button>
      <button type="button" style={{
        padding:'10px 14px', borderRadius:'var(--r-md)',
        background:'transparent', color: eHs('game'),
        border:`1px solid ${eHs('game', 0.4)}`,
        fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, cursor:'pointer',
        display:'inline-flex', alignItems:'center', gap: 5,
      }}><span aria-hidden="true">🎲</span>Cambia gioco</button>
    </div>
  </section>
);

// ─── BODY ────────────────────────────────────────────
const SummaryBody = ({ stateOverride, compact, emptyAch, emptyPhotos, emptyChat }) => {
  if (stateOverride === 'loading') {
    return (
      <div style={{ padding: compact ? 14 : 24, display:'flex', flexDirection:'column', gap: 14 }}>
        <div className="mai-shimmer" style={{ height: compact ? 200 : 280, borderRadius:'var(--r-xl)', background:'var(--bg-muted)' }}/>
        <div className="mai-shimmer" style={{ height: 100, borderRadius:'var(--r-lg)', background:'var(--bg-muted)' }}/>
        <div className="mai-shimmer" style={{ height: 240, borderRadius:'var(--r-lg)', background:'var(--bg-muted)' }}/>
      </div>
    );
  }
  if (stateOverride === 'error') {
    return (
      <div style={{
        padding:'40px 24px', textAlign:'center', margin: 24,
        background:'hsl(var(--c-danger) / .06)',
        border:'1px solid hsl(var(--c-danger) / .25)',
        borderRadius:'var(--r-xl)',
      }}>
        <div style={{ fontSize: 32, marginBottom: 8 }} aria-hidden="true">⚠</div>
        <h3 style={{ fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800, margin:'0 0 4px' }}>Errore caricamento riepilogo</h3>
        <p style={{ fontSize: 12.5, color:'var(--text-muted)', margin:'0 0 14px' }}>Impossibile recuperare i dati della partita.</p>
        <button type="button" style={{
          padding:'8px 14px', borderRadius:'var(--r-md)',
          background:'hsl(var(--c-danger))', color:'#fff', border:'none',
          fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800, cursor:'pointer',
        }}>↻ Riprova</button>
      </div>
    );
  }
  return (
    <div style={{ padding: compact ? '14px 14px 32px' : '24px 32px 64px', display:'flex', flexDirection:'column', gap: compact ? 18 : 26, maxWidth: 1280, margin:'0 auto', width:'100%' }}>
      <section id="section-session"><SP.KpiGrid compact={compact}/></section>
      <section id="section-player"><SP.ScoringBreakdownTable compact={compact}/></section>
      <section id="section-agent"><Achievements empty={emptyAch} compact={compact}/></section>
      <Diary/>
      <PhotoGallery empty={emptyPhotos} compact={compact}/>
      <ChatHighlights empty={emptyChat}/>
      <ShareCard compact={compact}/>
      <PlayAgain/>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── FRAMES ───────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}>
    <span style={{ fontFamily:'var(--f-mono)' }}>15:33</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">88%</span></div>
  </div>
);

const TopNav = ({ compact }) => (
  <div style={{
    display:'flex', alignItems:'center', gap: 10,
    padding: compact ? '8px 12px' : '10px 24px',
    background:'var(--glass-bg)', backdropFilter:'blur(12px)',
    borderBottom:'1px solid var(--border)',
  }}>
    <button type="button" aria-label="Indietro" style={{
      width: 30, height: 30, borderRadius:'var(--r-md)',
      background:'var(--bg-card)', border:'1px solid var(--border)',
      color:'var(--text-sec)', cursor:'pointer', fontSize: 13, fontWeight: 800,
    }}>←</button>
    <div style={{
      display:'inline-flex', alignItems:'center', gap: 5,
      padding:'2px 8px', borderRadius:'var(--r-pill)',
      background: eHs('game', 0.1), color: eHs('game'),
      border:`1px solid ${eHs('game', 0.3)}`,
      fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
      textTransform:'uppercase', letterSpacing:'.05em',
    }}>🎲 Wingspan</div>
    <span style={{ fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, color:'var(--text)' }}>Serata #3</span>
    <div style={{ flex: 1 }}/>
  </div>
);

const PhoneScreen = (props) => (
  <>
    <PhoneSbar/>
    <div style={{ flex: 1, overflow:'auto', display:'flex', flexDirection:'column', background:'var(--bg)' }}>
      <TopNav compact/>
      <SP.SummaryHeroPodium variant={props.variant} compact/>
      <SP.ConnectionBar compact/>
      <SummaryBody {...props} compact/>
    </div>
  </>
);

const DesktopFrameInner = (props) => (
  <div style={{ display:'flex', flexDirection:'column', minHeight: 720, background:'var(--bg)' }}>
    <TopNav/>
    <SP.SummaryHeroPodium variant={props.variant}/>
    <SP.ConnectionBar/>
    <SummaryBody {...props}/>
  </div>
);

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
    <div style={{
      width:'100%', maxWidth: 1280,
      borderRadius:'var(--r-xl)', border:'1px solid var(--border)',
      background:'var(--bg)', overflow:'hidden', boxShadow:'var(--shadow-lg)',
    }}>
      <div style={{
        display:'flex', alignItems:'center', gap: 8,
        padding:'9px 14px', background:'var(--bg-muted)',
        borderBottom:'1px solid var(--border)',
        fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      }}>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-event))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-warning))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-toolkit))' }}/>
        <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/sessions/wing-3</span>
      </div>
      {children}
    </div>
    {desc && <div style={{ fontSize: 11, color:'var(--text-muted)', maxWidth: 800, textAlign:'center', lineHeight: 1.55 }}>{desc}</div>}
  </div>
);

const MOBILE_STATES = [
  { id:'m1', variant:'default', label:'01 · Default winner', desc:'Hero podio con confetti CSS-only first-load + Marco 1° corona + 2°/3° avatars + 4° row sotto. ConnectionBar prod 1:1. Body sezioni stack: KPI 2-col, Scoring tabella scroll-x.' },
  { id:'m2', variant:'tied', label:'02 · Pareggio', desc:'2 corone shared 1° (Marco + Anna) con marker T sull\'avatar + banner "🤝 Pareggio tra Marco e Anna" entity-toolkit.' },
  { id:'m3', variant:'abandoned', label:'03 · Abbandonata', desc:'Hero ridotto entity-event (no podio) + banner "⏸ Partita interrotta al turno 12/18" + 2 CTA Riprendi/Archivia.' },
  { id:'m4', variant:'solo', label:'04 · Solo run', desc:'Single-player podio 1-place: avatar grande corona toolkit + score 56 Quicksand. KPI personali.' },
  { id:'m5', variant:'default', emptyAch: true, emptyPhotos: true, emptyChat: true, label:'05 · Empty everything', desc:'Stati vuoti achievements/foto/chat — illustrazioni + CTA contestuali per riprovare.' },
  { id:'m6', state:'loading', label:'06 · Loading', desc:'Skeleton hero 200 + KPI + tabella shimmer.' },
  { id:'m7', state:'error', label:'07 · Error', desc:'CTA Riprova in danger color, fallisce caricamento riepilogo.' },
];

function ThemeToggle() {
  const [dark, setDark] = us(false);
  ue(() => { document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light'); }, [dark]);
  return (
    <button onClick={() => setDark(d => !d)} className="theme-toggle" aria-label={dark ? 'Tema chiaro' : 'Tema scuro'}>
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
        <div style={{ fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em', marginBottom: 8 }}>SP4 wave 2 · #3/3 FINALE · Session summary 🏆</div>
        <h1>Session summary — /sessions/[id]</h1>
        <p className="lead">
          Pagina celebrativa post-game. Hero podio 3-place con confetti CSS-only + ConnectionBar prod 1:1 (6 pip incluso event empty).
          Body lineare scrollable: <strong>KPI grid · Scoring breakdown table/charts · Achievements 3-col · Diary timeline raggruppato per turno · Foto gallery 4-col · Chat highlights · ShareCard preview light/dark · PlayAgain</strong>.
          ConnectionBar pip cliccabili → smooth scroll alla sezione. Tone: festoso ma elegante, no Vegas. prefers-reduced-motion rispettato.
        </p>

        <div className="section-label">Mobile · 375 — 7 stati (default · tied · abandoned · solo · empty all · loading · error)</div>
        <div className="phones-grid">
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc}>
              <PhoneScreen variant={s.variant || 'default'} stateOverride={s.state} emptyAch={s.emptyAch} emptyPhotos={s.emptyPhotos} emptyChat={s.emptyChat}/>
            </PhoneShell>
          ))}
        </div>

        <div className="section-label">Desktop · 1440 — 5 stati chiave</div>
        <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
          <DesktopFrame label="08 · Desktop · Default winner"
            desc="Container 1280. Podio hero 320 con avatars 92/72/72 + 4° row. ConnectionBar sticky cliccabile. KPI 4-col + tabella scoring 6-col + achievements 3-col + diary turn 1/8/12/18 (T1 expanded) + photo grid 4-col + share card 4:3 preview + PlayAgain banner.">
            <DesktopFrameInner variant="default"/>
          </DesktopFrame>
          <DesktopFrame label="09 · Desktop · Pareggio" desc="2 podi 1° share — Marco e Anna entrambi corona toolkit + marker T avatar.">
            <DesktopFrameInner variant="tied"/>
          </DesktopFrame>
          <DesktopFrame label="10 · Desktop · Dark mode" dark desc="Confetti glow più visibile, gradient entity-session shines. Tutti gli accent toolkit/session entity-aware.">
            <DesktopFrameInner variant="default"/>
          </DesktopFrame>
          <DesktopFrame label="11 · Desktop · Abbandonata" desc="Hero compatto entity-event (no podio) + 2 CTA Riprendi/Archivia. Body sezioni mostrate comunque per consultazione.">
            <DesktopFrameInner variant="abandoned"/>
          </DesktopFrame>
          <DesktopFrame label="12 · Desktop · Empty achievements + foto + chat"
            desc="3 sezioni in empty state contestuale: achievements illustrazione 🏅, foto dashed entity-kb + CTA, chat fallback message.">
            <DesktopFrameInner variant="default" emptyAch emptyPhotos emptyChat/>
          </DesktopFrame>
        </div>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim { 0% { background-position: -200% 0; } 100% { background-position: 200% 0; } }
        @keyframes mai-confetti-fall {
          0% { transform: translateY(-30px) rotate(0deg); opacity: 0; }
          15% { opacity: 1; }
          100% { transform: translateY(180px) rotate(540deg); opacity: 0; }
        }
        .mai-confetti-piece { animation: mai-confetti-fall 2.4s cubic-bezier(.4,0,.2,1) forwards; }
        @media (prefers-reduced-motion: reduce) {
          .mai-confetti-piece { animation: none !important; opacity: .35; }
          .mai-shimmer { animation: none !important; }
        }
        .mai-shimmer {
          background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important;
          background-size: 200% 100% !important;
          animation: mai-shimmer-anim 1.4s linear infinite;
        }
        .phones-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(380px, 1fr)); gap: var(--s-7); align-items: start; }
        .stage h1 { font-size: var(--fs-3xl); }
        .stage .lead { line-height: var(--lh-relax); }
        .phone > div::-webkit-scrollbar { display: none; }
        .mai-cb-scroll::-webkit-scrollbar { display: none; }
        button:focus-visible, a:focus-visible { outline: 2px solid hsl(var(--c-session)); outline-offset: 2px; }
        @media print {
          .phones-grid, .stage > h1, .stage > .lead, .section-label, .theme-toggle { display: none !important; }
        }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);

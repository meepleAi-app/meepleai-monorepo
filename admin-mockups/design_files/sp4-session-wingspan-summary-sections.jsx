/* MeepleAI SP4 wave 2 — Session summary SECTIONS — window.SummarySections
   The original celebratory body of sp4-session-summary.jsx, modularised:
     SectionTitle · Achievements · Diary · PhotoGallery · ChatHighlights · ShareCard · PlayAgain
   Unchanged behaviour; consumes window.SummaryParts (SP) + window.MAI. */

(function () {
  const { useState: us } = React;
  const SP = window.SummaryParts;
  const eHs = SP.eH;

  const ACHIEVEMENTS = [
    { id: 'a1', em: '🦅', name: 'Master birder', desc: '15+ birds in single round', unlocked: true },
    { id: 'a2', em: '🌿', name: 'Habitat king', desc: '7+ in single column', unlocked: true },
    { id: 'a3', em: '⚡', name: 'Speed run', desc: 'Turno < 30s medio', unlocked: true },
    { id: 'a4', em: '🥚', name: 'Egg layer', desc: '25+ eggs deposte', unlocked: false, lockText: 'Servono 25 uova' },
    { id: 'a5', em: '🎯', name: 'Bonus hunter', desc: '5+ bonus card completate', unlocked: false, lockText: '5+ bonus card' },
    { id: 'a6', em: '🌍', name: 'World traveler', desc: 'Giocato 10+ giochi diversi', unlocked: false, lockText: '10 giochi distinti' },
  ];

  const DIARY = [
    { turn: 1, events: [
      { t: '13:08', kind: 'event', em: '▶', text: 'Sessione iniziata · setup completato' },
      { t: '13:09', kind: 'tool', em: '🎲', text: 'Marco ha tirato 4 (mano iniziale)' },
    ] },
    { turn: 8, events: [
      { t: '14:05', kind: 'score', em: '🐦', text: 'Anna ha giocato Bald Eagle (Forest +12)' },
      { t: '14:08', kind: 'photo', em: '📷', text: 'Foto tableau aggiunta' },
    ] },
    { turn: 12, events: [
      { t: '14:38', kind: 'event', em: '⏸', text: 'Pausa 14:32–14:38 (6m)' },
      { t: '14:43', kind: 'score', em: '🏆', text: 'Anna ha guadagnato +8 punti (Habitat)' },
      { t: '14:51', kind: 'agent', em: '🤖', text: 'Agente: "Considera la wetland..."' },
      { t: '14:58', kind: 'chat', em: '💬', text: 'Marco: "Quanto valgono i bird con uova multiple?"' },
    ] },
    { turn: 18, events: [
      { t: '15:30', kind: 'score', em: '🏆', text: 'Marco ha completato bonus card "Eggs in nest" (+16)' },
      { t: '15:32', kind: 'event', em: '⏹', text: 'Sessione terminata · score finalizzati' },
    ] },
  ];
  const eventColor = (k) => ({ score: 'session', tool: 'tool', agent: 'agent', chat: 'chat', photo: 'kb', event: 'event' }[k] || 'session');

  const SectionTitle = ({ em, children, action }) => (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10, gap: 8, flexWrap: 'wrap' }}>
      <h2 style={{ fontFamily: 'var(--f-display)', fontSize: 16, fontWeight: 800, color: 'var(--text)', margin: 0, display: 'inline-flex', alignItems: 'center', gap: 7 }}>
        <span aria-hidden="true">{em}</span>{children}
      </h2>
      {action}
    </div>
  );

  const Achievements = ({ empty, compact }) => (
    <section id="section-agent">
      <SectionTitle em="🏅">Achievements <span style={{ fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700, color: 'var(--text-muted)' }}>· 3 / 6 sbloccati</span></SectionTitle>
      {empty ? (
        <div style={{ padding: 24, borderRadius: 'var(--r-lg)', background: 'var(--bg-card)', border: '1px dashed var(--border-strong)', textAlign: 'center' }}>
          <div style={{ fontSize: 32, marginBottom: 8 }} aria-hidden="true">🏅</div>
          <div style={{ fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 800, color: 'var(--text)', marginBottom: 3 }}>Nessun achievement questa partita</div>
          <div style={{ fontSize: 12, color: 'var(--text-muted)' }}>Riprova per sbloccare nuovi badge!</div>
        </div>
      ) : (
        <div style={{ display: 'grid', gridTemplateColumns: compact ? 'repeat(2, 1fr)' : 'repeat(3, 1fr)', gap: 8 }}>
          {ACHIEVEMENTS.map(a => (
            <div key={a.id} title={a.unlocked ? a.desc : a.lockText} style={{ padding: '10px 12px', borderRadius: 'var(--r-md)', background: a.unlocked ? eHs('toolkit', 0.06) : 'var(--bg-muted)', border: a.unlocked ? `1px solid ${eHs('toolkit', 0.3)}` : '1px dashed var(--border)', display: 'flex', alignItems: 'center', gap: 9, opacity: a.unlocked ? 1 : 0.45 }}>
              <div style={{ width: 36, height: 36, borderRadius: '50%', background: a.unlocked ? eHs('toolkit', 0.18) : 'var(--bg-hover)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 18, flexShrink: 0 }} aria-hidden="true">{a.unlocked ? a.em : '🔒'}</div>
              <div style={{ minWidth: 0 }}>
                <div style={{ fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800, color: a.unlocked ? eHs('toolkit') : 'var(--text-muted)' }}>{a.name}</div>
                <div style={{ fontFamily: 'var(--f-mono)', fontSize: 9.5, color: 'var(--text-muted)', fontWeight: 700 }}>{a.unlocked ? a.desc : a.lockText}</div>
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  );

  const Diary = () => {
    const [filter, setFilter] = us('all');
    const [expanded, setExpanded] = us({ 1: true });
    const filters = [{ id: 'all', lb: 'Tutti' }, { id: 'score', lb: 'Score' }, { id: 'event', lb: 'Eventi' }, { id: 'chat', lb: 'Chat' }, { id: 'photo', lb: 'Foto' }];
    return (
      <section id="section-event">
        <SectionTitle em="📜">Diario partita</SectionTitle>
        <div style={{ display: 'flex', gap: 4, marginBottom: 10, flexWrap: 'wrap' }}>
          {filters.map(f => (
            <button key={f.id} type="button" onClick={() => setFilter(f.id)} aria-pressed={filter === f.id} style={{ padding: '4px 10px', borderRadius: 'var(--r-pill)', background: filter === f.id ? eHs('session', 0.14) : 'var(--bg-card)', border: filter === f.id ? `1px solid ${eHs('session', 0.4)}` : '1px solid var(--border)', color: filter === f.id ? eHs('session') : 'var(--text-sec)', fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 700, cursor: 'pointer' }}>{f.lb}</button>
          ))}
        </div>
        <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 'var(--r-lg)', overflow: 'hidden' }}>
          {DIARY.map((t, idx) => {
            const open = expanded[t.turn];
            const filtered = filter === 'all' ? t.events : t.events.filter(e => e.kind === filter);
            if (filter !== 'all' && filtered.length === 0) return null;
            return (
              <div key={t.turn} style={{ borderBottom: idx < DIARY.length - 1 ? '1px solid var(--border-light)' : 'none' }}>
                <button type="button" onClick={() => setExpanded(s => ({ ...s, [t.turn]: !s[t.turn] }))} style={{ width: '100%', padding: '10px 14px', background: 'transparent', border: 'none', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 10, color: 'var(--text)' }}>
                  <span style={{ padding: '2px 8px', borderRadius: 'var(--r-pill)', background: eHs('session', 0.12), color: eHs('session'), fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, textTransform: 'uppercase', letterSpacing: '.06em' }}>Turno {t.turn}</span>
                  <span style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)', fontWeight: 700 }}>{filtered.length} eventi</span>
                  <span style={{ flex: 1 }} />
                  <span aria-hidden="true" style={{ fontSize: 12, color: 'var(--text-muted)' }}>{open ? '▴' : '▾'}</span>
                </button>
                {open && (
                  <div style={{ padding: '4px 14px 14px', position: 'relative' }}>
                    <div aria-hidden="true" style={{ position: 'absolute', left: 24, top: 4, bottom: 14, width: 2, background: eHs('session', 0.2) }} />
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                      {filtered.map((e, ei) => {
                        const ec = eventColor(e.kind);
                        return (
                          <div key={ei} style={{ display: 'flex', gap: 9, paddingLeft: 4, position: 'relative' }}>
                            <div style={{ width: 20, height: 20, borderRadius: '50%', background: eHs(ec, 0.14), border: `2px solid ${eHs(ec, 0.4)}`, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 9, flexShrink: 0, zIndex: 1 }} aria-hidden="true">{e.em}</div>
                            <div style={{ flex: 1, padding: '3px 0' }}>
                              <span style={{ fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 800, color: eHs(ec), marginRight: 6, textTransform: 'uppercase', letterSpacing: '.06em' }}>{e.t}</span>
                              <span style={{ fontFamily: 'var(--f-body)', fontSize: 12, color: 'var(--text-sec)', fontWeight: 600 }}>{e.text}</span>
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

  const PhotoGallery = ({ empty, compact }) => (
    <section id="section-kb">
      <SectionTitle em="📷">Foto della partita {!empty && <span style={{ fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700, color: 'var(--text-muted)' }}>· 3</span>}</SectionTitle>
      {empty ? (
        <div style={{ padding: '30px 16px', borderRadius: 'var(--r-lg)', background: 'var(--bg-card)', border: '1px dashed var(--border-strong)', textAlign: 'center' }}>
          <div style={{ fontSize: 36, marginBottom: 8 }} aria-hidden="true">📷</div>
          <div style={{ fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 800, color: 'var(--text)', marginBottom: 3 }}>Nessuna foto — la prossima volta!</div>
          <button type="button" style={{ marginTop: 8, padding: '7px 14px', borderRadius: 'var(--r-md)', background: 'transparent', color: eHs('kb'), border: `1px dashed ${eHs('kb', 0.4)}`, fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 700, cursor: 'pointer' }}>+ Aggiungi foto</button>
        </div>
      ) : (
        <div style={{ display: 'grid', gridTemplateColumns: compact ? 'repeat(2, 1fr)' : 'repeat(4, 1fr)', gap: 8 }}>
          {[{ lb: 'Tableau T8', g: 'linear-gradient(135deg, hsl(85,60%,55%), hsl(180,50%,40%))' }, { lb: 'Wetland', g: 'linear-gradient(135deg, hsl(200,60%,55%), hsl(230,50%,40%))' }, { lb: 'Final scores', g: 'linear-gradient(135deg, hsl(40,70%,60%), hsl(10,60%,45%))' }].map((p, i) => (
            <button key={i} type="button" style={{ aspectRatio: '1', borderRadius: 'var(--r-md)', background: p.g, border: '1px solid var(--border)', position: 'relative', overflow: 'hidden', cursor: 'pointer', padding: 0 }}>
              <span style={{ position: 'absolute', bottom: 6, left: 6, fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 800, color: '#fff', background: 'rgba(0,0,0,.45)', padding: '2px 6px', borderRadius: 'var(--r-pill)' }}>{p.lb}</span>
            </button>
          ))}
          <button type="button" aria-label="Aggiungi foto" style={{ aspectRatio: '1', borderRadius: 'var(--r-md)', background: 'transparent', border: '1px dashed var(--border-strong)', color: 'var(--text-muted)', fontSize: 22, cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>+</button>
        </div>
      )}
    </section>
  );

  const ChatHighlights = ({ empty }) => {
    const [open, setOpen] = us(true);
    return (
      <section id="section-chat">
        <SectionTitle em="💬" action={<button type="button" onClick={() => setOpen(o => !o)} style={{ padding: '4px 10px', borderRadius: 'var(--r-pill)', background: 'var(--bg-card)', border: '1px solid var(--border)', color: 'var(--text-sec)', fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700, cursor: 'pointer' }}>{open ? '▴ Comprimi' : '▾ Espandi'}</button>}>
          Chat highlights {!empty && <span style={{ fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700, color: 'var(--text-muted)' }}>· 3 di 8</span>}
        </SectionTitle>
        {empty ? (
          <div style={{ padding: 20, borderRadius: 'var(--r-lg)', background: 'var(--bg-card)', border: '1px dashed var(--border-strong)', textAlign: 'center', fontSize: 12.5, color: 'var(--text-muted)' }}>Nessuna chat con l'agente in questa partita.</div>
        ) : open && (
          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 'var(--r-lg)', padding: 12, display: 'flex', flexDirection: 'column', gap: 10 }}>
            {[
              { from: 'user', t: '14:58', text: 'Quanto valgono i bird con uova multiple?' },
              { from: 'agent', t: '15:02', text: 'I bird con simbolo uovo nell\'angolo valgono 1 punto bonus per ogni uovo deposto.', citations: ['Wingspan §4.2', 'FAQ #12'] },
              { from: 'user', t: '15:08', text: 'Posso giocare un bird sopra il limite di habitat?' },
            ].map((m, i) => {
              const isAgent = m.from === 'agent';
              return (
                <div key={i} style={{ alignSelf: isAgent ? 'flex-start' : 'flex-end', maxWidth: '88%' }}>
                  <div style={{ padding: '7px 11px', borderRadius: 'var(--r-lg)', background: isAgent ? eHs('agent', 0.1) : eHs('chat', 0.12), border: `1px solid ${isAgent ? eHs('agent', 0.25) : eHs('chat', 0.25)}`, borderTopLeftRadius: isAgent ? 4 : 'var(--r-lg)', borderTopRightRadius: isAgent ? 'var(--r-lg)' : 4, fontFamily: 'var(--f-body)', fontSize: 12.5, fontWeight: 500, color: 'var(--text)', lineHeight: 1.45 }}>{m.text}</div>
                  <div style={{ display: 'flex', gap: 4, alignItems: 'center', marginTop: 3, padding: '0 4px', flexWrap: 'wrap' }}>
                    <span style={{ fontFamily: 'var(--f-mono)', fontSize: 9, color: 'var(--text-muted)', fontWeight: 700 }}>{m.t}</span>
                    {m.citations?.map(c => (
                      <span key={c} style={{ padding: '1px 5px', borderRadius: 'var(--r-pill)', background: eHs('kb', 0.1), color: eHs('kb'), border: `1px solid ${eHs('kb', 0.25)}`, fontFamily: 'var(--f-mono)', fontSize: 8.5, fontWeight: 800 }}>📄 {c}</span>
                    ))}
                  </div>
                </div>
              );
            })}
            <button type="button" style={{ alignSelf: 'center', marginTop: 4, padding: '5px 12px', borderRadius: 'var(--r-pill)', background: eHs('chat', 0.1), color: eHs('chat'), border: `1px solid ${eHs('chat', 0.3)}`, fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, cursor: 'pointer', textTransform: 'uppercase', letterSpacing: '.05em' }}>Vedi thread completo →</button>
          </div>
        )}
      </section>
    );
  };

  const ShareCard = ({ compact }) => {
    const [previewDark, setPreviewDark] = us(false);
    const FINAL = SP.FINAL;
    return (
      <section>
        <SectionTitle em="📤" action={
          <div role="radiogroup" style={{ display: 'inline-flex', borderRadius: 'var(--r-md)', border: '1px solid var(--border)', background: 'var(--bg-card)', overflow: 'hidden' }}>
            {['light', 'dark'].map(t => {
              const active = (t === 'dark') === previewDark;
              return (
                <button key={t} type="button" onClick={() => setPreviewDark(t === 'dark')} aria-checked={active} style={{ padding: '4px 10px', background: active ? eHs('session', 0.14) : 'transparent', color: active ? eHs('session') : 'var(--text-muted)', border: 'none', cursor: 'pointer', fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, textTransform: 'uppercase', letterSpacing: '.06em' }}>{t}</button>
              );
            })}
          </div>
        }>Share card preview</SectionTitle>
        <div style={{ background: previewDark ? '#0f0c1e' : '#fff', borderRadius: 'var(--r-lg)', border: `2px solid ${eHs('session', 0.3)}`, overflow: 'hidden', position: 'relative', aspectRatio: compact ? '1' : '4/3', maxWidth: compact ? '100%' : 600, margin: '0 auto', padding: compact ? '20px 16px' : '32px 28px', backgroundImage: previewDark ? `radial-gradient(circle at 20% 0%, ${eHs('session', 0.3)} 0%, transparent 60%), radial-gradient(circle at 80% 100%, ${eHs('toolkit', 0.18)} 0%, transparent 50%)` : `radial-gradient(circle at 20% 0%, ${eHs('session', 0.18)} 0%, transparent 60%), radial-gradient(circle at 80% 100%, ${eHs('toolkit', 0.1)} 0%, transparent 50%)` }}>
          <div style={{ position: 'absolute', top: 14, right: 16, fontSize: compact ? 32 : 56, opacity: 0.18 }} aria-hidden="true">🦜</div>
          <div style={{ position: 'relative', zIndex: 1, height: '100%', display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
            <div>
              <div style={{ display: 'inline-flex', alignItems: 'center', gap: 5, padding: '2px 8px', borderRadius: 'var(--r-pill)', background: eHs('toolkit', 0.18), color: eHs('toolkit'), fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800, textTransform: 'uppercase', letterSpacing: '.1em' }}>🏆 Vittoria</div>
              <h3 style={{ fontFamily: 'var(--f-display)', fontSize: compact ? 18 : 26, fontWeight: 800, color: previewDark ? '#fff' : '#0f0c1e', margin: '8px 0 2px', letterSpacing: '-.02em' }}>Marco vince Wingspan</h3>
              <div style={{ fontFamily: 'var(--f-mono)', fontSize: compact ? 10 : 11, color: previewDark ? 'rgba(255,255,255,.7)' : 'var(--text-muted)', fontWeight: 700 }}>23 apr 2026 · 1h 24min · 4 giocatori</div>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-around', alignItems: 'flex-end', gap: 8, marginTop: 12 }}>
              {[FINAL[1], FINAL[0], FINAL[2]].map((p, idx) => {
                const place = idx === 1 ? 1 : (idx === 0 ? 2 : 3);
                const isW = place === 1;
                const sz = isW ? (compact ? 38 : 56) : (compact ? 30 : 44);
                return (
                  <div key={p.id} style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 3 }}>
                    {isW && <span style={{ fontSize: compact ? 14 : 20 }}>🏆</span>}
                    <div style={{ width: sz, height: sz, borderRadius: '50%', background: `linear-gradient(135deg, hsl(${p.hue},70%,65%), hsl(${p.hue},60%,42%))`, color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontFamily: 'var(--f-display)', fontSize: isW ? (compact ? 17 : 24) : (compact ? 13 : 18), fontWeight: 800, border: isW ? `2px solid ${eHs('toolkit')}` : 'none' }}>{p.name[0]}</div>
                    <div style={{ fontFamily: 'var(--f-display)', fontSize: isW ? (compact ? 18 : 26) : (compact ? 13 : 18), fontWeight: 800, color: isW ? eHs('toolkit') : (previewDark ? '#fff' : '#0f0c1e'), fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{p.score}</div>
                  </div>
                );
              })}
            </div>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: 10 }}>
              <div style={{ fontFamily: 'var(--f-display)', fontSize: compact ? 10 : 12, fontWeight: 800, color: previewDark ? 'rgba(255,255,255,.5)' : 'var(--text-muted)', display: 'inline-flex', alignItems: 'center', gap: 4 }}>
                <span style={{ width: 14, height: 14, borderRadius: 4, background: `linear-gradient(135deg, ${eHs('game')}, ${eHs('event')})`, color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 9, fontWeight: 800 }}>M</span>
                MeepleAI
              </div>
              <div style={{ fontFamily: 'var(--f-mono)', fontSize: 9, color: previewDark ? 'rgba(255,255,255,.4)' : 'var(--text-muted)', fontWeight: 700 }}>meepleai.app</div>
            </div>
          </div>
        </div>
        <div style={{ display: 'flex', gap: 6, marginTop: 12, flexWrap: 'wrap', justifyContent: 'center' }}>
          {[{ lb: 'Twitter', em: '𝕏' }, { lb: 'Instagram', em: '📸' }, { lb: 'WhatsApp', em: '💬' }, { lb: 'Copia link', em: '🔗' }, { lb: 'Download PNG', em: '⬇' }].map(b => (
            <button key={b.lb} type="button" style={{ padding: '7px 12px', borderRadius: 'var(--r-md)', background: 'var(--bg-card)', border: `1px solid ${eHs('session', 0.3)}`, color: 'var(--text-sec)', fontFamily: 'var(--f-display)', fontSize: 11.5, fontWeight: 700, cursor: 'pointer', display: 'inline-flex', alignItems: 'center', gap: 4 }}><span aria-hidden="true">{b.em}</span>{b.lb}</button>
          ))}
        </div>
      </section>
    );
  };

  const PlayAgain = () => (
    <section style={{ padding: '18px 20px', borderRadius: 'var(--r-xl)', background: `linear-gradient(135deg, ${eHs('game', 0.1)} 0%, ${eHs('session', 0.1)} 100%)`, border: `1px solid ${eHs('game', 0.3)}`, display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 14, flexWrap: 'wrap' }}>
      <div style={{ flex: 1, minWidth: 200 }}>
        <h3 style={{ fontFamily: 'var(--f-display)', fontSize: 17, fontWeight: 800, color: 'var(--text)', margin: '0 0 3px', letterSpacing: '-.01em' }}>🎲 Pronti per la rivincita?</h3>
        <p style={{ fontFamily: 'var(--f-body)', fontSize: 12.5, color: 'var(--text-sec)', margin: 0 }}>Stessi giocatori e gioco — agente già caricato.</p>
      </div>
      <div style={{ display: 'flex', gap: 7, flexWrap: 'wrap' }}>
        <button type="button" style={{ padding: '10px 16px', borderRadius: 'var(--r-md)', background: eHs('session'), color: '#fff', border: 'none', fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800, cursor: 'pointer', boxShadow: `0 4px 14px ${eHs('session', 0.4)}`, display: 'inline-flex', alignItems: 'center', gap: 5 }}><span aria-hidden="true">▶</span>Riavvia con stessi player</button>
        <button type="button" style={{ padding: '10px 14px', borderRadius: 'var(--r-md)', background: 'transparent', color: eHs('game'), border: `1px solid ${eHs('game', 0.4)}`, fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800, cursor: 'pointer', display: 'inline-flex', alignItems: 'center', gap: 5 }}><span aria-hidden="true">🎲</span>Cambia gioco</button>
      </div>
    </section>
  );

  window.SummarySections = { SectionTitle, Achievements, Diary, PhotoGallery, ChatHighlights, ShareCard, PlayAgain };
})();

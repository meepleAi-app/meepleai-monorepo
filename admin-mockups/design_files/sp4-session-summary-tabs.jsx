/* MeepleAI SP4 wave 2 — Session summary NEW REVIEW TABS — window.SummaryReviewTabs
   3 consolidated post-game routes → tabs in a RightColumnTabs analogous to live:
     scoreboard → /sessions/[id]/scoreboard  (final standalone podium)
     notes      → /sessions/[id]/notes       (markdown editor)
     players    → /sessions/[id]/players      (post-game stats mgmt)
   States: default · empty · loading · error · offline
   (post-game has no live SSE stream — "offline" = cached/unsynced, the honest analog).
   Consumes window.MAI + window.SummaryParts. */

(function () {
  const { useState } = React;
  const M = window.MAI;
  const SP = window.SummaryParts;
  const eH = M.entityHsl;
  const { StateBlock, Shimmer } = M;
  const total = (id) => Object.values(M.BREAKDOWN[id]).reduce((a, b) => a + b, 0);

  const Body = ({ children }) => (
    <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', display: 'flex', flexDirection: 'column' }}>{children}</div>
  );
  const Pad = ({ children, gap = 10 }) => (
    <div style={{ padding: 12, display: 'flex', flexDirection: 'column', gap }}>{children}</div>
  );
  const Centered = ({ children }) => (<div style={{ flex: 1, display: 'flex', padding: 16 }}>{children}</div>);

  const OfflineBanner = ({ what = 'scoreboard' }) => (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 8, padding: '8px 12px', borderRadius: 'var(--r-md)', margin: 12,
      background: 'var(--bg-muted)', border: '1px solid var(--border-strong)',
      fontFamily: 'var(--f-mono)', fontSize: 10.5, fontWeight: 700, color: 'var(--text-sec)',
    }}>
      <span aria-hidden="true">⚡</span>
      <span style={{ flex: 1 }}><strong>Offline</strong> · {what} da cache locale · sync alla riconnessione</span>
    </div>
  );

  const cta = (e, fill) => ({
    padding: '8px 14px', borderRadius: 'var(--r-md)',
    background: fill ? eH(e) : eH(e, 0.12), color: fill ? '#fff' : eH(e),
    border: fill ? 'none' : `1px solid ${eH(e, 0.35)}`,
    fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800, cursor: 'pointer',
  });

  // ══════════════════════════════════════════════════
  // 1 · SCOREBOARD — final standalone podium
  // ══════════════════════════════════════════════════
  const ScoreboardTab = ({ state = 'default' }) => {
    if (state === 'loading') return <Body><Pad><Shimmer h={150} r="var(--r-lg)" />{[0, 1, 2, 3].map(i => <Shimmer key={i} h={44} r="var(--r-md)" />)}</Pad></Body>;
    if (state === 'error') return <Body><Centered><StateBlock tone="error" icon="⚠" title="Scoreboard non disponibile" body="Punteggi finali non recuperati." action={<button type="button" style={cta('event', true)}>↻ Riprova</button>} /></Centered></Body>;
    if (state === 'empty') return <Body><Centered><StateBlock icon="🏁" title="Punteggi non finalizzati" body="La partita non è ancora terminata: chiudi la sessione per generare lo scoreboard." action={<button type="button" style={cta('session')}>Termina partita</button>} /></Centered></Body>;
    const offline = state === 'offline';
    const winner = SP.FINAL[0];
    return (
      <Body>
        {offline && <OfflineBanner what="scoreboard" />}
        <Pad gap={12}>
          {/* compact podium */}
          <div style={{ borderRadius: 'var(--r-lg)', overflow: 'hidden', border: `1px solid ${eH('toolkit', 0.3)}` }}>
            <SP.SummaryHeroPodium variant="default" compact />
          </div>
          {/* ranked list w/ totals */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {SP.FINAL.map((p, i) => (
              <div key={p.id} style={{
                display: 'flex', alignItems: 'center', gap: 10, padding: '9px 11px', borderRadius: 'var(--r-md)',
                background: i === 0 ? eH('toolkit', 0.06) : 'var(--bg-card)',
                border: i === 0 ? `1px solid ${eH('toolkit', 0.3)}` : '1px solid var(--border)',
              }}>
                <span style={{ fontFamily: 'var(--f-mono)', fontSize: 12, fontWeight: 800, color: i === 0 ? eH('toolkit') : 'var(--text-muted)', width: 16 }}>{i + 1}</span>
                <M.ScoringInline player={p} size="sm" leader={i === 0} />
                <div style={{ flex: 1 }} />
                <span style={{ fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 700, color: 'var(--text-muted)' }}>{i === 0 ? '🏆 winner' : `+${winner.score - p.score} dal 1°`}</span>
              </div>
            ))}
          </div>
          <button type="button" style={{ ...cta('session', true), display: 'inline-flex', justifyContent: 'center', gap: 6 }}>📤 Esporta scoreboard</button>
        </Pad>
      </Body>
    );
  };

  // ══════════════════════════════════════════════════
  // 2 · NOTES — post-session markdown editor
  // ══════════════════════════════════════════════════
  const NotesTab = ({ state = 'default' }) => {
    const [preview, setPreview] = useState(false);
    if (state === 'loading') return <Body><Pad><Shimmer h={20} r="var(--r-sm)" style={{ width: 120 }} /><Shimmer h={180} r="var(--r-md)" /></Pad></Body>;
    if (state === 'error') return <Body><Centered><StateBlock tone="error" icon="⚠" title="Note non caricate" body="Impossibile aprire l'editor note." action={<button type="button" style={cta('event', true)}>↻ Riprova</button>} /></Centered></Body>;
    if (state === 'empty') return <Body><Centered><StateBlock icon="📝" title="Nessuna nota" body="Annota strategie, momenti chiave e regole da ricordare per la rivincita." action={<button type="button" style={cta('kb')}>+ Scrivi una nota</button>} /></Centered></Body>;
    const offline = state === 'offline';
    const md = '## Serata #3 — Wingspan\n\n**MVP:** Marco (bonus *Eggs in nest* +16)\n\n- Anna fortissima sul **Forest** early\n- Wetland sottovalutata fino a T10\n- [ ] Provare strategia _tucked cards_';
    return (
      <Body>
        {offline && <OfflineBanner what="bozza note" />}
        <Pad gap={8}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', flex: 1 }}>Note · Markdown</div>
            <div role="radiogroup" style={{ display: 'inline-flex', borderRadius: 'var(--r-md)', border: '1px solid var(--border)', overflow: 'hidden' }}>
              {[['Edit', false], ['Preview', true]].map(([lb, v]) => {
                const on = preview === v;
                return <button key={lb} type="button" onClick={() => setPreview(v)} aria-checked={on} style={{ padding: '4px 10px', background: on ? eH('kb', 0.14) : 'transparent', color: on ? eH('kb') : 'var(--text-muted)', border: 'none', cursor: 'pointer', fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 800 }}>{lb}</button>;
              })}
            </div>
          </div>
          {/* markdown toolbar */}
          {!preview && (
            <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
              {['B', 'I', 'H', '•', '☑', '🔗'].map(t => (
                <button key={t} type="button" style={{ width: 28, height: 28, borderRadius: 'var(--r-sm)', background: 'var(--bg-muted)', border: '1px solid var(--border)', color: 'var(--text-sec)', fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, cursor: 'pointer' }}>{t}</button>
              ))}
            </div>
          )}
          {preview ? (
            <div style={{ padding: '12px 14px', borderRadius: 'var(--r-md)', background: 'var(--bg-card)', border: '1px solid var(--border)', minHeight: 200 }}>
              <h4 style={{ fontFamily: 'var(--f-display)', fontSize: 15, fontWeight: 800, color: 'var(--text)', margin: '0 0 8px' }}>Serata #3 — Wingspan</h4>
              <p style={{ fontFamily: 'var(--f-body)', fontSize: 12.5, color: 'var(--text-sec)', margin: '0 0 8px' }}><strong style={{ color: 'var(--text)' }}>MVP:</strong> Marco (bonus <em>Eggs in nest</em> +16)</p>
              <ul style={{ margin: 0, paddingLeft: 18, fontFamily: 'var(--f-body)', fontSize: 12.5, color: 'var(--text-sec)', lineHeight: 1.7 }}>
                <li>Anna fortissima sul <strong>Forest</strong> early</li>
                <li>Wetland sottovalutata fino a T10</li>
                <li style={{ color: 'var(--text-muted)' }}>☐ Provare strategia <em>tucked cards</em></li>
              </ul>
            </div>
          ) : (
            <textarea defaultValue={md} style={{ width: '100%', minHeight: 200, padding: '12px 14px', borderRadius: 'var(--r-md)', border: '1px solid var(--border)', background: 'var(--bg-card)', color: 'var(--text)', fontFamily: 'var(--f-mono)', fontSize: 12, lineHeight: 1.6, resize: 'vertical' }} />
          )}
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 700, color: offline ? 'var(--text-muted)' : eH('toolkit'), flex: 1 }}>{offline ? '○ Bozza locale · non salvata' : '✓ Salvato 15:36'}</span>
            <button type="button" style={cta('kb', true)}>Salva note</button>
          </div>
        </Pad>
      </Body>
    );
  };

  // ══════════════════════════════════════════════════
  // 3 · PLAYERS — post-game stats management
  // ══════════════════════════════════════════════════
  const PSTATS = {
    'p-marco': { played: 24, wins: 11, avg: 78 },
    'p-anna':  { played: 31, wins: 14, avg: 81 },
    'p-luca':  { played: 18, wins: 5,  avg: 69 },
    'p-sara':  { played: 12, wins: 3,  avg: 64 },
  };
  const PlayersTab = ({ state = 'default' }) => {
    if (state === 'loading') return <Body><Pad>{[0, 1, 2, 3].map(i => <Shimmer key={i} h={72} r="var(--r-md)" />)}</Pad></Body>;
    if (state === 'error') return <Body><Centered><StateBlock tone="error" icon="⚠" title="Statistiche non disponibili" body="Impossibile caricare i profili giocatori." action={<button type="button" style={cta('event', true)}>↻ Riprova</button>} /></Centered></Body>;
    if (state === 'empty') return <Body><Centered><StateBlock icon="👥" title="Nessun profilo salvato" body="Salva i giocatori di questa partita per tracciarne le statistiche nel tempo." action={<button type="button" style={cta('player')}>Salva giocatori</button>} /></Centered></Body>;
    const offline = state === 'offline';
    return (
      <Body>
        {offline && <OfflineBanner what="statistiche" />}
        <Pad>
          <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em' }}>Giocatori · stats storiche</div>
          {SP.FINAL.map((p, i) => {
            const s = PSTATS[p.id];
            const wr = Math.round((s.wins / s.played) * 100);
            return (
              <div key={p.id} style={{ padding: '10px 11px', borderRadius: 'var(--r-md)', background: 'var(--bg-card)', border: '1px solid var(--border)', display: 'flex', flexDirection: 'column', gap: 8 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <div style={{ width: 34, height: 34, borderRadius: '50%', background: `linear-gradient(135deg, hsl(${p.hue},70%,64%), hsl(${p.hue},58%,44%))`, color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 800, flexShrink: 0 }}>{p.name[0]}</div>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800, color: 'var(--text)', display: 'flex', alignItems: 'center', gap: 5 }}>{p.name}{i === 0 && <span style={{ fontFamily: 'var(--f-mono)', fontSize: 8, fontWeight: 800, color: eH('toolkit'), background: eH('toolkit', 0.14), padding: '1px 5px', borderRadius: 'var(--r-pill)', textTransform: 'uppercase', letterSpacing: '.05em' }}>MVP</span>}</div>
                    <div style={{ fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 700, color: 'var(--text-muted)' }}>questa partita: {p.score} pt</div>
                  </div>
                  <button type="button" aria-label="Profilo" style={{ width: 26, height: 26, borderRadius: 'var(--r-sm)', background: 'var(--bg-muted)', border: '1px solid var(--border)', color: 'var(--text-muted)', cursor: 'pointer', fontSize: 12 }}>→</button>
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 6 }}>
                  {[['Partite', s.played, 'player'], ['Vittorie', `${s.wins} · ${wr}%`, 'toolkit'], ['Media', s.avg, 'session']].map(([lb, v, e]) => (
                    <div key={lb} style={{ padding: '6px 8px', borderRadius: 'var(--r-sm)', background: eH(e, 0.06), textAlign: 'center' }}>
                      <div style={{ fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 800, color: eH(e), fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{v}</div>
                      <div style={{ fontFamily: 'var(--f-mono)', fontSize: 8, fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.04em', marginTop: 2 }}>{lb}</div>
                    </div>
                  ))}
                </div>
              </div>
            );
          })}
        </Pad>
      </Body>
    );
  };

  window.SummaryReviewTabs = { ScoreboardTab, NotesTab, PlayersTab };
})();

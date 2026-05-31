/* MeepleAI SP4 — Generic Session Skeleton · POLYMORPHIC RENDERERS
   The heart of the skeleton. Nothing game-specific: every branch reads from
   the Toolkit JSON shape (toolkit.schemas.ts) passed in as `data`.

     ScoringPanelRenderer  → switch on scoring.scoreType
                             Points · Ranking · BinaryWin · Objectives
     TurnIndicatorRenderer → switch on turn.turnOrderType
                             RoundRobin · Sequential · Simultaneous ·
                             Realtime · None · Custom/Free
     ToolkitRenderer       → dispatch widgets[] → WidgetBlock
                             RandomGenerator · TurnManager · ScoreTracker ·
                             ResourceManager · NoteManager · Whiteboard

   Each renderer honours the 5 canonical states:
     default · empty · loading · error · sse-disconnect
   reusing MAI primitives (StateBlock · Shimmer · SseBanner) — not reinvented.

   Exposes window.SkeletonRenderers. */

(function () {
  const M = window.MAI;
  const eHsl = M.entityHsl;
  const { useState } = React;

  // ─── tiny shared atoms ───────────────────────────────────────────────────
  const mono = (size, weight, color) => ({
    fontFamily: 'var(--f-mono)', fontSize: size, fontWeight: weight, color,
  });
  const Label = ({ children, mt }) => (
    <div style={{ ...mono(10, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.08em', marginTop: mt }}>{children}</div>
  );
  const Avatar = ({ p, size = 26, ring }) => (
    <div aria-hidden="true" style={{
      width: size, height: size, borderRadius: '50%', flexShrink: 0,
      background: `linear-gradient(135deg, hsl(${p.hue},70%,64%), hsl(${p.hue},58%,44%))`,
      color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontFamily: 'var(--f-display)', fontSize: size * 0.46, fontWeight: 800,
      border: ring ? `2px solid ${eHsl('toolkit')}` : 'none',
    }}>{p.name[0]}</div>
  );
  const RetryBtn = ({ children = '↻ Riprova' }) => (
    <button type="button" style={{
      marginTop: 4, padding: '5px 12px', borderRadius: 'var(--r-pill)',
      background: eHsl('event', 0.12), color: eHsl('event'),
      border: `1px solid ${eHsl('event', 0.35)}`, cursor: 'pointer',
      ...mono(10, 800), textTransform: 'uppercase', letterSpacing: '.05em',
    }}>{children}</button>
  );

  // computation → icon + tint (drives the per-category UI variant)
  const COMPUTATION = {
    Count:     { g: '#', e: 'tool',    lb: 'Conteggio' },
    Sum:       { g: 'Σ', e: 'session', lb: 'Somma' },
    RankBased: { g: '①', e: 'toolkit', lb: 'Per rank' },
    Custom:    { g: '✎', e: 'agent',   lb: 'Manuale' },
  };
  const ComputationBadge = ({ c, size = 18 }) => {
    const m = COMPUTATION[c] || COMPUTATION.Custom;
    return (
      <span title={m.lb} aria-label={m.lb} style={{
        width: size, height: size, borderRadius: 'var(--r-sm)', flexShrink: 0,
        background: eHsl(m.e, 0.14), color: eHsl(m.e), border: `1px solid ${eHsl(m.e, 0.3)}`,
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        ...mono(size * 0.55, 800),
      }}>{m.g}</span>
    );
  };

  // ─── state scaffold: wraps any default render with the other 4 states ─────
  const Loading = ({ rows = 4, h = 30, pad = 14 }) => (
    <div style={{ padding: pad, display: 'flex', flexDirection: 'column', gap: 10 }} aria-busy="true" aria-label="Caricamento">
      {Array.from({ length: rows }).map((_, i) => <M.Shimmer key={i} h={h} />)}
    </div>
  );
  const Centered = ({ children }) => (
    <div style={{ flex: 1, minHeight: 160, display: 'flex', padding: 16 }}>{children}</div>
  );
  const StateScaffold = ({ state, empty, error, sseWhere, loading, children }) => {
    if (state === 'loading') return loading || <Loading />;
    if (state === 'empty') return <Centered><M.StateBlock {...empty} /></Centered>;
    if (state === 'error') return <Centered><M.StateBlock tone="error" icon="⚠️" action={<RetryBtn />} {...error} /></Centered>;
    if (state === 'sse') return (
      <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0 }}>
        <M.SseBanner where={sseWhere} />
        <div aria-live="off" style={{ opacity: 0.5, pointerEvents: 'none', filter: 'saturate(.7)', flex: 1, minHeight: 0, overflow: 'hidden' }}>{children}</div>
      </div>
    );
    return children;
  };

  const Panel = ({ children, gap = 12, pad = 14 }) => (
    <div style={{ padding: pad, display: 'flex', flexDirection: 'column', gap, overflowY: 'auto', flex: 1, minHeight: 0 }}>{children}</div>
  );
  const Meter = ({ value, max, e, danger }) => (
    <div style={{ height: 8, borderRadius: 'var(--r-pill)', background: 'var(--bg-sunken)', overflow: 'hidden' }}>
      <div style={{ width: `${Math.min(100, (value / max) * 100)}%`, height: '100%', borderRadius: 'var(--r-pill)', background: eHsl(danger ? 'event' : (e || 'toolkit')) }} />
    </div>
  );

  // ══════════════════════════════════════════════════════════════════════════
  // ScoringPanelRenderer — switch on scoreType
  // ══════════════════════════════════════════════════════════════════════════
  const ScoreTypePill = ({ type }) => (
    <span style={{
      ...mono(9, 800), textTransform: 'uppercase', letterSpacing: '.06em',
      padding: '2px 8px', borderRadius: 'var(--r-pill)',
      background: eHsl('session', 0.12), color: eHsl('session'), border: `1px solid ${eHsl('session', 0.3)}`,
    }}>scoreType · {type}</span>
  );

  // — Points: leaderboard + per-category computation breakdown —
  const PointsScoring = ({ data }) => {
    const ranked = [...data.players].sort((a, b) => b.score - a.score);
    const lead = ranked[0];
    return (
      <Panel>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
          <Label>Classifica live</Label><div style={{ flex: 1 }} /><ScoreTypePill type="Points" />
        </div>
        <div role="list" aria-live="polite" style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {ranked.map((p, i) => {
            const leader = p.id === lead.id;
            return (
              <div key={p.id} role="listitem" style={{
                display: 'flex', alignItems: 'center', gap: 10, padding: '8px 10px', borderRadius: 'var(--r-md)',
                background: leader ? eHsl('toolkit', 0.08) : 'var(--bg-card)',
                border: `1px solid ${leader ? eHsl('toolkit', 0.3) : 'var(--border)'}`,
              }}>
                <span style={{ ...mono(11, 800, leader ? eHsl('toolkit') : 'var(--text-muted)'), width: 16 }}>{i + 1}°</span>
                <Avatar p={p} ring={leader} />
                <span style={{ flex: 1, fontFamily: 'var(--f-display)', fontSize: 13.5, fontWeight: 800, color: 'var(--text)' }}>{p.name}</span>
                {p.turnDelta ? <span style={{ ...mono(10, 800, eHsl('toolkit')) }}>+{p.turnDelta}</span> : null}
                <span style={{ fontFamily: 'var(--f-display)', fontSize: 20, fontWeight: 800, color: leader ? eHsl('toolkit') : 'var(--text)', fontVariantNumeric: 'tabular-nums' }}>{p.score}</span>
              </div>
            );
          })}
        </div>
        <Label mt={4}>Dettaglio categorie · {data.scoring.categories.length}</Label>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          {data.scoring.categories.map(cat => (
            <div key={cat.id} title={cat.description} style={{
              display: 'flex', alignItems: 'center', gap: 8, padding: '7px 9px', borderRadius: 'var(--r-md)',
              background: 'var(--bg-muted)', border: '1px solid var(--border-light)',
            }}>
              <ComputationBadge c={cat.computation} />
              <span style={{ flex: 1, minWidth: 0, fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 700, color: 'var(--text)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{cat.label}</span>
              <div style={{ display: 'flex', gap: 4 }}>
                {ranked.map(p => (
                  <span key={p.id} title={p.name} style={{ ...mono(10.5, 700, 'var(--text-sec)'), minWidth: 18, textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}>
                    {data.breakdown?.[p.id]?.[cat.id] ?? '–'}
                  </span>
                ))}
              </div>
            </div>
          ))}
        </div>
      </Panel>
    );
  };

  // — Ranking: live ordered list with rank pills —
  const RankingScoring = ({ data }) => (
    <Panel>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
        <Label>{data.meta || 'Classifica'}</Label><div style={{ flex: 1 }} /><ScoreTypePill type="Ranking" />
      </div>
      <div role="list" aria-live="polite" style={{ display: 'flex', flexDirection: 'column', gap: 7 }}>
        {data.ranking.map(p => {
          const leader = p.rank === 1;
          return (
            <div key={p.id} role="listitem" style={{
              display: 'flex', alignItems: 'center', gap: 11, padding: '9px 11px', borderRadius: 'var(--r-md)',
              background: leader ? eHsl('toolkit', 0.08) : 'var(--bg-card)',
              border: `1px solid ${leader ? eHsl('toolkit', 0.3) : 'var(--border)'}`,
            }}>
              <span style={{
                width: 26, height: 26, borderRadius: 'var(--r-pill)', flexShrink: 0,
                background: leader ? eHsl('toolkit') : eHsl('session', 0.12),
                color: leader ? '#fff' : eHsl('session'),
                display: 'flex', alignItems: 'center', justifyContent: 'center', ...mono(12, 800),
              }}>{p.rank}</span>
              <Avatar p={p} ring={leader} />
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontFamily: 'var(--f-display)', fontSize: 13.5, fontWeight: 800, color: 'var(--text)' }}>{p.name}</div>
                <div style={{ ...mono(9.5, 700, 'var(--text-muted)') }}>{p.sub}</div>
              </div>
              {leader && <span style={{ fontSize: 16 }} aria-hidden="true">🏆</span>}
            </div>
          );
        })}
      </div>
    </Panel>
  );

  // — BinaryWin: collective outcome (no points) —
  const BinaryWinScoring = ({ data }) => {
    const c = data.scoring.collective || {};
    return (
      <Panel>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
          <Label>Esito collettivo</Label><div style={{ flex: 1 }} /><ScoreTypePill type="BinaryWin" />
        </div>
        <div style={{ padding: 14, borderRadius: 'var(--r-lg)', background: eHsl('session', 0.06), border: `1px solid ${eHsl('session', 0.25)}`, textAlign: 'center' }} aria-live="polite">
          <div style={{ fontSize: 26, marginBottom: 2 }} aria-hidden="true">🤝</div>
          <div style={{ fontFamily: 'var(--f-display)', fontSize: 15, fontWeight: 800, color: 'var(--text)' }}>Partita in corso</div>
          <div style={{ ...mono(10, 700, 'var(--text-muted)'), marginTop: 2 }}>nessun punteggio · vittoria/sconfitta condivisa</div>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
            <span style={{ fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800, color: eHsl('toolkit') }}>{c.goalLabel}</span>
            <span style={{ ...mono(11, 800, 'var(--text)') }}>{c.goalValue}/{c.goalMax} {c.goalHint}</span>
          </div>
          <Meter value={c.goalValue} max={c.goalMax} e="toolkit" />
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
            <span style={{ fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800, color: eHsl('event') }}>{c.failLabel}</span>
            <span style={{ ...mono(11, 800, 'var(--text)') }}>{c.failValue}/{c.failMax} · {c.failHint}</span>
          </div>
          <Meter value={c.failValue} max={c.failMax} danger />
        </div>
        <Label mt={4}>Condizioni</Label>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          {data.scoring.categories.map(cat => (
            <div key={cat.id} title={cat.description} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '7px 9px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)' }}>
              <ComputationBadge c={cat.computation} />
              <span style={{ flex: 1, minWidth: 0, fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 700, color: 'var(--text)' }}>{cat.label}</span>
              <span style={{ ...mono(10, 800, cat.weight > 0 ? eHsl('toolkit') : cat.weight < 0 ? eHsl('event') : 'var(--text-muted)') }}>
                {cat.weight > 0 ? 'vince' : cat.weight < 0 ? 'perde' : 'neutro'}
              </span>
            </div>
          ))}
        </div>
      </Panel>
    );
  };

  // — Objectives: checklist —
  const ObjectivesScoring = ({ data }) => {
    const done = data.objectives.filter(o => o.done).length;
    return (
      <Panel>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
          <Label>{data.meta || 'Obiettivi'}</Label><div style={{ flex: 1 }} /><ScoreTypePill type="Objectives" />
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
            <span style={{ fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800, color: 'var(--text)' }}>Completati</span>
            <span style={{ ...mono(11, 800, eHsl('toolkit')) }}>{done}/{data.objectives.length}</span>
          </div>
          <Meter value={done} max={data.objectives.length} e="toolkit" />
        </div>
        <div role="list" style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {data.objectives.map(o => (
            <div key={o.id} role="listitem" style={{
              display: 'flex', alignItems: 'center', gap: 10, padding: '9px 11px', borderRadius: 'var(--r-md)',
              background: o.done ? eHsl('toolkit', 0.07) : 'var(--bg-card)',
              border: `1px solid ${o.done ? eHsl('toolkit', 0.28) : 'var(--border)'}`,
            }}>
              <span aria-hidden="true" style={{
                width: 20, height: 20, borderRadius: 'var(--r-sm)', flexShrink: 0,
                background: o.done ? eHsl('toolkit') : 'transparent',
                border: o.done ? 'none' : `2px solid ${eHsl('session', 0.4)}`,
                color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 12, fontWeight: 800,
              }}>{o.done ? '✓' : ''}</span>
              <span style={{ flex: 1, fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 700, color: 'var(--text)', textDecoration: o.done ? 'line-through' : 'none', opacity: o.done ? 0.65 : 1 }}>{o.label}</span>
              {o.progress && <span style={{ ...mono(9.5, 700, 'var(--text-muted)'), flexShrink: 0 }}>{o.progress}</span>}
            </div>
          ))}
        </div>
      </Panel>
    );
  };

  const SCORING_BRANCH = { Points: PointsScoring, Ranking: RankingScoring, BinaryWin: BinaryWinScoring, Objectives: ObjectivesScoring };
  const ScoringPanelRenderer = ({ data, state = 'default' }) => {
    const type = data?.scoring?.scoreType;
    const Branch = SCORING_BRANCH[type];
    return (
      <StateScaffold state={state} sseWhere="punteggio"
        empty={{ icon: '🎯', title: 'Nessun punteggio', body: 'Il punteggio comparirà appena la prima azione viene registrata.' }}
        error={{ title: 'Punteggio non disponibile', body: 'Impossibile caricare il template di scoring dal toolkit.' }}>
        {Branch ? <Branch data={data} /> : <Centered><M.StateBlock icon="❔" title={`scoreType "${type}" sconosciuto`} body="Nessun renderer registrato per questo tipo." /></Centered>}
      </StateScaffold>
    );
  };

  // ══════════════════════════════════════════════════════════════════════════
  // TurnIndicatorRenderer — switch on turnOrderType
  // ══════════════════════════════════════════════════════════════════════════
  const TurnTypePill = ({ type }) => (
    <span style={{
      ...mono(9, 800), textTransform: 'uppercase', letterSpacing: '.06em',
      padding: '2px 8px', borderRadius: 'var(--r-pill)',
      background: eHsl('player', 0.12), color: eHsl('player'), border: `1px solid ${eHsl('player', 0.3)}`,
    }}>turnOrder · {type}</span>
  );

  // generic phase stepper (Sequential / Simultaneous / Custom)
  const PhaseStepper = ({ phases, activeIndex }) => (
    <ol style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: 0 }}>
      {phases.map((ph, i) => {
        const on = i === activeIndex, past = i < activeIndex;
        return (
          <li key={ph} aria-current={on ? 'step' : undefined} style={{ display: 'flex', alignItems: 'center', gap: 11, padding: '4px 0' }}>
            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', alignSelf: 'stretch' }}>
              <span style={{
                width: 24, height: 24, borderRadius: '50%', flexShrink: 0,
                background: on ? eHsl('player') : past ? eHsl('player', 0.2) : 'var(--bg-muted)',
                color: on ? '#fff' : past ? eHsl('player') : 'var(--text-muted)',
                border: on ? 'none' : `1px solid var(--border)`,
                display: 'flex', alignItems: 'center', justifyContent: 'center', ...mono(11, 800),
              }}>{past ? '✓' : i + 1}</span>
              {i < phases.length - 1 && <span style={{ width: 2, flex: 1, minHeight: 12, background: past ? eHsl('player', 0.3) : 'var(--border)' }} />}
            </div>
            <span style={{
              fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: on ? 800 : 600,
              color: on ? eHsl('player') : past ? 'var(--text-sec)' : 'var(--text-muted)', padding: '2px 0',
            }}>{ph}{on && <span style={{ ...mono(9, 800, eHsl('player')), marginLeft: 8 }}>◀ attuale</span>}</span>
          </li>
        );
      })}
    </ol>
  );

  const Banner = ({ e, icon, title, body }) => (
    <div role="status" style={{
      margin: 14, padding: 16, borderRadius: 'var(--r-lg)', textAlign: 'center',
      background: eHsl(e, 0.08), border: `1px solid ${eHsl(e, 0.3)}`,
    }}>
      <div style={{ fontSize: 30, marginBottom: 4 }} aria-hidden="true">{icon}</div>
      <div style={{ fontFamily: 'var(--f-display)', fontSize: 15, fontWeight: 800, color: eHsl(e) }}>{title}</div>
      <div style={{ ...mono(10.5, 700, 'var(--text-sec)'), marginTop: 4, lineHeight: 1.5 }}>{body}</div>
    </div>
  );

  const RoundRobinTurn = ({ data }) => {
    const t = data.turn, ts = data.turnState;
    const active = data.players.find(p => p.id === ts.activePlayerId) || data.players[0];
    const order = data.players;
    const totalTurns = t.turnsPerRound?.[ts.round - 1];
    return (
      <Panel>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
          <Label>Turno attivo</Label><div style={{ flex: 1 }} /><TurnTypePill type="RoundRobin" />
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, padding: 12, borderRadius: 'var(--r-lg)', background: eHsl('player', 0.07), border: `1px solid ${eHsl('player', 0.28)}` }} aria-live="polite">
          <Avatar p={active} size={44} />
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ ...mono(9.5, 800, eHsl('player')), textTransform: 'uppercase', letterSpacing: '.06em' }}>Sta giocando</div>
            <div style={{ fontFamily: 'var(--f-display)', fontSize: 17, fontWeight: 800, color: 'var(--text)' }}>{active.name}</div>
            {t.turnActions?.[ts.actionIndex] && <div style={{ ...mono(10, 700, 'var(--text-muted)') }}>azione: {t.turnActions[ts.actionIndex]}</div>}
          </div>
        </div>
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', marginBottom: 6 }}>
            <Label>Round {ts.round} / {t.rounds}</Label>
            <span style={{ ...mono(10, 800, 'var(--text-sec)') }}>turno {ts.turnInRound} / {totalTurns}</span>
          </div>
          <div style={{ display: 'flex', gap: 6 }}>
            {t.phases.map((ph, i) => {
              const on = i === ts.round - 1, past = i < ts.round - 1;
              return (
                <div key={ph} aria-current={on ? 'step' : undefined} style={{
                  flex: 1, textAlign: 'center', padding: '6px 2px', borderRadius: 'var(--r-md)',
                  background: on ? eHsl('player', 0.14) : past ? eHsl('player', 0.06) : 'var(--bg-muted)',
                  border: `1px solid ${on ? eHsl('player', 0.4) : 'var(--border-light)'}`,
                  color: on ? eHsl('player') : 'var(--text-muted)', ...mono(10, 800),
                }}>{i + 1}{on && t.turnsPerRound && <div style={{ ...mono(8, 700), opacity: 0.8 }}>{t.turnsPerRound[i]}t</div>}</div>
              );
            })}
          </div>
        </div>
        <Label mt={4}>Ordine di gioco · {t.direction}</Label>
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', alignItems: 'center' }}>
          {order.map((p, i) => (
            <React.Fragment key={p.id}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 5, padding: '4px 8px 4px 4px', borderRadius: 'var(--r-pill)', background: p.id === active.id ? eHsl('player', 0.12) : 'var(--bg-muted)', border: `1px solid ${p.id === active.id ? eHsl('player', 0.35) : 'var(--border-light)'}` }}>
                <Avatar p={p} size={20} />
                <span style={{ fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 700, color: 'var(--text)' }}>{p.name}</span>
              </div>
              {i < order.length - 1 && <span style={{ color: 'var(--text-muted)' }} aria-hidden="true">→</span>}
            </React.Fragment>
          ))}
        </div>
      </Panel>
    );
  };

  const SequentialTurn = ({ data }) => (
    <Panel>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
        <Label>{data.meta || 'Fasi'}</Label><div style={{ flex: 1 }} /><TurnTypePill type="Sequential" />
      </div>
      <div style={{ ...mono(10, 700, 'var(--text-muted)') }}>Sequenza di fasi a squadra — risolte in ordine fisso, non per giocatore.</div>
      <div style={{ padding: 12, borderRadius: 'var(--r-lg)', background: 'var(--bg-card)', border: '1px solid var(--border)' }}>
        <PhaseStepper phases={data.turn.phases} activeIndex={data.turnState.phaseIndex} />
      </div>
    </Panel>
  );

  const SimultaneousTurn = ({ data }) => (
    <Panel>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
        <Label>Gioco simultaneo</Label><div style={{ flex: 1 }} /><TurnTypePill type="Simultaneous" />
      </div>
      <Banner e="toolkit" icon="⚡" title="Tutti giocano insieme" body="Co-op simultaneo · nessun ordine di turno individuale" />
      <Label>Giocatori attivi · tutti</Label>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 7 }}>
        {data.players.map(p => (
          <div key={p.id} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 10px', borderRadius: 'var(--r-md)', background: eHsl('toolkit', 0.08), border: `1px solid ${eHsl('toolkit', 0.3)}` }}>
            <Avatar p={p} size={26} ring />
            <span style={{ fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800, color: 'var(--text)' }}>{p.name}</span>
          </div>
        ))}
      </div>
      {data.turn.phases?.length > 0 && (<>
        <Label mt={4}>Fase del giorno</Label>
        <div style={{ display: 'flex', gap: 6 }}>
          {data.turn.phases.map((ph, i) => {
            const on = i === data.turnState.phaseIndex;
            return <div key={ph} aria-current={on ? 'step' : undefined} style={{ flex: 1, textAlign: 'center', padding: '8px 4px', borderRadius: 'var(--r-md)', background: on ? eHsl('player', 0.14) : 'var(--bg-muted)', border: `1px solid ${on ? eHsl('player', 0.4) : 'var(--border-light)'}`, color: on ? eHsl('player') : 'var(--text-muted)', fontFamily: 'var(--f-display)', fontSize: 11.5, fontWeight: 800 }}>{ph}</div>;
          })}
        </div>
      </>)}
    </Panel>
  );

  const RealtimeTurn = ({ data }) => (
    <Panel>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
        <Label>Tempo reale</Label><div style={{ flex: 1 }} /><TurnTypePill type="Realtime" />
      </div>
      <Banner e="warning" icon="⏱" title="Nessun turno" body="Partita in tempo reale e simultanea · l'indicatore di turno non si applica" />
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '10px 12px', borderRadius: 'var(--r-md)', background: eHsl('agent', 0.08), border: `1px solid ${eHsl('agent', 0.3)}` }}>
        <span className="mai-pulse-dot" aria-hidden="true" style={{ color: eHsl('agent') }}>●</span>
        <span style={{ ...mono(10.5, 700, 'var(--text-sec)') }}>Tutti i giocatori agiscono in parallelo, senza attendere.</span>
      </div>
    </Panel>
  );

  const NoneTurn = ({ data }) => (
    <Panel>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
        <Label>Gioco libero</Label><div style={{ flex: 1 }} /><TurnTypePill type="None" />
      </div>
      <Banner e="session" icon="🎲" title="Nessun ordine di turno" body="Gioco a struttura libera · i giocatori procedono senza turni definiti" />
    </Panel>
  );

  const CustomTurn = ({ data }) => (
    <Panel>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
        <Label>{data.meta || 'Sequenza custom'}</Label><div style={{ flex: 1 }} /><TurnTypePill type={data.turn.turnOrderType} />
      </div>
      <div style={{ ...mono(10, 700, 'var(--text-muted)') }}>Fasi definite dal toolkit, ripetute ogni round.</div>
      <div style={{ padding: 12, borderRadius: 'var(--r-lg)', background: 'var(--bg-card)', border: '1px solid var(--border)' }}>
        <PhaseStepper phases={data.turn.phases} activeIndex={data.turnState.phaseIndex || 0} />
      </div>
    </Panel>
  );

  const TURN_BRANCH = {
    RoundRobin: RoundRobinTurn, Sequential: SequentialTurn, Simultaneous: SimultaneousTurn,
    Realtime: RealtimeTurn, None: NoneTurn, Custom: CustomTurn, Free: CustomTurn,
  };
  const TurnIndicatorRenderer = ({ data, state = 'default' }) => {
    const type = data?.turn?.turnOrderType;
    const Branch = TURN_BRANCH[type];
    return (
      <StateScaffold state={state} sseWhere="turni"
        empty={{ icon: '🎯', title: 'Turno non avviato', body: 'L\u2019indicatore comparirà all\u2019avvio della partita.' }}
        error={{ title: 'Turno non disponibile', body: 'Impossibile leggere il template dei turni dal toolkit.' }}>
        {Branch ? <Branch data={data} /> : <Centered><M.StateBlock icon="❔" title={`turnOrderType "${type}" sconosciuto`} body="Nessun renderer registrato." /></Centered>}
      </StateScaffold>
    );
  };

  // ══════════════════════════════════════════════════════════════════════════
  // ToolkitRenderer — dispatch widgets[] → WidgetBlock (6 types)
  // ══════════════════════════════════════════════════════════════════════════
  const WidgetShell = ({ icon, title, type, children, accent = 'tool', collapsed, onHeaderClick }) => (
    <div style={{ borderRadius: 'var(--r-md)', background: 'var(--bg-card)', border: `1px solid ${collapsed ? 'var(--border)' : eHsl(accent, 0.3)}`, overflow: 'hidden', flexShrink: 0 }}>
      <button type="button" onClick={onHeaderClick} aria-expanded={!collapsed} style={{ width: '100%', textAlign: 'left', display: 'flex', alignItems: 'center', gap: 8, padding: '8px 10px', background: eHsl(accent, collapsed ? 0.04 : 0.08), borderBottom: collapsed ? 'none' : `1px solid ${eHsl(accent, 0.18)}`, border: 'none', borderLeft: collapsed ? 'none' : `2px solid ${eHsl(accent)}`, cursor: onHeaderClick ? 'pointer' : 'default' }}>
        <span style={{ width: 24, height: 24, borderRadius: 'var(--r-sm)', background: eHsl(accent, 0.14), display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 13, flexShrink: 0 }} aria-hidden="true">{icon}</span>
        <span style={{ flex: 1, minWidth: 0, fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800, color: 'var(--text)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{title}</span>
        <span style={{ ...mono(8, 800, eHsl(accent)), textTransform: 'uppercase', letterSpacing: '.05em', padding: '1px 6px', borderRadius: 'var(--r-pill)', background: eHsl(accent, 0.1), flexShrink: 0 }}>{type}</span>
        {onHeaderClick && <span aria-hidden="true" style={{ ...mono(11, 800, eHsl(accent)), flexShrink: 0, transform: collapsed ? 'rotate(-90deg)' : 'none', transition: 'transform var(--dur-sm) var(--ease-out)' }}>▾</span>}
      </button>
      {!collapsed && <div style={{ padding: 10 }}>{children}</div>}
    </div>
  );
  const Stepper = ({ value, color = 'tool', wide }) => (
    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
      <button type="button" aria-label="meno" style={{ width: 24, height: 24, borderRadius: 'var(--r-sm)', background: 'var(--bg-muted)', border: '1px solid var(--border)', color: 'var(--text-sec)', cursor: 'pointer', fontWeight: 800 }}>–</button>
      <span style={{ ...mono(14, 800, 'var(--text)'), minWidth: wide ? 34 : 20, textAlign: 'center', fontVariantNumeric: 'tabular-nums' }}>{value}</span>
      <button type="button" aria-label="più" style={{ width: 24, height: 24, borderRadius: 'var(--r-sm)', background: eHsl(color, 0.12), border: `1px solid ${eHsl(color, 0.3)}`, color: eHsl(color), cursor: 'pointer', fontWeight: 800 }}>+</button>
    </div>
  );

  const RandomGeneratorBlock = ({ w, shell }) => (
    <WidgetShell icon="🎲" title={w.config.name || 'Generatore'} type="RandomGenerator" accent="tool" {...shell}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
        <div style={{ flex: 1, padding: '10px 12px', borderRadius: 'var(--r-md)', background: eHsl('tool', 0.06), border: `1px dashed ${eHsl('tool', 0.3)}` }}>
          <div style={{ ...mono(8.5, 800, eHsl('tool')), textTransform: 'uppercase', letterSpacing: '.06em' }}>Ultimo</div>
          <div style={{ ...mono(16, 800, 'var(--text)'), marginTop: 2 }}>{w.config.last || '—'}</div>
        </div>
        <button type="button" style={{ padding: '10px 14px', borderRadius: 'var(--r-md)', background: eHsl('tool'), color: '#06222b', border: 'none', fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800, cursor: 'pointer', flexShrink: 0 }}>Genera</button>
      </div>
      {w.config.faces && <div style={{ ...mono(9.5, 700, 'var(--text-muted)'), marginTop: 8 }}>{w.config.quantity}× · facce: {w.config.faces.join(' ')}</div>}
    </WidgetShell>
  );
  const TurnManagerBlock = ({ w, shell }) => (
    <WidgetShell icon="🔄" title="Gestore turni" type="TurnManager" accent="player" {...shell}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <button type="button" style={{ padding: '8px 10px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border)', color: 'var(--text-sec)', cursor: 'pointer', fontWeight: 800 }}>‹ Prec</button>
        <div style={{ flex: 1, textAlign: 'center' }}>
          <div style={{ ...mono(8.5, 800, eHsl('player')), textTransform: 'uppercase' }}>{w.config.phaseBased ? 'Fase' : 'Turno di'}</div>
          <div style={{ fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 800, color: 'var(--text)' }}>{w.config.phaseBased ? 'Giorno' : '—'}</div>
        </div>
        <button type="button" style={{ padding: '8px 10px', borderRadius: 'var(--r-md)', background: eHsl('player', 0.12), border: `1px solid ${eHsl('player', 0.3)}`, color: eHsl('player'), cursor: 'pointer', fontWeight: 800 }}>Pross ›</button>
      </div>
    </WidgetShell>
  );
  const ScoreTrackerBlock = ({ w, players = [], shell }) => (
    <WidgetShell icon="🧮" title="Segna punti" type="ScoreTracker" accent="session" {...shell}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 7 }}>
        {(players.length ? players : [{ id: 'x', name: 'Giocatore', hue: 240, score: 0 }]).slice(0, 4).map(p => (
          <div key={p.id} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <Avatar p={p} size={22} />
            <span style={{ flex: 1, fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 700, color: 'var(--text)' }}>{p.name}</span>
            <Stepper value={p.score ?? 0} color="session" wide />
          </div>
        ))}
      </div>
    </WidgetShell>
  );
  const ResourceManagerBlock = ({ w, shell }) => (
    <WidgetShell icon="📦" title={w.config.shared ? 'Risorse condivise' : 'Risorse'} type="ResourceManager" accent="kb" {...shell}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        {(w.config.resources || []).map(r => (
          <div key={r.label} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '4px 6px', borderRadius: 'var(--r-sm)', background: r.danger ? eHsl('event', 0.07) : 'transparent', border: r.danger ? `1px solid ${eHsl('event', 0.25)}` : '1px solid transparent' }}>
            <span style={{ flex: 1, fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 700, color: r.danger ? eHsl('event') : 'var(--text)' }}>{r.label}</span>
            <span style={{ ...mono(9, 700, 'var(--text-muted)') }}>/{r.max}</span>
            <Stepper value={r.value} color={r.danger ? 'event' : 'kb'} />
          </div>
        ))}
      </div>
    </WidgetShell>
  );
  const NoteManagerBlock = ({ w, shell }) => (
    <WidgetShell icon="📝" title="Note" type="NoteManager" accent="kb" {...shell}>
      <textarea defaultValue={w.config.text || ''} rows={3} placeholder="Scrivi una nota condivisa…" style={{ width: '100%', padding: '8px 10px', borderRadius: 'var(--r-sm)', border: '1px solid var(--border)', background: 'var(--bg-sunken)', color: 'var(--text)', fontFamily: 'var(--f-mono)', fontSize: 11.5, lineHeight: 1.5, resize: 'vertical' }} />
    </WidgetShell>
  );
  const WhiteboardBlock = ({ w, shell }) => (
    <WidgetShell icon="🖊" title="Lavagna" type="Whiteboard" accent="chat" {...shell}>
      <div style={{ display: 'flex', gap: 5, marginBottom: 8 }}>
        {['✏️', '⬛', '⭕', '🩹'].map((t, i) => <button key={i} type="button" aria-label={`strumento ${i + 1}`} style={{ width: 28, height: 28, borderRadius: 'var(--r-sm)', background: i === 0 ? eHsl('chat', 0.14) : 'var(--bg-muted)', border: `1px solid ${i === 0 ? eHsl('chat', 0.3) : 'var(--border)'}`, cursor: 'pointer', fontSize: 13 }}>{t}</button>)}
      </div>
      <div style={{ height: 86, borderRadius: 'var(--r-sm)', border: `1px dashed ${eHsl('chat', 0.3)}`, background: 'repeating-linear-gradient(45deg, var(--bg-sunken), var(--bg-sunken) 9px, var(--bg-muted) 9px, var(--bg-muted) 18px)', display: 'flex', alignItems: 'center', justifyContent: 'center', ...mono(9.5, 700, 'var(--text-muted)') }}>area disegno condivisa</div>
    </WidgetShell>
  );

  const WIDGET_BRANCH = {
    RandomGenerator: RandomGeneratorBlock, TurnManager: TurnManagerBlock, ScoreTracker: ScoreTrackerBlock,
    ResourceManager: ResourceManagerBlock, NoteManager: NoteManagerBlock, Whiteboard: WhiteboardBlock,
  };
  const WIDGET_META = {
    RandomGenerator: { icon: '🎲', lb: 'Random' }, TurnManager: { icon: '🔄', lb: 'Turni' },
    ScoreTracker: { icon: '🧮', lb: 'Punti' }, ResourceManager: { icon: '📦', lb: 'Risorse' },
    NoteManager: { icon: '📝', lb: 'Note' }, Whiteboard: { icon: '🖊', lb: 'Lavagna' },
  };
  const WidgetBlock = ({ widget, players, collapsed, onHeaderClick }) => {
    const Branch = WIDGET_BRANCH[widget.type];
    if (!Branch) return <Centered><M.StateBlock icon="❔" title={`widget "${widget.type}"`} body="Tipo non riconosciuto dal dispatcher." /></Centered>;
    const shell = onHeaderClick != null ? { collapsed, onHeaderClick } : undefined;
    return <Branch w={widget} players={players} shell={shell} />;
  };

  const ToolkitRenderer = ({ data, widgets, players, state = 'default', accordion = true }) => {
    const list = (widgets || data?.widgets || []);
    const enabled = list.filter(w => w.isEnabled).sort((a, b) => a.displayOrder - b.displayOrder);
    const disabled = list.filter(w => !w.isEnabled);
    const [open, setOpen] = useState(null);
    const openId = open || enabled[0]?.id;
    return (
      <StateScaffold state={state} sseWhere="widget"
        empty={{ icon: '🧰', title: 'Nessun widget attivo', body: 'Abilita i widget dal toolkit per usarli durante la sessione.', action: <button type="button" style={{ marginTop: 4, padding: '6px 12px', borderRadius: 'var(--r-pill)', background: eHsl('toolkit', 0.12), color: eHsl('toolkit'), border: `1px solid ${eHsl('toolkit', 0.35)}`, cursor: 'pointer', ...mono(10, 800) }}>Apri toolkit</button> }}
        error={{ title: 'Toolkit non disponibile', body: 'Impossibile caricare la dashboard dei widget.' }}>
        <Panel gap={10}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <Label>Widget attivi · {enabled.length}</Label>
            <div style={{ flex: 1 }} />
            <span style={{ ...mono(9, 800, eHsl('toolkit')), textTransform: 'uppercase', letterSpacing: '.06em', padding: '2px 8px', borderRadius: 'var(--r-pill)', background: eHsl('toolkit', 0.12), border: `1px solid ${eHsl('toolkit', 0.3)}` }}>ToolkitDashboard</span>
          </div>
          {enabled.length === 0
            ? <Centered><M.StateBlock icon="🧰" title="Tutti i widget disattivati" body="Nessun widget abilitato in questa sessione." /></Centered>
            : enabled.map(w => accordion
                ? <WidgetBlock key={w.id} widget={w} players={players} collapsed={w.id !== openId} onHeaderClick={() => setOpen(w.id)} />
                : <WidgetBlock key={w.id} widget={w} players={players} />)}
          {disabled.length > 0 && (
            <div style={{ marginTop: 2 }}>
              <Label>Disattivati · {disabled.length}</Label>
              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginTop: 6 }}>
                {disabled.map(w => {
                  const m = WIDGET_META[w.type] || { icon: '❔', lb: w.type };
                  return <span key={w.id} style={{ display: 'inline-flex', alignItems: 'center', gap: 5, padding: '4px 9px', borderRadius: 'var(--r-pill)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)', ...mono(10, 700, 'var(--text-muted)') }}><span aria-hidden="true">{m.icon}</span>{m.lb}</span>;
                })}
              </div>
            </div>
          )}
        </Panel>
      </StateScaffold>
    );
  };

  window.SkeletonRenderers = {
    ScoringPanelRenderer, TurnIndicatorRenderer, ToolkitRenderer, WidgetBlock,
    ComputationBadge, COMPUTATION, WIDGET_META, Avatar, Label, mono, Panel, StateScaffold, Centered,
  };
})();

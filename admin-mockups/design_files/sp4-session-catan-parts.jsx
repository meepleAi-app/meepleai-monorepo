/* MeepleAI SP4 · Coloni di Catan — PARTS  (window.CatanParts)
   Game-specific panels + layout that sit ON TOP of the universal skeleton.

   Reused verbatim from the skeleton (window.SkeletonParts):
     TopBar · ChatAgentPanel · ActionLogTimeline · StateSwitch
   Reused renderers (window.SkeletonRenderers):
     ScoringPanelRenderer (Points) · StateScaffold
   Adds (Catan-specific):
     SectionCard · PlayerStateCard · PlayerRail · ResourceBank · DevDeck ·
     TurnPhaseBar · RobberBanner · BoardPanel · TradePanel · DevCardsPanel ·
     BuildPanel · CatanRightTabs · DesktopBody · MobileBody · PhoneShell

   Reads window.CATAN (data) · window.CatanFlavor (atoms) · window.MAI (eHsl). */

(function () {
  const M = window.MAI;
  const C = window.CATAN;
  const F = window.CatanFlavor;
  const S = window.SkeletonParts;
  const R = window.SkeletonRenderers;
  const eHsl = M.entityHsl;
  const { useState } = React;
  const { mono, disp, playerOf } = F;

  const STATES = window.SkeletonData.STATES;

  // ─── SectionCard — titled container ───────────────────────────────────────
  const SectionCard = ({ title, entity = 'session', accent, icon, right, children, pad = 12, scroll }) => (
    <section style={{ background: 'var(--bg-card)', border: `1px solid ${accent ? eHsl(entity, 0.3) : 'var(--border)'}`, borderRadius: 'var(--r-lg)', overflow: 'hidden', display: 'flex', flexDirection: 'column', minHeight: 0 }}>
      {title && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 7, padding: '8px 11px', background: accent ? eHsl(entity, 0.06) : 'transparent', borderBottom: '1px solid var(--border-light)', borderLeft: accent ? `2px solid ${eHsl(entity)}` : 'none', flexShrink: 0 }}>
          {icon && <span aria-hidden="true" style={{ fontSize: 13 }}>{icon}</span>}
          <span style={{ ...mono(10, 800, accent ? eHsl(entity) : 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', flex: 1, minWidth: 0 }}>{title}</span>
          {right}
        </div>
      )}
      <div style={{ padding: pad, ...(scroll ? { overflowY: 'auto', minHeight: 0 } : {}) }}>{children}</div>
    </section>
  );

  // ─── PieceCounter — remaining pieces glyph ────────────────────────────────
  const PieceCounter = ({ icon, lb, remaining, total }) => (
    <div title={`${lb}: ${remaining} su ${total} disponibili`} style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 2, flex: 1, padding: '5px 2px', borderRadius: 'var(--r-sm)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)' }}>
      <span aria-hidden="true" style={{ fontSize: 13 }}>{icon}</span>
      <span style={{ ...mono(11, 800, remaining === 0 ? eHsl('event') : 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>{remaining}<span style={{ color: 'var(--text-muted)', fontWeight: 600, fontSize: 9 }}>/{total}</span></span>
      <span style={{ ...mono(7.5, 700, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.04em' }}>{lb}</span>
    </div>
  );

  // ─── PlayerStateCard — one player in the left rail ────────────────────────
  const PlayerStateCard = ({ p, active, compact }) => {
    const T = C.TOTAL_PIECES;
    const rem = { settlements: T.settlements - p.pieces.settlements, cities: T.cities - p.pieces.cities, roads: T.roads - p.pieces.roads };
    return (
      <div style={{
        borderRadius: 'var(--r-lg)', overflow: 'hidden', flexShrink: 0,
        background: active ? eHsl('session', 0.06) : 'var(--bg-card)',
        border: `1px solid ${active ? eHsl('session', 0.45) : 'var(--border)'}`,
        boxShadow: active ? `0 3px 14px ${eHsl('session', 0.18)}` : 'var(--shadow-xs)',
      }}>
        {/* header: piece-color stripe + avatar + name + VP */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 10px', borderBottom: '1px solid var(--border-light)', borderTop: `3px solid ${p.pc}` }}>
          <div aria-hidden="true" style={{ width: 30, height: 30, borderRadius: '50%', flexShrink: 0, background: `linear-gradient(135deg, hsl(${p.hue},70%,64%), hsl(${p.hue},58%,44%))`, color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', ...disp(14, 800, '#fff'), border: active ? `2px solid ${eHsl('session')}` : '2px solid var(--bg-card)' }}>{p.name[0]}</div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
              <span style={{ ...disp(13.5, 800, 'var(--text)') }}>{p.name}</span>
              <F.PlayerDot p={p} size={9} />
              {active && <span className="mai-pulse-dot-host" style={{ ...mono(7.5, 800, eHsl('session')), textTransform: 'uppercase', letterSpacing: '.05em', padding: '1px 5px', borderRadius: 'var(--r-pill)', background: eHsl('session', 0.14), border: `1px solid ${eHsl('session', 0.35)}` }}>turno</span>}
            </div>
            <div style={{ ...mono(8.5, 700, 'var(--text-muted)') }}>{p.pieces.settlements} ins · {p.pieces.cities} città · {p.pieces.roads} strade</div>
          </div>
          <F.VPBadge vp={p.vp} leader={active} />
        </div>
        <div style={{ padding: '8px 10px', display: 'flex', flexDirection: 'column', gap: 8 }}>
          {/* hand */}
          <F.ResourceHandBar hand={p.hand} hidden={!active} label={active ? 'Mano (visibile)' : 'Mano avversario'} />
          {/* pieces remaining */}
          <div>
            <div style={{ ...mono(9, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 4 }}>Pezzi rimasti</div>
            <div style={{ display: 'flex', gap: 5 }}>
              <PieceCounter icon="🏠" lb="ins." remaining={rem.settlements} total={T.settlements} />
              <PieceCounter icon="🏛️" lb="città" remaining={rem.cities} total={T.cities} />
              <PieceCounter icon="🛤️" lb="strade" remaining={rem.roads} total={T.roads} />
            </div>
          </div>
          {/* dev + badges */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, padding: '3px 8px', borderRadius: 'var(--r-pill)', background: eHsl('toolkit', 0.1), border: `1px solid ${eHsl('toolkit', 0.28)}`, ...mono(9.5, 800, eHsl('toolkit')) }}>
              <span aria-hidden="true">🃏</span>{p.dev.held}<span style={{ color: 'var(--text-muted)', fontWeight: 600 }}>sviluppo</span>
            </span>
            <span title="Cavalieri giocati" style={{ display: 'inline-flex', alignItems: 'center', gap: 4, padding: '3px 8px', borderRadius: 'var(--r-pill)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)', ...mono(9.5, 800, 'var(--text-sec)') }}>
              <span aria-hidden="true">⚔️</span>{p.dev.knightsPlayed}
            </span>
          </div>
          {(p.badges.longestRoad || p.badges.largestArmy) && (
            <div style={{ display: 'flex', gap: 5, flexWrap: 'wrap' }}>
              {p.badges.longestRoad && <F.LongestRoadBadge on len={p.longestRoadLen} />}
              {p.badges.largestArmy && <F.LargestArmyBadge on n={p.knights} />}
            </div>
          )}
        </div>
      </div>
    );
  };

  const PlayerRail = ({ players = C.PLAYERS }) => (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      <div style={{ ...mono(10, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>Giocatori · {players.length}</div>
      {players.map(p => <PlayerStateCard key={p.id} p={p} active={p.current} />)}
    </div>
  );

  // ─── ResourceBank · DevDeck (shared) ──────────────────────────────────────
  const ResourceBank = ({ bank = C.BANK }) => (
    <SectionCard title="Banca risorse" entity="kb" accent icon="🏦">
      <div style={{ display: 'flex', flexDirection: 'column', gap: 7 }}>
        {C.RES_ORDER.map(k => {
          const v = bank[k], low = v <= 8;
          return (
            <div key={k} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <F.ResourceToken id={k} size={18} />
              <span style={{ flex: 1, ...disp(12, 700, 'var(--text)') }}>{C.RES[k].lb}</span>
              <div style={{ flex: 1.4, height: 7, borderRadius: 'var(--r-pill)', background: 'var(--bg-sunken)', overflow: 'hidden' }}>
                <div style={{ width: `${(v / 19) * 100}%`, height: '100%', borderRadius: 'var(--r-pill)', background: low ? eHsl('event') : eHsl('kb') }} />
              </div>
              <span style={{ ...mono(11, 800, low ? eHsl('event') : 'var(--text)'), minWidth: 30, textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}>{v}<span style={{ color: 'var(--text-muted)', fontWeight: 600 }}>/19</span></span>
            </div>
          );
        })}
      </div>
    </SectionCard>
  );

  const DevDeck = ({ deck = C.DEV_DECK }) => (
    <SectionCard title="Mazzo sviluppo" entity="toolkit" accent icon="🃏">
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <div style={{ position: 'relative', width: 44, height: 60, flexShrink: 0 }} aria-hidden="true">
          {[0, 1, 2].map(i => (
            <div key={i} style={{ position: 'absolute', inset: 0, transform: `translate(${i * 3}px, ${-i * 3}px)`, borderRadius: 'var(--r-sm)', background: 'linear-gradient(150deg, hsl(150,30%,30%), hsl(150,35%,20%))', border: '1.5px solid hsl(150,30%,16%)', boxShadow: '0 2px 5px rgba(0,0,0,.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 18 }}>{i === 2 ? '🃏' : ''}</div>
          ))}
        </div>
        <div style={{ flex: 1 }}>
          <div style={{ ...disp(22, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{deck.remaining}<span style={{ ...mono(11, 700, 'var(--text-muted)') }}> / {deck.total}</span></div>
          <div style={{ ...mono(8.5, 700, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.05em', marginTop: 2 }}>carte nel mazzo</div>
          <div style={{ height: 6, borderRadius: 'var(--r-pill)', background: 'var(--bg-sunken)', overflow: 'hidden', marginTop: 6 }}>
            <div style={{ width: `${(deck.remaining / deck.total) * 100}%`, height: '100%', background: eHsl('toolkit') }} />
          </div>
        </div>
      </div>
    </SectionCard>
  );

  // ─── TurnPhaseBar — active player + 3 phases + dice ───────────────────────
  const TurnPhaseBar = ({ ds = C.ds, dice = C.DICE, onRoll }) => {
    const ts = ds.turnState, t = ds.turn;
    const active = ds.players.find(p => p.id === ts.activePlayerId) || ds.players[0];
    return (
      <div style={{ display: 'flex', gap: 12, alignItems: 'stretch', flexWrap: 'wrap' }}>
        {/* active player + phases */}
        <div style={{ flex: '1 1 320px', minWidth: 0, display: 'flex', flexDirection: 'column', gap: 8, padding: 12, borderRadius: 'var(--r-lg)', background: eHsl('player', 0.06), border: `1px solid ${eHsl('player', 0.28)}` }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }} aria-live="polite">
            <div aria-hidden="true" style={{ width: 38, height: 38, borderRadius: '50%', flexShrink: 0, background: `linear-gradient(135deg, hsl(${active.hue},70%,64%), hsl(${active.hue},58%,44%))`, color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', ...disp(16, 800, '#fff'), border: `2px solid ${active.pc}` }}>{active.name[0]}</div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ ...mono(8.5, 800, eHsl('player')), textTransform: 'uppercase', letterSpacing: '.06em' }}>Turno di · round {ts.round}</div>
              <div style={{ ...disp(16, 800, 'var(--text)') }}>{active.name}</div>
            </div>
            <span style={{ ...mono(9, 800, eHsl('player')), textTransform: 'uppercase', letterSpacing: '.05em', padding: '2px 8px', borderRadius: 'var(--r-pill)', background: eHsl('player', 0.12), border: `1px solid ${eHsl('player', 0.3)}` }}>↻ {t.direction === 'clockwise' ? 'orario' : t.direction}</span>
          </div>
          <div style={{ display: 'flex', gap: 6 }}>
            {t.phases.map((ph, i) => {
              const on = i === ts.phaseIndex, past = i < ts.phaseIndex;
              return (
                <div key={ph} aria-current={on ? 'step' : undefined} style={{ flex: 1, padding: '6px 6px', borderRadius: 'var(--r-md)', textAlign: 'center', background: on ? eHsl('player', 0.16) : past ? eHsl('player', 0.06) : 'var(--bg-muted)', border: `1px solid ${on ? eHsl('player', 0.45) : 'var(--border-light)'}`, color: on ? eHsl('player') : past ? 'var(--text-sec)' : 'var(--text-muted)' }}>
                  <div style={{ ...mono(8, 800), opacity: 0.7 }}>{past ? '✓' : i + 1}</div>
                  <div style={{ ...disp(10.5, 800), lineHeight: 1.1, marginTop: 1 }}>{ph}</div>
                </div>
              );
            })}
          </div>
        </div>
        {/* dice */}
        <div style={{ flex: '0 1 230px', display: 'flex', flexDirection: 'column', gap: 8, padding: 12, borderRadius: 'var(--r-lg)', background: 'var(--bg-card)', border: '1px solid var(--border)' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8 }}>
            <F.DiceDisplay dice={dice} />
            <button type="button" onClick={onRoll} style={{ padding: '8px 13px', borderRadius: 'var(--r-md)', background: eHsl('tool'), color: '#06222b', border: 'none', ...disp(12, 800, '#06222b'), cursor: 'pointer', flexShrink: 0, boxShadow: `0 3px 10px ${eHsl('tool', 0.4)}` }}>🎲 Tira</button>
          </div>
          <F.DiceHistory dice={dice} history={dice.history} />
        </div>
      </div>
    );
  };

  // ─── RobberBanner — alert when a 7 is rolled ──────────────────────────────
  const RobberBanner = ({ mode }) => {
    if (!mode) return null;
    const isDiscard = mode === 'discard';
    return (
      <div role="alert" aria-live="assertive" style={{ display: 'flex', alignItems: 'center', gap: 11, padding: '11px 14px', borderRadius: 'var(--r-lg)', background: eHsl('event', 0.1), border: `1px solid ${eHsl('event', 0.4)}`, boxShadow: `0 3px 14px ${eHsl('event', 0.2)}` }}>
        <span aria-hidden="true" style={{ width: 34, height: 34, borderRadius: '50%', flexShrink: 0, background: eHsl('event', 0.16), display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 18 }}>🦹</span>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ ...disp(13.5, 800, eHsl('event')) }}>È uscito un 7 — il Ladro!</div>
          <div style={{ ...mono(10, 700, 'var(--text-sec)') }}>{isDiscard ? 'Chi ha più di 7 carte ne scarta metà' : 'Sposta il ladro e ruba una carta'}</div>
        </div>
        <div style={{ display: 'flex', gap: 6, flexShrink: 0 }}>
          <span style={{ ...mono(9, 800, isDiscard ? eHsl('event') : 'var(--text-muted)'), padding: '4px 9px', borderRadius: 'var(--r-pill)', background: isDiscard ? eHsl('event', 0.14) : 'var(--bg-muted)', border: `1px solid ${isDiscard ? eHsl('event', 0.4) : 'var(--border-light)'}` }}>1 · scarta</span>
          <span style={{ ...mono(9, 800, !isDiscard ? eHsl('event') : 'var(--text-muted)'), padding: '4px 9px', borderRadius: 'var(--r-pill)', background: !isDiscard ? eHsl('event', 0.14) : 'var(--bg-muted)', border: `1px solid ${!isDiscard ? eHsl('event', 0.4) : 'var(--border-light)'}` }}>2 · sposta</span>
        </div>
      </div>
    );
  };

  // ─── BoardPanel — center column board + dice + log ────────────────────────
  const BoardPanel = ({ ds = C.ds, R: hexR = 44, rolled7, robberMode, embedded }) => {
    const dice = C.DICE;
    const sum = dice.last[0] + dice.last[1];
    const robberHex = C.ROBBER_HEX;
    const rob = C.HEXES.find(h => h.id === robberHex);
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12, minHeight: 0 }}>
        <TurnPhaseBar ds={ds} dice={dice} />
        {robberMode && <RobberBanner mode={robberMode} />}
        <SectionCard title={`Tabellone · ultimo lancio ${sum}`} entity="session" accent icon="🗺️"
          right={<span style={{ ...mono(9, 700, 'var(--text-muted)') }}>ladro su {C.RES[rob.t].terrain}-{rob.n}</span>} pad={10}>
          <F.HexBoard R={hexR} highlightNumber={sum === 7 ? null : sum} robberHex={robberHex} />
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 10, flexWrap: 'wrap', justifyContent: 'center' }}>
            {C.PLAYERS.map(p => (
              <span key={p.id} style={{ display: 'inline-flex', alignItems: 'center', gap: 5, ...mono(9.5, 700, 'var(--text-sec)') }}>
                <F.PlayerDot p={p} size={10} />{p.name}
              </span>
            ))}
            <span style={{ ...mono(9, 700, 'var(--text-muted)'), marginLeft: 4 }}>· i numeri 6 e 8 (rossi) sono i più probabili</span>
          </div>
        </SectionCard>
        <div style={{ display: 'grid', gridTemplateColumns: embedded ? '1fr' : '1.3fr 1fr', gap: 12 }}>
          <ResourceBank />
          <DevDeck />
        </div>
        <S.ActionLogTimeline ds={ds} />
      </div>
    );
  };

  // ─── state wrapper ────────────────────────────────────────────────────────
  const withStates = (state, opts, children) => (
    <R.StateScaffold state={state} sseWhere={opts.sseWhere} empty={opts.empty} error={opts.error}>{children}</R.StateScaffold>
  );
  const SubLabel = ({ children, mt }) => (
    <div style={{ ...mono(9.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', marginTop: mt }}>{children}</div>
  );
  const ResSet = ({ set, sign }) => (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, flexWrap: 'wrap' }}>
      {Object.entries(set).map(([k, n]) => (
        <span key={k} style={{ display: 'inline-flex', alignItems: 'center', gap: 2 }}>
          <F.ResourceToken id={k} size={16} />
          <span style={{ ...mono(11, 800, 'var(--text)') }}>{sign}{n}</span>
        </span>
      ))}
    </span>
  );

  // ─── TradePanel ───────────────────────────────────────────────────────────
  const OfferCard = ({ offer, kind }) => {
    const other = kind === 'in' ? playerOf(offer.from) : null;
    return (
      <div style={{ padding: 11, borderRadius: 'var(--r-md)', background: 'var(--bg-card)', border: `1px solid ${kind === 'in' ? eHsl('chat', 0.3) : eHsl('session', 0.3)}` }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 7, marginBottom: 8 }}>
          {other && <F.PlayerDot p={other} size={11} />}
          <span style={{ ...disp(12.5, 800, 'var(--text)') }}>{kind === 'in' ? `${other.name} propone` : 'La tua proposta'}</span>
          <div style={{ flex: 1 }} />
          <span style={{ ...mono(8, 800, kind === 'in' ? eHsl('chat') : eHsl('session')), textTransform: 'uppercase', letterSpacing: '.05em', padding: '2px 7px', borderRadius: 'var(--r-pill)', background: kind === 'in' ? eHsl('chat', 0.12) : eHsl('session', 0.12) }}>{kind === 'in' ? 'in arrivo' : 'aperta'}</span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '8px 10px', borderRadius: 'var(--r-sm)', background: 'var(--bg-muted)' }}>
          <div><div style={{ ...mono(8, 700, 'var(--text-muted)'), textTransform: 'uppercase', marginBottom: 3 }}>{kind === 'in' ? 'ti danno' : 'tu dai'}</div><ResSet set={kind === 'in' ? offer.give : offer.give} /></div>
          <span style={{ color: 'var(--text-muted)', fontSize: 16 }} aria-hidden="true">⇄</span>
          <div><div style={{ ...mono(8, 700, 'var(--text-muted)'), textTransform: 'uppercase', marginBottom: 3 }}>{kind === 'in' ? 'vogliono' : 'tu vuoi'}</div><ResSet set={kind === 'in' ? offer.want : offer.want} /></div>
        </div>
        {offer.note ? <div style={{ ...mono(9.5, 600, 'var(--text-muted)'), marginTop: 6, fontStyle: 'italic' }}>“{offer.note}”</div> : null}
        {kind === 'in' && (
          <div style={{ display: 'flex', gap: 6, marginTop: 9 }}>
            <button type="button" style={{ flex: 1, padding: '7px', borderRadius: 'var(--r-md)', background: eHsl('toolkit'), color: '#fff', border: 'none', ...disp(11.5, 800, '#fff'), cursor: 'pointer' }}>Accetta</button>
            <button type="button" style={{ flex: 1, padding: '7px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', color: 'var(--text-sec)', border: '1px solid var(--border)', ...disp(11.5, 800), cursor: 'pointer' }}>Rifiuta</button>
            <button type="button" style={{ padding: '7px 11px', borderRadius: 'var(--r-md)', background: 'transparent', color: eHsl('chat'), border: `1px solid ${eHsl('chat', 0.4)}`, ...disp(11.5, 800), cursor: 'pointer' }}>Contro</button>
          </div>
        )}
      </div>
    );
  };
  const BankTradeRow = ({ ratio, lb, e, ports }) => (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 10px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)' }}>
      <span style={{ width: 34, ...disp(14, 800, eHsl(e)), textAlign: 'center', fontVariantNumeric: 'tabular-nums' }}>{ratio}</span>
      <span style={{ flex: 1, ...mono(10, 700, 'var(--text-sec)') }}>{lb}</span>
      {ports && <span style={{ display: 'inline-flex', gap: 3 }}>{ports.map((pt, i) => pt.type === 'generic' ? <span key={i} style={{ ...mono(9, 800, 'var(--text-muted)') }}>3:1</span> : <F.ResourceToken key={i} id={pt.type} size={15} />)}</span>}
    </div>
  );
  const TradePanel = ({ state = 'default', ds = C.ds }) => withStates(state, {
    sseWhere: 'scambi',
    empty: { icon: '🤝', title: 'Nessuna proposta', body: 'Le offerte di scambio compariranno qui durante la fase di commercio.' },
    error: { title: 'Scambi non disponibili', body: 'Impossibile sincronizzare le proposte di scambio.' },
  }, (
    <R.Panel gap={11}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <SubLabel>Offerte in arrivo · {C.TRADE.incoming.length}</SubLabel><div style={{ flex: 1 }} />
        <span style={{ ...mono(9, 800, eHsl('chat')), textTransform: 'uppercase', letterSpacing: '.05em', padding: '2px 8px', borderRadius: 'var(--r-pill)', background: eHsl('chat', 0.12), border: `1px solid ${eHsl('chat', 0.3)}` }}>player-to-player</span>
      </div>
      {C.TRADE.incoming.map(o => <OfferCard key={o.id} offer={o} kind="in" />)}
      <SubLabel mt={4}>La tua proposta</SubLabel>
      <OfferCard offer={C.TRADE.outgoing} kind="out" />
      <SubLabel mt={4}>Commercio con la banca / porti</SubLabel>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        <BankTradeRow ratio="4:1" lb="Banca · qualsiasi risorsa" e="kb" />
        <BankTradeRow ratio="3:1" lb="Porto generico" e="session" ports={C.PLAYER_PORTS[ds.turnState.activePlayerId]?.filter(p => p.type === 'generic')} />
        <BankTradeRow ratio="2:1" lb="Porto specifico (tuoi)" e="toolkit" ports={C.PLAYER_PORTS[ds.turnState.activePlayerId]?.filter(p => p.type !== 'generic')} />
      </div>
    </R.Panel>
  ));

  // ─── DevCardsPanel ────────────────────────────────────────────────────────
  const DevCardsPanel = ({ state = 'default', ds = C.ds }) => {
    const me = ds.players.find(p => p.id === ds.turnState.activePlayerId) || ds.players[0];
    return withStates(state, {
      sseWhere: 'carte sviluppo',
      empty: { icon: '🃏', title: 'Nessuna carta sviluppo', body: 'Compra carte con 1 lana + 1 grano + 1 minerale.' },
      error: { title: 'Carte non disponibili', body: 'Impossibile caricare le carte sviluppo.' },
    }, (
      <R.Panel gap={11}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <SubLabel>La tua mano · {me.name}</SubLabel><div style={{ flex: 1 }} />
          <span style={{ ...mono(9, 800, eHsl('toolkit')), padding: '2px 8px', borderRadius: 'var(--r-pill)', background: eHsl('toolkit', 0.12), border: `1px solid ${eHsl('toolkit', 0.3)}` }}>segreta</span>
        </div>
        <div style={{ display: 'flex', gap: 7 }}>
          <div style={{ flex: 1, padding: '10px 12px', borderRadius: 'var(--r-md)', background: eHsl('toolkit', 0.07), border: `1px solid ${eHsl('toolkit', 0.3)}`, textAlign: 'center' }}>
            <div style={{ ...disp(22, 800, 'var(--text)') }}>{me.dev.held}</div>
            <div style={{ ...mono(8, 700, 'var(--text-muted)'), textTransform: 'uppercase' }}>in mano</div>
          </div>
          <div style={{ flex: 1, padding: '10px 12px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)', textAlign: 'center' }}>
            <div style={{ ...disp(22, 800, eHsl('event')) }}>{me.dev.knightsPlayed}</div>
            <div style={{ ...mono(8, 700, 'var(--text-muted)'), textTransform: 'uppercase' }}>cavalieri giocati</div>
          </div>
          <div style={{ flex: 1, padding: '10px 12px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)', textAlign: 'center' }}>
            <div style={{ ...disp(22, 800, eHsl('toolkit')) }}>{me.dev.vpCards}</div>
            <div style={{ ...mono(8, 700, 'var(--text-muted)'), textTransform: 'uppercase' }}>carte PV</div>
          </div>
        </div>
        <SubLabel mt={4}>Tipi nel mazzo · {C.DEV_DECK.remaining} rimaste</SubLabel>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {C.DEV_TYPES.map(d => (
            <div key={d.id} title={d.desc} style={{ display: 'flex', alignItems: 'center', gap: 9, padding: '8px 10px', borderRadius: 'var(--r-md)', background: 'var(--bg-card)', border: '1px solid var(--border)' }}>
              <span aria-hidden="true" style={{ width: 26, height: 26, borderRadius: 'var(--r-sm)', background: eHsl(d.e, 0.14), border: `1px solid ${eHsl(d.e, 0.3)}`, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 14, flexShrink: 0 }}>{d.icon}</span>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ ...disp(12, 800, 'var(--text)') }}>{d.lb}</div>
                <div style={{ ...mono(9, 600, 'var(--text-muted)'), whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{d.desc}</div>
              </div>
              <span style={{ ...mono(10, 800, 'var(--text-sec)'), flexShrink: 0 }}>×{d.total}</span>
            </div>
          ))}
        </div>
      </R.Panel>
    ));
  };

  // ─── BuildPanel ───────────────────────────────────────────────────────────
  const BuildPanel = ({ state = 'default', ds = C.ds }) => {
    const me = ds.players.find(p => p.id === ds.turnState.activePlayerId) || ds.players[0];
    const T = C.TOTAL_PIECES;
    const canAfford = (cost) => C.RES_ORDER.every(k => (me.hand[k] || 0) >= (cost[k] || 0));
    return withStates(state, {
      sseWhere: 'costruzione',
      empty: { icon: '🧱', title: 'Niente da costruire', body: 'Raccogli risorse per costruire strade, insediamenti e città.' },
      error: { title: 'Costruzione non disponibile', body: 'Impossibile caricare i costi di costruzione.' },
    }, (
      <R.Panel gap={11}>
        <SubLabel>Costi di costruzione</SubLabel>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 7 }}>
          {C.BUILD_COSTS.map(b => {
            const ok = canAfford(b.cost);
            return (
              <div key={b.id} style={{ padding: '9px 11px', borderRadius: 'var(--r-md)', background: ok ? eHsl(b.e, 0.06) : 'var(--bg-card)', border: `1px solid ${ok ? eHsl(b.e, 0.3) : 'var(--border)'}` }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <span aria-hidden="true" style={{ fontSize: 16 }}>{b.icon}</span>
                  <span style={{ ...disp(13, 800, 'var(--text)') }}>{b.lb}</span>
                  {b.vp > 0 && <span style={{ ...mono(8.5, 800, eHsl('toolkit')), padding: '1px 6px', borderRadius: 'var(--r-pill)', background: eHsl('toolkit', 0.12) }}>+{b.vp} PV</span>}
                  <div style={{ flex: 1 }} />
                  <ResSet set={b.cost} />
                  <button type="button" disabled={!ok} style={{ marginLeft: 6, padding: '5px 11px', borderRadius: 'var(--r-md)', background: ok ? eHsl(b.e) : 'var(--bg-muted)', color: ok ? '#fff' : 'var(--text-muted)', border: ok ? 'none' : '1px solid var(--border)', ...disp(11, 800), cursor: ok ? 'pointer' : 'not-allowed', opacity: ok ? 1 : 0.7 }}>{ok ? 'Costruisci' : 'manca'}</button>
                </div>
                <div style={{ ...mono(9, 600, 'var(--text-muted)'), marginTop: 5 }}>{b.note}</div>
              </div>
            );
          })}
        </div>
        <SubLabel mt={4}>Pezzi rimasti · {me.name}</SubLabel>
        <div style={{ display: 'flex', gap: 7 }}>
          <PieceCounter icon="🏠" lb="insediamenti" remaining={T.settlements - me.pieces.settlements} total={T.settlements} />
          <PieceCounter icon="🏛️" lb="città" remaining={T.cities - me.pieces.cities} total={T.cities} />
          <PieceCounter icon="🛤️" lb="strade" remaining={T.roads - me.pieces.roads} total={T.roads} />
        </div>
      </R.Panel>
    ));
  };

  // ─── CatanRightTabs — Scoring · Trade · Dev · Build · Chat ─────────────────
  const CAT_TABS = [
    { id: 'scoring', icon: '🎯', label: 'Punti', entity: 'session', render: (ds, st) => <R.ScoringPanelRenderer data={ds} state={st} /> },
    { id: 'trade',   icon: '🤝', label: 'Scambi', entity: 'chat',    render: (ds, st) => <TradePanel ds={ds} state={st} /> },
    { id: 'dev',     icon: '🃏', label: 'Sviluppo', entity: 'toolkit', render: (ds, st) => <DevCardsPanel ds={ds} state={st} /> },
    { id: 'build',   icon: '🧱', label: 'Costruisci', entity: 'game', render: (ds, st) => <BuildPanel ds={ds} state={st} /> },
    { id: 'chat',    icon: '🤖', label: 'Agente', entity: 'agent',   render: (ds, st) => <div style={{ flex: 1, minHeight: 0, display: 'flex', padding: 10 }}><S.ChatAgentPanel ds={ds} /></div> },
  ];

  const CatanRightTabs = ({ ds = C.ds, initial = 'scoring', initialState = 'default', embedded, width = 372 }) => {
    const [tab, setTab] = useState(initial);
    const [st, setSt] = useState(initialState);
    const active = CAT_TABS.find(t => t.id === tab) || CAT_TABS[0];
    return (
      <aside aria-label="Pannello sessione Catan" style={{ width: embedded ? '100%' : width, minWidth: embedded ? 0 : 320, flexShrink: 0, height: '100%', background: 'var(--bg-card)', borderLeft: embedded ? 'none' : '1px solid var(--border)', display: 'flex', flexDirection: 'column', minHeight: 0 }}>
        <div role="tablist" aria-label="Sezioni" style={{ display: 'flex', borderBottom: '1px solid var(--border-light)', flexShrink: 0 }}>
          {CAT_TABS.map(t => {
            const on = tab === t.id;
            return (
              <button key={t.id} type="button" role="tab" aria-selected={on} aria-label={t.label} onClick={() => setTab(t.id)} style={{ flex: 1, padding: '9px 4px', background: on ? eHsl(t.entity, 0.06) : 'transparent', border: 'none', borderBottom: on ? `2px solid ${eHsl(t.entity)}` : '2px solid transparent', color: on ? eHsl(t.entity) : 'var(--text-sec)', ...disp(11, 800), cursor: 'pointer', display: 'inline-flex', flexDirection: 'column', alignItems: 'center', gap: 3 }}>
                <span aria-hidden="true" style={{ fontSize: 15 }}>{t.icon}</span><span>{t.label}</span>
              </button>
            );
          })}
        </div>
        {tab !== 'chat' && <S.StateSwitch value={st} onChange={setSt} />}
        <div role="tabpanel" style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>{active.render(ds, tab === 'chat' ? 'default' : st)}</div>
      </aside>
    );
  };

  // ─── DesktopBody — LEFT rail · CENTER board · RIGHT tabs ──────────────────
  const DesktopBody = ({ ds = C.ds, initialTab = 'scoring', initialState = 'default' }) => (
    <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
      <div style={{ width: 244, flexShrink: 0, borderRight: '1px solid var(--border)', overflowY: 'auto', padding: 12, background: 'var(--bg)' }}>
        <PlayerRail players={ds.players} />
      </div>
      <main style={{ flex: 1, minWidth: 0, overflowY: 'auto', padding: 14, background: 'var(--bg)' }}>
        <BoardPanel ds={ds} R={44} />
      </main>
      <CatanRightTabs ds={ds} initial={initialTab} initialState={initialState} />
    </div>
  );

  // ─── MOBILE body ──────────────────────────────────────────────────────────
  const MobileSheet = ({ ds, open, tab, st, setSt, onClose }) => {
    const active = CAT_TABS.find(t => t.id === tab) || CAT_TABS[0];
    return (
      <div aria-hidden={!open} style={{ position: 'absolute', inset: 0, zIndex: 50, pointerEvents: open ? 'auto' : 'none' }}>
        <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(15,12,30,.5)', backdropFilter: 'blur(3px)', opacity: open ? 1 : 0, transition: 'opacity var(--dur-md) var(--ease-out)' }} />
        <div role="dialog" aria-modal="true" aria-label={active.label} style={{ position: 'absolute', left: 0, right: 0, bottom: 0, height: '86%', background: 'var(--bg-card)', borderTopLeftRadius: 'var(--r-2xl)', borderTopRightRadius: 'var(--r-2xl)', borderTop: `3px solid ${eHsl(active.entity)}`, boxShadow: 'var(--shadow-drawer)', display: 'flex', flexDirection: 'column', minHeight: 0, transform: open ? 'translateY(0)' : 'translateY(101%)', transition: 'transform var(--dur-lg) var(--ease-spring)' }}>
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', paddingTop: 8, flexShrink: 0 }}>
            <div style={{ width: 38, height: 4, borderRadius: 999, background: 'var(--border-strong)' }} />
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 14px 10px', flexShrink: 0 }}>
            <span aria-hidden="true" style={{ fontSize: 16 }}>{active.icon}</span>
            <span style={{ ...disp(15, 800, eHsl(active.entity)) }}>{active.label}</span>
            <div style={{ flex: 1 }} />
            <button type="button" onClick={onClose} aria-label="Chiudi" style={{ width: 28, height: 28, borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border)', color: 'var(--text-sec)', cursor: 'pointer', fontSize: 13 }}>✕</button>
          </div>
          {tab !== 'chat' && <S.StateSwitch value={st} onChange={setSt} />}
          <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>{active.render(ds, tab === 'chat' ? 'default' : st)}</div>
        </div>
      </div>
    );
  };

  const MobileBody = ({ ds = C.ds, initialTab }) => {
    const [sheet, setSheet] = useState(initialTab || null);
    const [st, setSt] = useState('default');
    const [railOpen, setRailOpen] = useState(false);
    const active = ds.players.find(p => p.id === ds.turnState.activePlayerId) || ds.players[0];
    return (
      <>
        <div style={{ flex: 1, overflowY: 'auto', minHeight: 0, background: 'var(--bg)' }}>
          <div style={{ padding: 12, display: 'flex', flexDirection: 'column', gap: 12 }}>
            <BoardPanel ds={ds} R={32} embedded />
          </div>
        </div>
        {/* collapsible player rail */}
        <div style={{ flexShrink: 0, borderTop: '1px solid var(--border)', background: 'var(--bg-card)' }}>
          <button type="button" onClick={() => setRailOpen(o => !o)} aria-expanded={railOpen} style={{ width: '100%', display: 'flex', alignItems: 'center', gap: 8, padding: '8px 12px', background: 'transparent', border: 'none', cursor: 'pointer' }}>
            <span style={{ ...mono(9.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>Giocatori</span>
            <div style={{ display: 'flex', marginLeft: 2 }}>{ds.players.map((p, i) => <span key={p.id} style={{ marginLeft: i ? -6 : 0 }}><F.PlayerDot p={p} size={16} /></span>)}</div>
            <div style={{ flex: 1 }} />
            <span style={{ ...mono(9, 700, eHsl('session')) }}>turno: {active.name}</span>
            <span aria-hidden="true" style={{ ...mono(11, 800, 'var(--text-muted)'), transform: railOpen ? 'none' : 'rotate(-90deg)', transition: 'transform var(--dur-sm)' }}>▾</span>
          </button>
          {railOpen && <div style={{ padding: '0 12px 12px', display: 'flex', flexDirection: 'column', gap: 10, maxHeight: 320, overflowY: 'auto' }}>{ds.players.map(p => <PlayerStateCard key={p.id} p={p} active={p.current} />)}</div>}
        </div>
        {/* tab strip */}
        <nav className="mai-cb-scroll" aria-label="Sezioni" style={{ display: 'flex', gap: 6, overflowX: 'auto', flexShrink: 0, padding: '8px 10px', background: 'var(--glass-bg)', backdropFilter: 'blur(14px)', borderTop: '1px solid var(--border)' }}>
          {CAT_TABS.map(t => (
            <button key={t.id} type="button" onClick={() => { setSt('default'); setSheet(t.id); }} style={{ flex: '0 0 auto', display: 'inline-flex', alignItems: 'center', gap: 5, padding: '7px 13px', borderRadius: 'var(--r-pill)', background: eHsl(t.entity, 0.1), border: `1px solid ${eHsl(t.entity, 0.28)}`, color: eHsl(t.entity), ...disp(12, 800), cursor: 'pointer', whiteSpace: 'nowrap' }}>
              <span aria-hidden="true">{t.icon}</span>{t.label}
            </button>
          ))}
        </nav>
        <MobileSheet ds={ds} open={!!sheet} tab={sheet} st={st} setSt={setSt} onClose={() => setSheet(null)} />
      </>
    );
  };

  // ─── PhoneShell ───────────────────────────────────────────────────────────
  const PhoneShell = ({ ds = C.ds, label, desc, dark, initialTab }) => (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10 }}>
      <div style={{ ...mono(11, 700, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>{label}{dark && <span style={{ color: eHsl('session'), marginLeft: 6 }}>· dark</span>}</div>
      <div className="phone" data-theme={dark ? 'dark' : undefined}>
        <div className="phone-sbar" style={{ color: 'var(--text)' }}>
          <span style={{ fontFamily: 'var(--f-mono)' }}>19:42</span>
          <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">82%</span></div>
        </div>
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', background: 'var(--bg)', position: 'relative', overflow: 'hidden', minHeight: 0 }}>
          <S.TopBar ds={ds} compact />
          <MobileBody ds={ds} initialTab={initialTab} />
        </div>
      </div>
      {desc && <div style={{ fontSize: 11, color: 'var(--text-muted)', maxWidth: 340, textAlign: 'center', lineHeight: 1.55 }}>{desc}</div>}
    </div>
  );

  window.CatanParts = {
    SectionCard, PieceCounter, PlayerStateCard, PlayerRail, ResourceBank, DevDeck,
    TurnPhaseBar, RobberBanner, BoardPanel, TradePanel, DevCardsPanel, BuildPanel,
    CatanRightTabs, DesktopBody, MobileBody, MobileSheet, PhoneShell, CAT_TABS, STATES,
    SubLabel, ResSet, OfferCard,
  };
})();

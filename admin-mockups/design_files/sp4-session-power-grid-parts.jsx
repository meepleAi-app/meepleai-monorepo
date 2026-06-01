/* MeepleAI SP4 · Power Grid — COMPOSED PARTS  (window.PGParts)
   Pannelli specifici Power Grid montati sopra lo skeleton universale:
     SectionCard · PanelFrame · STATES
     PhaseIndicator      — timeline 5 fasi del round + Step + Round (aria-current)
     TurnOrderStrip      — ordine corrente + indicatore REVERSE (leader ultimo)
     PlayerStateRail     — stato giocatore (Elektro · risorse · centrali · città)
     PowerPlantMarket    — 8 carte (Current + Future) · 2 righe
     AuctionOverlay      — flusso d'asta fase 2 (bidder + highest bid · aria-live)
     ResourceMarket      — 4 colonne con scarsità + "prossimo prezzo"
     NetworkMap          — mappa Germania (nodi+archi · owner coloring · highlight)
     PlantsRail / PlantsTab · MarketTab
     RightColumnTabs     — Scoring · Market · Network · Plants · Chat
     DesktopBody · PhoneShell

   Riusa: S.TopBar · S.ChatAgentPanel · S.ActionLogTimeline · S.StateSwitch
          R.ScoringPanelRenderer · R.StateScaffold · M.StateBlock/Shimmer/SseBanner
   Legge window.PG (dati) · window.PGFlavor (atomi) · window.MAI (eHsl). */

(function () {
  const M = window.MAI;
  const R = window.SkeletonRenderers;
  const S = window.SkeletonParts;
  const PG = window.PG;
  const F = window.PGFlavor;
  const eHsl = M.entityHsl;
  const { useState } = React;
  const { mono, disp } = F;

  const STATES = window.SkeletonData.STATES;

  // ─── SectionCard — contenitore titolato ───────────────────────────────────
  const SectionCard = ({ title, entity = 'session', accent, right, children, pad = 12, flush, collapsed, onHeaderClick }) => {
    const collapsible = !!onHeaderClick;
    const headStyle = { width: '100%', textAlign: 'left', display: 'flex', alignItems: 'center', gap: 8, padding: '8px 12px', background: accent ? eHsl(entity, collapsed ? 0.04 : 0.06) : 'var(--bg-muted)', border: 'none', borderBottom: collapsed ? 'none' : `1px solid ${accent ? eHsl(entity, 0.18) : 'var(--border-light)'}`, cursor: collapsible ? 'pointer' : 'default' };
    const headInner = (
      <React.Fragment>
        <span style={{ ...mono(10, 800, accent ? eHsl(entity) : 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>{title}</span>
        <div style={{ flex: 1 }} />{right}
        {collapsible && <span aria-hidden="true" style={{ ...mono(11, 800, eHsl(entity)), flexShrink: 0, transform: collapsed ? 'rotate(-90deg)' : 'none', transition: 'transform var(--dur-sm) var(--ease-out)' }}>▾</span>}
      </React.Fragment>
    );
    return (
      <section style={{ background: 'var(--bg-card)', border: `1px solid ${accent || collapsible ? eHsl(entity, collapsed ? 0.2 : 0.3) : 'var(--border)'}`, borderRadius: 'var(--r-lg)', overflow: 'hidden', boxShadow: 'var(--shadow-sm)', flexShrink: 0 }}>
        {title && (collapsible
          ? <button type="button" onClick={onHeaderClick} aria-expanded={!collapsed} style={headStyle}>{headInner}</button>
          : <div style={headStyle}>{headInner}</div>)}
        {!collapsed && <div style={{ padding: flush ? 0 : pad }}>{children}</div>}
      </section>
    );
  };

  // ─── PanelFrame — frame rail per la gallery degli stati ───────────────────
  const PanelFrame = ({ label, entity = 'session', dark, children, w = 300, h = 470 }) => (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }} data-theme={dark ? 'dark' : undefined}>
      <div style={{ ...mono(10, 800, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>{label}</div>
      <div style={{ width: w, height: h, borderRadius: 'var(--r-lg)', border: `1px solid ${eHsl(entity, 0.3)}`, background: 'var(--bg-card)', overflow: 'hidden', display: 'flex', flexDirection: 'column', boxShadow: 'var(--shadow-sm)' }}>{children}</div>
    </div>
  );

  // ══════════════════════════════════════════════════════════════════════════
  // PhaseIndicator — timeline delle 5 fasi · Step + Round prominenti
  // ══════════════════════════════════════════════════════════════════════════
  const PhaseIndicator = ({ compact, sticky }) => {
    const { phases, phaseState } = PG;
    return (
      <div role="group" aria-label="Fase del round" style={{
        display: 'flex', alignItems: 'center', gap: compact ? 8 : 14, padding: compact ? '8px 12px' : '10px 16px',
        background: 'var(--glass-bg)', backdropFilter: 'blur(14px)', borderBottom: '1px solid var(--border)',
        ...(sticky ? { position: 'sticky', top: 0, zIndex: 10 } : {}), flexShrink: 0, flexWrap: 'wrap', rowGap: 7,
      }}>
        {/* Round + Step block */}
        <div style={{ display: 'flex', alignItems: 'center', gap: compact ? 7 : 10, flexShrink: 0 }}>
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', lineHeight: 1 }}>
            <span style={{ ...disp(compact ? 17 : 21, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>R{phaseState.round}</span>
            <span style={{ ...mono(7.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>Round</span>
          </div>
          <F.StepIndicator step={phaseState.step} compact={compact} />
        </div>
        <span aria-hidden="true" style={{ width: 1, alignSelf: 'stretch', background: 'var(--border)', flexShrink: 0 }} />
        {/* 5-phase timeline */}
        <ol style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', alignItems: 'center', flexWrap: 'wrap', rowGap: 6, gap: compact ? 5 : 7, flex: 1, minWidth: 0 }}>
          {phases.map((ph, i) => {
            const on = i === phaseState.phaseIndex, past = i < phaseState.phaseIndex;
            const e = on ? 'session' : ph.reverse ? 'event' : 'player';
            return (
              <li key={ph.id} aria-current={on ? 'step' : undefined} style={{ display: 'flex', alignItems: 'center', gap: compact ? 5 : 7, flexShrink: 0 }}>
                <div title={ph.desc} style={{
                  display: 'flex', alignItems: 'center', gap: 6, padding: compact ? '5px 8px' : '6px 11px', borderRadius: 'var(--r-pill)',
                  background: on ? eHsl('session', 0.14) : past ? eHsl('toolkit', 0.07) : 'var(--bg-muted)',
                  border: `1px solid ${on ? eHsl('session', 0.45) : past ? eHsl('toolkit', 0.25) : 'var(--border-light)'}`,
                  boxShadow: on ? `0 3px 10px ${eHsl('session', 0.22)}` : 'none',
                }}>
                  <span aria-hidden="true" style={{
                    width: compact ? 17 : 20, height: compact ? 17 : 20, borderRadius: '50%', flexShrink: 0,
                    background: on ? eHsl('session') : past ? eHsl('toolkit', 0.25) : 'transparent',
                    color: on ? '#fff' : past ? eHsl('toolkit') : 'var(--text-muted)',
                    border: on || past ? 'none' : '1px solid var(--border-strong)',
                    display: 'inline-flex', alignItems: 'center', justifyContent: 'center', ...mono(compact ? 9 : 10, 800),
                  }}>{past ? '✓' : ph.n}</span>
                  {!compact && <span style={{ ...disp(11.5, on ? 800 : 600, on ? eHsl('session') : past ? 'var(--text-sec)' : 'var(--text-muted)'), whiteSpace: 'nowrap' }}>{ph.short}</span>}
                  {ph.reverse && <span title="Ordine ribaltato in questa fase" aria-label="ordine ribaltato" style={{ ...mono(9, 800, on ? eHsl('event') : 'var(--text-muted)') }}>⇄</span>}
                </div>
                {i < phases.length - 1 && <span aria-hidden="true" style={{ ...mono(10, 800, past ? eHsl('toolkit') : 'var(--text-muted)') }}>{compact ? '·' : '→'}</span>}
              </li>
            );
          })}
        </ol>
      </div>
    );
  };

  // ══════════════════════════════════════════════════════════════════════════
  // TurnOrderStrip — ordine corrente + REVERSE (leader gioca ultimo)
  // ══════════════════════════════════════════════════════════════════════════
  const TurnOrderStrip = ({ phaseId, awaitingId, compact }) => {
    const { order, reverse, phase } = PG.turnOrder(phaseId);
    const activeId = awaitingId || PG.auction.awaitingId;
    return (
      <div aria-label={`Ordine di gioco · fase ${phase.lb}`} style={{
        display: 'flex', alignItems: 'center', gap: compact ? 7 : 10, padding: compact ? '7px 12px' : '8px 16px',
        background: reverse ? eHsl('event', 0.05) : 'var(--bg-card)', borderBottom: '1px solid var(--border)',
        flexWrap: 'wrap', rowGap: 6, flexShrink: 0,
      }}>
        <div style={{ display: 'flex', flexDirection: 'column', flexShrink: 0, lineHeight: 1.15 }}>
          <span style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>Ordine · F{phase.n}</span>
          <F.ReverseBadge reverse={reverse} />
        </div>
        <span aria-hidden="true" style={{ width: 1, alignSelf: 'stretch', background: 'var(--border)', flexShrink: 0 }} />
        <div style={{ display: 'flex', alignItems: 'center', gap: 4, flexWrap: 'wrap', rowGap: 5 }}>
          {order.map((p, i) => {
            const on = p.id === activeId;
            const passed = PG.auction.passed.includes(p.id) && phase.id === 'auction';
            return (
              <React.Fragment key={p.id}>
                <div title={`${p.name} · ${p.cities} città`} style={{
                  display: 'flex', alignItems: 'center', gap: 5, padding: '3px 9px 3px 3px', borderRadius: 'var(--r-pill)', flexShrink: 0,
                  background: on ? eHsl('player', 0.14) : 'var(--bg-muted)',
                  border: `1px solid ${on ? eHsl('player', 0.45) : 'var(--border-light)'}`,
                  opacity: passed ? 0.45 : 1,
                }}>
                  <span style={{ ...mono(8.5, 800, on ? eHsl('player') : 'var(--text-muted)'), width: 12, textAlign: 'center' }}>{i + 1}</span>
                  <F.MiniAvatar p={p} size={20} ring={on} dim={passed} />
                  {!compact && <span style={{ ...disp(11, on ? 800 : 600, on ? 'var(--text)' : 'var(--text-sec)') }}>{p.name}</span>}
                  {on && <span className="mai-pulse-dot" aria-label="al turno" style={{ ...mono(8, 800, eHsl('player')) }}>●</span>}
                  {passed && <span style={{ ...mono(7.5, 800, 'var(--text-muted)'), textTransform: 'uppercase' }}>pass</span>}
                </div>
                {i < order.length - 1 && <span aria-hidden="true" style={{ ...mono(10, 700, 'var(--text-muted)'), flexShrink: 0 }}>›</span>}
              </React.Fragment>
            );
          })}
        </div>
        {reverse && <span style={{ ...mono(8.5, 700, eHsl('event')), flexShrink: 0, whiteSpace: 'nowrap', marginLeft: 'auto' }}>👑 {order[order.length - 1].name} (leader) gioca ultimo</span>}
      </div>
    );
  };

  // ══════════════════════════════════════════════════════════════════════════
  // PlayerStateRail — stato del giocatore attivo (rail laterale)
  // ══════════════════════════════════════════════════════════════════════════
  const PlayerStateRail = ({ player, others, onPick }) => (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      <SectionCard title={`Plancia · ${player.name}`} entity="session" accent pad={11}
        right={<span style={{ ...mono(8.5, 800, eHsl('player')), padding: '1px 7px', borderRadius: 'var(--r-pill)', background: eHsl('player', 0.12), border: `1px solid ${eHsl('player', 0.3)}` }}>attivo</span>}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          <F.ElektroCounter value={player.elektro} big />
          <div style={{ display: 'flex', gap: 7 }}>
            <F.SupplyStat kind="city" value={player.cities} sub="città" accent="player" />
            <F.SupplyStat kind="powered" value={player.powered} sub="alimentate" accent="game" />
          </div>
          <F.ResourceRow store={player.store} />
          <div>
            <div style={{ display: 'flex', alignItems: 'baseline', gap: 6, marginBottom: 5 }}>
              <span style={{ ...mono(9, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>Centrali</span>
              <span style={{ ...mono(9, 800, eHsl('session')) }}>{player.plants.length}/3</span>
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
              {player.plants.map((pl, i) => <F.PlantMini key={i} plant={pl} loaded={pl.loaded} />)}
              {Array.from({ length: 3 - player.plants.length }).map((_, i) => (
                <div key={`e${i}`} style={{ height: 44, borderRadius: 'var(--r-md)', border: '1.5px dashed var(--border-strong)', background: 'var(--bg-sunken)', opacity: 0.5, display: 'flex', alignItems: 'center', justifyContent: 'center', ...mono(9, 700, 'var(--text-muted)') }}>slot libero</div>
              ))}
            </div>
          </div>
        </div>
      </SectionCard>
      {others && (
        <SectionCard title="Altri giocatori" entity="player" pad={10}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {others.map(p => (
              <button key={p.id} type="button" onClick={() => onPick && onPick(p.id)} style={{ display: 'flex', alignItems: 'center', gap: 7, padding: '6px 8px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)', cursor: onPick ? 'pointer' : 'default', textAlign: 'left' }}>
                <F.MiniAvatar p={p} size={24} />
                <span style={{ flex: 1, minWidth: 0, ...disp(12, 800, 'var(--text)'), overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{p.name}</span>
                <span title="città · alimentate" style={{ display: 'inline-flex', gap: 5, flexShrink: 0 }}>
                  <span style={{ ...mono(9, 700, 'var(--text-muted)') }}>🏙{p.cities}</span>
                  <span style={{ ...mono(9, 700, eHsl('game')) }}>⚡{p.powered}</span>
                </span>
                <F.ElektroCounter value={p.elektro} compact />
              </button>
            ))}
          </div>
        </SectionCard>
      )}
    </div>
  );

  // ══════════════════════════════════════════════════════════════════════════
  // PowerPlantMarket — 8 carte · Current (comprabili) + Future (preview)
  // ══════════════════════════════════════════════════════════════════════════
  const PowerPlantMarketInner = ({ compact }) => {
    const cur = PG.market.current.map(id => PG.PLANTS[id]);
    const fut = PG.market.future.map(id => PG.PLANTS[id]);
    const minCell = compact ? 92 : 108;
    const grid = { display: 'grid', gridTemplateColumns: `repeat(auto-fill, minmax(${minCell}px, 1fr))`, gap: 8 };
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10, padding: 12 }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 7, marginBottom: 7 }}>
            <span style={{ ...mono(9, 800, eHsl('session')), textTransform: 'uppercase', letterSpacing: '.07em' }}>Mercato attuale</span>
            <span style={{ ...mono(8.5, 700, 'var(--text-muted)') }}>4 centrali · all'asta</span>
          </div>
          <div style={grid}>
            {cur.map((pl, i) => <F.PlantCard key={i} plant={pl} w="100%" buyable compact={compact} onClick={() => {}} />)}
          </div>
        </div>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 7, marginBottom: 7 }}>
            <span style={{ ...mono(9, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>Mercato futuro</span>
            <span style={{ ...mono(8.5, 700, 'var(--text-muted)') }}>4 centrali · anteprima · non comprabili</span>
          </div>
          <div style={grid}>
            {fut.map((pl, i) => <F.PlantCard key={i} plant={pl} w="100%" future compact={compact} />)}
          </div>
        </div>
      </div>
    );
  };
  const PowerPlantMarket = ({ state = 'default', compact, withStep, collapsed, onHeaderClick }) => (
    <SectionCard title="Mercato centrali" entity="game" accent flush collapsed={collapsed} onHeaderClick={onHeaderClick}
      right={withStep ? <F.StepIndicator step={PG.phaseState.step} compact /> : <span style={{ ...mono(8.5, 800, eHsl('game')), padding: '1px 7px', borderRadius: 'var(--r-pill)', background: eHsl('game', 0.12), border: `1px solid ${eHsl('game', 0.3)}` }}>8 carte</span>}>
      <R.StateScaffold state={state} sseWhere="mercato"
        empty={{ icon: '⚡', title: 'Mercato vuoto', body: 'Le centrali compariranno all\u2019avvio della fase asta.' }}
        error={{ title: 'Mercato non disponibile', body: 'Impossibile caricare il mercato delle centrali.' }}
        loading={<div style={{ padding: 14, display: 'flex', gap: 8, flexWrap: 'wrap' }}>{Array.from({ length: 8 }).map((_, i) => <M.Shimmer key={i} h={compact ? 96 : 132} style={{ width: compact ? 104 : 116, borderRadius: 'var(--r-md)' }} />)}</div>}>
        <PowerPlantMarketInner compact={compact} />
      </R.StateScaffold>
    </SectionCard>
  );

  // ─── AuctionOverlay — flusso asta fase 2 (bidder + highest bid) ────────────
  const AuctionOverlay = ({ compact, collapsed, onHeaderClick }) => {
    const a = PG.auction;
    const plant = PG.PLANTS[a.plantId];
    const high = PG.byId(a.highBidderId);
    const nom = PG.byId(a.nominatorId);
    const awaiting = PG.byId(a.awaitingId);
    const collapsible = !!onHeaderClick;
    const head = (
      <React.Fragment>
        <span className="mai-pulse-dot" aria-hidden="true" style={{ color: eHsl('session'), fontSize: 10, flexShrink: 0 }}>●</span>
        <span style={{ ...disp(12.5, 800, eHsl('session')), whiteSpace: 'nowrap' }}>Asta in corso</span>
        {collapsible && <span aria-hidden="true" style={{ ...mono(11, 800, eHsl('session')), flexShrink: 0, marginLeft: 'auto', transform: collapsed ? 'rotate(-90deg)' : 'none', transition: 'transform var(--dur-sm) var(--ease-out)' }}>▾</span>}
        <span style={{ flexBasis: '100%', ...mono(8.5, 700, 'var(--text-muted)') }}>fase 2 · ordine ⇄ ribaltato · nominata da <strong style={{ color: `hsl(${nom.hue},60%,55%)` }}>{nom.name}</strong></span>
      </React.Fragment>
    );
    const headStyle = { width: '100%', textAlign: 'left', display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', rowGap: 4, padding: '7px 12px', background: eHsl('session', 0.1), border: 'none', borderBottom: collapsed ? 'none' : `1px solid ${eHsl('session', 0.25)}`, cursor: collapsible ? 'pointer' : 'default' };
    return (
      <div role="region" aria-label="Asta in corso" aria-live="polite" style={{
        borderRadius: 'var(--r-lg)', overflow: 'hidden', border: `2px solid ${eHsl('session', collapsed ? 0.25 : 0.5)}`,
        background: 'var(--bg-card)', boxShadow: collapsed ? 'var(--shadow-sm)' : `0 8px 28px ${eHsl('session', 0.25)}`, flexShrink: 0,
      }}>
        {collapsible
          ? <button type="button" onClick={onHeaderClick} aria-expanded={!collapsed} style={headStyle}>{head}</button>
          : <div style={headStyle}>{head}</div>}
        {!collapsed && (
        <div style={{ display: 'flex', alignItems: 'center', gap: compact ? 10 : 16, padding: 12, flexWrap: 'wrap' }}>
          <F.PlantCard plant={plant} w={compact ? 104 : 112} compact={compact} />
          <div style={{ flex: 1, minWidth: 160, display: 'flex', flexDirection: 'column', gap: 9 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
              <div>
                <div style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>Puntata minima</div>
                <div style={{ ...disp(18, 800, 'var(--text-sec)'), fontVariantNumeric: 'tabular-nums' }}>€{a.minBid}</div>
              </div>
              <span aria-hidden="true" style={{ ...mono(14, 800, 'var(--text-muted)') }}>→</span>
              <div>
                <div style={{ ...mono(8.5, 800, eHsl('session')), textTransform: 'uppercase', letterSpacing: '.06em' }}>Offerta più alta</div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
                  <span style={{ ...disp(24, 800, eHsl('session')), fontVariantNumeric: 'tabular-nums' }}>€{a.highBid}</span>
                  <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5, padding: '2px 8px 2px 3px', borderRadius: 'var(--r-pill)', background: eHsl('player', 0.12), border: `1px solid ${eHsl('player', 0.3)}` }}>
                    <F.MiniAvatar p={high} size={18} /><span style={{ ...disp(11, 800, 'var(--text)') }}>{high.name}</span>
                  </span>
                </div>
              </div>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '7px 10px', borderRadius: 'var(--r-md)', background: eHsl('player', 0.07), border: `1px solid ${eHsl('player', 0.25)}`, flexWrap: 'wrap', rowGap: 8 }}>
              <F.MiniAvatar p={awaiting} size={22} ring />
              <span style={{ flex: '1 1 120px', minWidth: 0, ...mono(10, 700, 'var(--text-sec)') }}>Tocca a <strong style={{ color: 'var(--text)' }}>{awaiting.name}</strong> · rilancia o passa</span>
              <div style={{ display: 'flex', gap: 6, flexShrink: 0, marginLeft: 'auto' }}>
                <button type="button" style={{ padding: '5px 11px', borderRadius: 'var(--r-md)', background: eHsl('session'), color: '#fff', border: 'none', ...disp(11.5, 800, '#fff'), cursor: 'pointer', whiteSpace: 'nowrap' }}>Rilancia €{a.highBid + 1}</button>
                <button type="button" style={{ padding: '5px 11px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border)', ...disp(11.5, 800, 'var(--text-sec)'), cursor: 'pointer', whiteSpace: 'nowrap' }}>Passa</button>
              </div>
            </div>
          </div>
        </div>
        )}
      </div>
    );
  };

  // ══════════════════════════════════════════════════════════════════════════
  // ResourceMarket — 4 colonne con scarsità + prossimo prezzo
  // ══════════════════════════════════════════════════════════════════════════
  const ResourceColumn = ({ resId }) => {
    const col = PG.resourceMarket[resId];
    const res = PG.RES[resId];
    const next = PG.nextPrice(resId);
    const left = PG.remaining(resId);
    // raggruppa scaffali per prezzo (dal più caro in alto al più economico in basso)
    const byPrice = [];
    [...col.shelves].forEach(s => {
      let row = byPrice.find(r => r.price === s.price);
      if (!row) { row = { price: s.price, slots: [] }; byPrice.push(row); }
      row.slots.push(s);
    });
    byPrice.sort((a, b) => b.price - a.price); // caro in alto
    const scarce = left <= 6;
    return (
      <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', gap: 6 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
          <F.ResourceToken id={resId} size={18} />
          <span style={{ ...disp(11.5, 800, 'var(--text)') }}>{res.lb}</span>
        </div>
        {/* scaffalatura: righe per prezzo */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 2, padding: 6, borderRadius: 'var(--r-md)', background: 'var(--bg-sunken)', border: '1px solid var(--border-light)' }}>
          {byPrice.map(row => {
            const isNext = row.price === next && row.slots.some(s => s.available);
            return (
              <div key={row.price} style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
                <span style={{ ...mono(8.5, 800, isNext ? eHsl('game') : 'var(--text-muted)'), width: 22, textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}>€{row.price}</span>
                <div style={{ flex: 1, display: 'flex', gap: 2, padding: '2px 4px', borderRadius: 'var(--r-sm)', background: isNext ? eHsl('game', 0.1) : 'transparent', border: isNext ? `1px solid ${eHsl('game', 0.3)}` : '1px solid transparent', minHeight: 18, alignItems: 'center' }}>
                  {row.slots.map((s, i) => s.available
                    ? <F.ResourceToken key={i} id={resId} size={13} />
                    : <span key={i} aria-hidden="true" style={{ width: 13, height: 13, borderRadius: resId === 'uranium' ? '50%' : 'var(--r-xs)', border: '1px dashed var(--border-strong)', opacity: 0.4 }} />)}
                </div>
              </div>
            );
          })}
        </div>
        {/* prossimo prezzo */}
        <div style={{ padding: '6px 8px', borderRadius: 'var(--r-md)', background: scarce ? eHsl('event', 0.08) : eHsl('game', 0.08), border: `1px solid ${scarce ? eHsl('event', 0.3) : eHsl('game', 0.3)}` }}>
          <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 4 }}>
            <span style={{ ...mono(7.5, 800, scarce ? eHsl('event') : eHsl('game')), textTransform: 'uppercase', letterSpacing: '.05em' }}>Prossimo</span>
            {scarce && <span style={{ ...mono(7, 800, eHsl('event')), textTransform: 'uppercase', letterSpacing: '.04em' }}>scarso</span>}
          </div>
          <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 4 }}>
            <span style={{ ...disp(15, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>{next != null ? `€${next}` : '—'}</span>
            <span style={{ ...mono(8, 700, scarce ? eHsl('event') : 'var(--text-muted)') }}>{left} rim.</span>
          </div>
        </div>
      </div>
    );
  };
  const ResourceMarket = ({ state = 'default', compact, collapsed, onHeaderClick }) => (
    <SectionCard title="Mercato risorse" entity="kb" accent flush collapsed={collapsed} onHeaderClick={onHeaderClick}
      right={<span style={{ ...mono(8.5, 700, 'var(--text-muted)') }}>più scarso = più caro</span>}>
      <R.StateScaffold state={state} sseWhere="risorse"
        empty={{ icon: '🛢', title: 'Mercato risorse vuoto', body: 'Le risorse compariranno con la fase di rifornimento.' }}
        error={{ title: 'Risorse non disponibili', body: 'Impossibile caricare i prezzi del mercato risorse.' }}
        loading={<div style={{ padding: 14, display: 'flex', gap: 10 }}>{Array.from({ length: 4 }).map((_, i) => <M.Shimmer key={i} h={180} style={{ flex: 1 }} />)}</div>}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))', gap: 10, padding: 12 }}>
          {PG.RES_ORDER.map(id => <ResourceColumn key={id} resId={id} />)}
        </div>
      </R.StateScaffold>
    </SectionCard>
  );

  // ══════════════════════════════════════════════════════════════════════════
  // NetworkMap — mappa Germania (nodi + archi · owner coloring · highlight)
  // ══════════════════════════════════════════════════════════════════════════
  const COST_COLOR = { 5: 'toolkit', 10: 'tool', 15: 'agent', 20: 'event' };
  const NetworkMap = ({ state = 'default', focusPlayerId = 'p-marco', h = 360, showLegend = true }) => {
    const net = PG.network;
    const nodeById = id => net.nodes.find(n => n.id === id);
    const reach = net.reachableBy[focusPlayerId] || [];
    return (
      <SectionCard title="Rete · Germania" entity="session" accent flush
        right={<span style={{ ...mono(8.5, 700, 'var(--text-muted)') }}>{net.region}</span>}>
        <R.StateScaffold state={state} sseWhere="rete"
          empty={{ icon: '🗺', title: 'Rete vuota', body: 'Le città compariranno quando i giocatori costruiscono.' }}
          error={{ title: 'Rete non disponibile', body: 'Impossibile caricare la mappa della rete.' }}
          loading={<div style={{ padding: 14 }}><M.Shimmer h={h - 20} /></div>}>
          <div style={{ padding: 10 }}>
            <div style={{ position: 'relative', width: '100%', height: h, borderRadius: 'var(--r-md)', overflow: 'hidden', background: 'var(--bg-sunken)', border: '1px solid var(--border-light)' }}>
              {/* placeholder texture: schema astratto, non geografia reale */}
              <svg viewBox="-7 -10 114 112" preserveAspectRatio="xMidYMid meet" style={{ position: 'absolute', inset: 0, width: '100%', height: '100%' }} role="img" aria-label="Mappa rete città e connessioni">
                <defs>
                  <pattern id="pg-grid" width="8" height="8" patternUnits="userSpaceOnUse">
                    <path d="M8 0H0V8" fill="none" stroke="var(--border-light)" strokeWidth="0.3" />
                  </pattern>
                </defs>
                <rect x="-7" y="-10" width="114" height="112" fill="url(#pg-grid)" />
                {/* archi + costo */}
                {net.edges.map(([a, b, cost], i) => {
                  const na = nodeById(a), nb = nodeById(b);
                  const mx = (na.x + nb.x) / 2, my = (na.y + nb.y) / 2;
                  const both = na.owner && nb.owner;
                  return (
                    <g key={i}>
                      <line x1={na.x} y1={na.y} x2={nb.x} y2={nb.y} stroke={both ? eHsl('session', 0.45) : 'var(--border-strong)'} strokeWidth={both ? 0.7 : 0.5} strokeDasharray={both ? 'none' : '1.4 1.2'} />
                      <circle cx={mx} cy={my} r="1.8" fill={`hsl(var(--c-${COST_COLOR[cost] || 'tool'}))`} stroke="var(--bg-sunken)" strokeWidth="0.45">
                        <title>{`Costo connessione: €${cost}`}</title>
                      </circle>
                    </g>
                  );
                })}
                {/* nodi città */}
                {net.nodes.map(n => {
                  const owner = n.owner ? PG.byId(n.owner) : null;
                  const reachable = reach.includes(n.id);
                  return (
                    <g key={n.id}>
                      {reachable && <circle cx={n.x} cy={n.y} r="4.4" fill="none" stroke={eHsl('game')} strokeWidth="0.7" strokeDasharray="1.4 1" />}
                      <circle cx={n.x} cy={n.y} r="2.9"
                        fill={owner ? `hsl(${owner.hue},62%,52%)` : 'var(--bg-card)'}
                        stroke={owner ? 'rgba(0,0,0,.3)' : 'var(--border-strong)'} strokeWidth="0.5" />
                      {(() => {
                        const lp = n.lp || 'top';
                        const pos = lp === 'bottom' ? { x: n.x, y: n.y + 5.6, a: 'middle' }
                          : lp === 'left' ? { x: n.x - 4.2, y: n.y + 0.9, a: 'end' }
                          : lp === 'right' ? { x: n.x + 4.2, y: n.y + 0.9, a: 'start' }
                          : { x: n.x, y: n.y - 4.1, a: 'middle' };
                        return <text x={pos.x} y={pos.y} textAnchor={pos.a} style={{ fontFamily: 'var(--f-display)', fontSize: 2.4, fontWeight: 700, fill: 'var(--text-sec)', stroke: 'var(--bg-sunken)', strokeWidth: 1, paintOrder: 'stroke', strokeLinejoin: 'round' }}>{n.name}</text>;
                      })()}
                    </g>
                  );
                })}
              </svg>
              {/* badge placeholder */}
              <span style={{ position: 'absolute', bottom: 6, left: 8, ...mono(8, 700, 'var(--text-muted)'), background: 'var(--glass-bg)', padding: '2px 7px', borderRadius: 'var(--r-pill)', border: '1px solid var(--border-light)' }}>layout schematico · mappa reale al rilascio</span>
            </div>
            {showLegend && (
              <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginTop: 9, alignItems: 'center' }}>
                <span style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>Costo connessione</span>
                {PG.network.connectCosts.map(c => (
                  <span key={c} style={{ display: 'inline-flex', alignItems: 'center', gap: 4, ...mono(9, 700, 'var(--text-sec)') }}>
                    <span style={{ width: 12, height: 12, borderRadius: '50%', background: eHsl(COST_COLOR[c] || 'tool'), color: '#fff', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontSize: 7, fontWeight: 800 }}>{c}</span>€{c}
                  </span>
                ))}
                <span aria-hidden="true" style={{ width: 1, height: 14, background: 'var(--border)' }} />
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, ...mono(9, 700, eHsl('game')) }}><span style={{ width: 11, height: 11, borderRadius: '50%', border: `1.5px dashed ${eHsl('game')}` }} />raggiungibili da {PG.byId(focusPlayerId).name}</span>
              </div>
            )}
          </div>
        </R.StateScaffold>
      </SectionCard>
    );
  };

  // ─── PlantsRail / PlantsTab — dettaglio 3 centrali del giocatore ───────────
  const PlantsTab = ({ state = 'default', player }) => {
    const p = player || PG.roster[0];
    return (
      <R.StateScaffold state={state} sseWhere="centrali"
        empty={{ icon: '⚡', title: 'Nessuna centrale', body: 'Acquista centrali all\u2019asta per alimentare le città.' }}
        error={{ title: 'Centrali non disponibili', body: 'Impossibile caricare le centrali del giocatore.' }}>
        <R.Panel gap={11}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <F.MiniAvatar p={p} size={26} />
            <span style={{ ...disp(13.5, 800, 'var(--text)') }}>Centrali di {p.name}</span>
            <div style={{ flex: 1 }} />
            <span style={{ ...mono(9, 800, eHsl('session')), padding: '1px 7px', borderRadius: 'var(--r-pill)', background: eHsl('session', 0.12), border: `1px solid ${eHsl('session', 0.3)}` }}>{p.plants.length}/3</span>
          </div>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            {p.plants.map((pl, i) => <F.PlantCard key={i} plant={pl} w={120} loaded={pl.loaded} />)}
            {Array.from({ length: 3 - p.plants.length }).map((_, i) => <F.PlantCard key={`g${i}`} ghost w={120} />)}
          </div>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '8px 11px', borderRadius: 'var(--r-md)', background: eHsl('game', 0.08), border: `1px solid ${eHsl('game', 0.28)}` }}>
            <span style={{ ...mono(10, 700, 'var(--text-sec)') }}>Capacità totale · città alimentabili</span>
            <span style={{ ...disp(18, 800, eHsl('game')), fontVariantNumeric: 'tabular-nums' }}>⚡ {PG.plantsCapOf(p)}</span>
          </div>
          <div style={{ ...mono(9.5, 700, 'var(--text-muted)'), lineHeight: 1.5 }}>Alimenti il minimo fra capacità totale e città possedute. Risorse caricate consumate in Burocrazia per incassare il reddito.</div>
        </R.Panel>
      </R.StateScaffold>
    );
  };

  // ─── MarketTab — plant market + resource market + step (tab dedicata) ──────
  const MarketTab = ({ state = 'default' }) => (
    <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: 12, display: 'flex', flexDirection: 'column', gap: 12 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', rowGap: 6 }}>
        <span style={{ ...disp(13, 800, 'var(--text)'), whiteSpace: 'nowrap' }}>Mercato & Step</span>
        <div style={{ flex: 1 }} />
        <span style={{ flexShrink: 0 }}><F.StepIndicator step={PG.phaseState.step} /></span>
      </div>
      <PowerPlantMarket state={state} compact />
      <ResourceMarket state={state} compact />
    </div>
  );

  // ══════════════════════════════════════════════════════════════════════════
  // RightColumnTabs — Scoring · Market · Network · Plants · Chat
  // ══════════════════════════════════════════════════════════════════════════
  const RC_TABS = [
    { id: 'scoring', icon: '🎯', label: 'Scoring', entity: 'session', render: (st) => <R.ScoringPanelRenderer data={PG.ds} state={st} /> },
    { id: 'market',  icon: '⚡', label: 'Market',  entity: 'game',    render: (st) => <MarketTab state={st} /> },
    { id: 'network', icon: '🗺', label: 'Network', entity: 'session', render: (st) => <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: 12 }}><NetworkMap state={st} h={300} /></div> },
    { id: 'plants',  icon: '🏭', label: 'Plants',  entity: 'kb',      render: (st) => <PlantsTab state={st} /> },
    { id: 'chat',    icon: '💬', label: 'Chat',    entity: 'agent',   render: () => <div style={{ flex: 1, minHeight: 0, padding: 12, display: 'flex' }}><S.ChatAgentPanel ds={PG.ds} /></div> },
  ];
  const RightColumnTabs = ({ initial = 'scoring', initialState = 'default', embedded }) => {
    const [tab, setTab] = useState(initial);
    const [st, setSt] = useState(initialState);
    const active = RC_TABS.find(t => t.id === tab) || RC_TABS[0];
    return (
      <aside aria-label="Pannello sessione" style={{ width: embedded ? '100%' : 348, minWidth: embedded ? 0 : 300, flexShrink: 0, height: '100%', background: 'var(--bg-card)', borderLeft: embedded ? 'none' : '1px solid var(--border)', display: 'flex', flexDirection: 'column', minHeight: 0 }}>
        <div role="tablist" aria-label="Sezioni" style={{ display: 'flex', borderBottom: '1px solid var(--border-light)', flexShrink: 0, overflow: 'hidden' }}>
          {RC_TABS.map(t => {
            const on = tab === t.id;
            return (
              <button key={t.id} type="button" role="tab" aria-selected={on} aria-label={t.label} onClick={() => setTab(t.id)} style={{ flex: '1 1 0', minWidth: 0, padding: '10px 3px', background: on ? eHsl(t.entity, 0.06) : 'transparent', border: 'none', borderBottom: on ? `2px solid ${eHsl(t.entity)}` : '2px solid transparent', color: on ? eHsl(t.entity) : 'var(--text-sec)', ...disp(10.5, 800), cursor: 'pointer', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 4, whiteSpace: 'nowrap' }}>
                <span aria-hidden="true" style={{ fontSize: 12, flexShrink: 0 }}>{t.icon}</span><span style={{ minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis' }}>{t.label}</span>
              </button>
            );
          })}
        </div>
        {active.id !== 'chat' && <S.StateSwitch value={st} onChange={setSt} />}
        <div role="tabpanel" style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>{active.render(st)}</div>
      </aside>
    );
  };

  // ══════════════════════════════════════════════════════════════════════════
  // DESKTOP body — top band (phase + turn order) · 3 colonne
  // ══════════════════════════════════════════════════════════════════════════
  const DesktopBody = ({ initialTab, initialState }) => {
    const [focusId, setFocusId] = useState('p-marco');
    const [expanded, setExpanded] = useState('auction');
    const sec = (id) => ({ collapsed: expanded !== id, onHeaderClick: () => setExpanded(id) });
    const player = PG.byId(focusId);
    const others = PG.baseOrder.filter(p => p.id !== focusId);
    return (
      <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0 }}>
        <PhaseIndicator />
        <TurnOrderStrip />
        <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
          {/* LEFT — player state rail */}
          <div className="mai-cb-scroll" style={{ width: 252, flexShrink: 0, borderRight: '1px solid var(--border)', overflowY: 'auto', padding: 12, background: 'var(--bg)' }}>
            <PlayerStateRail player={player} others={others} onPick={setFocusId} />
          </div>
          {/* CENTER — accordion: asta · mercato · risorse · registro */}
          <main className="mai-cb-scroll" style={{ flex: 1, minWidth: 0, overflowY: 'auto', padding: 12, display: 'flex', flexDirection: 'column', gap: 10, background: 'var(--bg)' }}>
            <AuctionOverlay {...sec('auction')} />
            <PowerPlantMarket withStep {...sec('plants')} />
            <ResourceMarket {...sec('resources')} />
            <S.ActionLogTimeline ds={PG.ds} {...sec('log')} />
          </main>
          {/* RIGHT — tabs */}
          <RightColumnTabs initial={initialTab} initialState={initialState} />
        </div>
      </div>
    );
  };

  // ══════════════════════════════════════════════════════════════════════════
  // MOBILE — phase sticky · board scroll · tab strip → bottom sheet
  // ══════════════════════════════════════════════════════════════════════════
  const MobileSheet = ({ open, tab, st, setSt, onClose }) => {
    const active = RC_TABS.find(t => t.id === tab) || RC_TABS[0];
    return (
      <div aria-hidden={!open} style={{ position: 'absolute', inset: 0, zIndex: 50, pointerEvents: open ? 'auto' : 'none' }}>
        <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(15,12,8,.5)', backdropFilter: 'blur(3px)', opacity: open ? 1 : 0, transition: 'opacity var(--dur-md) var(--ease-out)' }} />
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
          {active.id !== 'chat' && <S.StateSwitch value={st} onChange={setSt} />}
          <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>{active.render(st)}</div>
        </div>
      </div>
    );
  };

  const MobileBody = ({ initialTab }) => {
    const [sheet, setSheet] = useState(initialTab || null);
    const [st, setSt] = useState('default');
    const [expanded, setExpanded] = useState('auction');
    const sec = (id) => ({ collapsed: expanded !== id, onHeaderClick: () => setExpanded(id) });
    const player = PG.roster[0];
    return (
      <>
        <PhaseIndicator compact sticky />
        <div className="mai-cb-scroll" style={{ flex: 1, overflowY: 'auto', minHeight: 0, background: 'var(--bg)' }}>
          <TurnOrderStrip compact />
          <div style={{ padding: 12, display: 'flex', flexDirection: 'column', gap: 10 }}>
            <AuctionOverlay compact {...sec('auction')} />
            <PlayerStateRail player={player} />
            <PowerPlantMarket compact withStep {...sec('plants')} />
            <ResourceMarket compact {...sec('resources')} />
            <S.ActionLogTimeline ds={PG.ds} compact />
          </div>
        </div>
        <nav aria-label="Sezioni sessione" style={{ display: 'flex', gap: 5, flexShrink: 0, padding: '8px 8px', background: 'var(--glass-bg)', backdropFilter: 'blur(14px)', borderTop: '1px solid var(--border)' }}>
          {RC_TABS.map(t => (
            <button key={t.id} type="button" onClick={() => { setSt('default'); setSheet(t.id); }} style={{ flex: '1 1 0', minWidth: 0, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 4, padding: '7px 4px', borderRadius: 'var(--r-pill)', background: eHsl(t.entity, 0.1), border: `1px solid ${eHsl(t.entity, 0.28)}`, color: eHsl(t.entity), ...disp(10.5, 800), cursor: 'pointer', whiteSpace: 'nowrap' }}>
              <span aria-hidden="true" style={{ flexShrink: 0 }}>{t.icon}</span><span style={{ minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis' }}>{t.label}</span>
            </button>
          ))}
        </nav>
        <MobileSheet open={!!sheet} tab={sheet} st={st} setSt={setSt} onClose={() => setSheet(null)} />
      </>
    );
  };

  // ─── PhoneShell ───────────────────────────────────────────────────────────
  const PhoneSbar = () => (
    <div className="phone-sbar" style={{ color: 'var(--text)' }}>
      <span style={{ fontFamily: 'var(--f-mono)' }}>15:09</span>
      <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
    </div>
  );
  const PhoneShell = ({ label, desc, dark, initialTab }) => (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10 }}>
      <div style={{ ...mono(11, 700, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>{label}{dark && <span style={{ color: eHsl('session'), marginLeft: 6 }}>· dark</span>}</div>
      <div className="phone" data-theme={dark ? 'dark' : undefined}>
        <PhoneSbar />
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', background: 'var(--bg)', position: 'relative', overflow: 'hidden', minHeight: 0 }}>
          <S.TopBar ds={PG.ds} compact />
          <MobileBody initialTab={initialTab} />
        </div>
      </div>
      {desc && <div style={{ fontSize: 11, color: 'var(--text-muted)', maxWidth: 340, textAlign: 'center', lineHeight: 1.55 }}>{desc}</div>}
    </div>
  );

  window.PGParts = {
    STATES, SectionCard, PanelFrame,
    PhaseIndicator, TurnOrderStrip, PlayerStateRail,
    PowerPlantMarket, AuctionOverlay, ResourceMarket, NetworkMap,
    PlantsTab, MarketTab, RightColumnTabs, DesktopBody, MobileBody, PhoneShell,
  };
})();

/* MeepleAI SP4 · Puerto Rico — PARTS  (window.PRParts)
   Game-specific composed components layered ON the universal skeleton.

   Reuses directly from the skeleton:
     window.MAI               · primitives (StateBlock, Shimmer, eHsl…)
     window.SkeletonRenderers · ScoringPanelRenderer · TurnIndicatorRenderer ·
                                ToolkitRenderer · StateScaffold · Panel · Label
     window.SkeletonParts     · TopBar · ChatAgentPanel · ActionLogTimeline
     window.PRFlavor          · goods/tiles/role-card atoms
     window.PR                · dataset

   Adds (Puerto-Rico specific):
     PlayerMat · OtherPlayersBar · PlayerMatDrawer · RoleSelectionBoard ·
     GalleonsShipping · TradingHouseSlots · ColonistShip · AvailablePlantations ·
     BuildingSupplyBoard · SharedStateBoard · RightColumnTabs (Scoring/Roles/
     Trade/Ship/Chat) · DesktopBody · MobileBody · frames.

   Right column = the brief's 5 tabs. Captain phase ⇒ Ship tab is contextual. */

(function () {
  const M = window.MAI;
  const R = window.SkeletonRenderers;
  const S = window.SkeletonParts;
  const F = window.PRFlavor;
  const PR = window.PR;
  const eHsl = M.entityHsl;
  const { useState } = React;
  const mono = F.mono, disp = F.disp;
  const playerById = (id) => PR.roster.find(p => p.id === id);

  const STATES = [
    { id: 'default', lb: 'Default' }, { id: 'empty', lb: 'Empty' }, { id: 'loading', lb: 'Loading' },
    { id: 'error', lb: 'Error' }, { id: 'sse', lb: 'SSE-off' },
  ];

  // ─── SectionCard — labelled block used across the board ────────────────────
  const SectionCard = ({ title, entity = 'session', badge, right, children, pad = 12, accent }) => (
    <section style={{
      background: 'var(--bg-card)', border: `1px solid ${accent ? eHsl(entity, 0.35) : 'var(--border)'}`,
      borderRadius: 'var(--r-lg)', overflow: 'hidden', boxShadow: 'var(--shadow-sm)', flexShrink: 0,
    }}>
      {title && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 12px', background: accent ? eHsl(entity, 0.06) : 'var(--bg-muted)', borderBottom: '1px solid var(--border-light)' }}>
          <span style={{ ...mono(10, 800, accent ? eHsl(entity) : 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>{title}</span>
          {badge != null && <span style={{ ...mono(9, 800, eHsl(entity)), padding: '1px 7px', borderRadius: 'var(--r-pill)', background: eHsl(entity, 0.12), border: `1px solid ${eHsl(entity, 0.28)}` }}>{badge}</span>}
          <div style={{ flex: 1 }} />
          {right}
        </div>
      )}
      <div style={{ padding: pad }}>{children}</div>
    </section>
  );

  // ═══ PLAYER MAT (per-player state) ════════════════════════════════════════
  const RoleChip = ({ roleId }) => {
    const role = PR.ROLES.find(r => r.id === roleId);
    if (!role) return null;
    return (
      <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, padding: '2px 8px', borderRadius: 'var(--r-pill)', background: eHsl(role.e, 0.12), color: eHsl(role.e), border: `1px solid ${eHsl(role.e, 0.3)}`, ...mono(9, 800), textTransform: 'uppercase', letterSpacing: '.04em' }}>
        <span aria-hidden="true">{role.icon}</span>{role.lb}
      </span>
    );
  };

  const PlayerMat = ({ player, active, compact, tileSize = 30, bw = 78 }) => {
    const m = player.mat;
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 11 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
          <F.MiniAvatar p={player} size={34} ring={active} />
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <span style={{ ...disp(15, 800, 'var(--text)') }}>{player.name}</span>
              {player.governor && <F.GovernorToken size="sm" />}
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 3 }}>
              <RoleChip roleId={player.role} />
              {active && <span style={{ ...mono(8.5, 800, eHsl('session')), textTransform: 'uppercase', letterSpacing: '.05em' }}>● sta giocando</span>}
            </div>
          </div>
          <div style={{ textAlign: 'right' }}>
            <div style={{ ...disp(22, 800, eHsl('toolkit')), fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{m.vp}</div>
            <div style={{ ...mono(8, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>PV chip</div>
          </div>
        </div>

        <div style={{ display: 'flex', gap: 6 }}>
          <F.SupplyCounter kind="doubloon" value={m.doubloons} sub="Dobloni" />
          <F.SupplyCounter kind="colonist" value={m.colonistsAvail} sub="Coloni liberi" />
        </div>

        <F.StorehouseRow store={m.storehouse} />
        <F.PlantationGrid tiles={m.plantations} size={tileSize} cols={compact ? 6 : 4} />
        <F.BuildingGrid buildings={m.buildings} w={bw} cols={compact ? 4 : 3} />
      </div>
    );
  };

  // collapsed strip of the OTHER players → open drawer
  const OtherPlayersBar = ({ players, activeId, onOpen }) => (
    <div>
      <div style={{ ...mono(9, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 6 }}>Altri giocatori · plance</div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        {players.filter(p => p.id !== activeId).map(p => (
          <button key={p.id} type="button" onClick={() => onOpen(p.id)} aria-label={`Apri plancia di ${p.name}`} style={{
            display: 'flex', alignItems: 'center', gap: 9, padding: '8px 10px', borderRadius: 'var(--r-md)',
            background: 'var(--bg-card)', border: '1px solid var(--border)', cursor: 'pointer', textAlign: 'left', width: '100%',
          }}>
            <F.MiniAvatar p={p} size={26} />
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
                <span style={{ ...disp(12.5, 800, 'var(--text)') }}>{p.name}</span>
                {p.governor && <span aria-hidden="true" style={{ width: 11, height: 11, borderRadius: '50%', background: 'radial-gradient(circle at 35% 30%, #ffe9a8, #d4a017)', border: '1px solid #b8860b' }} title="Governatore" />}
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 4, marginTop: 3 }}>
                {PR.GOODS.map(g => p.mat.storehouse[g.id] ? <F.GoodToken key={g.id} id={g.id} size={13} /> : null)}
                <span style={{ ...mono(9, 700, 'var(--text-muted)'), marginLeft: 2 }}>· {p.mat.doubloons}🪙</span>
              </div>
            </div>
            <div style={{ textAlign: 'right' }}>
              <div style={{ ...disp(15, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>{p.mat.vp}</div>
              <span style={{ ...mono(8.5, 800, eHsl('session')) }}>apri ›</span>
            </div>
          </button>
        ))}
      </div>
    </div>
  );

  const PlayerMatDrawer = ({ player, open, onClose, side }) => (
    <div aria-hidden={!open} style={{ position: 'absolute', inset: 0, zIndex: 50, pointerEvents: open ? 'auto' : 'none' }}>
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(15,12,30,.5)', backdropFilter: 'blur(3px)', opacity: open ? 1 : 0, transition: 'opacity var(--dur-md) var(--ease-out)' }} />
      <div role="dialog" aria-modal="true" aria-label={player ? `Plancia di ${player.name}` : 'Plancia'} style={{
        position: 'absolute', background: 'var(--bg-card)', boxShadow: 'var(--shadow-drawer)', display: 'flex', flexDirection: 'column', minHeight: 0,
        ...(side === 'right'
          ? { top: 0, bottom: 0, right: 0, width: 'min(380px, 86%)', borderLeft: `3px solid ${eHsl('player')}`, transform: open ? 'translateX(0)' : 'translateX(101%)', transition: 'transform var(--dur-md) var(--ease-spring)' }
          : { left: 0, right: 0, bottom: 0, height: '86%', borderTopLeftRadius: 'var(--r-2xl)', borderTopRightRadius: 'var(--r-2xl)', borderTop: `3px solid ${eHsl('player')}`, transform: open ? 'translateY(0)' : 'translateY(101%)', transition: 'transform var(--dur-lg) var(--ease-spring)' }),
      }}>
        {side !== 'right' && <div style={{ display: 'flex', justifyContent: 'center', paddingTop: 8 }}><div style={{ width: 38, height: 4, borderRadius: 999, background: 'var(--border-strong)' }} /></div>}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '10px 14px', borderBottom: '1px solid var(--border-light)', flexShrink: 0 }}>
          <span style={{ ...mono(10, 800, eHsl('player')), textTransform: 'uppercase', letterSpacing: '.06em' }}>Plancia giocatore</span>
          <div style={{ flex: 1 }} />
          <button type="button" onClick={onClose} aria-label="Chiudi" style={{ width: 28, height: 28, borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border)', color: 'var(--text-sec)', cursor: 'pointer', fontSize: 13 }}>✕</button>
        </div>
        <div style={{ flex: 1, overflowY: 'auto', padding: 14, minHeight: 0 }}>
          {player && <PlayerMat player={player} active={player.current} tileSize={28} bw={74} />}
        </div>
      </div>
    </div>
  );

  // ═══ ROLE SELECTION BOARD ═════════════════════════════════════════════════
  const RoleSelectionBoard = ({ compact, rows }) => {
    const rs = PR.roleState;
    const fivePlayer = PR.roster.length >= 5;
    const roles = PR.ROLES.filter(r => !r.fivePlayerOnly || fivePlayer);
    const showProsp2 = PR.ROLES.find(r => r.id === 'prospector2');
    const list = fivePlayer ? roles : [...roles, showProsp2];
    return (
      <div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 9, flexWrap: 'wrap' }}>
          <span style={{ ...disp(14, 800, 'var(--text)') }}>Selezione ruoli</span>
          <span style={{ ...mono(9.5, 800, eHsl('session')), padding: '2px 8px', borderRadius: 'var(--r-pill)', background: eHsl('session', 0.12), border: `1px solid ${eHsl('session', 0.3)}` }}>Round {rs.round}</span>
          <div style={{ flex: 1 }} />
          <span style={{ ...mono(9, 700, 'var(--text-muted)') }}>governatore</span>
          <F.MiniAvatar p={playerById(rs.chosenBy.captain) ? PR.roster.find(p => p.governor) : PR.roster[0]} size={20} />
        </div>
        <div style={{
          display: compact ? 'flex' : 'grid', gap: 8,
          ...(compact
            ? { overflowX: 'auto', paddingBottom: 6 }
            : { gridTemplateColumns: `repeat(${rows || 4}, minmax(0, 1fr))` }),
        }} className={compact ? 'mai-cb-scroll' : undefined}>
          {list.map(role => {
            const disabled = role.fivePlayerOnly && !fivePlayer;
            return (
              <F.RoleCard
                key={role.id}
                role={role}
                active={rs.activeRole === role.id}
                chosenBy={rs.chosenBy[role.id] ? playerById(rs.chosenBy[role.id]) : null}
                doubloons={rs.doubloonsOn[role.id] || 0}
                disabled={disabled}
                compact={compact}
              />
            );
          })}
        </div>
      </div>
    );
  };

  // ═══ SHARED STATE PIECES ══════════════════════════════════════════════════
  // — Galleons (Captain phase) — stateful —
  const ShipRow = ({ ship }) => {
    const g = ship.good;
    const full = ship.loaded >= ship.cap;
    return (
      <div style={{ display: 'flex', alignItems: 'center', gap: 9, padding: '8px 10px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: `1px solid ${full ? eHsl('toolkit', 0.4) : 'var(--border-light)'}` }}>
        <span aria-hidden="true" style={{ fontSize: 18, flexShrink: 0 }}>⛵</span>
        <div style={{ display: 'flex', gap: 3, flexWrap: 'wrap', flex: 1 }} aria-label={g ? `${ship.loaded} ${F.goodOf(g).lb} su ${ship.cap}` : `galeone vuoto, capacità ${ship.cap}`}>
          {Array.from({ length: ship.cap }).map((_, i) => i < ship.loaded
            ? <F.GoodToken key={i} id={g} size={17} />
            : <span key={i} aria-hidden="true" style={{ width: 17, height: 17, borderRadius: '50%', border: '1.5px dashed var(--border-strong)', background: 'var(--bg-sunken)' }} />)}
        </div>
        <div style={{ textAlign: 'right', flexShrink: 0 }}>
          <div style={{ ...mono(11, 800, full ? eHsl('toolkit') : 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>{ship.loaded}/{ship.cap}</div>
          <div style={{ ...mono(8, 700, 'var(--text-muted)') }}>{full ? 'pieno' : g ? F.goodOf(g).lb : 'libero'}</div>
        </div>
      </div>
    );
  };
  const GalleonsShipping = ({ state = 'default', compact }) => (
    <R.StateScaffold state={state} sseWhere="spedizioni"
      empty={{ icon: '⛵', title: 'Nessun galeone in uso', body: 'I galeoni compaiono all’avvio della fase Capitano.' }}
      error={{ title: 'Galeoni non disponibili', body: 'Impossibile leggere lo stato di spedizione.' }}>
      <R.Panel gap={9}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <R.Label>Galeoni · fase Capitano</R.Label>
          <div style={{ flex: 1 }} />
          <span style={{ ...mono(9, 800, eHsl('event')), padding: '2px 8px', borderRadius: 'var(--r-pill)', background: eHsl('event', 0.1), border: `1px solid ${eHsl('event', 0.28)}` }}>obbligo</span>
        </div>
        <div style={{ ...mono(9.5, 700, 'var(--text-muted)'), lineHeight: 1.4 }}>Ogni nave porta <strong>un solo tipo</strong> di merce. Devi spedire se puoi.</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 7 }}>
          {PR.shared.galleons.map(s => <ShipRow key={s.id} ship={s} />)}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 10px', borderRadius: 'var(--r-md)', background: eHsl('session', 0.07), border: `1px solid ${eHsl('session', 0.28)}` }}>
          <F.MiniAvatar p={playerById('p-marco')} size={22} />
          <span style={{ ...mono(10, 700, 'var(--text-sec)'), flex: 1 }}>Tocca a <strong style={{ color: 'var(--text)' }}>Marco</strong> — spedisce <strong>mais</strong> · +1 PV privilegio</span>
        </div>
      </R.Panel>
    </R.StateScaffold>
  );

  // — Trading house (Trader phase) — stateful —
  const TradingHouseSlots = ({ state = 'default' }) => {
    const th = PR.shared.tradingHouse;
    return (
      <R.StateScaffold state={state} sseWhere="commercio"
        empty={{ icon: '⚖️', title: 'Casa commerciale vuota', body: 'Gli slot si riempiono quando un mercante vende una merce.' }}
        error={{ title: 'Commercio non disponibile', body: 'Impossibile leggere la casa commerciale.' }}>
        <R.Panel gap={10}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <R.Label>Casa commerciale · 4 slot</R.Label>
            <div style={{ flex: 1 }} />
            <span style={{ ...mono(9, 800, eHsl('chat')), padding: '2px 8px', borderRadius: 'var(--r-pill)', background: eHsl('chat', 0.1), border: `1px solid ${eHsl('chat', 0.28)}` }}>{th.slots.filter(Boolean).length}/4</span>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 7 }}>
            {th.slots.map((s, i) => (
              <div key={i} style={{ aspectRatio: '1', borderRadius: 'var(--r-md)', border: s ? `1px solid ${eHsl('chat', 0.35)}` : '1.5px dashed var(--border-strong)', background: s ? eHsl('chat', 0.06) : 'var(--bg-sunken)', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 4 }}>
                {s ? (<>
                  <F.GoodToken id={s.good} size={24} />
                  <span style={{ ...mono(9, 800, eHsl('chat')) }}>+{s.price}🪙</span>
                </>) : <span style={{ ...mono(9, 700, 'var(--text-muted)') }}>vuoto</span>}
              </div>
            ))}
          </div>
          <R.Label>Prezzo base per merce</R.Label>
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
            {PR.GOODS.map(g => (
              <span key={g.id} style={{ display: 'inline-flex', alignItems: 'center', gap: 5, padding: '3px 8px 3px 4px', borderRadius: 'var(--r-pill)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)' }}>
                <F.GoodToken id={g.id} size={15} />
                <span style={{ ...mono(10, 800, 'var(--text)') }}>{th.basePrice[g.id]}</span>
                <span style={{ ...mono(8.5, 700, 'var(--text-muted)') }}>🪙</span>
              </span>
            ))}
          </div>
          <div style={{ ...mono(9, 700, 'var(--text-muted)'), lineHeight: 1.4 }}>Max 1 merce per tipo (salvo Ufficio). Si svuota quando i 4 slot sono pieni.</div>
        </R.Panel>
      </R.StateScaffold>
    );
  };

  // — Colonist ship —
  const ColonistShip = ({ compact }) => {
    const c = PR.shared.colonistShip;
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span aria-hidden="true" style={{ fontSize: 18 }}>🚢</span>
          <span style={{ ...disp(12.5, 800, 'var(--text)') }}>Nave coloni</span>
          <div style={{ flex: 1 }} />
          <span style={{ ...mono(9, 800, eHsl('game')), padding: '1px 7px', borderRadius: 'var(--r-pill)', background: eHsl('game', 0.12), border: `1px solid ${eHsl('game', 0.28)}` }}>+{c.incoming} in arrivo</span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 5, padding: '8px 10px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)', flexWrap: 'wrap' }} aria-label={`${c.onShip} coloni sulla nave`}>
          {Array.from({ length: c.onShip }).map((_, i) => <F.ColonistPip key={i} size={14} />)}
          <span style={{ ...mono(9.5, 700, 'var(--text-muted)'), marginLeft: 4 }}>sulla nave</span>
        </div>
        <div style={{ ...mono(9, 700, 'var(--text-muted)') }}>Riserva coloni: <strong style={{ color: 'var(--text)' }}>{c.supply}</strong> · fine partita se si esaurisce</div>
      </div>
    );
  };

  // — Available plantations + quarry stack —
  const AvailablePlantations = () => {
    const p = PR.shared.plantations;
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 6 }}>
          <span style={{ ...disp(12.5, 800, 'var(--text)') }}>Piantagioni disponibili</span>
          <span style={{ ...mono(9, 700, 'var(--text-muted)') }}>scoperte {p.faceUp.length} · +{p.faceDown} coperta</span>
        </div>
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', alignItems: 'center' }}>
          {p.faceUp.map((t, i) => <F.PlantationTile key={i} tile={{ t, c: false }} size={32} />)}
          <div aria-label="tessera coperta" style={{ width: 32, height: 32, borderRadius: 'var(--r-sm)', background: 'repeating-linear-gradient(45deg, var(--bg-sunken), var(--bg-sunken) 4px, var(--bg-muted) 4px, var(--bg-muted) 8px)', border: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'center', ...mono(13, 800, 'var(--text-muted)') }}>?</div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 5, marginLeft: 4, paddingLeft: 8, borderLeft: '1px solid var(--border)' }}>
            <F.GoodToken id="quarry" size={28} />
            <div>
              <div style={{ ...disp(14, 800, 'var(--text)'), lineHeight: 1 }}>{p.quarryStack}</div>
              <div style={{ ...mono(8, 700, 'var(--text-muted)'), textTransform: 'uppercase' }}>cave</div>
            </div>
          </div>
        </div>
      </div>
    );
  };

  // — Building supply offer —
  const BuildingSupplyBoard = () => (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      <span style={{ ...disp(12.5, 800, 'var(--text)') }}>Offerta edifici</span>
      <div style={{ display: 'flex', gap: 7, flexWrap: 'wrap' }}>
        {PR.shared.buildingSupply.map((b, i) => {
          const ks = F.KIND_STYLE[b.kind];
          return (
            <div key={i} style={{ width: 96, padding: '7px 8px', borderRadius: 'var(--r-sm)', background: b.kind === 'large' ? eHsl('player', 0.1) : 'var(--bg-card)', border: `1px solid ${b.kind === 'large' ? eHsl('player', 0.4) : eHsl(ks.e, 0.3)}`, position: 'relative' }}>
              <span style={{ position: 'absolute', top: 5, right: 5, width: 14, height: 14, borderRadius: 'var(--r-xs)', background: PR.SUPPLY.vp.color, color: '#fff', ...mono(9, 800), display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}>{b.vp}</span>
              <div style={{ ...disp(11, 800, 'var(--text)'), paddingRight: 16, lineHeight: 1.15 }}>{b.n}</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 4, marginTop: 5 }}>
                <span style={{ ...mono(10, 800, eHsl('game')) }}>{b.cost}🪙</span>
                <div style={{ flex: 1 }} />
                <span style={{ ...mono(8.5, 700, b.left ? 'var(--text-muted)' : eHsl('event')) }}>×{b.left}</span>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );

  // — Composed shared board for the center column —
  const SharedStateBoard = ({ compact }) => (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <SectionCard title="Stato condiviso · spedizione" entity="session" accent badge="Capitano">
        <GalleonsShipping />
      </SectionCard>
      <div style={{ display: 'grid', gridTemplateColumns: compact ? '1fr' : '1fr 1fr', gap: 12 }}>
        <SectionCard title="Rifornimenti"><AvailablePlantations /></SectionCard>
        <SectionCard title="Coloni"><ColonistShip /></SectionCard>
      </div>
      <SectionCard title="Edifici acquistabili"><BuildingSupplyBoard /></SectionCard>
    </div>
  );

  // ═══ RIGHT COLUMN TABS (Scoring · Roles · Trade · Ship · Chat) ════════════
  const RolesTab = ({ state = 'default' }) => (
    <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0, overflowY: 'auto' }}>
      <div style={{ padding: 12, borderBottom: '1px solid var(--border-light)' }}>
        <RoleSelectionBoard compact />
      </div>
      <div style={{ flex: 1, minHeight: 0 }}>
        <R.TurnIndicatorRenderer data={PR.ds} state={state} />
      </div>
    </div>
  );

  const StateSwitch = ({ value, onChange }) => (
    <div className="mai-cb-scroll" role="group" aria-label="Stato del componente" style={{ display: 'flex', gap: 4, overflowX: 'auto', padding: '7px 10px', borderBottom: '1px solid var(--border-light)', background: 'var(--bg)', flexShrink: 0 }}>
      <span style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.08em', alignSelf: 'center', flexShrink: 0, marginRight: 2 }}>Stato</span>
      {STATES.map(s => {
        const active = value === s.id;
        return (
          <button key={s.id} type="button" onClick={() => onChange(s.id)} aria-pressed={active} style={{ padding: '3px 9px', borderRadius: 'var(--r-pill)', flexShrink: 0, background: active ? eHsl('session', 0.14) : 'var(--bg-card)', border: active ? `1px solid ${eHsl('session', 0.4)}` : '1px solid var(--border)', color: active ? eHsl('session') : 'var(--text-sec)', ...mono(9.5, 800), cursor: 'pointer' }}>{s.lb}</button>
        );
      })}
    </div>
  );

  const TABS = [
    { id: 'scoring', icon: '🎯', label: 'Scoring', entity: 'session', stateful: true,  render: (st) => <R.ScoringPanelRenderer data={PR.ds} state={st} /> },
    { id: 'roles',   icon: '🗳️', label: 'Roles',   entity: 'player',  stateful: true,  render: (st) => <RolesTab state={st} /> },
    { id: 'trade',   icon: '⚖️', label: 'Trade',   entity: 'chat',    stateful: true,  render: (st) => <TradingHouseSlots state={st} /> },
    { id: 'ship',    icon: '⛵', label: 'Ship',    entity: 'toolkit', stateful: true,  render: (st) => <GalleonsShipping state={st} /> },
    { id: 'chat',    icon: '💬', label: 'Chat',    entity: 'agent',   stateful: false, render: () => <div style={{ flex: 1, minHeight: 0, display: 'flex', padding: 10 }}><S.ChatAgentPanel ds={PR.ds} /></div> },
  ];

  const RightColumnTabs = ({ initial = 'ship', initialState = 'default', width = 360, embedded }) => {
    const [tab, setTab] = useState(initial);
    const [st, setSt] = useState(initialState);
    const active = TABS.find(t => t.id === tab) || TABS[0];
    return (
      <aside aria-label="Pannelli sessione Puerto Rico" style={{ width: embedded ? '100%' : width, flexShrink: 0, height: '100%', background: 'var(--bg-card)', borderLeft: embedded ? 'none' : '1px solid var(--border)', display: 'flex', flexDirection: 'column', minHeight: 0 }}>
        <div role="tablist" aria-label="Sezioni" style={{ display: 'flex', borderBottom: '1px solid var(--border-light)', flexShrink: 0 }}>
          {TABS.map(t => {
            const on = tab === t.id;
            return (
              <button key={t.id} type="button" role="tab" aria-selected={on} aria-label={t.label} title={t.label} onClick={() => setTab(t.id)} style={{ flex: 1, minWidth: 0, padding: '10px 4px', background: on ? eHsl(t.entity, 0.06) : 'transparent', border: 'none', borderBottom: on ? `2px solid ${eHsl(t.entity)}` : '2px solid transparent', color: on ? eHsl(t.entity) : 'var(--text-sec)', ...disp(11.5, 800), cursor: 'pointer', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 4, whiteSpace: 'nowrap' }}>
                <span aria-hidden="true" style={{ fontSize: 14 }}>{t.icon}</span><span>{t.label}</span>
              </button>
            );
          })}
        </div>
        {active.stateful && <StateSwitch value={st} onChange={setSt} />}
        <div role="tabpanel" style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>{active.render(st)}</div>
      </aside>
    );
  };

  // ═══ DESKTOP BODY (3 regions + right tabs) ════════════════════════════════
  const DesktopBody = ({ initialTab = 'ship', initialState = 'default' }) => {
    const active = PR.roster.find(p => p.current) || PR.roster[0];
    const [drawer, setDrawer] = useState(null);
    return (
      <div style={{ display: 'flex', flex: 1, minHeight: 0, position: 'relative' }}>
        {/* LEFT — active mat + other players */}
        <div style={{ width: 312, flexShrink: 0, borderRight: '1px solid var(--border)', overflowY: 'auto', padding: 14, display: 'flex', flexDirection: 'column', gap: 14, background: 'var(--bg-card)' }}>
          <SectionCard title="Plancia · giocatore attivo" entity="session" accent pad={12}>
            <PlayerMat player={active} active tileSize={30} bw={84} />
          </SectionCard>
          <OtherPlayersBar players={PR.roster} activeId={active.id} onOpen={setDrawer} />
        </div>
        {/* CENTER — role board + shared state + log */}
        <main style={{ flex: 1, minWidth: 0, overflowY: 'auto', padding: 16, display: 'flex', flexDirection: 'column', gap: 14 }}>
          <SectionCard title="Tavolo · selezione ruoli" entity="session" accent pad={14}>
            <RoleSelectionBoard />
          </SectionCard>
          <SharedStateBoard />
          <S.ActionLogTimeline ds={PR.ds} />
        </main>
        {/* RIGHT — tabs */}
        <RightColumnTabs initial={initialTab} initialState={initialState} />
        <PlayerMatDrawer player={drawer ? playerById(drawer) : null} open={!!drawer} side="right" onClose={() => setDrawer(null)} />
      </div>
    );
  };

  // ═══ MOBILE BODY ══════════════════════════════════════════════════════════
  const MobileTabSheet = ({ open, tab, st, setSt, onClose }) => {
    const active = TABS.find(t => t.id === tab) || TABS[0];
    return (
      <div aria-hidden={!open} style={{ position: 'absolute', inset: 0, zIndex: 50, pointerEvents: open ? 'auto' : 'none' }}>
        <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(15,12,30,.5)', backdropFilter: 'blur(3px)', opacity: open ? 1 : 0, transition: 'opacity var(--dur-md) var(--ease-out)' }} />
        <div role="dialog" aria-modal="true" aria-label={active.label} style={{ position: 'absolute', left: 0, right: 0, bottom: 0, height: '84%', background: 'var(--bg-card)', borderTopLeftRadius: 'var(--r-2xl)', borderTopRightRadius: 'var(--r-2xl)', borderTop: `3px solid ${eHsl(active.entity)}`, boxShadow: 'var(--shadow-drawer)', display: 'flex', flexDirection: 'column', minHeight: 0, transform: open ? 'translateY(0)' : 'translateY(101%)', transition: 'transform var(--dur-lg) var(--ease-spring)' }}>
          <div style={{ display: 'flex', justifyContent: 'center', paddingTop: 8 }}><div style={{ width: 38, height: 4, borderRadius: 999, background: 'var(--border-strong)' }} /></div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 14px 10px', flexShrink: 0 }}>
            <span aria-hidden="true" style={{ fontSize: 16 }}>{active.icon}</span>
            <span style={{ ...disp(15, 800, eHsl(active.entity)) }}>{active.label}</span>
            <div style={{ flex: 1 }} />
            <button type="button" onClick={onClose} aria-label="Chiudi" style={{ width: 28, height: 28, borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border)', color: 'var(--text-sec)', cursor: 'pointer', fontSize: 13 }}>✕</button>
          </div>
          {active.stateful && <StateSwitch value={st} onChange={setSt} />}
          <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>{active.render(st)}</div>
        </div>
      </div>
    );
  };

  const MobileBody = ({ initialTab }) => {
    const active = PR.roster.find(p => p.current) || PR.roster[0];
    const [sheet, setSheet] = useState(initialTab || null);
    const [st, setSt] = useState('default');
    const [matDrawer, setMatDrawer] = useState(null);
    return (
      <>
        <div style={{ flex: 1, overflowY: 'auto', minHeight: 0, background: 'var(--bg)' }}>
          <div style={{ padding: 12, display: 'flex', flexDirection: 'column', gap: 12 }}>
            <SectionCard title="Selezione ruoli" entity="session" accent pad={12}>
              <RoleSelectionBoard compact />
            </SectionCard>
            <SectionCard title="Spedizione · Capitano" entity="session" accent badge="obbligo" pad={0}>
              <GalleonsShipping />
            </SectionCard>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: 12 }}>
              <SectionCard title="Rifornimenti"><AvailablePlantations /></SectionCard>
            </div>
            <S.ActionLogTimeline ds={PR.ds} compact />
          </div>
        </div>
        {/* mat access bar */}
        <div className="mai-cb-scroll" style={{ display: 'flex', gap: 6, overflowX: 'auto', flexShrink: 0, padding: '7px 10px', background: 'var(--bg-muted)', borderTop: '1px solid var(--border-light)' }}>
          <span style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', alignSelf: 'center', flexShrink: 0 }}>Plance</span>
          {PR.roster.map(p => (
            <button key={p.id} type="button" onClick={() => setMatDrawer(p.id)} style={{ flex: '0 0 auto', display: 'inline-flex', alignItems: 'center', gap: 5, padding: '4px 10px 4px 4px', borderRadius: 'var(--r-pill)', background: p.current ? eHsl('session', 0.12) : 'var(--bg-card)', border: `1px solid ${p.current ? eHsl('session', 0.35) : 'var(--border)'}`, cursor: 'pointer' }}>
              <F.MiniAvatar p={p} size={18} ring={p.current} />
              <span style={{ ...disp(11, 800, 'var(--text)') }}>{p.name}</span>
              <span style={{ ...mono(10, 800, eHsl('toolkit')) }}>{p.mat.vp}</span>
            </button>
          ))}
        </div>
        {/* tab strip */}
        <nav className="mai-cb-scroll" aria-label="Sezioni sessione" style={{ display: 'flex', gap: 6, overflowX: 'auto', flexShrink: 0, padding: '8px 10px', background: 'var(--glass-bg)', backdropFilter: 'blur(14px)', borderTop: '1px solid var(--border)' }}>
          {TABS.map(t => (
            <button key={t.id} type="button" onClick={() => { setSt('default'); setSheet(t.id); }} style={{ flex: '0 0 auto', display: 'inline-flex', alignItems: 'center', gap: 5, padding: '7px 13px', borderRadius: 'var(--r-pill)', background: eHsl(t.entity, 0.1), border: `1px solid ${eHsl(t.entity, 0.28)}`, color: eHsl(t.entity), ...disp(12, 800), cursor: 'pointer', whiteSpace: 'nowrap' }}>
              <span aria-hidden="true">{t.icon}</span>{t.label}
            </button>
          ))}
        </nav>
        <MobileTabSheet open={!!sheet} tab={sheet} st={st} setSt={setSt} onClose={() => setSheet(null)} />
        <PlayerMatDrawer player={matDrawer ? playerById(matDrawer) : null} open={!!matDrawer} onClose={() => setMatDrawer(null)} />
      </>
    );
  };

  // ═══ FRAMES ═══════════════════════════════════════════════════════════════
  const PhoneSbar = () => (
    <div className="phone-sbar" style={{ color: 'var(--text)' }}>
      <span style={{ fontFamily: 'var(--f-mono)' }}>15:18</span>
      <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
    </div>
  );
  const PhoneShell = ({ label, desc, dark, initialTab }) => (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10 }}>
      <div style={{ ...mono(11, 700, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>{label}{dark && <span style={{ color: eHsl('session'), marginLeft: 6 }}>· dark</span>}</div>
      <div className="phone" data-theme={dark ? 'dark' : undefined}>
        <PhoneSbar />
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', background: 'var(--bg)', position: 'relative', overflow: 'hidden', minHeight: 0 }}>
          <S.TopBar ds={PR.ds} compact />
          <MobileBody initialTab={initialTab} />
        </div>
      </div>
      {desc && <div style={{ fontSize: 11, color: 'var(--text-muted)', maxWidth: 340, textAlign: 'center', lineHeight: 1.55 }}>{desc}</div>}
    </div>
  );

  // states-gallery rail frame
  const PanelFrame = ({ label, entity, dark, children, w = 320, h = 480 }) => (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }} data-theme={dark ? 'dark' : undefined}>
      <div style={{ ...mono(10, 800, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>{label}</div>
      <div style={{ width: w, height: h, borderRadius: 'var(--r-lg)', border: `1px solid ${eHsl(entity, 0.3)}`, background: 'var(--bg-card)', overflow: 'hidden', display: 'flex', flexDirection: 'column', boxShadow: 'var(--shadow-sm)' }}>{children}</div>
    </div>
  );

  window.PRParts = {
    SectionCard, RoleChip, PlayerMat, OtherPlayersBar, PlayerMatDrawer,
    RoleSelectionBoard, GalleonsShipping, TradingHouseSlots, ColonistShip,
    AvailablePlantations, BuildingSupplyBoard, SharedStateBoard,
    RolesTab, RightColumnTabs, StateSwitch, TABS, STATES,
    DesktopBody, MobileBody, PhoneShell, PanelFrame,
  };
})();

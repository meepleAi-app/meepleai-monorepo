/* MeepleAI SP4 · Puerto Rico — FLAVOR ATOMS  (window.PRFlavor)
   Small game-specific visual primitives shared by live + summary:
     GoodToken · GoodCount · StorehouseRow · SupplyCounter ·
     PlantationTile · PlantationGrid · BuildingTile · BuildingGrid ·
     ColonistPip · RoleCard · GovernorToken · MiniAvatar

   Goods colors come from window.PR (verbatim from puerto-rico.json). All other
   color/space/radius values are tokens.css vars. Reads window.MAI for eHsl. */

(function () {
  const M = window.MAI;
  const PR = window.PR;
  const eHsl = M.entityHsl;
  const { GOOD, SUPPLY } = PR;
  const mono = (s, w, c) => ({ fontFamily: 'var(--f-mono)', fontSize: s, fontWeight: w, color: c });
  const disp = (s, w, c) => ({ fontFamily: 'var(--f-display)', fontSize: s, fontWeight: w, color: c });

  const goodOf = (id) => GOOD[id] || SUPPLY[id] || { lb: id, color: '#888', dark: '#fff' };

  // ─── MiniAvatar ───────────────────────────────────────────────────────────
  const MiniAvatar = ({ p, size = 22, ring }) => (
    <div aria-hidden="true" style={{
      width: size, height: size, borderRadius: '50%', flexShrink: 0,
      background: `linear-gradient(135deg, hsl(${p.hue},70%,64%), hsl(${p.hue},58%,44%))`,
      color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
      ...disp(size * 0.46, 800, '#fff'),
      border: ring ? `2px solid ${eHsl('toolkit')}` : '2px solid var(--bg-card)',
    }}>{p.name[0]}</div>
  );

  // ─── GoodToken — colored barrel/disc with 1-letter label ──────────────────
  const GoodToken = ({ id, size = 18, title }) => {
    const g = goodOf(id);
    const quarry = id === 'quarry';
    return (
      <span title={title || g.lb} aria-label={g.lb} style={{
        width: size, height: size, borderRadius: quarry ? 'var(--r-xs)' : '50%', flexShrink: 0,
        background: quarry ? 'repeating-linear-gradient(135deg, #9a9d9f, #9a9d9f 3px, #7f8284 3px, #7f8284 6px)' : g.color,
        color: g.dark, display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        ...mono(size * 0.5, 800), border: '1px solid rgba(0,0,0,.22)',
        boxShadow: 'inset 0 1px 1px rgba(255,255,255,.35), inset 0 -1px 2px rgba(0,0,0,.18)',
      }}>{quarry ? '◇' : g.lb[0]}</span>
    );
  };

  // ─── GoodCount — token + count (storehouse / shipped) ─────────────────────
  const GoodCount = ({ id, n, dim }) => (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 4, padding: '2px 7px 2px 3px',
      borderRadius: 'var(--r-pill)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)',
      opacity: dim && !n ? 0.45 : 1,
    }}>
      <GoodToken id={id} size={16} />
      <span style={{ ...mono(11, 800, n ? 'var(--text)' : 'var(--text-muted)'), fontVariantNumeric: 'tabular-nums', minWidth: 8, textAlign: 'center' }}>{n}</span>
    </span>
  );

  // storehouse = the 5 goods counters, aria-live for updates
  const StorehouseRow = ({ store, label = 'Magazzino' }) => (
    <div>
      {label && <div style={{ ...mono(9, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 5 }}>{label}</div>}
      <div aria-live="polite" style={{ display: 'flex', gap: 5, flexWrap: 'wrap' }}>
        {PR.GOODS.map(g => <GoodCount key={g.id} id={g.id} n={store[g.id] || 0} dim />)}
      </div>
    </div>
  );

  // ─── SupplyCounter — doubloons / colonists / VP big display ───────────────
  const SupplyCounter = ({ kind, value, sub, big }) => {
    const s = SUPPLY[kind];
    return (
      <div style={{
        display: 'flex', alignItems: 'center', gap: 8, padding: big ? '8px 12px' : '6px 9px',
        borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)', flex: 1, minWidth: 0,
      }}>
        <span aria-hidden="true" style={{
          width: big ? 26 : 22, height: big ? 26 : 22, borderRadius: '50%', flexShrink: 0,
          background: s.color, color: s.dark, display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          ...mono(big ? 12 : 10, 800), border: '1px solid rgba(0,0,0,.25)',
          boxShadow: 'inset 0 1px 1px rgba(255,255,255,.4), inset 0 -1px 2px rgba(0,0,0,.2)',
        }}>{s.lb[0]}</span>
        <div style={{ minWidth: 0 }}>
          <div aria-live="polite" style={{ ...disp(big ? 20 : 16, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{value}</div>
          <div style={{ ...mono(8.5, 700, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.05em', whiteSpace: 'nowrap' }}>{sub || s.lb}</div>
        </div>
      </div>
    );
  };

  // ─── ColonistPip — the brown worker token ─────────────────────────────────
  const ColonistPip = ({ size = 10, empty }) => (
    <span aria-hidden="true" style={{
      width: size, height: size, borderRadius: '50%', flexShrink: 0,
      background: empty ? 'transparent' : SUPPLY.colonist.color,
      border: empty ? `1.5px dashed ${eHsl('game', 0.5)}` : '1px solid rgba(0,0,0,.3)',
      boxShadow: empty ? 'none' : 'inset 0 1px 1px rgba(255,255,255,.3)',
    }} />
  );

  // ─── PlantationTile — colored field with colonist slot ────────────────────
  const PlantationTile = ({ tile, size = 30 }) => {
    const g = goodOf(tile.t);
    const quarry = tile.t === 'quarry';
    return (
      <div title={`${g.lb}${tile.c ? ' · colono' : ' · vuoto'}`} style={{
        width: size, height: size, borderRadius: 'var(--r-sm)', position: 'relative', flexShrink: 0,
        background: quarry ? 'repeating-linear-gradient(135deg, #9a9d9f, #9a9d9f 4px, #7f8284 4px, #7f8284 8px)' : g.color,
        border: '1px solid rgba(0,0,0,.25)',
        boxShadow: 'inset 0 1px 2px rgba(255,255,255,.3), inset 0 -2px 3px rgba(0,0,0,.18)',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
      }}>
        <span style={{ ...mono(size * 0.34, 800, g.dark), opacity: 0.85 }} aria-hidden="true">{quarry ? '◇' : g.lb[0]}</span>
        <span style={{ position: 'absolute', bottom: 2, right: 2 }}><ColonistPip size={Math.round(size * 0.3)} empty={!tile.c} /></span>
      </div>
    );
  };

  // PlantationGrid — up to 12 tiles, fills empty plots with dashed slots
  const PlantationGrid = ({ tiles, max = 12, cols = 4, size = 30, label = 'Piantagioni · cave' }) => {
    const slots = [...tiles];
    while (slots.length < max) slots.push(null);
    return (
      <div>
        {label && (
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 6, marginBottom: 5 }}>
            <span style={{ ...mono(9, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>{label}</span>
            <span style={{ ...mono(9, 800, eHsl('session')) }}>{tiles.length}/{max}</span>
          </div>
        )}
        <div role="group" aria-label={label} style={{ display: 'grid', gridTemplateColumns: `repeat(${cols}, ${size}px)`, gap: 5 }}>
          {slots.map((t, i) => t
            ? <PlantationTile key={i} tile={t} size={size} />
            : <div key={i} aria-hidden="true" style={{ width: size, height: size, borderRadius: 'var(--r-sm)', border: '1.5px dashed var(--border-strong)', background: 'var(--bg-sunken)', opacity: 0.5 }} />)}
        </div>
      </div>
    );
  };

  // ─── BuildingTile — plot with printed VP + colonist slots ─────────────────
  const KIND_STYLE = {
    prod:  { e: 'kb',      lb: 'Produzione' },
    small: { e: 'game',    lb: 'Cittadino' },
    large: { e: 'player',  lb: 'Grande (viola)' }, // purple → player entity
  };
  const BuildingTile = ({ b, w = 78 }) => {
    const ks = KIND_STYLE[b.kind] || KIND_STYLE.small;
    const filled = b.filled || 0;
    return (
      <div title={`${b.n} · ${ks.lb} · ${b.vp} PV · coloni ${filled}/${b.slots}`} style={{
        width: w, minHeight: 56, borderRadius: 'var(--r-sm)', position: 'relative', flexShrink: 0,
        background: b.kind === 'large' ? eHsl('player', 0.14) : 'var(--bg-card)',
        border: `1px solid ${b.kind === 'large' ? eHsl('player', 0.45) : eHsl(ks.e, 0.3)}`,
        padding: '6px 6px 5px', display: 'flex', flexDirection: 'column', gap: 4, justifyContent: 'space-between',
        boxShadow: 'var(--shadow-xs)',
      }}>
        <div style={{ ...disp(10.5, 800, 'var(--text)'), lineHeight: 1.12, paddingRight: 14 }}>{b.n}</div>
        <span style={{ position: 'absolute', top: 4, right: 4, width: 14, height: 14, borderRadius: 'var(--r-xs)', background: SUPPLY.vp.color, color: '#fff', ...mono(9, 800), display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }} aria-hidden="true">{b.vp}</span>
        <div style={{ display: 'flex', gap: 3, alignItems: 'center' }} aria-label={`coloni ${filled} su ${b.slots}`}>
          {Array.from({ length: b.slots }).map((_, i) => <ColonistPip key={i} size={9} empty={i >= filled} />)}
        </div>
      </div>
    );
  };

  const BuildingGrid = ({ buildings, max = 12, cols = 4, w = 78, label = 'Edifici' }) => {
    const filled = buildings.length;
    return (
      <div>
        {label && (
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 6, marginBottom: 5 }}>
            <span style={{ ...mono(9, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>{label}</span>
            <span style={{ ...mono(9, 800, eHsl('session')) }}>{filled}/{max}</span>
          </div>
        )}
        <div role="group" aria-label={label} style={{ display: 'grid', gridTemplateColumns: `repeat(${cols}, ${w}px)`, gap: 5 }}>
          {buildings.map((b, i) => <BuildingTile key={i} b={b} w={w} />)}
          {Array.from({ length: Math.max(0, max - filled) }).map((_, i) => (
            <div key={`e${i}`} aria-hidden="true" style={{ width: w, minHeight: 56, borderRadius: 'var(--r-sm)', border: '1.5px dashed var(--border-strong)', background: 'var(--bg-sunken)', opacity: 0.5, display: 'flex', alignItems: 'center', justifyContent: 'center', ...mono(9, 700, 'var(--text-muted)') }}>libero</div>
          ))}
        </div>
      </div>
    );
  };

  // ─── GovernorToken ────────────────────────────────────────────────────────
  const GovernorToken = ({ size = 'md' }) => {
    const sm = size === 'sm';
    return (
      <span style={{
        display: 'inline-flex', alignItems: 'center', gap: 5, padding: sm ? '2px 8px' : '3px 10px',
        borderRadius: 'var(--r-pill)', background: eHsl('game', 0.14), color: eHsl('game'),
        border: `1px solid ${eHsl('game', 0.4)}`, ...mono(sm ? 9 : 10, 800), textTransform: 'uppercase', letterSpacing: '.05em',
      }}>
        <span aria-hidden="true" style={{ width: sm ? 12 : 14, height: sm ? 12 : 14, borderRadius: '50%', background: 'radial-gradient(circle at 35% 30%, #ffe9a8, #d4a017)', border: '1px solid #b8860b', boxShadow: 'inset 0 -1px 2px rgba(0,0,0,.3)' }} />
        Governatore
      </span>
    );
  };

  // ─── RoleCard — accessible role-selection button ──────────────────────────
  // shows: role identity · effect/privilege · chooser avatar (or doubloons
  // accrued if unchosen) · active highlight (session entity color).
  const RoleCard = ({ role, chosenBy, doubloons = 0, active, disabled, compact, onClick }) => {
    const e = active ? 'session' : role.e;
    return (
      <button
        type="button"
        onClick={onClick}
        disabled={disabled}
        aria-current={active ? 'true' : undefined}
        aria-label={`Ruolo ${role.lb}${active ? ', fase attuale' : ''}${chosenBy ? ', scelto' : doubloons ? `, ${doubloons} dobloni` : ''}`}
        style={{
          position: 'relative', textAlign: 'left', cursor: disabled ? 'not-allowed' : 'pointer',
          width: compact ? 132 : '100%', minHeight: compact ? 96 : 78,
          padding: compact ? '10px 10px 9px' : '10px 12px',
          borderRadius: 'var(--r-md)', flexShrink: 0,
          background: active ? eHsl('session', 0.1) : disabled ? 'var(--bg-sunken)' : 'var(--bg-card)',
          border: active ? `2px solid ${eHsl('session')}` : `1px solid ${eHsl(e, 0.28)}`,
          boxShadow: active ? `0 4px 16px ${eHsl('session', 0.28)}` : 'var(--shadow-xs)',
          opacity: disabled ? 0.5 : 1,
          display: 'flex', flexDirection: 'column', gap: 5,
        }}
      >
        {active && <span style={{ position: 'absolute', top: 8, right: 9, ...mono(8, 800, eHsl('session')), textTransform: 'uppercase', letterSpacing: '.06em' }} className="mai-pulse-dot-host">● fase</span>}
        <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
          <span aria-hidden="true" style={{ width: 26, height: 26, borderRadius: 'var(--r-sm)', background: eHsl(e, 0.14), display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontSize: 14, flexShrink: 0 }}>{role.icon}</span>
          <span style={{ ...disp(13, 800, active ? eHsl('session') : 'var(--text)') }}>{role.lb}</span>
        </div>
        <div style={{ ...mono(9.5, 600, 'var(--text-sec)'), lineHeight: 1.35 }}>{role.effect}</div>
        {!compact && <div style={{ ...mono(8.5, 700, eHsl(role.e)), lineHeight: 1.3 }}>{role.priv}</div>}
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 'auto' }}>
          {chosenBy
            ? <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}>
                <MiniAvatar p={chosenBy} size={18} />
                <span style={{ ...mono(8.5, 800, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.05em' }}>scelto</span>
              </span>
            : disabled
              ? <span style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.05em' }}>solo 5 giocatori</span>
              : <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }} aria-label={`${doubloons} dobloni accumulati`}>
                  {Array.from({ length: Math.min(doubloons, 4) }).map((_, i) => (
                    <span key={i} aria-hidden="true" style={{ width: 13, height: 13, borderRadius: '50%', background: SUPPLY.doubloon.color, border: '1px solid #b8860b', marginLeft: i ? -5 : 0, boxShadow: 'inset 0 -1px 1px rgba(0,0,0,.3)' }} />
                  ))}
                  <span style={{ ...mono(10, 800, doubloons ? 'var(--text)' : 'var(--text-muted)'), marginLeft: 3 }}>{doubloons ? `+${doubloons}` : 'libero'}</span>
                </span>}
        </div>
      </button>
    );
  };

  window.PRFlavor = {
    goodOf, MiniAvatar, GoodToken, GoodCount, StorehouseRow, SupplyCounter,
    ColonistPip, PlantationTile, PlantationGrid, BuildingTile, BuildingGrid,
    GovernorToken, RoleCard, KIND_STYLE, mono, disp,
  };
})();

/* MeepleAI SP4 · Coloni di Catan — FLAVOR ATOMS  (window.CatanFlavor)
   Game-specific visual primitives shared by live + summary:
     hexGeom · HexTile · NumberToken · RobberToken · PortMarker ·
     Settlement · City · Road · HexBoard ·
     ResourceToken · ResourceCount · ResourceHandBar ·
     Die · DiceDisplay · VPBadge · LongestRoadBadge · LargestArmyBadge · PlayerDot

   Resource/terrain colors come from window.CATAN.RES (verbatim from catan.json).
   All spacing / radius / surface / text colors are tokens.css vars.
   Reads window.MAI for entityHsl. */

(function () {
  const M = window.MAI;
  const C = window.CATAN;
  const eHsl = M.entityHsl;
  const { RES } = C;
  const mono = (s, w, c) => ({ fontFamily: 'var(--f-mono)', fontSize: s, fontWeight: w, color: c });
  const disp = (s, w, c) => ({ fontFamily: 'var(--f-display)', fontSize: s, fontWeight: w, color: c });
  const SQRT3 = Math.sqrt(3);

  const playerOf = (id) => C.PLAYERS.find(p => p.id === id) || C.PLAYERS[0];

  // ─── hex geometry (flat-top, columns 3·4·5·4·3) ───────────────────────────
  // R = circumradius (center→vertex). width 2R, height √3 R.
  const hexGeom = (R) => {
    const h = SQRT3 * R;
    const maxCol = Math.max(...C.COL_HEIGHTS);
    const centers = {};
    C.HEXES.forEach(hx => {
      const cx = R + hx.col * 1.5 * R;
      const n = C.COL_HEIGHTS[hx.col];
      const colTop = (maxCol - n) / 2 * h;
      const cy = colTop + h / 2 + hx.row * h;
      centers[hx.id] = { cx, cy };
    });
    const width = R + (C.COL_HEIGHTS.length - 1) * 1.5 * R + R;
    const height = maxCol * h;
    // corner k: angle 60k° ; 0 E,1 SE,2 SW,3 W,4 NW,5 NE
    const corner = (hexId, k) => {
      const { cx, cy } = centers[hexId];
      const a = (Math.PI / 180) * (60 * k);
      return { x: cx + R * Math.cos(a), y: cy + R * Math.sin(a) };
    };
    // edge k midpoint: angle 30+60k° ; rotation = that + 90
    const edge = (hexId, k) => {
      const { cx, cy } = centers[hexId];
      const deg = 30 + 60 * k;
      const a = (Math.PI / 180) * deg;
      const ap = (SQRT3 / 2) * R;
      return { x: cx + ap * Math.cos(a), y: cy + ap * Math.sin(a), rot: deg + 90 };
    };
    return { R, h, width, height, centers, corner, edge };
  };

  // ─── NumberToken — production chit with probability pips ───────────────────
  const NumberToken = ({ n, R, hot }) => {
    if (n == null) return null;
    const d = Math.round(R * 0.82);
    const red = n === 6 || n === 8;
    const pips = 6 - Math.abs(7 - n);
    return (
      <div aria-hidden="true" style={{
        width: d, height: d, borderRadius: '50%',
        background: 'radial-gradient(circle at 38% 32%, #fbf3df, #ecdcb8)',
        border: `1.5px solid ${hot ? eHsl('event') : 'rgba(70,45,15,.45)'}`,
        boxShadow: hot ? `0 0 0 3px ${eHsl('event', 0.4)}, inset 0 1px 2px rgba(255,255,255,.7)` : 'inset 0 1px 2px rgba(255,255,255,.6), 0 1px 3px rgba(0,0,0,.25)',
        display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', lineHeight: 1,
      }}>
        <span style={{ ...disp(R * 0.42, 800, red ? '#b4271c' : '#42301a'), fontVariantNumeric: 'tabular-nums' }}>{n}</span>
        <span style={{ display: 'flex', gap: 1.5, marginTop: 1 }}>
          {Array.from({ length: pips }).map((_, i) => (
            <span key={i} style={{ width: Math.max(2, R * 0.045), height: Math.max(2, R * 0.045), borderRadius: '50%', background: red ? '#b4271c' : '#6a4e2a' }} />
          ))}
        </span>
      </div>
    );
  };

  // ─── RobberToken ──────────────────────────────────────────────────────────
  const RobberToken = ({ R, size }) => {
    const d = size || Math.round(R * 0.62);
    return (
      <div aria-label="Ladro" title="Ladro" style={{
        width: d * 0.74, height: d, borderRadius: `${d}px ${d}px ${d * 0.4}px ${d * 0.4}px`,
        background: 'linear-gradient(160deg, #4a4a52, #232327)',
        border: '1.5px solid #16161a', boxShadow: '0 2px 5px rgba(0,0,0,.5), inset 0 1px 1px rgba(255,255,255,.18)',
        position: 'relative',
      }}>
        <span style={{ position: 'absolute', top: d * 0.12, left: '50%', transform: 'translateX(-50%)', width: d * 0.34, height: d * 0.34, borderRadius: '50%', background: 'linear-gradient(160deg,#52525a,#2c2c31)', border: '1.5px solid #16161a' }} />
      </div>
    );
  };

  // ─── pieces: Settlement · City · Road ─────────────────────────────────────
  const Settlement = ({ p, R, atGeom }) => {
    const s = Math.round(R * 0.46);
    return (
      <div title={`Insediamento · ${p.name}`} aria-label={`Insediamento di ${p.name}`} style={{
        position: 'absolute', left: atGeom.x, top: atGeom.y, transform: 'translate(-50%,-50%)',
        width: s, height: s * 0.92, background: p.pc,
        clipPath: 'polygon(50% 0, 100% 40%, 100% 100%, 0 100%, 0 40%)',
        border: `1px solid ${p.pcInk === '#fff' ? 'rgba(0,0,0,.35)' : p.pcInk}`,
        boxShadow: '0 1px 3px rgba(0,0,0,.4)', zIndex: 6,
        outline: p.pc === '#e9e3d4' ? '1px solid rgba(74,58,30,.6)' : 'none',
      }} />
    );
  };
  const City = ({ p, R, atGeom }) => {
    const s = Math.round(R * 0.66);
    return (
      <div title={`Città · ${p.name}`} aria-label={`Città di ${p.name}`} style={{
        position: 'absolute', left: atGeom.x, top: atGeom.y, transform: 'translate(-50%,-50%)',
        width: s, height: s * 0.74, background: p.pc,
        clipPath: 'polygon(0 45%, 34% 45%, 34% 0, 64% 0, 64% 45%, 100% 45%, 100% 100%, 0 100%)',
        border: `1px solid ${p.pcInk === '#fff' ? 'rgba(0,0,0,.4)' : p.pcInk}`,
        boxShadow: '0 1px 4px rgba(0,0,0,.45)', zIndex: 7,
        outline: p.pc === '#e9e3d4' ? '1px solid rgba(74,58,30,.6)' : 'none',
      }} />
    );
  };
  const Road = ({ p, R, atGeom }) => {
    const len = R * 0.86, th = Math.max(4, R * 0.2);
    return (
      <div aria-label={`Strada di ${p.name}`} style={{
        position: 'absolute', left: atGeom.x, top: atGeom.y,
        width: len, height: th, borderRadius: th,
        transform: `translate(-50%,-50%) rotate(${atGeom.rot}deg)`,
        background: p.pc, border: `1px solid ${p.pcInk === '#fff' ? 'rgba(0,0,0,.3)' : 'rgba(58,38,6,.5)'}`,
        boxShadow: '0 1px 2px rgba(0,0,0,.35)', zIndex: 5,
      }} />
    );
  };

  // ─── PortMarker ───────────────────────────────────────────────────────────
  const PortMarker = ({ port, atGeom, R }) => {
    const generic = port.type === 'generic';
    const r = RES[port.type];
    const d = Math.round(R * 0.5);
    return (
      <div title={`Porto ${port.ratio}${generic ? '' : ' · ' + r.lb}`} aria-label={`Porto ${port.ratio}${generic ? '' : ' ' + r.lb}`} style={{
        position: 'absolute', left: atGeom.x, top: atGeom.y, transform: 'translate(-50%,-50%)',
        display: 'flex', alignItems: 'center', gap: 2, padding: '1px 4px 1px 1px',
        borderRadius: 'var(--r-pill)', background: 'rgba(20,26,40,.82)', border: '1px solid rgba(120,160,220,.5)',
        boxShadow: '0 1px 3px rgba(0,0,0,.4)', zIndex: 4,
      }}>
        <span style={{ width: d, height: d, borderRadius: '50%', background: generic ? '#5b7fb0' : r.color, border: '1px solid rgba(0,0,0,.3)', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', ...mono(d * 0.5, 800, generic ? '#fff' : r.ink) }}>{generic ? '?' : r.letter}</span>
        <span style={{ ...mono(R * 0.2, 800, '#cfe0f5') }}>{port.ratio}</span>
      </div>
    );
  };

  // ─── HexTile — terrain hexagon (flat-top) with texture + number ───────────
  const HexTile = ({ hx, geom, robber, hot }) => {
    const r = RES[hx.t];
    const { cx, cy } = geom.centers[hx.id];
    const w = 2 * geom.R, h = geom.h;
    const label = `${r.terrain}${hx.n ? `, produce ${r.res}, numero ${hx.n}` : ', deserto, nessuna risorsa'}${robber ? ', con il ladro' : ''}${hot ? ', in produzione' : ''}`;
    return (
      <div role="img" aria-label={label} className={hot ? 'catan-hot' : undefined} style={{
        position: 'absolute', left: cx - geom.R, top: cy - h / 2, width: w, height: h,
        clipPath: 'polygon(100% 50%, 75% 100%, 25% 100%, 0% 50%, 25% 0%, 75% 0%)',
        background: hot
          ? `repeating-linear-gradient(48deg, ${r.color}, ${r.color} 7px, ${r.stripe} 7px, ${r.stripe} 10px), radial-gradient(circle at 50% 50%, rgba(255,235,170,.55), transparent 70%)`
          : `repeating-linear-gradient(48deg, ${r.color}, ${r.color} 7px, ${r.stripe} 7px, ${r.stripe} 10px)`,
        boxShadow: hot ? `inset 0 0 0 4px ${eHsl('event')}` : 'inset 0 0 0 1px rgba(0,0,0,.18)',
        filter: hot ? `drop-shadow(0 0 5px ${eHsl('event', 0.9)}) drop-shadow(0 0 11px ${eHsl('event', 0.6)})` : 'none',
        zIndex: hot ? 3 : 1,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        transition: 'box-shadow var(--dur-md) var(--ease-out)',
      }}>
        <span style={{ position: 'absolute', top: 4, left: 0, right: 0, textAlign: 'center', ...mono(geom.R * 0.2, 700, r.ink), opacity: 0.65, textTransform: 'uppercase', letterSpacing: '.04em', pointerEvents: 'none' }}>{r.icon}</span>
        {hx.n != null
          ? <NumberToken n={hx.n} R={geom.R} hot={hot} />
          : <span style={{ ...disp(geom.R * 0.26, 800, r.ink), opacity: 0.8, marginTop: 6 }}>Deserto</span>}
      </div>
    );
  };

  // ─── HexBoard — full 19-hex board + pieces + ports + robber + highlight ───
  const HexBoard = ({ R = 44, highlightNumber = null, robberHex = C.ROBBER_HEX, showPorts = true, showPieces = true, pad = 16 }) => {
    const geom = hexGeom(R);
    const hotIds = highlightNumber != null
      ? C.HEXES.filter(h => h.n === highlightNumber && h.id !== robberHex).map(h => h.id)
      : [];
    return (
      <div role="group" aria-label="Tabellone di Catan, 19 esagoni" style={{
        position: 'relative', width: geom.width + pad * 2, height: geom.height + pad * 2,
        margin: '0 auto', flexShrink: 0,
        background: 'radial-gradient(ellipse at 50% 42%, hsl(205,55%,42%), hsl(212,60%,28%))',
        borderRadius: 'var(--r-2xl)', padding: pad,
        boxShadow: 'inset 0 2px 12px rgba(0,0,0,.4), var(--shadow-md)',
        overflow: 'hidden',
      }}>
        <div style={{ position: 'absolute', inset: pad, width: geom.width, height: geom.height }}>
          {C.HEXES.map(hx => (
            <HexTile key={hx.id} hx={hx} geom={geom} robber={hx.id === robberHex} hot={hotIds.includes(hx.id)} />
          ))}
          {showPorts && C.PORTS.map((pt, i) => <PortMarker key={i} port={pt} R={R} atGeom={geom.edge(pt.hex, pt.edge)} />)}
          {showPieces && C.ROADS.map((rd, i) => <Road key={`r${i}`} p={playerOf(rd.p)} R={R} atGeom={geom.edge(rd.hex, rd.edge)} />)}
          {showPieces && C.SETTLEMENTS.map((st, i) => <Settlement key={`s${i}`} p={playerOf(st.p)} R={R} atGeom={geom.corner(st.hex, st.corner)} />)}
          {showPieces && C.CITIES.map((ct, i) => <City key={`c${i}`} p={playerOf(ct.p)} R={R} atGeom={geom.corner(ct.hex, ct.corner)} />)}
          {robberHex && (() => { const g = geom.centers[robberHex]; return (
            <div style={{ position: 'absolute', left: g.cx, top: g.cy - R * 0.05, transform: 'translate(-50%,-50%)', zIndex: 9 }}><RobberToken R={R} /></div>
          ); })()}
        </div>
      </div>
    );
  };

  // ─── ResourceToken / ResourceCount / ResourceHandBar ──────────────────────
  const ResourceToken = ({ id, size = 18, title }) => {
    const r = RES[id];
    return (
      <span title={title || r.lb} aria-label={r.lb} style={{
        width: size, height: size, borderRadius: '50%', flexShrink: 0,
        background: `radial-gradient(circle at 36% 30%, ${r.color}, ${r.stripe})`,
        color: r.ink, display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        ...mono(size * 0.5, 800), border: '1px solid rgba(0,0,0,.25)',
        boxShadow: 'inset 0 1px 1px rgba(255,255,255,.4), inset 0 -1px 2px rgba(0,0,0,.18)',
      }}>{r.letter}</span>
    );
  };
  const ResourceCount = ({ id, n, dim, max }) => (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 4, padding: '2px 8px 2px 3px',
      borderRadius: 'var(--r-pill)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)',
      opacity: dim && !n ? 0.42 : 1,
    }}>
      <ResourceToken id={id} size={16} />
      <span style={{ ...mono(11.5, 800, n ? 'var(--text)' : 'var(--text-muted)'), fontVariantNumeric: 'tabular-nums', minWidth: 8, textAlign: 'center' }}>{n}{max ? <span style={{ color: 'var(--text-muted)', fontWeight: 600 }}>/{max}</span> : null}</span>
    </span>
  );
  // hidden-count variant: shows total cards as a fanned back (opponent hand)
  const HiddenHand = ({ n }) => (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }} title={`${n} carte in mano (segrete)`}>
      <span style={{ display: 'inline-flex' }} aria-hidden="true">
        {Array.from({ length: Math.min(n, 5) }).map((_, i) => (
          <span key={i} style={{ width: 11, height: 15, borderRadius: 2, marginLeft: i ? -6 : 0, background: 'linear-gradient(150deg, hsl(28,55%,46%), hsl(28,60%,34%))', border: '1px solid rgba(0,0,0,.35)', boxShadow: '0 1px 2px rgba(0,0,0,.3)' }} />
        ))}
      </span>
      <span style={{ ...mono(12, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>{n}</span>
    </span>
  );
  const ResourceHandBar = ({ hand, hidden, label = 'Mano' }) => {
    const total = C.RES_ORDER.reduce((s, k) => s + (hand[k] || 0), 0);
    return (
      <div>
        {label && <div style={{ display: 'flex', alignItems: 'baseline', gap: 6, marginBottom: 5 }}>
          <span style={{ ...mono(9, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>{label}</span>
          <span style={{ ...mono(9, 800, eHsl('session')) }}>{total} carte</span>
        </div>}
        {hidden
          ? <HiddenHand n={total} />
          : <div aria-live="polite" style={{ display: 'flex', gap: 5, flexWrap: 'wrap' }}>
              {C.RES_ORDER.map(k => <ResourceCount key={k} id={k} n={hand[k] || 0} dim />)}
            </div>}
      </div>
    );
  };

  // ─── Die · DiceDisplay ────────────────────────────────────────────────────
  // pip layout per face value
  const PIPS = {
    1: [4], 2: [0, 8], 3: [0, 4, 8], 4: [0, 2, 6, 8], 5: [0, 2, 4, 6, 8], 6: [0, 2, 3, 5, 6, 8],
  };
  const Die = ({ v, size = 34, accent }) => (
    <div aria-label={`Dado: ${v}`} style={{
      width: size, height: size, borderRadius: size * 0.22, flexShrink: 0,
      background: accent ? 'linear-gradient(150deg,#fff,#f1e6d6)' : 'linear-gradient(150deg,#fbf6ec,#ede0cb)',
      border: '1px solid rgba(70,45,15,.4)', boxShadow: '0 2px 5px rgba(0,0,0,.3), inset 0 1px 2px rgba(255,255,255,.7)',
      display: 'grid', gridTemplateColumns: 'repeat(3,1fr)', gridTemplateRows: 'repeat(3,1fr)', padding: size * 0.16, gap: size * 0.04,
    }}>
      {Array.from({ length: 9 }).map((_, i) => (
        <span key={i} style={{ borderRadius: '50%', background: (PIPS[v] || []).includes(i) ? '#9a2b1f' : 'transparent', alignSelf: 'center', justifySelf: 'center', width: size * 0.17, height: size * 0.17 }} />
      ))}
    </div>
  );
  const DiceDisplay = ({ dice = C.DICE, compact, onRoll }) => {
    const sum = dice.last[0] + dice.last[1];
    const hot = sum === 6 || sum === 8;
    return (
      <div style={{ display: 'flex', alignItems: 'center', gap: compact ? 8 : 12 }}>
        <div style={{ display: 'flex', gap: 6 }} aria-live="polite" aria-label={`Ultimo lancio: ${dice.last[0]} e ${dice.last[1]}, totale ${sum}`}>
          <Die v={dice.last[0]} size={compact ? 30 : 38} />
          <Die v={dice.last[1]} size={compact ? 30 : 38} />
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', lineHeight: 1 }}>
          <span style={{ ...disp(compact ? 22 : 28, 800, sum === 7 ? eHsl('event') : 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>{sum}</span>
          <span style={{ ...mono(8.5, 800, hot ? '#b4271c' : 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>{sum === 7 ? 'ladro!' : hot ? 'numero caldo' : 'totale'}</span>
        </div>
      </div>
    );
  };
  const DiceHistory = ({ history = C.DICE.history }) => (
    <div style={{ display: 'flex', gap: 3, flexWrap: 'wrap' }} aria-label="Cronologia dei lanci">
      {history.map((n, i) => (
        <span key={i} style={{
          width: 20, height: 20, borderRadius: 'var(--r-sm)', display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          ...mono(10, 800, n === 7 ? '#fff' : (n === 6 || n === 8) ? '#b4271c' : 'var(--text-sec)'),
          background: n === 7 ? eHsl('event') : i === 0 ? 'var(--bg-card)' : 'var(--bg-muted)',
          border: i === 0 ? `1.5px solid ${eHsl('session', 0.6)}` : '1px solid var(--border-light)',
          fontVariantNumeric: 'tabular-nums',
        }}>{n}</span>
      ))}
    </div>
  );

  // ─── badges ───────────────────────────────────────────────────────────────
  const Badge = ({ e, icon, lb, sub, on }) => (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 5, padding: '3px 9px', borderRadius: 'var(--r-pill)',
      background: on ? eHsl(e, 0.14) : 'var(--bg-muted)', color: on ? eHsl(e) : 'var(--text-muted)',
      border: `1px solid ${on ? eHsl(e, 0.4) : 'var(--border-light)'}`,
      ...mono(9, 800), textTransform: 'uppercase', letterSpacing: '.04em', whiteSpace: 'nowrap',
      opacity: on ? 1 : 0.7,
    }}>
      <span aria-hidden="true" style={{ fontSize: 11 }}>{icon}</span>{lb}{sub != null && <span style={{ opacity: 0.8 }}>{sub}</span>}
    </span>
  );
  const LongestRoadBadge = ({ on, len }) => <Badge e="session" icon="🛤️" lb="Strada+" sub={len ? ` ${len}` : null} on={on} />;
  const LargestArmyBadge = ({ on, n }) => <Badge e="event" icon="⚔️" lb="Armata+" sub={n ? ` ${n}` : null} on={on} />;

  const VPBadge = ({ vp, target = 10, leader, size = 'md' }) => {
    const big = size === 'lg';
    return (
      <div style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}>
        <span aria-hidden="true" style={{ fontSize: big ? 18 : 14, color: eHsl('toolkit') }}>⭐</span>
        <span style={{ ...disp(big ? 30 : 22, 800, leader ? eHsl('toolkit') : 'var(--text)'), fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{vp}</span>
        <span style={{ ...mono(big ? 11 : 9.5, 700, 'var(--text-muted)') }}>/{target} PV</span>
      </div>
    );
  };

  const PlayerDot = ({ p, size = 12 }) => (
    <span title={p.name} style={{ width: size, height: size, borderRadius: '50%', background: p.pc, border: `1.5px solid ${p.pc === '#e9e3d4' ? 'rgba(74,58,30,.6)' : 'rgba(0,0,0,.3)'}`, flexShrink: 0, display: 'inline-block', boxShadow: 'inset 0 1px 1px rgba(255,255,255,.3)' }} />
  );

  window.CatanFlavor = {
    hexGeom, playerOf, mono, disp,
    NumberToken, RobberToken, Settlement, City, Road, PortMarker, HexTile, HexBoard,
    ResourceToken, ResourceCount, HiddenHand, ResourceHandBar,
    Die, DiceDisplay, DiceHistory,
    LongestRoadBadge, LargestArmyBadge, VPBadge, PlayerDot, Badge,
  };
})();

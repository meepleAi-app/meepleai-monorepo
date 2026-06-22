/* MeepleAI SP4 · Power Grid — FLAVOR ATOMS  (window.PGFlavor)
   Primitivi visivi game-specific condivisi da live + summary:
     ResourceToken · ResourceCount · ResourceRow · ElektroCounter ·
     SupplyStat · CityPip · PlantCard · PlantMini · StepIndicator ·
     PhasePill · ReverseBadge · MiniAvatar

   Colori risorse/elektro da window.PG (verbatim power-grid.json). Ogni altro
   valore di colore/spazio/raggio = var di tokens.css. Legge window.MAI (eHsl). */

(function () {
  const M = window.MAI;
  const PG = window.PG;
  const eHsl = M.entityHsl;
  const { RES, RES_ORDER, SUPPLY } = PG;
  const mono = (s, w, c) => ({ fontFamily: 'var(--f-mono)', fontSize: s, fontWeight: w, color: c });
  const disp = (s, w, c) => ({ fontFamily: 'var(--f-display)', fontSize: s, fontWeight: w, color: c });

  const resOf = (id) => RES[id] || { lb: id, glyph: '?', color: '#888', dark: '#fff' };

  // ─── MiniAvatar ───────────────────────────────────────────────────────────
  const MiniAvatar = ({ p, size = 22, ring, dim }) => (
    <div aria-hidden="true" style={{
      width: size, height: size, borderRadius: '50%', flexShrink: 0, opacity: dim ? 0.4 : 1,
      background: `linear-gradient(135deg, hsl(${p.hue},70%,64%), hsl(${p.hue},58%,44%))`,
      color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
      ...disp(size * 0.46, 800, '#fff'),
      border: ring ? `2px solid ${eHsl('session')}` : '2px solid var(--bg-card)',
    }}>{p.name[0]}</div>
  );

  // ─── ResourceToken — barile/disco colorato con glyph 1-lettera ────────────
  const ResourceToken = ({ id, size = 18, title }) => {
    const g = resOf(id);
    const nuke = id === 'uranium';
    return (
      <span title={title || g.lb} aria-label={g.lb} style={{
        width: size, height: size, borderRadius: nuke ? '50%' : 'var(--r-xs)', flexShrink: 0,
        background: g.color, color: g.dark,
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        ...mono(size * 0.5, 800), border: '1px solid rgba(0,0,0,.28)',
        boxShadow: 'inset 0 1px 1px rgba(255,255,255,.28), inset 0 -1px 2px rgba(0,0,0,.22)',
      }}>{g.glyph}</span>
    );
  };

  // ─── ResourceCount — token + numero ───────────────────────────────────────
  const ResourceCount = ({ id, n, dim }) => (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 4, padding: '2px 7px 2px 3px',
      borderRadius: 'var(--r-pill)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)',
      opacity: dim && !n ? 0.42 : 1,
    }}>
      <ResourceToken id={id} size={15} />
      <span style={{ ...mono(11, 800, n ? 'var(--text)' : 'var(--text-muted)'), fontVariantNumeric: 'tabular-nums', minWidth: 8, textAlign: 'center' }}>{n}</span>
    </span>
  );

  // ResourceRow — i 4 contatori risorsa (magazzino giocatore)
  const ResourceRow = ({ store, label = 'Risorse in scorta' }) => (
    <div>
      {label && <div style={{ ...mono(9, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 5 }}>{label}</div>}
      <div aria-live="polite" style={{ display: 'flex', gap: 5, flexWrap: 'wrap' }}>
        {RES_ORDER.map(id => <ResourceCount key={id} id={id} n={store[id] || 0} dim />)}
      </div>
    </div>
  );

  // ─── ElektroCounter — valuta, display grande ──────────────────────────────
  const ElektroCounter = ({ value, big, compact, label = 'Elektro' }) => {
    const s = SUPPLY.elektro;
    if (compact) {
      return (
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, padding: '2px 8px 2px 3px', borderRadius: 'var(--r-pill)', background: `${s.color}1f`, border: `1px solid ${s.color}55`, flexShrink: 0 }}>
          <span aria-hidden="true" style={{ width: 16, height: 16, borderRadius: '50%', flexShrink: 0, background: `radial-gradient(circle at 35% 30%, #ffe9a8, ${s.color})`, color: s.dark, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', ...mono(9, 800), border: '1px solid #b8860b' }}>€</span>
          <span style={{ ...disp(12, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>{value}</span>
        </span>
      );
    }
    return (
      <div style={{
        display: 'flex', alignItems: 'center', gap: big ? 9 : 7, padding: big ? '8px 13px' : '5px 9px',
        borderRadius: 'var(--r-md)', background: `linear-gradient(135deg, ${s.color}22, ${s.color}10)`,
        border: `1px solid ${s.color}55`,
      }}>
        <span aria-hidden="true" style={{
          width: big ? 30 : 22, height: big ? 30 : 22, borderRadius: '50%', flexShrink: 0,
          background: `radial-gradient(circle at 35% 30%, #ffe9a8, ${s.color})`, color: s.dark,
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          ...mono(big ? 13 : 10, 800), border: '1px solid #b8860b',
          boxShadow: 'inset 0 1px 1px rgba(255,255,255,.5), inset 0 -1px 2px rgba(0,0,0,.25)',
        }}>€</span>
        <div style={{ minWidth: 0, lineHeight: 1 }}>
          <div aria-live="polite" style={{ ...disp(big ? 24 : 17, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>{value}</div>
          <div style={{ ...mono(8.5, 700, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>{label}</div>
        </div>
      </div>
    );
  };

  // ─── SupplyStat — città / alimentate (mini stat) ──────────────────────────
  const SupplyStat = ({ kind, value, sub, accent }) => {
    const s = SUPPLY[kind] || SUPPLY.city;
    const col = accent || 'session';
    return (
      <div style={{ display: 'flex', alignItems: 'center', gap: 7, padding: '5px 9px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)', flex: 1, minWidth: 0 }}>
        <span aria-hidden="true" style={{ width: 22, height: 22, borderRadius: 'var(--r-sm)', flexShrink: 0, background: eHsl(col, 0.16), color: eHsl(col), display: 'inline-flex', alignItems: 'center', justifyContent: 'center', ...mono(11, 800) }}>{s.glyph}</span>
        <div style={{ minWidth: 0, lineHeight: 1 }}>
          <div aria-live="polite" style={{ ...disp(17, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>{value}</div>
          <div style={{ ...mono(8.5, 700, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.05em', whiteSpace: 'nowrap' }}>{sub || s.lb}</div>
        </div>
      </div>
    );
  };

  // ─── CityPip — pallino città colorato per owner ───────────────────────────
  const CityPip = ({ player, size = 12, powered, empty }) => (
    <span aria-hidden="true" title={player ? player.name : 'non costruita'} style={{
      width: size, height: size, borderRadius: '50%', flexShrink: 0, display: 'inline-block',
      background: empty ? 'transparent' : `hsl(${player.hue},62%,52%)`,
      border: empty ? `1.5px dashed var(--border-strong)` : `1.5px solid ${powered ? SUPPLY.powered.color : 'rgba(0,0,0,.25)'}`,
      boxShadow: !empty && powered ? `0 0 6px ${SUPPLY.powered.color}99` : 'none',
    }} />
  );

  // ─── PlantCard — la carta centrale distintiva (numero grande + icona) ─────
  // n = numero (puntata minima). Header colorato per tipo combustibile.
  const fuelMeta = (plant) => {
    if (plant.eco) return { e: 'toolkit', lb: 'Eco', tone: '#1e8449' };
    if (plant.hybrid) return { e: 'agent', lb: 'Ibrida', tone: '#a04000' };
    const id = plant.res[0];
    const map = { coal: '#2c3e50', oil: '#34495e', garbage: '#7d6608', uranium: '#1e8449' };
    return { e: 'kb', lb: RES[id].lb, tone: map[id] || '#555' };
  };

  // slots = combustibile attualmente caricato (live, su plant posseduta)
  const PlantCard = ({ plant, w = 116, future, buyable, loaded, ghost, onClick, compact }) => {
    const fm = fuelMeta(plant);
    const h = compact ? 96 : 132;
    if (ghost) {
      return (
        <div aria-hidden="true" style={{ width: w, height: h, borderRadius: 'var(--r-md)', border: '1.5px dashed var(--border-strong)', background: 'var(--bg-sunken)', opacity: 0.5, display: 'flex', alignItems: 'center', justifyContent: 'center', ...mono(9, 700, 'var(--text-muted)'), flexShrink: 0 }}>vuoto</div>
      );
    }
    return (
      <button type="button" onClick={onClick} disabled={!onClick}
        aria-label={`Centrale ${plant.n}, ${fm.lb}, ${plant.cap} città, consumo ${plant.cons || 0}`}
        style={{
          width: w, minHeight: h, borderRadius: 'var(--r-md)', flexShrink: 0, position: 'relative',
          textAlign: 'left', cursor: onClick ? 'pointer' : 'default', overflow: 'hidden',
          background: 'var(--bg-card)', padding: 0,
          border: buyable ? `2px solid ${eHsl('session', 0.55)}` : `1px solid var(--border)`,
          boxShadow: buyable ? `0 4px 14px ${eHsl('session', 0.22)}` : 'var(--shadow-xs)',
          opacity: future ? 0.72 : 1, display: 'flex', flexDirection: 'column',
        }}>
        {/* header: numero + tipo */}
        <div style={{ display: 'flex', alignItems: 'stretch', borderBottom: `1px solid ${fm.tone}55` }}>
          <div style={{ width: compact ? 36 : 44, flexShrink: 0, background: fm.tone, color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', ...disp(compact ? 20 : 26, 800, '#fff'), fontVariantNumeric: 'tabular-nums', textShadow: '0 1px 2px rgba(0,0,0,.3)' }}>{plant.n}</div>
          <div style={{ flex: 1, padding: '4px 7px', display: 'flex', flexDirection: 'column', justifyContent: 'center', background: `${fm.tone}14` }}>
            <span style={{ ...mono(8, 800, fm.tone === '#7d6608' ? '#7d6608' : fm.tone), textTransform: 'uppercase', letterSpacing: '.05em' }}>{fm.lb}</span>
            <span style={{ ...mono(7.5, 700, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.04em' }}>min. {plant.n}</span>
          </div>
        </div>
        {/* body: consumo → capacità */}
        <div style={{ flex: 1, padding: compact ? '7px 8px' : '9px 9px', display: 'flex', flexDirection: 'column', gap: 6, justifyContent: 'space-between' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
            {plant.eco
              ? <span style={{ ...mono(8.5, 800, eHsl('toolkit')), display: 'inline-flex', alignItems: 'center', gap: 4 }}><span aria-hidden="true">🌿</span>nessun combustibile</span>
              : (<>
                  {Array.from({ length: plant.cons }).map((_, i) => (
                    plant.hybrid && i === 0
                      ? <span key={i} style={{ display: 'inline-flex', alignItems: 'center' }}><ResourceToken id={plant.res[0]} size={15} /><span style={{ ...mono(9, 800, 'var(--text-muted)'), margin: '0 1px' }}>/</span><ResourceToken id={plant.res[1]} size={15} /></span>
                      : <ResourceToken key={i} id={plant.hybrid ? plant.res[0] : plant.res[0]} size={15} />
                  ))}
                  <span style={{ ...mono(8.5, 700, 'var(--text-muted)') }}>×{plant.cons}</span>
                </>)}
          </div>
          {/* capacità città (grande, leggibilissima) */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <span style={{ width: compact ? 24 : 28, height: compact ? 24 : 28, borderRadius: '50%', flexShrink: 0, background: eHsl('session', 0.14), color: eHsl('session'), display: 'inline-flex', alignItems: 'center', justifyContent: 'center', ...disp(compact ? 14 : 16, 800), fontVariantNumeric: 'tabular-nums', border: `1.5px solid ${eHsl('session', 0.4)}` }}>{plant.cap}</span>
            <span style={{ ...mono(8.5, 700, 'var(--text-muted)'), lineHeight: 1.1 }}>città<br/>alimentate</span>
          </div>
          {/* combustibile caricato (solo plant possedute) */}
          {loaded && (
            <div style={{ display: 'flex', gap: 3, alignItems: 'center', paddingTop: 4, borderTop: '1px solid var(--border-light)' }} aria-label="combustibile caricato">
              {plant.eco
                ? <span style={{ ...mono(8, 700, eHsl('toolkit')) }}>🌿 pronta</span>
                : Array.from({ length: plant.cons }).map((_, i) => {
                    const total = Object.values(loaded).reduce((a, b) => a + b, 0);
                    return <span key={i} style={{ opacity: i < total ? 1 : 0.28 }}><ResourceToken id={plant.res[0]} size={12} title={i < total ? 'caricato' : 'vuoto'} /></span>;
                  })}
            </div>
          )}
        </div>
        {buyable && <span style={{ position: 'absolute', top: 5, right: 5, ...mono(7.5, 800, '#fff'), background: eHsl('session'), padding: '1px 6px', borderRadius: 'var(--r-pill)', textTransform: 'uppercase', letterSpacing: '.05em' }}>asta</span>}
      </button>
    );
  };

  // ─── PlantMini — centrale del giocatore nel rail (numero + cap + fuel) ─────
  const PlantMini = ({ plant, loaded }) => {
    const fm = fuelMeta(plant);
    const total = loaded ? Object.values(loaded).reduce((a, b) => a + b, 0) : 0;
    return (
      <div title={`Centrale ${plant.n} · ${plant.cap} città`} style={{
        display: 'flex', alignItems: 'center', gap: 8, padding: '6px 8px', borderRadius: 'var(--r-md)',
        background: 'var(--bg-card)', border: '1px solid var(--border)', flexShrink: 0,
      }}>
        <span style={{ width: 30, height: 30, borderRadius: 'var(--r-sm)', flexShrink: 0, background: fm.tone, color: '#fff', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', ...disp(15, 800, '#fff'), fontVariantNumeric: 'tabular-nums' }}>{plant.n}</span>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
            <span style={{ ...mono(9, 800, eHsl('session')) }}>⚡{plant.cap}</span>
            <span style={{ ...mono(8, 700, 'var(--text-muted)'), textTransform: 'uppercase' }}>{fm.lb}</span>
          </div>
          <div style={{ display: 'flex', gap: 2, marginTop: 3, alignItems: 'center' }}>
            {plant.eco
              ? <span style={{ ...mono(8, 700, eHsl('toolkit')) }}>🌿 pronta</span>
              : Array.from({ length: plant.cons }).map((_, i) => (
                  <span key={i} style={{ opacity: i < total ? 1 : 0.3 }}><ResourceToken id={plant.res[0]} size={11} /></span>
                ))}
          </div>
        </div>
      </div>
    );
  };

  // ─── StepIndicator — Step 1/2/3 prominente ────────────────────────────────
  const StepIndicator = ({ step, compact }) => (
    <div role="group" aria-label={`Step di gioco ${step} di 3`} style={{ display: 'inline-flex', alignItems: 'center', gap: compact ? 4 : 5 }}>
      {!compact && <span style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>Step</span>}
      {[1, 2, 3].map(n => {
        const on = n === step, past = n < step;
        return (
          <span key={n} aria-current={on ? 'true' : undefined} style={{
            width: compact ? 18 : 22, height: compact ? 18 : 22, borderRadius: 'var(--r-sm)',
            background: on ? eHsl('game') : past ? eHsl('game', 0.2) : 'var(--bg-muted)',
            color: on ? '#fff' : past ? eHsl('game') : 'var(--text-muted)',
            border: on ? 'none' : '1px solid var(--border)',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            ...disp(compact ? 11 : 13, 800), boxShadow: on ? `0 2px 8px ${eHsl('game', 0.4)}` : 'none',
          }}>{n}</span>
        );
      })}
    </div>
  );

  // ─── PhasePill — fase compatta ────────────────────────────────────────────
  const PhasePill = ({ phase, active }) => (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 5, padding: '3px 9px', borderRadius: 'var(--r-pill)',
      background: active ? eHsl('session', 0.14) : 'var(--bg-muted)',
      color: active ? eHsl('session') : 'var(--text-muted)',
      border: `1px solid ${active ? eHsl('session', 0.4) : 'var(--border-light)'}`,
      ...mono(9.5, 800), whiteSpace: 'nowrap',
    }}><span aria-hidden="true">{phase.icon}</span>{phase.n}. {phase.short}</span>
  );

  // ─── ReverseBadge — indicatore ordine ribaltato ───────────────────────────
  const ReverseBadge = ({ reverse }) => (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 5, padding: '2px 9px', borderRadius: 'var(--r-pill)',
      background: reverse ? eHsl('event', 0.12) : eHsl('toolkit', 0.12),
      color: reverse ? eHsl('event') : eHsl('toolkit'),
      border: `1px solid ${reverse ? eHsl('event', 0.32) : eHsl('toolkit', 0.32)}`,
      ...mono(9, 800), textTransform: 'uppercase', letterSpacing: '.05em', whiteSpace: 'nowrap',
    }}>
      <span aria-hidden="true">{reverse ? '⇄' : '→'}</span>{reverse ? 'Ordine ribaltato' : 'Ordine normale'}
    </span>
  );

  window.PGFlavor = {
    resOf, fuelMeta, mono, disp,
    MiniAvatar, ResourceToken, ResourceCount, ResourceRow, ElektroCounter,
    SupplyStat, CityPip, PlantCard, PlantMini, StepIndicator, PhasePill, ReverseBadge,
  };
})();

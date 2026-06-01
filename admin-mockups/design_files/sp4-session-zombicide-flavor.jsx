/* MeepleAI SP4 · Zombicide: Green Horde — FLAVOR ATOMS  (window.ZOMFlavor)
   Primitivi visivi game-specific condivisi da live + summary:
     mono · disp · SurvivorAvatar · WoundPips · XPBar · APCounter · LevelChip ·
     SkillTree · EquipChip · EquipSlots · ZombieToken · ZombieCounter ·
     DangerLevel · DieFace · DiceTray · WeaponRefRow · ObjectiveRow ·
     PhaseTimeline · MapTilesGrid

   Colori zombie/skill da window.ZOM (verbatim zombicide-green-horde.json /
   mappati su token). Ogni altro valore = var di tokens.css. Legge window.MAI (eHsl). */

(function () {
  const M = window.MAI;
  const Z = window.ZOM;
  const eHsl = M.entityHsl;
  const { LEVELS, levelMeta, levelIndex, ZTYPES, DANGER } = Z;
  const mono = (s, w, c) => ({ fontFamily: 'var(--f-mono)', fontSize: s, fontWeight: w, color: c });
  const disp = (s, w, c) => ({ fontFamily: 'var(--f-display)', fontSize: s, fontWeight: w, color: c });

  // ─── SurvivorAvatar (morto = desaturato + teschio) ────────────────────────
  const SurvivorAvatar = ({ s, size = 26, ring, dead }) => {
    const isDead = dead != null ? dead : s.dead;
    return (
      <div aria-hidden="true" title={s.name} style={{
        width: size, height: size, borderRadius: '50%', flexShrink: 0, position: 'relative',
        background: isDead ? 'var(--bg-sunken)' : `linear-gradient(135deg, hsl(${s.hue},66%,62%), hsl(${s.hue},54%,42%))`,
        color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
        ...disp(size * 0.46, 800, isDead ? 'var(--text-muted)' : '#fff'),
        border: ring ? `2px solid ${eHsl('session')}` : '2px solid var(--bg-card)',
        filter: isDead ? 'grayscale(1)' : 'none',
      }}>{isDead ? '☠' : s.name[0]}</div>
    );
  };

  // ─── WoundPips (0/1/2 · 2 = morto · aria assertivo) ───────────────────────
  const WoundPips = ({ wounds, dead, size = 16 }) => {
    const isDead = dead != null ? dead : wounds >= 2;
    return (
      <div role="status" aria-live="assertive"
        aria-label={isDead ? 'Survivor a terra: 2 ferite' : `Ferite: ${wounds} su 2`}
        style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
        {[0, 1].map(i => {
          const filled = i < wounds;
          return (
            <span key={i} aria-hidden="true" style={{
              width: size, height: size, borderRadius: 'var(--r-sm)', display: 'inline-flex',
              alignItems: 'center', justifyContent: 'center', ...mono(size * 0.6, 800, filled ? '#fff' : 'var(--text-muted)'),
              background: filled ? eHsl('danger', 0.9) : 'var(--bg-sunken)',
              border: `1px solid ${filled ? eHsl('danger') : 'var(--border-strong)'}`,
            }}>{filled ? '✚' : ''}</span>
          );
        })}
        {isDead && <span style={{ ...mono(8.5, 800, eHsl('danger')), textTransform: 'uppercase', letterSpacing: '.05em', marginLeft: 2 }}>a terra</span>}
      </div>
    );
  };

  // ─── LevelChip — livello skill color-coded ────────────────────────────────
  const LevelChip = ({ level, compact }) => {
    const m = levelMeta(level);
    return (
      <span style={{
        display: 'inline-flex', alignItems: 'center', gap: 4, padding: compact ? '1px 7px' : '2px 9px',
        borderRadius: 'var(--r-pill)', background: eHsl(m.e, 0.14), color: eHsl(m.e),
        border: `1px solid ${eHsl(m.e, 0.4)}`, ...mono(compact ? 8.5 : 9.5, 800),
        textTransform: 'uppercase', letterSpacing: '.05em', whiteSpace: 'nowrap',
      }}>
        <span aria-hidden="true" style={{ width: 8, height: 8, borderRadius: '50%', background: eHsl(m.e), flexShrink: 0 }} />{m.lb}
      </span>
    );
  };

  // ─── XPBar — 4 segmenti (Blu/Giallo/Aran/Rosso) + marker XP corrente ──────
  const XPBar = ({ xp, level, compact }) => {
    const lv = level || Z.levelOf(xp);
    const idx = levelIndex(lv);
    return (
      <div>
        <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', marginBottom: 4 }}>
          <span style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>XP · progressione</span>
          <span style={{ ...disp(compact ? 13 : 15, 800, eHsl(levelMeta(lv).e)), fontVariantNumeric: 'tabular-nums' }}>{xp}</span>
        </div>
        <div style={{ display: 'flex', gap: 3 }} role="img" aria-label={`XP ${xp}, livello ${levelMeta(lv).lb}`}>
          {LEVELS.map((l, i) => {
            const reached = i <= idx;
            const isCur = i === idx;
            return (
              <div key={l.id} title={`${l.lb} · ${l.range} XP`} style={{ flex: l.id === 'red' ? 0.7 : 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
                <div style={{
                  height: compact ? 7 : 9, borderRadius: 'var(--r-pill)',
                  background: reached ? eHsl(l.e, isCur ? 1 : 0.55) : 'var(--bg-sunken)',
                  border: `1px solid ${reached ? eHsl(l.e) : 'var(--border-light)'}`,
                  boxShadow: isCur ? `0 0 8px ${eHsl(l.e, 0.5)}` : 'none',
                }} />
                <span style={{ ...mono(7, 700, isCur ? eHsl(l.e) : 'var(--text-muted)'), textAlign: 'center' }}>{l.range}</span>
              </div>
            );
          })}
        </div>
      </div>
    );
  };

  // ─── APCounter — punti azione (3 base + bonus skill) ──────────────────────
  const APCounter = ({ ap, base, bonus, compact }) => (
    <div role="group" aria-label={`Punti azione: ${ap}`} style={{
      display: 'inline-flex', alignItems: 'center', gap: 6, padding: compact ? '4px 9px' : '6px 11px',
      borderRadius: 'var(--r-md)', background: eHsl('warning', 0.1), border: `1px solid ${eHsl('warning', 0.32)}`,
    }}>
      <span aria-hidden="true" style={{ ...disp(compact ? 16 : 19, 800, eHsl('warning')), fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>⚡{ap}</span>
      <div style={{ lineHeight: 1.05 }}>
        <div style={{ ...mono(8, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.05em' }}>azioni</div>
        <div style={{ ...mono(8, 700, 'var(--text-sec)') }}>{base} base{bonus ? ` +${bonus}` : ''}</div>
      </div>
    </div>
  );

  // ─── SkillTree — 4 livelli color-coded · chosen highlighted · futuri greyed ─
  // layout: 'grid' (4 colonne) o 'rows' (verticale, per contesti stretti)
  const SkillNode = ({ name, tone, kind }) => {
    // kind: 'chosen' | 'option' | 'pick' | 'locked'
    const styles = {
      chosen: { bg: eHsl(tone, 0.16), bd: eHsl(tone, 0.5), col: eHsl(tone), wt: 800, opacity: 1, dash: false },
      pick:   { bg: 'var(--bg-card)', bd: eHsl(tone, 0.45), col: eHsl(tone), wt: 700, opacity: 1, dash: true },
      option: { bg: 'var(--bg-muted)', bd: 'var(--border-light)', col: 'var(--text-muted)', wt: 600, opacity: 0.7, dash: false },
      locked: { bg: 'var(--bg-sunken)', bd: 'var(--border-light)', col: 'var(--text-muted)', wt: 600, opacity: 0.5, dash: true },
    }[kind];
    return (
      <div style={{
        display: 'flex', alignItems: 'center', gap: 5, padding: '4px 7px', borderRadius: 'var(--r-sm)',
        background: styles.bg, border: `1px ${styles.dash ? 'dashed' : 'solid'} ${styles.bd}`,
        opacity: styles.opacity,
      }}>
        {kind === 'chosen' && <span aria-hidden="true" style={{ ...mono(8.5, 800, styles.col), flexShrink: 0 }}>✓</span>}
        {kind === 'pick' && <span aria-hidden="true" style={{ ...mono(8.5, 800, styles.col), flexShrink: 0 }}>◆</span>}
        {kind === 'locked' && <span aria-hidden="true" style={{ ...mono(8, 700, styles.col), flexShrink: 0 }}>🔒</span>}
        <span style={{ ...disp(10.5, styles.wt, styles.col), whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{name}</span>
      </div>
    );
  };
  const SkillLevelColumn = ({ row, layout }) => {
    const m = levelMeta(row.level);
    const isRow = layout === 'rows';
    // nodi: il chosen (se c'è) + alternative. Per livello locked tutte greyed.
    const nodes = [];
    row.options.forEach(opt => {
      if (row.status === 'done') {
        nodes.push({ name: opt, kind: opt === row.chosen ? 'chosen' : 'option' });
      } else if (row.status === 'available') {
        nodes.push({ name: opt, kind: row.chosen ? (opt === row.chosen ? 'chosen' : 'option') : 'pick' });
      } else {
        nodes.push({ name: opt, kind: 'locked' });
      }
    });
    return (
      <div style={{
        flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', gap: 5,
        padding: 7, borderRadius: 'var(--r-md)',
        background: row.status === 'locked' ? 'var(--bg-sunken)' : eHsl(m.e, 0.05),
        border: `1px solid ${row.status === 'locked' ? 'var(--border-light)' : eHsl(m.e, row.status === 'available' ? 0.4 : 0.22)}`,
        ...(isRow ? { flexDirection: 'row', alignItems: 'center', flexWrap: 'wrap' } : {}),
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 5, ...(isRow ? { width: 92, flexShrink: 0 } : { marginBottom: 1 }) }}>
          <span aria-hidden="true" style={{ width: 9, height: 9, borderRadius: '50%', background: eHsl(m.e), flexShrink: 0, opacity: row.status === 'locked' ? 0.5 : 1 }} />
          <span style={{ ...mono(8.5, 800, row.status === 'locked' ? 'var(--text-muted)' : eHsl(m.e)), textTransform: 'uppercase', letterSpacing: '.04em' }}>{m.lb}</span>
          {row.status === 'available' && !row.chosen && <span style={{ ...mono(7, 800, eHsl(m.e)), textTransform: 'uppercase' }}>scegli</span>}
        </div>
        <div style={{ display: 'flex', flexDirection: isRow ? 'row' : 'column', gap: 4, flexWrap: 'wrap', flex: isRow ? 1 : 'none', minWidth: 0 }}>
          {nodes.map((n, i) => <SkillNode key={i} name={n.name} tone={m.e} kind={n.kind} />)}
        </div>
      </div>
    );
  };
  const SkillTree = ({ survivor, layout = 'grid' }) => (
    <div role="group" aria-label={`Albero abilità di ${survivor.name}`} style={{
      display: layout === 'rows' ? 'flex' : 'grid',
      ...(layout === 'rows' ? { flexDirection: 'column', gap: 6 } : { gridTemplateColumns: 'repeat(4, 1fr)', gap: 6 }),
    }}>
      {survivor.tree.map(row => <SkillLevelColumn key={row.level} row={row} layout={layout} />)}
    </div>
  );

  // ─── EquipChip / EquipSlots ───────────────────────────────────────────────
  const TYPE_LB = { melee: 'Mischia', ranged: 'Distanza', armor: 'Armatura', item: 'Oggetto' };
  const EquipCard = ({ id, slotLb, w }) => {
    const e = Z.eq(id);
    const empty = !id;
    if (empty) {
      return (
        <div style={{ width: w, minHeight: 64, borderRadius: 'var(--r-md)', border: '1.5px dashed var(--border-strong)', background: 'var(--bg-sunken)', opacity: 0.55, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 2, flexShrink: 0 }}>
          <span style={{ ...mono(8, 700, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.05em' }}>{slotLb}</span>
          <span style={{ ...mono(8.5, 700, 'var(--text-muted)') }}>vuoto</span>
        </div>
      );
    }
    const acc = e.accent || 'tool';
    return (
      <div title={`${e.name} · ${TYPE_LB[e.type]}`} style={{
        width: w, minHeight: 64, borderRadius: 'var(--r-md)', background: 'var(--bg-card)',
        border: `1px solid ${eHsl(acc, 0.35)}`, overflow: 'hidden', display: 'flex', flexDirection: 'column', flexShrink: 0,
        boxShadow: 'var(--shadow-xs)',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 5, padding: '4px 7px', background: eHsl(acc, 0.1), borderBottom: `1px solid ${eHsl(acc, 0.2)}` }}>
          <span aria-hidden="true" style={{ fontSize: 12, flexShrink: 0 }}>{e.glyph}</span>
          <span style={{ flex: 1, minWidth: 0, ...mono(7.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.04em' }}>{slotLb}</span>
          <span style={{ ...mono(7.5, 800, eHsl(acc)), textTransform: 'uppercase' }}>{TYPE_LB[e.type]}</span>
        </div>
        <div style={{ flex: 1, padding: '5px 7px', display: 'flex', flexDirection: 'column', justifyContent: 'space-between', gap: 3 }}>
          <span style={{ ...disp(11.5, 800, 'var(--text)'), lineHeight: 1.15 }}>{e.name}</span>
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
            {e.range != null && <span style={{ ...mono(8, 700, 'var(--text-sec)') }}>gittata {e.range}</span>}
            {e.dice != null && <span style={{ ...mono(8, 700, eHsl(acc)) }}>{e.dice} dado{e.dice > 1 ? 'i' : ''}</span>}
            {e.dmg != null && <span style={{ ...mono(8, 700, 'var(--text-sec)') }}>danno {e.dmg}</span>}
            {e.save && <span style={{ ...mono(8, 700, eHsl(acc)) }}>save {e.save}</span>}
            {e.area && <span style={{ ...mono(8, 800, eHsl('event')) }}>area</span>}
            {e.reload && <span style={{ ...mono(8, 700, 'var(--text-muted)') }}>ricarica</span>}
          </div>
        </div>
      </div>
    );
  };
  const EquipSlots = ({ survivor, w = 124, cols }) => (
    <div style={{ display: 'grid', gridTemplateColumns: cols ? `repeat(${cols}, 1fr)` : `repeat(auto-fill, minmax(${w}px, 1fr))`, gap: 6 }}>
      {Z.EQUIP_SLOTS.map(slot => <EquipCard key={slot.id} id={survivor.equip[slot.id]} slotLb={slot.lb} w="100%" />)}
    </div>
  );

  // ─── ZombieToken — gettone colorato distintivo per tipo (glyph 1-lettera) ──
  const ZombieToken = ({ type, size = 22, title }) => {
    const z = ZTYPES[type] || { lb: type, glyph: '?', color: '#888' };
    return (
      <span title={title || z.lb} aria-label={z.lb} style={{
        width: size, height: size, borderRadius: 'var(--r-sm)', flexShrink: 0,
        background: z.color, color: '#fff',
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        ...mono(size * 0.5, 800), border: '1px solid rgba(0,0,0,.3)',
        boxShadow: 'inset 0 1px 1px rgba(255,255,255,.22), inset 0 -1px 2px rgba(0,0,0,.28)',
      }}>{z.glyph}</span>
    );
  };

  // ─── ZombieCounter — counter grande con icona distintiva (aria tipo+count) ─
  const ZombieCounter = ({ type, count, big }) => {
    const z = ZTYPES[type];
    const boss = type === 'abomination' || type === 'necromancer';
    return (
      <div role="status" aria-label={`${z.lb}: ${count} sulla plancia`} style={{
        display: 'flex', alignItems: 'center', gap: big ? 9 : 7, padding: big ? '9px 11px' : '7px 9px',
        borderRadius: 'var(--r-md)', background: 'var(--bg-card)',
        border: `1px solid ${boss ? z.color : 'var(--border)'}`,
        boxShadow: boss ? `0 0 0 1px ${z.color}44` : 'var(--shadow-xs)',
      }}>
        <ZombieToken type={type} size={big ? 30 : 26} />
        <div style={{ minWidth: 0, lineHeight: 1.05, flex: 1 }}>
          <div style={{ ...mono(8.5, 800, 'var(--text-sec)'), whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{z.lb}</div>
          <div style={{ ...mono(7.5, 700, 'var(--text-muted)') }}>{z.actions} att · dmg {z.dmg || '—'}</div>
        </div>
        <span aria-hidden="true" style={{ ...disp(big ? 24 : 20, 800, count > 0 ? 'var(--text)' : 'var(--text-muted)'), fontVariantNumeric: 'tabular-nums' }}>{count}</span>
      </div>
    );
  };

  // ─── DangerLevel — indicatore Blu/Giallo/Aran/Rosso (current evidenziato) ──
  const DangerLevel = ({ current, compact, reason }) => {
    const idx = levelIndex(current);
    return (
      <div role="group" aria-label={`Danger Level: ${levelMeta(current).lb}`} style={{ display: 'flex', alignItems: 'center', gap: compact ? 6 : 9, flexWrap: 'wrap', rowGap: 5 }}>
        <div style={{ display: 'flex', flexDirection: 'column', lineHeight: 1.1, flexShrink: 0 }}>
          <span style={{ ...mono(8, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>Danger Level</span>
          {!compact && reason && <span style={{ ...mono(7.5, 700, 'var(--text-muted)') }}>{reason}</span>}
        </div>
        <div style={{ display: 'flex', gap: 3, flexWrap: 'wrap', rowGap: 4, minWidth: 0 }}>
          {DANGER.map((d, i) => {
            const on = i === idx, past = i < idx;
            return (
              <span key={d.id} aria-current={on ? 'true' : undefined} title={d.spawn} style={{
                display: 'inline-flex', alignItems: 'center', gap: 4, padding: compact ? '2px 7px' : '3px 9px', borderRadius: 'var(--r-pill)', flexShrink: 0,
                background: on ? eHsl(d.e, 0.16) : past ? eHsl(d.e, 0.06) : 'var(--bg-muted)',
                border: `1px solid ${on ? eHsl(d.e, 0.5) : past ? eHsl(d.e, 0.22) : 'var(--border-light)'}`,
                boxShadow: on ? `0 0 8px ${eHsl(d.e, 0.4)}` : 'none',
              }}>
                <span aria-hidden="true" style={{ width: 8, height: 8, borderRadius: '50%', background: eHsl(d.e), opacity: on || past ? 1 : 0.4, flexShrink: 0 }} />
                <span style={{ ...mono(compact ? 8.5 : 9.5, on ? 800 : 600, on ? eHsl(d.e) : 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.04em' }}>{d.lb}</span>
              </span>
            );
          })}
        </div>
      </div>
    );
  };

  // ─── DieFace — faccia D6 a pip · hit (≥soglia) evidenziato ─────────────────
  const PIPS = {
    1: [[1, 1]], 2: [[0, 0], [2, 2]], 3: [[0, 0], [1, 1], [2, 2]],
    4: [[0, 0], [0, 2], [2, 0], [2, 2]], 5: [[0, 0], [0, 2], [1, 1], [2, 0], [2, 2]],
    6: [[0, 0], [0, 2], [1, 0], [1, 2], [2, 0], [2, 2]],
  };
  const DieFace = ({ value, hit, size = 40, rolling }) => {
    const cells = Array.from({ length: 9 });
    const on = PIPS[value] || [];
    const onSet = new Set(on.map(([r, c]) => r * 3 + c));
    return (
      <div role="img" aria-label={`Dado ${value}${hit ? ', colpo' : ', mancato'}`} className={rolling ? 'zom-die-roll' : undefined} style={{
        width: size, height: size, borderRadius: 'var(--r-md)', flexShrink: 0,
        background: hit ? eHsl('toolkit', 0.16) : 'var(--bg-card)',
        border: `2px solid ${hit ? eHsl('toolkit') : 'var(--border-strong)'}`,
        boxShadow: hit ? `0 0 10px ${eHsl('toolkit', 0.4)}` : 'var(--shadow-xs)',
        display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gridTemplateRows: 'repeat(3, 1fr)',
        padding: size * 0.16, gap: size * 0.04,
      }}>
        {cells.map((_, i) => (
          <span key={i} style={{ borderRadius: '50%', alignSelf: 'center', justifySelf: 'center',
            width: size * 0.15, height: size * 0.15,
            background: onSet.has(i) ? (hit ? eHsl('toolkit') : 'var(--text-sec)') : 'transparent' }} />
        ))}
      </div>
    );
  };
  const DiceTray = ({ roll, threshold, size = 40 }) => (
    <div style={{ display: 'flex', gap: 7, flexWrap: 'wrap' }}>
      {roll.map((v, i) => <DieFace key={i} value={v} hit={v >= threshold} size={size} />)}
    </div>
  );

  // ─── WeaponRefRow — riferimento rapido arma (gittata + dadi) ───────────────
  const WeaponRefRow = ({ w }) => {
    const e = Z.eq(w.id);
    return (
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '6px 9px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)' }}>
        <span aria-hidden="true" style={{ fontSize: 13, flexShrink: 0 }}>{e.glyph}</span>
        <span style={{ flex: 1, minWidth: 0, ...disp(11.5, 800, 'var(--text)') }}>{w.lb}</span>
        <span style={{ ...mono(9, 700, 'var(--text-sec)') }}>gittata <strong style={{ color: 'var(--text)' }}>{w.range}</strong></span>
        <span style={{ ...mono(9, 700, eHsl('session')) }}>{w.dice}d · {w.hit}</span>
        {w.note && <span style={{ ...mono(8, 800, eHsl('event')), textTransform: 'uppercase' }}>{w.note}</span>}
      </div>
    );
  };

  // ─── ObjectiveRow — riga obiettivo scenario ───────────────────────────────
  const ObjectiveRow = ({ o }) => (
    <div role="listitem" style={{
      display: 'flex', alignItems: 'center', gap: 9, padding: '8px 10px', borderRadius: 'var(--r-md)',
      background: o.done ? eHsl('toolkit', 0.07) : 'var(--bg-card)',
      border: `1px solid ${o.done ? eHsl('toolkit', 0.28) : 'var(--border)'}`,
    }}>
      <span aria-hidden="true" style={{
        width: 19, height: 19, borderRadius: 'var(--r-sm)', flexShrink: 0,
        background: o.done ? eHsl('toolkit') : 'transparent',
        border: o.done ? 'none' : `2px solid ${eHsl('session', 0.4)}`,
        color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', ...mono(11, 800),
      }}>{o.done ? '✓' : ''}</span>
      <span style={{ flex: 1, minWidth: 0, ...disp(12, 700, 'var(--text)'), textDecoration: o.done ? 'line-through' : 'none', opacity: o.done ? 0.7 : 1 }}>{o.label}</span>
      {o.progress && !o.done && <span style={{ ...mono(9, 800, eHsl('session')), flexShrink: 0 }}>{o.progress}</span>}
    </div>
  );

  // ─── PhaseTimeline — banner 3 fasi del round (aria-current) ────────────────
  const PhaseTimeline = ({ compact, sticky }) => {
    const { phases, phaseState } = Z;
    return (
      <div role="group" aria-label="Fase del round" style={{
        display: 'flex', alignItems: 'center', gap: compact ? 8 : 14, padding: compact ? '8px 12px' : '10px 16px',
        background: 'var(--glass-bg)', backdropFilter: 'blur(14px)', borderBottom: '1px solid var(--border)',
        ...(sticky ? { position: 'sticky', top: 0, zIndex: 10 } : {}), flexShrink: 0, flexWrap: 'wrap', rowGap: 7,
      }}>
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', lineHeight: 1, flexShrink: 0 }}>
          <span style={{ ...disp(compact ? 17 : 21, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>R{phaseState.round}</span>
          <span style={{ ...mono(7.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>Round</span>
        </div>
        <span aria-hidden="true" style={{ width: 1, alignSelf: 'stretch', background: 'var(--border)', flexShrink: 0 }} />
        <ol style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', alignItems: 'center', flexWrap: 'wrap', rowGap: 6, gap: compact ? 5 : 7, flex: 1, minWidth: 0 }}>
          {phases.map((ph, i) => {
            const on = i === phaseState.phaseIndex, past = i < phaseState.phaseIndex;
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
                </div>
                {i < phases.length - 1 && <span aria-hidden="true" style={{ ...mono(10, 800, past ? eHsl('toolkit') : 'var(--text-muted)') }}>{compact ? '·' : '→'}</span>}
              </li>
            );
          })}
        </ol>
        <span style={{ ...mono(8.5, 700, 'var(--text-muted)'), flexShrink: 0, whiteSpace: 'nowrap', textTransform: 'uppercase', letterSpacing: '.05em' }} aria-hidden="true">↻ orario</span>
      </div>
    );
  };

  // ─── MapTilesGrid — snapshot schematico tessere (placeholder) ──────────────
  const MapTilesGrid = ({ tiles, focusId, h }) => {
    const t = tiles || Z.mapTiles;
    return (
      <div>
        <div style={{ position: 'relative', borderRadius: 'var(--r-md)', overflow: 'hidden', background: 'var(--bg-sunken)', border: '1px solid var(--border-light)', padding: 8 }}>
          <div style={{ display: 'grid', gridTemplateColumns: `repeat(${t.cols}, 1fr)`, gap: 6 }}>
            {t.cells.map(cell => {
              const hasFocus = focusId && cell.survivors.includes(focusId);
              return (
                <div key={cell.id} style={{
                  minHeight: h ? h / t.rows - 12 : 92, borderRadius: 'var(--r-sm)', padding: 6, position: 'relative',
                  background: 'repeating-linear-gradient(45deg, var(--bg-card), var(--bg-card) 7px, var(--bg-muted) 7px, var(--bg-muted) 14px)',
                  border: `1px solid ${hasFocus ? eHsl('session', 0.55) : cell.exit ? eHsl('toolkit', 0.4) : 'var(--border)'}`,
                  boxShadow: hasFocus ? `0 0 0 1px ${eHsl('session', 0.4)}` : 'none',
                  display: 'flex', flexDirection: 'column', gap: 4,
                }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                    <span style={{ ...mono(7.5, 800, 'var(--text-muted)') }}>{cell.id}</span>
                    <span style={{ ...disp(9.5, 800, 'var(--text-sec)'), whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{cell.name}</span>
                    {cell.objective && <span title="obiettivo" aria-hidden="true" style={{ ...mono(9, 800, eHsl('toolkit')), marginLeft: 'auto' }}>★</span>}
                    {cell.exit && <span title="estrazione" aria-hidden="true" style={{ ...mono(7.5, 800, eHsl('toolkit')), textTransform: 'uppercase' }}>exit</span>}
                  </div>
                  <div style={{ display: 'flex', gap: 3, flexWrap: 'wrap', alignItems: 'center' }}>
                    {cell.survivors.map(sid => { const s = Z.byId(sid); return s ? <SurvivorAvatar key={sid} s={s} size={18} ring={sid === focusId} /> : null; })}
                    {cell.spawn && <span title="spawn point" style={{ ...mono(7.5, 800, eHsl('event')), padding: '0 4px', borderRadius: 'var(--r-pill)', background: eHsl('event', 0.12), border: `1px solid ${eHsl('event', 0.3)}` }}>spawn</span>}
                  </div>
                  <div style={{ display: 'flex', gap: 3, flexWrap: 'wrap', marginTop: 'auto', alignItems: 'center' }}>
                    {Object.entries(cell.z).map(([type, n]) => (
                      <span key={type} style={{ display: 'inline-flex', alignItems: 'center', gap: 2 }}>
                        <ZombieToken type={type} size={14} />
                        <span style={{ ...mono(8, 800, 'var(--text-sec)'), fontVariantNumeric: 'tabular-nums' }}>{n}</span>
                      </span>
                    ))}
                  </div>
                </div>
              );
            })}
          </div>
          <span style={{ display: 'inline-block', marginTop: 8, ...mono(8, 700, 'var(--text-muted)'), background: 'var(--glass-bg)', padding: '2px 7px', borderRadius: 'var(--r-pill)', border: '1px solid var(--border-light)' }}>layout schematico · mappa reale al rilascio</span>
        </div>
      </div>
    );
  };

  window.ZOMFlavor = {
    mono, disp,
    SurvivorAvatar, WoundPips, LevelChip, XPBar, APCounter,
    SkillTree, SkillNode, EquipCard, EquipSlots,
    ZombieToken, ZombieCounter, DangerLevel, DieFace, DiceTray,
    WeaponRefRow, ObjectiveRow, PhaseTimeline, MapTilesGrid,
  };
})();

/* sp4-toolkit-play-dice.jsx — Dice Builder full power (presets / builder visivo /
   formula parser / result + history). Replaces the old fixed dice grid.
   Loads after sp4-toolkit-play-tools.jsx, before sp4-toolkit-play-ui.jsx. Route: /toolkit/play. */

const { useState, useEffect, useMemo, useRef, useCallback } = React;
const TPD = window.__TP;
const {
  PAD, reducedMotion, mkActor, DICE_TYPES, DICE_PRESETS, DICE_SYNTAX, DICE_HISTORY,
  parseFormula, specToFormula, describeSpec, rollSpec, resultToHistory,
} = TPD;

const hhmm = () => { const d = new Date(); return `${PAD(d.getHours())}:${PAD(d.getMinutes())}`; };

// ─── individual die in the result breakdown ─────────────
const DieResult = ({ r, idx }) => {
  const cls = 'tp-die-result' + (r.kept ? ' kept' : ' dropped') + (r.crit ? ' crit' : '') + (r.fail ? ' fail' : '');
  return (
    <span className={cls} style={{ animationDelay: (idx * 55) + 'ms' }} title={r.rerolledFrom != null ? `re-roll da ${r.rerolledFrom}` : (r.exploded ? 'esploso' : '')}>
      {r.value}
      {r.kept && r.crit && <span className="mk" aria-hidden="true">✨</span>}
      {r.kept && r.fail && <span className="mk" aria-hidden="true">💀</span>}
    </span>
  );
};

// ─── result calc line ───────────────────────────────────
const CalcLine = ({ res }) => {
  if (res.mode === 'cs') {
    return <div className="tp-calc">{res.spec.count} dadi · <b>{res.successes}</b> {res.successes === 1 ? 'tiro' : 'tiri'} <span className="eq">≥ {res.spec.cs}</span></div>;
  }
  const kept = res.rolls.filter(r => r.kept).map(r => r.value);
  return (
    <div className="tp-calc">
      {kept.join(' + ')} <span className="eq">=</span> {res.sum}
      {res.spec.mod ? <React.Fragment> <span className="mod">{res.spec.mod > 0 ? '+' : '−'}{Math.abs(res.spec.mod)}</span> <span className="eq">=</span> {res.total}</React.Fragment> : null}
    </div>
  );
};

// ─── history row ────────────────────────────────────────
const HistRow = ({ h, onApply, onReroll }) => {
  const ac = mkActor(h.actorLabel);
  return (
    <div className="tp-hist-row" onClick={() => onApply(h.formula)} role="listitem"
      tabIndex={0} onKeyDown={e => { if (e.key === 'Enter') onReroll(h.formula); }}>
      <span className="tp-hist-f">{h.formula}</span>
      <span className="tp-hist-res">→ <b>{h.resultText}</b></span>
      {h.detail && <span className="tp-hist-detail">({h.detail})</span>}
      <span className="tp-hist-meta">
        <span className="tp-hist-time">{h.time}</span>
        {ac && (
          <span className="tp-hist-chip" title={ac.name}>
            <span className="av" style={{ background: `hsl(${ac.color},58%,52%)` }} aria-hidden="true">{ac.initials}</span>
            <span className="nm">{ac.name}</span>
          </span>
        )}
        <button type="button" className="tp-hist-reroll" aria-label={`Ri-tira ${h.formula}`}
          onClick={e => { e.stopPropagation(); onReroll(h.formula); }}>🔁</button>
      </span>
    </div>
  );
};

// ─── advanced toggle ────────────────────────────────────
const AdvToggle = ({ on, onToggle, label, icon, value, min, max, onValue, hasNum = true }) => (
  <div className={'tp-adv-toggle' + (on ? ' on' : '')}>
    <button type="button" className="tp-adv-sw" role="switch" aria-checked={on} aria-label={label} onClick={onToggle} />
    <span className="tl"><span aria-hidden="true">{icon}</span>{label}</span>
    {hasNum && (
      <input className="num" type="number" min={min} max={max} value={on ? value : ''} disabled={!on}
        aria-label={`${label} valore`} onChange={e => onValue(Math.max(min, Math.min(max, +e.target.value || min)))} />
    )}
  </div>
);

// ─── unified accordion section (single-open) ────────────
const AccSection = ({ open, onToggle, icon, label, count, badge, children }) => (
  <div className={'tp-acc' + (open ? ' open' : '')}>
    <button type="button" className="tp-acc-head" aria-expanded={open} onClick={onToggle}>
      <span className="ai" aria-hidden="true">{icon}</span>
      <span className="al">{label}</span>
      {count != null && <span className="ac">{count}</span>}
      <span className="grow" />
      {badge}
      <span className="chev" aria-hidden="true">▾</span>
    </button>
    {open && <div className="tp-acc-body">{children}</div>}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── DICE BUILDER ───────────────────────────────────
// ═══════════════════════════════════════════════════════
const DiceBuilder = ({ addLog, actor, dice = {} }) => {
  const init = dice.init || {};
  const [sides, setSides] = useState(init.sides || 6);
  const [count, setCount] = useState(init.count || 1);
  const [mod, setMod] = useState(init.mod || 0);
  const [kh, setKh] = useState(init.kh ?? null);
  const [kl, setKl] = useState(init.kl ?? null);
  const [cs, setCs] = useState(init.cs ?? null);
  const [reroll, setReroll] = useState(init.reroll ?? null);
  const [exploding, setExploding] = useState(!!init.exploding);
  const initPanel = dice.fmOpen ? 'formula' : dice.advOpen ? 'adv' : dice.showHistory ? 'history' : dice.showSyntax ? 'syntax' : null;
  const [panel, setPanel] = useState(initPanel);
  const togglePanel = id => setPanel(p => (p === id ? null : id));
  const [fmText, setFmText] = useState(dice.fmText || '');
  const [history, setHistory] = useState(dice.history !== undefined ? dice.history : DICE_HISTORY.map(h => ({ ...h })));
  const [result, setResult] = useState(null);
  const [phase, setPhase] = useState('idle');     // idle | rolling | result
  const [pendingN, setPendingN] = useState(0);
  const [pop, setPop] = useState(false);
  const [pulseChip, setPulseChip] = useState(dice.pulse || null);
  const [histAll, setHistAll] = useState(false);
  const idRef = useRef(0);
  const rollT = useRef(null), popT = useRef(null), pulseT = useRef(null);
  const actorRef = useRef(actor);
  useEffect(() => { actorRef.current = actor; }, [actor]);

  const builderSpec = { count, sides, mod, kh, kl, cs, reroll, exploding };
  const builderValid = !(kh != null && kl != null) && (kh == null || kh <= count) && (kl == null || kl <= count);
  const builderFormula = specToFormula(builderSpec);
  const fmParse = useMemo(() => (fmText ? parseFormula(fmText) : null), [fmText]);

  const applySpec = sp => {
    setSides(sp.sides); setCount(sp.count); setMod(sp.mod || 0);
    setKh(sp.kh ?? null); setKl(sp.kl ?? null); setCs(sp.cs ?? null);
    setReroll(sp.reroll ?? null); setExploding(!!sp.exploding); setPanel(null);
  };

  const doRoll = useCallback((spec, animate) => {
    if (!spec) return;
    const exec = () => {
      const res = rollSpec(spec);
      setResult(res); setPhase('result'); setPop(true);
      clearTimeout(popT.current); popT.current = setTimeout(() => setPop(false), 470);
      idRef.current += 1;
      const h = { ...resultToHistory(res, 'dh-r' + idRef.current), time: hhmm(), actorLabel: actorRef.current || null };
      setHistory(prev => [h, ...prev].slice(0, 12));
      addLog({ toolType: 'dice', action: 'roll', result: `${h.formula} → ${h.resultText}` });
    };
    if (!animate || reducedMotion()) { setPhase('result'); exec(); return; }
    setPendingN(spec.count); setPhase('rolling');
    clearTimeout(rollT.current);
    rollT.current = setTimeout(exec, 620);
  }, [addLog]);

  // mount: auto-roll / seed / pulse per scenario
  useEffect(() => {
    const sp = panel === 'formula' ? (fmParse && fmParse.ok ? fmParse.spec : null) : (builderValid ? builderSpec : null);
    if (dice.seedResult) doRoll(sp, false);
    else if (dice.autoRoll) doRoll(sp, true);
    if (dice.pulse) { pulseT.current = setTimeout(() => setPulseChip(null), 280); }
    return () => { clearTimeout(rollT.current); clearTimeout(popT.current); clearTimeout(pulseT.current); };
  }, []); // eslint-disable-line

  const applyFormula = f => { const p = parseFormula(f); if (p.ok) applySpec(p.spec); };
  const rerollFormula = f => { const p = parseFormula(f); if (p.ok) { applySpec(p.spec); doRoll(p.spec, true); } };
  const onPreset = p => { const r = parseFormula(p); if (r.ok) { applySpec(r.spec); setPulseChip(p); clearTimeout(pulseT.current); pulseT.current = setTimeout(() => setPulseChip(null), 280); } };

  // advanced toggle handlers
  const toggleKh = () => { if (kh != null) setKh(null); else { setKh(Math.min(count, 3)); setKl(null); } };
  const toggleKl = () => { if (kl != null) setKl(null); else { setKl(Math.min(count, 1)); setKh(null); } };
  const toggleCs = () => setCs(cs != null ? null : Math.min(sides, sides >= 6 ? 6 : sides));
  const toggleRr = () => setReroll(reroll != null ? null : 1);

  const histShown = histAll ? history : history.slice(0, 5);
  const total = result ? (result.mode === 'cs' ? result.successes : result.total) : 0;

  return (
    <div className="tp-db" role="group" aria-label="Dice builder">
      {/* ── 1.A presets ── */}
      <div className="tp-db-presets">
        <span className="lbl">Preset:</span>
        {DICE_PRESETS.map(p => (
          <button key={p} type="button" className={'tp-preset' + (pulseChip === p ? ' pulse' : '')} onClick={() => onPreset(p)}>{p}</button>
        ))}
        <button type="button" className={'tp-preset custom' + (panel === 'adv' ? ' on' : '')} onClick={() => togglePanel('adv')}>+ Avanzate</button>
      </div>

      {/* ── 1.B builder visivo ── */}
      <div className="tp-db-grid">
        <div className="tp-db-col">
          <span className="lbl">Tipo dado</span>
          <div className="tp-db-typechips" role="radiogroup" aria-label="Tipo di dado">
            {DICE_TYPES.map(s => (
              <button key={s} type="button" role="radio" aria-checked={sides === s}
                className={'tp-typechip' + (sides === s ? ' on' : '')} onClick={() => setSides(s)}>D{s}</button>
            ))}
          </div>
        </div>
        <div className="tp-db-col">
          <span className="lbl">Quantità</span>
          <div className="tp-stepper" role="spinbutton" aria-label="Quantità dadi (1-20)" aria-valuenow={count} aria-valuemin={1} aria-valuemax={20}>
            <button type="button" className="tp-step-btn g" disabled={count <= 1} onClick={() => setCount(c => Math.max(1, c - 1))} aria-label="Diminuisci quantità">−</button>
            <span className="tp-step-val">{count}</span>
            <button type="button" className="tp-step-btn g" disabled={count >= 20} onClick={() => setCount(c => Math.min(20, c + 1))} aria-label="Aumenta quantità">+</button>
          </div>
          <span className="hint">min 1 · max 20</span>
        </div>
        <div className="tp-db-col">
          <span className="lbl">Modificatore</span>
          <div className="tp-stepper" role="spinbutton" aria-label="Modificatore (-10 / +20)" aria-valuenow={mod} aria-valuemin={-10} aria-valuemax={20}>
            <button type="button" className="tp-step-btn t" disabled={mod <= -10} onClick={() => setMod(m => Math.max(-10, m - 1))} aria-label="Diminuisci modificatore">−</button>
            <span className={'tp-step-val' + (mod === 0 ? ' muted' : '')}>{mod > 0 ? '+' : ''}{mod}</span>
            <button type="button" className="tp-step-btn t" disabled={mod >= 20} onClick={() => setMod(m => Math.min(20, m + 1))} aria-label="Aumenta modificatore">+</button>
          </div>
          <span className="hint">−10 / +20</span>
        </div>
      </div>

      {/* builder CTA */}
      <button type="button" className="tp-db-cta" disabled={!builderValid} onClick={() => doRoll(builderValid ? builderSpec : null, true)}
        aria-label={builderValid ? `Tira ${describeSpec(builderSpec)}` : 'Formula non valida'}>
        <span aria-hidden="true">🎲</span>{builderValid ? `Tira ${builderFormula}` : 'Configurazione non valida'}
      </button>

      {/* ── 1.D result ── */}
      {phase === 'rolling' ? (
        <div className="tp-db-result" aria-live="polite" aria-label="Tiro in corso">
          <div className="tp-result-top"><span className="f">{builderFormula}</span><span className="t">· tirando…</span></div>
          <div className="tp-dice-row">
            {Array.from({ length: Math.min(pendingN, 20) }).map((_, i) => <span key={i} className="tp-die-result rolling" style={{ animationDelay: (i * 60) + 'ms' }}>?</span>)}
          </div>
        </div>
      ) : result ? (
        <div className="tp-db-result" role="status" aria-live="polite" aria-label={`Risultato ultimo tiro: ${total}`}>
          <div className="tp-result-top"><span className="f">{specToFormula(result.spec)}</span><span className="t">· adesso</span></div>
          <div className="tp-dice-row">{result.rolls.map((r, i) => <DieResult key={i} r={r} idx={i} />)}</div>
          <CalcLine res={result} />
          <div className="tp-total-row">
            <span className="tl">Totale</span>
            <span className={'tp-total' + (pop ? ' pop' : '')}>{total}{result.mode === 'cs' && <span className="suf">success{result.successes === 1 ? 'o' : 'i'}</span>}</span>
          </div>
        </div>
      ) : (
        <div className="tp-db-result empty">Configura e tira — il risultato apparirà qui</div>
      )}

      {/* ── secondary controls: single-open accordion ── */}
      <div className="tp-db-acc">
        <AccSection open={panel === 'adv'} onToggle={() => togglePanel('adv')} icon="⚙" label="Modificatori avanzati">
          <div className="tp-adv-grid">
            <AdvToggle on={kh != null} onToggle={toggleKh} label="Keep highest" icon="🔼" value={kh || 1} min={1} max={count} onValue={setKh} />
            <AdvToggle on={kl != null} onToggle={toggleKl} label="Keep lowest" icon="🔽" value={kl || 1} min={1} max={count} onValue={setKl} />
            <AdvToggle on={cs != null} onToggle={toggleCs} label="Successi ≥" icon="🎯" value={cs || sides} min={1} max={sides} onValue={setCs} />
            <AdvToggle on={reroll != null} onToggle={toggleRr} label="Re-roll ≤" icon="🔄" value={reroll || 1} min={1} max={sides - 1} onValue={setReroll} />
            <AdvToggle on={exploding} onToggle={() => setExploding(v => !v)} label="Esplosivi (max face)" icon="⚠️" hasNum={false} />
          </div>
        </AccSection>

        <AccSection open={panel === 'formula'} onToggle={() => togglePanel('formula')} icon="🧙" label="Modalità avanzata (formula)">
          <div className="tp-db-formula">
            <div className="tp-formula-row">
              <span className="fl">Formula</span>
              <input className={'tp-formula-input' + (fmParse && !fmParse.ok ? ' bad' : '')} value={fmText}
                role="textbox" aria-label="Formula dadi (sintassi avanzata)" aria-invalid={fmParse ? !fmParse.ok : false} aria-describedby="tp-parse-fb"
                placeholder="es. 2D6+3, 4D6kh3, 6D6cs6, 3D10!" onChange={e => setFmText(e.target.value)}
                onKeyDown={e => { if ((e.ctrlKey || e.metaKey) && e.key === 'Enter' && fmParse && fmParse.ok) doRoll(fmParse.spec, true); }} />
            </div>
            {fmParse && (
              <div id="tp-parse-fb" className={'tp-parse ' + (fmParse.ok ? 'ok' : 'bad')} aria-live="polite">
                <span className="ic" aria-hidden="true">{fmParse.ok ? '✓' : '✗'}</span>
                {fmParse.ok ? <span>Valido: {describeSpec(fmParse.spec)}</span> : <span>Errore: {fmParse.error}</span>}
              </div>
            )}
            <button type="button" className="tp-db-cta" disabled={!fmParse || !fmParse.ok} onClick={() => fmParse && fmParse.ok && doRoll(fmParse.spec, true)}>
              <span aria-hidden="true">🎲</span>Tira formula
            </button>
          </div>
        </AccSection>

        <AccSection open={panel === 'syntax'} onToggle={() => togglePanel('syntax')} icon="❓" label="Sintassi formula">
          <div className="tp-syntax" role="note" aria-label="Sintassi formula">
            {DICE_SYNTAX.map((row, i) => (
              <div className="tp-syntax-row" key={i}>
                <code>{row[0]}</code><span className="d">{row[1]}</span><span className="ex">{row[2]}</span>
              </div>
            ))}
          </div>
        </AccSection>

        <AccSection open={panel === 'history'} onToggle={() => togglePanel('history')} icon="📜" label="Storico recente" count={history.length}
          badge={history.length > 0 ? <span role="button" tabIndex={0} className="tp-hist-clear" aria-label="Pulisci storico"
            onClick={e => { e.stopPropagation(); setHistory([]); setHistAll(false); }}
            onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') { e.stopPropagation(); setHistory([]); setHistAll(false); } }}>🗑</span> : null}>
          {history.length === 0
            ? <div className="tp-hist-empty">Nessun tiro ancora — tira per popolare lo storico.</div>
            : <div role="log" aria-relevant="additions" aria-label="Storico tiri">
                {histShown.map(h => <HistRow key={h.id} h={h} onApply={applyFormula} onReroll={rerollFormula} />)}
                {history.length > 5 && !histAll && <button type="button" className="tp-hist-more" onClick={() => setHistAll(true)}>Mostra altri {history.length - 5}</button>}
              </div>}
        </AccSection>
      </div>
    </div>
  );
};

Object.assign(window, { DiceBuilder });

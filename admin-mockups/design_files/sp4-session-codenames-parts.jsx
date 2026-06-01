/* MeepleAI SP4 · Codenames — COMPOSED PARTS  (window.CNParts)
   Game-specific widgets mounted on the universal skeleton:
     SectionCard · PanelFrame · PhaseBanner · CurrentCluePanel ·
     WordCard · WordGrid · SpymasterKeyCardOverlay · TeamPanel ·
     ClueHistoryTimeline · RightColumnTabs · DesktopBody · MobileBody · PhoneShell

   Reuses from skeleton: S.TopBar · S.ChatAgentPanel · S.ActionLogTimeline ·
   S.StateSwitch · R.ScoringPanelRenderer (Ranking) · R.TurnIndicatorRenderer
   (Sequential) · R.StateScaffold · M.Shimmer/StateBlock.
   Accordion: one panel expanded at a time via sec(id), exactly like the
   skeleton DesktopSessionBody + Power Grid extension. */

(function () {
  const M = window.MAI;
  const R = window.SkeletonRenderers;
  const S = window.SkeletonParts;
  const CN = window.CN;
  const F = window.CNFlavor;
  const eHsl = M.entityHsl;
  const { useState } = React;
  const { mono, disp, teamEntity, RoleAvatar, RoleTag, ClueChip, GuessPips, KeyCell, TimerChip, ClueOutcome } = F;
  const STATES = window.SkeletonData.STATES;

  // ─── SectionCard — titled collapsible container (skeleton/PG pattern) ─────
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

  const PanelFrame = ({ label, entity = 'session', dark, children, w = 320, h = 480 }) => (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }} data-theme={dark ? 'dark' : undefined}>
      <div style={{ ...mono(10, 800, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>{label}</div>
      <div style={{ width: w, height: h, borderRadius: 'var(--r-lg)', border: `1px solid ${eHsl(entity, 0.3)}`, background: 'var(--bg-card)', overflow: 'hidden', display: 'flex', flexDirection: 'column', boxShadow: 'var(--shadow-sm)' }}>{children}</div>
    </div>
  );

  // ══════════════════════════════════════════════════════════════════════════
  // PhaseBanner — 4-phase team-alternating timeline (aria-current step)
  // ══════════════════════════════════════════════════════════════════════════
  const PhaseBanner = ({ compact, sticky, phaseState = CN.phaseState }) => {
    const { PHASES } = CN;
    return (
      <div role="group" aria-label="Fase del turno" style={{
        display: 'flex', alignItems: 'center', gap: compact ? 8 : 13, padding: compact ? '8px 12px' : '10px 16px',
        background: 'var(--glass-bg)', backdropFilter: 'blur(14px)', borderBottom: '1px solid var(--border)',
        ...(sticky ? { position: 'sticky', top: 0, zIndex: 10 } : {}), flexShrink: 0, flexWrap: 'wrap', rowGap: 7,
      }}>
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', lineHeight: 1, flexShrink: 0 }}>
          <span style={{ ...disp(compact ? 16 : 20, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>R{phaseState.round}</span>
          <span style={{ ...mono(7.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>Round</span>
        </div>
        <span aria-hidden="true" style={{ width: 1, alignSelf: 'stretch', background: 'var(--border)', flexShrink: 0 }} />
        <ol style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', alignItems: 'center', flexWrap: 'wrap', rowGap: 6, gap: compact ? 4 : 6, flex: 1, minWidth: 0 }}>
          {PHASES.map((ph, i) => {
            const on = i === phaseState.phaseIndex, past = i < phaseState.phaseIndex;
            const e = teamEntity(ph.team);
            return (
              <li key={ph.id} aria-current={on ? 'step' : undefined} style={{ display: 'flex', alignItems: 'center', gap: compact ? 4 : 6, flexShrink: 0 }}>
                <div title={ph.desc} style={{
                  display: 'flex', alignItems: 'center', gap: 6, padding: compact ? '5px 8px' : '6px 11px', borderRadius: 'var(--r-pill)',
                  background: on ? eHsl(e, 0.16) : past ? eHsl(e, 0.06) : 'var(--bg-muted)',
                  border: `1px solid ${on ? eHsl(e, 0.5) : past ? eHsl(e, 0.2) : 'var(--border-light)'}`,
                  boxShadow: on ? `0 3px 10px ${eHsl(e, 0.22)}` : 'none',
                }}>
                  <span aria-hidden="true" style={{
                    width: compact ? 17 : 20, height: compact ? 17 : 20, borderRadius: '50%', flexShrink: 0,
                    background: on ? eHsl(e) : past ? eHsl(e, 0.2) : 'transparent',
                    color: on ? '#fff' : past ? eHsl(e) : 'var(--text-muted)',
                    border: on || past ? 'none' : '1px solid var(--border-strong)',
                    display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontSize: compact ? 9 : 10.5,
                  }}>{past ? '✓' : ph.kind === 'clue' ? '🕶' : '🔎'}</span>
                  {!compact && <span style={{ ...disp(11.5, on ? 800 : 600, on ? eHsl(e) : past ? 'var(--text-sec)' : 'var(--text-muted)'), whiteSpace: 'nowrap' }}>{ph.short}</span>}
                  {on && <span className="mai-pulse-dot" aria-hidden="true" style={{ ...mono(8, 800, eHsl(e)) }}>●</span>}
                </div>
                {i < PHASES.length - 1 && <span aria-hidden="true" style={{ ...mono(10, 800, past ? eHsl(e) : 'var(--text-muted)') }}>{compact ? '·' : '→'}</span>}
              </li>
            );
          })}
        </ol>
      </div>
    );
  };

  // ══════════════════════════════════════════════════════════════════════════
  // CurrentCluePanel — prominent active-clue area (clue · pips · timer)
  // ══════════════════════════════════════════════════════════════════════════
  const CurrentCluePanel = ({ compact, clue = CN.currentClue }) => {
    const c = clue;
    const e = teamEntity(c.team);
    const spy = CN.byId(c.spymaster);
    return (
      <div role="region" aria-label="Indizio attuale" aria-live="polite" style={{
        display: 'flex', alignItems: 'center', gap: compact ? 12 : 18, flexWrap: 'wrap', rowGap: 10,
        padding: compact ? '11px 13px' : '13px 16px', borderRadius: 'var(--r-lg)',
        background: 'var(--bg-card)', border: `2px solid ${eHsl(e, 0.45)}`, boxShadow: `0 6px 22px ${eHsl(e, 0.16)}`, flexShrink: 0,
      }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 7, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
            <span className="mai-pulse-dot" aria-hidden="true" style={{ fontSize: 9, color: eHsl(e) }}>●</span>
            <span style={{ ...mono(9, 800, eHsl(e)), textTransform: 'uppercase', letterSpacing: '.07em' }}>Indizio · {F.teamName(c.team)} indovinano</span>
          </div>
          <ClueChip word={c.word} number={c.number} team={c.team} big={!compact} />
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <RoleAvatar p={spy} size={20} />
            <span style={{ ...mono(9.5, 700, 'var(--text-muted)') }}>dato da <strong style={{ color: 'var(--text-sec)' }}>{spy.name}</strong> · Spymaster</span>
          </div>
        </div>
        <span aria-hidden="true" style={{ width: 1, alignSelf: 'stretch', background: 'var(--border)', flexShrink: 0 }} />
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8, flex: 1, minWidth: 140 }}>
          <div>
            <div style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em', marginBottom: 5 }}>Tentativi</div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 9, flexWrap: 'wrap' }}>
              <GuessPips made={c.guessesMade} number={c.number} team={c.team} />
              <span style={{ ...mono(10, 700, 'var(--text-sec)') }}>
                <strong style={{ color: eHsl(e), fontSize: 13 }}>{c.guessesMade}</strong> indovinati · ancora <strong style={{ color: 'var(--text)' }}>{c.remaining}</strong> <span style={{ color: 'var(--text-muted)' }}>(incl. +1 bonus)</span>
              </span>
            </div>
          </div>
          <div style={{ display: 'flex', gap: 7, flexWrap: 'wrap' }}>
            <TimerChip timer={CN.timers.guess} compact={compact} />
            <div style={{ display: 'flex', gap: 6, marginLeft: 'auto' }}>
              <button type="button" style={{ padding: '5px 11px', borderRadius: 'var(--r-md)', background: eHsl(e), color: '#fff', border: 'none', ...disp(11.5, 800, '#fff'), cursor: 'pointer', whiteSpace: 'nowrap' }}>Indovina</button>
              <button type="button" style={{ padding: '5px 11px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border)', ...disp(11.5, 800, 'var(--text-sec)'), cursor: 'pointer', whiteSpace: 'nowrap' }}>Passa</button>
            </div>
          </div>
        </div>
      </div>
    );
  };

  // ══════════════════════════════════════════════════════════════════════════
  // WordCard — one tile · states: covered / red / blue / neutral / assassin
  // ══════════════════════════════════════════════════════════════════════════
  const WordCard = ({ cell, revealKey, compact }) => {
    const k = cell.key;
    const h = compact ? 46 : 64;
    const baseFs = compact ? 9.5 : 13;
    let bg = 'var(--bg-card)', fg = 'var(--text)', border = '1px solid var(--border)', stripe = null, badge = null, opacity = 1, txtDeco = 'none';

    if (cell.revealed) {
      if (k === 'red')      { bg = eHsl('event'); fg = '#fff'; border = `1px solid ${eHsl('event')}`; badge = '✓'; }
      else if (k === 'blue'){ bg = eHsl('chat');  fg = '#fff'; border = `1px solid ${eHsl('chat')}`;  badge = '✓'; }
      else if (k === 'neutral') { bg = 'var(--bg-sunken)'; fg = 'var(--text-muted)'; border = '1px solid var(--border-strong)'; opacity = 0.92; txtDeco = 'line-through'; }
      else /* assassin */   { bg = '#0f0c08'; fg = '#fff'; border = `1.5px solid ${eHsl('event', 0.7)}`; badge = '💀'; }
    } else if (revealKey) {
      // Spymaster key view: tint covered tiles by their real identity
      if (k === 'red')       { bg = eHsl('event', 0.14); border = `1px solid ${eHsl('event', 0.5)}`; stripe = eHsl('event'); }
      else if (k === 'blue') { bg = eHsl('chat', 0.14);  border = `1px solid ${eHsl('chat', 0.5)}`;  stripe = eHsl('chat'); }
      else if (k === 'neutral') { bg = 'var(--bg-muted)'; border = '1px solid var(--border-strong)'; stripe = 'var(--text-muted)'; }
      else /* assassin */    { bg = 'repeating-linear-gradient(45deg, #0f0c08, #0f0c08 6px, #241c13 6px, #241c13 12px)'; fg = '#fff'; border = `1.5px solid ${eHsl('event', 0.7)}`; stripe = eHsl('event'); badge = '💀'; }
    }

    const status = cell.revealed ? (F.KEY[k].label + ' · rivelato') : revealKey ? (F.KEY[k].label + ' · coperto') : 'coperta';
    return (
      <button type="button" aria-label={`Parola ${cell.word}, ${status}`} aria-pressed={cell.revealed || undefined}
        disabled={cell.revealed}
        style={{
          position: 'relative', height: h, borderRadius: 'var(--r-md)', background: bg, border, color: fg, opacity,
          display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '4px 6px', overflow: 'hidden',
          cursor: cell.revealed ? 'default' : 'pointer', textAlign: 'center', boxShadow: cell.revealed ? 'none' : 'var(--shadow-xs)',
          transition: 'transform var(--dur-xs) var(--ease-out), box-shadow var(--dur-xs) var(--ease-out)',
        }}>
        {stripe && <span aria-hidden="true" style={{ position: 'absolute', top: 0, left: 0, right: 0, height: 4, background: stripe }} />}
        <span style={{ ...disp(baseFs, 800, fg), letterSpacing: '.02em', textTransform: 'uppercase', textDecoration: txtDeco, lineHeight: 1.1, wordBreak: 'break-word' }}>{cell.word}</span>
        {badge && <span aria-hidden="true" style={{ position: 'absolute', bottom: 3, right: 5, fontSize: compact ? 9 : 11, opacity: 0.9 }}>{badge}</span>}
      </button>
    );
  };

  // ─── WordGrid — 5×5, perspective-aware, with board toolbar + states ───────
  const WordGridInner = ({ perspective, keyVisible, compact, board = CN.BOARD }) => {
    const reveal = perspective === 'spymaster' && keyVisible;
    return (
      <div role="grid" aria-label="Griglia 5×5 delle parole" style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: compact ? 5 : 7 }}>
        {board.map((cell, i) => <WordCard key={i} cell={cell} revealKey={reveal} compact={compact} />)}
      </div>
    );
  };
  const WordGrid = ({ state = 'default', compact, perspective = 'operative', keyVisible = true, onTogglePerspective, onToggleKey, board = CN.BOARD }) => {
    const spy = perspective === 'spymaster';
    return (
      <SectionCard title="Griglia parole" entity="session" accent flush
        right={
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap', justifyContent: 'flex-end' }}>
            <div role="group" aria-label="Prospettiva" style={{ display: 'inline-flex', borderRadius: 'var(--r-pill)', background: 'var(--bg-card)', border: '1px solid var(--border)', overflow: 'hidden' }}>
              {[['operative', '🔎 Operativo'], ['spymaster', '🕶 Spymaster']].map(([id, lb]) => {
                const on = perspective === id;
                const e = id === 'spymaster' ? 'session' : 'player';
                return (
                  <button key={id} type="button" aria-pressed={on} onClick={() => onTogglePerspective && onTogglePerspective(id)}
                    style={{ padding: '3px 9px', border: 'none', background: on ? eHsl(e, 0.16) : 'transparent', color: on ? eHsl(e) : 'var(--text-muted)', ...mono(9, 800), cursor: 'pointer', whiteSpace: 'nowrap' }}>{lb}</button>
                );
              })}
            </div>
            {spy && (
              <button type="button" aria-pressed={keyVisible} onClick={() => onToggleKey && onToggleKey()} title="Mostra/nascondi key card (privacy screen-share)"
                style={{ padding: '3px 9px', borderRadius: 'var(--r-pill)', background: keyVisible ? eHsl('session', 0.14) : 'var(--bg-muted)', border: `1px solid ${keyVisible ? eHsl('session', 0.4) : 'var(--border)'}`, color: keyVisible ? eHsl('session') : 'var(--text-muted)', ...mono(9, 800), cursor: 'pointer', whiteSpace: 'nowrap' }}>
                {keyVisible ? '👁 Key visibile' : '🚫 Key nascosta'}
              </button>
            )}
          </div>
        }>
        <R.StateScaffold state={state} sseWhere="griglia"
          empty={{ icon: '🗂', title: 'Griglia non pronta', body: 'Le 25 parole compaiono all\u2019avvio della partita.' }}
          error={{ title: 'Griglia non disponibile', body: 'Impossibile caricare le carte parola.' }}
          loading={<div style={{ padding: 12, display: 'grid', gridTemplateColumns: 'repeat(5,1fr)', gap: compact ? 5 : 7 }}>{Array.from({ length: 25 }).map((_, i) => <M.Shimmer key={i} h={compact ? 46 : 64} />)}</div>}>
          <div style={{ padding: compact ? 9 : 12 }}>
            <WordGridInner perspective={perspective} keyVisible={keyVisible} compact={compact} board={board} />
            {spy && keyVisible && <SpymasterKeyCardOverlay onToggleKey={onToggleKey} board={board} embedded />}
          </div>
        </R.StateScaffold>
      </SectionCard>
    );
  };

  // ─── SpymasterKeyCardOverlay — secret 5×5 key (toggleable) ────────────────
  const SpymasterKeyCardOverlay = ({ onToggleKey, embedded, board = CN.BOARD }) => {
    const counts = { red: 0, blue: 0, neutral: 0, assassin: 0 };
    board.forEach((c) => { counts[c.key]++; });
    return (
      <div role="region" aria-label="Key card Spymaster" style={{ marginTop: 11, padding: 11, borderRadius: 'var(--r-md)', background: 'var(--bg-sunken)', border: `1px dashed ${eHsl('session', 0.4)}` }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 9, flexWrap: 'wrap', rowGap: 6 }}>
          <span style={{ ...mono(9, 800, eHsl('session')), textTransform: 'uppercase', letterSpacing: '.06em' }}>🔑 Key card · segreta</span>
          <div style={{ display: 'flex', gap: 5, flexWrap: 'wrap' }}>
            {[['red', 'event'], ['blue', 'chat'], ['neutral', null], ['assassin', null]].map(([k, e]) => (
              <span key={k} style={{ display: 'inline-flex', alignItems: 'center', gap: 4, ...mono(8.5, 700, 'var(--text-sec)') }}>
                <span style={{ width: 10, height: 10, borderRadius: 2, background: e ? eHsl(e) : k === 'assassin' ? '#0f0c08' : 'var(--bg-muted)', border: '1px solid var(--border-strong)' }} />{counts[k]}
              </span>
            ))}
          </div>
          {onToggleKey && <button type="button" aria-pressed="true" onClick={onToggleKey} style={{ marginLeft: 'auto', padding: '2px 9px', borderRadius: 'var(--r-pill)', background: 'var(--bg-muted)', border: '1px solid var(--border)', ...mono(8.5, 800, 'var(--text-muted)'), cursor: 'pointer' }}>🚫 Nascondi</button>}
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 4, maxWidth: embedded ? 260 : '100%' }}>
          {board.map((c, i) => <KeyCell key={i} keyType={c.key} revealed={c.revealed} />)}
        </div>
        <div style={{ ...mono(8.5, 700, 'var(--text-muted)'), marginTop: 8, lineHeight: 1.5 }}>Solo per lo Spymaster · nascondi durante screen-share. I quadretti sbiaditi sono già rivelati.</div>
      </div>
    );
  };

  // ─── GameOverBanner — end-of-game result (e.g. assassin defeat) ───────────
  const GameOverBanner = ({ over, compact }) => {
    const winnerE = teamEntity(over.winner);
    return (
      <div role="alert" style={{
        position: 'relative', overflow: 'hidden', borderRadius: 'var(--r-lg)', flexShrink: 0,
        padding: compact ? '13px 14px' : '16px 18px',
        background: 'repeating-linear-gradient(45deg, #0f0c08, #0f0c08 10px, #1a140d 10px, #1a140d 20px)',
        border: `2px solid ${eHsl('event', 0.6)}`, boxShadow: `0 8px 28px ${eHsl('event', 0.25)}`, color: '#fff',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: compact ? 12 : 16, flexWrap: 'wrap', rowGap: 8 }}>
          <span aria-hidden="true" style={{ fontSize: compact ? 32 : 42, lineHeight: 1 }}>{over.icon}</span>
          <div style={{ minWidth: 0, flex: 1 }}>
            <div style={{ ...mono(9, 800, eHsl('event')), textTransform: 'uppercase', letterSpacing: '.1em', marginBottom: 3 }}>Partita conclusa</div>
            <div style={{ ...disp(compact ? 18 : 22, 800, '#fff') }}>{over.title}</div>
            <div style={{ ...disp(compact ? 12 : 13.5, 600, 'rgba(255,255,255,.9)'), marginTop: 3 }}>{over.cause}</div>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 7, padding: '6px 12px', borderRadius: 'var(--r-pill)', background: eHsl(winnerE, 0.22), border: `1px solid ${eHsl(winnerE, 0.55)}` }}>
              <span aria-hidden="true">🏆</span><span style={{ ...disp(12.5, 800, '#fff') }}>Vince {F.teamName(over.winner)}</span>
            </span>
            <a href="sp4-session-codenames-summary.html" style={{ padding: '7px 13px', borderRadius: 'var(--r-md)', background: '#fff', color: '#1a140d', ...disp(12, 800), whiteSpace: 'nowrap' }}>Vai al recap →</a>
          </div>
        </div>
      </div>
    );
  };

  // ─── ScenarioSwitcher — switch the depicted game state ────────────────────
  const ScenarioSwitcher = ({ value, onChange, compact }) => (
    <div role="group" aria-label="Scenario partita" style={{
      display: 'flex', alignItems: 'center', gap: 8, padding: compact ? '6px 12px' : '7px 16px',
      background: 'var(--bg-muted)', borderBottom: '1px solid var(--border)', flexShrink: 0, flexWrap: 'wrap', rowGap: 6,
    }}>
      <span style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', flexShrink: 0 }}>Scenario</span>
      <div style={{ display: 'inline-flex', borderRadius: 'var(--r-pill)', background: 'var(--bg-card)', border: '1px solid var(--border)', overflow: 'hidden' }}>
        {CN.SCN_ORDER.map((id) => {
          const sc = CN.SCENARIOS[id];
          const on = value === id;
          const e = sc.tone;
          return (
            <button key={id} type="button" aria-pressed={on} onClick={() => onChange && onChange(id)}
              title={sc.sub}
              style={{ padding: compact ? '4px 9px' : '5px 12px', border: 'none', background: on ? eHsl(e, 0.16) : 'transparent', color: on ? eHsl(e) : 'var(--text-muted)', ...mono(compact ? 8.5 : 9.5, 800), cursor: 'pointer', whiteSpace: 'nowrap', borderRight: id !== 'assassin' ? '1px solid var(--border-light)' : 'none' }}>
              {sc.label}
            </button>
          );
        })}
      </div>
      {!compact && <span style={{ ...mono(9, 700, 'var(--text-muted)') }}>{CN.SCENARIOS[value].sub}</span>}
    </div>
  );

  window.CNParts = { SectionCard, PanelFrame, PhaseBanner, CurrentCluePanel, WordCard, WordGrid, WordGridInner, SpymasterKeyCardOverlay, GameOverBanner, ScenarioSwitcher };
})();

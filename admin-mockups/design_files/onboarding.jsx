// MeepleAI — Onboarding Flow
// React 18 + Babel (loaded from onboarding.html)
// Steps: 0 Welcome | 1 Games | 2 Agents | 3 Session | 4 Complete

const { useState, useEffect, useRef } = React;

// ─── Data ────────────────────────────────────────────────────────────────────

const GAMES = [
  { id: 'catan',        title: 'Catan',            year: 1995, players: '3–4', em: '🌾', g: ['25 88% 50%', '15 80% 38%'] },
  { id: 'carcassonne',  title: 'Carcassonne',       year: 2000, players: '2–5', em: '🏰', g: ['142 52% 38%', '158 50% 28%'] },
  { id: 'ticket',       title: 'Ticket to Ride',    year: 2004, players: '2–5', em: '🚂', g: ['220 68% 50%', '235 62% 40%'] },
  { id: 'wingspan',     title: 'Wingspan',           year: 2019, players: '1–5', em: '🦜', g: ['262 65% 55%', '278 60% 42%'] },
  { id: '7wonders',     title: '7 Wonders',          year: 2010, players: '2–7', em: '🏛️', g: ['38 85% 50%', '22 78% 40%'] },
  { id: 'terraforming', title: 'Terraforming Mars',  year: 2016, players: '1–5', em: '🚀', g: ['350 72% 52%', '8  65% 40%'] },
  { id: 'azul',         title: 'Azul',               year: 2017, players: '2–4', em: '🔷', g: ['195 78% 48%', '210 68% 38%'] },
  { id: 'splendor',     title: 'Splendor',            year: 2014, players: '2–4', em: '💎', g: ['174 62% 38%', '188 56% 28%'] },
];

const AGENTS = [
  { id: 'rules',    em: '🎲', name: 'Agente Regole',    desc: 'Risposte precise con citazione pagina PDF', defaultOn: true },
  { id: 'strategy', em: '🎯', name: 'Agente Strategia', desc: 'Consigli tattici durante la partita',       defaultOn: true },
  { id: 'setup',    em: '🔧', name: 'Agente Setup',     desc: 'Ti guida nel preparare il tavolo',          defaultOn: true },
  { id: 'narrator', em: '📚', name: 'Agente Cronista',  desc: 'Narra la partita in tempo reale',           defaultOn: false },
];

const ACTIONS = [
  { id: 'event',   em: '🎉', title: 'Crea la prima serata', desc: 'Pianifica con amici',  entityClass: 'e-event',  href: 'home.html' },
  { id: 'library', em: '🎲', title: 'Esplora la library',   desc: 'Vedi i tuoi giochi',   entityClass: 'e-game',   href: 'home.html' },
  { id: 'chat',    em: '💬', title: 'Chatta con un agente', desc: 'Prova una domanda',     entityClass: 'e-agent',  href: 'home.html' },
];

// Entity accent per step (1-3)
const STEP_ENTITY   = ['e-game', 'e-agent', 'e-session'];
const STEP_CSS_VAR  = ['var(--c-game)', 'var(--c-agent)', 'var(--c-session)'];
const STEP_LABELS   = ['Giochi', 'Agenti', 'Sessione'];
const MIN_SELECTED  = 3;

// ─── Confetti ────────────────────────────────────────────────────────────────

function Confetti() {
  const COLORS = [
    'hsl(25 95% 58%)', 'hsl(38 92% 62%)', 'hsl(240 62% 68%)',
    'hsl(262 68% 68%)', 'hsl(350 85% 68%)', 'hsl(142 60% 55%)',
    'hsl(195 76% 58%)', 'hsl(174 62% 52%)',
  ];
  const pieces = Array.from({ length: 40 }, (_, i) => ({
    id: i,
    x: 5 + Math.random() * 90,
    color: COLORS[i % COLORS.length],
    delay: (Math.random() * 0.9).toFixed(2),
    dur: (2.2 + Math.random() * 1.6).toFixed(2),
    size: Math.round(6 + Math.random() * 9),
    rot: Math.round(Math.random() * 360),
    wide: Math.random() > 0.5,
  }));

  return (
    <div className="ob-confetti" aria-hidden="true">
      {pieces.map(p => (
        <div key={p.id} className="ob-confetti-piece" style={{
          left: p.x + '%',
          background: p.color,
          width:  p.wide ? p.size * 1.6 + 'px' : p.size + 'px',
          height: p.wide ? p.size * 0.55 + 'px' : p.size + 'px',
          borderRadius: p.wide ? '2px' : '50%',
          animationDelay: p.delay + 's',
          animationDuration: p.dur + 's',
          transform: `rotate(${p.rot}deg)`,
        }} />
      ))}
    </div>
  );
}

// ─── OnboardingProgress ──────────────────────────────────────────────────────

function OnboardingProgress({ step, onSkip }) {
  return (
    <div className="ob-topbar">
      <div
        className="ob-progress"
        role="progressbar"
        aria-valuenow={step}
        aria-valuemax={3}
        aria-label="Avanzamento onboarding"
      >
        {STEP_LABELS.map((label, i) => {
          const n = i + 1;
          const state = step > n ? 'done' : step === n ? 'active' : 'pending';
          return (
            <React.Fragment key={n}>
              {i > 0 && (
                <div
                  className="ob-pline"
                  style={{ background: step > i ? `hsl(${STEP_CSS_VAR[i]})` : 'var(--border)' }}
                />
              )}
              <div className="ob-pdot-col">
                <div
                  className={`ob-pdot ${STEP_ENTITY[i]} ${state}`}
                />
                <span
                  className="ob-plabel"
                  style={state === 'active' ? { color: `hsl(${STEP_CSS_VAR[i]})`, fontWeight: 700 } : {}}
                >
                  {label}
                </span>
              </div>
            </React.Fragment>
          );
        })}
      </div>
      <button className="ob-skip-btn" onClick={onSkip} aria-label="Salta onboarding">
        Salta
      </button>
    </div>
  );
}

// ─── GameCard ────────────────────────────────────────────────────────────────

function GameCard({ game, selected, onToggle }) {
  return (
    <button
      className={`ob-game-card ${selected ? 'selected' : ''}`}
      onClick={() => onToggle(game.id)}
      aria-pressed={selected}
      aria-label={`${game.title}, seleziona`}
    >
      <div
        className="ob-game-cover"
        style={{ background: `linear-gradient(160deg, hsl(${game.g[0]}), hsl(${game.g[1]}))` }}
      >
        <span className="ob-game-em">{game.em}</span>
        {selected && (
          <div className="ob-game-check" aria-hidden="true">✓</div>
        )}
      </div>
      <div className="ob-game-info">
        <span className="ob-game-title">{game.title}</span>
        <span className="ob-game-meta">{game.year} · {game.players}p</span>
      </div>
    </button>
  );
}

// ─── AgentRow ────────────────────────────────────────────────────────────────

function AgentRow({ agent, enabled, onToggle }) {
  const id = `toggle-${agent.id}`;
  return (
    <div className="ob-agent-row">
      <span className="ob-agent-em" aria-hidden="true">{agent.em}</span>
      <div className="ob-agent-text">
        <span className="ob-agent-name">{agent.name}</span>
        <span className="ob-agent-desc">{agent.desc}</span>
      </div>
      <button
        id={id}
        role="switch"
        aria-checked={enabled}
        aria-label={`${agent.name}: ${enabled ? 'attivo' : 'disattivo'}`}
        className={`ob-toggle ${enabled ? 'on' : ''}`}
        onClick={() => onToggle(agent.id)}
      >
        <span className="ob-toggle-thumb" />
      </button>
    </div>
  );
}

// ─── ActionCard ──────────────────────────────────────────────────────────────

function ActionCard({ action, onChoose }) {
  return (
    <button
      className={`ob-action-card ${action.entityClass}`}
      onClick={() => onChoose(action.href)}
      aria-label={action.title}
    >
      <span className="ob-action-em e-tint">{action.em}</span>
      <div className="ob-action-text">
        <span className="ob-action-title">{action.title}</span>
        <span className="ob-action-desc">{action.desc}</span>
      </div>
      <span className="ob-action-arrow" aria-hidden="true">→</span>
    </button>
  );
}

// ─── BottomBar ───────────────────────────────────────────────────────────────

function BottomBar({ onBack, onNext, nextLabel = 'Avanti →', nextDisabled = false, showBack = true }) {
  return (
    <div className="ob-bottom-bar">
      {showBack
        ? <button className="btn ghost" onClick={onBack}>← Indietro</button>
        : <span />
      }
      <button
        className="btn primary ob-next-btn"
        onClick={onNext}
        disabled={nextDisabled}
        aria-disabled={nextDisabled}
      >
        {nextLabel}
      </button>
    </div>
  );
}

// ─── Step 0: Welcome ─────────────────────────────────────────────────────────

function WelcomeStep({ userName, onStart, onSkip }) {
  return (
    <div className="ob-step ob-welcome">
      <div className="ob-welcome-hero">
        <div className="ob-brand-mark-lg" aria-hidden="true">M</div>
        <div className="ob-welcome-emoji" aria-hidden="true">
          <span>♟️</span><span>🎲</span><span>🃏</span>
        </div>
      </div>
      <div className="ob-welcome-body">
        <h1 className="ob-welcome-title">
          Benvenuto in <span className="ob-gradient-text">MeepleAI</span>
          {userName ? `, ${userName}` : ''}!
        </h1>
        <p className="ob-welcome-sub">
          Configuriamo l'app in 3 minuti —<br/>
          giochi, agenti e prima sessione.
        </p>
        <div className="ob-welcome-actions">
          <button className="btn primary ob-start-btn" onClick={onStart}>
            Inizia il tour →
          </button>
          <button className="btn ghost" onClick={onSkip}>
            Salta, esploro da solo
          </button>
        </div>
      </div>
    </div>
  );
}

// ─── Step 1: Choose Games ────────────────────────────────────────────────────

function GameSelectStep({ selected, onToggle }) {
  const count = selected.length;
  const ready = count >= MIN_SELECTED;
  return (
    <div className="ob-step ob-game-step e-game">
      <div className="ob-step-head">
        <h2 className="ob-step-title">Quali giochi hai nella tua <span className="e-fg">ludoteca</span>?</h2>
        <p className="ob-step-sub">Seleziona almeno {MIN_SELECTED}, li useremo per personalizzare l'esperienza.</p>
      </div>
      <div className="ob-games-grid">
        {GAMES.map(g => (
          <GameCard
            key={g.id}
            game={g}
            selected={selected.includes(g.id)}
            onToggle={onToggle}
          />
        ))}
      </div>
      <div className={`ob-counter ${ready ? 'ob-counter-ready' : ''}`} aria-live="polite">
        {ready
          ? `✓ ${count} giochi selezionati`
          : `${count} di ${MIN_SELECTED} selezionati — ancora ${MIN_SELECTED - count}`}
      </div>
    </div>
  );
}

// ─── Step 2: Agents ──────────────────────────────────────────────────────────

function AgentToggleStep({ agentStates, onToggle }) {
  const activeCount = Object.values(agentStates).filter(Boolean).length;
  return (
    <div className="ob-step ob-agent-step e-agent">
      <div className="ob-step-head">
        <h2 className="ob-step-title">Attiva i tuoi <span className="e-fg">assistenti</span></h2>
        <p className="ob-step-sub">Ogni agente è specializzato in un tipo di domanda. Puoi modificarli in qualsiasi momento.</p>
      </div>
      <div className="ob-agents-list">
        {AGENTS.map(a => (
          <AgentRow
            key={a.id}
            agent={a}
            enabled={agentStates[a.id]}
            onToggle={onToggle}
          />
        ))}
      </div>
      <p className="ob-agent-hint" aria-live="polite">
        {activeCount} agente{activeCount !== 1 ? 'i' : ''} attiv{activeCount !== 1 ? 'i' : 'o'}
      </p>
    </div>
  );
}

// ─── Step 3: First Session ───────────────────────────────────────────────────

function FirstSessionStep({ onChoose, onBack, onSkip }) {
  return (
    <div className="ob-step ob-session-step e-session">
      <div className="ob-step-head">
        <h2 className="ob-step-title">Tutto pronto! Cosa vuoi fare <span className="e-fg">ora</span>?</h2>
        <p className="ob-step-sub">Scegli da dove partire — puoi sempre tornare.</p>
      </div>
      <div className="ob-actions-list">
        {ACTIONS.map(a => (
          <ActionCard key={a.id} action={a} onChoose={onChoose} />
        ))}
      </div>
    </div>
  );
}

// ─── Step 4: Complete ────────────────────────────────────────────────────────

function CompleteStep({ onHome }) {
  return (
    <div className="ob-step ob-complete-step">
      <Confetti />
      <div className="ob-complete-body">
        <div className="ob-brand-mark-lg ob-brand-mark-xl" aria-hidden="true">M</div>
        <h1 className="ob-complete-title">
          <span className="ob-gradient-text">Benvenuto!</span>
        </h1>
        <p className="ob-complete-sub">MeepleAI è pronto.<br/>Buon gioco!</p>
        <button className="btn primary ob-home-btn" onClick={onHome}>
          Vai alla home →
        </button>
      </div>
    </div>
  );
}

// ─── App ─────────────────────────────────────────────────────────────────────

function OnboardingApp() {
  const [step, setStep]           = useState(0);
  const [dir, setDir]             = useState(1);       // 1 = forward, -1 = back
  const [animKey, setAnimKey]     = useState(0);
  const [selectedGames, setSelectedGames] = useState([]);
  const [agentStates, setAgentStates]     = useState(
    Object.fromEntries(AGENTS.map(a => [a.id, a.defaultOn]))
  );

  // Persist step to localStorage
  useEffect(() => {
    const saved = parseInt(localStorage.getItem('mai-onboarding-step') || '0', 10);
    if (saved > 0 && saved <= 4) setStep(saved);
  }, []);
  useEffect(() => {
    localStorage.setItem('mai-onboarding-step', String(step));
  }, [step]);

  function go(next, direction = 1) {
    setDir(direction);
    setAnimKey(k => k + 1);
    setStep(next);
  }

  function handleNext() {
    if (step === 3) go(4);
    else go(step + 1, 1);
  }
  function handleBack() { go(step - 1, -1); }
  function handleSkip() { go(4, 1); }
  function handleHome() { window.location.href = 'home.html'; }
  function handleChoose(href) { window.location.href = href; }

  function toggleGame(id) {
    setSelectedGames(prev =>
      prev.includes(id) ? prev.filter(g => g !== id) : [...prev, id]
    );
  }
  function toggleAgent(id) {
    setAgentStates(prev => ({ ...prev, [id]: !prev[id] }));
  }

  // Entity class for the step container (steps 1–3)
  const stepEntityClass = step >= 1 && step <= 3 ? STEP_ENTITY[step - 1] : '';

  const userName = 'Luca'; // demo

  return (
    <div className={`ob-root ${stepEntityClass}`}>
      {/* Skip link a11y */}
      <a href="#ob-main" className="ob-skip-link">Vai al contenuto</a>

      {/* Hub nav */}
      <nav className="hub-nav">
        <a href="00-hub.html" className="brand" style={{ color: 'inherit', textDecoration: 'none' }}>
          <span className="brand-mark">M</span>
          <span>MeepleAI</span>
        </a>
        <span style={{ fontSize: 11, padding: '3px 8px', borderRadius: 6, background: 'var(--bg-muted)', fontFamily: 'var(--f-mono)', color: 'var(--text-muted)' }}>
          10 · Onboarding
        </span>
        <div className="spacer" />
        <a href="00-hub.html">← Hub</a>
      </nav>

      {/* Theme toggle */}
      <button className="theme-toggle" id="themeToggle">
        🌗 <span id="themeLabel">Light</span>
      </button>

      {/* Main content */}
      <main id="ob-main" className="ob-main">
        <div className="ob-frame">
          {/* Phone status bar */}
          <div className="phone-sbar ob-sbar">
            <span>9:41</span>
            <span className="ind">▲ ◆ ▶</span>
          </div>

          {/* Progress bar (steps 1–3) */}
          {step >= 1 && step <= 3 && (
            <OnboardingProgress step={step} onSkip={handleSkip} />
          )}

          {/* Step content */}
          <div
            key={animKey}
            className={`ob-content ${dir > 0 ? 'slide-in-right' : 'slide-in-left'}`}
          >
            {step === 0 && (
              <WelcomeStep
                userName={userName}
                onStart={() => go(1, 1)}
                onSkip={handleSkip}
              />
            )}
            {step === 1 && (
              <GameSelectStep selected={selectedGames} onToggle={toggleGame} />
            )}
            {step === 2 && (
              <AgentToggleStep agentStates={agentStates} onToggle={toggleAgent} />
            )}
            {step === 3 && (
              <FirstSessionStep onChoose={handleChoose} onBack={handleBack} onSkip={handleSkip} />
            )}
            {step === 4 && (
              <CompleteStep onHome={handleHome} />
            )}
          </div>

          {/* Bottom bar */}
          {step === 1 && (
            <BottomBar
              showBack={false}
              onNext={handleNext}
              nextDisabled={selectedGames.length < MIN_SELECTED}
              nextLabel={`Avanti → (${selectedGames.length}/${MIN_SELECTED})`}
            />
          )}
          {step === 2 && (
            <BottomBar
              onBack={handleBack}
              onNext={handleNext}
              nextLabel="Avanti →"
            />
          )}
          {step === 3 && (
            <BottomBar
              onBack={handleBack}
              onNext={handleSkip}
              nextLabel="Salta"
            />
          )}
        </div>
      </main>

      {/* Tweaks panel */}
      <div id="tweaks-panel">
        <h4>Tweaks</h4>
        <label>
          <span>Frame style</span>
          <select id="tweak-frame">
            <option value="phone">Phone frame</option>
            <option value="card">Card only</option>
            <option value="full">Full width</option>
          </select>
        </label>
        <label>
          <span>Utente</span>
          <select id="tweak-user">
            <option value="Luca">Luca</option>
            <option value="">Anonimo</option>
          </select>
        </label>
        <label>
          <span>Step demo</span>
          <input type="range" id="tweak-step" min="0" max="4" step="1" defaultValue="0" />
        </label>
      </div>
    </div>
  );
}

// ─── Mount ───────────────────────────────────────────────────────────────────
const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(<OnboardingApp />);

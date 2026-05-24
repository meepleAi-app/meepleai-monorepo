/* MeepleAI · Chat full-screen mockup (B11b · chiusura parziale issue #491)
   Routes: /chat/[threadId]   (3 screen: Desktop 3-col, Mobile single-pane)
           /chat/new          (Empty state, 3 quick-starter variants per agent type)
   File: admin-mockups/design_files/chat-fullscreen.{html,jsx}

   ─── Twin React-style del HTML ───
   Component naming match HTML pattern:
     - ChatFullscreenDesktop  → Screen B11b-1 (3-col layout)
     - ChatFullscreenMobile   → Screen B11b-2 (single-pane)
     - ChatEmptyState         → Screen B11b-3 (/chat/new hero + quick-starters)
   Sub-components:
     - ThreadList             (sidebar sx desktop, group-by oggi/ieri/settimana)
     - ChatComposer           (textarea autoresize + attachment + voice + send)
     - MessageBubble          (user / agent variant, reader-mode 24pt support)
     - CitationPill           (click → sp4-citation-pdf-viewer overlay, RIUSO)
     - AgentInfoMiniCard      (compressed sp4-agent-detail Hero pattern)
     - SuggestedActions       (sidebar dx, "apri libro pag X", "crea play record")
     - QuickStarterCard       (variant per agent type: libro-game/boardgame/default)
     - TelemetryFooter        (cost transparency, latency, quota — coerenza Task 1-4)
     - StreamErrorBanner      (Nygard failure mode 3 — last user msg preserved)

   ─── Pattern decisions ───
   - Slide-over ↔ full-screen routing: viewport-driven (<1024px → full-screen sempre)
   - Streaming: token-by-token via `useThreadMessages` hook (PR#551)
                AbortController per cancel istantaneo <100ms
   - Citation pill: click → sp4-citation-pdf-viewer overlay (NO fork)
   - Attachment Aaron-specific: foto inline apre flow translate (Task 4)
                                librogame-runthrough-translate-viewer.html ?mode=manual o camera
   - Voice 🎤: UI placeholder, backend Whisper integration deferita v1
   - Quick-starter: derivato da agent.metadata.category (libro-game|boardgame|default)
                    Fallback graceful a "default" se field mancante

   ─── Reader mode + wake-lock (coerenza Task 2 sez 1.1.1) ───
   - Toggle 📖 → bubble agent font 24pt (localStorage `reader-mode-enabled`)
   - Wake-lock 🔆 badge nel telemetry footer durante streaming
     3 stati: active | disabled | unsupported (Safari <16.4)

   ─── Stream states (Nygard failure modes) ───
   1. idle           — composer empty, send disabled
   2. streaming      — token-by-token, typing dots, cancel CTA visibile
   3. stream-error   — banner errore + retry, last user msg preserved nel composer
   4. stream-abort   — message preserved con badge "[interrotto]", cost parziale fatturato

   ─── Cross-ref ───
   - PR#551 useThreadMessages hook → apps/web/src/components/chat/shared/use-thread-messages.ts
   - sp4-citation-pdf-viewer.html → citation pill overlay reuse (G4 v3, NO fork)
   - sp4-agent-detail.html → AgentInfoMiniCard pattern compresso
   - nanolith-nav-chat-panel.html → slide-over secondary mode (viewport ≥1024px)
   - librogame-runthrough-translate-viewer.html → Task 2+4 translate ponte
   - audit docs/for-developers/audits/2026-05-22-mockup-gaps.md § P0
   - spec docs/superpowers/specs/2026-05-23-mockup-refinement-aaron-core-design.md sez 6

   Persona: Aaron (badsworm@gmail.com), narrative "Sera con i ragazzi · Runa di Ardenel · §147"
            (coerenza Task 1-4). 1 thread Boardgamer (Wingspan) per multi-agent demo.
*/

const { useState, useEffect, useRef, useCallback } = React;

// ─── Reader mode persistence (sez 1.1.1 coerenza Task 2 sez 1b.B) ───────
// IMPORTANT: stessa key del translate-viewer per cross-route consistency Aaron
const READER_MODE_KEY = 'reader-mode-enabled';

const useReaderMode = () => {
  const [enabled, setEnabled] = useState(() => {
    if (typeof window === 'undefined') return false;
    try { return window.localStorage.getItem(READER_MODE_KEY) === 'true'; }
    catch { return false; }
  });
  const toggle = useCallback(() => {
    setEnabled(prev => {
      const next = !prev;
      try { window.localStorage.setItem(READER_MODE_KEY, String(next)); } catch {}
      return next;
    });
  }, []);
  return [enabled, toggle];
};

// ─── Wake-lock badge (sez 1.1.1 coerenza Task 2 sez 1b.C) ───────────────
const useWakeLock = (active) => {
  // 'active' | 'disabled' | 'unsupported'
  const [status, setStatus] = useState('disabled');
  useEffect(() => {
    if (typeof navigator === 'undefined' || !('wakeLock' in navigator)) {
      setStatus('unsupported');
      return;
    }
    let lock = null;
    if (active) {
      navigator.wakeLock.request('screen')
        .then(l => { lock = l; setStatus('active'); })
        .catch(() => setStatus('disabled'));
    } else {
      setStatus('disabled');
    }
    return () => { if (lock) lock.release().catch(() => {}); };
  }, [active]);
  return status;
};

// ─── Sample data (narrative "Sera con i ragazzi · Runa di Ardenel · §147") ──
const THREADS = [
  { id: 't1', group: 'Oggi', kind: 'libro', avatar: '📕', title: 'Sera con i ragazzi · §147', preview: 'Aaron: Niamh affronta il Goblin Captain…', ts: '22:34', unread: 2, active: true },
  { id: 't2', group: 'Oggi', kind: 'libro', avatar: '📕', title: 'Runa di Ardenel · §89', preview: 'Riassumi capitolo precedente…', ts: '14:10' },
  { id: 't3', group: 'Ieri', kind: 'boardgame', avatar: '🦅', title: 'Wingspan · scoring fine partita', preview: 'Bonus ornitologo come si calcola?', ts: 'ieri' },
  { id: 't4', group: 'Ieri', kind: 'libro', avatar: '📕', title: 'Press Start · setup 4 giocatori', preview: 'Press Start p.3-7 spiega che…', ts: 'ieri' },
  { id: 't5', group: 'Settimana', kind: 'boardgame', avatar: '🚂', title: 'Brass: Birmingham · prestiti', preview: 'I prestiti aumentano VP penalty…', ts: '2g' },
];

const AGENT_RUNA = {
  id: 'tutor-runa-ardenel',
  name: 'Tutor Runa di Ardenel',
  persona: 'Libro-game · narratore',
  avatar: '📕',
  category: 'libro-game',
  description: 'Risponde a domande sul gamebook, ricorda decisioni narrative, traduce paragrafi EN→IT. Non spoilera scelte future.',
  stats: { queries: 12, citations: 5, characters: 3 },
};

const AGENT_WINGSPAN = {
  id: 'wingspan-mago',
  name: 'Mago di Wingspan',
  persona: 'Boardgamer · strategia',
  avatar: '🦅',
  category: 'boardgame',
};

// ─── Quick-starter variants (3 per agent.metadata.category) ──────────────
const QUICK_STARTERS = {
  'libro-game': [
    { icon: '📜', cat: 'Libro-game · regole', q: 'Quali sono le regole di Press Start?' },
    { icon: '⚔️', cat: 'Libro-game · encounter', q: 'Spiega questa Encounter (foto §147)' },
    { icon: '📖', cat: 'Libro-game · riassunto', q: 'Riassumi paragrafo 42 in italiano' },
    { icon: '🧙', cat: 'Libro-game · personaggi', q: 'Lista personaggi nella campagna' },
  ],
  'boardgame': [
    { icon: '🎲', cat: 'Boardgamer · setup', q: 'Setup iniziale Wingspan 3 giocatori' },
    { icon: '⚖️', cat: 'Boardgamer · regole', q: 'Regole edge case (predatore + jolly)' },
    { icon: '🧠', cat: 'Boardgamer · strategia', q: 'Strategie comuni habitat foresta' },
    { icon: '❓', cat: 'Boardgamer · FAQ', q: 'FAQ ufficiali del gioco' },
  ],
  'default': null, // → agent picker dropdown CTA invece di quick-starters
};

// Fallback graceful: agent senza metadata.category → 'default' variant
const resolveQuickStarters = (agent) => {
  if (!agent || !agent.category) return QUICK_STARTERS['default'];
  return QUICK_STARTERS[agent.category] || QUICK_STARTERS['default'];
};

const SAMPLE_AGENTS = [
  { id: 'tutor-runa-ardenel', name: 'Tutor Runa di Ardenel', avatar: '📕', category: 'libro-game', lastUsed: '5min fa' },
  { id: 'wingspan-mago', name: 'Mago di Wingspan', avatar: '🦅', category: 'boardgame', lastUsed: '2h fa' },
  { id: 'brass-tutor', name: 'Brass: Birmingham Tutor', avatar: '🚂', category: 'boardgame', lastUsed: '2g fa' },
];

// ─── Sub-component · ThreadList (sidebar sx desktop) ──────────────────────
const ThreadList = ({ threads, activeId, onSelect }) => {
  const groups = threads.reduce((acc, t) => {
    acc[t.group] = acc[t.group] || [];
    acc[t.group].push(t);
    return acc;
  }, {});
  return (
    <div className="sl-list">
      {Object.entries(groups).map(([group, items]) => (
        <React.Fragment key={group}>
          <div className="group-h">{group}</div>
          {items.map(t => (
            <button
              key={t.id}
              type="button"
              className={`sl-thread ${t.kind} ${t.id === activeId ? 'active' : ''}`}
              onClick={() => onSelect && onSelect(t.id)}
              aria-current={t.id === activeId ? 'true' : undefined}
            >
              <div className="av">{t.avatar}</div>
              <div className="info">
                <div className="ti">{t.title}</div>
                <div className="pv">{t.preview}</div>
              </div>
              <div className="meta">
                <span className="ts">{t.ts}</span>
                {t.unread ? <span className="unread">{t.unread}</span> : null}
              </div>
            </button>
          ))}
        </React.Fragment>
      ))}
    </div>
  );
};

// ─── Sub-component · CitationPill (click → sp4-citation-pdf-viewer overlay) ──
const CitationPill = ({ paragraph, variant = 'default', onOpen }) => {
  // variant: 'default' (kb generic) | 'translate' (Aaron flow translate, color --c-kb)
  const className = `cit-pill${variant === 'translate' ? ' translate' : ''}`;
  return (
    <a
      className={className}
      href="sp4-citation-pdf-viewer.html"
      target="_blank"
      rel="noopener"
      onClick={(e) => { if (onOpen) { e.preventDefault(); onOpen(paragraph); } }}
      aria-label={`Apri ${paragraph} in PDF viewer (citation pill)`}
    >
      <span className="ico">📖</span>{paragraph}
    </a>
  );
};

// ─── Sub-component · MessageBubble (user/agent, reader-mode support) ─────
const MessageBubble = ({ role, children, readerMode, meta, abortBadge, isStreaming }) => {
  const isAgent = role === 'agent';
  const cls = `bubble ${role}${isAgent && readerMode ? ' reader-on' : ''}`;
  return (
    <div className={`msg-row ${role}`}>
      <div className="av-msg">{isAgent ? '📕' : '🎲'}</div>
      <div className={cls}>
        {children}
        {isStreaming && <span className="stream-cursor" aria-hidden="true"></span>}
        {abortBadge && (
          <span className="abort-badge" aria-label={`Stream interrotto: ${abortBadge}`}>
            {abortBadge === 'user' ? '⊘ interrotto' : '⚠ stream-error'}
          </span>
        )}
        {meta && (
          <div className="meta-foot">
            {meta.confidence && (
              <span className={`conf-chip ${meta.confidenceLevel || 'high'}`}>
                ● {meta.confidence}
              </span>
            )}
            {meta.source && <span>· {meta.source}</span>}
            {meta.timestamp && <span style={{ marginLeft: 'auto' }}>{meta.timestamp}</span>}
          </div>
        )}
      </div>
    </div>
  );
};

// ─── Sub-component · ChatComposer (textarea + attachment + voice + send) ──
const ChatComposer = ({
  value,
  onChange,
  onSend,
  disabled,
  isStreaming,
  onAttach,    // Aaron-specific: apre flow translate
  onVoice,     // UI placeholder, Whisper deferita
  mobile = false,
}) => {
  const inputRef = useRef(null);

  // Autoresize 1-row default → max 4 rows
  useEffect(() => {
    if (!inputRef.current) return;
    inputRef.current.style.height = 'auto';
    const lines = Math.min((value || '').split('\n').length, 4);
    inputRef.current.style.height = `${44 + (lines - 1) * 22}px`;
  }, [value]);

  const handleKeyDown = (e) => {
    if (e.key === 'Enter' && !e.shiftKey && !disabled && !isStreaming) {
      e.preventDefault();
      onSend && onSend(value);
    }
  };

  return (
    <div className={mobile ? 'mob-composer' : 'composer'}>
      <div className="composer-row">
        <div className="composer-tools" style={mobile ? { flexDirection: 'row' } : {}}>
          <button
            type="button"
            className="tool-btn aaron"
            disabled={disabled || isStreaming}
            onClick={onAttach}
            title="Allega foto (Aaron: apre flow translate — librogame-runthrough-translate-viewer.html)"
            aria-label="Allega foto — ponte verso translate flow Task 4"
          >📎</button>
          <button
            type="button"
            className="tool-btn"
            disabled={disabled || isStreaming}
            onClick={onVoice}
            title="Microfono (voice — UI placeholder, Whisper backend deferito v1)"
            aria-label="Microfono — UI placeholder, backend Whisper deferito"
          >🎤</button>
        </div>
        <textarea
          ref={inputRef}
          className="composer-input"
          placeholder={isStreaming ? 'In attesa della risposta…' : disabled ? '' : 'Scrivi un messaggio…'}
          value={value || ''}
          onChange={(e) => onChange && onChange(e.target.value)}
          onKeyDown={handleKeyDown}
          rows={1}
          disabled={disabled || isStreaming}
          aria-label={isStreaming ? 'Composer (disabled durante streaming)' : 'Composer'}
        />
        <button
          type="button"
          className="send-btn"
          disabled={disabled || isStreaming || !(value || '').trim()}
          onClick={() => onSend && onSend(value)}
          aria-label={isStreaming ? 'Invia (disabled durante streaming)' : 'Invia messaggio'}
        >➤</button>
      </div>
    </div>
  );
};

// ─── Sub-component · StreamErrorBanner (Nygard failure mode 3) ────────────
const StreamErrorBanner = ({ message, timeout, onRetry }) => (
  <div className="stream-error-banner" role="alert" aria-label="Errore stream — retry disponibile">
    <span className="ico">⚠️</span>
    <div className="info">
      <div className="v">Errore stream — connessione interrotta dopo {timeout}s idle</div>
      <div className="s">Last user message conservato nel composer · click "Riprova" per re-inviare con stesso AbortController</div>
    </div>
    <button className="retry" type="button" onClick={onRetry} aria-label="Riprova stream con stesso messaggio">
      ↻ Riprova
    </button>
  </div>
);

// ─── Sub-component · AgentInfoMiniCard (compressed sp4-agent-detail Hero) ──
const AgentInfoMiniCard = ({ agent }) => (
  <div className="agent-mini">
    <div className="row1">
      <div className="av">{agent.avatar}</div>
      <div className="info">
        <div className="name">{agent.name}</div>
        <div className="persona">{agent.persona}</div>
      </div>
    </div>
    {agent.description && (
      <p style={{ margin: 0, fontSize: 'var(--fs-xs)', color: 'var(--text-sec)', lineHeight: 'var(--lh-snug)' }}>
        {agent.description}
      </p>
    )}
    {agent.stats && (
      <div className="stats">
        <div className="s-item"><span className="v">{agent.stats.queries}</span> query oggi</div>
        <div className="s-item"><span className="v">{agent.stats.citations}</span> citazioni</div>
        <div className="s-item"><span className="v">{agent.stats.characters}</span> personaggi</div>
      </div>
    )}
  </div>
);

// ─── Sub-component · SuggestedActions (sidebar dx, contestuali) ───────────
const SuggestedActions = ({ actions }) => (
  <div className="panel-section">
    <h4>Azioni suggerite</h4>
    {actions.map((a, i) => (
      <button key={i} className="action-card" type="button" aria-label={a.label} onClick={a.onClick}>
        <span className="ico">{a.icon}</span>
        <span>{a.label}</span>
        <span className="arrow">›</span>
      </button>
    ))}
  </div>
);

// ─── Sub-component · QuickStarterCard (variant per agent type) ────────────
const QuickStarterCard = ({ icon, cat, question, variant, onClick }) => (
  <button
    type="button"
    className={`qs-card ${variant}`}
    onClick={onClick}
    aria-label={`Quick starter ${cat} — ${question}`}
  >
    <span className="qs-ico">{icon}</span>
    <span className="qs-cat">{cat}</span>
    <span className="qs-q">{question}</span>
  </button>
);

// ─── Sub-component · TelemetryFooter (cost, latency, quota, wake-lock) ────
const TelemetryFooter = ({
  streamState,    // 'idle' | 'streaming' | 'stream-error' | 'stream-abort'
  cost,
  tokens,
  quota,
  wakeLockStatus, // 'active' | 'disabled' | 'unsupported'
  viewMode = 'full-screen',
  variant,        // for /chat/new empty state
}) => {
  const wakeLockLabel = {
    active: '🔆 wake-lock attivo',
    disabled: '🔆 wake-lock off',
    unsupported: '🔆 non supportato',
  }[wakeLockStatus] || '🔆 off';

  const statePip = {
    idle: { cls: '', label: 'idle' },
    streaming: { cls: 'live', label: 'streaming' },
    'stream-error': { cls: 'warn', label: 'stream-error' },
    'stream-abort': { cls: 'warn', label: 'stream-abort' },
  }[streamState] || { cls: '', label: streamState };

  return (
    <div className="telemetry-foot" aria-label="Telemetria chat">
      <span className={`pip ${statePip.cls}`}>
        {statePip.cls === 'live' ? '●' : ''} <span className="v">{statePip.label}</span>
      </span>
      {cost && <span className="pip"><span>cost:</span> <span className="v">€{cost}</span></span>}
      {tokens && <span className="pip"><span>tok:</span> <span className="v">{tokens}</span></span>}
      {quota && <span className="pip warn"><span>quota:</span> <span className="v">{quota}</span></span>}
      {variant && <span className="pip"><span>variant:</span> <span className="v">{variant}</span></span>}
      <span className="spacer"></span>
      <span className={`wake-lock-badge ${wakeLockStatus}`} title={`Wake-lock ${wakeLockStatus} (sez 1.1.1 coerenza Task 2 sez 1b.C)`}>
        {wakeLockLabel}
      </span>
      {viewMode && <span className="pip"><span>view:</span> <span className="v">{viewMode}</span></span>}
    </div>
  );
};

// ═══════════════════════════════════════════════════════════════════════
// SCREEN B11b-1 · ChatFullscreenDesktop (3-col layout, 1280px)
// ═══════════════════════════════════════════════════════════════════════
const ChatFullscreenDesktop = ({
  threadId = 't1',
  agent = AGENT_RUNA,
  streamState = 'streaming',  // demo: idle | streaming | stream-error | stream-abort
}) => {
  const [readerMode, toggleReader] = useReaderMode();
  const wakeLockStatus = useWakeLock(streamState === 'streaming');
  const [composerValue, setComposerValue] = useState(
    streamState === 'stream-error' ? 'E se ho 2 ferite, il malus −2 si cumula con quello del Reaver?' : ''
  );
  const [activeThread, setActiveThread] = useState(threadId);
  const abortRef = useRef(null);

  // Riusa pattern useThreadMessages hook PR#551 (AbortController per cancel istantaneo)
  const handleCancel = useCallback(() => {
    if (abortRef.current) {
      abortRef.current.abort();
      abortRef.current = null;
    }
  }, []);

  const handleAttach = useCallback(() => {
    // Aaron-specific: apre flow translate (Task 4)
    // librogame-runthrough-translate-viewer.html ?mode=manual o camera
    window.open('librogame-runthrough-translate-viewer.html?mode=manual', '_blank');
  }, []);

  const handleRetry = useCallback(() => {
    // Riusa stesso AbortController dal hook — preview last user message preservato
    abortRef.current = new AbortController();
    // … fetch streaming endpoint with abortRef.current.signal
  }, []);

  const suggestedActions = [
    { icon: '📕', label: 'Apri libro pag 42', onClick: () => {} },
    { icon: '🎲', label: 'Crea play record (§147)', onClick: () => {} },
    { icon: '📖', label: 'Glossario campagna', onClick: () => {} },
    { icon: '↩', label: 'Modalità slide-over', onClick: () => {} }, // → nanolith-nav-chat-panel
  ];

  return (
    <div className="desktop" role="region" aria-label="Desktop 3-col chat full-screen">
      <div className="titlebar">
        <span className="dot r"></span><span className="dot y"></span><span className="dot g"></span>
        <span className="url">meepleai.app/chat/{activeThread} ← full-screen mode</span>
      </div>
      <div className="chat-3col">
        {/* ── Sidebar SX · Thread list ── */}
        <aside className="sidebar-l" aria-label="Thread list">
          <div className="sl-head">
            <div className="sl-tabs" role="tablist">
              <button className="tab active" role="tab" aria-selected="true">Tutti</button>
              <button className="tab" role="tab" aria-selected="false">Preferiti</button>
              <button className="tab" role="tab" aria-selected="false">Archiviati</button>
            </div>
            <input type="text" className="sl-search" placeholder="🔍 Cerca thread…" aria-label="Cerca thread"/>
            <button className="sl-filter" type="button" aria-label="Filtro per agent (dropdown chip)">
              <span>🧙 Tutti gli agent</span><span className="chevron">▾</span>
            </button>
          </div>
          <button className="sl-cta" type="button">
            <span>＋</span><span>Nuova chat</span>
          </button>
          <ThreadList threads={THREADS} activeId={activeThread} onSelect={setActiveThread} />
        </aside>

        {/* ── MAIN · Chat stream ── */}
        <section className="main-c" aria-label="Conversazione attiva">
          <div className="main-h">
            <div className="ag">{agent.avatar}</div>
            <div className="ti-block">
              <div className="ti">Sera con i ragazzi · §147</div>
              <div className="sub">
                {agent.name} · DeepSeek IT · 12 query oggi · stream: <strong>{streamState}</strong>
              </div>
            </div>
            <div className="actions" role="toolbar" aria-label="Azioni thread">
              <button
                className={`icon-btn ${readerMode ? 'active' : ''}`}
                type="button"
                onClick={toggleReader}
                aria-pressed={readerMode}
                aria-label="Reader mode 24pt (localStorage `reader-mode-enabled`, coerenza Task 2)"
                title="Reader mode 24pt"
              >📖</button>
              <button className="icon-btn" type="button" aria-label="Preferiti" title="Preferiti">⭐</button>
              <button className="icon-btn" type="button" aria-label="Archivia" title="Archivia">📦</button>
              <button className="icon-btn" type="button" aria-label="Altre azioni" title="Altre azioni">⋮</button>
            </div>
          </div>

          <div className="stream" role="log" aria-live="polite" aria-relevant="additions" aria-label="Stream messaggi chat">
            <div className="ts-divider">oggi · 22:14</div>

            <MessageBubble role="user">
              Niamh affronta il Goblin Captain al §147. Posso tirare 2d6 + bonus spada?
            </MessageBubble>

            <MessageBubble
              role="agent"
              readerMode={readerMode}
              meta={{ confidence: 'alta · 94%', confidenceLevel: 'high', source: 'Press Start §147 · Rules p.42 §3.7', timestamp: '22:33 · 1.2s · 412 tok · €0,0021' }}
            >
              Sì — Niamh ha la <em>Spada Antica</em> dal §123, quindi tira <strong>2d6 + 3</strong>.
              Il Goblin Captain ha CD 9, quindi colpisci con 6+. Se vinci, vai al{' '}
              <CitationPill paragraph="§147" variant="translate" />{' '}oppure{' '}
              <CitationPill paragraph="§271" variant="translate" />{' '}per usare l'oggetto Voidstone come arma.
            </MessageBubble>

            {streamState === 'streaming' && (
              <>
                <MessageBubble role="user">
                  E se ho 2 ferite, il malus −2 si cumula con quello del Reaver?
                </MessageBubble>
                <MessageBubble role="agent" readerMode={readerMode} isStreaming>
                  I malus di ferita cumulano solo con malus situazionali (oscurità, distanza). Il Reaver impone un malus combat-specific, quindi
                </MessageBubble>
                <div className="msg-row agent">
                  <div className="av-msg">📕</div>
                  <div className="typing-dots" role="status" aria-label="Tutor sta scrivendo">
                    <span></span><span></span><span></span>
                  </div>
                </div>
                <button
                  className="cancel-stream-cta"
                  type="button"
                  onClick={handleCancel}
                  aria-label="Cancella streaming in corso (AbortController, riusa useThreadMessages hook PR#551)"
                >
                  ✕ Cancella stream (4.2s)
                </button>
              </>
            )}

            {streamState === 'stream-error' && (
              <>
                <MessageBubble role="user">
                  E se ho 2 ferite, il malus −2 si cumula con quello del Reaver?
                </MessageBubble>
                <MessageBubble role="agent" abortBadge="error">
                  I malus di ferita cumulano solo con malus situazionali (oscurità, distanza). Il Reaver impone un malus combat-specific, quindi
                </MessageBubble>
                <StreamErrorBanner message="" timeout={30} onRetry={handleRetry} />
              </>
            )}

            {streamState === 'stream-abort' && (
              <>
                <MessageBubble role="user">E se ho 2 ferite…</MessageBubble>
                <MessageBubble
                  role="agent"
                  abortBadge="user"
                  meta={{ confidence: 'parziale · 60%', confidenceLevel: 'med', source: '4.2s · cancelled by user · 124 tok partial' }}
                >
                  I malus di ferita cumulano solo con malus situazionali (oscurità, distanza). Il Reaver impone un malus combat-specific
                </MessageBubble>
              </>
            )}
          </div>

          <ChatComposer
            value={composerValue}
            onChange={setComposerValue}
            disabled={streamState === 'streaming'}
            isStreaming={streamState === 'streaming'}
            onAttach={handleAttach}
          />

          <TelemetryFooter
            streamState={streamState}
            cost={streamState === 'streaming' ? '0,0034' : streamState === 'stream-error' ? '0,0019' : streamState === 'stream-abort' ? '0,0011' : null}
            tokens={streamState === 'streaming' ? '418/2k' : streamState === 'stream-error' ? '187/2k' : streamState === 'stream-abort' ? '124/2k' : null}
            quota="12/15 free"
            wakeLockStatus={wakeLockStatus}
            viewMode="full-screen"
          />
        </section>

        {/* ── Sidebar DX · Agent info + Sources + Suggested actions ── */}
        <aside className="sidebar-r" aria-label="Pannello contesto (agent, fonti, azioni)">
          <AgentInfoMiniCard agent={agent} />
          <div className="panel-section">
            <h4>Fonti citate · 3</h4>
            <button className="src-card" type="button" aria-label="Apri Press Start §147 in PDF viewer">
              <span className="ico">📖</span>
              <div className="l"><div className="v">Press Start · §147</div><div className="s">Goblin Captain encounter · p.42</div></div>
            </button>
            <button className="src-card" type="button" aria-label="Apri Rules §3.7 in PDF viewer">
              <span className="ico">📖</span>
              <div className="l"><div className="v">Rules · §3.7</div><div className="s">Malus ferita + situazionali · p.42</div></div>
            </button>
            <button className="src-card" type="button" aria-label="Apri Encounter §271 in PDF viewer">
              <span className="ico">📖</span>
              <div className="l"><div className="v">Encounter Book · §271</div><div className="s">Voidstone · oggetto non perdibile</div></div>
            </button>
          </div>
          <SuggestedActions actions={suggestedActions} />
        </aside>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════════════════════
// SCREEN B11b-2 · ChatFullscreenMobile (single-pane, 375px)
// ═══════════════════════════════════════════════════════════════════════
const ChatFullscreenMobile = ({
  agent = AGENT_RUNA,
  streamState = 'idle',
  showContextSheet = false,
}) => {
  const [readerMode] = useReaderMode();
  const wakeLockStatus = useWakeLock(streamState === 'streaming');
  const [sheetOpen, setSheetOpen] = useState(showContextSheet);
  const [composerValue, setComposerValue] = useState('');

  const handleAttach = useCallback(() => {
    // Aaron-specific: foto inline apre flow translate Task 4
    window.open('librogame-runthrough-translate-viewer.html?mode=manual', '_blank');
  }, []);

  return (
    <div className="phone" role="region" aria-label="Mobile chat full-screen">
      <div className="phone-sbar"><span>22:14</span><span>📶 🔋 78%</span></div>

      <div className="mob-h">
        <button className="back" aria-label="Indietro">←</button>
        <div
          className="ti-block"
          aria-label="Apri context panel (bottom sheet)"
          role="button"
          tabIndex={0}
          onClick={() => setSheetOpen(true)}
        >
          <div className="ti">Sera con i ragazzi · §147</div>
          <div className="sub">{agent.name} · 12 query oggi</div>
        </div>
        <div className="av">{agent.avatar}</div>
        <button className="kebab" aria-label="Altre azioni">⋮</button>
      </div>

      <div className="mob-stream" role="log">
        <div className="ts-divider">oggi · 22:14</div>
        <MessageBubble role="user">Niamh affronta Goblin Captain §147. 2d6 + bonus spada?</MessageBubble>
        <MessageBubble
          role="agent"
          readerMode={readerMode}
          meta={{ confidence: 'alta · 94%', confidenceLevel: 'high', timestamp: '22:33' }}
        >
          Sì — Spada Antica da §123, tira 2d6 + 3. Goblin Captain CD 9, colpisci con 6+.{' '}
          <CitationPill paragraph="§147" variant="translate" />
        </MessageBubble>
        {streamState === 'streaming' && (
          <>
            <MessageBubble role="user">Se ho 2 ferite, malus −2 cumula con Reaver?</MessageBubble>
            <MessageBubble role="agent" readerMode={readerMode} isStreaming>
              I malus di ferita cumulano solo con malus situazionali (oscurità, distanza)
            </MessageBubble>
            <button className="cancel-stream-cta" type="button" aria-label="Cancella stream">
              ✕ Cancella (4.2s)
            </button>
          </>
        )}
      </div>

      <ChatComposer
        value={composerValue}
        onChange={setComposerValue}
        disabled={streamState === 'streaming'}
        isStreaming={streamState === 'streaming'}
        onAttach={handleAttach}
        mobile
      />

      <TelemetryFooter
        streamState={streamState}
        tokens={streamState === 'streaming' ? '187' : null}
        quota="12/15"
        wakeLockStatus={wakeLockStatus}
      />

      {/* Bottom sheet context panel (tap header → open) */}
      {sheetOpen && (
        <>
          <div className="mob-sheet-backdrop" onClick={() => setSheetOpen(false)} aria-hidden="true" />
          <aside className="mob-sheet" role="dialog" aria-modal="true" aria-label="Context panel (agent · fonti · azioni)">
            <div className="grip" aria-hidden="true" />
            <AgentInfoMiniCard agent={agent} />
            <div className="panel-section">
              <h4>Fonti citate · 3</h4>
              <button className="src-card"><span className="ico">📖</span><div className="l"><div className="v">Press Start · §147</div><div className="s">Goblin Captain · p.42</div></div></button>
              <button className="src-card"><span className="ico">📖</span><div className="l"><div className="v">Rules · §3.7</div><div className="s">Malus ferita</div></div></button>
              <button className="src-card"><span className="ico">📖</span><div className="l"><div className="v">Encounter · §271</div><div className="s">Voidstone</div></div></button>
            </div>
            <SuggestedActions actions={[
              { icon: '📕', label: 'Apri libro pag 42', onClick: () => {} },
              { icon: '🎲', label: 'Crea play record', onClick: () => {} },
            ]}/>
          </aside>
        </>
      )}
    </div>
  );
};

// ═══════════════════════════════════════════════════════════════════════
// SCREEN B11b-3 · ChatEmptyState (/chat/new, 3 quick-starter variants)
// ═══════════════════════════════════════════════════════════════════════
const ChatEmptyState = ({
  agent = AGENT_RUNA,    // null → default variant (no agent)
  mobile = false,
  onStartChat,           // callback su click quick-starter o picker
}) => {
  const wakeLockStatus = useWakeLock(false);

  // Quick-starter selection logic (sez 6 spec)
  // agent.metadata.category → libro-game | boardgame | default
  // Fallback graceful: agent senza category → default variant
  const starters = resolveQuickStarters(agent);
  const variantName = agent?.category || 'default';

  // No agent → Default variant (CTA picker dropdown)
  if (!agent || starters === null) {
    return (
      <div className={mobile ? 'phone' : 'desktop'} role="region" aria-label="Empty state — Default variant (no agent)">
        {mobile ? (
          <>
            <div className="phone-sbar"><span>21:08</span><span>📶 🔋 82%</span></div>
            <div className="mob-h">
              <button className="back">←</button>
              <div className="ti-block">
                <div className="ti">Nuova chat</div>
                <div className="sub">Default · nessun agent selezionato · fallback graceful</div>
              </div>
            </div>
          </>
        ) : (
          <div className="titlebar">
            <span className="dot r"></span><span className="dot y"></span><span className="dot g"></span>
            <span className="url">meepleai.app/chat/new</span>
          </div>
        )}
        <div className="empty-shell" style={mobile ? { padding: 'var(--s-7) var(--s-4)' } : {}}>
          <div className="hero-icon" aria-hidden="true">💬</div>
          <h2 style={mobile ? { fontSize: 'var(--fs-xl)' } : {}}>Scegli un agent</h2>
          <p className="subtitle">
            Non hai ancora selezionato un agent. Sceglilo dalla lista o crea una nuova chat dall'index agent.
          </p>
          <button className="agent-picker-trig" type="button" aria-label="Apri agent picker dropdown">
            <span>🧙</span><span>Scegli agent</span><span style={{ opacity: 0.7 }}>▾</span>
          </button>
          <div className="agent-picker-list" role="listbox" aria-label="Lista agent disponibili">
            {SAMPLE_AGENTS.map(a => (
              <button
                key={a.id}
                className="ag-item"
                role="option"
                aria-selected="false"
                onClick={() => onStartChat && onStartChat(a)}
              >
                <div
                  className="av"
                  style={a.category === 'libro-game'
                    ? { background: 'hsl(var(--c-kb) / 0.18)', color: 'hsl(var(--c-kb))' }
                    : { background: 'hsl(var(--c-game) / 0.18)', color: 'hsl(var(--c-game))' }}
                >{a.avatar}</div>
                <div className="info">
                  <div className="nm">{a.name}</div>
                  <div className="lu">{a.category} · last used {a.lastUsed}</div>
                </div>
              </button>
            ))}
          </div>
        </div>
        <TelemetryFooter streamState="idle" variant="default" wakeLockStatus={wakeLockStatus} />
      </div>
    );
  }

  // Agent presente → Libro-game o Boardgamer variant (quick-starter cards)
  const gridCls = mobile ? 'qs-grid' : 'qs-grid desk-1x4';

  if (mobile) {
    return (
      <div className="phone" role="region" aria-label={`Empty state — ${variantName} variant`}>
        <div className="phone-sbar"><span>21:05</span><span>📶 🔋 84%</span></div>
        <div className="mob-h">
          <button className="back">←</button>
          <div className="ti-block">
            <div className="ti">Nuova chat · {agent.name}</div>
            <div className="sub">{variantName === 'libro-game' ? 'Libro-game' : 'Boardgamer'} · meta.category={variantName}</div>
          </div>
          <div
            className="av"
            style={variantName === 'libro-game'
              ? { background: 'hsl(var(--c-kb) / 0.18)', color: 'hsl(var(--c-kb))' }
              : { background: 'hsl(var(--c-game) / 0.18)', color: 'hsl(var(--c-game))' }}
          >{agent.avatar}</div>
        </div>
        <div className="empty-shell" style={{ padding: 'var(--s-7) var(--s-4)' }}>
          <div className="hero-icon" aria-hidden="true">💬</div>
          <h2 style={{ fontSize: 'var(--fs-xl)' }}>
            {variantName === 'libro-game' ? 'Inizia una conversazione' : `Inizia con ${agent.name.replace('Mago di ', '')}`}
          </h2>
          <p className="subtitle">
            {variantName === 'libro-game'
              ? 'Chiedi al tuo agent una regola, una strategia, un riassunto o traduci un paragrafo del libro.'
              : 'Setup, regole edge case, strategie comuni o FAQ del gioco.'}
          </p>
          <div className={gridCls}>
            {starters.map((s, i) => (
              <QuickStarterCard
                key={i}
                icon={s.icon}
                cat={s.cat}
                question={s.q}
                variant={variantName}
                onClick={() => onStartChat && onStartChat({ agent, prompt: s.q })}
              />
            ))}
          </div>
        </div>
        <ChatComposer
          value=""
          onChange={() => {}}
          disabled={false}
          isStreaming={false}
          mobile
        />
        <TelemetryFooter streamState="idle" variant={variantName} quota="12/15" wakeLockStatus={wakeLockStatus} />
      </div>
    );
  }

  // Desktop empty state
  return (
    <div className="desktop" style={{ minHeight: 680 }} role="region" aria-label={`Empty state — ${variantName} variant (desktop)`}>
      <div className="titlebar">
        <span className="dot r"></span><span className="dot y"></span><span className="dot g"></span>
        <span className="url">meepleai.app/chat/new?agent={agent.id}</span>
      </div>
      <div className="chat-3col">
        <aside className="sidebar-l">
          <div className="sl-head">
            <div className="sl-tabs">
              <button className="tab active">Tutti</button>
              <button className="tab">Preferiti</button>
              <button className="tab">Archiviati</button>
            </div>
          </div>
          <button className="sl-cta"><span>＋</span><span>Nuova chat</span></button>
          <div className="sl-list">
            <div className="group-h">Recenti</div>
            <button className="sl-thread libro">
              <div className="av">📕</div>
              <div className="info"><div className="ti">Sera con i ragazzi · §147</div><div className="pv">…malus Reaver…</div></div>
              <div className="meta"><span className="ts">22:37</span></div>
            </button>
          </div>
        </aside>

        <section className="main-c">
          <div className="main-h">
            <div className="ag">{agent.avatar}</div>
            <div className="ti-block">
              <div className="ti">Nuova chat con {agent.name}</div>
              <div className="sub">Libro-game · agent.metadata.category=<code>{variantName}</code></div>
            </div>
          </div>
          <div className="empty-shell">
            <div className="hero-icon" aria-hidden="true">💬</div>
            <h2>Inizia una conversazione</h2>
            <p className="subtitle">
              Chiedi al tuo agent una regola, una strategia, un riassunto o traduci un paragrafo del libro.
            </p>
            <div className={gridCls}>
              {starters.map((s, i) => (
                <QuickStarterCard
                  key={i}
                  icon={s.icon}
                  cat={s.cat}
                  question={s.q}
                  variant={variantName}
                  onClick={() => onStartChat && onStartChat({ agent, prompt: s.q })}
                />
              ))}
            </div>
            <div className="cost-banner" style={{ marginTop: 'var(--s-5)' }}>
              <span className="ico">💸</span>
              <span>
                <span className="v">Cost-aware suggestion:</span> ogni quick-starter usa Tutor cache hit quando possibile
                (paragrafi già tradotti → 0 cost). Foto Encounter scatena flow translate Task 4 (~€0,01 per re-OCR).
              </span>
            </div>
          </div>
          <ChatComposer value="" onChange={() => {}} disabled={false} isStreaming={false} />
          <TelemetryFooter streamState="idle" variant={variantName} quota="12/15 free" wakeLockStatus={wakeLockStatus} />
        </section>

        <aside className="sidebar-r">
          <AgentInfoMiniCard agent={agent} />
        </aside>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════════════════════
// ROOT · demo all screens (Desktop 3-col 4 states + Mobile 3 states + Empty 3 variants)
// ═══════════════════════════════════════════════════════════════════════
const Root = () => {
  const [theme, setTheme] = useState('light');

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
  }, [theme]);

  return (
    <>
      <header className="top">
        <h1>Chat full-screen · B11b (chiusura parziale #491)</h1>
        <span className="route">/chat/[threadId] + /chat/new</span>
        <span className="spacer"></span>
        <button onClick={() => setTheme(t => t === 'dark' ? 'light' : 'dark')} aria-label="Cambia tema">🌗 Tema</button>
      </header>
      <main className="stage">

        <section className="state-section">
          <span className="label">
            Screen B11b-1 · Desktop 3-col · streaming
            <span className="cmp">ChatFullscreenDesktop</span>
          </span>
          <h2>Desktop 3-col · /chat/[threadId] · stream attivo (streaming state)</h2>
          <div className="frames">
            <ChatFullscreenDesktop streamState="streaming" />
          </div>
        </section>

        <section className="state-section">
          <span className="label">
            Screen B11b-1 · Desktop · stream-error
            <span className="cmp">ChatFullscreenDesktop · Nygard failure mode 3</span>
          </span>
          <h2>Desktop 3-col · stream-error · last user message preserved in composer</h2>
          <div className="frames">
            <ChatFullscreenDesktop streamState="stream-error" />
          </div>
        </section>

        <section className="state-section">
          <span className="label">
            Screen B11b-1 · Desktop · stream-abort
            <span className="cmp">ChatFullscreenDesktop · user cancel</span>
          </span>
          <h2>Desktop 3-col · stream-abort · message preserved con badge "[interrotto]"</h2>
          <div className="frames">
            <ChatFullscreenDesktop streamState="stream-abort" />
          </div>
        </section>

        <section className="state-section">
          <span className="label">
            Screen B11b-2 · Mobile single-pane
            <span className="cmp">ChatFullscreenMobile · 375px</span>
          </span>
          <h2>Mobile · /chat/[threadId] · 3 states (idle, streaming, bottom sheet)</h2>
          <div className="frames">
            <ChatFullscreenMobile streamState="idle" />
            <ChatFullscreenMobile streamState="streaming" />
            <ChatFullscreenMobile streamState="idle" showContextSheet={true} />
          </div>
        </section>

        <section className="state-section">
          <span className="label">
            Screen B11b-3 · Empty state /chat/new
            <span className="cmp">ChatEmptyState · 3 variants (libro-game · boardgame · default)</span>
          </span>
          <h2>Empty state /chat/new · 4 quick-starter cards · 3 agent type variants</h2>
          <div className="frames">
            {/* Variant 1: Libro-game (Aaron) desktop */}
            <ChatEmptyState agent={AGENT_RUNA} mobile={false} />
            {/* Variant 2: Boardgamer mobile */}
            <ChatEmptyState agent={AGENT_WINGSPAN} mobile={true} />
            {/* Variant 3: Default (no agent → CTA picker) mobile */}
            <ChatEmptyState agent={null} mobile={true} />
          </div>
        </section>

      </main>
    </>
  );
};

// Render root if React DOM available (mockup standalone)
if (typeof ReactDOM !== 'undefined') {
  const container = document.getElementById('root');
  if (container) {
    const root = ReactDOM.createRoot
      ? ReactDOM.createRoot(container)
      : null;
    if (root) {
      root.render(<Root />);
    } else if (ReactDOM.render) {
      ReactDOM.render(<Root />, container);
    }
  }
}

// Named exports per consumo programmatic (TypeScript-friendly)
window.ChatFullscreenMockup = {
  Root,
  ChatFullscreenDesktop,
  ChatFullscreenMobile,
  ChatEmptyState,
  // Sub-components
  ThreadList,
  ChatComposer,
  MessageBubble,
  CitationPill,
  AgentInfoMiniCard,
  SuggestedActions,
  QuickStarterCard,
  TelemetryFooter,
  StreamErrorBanner,
  // Hooks
  useReaderMode,
  useWakeLock,
  // Data
  THREADS,
  AGENT_RUNA,
  AGENT_WINGSPAN,
  QUICK_STARTERS,
  SAMPLE_AGENTS,
  resolveQuickStarters,
  READER_MODE_KEY,
};

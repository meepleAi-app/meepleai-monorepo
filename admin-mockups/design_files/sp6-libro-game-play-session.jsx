/* SP6 · Libro Game — Play Session
   Route: /gamebook/[gameId]/play
   Persona: Sara, 32, GM al tavolo, smartphone in mano (single-device GM master)
   Pattern: Tabbed mobile fullscreen / Split-view desktop (left 380 game-ctx, right flex)
   MVP Phase 1 — vision §6
   Stati Gherkin coperti: G3.1, G3.2, G3.3, G3.4, G3.5 + Tab Chat empty
                          G4 default, G4.4, G4.6, G4.7
                          G2 default, G2 generated, G2.3 unclear
   G4.7 mid-translation-lost = scope mockup D (translation viewer fullscreen),
   qui copriamo solo G4.7 offline-cached banner */

const { useState, useMemo } = React;

/* ═══════════════════════════════════════════════════════
   ENTITY HSL HELPER (replica inline — apps/web/src/lib/theme/entity-hsl.ts)
   ═══════════════════════════════════════════════════════ */
const ENTITY_HSL = {
  game:    '25 95% 45%',
  player:  '262 83% 58%',
  session: '240 60% 55%',
  agent:   '38 92% 50%',
  kb:      '174 60% 40%',
  chat:    '220 80% 55%',
  event:   '350 89% 60%',
  toolkit: '142 70% 45%',
  tool:    '195 80% 50%',
};
const ENTITY_EM = {
  game: '🎲', player: '👤', session: '🎯', agent: '🤖', kb: '📄',
  chat: '💬', event: '🎉', toolkit: '🧰', tool: '🔧',
};
const entityHsl = (entity, alpha) =>
  alpha != null ? `hsl(var(--c-${entity}) / ${alpha})` : `hsl(var(--c-${entity}))`;

/* ═══════════════════════════════════════════════════════
   ENTITY CHIP (sm) — replica produzione
   ═══════════════════════════════════════════════════════ */
const EntityChip = ({ entity, children, size = 'sm', icon, dashed }) => {
  const padding = size === 'sm' ? '3px 8px' : '5px 12px';
  const fs = size === 'sm' ? 11 : 12;
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 5,
      padding, borderRadius: 'var(--r-pill)',
      background: dashed ? 'transparent' : entityHsl(entity, 0.12),
      color: entityHsl(entity),
      border: dashed
        ? `1.5px dashed ${entityHsl(entity, 0.5)}`
        : `1px solid ${entityHsl(entity, 0.2)}`,
      fontFamily: 'var(--f-display)', fontSize: fs, fontWeight: 700,
      whiteSpace: 'nowrap', lineHeight: 1.2,
    }}>
      <span aria-hidden="true" style={{ fontSize: fs + 1 }}>{icon || ENTITY_EM[entity]}</span>
      {children}
    </span>
  );
};

/* ═══════════════════════════════════════════════════════
   CONNECTIONBAR (1:1 prod, lezione PR #568/#569/#570)
   - borderRadius 999 (pill)
   - bg pieno entityHsl(.1) non-empty / dashed + opacity 0.6 + Plus icon empty
   - aria-label `${label}: ${count}` non-empty / `${label}` empty
   - tabular-nums per count
   ═══════════════════════════════════════════════════════ */
const ConnectionBar = ({ connections, dense }) => (
  <div className="mai-noscroll" style={{
    display: 'flex', alignItems: 'center', gap: 6,
    overflowX: 'auto', padding: dense ? '4px 0' : '8px 0',
  }}>
    {connections.map(c => {
      const isEmpty = c.isEmpty || c.count === 0;
      const ariaLabel = isEmpty ? c.label : `${c.label}: ${c.count}`;
      return (
        <button
          key={c.entityType}
          type="button"
          aria-label={ariaLabel}
          style={{
            display: 'inline-flex', alignItems: 'center', gap: 5,
            padding: '5px 10px', borderRadius: 999,
            fontSize: 11, fontWeight: 700, fontFamily: 'var(--f-display)',
            background: isEmpty ? 'transparent' : entityHsl(c.entityType, 0.1),
            color: entityHsl(c.entityType),
            border: isEmpty
              ? `1.5px dashed ${entityHsl(c.entityType, 0.5)}`
              : `1px solid ${entityHsl(c.entityType, 0.2)}`,
            opacity: isEmpty ? 0.6 : 1,
            cursor: 'pointer', flexShrink: 0, whiteSpace: 'nowrap',
          }}
        >
          <span aria-hidden="true" style={{ fontSize: 12, lineHeight: 1 }}>{ENTITY_EM[c.entityType]}</span>
          {isEmpty ? (
            <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                 strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <line x1="12" y1="5" x2="12" y2="19"/>
              <line x1="5" y1="12" x2="19" y2="12"/>
            </svg>
          ) : (
            <span style={{ fontFamily: 'var(--f-mono)', fontVariantNumeric: 'tabular-nums', fontWeight: 700 }}>
              {c.count}
            </span>
          )}
          <span>{c.label}</span>
        </button>
      );
    })}
  </div>
);

const SESSION_CONNECTIONS = [
  { entityType: 'kb',      count: 142, label: 'pagine',    isEmpty: false },
  { entityType: 'chat',    count: 8,   label: 'domande',   isEmpty: false },
  { entityType: 'agent',   count: 2,   label: 'house rules', isEmpty: false },
  { entityType: 'tool',    count: 0,   label: 'setup',     isEmpty: true  },
  { entityType: 'player',  count: 4,   label: 'giocatori', isEmpty: false },
  { entityType: 'session', count: 1,   label: 'sessione',  isEmpty: false },
];

/* ═══════════════════════════════════════════════════════
   PHONE FRAME (status bar + screen body)
   ═══════════════════════════════════════════════════════ */
const Phone = ({ children }) => (
  <div className="phone-sp6">
    <div className="phone-sbar-sp6">
      <span>21:47</span>
      <span className="ind"><span>📶</span><span>📡</span><span>78%</span></span>
    </div>
    <div className="phone-screen">{children}</div>
  </div>
);

/* ═══════════════════════════════════════════════════════
   SESSION HEADER (sticky)
   ═══════════════════════════════════════════════════════ */
const SessionHeader = ({ compact }) => (
  <div style={{
    padding: compact ? '10px 14px 8px' : '12px 18px 10px',
    background: 'var(--bg)',
    borderBottom: '1px solid var(--border-light)',
    flexShrink: 0,
  }}>
    <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6 }}>
      {/* game cover mini */}
      <div style={{
        width: 38, height: 46, borderRadius: 'var(--r-sm)',
        background: `linear-gradient(135deg, ${entityHsl('game', 0.85)}, ${entityHsl('event', 0.7)})`,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 18, flexShrink: 0,
        boxShadow: 'var(--shadow-xs)',
      }} aria-hidden="true">🗡️</div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <h1 style={{
          fontFamily: 'var(--f-display)', fontWeight: 800,
          fontSize: 18, letterSpacing: '-0.01em', lineHeight: 1.15,
          color: 'var(--text)', margin: '0 0 2px',
          overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
        }}>Tainted Grail</h1>
        <div style={{
          display: 'inline-flex', alignItems: 'center', gap: 5,
          fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
          color: entityHsl('session'), textTransform: 'uppercase', letterSpacing: '0.06em',
        }}>
          <span aria-hidden="true" style={{
            display: 'inline-block', width: 7, height: 7, borderRadius: '50%',
            background: entityHsl('session'),
            animation: 'mai-pulse-session 1.6s ease-in-out infinite',
          }} className="mai-pulse"/>
          🎯 In corso · 47 min
        </div>
      </div>
      <button type="button" aria-label="Menu sessione" style={{
        width: 36, height: 36, borderRadius: 'var(--r-md)',
        background: 'transparent', border: '1px solid var(--border)',
        color: 'var(--text-sec)', fontSize: 16, cursor: 'pointer',
        flexShrink: 0,
      }}>⋯</button>
    </div>
    <ConnectionBar connections={SESSION_CONNECTIONS} dense/>
  </div>
);

/* ═══════════════════════════════════════════════════════
   TABS NAV (animated underline, riuso wave 1)
   ═══════════════════════════════════════════════════════ */
const TABS = [
  { id: 'chat',      em: '💬', label: 'Chat regole', entity: 'chat' },
  { id: 'paragraph', em: '📖', label: 'Paragrafo',   entity: 'kb' },
  { id: 'setup',     em: '🎲', label: 'Setup',       entity: 'tool' },
];
const TabsNav = ({ active }) => (
  <div role="tablist" aria-label="Sezioni sessione" style={{
    display: 'flex', position: 'relative',
    borderBottom: '1px solid var(--border)',
    background: 'var(--bg)', flexShrink: 0,
  }}>
    {TABS.map((t, i) => {
      const isActive = t.id === active;
      return (
        <button key={t.id} role="tab" aria-selected={isActive}
          type="button"
          style={{
            flex: 1, padding: '11px 6px',
            background: 'transparent', border: 'none',
            borderBottom: isActive
              ? `2px solid ${entityHsl(t.entity)}`
              : '2px solid transparent',
            color: isActive ? entityHsl(t.entity) : 'var(--text-sec)',
            fontFamily: 'var(--f-display)',
            fontSize: 13, fontWeight: isActive ? 800 : 600,
            cursor: 'pointer',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 6,
            marginBottom: '-1px',
            transition: 'color var(--dur-sm)',
          }}>
          <span aria-hidden="true">{t.em}</span>{t.label}
        </button>
      );
    })}
  </div>
);

/* ═══════════════════════════════════════════════════════
   SESSION BAR (mobile, sostituisce BottomBar)
   ═══════════════════════════════════════════════════════ */
const SessionBar = ({ activeTab }) => (
  <div style={{
    flexShrink: 0,
    padding: '10px 14px 14px',
    borderTop: '1px solid var(--border-light)',
    background: `linear-gradient(180deg, ${entityHsl('session', 0.06)}, ${entityHsl('session', 0.12)})`,
    display: 'flex', alignItems: 'center', gap: 6,
  }}>
    <span aria-hidden="true" style={{
      display: 'inline-block', width: 8, height: 8, borderRadius: '50%',
      background: entityHsl('session'),
      animation: 'mai-pulse-session 1.6s ease-in-out infinite',
      marginRight: 4, flexShrink: 0,
    }} className="mai-pulse"/>
    {[
      { id: 'chat',  em: '💬', label: 'Chat',  entity: 'chat' },
      { id: 'paragraph', em: '📖', label: '§147', entity: 'kb' },
      { id: 'setup', em: '🎲', label: 'Setup', entity: 'tool' },
    ].map(b => {
      const isActive = b.id === activeTab;
      return (
        <button key={b.id} type="button" style={{
          flex: 1, padding: '8px 4px', borderRadius: 'var(--r-md)',
          background: isActive ? entityHsl(b.entity, 0.14) : 'transparent',
          border: 'none',
          color: isActive ? entityHsl(b.entity) : 'var(--text-sec)',
          fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 700,
          display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 2,
          cursor: 'pointer',
        }}>
          <span aria-hidden="true" style={{ fontSize: 14 }}>{b.em}</span>
          {b.label}
        </button>
      );
    })}
    <button type="button" aria-label="Pausa sessione" style={{
      width: 40, height: 40, borderRadius: 'var(--r-md)',
      background: 'transparent', border: '1px solid var(--border)',
      color: 'var(--text-sec)', fontSize: 14, cursor: 'pointer',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      flexShrink: 0,
    }}>⏸</button>
  </div>
);

/* ═══════════════════════════════════════════════════════
   CHAT BUBBLES
   ═══════════════════════════════════════════════════════ */
const ConfidenceBadge = ({ level }) => {
  const map = {
    high: { color: 'toolkit', label: 'Alta', em: '🟢', value: '0.92' },
    med:  { color: 'agent',   label: 'Media', em: '🟡', value: '0.62' },
    low:  { color: 'event',   label: 'Bassa', em: '🔴', value: '0.42' },
  };
  const c = map[level];
  return (
    <span title={`Confidence ${c.value}`} style={{
      display: 'inline-flex', alignItems: 'center', gap: 4,
      padding: '2px 7px', borderRadius: 'var(--r-pill)',
      background: entityHsl(c.color, 0.12),
      color: entityHsl(c.color),
      fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
      letterSpacing: '0.04em',
    }}>
      <span aria-hidden="true">{c.em}</span>{c.label}
    </span>
  );
};

const UserBubble = ({ children, who = 'Sara' }) => (
  <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 12 }}>
    <div style={{ maxWidth: '80%', display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 3 }}>
      <span style={{
        fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 700,
        color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.06em',
      }}>{who}</span>
      <div style={{
        background: entityHsl('chat'),
        color: '#fff',
        padding: '10px 14px',
        borderRadius: '14px 14px 4px 14px',
        fontSize: 14, fontWeight: 500, lineHeight: 1.45,
      }}>
        {children}
      </div>
    </div>
  </div>
);

const AgentBubble = ({ children, footer, confidence, actions, badge }) => (
  <div style={{ display: 'flex', alignItems: 'flex-start', gap: 8, marginBottom: 14 }}>
    <div style={{
      width: 28, height: 28, borderRadius: '50%', flexShrink: 0,
      background: entityHsl('agent', 0.18),
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontSize: 14, marginTop: 16,
    }} aria-hidden="true">🤖</div>
    <div style={{ flex: 1, minWidth: 0 }}>
      <div style={{
        display: 'flex', alignItems: 'center', gap: 6, marginBottom: 3,
      }}>
        <span style={{
          fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 700,
          color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.06em',
        }}>Agente regole</span>
        {confidence && <ConfidenceBadge level={confidence}/>}
        {badge}
      </div>
      <div style={{
        background: entityHsl('agent', 0.08),
        border: `1px solid ${entityHsl('agent', 0.16)}`,
        padding: '10px 14px',
        borderRadius: '14px 14px 14px 4px',
        fontSize: 14, lineHeight: 1.55,
        color: 'var(--text)',
      }}>
        {children}
        {footer && (
          <div style={{
            marginTop: 10, paddingTop: 8,
            borderTop: `1px solid ${entityHsl('agent', 0.18)}`,
            display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: 6,
          }}>{footer}</div>
        )}
      </div>
      {actions && (
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginTop: 6 }}>
          {actions}
        </div>
      )}
    </div>
  </div>
);

const ActionChip = ({ icon, children, primary }) => (
  <button type="button" style={{
    display: 'inline-flex', alignItems: 'center', gap: 4,
    padding: '5px 9px', borderRadius: 'var(--r-pill)',
    background: primary ? entityHsl('agent', 0.16) : 'var(--bg-card)',
    border: primary ? `1px solid ${entityHsl('agent', 0.4)}` : '1px solid var(--border)',
    color: primary ? entityHsl('agent') : 'var(--text-sec)',
    fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 700,
    cursor: 'pointer', whiteSpace: 'nowrap',
  }}>
    {icon && <span aria-hidden="true">{icon}</span>}
    {children}
  </button>
);

const ChatInput = ({ placeholder = 'Chiedi qualcosa sulle regole…' }) => (
  <div style={{
    flexShrink: 0, padding: '8px 12px',
    borderTop: '1px solid var(--border-light)',
    background: 'var(--bg)',
    display: 'flex', alignItems: 'center', gap: 6,
  }}>
    <div style={{
      flex: 1,
      padding: '8px 12px',
      background: 'var(--bg-muted)',
      borderRadius: 'var(--r-pill)',
      fontSize: 13, fontFamily: 'var(--f-body)',
      color: 'var(--text-muted)',
      display: 'flex', alignItems: 'center', gap: 8,
    }}>
      <span style={{ flex: 1 }}>{placeholder}</span>
      <button type="button" aria-label="Voice input" style={{
        background: 'transparent', border: 'none', color: 'var(--text-sec)',
        fontSize: 14, cursor: 'pointer', padding: 0,
      }}>🎙</button>
    </div>
    <button type="button" aria-label="Invia" style={{
      width: 40, height: 40, borderRadius: '50%',
      background: entityHsl('chat'), color: '#fff', border: 'none',
      fontSize: 14, cursor: 'pointer', flexShrink: 0,
      boxShadow: `0 4px 12px ${entityHsl('chat', 0.4)}`,
    }}>↑</button>
  </div>
);

/* ═══════════════════════════════════════════════════════
   TAB CHAT — 6 stati
   ═══════════════════════════════════════════════════════ */
const ChatBody = ({ children }) => (
  <div style={{
    flex: 1, overflowY: 'auto', padding: '14px 14px 8px',
    background: 'var(--bg)',
    display: 'flex', flexDirection: 'column',
  }}>{children}</div>
);

const ChatStateDefault = () => (
  <ChatBody>
    <UserBubble who="Marco">
      Quanti dadi tira il difensore in combattimento marziale?
    </UserBubble>
    <AgentBubble
      confidence="high"
      footer={<EntityChip entity="kb" size="sm">p.18 — Combattimento Marziale</EntityChip>}
      actions={<>
        <ActionChip icon="👍">Utile</ActionChip>
        <ActionChip icon="👎">Non chiara</ActionChip>
        <ActionChip icon="🔁">Rigenera</ActionChip>
        <ActionChip icon="📋">Copia</ActionChip>
      </>}>
      Il difensore tira <strong>2 dadi rossi</strong>: il valore più alto è il successo.
      Se è in posizione di guardia (token Guard attivo), aggiunge un dado bianco.
      Vince il duello chi totalizza il valore più alto — pareggio = entrambi feriti.
    </AgentBubble>
    <UserBubble>I critici fanno doppio danno?</UserBubble>
    <AgentBubble
      confidence="high"
      footer={<EntityChip entity="kb" size="sm">p.22 — Colpi critici</EntityChip>}>
      Sì. Quando ottieni un <strong>6 naturale</strong> il danno base viene moltiplicato per 1.5
      (arrotondato per eccesso). Esempio: 4 danno → 6 danno con critico.
    </AgentBubble>
  </ChatBody>
);

const ChatStateAmbiguity = () => (
  <ChatBody>
    <UserBubble>Cosa succede quando si pareggia?</UserBubble>
    <AgentBubble confidence="med">
      La tua domanda può riferirsi a <strong>4 contesti diversi</strong> nel manuale.
      Quale ti interessa?
      <div style={{
        marginTop: 10, display: 'flex', flexWrap: 'wrap', gap: 6,
      }}>
        <EntityChip entity="kb" size="sm">⚔️ Combattimento</EntityChip>
        <EntityChip entity="kb" size="sm">⏱️ Iniziativa</EntityChip>
        <EntityChip entity="kb" size="sm">🗳️ Voto del gruppo</EntityChip>
        <EntityChip entity="kb" size="sm">🧭 Esplorazione</EntityChip>
      </div>
    </AgentBubble>
  </ChatBody>
);

const ChatStateLowConfidence = () => (
  <ChatBody>
    <UserBubble who="Marco">
      Se due giocatori muoiono nello stesso turno, chi diventa il leader?
    </UserBubble>
    <AgentBubble
      confidence="low"
      footer={<EntityChip entity="kb" size="sm">p.34 — Stato del gruppo</EntityChip>}
      actions={<>
        <ActionChip icon="📷">Rifotografa pagina</ActionChip>
        <ActionChip icon="🤝" primary>Definisci house rule</ActionChip>
        <ActionChip icon="🔍">Cerca su BGG</ActionChip>
      </>}>
      <strong>Non sono sicuro</strong> — questa regola non è chiara dal manuale.
      Il testo a p.34 menziona la successione del leader ma non specifica il caso
      di morte simultanea. Confidence bassa (0.42).
    </AgentBubble>
  </ChatBody>
);

const ChatStateHouseRule = () => (
  <ChatBody>
    <UserBubble>I critici fanno doppio danno o +50%?</UserBubble>
    <AgentBubble
      confidence="high"
      badge={<EntityChip entity="agent" size="sm">House rule attiva</EntityChip>}
      footer={
        <span style={{
          fontSize: 11, color: 'var(--text-muted)',
          fontFamily: 'var(--f-body)', fontStyle: 'italic',
          display: 'inline-flex', alignItems: 'center', gap: 4, flexWrap: 'wrap',
        }}>
          📖 Regola ufficiale manuale: +50% danno
          <EntityChip entity="kb" size="sm">p.22</EntityChip>
          — ma state usando la vostra variante
        </span>
      }>
      Per il vostro gruppo: <strong>doppio danno</strong> sui critici
      (house rule definita 14 min fa da Sara).
    </AgentBubble>
  </ChatBody>
);

const ChatStateTimeout = () => (
  <ChatBody>
    <UserBubble who="Giulia">
      Come si risolve un test di Resistenza con vantaggio?
    </UserBubble>
    <AgentBubble>
      <div style={{
        display: 'inline-flex', alignItems: 'center', gap: 8,
        color: entityHsl('agent'),
        fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 13,
      }}>
        <span style={{ display: 'inline-flex', gap: 3 }}>
          {[0, 1, 2].map(i => (
            <span key={i} aria-hidden="true" className="mai-typing-dot"
              style={{
                width: 7, height: 7, borderRadius: '50%',
                background: entityHsl('agent'),
                display: 'inline-block',
                animationDelay: `${i * 0.15}s`,
              }}/>
          ))}
        </span>
        🟡 Sto pensando, ci vuole più del solito…
      </div>
      <div style={{
        marginTop: 8, fontSize: 12, color: 'var(--text-muted)',
        fontFamily: 'var(--f-mono)',
      }}>8s · connessione lenta</div>
    </AgentBubble>
    <AgentBubble
      actions={<>
        <ActionChip icon="📖" primary>Mostrami le pagine sul tema "Resistenza"</ActionChip>
        <ActionChip icon="🔁">Riprova</ActionChip>
      </>}>
      <strong>Connessione lenta</strong> — passa a modalità manuale: ti mostro le pagine
      che parlano del tema così leggi tu mentre ripristiniamo la connessione.
    </AgentBubble>
  </ChatBody>
);

const ChatStateEmpty = () => (
  <ChatBody>
    <div style={{
      flex: 1, display: 'flex', flexDirection: 'column',
      alignItems: 'center', justifyContent: 'center',
      textAlign: 'center', padding: '40px 20px', gap: 14,
    }}>
      <div style={{
        width: 88, height: 88, borderRadius: 'var(--r-2xl)',
        background: `linear-gradient(135deg, ${entityHsl('chat', 0.18)}, ${entityHsl('agent', 0.16)})`,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 38,
      }} aria-hidden="true">💬</div>
      <div>
        <h3 style={{
          fontFamily: 'var(--f-display)', fontSize: 18, fontWeight: 800,
          margin: '0 0 6px', color: 'var(--text)',
        }}>Prima domanda della sessione</h3>
        <p style={{
          fontSize: 13, color: 'var(--text-sec)',
          margin: 0, lineHeight: 1.5, maxWidth: '28ch',
        }}>
          Chiedi qualcosa sulle regole. Ti rispondo citando la pagina del manuale.
        </p>
      </div>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, justifyContent: 'center', maxWidth: 280 }}>
        {['Come funziona il combattimento?', 'Setup per 4 giocatori', 'Cos\'è il Wraithstone?'].map(s => (
          <button key={s} type="button" style={{
            padding: '6px 11px', borderRadius: 'var(--r-pill)',
            background: 'var(--bg-card)', border: '1px solid var(--border)',
            color: 'var(--text-sec)',
            fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 700,
            cursor: 'pointer',
          }}>{s}</button>
        ))}
      </div>
    </div>
  </ChatBody>
);

/* ═══════════════════════════════════════════════════════
   TAB PARAGRAFO — 4 stati
   ═══════════════════════════════════════════════════════ */
const ParagraphToolbar = ({ active = '147', mode = 'IT' }) => (
  <div style={{
    flexShrink: 0, padding: '10px 14px',
    background: 'var(--bg)',
    borderBottom: '1px solid var(--border-light)',
    display: 'flex', alignItems: 'center', gap: 8,
  }}>
    <div style={{
      display: 'inline-flex', alignItems: 'center', gap: 6,
      padding: '7px 12px', borderRadius: 'var(--r-md)',
      background: 'var(--bg-muted)',
      flex: 1, minWidth: 0,
    }}>
      <span style={{
        fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700,
        color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.06em',
      }}>Vai al §</span>
      <span style={{
        fontFamily: 'var(--f-mono)', fontSize: 18, fontWeight: 700,
        color: entityHsl('kb'),
      }}>{active}</span>
    </div>
    <div role="tablist" aria-label="Lingua" style={{
      display: 'inline-flex', padding: 2, borderRadius: 'var(--r-md)',
      background: 'var(--bg-muted)',
    }}>
      {['IT', 'EN'].map(m => (
        <button key={m} type="button" role="tab" aria-selected={m === mode} style={{
          padding: '6px 10px', borderRadius: 'var(--r-sm)',
          background: m === mode ? 'var(--bg-card)' : 'transparent',
          border: 'none', color: m === mode ? entityHsl('kb') : 'var(--text-sec)',
          fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 700,
          cursor: 'pointer',
          boxShadow: m === mode ? 'var(--shadow-xs)' : 'none',
        }}>{m}</button>
      ))}
    </div>
  </div>
);

const ParagraphHeader = ({ num, chapter }) => (
  <div style={{ marginBottom: 18 }}>
    <div style={{
      fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 600,
      color: 'var(--text-muted)', marginBottom: 4,
    }}>{chapter}</div>
    <div style={{
      fontFamily: 'var(--f-mono)', fontSize: 36, fontWeight: 700,
      color: entityHsl('kb'), letterSpacing: '-0.01em', lineHeight: 1,
    }}>§{num}</div>
  </div>
);

const ParagraphStateDefault = () => (
  <>
    <ParagraphToolbar active="147" mode="IT"/>
    <div style={{ flex: 1, overflowY: 'auto', padding: '18px 18px 14px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8, marginBottom: 4 }}>
        <ParagraphHeader num="147" chapter="Capitolo VII — Le brughiere di Avalon"/>
        <EntityChip entity="kb" size="sm" icon="📦">Cached</EntityChip>
      </div>
      <div className="reading-body">
        <p>
          La nebbia si dirada e davanti a te si apre la torbiera silenziosa. <strong>Niamh</strong> stringe
          la <strong>Spada di Avalon</strong> — la lama riflette un bagliore che non viene da nessuna luna.
        </p>
        <p className="dialogue">
          «Senti anche tu? Qualcosa ci osserva da quelle pietre.»
        </p>
        <p>
          Una <strong>Pietra Spettrale</strong> emerge dal fango: la sua superficie è incisa con simboli
          che bruciano gli occhi se fissati troppo a lungo. Tira un dado di Volontà.
        </p>
      </div>
      <div style={{
        marginTop: 16, fontSize: 11, color: 'var(--text-muted)',
        fontStyle: 'italic', fontFamily: 'var(--f-body)',
      }}>
        Glossario applicato: Spada di Avalon, Pietra Spettrale, Niamh
      </div>
    </div>
    <div style={{
      flexShrink: 0, padding: '10px 14px',
      borderTop: '1px solid var(--border-light)',
      display: 'flex', gap: 6, alignItems: 'center',
    }}>
      <button type="button" style={{
        padding: '8px 10px', borderRadius: 'var(--r-md)',
        background: 'transparent', border: '1px solid var(--border)',
        color: 'var(--text-sec)', fontFamily: 'var(--f-display)',
        fontSize: 12, fontWeight: 700, cursor: 'pointer',
      }} onClick={() => { setTimeout(() => { window.location.href = 'librogame-runthrough-game-detail.html'; }, 0); /* DEMO-NAV */ }}>← § prec.</button>
      <button type="button" style={{
        flex: 1, padding: '8px 10px', borderRadius: 'var(--r-md)',
        background: entityHsl('kb', 0.1),
        border: `1px solid ${entityHsl('kb', 0.3)}`,
        color: entityHsl('kb'),
        fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 700,
        cursor: 'pointer',
      }}>Letto, vai al prossimo →</button>
      <button type="button" aria-label="Fullscreen" style={{
        width: 38, height: 38, borderRadius: 'var(--r-md)',
        background: 'transparent', border: '1px solid var(--border)',
        color: 'var(--text-sec)', fontSize: 14, cursor: 'pointer',
      }}>⛶</button>
    </div>
  </>
);

const ParagraphStateNotFound = () => (
  <>
    <ParagraphToolbar active="299" mode="IT"/>
    <div style={{ flex: 1, overflowY: 'auto', padding: '20px 18px', display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{
        display: 'flex', alignItems: 'flex-start', gap: 10,
        padding: '14px',
        borderRadius: 'var(--r-lg)',
        background: entityHsl('event', 0.08),
        border: `1px solid ${entityHsl('event', 0.24)}`,
      }}>
        <span aria-hidden="true" style={{ fontSize: 22, lineHeight: 1, marginTop: 2 }}>🔴</span>
        <div>
          <h3 style={{
            fontFamily: 'var(--f-display)', fontSize: 15, fontWeight: 800,
            margin: '0 0 4px', color: 'var(--text)',
          }}>Paragrafo §299 non trovato</h3>
          <p style={{ margin: 0, fontSize: 13, color: 'var(--text-sec)', lineHeight: 1.5 }}>
            Non è presente nel manuale indicizzato. Forse hai saltato qualche pagina
            durante l'acquisizione.
          </p>
        </div>
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {[
          { em: '🔍', label: 'Cerca testo simile', sub: 'Mostra paragrafi vicini §290-§300' },
          { em: '📷', label: 'Rifotografa pagine mancanti', sub: 'Apri photo-upload con pre-filtro' },
          { em: '⏭️', label: 'Salta paragrafo', sub: 'Vai a §300 (prossimo indicizzato)' },
        ].map(a => (
          <button key={a.label} type="button" style={{
            display: 'flex', alignItems: 'center', gap: 12,
            padding: '12px 14px', borderRadius: 'var(--r-md)',
            background: 'var(--bg-card)', border: '1px solid var(--border)',
            color: 'var(--text)', textAlign: 'left', cursor: 'pointer',
          }}>
            <span aria-hidden="true" style={{ fontSize: 22, flexShrink: 0 }}>{a.em}</span>
            <div style={{ flex: 1 }}>
              <div style={{ fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 700 }}>{a.label}</div>
              <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>{a.sub}</div>
            </div>
            <span aria-hidden="true" style={{ color: 'var(--text-muted)' }}>›</span>
          </button>
        ))}
      </div>
    </div>
  </>
);

const ParagraphStateOffline = () => (
  <>
    <ParagraphToolbar active="147" mode="IT"/>
    <div style={{
      flexShrink: 0, padding: '8px 14px',
      background: entityHsl('agent', 0.12),
      borderBottom: `1px solid ${entityHsl('agent', 0.24)}`,
      display: 'flex', alignItems: 'center', gap: 8,
      fontSize: 12, color: entityHsl('agent'),
      fontFamily: 'var(--f-display)', fontWeight: 700,
    }}>
      <span aria-hidden="true">🟡</span>
      <span style={{ flex: 1 }}>Connessione persa — paragrafo originale disponibile</span>
      <button type="button" style={{
        padding: '4px 9px', borderRadius: 'var(--r-sm)',
        background: 'var(--bg-card)',
        border: `1px solid ${entityHsl('agent', 0.4)}`,
        color: entityHsl('agent'),
        fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 700,
        cursor: 'pointer',
      }}>Mostra EN</button>
    </div>
    <div style={{ flex: 1, overflowY: 'auto', padding: '18px 18px 14px' }}>
      <ParagraphHeader num="147" chapter="Chapter VII — The moors of Avalon"/>
      <div className="reading-body" style={{ opacity: 0.85 }}>
        <p>
          The mist parts and the silent fen opens before you. <strong>Niamh</strong> grips
          the <strong>Sword of Avalon</strong> — the blade reflects a glow that comes from no moon.
        </p>
        <p className="dialogue">
          «Do you feel it too? Something watches us from those stones.»
        </p>
        <p>
          A <strong>Wraithstone</strong> rises from the mud, etched with symbols that
          burn the eye if held too long.
        </p>
      </div>
      <div style={{
        marginTop: 16, fontSize: 11, color: 'var(--text-muted)',
        fontStyle: 'italic', fontFamily: 'var(--f-body)',
      }}>
        🔄 Auto-retry traduzione fra 8s…
      </div>
    </div>
  </>
);

const ParagraphStateQuota = () => (
  <>
    <ParagraphToolbar active="147" mode="IT"/>
    <div style={{ flex: 1, overflowY: 'auto', padding: '18px 18px', display: 'flex', flexDirection: 'column', gap: 14 }}>
      <ParagraphHeader num="147" chapter="Capitolo VII — Le brughiere di Avalon"/>
      <div style={{
        padding: 18, borderRadius: 'var(--r-xl)',
        background: `linear-gradient(135deg, ${entityHsl('event', 0.12)}, ${entityHsl('toolkit', 0.06)})`,
        border: `1px solid ${entityHsl('event', 0.24)}`,
        textAlign: 'center',
      }}>
        <div aria-hidden="true" style={{ fontSize: 38, marginBottom: 8 }}>💎</div>
        <h3 style={{
          fontFamily: 'var(--f-display)', fontSize: 17, fontWeight: 800,
          margin: '0 0 6px', color: 'var(--text)',
        }}>Hai esaurito la quota gratuita</h3>
        <p style={{
          margin: '0 0 14px', fontSize: 13, color: 'var(--text-sec)', lineHeight: 1.5,
        }}>
          50/50 paragrafi tradotti questo mese. Si resetta il 1° giugno (12 giorni).
        </p>
        <div style={{
          width: '100%', height: 6, borderRadius: 'var(--r-pill)',
          background: 'var(--bg-muted)', overflow: 'hidden',
          marginBottom: 14,
        }}>
          <div style={{
            width: '100%', height: '100%',
            background: entityHsl('event', 0.7),
          }}/>
        </div>
        <button type="button" style={{
          width: '100%', padding: '11px 14px', borderRadius: 'var(--r-md)',
          background: entityHsl('event'),
          color: '#fff', border: 'none',
          fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800,
          cursor: 'pointer',
          boxShadow: `0 6px 18px ${entityHsl('event', 0.4)}`,
        }}>💎 Acquista crediti — €5 per 100</button>
        <button type="button" style={{
          marginTop: 8, width: '100%', padding: '9px 14px',
          background: 'transparent', border: '1px solid var(--border)',
          color: 'var(--text-sec)', borderRadius: 'var(--r-md)',
          fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 700,
          cursor: 'pointer',
        }} onClick={() => { setTimeout(() => { window.location.href = 'librogame-runthrough-translate-viewer.html'; }, 0); /* DEMO-NAV */ }}>⏸️ Continua senza traduzione</button>
      </div>
    </div>
  </>
);

/* ─── Skeleton row reusable (rispetta prefers-reduced-motion via .mai-shimmer guard) ─── */
const SkeletonRow = ({ width = '100%' }) => (
  <div className="mai-shimmer" style={{
    height: 18, width, borderRadius: 'var(--r-sm)',
    background: 'var(--bg-muted)', marginBottom: 10,
  }}/>
);

const ParagraphStateEmpty = () => (
  <>
    <ParagraphToolbar active="—" mode="IT"/>
    <div style={{
      flex: 1, display: 'flex', flexDirection: 'column',
      alignItems: 'center', justifyContent: 'center',
      textAlign: 'center', padding: '40px 22px', gap: 14,
    }}>
      <div aria-hidden="true" style={{ fontSize: 64, opacity: 0.4, lineHeight: 1 }}>📖</div>
      <h3 style={{
        fontFamily: 'var(--f-display)', fontSize: 17, fontWeight: 800,
        margin: 0, color: 'var(--text)', maxWidth: '24ch', lineHeight: 1.25,
      }}>Inserisci un numero § o cerca un testo per iniziare</h3>
      <div style={{ fontSize: 12, color: 'var(--text-muted)', fontFamily: 'var(--f-body)' }}>
        Esempio: §147 oppure "combattimento marziale"
      </div>
    </div>
    <div style={{
      flexShrink: 0, padding: '10px 14px',
      borderTop: '1px solid var(--border-light)',
      display: 'flex', gap: 6, alignItems: 'center', opacity: 0.4,
      pointerEvents: 'none',
    }}>
      <button type="button" disabled style={{
        padding: '8px 10px', borderRadius: 'var(--r-md)',
        background: 'transparent', border: '1px solid var(--border)',
        color: 'var(--text-sec)', fontFamily: 'var(--f-display)',
        fontSize: 12, fontWeight: 700,
      }} onClick={() => { setTimeout(() => { window.location.href = 'librogame-runthrough-game-detail.html'; }, 0); /* DEMO-NAV */ }}>← § prec.</button>
      <button type="button" disabled style={{
        flex: 1, padding: '8px 10px', borderRadius: 'var(--r-md)',
        background: 'transparent', border: '1px solid var(--border)',
        color: 'var(--text-sec)',
        fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 700,
      }}>A− A A+</button>
      <button type="button" disabled aria-label="Fullscreen" style={{
        width: 38, height: 38, borderRadius: 'var(--r-md)',
        background: 'transparent', border: '1px solid var(--border)',
        color: 'var(--text-sec)', fontSize: 14,
      }}>⛶</button>
    </div>
  </>
);

const ParagraphStateLoading = () => (
  <>
    <ParagraphToolbar active="147" mode="IT"/>
    <div style={{ flex: 1, overflowY: 'auto', padding: '18px 18px 14px' }}>
      <ParagraphHeader num="147" chapter="Capitolo VII — Le brughiere di Avalon"/>
      <div style={{ marginTop: 6 }}>
        <SkeletonRow width="100%"/>
        <SkeletonRow width="92%"/>
        <SkeletonRow width="78%"/>
      </div>
    </div>
    <div style={{
      flexShrink: 0, padding: '10px 14px',
      borderTop: '1px solid var(--border-light)',
      display: 'flex', gap: 6, alignItems: 'center', opacity: 0.4,
      pointerEvents: 'none',
    }}>
      <button type="button" disabled style={{
        padding: '8px 10px', borderRadius: 'var(--r-md)',
        background: 'transparent', border: '1px solid var(--border)',
        color: 'var(--text-sec)', fontFamily: 'var(--f-display)',
        fontSize: 12, fontWeight: 700,
      }} onClick={() => { setTimeout(() => { window.location.href = 'librogame-runthrough-game-detail.html'; }, 0); /* DEMO-NAV */ }}>← § prec.</button>
      <button type="button" disabled style={{
        flex: 1, padding: '8px 10px', borderRadius: 'var(--r-md)',
        background: entityHsl('kb', 0.1),
        border: `1px solid ${entityHsl('kb', 0.3)}`,
        color: entityHsl('kb'),
        fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 700,
      }}>Letto, vai al prossimo →</button>
    </div>
  </>
);

/* ═══════════════════════════════════════════════════════
   TAB SETUP — 3 stati + 1 generating
   ═══════════════════════════════════════════════════════ */
const PlayerPicker = ({ active = 4 }) => (
  <div style={{
    display: 'flex', gap: 4, padding: 3,
    background: 'var(--bg-muted)', borderRadius: 'var(--r-md)',
  }}>
    {[2, 3, 4, 5, 6].map(n => (
      <button key={n} type="button" style={{
        flex: 1, padding: '8px 0', borderRadius: 'var(--r-sm)',
        background: n === active ? 'var(--bg-card)' : 'transparent',
        border: 'none',
        color: n === active ? entityHsl('tool') : 'var(--text-sec)',
        fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 800,
        cursor: 'pointer',
        boxShadow: n === active ? 'var(--shadow-xs)' : 'none',
      }}>{n}</button>
    ))}
  </div>
);

const SetupStateDefault = () => (
  <div style={{ flex: 1, overflowY: 'auto', padding: '18px 16px', display: 'flex', flexDirection: 'column', gap: 14 }}>
    <div>
      <div style={{
        fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700,
        color: 'var(--text-muted)', textTransform: 'uppercase',
        letterSpacing: '0.08em', marginBottom: 8,
      }}>Quanti giocate stasera?</div>
      <PlayerPicker active={4}/>
    </div>
    <button type="button" style={{
      padding: '12px 16px', borderRadius: 'var(--r-md)',
      background: entityHsl('tool'),
      color: '#fff', border: 'none',
      fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 800,
      cursor: 'pointer',
      boxShadow: `0 4px 14px ${entityHsl('tool', 0.4)}`,
    }}>⚙️ Genera guida setup</button>
    <div style={{
      flex: 1, display: 'flex', flexDirection: 'column',
      alignItems: 'center', justifyContent: 'center',
      textAlign: 'center', padding: '30px 16px', gap: 12,
    }}>
      <div style={{
        width: 80, height: 80, borderRadius: 'var(--r-2xl)',
        background: `linear-gradient(135deg, ${entityHsl('tool', 0.18)}, ${entityHsl('kb', 0.16)})`,
        display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 34,
      }} aria-hidden="true">🎲</div>
      <p style={{
        fontSize: 13, color: 'var(--text-sec)', margin: 0,
        lineHeight: 1.5, maxWidth: '30ch',
      }}>
        Premi <strong>Genera guida setup</strong>: ti mostro il paragrafo di setup
        tradotto per {4} giocatori.
      </p>
    </div>
  </div>
);

const SetupStateGenerated = () => (
  <div style={{ flex: 1, overflowY: 'auto', padding: '18px 16px', display: 'flex', flexDirection: 'column', gap: 14 }}>
    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
      <h3 style={{
        flex: 1, fontFamily: 'var(--f-display)', fontSize: 17, fontWeight: 800,
        margin: 0, color: 'var(--text)',
      }}>Setup per 4 giocatori</h3>
      <EntityChip entity="kb" size="sm">p.4 — Setup base</EntityChip>
    </div>
    <div className="reading-body">
      <p>
        Disponi al centro del tavolo la <strong>plancia di Avalon</strong>, lato giorno verso l'alto.
        Mescola separatamente i mazzi <em>Esplorazione</em>, <em>Combattimento</em> e <em>Diplomazia</em>;
        posizionali nelle aree dedicate.
      </p>
      <p>
        Ogni giocatore sceglie un <strong>Personaggio</strong> e ne prende la <strong>plancia eroe</strong>,
        i <strong>3 Token Vita</strong> iniziali e la carta abilità di partenza. Per <strong>4 giocatori</strong>:
        rimuovi le carte evento contrassegnate con il simbolo solo (☉).
      </p>
      <p>
        Posiziona il segnalino <strong>Wyrdness</strong> sullo spazio 7 del tracciato. Il primo giocatore
        è quello che ha letto un libro più recentemente.
      </p>
    </div>
    <div style={{
      fontSize: 11, color: 'var(--text-muted)',
      fontStyle: 'italic', fontFamily: 'var(--f-body)',
    }}>
      Glossario applicato: Wyrdness, Token Vita, Plancia eroe
    </div>
    <div style={{
      padding: '8px 12px', borderRadius: 'var(--r-md)',
      background: entityHsl('tool', 0.1),
      border: `1px dashed ${entityHsl('tool', 0.4)}`,
      color: entityHsl('tool'),
      fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 700,
      display: 'inline-flex', alignItems: 'center', gap: 6,
      alignSelf: 'flex-start',
    }}>💡 Checklist interattiva con step disponibile in MVP-1</div>
  </div>
);

const SetupStateGenerating = () => (
  <div style={{ flex: 1, overflowY: 'auto', padding: '18px 16px', display: 'flex', flexDirection: 'column', gap: 14, opacity: 0.85 }}>
    <div style={{ pointerEvents: 'none', opacity: 0.6 }}>
      <div style={{
        fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
        color: 'var(--text-muted)', textTransform: 'uppercase',
        letterSpacing: '0.08em', marginBottom: 8,
      }}>Quanti giocate stasera?</div>
      <PlayerPicker active={4}/>
    </div>
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10,
      padding: '12px 14px', borderRadius: 'var(--r-md)',
      background: entityHsl('tool', 0.1),
      border: `1px solid ${entityHsl('tool', 0.28)}`,
      color: entityHsl('tool'),
      fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 700,
    }}>
      <span aria-hidden="true" className="mai-spinner" style={{
        width: 16, height: 16, borderRadius: '50%',
        border: `2px solid ${entityHsl('tool', 0.25)}`,
        borderTopColor: entityHsl('tool'),
        display: 'inline-block', flexShrink: 0,
      }}/>
      <span>Generazione setup… 12s</span>
    </div>
    <div style={{ marginTop: 4 }}>
      <SkeletonRow width="100%"/>
      <SkeletonRow width="94%"/>
      <SkeletonRow width="86%"/>
      <SkeletonRow width="72%"/>
    </div>
  </div>
);

const SetupStateUnclear = () => (
  <div style={{ flex: 1, overflowY: 'auto', padding: '18px 16px', display: 'flex', flexDirection: 'column', gap: 14 }}>
    <PlayerPicker active={4}/>
    <div style={{
      padding: 14, borderRadius: 'var(--r-lg)',
      background: entityHsl('agent', 0.1),
      border: `1px solid ${entityHsl('agent', 0.28)}`,
      display: 'flex', gap: 10, alignItems: 'flex-start',
    }}>
      <span aria-hidden="true" style={{ fontSize: 22 }}>⚠️</span>
      <div>
        <h3 style={{
          fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 800,
          margin: '0 0 4px', color: 'var(--text)',
        }}>Sezione setup non chiara</h3>
        <p style={{ margin: 0, fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.5 }}>
          Non riesco a identificare una sezione setup univoca nel manuale.
          Confidence 0.38 sull'unica sezione candidata (p.4-6).
        </p>
      </div>
    </div>
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      {[
        { em: '✅', label: 'Cerca nel testo generale', sub: 'Mostro p.4-6 senza identificare i passi' },
        { em: '📷', label: 'Indica tu le pagine setup', sub: 'Selezione manuale dalle 47 pagine indicizzate' },
        { em: '⏭️', label: 'Salta e inizia direttamente', sub: 'Apri Tab Chat per fare domande live' },
      ].map(a => (
        <button key={a.label} type="button" style={{
          display: 'flex', alignItems: 'center', gap: 12,
          padding: '12px 14px', borderRadius: 'var(--r-md)',
          background: 'var(--bg-card)', border: '1px solid var(--border)',
          color: 'var(--text)', textAlign: 'left', cursor: 'pointer',
        }}>
          <span aria-hidden="true" style={{ fontSize: 20, flexShrink: 0 }}>{a.em}</span>
          <div style={{ flex: 1 }}>
            <div style={{ fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 700 }}>{a.label}</div>
            <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>{a.sub}</div>
          </div>
        </button>
      ))}
    </div>
  </div>
);

/* ═══════════════════════════════════════════════════════
   MOBILE STATES (registry)
   ═══════════════════════════════════════════════════════ */
const CHAT_STATES = [
  { id: 'c1', anchor: 'state-01-chat-default-high-conf', tab: 'chat', label: '01 · Default', gherkin: 'G3.1', desc: 'Q&A con citation kb p.18 + confidence alta + action chips. Bubble user di Marco + Sara.', body: <ChatStateDefault/> },
  { id: 'c2', anchor: 'state-02-chat-ambiguity', tab: 'chat', label: '02 · Ambiguity', gherkin: 'G3.2', desc: '4 chip kb selezionabili (Combat / Iniziativa / Voto / Esplorazione) per disambig.', body: <ChatStateAmbiguity/> },
  { id: 'c3', anchor: 'state-03-chat-low-confidence', tab: 'chat', label: '03 · Low-confidence', gherkin: 'G3.3', desc: 'Confidence 🔴 + 3 azioni: rifotografa / house rule (primary) / BGG.', body: <ChatStateLowConfidence/> },
  { id: 'c4', anchor: 'state-04-chat-house-rule-applied', tab: 'chat', label: '04 · House rule applicata', gherkin: 'G3.4', desc: 'Badge chip agent "House rule attiva" + footnote citation manuale ufficiale.', body: <ChatStateHouseRule/> },
  { id: 'c5', anchor: 'state-05-chat-timeout', tab: 'chat', label: '05 · Timeout', gherkin: 'G3.5', desc: 'Typing indicator >8s + fallback CTA "Mostrami le pagine sul tema X" manuale.', body: <ChatStateTimeout/> },
  { id: 'c6', anchor: 'state-06-chat-empty', tab: 'chat', label: '06 · Empty (prima domanda)', gherkin: '—', desc: 'Onboarding session: illustrazione 💬 + 3 suggerimenti tappabili.', body: <ChatStateEmpty/> },
];

const PARAGRAPH_STATES = [
  { id: 'p0', anchor: 'state-06bis-paragrafo-empty', tab: 'paragraph', label: '06-bis · Empty', gherkin: '—', desc: 'Simmetrico a #06 chat empty: toolbar visibile, illustrazione 📖, controls disabled. (no Gherkin tag)', body: <ParagraphStateEmpty/> },
  { id: 'p1', anchor: 'state-07-paragrafo-default', tab: 'paragraph', label: '07 · Default §147', gherkin: 'G4', desc: '§147 Tainted Grail tradotto + glossario applicato + cache indicator + nav prec/next.', body: <ParagraphStateDefault/> },
  { id: 'p1b', anchor: 'state-07bis-paragrafo-loading', tab: 'paragraph', label: '07-bis · Loading', gherkin: 'G4 loading', desc: 'Header §147 visibile, body 3 righe shimmer (100/92/78%), cache indicator nascosto, footer disabled.', body: <ParagraphStateLoading/> },
  { id: 'p2', anchor: 'state-08-paragrafo-not-found', tab: 'paragraph', label: '08 · Not found §299', gherkin: 'G4.4', desc: '3 azioni: cerca testo simile / rifotografa pagine / salta paragrafo.', body: <ParagraphStateNotFound/> },
  { id: 'p3', anchor: 'state-09-paragrafo-offline-cached', tab: 'paragraph', label: '09 · Offline cached', gherkin: 'G4.7', desc: 'Banner amber connessione persa + fallback EN visibile + auto-retry indicator.', body: <ParagraphStateOffline/> },
  { id: 'p4', anchor: 'state-10-paragrafo-quota-exceeded', tab: 'paragraph', label: '10 · Quota exceeded', gherkin: 'G4.6', desc: 'Block paragrafo + CTA "Acquista crediti €5/100" event color → trigger mockup E.', body: <ParagraphStateQuota/> },
];

const SETUP_STATES = [
  { id: 's1', anchor: 'state-11-setup-default', tab: 'setup', label: '11 · Default (4 giocatori)', gherkin: 'G2', desc: 'Player picker 4 attivo + CTA "Genera guida setup" tool color + illustrazione empty.', body: <SetupStateDefault/> },
  { id: 's2', anchor: 'state-12-setup-generated', tab: 'setup', label: '12 · Generated', gherkin: 'G2', desc: 'Paragrafo setup tradotto reading-mode 24px + citation p.4 + disclaimer "checklist in MVP-1".', body: <SetupStateGenerated/> },
  { id: 's2b', anchor: 'state-12bis-setup-generating', tab: 'setup', label: '12-bis · Generating', gherkin: 'G2 generating', desc: 'Player picker disabled + CTA sostituito da spinner inline "Generazione setup… 12s" + 4 righe shimmer.', body: <SetupStateGenerating/> },
  { id: 's3', anchor: 'state-13-setup-unclear', tab: 'setup', label: '13 · Setup unclear', gherkin: 'G2.3', desc: 'Confidence 0.38: 3 opzioni (cerca generale / indica pagine / salta a chat).', body: <SetupStateUnclear/> },
];

/* ═══════════════════════════════════════════════════════
   MOBILE SCREEN — Phone wrapper con header + tabs + body + session bar
   ═══════════════════════════════════════════════════════ */
const MobileScreen = ({ tab, body }) => (
  <Phone>
    <SessionHeader compact/>
    <TabsNav active={tab}/>
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0 }}>
      {body}
    </div>
    {tab === 'chat' && <ChatInput/>}
    <SessionBar activeTab={tab}/>
  </Phone>
);

const Frame = ({ state }) => (
  <div className="frame-cell" id={state.anchor}>
    <MobileScreen tab={state.tab} body={state.body}/>
    <div className="frame-meta">
      <strong>{state.label}</strong>
      {state.gherkin !== '—' && <span className="gherkin">{state.gherkin}</span>}
      {state.desc}
    </div>
  </div>
);

/* ═══════════════════════════════════════════════════════
   DESKTOP SPLIT-VIEW
   ═══════════════════════════════════════════════════════ */
const DesktopGameContext = () => (
  <aside style={{
    width: 360, flexShrink: 0,
    borderRight: '1px solid var(--border)',
    background: 'var(--bg-card)',
    display: 'flex', flexDirection: 'column', gap: 0,
  }}>
    {/* Cover */}
    <div style={{
      height: 180,
      background: `linear-gradient(135deg, ${entityHsl('game', 0.85)}, ${entityHsl('event', 0.7)})`,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontSize: 70,
      position: 'relative',
    }} aria-hidden="true">
      🗡️
      <div style={{
        position: 'absolute', inset: 0,
        background: 'linear-gradient(180deg, transparent 50%, rgba(0,0,0,.45) 100%)',
      }}/>
    </div>
    {/* Title block */}
    <div style={{ padding: '16px 20px 14px', borderBottom: '1px solid var(--border-light)' }}>
      <h2 style={{
        fontFamily: 'var(--f-display)', fontSize: 22, fontWeight: 800,
        letterSpacing: '-0.01em', margin: '0 0 4px', color: 'var(--text)',
      }}>Tainted Grail</h2>
      <div style={{
        fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)',
        marginBottom: 10,
      }}>Awaken Realms · 2019 · 1-4 pl · 240 min</div>
      <div style={{
        display: 'inline-flex', alignItems: 'center', gap: 6,
        fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700,
        color: entityHsl('session'), textTransform: 'uppercase', letterSpacing: '0.06em',
      }}>
        <span aria-hidden="true" style={{
          display: 'inline-block', width: 8, height: 8, borderRadius: '50%',
          background: entityHsl('session'),
          animation: 'mai-pulse-session 1.6s ease-in-out infinite',
        }} className="mai-pulse"/>
        🎯 Sessione in corso · iniziata 47 min fa
      </div>
    </div>
    {/* Connection bar (vertical context) */}
    <div style={{ padding: '14px 20px', borderBottom: '1px solid var(--border-light)' }}>
      <div style={{
        fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
        color: 'var(--text-muted)', textTransform: 'uppercase',
        letterSpacing: '0.08em', marginBottom: 8,
      }}>Connessioni sessione</div>
      <ConnectionBar connections={SESSION_CONNECTIONS}/>
    </div>
    {/* Players at table */}
    <div style={{ padding: '14px 20px', borderBottom: '1px solid var(--border-light)' }}>
      <div style={{
        fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
        color: 'var(--text-muted)', textTransform: 'uppercase',
        letterSpacing: '0.08em', marginBottom: 8,
      }}>Giocatori al tavolo</div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        {[
          { name: 'Sara', role: 'GM (tu)', color: 'player' },
          { name: 'Marco', role: 'Niamh', color: 'player' },
          { name: 'Giulia', role: 'Beor', color: 'player' },
          { name: 'Davide', role: 'Maeve', color: 'player' },
        ].map(p => (
          <div key={p.name} style={{
            display: 'flex', alignItems: 'center', gap: 10,
          }}>
            <div style={{
              width: 30, height: 30, borderRadius: '50%',
              background: entityHsl(p.color, 0.18),
              color: entityHsl(p.color),
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800,
              flexShrink: 0,
            }}>{p.name[0]}</div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 700 }}>{p.name}</div>
              <div style={{ fontSize: 10, color: 'var(--text-muted)', fontFamily: 'var(--f-mono)' }}>{p.role}</div>
            </div>
          </div>
        ))}
      </div>
    </div>
    {/* House rules */}
    <div style={{ padding: '14px 20px', flex: 1 }}>
      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        marginBottom: 8,
      }}>
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
          color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.08em',
        }}>House rules attive</div>
        <button type="button" style={{
          background: 'transparent', border: 'none',
          color: entityHsl('agent'),
          fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 700, cursor: 'pointer',
        }} onClick={() => { setTimeout(() => { window.location.href = 'librogame-runthrough-game-detail.html'; }, 0); /* DEMO-NAV */ }}>+ Aggiungi</button>
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        {[
          'Critici fanno ×2 danno (no +50%)',
          'Morte simultanea: chi ha più HP max diventa leader',
        ].map(r => (
          <div key={r} style={{
            padding: '8px 10px', borderRadius: 'var(--r-md)',
            background: entityHsl('agent', 0.08),
            border: `1px solid ${entityHsl('agent', 0.18)}`,
            fontSize: 12, color: 'var(--text)', lineHeight: 1.4,
            display: 'flex', gap: 6, alignItems: 'flex-start',
          }}>
            <span aria-hidden="true">🤝</span>
            <span style={{ flex: 1 }}>{r}</span>
          </div>
        ))}
      </div>
    </div>
  </aside>
);

const DesktopSplit = ({ tab, body, label, gherkin, desc, anchor, theme }) => (
  <div className="frame-cell" id={anchor} data-theme={theme}>
    <div className="desktop-frame">
      <div className="desktop-chrome">
        <span className="dot" style={{ background: '#ff5f57' }}/>
        <span className="dot" style={{ background: '#febc2e' }}/>
        <span className="dot" style={{ background: '#28c840' }}/>
        <span className="url">meepleai.app/gamebook/gb-tainted-grail/play</span>
      </div>
      <div style={{ display: 'flex', height: 720, background: 'var(--bg)' }}>
        <DesktopGameContext/>
        <main style={{
          flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0,
        }}>
          <TabsNav active={tab}/>
          <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0 }}>
            {body}
          </div>
          {tab === 'chat' && <ChatInput/>}
        </main>
      </div>
    </div>
    <div className="frame-meta">
      <strong>{label}</strong>
      {gherkin && <span className="gherkin">{gherkin}</span>}
      {desc}
    </div>
  </div>
);

/* ═══════════════════════════════════════════════════════
   MOUNT
   ═══════════════════════════════════════════════════════ */
const ChatRow = () => (
  <>{CHAT_STATES.map(s => <Frame key={s.id} state={s}/>)}</>
);
const ParagraphRow = () => (
  <>{PARAGRAPH_STATES.map(s => <Frame key={s.id} state={s}/>)}</>
);
const SetupRow = () => (
  <>{SETUP_STATES.map(s => <Frame key={s.id} state={s}/>)}</>
);
const DesktopRow = () => (
  <>
    <DesktopSplit
      anchor="state-14-desktop-split-chat-default"
      tab="chat"
      body={<ChatStateDefault/>}
      label="14 · Desktop · Chat default"
      gherkin="G3.1"
      desc="Split-view: left 360px game-context (cover hero + connections + giocatori + house rules), right tab Chat con conversation feed e citation chips."/>
    <DesktopSplit
      anchor="state-15-desktop-split-paragrafo-default"
      tab="paragraph"
      body={<ParagraphStateDefault/>}
      label="15 · Desktop · Paragrafo default"
      gherkin="G4"
      desc="Tab Paragrafo §147 con reading body 24px (=18pt) max-width 60ch e nav prec/next sticky. Cache indicator visibile."/>
    <DesktopSplit
      anchor="state-16-desktop-split-chat-low-confidence"
      tab="chat"
      body={<ChatStateLowConfidence/>}
      label="16 · Desktop · Chat low-confidence"
      gherkin="G3.3"
      desc="Stato critico desktop: il drawer F si apre come side panel right 380px sopra questa view (non rappresentato qui — vedi mockup F)."/>
  </>
);

ReactDOM.createRoot(document.getElementById('mount-chat')).render(<ChatRow/>);
ReactDOM.createRoot(document.getElementById('mount-paragraph')).render(<ParagraphRow/>);
ReactDOM.createRoot(document.getElementById('mount-setup')).render(<SetupRow/>);
ReactDOM.createRoot(document.getElementById('mount-desktop')).render(<DesktopRow/>);

/* ═══════════════════════════════════════════════════════
   DARK THEME PROOFS — riusa JSX esistente, wrap data-theme
   ═══════════════════════════════════════════════════════ */
const DarkFrame = ({ anchor, label, gherkin, desc, tab, body }) => (
  <div className="frame-cell" id={anchor}>
    <div data-theme="dark" style={{ background: 'var(--bg)', borderRadius: 48, padding: 0 }}>
      <MobileScreen tab={tab} body={body}/>
    </div>
    <div className="frame-meta">
      <strong>{label}</strong>
      <span className="gherkin">{gherkin}</span>
      {desc}
    </div>
  </div>
);

const DarkRow = () => (
  <>
    <DarkFrame
      anchor="state-17-chat-default-dark"
      label="17 · Chat default · DARK"
      gherkin="G3.1 · dark theme proof"
      desc="Riuso esatto JSX #01: verifica contrast kb chip + bubble agent + ConnectionPip su bg dark warm neutrals."
      tab="chat"
      body={<ChatStateDefault/>}/>
    <DarkFrame
      anchor="state-18-paragrafo-default-dark"
      label="18 · Paragrafo default §147 · DARK"
      gherkin="G4 · dark theme proof"
      desc="Riuso esatto JSX #07: reading-body 24px contrast + cache pill kb + footer nav prec/next."
      tab="paragraph"
      body={<ParagraphStateDefault/>}/>
    <DarkFrame
      anchor="state-19-setup-generated-dark"
      label="19 · Setup generated · DARK"
      gherkin="G2 · dark theme proof"
      desc="Riuso esatto JSX #12: citation kb p.4 + reading-body + disclaimer tool dashed border."
      tab="setup"
      body={<SetupStateGenerated/>}/>
  </>
);

ReactDOM.createRoot(document.getElementById('mount-dark')).render(<DarkRow/>);

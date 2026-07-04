// ── sp7-notifications-hub.jsx — MeepleAI Design System v1 · SP7-I
// Route: /notifications · Persona: Marco al risveglio (mobile mass-check)
// Pattern: timeline + grouping + bulk actions
// Components: SeverityDot, EntityIcon, FilterTabs, BulkActionsBar,
//             GroupDivider, NotificationItem, ActionDrawer, ConfirmModal,
//             Toast, ErrorBanner, EmptyState, NotificationsFeed
// 8 stati esportati a window per sp7-notifications-hub.html
//
// Severity → entity color mapping (via entityHsl helper, FREEZE-safe):
//   🔴 Critica    = entityHsl('event')   rose
//   🟡 Importante = entityHsl('agent')   amber
//   🟢 Info       = entityHsl('toolkit') green
//   ⚫ Lette      = var(--text-muted) / var(--text-sec)

const { useState, useRef, useCallback } = React;

// ── Helpers — FREEZE-safe: emette `hsl(var(--c-*))`, mai `hsl(<numero>` ──
const entityHsl = (entity, alpha) =>
  alpha !== undefined
    ? `hsl(var(--c-${entity}) / ${alpha})`
    : `hsl(var(--c-${entity}))`;

// Mappa severity → entity token (single source of truth)
const SEVERITY = {
  critical:  { entity: 'event',   dot: '🔴', label: 'Critica' },
  important: { entity: 'agent',   dot: '🟡', label: 'Importante' },
  info:      { entity: 'toolkit', dot: '🟢', label: 'Info' },
};
const sevColor = (sev, alpha) => entityHsl(SEVERITY[sev].entity, alpha);

// ── Demo data — persona italiana (Marco), 15 notifiche miste ──────
// group: oggi | ieri | settimana ; sev: critical|important|info ; read: bool
// mention: true → compare nel filtro "Mention"
const NOTIFICATIONS = [
  // ── OGGI ──
  { id: 1, entity: 'event', icon: '🎉', sev: 'critical', read: false, mention: false,
    title: 'Davide ha annullato all\'ultimo',
    desc: 'Davide si è tirato indietro da "Sabato boardgame con i Padovani". Ora siete in 5.',
    time: '3 min fa', timeAbs: 'Oggi alle 08:12', group: 'oggi', cta: 'Apri serata' },
  { id: 2, entity: 'agent', icon: '🤖', sev: 'important', read: false, mention: false,
    title: 'Training agente fallito per Wingspan',
    desc: 'L\'indicizzazione del manuale Wingspan si è interrotta al 60%. Controlla il documento sorgente.',
    time: '12 min fa', timeAbs: 'Oggi alle 08:03', group: 'oggi', cta: 'Riprova training' },
  { id: 3, entity: 'player', icon: '👤', sev: 'important', read: false, mention: false,
    title: 'Giulia ha confermato per Sabato boardgame',
    desc: 'Giulia B. ha accettato l\'invito per sabato 27 alle 21:00. Conferme: 6 su 8.',
    time: '38 min fa', timeAbs: 'Oggi alle 07:37', group: 'oggi', cta: 'Vedi RSVP' },
  { id: 4, entity: 'chat', icon: '💬', sev: 'info', read: false, mention: true,
    title: 'Luca ti ha menzionato',
    desc: '@Marco hai ancora il manuale di Terraforming Mars? Servirebbe per giovedì.',
    time: '1 ora fa', timeAbs: 'Oggi alle 07:15', group: 'oggi', cta: 'Rispondi' },
  { id: 5, entity: 'toolkit', icon: '🧰', sev: 'info', read: false, mention: false,
    title: 'Suggerimento agente: nuova house rule',
    desc: 'L\'agente propone di salvare la variante "porto rapido" usata nelle ultime 3 partite di Catan.',
    time: '2 ore fa', timeAbs: 'Oggi alle 06:20', group: 'oggi', cta: 'Vedi suggerimento' },
  // ── IERI ──
  { id: 6, entity: 'kb', icon: '📖', sev: 'important', read: true, mention: false,
    title: 'Embedding KB completato',
    desc: 'Il manuale di Ark Nova è stato indicizzato. 142 chunk pronti per le domande dell\'agente.',
    time: 'ieri 18:32', timeAbs: 'Ieri alle 18:32', group: 'ieri', cta: 'Apri knowledge base' },
  { id: 7, entity: 'player', icon: '👤', sev: 'info', read: true, mention: false,
    title: 'Sara si è unita alla partita',
    desc: 'Sara F. ha accettato il tuo invito per Carcassonne di domenica. Ora siete in 4.',
    time: 'ieri 17:05', timeAbs: 'Ieri alle 17:05', group: 'ieri', cta: 'Vedi partita' },
  { id: 8, entity: 'event', icon: '🎉', sev: 'critical', read: true, mention: false,
    title: 'Serata di venerdì a rischio',
    desc: 'Solo 2 conferme su 6 per "Aperitivo & Azul". Manda un promemoria agli indecisi?',
    time: 'ieri 15:40', timeAbs: 'Ieri alle 15:40', group: 'ieri', cta: 'Invia promemoria' },
  { id: 9, entity: 'chat', icon: '💬', sev: 'info', read: true, mention: true,
    title: 'Federica ti ha menzionato',
    desc: '@Marco grazie per il riepilogo, ci vediamo sabato! 🎲',
    time: 'ieri 14:18', timeAbs: 'Ieri alle 14:18', group: 'ieri', cta: 'Apri chat' },
  { id: 10, entity: 'agent', icon: '🤖', sev: 'important', read: true, mention: false,
    title: 'Training agente completato per Wingspan',
    desc: 'L\'agente Regolamento Wingspan è pronto. Puoi già fargli domande sulle regole.',
    time: 'ieri 11:02', timeAbs: 'Ieri alle 11:02', group: 'ieri', cta: 'Chiedi all\'agente' },
  // ── QUESTA SETTIMANA ──
  { id: 11, entity: 'toolkit', icon: '🧰', sev: 'info', read: true, mention: false,
    title: 'Riepilogo settimanale pronto',
    desc: 'Questa settimana: 4 partite giocate, 2 serate organizzate, striscia di 3 settimane attiva.',
    time: 'lun 09:00', timeAbs: 'Lunedì alle 09:00', group: 'settimana', cta: 'Vedi riepilogo' },
  { id: 12, entity: 'player', icon: '👤', sev: 'info', read: true, mention: false,
    title: 'Luca ha confermato per la serata',
    desc: 'Luca M. parteciperà a "Sabato boardgame con i Padovani". Conferme: 5 su 8.',
    time: 'lun 08:30', timeAbs: 'Lunedì alle 08:30', group: 'settimana', cta: 'Vedi RSVP' },
  { id: 13, entity: 'kb', icon: '📖', sev: 'important', read: true, mention: false,
    title: 'House rule aggiunta da Davide',
    desc: 'Davide ha aggiunto a Risiko: "Prima mossa — dadi una sola volta per territorio".',
    time: 'dom 20:14', timeAbs: 'Domenica alle 20:14', group: 'settimana', cta: 'Vedi regola' },
  { id: 14, entity: 'game', icon: '🏆', sev: 'info', read: true, mention: false,
    title: 'Nuovo record personale',
    desc: 'Hai battuto il tuo punteggio massimo a Wingspan: 98 punti nella partita di sabato.',
    time: 'dom 19:00', timeAbs: 'Domenica alle 19:00', group: 'settimana', cta: 'Vedi statistiche' },
  { id: 15, entity: 'event', icon: '🎉', sev: 'critical', read: true, mention: false,
    title: 'Federica ha annullato la serata di domenica',
    desc: 'La serata "Domenica leggera con Patchwork" è stata cancellata dall\'organizzatrice.',
    time: 'sab 22:45', timeAbs: 'Sabato alle 22:45', group: 'settimana', cta: 'Vedi dettagli' },
];

const GROUPS = [
  { key: 'oggi',      label: 'Oggi' },
  { key: 'ieri',      label: 'Ieri' },
  { key: 'settimana', label: 'Questa settimana' },
];

const FILTERS = [
  { key: 'all',      label: 'Tutte',    entity: 'game' },
  { key: 'unread',   label: 'Non lette', entity: 'agent' },
  { key: 'mention',  label: 'Mention',  entity: 'chat' },
  { key: 'critical', label: 'Critiche', entity: 'event' },
];

// Predicate per ogni filtro
const matchFilter = (n, key) => {
  if (key === 'unread')   return !n.read;
  if (key === 'mention')  return n.mention;
  if (key === 'critical') return n.sev === 'critical';
  return true; // all
};

// ── PhoneTopBar — header con title + counter ─────────────────────
function PhoneTopBar({ unreadCount, totalCount }) {
  return (
    <div style={{
      padding: '12px 14px 10px',
      borderBottom: '1px solid var(--border)',
      background: 'var(--bg)', flexShrink: 0,
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
        <span style={{
          fontFamily: 'var(--f-display)', fontWeight: 800,
          fontSize: 19, flex: 1, letterSpacing: '-.01em', color: 'var(--text)',
        }}>Notifiche</span>
        {unreadCount > 0 && (
          <div aria-hidden="true" style={{
            minWidth: 22, height: 22, borderRadius: 11,
            background: entityHsl('event'), color: '#fff',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 11, fontWeight: 800, fontFamily: 'var(--f-mono)',
            padding: '0 6px', border: '2px solid var(--bg)',
          }}>{unreadCount > 9 ? '9+' : unreadCount}</div>
        )}
        <button aria-label="Altre opzioni" style={{
          width: 32, height: 32, borderRadius: '50%',
          background: 'var(--bg-muted)', border: 'none', cursor: 'pointer',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontSize: 14, color: 'var(--text-sec)',
        }}>⋯</button>
      </div>
      <div style={{
        marginTop: 4, fontSize: 11.5, color: 'var(--text-muted)',
        fontFamily: 'var(--f-mono)', letterSpacing: '.01em',
      }}>
        <span style={{ color: 'var(--text-sec)', fontWeight: 700 }}>{unreadCount} nuove</span>
        {' · '}{totalCount} totali
      </div>
    </div>
  );
}

// ── FilterTabs — [Tutte][Non lette][Mention][Critiche] ───────────
function FilterTabs({ active, onChange, counts }) {
  return (
    <div role="tablist" aria-label="Filtra notifiche" style={{
      display: 'flex', gap: 5, padding: '8px 12px',
      overflowX: 'auto', flexShrink: 0,
      borderBottom: '1px solid var(--border)',
      background: 'var(--bg)', scrollbarWidth: 'none',
    }}>
      {FILTERS.map(f => {
        const isActive = active === f.key;
        const count = counts?.[f.key] ?? 0;
        return (
          <button key={f.key} role="tab" aria-selected={isActive}
            onClick={() => onChange(f.key)} style={{
              display: 'flex', alignItems: 'center', gap: 5,
              padding: '6px 11px', borderRadius: 'var(--r-pill)',
              border: 'none', cursor: 'pointer', whiteSpace: 'nowrap',
              fontSize: 11.5, fontWeight: 700, fontFamily: 'var(--f-display)',
              transition: 'all var(--dur-sm) var(--ease-out)',
              background: isActive ? entityHsl(f.entity, 0.12) : 'transparent',
              color: isActive ? entityHsl(f.entity) : 'var(--text-sec)',
            }}>
            <span>{f.label}</span>
            {count > 0 && (
              <span style={{
                background: isActive ? entityHsl(f.entity) : 'var(--bg-muted)',
                color: isActive ? '#fff' : 'var(--text-muted)',
                borderRadius: 'var(--r-pill)', fontSize: 9, fontWeight: 800,
                padding: '1px 5px', lineHeight: 1.6, fontFamily: 'var(--f-mono)',
              }}>{count}</span>
            )}
          </button>
        );
      })}
    </div>
  );
}

// ── BulkActionsBar — visibile se > 5 unread ──────────────────────
function BulkActionsBar({ unreadCount, onMarkAll }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 8,
      padding: '8px 14px',
      background: entityHsl('agent', 0.07),
      borderBottom: '1px solid var(--border-light)', flexShrink: 0,
    }}>
      <span style={{
        flex: 1, fontSize: 11.5, color: 'var(--text-sec)',
        fontFamily: 'var(--f-display)', fontWeight: 600,
      }}>{unreadCount} non lette</span>
      <button onClick={onMarkAll} style={{
        padding: '5px 11px', fontSize: 11, fontWeight: 700,
        borderRadius: 'var(--r-pill)', cursor: 'pointer',
        background: entityHsl('agent', 0.14), color: entityHsl('agent'),
        border: 'none', fontFamily: 'var(--f-display)',
      }}>Segna tutte come lette</button>
      <button style={{
        padding: '5px 11px', fontSize: 11, fontWeight: 700,
        borderRadius: 'var(--r-pill)', cursor: 'pointer',
        background: 'var(--bg-muted)', color: 'var(--text-muted)',
        border: 'none', fontFamily: 'var(--f-display)',
      }}>Archivia tutte</button>
    </div>
  );
}

// ── SeverityDot ──────────────────────────────────────────────────
function SeverityDot({ sev, read }) {
  return (
    <span aria-hidden="true" style={{
      width: 8, height: 8, borderRadius: '50%', flexShrink: 0,
      background: read ? 'var(--text-muted)' : sevColor(sev),
      opacity: read ? 0.5 : 1,
    }} />
  );
}

// ── EntityIcon ───────────────────────────────────────────────────
function EntityIcon({ entity, icon, sev, read, size = 40 }) {
  // Lette → tinta neutra; non lette → tinta severity
  const bg = read ? 'var(--bg-muted)' : sevColor(sev, 0.14);
  const fg = read ? 'var(--text-muted)' : sevColor(sev);
  return (
    <div aria-hidden="true" style={{
      width: size, height: size, borderRadius: 'var(--r-lg)',
      background: bg, color: fg,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontSize: Math.round(size * 0.45), flexShrink: 0,
    }}>{icon}</div>
  );
}

// ── TimestampRelative ────────────────────────────────────────────
function TimestampRelative({ time }) {
  return (
    <span style={{
      fontFamily: 'var(--f-mono)', fontSize: 10,
      color: 'var(--text-muted)', whiteSpace: 'nowrap',
    }}>{time}</span>
  );
}

// ── GroupDivider ─────────────────────────────────────────────────
function GroupDivider({ label, count }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 8,
      padding: '12px 14px 6px',
      fontFamily: 'var(--f-mono)', fontSize: 10,
      color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em',
    }}>
      <span>{label}</span>
      <span style={{
        background: 'var(--bg-muted)', borderRadius: 10,
        padding: '1px 6px', fontSize: 9, fontWeight: 700,
      }}>{count}</span>
      <div style={{ flex: 1, height: 1, background: 'var(--border-light)' }} />
    </div>
  );
}

// ── NotificationItem ─────────────────────────────────────────────
function NotificationItem({ notif, onLongPress }) {
  const [hover, setHover] = useState(false);
  const timer = useRef(null);

  const startPress = () => {
    timer.current = setTimeout(() => onLongPress?.(notif), 480);
  };
  const cancelPress = () => {
    if (timer.current) { clearTimeout(timer.current); timer.current = null; }
  };

  return (
    <div
      role="listitem"
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => { setHover(false); cancelPress(); }}
      onTouchStart={startPress}
      onTouchEnd={cancelPress}
      onTouchMove={cancelPress}
      style={{
        display: 'flex', alignItems: 'flex-start', gap: 12,
        padding: '12px 14px',
        borderLeft: `3px solid ${notif.read ? 'var(--border)' : sevColor(notif.sev)}`,
        background: hover ? 'var(--bg-hover)' : 'transparent',
        cursor: 'pointer',
        transition: 'background var(--dur-xs) var(--ease-out)',
        minHeight: 64,
      }}
    >
      <EntityIcon entity={notif.entity} icon={notif.icon} sev={notif.sev} read={notif.read} />
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          display: 'flex', alignItems: 'center',
          justifyContent: 'space-between', gap: 8, marginBottom: 2,
        }}>
          <span style={{
            display: 'flex', alignItems: 'center', gap: 6, minWidth: 0,
          }}>
            <SeverityDot sev={notif.sev} read={notif.read} />
            <span style={{
              fontSize: 13, fontWeight: notif.read ? 500 : 700,
              color: notif.read ? 'var(--text-sec)' : 'var(--text)',
              fontFamily: 'var(--f-display)',
              overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
            }}>{notif.title}</span>
          </span>
          <TimestampRelative time={notif.time} />
        </div>
        <div style={{
          fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.45,
          display: '-webkit-box', WebkitLineClamp: 2,
          WebkitBoxOrient: 'vertical', overflow: 'hidden',
        }}>{notif.desc}</div>
        {hover && (
          <div style={{ display: 'flex', gap: 5, marginTop: 8 }}>
            {notif.cta && (
              <button style={{
                padding: '3px 9px', fontSize: 10, fontWeight: 700,
                borderRadius: 'var(--r-sm)',
                background: sevColor(notif.sev, 0.14), color: sevColor(notif.sev),
                border: 'none', cursor: 'pointer', fontFamily: 'var(--f-display)',
              }}>{notif.cta}</button>
            )}
            <button style={{
              padding: '3px 9px', fontSize: 10, fontWeight: 700,
              borderRadius: 'var(--r-sm)',
              background: 'var(--bg-muted)', color: 'var(--text-muted)',
              border: 'none', cursor: 'pointer', fontFamily: 'var(--f-display)',
            }}>Posticipa 1h</button>
          </div>
        )}
      </div>
    </div>
  );
}

// ── ActionDrawer — bottom drawer 4 azioni (long-press) ───────────
function ActionDrawer({ notif, onClose }) {
  if (!notif) return null;
  const actions = [
    { icon: '✓',  label: 'Segna come letta', tint: entityHsl('toolkit', 0.14), fg: entityHsl('toolkit') },
    { icon: '🕘', label: 'Posticipa di 1 ora', tint: entityHsl('agent', 0.14),   fg: entityHsl('agent') },
    { icon: '📦', label: 'Archivia',          tint: 'var(--bg-muted)',           fg: 'var(--text-sec)' },
    { icon: '↗',  label: 'Apri',              tint: sevColor(notif.sev, 0.14),   fg: sevColor(notif.sev) },
  ];
  return (
    <div role="dialog" aria-label="Azioni notifica" style={{
      position: 'absolute', bottom: 0, left: 0, right: 0,
      background: 'var(--bg-card)',
      borderRadius: 'var(--r-2xl) var(--r-2xl) 0 0',
      boxShadow: 'var(--shadow-drawer)', zIndex: 10,
      animation: 'slideUp var(--dur-md) var(--ease-out)',
    }}>
      {/* Handle */}
      <div style={{ display: 'flex', justifyContent: 'center', padding: '10px 0 4px' }}>
        <div style={{ width: 36, height: 4, borderRadius: 2, background: 'var(--border-strong)' }} />
      </div>
      {/* Context header */}
      <div style={{ padding: '6px 16px 12px', display: 'flex', gap: 10, alignItems: 'center' }}>
        <EntityIcon entity={notif.entity} icon={notif.icon} sev={notif.sev} read={notif.read} size={36} />
        <span style={{
          fontSize: 13, fontWeight: 700, fontFamily: 'var(--f-display)',
          color: 'var(--text)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
        }}>{notif.title}</span>
      </div>
      <div style={{ height: 1, background: 'var(--border-light)', margin: '0 16px' }} />
      {/* Actions */}
      <div style={{ padding: '8px 12px 4px' }}>
        {actions.map((a, i) => (
          <button key={i} onClick={onClose} style={{
            display: 'flex', alignItems: 'center', gap: 12, width: '100%',
            padding: '11px 12px', background: 'transparent', border: 'none',
            borderRadius: 'var(--r-lg)', cursor: 'pointer', textAlign: 'left',
            fontFamily: 'var(--f-display)',
          }}>
            <span aria-hidden="true" style={{
              width: 34, height: 34, borderRadius: 'var(--r-md)',
              background: a.tint, color: a.fg,
              display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 15,
            }}>{a.icon}</span>
            <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--text)' }}>{a.label}</span>
          </button>
        ))}
      </div>
      <div style={{ padding: '4px 16px 24px' }}>
        <button onClick={onClose} style={{
          width: '100%', padding: '10px 16px',
          background: 'var(--bg-muted)', color: 'var(--text-sec)',
          border: 'none', borderRadius: 'var(--r-lg)', cursor: 'pointer',
          fontSize: 12, fontWeight: 700, fontFamily: 'var(--f-display)',
        }}>Annulla</button>
      </div>
    </div>
  );
}

// ── ConfirmModal — "Segna tutte come lette" ──────────────────────
function ConfirmModal({ count, onCancel, onConfirm }) {
  return (
    <div role="dialog" aria-modal="true" aria-label="Conferma azione" style={{
      position: 'absolute', inset: 0, zIndex: 20,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      padding: 22,
    }}>
      <div style={{
        position: 'absolute', inset: 0,
        background: 'rgba(0,0,0,0.28)', backdropFilter: 'blur(2px)',
      }} onClick={onCancel} />
      <div style={{
        position: 'relative', width: '100%', maxWidth: 300,
        background: 'var(--bg-card)', borderRadius: 'var(--r-2xl)',
        boxShadow: 'var(--shadow-lg)', padding: '22px 20px 18px', textAlign: 'center',
      }}>
        <div aria-hidden="true" style={{
          width: 52, height: 52, borderRadius: '50%', margin: '0 auto 14px',
          background: entityHsl('agent', 0.14), color: entityHsl('agent'),
          display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 24,
        }}>✓</div>
        <div style={{
          fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 16,
          color: 'var(--text)', marginBottom: 6, letterSpacing: '-.01em',
        }}>Segnare tutte come lette?</div>
        <div style={{
          fontSize: 12.5, color: 'var(--text-muted)', lineHeight: 1.5, marginBottom: 18,
        }}>{count} notifiche non lette verranno marcate come lette. L&apos;azione non è reversibile.</div>
        <div style={{ display: 'flex', gap: 9 }}>
          <button onClick={onCancel} style={{
            flex: 1, padding: '10px 14px', background: 'var(--bg-muted)',
            color: 'var(--text-sec)', border: 'none', borderRadius: 'var(--r-lg)',
            cursor: 'pointer', fontSize: 12.5, fontWeight: 700, fontFamily: 'var(--f-display)',
          }}>Annulla</button>
          <button onClick={onConfirm} style={{
            flex: 1, padding: '10px 14px', background: entityHsl('agent'),
            color: '#fff', border: 'none', borderRadius: 'var(--r-lg)',
            cursor: 'pointer', fontSize: 12.5, fontWeight: 700, fontFamily: 'var(--f-display)',
          }}>Conferma</button>
        </div>
      </div>
    </div>
  );
}

// ── Toast — success "Notifica posticipata di 1h" ─────────────────
function Toast({ icon, text, entity }) {
  return (
    <div role="status" style={{
      position: 'absolute', left: 14, right: 14, bottom: 18, zIndex: 30,
      display: 'flex', alignItems: 'center', gap: 10,
      padding: '12px 14px', borderRadius: 'var(--r-lg)',
      background: 'var(--bg-card)', boxShadow: 'var(--shadow-lg)',
      border: `1px solid ${entityHsl(entity, 0.3)}`,
      animation: 'slideUp var(--dur-md) var(--ease-out)',
    }}>
      <span aria-hidden="true" style={{
        width: 32, height: 32, borderRadius: 'var(--r-md)', flexShrink: 0,
        background: entityHsl(entity, 0.14), color: entityHsl(entity),
        display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 16,
      }}>{icon}</span>
      <span style={{
        flex: 1, fontSize: 12.5, fontWeight: 600, color: 'var(--text)',
        fontFamily: 'var(--f-display)',
      }}>{text}</span>
      <button style={{
        padding: '3px 9px', fontSize: 11, fontWeight: 700,
        background: 'transparent', color: entityHsl(entity),
        border: 'none', cursor: 'pointer', fontFamily: 'var(--f-display)',
      }}>Annulla</button>
    </div>
  );
}

// ── ErrorBanner — "Impossibile caricare nuove notifiche" ─────────
function ErrorBanner({ onRetry }) {
  return (
    <div role="alert" style={{
      display: 'flex', alignItems: 'center', gap: 10,
      margin: '10px 12px', padding: '11px 13px',
      borderRadius: 'var(--r-lg)', flexShrink: 0,
      background: entityHsl('event', 0.1),
      border: `1px solid ${entityHsl('event', 0.3)}`,
    }}>
      <span aria-hidden="true" style={{
        width: 30, height: 30, borderRadius: 'var(--r-md)', flexShrink: 0,
        background: entityHsl('event', 0.16), color: entityHsl('event'),
        display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 15,
      }}>⚠️</span>
      <div style={{ flex: 1 }}>
        <div style={{
          fontSize: 12.5, fontWeight: 700, color: entityHsl('event'),
          fontFamily: 'var(--f-display)',
        }}>Impossibile caricare nuove notifiche</div>
        <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 1 }}>
          Connessione assente. Le notifiche mostrate potrebbero non essere aggiornate.
        </div>
      </div>
      <button onClick={onRetry} style={{
        padding: '5px 12px', fontSize: 11, fontWeight: 700, flexShrink: 0,
        borderRadius: 'var(--r-pill)', cursor: 'pointer',
        background: entityHsl('event'), color: '#fff',
        border: 'none', fontFamily: 'var(--f-display)',
      }}>Riprova</button>
    </div>
  );
}

// ── EmptyState — "Nessuna notifica" + suggested actions ──────────
function EmptyState() {
  const suggestions = [
    { icon: '🎉', label: 'Organizza una serata gioco', entity: 'event' },
    { icon: '🤖', label: 'Crea un agente regolamento', entity: 'agent' },
    { icon: '📖', label: 'Aggiungi un gioco alla libreria', entity: 'toolkit' },
  ];
  return (
    <div style={{
      flex: 1, display: 'flex', flexDirection: 'column',
      alignItems: 'center', justifyContent: 'center',
      padding: '36px 22px', gap: 14, textAlign: 'center',
    }}>
      <div aria-hidden="true" style={{
        width: 96, height: 96, borderRadius: '50%',
        background: entityHsl('toolkit', 0.1),
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 42, marginBottom: 2,
      }}>🔔</div>
      <div style={{
        fontFamily: 'var(--f-display)', fontWeight: 800,
        fontSize: 17, color: 'var(--text)', letterSpacing: '-.01em',
      }}>Nessuna notifica</div>
      <div style={{
        fontSize: 13, color: 'var(--text-muted)',
        lineHeight: 1.55, maxWidth: 250,
      }}>
        Sei in pari! Qui appariranno aggiornamenti su serate, agenti, RSVP e altro.
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, width: '100%', maxWidth: 280, marginTop: 6 }}>
        {suggestions.map((s, i) => (
          <button key={i} style={{
            display: 'flex', alignItems: 'center', gap: 11, width: '100%',
            padding: '11px 13px', borderRadius: 'var(--r-lg)', cursor: 'pointer',
            background: 'var(--bg-card)', border: '1px solid var(--border)',
            fontFamily: 'var(--f-display)', textAlign: 'left',
          }}>
            <span aria-hidden="true" style={{
              width: 32, height: 32, borderRadius: 'var(--r-md)', flexShrink: 0,
              background: entityHsl(s.entity, 0.12), color: entityHsl(s.entity),
              display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 15,
            }}>{s.icon}</span>
            <span style={{ flex: 1, fontSize: 12.5, fontWeight: 700, color: 'var(--text)' }}>{s.label}</span>
            <span aria-hidden="true" style={{ color: 'var(--text-muted)', fontSize: 15 }}>›</span>
          </button>
        ))}
      </div>
    </div>
  );
}

// ── NotificationsFeed (shared logic) ─────────────────────────────
function NotificationsFeed({ notifs, onLongPress, dim }) {
  const grouped = GROUPS.map(g => ({
    ...g,
    items: notifs.filter(n => n.group === g.key),
  })).filter(g => g.items.length > 0);

  return (
    <div role="feed" aria-label="Notifiche" style={{
      flex: 1, overflowY: 'auto', display: 'flex', flexDirection: 'column',
      opacity: dim ? 0.4 : 1,
    }}>
      {grouped.map(g => (
        <div key={g.key}>
          <GroupDivider label={g.label} count={g.items.length} />
          {g.items.map(n => (
            <NotificationItem key={n.id} notif={n} onLongPress={onLongPress} />
          ))}
        </div>
      ))}
    </div>
  );
}

// Conteggi per i filter tabs (sul dataset completo)
function filterCounts(notifs) {
  const c = {};
  FILTERS.forEach(f => {
    c[f.key] = f.key === 'all'
      ? notifs.length
      : notifs.filter(n => matchFilter(n, f.key)).length;
  });
  return c;
}

const UNREAD = NOTIFICATIONS.filter(n => !n.read).length; // 5
const TOTAL = NOTIFICATIONS.length;                       // 15

// ── STATE 01 — Empty, nessuna notifica ───────────────────────────
function State01Empty() {
  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <PhoneTopBar unreadCount={0} totalCount={0} />
      <FilterTabs active="all" onChange={() => {}} counts={{ all: 0, unread: 0, mention: 0, critical: 0 }} />
      <EmptyState />
    </div>
  );
}

// ── STATE 02 — 15 notifiche miste, 5 unread, gruppi ──────────────
function State02Mixed() {
  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <PhoneTopBar unreadCount={UNREAD} totalCount={TOTAL} />
      <FilterTabs active="all" onChange={() => {}} counts={filterCounts(NOTIFICATIONS)} />
      <BulkActionsBar unreadCount={UNREAD} onMarkAll={() => {}} />
      <NotificationsFeed notifs={NOTIFICATIONS} />
    </div>
  );
}

// ── STATE 03 — Filtro "Non lette" → 5 ────────────────────────────
function State03Unread() {
  const filtered = NOTIFICATIONS.filter(n => matchFilter(n, 'unread'));
  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <PhoneTopBar unreadCount={UNREAD} totalCount={TOTAL} />
      <FilterTabs active="unread" onChange={() => {}} counts={filterCounts(NOTIFICATIONS)} />
      <NotificationsFeed notifs={filtered} />
    </div>
  );
}

// ── STATE 04 — Bulk "Segna tutte come lette" → confirm modal ─────
function State04BulkConfirm() {
  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden', position: 'relative' }}>
      <PhoneTopBar unreadCount={UNREAD} totalCount={TOTAL} />
      <FilterTabs active="all" onChange={() => {}} counts={filterCounts(NOTIFICATIONS)} />
      <BulkActionsBar unreadCount={UNREAD} onMarkAll={() => {}} />
      <NotificationsFeed notifs={NOTIFICATIONS} dim />
      <ConfirmModal count={UNREAD} onCancel={() => {}} onConfirm={() => {}} />
    </div>
  );
}

// ── STATE 05 — Long-press → action drawer (4 azioni) ─────────────
function State05ActionDrawer() {
  const target = NOTIFICATIONS[0]; // notifica critica "Davide ha annullato"
  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden', position: 'relative' }}>
      <PhoneTopBar unreadCount={UNREAD} totalCount={TOTAL} />
      <FilterTabs active="all" onChange={() => {}} counts={filterCounts(NOTIFICATIONS)} />
      <NotificationsFeed notifs={NOTIFICATIONS} dim />
      <div style={{
        position: 'absolute', inset: 0, zIndex: 5,
        background: 'rgba(0,0,0,0.22)', backdropFilter: 'blur(1px)',
      }} />
      <ActionDrawer notif={target} onClose={() => {}} />
    </div>
  );
}

// ── STATE 06 — Snooze success toast ──────────────────────────────
function State06SnoozeSuccess() {
  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden', position: 'relative' }}>
      <PhoneTopBar unreadCount={UNREAD - 1} totalCount={TOTAL} />
      <FilterTabs active="all" onChange={() => {}} counts={filterCounts(NOTIFICATIONS)} />
      {/* notifica 1 posticipata → rimossa dal feed visibile */}
      <NotificationsFeed notifs={NOTIFICATIONS.filter(n => n.id !== 1)} />
      <Toast icon="🕘" entity="agent" text="Notifica posticipata di 1 ora" />
    </div>
  );
}

// ── STATE 07 — Error network banner + retry ──────────────────────
function State07Error() {
  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <PhoneTopBar unreadCount={UNREAD} totalCount={TOTAL} />
      <FilterTabs active="all" onChange={() => {}} counts={filterCounts(NOTIFICATIONS)} />
      <ErrorBanner onRetry={() => {}} />
      <NotificationsFeed notifs={NOTIFICATIONS} />
    </div>
  );
}

// ── STATE 08 — Mobile stack (vertical, compact) ──────────────────
// Variante stack verticale: feed denso senza divisori interni, ideale
// per il "mass-check" mobile di Marco al risveglio.
function State08MobileStack() {
  const sample = NOTIFICATIONS.slice(0, 8);
  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <PhoneTopBar unreadCount={UNREAD} totalCount={TOTAL} />
      <div role="feed" aria-label="Notifiche" style={{ flex: 1, overflowY: 'auto' }}>
        {sample.map((n, i) => (
          <div key={n.id} style={{
            borderBottom: i < sample.length - 1 ? '1px solid var(--border-light)' : 'none',
          }}>
            <NotificationItem notif={n} onLongPress={() => {}} />
          </div>
        ))}
      </div>
    </div>
  );
}

// ── Export to window ─────────────────────────────────────────────
Object.assign(window, {
  State01Empty, State02Mixed, State03Unread, State04BulkConfirm,
  State05ActionDrawer, State06SnoozeSuccess, State07Error, State08MobileStack,
});

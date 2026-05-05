/* MeepleAI SP3 — Accept Invite (game night)
   Route target: /accept-invite/[token]
   Pagina che riceve un invite token via email per unirsi a una serata di
   gioco come guest player. 7 stati richiesti.

   Componenti v2 nuovi:
   - InviteHostCard (avatar+nome+welcome message, riusabile in #5 detail)
   - SessionMetaGrid (game · date · time · duration · players)
   - DeclinedShell (variante neutrale del SuccessShell)

   Riusi:
   - AuthCard, InputField, PrimaryBtn (SP1)
   - SuccessCard pattern (SP1, riusato in #1) — esteso a "accepted-success"
   - Banner (#1)
   - MeepleCardGame compact (#2)

   Decisioni di design:
   - Avatar host: ROUND, 56px sulla card, 40px nelle versioni compatte.
     Round è coerente con il sistema (player-pip, contributors sidebar in #2).
   - Token-expired CTA: link a "Contatta host" (mailto-style) — più diretto
     di /join che è alpha generica. /join rimane fallback in fondo.
   - Animazione success: confetti leggero (CSS-only, 12 particelle) — è una
     interazione celebrativa one-shot e l'utente è già al picco emotivo.
*/

const { useState, useEffect, useMemo, useRef } = React;
const DS = window.DS;

// ─── DATI MOCK INVITE ────────────────────────────────
// Host preso da DS.players[0] (Marco R., Membro Gen 2025)
const HOST = DS.players[0];
// Game preso da DS.games — scegliamo Wingspan (gradient verde, leggibile su entrambe le card)
const GAME = DS.games.find(g => g.id === 'g-wingspan') || DS.games[2];

const INVITE = {
  id: 'inv-7f3a9c',
  token: 'MOCKUP-FAKE-TOKEN-DO-NOT-USE',
  hostName: HOST.title,         // "Marco R."
  hostInitials: HOST.initials,
  hostColor: HOST.color,
  hostFav: HOST.fav,
  game: GAME,
  date: 'Sabato 9 Maggio 2026',
  dateShort: 'Sab 9 Mag',
  time: '20:30',
  duration: '90–120m',
  location: 'Casa di Marco · Milano (NoLo)',
  expectedPlayers: 4,
  acceptedSoFar: 2,
  welcomeMessage: 'Ehi! Domenica vorrei provare Wingspan con il toolkit \'Wingspan: Friendly\'. Ti aspetto :)',
  expiresAt: 'Venerdì 8 Maggio · 18:00',
};

// Already-confirmed roster (per stato "already-accepted")
const ROSTER = [
  { id: HOST.id, name: HOST.title, initials: HOST.initials, color: HOST.color, isHost: true },
  { id: 'p-sara', name: 'Sara T.', initials: 'ST', color: 320 },
  { id: 'p-andrea', name: 'Andrea P.', initials: 'AP', color: 100 },
];

// ─── BRAND MARK ──────────────────────────────────────
const BrandMark = ({ size = 44 }) => (
  <div style={{ display:'flex', alignItems:'center', gap: 9 }}>
    <div style={{
      width: size, height: size, borderRadius: 12,
      background: 'linear-gradient(135deg, hsl(var(--c-game)), hsl(var(--c-event)) 60%, hsl(var(--c-player)))',
      display:'flex', alignItems:'center', justifyContent:'center',
      color:'#fff', fontWeight: 800, fontSize: size > 40 ? 18 : 14,
      fontFamily:'var(--f-display)',
      boxShadow:'0 4px 14px hsl(var(--c-game) / .3)',
    }}>M</div>
    <span style={{
      fontFamily:'var(--f-display)', fontWeight: 700, fontSize: 14,
      letterSpacing:'-.02em', color:'var(--text)',
    }}>MeepleAI</span>
  </div>
);

// ─── BANNER (riuso #1) ───────────────────────────────
const Banner = ({ tone='info', children, action }) => {
  const colors = { success:'c-toolkit', error:'c-danger', warning:'c-warning', info:'c-info' };
  const c = colors[tone];
  const icon = tone === 'success' ? '✓' : tone === 'error' ? '✕' : tone === 'warning' ? '!' : 'ℹ';
  return (
    <div role={tone === 'error' ? 'alert' : 'status'} style={{
      display:'flex', alignItems:'flex-start', gap: 10,
      padding:'10px 12px', borderRadius:'var(--r-md)',
      background:`hsl(var(--${c}) / .1)`,
      border:`1px solid hsl(var(--${c}) / .25)`,
      color:`hsl(var(--${c}))`,
      fontSize: 12, fontWeight: 600, marginBottom: 14,
    }}>
      <span aria-hidden="true" style={{
        fontSize: 11, lineHeight: 1, flexShrink: 0,
        width: 18, height: 18, borderRadius:'50%',
        background:`hsl(var(--${c}) / .18)`,
        display:'inline-flex', alignItems:'center', justifyContent:'center',
        marginTop: 1,
      }}>{icon}</span>
      <span style={{ flex: 1, lineHeight: 1.5 }}>{children}</span>
      {action}
    </div>
  );
};

// ─── HOST AVATAR ────────────────────────────────────
const HostAvatar = ({ size = 56, host = INVITE, ring = true }) => (
  <div style={{
    width: size, height: size, borderRadius: '50%',
    background: `hsl(${host.hostColor} 60% 55%)`,
    color: '#fff',
    display:'inline-flex', alignItems:'center', justifyContent:'center',
    fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: size * 0.36,
    boxShadow: ring
      ? `0 0 0 3px var(--bg-card), 0 0 0 5px hsl(${host.hostColor} 60% 55% / .3), 0 6px 20px hsl(${host.hostColor} 60% 55% / .35)`
      : 'inset 0 1px 0 rgba(255,255,255,.15)',
    flexShrink: 0,
  }} aria-hidden="true">{host.hostInitials}</div>
);

// ═══════════════════════════════════════════════════════
// ─── INVITEHOSTCARD (NUOVO COMPONENTE V2) ────────────
// ═══════════════════════════════════════════════════════
const InviteHostCard = ({ host = INVITE, compact }) => (
  <div style={{
    display:'flex', alignItems:'flex-start', gap: 12,
    padding: compact ? '11px 12px' : '14px 14px',
    background:`hsl(${host.hostColor} 60% 55% / .07)`,
    border:`1px solid hsl(${host.hostColor} 60% 55% / .25)`,
    borderRadius:'var(--r-lg)',
  }}>
    <HostAvatar size={compact ? 38 : 44} host={host} ring={false}/>
    <div style={{ flex: 1, minWidth: 0 }}>
      <div style={{
        display:'flex', alignItems:'center', gap: 6, marginBottom: 4,
      }}>
        <span style={{
          fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
          color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
        }}>Host</span>
        <span style={{
          fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700,
          color:'var(--text)',
        }}>{host.hostName}</span>
        <span aria-hidden="true" style={{
          width: 5, height: 5, borderRadius:'50%',
          background:'hsl(var(--c-toolkit))', flexShrink: 0,
        }} title="Online"/>
      </div>
      <div style={{
        fontSize: 12, color:'var(--text-sec)', lineHeight: 1.5,
        fontStyle: host.welcomeMessage ? 'italic' : 'normal',
      }}>
        {host.welcomeMessage
          ? <>«{host.welcomeMessage}»</>
          : <span style={{ color:'var(--text-muted)' }}>Nessun messaggio dall'host.</span>
        }
      </div>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── SESSIONMETAGRID (NUOVO COMPONENTE V2) ───────────
// ═══════════════════════════════════════════════════════
const SessionMetaGrid = ({ inv = INVITE, compact }) => {
  const meta = [
    { icon:'📅', label:'Data',     value: inv.date,     mono: false },
    { icon:'🕗', label:'Ora',      value: inv.time,     mono: true  },
    { icon:'⏱',  label:'Durata',   value: inv.duration, mono: true  },
    { icon:'📍', label:'Luogo',    value: inv.location, mono: false },
    { icon:'👥', label:'Giocatori',value: `${inv.acceptedSoFar} confermati / ${inv.expectedPlayers} attesi`, mono: false },
  ];
  return (
    <ul style={{
      listStyle:'none', padding: 0, margin: 0,
      display:'flex', flexDirection:'column',
      borderRadius:'var(--r-lg)',
      border:'1px solid var(--border)',
      background:'var(--bg-muted)',
      overflow:'hidden',
    }}>
      {meta.map((m, i) => (
        <li key={m.label} style={{
          display:'flex', alignItems:'center', gap: 10,
          padding: compact ? '8px 12px' : '9px 14px',
          borderTop: i === 0 ? 'none' : '1px solid var(--border-light)',
        }}>
          <span aria-hidden="true" style={{
            width: 22, height: 22, borderRadius: 6,
            background:'var(--bg-card)',
            display:'inline-flex', alignItems:'center', justifyContent:'center',
            fontSize: 12, flexShrink: 0,
          }}>{m.icon}</span>
          <span style={{
            fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
            textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
            width: 60, flexShrink: 0,
          }}>{m.label}</span>
          <span style={{
            flex: 1, fontSize: 12, color:'var(--text)', fontWeight: 600,
            fontFamily: m.mono ? 'var(--f-mono)' : 'var(--f-body)',
            textAlign:'right',
          }}>{m.value}</span>
        </li>
      ))}
    </ul>
  );
};

// ─── GAME COMPACT CARD (riuso pattern MeepleCard #2) ─
const MeepleCardGameCompact = ({ game }) => (
  <div style={{
    display:'flex', alignItems:'center', gap: 12,
    padding: 10,
    border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)',
    background:'var(--bg-card)',
  }}>
    <div style={{
      width: 56, height: 56, borderRadius:'var(--r-md)',
      background: game.cover,
      display:'flex', alignItems:'center', justifyContent:'center',
      flexShrink: 0,
    }}>
      <span aria-hidden="true" style={{
        fontSize: 26, filter:'drop-shadow(0 2px 5px rgba(0,0,0,.25))',
      }}>{game.coverEmoji}</span>
    </div>
    <div style={{ flex: 1, minWidth: 0 }}>
      <div style={{
        fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 700,
        color:'var(--text)', marginBottom: 2,
        whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
      }}>{game.title}</div>
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
        letterSpacing:'.03em',
      }}>
        {game.year} · {game.players || '1–5'} pl · {game.duration || '40–80m'}
      </div>
    </div>
  </div>
);

// ─── INPUT FIELD (riuso da #1) ──────────────────────
const InputField = ({ label, type='text', placeholder, value, onChange, error, hint, optional, disabled }) => (
  <div style={{ marginBottom: 13 }}>
    <label style={{
      display:'flex', alignItems:'baseline', justifyContent:'space-between',
      fontSize: 10, fontWeight: 700, fontFamily:'var(--f-display)',
      color:'var(--text-sec)', marginBottom: 5,
      textTransform:'uppercase', letterSpacing:'.06em',
    }}>
      <span>{label}</span>
      {optional && <span style={{
        fontWeight: 600, color:'var(--text-muted)',
        fontFamily:'var(--f-mono)', textTransform:'none', letterSpacing: 0,
        fontSize: 10,
      }}>opzionale</span>}
    </label>
    <input
      type={type} placeholder={placeholder} value={value}
      onChange={onChange} disabled={disabled}
      style={{
        display:'block', width:'100%',
        padding:'10px 12px',
        borderRadius:'var(--r-md)',
        border: error ? '1.5px solid hsl(var(--c-danger))' : '1.5px solid var(--border)',
        background: error ? 'hsl(var(--c-danger) / .07)' : 'var(--bg)',
        color:'var(--text)', fontSize: 13, outline:'none',
        fontFamily:'var(--f-body)',
        transition:'border-color var(--dur-sm) var(--ease-out)',
        boxSizing:'border-box',
        opacity: disabled ? .65 : 1,
      }}
    />
    {error && <div style={{ fontSize: 11, color:'hsl(var(--c-danger))', marginTop: 4, fontWeight: 600 }}>{error}</div>}
    {hint && !error && <div style={{ fontSize: 11, color:'var(--text-muted)', marginTop: 4 }}>{hint}</div>}
  </div>
);

// ─── PRIMARY BTN (riuso da #1) ──────────────────────
const PrimaryBtn = ({ children, onClick, disabled, loading }) => (
  <button type="button" onClick={onClick} disabled={disabled || loading}
    style={{
      display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 8,
      width:'100%', padding:'11px 14px', fontSize: 14,
      borderRadius:'var(--r-md)',
      fontFamily:'var(--f-display)', fontWeight: 700, border:'none',
      background:'hsl(var(--c-game))', color:'#fff',
      boxShadow:'0 3px 12px hsl(var(--c-game) / .35)',
      cursor: (disabled || loading) ? 'not-allowed' : 'pointer',
      opacity: (disabled || loading) ? .7 : 1,
      transition:'transform var(--dur-sm) var(--ease-out), box-shadow var(--dur-sm)',
    }}
    onMouseDown={e => !(disabled || loading) && (e.currentTarget.style.transform='scale(.98)')}
    onMouseUp={e => (e.currentTarget.style.transform='')}
    onMouseLeave={e => (e.currentTarget.style.transform='')}
  >
    {loading && (
      <span aria-hidden="true" style={{
        width: 14, height: 14, border:'2px solid rgba(255,255,255,.4)',
        borderTopColor:'#fff', borderRadius:'50%',
        animation:'mai-spin .7s linear infinite', display:'inline-block',
      }}/>
    )}
    {children}
  </button>
);

// ─── GHOST BTN ──────────────────────────────────────
const GhostBtn = ({ children, onClick, disabled, danger }) => (
  <button type="button" onClick={onClick} disabled={disabled}
    style={{
      display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 6,
      width:'100%', padding:'9px 14px', fontSize: 13,
      borderRadius:'var(--r-md)',
      fontFamily:'var(--f-display)', fontWeight: 600,
      background:'transparent',
      border:`1px solid var(--border)`,
      color: danger ? 'hsl(var(--c-danger))' : 'var(--text-sec)',
      cursor: disabled ? 'not-allowed' : 'pointer',
      opacity: disabled ? .55 : 1,
      transition:'all var(--dur-sm) var(--ease-out)',
    }}
    onMouseEnter={e => !disabled && (e.currentTarget.style.background = 'var(--bg-hover)')}
    onMouseLeave={e => (e.currentTarget.style.background = 'transparent')}
  >{children}</button>
);

// ═══════════════════════════════════════════════════════
// ─── AUTH CARD SHELL ─────────────────────────────────
// ═══════════════════════════════════════════════════════
const AuthCard = ({ children, width = 380 }) => (
  <div style={{
    width: '100%', maxWidth: width,
    background:'var(--bg-card)',
    border:'1px solid var(--border)',
    borderRadius:'var(--r-2xl)',
    padding: '22px 22px 20px',
    boxShadow:'var(--shadow-lg)',
    boxSizing:'border-box',
  }}>{children}</div>
);

// ─── HERO LEGGERO ────────────────────────────────────
const Hero = ({ inv = INVITE, compact }) => (
  <div style={{
    textAlign:'center',
    padding: compact ? '14px 16px 6px' : '18px 24px 8px',
  }}>
    <div style={{
      display:'inline-flex', alignItems:'center', gap: 6,
      padding:'4px 10px', borderRadius:'var(--r-pill)',
      background:'hsl(var(--c-event) / .14)',
      color:'hsl(var(--c-event))',
      fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
      textTransform:'uppercase', letterSpacing:'.08em',
      marginBottom: 10,
    }}>
      <span aria-hidden="true">🎯</span>
      Game night
    </div>
    <h1 style={{
      fontFamily:'var(--f-display)', fontWeight: 800,
      fontSize: compact ? 17 : 20,
      letterSpacing:'-.01em', lineHeight: 1.2,
      color:'var(--text)', margin: '0 0 4px',
    }}>
      <strong style={{ color:'hsl(var(--c-game))' }}>{inv.hostName}</strong>{' '}ti invita a giocare a{' '}
      <strong style={{ color:'hsl(var(--c-toolkit))' }}>{inv.game.title}</strong>
    </h1>
    <p style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      letterSpacing:'.04em', margin: 0,
    }}>{inv.dateShort} · {inv.time}</p>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── ROSTER MINI (per already-accepted) ──────────────
// ═══════════════════════════════════════════════════════
const RosterMini = ({ roster = ROSTER }) => (
  <div style={{
    display:'flex', flexDirection:'column', gap: 7,
    padding:'12px',
    background:'var(--bg-muted)',
    border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)',
  }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
      marginBottom: 2,
    }}>// Confermati</div>
    {roster.map(p => (
      <div key={p.id} style={{ display:'flex', alignItems:'center', gap: 9 }}>
        <span style={{
          width: 26, height: 26, borderRadius:'50%',
          background:`hsl(${p.color} 60% 55%)`, color:'#fff',
          display:'inline-flex', alignItems:'center', justifyContent:'center',
          fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 10, flexShrink: 0,
        }} aria-hidden="true">{p.initials}</span>
        <span style={{
          flex: 1, fontSize: 12, fontWeight: 600, color:'var(--text)',
        }}>{p.name}</span>
        {p.isHost && <span style={{
          padding:'1px 7px', borderRadius:'var(--r-pill)',
          background:'hsl(var(--c-game) / .14)', color:'hsl(var(--c-game))',
          fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
          textTransform:'uppercase', letterSpacing:'.06em',
        }}>Host</span>}
      </div>
    ))}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── CONFETTI (CSS-only, 12 particles) ──────────────
// ═══════════════════════════════════════════════════════
const Confetti = () => {
  const particles = useMemo(() => Array.from({ length: 14 }, (_, i) => ({
    id: i,
    left: 5 + Math.random() * 90,
    delay: Math.random() * 0.4,
    duration: 1.4 + Math.random() * 1.0,
    color: ['c-game','c-event','c-toolkit','c-player','c-agent'][i % 5],
    size: 6 + Math.random() * 6,
    rotate: Math.random() * 360,
  })), []);
  return (
    <div aria-hidden="true" style={{
      position:'absolute', inset: 0, overflow:'hidden', pointerEvents:'none',
    }}>
      {particles.map(p => (
        <span key={p.id} style={{
          position:'absolute', top: '-12px', left: `${p.left}%`,
          width: p.size, height: p.size,
          background: `hsl(var(--${p.color}))`,
          borderRadius: p.id % 2 ? '50%' : '2px',
          transform: `rotate(${p.rotate}deg)`,
          animation: `mai-confetti ${p.duration}s cubic-bezier(.4,.7,.6,1) ${p.delay}s forwards`,
        }}/>
      ))}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── ACCEPTEDSUCCESS SHELL ───────────────────────────
// ═══════════════════════════════════════════════════════
const AcceptedSuccessShell = ({ inv = INVITE, onDismiss }) => (
  <div style={{ position:'relative', overflow:'hidden' }}>
    <Confetti/>
    <div style={{
      display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center',
      padding:'4px 4px 4px',
    }}>
      <div style={{
        width: 64, height: 64, borderRadius:'50%',
        background:'hsl(var(--c-toolkit) / .14)',
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 30, marginBottom: 14,
        boxShadow:'inset 0 0 0 3px hsl(var(--c-toolkit) / .25)',
      }} aria-hidden="true">🎲</div>
      <h2 style={{
        fontFamily:'var(--f-display)', fontSize: 19, fontWeight: 800,
        color:'var(--text)', margin:'0 0 6px', letterSpacing:'-.01em',
      }}>Ci sei!</h2>
      <p style={{
        fontSize: 12, color:'var(--text-muted)', lineHeight: 1.6, margin:'0 0 16px',
      }}>
        {inv.hostName} riceverà la tua conferma.<br/>
        Ti ricordiamo l'evento il giorno prima.
      </p>
      <div style={{
        width:'100%', padding:'12px 14px',
        background:'var(--bg-muted)', borderRadius:'var(--r-md)',
        marginBottom: 14, textAlign:'left',
        border:'1px solid var(--border-light)',
      }}>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
          color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
          marginBottom: 6,
        }}>Riepilogo</div>
        <div style={{ display:'flex', alignItems:'center', gap: 8, marginBottom: 4 }}>
          <span aria-hidden="true">🎲</span>
          <strong style={{ fontFamily:'var(--f-display)', fontSize: 14, color:'var(--text)' }}>{inv.game.title}</strong>
        </div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
          letterSpacing:'.04em',
        }}>{inv.dateShort} · {inv.time} · {inv.location.split(' · ')[0]}</div>
      </div>
      <PrimaryBtn onClick={onDismiss}>Vai alla sessione →</PrimaryBtn>
      <button type="button" onClick={onDismiss} style={{
        marginTop: 10, padding: 8, fontSize: 11,
        background:'transparent', border:'none',
        color:'var(--text-muted)', textDecoration:'underline',
        cursor:'pointer', fontFamily:'var(--f-body)',
      }}>Aggiungi al calendario (.ics)</button>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── DECLINED SHELL ──────────────────────────────────
// ═══════════════════════════════════════════════════════
const DeclinedShell = ({ inv = INVITE, onUndo }) => (
  <div style={{
    display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center',
    padding:'4px 4px 4px',
  }}>
    <div style={{
      width: 60, height: 60, borderRadius:'50%',
      background:'var(--bg-muted)',
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 26, marginBottom: 14,
      border:'1px solid var(--border)',
    }} aria-hidden="true">🌙</div>
    <h2 style={{
      fontFamily:'var(--f-display)', fontSize: 18, fontWeight: 700,
      color:'var(--text)', margin:'0 0 6px',
    }}>Ok, sarà per la prossima!</h2>
    <p style={{
      fontSize: 12, color:'var(--text-muted)', lineHeight: 1.6, margin:'0 0 16px',
    }}>
      {inv.hostName} riceverà la tua risposta.<br/>
      Nessun problema — ti aspettiamo per la prossima serata.
    </p>
    <div style={{ display:'flex', flexDirection:'column', gap: 8, width:'100%' }}>
      <a href="#" onClick={e => e.preventDefault()} style={{
        display:'inline-flex', alignItems:'center', justifyContent:'center',
        padding:'10px 14px', borderRadius:'var(--r-md)',
        background:'var(--bg-muted)', color:'var(--text)',
        fontFamily:'var(--f-display)', fontWeight: 700, fontSize: 13,
        textDecoration:'none',
        border:'1px solid var(--border)',
      }}>Torna alla home →</a>
      <button onClick={onUndo} style={{
        padding: 8, fontSize: 11,
        background:'transparent', border:'none',
        color:'var(--text-muted)', textDecoration:'underline',
        cursor:'pointer', fontFamily:'var(--f-body)',
      }}>Ho cambiato idea, accetta l'invito</button>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── INVITE BODY (default | logged-in | error stati) ─
// ═══════════════════════════════════════════════════════
const InviteBody = ({ stateOverride }) => {
  const [inner, setInner] = useState(stateOverride);
  useEffect(() => setInner(stateOverride), [stateOverride]);

  // ── Stato accepted-success
  if (inner === 'accepted-success') {
    return <AcceptedSuccessShell onDismiss={() => setInner(stateOverride)}/>;
  }

  // ── Stato declined
  if (inner === 'declined') {
    return <DeclinedShell onUndo={() => setInner('logged-in')}/>;
  }

  // ── Errori (token-expired | token-invalid | already-accepted)
  if (inner === 'token-expired') {
    return (
      <div>
        <Banner tone="error">
          Questo invito è <strong>scaduto</strong> il {INVITE.expiresAt}.
        </Banner>
        <p style={{
          fontSize: 12, color:'var(--text-sec)', lineHeight: 1.6,
          margin:'0 0 14px', textAlign:'center',
        }}>
          Per partecipare alla serata, contatta direttamente l'host
          per ricevere un nuovo invito.
        </p>
        <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
          <PrimaryBtn onClick={() => {}}>✉ Contatta {INVITE.hostName}</PrimaryBtn>
          <a href="#" onClick={e=>e.preventDefault()} style={{
            display:'inline-flex', alignItems:'center', justifyContent:'center',
            padding:'9px 14px', borderRadius:'var(--r-md)',
            color:'var(--text-muted)', fontSize: 12,
            textDecoration:'underline', fontFamily:'var(--f-body)',
          }}>Torna alla home</a>
        </div>
      </div>
    );
  }

  if (inner === 'token-invalid') {
    return (
      <div>
        <Banner tone="error">
          Questo invito non è valido o è già stato utilizzato.
        </Banner>
        <p style={{
          fontSize: 12, color:'var(--text-sec)', lineHeight: 1.6,
          margin:'0 0 14px', textAlign:'center',
        }}>
          Possibili cause:
        </p>
        <ul style={{
          fontSize: 12, color:'var(--text-muted)', lineHeight: 1.7,
          paddingLeft: 18, margin:'0 0 16px',
        }}>
          <li>Il link è stato copiato in modo incompleto</li>
          <li>L'invito è già stato accettato da un altro device</li>
          <li>L'host ha revocato l'invito</li>
        </ul>
        <PrimaryBtn onClick={() => {}}>Torna alla home →</PrimaryBtn>
      </div>
    );
  }

  if (inner === 'already-accepted') {
    return (
      <div>
        <Banner tone="info">
          Hai <strong>già confermato</strong> la partecipazione a questa serata.
        </Banner>
        <RosterMini/>
        <div style={{ marginTop: 14, display:'flex', flexDirection:'column', gap: 8 }}>
          <PrimaryBtn onClick={() => {}}>Vai alla sessione →</PrimaryBtn>
          <GhostBtn onClick={() => setInner('declined')} danger>
            Disdici la partecipazione
          </GhostBtn>
        </div>
      </div>
    );
  }

  // ── Stato default (non loggato) | logged-in
  const isLogged = inner === 'logged-in';
  return (
    <div>
      <MeepleCardGameCompact game={INVITE.game}/>
      <div style={{ height: 12 }}/>
      <SessionMetaGrid/>
      <div style={{ height: 12 }}/>
      <InviteHostCard/>
      <div style={{ height: 16 }}/>

      {!isLogged && (
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
          color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
          marginBottom: 8, paddingTop: 4,
          borderTop:'1px dashed var(--border)',
        }}>// Per accettare, accedi o registrati</div>
      )}

      <div style={{ display:'flex', flexDirection:'column', gap: 9 }}>
        <PrimaryBtn onClick={() => setInner('accepted-success')}>
          {isLogged ? '✓ Accetta invito' : 'Accetta e accedi →'}
        </PrimaryBtn>
        <GhostBtn onClick={() => setInner('declined')}>Non posso partecipare</GhostBtn>
      </div>

      {!isLogged && (
        <p style={{
          fontSize: 11, color:'var(--text-muted)', lineHeight: 1.55,
          margin:'12px 0 0', textAlign:'center',
        }}>
          Hai già un account? <a href="#" onClick={e=>e.preventDefault()} style={{
            color:'hsl(var(--c-game))', fontWeight: 600, textDecoration:'none',
          }}>Accedi</a> per accettare in 1 click.
        </p>
      )}
    </div>
  );
};

// ─── PAGE WRAPPER ────────────────────────────────────
const InvitePage = ({ stateOverride, compact }) => (
  <div style={{
    minHeight:'100%',
    background:'radial-gradient(80% 60% at 50% 0%, hsl(var(--c-game) / .06), transparent 70%)',
    display:'flex', flexDirection:'column',
    padding: compact ? '12px 0 18px' : '20px 0 32px',
  }}>
    <div style={{ padding: compact ? '0 16px 8px' : '0 24px 12px' }}>
      <BrandMark/>
    </div>
    <Hero compact={compact}/>
    <div style={{
      display:'flex', justifyContent:'center',
      padding: compact ? '8px 14px 0' : '12px 24px 0',
    }}>
      <AuthCard width={compact ? '100%' : 380}>
        <InviteBody stateOverride={stateOverride}/>
      </AuthCard>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── MOBILE 375 SCREEN ───────────────────────────────
// ═══════════════════════════════════════════════════════
const PhoneSbar = () => (
  <div className="phone-sbar">
    <span style={{ fontFamily:'var(--f-mono)' }}>14:32</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);

const InviteMobileScreen = ({ stateOverride }) => (
  <>
    <PhoneSbar/>
    <div style={{
      flex: 1, overflowY:'auto', scrollbarWidth:'none',
      background:'var(--bg)',
    }}>
      <InvitePage stateOverride={stateOverride} compact/>
    </div>
  </>
);

const PhoneShell = ({ label, desc, children }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 10 }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}</div>
    <div className="phone">{children}</div>
    {desc && <div style={{
      fontSize: 11, color:'var(--text-muted)', maxWidth: 340,
      textAlign:'center', lineHeight: 1.55,
    }}>{desc}</div>}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── DESKTOP 1440 SCREEN ─────────────────────────────
// ═══════════════════════════════════════════════════════
const DesktopFrame = ({ children, label, desc }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 12, width:'100%' }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}</div>
    <div style={{
      width:'100%', maxWidth: 1180,
      borderRadius:'var(--r-xl)',
      border:'1px solid var(--border)',
      background:'var(--bg)',
      overflow:'hidden',
      boxShadow:'var(--shadow-lg)',
    }}>
      <div style={{
        display:'flex', alignItems:'center', gap: 8,
        padding:'9px 14px',
        background:'var(--bg-muted)',
        borderBottom:'1px solid var(--border)',
        fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      }}>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-event))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-warning))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-toolkit))' }}/>
        <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/accept-invite/{INVITE.token.slice(0, 8)}…</span>
      </div>
      {children}
    </div>
    {desc && <div style={{
      fontSize: 11, color:'var(--text-muted)', maxWidth: 720,
      textAlign:'center', lineHeight: 1.55,
    }}>{desc}</div>}
  </div>
);

// Desktop layout: split — left big game card + roster context, right form
const InviteDesktopScreen = ({ stateOverride }) => (
  <div style={{
    minHeight: 640,
    background:'radial-gradient(75% 55% at 50% -10%, hsl(var(--c-game) / .08), transparent 70%)',
    padding:'20px 32px 32px',
  }}>
    <div style={{ marginBottom: 16 }}><BrandMark/></div>
    <div style={{
      display:'grid', gridTemplateColumns:'1.1fr 1fr', gap: 32,
      maxWidth: 980, margin:'0 auto',
      alignItems:'start',
    }}>
      {/* LEFT — game hero + meta */}
      <div>
        <div style={{
          display:'inline-flex', alignItems:'center', gap: 6,
          padding:'4px 10px', borderRadius:'var(--r-pill)',
          background:'hsl(var(--c-event) / .14)',
          color:'hsl(var(--c-event))',
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
          textTransform:'uppercase', letterSpacing:'.08em',
          marginBottom: 12,
        }}>
          <span aria-hidden="true">🎯</span>Game night
        </div>
        <h1 style={{
          fontFamily:'var(--f-display)', fontWeight: 800,
          fontSize: 28, letterSpacing:'-.02em', lineHeight: 1.15,
          color:'var(--text)', margin: '0 0 6px',
        }}>
          <strong style={{ color:'hsl(var(--c-game))' }}>{INVITE.hostName}</strong>{' '}ti invita a giocare a{' '}
          <strong style={{ color:'hsl(var(--c-toolkit))' }}>{INVITE.game.title}</strong>
        </h1>
        <p style={{
          fontFamily:'var(--f-body)', fontSize: 14, color:'var(--text-sec)',
          lineHeight: 1.55, margin:'0 0 18px',
        }}>
          {INVITE.date} · {INVITE.time} · {INVITE.location}
        </p>

        {/* Big game card */}
        <div style={{
          borderRadius:'var(--r-xl)', overflow:'hidden',
          border:'1px solid var(--border)',
          background:'var(--bg-card)',
          marginBottom: 14,
          boxShadow:'var(--shadow-xs)',
        }}>
          <div style={{
            height: 120,
            background: INVITE.game.cover,
            display:'flex', alignItems:'center', justifyContent:'center',
          }}>
            <span aria-hidden="true" style={{
              fontSize: 48, filter:'drop-shadow(0 3px 8px rgba(0,0,0,.25))',
            }}>{INVITE.game.coverEmoji}</span>
          </div>
          <div style={{ padding:'12px 14px' }}>
            <div style={{
              fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 700,
              color:'var(--text)', marginBottom: 3,
            }}>{INVITE.game.title}</div>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
              letterSpacing:'.04em',
            }}>{INVITE.game.year} · {INVITE.game.players || '1–5'} pl · {INVITE.game.duration || '40–80m'}</div>
          </div>
        </div>

        <SessionMetaGrid/>
      </div>

      {/* RIGHT — invite card */}
      <div>
        <AuthCard width="100%">
          <InviteBody stateOverride={stateOverride}/>
        </AuthCard>
      </div>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── ROOT ────────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const MOBILE_STATES = [
  { id: 'default',           label: '01 · Default (non loggato)',   desc: 'Token valido. CTA "Accetta e accedi" → flow login dopo. Link "Hai già account? Accedi".' },
  { id: 'logged-in',         label: '02 · Logged-in',               desc: 'Utente loggato. CTA secca "Accetta invito" — 1 click conferma.' },
  { id: 'accepted-success',  label: '03 · Accepted (success)',      desc: 'Confetti CSS one-shot + riepilogo. CTA "Vai alla sessione". Link .ics calendar.' },
  { id: 'declined',          label: '04 · Declined',                desc: 'Tono neutro non-shame. Link "ho cambiato idea" per undo.' },
  { id: 'token-expired',     label: '05 · Token expired',           desc: 'Banner error + CTA "Contatta host" (mailto). Niente /join che è alpha generica.' },
  { id: 'token-invalid',     label: '06 · Token invalid',           desc: 'Banner error + checklist diagnostica (link rotto, già usato, revocato).' },
  { id: 'already-accepted',  label: '07 · Already accepted',        desc: 'Roster confermati visibili. CTA "vai alla sessione" + ghost "disdici".' },
];

function ThemeToggle() {
  const [dark, setDark] = useState(false);
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
  }, [dark]);
  return (
    <button onClick={() => setDark(d => !d)} className="theme-toggle"
      aria-label={dark ? 'Passa a tema chiaro' : 'Passa a tema scuro'}>
      <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span>
      <span>{dark ? 'Dark' : 'Light'}</span>
    </button>
  );
}

function App() {
  return (
    <div className="stage">
      <ThemeToggle/>
      <div className="stage-wrap">
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
          textTransform:'uppercase', letterSpacing:'.08em', marginBottom: 8,
        }}>SP3 · Public Secondary · #3</div>
        <h1>Accept Invite — Game night</h1>
        <p className="lead">
          <code style={{ background:'var(--bg-muted)', padding:'2px 7px', borderRadius:'var(--r-sm)', fontSize: 12 }}>/accept-invite/[token]</code>
          {' · '}7 stati: 2 happy-path (non loggato + loggato) · 2 outcome (accepted+confetti, declined) · 3 errori token (expired, invalid, already-accepted).
          Componenti v2 nuovi: <strong>InviteHostCard</strong>, <strong>SessionMetaGrid</strong>, <strong>RosterMini</strong>.
          Riusa AuthCard, PrimaryBtn, Banner, MeepleCardGameCompact.
        </p>

        <div className="section-label">Mobile · 375 — 7 stati</div>
        <div className="phones-grid">
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc}>
              <InviteMobileScreen stateOverride={s.id}/>
            </PhoneShell>
          ))}
        </div>

        <div className="section-label">Desktop · 1440 — split layout</div>
        <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
          <DesktopFrame label="08 · Desktop · Default (non loggato)"
            desc="Layout split. LEFT: hero copy + game cover hero (120px) + SessionMetaGrid. RIGHT: AuthCard 380 con InviteHostCard + CTA. Sopra-fold complete: niente scroll richiesto.">
            <InviteDesktopScreen stateOverride="default"/>
          </DesktopFrame>
          <DesktopFrame label="09 · Desktop · Accepted (success)"
            desc="Stessa griglia, RIGHT diventa SuccessShell con confetti animation. LEFT context rimane visibile per dare permanenza al risultato.">
            <InviteDesktopScreen stateOverride="accepted-success"/>
          </DesktopFrame>
          <DesktopFrame label="10 · Desktop · Token expired"
            desc="LEFT hero rimane (l'utente capisce a cosa è stato invitato), RIGHT mostra error state + CTA contatta host.">
            <InviteDesktopScreen stateOverride="token-expired"/>
          </DesktopFrame>
        </div>
      </div>

      <style>{`
        @keyframes mai-spin { to { transform: rotate(360deg); } }
        @keyframes mai-confetti {
          0%   { transform: translate3d(0, 0, 0) rotate(0deg); opacity: 0; }
          10%  { opacity: 1; }
          100% { transform: translate3d(var(--mai-cx, 0), 280px, 0) rotate(540deg); opacity: 0; }
        }
        .phones-grid {
          display: grid;
          grid-template-columns: repeat(auto-fill, minmax(380px, 1fr));
          gap: var(--s-7);
          align-items: start;
        }
        .stage h1 { font-size: var(--fs-3xl); }
        .stage .lead { line-height: var(--lh-relax); }
        .phone > div::-webkit-scrollbar { display: none; }
        button:focus-visible, a:focus-visible, input:focus-visible {
          outline: 2px solid hsl(var(--c-game));
          outline-offset: 2px;
        }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);

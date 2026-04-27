/* MeepleAI SP3 — Join / Waitlist Alpha
   Route target: /join
   Pagina pubblica per richiedere accesso all'alpha privata. Form waitlist
   (email, nome, gioco preferito, newsletter) con 5 stati: default, submitting,
   success, error, already-on-list. Riusa AuthCard/InputField/PwdInput/SuccessCard
   pattern; introduce GamePreferenceSelect (top 10 + Altro).

   Caricato da sp3-join.html via Babel standalone, stessa tecnica di auth-flow.jsx. */

const { useState, useEffect, useRef, useMemo } = React;

// ─── DATI ────────────────────────────────────────────
// Top 10 giochi preferiti (riusa data.js + qualche extra realistico)
const TOP_GAMES = [
  { id: 'g-azul', label: 'Azul', emoji: '🔷' },
  { id: 'g-catan', label: 'I Coloni di Catan', emoji: '🌾' },
  { id: 'g-wingspan', label: 'Wingspan', emoji: '🦜' },
  { id: 'g-brass', label: 'Brass: Birmingham', emoji: '🏭' },
  { id: 'g-gloomhaven', label: 'Gloomhaven', emoji: '⚔️' },
  { id: 'g-arknova', label: 'Ark Nova', emoji: '🦒' },
  { id: 'g-spirit', label: 'Spirit Island', emoji: '🌋' },
  { id: 'g-7wonders', label: '7 Wonders Duel', emoji: '🏛️' },
  { id: 'g-carcassonne', label: 'Carcassonne', emoji: '🏰' },
  { id: 'g-ticket', label: 'Ticket to Ride', emoji: '🚂' },
  { id: 'g-other', label: 'Altro…', emoji: '✦' },
];

const ALPHA_FEATURES = [
  { e: 'kb', emoji: '📚', title: 'Library smart',
    desc: 'Aggiungi un gioco e l\'AI indicizza il manuale in 60 secondi.' },
  { e: 'agent', emoji: '🤖', title: 'Agenti AI',
    desc: 'Un esperto dedicato per ogni gioco. Cita la pagina del PDF.' },
  { e: 'session', emoji: '🎯', title: 'Session live',
    desc: 'Timer, punteggio, storico — tutto durante la partita.' },
];

// ─── BRAND MARK (clone da auth-flow) ─────────────────
const BrandMark = ({ size = 52 }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 6 }}>
    <div style={{
      width: size, height: size, borderRadius: size > 40 ? 16 : 12,
      background: 'linear-gradient(135deg, hsl(var(--c-game)), hsl(var(--c-event)) 60%, hsl(var(--c-player)))',
      display:'flex', alignItems:'center', justifyContent:'center',
      color:'#fff', fontWeight: 800, fontSize: size > 40 ? 24 : 18,
      fontFamily:'var(--f-display)',
      boxShadow:'0 6px 20px hsl(var(--c-game) / .35)',
      flexShrink: 0,
    }}>M</div>
    <span style={{
      fontFamily:'var(--f-display)', fontWeight: 700, fontSize: size > 40 ? 16 : 13,
      letterSpacing:'-.02em', color:'var(--text)',
    }}>MeepleAI</span>
  </div>
);

// ─── ALPHA BADGE PILL ────────────────────────────────
const AlphaPill = () => (
  <div style={{
    display:'inline-flex', alignItems:'center', gap: 6,
    padding:'4px 10px', borderRadius:'var(--r-pill)',
    background:'hsl(var(--c-event) / .14)',
    color:'hsl(var(--c-event))',
    fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
    textTransform:'uppercase', letterSpacing:'.08em',
  }}>
    <span style={{ width: 6, height: 6, borderRadius:'50%', background:'hsl(var(--c-event))' }}/>
    Alpha privata
  </div>
);

// ─── INPUT FIELD (cloned + minor tweaks) ─────────────
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
      type={type}
      placeholder={placeholder}
      value={value}
      onChange={onChange}
      disabled={disabled}
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

// ─── GAMEPREFERENCESELECT (NUOVO COMPONENTE V2) ──────
// Combobox-like: pulsante che apre un popover-listbox con i top 10 + "Altro".
// Quando si sceglie "Altro" si rivela un input testuale. Tastiera-friendly.
const GamePreferenceSelect = ({ value, onChange, otherText, onOtherText, error }) => {
  const [open, setOpen] = useState(false);
  const [activeIdx, setActiveIdx] = useState(0);
  const btnRef = useRef(null);
  const popRef = useRef(null);

  const selected = TOP_GAMES.find(g => g.id === value);
  const isOther = value === 'g-other';

  // Click outside
  useEffect(() => {
    if (!open) return;
    const onDoc = (e) => {
      if (!btnRef.current?.contains(e.target) && !popRef.current?.contains(e.target)) setOpen(false);
    };
    document.addEventListener('mousedown', onDoc);
    return () => document.removeEventListener('mousedown', onDoc);
  }, [open]);

  const onKey = (e) => {
    if (!open && (e.key === 'Enter' || e.key === ' ' || e.key === 'ArrowDown')) {
      e.preventDefault();
      setOpen(true);
      const i = TOP_GAMES.findIndex(g => g.id === value);
      setActiveIdx(i >= 0 ? i : 0);
      return;
    }
    if (!open) return;
    if (e.key === 'Escape') { e.preventDefault(); setOpen(false); btnRef.current?.focus(); }
    else if (e.key === 'ArrowDown') { e.preventDefault(); setActiveIdx(i => Math.min(i + 1, TOP_GAMES.length - 1)); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActiveIdx(i => Math.max(i - 1, 0)); }
    else if (e.key === 'Enter') {
      e.preventDefault();
      onChange(TOP_GAMES[activeIdx].id);
      setOpen(false);
      btnRef.current?.focus();
    }
  };

  return (
    <div style={{ marginBottom: 13 }}>
      <label style={{
        display:'block', fontSize: 10, fontWeight: 700, fontFamily:'var(--f-display)',
        color:'var(--text-sec)', marginBottom: 5,
        textTransform:'uppercase', letterSpacing:'.06em',
      }}>Quale gioco vorresti un agente per?</label>

      <div style={{ position:'relative' }}>
        <button
          ref={btnRef}
          type="button"
          onClick={() => setOpen(v => !v)}
          onKeyDown={onKey}
          aria-haspopup="listbox"
          aria-expanded={open}
          aria-label="Seleziona gioco preferito"
          style={{
            display:'flex', alignItems:'center', justifyContent:'space-between',
            width:'100%', padding:'10px 12px',
            borderRadius:'var(--r-md)',
            border: error ? '1.5px solid hsl(var(--c-danger))' : '1.5px solid var(--border)',
            background: error ? 'hsl(var(--c-danger) / .07)' : 'var(--bg)',
            color:'var(--text)', fontSize: 13, fontFamily:'var(--f-body)',
            cursor:'pointer', textAlign:'left',
            outline:'none',
            transition:'border-color var(--dur-sm) var(--ease-out)',
          }}>
          <span style={{ display:'flex', alignItems:'center', gap: 8 }}>
            {selected ? (
              <>
                <span aria-hidden="true" style={{ fontSize: 16, lineHeight: 1 }}>{selected.emoji}</span>
                <span>{selected.label}</span>
              </>
            ) : (
              <span style={{ color:'var(--text-muted)' }}>Scegli un gioco…</span>
            )}
          </span>
          <span aria-hidden="true" style={{
            fontFamily:'var(--f-mono)', color:'var(--text-muted)', fontSize: 11,
            transform: open ? 'rotate(180deg)' : 'none',
            transition:'transform var(--dur-sm)',
          }}>▾</span>
        </button>

        {open && (
          <div
            ref={popRef}
            role="listbox"
            aria-label="Lista giochi"
            style={{
              position:'absolute', top:'calc(100% + 4px)', left: 0, right: 0,
              maxHeight: 240, overflowY:'auto',
              background:'var(--bg-card)',
              border:'1px solid var(--border)',
              borderRadius:'var(--r-md)',
              boxShadow:'var(--shadow-lg)',
              zIndex: 5,
              padding: 4,
            }}>
            {TOP_GAMES.map((g, i) => {
              const isSel = g.id === value;
              const isAct = i === activeIdx;
              const isOtherRow = g.id === 'g-other';
              return (
                <div
                  key={g.id}
                  role="option"
                  aria-selected={isSel}
                  onClick={() => { onChange(g.id); setOpen(false); btnRef.current?.focus(); }}
                  onMouseEnter={() => setActiveIdx(i)}
                  style={{
                    display:'flex', alignItems:'center', gap: 9,
                    padding:'7px 10px', borderRadius:'var(--r-sm)',
                    cursor:'pointer', fontSize: 13, color:'var(--text)',
                    background: isAct ? 'var(--bg-hover)' : 'transparent',
                    fontWeight: isSel ? 700 : 500,
                    fontFamily: isOtherRow ? 'var(--f-mono)' : 'var(--f-body)',
                    borderTop: isOtherRow ? '1px solid var(--border-light)' : 'none',
                    marginTop: isOtherRow ? 4 : 0,
                    paddingTop: isOtherRow ? 11 : 7,
                  }}>
                  <span aria-hidden="true" style={{ fontSize: 15, lineHeight: 1 }}>{g.emoji}</span>
                  <span style={{ flex: 1 }}>{g.label}</span>
                  {isSel && <span aria-hidden="true" style={{ color:'hsl(var(--c-game))', fontWeight: 800 }}>✓</span>}
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* Other text input (rivelato solo se "Altro") */}
      {isOther && (
        <input
          type="text"
          autoFocus
          value={otherText}
          onChange={onOtherText}
          placeholder="Es. Terraforming Mars"
          style={{
            display:'block', width:'100%', marginTop: 8,
            padding:'9px 12px',
            borderRadius:'var(--r-md)',
            border:'1.5px solid var(--border)',
            background:'var(--bg)',
            color:'var(--text)', fontSize: 13, outline:'none',
            fontFamily:'var(--f-body)',
            boxSizing:'border-box',
          }}
        />
      )}

      {error && <div style={{ fontSize: 11, color:'hsl(var(--c-danger))', marginTop: 4, fontWeight: 600 }}>{error}</div>}
    </div>
  );
};

// ─── CHECKBOX RIGA ───────────────────────────────────
const CheckRow = ({ checked, onChange, children }) => (
  <label style={{ display:'flex', alignItems:'flex-start', gap: 8, cursor:'pointer', marginBottom: 14 }}>
    <input
      type="checkbox"
      checked={checked}
      onChange={onChange}
      style={{
        marginTop: 2, accentColor:'hsl(var(--c-game))',
        width: 14, height: 14, flexShrink: 0, cursor:'pointer',
      }}
    />
    <span style={{ fontSize: 11, color:'var(--text-sec)', lineHeight: 1.55 }}>{children}</span>
  </label>
);

// ─── PRIMARY BTN (con stato loading) ─────────────────
const PrimaryBtn = ({ children, onClick, disabled, loading }) => (
  <button
    type="button"
    onClick={onClick}
    disabled={disabled || loading}
    style={{
      display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 8,
      width:'100%',
      padding:'11px 14px', fontSize: 14, borderRadius:'var(--r-md)',
      fontFamily:'var(--f-display)', fontWeight: 700,
      border: 'none',
      background: 'hsl(var(--c-game))',
      color: '#fff',
      opacity: (disabled || loading) ? .65 : 1,
      cursor: (disabled || loading) ? 'not-allowed' : 'pointer',
      transition:'all var(--dur-sm) var(--ease-out)',
      boxShadow: loading ? 'none' : '0 4px 14px hsl(var(--c-game) / .3)',
    }}>
    {loading && <Spinner/>}
    {children}
  </button>
);

const Spinner = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" aria-hidden="true" style={{
    animation:'mai-spin 0.9s linear infinite',
  }}>
    <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="2.5" fill="none" opacity=".25"/>
    <path d="M21 12a9 9 0 0 0-9-9" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" fill="none"/>
  </svg>
);

// ─── BANNER (success / error / info) ─────────────────
const Banner = ({ tone='info', children }) => {
  const colors = {
    success: 'c-toolkit',
    error:   'c-danger',
    warning: 'c-warning',
    info:    'c-info',
  };
  const c = colors[tone];
  return (
    <div role={tone === 'error' ? 'alert' : 'status'} style={{
      display:'flex', alignItems:'flex-start', gap: 9,
      padding:'10px 12px',
      borderRadius:'var(--r-md)',
      background:`hsl(var(--${c}) / .1)`,
      border:`1px solid hsl(var(--${c}) / .25)`,
      color:`hsl(var(--${c}))`,
      fontSize: 12, fontWeight: 600,
      marginBottom: 12,
    }}>
      <span aria-hidden="true" style={{ fontSize: 14, lineHeight: 1.2, flexShrink: 0 }}>
        {tone === 'success' ? '✓' : tone === 'error' ? '✕' : tone === 'warning' ? '!' : 'ℹ'}
      </span>
      <span style={{ lineHeight: 1.45 }}>{children}</span>
    </div>
  );
};

// ─── FEATURE MINI-CARD ───────────────────────────────
const FeatureCard = ({ feat }) => (
  <div className={`e-${feat.e}`} style={{
    background:'var(--bg-card)',
    border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)',
    padding:'14px 13px',
    display:'flex', flexDirection:'column', gap: 7,
    boxShadow:'var(--shadow-xs)',
  }}>
    <div style={{
      width: 30, height: 30, borderRadius: 9,
      background:'hsl(var(--e) / .14)',
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 16,
    }} aria-hidden="true">{feat.emoji}</div>
    <div style={{
      fontFamily:'var(--f-display)', fontWeight: 700, fontSize: 13,
      color:'var(--text)',
    }}>{feat.title}</div>
    <div style={{
      fontSize: 11, color:'var(--text-sec)', lineHeight: 1.5,
    }}>{feat.desc}</div>
    <div style={{
      marginTop: 'auto',
      display:'inline-flex', alignSelf:'flex-start',
      padding:'2px 7px', borderRadius:'var(--r-sm)',
      background:'hsl(var(--e) / .12)', color:'hsl(var(--e))',
      fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
      textTransform:'uppercase', letterSpacing:'.06em',
    }}>{feat.e}</div>
  </div>
);

// ─── HERO TEXT ───────────────────────────────────────
const HeroText = ({ compact }) => (
  <div style={{ textAlign: compact ? 'left' : 'center', marginBottom: compact ? 16 : 22 }}>
    <div style={{ display:'flex', justifyContent: compact ? 'flex-start' : 'center', marginBottom: 14 }}>
      <AlphaPill/>
    </div>
    <h1 style={{
      fontFamily:'var(--f-display)', fontWeight: 800,
      fontSize: compact ? 22 : 26,
      letterSpacing:'-.02em', lineHeight: 1.15,
      color:'var(--text)',
      margin:'0 0 8px',
    }}>
      MeepleAI è in <span style={{
        background:'linear-gradient(95deg, hsl(var(--c-game)), hsl(var(--c-event)))',
        WebkitBackgroundClip:'text', WebkitTextFillColor:'transparent',
        backgroundClip:'text', color:'transparent',
      }}>alpha privata</span>
    </h1>
    <p style={{
      fontFamily:'var(--f-body)', fontSize: 13,
      color:'var(--text-sec)', lineHeight: 1.55,
      margin: 0, maxWidth: compact ? 360 : 320,
      marginLeft: compact ? 0 : 'auto', marginRight: compact ? 0 : 'auto',
    }}>
      Stiamo aprendo l'accesso a piccoli gruppi di boardgamer.
      Lascia la tua email — ti contattiamo non appena c'è posto.
    </p>
  </div>
);

// ─── JOIN FORM (cuore della pagina, riusabile) ───────
// Riceve `state` esterno per forzare la varianti del preview.
const JoinForm = ({ stateOverride }) => {
  // GDPR Art. 7 + EDPB: opt-in esplicito, MAI pre-flagged
  const [form, setForm] = useState({ email:'', name:'', game:'', other:'', news: false });
  const [errors, setErrors] = useState({});
  const [innerState, setInnerState] = useState('default');
  const state = stateOverride || innerState;

  const set = (k) => (e) => setForm(f => ({ ...f, [k]: e.target.value }));
  const setGame = (id) => setForm(f => ({ ...f, game: id, other: id === 'g-other' ? f.other : '' }));

  const submit = () => {
    const e = {};
    if (!form.email.includes('@')) e.email = 'Email non valida';
    if (!form.game) e.game = 'Seleziona un gioco';
    if (form.game === 'g-other' && form.other.trim().length < 2) e.game = 'Specifica il gioco';
    setErrors(e);
    if (Object.keys(e).length === 0) {
      setInnerState('submitting');
      setTimeout(() => setInnerState('success'), 1100);
    }
  };

  // ── Stato success
  if (state === 'success') {
    return (
      <div style={{
        display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center',
        padding:'8px 4px 4px',
      }}>
        <div style={{
          width: 64, height: 64, borderRadius:'50%',
          background:'hsl(var(--c-toolkit) / .14)',
          display:'flex', alignItems:'center', justifyContent:'center',
          fontSize: 30, marginBottom: 14,
        }} aria-hidden="true">🎲</div>
        <h2 style={{
          fontFamily:'var(--f-display)', fontSize: 19, fontWeight: 700,
          color:'var(--text)', margin:'0 0 6px',
        }}>Sei in lista!</h2>
        <p style={{
          fontSize: 12, color:'var(--text-muted)', lineHeight: 1.6, margin:'0 0 16px',
        }}>
          Ti contattiamo entro <strong style={{ color:'var(--text)' }}>2–3 settimane</strong>.<br/>
          Intanto controlla la mail per la conferma.
        </p>
        <div style={{
          width:'100%', padding:'10px 12px',
          background:'var(--bg-muted)', borderRadius:'var(--r-md)',
          marginBottom: 14, textAlign:'left',
        }}>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
            color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
            marginBottom: 4,
          }}>La tua posizione</div>
          <div style={{
            fontFamily:'var(--f-display)', fontSize: 22, fontWeight: 800,
            color:'hsl(var(--c-game))', lineHeight: 1.1,
          }}>#247 <span style={{
            fontSize: 11, color:'var(--text-muted)', fontWeight: 600,
            fontFamily:'var(--f-mono)', letterSpacing:'.04em',
          }}>· stima 2 settimane</span></div>
        </div>
        <button type="button" onClick={() => { setInnerState('default'); setForm({ email:'', name:'', game:'', other:'', news: true }); }}
          style={{
            display:'inline-flex', alignItems:'center', justifyContent:'center',
            width:'100%', padding:'10px 14px', fontSize: 13,
            borderRadius:'var(--r-md)',
            fontFamily:'var(--f-display)', fontWeight: 700,
            background:'transparent', border:'1px solid var(--border)',
            color:'var(--text-sec)', cursor:'pointer',
          }}>Iscrivi un'altra email</button>
      </div>
    );
  }

  // Variante "already-on-list"
  const showAlreadyBanner = state === 'already-on-list';
  const showErrorBanner   = state === 'error';
  const isSubmitting      = state === 'submitting';

  return (
    <div>
      {showAlreadyBanner && (
        <Banner tone="warning">
          Questa email è <strong>già in lista</strong> — sei alla posizione <strong>#247</strong>.
          Stima ingresso: <strong>2 settimane</strong>.
        </Banner>
      )}
      {showErrorBanner && (
        <Banner tone="error">
          Qualcosa è andato storto. Controlla la connessione e riprova fra qualche secondo.
        </Banner>
      )}

      <InputField
        label="Email"
        type="email"
        placeholder="tu@email.com"
        value={form.email}
        onChange={set('email')}
        error={errors.email || (showErrorBanner && 'Errore di rete') || (showAlreadyBanner && 'Già registrata')}
        disabled={isSubmitting}
      />
      <InputField
        label="Nome"
        placeholder="Come ti chiami?"
        value={form.name}
        onChange={set('name')}
        optional
        disabled={isSubmitting}
        hint="Se ce lo dici, ti chiamiamo per nome nelle email"
      />
      <GamePreferenceSelect
        value={form.game}
        onChange={setGame}
        otherText={form.other}
        onOtherText={set('other')}
        error={errors.game}
      />
      <CheckRow checked={form.news} onChange={e => setForm(f => ({ ...f, news: e.target.checked }))}>
        Voglio ricevere <strong style={{ color:'var(--text)' }}>aggiornamenti via email</strong> (opzionale).
        Update prodotto, devlog e inviti early-access — niente spam, una volta al mese.
      </CheckRow>

      <PrimaryBtn onClick={submit} loading={isSubmitting}>
        {isSubmitting ? 'Invio in corso…' : 'Entra in waitlist →'}
      </PrimaryBtn>

      <div style={{
        textAlign:'center', marginTop: 12,
        fontSize: 11, color:'var(--text-muted)',
        fontFamily:'var(--f-mono)', letterSpacing:'.02em',
      }}>
        Già hai un invito? <a href="#" onClick={e => e.preventDefault()}
          style={{ color:'hsl(var(--c-game))', fontWeight: 700, fontFamily:'var(--f-display)' }}>
          Accedi
        </a>
      </div>
    </div>
  );
};

// ─── PHONE FRAME ─────────────────────────────────────
const PhoneSbar = () => (
  <div className="phone-sbar">
    <span style={{ fontFamily:'var(--f-mono)' }}>14:32</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);

// ─── JOIN MOBILE SCREEN (a phone-shaped instance) ────
const JoinMobileScreen = ({ stateOverride }) => (
  <>
    <PhoneSbar/>
    <div style={{
      flex: 1, overflowY:'auto', scrollbarWidth:'none',
      background:'var(--bg)',
      padding:'18px 16px 22px',
    }}>
      <div style={{ display:'flex', justifyContent:'center', marginBottom: 14 }}>
        <BrandMark size={42}/>
      </div>

      <HeroText/>

      <div style={{
        background:'var(--bg-card)',
        borderRadius:'var(--r-2xl)',
        border:'1px solid var(--border)',
        boxShadow:'var(--shadow-lg)',
        padding:'18px 16px 16px',
      }}>
        <JoinForm stateOverride={stateOverride}/>
      </div>

      {/* Cosa ti aspetta */}
      <div style={{ marginTop: 22 }}>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
          color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
          marginBottom: 10, paddingLeft: 2,
        }}>// Cosa ti aspetta</div>
        <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
          {ALPHA_FEATURES.map(f => <FeatureCard key={f.title} feat={f}/>)}
        </div>
      </div>

      {/* Footer mini */}
      <div style={{
        marginTop: 22, paddingTop: 14, borderTop:'1px solid var(--border-light)',
        textAlign:'center',
        fontSize: 10, color:'var(--text-muted)', fontFamily:'var(--f-mono)',
        letterSpacing:'.04em',
      }}>
        © 2026 MeepleAI · <a href="#" onClick={e=>e.preventDefault()} style={{ color:'inherit' }}>Privacy</a> · <a href="#" onClick={e=>e.preventDefault()} style={{ color:'inherit' }}>Termini</a>
      </div>
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

// ─── DESKTOP LAYOUT (1440-style frame) ───────────────
const DesktopFrame = ({ children, label, desc }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 12, width:'100%' }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}</div>
    <div style={{
      width: '100%', maxWidth: 1280,
      borderRadius:'var(--r-xl)',
      border:'1px solid var(--border)',
      background:'var(--bg)',
      overflow:'hidden',
      boxShadow:'var(--shadow-lg)',
    }}>
      {/* fake browser chrome */}
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
        <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/join</span>
      </div>
      {children}
    </div>
    {desc && <div style={{
      fontSize: 11, color:'var(--text-muted)', maxWidth: 720,
      textAlign:'center', lineHeight: 1.55,
    }}>{desc}</div>}
  </div>
);

// Desktop nav clone (versione semplificata di public.jsx PublicNav)
const DesktopNav = () => (
  <div style={{
    position:'sticky', top: 0, zIndex: 5,
    display:'flex', alignItems:'center', gap: 14,
    padding:'12px 32px',
    background:'var(--glass-bg)', backdropFilter:'blur(12px)',
    borderBottom:'1px solid var(--border)',
  }}>
    <div style={{ display:'flex', alignItems:'center', gap: 9 }}>
      <div style={{
        width: 26, height: 26, borderRadius: 7,
        background:'linear-gradient(135deg, hsl(var(--c-game)), hsl(var(--c-event)))',
        color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
        fontWeight: 800, fontSize: 13, fontFamily:'var(--f-display)',
      }}>M</div>
      <span style={{ fontFamily:'var(--f-display)', fontWeight: 700, fontSize: 14, color:'var(--text)' }}>MeepleAI</span>
    </div>
    <div style={{ flex: 1, display:'flex', gap: 6, marginLeft: 22 }}>
      {['Prodotto','Prezzi','Chi siamo','Contatti'].map(l => (
        <a key={l} href="#" onClick={e=>e.preventDefault()} style={{
          padding:'6px 12px', borderRadius:'var(--r-md)',
          color:'var(--text-sec)', fontSize: 13, fontWeight: 600,
          fontFamily:'var(--f-display)',
        }}>{l}</a>
      ))}
    </div>
    <a href="#" onClick={e=>e.preventDefault()} style={{
      padding:'6px 12px', borderRadius:'var(--r-md)',
      color:'var(--text-sec)', fontSize: 13, fontWeight: 700, fontFamily:'var(--f-display)',
      border:'1px solid var(--border)',
    }}>Accedi</a>
    <a href="#" onClick={e=>e.preventDefault()} style={{
      padding:'7px 14px', borderRadius:'var(--r-md)',
      color:'#fff', fontSize: 13, fontWeight: 700, fontFamily:'var(--f-display)',
      background:'hsl(var(--c-game))',
      boxShadow:'0 3px 10px hsl(var(--c-game) / .3)',
    }}>Inizia gratis</a>
  </div>
);

const JoinDesktopScreen = ({ stateOverride }) => (
  <div>
    <DesktopNav/>
    <div style={{
      display:'grid',
      gridTemplateColumns:'1.1fr .9fr',
      gap: 40,
      padding:'48px 64px 56px',
      minHeight: 720,
      position:'relative',
      background:'radial-gradient(120% 80% at 80% 0%, hsl(var(--c-event) / .07), transparent 60%), radial-gradient(80% 70% at 0% 100%, hsl(var(--c-game) / .06), transparent 70%)',
    }}>
      {/* Left: hero + features */}
      <div style={{ paddingTop: 12 }}>
        <HeroText compact/>

        <div style={{
          display:'grid', gridTemplateColumns:'1fr 1fr 1fr', gap: 12,
          marginTop: 20,
        }}>
          {ALPHA_FEATURES.map(f => <FeatureCard key={f.title} feat={f}/>)}
        </div>

        {/* Stats riga */}
        <div style={{
          marginTop: 28,
          display:'grid', gridTemplateColumns:'repeat(3, 1fr)', gap: 14,
        }}>
          {[
            { v:'1.247', l:'già in lista' },
            { v:'~2 set.', l:'tempo medio attesa' },
            { v:'100%', l:'gratis durante alpha' },
          ].map(s => (
            <div key={s.l} style={{
              padding:'12px 14px',
              borderRadius:'var(--r-lg)',
              background:'var(--bg-card)',
              border:'1px solid var(--border)',
              boxShadow:'var(--shadow-xs)',
            }}>
              <div style={{
                fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 22,
                color:'hsl(var(--c-game))', lineHeight: 1.1,
              }}>{s.v}</div>
              <div style={{
                fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
                textTransform:'uppercase', letterSpacing:'.08em', marginTop: 4,
              }}>{s.l}</div>
            </div>
          ))}
        </div>

        <div style={{
          marginTop: 28,
          display:'flex', alignItems:'center', gap: 10,
          fontSize: 11, color:'var(--text-muted)',
          fontFamily:'var(--f-mono)', letterSpacing:'.04em',
        }}>
          <span style={{
            display:'inline-flex', width: 22, height: 22, borderRadius:'50%',
            background:'linear-gradient(135deg, hsl(var(--c-player)), hsl(var(--c-game)))',
          }}/>
          <span style={{
            display:'inline-flex', width: 22, height: 22, borderRadius:'50%',
            background:'linear-gradient(135deg, hsl(var(--c-event)), hsl(var(--c-toolkit)))',
            marginLeft: -10, border:'2px solid var(--bg)',
          }}/>
          <span style={{
            display:'inline-flex', width: 22, height: 22, borderRadius:'50%',
            background:'linear-gradient(135deg, hsl(var(--c-kb)), hsl(var(--c-chat)))',
            marginLeft: -10, border:'2px solid var(--bg)',
          }}/>
          <span style={{ marginLeft: 4 }}>+ 1.244 boardgamer ti hanno preceduto</span>
        </div>
      </div>

      {/* Right: form card */}
      <div style={{ position:'sticky', top: 80, alignSelf:'start' }}>
        <div style={{
          background:'var(--bg-card)',
          border:'1px solid var(--border)',
          borderRadius:'var(--r-2xl)',
          boxShadow:'var(--shadow-lg)',
          padding:'24px 22px 22px',
        }}>
          <h2 style={{
            fontFamily:'var(--f-display)', fontWeight: 700, fontSize: 18,
            color:'var(--text)', margin:'0 0 4px', letterSpacing:'-.01em',
          }}>Richiedi accesso</h2>
          <p style={{
            fontSize: 12, color:'var(--text-muted)', margin:'0 0 16px', lineHeight: 1.55,
          }}>Lascia la tua email — ti contattiamo non appena c'è posto.</p>
          <JoinForm stateOverride={stateOverride}/>
        </div>
      </div>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── ROOT — STAGE LAYOUT ─────────────────────────────
// ═══════════════════════════════════════════════════════
const MOBILE_STATES = [
  { id: 'default',         label: '01 · Default',         desc: 'Form vuoto — primo accesso. GamePreferenceSelect chiuso.' },
  { id: 'submitting',      label: '02 · Submitting',      desc: 'Bottone in loading, campi disabilitati durante invio.' },
  { id: 'success',         label: '03 · Success',         desc: 'SuccessCard con posizione waitlist + countdown stimato.' },
  { id: 'error',           label: '04 · Error',           desc: 'Banner rosso top, errore di rete — campo email evidenziato.' },
  { id: 'already-on-list', label: '05 · Già in lista',    desc: 'Banner ambra: email già registrata, mostra posizione esistente.' },
];

function ThemeToggle() {
  const [dark, setDark] = useState(false);
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
  }, [dark]);
  return (
    <button
      onClick={() => setDark(d => !d)}
      className="theme-toggle"
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
        }}>SP3 · Public Secondary · #1</div>
        <h1>Join / Waitlist Alpha</h1>
        <p className="lead">
          <code style={{ background:'var(--bg-muted)', padding:'2px 7px', borderRadius:'var(--r-sm)', fontSize: 12 }}>/join</code>
          {' · '}5 stati, mobile 375 + desktop 1440.
          Riusa <strong>AuthCard / InputField / SuccessCard</strong> da SP1; introduce <strong>GamePreferenceSelect</strong> (combobox a11y, top 10 + Altro).
          Toggle tema in alto a destra per verificare light/dark.
        </p>

        {/* MOBILE — 5 stati */}
        <div className="section-label">Mobile · 375 — 5 stati</div>
        <div className="phones-grid">
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc}>
              <JoinMobileScreen stateOverride={s.id}/>
            </PhoneShell>
          ))}
        </div>

        {/* DESKTOP — default + success */}
        <div className="section-label">Desktop · 1440 — split hero + form sticky</div>
        <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
          <DesktopFrame label="06 · Desktop · Default"
            desc="Hero a sinistra (kicker alpha + h1 + features mini-card + social proof avatar stack); form sticky a destra. Background con due radial-gradient a bassa opacità (event + game).">
            <JoinDesktopScreen stateOverride="default"/>
          </DesktopFrame>
          <DesktopFrame label="07 · Desktop · Success"
            desc="Stato success desktop — la card a destra mostra la posizione waitlist con countdown.">
            <JoinDesktopScreen stateOverride="success"/>
          </DesktopFrame>
        </div>
      </div>

      <style>{`
        @keyframes mai-spin { to { transform: rotate(360deg); } }
        .phones-grid {
          display: grid;
          grid-template-columns: repeat(auto-fill, minmax(380px, 1fr));
          gap: var(--s-7);
          align-items: start;
        }
        .stage h1 { font-size: var(--fs-3xl); }
        .stage .lead { line-height: var(--lh-relax); }
        /* keep phone scroll smooth */
        .phone > div::-webkit-scrollbar { display: none; }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);

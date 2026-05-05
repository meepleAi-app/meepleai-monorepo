/* MeepleAI Design System v1 — Auth Flow
   6 screen pre-autenticazione: Login, Registrazione, Recupero password,
   Reset password, Verifica email, Setup 2FA.
   React 18 UMD + Babel standalone — stessa tecnica di mobile-app.jsx */

const { useState, useEffect, useRef } = React;

// ─── BRAND LOGO ──────────────────────────────────────
const BrandMark = () => (
  <div style={{
    display:'flex',flexDirection:'column',alignItems:'center',gap:6,marginBottom:22,
  }}>
    <div style={{
      width:52,height:52,borderRadius:16,
      background:'linear-gradient(135deg, hsl(var(--c-game)), hsl(var(--c-event)) 60%, hsl(var(--c-player)))',
      display:'flex',alignItems:'center',justifyContent:'center',
      color:'#fff',fontWeight:800,fontSize:24,fontFamily:'var(--f-display)',
      boxShadow:'0 6px 20px hsl(var(--c-game) / .35)',
      flexShrink:0,
    }}>M</div>
    <span style={{
      fontFamily:'var(--f-display)',fontWeight:700,fontSize:16,
      letterSpacing:'-.02em',color:'var(--text)',
    }}>MeepleAI</span>
  </div>
);

// ─── AUTH CARD WRAPPER ────────────────────────────────
const AuthCard = ({ title, subtitle, children }) => (
  <div style={{
    flex:1,overflowY:'auto',scrollbarWidth:'none',
    display:'flex',flexDirection:'column',alignItems:'stretch',
    padding:'24px 18px 28px',background:'var(--bg)',gap:0,
  }}>
    <div style={{display:'flex',justifyContent:'center'}}><BrandMark /></div>
    <div style={{
      background:'var(--bg-card)',
      borderRadius:'var(--r-2xl)',
      border:'1px solid var(--border)',
      boxShadow:'var(--shadow-lg)',
      padding:'22px 18px 18px',
    }}>
      {title && (
        <h2 style={{
          fontFamily:'var(--f-display)',fontSize:21,fontWeight:700,
          letterSpacing:'-.02em',color:'var(--text)',margin:'0 0 4px',
        }}>{title}</h2>
      )}
      {subtitle && (
        <p style={{fontSize:12,color:'var(--text-muted)',margin:'0 0 18px',lineHeight:1.55}}>{subtitle}</p>
      )}
      {children}
    </div>
  </div>
);

// ─── INPUT FIELD ─────────────────────────────────────
const InputField = ({ label, type='text', placeholder, value, onChange, error, hint, right }) => (
  <div style={{marginBottom:13}}>
    <label style={{
      display:'block',fontSize:10,fontWeight:700,fontFamily:'var(--f-display)',
      color:'var(--text-sec)',marginBottom:5,
      textTransform:'uppercase',letterSpacing:'.06em',
    }}>{label}</label>
    <div style={{position:'relative'}}>
      <input
        type={type}
        placeholder={placeholder}
        value={value}
        onChange={onChange}
        style={{
          display:'block',width:'100%',
          padding: right ? '9px 36px 9px 11px' : '9px 11px',
          borderRadius:'var(--r-md)',
          border: error
            ? '1.5px solid hsl(var(--c-danger))'
            : '1.5px solid var(--border)',
          background: error
            ? 'hsl(var(--c-danger) / .07)'
            : 'var(--bg)',
          color:'var(--text)',fontSize:13,outline:'none',
          fontFamily:'var(--f-body)',
          transition:'border-color var(--dur-sm) var(--ease-out)',
          boxSizing:'border-box',
        }}
      />
      {right && (
        <div style={{
          position:'absolute',right:10,top:'50%',transform:'translateY(-50%)',
          cursor:'pointer',color:'var(--text-muted)',fontSize:14,userSelect:'none',
          lineHeight:1,
        }}>{right}</div>
      )}
    </div>
    {error && <div style={{fontSize:11,color:'hsl(var(--c-danger))',marginTop:4,fontWeight:600}}>{error}</div>}
    {hint && !error && <div style={{fontSize:11,color:'var(--text-muted)',marginTop:4}}>{hint}</div>}
  </div>
);

// ─── DIVIDER ─────────────────────────────────────────
const Divider = ({ label='oppure' }) => (
  <div style={{display:'flex',alignItems:'center',gap:8,margin:'14px 0'}}>
    <div style={{flex:1,height:1,background:'var(--border)'}}></div>
    <span style={{
      fontSize:10,color:'var(--text-muted)',fontWeight:700,
      fontFamily:'var(--f-mono)',textTransform:'uppercase',letterSpacing:'.08em',
    }}>{label}</span>
    <div style={{flex:1,height:1,background:'var(--border)'}}></div>
  </div>
);

// ─── OAUTH BUTTONS ────────────────────────────────────
const OAuthButtons = () => {
  const providers = [
    {
      id:'google', label:'Google',
      icon:(
        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" style={{flexShrink:0}}>
          <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z" fill="#4285F4"/>
          <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/>
          <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l3.66-2.84z" fill="#FBBC05"/>
          <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/>
        </svg>
      ),
    },
    {
      id:'discord', label:'Discord',
      icon:(
        <svg width="15" height="15" viewBox="0 0 24 24" fill="#5865F2" style={{flexShrink:0}}>
          <path d="M20.317 4.37a19.791 19.791 0 00-4.885-1.515.074.074 0 00-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 00-5.487 0 12.64 12.64 0 00-.617-1.25.077.077 0 00-.079-.037A19.736 19.736 0 003.677 4.37a.07.07 0 00-.032.027C.533 9.046-.32 13.58.099 18.057c.002.022.015.043.032.056a19.9 19.9 0 005.993 3.03.078.078 0 00.084-.028c.462-.63.874-1.295 1.226-1.993.021-.041.001-.09-.041-.106a13.1 13.1 0 01-1.872-.892.077.077 0 01-.008-.128 10.2 10.2 0 00.372-.292.074.074 0 01.077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 01.078.01c.12.098.246.198.373.292a.077.077 0 01-.006.127 12.299 12.299 0 01-1.873.892.077.077 0 00-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 00.084.028 19.839 19.839 0 006.002-3.03.077.077 0 00.032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 00-.031-.03zM8.02 15.33c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.956-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.956 2.418-2.157 2.418zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.955-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.946 2.418-2.157 2.418z"/>
        </svg>
      ),
    },
  ];

  return (
    <div style={{display:'flex',flexDirection:'column',gap:7}}>
      {providers.map(p => (
        <button key={p.id} className="btn ghost" style={{
          width:'100%',justifyContent:'center',
          padding:'9px 14px',fontSize:12,gap:7,
          borderRadius:'var(--r-md)',fontFamily:'var(--f-display)',
        }}>
          {p.icon}
          <span>Continua con {p.label}</span>
        </button>
      ))}
    </div>
  );
};

// ─── FOOTER LINK ─────────────────────────────────────
const FooterLink = ({ text, cta, onClick }) => (
  <div style={{textAlign:'center',marginTop:14,fontSize:12,color:'var(--text-muted)'}}>
    {text}{' '}
    <span
      onClick={onClick}
      style={{color:'hsl(var(--c-game))',fontWeight:700,cursor:'pointer',fontFamily:'var(--f-display)'}}
    >{cta}</span>
  </div>
);

// ─── PASSWORD STRENGTH ────────────────────────────────
const StrengthMeter = ({ password }) => {
  if (!password) return null;
  const score =
    password.length < 6 ? 1 :
    password.length < 10 ? 2 :
    /[A-Z]/.test(password) && /[0-9]/.test(password) && /[^A-Za-z0-9]/.test(password) ? 4 : 3;
  const labels = ['','Debole','Discreta','Buona','Ottima'];
  const colors = ['','hsl(var(--c-danger))','hsl(var(--c-warning))','hsl(var(--c-kb))','hsl(var(--c-toolkit))'];
  return (
    <div style={{marginTop:5}}>
      <div style={{display:'flex',gap:3,marginBottom:3}}>
        {[1,2,3,4].map(i => (
          <div key={i} style={{
            flex:1,height:3,borderRadius:2,
            background: i<=score ? colors[score] : 'var(--border)',
            transition:'background var(--dur-md)',
          }}/>
        ))}
      </div>
      <div style={{fontSize:10,color:colors[score],fontWeight:700,fontFamily:'var(--f-display)'}}>
        {labels[score]}
      </div>
    </div>
  );
};

// ─── PWD INPUT (with toggle) ─────────────────────────
const PwdInput = ({ label, placeholder, value, onChange, error, showStrength }) => {
  const [show, setShow] = useState(false);
  return (
    <div style={{marginBottom:13}}>
      <label style={{
        display:'block',fontSize:10,fontWeight:700,fontFamily:'var(--f-display)',
        color:'var(--text-sec)',marginBottom:5,textTransform:'uppercase',letterSpacing:'.06em',
      }}>{label}</label>
      <div style={{position:'relative'}}>
        <input
          type={show ? 'text' : 'password'}
          placeholder={placeholder}
          value={value}
          onChange={onChange}
          style={{
            display:'block',width:'100%',padding:'9px 36px 9px 11px',
            borderRadius:'var(--r-md)',
            border: error ? '1.5px solid hsl(var(--c-danger))' : '1.5px solid var(--border)',
            background: error ? 'hsl(var(--c-danger) / .07)' : 'var(--bg)',
            color:'var(--text)',fontSize:13,outline:'none',
            fontFamily:'var(--f-body)',boxSizing:'border-box',
            transition:'border-color var(--dur-sm) var(--ease-out)',
          }}
        />
        <div onClick={() => setShow(s => !s)} style={{
          position:'absolute',right:10,top:'50%',transform:'translateY(-50%)',
          cursor:'pointer',color:'var(--text-muted)',fontSize:13,userSelect:'none',lineHeight:1,
        }}>{show ? '🙈' : '👁️'}</div>
      </div>
      {showStrength && <StrengthMeter password={value} />}
      {error && <div style={{fontSize:11,color:'hsl(var(--c-danger))',marginTop:4,fontWeight:600}}>{error}</div>}
    </div>
  );
};

// ─── BTN PRIMARY LARGE ────────────────────────────────
const PrimaryBtn = ({ children, onClick, disabled }) => (
  <button
    className="btn primary"
    onClick={onClick}
    disabled={disabled}
    style={{
      width:'100%',justifyContent:'center',
      padding:'11px 14px',fontSize:14,borderRadius:'var(--r-md)',
      fontFamily:'var(--f-display)',fontWeight:700,
      opacity: disabled ? .55 : 1,
      transition:'all var(--dur-sm) var(--ease-out)',
    }}
  >{children}</button>
);

// ─── PIN INPUT (6-digit OTP) ─────────────────────────
const PinInput = ({ value, onChange }) => {
  const refs = useRef([]);
  const digits = value.padEnd(6,' ').split('').slice(0,6);

  const handleKey = (i, e) => {
    if (e.key === 'Backspace') {
      const next = value.slice(0,-1);
      onChange(next);
      if (i > 0) refs.current[i-1]?.focus();
      return;
    }
    if (/^\d$/.test(e.key)) {
      const next = (value + e.key).slice(0,6);
      onChange(next);
      if (i < 5 && next.length > i) refs.current[i+1]?.focus();
    }
    e.preventDefault();
  };

  return (
    <div style={{display:'flex',gap:7,justifyContent:'center'}}>
      {[0,1,2,3,4,5].map(i => (
        <input
          key={i}
          ref={el => refs.current[i] = el}
          type="text"
          inputMode="numeric"
          value={digits[i] === ' ' ? '' : digits[i]}
          onKeyDown={e => handleKey(i,e)}
          onChange={() => {}}
          onClick={() => refs.current[i]?.focus()}
          style={{
            width:38,height:46,textAlign:'center',
            borderRadius:'var(--r-md)',
            border: digits[i] !== ' ' ? '1.5px solid hsl(var(--c-game))' : '1.5px solid var(--border)',
            background:'var(--bg)',color:'var(--text)',
            fontSize:20,fontWeight:700,fontFamily:'var(--f-mono)',
            outline:'none',transition:'border-color var(--dur-sm)',
          }}
        />
      ))}
    </div>
  );
};

// ─── QR PLACEHOLDER SVG ───────────────────────────────
// Deterministic bit pattern — no Math.random()
const QRPattern = () => {
  // 10×10 inner grid bit map (hand-authored)
  const bits = [
    [1,0,1,1,0,1,0,1,1,0],
    [0,1,0,1,1,0,1,0,0,1],
    [1,1,0,0,1,1,0,1,0,0],
    [0,0,1,0,0,1,1,1,1,0],
    [1,0,0,1,1,0,1,0,0,1],
    [0,1,1,0,0,1,0,1,1,0],
    [1,0,1,1,0,0,1,0,0,1],
    [0,1,0,0,1,1,0,1,0,1],
    [1,1,0,1,0,0,1,0,1,0],
    [0,0,1,0,1,0,0,1,1,1],
  ];
  return (
    <svg width="130" height="130" viewBox="0 0 130 130">
      {/* Corner TL */}
      <rect x="4" y="4" width="32" height="32" rx="4" fill="none" stroke="var(--text)" strokeWidth="3"/>
      <rect x="10" y="10" width="20" height="20" rx="3" fill="var(--text)"/>
      <rect x="14" y="14" width="12" height="12" rx="2" fill="var(--bg-card)"/>
      {/* Corner TR */}
      <rect x="94" y="4" width="32" height="32" rx="4" fill="none" stroke="var(--text)" strokeWidth="3"/>
      <rect x="100" y="10" width="20" height="20" rx="3" fill="var(--text)"/>
      <rect x="104" y="14" width="12" height="12" rx="2" fill="var(--bg-card)"/>
      {/* Corner BL */}
      <rect x="4" y="94" width="32" height="32" rx="4" fill="none" stroke="var(--text)" strokeWidth="3"/>
      <rect x="10" y="100" width="20" height="20" rx="3" fill="var(--text)"/>
      <rect x="14" y="104" width="12" height="12" rx="2" fill="var(--bg-card)"/>
      {/* Inner dots */}
      {bits.map((row, r) => row.map((bit, c) => bit ? (
        <rect key={`${r}-${c}`}
          x={40 + c*9} y={40 + r*9}
          width={8} height={8} rx="1.5"
          fill="var(--text)"
        />
      ) : null))}
      {/* Logo center overlay */}
      <rect x="54" y="54" width="22" height="22" rx="5"
        fill="url(#qrgrad)" stroke="var(--bg-card)" strokeWidth="2"/>
      <text x="65" y="69.5" textAnchor="middle"
        fill="white" fontSize="12" fontWeight="800"
        fontFamily="var(--f-display)">M</text>
      <defs>
        <linearGradient id="qrgrad" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" stopColor="hsl(var(--c-game))"/>
          <stop offset="100%" stopColor="hsl(var(--c-player))"/>
        </linearGradient>
      </defs>
    </svg>
  );
};

// ─── SUCCESS STATE ────────────────────────────────────
const SuccessCard = ({ emoji, title, body, cta, onCta }) => (
  <AuthCard>
    <div style={{display:'flex',flexDirection:'column',alignItems:'center',textAlign:'center',padding:'8px 0 4px'}}>
      <div style={{
        width:72,height:72,borderRadius:'50%',
        background:'hsl(var(--c-toolkit) / .12)',
        display:'flex',alignItems:'center',justifyContent:'center',
        fontSize:34,marginBottom:18,
      }}>{emoji}</div>
      <h2 style={{fontFamily:'var(--f-display)',fontSize:20,fontWeight:700,color:'var(--text)',margin:'0 0 8px'}}>{title}</h2>
      <p style={{fontSize:12,color:'var(--text-muted)',lineHeight:1.6,margin:'0 0 20px'}}>{body}</p>
      <PrimaryBtn onClick={onCta}>{cta}</PrimaryBtn>
    </div>
  </AuthCard>
);

// ─── PHONE SBAR ───────────────────────────────────────
const PhoneSbar = () => (
  <div className="phone-sbar">
    <span>14:32</span>
    <div className="ind"><span>📶</span><span>🔋</span></div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── 1. LOGIN SCREEN ─────────────────────────────────
// ═══════════════════════════════════════════════════════
const LoginScreen = ({ onNav }) => {
  const [email, setEmail] = useState('');
  const [pw, setPw] = useState('');
  const [errors, setErrors] = useState({});

  const submit = () => {
    const e = {};
    if (!email.includes('@')) e.email = 'Email non valida';
    if (pw.length < 6) e.pw = 'Minimo 6 caratteri';
    setErrors(e);
  };

  return (
    <>
      <PhoneSbar/>
      <AuthCard title="Bentornato" subtitle="Accedi al tuo account MeepleAI">
        <InputField
          label="Email" type="email" placeholder="tu@email.com"
          value={email} onChange={e => setEmail(e.target.value)}
          error={errors.email}
        />
        <PwdInput
          label="Password" placeholder="••••••••"
          value={pw} onChange={e => setPw(e.target.value)}
          error={errors.pw}
        />
        <div style={{textAlign:'right',marginTop:-7,marginBottom:14}}>
          <span onClick={() => onNav('forgot')}
            style={{fontSize:11,color:'hsl(var(--c-game))',fontWeight:700,cursor:'pointer',fontFamily:'var(--f-display)'}}>
            Password dimenticata?
          </span>
        </div>
        <PrimaryBtn onClick={submit}>Accedi</PrimaryBtn>
        <Divider/>
        <OAuthButtons/>
        <FooterLink text="Non hai un account?" cta="Registrati" onClick={() => onNav('register')}/>
      </AuthCard>
    </>
  );
};

// ═══════════════════════════════════════════════════════
// ─── 2. REGISTER SCREEN ──────────────────────────────
// ═══════════════════════════════════════════════════════
const RegisterScreen = ({ onNav }) => {
  const [form, setForm] = useState({ email:'', pw:'', username:'' });
  const [terms, setTerms] = useState(false);
  const [errors, setErrors] = useState({});
  const set = k => e => setForm(f => ({...f, [k]: e.target.value}));

  const submit = () => {
    const e = {};
    if (form.username.length < 3) e.username = 'Minimo 3 caratteri';
    if (!form.email.includes('@')) e.email = 'Email non valida';
    if (form.pw.length < 8) e.pw = 'Minimo 8 caratteri';
    if (!terms) e.terms = 'Accetta i termini per continuare';
    setErrors(e);
  };

  return (
    <>
      <PhoneSbar/>
      <AuthCard title="Crea account" subtitle="Unisciti alla comunità MeepleAI">
        <InputField
          label="Username" placeholder="meepler_42"
          value={form.username} onChange={set('username')}
          error={errors.username}
          hint="Visibile agli altri giocatori"
        />
        <InputField
          label="Email" type="email" placeholder="tu@email.com"
          value={form.email} onChange={set('email')}
          error={errors.email}
        />
        <PwdInput
          label="Password" placeholder="Min. 8 caratteri"
          value={form.pw} onChange={set('pw')}
          error={errors.pw} showStrength
        />
        <div style={{marginBottom:14}}>
          <label style={{display:'flex',alignItems:'flex-start',gap:8,cursor:'pointer'}}>
            <input
              type="checkbox" checked={terms} onChange={e => setTerms(e.target.checked)}
              style={{marginTop:2,accentColor:'hsl(var(--c-game))',width:14,height:14,flexShrink:0}}
            />
            <span style={{fontSize:11,color:'var(--text-sec)',lineHeight:1.55}}>
              Accetto i{' '}
              <span style={{color:'hsl(var(--c-game))',fontWeight:700}}>Termini di Servizio</span>
              {' '}e la{' '}
              <span style={{color:'hsl(var(--c-game))',fontWeight:700}}>Privacy Policy</span>
            </span>
          </label>
          {errors.terms && <div style={{fontSize:11,color:'hsl(var(--c-danger))',marginTop:4,fontWeight:600}}>{errors.terms}</div>}
        </div>
        <PrimaryBtn onClick={submit}>Crea account</PrimaryBtn>
        <Divider/>
        <OAuthButtons/>
        <FooterLink text="Hai già un account?" cta="Accedi" onClick={() => onNav('login')}/>
      </AuthCard>
    </>
  );
};

// ═══════════════════════════════════════════════════════
// ─── 3. FORGOT PASSWORD SCREEN ───────────────────────
// ═══════════════════════════════════════════════════════
const ForgotPasswordScreen = ({ onNav }) => {
  const [email, setEmail] = useState('');
  const [error, setError] = useState('');
  const [sent, setSent] = useState(false);

  const submit = () => {
    if (!email.includes('@')) { setError('Inserisci un\'email valida'); return; }
    setSent(true);
  };

  if (sent) return (
    <>
      <PhoneSbar/>
      <SuccessCard
        emoji="📬"
        title="Email inviata!"
        body={<>Controlla la casella di <strong style={{color:'var(--text)'}}>{email}</strong>.<br/>Il link è valido per 30 minuti.</>}
        cta="Invia di nuovo"
        onCta={() => setSent(false)}
      />
    </>
  );

  return (
    <>
      <PhoneSbar/>
      <AuthCard title="Password dimenticata?" subtitle="Riceverai un link per reimpostarla via email">
        <InputField
          label="Email" type="email" placeholder="tu@email.com"
          value={email}
          onChange={e => { setEmail(e.target.value); setError(''); }}
          error={error}
        />
        <PrimaryBtn onClick={submit}>Invia link reset</PrimaryBtn>
        <FooterLink text="Ricordi la password?" cta="Torna al login" onClick={() => onNav('login')}/>
      </AuthCard>
    </>
  );
};

// ═══════════════════════════════════════════════════════
// ─── 4. RESET PASSWORD SCREEN ────────────────────────
// ═══════════════════════════════════════════════════════
const ResetPasswordScreen = ({ onNav }) => {
  const [pw, setPw] = useState('');
  const [confirm, setConfirm] = useState('');
  const [errors, setErrors] = useState({});
  const [done, setDone] = useState(false);

  const submit = () => {
    const e = {};
    if (pw.length < 8) e.pw = 'Minimo 8 caratteri';
    if (confirm !== pw) e.confirm = 'Le password non coincidono';
    setErrors(e);
    if (!e.pw && !e.confirm) setDone(true);
  };

  if (done) return (
    <>
      <PhoneSbar/>
      <SuccessCard
        emoji="✅"
        title="Password aggiornata!"
        body="Puoi ora accedere con la tua nuova password."
        cta="Vai al login"
        onCta={() => onNav('login')}
      />
    </>
  );

  return (
    <>
      <PhoneSbar/>
      <AuthCard title="Nuova password" subtitle="Scegli una password sicura per il tuo account">
        <input type="hidden" value="tok_reset_meeple_abc123x" readOnly/>
        <PwdInput
          label="Nuova password" placeholder="Min. 8 caratteri"
          value={pw} onChange={e => setPw(e.target.value)}
          error={errors.pw} showStrength
        />
        <PwdInput
          label="Conferma password" placeholder="Ripeti la password"
          value={confirm} onChange={e => setConfirm(e.target.value)}
          error={errors.confirm}
        />
        <PrimaryBtn onClick={submit}>Imposta password</PrimaryBtn>
        <FooterLink text="Torna al" cta="Login" onClick={() => onNav('login')}/>
      </AuthCard>
    </>
  );
};

// ═══════════════════════════════════════════════════════
// ─── 5. VERIFY EMAIL SCREEN ──────────────────────────
// ═══════════════════════════════════════════════════════
const VerifyEmailScreen = ({ onNav }) => {
  const [resent, setResent] = useState(false);
  const [countdown, setCountdown] = useState(0);

  const resend = () => {
    setResent(true);
    setCountdown(30);
  };

  useEffect(() => {
    if (countdown <= 0) return;
    const t = setTimeout(() => setCountdown(c => c - 1), 1000);
    return () => clearTimeout(t);
  }, [countdown]);

  return (
    <>
      <PhoneSbar/>
      <AuthCard>
        <div style={{display:'flex',flexDirection:'column',alignItems:'center',textAlign:'center',padding:'4px 0'}}>
          <div style={{
            width:72,height:72,borderRadius:'50%',
            background:'hsl(var(--c-chat) / .1)',
            display:'flex',alignItems:'center',justifyContent:'center',
            fontSize:34,marginBottom:16,
          }}>✉️</div>
          <h2 style={{fontFamily:'var(--f-display)',fontSize:19,fontWeight:700,color:'var(--text)',margin:'0 0 8px'}}>Controlla la casella</h2>
          <p style={{fontSize:12,color:'var(--text-muted)',lineHeight:1.6,margin:'0 0 16px'}}>
            Abbiamo inviato un link di verifica a<br/>
            <strong style={{color:'var(--text)'}}>marco@example.com</strong>
          </p>
          {resent && (
            <div style={{
              padding:'7px 12px',borderRadius:'var(--r-md)',
              background:'hsl(var(--c-toolkit) / .1)',
              border:'1px solid hsl(var(--c-toolkit) / .25)',
              color:'hsl(var(--c-toolkit))',fontSize:11,fontWeight:700,
              marginBottom:12,width:'100%',
            }}>Email inviata di nuovo ✓</div>
          )}
          <PrimaryBtn onClick={resend} disabled={countdown > 0}>
            {countdown > 0 ? `Riprova tra ${countdown}s` : 'Invia di nuovo'}
          </PrimaryBtn>
          <button className="btn ghost" onClick={() => onNav('change-email')} style={{
            width:'100%',justifyContent:'center',
            padding:'10px 14px',fontSize:13,borderRadius:'var(--r-md)',
            fontFamily:'var(--f-display)',fontWeight:700,marginTop:8,
          }}>Cambia email</button>
          <FooterLink text="Torna al" cta="Login" onClick={() => onNav('login')}/>
        </div>
      </AuthCard>
    </>
  );
};

// ═══════════════════════════════════════════════════════
// ─── 6. TWO FACTOR SETUP SCREEN ──────────────────────
// ═══════════════════════════════════════════════════════
const TwoFactorSetupScreen = ({ onNav }) => {
  const [pin, setPin] = useState('');
  const [error, setError] = useState('');
  const [verified, setVerified] = useState(false);
  const manualCode = 'MFSA-K7P2-W9NB-4XLQ';

  const verify = () => {
    if (pin.length < 6) { setError('Inserisci il codice a 6 cifre'); return; }
    setVerified(true);
  };

  if (verified) return (
    <>
      <PhoneSbar/>
      <SuccessCard
        emoji="🛡️"
        title="2FA attivato!"
        body="Il tuo account è ora protetto con l'autenticazione a due fattori."
        cta="Vai al login"
        onCta={() => onNav('login')}
      />
    </>
  );

  return (
    <>
      <PhoneSbar/>
      <div style={{flex:1,overflowY:'auto',scrollbarWidth:'none',padding:'20px 18px 24px',background:'var(--bg)'}}>
        <div style={{display:'flex',justifyContent:'center',marginBottom:18}}>
          <div style={{display:'flex',flexDirection:'column',alignItems:'center',gap:6}}>
            <div style={{
              width:44,height:44,borderRadius:13,
              background:'linear-gradient(135deg, hsl(var(--c-game)), hsl(var(--c-event)) 60%, hsl(var(--c-player)))',
              display:'flex',alignItems:'center',justifyContent:'center',
              color:'#fff',fontWeight:800,fontSize:20,fontFamily:'var(--f-display)',
              boxShadow:'0 4px 14px hsl(var(--c-game) / .3)',
            }}>M</div>
            <span style={{fontFamily:'var(--f-display)',fontWeight:700,fontSize:14,color:'var(--text)'}}>MeepleAI</span>
          </div>
        </div>

        <div style={{
          background:'var(--bg-card)',borderRadius:'var(--r-2xl)',
          border:'1px solid var(--border)',boxShadow:'var(--shadow-lg)',
          padding:'20px 16px 18px',
        }}>
          {/* Header */}
          <div style={{display:'flex',alignItems:'center',gap:9,marginBottom:16}}>
            <div style={{
              width:32,height:32,borderRadius:9,
              background:'hsl(var(--c-game) / .12)',
              display:'flex',alignItems:'center',justifyContent:'center',fontSize:16,
            }}>🛡️</div>
            <div>
              <div style={{fontFamily:'var(--f-display)',fontSize:15,fontWeight:700,color:'var(--text)'}}>Autenticazione 2FA</div>
              <div style={{fontSize:11,color:'var(--text-muted)'}}>Scansiona con la tua app authenticator</div>
            </div>
          </div>

          {/* QR Code */}
          <div style={{display:'flex',flexDirection:'column',alignItems:'center',margin:'0 0 14px'}}>
            <div style={{
              padding:10,borderRadius:'var(--r-lg)',
              background:'var(--bg)',border:'1px solid var(--border)',
              display:'inline-flex',
            }}>
              <QRPattern/>
            </div>
            <div style={{
              fontSize:10,color:'var(--text-muted)',marginTop:6,
              fontFamily:'var(--f-mono)',letterSpacing:'.04em',
            }}>QR · Google / Microsoft Authenticator</div>
          </div>

          {/* Manual code */}
          <div style={{
            background:'var(--bg-muted)',borderRadius:'var(--r-md)',
            padding:'9px 12px',marginBottom:16,
          }}>
            <div style={{
              fontSize:9,color:'var(--text-muted)',fontWeight:700,
              fontFamily:'var(--f-mono)',textTransform:'uppercase',
              letterSpacing:'.08em',marginBottom:5,
            }}>Codice manuale</div>
            <div style={{
              fontFamily:'var(--f-mono)',fontSize:14,fontWeight:600,
              color:'var(--text)',letterSpacing:'.14em',wordBreak:'break-all',
              userSelect:'all',
            }}>{manualCode}</div>
          </div>

          {/* PIN */}
          <div style={{marginBottom:14}}>
            <div style={{
              fontSize:10,color:'var(--text-sec)',fontWeight:700,
              fontFamily:'var(--f-display)',textTransform:'uppercase',
              letterSpacing:'.06em',textAlign:'center',marginBottom:10,
            }}>Codice di verifica (6 cifre)</div>
            <PinInput value={pin} onChange={p => { setPin(p); setError(''); }}/>
            {error && <div style={{fontSize:11,color:'hsl(var(--c-danger))',marginTop:6,fontWeight:600,textAlign:'center'}}>{error}</div>}
          </div>

          <PrimaryBtn onClick={verify}>Verifica e attiva</PrimaryBtn>
        </div>

        <div style={{textAlign:'center',marginTop:14,fontSize:12,color:'var(--text-muted)'}}>
          <span onClick={() => onNav('login')} style={{color:'hsl(var(--c-game))',fontWeight:700,cursor:'pointer',fontFamily:'var(--f-display)'}}>Salta per ora</span>
        </div>
      </div>
    </>
  );
};

// ═══════════════════════════════════════════════════════
// ─── PHONE SHELL ─────────────────────────────────────
// ═══════════════════════════════════════════════════════
const PhoneShell = ({ label, desc, children }) => (
  <div style={{display:'flex',flexDirection:'column',alignItems:'center',gap:10}}>
    <div style={{
      fontFamily:'var(--f-mono)',fontSize:11,color:'var(--text-sec)',
      textTransform:'uppercase',letterSpacing:'.08em',fontWeight:700,
    }}>{label}</div>
    <div className="phone">{children}</div>
    {desc && <div style={{fontSize:11,color:'var(--text-muted)',maxWidth:340,textAlign:'center',lineHeight:1.55}}>{desc}</div>}
  </div>
);

// ─── SCREEN REGISTRY ─────────────────────────────────
const SCREENS = {
  login:    LoginScreen,
  register: RegisterScreen,
  forgot:   ForgotPasswordScreen,
  reset:    ResetPasswordScreen,
  verify:   VerifyEmailScreen,
  '2fa':    TwoFactorSetupScreen,
};

// ─── INTERACTIVE PHONE (with inner nav) ──────────────
const AuthPhone = ({ initialScreen, label, desc }) => {
  const [screen, setScreen] = useState(initialScreen);
  const Screen = SCREENS[screen] || LoginScreen;
  return (
    <PhoneShell label={label} desc={desc}>
      <Screen onNav={setScreen}/>
    </PhoneShell>
  );
};

// ─── ROOT ────────────────────────────────────────────
const config = [
  { id:'login',    label:'01 · Login',           desc:'Validazione in-line, mostra/nascondi password, link dimenticata → naviga al phone 03' },
  { id:'register', label:'02 · Registrazione',   desc:'Strength meter, termini checkbox, OAuth — link "Accedi" naviga al login' },
  { id:'forgot',   label:'03 · Recupero password', desc:'Submit → stato "email inviata" con illustrazione e resend' },
  { id:'reset',    label:'04 · Reset password',  desc:'Token hidden, strength meter, conferma — submit → stato successo' },
  { id:'verify',   label:'05 · Verifica email',  desc:'Resend con countdown 30s, stato "inviato di nuovo", tasto cambia email' },
  { id:'2fa',      label:'06 · Setup 2FA',       desc:'QR code + codice manuale mono + PIN input 6 cifre — submit → stato 2FA attivo' },
];

function AuthRoot() {
  return (
    <div className="phones-grid">
      {config.map(c => (
        <AuthPhone key={c.id} initialScreen={c.id} label={c.label} desc={c.desc}/>
      ))}
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<AuthRoot/>);

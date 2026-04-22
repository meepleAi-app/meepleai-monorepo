/* MeepleAI Design System v1 — Settings
   7 pannelli: Profile, Account, Preferenze, Notifiche, API Keys, Servizi.
   Desktop: sidebar 260px + content. Mobile: phone frames grid.
   React 18 UMD + Babel standalone — stesso pattern di auth-flow.jsx */

const { useState, useEffect, useRef } = React;

// ══════════════════════════════════════════════════════
// ─── SHARED PRIMITIVES ────────────────────────────────
// ══════════════════════════════════════════════════════

const ToggleSwitch = ({ checked, onChange, ec, label }) => (
  <button
    role="switch" aria-checked={checked} aria-label={label || ''}
    onClick={() => onChange(!checked)}
    style={{
      width:48, height:28, borderRadius:14, padding:0, flexShrink:0,
      border: checked ? `1px solid hsl(${ec || 'var(--c-game)'})` : '1px solid var(--border)',
      background: checked ? `hsl(${ec || 'var(--c-game)'})` : 'var(--bg-muted)',
      position:'relative', cursor:'pointer',
      transition:'background 180ms ease-out, border-color 180ms ease-out',
      outline:'none',
    }}
  >
    <div style={{
      position:'absolute', top:2, left: checked ? 20 : 2,
      width:22, height:22, borderRadius:'50%', background:'#fff',
      boxShadow:'0 1px 4px rgba(0,0,0,.25)',
      transition:'left 180ms ease-out',
    }}/>
  </button>
);

const SegmentedControl = ({ options, value, onChange }) => (
  <div style={{
    display:'inline-flex', background:'var(--bg-muted)',
    borderRadius:'var(--r-md)', border:'1px solid var(--border)', padding:2, gap:2,
  }}>
    {options.map(opt => (
      <button key={opt.value} onClick={() => onChange(opt.value)} style={{
        padding:'6px 14px', border:'none', cursor:'pointer',
        borderRadius:'calc(var(--r-md) - 2px)',
        background: value===opt.value ? 'var(--bg-card)' : 'transparent',
        color: value===opt.value ? 'var(--text)' : 'var(--text-sec)',
        fontFamily:'var(--f-display)', fontSize:12, fontWeight:700,
        boxShadow: value===opt.value ? 'var(--shadow-xs)' : 'none',
        transition:'all var(--dur-sm) var(--ease-out)',
      }}>{opt.label}</button>
    ))}
  </div>
);

const Lbl = ({ children }) => (
  <div style={{
    fontSize:11, fontWeight:700, fontFamily:'var(--f-display)',
    color:'var(--text-sec)', textTransform:'uppercase', letterSpacing:'.08em', marginBottom:6,
  }}>{children}</div>
);

const FInput = ({ label, type='text', value, onChange, placeholder, hint, right, disabled }) => (
  <div style={{marginBottom:16}}>
    {label && <Lbl>{label}</Lbl>}
    <div style={{position:'relative'}}>
      <input type={type} value={value} onChange={onChange} placeholder={placeholder} disabled={disabled}
        style={{
          display:'block', width:'100%', height:40, boxSizing:'border-box',
          padding: right ? '0 36px 0 12px' : '0 12px',
          borderRadius:'var(--r-md)', border:'1px solid var(--border)',
          background: disabled ? 'var(--bg-muted)' : 'var(--bg)',
          color: disabled ? 'var(--text-muted)' : 'var(--text)',
          fontSize:13, fontFamily:'var(--f-body)', outline:'none',
          transition:'border-color var(--dur-sm)',
        }}
        onFocus={e => !disabled && (e.target.style.borderColor='hsl(var(--c-game)/.6)')}
        onBlur={e => (e.target.style.borderColor='')}
      />
      {right && (
        <div style={{position:'absolute',right:10,top:'50%',transform:'translateY(-50%)',
          color:'var(--text-muted)',fontSize:13,cursor:'pointer',userSelect:'none'}}>
          {right}
        </div>
      )}
    </div>
    {hint && <div style={{fontSize:11,color:'var(--text-muted)',marginTop:4,lineHeight:1.4}}>{hint}</div>}
  </div>
);

const FPwd = ({ label, value, onChange, placeholder, hint }) => {
  const [show, setShow] = useState(false);
  return (
    <FInput label={label} type={show ? 'text' : 'password'}
      value={value} onChange={onChange} placeholder={placeholder} hint={hint}
      right={<span onClick={() => setShow(s=>!s)}>{show ? '🙈' : '👁️'}</span>}
    />
  );
};

const FTextarea = ({ label, value, onChange, placeholder, rows=3 }) => (
  <div style={{marginBottom:16}}>
    {label && <Lbl>{label}</Lbl>}
    <textarea value={value} onChange={onChange} placeholder={placeholder} rows={rows}
      style={{
        display:'block', width:'100%', boxSizing:'border-box',
        padding:'9px 12px', borderRadius:'var(--r-md)', border:'1px solid var(--border)',
        background:'var(--bg)', color:'var(--text)', fontSize:13, fontFamily:'var(--f-body)',
        outline:'none', resize:'vertical', lineHeight:1.5,
        transition:'border-color var(--dur-sm)',
      }}
      onFocus={e => (e.target.style.borderColor='hsl(var(--c-game)/.6)')}
      onBlur={e => (e.target.style.borderColor='')}
    />
  </div>
);

const FSelect = ({ label, value, onChange, options, hint }) => (
  <div style={{marginBottom:16}}>
    {label && <Lbl>{label}</Lbl>}
    <select value={value} onChange={onChange}
      style={{
        display:'block', width:'100%', height:40, padding:'0 12px', boxSizing:'border-box',
        borderRadius:'var(--r-md)', border:'1px solid var(--border)',
        background:'var(--bg)', color:'var(--text)', fontSize:13,
        fontFamily:'var(--f-body)', outline:'none', cursor:'pointer',
        appearance:'auto',
      }}
    >
      {options.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
    </select>
    {hint && <div style={{fontSize:11,color:'var(--text-muted)',marginTop:4}}>{hint}</div>}
  </div>
);

const PanelHead = ({ title, subtitle }) => (
  <div style={{marginBottom:24}}>
    <h2 style={{fontFamily:'var(--f-display)',fontSize:20,fontWeight:700,
      color:'var(--text)',margin:'0 0 4px',letterSpacing:'-.01em'}}>{title}</h2>
    {subtitle && <p style={{fontSize:13,color:'var(--text-sec)',margin:0,lineHeight:1.5}}>{subtitle}</p>}
  </div>
);

const Hr = ({ tight }) => (
  <div style={{height:1, background:'var(--border)', margin: tight ? '16px 0' : '22px 0'}}/>
);

const InfoCard = ({ children, style }) => (
  <div style={{
    background:'var(--bg-card)', borderRadius:'var(--r-xl)',
    border:'1px solid var(--border)', padding:'16px 18px', ...style,
  }}>{children}</div>
);

const DangerZone = ({ label, desc, onAction }) => (
  <div style={{
    padding:'14px 16px', borderRadius:'var(--r-lg)',
    background:'hsl(var(--c-danger)/.06)', border:'1px solid hsl(var(--c-danger)/.22)',
    borderLeft:'3px solid hsl(var(--c-danger))',
  }}>
    <div style={{fontFamily:'var(--f-display)',fontSize:13,fontWeight:700,
      color:'hsl(var(--c-danger))',marginBottom:4}}>{label}</div>
    {desc && <div style={{fontSize:12,color:'var(--text-sec)',marginBottom:10,lineHeight:1.5}}>{desc}</div>}
    <button onClick={onAction} style={{
      display:'inline-flex', alignItems:'center', padding:'6px 12px',
      borderRadius:'var(--r-md)', border:'1px solid hsl(var(--c-danger)/.4)',
      background:'transparent', color:'hsl(var(--c-danger))',
      fontFamily:'var(--f-display)', fontSize:12, fontWeight:700, cursor:'pointer',
    }}>{label}</button>
  </div>
);

const SaveFooter = ({ onSave, onCancel, saved }) => (
  <div style={{
    position:'sticky', bottom:0,
    background:'var(--glass-bg)', backdropFilter:'blur(12px)',
    borderTop:'1px solid var(--border)',
    padding:'12px 0', marginTop:24,
    display:'flex', gap:8, justifyContent:'flex-end', alignItems:'center',
  }}>
    {saved && (
      <span style={{fontSize:12,color:'hsl(var(--c-toolkit))',fontWeight:700,fontFamily:'var(--f-display)'}}>
        ✓ Salvato
      </span>
    )}
    <button className="btn ghost" onClick={onCancel} style={{fontFamily:'var(--f-display)',fontSize:12}}>
      Annulla
    </button>
    <button onClick={onSave} style={{
      display:'inline-flex', alignItems:'center', gap:6,
      padding:'8px 16px', borderRadius:'var(--r-md)', border:'none',
      background:'hsl(var(--c-game))', color:'#fff',
      fontFamily:'var(--f-display)', fontSize:12, fontWeight:700, cursor:'pointer',
      transition:'all var(--dur-sm) var(--ease-out)',
    }}>Salva modifiche</button>
  </div>
);

// ─── DESTRUCTIVE MODAL ────────────────────────────────
const DestructiveModal = ({ title, desc, confirmLabel='Elimina', onConfirm, onCancel }) => (
  <div
    style={{
      position:'fixed', inset:0, background:'rgba(0,0,0,.45)',
      display:'flex', alignItems:'center', justifyContent:'center',
      zIndex:60, backdropFilter:'blur(4px)', padding:20,
    }}
    onClick={onCancel}
  >
    <div onClick={e => e.stopPropagation()} style={{
      background:'var(--bg-card)', borderRadius:'var(--r-2xl)',
      border:'1px solid var(--border)', borderLeft:'3px solid hsl(var(--c-danger))',
      boxShadow:'var(--shadow-lg)', padding:24, width:'100%', maxWidth:420,
    }}>
      <div style={{
        background:'hsl(var(--c-danger)/.06)', borderRadius:'var(--r-lg)',
        padding:'14px', marginBottom:18,
        display:'flex', gap:12,
      }}>
        <span style={{fontSize:22, lineHeight:1, flexShrink:0}}>⚠️</span>
        <div>
          <div style={{fontFamily:'var(--f-display)',fontSize:15,fontWeight:700,
            color:'var(--text)',marginBottom:4}}>{title}</div>
          <div style={{fontSize:13,color:'var(--text-sec)',lineHeight:1.55}}>{desc}</div>
        </div>
      </div>
      <div style={{display:'flex', gap:8, justifyContent:'flex-end'}}>
        <button className="btn ghost" onClick={onCancel}
          style={{fontFamily:'var(--f-display)',fontSize:12}}>Annulla</button>
        <button onClick={onConfirm} style={{
          display:'inline-flex', alignItems:'center', padding:'8px 14px',
          borderRadius:'var(--r-md)', border:'none',
          background:'hsl(var(--c-danger))', color:'#fff',
          fontFamily:'var(--f-display)', fontSize:12, fontWeight:700, cursor:'pointer',
        }}>{confirmLabel}</button>
      </div>
    </div>
  </div>
);

// ══════════════════════════════════════════════════════
// ─── PANELS ───────────────────────────────────────────
// ══════════════════════════════════════════════════════

// ─── 1. PROFILE ───────────────────────────────────────
const ProfilePanel = () => {
  const [form, setForm] = useState({
    username:'meepler_42', bio:'Board game enthusiast, Eurogame collector. BGG rank: 1.492.',
    email:'marco@example.com',
  });
  const [saved, setSaved] = useState(false);
  const set = k => e => setForm(f => ({...f, [k]: e.target.value}));
  const save = () => { setSaved(true); setTimeout(() => setSaved(false), 2200); };

  return (
    <div>
      <PanelHead title="Profilo" subtitle="Informazioni visibili agli altri giocatori"/>

      {/* Avatar */}
      <div style={{display:'flex', alignItems:'center', gap:16, marginBottom:24}}>
        <div style={{
          width:68, height:68, borderRadius:'50%', flexShrink:0,
          background:'linear-gradient(135deg, hsl(var(--c-game)), hsl(var(--c-event)))',
          display:'flex', alignItems:'center', justifyContent:'center',
          color:'#fff', fontFamily:'var(--f-display)', fontSize:26, fontWeight:800,
          boxShadow:'0 4px 14px hsl(var(--c-game)/.3)',
        }}>M</div>
        <div>
          <button className="btn sm" style={{fontFamily:'var(--f-display)', marginBottom:5}}>
            Cambia avatar
          </button>
          <div style={{fontSize:11, color:'var(--text-muted)'}}>PNG o JPG · max 2 MB</div>
        </div>
      </div>

      <FInput
        label="Username" value={form.username} onChange={set('username')}
        placeholder="il_tuo_username"
        hint="Visibile agli altri giocatori · 3–20 caratteri, niente spazi"
      />
      <FTextarea
        label="Bio" value={form.bio} onChange={set('bio')}
        placeholder="Dicci qualcosa di te..." rows={3}
      />

      {/* Email con badge verify */}
      <div style={{marginBottom:16}}>
        <Lbl>Email</Lbl>
        <div style={{display:'flex', alignItems:'center', gap:8}}>
          <input value={form.email} onChange={set('email')} type="email"
            style={{
              flex:1, height:40, padding:'0 12px', boxSizing:'border-box',
              borderRadius:'var(--r-md)', border:'1px solid var(--border)',
              background:'var(--bg)', color:'var(--text)', fontSize:13,
              fontFamily:'var(--f-body)', outline:'none',
            }}
            onFocus={e => (e.target.style.borderColor='hsl(var(--c-game)/.6)')}
            onBlur={e => (e.target.style.borderColor='')}
          />
          <span style={{
            padding:'4px 9px', borderRadius:'var(--r-sm)', whiteSpace:'nowrap',
            background:'hsl(var(--c-toolkit)/.12)', color:'hsl(var(--c-toolkit))',
            fontSize:10, fontWeight:700, fontFamily:'var(--f-display)',
          }}>✓ Verificata</span>
        </div>
      </div>

      <SaveFooter onSave={save} onCancel={() => {}} saved={saved}/>
    </div>
  );
};

// ─── 2. ACCOUNT ───────────────────────────────────────
const AccountPanel = () => {
  const [pw, setPw] = useState({ cur:'', next:'', conf:'' });
  const [tfa, setTfa] = useState(false);
  const [showDel, setShowDel] = useState(false);
  const [pwSaved, setPwSaved] = useState(false);
  const set = k => e => setPw(p => ({...p, [k]: e.target.value}));

  const savePw = () => {
    if (!pw.cur || !pw.next || pw.next!==pw.conf) return;
    setPwSaved(true);
    setTimeout(() => setPwSaved(false), 2200);
  };

  return (
    <div>
      <PanelHead title="Account" subtitle="Sicurezza e autenticazione"/>

      <InfoCard style={{marginBottom:16}}>
        <div style={{fontFamily:'var(--f-display)',fontSize:14,fontWeight:700,
          color:'var(--text)',marginBottom:14}}>Cambia password</div>
        <FPwd label="Password attuale" value={pw.cur} onChange={set('cur')} placeholder="••••••••"/>
        <FPwd label="Nuova password" value={pw.next} onChange={set('next')} placeholder="Min. 8 caratteri"/>
        <FPwd label="Conferma nuova" value={pw.conf} onChange={set('conf')} placeholder="Ripeti la password"/>
        <div style={{display:'flex', gap:8, alignItems:'center'}}>
          <button onClick={savePw} style={{
            padding:'8px 14px', borderRadius:'var(--r-md)', border:'none',
            background:'hsl(var(--c-game))', color:'#fff',
            fontFamily:'var(--f-display)', fontSize:12, fontWeight:700, cursor:'pointer',
          }}>Aggiorna password</button>
          {pwSaved && <span style={{fontSize:12,color:'hsl(var(--c-toolkit))',fontWeight:700,fontFamily:'var(--f-display)'}}>✓ Aggiornata</span>}
        </div>
      </InfoCard>

      <InfoCard style={{marginBottom:20}}>
        <div style={{display:'flex', alignItems:'center', justifyContent:'space-between', gap:16}}>
          <div>
            <div style={{fontFamily:'var(--f-display)',fontSize:14,fontWeight:700,
              color:'var(--text)',marginBottom:3}}>Autenticazione a due fattori</div>
            <div style={{fontSize:12,color:'var(--text-sec)'}}>
              {tfa ? '🛡️ Attiva — account protetto' : 'Proteggi l\'account con un codice OTP'}
            </div>
          </div>
          <ToggleSwitch checked={tfa} onChange={setTfa} label="Attiva 2FA"/>
        </div>

        {tfa && (
          <div style={{
            marginTop:16, padding:'12px 14px',
            background:'var(--bg-muted)', borderRadius:'var(--r-md)',
          }}>
            <div style={{fontSize:11,color:'var(--text-sec)',marginBottom:10}}>
              Scansiona con Google / Microsoft Authenticator
            </div>
            {/* QR placeholder */}
            <div style={{
              width:96, height:96, borderRadius:'var(--r-md)',
              background:'var(--bg)', border:'1px solid var(--border)',
              display:'flex', alignItems:'center', justifyContent:'center',
              marginBottom:10,
            }}>
              <svg width="72" height="72" viewBox="0 0 72 72">
                <rect x="2" y="2" width="22" height="22" rx="2" fill="none" stroke="var(--text)" strokeWidth="2"/>
                <rect x="6" y="6" width="14" height="14" rx="1" fill="var(--text)"/>
                <rect x="8" y="8" width="10" height="10" rx="1" fill="var(--bg)"/>
                <rect x="48" y="2" width="22" height="22" rx="2" fill="none" stroke="var(--text)" strokeWidth="2"/>
                <rect x="52" y="6" width="14" height="14" rx="1" fill="var(--text)"/>
                <rect x="54" y="8" width="10" height="10" rx="1" fill="var(--bg)"/>
                <rect x="2" y="48" width="22" height="22" rx="2" fill="none" stroke="var(--text)" strokeWidth="2"/>
                <rect x="6" y="52" width="14" height="14" rx="1" fill="var(--text)"/>
                <rect x="8" y="54" width="10" height="10" rx="1" fill="var(--bg)"/>
                {[28,34,40,28,34,40,28,34,40].map((x,i) => (
                  [28,34,40].map((y,j) => (
                    (i*3+j)%3!==1 && <rect key={`${i}-${j}`} x={x} y={y} width={4} height={4} rx={1} fill="var(--text)"/>
                  ))
                ))}
              </svg>
            </div>
            <div style={{fontSize:10,color:'var(--text-muted)',fontFamily:'var(--f-mono)',letterSpacing:'.06em'}}>
              MFSA-K7P2-W9NB-4XLQ
            </div>
          </div>
        )}
      </InfoCard>

      <Hr/>

      <DangerZone
        label="Elimina account"
        desc="L'eliminazione è irreversibile. Tutti i dati, sessioni, knowledge base e preferenze verranno rimossi definitivamente."
        onAction={() => setShowDel(true)}
      />

      {showDel && (
        <DestructiveModal
          title="Elimina account"
          desc="Tutti i tuoi dati — sessioni, knowledge base, agenti e preferenze — verranno eliminati per sempre. Questa operazione non può essere annullata."
          confirmLabel="Sì, elimina tutto"
          onConfirm={() => setShowDel(false)}
          onCancel={() => setShowDel(false)}
        />
      )}
    </div>
  );
};

// ─── 3. PREFERENCES ───────────────────────────────────
const PreferencesPanel = () => {
  const [prefs, setPrefs] = useState({
    theme:'auto', lang:'it', tz:'Europe/Rome', density:'comfy',
  });
  const [saved, setSaved] = useState(false);
  const set = k => v => setPrefs(p => ({...p, [k]: v}));
  const save = () => { setSaved(true); setTimeout(() => setSaved(false), 2200); };

  return (
    <div>
      <PanelHead title="Preferenze" subtitle="Aspetto e comportamento dell'app"/>

      <InfoCard style={{marginBottom:16}}>
        <div style={{fontFamily:'var(--f-display)',fontSize:13,fontWeight:700,
          color:'var(--text)',marginBottom:14}}>Tema</div>
        <div style={{display:'flex', flexDirection:'column', gap:10}}>
          {[
            {v:'light', label:'☀️  Chiaro'},
            {v:'dark',  label:'🌙  Scuro'},
            {v:'auto',  label:'🖥  Automatico (segue il sistema)'},
          ].map(opt => (
            <label key={opt.v} style={{display:'flex',alignItems:'center',gap:10,cursor:'pointer'}}>
              <input type="radio" name="theme-pref" value={opt.v}
                checked={prefs.theme===opt.v}
                onChange={() => set('theme')(opt.v)}
                style={{accentColor:'hsl(var(--c-game))',width:15,height:15}}
              />
              <span style={{fontSize:13,color:'var(--text)',fontFamily:'var(--f-body)'}}>{opt.label}</span>
            </label>
          ))}
        </div>
      </InfoCard>

      <FSelect label="Lingua" value={prefs.lang}
        onChange={e => set('lang')(e.target.value)}
        options={[
          {value:'it', label:'🇮🇹  Italiano'},
          {value:'en', label:'🇬🇧  English'},
          {value:'es', label:'🇪🇸  Español'},
          {value:'fr', label:'🇫🇷  Français'},
        ]}
      />

      <FSelect label="Fuso orario" value={prefs.tz}
        onChange={e => set('tz')(e.target.value)}
        options={[
          {value:'Europe/Rome',         label:'Europe/Rome (UTC+1)'},
          {value:'Europe/London',       label:'Europe/London (UTC+0)'},
          {value:'America/New_York',    label:'America/New York (UTC-5)'},
          {value:'America/Los_Angeles', label:'America/Los Angeles (UTC-8)'},
          {value:'Asia/Tokyo',          label:'Asia/Tokyo (UTC+9)'},
        ]}
      />

      <div style={{marginBottom:16}}>
        <Lbl>Densità interfaccia</Lbl>
        <SegmentedControl
          options={[
            {value:'compact',  label:'Compatta'},
            {value:'comfy',    label:'Comoda'},
            {value:'spacious', label:'Spaziosa'},
          ]}
          value={prefs.density}
          onChange={set('density')}
        />
      </div>

      <SaveFooter onSave={save} onCancel={() => {}} saved={saved}/>
    </div>
  );
};

// ─── 4. NOTIFICATIONS ─────────────────────────────────
const NOTIF_ITEMS = [
  { key:'sessionStart', label:'Sessione iniziata',  desc:'Nuova sessione di gioco creata o condivisa',  ec:'var(--c-session)' },
  { key:'agentReply',   label:'Risposta agente AI', desc:'Un agente ha risposto alla tua domanda',       ec:'var(--c-agent)'   },
  { key:'gameNight',    label:'Invito game night',  desc:'Sei stato invitato a una serata di gioco',     ec:'var(--c-event)'   },
  { key:'weeklyDigest', label:'Digest settimanale', desc:'Riepilogo dei tuoi giochi e progressi',        ec:'var(--c-kb)'      },
  { key:'systemAlerts', label:'Avvisi di sistema',  desc:'Manutenzione, aggiornamenti, sicurezza',       ec:'var(--c-game)'    },
];

const NotificationsPanel = () => {
  const [notifs, setNotifs] = useState({
    sessionStart:true, agentReply:true, gameNight:false, weeklyDigest:true, systemAlerts:true,
  });
  const toggle = k => v => setNotifs(n => ({...n, [k]: v}));

  return (
    <div>
      <PanelHead title="Notifiche" subtitle="Scegli quali notifiche vuoi ricevere"/>
      <div style={{display:'flex', flexDirection:'column', gap:6}}>
        {NOTIF_ITEMS.map(item => (
          <div key={item.key} style={{
            display:'flex', alignItems:'center', justifyContent:'space-between', gap:16,
            padding:'14px 16px', borderRadius:'var(--r-xl)',
            background:'var(--bg-card)', border:'1px solid var(--border)',
          }}>
            <div style={{display:'flex', alignItems:'flex-start', gap:10}}>
              <div style={{
                width:8, height:8, borderRadius:'50%', flexShrink:0,
                background:`hsl(${item.ec})`, marginTop:5,
              }}/>
              <div>
                <div style={{fontSize:13,fontWeight:700,fontFamily:'var(--f-display)',
                  color:'var(--text)',marginBottom:2}}>{item.label}</div>
                <div style={{fontSize:11,color:'var(--text-muted)',lineHeight:1.4}}>{item.desc}</div>
              </div>
            </div>
            <ToggleSwitch checked={notifs[item.key]} onChange={toggle(item.key)}
              ec={item.ec} label={item.label}/>
          </div>
        ))}
      </div>
    </div>
  );
};

// ─── 5. API KEYS ──────────────────────────────────────
const ApiKeysPanel = () => {
  const [keys, setKeys] = useState([
    {id:1, name:'Produzione',       created:'12 gen 2026', lastUsed:'19 apr 2026', key:'mai_live_xK9p...8w2R'},
    {id:2, name:'Sviluppo locale',  created:'28 feb 2026', lastUsed:'17 apr 2026', key:'mai_dev_3nQ7...5tL1'},
    {id:3, name:'Webhook CI',       created:'5 mar 2026',  lastUsed:'11 apr 2026', key:'mai_ci_w0Lm...9xP4'},
  ]);
  const [revokeId,   setRevokeId]   = useState(null);
  const [showModal,  setShowModal]  = useState(false);
  const [newName,    setNewName]    = useState('');
  const [scopes,     setScopes]     = useState({read:true, write:false, admin:false});
  const [generated,  setGenerated]  = useState('');
  const [copied,     setCopied]     = useState(false);

  const revoke = id => {
    setKeys(ks => ks.filter(k => k.id!==id));
    setRevokeId(null);
  };

  const generate = () => {
    const slug = newName.toLowerCase().replace(/\s+/g,'_').slice(0,6) || 'key';
    setGenerated(`mai_${slug}_${Math.random().toString(36).slice(2,10)}...k9Lm`);
  };

  const closeModal = () => {
    if (generated) {
      setKeys(ks => [...ks, {
        id: Date.now(), name: newName,
        created:'20 apr 2026', lastUsed:'Mai', key: generated,
      }]);
    }
    setShowModal(false); setNewName(''); setScopes({read:true,write:false,admin:false});
    setGenerated(''); setCopied(false);
  };

  return (
    <div>
      <div style={{display:'flex',alignItems:'center',justifyContent:'space-between',marginBottom:24}}>
        <div>
          <h2 style={{fontFamily:'var(--f-display)',fontSize:20,fontWeight:700,
            color:'var(--text)',margin:'0 0 4px',letterSpacing:'-.01em'}}>API Keys</h2>
          <p style={{fontSize:13,color:'var(--text-sec)',margin:0}}>
            {keys.length} {keys.length===1 ? 'chiave attiva' : 'chiavi attive'}
          </p>
        </div>
        <button onClick={() => { setGenerated(''); setShowModal(true); }} style={{
          display:'inline-flex', alignItems:'center', gap:6,
          padding:'8px 14px', borderRadius:'var(--r-md)', border:'none',
          background:'hsl(var(--c-game))', color:'#fff',
          fontFamily:'var(--f-display)', fontSize:12, fontWeight:700, cursor:'pointer',
        }}>+ Nuova chiave</button>
      </div>

      <div style={{display:'flex', flexDirection:'column', gap:8}}>
        {keys.map(k => (
          <InfoCard key={k.id}>
            <div style={{display:'flex',alignItems:'center',justifyContent:'space-between',gap:12}}>
              <div>
                <div style={{fontFamily:'var(--f-display)',fontSize:13,fontWeight:700,
                  color:'var(--text)',marginBottom:3}}>{k.name}</div>
                <div style={{fontFamily:'var(--f-mono)',fontSize:11,color:'var(--text-muted)',marginBottom:5}}>
                  {k.key}
                </div>
                <div style={{display:'flex',gap:10,fontSize:11,color:'var(--text-muted)'}}>
                  <span>Creata {k.created}</span>
                  <span style={{color:'var(--border)'}}>·</span>
                  <span>Ultima richiesta {k.lastUsed}</span>
                </div>
              </div>
              <button onClick={() => setRevokeId(k.id)} style={{
                padding:'5px 10px', borderRadius:'var(--r-md)',
                border:'1px solid hsl(var(--c-danger)/.35)',
                background:'transparent', color:'hsl(var(--c-danger))',
                fontFamily:'var(--f-display)', fontSize:11, fontWeight:700,
                cursor:'pointer', flexShrink:0,
              }}>Revoca</button>
            </div>
          </InfoCard>
        ))}
      </div>

      {/* Revoke confirm */}
      {revokeId && (
        <DestructiveModal
          title="Revoca chiave API"
          desc="Le applicazioni che usano questa chiave perderanno immediatamente l'accesso. L'operazione non può essere annullata."
          confirmLabel="Revoca chiave"
          onConfirm={() => revoke(revokeId)}
          onCancel={() => setRevokeId(null)}
        />
      )}

      {/* Generate modal */}
      {showModal && (
        <div
          style={{
            position:'fixed', inset:0, background:'rgba(0,0,0,.45)',
            display:'flex', alignItems:'center', justifyContent:'center',
            zIndex:60, backdropFilter:'blur(4px)', padding:20,
          }}
          onClick={closeModal}
        >
          <div onClick={e => e.stopPropagation()} style={{
            background:'var(--bg-card)', borderRadius:'var(--r-2xl)',
            border:'1px solid var(--border)', boxShadow:'var(--shadow-lg)',
            padding:24, width:'100%', maxWidth:460,
          }}>
            <div style={{fontFamily:'var(--f-display)',fontSize:16,fontWeight:700,
              color:'var(--text)',marginBottom:18}}>
              {generated ? '🗝️ Chiave generata' : 'Genera nuova chiave API'}
            </div>

            {!generated ? (
              <>
                <FInput label="Nome chiave" value={newName}
                  onChange={e => setNewName(e.target.value)}
                  placeholder="es. Produzione, App iOS, Webhook CI..."
                />
                <div style={{marginBottom:18}}>
                  <Lbl>Permessi (scope)</Lbl>
                  {[
                    {k:'read',  l:'Read',  d:'Accesso in lettura a tutte le risorse'},
                    {k:'write', l:'Write', d:'Creazione e modifica di sessioni e KB'},
                    {k:'admin', l:'Admin', d:'Gestione account e API keys'},
                  ].map(s => (
                    <label key={s.k} style={{display:'flex',alignItems:'flex-start',gap:8,marginBottom:10,cursor:'pointer'}}>
                      <input type="checkbox" checked={scopes[s.k]}
                        onChange={e => setScopes(sc => ({...sc, [s.k]: e.target.checked}))}
                        style={{accentColor:'hsl(var(--c-game))',width:14,height:14,marginTop:2,flexShrink:0}}
                      />
                      <div>
                        <div style={{fontSize:13,fontWeight:700,fontFamily:'var(--f-display)',color:'var(--text)'}}>{s.l}</div>
                        <div style={{fontSize:11,color:'var(--text-muted)'}}>{s.d}</div>
                      </div>
                    </label>
                  ))}
                </div>
                <div style={{display:'flex', gap:8, justifyContent:'flex-end'}}>
                  <button className="btn ghost" onClick={closeModal}
                    style={{fontFamily:'var(--f-display)',fontSize:12}}>Annulla</button>
                  <button onClick={generate} disabled={!newName} style={{
                    padding:'8px 14px', borderRadius:'var(--r-md)', border:'none',
                    background: newName ? 'hsl(var(--c-game))' : 'var(--bg-muted)',
                    color: newName ? '#fff' : 'var(--text-muted)',
                    fontFamily:'var(--f-display)', fontSize:12, fontWeight:700, cursor: newName ? 'pointer' : 'not-allowed',
                  }}>Genera</button>
                </div>
              </>
            ) : (
              <>
                <div style={{
                  padding:'12px 14px', marginBottom:12,
                  background:'hsl(var(--c-toolkit)/.08)',
                  border:'1px solid hsl(var(--c-toolkit)/.3)',
                  borderRadius:'var(--r-md)',
                }}>
                  <div style={{fontSize:11,color:'hsl(var(--c-toolkit))',fontWeight:700,
                    fontFamily:'var(--f-display)',marginBottom:6}}>✓ Copiala ora — non la vedrai più</div>
                  <div style={{fontFamily:'var(--f-mono)',fontSize:12,color:'var(--text)',
                    wordBreak:'break-all',letterSpacing:'.04em'}}>{generated}</div>
                </div>
                <div style={{fontSize:11,color:'var(--text-muted)',marginBottom:16,lineHeight:1.5}}>
                  Conserva questa chiave in un password manager o vault sicuro.
                  Non sarà più visibile dopo aver chiuso questo dialog.
                </div>
                <div style={{display:'flex', gap:8, justifyContent:'flex-end'}}>
                  <button onClick={closeModal} className="btn ghost"
                    style={{fontFamily:'var(--f-display)',fontSize:12}}>Chiudi</button>
                  <button onClick={() => { navigator.clipboard?.writeText(generated); setCopied(true); setTimeout(()=>setCopied(false),2000); }}
                    style={{
                      padding:'8px 14px', borderRadius:'var(--r-md)', border:'none',
                      background:`hsl(${copied ? 'var(--c-toolkit)' : 'var(--c-game)'})`, color:'#fff',
                      fontFamily:'var(--f-display)', fontSize:12, fontWeight:700, cursor:'pointer',
                      transition:'background var(--dur-sm)',
                    }}>
                    {copied ? '✓ Copiata!' : 'Copia chiave'}
                  </button>
                </div>
              </>
            )}
          </div>
        </div>
      )}
    </div>
  );
};

// ─── 6. SERVICES ──────────────────────────────────────
const GoogleIcon = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
    <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z" fill="#4285F4"/>
    <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/>
    <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l3.66-2.84z" fill="#FBBC05"/>
    <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/>
  </svg>
);

const DiscordIcon = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="#5865F2">
    <path d="M20.317 4.37a19.791 19.791 0 00-4.885-1.515.074.074 0 00-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 00-5.487 0 12.64 12.64 0 00-.617-1.25.077.077 0 00-.079-.037A19.736 19.736 0 003.677 4.37a.07.07 0 00-.032.027C.533 9.046-.32 13.58.099 18.057c.002.022.015.043.032.056a19.9 19.9 0 005.993 3.03.078.078 0 00.084-.028c.462-.63.874-1.295 1.226-1.993.021-.041.001-.09-.041-.106a13.1 13.1 0 01-1.872-.892.077.077 0 01-.008-.128 10.2 10.2 0 00.372-.292.074.074 0 01.077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 01.078.01c.12.098.246.198.373.292a.077.077 0 01-.006.127 12.299 12.299 0 01-1.873.892.077.077 0 00-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 00.084.028 19.839 19.839 0 006.002-3.03.077.077 0 00.032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 00-.031-.03z"/>
  </svg>
);

const BggIcon = () => (
  <svg width="20" height="20" viewBox="0 0 24 24">
    <rect width="24" height="24" rx="4" fill="#FF5100"/>
    <text x="12" y="16.5" textAnchor="middle" fill="white"
      fontSize="8" fontWeight="800" fontFamily="system-ui, sans-serif">BGG</text>
  </svg>
);

const SERVICES_DATA = [
  {id:'google',  name:'Google',        desc:'Accesso con Google Account',         connected:true,  since:'12 gen 2026', Icon:GoogleIcon},
  {id:'discord', name:'Discord',       desc:'Condividi sessioni nel server',       connected:true,  since:'3 feb 2026',  Icon:DiscordIcon},
  {id:'bgg',     name:'BoardGameGeek', desc:'Sincronizza la tua collezione BGG',   connected:false, since:null,          Icon:BggIcon},
];

const ServicesPanel = () => {
  const [services, setServices] = useState(SERVICES_DATA);
  const [disconnectId, setDisconnectId] = useState(null);

  const connect    = id => setServices(ss => ss.map(s => s.id===id ? {...s, connected:true, since:'20 apr 2026'} : s));
  const disconnect = id => { setServices(ss => ss.map(s => s.id===id ? {...s, connected:false, since:null} : s)); setDisconnectId(null); };

  return (
    <div>
      <PanelHead title="Servizi connessi" subtitle="Collega account esterni per arricchire l'esperienza MeepleAI"/>
      <div style={{display:'flex', flexDirection:'column', gap:8}}>
        {services.map(svc => (
          <InfoCard key={svc.id} style={{display:'flex', alignItems:'center', gap:14}}>
            <div style={{
              width:44, height:44, borderRadius:'var(--r-md)', flexShrink:0,
              background:'var(--bg-muted)',
              display:'flex', alignItems:'center', justifyContent:'center',
            }}>
              <svc.Icon/>
            </div>
            <div style={{flex:1, minWidth:0}}>
              <div style={{fontFamily:'var(--f-display)',fontSize:13,fontWeight:700,
                color:'var(--text)',marginBottom:2}}>{svc.name}</div>
              <div style={{fontSize:11,color:'var(--text-muted)'}}>{svc.desc}</div>
              {svc.connected && svc.since && (
                <div style={{fontSize:10,color:'var(--text-muted)',marginTop:2,fontFamily:'var(--f-mono)'}}>
                  Dal {svc.since}
                </div>
              )}
            </div>
            {svc.connected ? (
              <div style={{display:'flex', alignItems:'center', gap:8, flexShrink:0}}>
                <span style={{
                  padding:'3px 8px', borderRadius:'var(--r-sm)',
                  background:'hsl(var(--c-toolkit)/.12)', color:'hsl(var(--c-toolkit))',
                  fontSize:10, fontWeight:700, fontFamily:'var(--f-display)', whiteSpace:'nowrap',
                }}>● Connesso</span>
                <button onClick={() => setDisconnectId(svc.id)} style={{
                  padding:'5px 10px', borderRadius:'var(--r-md)',
                  border:'1px solid var(--border)', background:'transparent',
                  color:'var(--text-muted)', fontFamily:'var(--f-display)',
                  fontSize:11, fontWeight:700, cursor:'pointer',
                }}>Disconnetti</button>
              </div>
            ) : (
              <button onClick={() => connect(svc.id)} style={{
                padding:'7px 12px', borderRadius:'var(--r-md)', border:'none',
                background:'hsl(var(--c-game))', color:'#fff',
                fontFamily:'var(--f-display)', fontSize:11, fontWeight:700,
                cursor:'pointer', flexShrink:0,
              }}>Connetti</button>
            )}
          </InfoCard>
        ))}
      </div>

      {disconnectId && (
        <DestructiveModal
          title={`Disconnetti ${services.find(s=>s.id===disconnectId)?.name}`}
          desc="Il collegamento con il servizio esterno verrà rimosso. Potrai ricollegarlo in qualsiasi momento."
          confirmLabel="Disconnetti"
          onConfirm={() => disconnect(disconnectId)}
          onCancel={() => setDisconnectId(null)}
        />
      )}
    </div>
  );
};

// ══════════════════════════════════════════════════════
// ─── DESKTOP APP SHELL ────────────────────────────────
// ══════════════════════════════════════════════════════

const CATEGORIES = [
  {id:'profile',       label:'Profilo',           icon:'👤', desc:'Avatar, username, bio'},
  {id:'account',       label:'Account',            icon:'🔐', desc:'Password, 2FA, sicurezza'},
  {id:'preferences',   label:'Preferenze',         icon:'🎨', desc:'Tema, lingua, densità'},
  {id:'notifications', label:'Notifiche',           icon:'🔔', desc:'Gestisci le notifiche'},
  {id:'apikeys',       label:'API Keys',            icon:'🗝️', desc:'3 chiavi attive', badge:'3'},
  {id:'services',      label:'Servizi connessi',    icon:'🔗', desc:'Google, Discord, BGG'},
];

const PANELS = {
  profile:       ProfilePanel,
  account:       AccountPanel,
  preferences:   PreferencesPanel,
  notifications: NotificationsPanel,
  apikeys:       ApiKeysPanel,
  services:      ServicesPanel,
};

function SettingsApp() {
  const saved = localStorage.getItem('mai-settings-panel') || 'profile';
  const [active, setActive] = useState(saved);
  const [search, setSearch] = useState('');
  const Panel = PANELS[active] || ProfilePanel;

  const nav = id => { setActive(id); localStorage.setItem('mai-settings-panel', id); };

  const filtered = CATEGORIES.filter(c =>
    !search || c.label.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <div style={{display:'flex', height:'100%', overflow:'hidden', background:'var(--bg)'}}>
      {/* ── Sidebar ── */}
      <div style={{
        width:260, flexShrink:0,
        background:'var(--bg-card)', borderRight:'1px solid var(--border)',
        display:'flex', flexDirection:'column', overflowY:'auto',
      }}>
        <div style={{padding:'22px 16px 14px'}}>
          <div style={{fontFamily:'var(--f-display)',fontSize:19,fontWeight:800,
            color:'var(--text)',marginBottom:14,letterSpacing:'-.02em'}}>Impostazioni</div>
          <input type="search" placeholder="Cerca..." value={search}
            onChange={e => setSearch(e.target.value)}
            style={{
              display:'block', width:'100%', height:34, padding:'0 10px',
              boxSizing:'border-box', borderRadius:'var(--r-md)',
              border:'1px solid var(--border)', background:'var(--bg-muted)',
              color:'var(--text)', fontSize:12, fontFamily:'var(--f-body)', outline:'none',
            }}
          />
        </div>
        <nav style={{padding:'2px 8px 20px', flex:1}}>
          {filtered.map(cat => (
            <button
              key={cat.id}
              onClick={() => nav(cat.id)}
              style={{
                display:'flex', alignItems:'center', gap:10, width:'100%',
                padding:'9px 10px', borderRadius:'var(--r-md)', border:'none',
                borderLeft: active===cat.id
                  ? '3px solid hsl(var(--c-game))'
                  : '3px solid transparent',
                background: active===cat.id ? 'hsl(var(--c-game)/.1)' : 'transparent',
                cursor:'pointer', textAlign:'left',
                transition:'all var(--dur-sm)', marginBottom:2,
              }}
            >
              <span style={{fontSize:16, lineHeight:1}}>{cat.icon}</span>
              <span style={{
                flex:1, fontSize:13, fontWeight:700, fontFamily:'var(--f-display)',
                color: active===cat.id ? 'hsl(var(--c-game))' : 'var(--text)',
              }}>{cat.label}</span>
              {cat.badge && (
                <span style={{
                  padding:'2px 7px', borderRadius:'var(--r-pill)',
                  background:'hsl(var(--c-game)/.15)', color:'hsl(var(--c-game))',
                  fontSize:10, fontWeight:700, fontFamily:'var(--f-display)',
                }}>{cat.badge}</span>
              )}
            </button>
          ))}
        </nav>
        {/* User footer */}
        <div style={{
          padding:'14px 16px', borderTop:'1px solid var(--border)',
          display:'flex', alignItems:'center', gap:10,
        }}>
          <div style={{
            width:32, height:32, borderRadius:'50%', flexShrink:0,
            background:'linear-gradient(135deg,hsl(var(--c-game)),hsl(var(--c-event)))',
            display:'flex', alignItems:'center', justifyContent:'center',
            color:'#fff', fontFamily:'var(--f-display)', fontSize:13, fontWeight:800,
          }}>M</div>
          <div style={{flex:1, minWidth:0}}>
            <div style={{fontSize:12,fontWeight:700,fontFamily:'var(--f-display)',
              color:'var(--text)',whiteSpace:'nowrap',overflow:'hidden',textOverflow:'ellipsis'}}>
              meepler_42
            </div>
            <div style={{fontSize:10,color:'var(--text-muted)'}}>Free plan</div>
          </div>
        </div>
      </div>

      {/* ── Content ── */}
      <div style={{flex:1, overflowY:'auto', padding:'28px 36px 40px'}}>
        <div style={{maxWidth:640}}>
          <Panel/>
        </div>
      </div>
    </div>
  );
}

// ══════════════════════════════════════════════════════
// ─── MOBILE PHONE SCREENS ─────────────────────────────
// ══════════════════════════════════════════════════════

const PhoneSbar = () => (
  <div className="phone-sbar" style={{
    background:'var(--bg)', color:'var(--text-sec)',
    fontFamily:'var(--f-display)', flexShrink:0,
  }}>
    <span>14:32</span>
    <div className="ind"><span>📶</span><span>🔋</span></div>
  </div>
);

// Mobile hub
const MobileHub = ({ onNav }) => (
  <>
    <PhoneSbar/>
    <div style={{flex:1, overflowY:'auto', scrollbarWidth:'none', background:'var(--bg)'}}>
      <div style={{padding:'16px 16px 24px'}}>
        <div style={{fontFamily:'var(--f-display)',fontSize:18,fontWeight:800,
          color:'var(--text)',marginBottom:16,letterSpacing:'-.01em'}}>Impostazioni</div>

        {/* User card */}
        <div style={{
          display:'flex', alignItems:'center', gap:12, padding:'12px 14px',
          background:'var(--bg-card)', borderRadius:'var(--r-xl)',
          border:'1px solid var(--border)', marginBottom:16, cursor:'pointer',
        }} onClick={() => onNav('profile')}>
          <div style={{
            width:44, height:44, borderRadius:'50%', flexShrink:0,
            background:'linear-gradient(135deg,hsl(var(--c-game)),hsl(var(--c-event)))',
            display:'flex', alignItems:'center', justifyContent:'center',
            color:'#fff', fontFamily:'var(--f-display)', fontSize:18, fontWeight:800,
          }}>M</div>
          <div style={{flex:1}}>
            <div style={{fontFamily:'var(--f-display)',fontSize:14,fontWeight:700,color:'var(--text)'}}>
              meepler_42
            </div>
            <div style={{fontSize:11,color:'var(--text-muted)'}}>marco@example.com</div>
          </div>
          <span style={{color:'var(--text-muted)',fontSize:16}}>›</span>
        </div>

        {/* Category list */}
        <div style={{display:'flex', flexDirection:'column', gap:4}}>
          {CATEGORIES.map(cat => (
            <button key={cat.id} onClick={() => onNav(cat.id)} style={{
              display:'flex', alignItems:'center', gap:12, width:'100%',
              padding:'12px 14px', borderRadius:'var(--r-lg)',
              border:'1px solid var(--border)',
              background:'var(--bg-card)', cursor:'pointer', textAlign:'left',
              transition:'all var(--dur-sm)',
            }}>
              <span style={{fontSize:18,lineHeight:1}}>{cat.icon}</span>
              <div style={{flex:1}}>
                <div style={{fontSize:13,fontWeight:700,fontFamily:'var(--f-display)',color:'var(--text)'}}>
                  {cat.label}
                </div>
                <div style={{fontSize:11,color:'var(--text-muted)',marginTop:1}}>{cat.desc}</div>
              </div>
              {cat.badge && (
                <span style={{
                  padding:'2px 7px', borderRadius:'var(--r-pill)',
                  background:'hsl(var(--c-game)/.12)', color:'hsl(var(--c-game))',
                  fontSize:10, fontWeight:700, fontFamily:'var(--f-display)',
                }}>{cat.badge}</span>
              )}
              <span style={{color:'var(--text-muted)',fontSize:15}}>›</span>
            </button>
          ))}
        </div>
      </div>
    </div>
  </>
);

// Mobile panel wrapper
const MobilePanel = ({ panelId, onBack }) => {
  const Panel = PANELS[panelId] || ProfilePanel;
  const cat = CATEGORIES.find(c => c.id===panelId);
  return (
    <>
      <PhoneSbar/>
      <div style={{
        display:'flex', alignItems:'center', gap:8,
        padding:'8px 14px', background:'var(--glass-bg)',
        backdropFilter:'blur(12px)', borderBottom:'1px solid var(--border)',
        position:'sticky', top:0, zIndex:10, flexShrink:0,
      }}>
        <button onClick={onBack} style={{
          display:'flex', alignItems:'center', gap:2,
          padding:'4px 8px', border:'none', background:'transparent',
          color:'hsl(var(--c-game))', fontFamily:'var(--f-display)',
          fontSize:13, fontWeight:700, cursor:'pointer',
        }}>‹ Indietro</button>
        <div style={{flex:1, textAlign:'center', fontFamily:'var(--f-display)',
          fontSize:13, fontWeight:700, color:'var(--text)'}}>{cat?.label}</div>
        <div style={{width:80}}/>
      </div>
      <div style={{flex:1, overflowY:'auto', scrollbarWidth:'none', padding:'16px 14px 28px'}}>
        <Panel/>
      </div>
    </>
  );
};

// Mobile interactive phone
const MobilePhone = ({ initialScreen, label, desc }) => {
  const [screen, setScreen] = useState(initialScreen);
  return (
    <div style={{display:'flex', flexDirection:'column', alignItems:'center', gap:10}}>
      <div style={{fontFamily:'var(--f-mono)',fontSize:11,color:'var(--text-sec)',
        textTransform:'uppercase',letterSpacing:'.08em',fontWeight:700}}>{label}</div>
      <div className="phone" style={{display:'flex', flexDirection:'column'}}>
        {screen==='hub' ? (
          <MobileHub onNav={setScreen}/>
        ) : (
          <MobilePanel panelId={screen} onBack={() => setScreen('hub')}/>
        )}
      </div>
      {desc && (
        <div style={{fontSize:11,color:'var(--text-muted)',maxWidth:340,textAlign:'center',lineHeight:1.55}}>
          {desc}
        </div>
      )}
    </div>
  );
};

function MobilePhones() {
  return (
    <div className="phones-grid">
      <MobilePhone initialScreen="hub"
        label="01 · Hub"
        desc="Lista categorie con avatar strip, badge contatori — ogni item apre il pannello full-page"
      />
      <MobilePhone initialScreen="notifications"
        label="02 · Notifiche"
        desc="Toggle entity-colored per tipo — session=indigo, agent=amber, event=rose, kb=teal"
      />
      <MobilePhone initialScreen="apikeys"
        label="03 · API Keys"
        desc="Lista chiavi + revoca destructive + modal genera nuova chiave con scopes e copy-to-clipboard"
      />
      <MobilePhone initialScreen="services"
        label="04 · Servizi"
        desc="OAuth services con badge stato, connect flow e disconnect confirm"
      />
    </div>
  );
}

// ─── MOUNT ────────────────────────────────────────────
ReactDOM.createRoot(document.getElementById('desktop-root')).render(<SettingsApp/>);
ReactDOM.createRoot(document.getElementById('mobile-root')).render(<MobilePhones/>);

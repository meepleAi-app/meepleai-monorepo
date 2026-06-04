/* MeepleAI Nav Prototype — Auth (login/register modal) + Onboarding wizard.
   Pre-app flow gated by the dev-only "Simulate first visit" toggle. */
const { useState: useStateA } = React;

function OAuthRow() {
  return h('div', null,
    h('div', { className: 'auth-divider' }, h('span', null, 'oppure')),
    h('div', { className: 'oauth-row' },
      h('button', { className: 'oauth-btn' }, h('span', null, '🔵'), 'Continua con Google'),
      h('button', { className: 'oauth-btn' }, h('span', null, '🟣'), 'Continua con Discord')),
    h('div', { style: { marginTop: 'var(--s-2)' } }, h(Gap, { cat: 'GAP-FEATURE', loc: 'Auth · OAuth', note: 'flow Google/Discord TBD' })));
}

function LoginForm({ state, onLogin, switchTab }) {
  const [show, setShow] = useStateA(false);
  const loading = state === 'loading';
  const offline = state === 'offline';
  return h('div', null,
    state === 'error' ? h('div', { className: 'auth-banner err' }, '⚠ Email o password non validi') : null,
    h('div', { className: 'wz-field' }, h('label', null, 'Email'), h('input', { className: 'wz-input', type: 'email', placeholder: 'tu@esempio.it', disabled: offline })),
    h('div', { className: 'wz-field' }, h('label', null, 'Password'),
      h('div', { className: 'pw-wrap' }, h('input', { className: 'wz-input', type: show ? 'text' : 'password', placeholder: '••••••••', disabled: offline }), h('button', { className: 'pw-toggle', onClick: () => setShow(s => !s) }, show ? '🙈' : '👁')),
      h('button', { className: 'auth-link right' }, 'Password dimenticata?'),
      h('div', { style: { marginTop: 'var(--s-1)' } }, h(Gap, { cat: 'GAP-CTA', mini: true, loc: 'Login · reset', note: 'password reset flow TBD' }))),
    h('label', { className: 'auth-check' }, h('input', { type: 'checkbox', defaultChecked: true }), 'Ricordami'),
    h('button', { className: 'auth-submit', disabled: loading || offline, onClick: onLogin }, loading ? h('span', null, h('span', { className: 'spin' }), 'Accesso in corso…') : 'Accedi'),
    h(OAuthRow, null),
    h('div', { className: 'auth-foot' }, 'Non hai un account? ', h('button', { className: 'auth-link', onClick: () => switchTab('register') }, 'Registrati'))
  );
}

function RegisterForm({ state, onRegister, switchTab }) {
  const [pw, setPw] = useStateA(''); const [pw2, setPw2] = useStateA(''); const [user, setUser] = useStateA(''); const [terms, setTerms] = useStateA(false);
  const loading = state === 'loading'; const offline = state === 'offline';
  const pwOk = pw.length >= 8 && /\d/.test(pw);
  const matchOk = pw2.length > 0 && pw === pw2;
  const userOk = /^[a-zA-Z0-9]{3,20}$/.test(user);
  const valid = pwOk && matchOk && userOk && terms && !offline;
  return h('div', null,
    state === 'error' ? h('div', { className: 'auth-banner err' }, '⚠ Email già registrata') : null,
    h('div', { className: 'wz-field' }, h('label', null, 'Email'), h('input', { className: 'wz-input', type: 'email', placeholder: 'tu@esempio.it', disabled: offline })),
    h('div', { className: 'wz-field' }, h('label', null, 'Password'),
      h('input', { className: 'wz-input', type: 'password', value: pw, onChange: (e) => setPw(e.target.value), disabled: offline }),
      h('span', { className: 'field-hint' + (pw && !pwOk ? ' bad' : (pwOk ? ' ok' : '')) }, 'Min 8 caratteri, almeno 1 numero')),
    h('div', { className: 'wz-field' }, h('label', null, 'Conferma password'),
      h('input', { className: 'wz-input', type: 'password', value: pw2, onChange: (e) => setPw2(e.target.value), disabled: offline }),
      pw2 ? h('span', { className: 'field-hint' + (matchOk ? ' ok' : ' bad') }, matchOk ? 'Le password coincidono' : 'Le password non coincidono') : null),
    h('div', { className: 'wz-field' }, h('label', null, 'Username'),
      h('input', { className: 'wz-input', value: user, onChange: (e) => setUser(e.target.value), disabled: offline }),
      h('span', { className: 'field-hint' + (user && !userOk ? ' bad' : (userOk ? ' ok' : '')) }, '3–20 caratteri, alfanumerico')),
    h('label', { className: 'auth-check' }, h('input', { type: 'checkbox', checked: terms, onChange: (e) => setTerms(e.target.checked) }), 'Accetto ', h('button', { className: 'auth-link inline' }, 'Terms & Privacy'),
      h(Gap, { cat: 'GAP-CTA', mini: true, loc: 'Register · legal', note: 'legal pages TBD' })),
    h('button', { className: 'auth-submit', disabled: !valid || loading, onClick: onRegister }, loading ? h('span', null, h('span', { className: 'spin' }), 'Creazione account…') : 'Crea account'),
    h(OAuthRow, null),
    h('div', { className: 'auth-foot' }, 'Hai già un account? ', h('button', { className: 'auth-link', onClick: () => switchTab('login') }, 'Accedi'))
  );
}

function AuthModal({ mode, setMode, state, onLogin, onRegister }) {
  return h('div', { className: 'auth-scrim' },
    h('div', { className: 'auth-modal' },
      state === 'offline' ? h('div', { className: 'auth-banner off sticky' }, '📡 Sei offline. Riprova quando torni online.') : null,
      h('div', { className: 'auth-logo' }, h('div', { className: 'brand-mark' }, 'M'), h('span', null, 'MeepleAI')),
      h('div', { className: 'auth-tabs' },
        h('button', { className: 'auth-tab', 'data-active': mode === 'login', onClick: () => setMode('login') }, 'Accedi'),
        h('button', { className: 'auth-tab', 'data-active': mode === 'register', onClick: () => setMode('register') }, 'Registrati')),
      h('div', { className: 'auth-body' },
        mode === 'login' ? h(LoginForm, { state, onLogin, switchTab: setMode }) : h(RegisterForm, { state, onRegister, switchTab: setMode }))
    )
  );
}

/* ── Onboarding ── */
const GENRES = ['Strategia', 'Cooperativo', 'Eurogame', 'Ameritrash', 'Party', 'Deck builder', 'Worker placement', 'Roll-and-write', 'Narrativo'];

function Onboarding({ state, onFinish }) {
  const [step, setStep] = useStateA(1);
  const [genres, setGenres] = useStateA([]);
  const [bgg, setBgg] = useStateA('');
  const [invites, setInvites] = useStateA([]);
  const [inviteVal, setInviteVal] = useStateA('');
  const toggleGenre = (g) => setGenres(a => a.includes(g) ? a.filter(x => x !== g) : [...a, g]);
  const addInvite = () => { if (inviteVal.trim()) { setInvites(a => [...a, inviteVal.trim()]); setInviteVal(''); } };

  const body =
    step === 1 ? h('div', { className: 'onb-step' },
      h('h1', null, 'Benvenuto in MeepleAI'),
      h('p', null, 'Scegli i generi che ti piacciono. Useremo questo per suggerirti game e agent.'),
      h('div', { className: 'genre-grid' }, GENRES.map(g => h('button', { key: g, className: 'genre-chip' + (genres.includes(g) ? ' on' : ''), onClick: () => toggleGenre(g) }, g)))) :
    step === 2 ? h('div', { className: 'onb-step' },
      h('h1', null, 'Importa la tua library da BGG'),
      h('p', null, 'Sincronizza la tua collezione BoardGameGeek. Puoi saltare e aggiungere giochi manualmente più tardi.'),
      h('div', { className: 'bgg-card' },
        state === 'offline' ? h('div', { className: 'auth-banner off' }, '📡 Offline — riprova quando torni online.') : null,
        state === 'error' ? h('div', { className: 'auth-banner err' }, '⚠ BGG non raggiungibile') : null,
        h('div', { className: 'bgg-logo' }, '🎲 BGG'),
        h('input', { className: 'wz-input', placeholder: 'BGG username', value: bgg, onChange: (e) => setBgg(e.target.value), disabled: state === 'offline' }),
        state === 'loading'
          ? h('button', { className: 'auth-submit', style: { background: 'hsl(var(--c-game))' }, disabled: true }, h('span', { className: 'spin' }), 'Sincronizzazione in corso…')
          : h('button', { className: 'auth-submit', style: { background: 'hsl(var(--c-game))' }, disabled: state === 'offline' }, state === 'error' ? 'Riprova' : 'Importa library'),
        h(Gap, { cat: 'GAP-FEATURE', loc: 'Onboarding · BGG', note: 'BGG sync flow TBD' })),
      h('button', { className: 'auth-link center', onClick: () => setStep(3) }, 'Salta per ora')) :
    h('div', { className: 'onb-step' },
      h('h1', null, 'Gioca con i tuoi friend'),
      h('p', null, 'Invita i tuoi compagni di tavolo. Potranno partecipare alle tue Game Night e condividere la library.'),
      h('div', { className: 'invite-row' },
        h('input', { className: 'wz-input', placeholder: 'email o username', value: inviteVal, onChange: (e) => setInviteVal(e.target.value), onKeyDown: (e) => { if (e.key === 'Enter') addInvite(); } }),
        h('button', { className: 'btn ghost', onClick: addInvite }, 'Invita')),
      invites.length ? h('div', { className: 'wz-chips', style: { marginTop: 'var(--s-3)' } }, invites.map((iv, i) => h('span', { key: i, className: 'wz-chip' }, iv, h('button', { className: 'wz-chip-x', onClick: () => setInvites(a => a.filter((_, j) => j !== i)) }, '✕')))) : null,
      h('button', { className: 'auth-link center', onClick: onFinish }, 'Salta per ora'));

  const canNext = step === 1 ? genres.length >= 1 : true;
  return h('div', { className: 'onb' },
    h('div', { className: 'onb-top' },
      h('div', { className: 'auth-logo sm' }, h('div', { className: 'brand-mark' }, 'M'), h('span', null, 'MeepleAI')),
      h('div', { className: 'onb-dots' }, [1, 2, 3].map(d => h('span', { key: d, className: 'onb-dot' + (d === step ? ' on' : '') }))),
      h('button', { className: 'auth-link', onClick: onFinish }, 'Salta')),
    h('div', { className: 'onb-body' }, body),
    h('div', { className: 'onb-foot' },
      step > 1 ? h('button', { className: 'btn ghost', onClick: () => setStep(s => s - 1) }, '‹ Indietro') : h('span'),
      step === 1 ? h('button', { className: 'auth-link', onClick: () => setStep(3) }, 'Salta') : h('span'),
      step < 3 ? h('button', { className: 'auth-submit inline', disabled: !canNext, onClick: () => setStep(s => s + 1) }, 'Avanti ›')
        : h('button', { className: 'auth-submit inline', onClick: onFinish }, 'Inizia'))
  );
}

Object.assign(window, { AuthModal, Onboarding });

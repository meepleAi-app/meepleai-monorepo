/* MeepleAI — Public marketing page components
   React 18 UMD + Babel standalone
   Requires: tokens.css, components.css */

// ─── NAV ────────────────────────────────────────────────────────────────────
function PublicNav({ currentPage, onNav }) {
  const [menuOpen, setMenuOpen] = React.useState(false);
  const links = [
    { id: 'landing', label: 'Prodotto' },
    { id: 'pricing', label: 'Prezzi' },
    { id: 'about',   label: 'Chi siamo' },
    { id: 'contact', label: 'Contatti' },
  ];
  return (
    <>
      <nav className="hub-nav" style={{ position: 'sticky', top: 0, zIndex: 20 }}>
        <a href="00-hub.html" className="brand" style={{ textDecoration: 'none' }}>
          <div className="brand-mark">M</div>
          <span>MeepleAI</span>
        </a>
        <div className="spacer" />
        {/* desktop links */}
        <div className="pub-nav-links">
          {links.map(l => (
            <a key={l.id} href="#" className={currentPage === l.id ? 'active' : ''}
               onClick={e => { e.preventDefault(); onNav(l.id); }}>
              {l.label}
            </a>
          ))}
        </div>
        <div style={{ width: 1, height: 20, background: 'var(--border)', margin: '0 4px' }} />
        <a href="auth-flow.html" className="btn ghost" style={{ padding: '6px 12px', fontSize: 'var(--fs-sm)' }}>Accedi</a>
        <a href="auth-flow.html" className="btn primary e-game" style={{ padding: '6px 14px', fontSize: 'var(--fs-sm)' }}>Inizia gratis</a>
        {/* hamburger */}
        <button className="ham-btn" onClick={() => setMenuOpen(!menuOpen)}
          aria-label="Menu" style={{ display: 'none' }}>
          <span /><span /><span />
        </button>
      </nav>
      {/* mobile drawer */}
      {menuOpen && (
        <div className="mob-drawer" onClick={() => setMenuOpen(false)}>
          <div className="mob-drawer-inner" onClick={e => e.stopPropagation()}>
            {links.map(l => (
              <a key={l.id} href="#" onClick={e => { e.preventDefault(); onNav(l.id); setMenuOpen(false); }}>
                {l.label}
              </a>
            ))}
            <hr style={{ border: 'none', borderTop: '1px solid var(--border)', margin: '8px 0' }} />
            <a href="auth-flow.html">Accedi</a>
            <a href="auth-flow.html" style={{ color: 'hsl(var(--c-game))', fontWeight: 700 }}>Inizia gratis →</a>
          </div>
        </div>
      )}
    </>
  );
}

// ─── LANDING ────────────────────────────────────────────────────────────────
function HeroSection({ onNav }) {
  return (
    <section className="pub-hero" id="hero">
      <div className="pub-hero-bg" aria-hidden="true" />
      <div className="pub-wrap">
        <p className="hero-kicker">AI · Board Game Assistant · RAG + Multi-agent</p>
        <h1>
          L'assistente AI che conosce il tuo
          {' '}<span className="mark">boardgame</span>
          <br />meglio di te
        </h1>
        <p className="hero-lead">
          Chiedi la regola esatta e ottieni la pagina del manuale. Organizza la serata,
          traccia le partite, ricorda le house rules. Tutto in un'unica piattaforma.
        </p>
        <div className="hero-ctas">
          <a href="auth-flow.html" className="btn primary e-game hero-btn">
            Inizia gratis — è gratuito
          </a>
          <button className="btn ghost hero-btn" onClick={() => document.getElementById('features')?.scrollIntoView({ behavior: 'smooth' })}>
            Vedi come funziona ↓
          </button>
        </div>
      </div>
    </section>
  );
}

function StatsRow() {
  const stats = [
    { v: '9', l: 'Tipi di entità' },
    { v: '24+', l: 'Schermate & flow' },
    { v: 'RAG', l: 'Hybrid search' },
    { v: 'Multi', l: 'Agent system' },
  ];
  return (
    <div className="pub-wrap stat-row">
      {stats.map(s => (
        <div key={s.l} className="st">
          <div className="v" style={{ color: 'hsl(var(--c-game))' }}>{s.v}</div>
          <div className="l">{s.l}</div>
        </div>
      ))}
    </div>
  );
}

function FeatureGrid() {
  const features = [
    { e: 'e-kb',      icon: '📚', title: 'RAG Hybrid Search',      desc: 'Trova la regola esatta con citazione pagina PDF. Zero allucinazioni, massima precisione.' },
    { e: 'e-agent',   icon: '🤖', title: 'Multi-agent System',     desc: 'Agente Regole, Agente Strategia, Agente Setup — ognuno specializzato, coordinati da un orchestratore.' },
    { e: 'e-game',    icon: '🎲', title: 'Living Documentation',   desc: 'Le tue house rules diventano parte della base di conoscenza. Il sistema impara dal tuo gioco.' },
    { e: 'e-session', icon: '⏱️', title: 'Session Mode',           desc: 'Timer, punteggio live, storico partite — tutto in un\'unica schermata durante il gioco.' },
    { e: 'e-event',   icon: '🌙', title: 'Game Nights',            desc: 'Pianifica serate multi-gioco, invita amici, scrivi diari cross-game. Ogni serata raccontata.' },
    { e: 'e-player',  icon: '🗂️', title: 'Community Catalog',     desc: 'Catalogo giochi condiviso. Carica PDF, aggiunte, promo — accessibili a tutti i tuoi giocatori.' },
  ];
  return (
    <section id="features" style={{ padding: '80px 0' }}>
      <div className="pub-wrap">
        <p className="section-label" style={{ marginTop: 0 }}>Funzionalità</p>
        <h2 className="pub-section-title">Tutto quello che serve<br />a un boardgamer serio</h2>
        <div className="feat-grid">
          {features.map(f => (
            <article key={f.title} className={`card interactive ${f.e}`} style={{ padding: '28px', display: 'flex', flexDirection: 'column', gap: 12 }}>
              <div style={{ fontSize: 32 }} aria-hidden="true">{f.icon}</div>
              <h3 style={{ fontSize: 'var(--fs-lg)', margin: 0 }}>{f.title}</h3>
              <p style={{ color: 'var(--text-sec)', fontSize: 'var(--fs-sm)', lineHeight: 'var(--lh-relax)', margin: 0 }}>{f.desc}</p>
              <div className="e-chip" style={{ marginTop: 'auto', alignSelf: 'flex-start' }}>{f.e.replace('e-','')}</div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}

function TestimonialSection() {
  const tests = [
    { name: 'Marco R.', role: 'Content creator board game', avatar: 'MR', color: 'var(--c-game)',
      quote: '"Finalmente non devo più sfogliare 48 pagine di manuale durante una partita. MeepleAI trova la regola in 3 secondi, con tanto di pagina."' },
    { name: 'Giulia F.', role: 'Game night organizer, 40+ giocatori', avatar: 'GF', color: 'var(--c-event)',
      quote: '"Ho caricato le house rules che usiamo da 5 anni e ora tutti possono consultarle. Non ci sono più dispute durante il gioco."' },
    { name: 'Stefano C.', role: 'Boardgame café owner', avatar: 'SC', color: 'var(--c-player)',
      quote: '"Il catalogo condiviso è una svolta. I clienti scansionano il QR, cercano il gioco e trovano subito le regole. Risparmio un\'ora al giorno."' },
  ];
  return (
    <section style={{ padding: '80px 0', background: 'var(--bg-muted)' }}>
      <div className="pub-wrap">
        <p className="section-label" style={{ marginTop: 0 }}>Testimonianze</p>
        <h2 className="pub-section-title">Chi gioca con MeepleAI</h2>
        <div className="test-grid">
          {tests.map(t => (
            <article key={t.name} className="card" style={{ padding: '28px 24px', display: 'flex', flexDirection: 'column', gap: 16 }}>
              <p style={{ fontStyle: 'italic', color: 'var(--text-sec)', lineHeight: 'var(--lh-relax)', margin: 0, fontSize: 'var(--fs-md)', flex: 1 }}>
                {t.quote}
              </p>
              <div style={{ display: 'flex', alignItems: 'center', gap: 12, borderTop: '1px solid var(--border)', paddingTop: 16 }}>
                <div style={{ width: 40, height: 40, borderRadius: '50%', background: `hsl(${t.color})`, color: '#fff',
                  display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 700, fontSize: 13, flexShrink: 0 }}>
                  {t.avatar}
                </div>
                <div>
                  <div style={{ fontWeight: 700, fontSize: 'var(--fs-sm)' }}>{t.name}</div>
                  <div style={{ color: 'var(--text-muted)', fontSize: 'var(--fs-xs)' }}>{t.role}</div>
                </div>
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}

function FinalCTA() {
  return (
    <section style={{ padding: '100px 24px', textAlign: 'center', background: 'var(--bg)' }}>
      <div style={{ maxWidth: 560, margin: '0 auto' }}>
        <h2 style={{ fontSize: 'clamp(28px, 4vw, 44px)', marginBottom: 16, lineHeight: 1.1 }}>
          Pronti a giocare
          {' '}<span className="mark">meglio?</span>
        </h2>
        <p style={{ color: 'var(--text-sec)', fontSize: 'var(--fs-lg)', marginBottom: 32, lineHeight: 'var(--lh-body)' }}>
          Inizia gratis, nessuna carta di credito richiesta. Setup in meno di 2 minuti.
        </p>
        <a href="auth-flow.html" className="btn primary e-game" style={{ fontSize: 'var(--fs-md)', padding: '12px 28px' }}>
          Crea il tuo account →
        </a>
      </div>
    </section>
  );
}

function LandingPage({ onNav }) {
  return (
    <>
      <HeroSection onNav={onNav} />
      <StatsRow />
      <FeatureGrid />
      <TestimonialSection />
      <FinalCTA />
    </>
  );
}

// ─── PRICING ────────────────────────────────────────────────────────────────
function PricingCard({ tier }) {
  const isHighlight = tier.highlight;
  return (
    <div className={`card pricing-card ${isHighlight ? 'pricing-highlight' : ''}`}
         style={isHighlight ? { transform: 'scale(1.05)', border: '2px solid hsl(var(--c-game))', boxShadow: 'var(--shadow-lg)', position: 'relative' } : { position: 'relative' }}>
      {isHighlight && (
        <div style={{ position: 'absolute', top: -14, left: '50%', transform: 'translateX(-50%)',
          background: 'hsl(var(--c-game))', color: '#fff', padding: '4px 16px',
          borderRadius: 'var(--r-pill)', fontSize: 'var(--fs-xs)', fontWeight: 700, whiteSpace: 'nowrap', letterSpacing: '0.04em' }}>
          ★ Più popolare
        </div>
      )}
      <div style={{ padding: '32px 28px' }}>
        <div className="e-chip" style={{ marginBottom: 12, '--e': isHighlight ? 'var(--c-game)' : 'var(--c-player)' }}>{tier.name}</div>
        <div style={{ marginBottom: 8 }}>
          <span style={{ fontSize: 42, fontWeight: 800, fontFamily: 'var(--f-display)', letterSpacing: '-0.02em' }}>{tier.price}</span>
          {tier.period && <span style={{ color: 'var(--text-muted)', fontSize: 'var(--fs-sm)' }}>{tier.period}</span>}
        </div>
        <p style={{ color: 'var(--text-sec)', fontSize: 'var(--fs-sm)', marginBottom: 24, lineHeight: 'var(--lh-body)', marginTop: 4 }}>{tier.desc}</p>
        <a href="auth-flow.html"
           className={`btn ${isHighlight ? 'primary e-game' : 'ghost'}`}
           style={{ width: '100%', justifyContent: 'center', padding: '10px 0', marginBottom: 24 }}>
          {tier.cta}
        </a>
        <ul style={{ listStyle: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: 10 }}>
          {tier.features.map(f => (
            <li key={f} style={{ display: 'flex', alignItems: 'flex-start', gap: 8, fontSize: 'var(--fs-sm)', color: 'var(--text-sec)' }}>
              <span style={{ color: 'hsl(var(--c-toolkit))', fontWeight: 700, flexShrink: 0, marginTop: 1 }}>✓</span>
              {f}
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}

function PricingMatrix() {
  const [open, setOpen] = React.useState(false);
  const rows = [
    ['Giochi salvati', '2', '∞', '∞'],
    ['Chat / mese', '10', '∞', '∞'],
    ['RAG hybrid search', '—', '✓', '✓'],
    ['Multi-agent', '—', '✓', '✓'],
    ['Session mode', '✓', '✓', '✓'],
    ['Game nights', '—', '✓', '✓'],
    ['KB condivisa', '—', '—', '✓'],
    ['Account team', '1', '1', '5'],
    ['Admin panel', '—', '—', '✓'],
    ['Support', 'Community', 'Priority', 'Dedicato'],
  ];
  return (
    <div className="pub-wrap" style={{ marginTop: 48 }}>
      <button onClick={() => setOpen(!open)}
        style={{ display: 'flex', alignItems: 'center', gap: 8, background: 'none', border: 'none',
          color: 'var(--text-sec)', cursor: 'pointer', fontFamily: 'var(--f-display)',
          fontSize: 'var(--fs-sm)', fontWeight: 600, padding: 0, marginBottom: open ? 20 : 0 }}>
        <span style={{ transition: 'transform 0.2s', transform: open ? 'rotate(90deg)' : 'none' }}>▶</span>
        Confronto completo funzionalità
      </button>
      {open && (
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
            <thead>
              <tr>
                {['Funzionalità', 'Free', 'Pro', 'Team'].map((h, i) => (
                  <th key={h} style={{ padding: '10px 16px', textAlign: i === 0 ? 'left' : 'center',
                    borderBottom: '2px solid var(--border)', fontFamily: 'var(--f-display)',
                    color: i === 2 ? 'hsl(var(--c-game))' : 'var(--text)', fontWeight: 700 }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map(([feat, ...vals], ri) => (
                <tr key={feat} style={{ background: ri % 2 ? 'var(--bg-muted)' : 'transparent' }}>
                  <td style={{ padding: '9px 16px', color: 'var(--text-sec)' }}>{feat}</td>
                  {vals.map((v, vi) => (
                    <td key={vi} style={{ padding: '9px 16px', textAlign: 'center',
                      color: v === '✓' ? 'hsl(var(--c-toolkit))' : v === '—' ? 'var(--text-muted)' : 'var(--text)',
                      fontWeight: v === '✓' || v === '—' ? 700 : 400 }}>{v}</td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function PricingPage() {
  const tiers = [
    { name: 'Free', price: '€0', period: '', cta: 'Inizia gratis', desc: 'Perfetto per esplorare MeepleAI con i tuoi giochi preferiti.',
      features: ['2 giochi salvati', '10 chat al mese', 'Session mode base', 'Accesso community catalog', 'Support community'] },
    { name: 'Pro', price: '€9', period: '/mese', cta: 'Scegli Pro', desc: 'Per il boardgamer serio che gioca spesso e vuole il massimo.', highlight: true,
      features: ['Giochi illimitati', 'Chat illimitate', 'RAG hybrid search', 'Multi-agent completo', 'Game nights & diary', 'Priority support'] },
    { name: 'Team', price: '€29', period: '/mese', cta: 'Contattaci', desc: 'Per boardgame café, club e gruppi organizzati.',
      features: ['5 account inclusi', 'KB condivisa del team', 'Admin panel', 'Game nights multi-account', 'Dedicated support', 'Early access funzionalità'] },
  ];
  return (
    <section style={{ padding: '80px 0 100px' }}>
      <div className="pub-wrap" style={{ textAlign: 'center', marginBottom: 60 }}>
        <p className="section-label" style={{ marginTop: 0 }}>Piani</p>
        <h1 className="pub-section-title" style={{ fontSize: 'clamp(32px, 5vw, 52px)' }}>Semplice, trasparente,<br /><span className="mark">board-game friendly</span></h1>
        <p style={{ color: 'var(--text-sec)', fontSize: 'var(--fs-lg)', maxWidth: 480, margin: '16px auto 0' }}>Nessun contratto. Cancella quando vuoi. Il piano Free rimane gratuito per sempre.</p>
      </div>
      <div className="pub-wrap pricing-grid">
        {tiers.map(t => <PricingCard key={t.name} tier={t} />)}
      </div>
      <PricingMatrix />
    </section>
  );
}

// ─── ABOUT ──────────────────────────────────────────────────────────────────
function TeamMember({ member }) {
  return (
    <article className="card" style={{ padding: '28px 24px', display: 'flex', flexDirection: 'column', gap: 12 }}>
      <div style={{ width: 64, height: 64, borderRadius: '50%',
        background: `linear-gradient(135deg, hsl(var(--c-game)), hsl(var(--c-event)))`,
        color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontWeight: 800, fontSize: 20, fontFamily: 'var(--f-display)' }}>
        {member.initials}
      </div>
      <div>
        <div style={{ fontWeight: 700, fontFamily: 'var(--f-display)', fontSize: 'var(--fs-md)' }}>{member.name}</div>
        <div className="e-chip e-game" style={{ marginTop: 4 }}>{member.role}</div>
      </div>
      <p style={{ color: 'var(--text-sec)', fontSize: 'var(--fs-sm)', lineHeight: 'var(--lh-relax)', margin: 0 }}>{member.bio}</p>
    </article>
  );
}

function AboutTimeline() {
  const milestones = [
    { year: '2023', label: 'L\'idea', desc: 'Tre boardgamer frustrati da manuali di 80 pagine decidono di costruire qualcosa.' },
    { year: '2024 Q1', label: 'Alpha privata', desc: 'Primi 50 tester, 200 giochi caricati, RAG funzionante. Il feedback è entusiasta.' },
    { year: '2024 Q4', label: 'Beta pubblica', desc: '1.000 utenti, multi-agent system completato, prime game nights organizzate.' },
    { year: '2026', label: 'Public launch', desc: 'MeepleAI è disponibile per tutti. Free forever plan, Pro e Team.' },
  ];
  return (
    <div style={{ position: 'relative', padding: '0 0 0 32px', marginTop: 32 }}>
      <div style={{ position: 'absolute', left: 10, top: 0, bottom: 0, width: 2, background: 'var(--border)' }} />
      {milestones.map((m, i) => (
        <div key={m.year} style={{ position: 'relative', marginBottom: 36 }}>
          <div style={{ position: 'absolute', left: -27, top: 4, width: 14, height: 14, borderRadius: '50%',
            background: `hsl(var(--c-game))`, border: '3px solid var(--bg)' }} />
          <div style={{ fontFamily: 'var(--f-mono)', fontSize: 'var(--fs-xs)', color: 'hsl(var(--c-game))', fontWeight: 700, marginBottom: 4 }}>{m.year}</div>
          <div style={{ fontWeight: 700, fontSize: 'var(--fs-md)', fontFamily: 'var(--f-display)', marginBottom: 6 }}>{m.label}</div>
          <p style={{ color: 'var(--text-sec)', fontSize: 'var(--fs-sm)', margin: 0, lineHeight: 'var(--lh-relax)' }}>{m.desc}</p>
        </div>
      ))}
    </div>
  );
}

function AboutPage() {
  const team = [
    { initials: 'AL', name: 'Alessandro L.', role: 'Co-founder & CEO', bio: 'Boardgamer da 20 anni. Ha trovato la stessa regola ambigua 47 volte. Ingegnere software con passato in NLP.' },
    { initials: 'SF', name: 'Sara F.', role: 'Co-founder & CTO', bio: 'Architetta di sistemi RAG. Ricercatrice AI prima di MeepleAI. Il suo gioco preferito è Twilight Imperium.' },
    { initials: 'MR', name: 'Marco R.', role: 'Co-founder & Design', bio: 'UX designer ossessionato dalla semplicità. Porta il "game feel" nell\'interfaccia digitale.' },
    { initials: 'EB', name: 'Elena B.', role: 'Head of Community', bio: 'Ha organizzato 300+ serate di gioco. Conosce ogni game night host in Italia.' },
  ];
  return (
    <>
      <section style={{ padding: '80px 0 40px' }}>
        <div className="pub-wrap">
          <p className="section-label" style={{ marginTop: 0 }}>La nostra storia</p>
          <h1 style={{ fontSize: 'clamp(32px, 5vw, 52px)', marginBottom: 24, lineHeight: 1.05 }}>
            Perché <span className="mark">MeepleAI</span>
          </h1>
          <div style={{ maxWidth: 680, display: 'flex', flexDirection: 'column', gap: 16 }}>
            <p style={{ color: 'var(--text-sec)', fontSize: 'var(--fs-lg)', lineHeight: 'var(--lh-relax)', margin: 0 }}>
              Tutto è iniziato con una domanda banale durante una partita a Spirit Island: "Ma quando attiva esattamente questo potere?"
              Quarantadue minuti e venti pagine di manuale dopo, avevamo ancora dubbi. Lì abbiamo capito che c'era un problema da risolvere.
            </p>
            <p style={{ color: 'var(--text-sec)', fontSize: 'var(--fs-md)', lineHeight: 'var(--lh-relax)', margin: 0 }}>
              MeepleAI nasce dall'idea che la tecnologia dovrebbe togliersi di mezzo e lasciare spazio al gioco.
              Niente più interruzioni, niente più dispute. Solo domande, risposte precise, e la serata che continua.
            </p>
            <p style={{ color: 'var(--text-sec)', fontSize: 'var(--fs-md)', lineHeight: 'var(--lh-relax)', margin: 0 }}>
              La nostra missione è rendere ogni boardgamer più informato, ogni serata più fluida, ogni partita più memorabile.
              Con AI che capisce davvero i giochi — non solo le parole, ma il contesto, le eccezioni, le varianti.
            </p>
          </div>
        </div>
      </section>
      <section style={{ padding: '40px 0 80px' }}>
        <div className="pub-wrap about-grid">
          <div>
            <p className="section-label" style={{ marginTop: 0 }}>Timeline</p>
            <h2 style={{ fontSize: 'var(--fs-2xl)', marginBottom: 8 }}>Come siamo arrivati qui</h2>
            <AboutTimeline />
          </div>
          <div>
            <p className="section-label" style={{ marginTop: 0 }}>Team</p>
            <h2 style={{ fontSize: 'var(--fs-2xl)', marginBottom: 20 }}>Chi costruisce MeepleAI</h2>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
              {team.map(m => <TeamMember key={m.name} member={m} />)}
            </div>
          </div>
        </div>
      </section>
    </>
  );
}

// ─── TERMS / PRIVACY ────────────────────────────────────────────────────────
function TermsPrivacyPage() {
  const [tab, setTab] = React.useState('terms');

  const termsContent = [
    { id: 'accettazione', title: '1. Accettazione dei termini', body: `Utilizzando MeepleAI accetti i presenti Termini di Servizio. Se non accetti, non puoi utilizzare il servizio. I termini possono essere aggiornati; ti notificheremo via email o notifica in-app.` },
    { id: 'account', title: '2. Account e sicurezza', body: `Sei responsabile della sicurezza del tuo account. Usa una password robusta e non condividere le credenziali. In caso di accesso non autorizzato, contattaci immediatamente all'indirizzo security@meepleai.it.` },
    { id: 'contenuti', title: '3. Contenuti e licenze', body: `I contenuti che carichi (PDF di regolamenti, house rules) rimangono di tua proprietà. Concedi a MeepleAI una licenza limitata per indicizzarli e fornire le risposte AI. Non caricare materiale copyrighted senza autorizzazione.` },
    { id: 'limitazioni', title: '4. Limitazioni di responsabilità', body: `MeepleAI fornisce risposte AI con alta precisione ma non garantisce l'accuratezza assoluta. Le risposte AI non sostituiscono la lettura del regolamento ufficiale. Non siamo responsabili per dispute di gioco basate su risposte AI.` },
    { id: 'abbonamenti', title: '5. Abbonamenti e pagamenti', body: `Il piano Free è gratuito senza limiti di tempo. I piani a pagamento si rinnovano automaticamente ogni mese. Puoi disdire in qualsiasi momento; l'accesso Pro rimane attivo fino alla fine del periodo pagato.` },
    { id: 'risoluzione', title: '6. Risoluzione del contratto', body: `Puoi eliminare il tuo account in qualsiasi momento dalle impostazioni. Ci riserviamo il diritto di sospendere account che violano questi termini. I dati sono eliminati entro 30 giorni dalla cancellazione.` },
  ];

  const privacyContent = [
    { id: 'raccolta', title: '1. Dati che raccogliamo', body: `Raccogliamo: email e nome all'iscrizione, i giochi che aggiungi, le chat con il sistema AI, le sessioni di gioco registrate. Non raccogliamo dati di localizzazione o comportamentali al di fuori dell'app.` },
    { id: 'utilizzo', title: '2. Come usiamo i dati', body: `I tuoi dati servono per: fornire il servizio AI, migliorare la qualità delle risposte (in forma anonimizzata), inviarti comunicazioni di servizio. Non vendiamo mai i tuoi dati a terze parti.` },
    { id: 'cookie', title: '3. Cookie e tracking', body: `Usiamo cookie essenziali per l'autenticazione e le preferenze. Non usiamo cookie di tracking pubblicitario. Puoi controllare i cookie dalle impostazioni del browser.` },
    { id: 'diritti', title: '4. I tuoi diritti (GDPR)', body: `Hai diritto di: accedere ai tuoi dati, correggerli, cancellarli, portarli altrove (data portability). Per esercitare questi diritti: privacy@meepleai.it. Risponderemo entro 30 giorni.` },
    { id: 'retention', title: '5. Conservazione dei dati', body: `Conserviamo i dati attivi finché il tuo account è aperto. Chat e sessioni vengono archiviate per 2 anni per permetterti di consultarle. Dopo la cancellazione dell'account, tutti i dati sono eliminati entro 30 giorni.` },
    { id: 'contatti', title: '6. Contatti privacy', body: `Data Protection Officer: privacy@meepleai.it\nSede legale: MeepleAI S.r.l., Via dei Giochi 1, Milano MI 20100\nAutorità di controllo: Garante Privacy italiano (www.garanteprivacy.it)` },
  ];

  const content = tab === 'terms' ? termsContent : privacyContent;

  return (
    <section style={{ padding: '60px 0 100px' }}>
      <div className="pub-wrap terms-layout">
        {/* sidebar */}
        <aside className="terms-sidebar">
          {/* tab toggle */}
          <div style={{ display: 'flex', background: 'var(--bg-muted)', borderRadius: 'var(--r-md)', padding: 3, marginBottom: 24 }}>
            {['terms', 'privacy'].map(t => (
              <button key={t} onClick={() => setTab(t)}
                style={{ flex: 1, padding: '7px 12px', borderRadius: 'calc(var(--r-md) - 2px)', border: 'none', cursor: 'pointer',
                  background: tab === t ? 'var(--bg-card)' : 'transparent',
                  color: tab === t ? 'var(--text)' : 'var(--text-muted)',
                  fontFamily: 'var(--f-display)', fontWeight: 600, fontSize: 'var(--fs-sm)',
                  boxShadow: tab === t ? 'var(--shadow-xs)' : 'none', transition: 'all 0.2s' }}>
                {t === 'terms' ? 'Termini' : 'Privacy'}
              </button>
            ))}
          </div>
          <nav style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            {content.map(s => (
              <a key={s.id} href={`#${s.id}`}
                style={{ padding: '6px 10px', borderRadius: 'var(--r-sm)', fontSize: 'var(--fs-sm)',
                  color: 'var(--text-sec)', fontWeight: 500, transition: 'all 0.15s' }}
                onMouseEnter={e => e.currentTarget.style.color = 'var(--text)'}
                onMouseLeave={e => e.currentTarget.style.color = 'var(--text-sec)'}>
                {s.title}
              </a>
            ))}
          </nav>
          <div style={{ marginTop: 24, padding: '12px', background: 'var(--bg-muted)', borderRadius: 'var(--r-md)', fontSize: 'var(--fs-xs)', color: 'var(--text-muted)', lineHeight: 1.6 }}>
            Ultimo aggiornamento:<br /><strong style={{ color: 'var(--text-sec)' }}>Aprile 2026</strong>
          </div>
        </aside>
        {/* content */}
        <main style={{ maxWidth: 720 }}>
          <h1 style={{ fontSize: 'clamp(28px, 4vw, 40px)', marginBottom: 8 }}>
            {tab === 'terms' ? 'Termini di Servizio' : 'Privacy Policy'}
          </h1>
          <p style={{ color: 'var(--text-muted)', fontFamily: 'var(--f-mono)', fontSize: 'var(--fs-xs)', marginBottom: 40 }}>
            MeepleAI S.r.l. — Versione 1.2 — Aprile 2026
          </p>
          {content.map(s => (
            <section key={s.id} id={s.id} style={{ marginBottom: 40, scrollMarginTop: 80 }}>
              <h2 style={{ fontSize: 'var(--fs-xl)', marginBottom: 12, color: 'var(--text)' }}>{s.title}</h2>
              <p style={{ color: 'var(--text-sec)', lineHeight: 'var(--lh-relax)', margin: 0, whiteSpace: 'pre-line', fontSize: 'var(--fs-md)' }}>{s.body}</p>
            </section>
          ))}
          <div style={{ padding: '20px', background: 'var(--bg-muted)', borderRadius: 'var(--r-lg)', borderLeft: '3px solid hsl(var(--c-game))', fontSize: 'var(--fs-sm)', color: 'var(--text-sec)' }}>
            Domande? Scrivici a <a href="mailto:legal@meepleai.it" style={{ color: 'hsl(var(--c-game))' }}>legal@meepleai.it</a>
          </div>
        </main>
      </div>
    </section>
  );
}

// ─── CONTACT ────────────────────────────────────────────────────────────────
function ContactPage() {
  const [form, setForm] = React.useState({ nome: '', email: '', tipo: 'general', messaggio: '' });
  const [sent, setSent] = React.useState(false);

  function handleSubmit(e) {
    e.preventDefault();
    setSent(true);
  }

  const tipi = [
    { v: 'general', l: 'Domanda generale' },
    { v: 'bug', l: 'Segnala un bug' },
    { v: 'partnership', l: 'Partnership' },
    { v: 'press', l: 'Stampa & media' },
  ];

  return (
    <section style={{ padding: '80px 0 100px' }}>
      <div className="pub-wrap contact-layout">
        {/* form */}
        <div>
          <p className="section-label" style={{ marginTop: 0 }}>Contatti</p>
          <h1 style={{ fontSize: 'clamp(28px, 4vw, 44px)', marginBottom: 8 }}>Parliamo di <span className="mark">giochi</span></h1>
          <p style={{ color: 'var(--text-sec)', fontSize: 'var(--fs-md)', marginBottom: 32, lineHeight: 'var(--lh-body)' }}>
            Hai domande, idee, vuoi collaborare? Siamo veri boardgamer — risponderemo entro 24 ore.
          </p>
          {sent ? (
            <div style={{ padding: '32px', background: 'hsl(var(--c-toolkit) / 0.1)', borderRadius: 'var(--r-xl)',
              border: '1px solid hsl(var(--c-toolkit) / 0.3)', textAlign: 'center' }}>
              <div style={{ fontSize: 48, marginBottom: 12 }}>🎲</div>
              <h3 style={{ margin: '0 0 8px', color: 'hsl(var(--c-toolkit))' }}>Messaggio inviato!</h3>
              <p style={{ color: 'var(--text-sec)', margin: 0 }}>Ti risponderemo entro 24 ore all'indirizzo <strong>{form.email}</strong></p>
              <button className="btn ghost" style={{ marginTop: 20 }} onClick={() => { setSent(false); setForm({ nome: '', email: '', tipo: 'general', messaggio: '' }); }}>
                Invia un altro messaggio
              </button>
            </div>
          ) : (
            <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                <label style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                  <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 600, color: 'var(--text-sec)' }}>Nome</span>
                  <input required value={form.nome} onChange={e => setForm({...form, nome: e.target.value})}
                    placeholder="Il tuo nome" className="form-input" />
                </label>
                <label style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                  <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 600, color: 'var(--text-sec)' }}>Email</span>
                  <input required type="email" value={form.email} onChange={e => setForm({...form, email: e.target.value})}
                    placeholder="tu@email.it" className="form-input" />
                </label>
              </div>
              <label style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 600, color: 'var(--text-sec)' }}>Tipo di richiesta</span>
                <select value={form.tipo} onChange={e => setForm({...form, tipo: e.target.value})} className="form-input">
                  {tipi.map(t => <option key={t.v} value={t.v}>{t.l}</option>)}
                </select>
              </label>
              <label style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 600, color: 'var(--text-sec)' }}>Messaggio</span>
                <textarea required rows={5} value={form.messaggio} onChange={e => setForm({...form, messaggio: e.target.value})}
                  placeholder="Raccontaci..." className="form-input" style={{ resize: 'vertical', minHeight: 120 }} />
              </label>
              <button type="submit" className="btn primary e-game" style={{ alignSelf: 'flex-start', padding: '10px 24px' }}>
                Invia messaggio →
              </button>
            </form>
          )}
        </div>
        {/* sidebar */}
        <aside style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
          <div className="card" style={{ padding: '24px 20px' }}>
            <h3 style={{ fontSize: 'var(--fs-md)', marginBottom: 16 }}>Info contatto</h3>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
              <div style={{ display: 'flex', gap: 12, alignItems: 'flex-start' }}>
                <span style={{ fontSize: 18, marginTop: 1 }}>✉️</span>
                <div>
                  <div style={{ fontWeight: 600, fontSize: 'var(--fs-sm)', marginBottom: 2 }}>Email diretta</div>
                  <a href="mailto:ciao@meepleai.it" style={{ color: 'hsl(var(--c-game))', fontSize: 'var(--fs-sm)' }}>ciao@meepleai.it</a>
                </div>
              </div>
              <div style={{ display: 'flex', gap: 12, alignItems: 'flex-start' }}>
                <span style={{ fontSize: 18, marginTop: 1 }}>⏱️</span>
                <div>
                  <div style={{ fontWeight: 600, fontSize: 'var(--fs-sm)', marginBottom: 2 }}>Tempo di risposta</div>
                  <div style={{ color: 'var(--text-sec)', fontSize: 'var(--fs-sm)' }}>Di solito entro 24 ore</div>
                </div>
              </div>
            </div>
          </div>
          <div className="card" style={{ padding: '24px 20px' }}>
            <h3 style={{ fontSize: 'var(--fs-md)', marginBottom: 16 }}>Seguici</h3>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              {[
                { name: 'Discord', color: '#5865F2', desc: 'Community italiana' },
                { name: 'Twitter / X', color: '#1DA1F2', desc: '@meepleai_it' },
                { name: 'GitHub', color: 'var(--text)', desc: 'github.com/meepleai' },
              ].map(s => (
                <a key={s.name} href="#" style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '8px 10px',
                  borderRadius: 'var(--r-md)', transition: 'background 0.15s' }}
                  onMouseEnter={e => e.currentTarget.style.background = 'var(--bg-hover)'}
                  onMouseLeave={e => e.currentTarget.style.background = 'transparent'}>
                  <div style={{ width: 32, height: 32, borderRadius: 'var(--r-sm)', background: s.color,
                    display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#fff',
                    fontSize: 14, fontWeight: 700, flexShrink: 0 }}>
                    {s.name[0]}
                  </div>
                  <div>
                    <div style={{ fontWeight: 600, fontSize: 'var(--fs-sm)' }}>{s.name}</div>
                    <div style={{ color: 'var(--text-muted)', fontSize: 'var(--fs-xs)' }}>{s.desc}</div>
                  </div>
                </a>
              ))}
            </div>
          </div>
        </aside>
      </div>
    </section>
  );
}

// ─── FOOTER ─────────────────────────────────────────────────────────────────
function Footer({ onNav }) {
  const cols = [
    { title: 'Prodotto', links: [
      { l: 'Funzionalità', page: 'landing' }, { l: 'Prezzi', page: 'pricing' },
      { l: 'RAG Search', page: 'landing' }, { l: 'Multi-agent', page: 'landing' }, { l: 'Game Nights', page: 'landing' }
    ]},
    { title: 'Azienda', links: [
      { l: 'Chi siamo', page: 'about' }, { l: 'Blog', page: null }, { l: 'Lavora con noi', page: null },
    ]},
    { title: 'Legale', links: [
      { l: 'Termini', page: 'terms' }, { l: 'Privacy', page: 'terms' }, { l: 'Cookie', page: 'terms' },
    ]},
    { title: 'Community', links: [
      { l: 'Discord', page: null }, { l: 'Twitter / X', page: null }, { l: 'GitHub', page: null },
    ]},
  ];
  return (
    <footer style={{ background: 'var(--bg-sunken)', borderTop: '1px solid var(--border)', padding: '48px 0 32px' }}>
      <div className="pub-wrap footer-grid">
        {cols.map(c => (
          <div key={c.title}>
            <div style={{ fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 'var(--fs-sm)', marginBottom: 14 }}>{c.title}</div>
            <ul style={{ listStyle: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: 8 }}>
              {c.links.map(lk => (
                <li key={lk.l}>
                  <a href="#" onClick={e => { e.preventDefault(); if (lk.page) onNav(lk.page); }}
                    style={{ color: 'var(--text-muted)', fontSize: 'var(--fs-sm)', transition: 'color 0.15s' }}
                    onMouseEnter={e => e.currentTarget.style.color = 'var(--text-sec)'}
                    onMouseLeave={e => e.currentTarget.style.color = 'var(--text-muted)'}>
                    {lk.l}
                  </a>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
      <div className="pub-wrap" style={{ marginTop: 32, paddingTop: 24, borderTop: '1px solid var(--border-light)',
        display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <div className="brand-mark" style={{ width: 24, height: 24, borderRadius: 6,
            background: 'linear-gradient(135deg, hsl(var(--c-game)), hsl(var(--c-event)))',
            color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontWeight: 800, fontSize: 12, flexShrink: 0 }}>M</div>
          <span style={{ fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 'var(--fs-sm)' }}>MeepleAI</span>
          <span style={{ color: 'var(--text-muted)', fontSize: 'var(--fs-xs)' }}>© 2026 MeepleAI S.r.l.</span>
        </div>
        <span style={{ color: 'var(--text-muted)', fontSize: 'var(--fs-xs)', fontFamily: 'var(--f-mono)' }}>
          Made with 🎲 in Milano
        </span>
      </div>
    </footer>
  );
}

// ─── APP ROOT ────────────────────────────────────────────────────────────────
function App() {
  const [page, setPage] = React.useState(() => {
    try { return localStorage.getItem('mai-public-page') || 'landing'; } catch { return 'landing'; }
  });
  const [tweaksOpen, setTweaksOpen] = React.useState(false);
  const [tweaks, setTweaks] = React.useState(() => {
    try { return JSON.parse(localStorage.getItem('mai-public-tweaks') || 'null') || null; } catch { return null; }
  });

  const TWEAK_DEFAULTS = /*EDITMODE-BEGIN*/{
    "heroSize": 72,
    "accentColor": "game",
    "showStats": true,
    "roundedCards": true
  }/*EDITMODE-END*/;

  const tw = { ...TWEAK_DEFAULTS, ...(tweaks || {}) };

  function navigate(p) {
    setPage(p);
    try { localStorage.setItem('mai-public-page', p); } catch {}
    window.scrollTo(0, 0);
  }

  React.useEffect(() => {
    window.addEventListener('message', e => {
      if (e.data?.type === '__activate_edit_mode') setTweaksOpen(true);
      if (e.data?.type === '__deactivate_edit_mode') setTweaksOpen(false);
    });
    window.parent.postMessage({ type: '__edit_mode_available' }, '*');
  }, []);

  function applyTweak(key, val) {
    const next = { ...(tweaks || {}), [key]: val };
    setTweaks(next);
    try { localStorage.setItem('mai-public-tweaks', JSON.stringify(next)); } catch {}
    window.parent.postMessage({ type: '__edit_mode_set_keys', edits: { [key]: val } }, '*');
  }

  return (
    <>
      <PublicNav currentPage={page} onNav={navigate} />
      <main>
        {page === 'landing' && <LandingPage onNav={navigate} />}
        {page === 'pricing' && <PricingPage />}
        {page === 'about' && <AboutPage />}
        {page === 'terms' && <TermsPrivacyPage />}
        {page === 'contact' && <ContactPage />}
      </main>
      <Footer onNav={navigate} />

      {/* TWEAKS PANEL */}
      {tweaksOpen && (
        <div id="tweaks-panel" style={{ display: 'block', position: 'fixed', bottom: 24, right: 24, zIndex: 9999,
          background: 'var(--glass-bg)', backdropFilter: 'blur(16px)', border: '1px solid var(--border)',
          borderRadius: 'var(--r-xl)', boxShadow: 'var(--shadow-lg)', padding: '16px 18px', minWidth: 220,
          fontFamily: 'var(--f-display)', fontSize: 13, color: 'var(--text)' }}>
          <div style={{ fontWeight: 700, marginBottom: 14, borderBottom: '1px solid var(--border)', paddingBottom: 10 }}>Tweaks</div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <label style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <span style={{ fontSize: 11, color: 'var(--text-muted)', fontWeight: 600 }}>Dimensione Hero H1 ({tw.heroSize}px)</span>
              <input type="range" min={40} max={90} value={tw.heroSize} onChange={e => applyTweak('heroSize', +e.target.value)} style={{ width: '100%' }} />
            </label>
            <label style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <span style={{ fontSize: 11, color: 'var(--text-muted)', fontWeight: 600 }}>Colore accento</span>
              <select value={tw.accentColor} onChange={e => applyTweak('accentColor', e.target.value)}
                style={{ border: '1px solid var(--border)', borderRadius: 'var(--r-sm)', padding: '4px 8px',
                  background: 'var(--bg-muted)', color: 'var(--text)', fontSize: 12 }}>
                <option value="game">Arancio (game)</option>
                <option value="event">Rosa (event)</option>
                <option value="player">Viola (player)</option>
              </select>
            </label>
            <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer' }}>
              <input type="checkbox" checked={tw.showStats} onChange={e => applyTweak('showStats', e.target.checked)} />
              <span style={{ fontSize: 12 }}>Mostra stats row</span>
            </label>
            <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer' }}>
              <input type="checkbox" checked={tw.roundedCards} onChange={e => applyTweak('roundedCards', e.target.checked)} />
              <span style={{ fontSize: 12 }}>Card arrotondate</span>
            </label>
          </div>
        </div>
      )}
    </>
  );
}

const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(<App />);

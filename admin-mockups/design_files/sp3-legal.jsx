/* MeepleAI SP3 missing — #3 · Legal pages template
   Route: /terms (template riusabile per /privacy /cookies)
   Componenti v2 nuovi: LegalPageShell, LegalTOC
   isMobile: prop esplicita dalla frame + scroll-spy basato su elementi nel DOM
*/

const { useState, useEffect, useRef } = React;

// ── PLACEHOLDER CONTENT ────────────────────────────────────────────────────
const TERMS_DOC = {
  type: 'terms',
  docTitle: 'Termini di Servizio',
  updated: '26 aprile 2026',
  version: '2.4.0',
  sections: [
    {
      id: 'accettazione',
      h: 'Accettazione dei termini',
      paragraphs: [
        'Accedendo o utilizzando MeepleAI ("il Servizio") accetti integralmente i presenti Termini di Servizio e la nostra Privacy Policy. Se non concordi con uno qualsiasi dei termini qui descritti, non sei autorizzato a utilizzare il Servizio.',
        'L\'accettazione avviene al primo accesso al Servizio o al momento della creazione del tuo account. Continuando a usare MeepleAI dopo eventuali modifiche, confermi l\'accettazione della versione aggiornata dei termini.',
        'Se utilizzi MeepleAI per conto di un\'organizzazione, dichiari di avere l\'autorità necessaria per impegnare tale organizzazione al rispetto dei presenti termini.',
      ],
    },
    {
      id: 'descrizione-servizio',
      h: 'Descrizione del servizio',
      paragraphs: [
        'MeepleAI è un servizio di companion app per giochi da tavolo che permette agli utenti di costruire una libreria personale, indicizzare regolamenti tramite agenti AI, gestire sessioni di gioco e condividere toolkit con la community.',
        'Il Servizio include funzionalità di intelligenza artificiale generativa basate su modelli di linguaggio di terze parti. Le risposte degli agenti sono indicative e non sostituiscono la consultazione del regolamento ufficiale del gioco. MeepleAI non garantisce l\'accuratezza assoluta delle informazioni fornite dagli agenti.',
        'Il Servizio è offerto in versione free, pro e team. Le caratteristiche di ciascun piano sono descritte sulla pagina /pricing e possono variare nel tempo con preavviso di almeno 30 giorni per gli utenti paganti.',
      ],
    },
    {
      id: 'account-utente',
      h: 'Account utente e responsabilità',
      paragraphs: [
        'Per accedere alla maggior parte delle funzionalità di MeepleAI è necessario creare un account. Sei responsabile della riservatezza delle tue credenziali e di tutte le attività che avvengono sotto il tuo account.',
        'Devi notificarci immediatamente qualsiasi uso non autorizzato del tuo account scrivendo a security@meepleai.app. MeepleAI non è responsabile per perdite causate dall\'uso non autorizzato del tuo account dovuto a tua negligenza nella custodia delle credenziali.',
        'Devi avere almeno 16 anni per creare un account. I minori di 16 anni possono utilizzare il Servizio solo con il consenso di un genitore o tutore legale, fornendo prova del consenso al nostro Data Protection Officer.',
      ],
    },
    {
      id: 'contenuti-utente',
      h: 'Contenuti utente e proprietà intellettuale',
      paragraphs: [
        'Mantieni la piena proprietà di tutti i contenuti che carichi su MeepleAI, inclusi PDF di regolamenti, house rules, toolkit personalizzati e dati delle sessioni di gioco. Concedi a MeepleAI una licenza limitata, non esclusiva e gratuita per elaborare, indicizzare e mostrare tali contenuti al solo scopo di fornirti il Servizio.',
        'È tua responsabilità assicurarti di avere il diritto di caricare il materiale che condividi. Non caricare PDF di regolamenti coperti da copyright se non sei l\'editore o non hai esplicita autorizzazione. MeepleAI rispetta il DMCA e risponde tempestivamente alle richieste di rimozione legittime.',
        'I toolkit pubblicati sulla community vengono distribuiti con licenza CC-BY-SA 4.0 di default, salvo diversa licenza specificata dall\'autore al momento della pubblicazione. Gli agenti e le KB rimangono di proprietà esclusiva del creatore.',
      ],
    },
    {
      id: 'comportamenti-vietati',
      h: 'Comportamenti vietati',
      paragraphs: [
        'È vietato utilizzare MeepleAI per: caricare contenuti illegali, diffamatori, osceni o che violino diritti di terzi; tentare di aggirare le limitazioni del piano sottoscritto; eseguire scraping automatizzato del catalogo community; effettuare attacchi DoS o tentativi di intrusione nei nostri sistemi.',
        'È inoltre vietato pubblicare toolkit o contenuti che incoraggino comportamenti tossici nella community boardgame, harassment di altri utenti o violazioni dei diritti di proprietà intellettuale di editori e autori.',
        'MeepleAI si riserva il diritto di sospendere o terminare account in violazione di queste regole, senza preavviso nei casi più gravi e con notifica via email negli altri casi. La decisione di MeepleAI è insindacabile.',
      ],
    },
    {
      id: 'limitazione-responsabilita',
      h: 'Limitazione di responsabilità',
      paragraphs: [
        'Il Servizio è fornito "così com\'è" senza garanzie di alcun tipo, espresse o implicite. MeepleAI non garantisce che il Servizio sarà ininterrotto, privo di errori o conforme a qualsiasi tua specifica esigenza.',
        'In nessun caso MeepleAI sarà responsabile per danni indiretti, incidentali, speciali, consequenziali o punitivi derivanti dall\'uso o dall\'impossibilità di utilizzare il Servizio. La responsabilità totale di MeepleAI nei tuoi confronti è limitata all\'ammontare pagato negli ultimi 12 mesi, o a 50 euro se utilizzi il piano free.',
        'Le risposte degli agenti AI hanno scopo informativo e non costituiscono consulenza legale, medica, finanziaria o di altro tipo. Le decisioni durante una partita rimangono responsabilità dei giocatori.',
      ],
    },
    {
      id: 'modifiche-termini',
      h: 'Modifiche ai termini',
      paragraphs: [
        'MeepleAI può modificare questi termini in qualsiasi momento. Le modifiche sostanziali saranno notificate via email registrata almeno 30 giorni prima dell\'entrata in vigore. Le modifiche minori (refusi, chiarimenti) entrano in vigore immediatamente con notifica in-app.',
        'La versione corrente dei termini è sempre disponibile su questa pagina con data di aggiornamento e numero di versione semver. Le versioni precedenti sono archiviate e accessibili tramite la nostra pagina /legal/archive.',
        'Se non concordi con le modifiche, hai diritto a chiudere il tuo account entro 30 giorni dalla notifica e ricevere il rimborso pro-rata della parte non goduta dell\'abbonamento attivo.',
      ],
    },
    {
      id: 'legge-applicabile',
      h: 'Legge applicabile e foro competente',
      paragraphs: [
        'I presenti termini sono regolati dalla legge italiana. Qualsiasi controversia derivante dall\'utilizzo del Servizio sarà sottoposta in via esclusiva alla giurisdizione del Tribunale di Milano, fatto salvo il diritto del consumatore di adire il foro della propria residenza ai sensi del Codice del Consumo.',
        'Prima di intraprendere azioni legali, le parti si impegnano a tentare una risoluzione amichevole tramite il nostro DPO (privacy@meepleai.app) per un periodo minimo di 30 giorni dalla prima comunicazione formale.',
        'Se una qualsiasi clausola di questi termini fosse dichiarata invalida o inapplicabile da un\'autorità competente, la restante parte dei termini rimarrà pienamente in vigore.',
      ],
    },
  ],
};

// ── LEGAL TOC — desktop sticky + mobile collapsible ────────────────────────
const LegalTOC = ({ sections, activeId, onClickAnchor, isMobile }) => {
  if (isMobile) {
    return (
      <details
        style={{
          margin: '0 0 24px',
          background: 'var(--bg-card)',
          border: '1px solid var(--border)',
          borderRadius: 12,
          overflow: 'hidden',
        }}
      >
        <summary
          style={{
            cursor: 'pointer',
            padding: '14px 16px',
            display: 'flex',
            alignItems: 'center',
            gap: 10,
            fontFamily: 'var(--f-display)',
            fontWeight: 700,
            fontSize: 14,
            color: 'var(--text)',
            listStyle: 'none',
          }}
        >
          <span aria-hidden="true">📋</span>
          <span>Indice</span>
          <span style={{
            marginLeft: 'auto',
            fontFamily: 'var(--f-mono)', fontSize: 11,
            color: 'var(--text-muted)', fontWeight: 500,
          }}>{sections.length} sezioni</span>
        </summary>
        <nav role="navigation" aria-label="Indice della pagina"
          style={{
            display: 'flex', flexDirection: 'column',
            borderTop: '1px solid var(--border-light)',
            padding: '8px 0',
          }}>
          {sections.map((s, i) => (
            <a
              key={s.id}
              href={`#${s.id}`}
              onClick={e => onClickAnchor(e, s.id)}
              style={{
                padding: '8px 16px',
                fontSize: 13.5, fontFamily: 'var(--f-body)',
                color: 'var(--text-sec)', textDecoration: 'none',
                display: 'flex', gap: 10, alignItems: 'center',
              }}
            >
              <span style={{
                fontFamily: 'var(--f-mono)', fontSize: 11,
                color: 'var(--text-muted)', fontVariantNumeric: 'tabular-nums',
                minWidth: 18,
              }}>{String(i + 1).padStart(2, '0')}</span>
              {s.h}
            </a>
          ))}
        </nav>
      </details>
    );
  }

  return (
    <aside style={{ position: 'sticky', top: 24, alignSelf: 'start', width: 240 }}>
      <div style={{
        fontFamily: 'var(--f-mono)',
        fontSize: 11, fontWeight: 600,
        color: 'var(--text-muted)',
        textTransform: 'uppercase',
        letterSpacing: '0.12em',
        marginBottom: 14,
        paddingLeft: 12,
      }}>
        In questa pagina
      </div>
      <nav role="navigation" aria-label="Indice della pagina"
        style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        {sections.map((s, i) => {
          const isActive = activeId === s.id;
          return (
            <a
              key={s.id}
              href={`#${s.id}`}
              aria-current={isActive ? 'location' : undefined}
              onClick={e => onClickAnchor(e, s.id)}
              style={{
                position: 'relative',
                padding: '8px 12px 8px 14px',
                borderLeft: `2px solid ${isActive ? 'hsl(var(--c-kb))' : 'transparent'}`,
                background: isActive ? 'hsl(var(--c-kb) / 0.06)' : 'transparent',
                color: isActive ? 'var(--text)' : 'var(--text-sec)',
                fontFamily: 'var(--f-body)',
                fontSize: 13.5,
                fontWeight: isActive ? 700 : 500,
                textDecoration: 'none',
                lineHeight: 1.4,
                borderRadius: '0 8px 8px 0',
                transition: 'all 120ms ease',
                display: 'flex', gap: 10, alignItems: 'flex-start',
              }}
            >
              <span style={{
                fontFamily: 'var(--f-mono)', fontSize: 10.5,
                color: isActive ? 'hsl(var(--c-kb))' : 'var(--text-muted)',
                fontVariantNumeric: 'tabular-nums',
                fontWeight: 600,
                marginTop: 2,
                minWidth: 18,
              }}>{String(i + 1).padStart(2, '0')}</span>
              <span>{s.h}</span>
            </a>
          );
        })}
      </nav>
    </aside>
  );
};

// ── LEGAL PAGE SHELL — Hero + Layout ───────────────────────────────────────
const LegalPageShell = ({ doc, isMobile }) => {
  const [activeId, setActiveId] = useState(doc.sections[0]?.id);
  const bodyRef = useRef(null);

  // scroll-spy — observes section H2 elements within the body
  useEffect(() => {
    if (isMobile) return;
    const root = bodyRef.current;
    if (!root) return;
    const sections = doc.sections.map(s => root.querySelector(`#${s.id}`)).filter(Boolean);
    const observer = new IntersectionObserver(
      entries => {
        const visible = entries
          .filter(e => e.isIntersecting)
          .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top);
        if (visible[0]?.target?.id) setActiveId(visible[0].target.id);
      },
      { rootMargin: '-20% 0px -60% 0px', threshold: [0, 0.5, 1] }
    );
    sections.forEach(s => observer.observe(s));
    return () => observer.disconnect();
  }, [doc.sections, isMobile]);

  const handleAnchor = (e, id) => {
    e.preventDefault();
    const el = bodyRef.current?.querySelector(`#${id}`);
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' });
      setActiveId(id);
    }
  };

  return (
    <main style={{ background: 'var(--bg)', minHeight: '100%' }}>
      {/* HERO */}
      <header style={{
        padding: isMobile ? '40px 16px 28px' : '64px 24px 40px',
        borderBottom: '1px solid var(--border)',
        background: `linear-gradient(180deg, hsl(var(--c-kb) / 0.04), transparent 80%)`,
      }}>
        <div style={{ maxWidth: 1180, margin: '0 auto' }}>
          <div style={{
            display: 'inline-flex', alignItems: 'center', gap: 8,
            padding: '4px 10px',
            background: 'hsl(var(--c-kb) / 0.12)',
            color: 'hsl(var(--c-kb))',
            border: '1px solid hsl(var(--c-kb) / 0.25)',
            borderRadius: 999,
            fontFamily: 'var(--f-display)', fontWeight: 600, fontSize: 11,
            letterSpacing: '0.06em', textTransform: 'uppercase',
            marginBottom: 16,
          }}>
            <span aria-hidden="true">📄</span>
            <span>{doc.type === 'terms' ? 'Termini' : doc.type === 'privacy' ? 'Privacy' : 'Cookie'}</span>
          </div>
          <h1 style={{
            fontFamily: 'var(--f-display)',
            fontSize: isMobile ? 30 : 44,
            fontWeight: 700,
            lineHeight: 1.1,
            letterSpacing: '-0.02em',
            margin: '0 0 12px',
            color: 'var(--text)',
            textWrap: 'balance',
          }}>{doc.docTitle}</h1>
          <div style={{
            display: 'flex', flexWrap: 'wrap', gap: 16,
            fontFamily: 'var(--f-mono)', fontSize: 12.5,
            color: 'var(--text-muted)',
          }}>
            <span>Ultimo aggiornamento: <strong style={{ color: 'var(--text-sec)', fontWeight: 600 }}>{doc.updated}</strong></span>
            <span style={{ color: 'var(--border-strong)' }}>·</span>
            <span>Versione <strong style={{ color: 'var(--text-sec)', fontWeight: 600 }}>{doc.version}</strong></span>
          </div>
        </div>
      </header>

      {/* LAYOUT */}
      <div style={{
        maxWidth: 1180, margin: '0 auto',
        padding: isMobile ? '24px 16px 60px' : '40px 24px 80px',
        display: isMobile ? 'block' : 'grid',
        gridTemplateColumns: isMobile ? '1fr' : '240px minmax(0, 1fr)',
        gap: isMobile ? 0 : 56,
        alignItems: 'start',
      }}>
        <LegalTOC
          sections={doc.sections}
          activeId={activeId}
          onClickAnchor={handleAnchor}
          isMobile={isMobile}
        />

        {/* BODY */}
        <article ref={bodyRef} style={{
          maxWidth: '68ch',
          fontFamily: 'var(--f-body)',
          color: 'var(--text)',
          lineHeight: 1.75,
          fontSize: isMobile ? 15 : 16,
        }}>
          {doc.sections.map((s, i) => (
            <section
              key={s.id}
              id={s.id}
              style={{
                marginBottom: isMobile ? 36 : 48,
                scrollMarginTop: 80,
              }}
            >
              <div style={{
                display: 'flex', alignItems: 'baseline', gap: 12,
                marginBottom: 14,
              }}>
                <span style={{
                  fontFamily: 'var(--f-mono)',
                  fontSize: 12, fontWeight: 600,
                  color: 'hsl(var(--c-kb))',
                  fontVariantNumeric: 'tabular-nums',
                  letterSpacing: '0.04em',
                }}>{String(i + 1).padStart(2, '0')}</span>
                <h2 style={{
                  fontFamily: 'var(--f-display)',
                  fontSize: isMobile ? 22 : 26,
                  fontWeight: 700,
                  lineHeight: 1.2,
                  letterSpacing: '-0.015em',
                  margin: 0,
                  color: 'var(--text)',
                  textWrap: 'balance',
                }}>{s.h}</h2>
              </div>
              {s.paragraphs.map((p, pi) => (
                <p key={pi} style={{
                  margin: '0 0 14px',
                  textWrap: 'pretty',
                  color: 'var(--text-sec)',
                }}>{renderInline(p)}</p>
              ))}
            </section>
          ))}

          {/* Footer card */}
          <aside style={{
            marginTop: 16,
            padding: isMobile ? '24px 20px' : '28px 32px',
            background: `linear-gradient(135deg,
              hsl(var(--c-kb) / 0.08),
              hsl(var(--c-toolkit) / 0.06))`,
            border: '1px solid hsl(var(--c-kb) / 0.22)',
            borderRadius: 16,
          }}>
            <div style={{
              fontFamily: 'var(--f-display)',
              fontWeight: 700,
              fontSize: isMobile ? 17 : 20,
              color: 'var(--text)',
              marginBottom: 6,
              letterSpacing: '-0.01em',
            }}>Hai domande sui nostri termini?</div>
            <p style={{
              margin: '0 0 16px',
              fontFamily: 'var(--f-body)', fontSize: 14.5, lineHeight: 1.6,
              color: 'var(--text-sec)',
            }}>
              Per qualsiasi richiesta legale o sulla privacy, contatta direttamente il nostro Data Protection Officer.
              Risponderemo entro 5 giorni lavorativi.
            </p>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12, alignItems: 'center' }}>
              <a href="mailto:privacy@meepleai.app" style={{
                display: 'inline-flex', alignItems: 'center', gap: 8,
                padding: '8px 14px',
                background: 'hsl(var(--c-kb))',
                color: '#fff',
                borderRadius: 999,
                textDecoration: 'none',
                fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 13.5,
                boxShadow: '0 4px 12px hsl(var(--c-kb) / 0.3)',
              }}>
                <span aria-hidden="true">✉</span>
                <code style={{
                  fontFamily: 'var(--f-mono)', fontSize: 12.5, fontWeight: 600,
                  color: '#fff', background: 'transparent',
                }}>privacy@meepleai.app</code>
              </a>
              <a href="#/contact" style={{
                fontFamily: 'var(--f-display)', fontWeight: 600,
                fontSize: 13.5,
                color: 'hsl(var(--c-kb))',
                textDecoration: 'underline',
                textDecorationColor: 'hsl(var(--c-kb) / 0.4)',
                textUnderlineOffset: 3,
              }}>Vai al modulo contatto →</a>
            </div>
          </aside>
        </article>
      </div>
    </main>
  );
};

// helper: render inline links + emails as styled spans
function renderInline(text) {
  // simple regex pass for emails to color them entity=kb mono inline
  const parts = text.split(/(\b[\w.-]+@[\w.-]+\.\w+\b|\/\w[\w-]*)/g);
  return parts.map((p, i) => {
    if (/^[\w.-]+@[\w.-]+\.\w+$/.test(p)) {
      return (
        <code key={i} style={{
          fontFamily: 'var(--f-mono)', fontSize: '0.92em',
          background: 'var(--bg-muted)',
          padding: '2px 6px', borderRadius: 4,
          color: 'hsl(var(--c-kb))',
        }}>{p}</code>
      );
    }
    if (/^\/\w[\w-]*$/.test(p)) {
      return (
        <a key={i} href={`#${p}`} style={{
          color: 'hsl(var(--c-kb))',
          textDecoration: 'underline',
          textDecorationColor: 'hsl(var(--c-kb) / 0.4)',
          textUnderlineOffset: 2,
          fontFamily: 'var(--f-mono)', fontSize: '0.92em',
        }}>{p}</a>
      );
    }
    return <React.Fragment key={i}>{p}</React.Fragment>;
  });
}

// ── DESKTOP / MOBILE FRAMES ────────────────────────────────────────────────
const DesktopScreen = ({ label, theme, doc }) => (
  <div data-screen-label={label} data-theme={theme} style={{
    width: 1440,
    background: 'var(--bg)',
    borderRadius: 16,
    border: '1px solid var(--border-strong)',
    overflow: 'hidden',
    boxShadow: '0 32px 64px -24px rgba(0,0,0,0.25), 0 12px 32px -12px rgba(0,0,0,0.12)',
  }}>
    <div style={{
      padding: '8px 14px',
      background: 'var(--bg-sunken)',
      borderBottom: '1px solid var(--border)',
      display: 'flex', alignItems: 'center', gap: 10,
      fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)',
    }}>
      <span style={{ display: 'inline-flex', gap: 6 }}>
        <span style={{ width: 10, height: 10, borderRadius: '50%', background: '#FF5F57' }} />
        <span style={{ width: 10, height: 10, borderRadius: '50%', background: '#FEBC2E' }} />
        <span style={{ width: 10, height: 10, borderRadius: '50%', background: '#28C840' }} />
      </span>
      <span style={{ marginLeft: 8 }}>meepleai.app/{doc.type} · {label}</span>
    </div>
    <LegalPageShell doc={doc} isMobile={false} />
  </div>
);

const MobileScreen = ({ label, theme, doc }) => (
  <div data-screen-label={label} data-theme={theme} style={{
    width: 375,
    background: 'var(--bg)',
    borderRadius: 36,
    border: '8px solid #1a1410',
    overflow: 'hidden',
    boxShadow: '0 30px 60px rgba(0,0,0,0.6)',
    position: 'relative',
  }}>
    <div style={{
      height: 30, background: 'var(--bg-sunken)',
      display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      padding: '0 18px',
      fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 11,
      color: 'var(--text)',
    }}>
      <span>9:41</span>
      <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}>
        <span>●●●</span><span>📶</span><span>🔋</span>
      </span>
    </div>
    <LegalPageShell doc={doc} isMobile={true} />
  </div>
);

// ── APP ────────────────────────────────────────────────────────────────────
const App = () => {
  const [theme, setTheme] = useState(() => {
    try { return localStorage.getItem('mai-sp3-legal-theme') || 'light'; } catch { return 'light'; }
  });
  const [docOpen, setDocOpen] = useState(false); // mobile TOC default closed

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    try { localStorage.setItem('mai-sp3-legal-theme', theme); } catch {}
  }, [theme]);

  return (
    <div style={{
      minHeight: '100vh',
      background: theme === 'dark' ? '#0a0805' : '#e8dfcf',
      padding: '40px 24px 80px',
    }}>
      {/* Toolbar */}
      <div style={{
        maxWidth: 1480, margin: '0 auto 32px',
        display: 'flex', alignItems: 'center', gap: 16, flexWrap: 'wrap',
      }}>
        <div>
          <div style={{
            fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 22,
            color: theme === 'dark' ? '#f0e4d2' : '#2b1f12',
            letterSpacing: '-0.015em',
          }}>SP3 · #3 — Legal pages template</div>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 12,
            color: theme === 'dark' ? '#8a7860' : '#9a8870',
          }}>Route: /terms · Template UNICO riusabile per /privacy + /cookies (stesso shell, contenuto diverso)</div>
        </div>
        <div style={{ marginLeft: 'auto' }}>
          <button onClick={() => setTheme(theme === 'light' ? 'dark' : 'light')}
            style={{
              padding: '8px 14px', borderRadius: 999,
              border: '1px solid ' + (theme === 'dark' ? 'rgba(224,180,110,0.28)' : 'rgba(180,130,80,0.32)'),
              background: theme === 'dark' ? '#1e1710' : '#fff',
              color: theme === 'dark' ? '#f0e4d2' : '#2b1f12',
              fontFamily: 'var(--f-display)', fontWeight: 600, fontSize: 13,
              cursor: 'pointer',
            }}>{theme === 'light' ? '🌙 Dark' : '☀️ Light'}</button>
        </div>
      </div>

      {/* DESKTOP */}
      <h2 style={{
        maxWidth: 1480, margin: '0 auto 14px',
        fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 700,
        textTransform: 'uppercase', letterSpacing: '0.08em',
        color: theme === 'dark' ? '#c8b896' : '#5a4a38',
      }}>Desktop · 1440 — Terms (template)</h2>
      <div style={{
        maxWidth: 1480, margin: '0 auto 48px',
        display: 'flex', justifyContent: 'center',
      }}>
        <DesktopScreen label="01 Desktop · /terms · TOC sticky + body 68ch" theme={theme} doc={TERMS_DOC} />
      </div>

      {/* MOBILE */}
      <h2 style={{
        maxWidth: 1480, margin: '0 auto 14px',
        fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 700,
        textTransform: 'uppercase', letterSpacing: '0.08em',
        color: theme === 'dark' ? '#c8b896' : '#5a4a38',
      }}>Mobile · 375 — Terms (template)</h2>
      <div style={{
        maxWidth: 1480, margin: '0 auto 48px',
        display: 'flex', justifyContent: 'center',
      }}>
        <MobileScreen label="02 Mobile · /terms · TOC <details> collassabile" theme={theme} doc={TERMS_DOC} />
      </div>

      {/* TEMPLATE FLAG */}
      <div style={{
        maxWidth: 900, margin: '40px auto 24px',
        padding: '20px 24px',
        background: theme === 'dark' ? '#1e1710' : '#fff',
        border: '1px dashed hsl(174 60% 45% / 0.5)',
        borderRadius: 16,
        color: theme === 'dark' ? '#f0e4d2' : '#2b1f12',
      }}>
        <div style={{
          fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 15,
          marginBottom: 8, color: 'hsl(var(--c-kb))',
          display: 'flex', alignItems: 'center', gap: 8,
        }}>
          <span aria-hidden="true">⚠</span>
          Template note (per il reviewer)
        </div>
        <p style={{
          margin: 0, fontFamily: 'var(--f-body)', fontSize: 13.5, lineHeight: 1.7,
          color: theme === 'dark' ? '#c8b896' : '#5a4a38',
        }}>
          Questo è un mockup UNICO che funge da template per <strong>3 route</strong>:{' '}
          <code style={{ fontFamily: 'var(--f-mono)', fontSize: 12, padding: '2px 6px',
            background: theme === 'dark' ? '#241c13' : '#efe6d9', borderRadius: 4 }}>/terms</code>,{' '}
          <code style={{ fontFamily: 'var(--f-mono)', fontSize: 12, padding: '2px 6px',
            background: theme === 'dark' ? '#241c13' : '#efe6d9', borderRadius: 4 }}>/privacy</code>,{' '}
          <code style={{ fontFamily: 'var(--f-mono)', fontSize: 12, padding: '2px 6px',
            background: theme === 'dark' ? '#241c13' : '#efe6d9', borderRadius: 4 }}>/cookies</code>.
          {' '}Lo shell <code style={{ fontFamily: 'var(--f-mono)', fontSize: 12 }}>LegalPageShell</code> accetta una prop{' '}
          <code style={{ fontFamily: 'var(--f-mono)', fontSize: 12 }}>doc</code> con{' '}
          <code style={{ fontFamily: 'var(--f-mono)', fontSize: 12 }}>{`{ type, docTitle, updated, version, sections[] }`}</code>.
          Per privacy + cookies basterà definire un altro oggetto con la stessa shape e cambierà solo il chip in alto, il titolo e il body — TOC, scroll-spy, sticky desktop, details mobile, footer DPO sono identici.
        </p>
      </div>

      {/* DOD CHECKLIST */}
      <div style={{
        maxWidth: 900, margin: '0 auto',
        padding: '24px 28px',
        background: theme === 'dark' ? '#1e1710' : '#fff',
        border: '1px solid ' + (theme === 'dark' ? 'rgba(224,180,110,0.14)' : 'rgba(180,130,80,0.18)'),
        borderRadius: 16,
        color: theme === 'dark' ? '#f0e4d2' : '#2b1f12',
      }}>
        <h3 style={{ fontFamily: 'var(--f-display)', margin: '0 0 12px', fontSize: 18 }}>DoD Checklist · sp3-legal</h3>
        <ul style={{ fontFamily: 'var(--f-body)', fontSize: 13.5, lineHeight: 1.7, color: theme === 'dark' ? '#c8b896' : '#5a4a38', paddingLeft: 20, margin: 0 }}>
          <li>✓ Route target /terms (template UNICO riusabile per /privacy /cookies — flag visibile)</li>
          <li>✓ Hero: chip entity-kb + h1 "Termini di Servizio" + meta "Ultimo aggiornamento: 26 aprile 2026 · Versione 2.4.0" mono</li>
          <li>✓ Border-bottom soft hero + gradient sottile kb tint</li>
          <li>✓ Desktop: grid 240px sidebar + body 68ch · TOC sticky top:24 con scroll-spy IntersectionObserver</li>
          <li>✓ TOC item active: border-left 2px hsl(--c-kb) + bg --c-kb/0.06 + text bold + numero 01–08 mono color-coded</li>
          <li>✓ Smooth scroll su click anchor + scrollMarginTop:80px sulle sezioni</li>
          <li>✓ Mobile: single column, TOC in &lt;details&gt; collassabile con summary "📋 Indice · 8 sezioni"</li>
          <li>✓ 8 sezioni H2 placeholder Terms italiano realistico (NO lorem ipsum), 2-3 paragrafi ciascuna</li>
          <li>✓ Tipografia reading: --f-body Nunito, line-height 1.75, max-width 68ch, fontSize 16/15</li>
          <li>✓ H2 Quicksand --fs-2xl (~26px desktop / 22px mobile), scroll-margin-top 80</li>
          <li>✓ Email inline rese come &lt;code&gt; --f-mono + bg --bg-muted + radius 4 + color --c-kb (helper renderInline)</li>
          <li>✓ Link inline /contact rendered come anchor mono color --c-kb underline soft</li>
          <li>✓ Footer card: gradient kb→toolkit, "Hai domande sui nostri termini?" + CTA mailto privacy@meepleai.app + sub-CTA /contact</li>
          <li>✓ ARIA: &lt;main&gt; wrapper, nav role="navigation" aria-label="Indice della pagina", aria-current="location" su sezione attiva</li>
          <li>✓ &lt;details&gt; mobile con &lt;summary&gt; clickable nativo (toggle keyboard accessibile out-of-the-box)</li>
          <li>✓ Light + Dark mode toggle persistito</li>
          <li>✓ NO window.innerWidth in body components — isMobile è prop esplicita dalla frame (lezione sp3-how-it-works applicata)</li>
          <li>✓ Componenti v2 nuovi: LegalPageShell · LegalTOC (varianti sticky+collapsible)</li>
          <li>✓ NON sovrascritto components.css né data.js</li>
          <li>✓ Stato singolo (default) — content statico come da contratto</li>
        </ul>
      </div>
    </div>
  );
};

ReactDOM.createRoot(document.getElementById('root')).render(<App />);

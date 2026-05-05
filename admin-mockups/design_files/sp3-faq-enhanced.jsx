/* MeepleAI SP3 — FAQ Enhanced
   Route: /faq (replaces existing static FAQ from PR #553)
   Pagina informativa pubblica con search + categorie + quick answers + accordion.

   Decisioni di design:
   - Categorie come tab pills sticky (pattern #4) — sidebar NO: troppo spazio sprecato
     su una pagina informativa.
   - Quick answers: 4 cards (2x2 desktop, 1x4 mobile). 4 > 5 perché grid pulita
     e 4 fit naturalmente in 2 col su tablet/desktop.
   - Highlight match di ricerca: <mark> con background hsl(c-warning, .25) +
     color text + font-weight 700 — più scannable di bold-only, meno aggressivo
     di bg pieno yellow.
   - Accordion: clone visivo di Radix Accordion (pattern attuale) — single-open,
     animated chevron rotate, smooth height transition.
*/

const { useState, useMemo, useEffect, useRef, useCallback } = React;

// ─── FAQ DATASET ──────────────────────────────────────
const FAQ_CATEGORIES = [
  { id:'all',     label:'Tutte',           icon:'📚' },
  { id:'account', label:'Account & Login', icon:'👤' },
  { id:'games',   label:'Giochi & Library',icon:'🎲' },
  { id:'ai',      label:'AI & Chat',       icon:'🤖' },
  { id:'privacy', label:'Privacy & Dati',  icon:'🔒' },
  { id:'billing', label:'Billing & Alpha', icon:'💳' },
];

const FAQS = [
  // Account
  { id:'q1', cat:'account', q:'Come creo un account su MeepleAI?',
    short:'Durante l\'alpha privata, accedi tramite invito ricevuto via email o iscriviti alla waitlist su /join.',
    long:'Durante l\'**alpha privata** (in corso fino a Q2 2026), MeepleAI è accessibile solo tramite invito.\n\nCi sono due modi:\n\n1. **Inviti diretti**: se conosci un membro alpha, puoi chiedergli un link `/accept-invite/{token}`. Ogni utente ha 5 inviti.\n2. **Waitlist**: registrati su `/join` indicando il tuo gioco preferito. Inviamo nuovi inviti settimanalmente in base alla posizione.\n\nDopo il go-live pubblico (post-alpha), la registrazione sarà aperta a tutti.',
    popular: true, popularRank: 1 },
  { id:'q2', cat:'account', q:'Ho dimenticato la password, come la recupero?',
    short:'Vai su /login e clicca "Password dimenticata?". Riceverai un\'email con il link di reset entro 5 minuti.',
    long:'Dalla pagina `/login` clicca **"Password dimenticata?"**. Inserisci la tua email registrata.\n\nRiceverai entro 5 minuti un\'email con un link di reset (valido 1 ora). Se non vedi l\'email, controlla **spam/promozioni**.\n\nSe il problema persiste, contattaci via `/contact`.',
    popular: false },
  { id:'q3', cat:'account', q:'Posso cambiare la mia email?',
    short:'Sì, da Impostazioni → Account → Email. Riceverai una verifica al nuovo indirizzo prima del cambio.',
    long:'Vai in **Impostazioni → Account → Email**. Inserisci la nuova email e la password attuale.\n\nTi invieremo un link di **verifica al nuovo indirizzo**: il cambio diventa effettivo solo dopo la conferma.',
    popular: false },

  // Games
  { id:'q4', cat:'games', q:'Come installo un toolkit per un gioco?',
    short:'Apri il gioco dal catalogo → tab Toolkit → clicca "Installa" sul toolkit desiderato. Si attiva entro 30 secondi.',
    long:'Per **installare un toolkit**:\n\n1. Vai al **Catalogo Giochi** (`/games`)\n2. Apri il gioco desiderato (es. Wingspan)\n3. Tab **Toolkit** → scegli un toolkit (Official o Community)\n4. Clicca **"Installa"**\n\nIl toolkit si attiva entro 30 secondi. Lo trovi sotto **"I miei toolkit"** nel pannello laterale del gioco. Per disinstallare, click destro → Rimuovi.',
    popular: true, popularRank: 2 },
  { id:'q5', cat:'games', q:'Posso aggiungere un gioco non presente nel catalogo?',
    short:'Durante l\'alpha solo gli admin possono aggiungere giochi. Suggerisci nuovi giochi via /contact.',
    long:'Durante l\'**alpha privata**, l\'aggiunta di nuovi giochi al catalogo è ristretta agli admin per garantire qualità delle KB.\n\nPuoi **suggerire un gioco** via `/contact`: indicaci titolo, editore, anno e link BoardGameGeek. Valutiamo le richieste settimanalmente. Post-alpha, gli utenti potranno proporre giochi self-service con approvazione moderata.',
    popular: false },
  { id:'q6', cat:'games', q:'Come funziona la mia libreria personale?',
    short:'La libreria contiene i giochi che possiedi o segui. Aggiungi giochi dal catalogo con il pulsante "Possiedo".',
    long:'La **Libreria** (`/library`) traccia i giochi che possiedi o vuoi seguire.\n\nDal catalogo, clicca **"Possiedo"** per aggiungerlo, **"Wishlist"** per seguirlo. Dalla libreria puoi:\n\n- Vedere statistiche delle tue partite\n- Accedere rapidamente a toolkit e KB\n- Ricevere notifiche su nuovi contenuti community',
    popular: false },

  // AI
  { id:'q7', cat:'ai', q:'Come funzionano gli agenti AI di MeepleAI?',
    short:'Ogni agente è specializzato per un gioco. Risponde a domande di regole, strategia e setup citando la KB ufficiale.',
    long:'Gli **agenti AI** sono specializzati per gioco. Ogni agente:\n\n- Usa una **KB indicizzata** (regolamenti, FAQ ufficiali, errata)\n- Cita sempre la fonte (pagina del manuale o URL)\n- Supporta **modelli diversi**: Claude Sonnet, GPT-4o, Gemini Pro\n\nApri un agente da una pagina gioco → tab **Agenti** → "Prova". La conversazione è privata di default; puoi condividerla post-fact con un link.',
    popular: true, popularRank: 3 },
  { id:'q8', cat:'ai', q:'Le mie conversazioni con l\'AI sono private?',
    short:'Sì. Le chat sono private per default. Puoi condividerle con un link pubblico se decidi di aiutare la community.',
    long:'**Sì, le tue conversazioni sono private** per default e non vengono usate per training.\n\nPuoi opzionalmente **rendere pubblica** una conversazione (toggle "Condividi") per aiutare altri giocatori. Solo le chat marcate come pubbliche sono visibili nel catalogo `/shared-chats`.',
    popular: false },
  { id:'q9', cat:'ai', q:'Posso scegliere quale modello AI usare?',
    short:'Sì. Ogni agente espone i modelli supportati (Claude, GPT-4o, Gemini). Cambialo dal menu chat in alto.',
    long:'Sì. Ogni agente dichiara i **modelli supportati**. Dal menu chat (in alto a destra durante una conversazione) puoi switchare tra:\n\n- **Claude Sonnet** (default, qualità top)\n- **GPT-4o** (veloce, economico)\n- **Gemini Pro** (long-context per regolamenti grossi)\n\nDurante l\'alpha tutti i modelli sono **gratis** con limite 100 msg/giorno.',
    popular: false },

  // Privacy
  { id:'q10', cat:'privacy', q:'Quali dati raccoglie MeepleAI?',
    short:'Email, nickname, attività di gioco e conversazioni AI. Nessun dato di pagamento durante alpha. Dettagli su /privacy.',
    long:'Raccogliamo:\n\n- **Account**: email, nickname, password (hash bcrypt)\n- **Gameplay**: giochi posseduti, sessioni, statistiche\n- **AI**: conversazioni con gli agenti (private per default)\n\nNon raccogliamo **dati di pagamento** durante l\'alpha (gratis). I dati sono ospitati su server EU (Frankfurt).\n\nPolicy completa: `/privacy`.',
    popular: true, popularRank: 4 },
  { id:'q11', cat:'privacy', q:'Posso esportare i miei dati?',
    short:'Sì. Da Impostazioni → Privacy → Esporta dati. Ricevi un .zip con JSON e markdown entro 24h.',
    long:'Sì, conformemente al **GDPR Art. 20**.\n\nDa **Impostazioni → Privacy → Esporta i miei dati**, richiedi un export. Riceverai entro 24h un `.zip` contenente:\n\n- `account.json` — profilo e impostazioni\n- `library.json` — giochi posseguiti, statistiche\n- `conversations/*.md` — tutte le chat in markdown\n\nL\'export è disponibile per il download per 7 giorni.',
    popular: false },
  { id:'q12', cat:'privacy', q:'Come elimino il mio account?',
    short:'Da Impostazioni → Account → Elimina account. La cancellazione è definitiva dopo 30 giorni di grace period.',
    long:'Da **Impostazioni → Account → Elimina account**.\n\nDopo la richiesta hai **30 giorni di grace period** per ripensarci (basta fare login). Trascorso il periodo, **tutti i tuoi dati sono cancellati** in modo definitivo: account, conversazioni, contributi pubblici (toolkit/agenti) vengono anonymizzati.',
    popular: false },

  // Billing
  { id:'q13', cat:'billing', q:'MeepleAI è gratuito?',
    short:'Durante l\'alpha sì, completamente gratuito. Post-alpha avremo un free tier + piano Pro a pagamento.',
    long:'Durante l\'**alpha privata** MeepleAI è **completamente gratuito** per i membri invitati.\n\nPost-alpha (Q2 2026 stimato) avremo:\n\n- **Free tier**: catalogo pubblico, 50 msg AI/giorno, 1 toolkit installato\n- **Pro** (~ €5/mese): AI illimitata, toolkit illimitati, agenti privati, priorità support\n\nI membri alpha riceveranno **3 mesi Pro gratis** come ringraziamento.',
    popular: true, popularRank: 5 },
  { id:'q14', cat:'billing', q:'Quanto durerà l\'alpha?',
    short:'L\'alpha è iniziata Gen 2026. Stimiamo go-live pubblico in Q2 2026 (Apr-Giu).',
    long:'L\'**alpha privata** è partita a **Gennaio 2026** con i primi 100 inviti.\n\nStimiamo il go-live pubblico per **Q2 2026** (Aprile-Giugno), in funzione di:\n\n- Stabilità infrastruttura\n- Catalogo giochi (target 100+ giochi indicizzati)\n- Feedback alpha members\n\nAggiornamenti regolari sul nostro changelog `/changelog`.',
    popular: false },
];

const POPULAR_FAQS = FAQS.filter(f => f.popular).sort((a,b) => a.popularRank - b.popularRank).slice(0, 4);

// ═══════════════════════════════════════════════════════
// ─── HIGHLIGHT UTIL ──────────────────────────────────
// ═══════════════════════════════════════════════════════
const highlight = (text, query) => {
  if (!query || !query.trim()) return text;
  const q = query.trim();
  const escaped = q.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const re = new RegExp(`(${escaped})`, 'gi');
  const parts = text.split(re);
  return parts.map((part, i) =>
    re.test(part) ? <mark key={i} style={{
      background:'hsl(var(--c-warning) / .28)',
      color:'var(--text)',
      padding:'1px 3px', borderRadius: 3,
      fontWeight: 700,
    }}>{part}</mark> : part
  );
};

// Strip tokens **bold** from short for highlight
const renderShort = (text, query) => highlight(text, query);

// Render long: split by ** for bold + lists
const renderLong = (text, query) => {
  const lines = text.split('\n');
  return lines.map((line, i) => {
    if (line.trim() === '') return <div key={i} style={{ height: 6 }}/>;
    if (/^\d+\.\s/.test(line)) {
      const content = line.replace(/^\d+\.\s/, '');
      const num = line.match(/^(\d+)\./)[1];
      return (
        <div key={i} style={{ display:'flex', gap: 8, marginBottom: 4 }}>
          <span style={{
            fontFamily:'var(--f-mono)', color:'var(--text-muted)',
            fontWeight: 700, fontSize: 12, flexShrink: 0,
          }}>{num}.</span>
          <span>{renderInline(content, query)}</span>
        </div>
      );
    }
    if (line.startsWith('- ')) {
      return (
        <div key={i} style={{ display:'flex', gap: 8, marginBottom: 3 }}>
          <span style={{ color:'var(--text-muted)', flexShrink: 0 }}>•</span>
          <span>{renderInline(line.slice(2), query)}</span>
        </div>
      );
    }
    return <p key={i} style={{ margin:'0 0 6px', lineHeight: 1.6 }}>{renderInline(line, query)}</p>;
  });
};
const renderInline = (text, query) => {
  // Split by ** bold ** marks → render bold parts, then highlight inside
  const parts = text.split(/(\*\*[^*]+\*\*|`[^`]+`)/g);
  return parts.map((p, i) => {
    if (p.startsWith('**') && p.endsWith('**')) {
      return <strong key={i} style={{ color:'var(--text)' }}>{highlight(p.slice(2, -2), query)}</strong>;
    }
    if (p.startsWith('`') && p.endsWith('`')) {
      return <code key={i} style={{
        fontFamily:'var(--f-mono)', fontSize: 11.5,
        padding:'1px 5px', borderRadius: 4,
        background:'var(--bg-muted)', color:'hsl(var(--c-info))',
      }}>{p.slice(1, -1)}</code>;
    }
    return <span key={i}>{highlight(p, query)}</span>;
  });
};

// ═══════════════════════════════════════════════════════
// ─── BANNER (riuso) ──────────────────────────────────
// ═══════════════════════════════════════════════════════
const Banner = ({ tone='info', children, action }) => {
  const colors = { success:'c-toolkit', error:'c-danger', warning:'c-warning', info:'c-info' };
  const c = colors[tone];
  const icon = tone === 'success' ? '✓' : tone === 'error' ? '✕' : tone === 'warning' ? '!' : 'ℹ';
  return (
    <div role="status" style={{
      display:'flex', alignItems:'flex-start', gap: 10,
      padding:'12px 14px', borderRadius:'var(--r-md)',
      background:`hsl(var(--${c}) / .1)`,
      border:`1px solid hsl(var(--${c}) / .25)`,
      color:`hsl(var(--${c}))`,
      fontSize: 13, fontWeight: 600,
    }}>
      <span aria-hidden="true" style={{
        fontSize: 11, lineHeight: 1, flexShrink: 0,
        width: 20, height: 20, borderRadius:'50%',
        background:`hsl(var(--${c}) / .18)`,
        display:'inline-flex', alignItems:'center', justifyContent:'center',
        marginTop: 1,
      }}>{icon}</span>
      <span style={{ flex: 1, lineHeight: 1.55, color:'var(--text)' }}>{children}</span>
      {action}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── FAQ SEARCHBAR ───────────────────────────────────
// ═══════════════════════════════════════════════════════
const FAQSearchBar = ({ value, onChange, placeholder='Cerca tra le FAQ...' }) => {
  const inputRef = useRef(null);
  return (
    <div style={{
      position:'relative',
      display:'flex', alignItems:'center',
      background:'var(--bg-card)',
      border:'1.5px solid var(--border)',
      borderRadius:'var(--r-pill)',
      boxShadow:'var(--shadow-sm)',
      transition:'border-color var(--dur-sm), box-shadow var(--dur-sm)',
    }}
    onFocus={e => {
      e.currentTarget.style.borderColor = 'hsl(var(--c-game) / .55)';
      e.currentTarget.style.boxShadow = '0 0 0 4px hsl(var(--c-game) / .12), var(--shadow-md)';
    }}
    onBlur={e => {
      e.currentTarget.style.borderColor = 'var(--border)';
      e.currentTarget.style.boxShadow = 'var(--shadow-sm)';
    }}
    >
      <span aria-hidden="true" style={{
        position:'absolute', left: 18,
        fontSize: 16, color:'var(--text-muted)',
        pointerEvents:'none',
      }}>🔍</span>
      <input
        ref={inputRef}
        type="search"
        value={value}
        onChange={e => onChange(e.target.value)}
        placeholder={placeholder}
        aria-label="Cerca tra le FAQ"
        style={{
          flex: 1, minWidth: 0,
          padding:'14px 18px 14px 46px',
          border:'none', outline:'none', background:'transparent',
          fontFamily:'var(--f-body)', fontSize: 15,
          color:'var(--text)',
          borderRadius:'var(--r-pill)',
        }}
      />
      {value && (
        <button
          type="button"
          onClick={() => { onChange(''); inputRef.current?.focus(); }}
          aria-label="Cancella ricerca"
          style={{
            marginRight: 8,
            width: 30, height: 30,
            borderRadius:'50%',
            background:'var(--bg-muted)',
            border:'none', color:'var(--text-sec)',
            fontSize: 13, cursor:'pointer',
            display:'inline-flex', alignItems:'center', justifyContent:'center',
            flexShrink: 0,
          }}>✕</button>
      )}
      {value && (
        <span style={{
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
          paddingRight: 16, fontWeight: 700, whiteSpace:'nowrap',
        }}>↵ Enter</span>
      )}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── QUICK ANSWER CARD ───────────────────────────────
// ═══════════════════════════════════════════════════════
const QuickAnswerCard = ({ faq, onClick }) => {
  const cat = FAQ_CATEGORIES.find(c => c.id === faq.cat);
  return (
    <button type="button" onClick={onClick} style={{
      display:'flex', flexDirection:'column', gap: 8,
      padding: 16, textAlign:'left',
      background:'var(--bg-card)',
      border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)',
      cursor:'pointer',
      transition:'all var(--dur-sm) var(--ease-out)',
      width:'100%', height:'100%',
    }}
    onMouseEnter={e => {
      e.currentTarget.style.borderColor = 'hsl(var(--c-game) / .35)';
      e.currentTarget.style.transform = 'translateY(-2px)';
      e.currentTarget.style.boxShadow = 'var(--shadow-md)';
    }}
    onMouseLeave={e => {
      e.currentTarget.style.borderColor = 'var(--border)';
      e.currentTarget.style.transform = '';
      e.currentTarget.style.boxShadow = '';
    }}
    >
      <div style={{
        display:'flex', alignItems:'center', gap: 8,
        fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
        color:'var(--text-muted)', textTransform:'uppercase',
        letterSpacing:'.08em',
      }}>
        <span aria-hidden="true">{cat.icon}</span>
        <span>{cat.label}</span>
        <span style={{ marginLeft:'auto', color:'hsl(var(--c-game))' }}>#{faq.popularRank}</span>
      </div>
      <h3 style={{
        fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 700,
        color:'var(--text)', margin: 0, lineHeight: 1.35,
      }}>{faq.q}</h3>
      <p style={{
        fontSize: 12.5, color:'var(--text-sec)',
        lineHeight: 1.55, margin: 0,
        display:'-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient:'vertical',
        overflow:'hidden',
      }}>{faq.short}</p>
      <div style={{
        marginTop: 'auto',
        fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 700,
        color:'hsl(var(--c-game))',
      }}>Leggi tutto →</div>
    </button>
  );
};

// ═══════════════════════════════════════════════════════
// ─── CATEGORY TABS (sticky) ──────────────────────────
// ═══════════════════════════════════════════════════════
const CategoryTabs = ({ active, onChange, counts }) => {
  const refs = useRef({});
  const [ind, setInd] = useState({ left: 0, width: 0 });
  useEffect(() => {
    const el = refs.current[active];
    if (el) setInd({ left: el.offsetLeft, width: el.offsetWidth });
  }, [active]);

  return (
    <div style={{
      position:'sticky', top: 0, zIndex: 3,
      background:'var(--glass-bg)', backdropFilter:'blur(12px)',
      borderBottom:'1px solid var(--border)',
      margin:'0 -16px', padding:'0 16px',
    }}>
      <div style={{
        position:'relative',
        display:'flex', alignItems:'center', gap: 4,
        overflowX:'auto', scrollbarWidth:'none',
        padding:'10px 0',
      }} className="mai-cb-scroll" role="tablist" aria-label="Categorie FAQ">
        {FAQ_CATEGORIES.map(c => {
          const isActive = c.id === active;
          const count = counts[c.id] ?? 0;
          return (
            <button
              key={c.id}
              type="button"
              ref={el => { refs.current[c.id] = el; }}
              onClick={() => onChange(c.id)}
              role="tab" aria-selected={isActive}
              style={{
                display:'inline-flex', alignItems:'center', gap: 6,
                padding:'8px 13px',
                background: isActive ? 'hsl(var(--c-game) / .12)' : 'transparent',
                border: isActive ? '1px solid hsl(var(--c-game) / .3)' : '1px solid var(--border)',
                borderRadius:'var(--r-pill)',
                color: isActive ? 'hsl(var(--c-game))' : 'var(--text-sec)',
                fontFamily:'var(--f-display)', fontSize: 12.5,
                fontWeight: isActive ? 700 : 600,
                cursor:'pointer',
                whiteSpace:'nowrap', flexShrink: 0,
                transition:'all var(--dur-sm) var(--ease-out)',
              }}
            >
              <span aria-hidden="true">{c.icon}</span>
              <span>{c.label}</span>
              <span style={{
                padding:'1px 6px', borderRadius:'var(--r-pill)',
                background: isActive ? 'hsl(var(--c-game) / .25)' : 'var(--bg-muted)',
                color: isActive ? 'hsl(var(--c-game))' : 'var(--text-muted)',
                fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
                fontVariantNumeric:'tabular-nums',
                minWidth: 16, textAlign:'center',
              }}>{count}</span>
            </button>
          );
        })}
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── ACCORDION ITEM ──────────────────────────────────
// ═══════════════════════════════════════════════════════
const AccordionItem = ({ faq, isOpen, onToggle, query, idx, scrollTarget }) => {
  const cat = FAQ_CATEGORIES.find(c => c.id === faq.cat);
  const ref = useRef(null);
  useEffect(() => {
    if (scrollTarget && ref.current) {
      // Mock — non scrolla in iframe ma evidenzia
    }
  }, [scrollTarget]);

  return (
    <div ref={ref} id={`faq-${faq.id}`} style={{
      borderBottom:'1px solid var(--border)',
      background: scrollTarget ? 'hsl(var(--c-warning) / .05)' : 'transparent',
      transition:'background var(--dur-md)',
    }}>
      <button
        type="button"
        onClick={onToggle}
        aria-expanded={isOpen}
        aria-controls={`faq-${faq.id}-panel`}
        style={{
          width:'100%', textAlign:'left',
          display:'flex', alignItems:'flex-start', gap: 12,
          padding:'18px 4px',
          background:'transparent', border:'none',
          cursor:'pointer',
        }}>
        <span aria-hidden="true" style={{
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
          color:'var(--text-muted)',
          padding:'2px 7px', borderRadius:'var(--r-pill)',
          background:'var(--bg-muted)',
          letterSpacing:'.04em',
          flexShrink: 0, marginTop: 2,
        }}>{cat.icon} {cat.label}</span>
        <span style={{
          flex: 1,
          fontFamily:'var(--f-display)',
          fontSize: 15, fontWeight: 600,
          color:'var(--text)',
          lineHeight: 1.45,
        }}>{renderShort(faq.q, query)}</span>
        <span aria-hidden="true" style={{
          flexShrink: 0,
          width: 28, height: 28, borderRadius:'50%',
          display:'inline-flex', alignItems:'center', justifyContent:'center',
          background: isOpen ? 'hsl(var(--c-game))' : 'var(--bg-muted)',
          color: isOpen ? '#fff' : 'var(--text-sec)',
          fontSize: 11, fontWeight: 700,
          transform: isOpen ? 'rotate(180deg)' : 'rotate(0)',
          transition:'all var(--dur-md) var(--ease-out)',
        }}>▾</span>
      </button>
      <div
        id={`faq-${faq.id}-panel`}
        role="region"
        style={{
          maxHeight: isOpen ? 800 : 0,
          overflow:'hidden',
          transition: isOpen
            ? 'max-height .35s cubic-bezier(.4,0,.2,1)'
            : 'max-height .25s cubic-bezier(.4,0,.2,1)',
        }}>
        <div style={{
          padding:'0 4px 18px 4px',
          fontSize: 13.5, color:'var(--text-sec)',
          lineHeight: 1.65,
          maxWidth: 720,
        }}>
          {renderLong(faq.long, query)}
        </div>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── CONTACT FOOTER CARD ─────────────────────────────
// ═══════════════════════════════════════════════════════
const ContactFooterCard = () => (
  <div style={{
    display:'flex', flexDirection:'column', alignItems:'center',
    gap: 10, padding:'28px 24px',
    textAlign:'center',
    background:'linear-gradient(135deg, hsl(var(--c-game) / .08), hsl(var(--c-event) / .06))',
    border:'1px solid hsl(var(--c-game) / .2)',
    borderRadius:'var(--r-xl)',
  }}>
    <div style={{
      width: 56, height: 56, borderRadius:'50%',
      background:'hsl(var(--c-game) / .15)',
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 26, marginBottom: 4,
    }} aria-hidden="true">💬</div>
    <h3 style={{
      fontFamily:'var(--f-display)', fontSize: 18, fontWeight: 700,
      color:'var(--text)', margin: 0,
    }}>Non hai trovato risposta?</h3>
    <p style={{
      fontSize: 13.5, color:'var(--text-sec)', maxWidth: 420,
      margin:'0 0 6px', lineHeight: 1.6,
    }}>Il nostro team risponde entro 24 ore. Scrivici per richieste, bug o feedback sulla piattaforma.</p>
    <a href="#" onClick={e=>e.preventDefault()} style={{
      padding:'10px 20px', borderRadius:'var(--r-pill)',
      background:'hsl(var(--c-game))', color:'#fff',
      fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 700,
      textDecoration:'none',
      boxShadow:'0 4px 14px hsl(var(--c-game) / .35)',
      transition:'transform var(--dur-sm)',
    }}
    onMouseEnter={e => e.currentTarget.style.transform = 'translateY(-1px)'}
    onMouseLeave={e => e.currentTarget.style.transform = ''}
    >Contattaci →</a>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── HERO ────────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const Hero = ({ query, setQuery, compact }) => (
  <div style={{
    padding: compact ? '28px 16px 20px' : '56px 32px 32px',
    textAlign:'center',
    background:'linear-gradient(180deg, hsl(var(--c-game) / .04), transparent)',
    borderBottom:'1px solid var(--border)',
  }}>
    <div style={{
      display:'inline-flex', alignItems:'center', gap: 6,
      padding:'4px 10px', borderRadius:'var(--r-pill)',
      background:'hsl(var(--c-game) / .12)',
      color:'hsl(var(--c-game))',
      fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
      textTransform:'uppercase', letterSpacing:'.1em',
      marginBottom: 12,
    }}>
      <span aria-hidden="true">📚</span>
      <span>Centro assistenza</span>
    </div>
    <h1 style={{
      fontFamily:'var(--f-display)', fontWeight: 800,
      fontSize: compact ? 26 : 38,
      letterSpacing:'-.02em', lineHeight: 1.1,
      color:'var(--text)', margin:'0 0 8px',
    }}>Domande frequenti</h1>
    <p style={{
      fontSize: compact ? 14 : 16, color:'var(--text-sec)',
      maxWidth: 540, margin:'0 auto 22px', lineHeight: 1.55,
    }}>Trova risposte rapide su MeepleAI — account, giochi, AI, privacy e altro.</p>
    <div style={{ maxWidth: 580, margin:'0 auto' }}>
      <FAQSearchBar value={query} onChange={setQuery}/>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── PAGE BODY ───────────────────────────────────────
// ═══════════════════════════════════════════════════════
const FAQPageBody = ({ stateOverride, compact }) => {
  const initialState = stateOverride === 'category-filter' ? { cat:'ai',  q:'' }
                     : stateOverride === 'search-results'  ? { cat:'all', q:'come installo un toolkit' }
                     : stateOverride === 'search-no'       ? { cat:'all', q:'xyz123' }
                     : { cat:'all', q:'' };

  const [activeCat, setActiveCat] = useState(initialState.cat);
  const [query, setQuery] = useState(initialState.q);
  const [openIds, setOpenIds] = useState(() => stateOverride === 'search-results' ? new Set(['q4']) : new Set());
  const [scrollTo, setScrollTo] = useState(stateOverride === 'search-results' ? 'q4' : null);

  const isLoading = stateOverride === 'loading';

  // Filter
  const filteredFAQs = useMemo(() => {
    let list = FAQS;
    if (activeCat !== 'all') list = list.filter(f => f.cat === activeCat);
    if (query.trim()) {
      const q = query.toLowerCase().trim();
      list = list.filter(f =>
        f.q.toLowerCase().includes(q) ||
        f.short.toLowerCase().includes(q) ||
        f.long.toLowerCase().includes(q)
      );
    }
    return list;
  }, [activeCat, query]);

  // Counts per category (always against full set, ignoring active cat)
  const counts = useMemo(() => {
    const c = { all: FAQS.length };
    FAQ_CATEGORIES.forEach(cat => {
      if (cat.id === 'all') return;
      const list = query.trim()
        ? FAQS.filter(f => f.cat === cat.id && (
            f.q.toLowerCase().includes(query.toLowerCase()) ||
            f.short.toLowerCase().includes(query.toLowerCase()) ||
            f.long.toLowerCase().includes(query.toLowerCase())
          ))
        : FAQS.filter(f => f.cat === cat.id);
      c[cat.id] = list.length;
    });
    if (query.trim()) {
      c.all = FAQS.filter(f =>
        f.q.toLowerCase().includes(query.toLowerCase()) ||
        f.short.toLowerCase().includes(query.toLowerCase()) ||
        f.long.toLowerCase().includes(query.toLowerCase())
      ).length;
    }
    return c;
  }, [query]);

  const toggleOpen = useCallback((id) => {
    setOpenIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
    setScrollTo(null);
  }, []);

  const onQuickAnswerClick = (faq) => {
    setQuery('');
    setActiveCat(faq.cat);
    setOpenIds(new Set([faq.id]));
    setScrollTo(faq.id);
  };

  if (isLoading) {
    return (
      <>
        <Hero query="" setQuery={()=>{}} compact={compact}/>
        <div style={{ padding: compact ? '20px 16px' : '32px', maxWidth: 880, margin:'0 auto' }}>
          <div style={{
            display:'grid',
            gridTemplateColumns: compact ? '1fr' : 'repeat(2, 1fr)',
            gap: 12, marginBottom: 24,
          }}>
            {[0,1,2,3].map(i => (
              <div key={i} className="mai-shimmer" style={{
                height: 130, borderRadius:'var(--r-lg)',
                background:'var(--bg-muted)',
              }}/>
            ))}
          </div>
          <div style={{ display:'flex', gap: 8, marginBottom: 16 }}>
            {[0,1,2,3,4].map(i => (
              <div key={i} className="mai-shimmer" style={{
                height: 32, width: 100,
                borderRadius:'var(--r-pill)',
                background:'var(--bg-muted)',
                flexShrink: 0,
              }}/>
            ))}
          </div>
          {[0,1,2,3,4].map(i => (
            <div key={i} className="mai-shimmer" style={{
              height: 60, marginBottom: 1,
              background:'var(--bg-muted)',
            }}/>
          ))}
        </div>
      </>
    );
  }

  const showNoResults = query.trim() && filteredFAQs.length === 0;
  const showQuickAnswers = !query.trim() && activeCat === 'all';

  return (
    <>
      <Hero query={query} setQuery={setQuery} compact={compact}/>

      <div style={{
        padding: compact ? '20px 16px 32px' : '28px 32px 56px',
        maxWidth: 880, margin:'0 auto',
      }}>
        {/* Search status */}
        {query.trim() && filteredFAQs.length > 0 && (
          <Banner tone="success">
            <strong>{filteredFAQs.length}</strong> risultat{filteredFAQs.length === 1 ? 'o' : 'i'} per
            "<strong>{query}</strong>"
          </Banner>
        )}

        {showNoResults && (
          <Banner tone="info" action={
            <a href="#" onClick={e=>e.preventDefault()} style={{
              padding:'6px 12px', borderRadius:'var(--r-md)',
              background:'hsl(var(--c-game))', color:'#fff',
              fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700,
              textDecoration:'none', whiteSpace:'nowrap',
            }}>Contattaci →</a>
          }>
            <strong>Nessun risultato</strong> per "<strong>{query}</strong>". Prova con altri termini o contattaci direttamente.
          </Banner>
        )}

        {/* Quick Answers */}
        {showQuickAnswers && (
          <div style={{ marginBottom: 24, marginTop: query.trim() ? 16 : 0 }}>
            <div style={{
              display:'flex', alignItems:'baseline', gap: 8,
              marginBottom: 12,
            }}>
              <h2 style={{
                fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 700,
                color:'var(--text)', margin: 0,
                textTransform:'uppercase', letterSpacing:'.08em',
              }}>⚡ Risposte rapide</h2>
              <span style={{
                fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
                fontWeight: 600,
              }}>top 4 più cercate</span>
            </div>
            <div style={{
              display:'grid',
              gridTemplateColumns: compact ? '1fr' : 'repeat(2, 1fr)',
              gap: 12,
            }}>
              {POPULAR_FAQS.map(f => (
                <QuickAnswerCard key={f.id} faq={f} onClick={() => onQuickAnswerClick(f)}/>
              ))}
            </div>
          </div>
        )}

        {/* Category tabs */}
        {!showNoResults && (
          <div style={{ marginTop: showQuickAnswers ? 8 : (query.trim() ? 16 : 0), marginBottom: 8 }}>
            <CategoryTabs active={activeCat} onChange={setActiveCat} counts={counts}/>
          </div>
        )}

        {/* Accordion */}
        {!showNoResults && filteredFAQs.length > 0 && (
          <div style={{
            background:'var(--bg-card)',
            border:'1px solid var(--border)',
            borderRadius:'var(--r-lg)',
            padding:'4px 18px',
            marginTop: 4,
          }}>
            {filteredFAQs.map((f, idx) => (
              <AccordionItem
                key={f.id}
                faq={f}
                idx={idx}
                isOpen={openIds.has(f.id)}
                onToggle={() => toggleOpen(f.id)}
                query={query}
                scrollTarget={scrollTo === f.id}
              />
            ))}
          </div>
        )}

        {/* Footer CTA */}
        <div style={{ marginTop: 32 }}>
          <ContactFooterCard/>
        </div>
      </div>
    </>
  );
};

// ═══════════════════════════════════════════════════════
// ─── DESKTOP / MOBILE SHELLS ─────────────────────────
// ═══════════════════════════════════════════════════════
const PhoneSbar = () => (
  <div className="phone-sbar">
    <span style={{ fontFamily:'var(--f-mono)' }}>14:32</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);
const PhoneTopNav = () => (
  <div style={{
    display:'flex', alignItems:'center', gap: 9,
    padding:'10px 14px', borderBottom:'1px solid var(--border)',
    background:'var(--bg)',
    position:'sticky', top: 0, zIndex: 5,
  }}>
    <button aria-label="Indietro" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'transparent', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 14, cursor:'pointer',
    }}>←</button>
    <div style={{ flex: 1, fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)', textAlign:'center' }}>
      meepleai.app / faq
    </div>
    <div style={{ width: 32 }}/>
  </div>
);

const MobileScreen = ({ stateOverride }) => (
  <>
    <PhoneSbar/>
    <div style={{
      flex: 1, overflowY:'auto', scrollbarWidth:'none',
      background:'var(--bg)', position:'relative',
    }}>
      <PhoneTopNav/>
      <FAQPageBody stateOverride={stateOverride} compact/>
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

const DesktopNav = () => (
  <div style={{
    display:'flex', alignItems:'center', gap: 14,
    padding:'12px 32px',
    background:'var(--glass-bg)', backdropFilter:'blur(12px)',
    borderBottom:'1px solid var(--border)',
    position:'sticky', top: 0, zIndex: 6,
  }}>
    <div style={{ display:'flex', alignItems:'center', gap: 9 }}>
      <div style={{
        width: 26, height: 26, borderRadius: 7,
        background:'linear-gradient(135deg, hsl(var(--c-game)), hsl(var(--c-event)))',
        color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
        fontWeight: 800, fontSize: 13, fontFamily:'var(--f-display)',
      }}>M</div>
      <span style={{ fontFamily:'var(--f-display)', fontWeight: 700, fontSize: 14 }}>MeepleAI</span>
    </div>
    <nav style={{ flex: 1, display:'flex', gap: 18, marginLeft: 24,
      fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 600,
      color:'var(--text-sec)' }}>
      <a href="#" style={{ color:'inherit' }}>Catalogo</a>
      <a href="#" style={{ color:'inherit' }}>Shared games</a>
      <a href="#" style={{ color:'hsl(var(--c-game))', fontWeight: 700 }}>FAQ</a>
      <a href="#" style={{ color:'inherit' }}>Privacy</a>
    </nav>
    <a href="#" onClick={e=>e.preventDefault()} style={{
      padding:'7px 14px', borderRadius:'var(--r-md)',
      color:'#fff', fontSize: 13, fontWeight: 700, fontFamily:'var(--f-display)',
      background:'hsl(var(--c-game))',
      boxShadow:'0 3px 10px hsl(var(--c-game) / .3)',
      textDecoration:'none',
    }}>Accedi</a>
  </div>
);

const DesktopScreen = ({ stateOverride }) => (
  <div style={{ minHeight: 720, background:'var(--bg)' }}>
    <DesktopNav/>
    <FAQPageBody stateOverride={stateOverride}/>
  </div>
);

const DesktopFrame = ({ children, label, desc }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 12, width:'100%' }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}</div>
    <div style={{
      width:'100%', maxWidth: 1280,
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
        <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/faq</span>
      </div>
      {children}
    </div>
    {desc && <div style={{
      fontSize: 11, color:'var(--text-muted)', maxWidth: 720,
      textAlign:'center', lineHeight: 1.55,
    }}>{desc}</div>}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── ROOT ────────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const MOBILE_STATES = [
  { id:'default',           label:'01 · Default',          desc:'Hero + search vuota + 4 quick answer card stacked + tab "Tutte" attivo + accordion 14 FAQ.' },
  { id:'category-filter',   label:'02 · Filtro categoria', desc:'Tab "AI & Chat" attivo. Quick answers nascoste (mostrate solo con cat=all + query vuota). Solo 3 FAQ AI.' },
  { id:'search-results',    label:'03 · Search results',   desc:'Query "come installo un toolkit". Banner success "1 risultato". Match highlighted con <mark> bg yellow. FAQ q4 auto-expanded e bg warning subtle.' },
  { id:'search-no',         label:'04 · No results',       desc:'Query "xyz123". Banner info + CTA inline contattaci. Nessuna lista, niente quick answers, niente accordion.' },
  { id:'loading',           label:'05 · Loading',          desc:'Hero static + 4 card skeleton + 5 tab skeleton + 5 accordion row skeleton.' },
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
        }}>SP3 · Public Secondary · #5 — FAQ Enhanced (delta su PR #553)</div>
        <h1>FAQ Enhanced — /faq</h1>
        <p className="lead">
          Versione enhanced della pagina /faq esistente. Aggiunge: <strong>FAQSearchBar</strong> (large pill +
          clear-button), <strong>QuickAnswerCard</strong> grid (top 4 popular), <strong>CategoryTabs</strong> sticky
          (riuso pattern #4 con count badge), <strong>Accordion radix-style</strong> animato + <mark style={{
            background:'hsl(var(--c-warning) / .28)', padding:'1px 4px', borderRadius: 3, fontWeight: 700,
          }}>highlight match</mark> con &lt;mark&gt; bg warning. Footer CTA "Contattaci" gradient card.
        </p>

        <div className="section-label">Mobile · 375 — 5 stati</div>
        <div className="phones-grid">
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc}>
              <MobileScreen stateOverride={s.id}/>
            </PhoneShell>
          ))}
        </div>

        <div className="section-label">Desktop · 1440 — 4 viste</div>
        <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
          <DesktopFrame label="06 · Desktop · Default"
            desc="Quick answers in grid 2x2. Categorie tab orizzontali sticky con count badge. Accordion 14 FAQ. Footer card gradient.">
            <DesktopScreen stateOverride="default"/>
          </DesktopFrame>
          <DesktopFrame label="07 · Desktop · Filtro AI & Chat"
            desc="Tab AI selezionato (count 3). Quick answers nascoste perché cat ≠ all. Solo 3 FAQ AI in accordion.">
            <DesktopScreen stateOverride="category-filter"/>
          </DesktopFrame>
          <DesktopFrame label="08 · Desktop · Search results"
            desc={`Query "come installo un toolkit". Banner success "1 risultato". Match highlighted nell'accordion (auto-expanded). Quick answers nascoste durante search.`}>
            <DesktopScreen stateOverride="search-results"/>
          </DesktopFrame>
          <DesktopFrame label="09 · Desktop · No results"
            desc={`Query "xyz123". Banner info + CTA inline. Nessun accordion. Footer rimane.`}>
            <DesktopScreen stateOverride="search-no"/>
          </DesktopFrame>
        </div>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim {
          0%   { background-position: -200% 0; }
          100% { background-position:  200% 0; }
        }
        .mai-shimmer {
          background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important;
          background-size: 200% 100% !important;
          animation: mai-shimmer-anim 1.4s linear infinite;
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
        .mai-cb-scroll::-webkit-scrollbar { display: none; }
        button:focus-visible, a:focus-visible, input:focus-visible {
          outline: 2px solid currentColor;
          outline-offset: 2px;
        }
        mark { color: var(--text); }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);

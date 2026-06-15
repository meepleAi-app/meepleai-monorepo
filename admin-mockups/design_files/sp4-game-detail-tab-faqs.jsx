/**
 * sp4-game-detail-tab-faqs.jsx — AI placeholder (#2148)
 *
 * Game-detail-scoped FAQ sub-tab for `/games/[id]/faqs`. The shared
 * `sp3-faq-enhanced.html` covers public-app FAQ; this variant scopes to
 * game-specific questions (rules clarifications, edge cases).
 * Designer review required.
 *
 * Parent canonical mockup: sp4-game-detail.html.
 */

function FaqsTab({ faqs = SAMPLE_FAQS }) {
  return (
    <section data-tab="faqs" aria-label="Domande frequenti">
      <header className="tab-header">
        <h1>
          FAQ{' '}
          <span className="entity-chip e-kb" aria-hidden>
            📚 KB
          </span>
        </h1>
        <p className="subtitle">Domande frequenti su questo gioco. Approvate dalla community.</p>
      </header>

      {faqs.length === 0 ? (
        <p className="empty-state" role="status">
          Nessuna FAQ pubblicata ancora. Le prime domande approvate compariranno qui.
        </p>
      ) : (
        faqs.map((f, i) => <FaqItem key={f.id} faq={f} defaultOpen={i === 0} />)
      )}
    </section>
  );
}

function FaqItem({ faq, defaultOpen }) {
  return (
    <details className="faq" open={defaultOpen}>
      <summary>{faq.question}</summary>
      <p>{faq.answer}</p>
      <div className="meta-row">
        <span>👁️ {faq.views} visualizzazioni</span>
        <span>👍 {faq.helpful} utili</span>
      </div>
    </details>
  );
}

const SAMPLE_FAQS = [
  { id: 'q1', question: 'Si possono giocare in 2 giocatori?', answer: 'Sì, con la variante "duello" descritta nel manuale (Cap. 7). Cambiano alcune regole di setup ma non il flusso di gioco.', views: 320, helpful: 28 },
  { id: 'q2', question: "Cosa succede se l'azione termina senza vincitore?", answer: 'Si applica la regola del "tempo limite" (Cap. 4, p. 16): chi ha più punti vince.', views: 180, helpful: 14 },
  { id: 'q3', question: 'Il setup avanzato è raccomandato per principianti?', answer: 'No, è raccomandato iniziare con il setup base. Il setup avanzato aggiunge 2 carte azione e regole speciali.', views: 95, helpful: 8 },
];

ReactDOM.createRoot(document.getElementById('root')).render(<FaqsTab />);

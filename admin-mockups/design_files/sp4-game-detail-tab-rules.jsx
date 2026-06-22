/**
 * sp4-game-detail-tab-rules.jsx — AI placeholder (#2148)
 *
 * Low-fidelity stub for the `/games/[id]/rules` sub-tab. Provides minimal
 * semantic structure + entity tokens to unblock US-9 conformity coverage.
 *
 * Designer review required: polish typography, add state variants
 * (loading/empty/error), pin acceptance values, graduate design_intent
 * from "forward-refactor" to "current".
 *
 * Parent canonical mockup: sp4-game-detail.html (multi-tab game detail).
 */

function RulesTab({ rules = SAMPLE_RULES }) {
  return (
    <section data-tab="rules" aria-label="Regole">
      <header className="tab-header">
        <h1>
          Regole{' '}
          <span className="entity-chip e-game" aria-hidden>
            🎲 Game
          </span>
        </h1>
        <p className="subtitle">
          Sintesi delle regole del gioco, con citazioni al PDF originale.
        </p>
      </header>

      {rules.length === 0 ? (
        <EmptyState />
      ) : (
        <ul className="rule-list">
          {rules.map(r => (
            <RuleSection key={r.id} title={r.title} body={r.body} citation={r.citation} />
          ))}
        </ul>
      )}

      <button type="button" className="floating-cta e-bg-agent" data-action="ask-agent">
        💬 Chiedi all&apos;Agente
      </button>
    </section>
  );
}

function RuleSection({ title, body, citation }) {
  return (
    <li className="rule-section">
      <h2>{title}</h2>
      <p>{body}</p>
      {citation && (
        <span className="citation" aria-label={`Riferimento: ${citation}`}>
          📄 {citation}
        </span>
      )}
    </li>
  );
}

function EmptyState() {
  return (
    <div className="empty-state" role="status">
      <p>Nessuna regola caricata. Carica il PDF del manuale per iniziare.</p>
    </div>
  );
}

const SAMPLE_RULES = [
  {
    id: 'r1',
    title: 'Preparazione',
    body: 'Ogni giocatore riceve la propria plancia, 3 carte iniziali e 5 risorse di partenza.',
    citation: 'Manuale Cap. 1, p. 4',
  },
  {
    id: 'r2',
    title: 'Svolgimento del turno',
    body: 'Il turno si svolge in 3 fasi: azione principale, scambio, mantenimento.',
    citation: 'Manuale Cap. 2, p. 8',
  },
  {
    id: 'r3',
    title: 'Condizioni di vittoria',
    body: 'Il primo giocatore a raggiungere 10 punti vittoria vince la partita.',
    citation: 'Manuale Cap. 5, p. 22',
  },
];

ReactDOM.createRoot(document.getElementById('root')).render(<RulesTab />);

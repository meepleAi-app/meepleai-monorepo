/**
 * sp4-game-detail-tab-strategies.jsx — AI placeholder (#2148)
 *
 * Low-fidelity stub for the `/games/[id]/strategies` sub-tab. Designer review
 * required: pin acceptance, add state variants (empty/loading/error).
 *
 * Parent canonical mockup: sp4-game-detail.html.
 */

function StrategiesTab({ strategies = SAMPLE_STRATEGIES }) {
  return (
    <section data-tab="strategies" aria-label="Strategie">
      <header className="tab-header">
        <h1>
          Strategie{' '}
          <span className="entity-chip e-toolkit" aria-hidden>
            🧰 Toolkit
          </span>
        </h1>
        <p className="subtitle">Strategie condivise dalla community per dominare questo gioco.</p>
      </header>

      {strategies.length === 0 ? (
        <p className="empty-state" role="status">
          Nessuna strategia ancora. Sii il primo a condividere la tua!
        </p>
      ) : (
        <ul>
          {strategies.map(s => (
            <StrategyCard key={s.id} strategy={s} />
          ))}
        </ul>
      )}
    </section>
  );
}

function StrategyCard({ strategy }) {
  return (
    <li className="strategy-card">
      <div className="meta">
        <span className="difficulty">{strategy.difficulty}</span>
        <span>👁️ {strategy.views} visualizzazioni</span>
      </div>
      <h2>{strategy.title}</h2>
      <p>{strategy.body}</p>
      <span className="author">
        di @{strategy.author} · {strategy.likes} like
      </span>
    </li>
  );
}

const SAMPLE_STRATEGIES = [
  { id: 's1', difficulty: 'Beginner', views: '1.2k', title: 'Apertura economica — controllo risorse', body: 'Concentrati sulla raccolta risorse nei primi 3 turni. Evita conflitti diretti finché non hai un vantaggio numerico chiaro.', author: 'marco_strategist', likes: 12 },
  { id: 's2', difficulty: 'Advanced', views: '740', title: 'Pressione tempo — rush in 5 turni', body: 'Sacrifica risorse mid-game per accelerare la condizione di vittoria. Funziona solo se avversari restano passivi.', author: 'luigi_aggressive', likes: 8 },
];

ReactDOM.createRoot(document.getElementById('root')).render(<StrategiesTab />);

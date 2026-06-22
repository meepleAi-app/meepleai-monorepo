/**
 * sp4-game-detail-tab-chat.jsx — AI placeholder (#2148)
 *
 * Standalone chat tab for `/games/[id]/chat`. Previously partial coverage
 * via `sp4-game-chat-tab.html` composite; this stub gives the sub-route its
 * own canonical mockup. Designer review required.
 *
 * Parent canonical mockup: sp4-game-detail.html.
 */

function ChatTab({ messages = SAMPLE_MESSAGES }) {
  return (
    <section data-tab="chat" aria-label="Chat con l'agente">
      <header className="tab-header">
        <h1>
          Chat con l&apos;Agente{' '}
          <span className="entity-chip e-agent" aria-hidden>
            🤖 Agent
          </span>
        </h1>
        <p className="subtitle">
          Domande sulle regole, strategie o dispute. Risposte con citazioni al manuale.
        </p>
      </header>

      <div className="chat-stream" role="log" aria-live="polite">
        {messages.map(m => (
          <Message key={m.id} message={m} />
        ))}
      </div>

      <ChatComposer onSubmit={text => console.log('placeholder send:', text)} />
    </section>
  );
}

function Message({ message }) {
  return (
    <div className={`msg ${message.role}`}>
      <div className="who">{message.role === 'user' ? 'Tu' : 'Agente'}</div>
      <p className="msg-text">{message.text}</p>
      {message.citation && (
        <span className="citation" aria-label={`Riferimento: ${message.citation}`}>
          📄 {message.citation}
        </span>
      )}
    </div>
  );
}

function ChatComposer({ onSubmit }) {
  const [draft, setDraft] = React.useState('');
  return (
    <form
      className="composer"
      onSubmit={e => {
        e.preventDefault();
        if (!draft.trim()) return;
        onSubmit(draft);
        setDraft('');
      }}
    >
      <input
        type="text"
        placeholder="Fai una domanda..."
        aria-label="Domanda all'Agente"
        value={draft}
        onChange={e => setDraft(e.target.value)}
      />
      <button type="submit">Invia</button>
    </form>
  );
}

const SAMPLE_MESSAGES = [
  { id: 'm1', role: 'user', text: 'Quante carte si pescano in caso di pareggio?', citation: null },
  { id: 'm2', role: 'agent', text: 'In caso di pareggio si pesca 1 carta extra per ogni giocatore coinvolto.', citation: 'Manuale Cap. 4, p. 18' },
  { id: 'm3', role: 'user', text: 'E se la pila è vuota?', citation: null },
  { id: 'm4', role: 'agent', text: 'Si rimischia lo scarto e si forma una nuova pila.', citation: 'Manuale Cap. 4, p. 19' },
];

ReactDOM.createRoot(document.getElementById('root')).render(<ChatTab />);

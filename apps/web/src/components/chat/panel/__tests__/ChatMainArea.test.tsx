import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { ChatMainArea } from '../ChatMainArea';

describe('ChatMainArea', () => {
  it('renders empty state with welcome when no messages', () => {
    render(
      <ChatMainArea
        messages={[]}
        gameName="Azul"
        suggestedQuestions={['Come si vince?', 'Regole base']}
        onSend={() => {}}
      />
    );
    expect(screen.getByText(/Ciao! Sono il tuo assistente AI/i)).toBeInTheDocument();
    expect(screen.getByText('Come si vince?')).toBeInTheDocument();
    expect(screen.getByText('Regole base')).toBeInTheDocument();
  });

  it('renders messages list when messages present', () => {
    const messages = [
      {
        id: 'm1',
        role: 'user' as const,
        content: 'Come si vince?',
        authorName: 'Marco',
        timestamp: '14:32',
      },
      {
        id: 'm2',
        role: 'assistant' as const,
        content: 'Per vincere devi fare X.',
        authorName: 'MeepleAI',
        timestamp: '14:33',
      },
    ];
    render(
      <ChatMainArea messages={messages} gameName="Azul" suggestedQuestions={[]} onSend={() => {}} />
    );
    expect(screen.getByText('Come si vince?')).toBeInTheDocument();
    expect(screen.getByText(/Per vincere devi fare X/i)).toBeInTheDocument();
  });

  it('calls onSend when message is sent via input', async () => {
    const onSend = vi.fn();
    const user = userEvent.setup();
    render(<ChatMainArea messages={[]} gameName="Azul" suggestedQuestions={[]} onSend={onSend} />);
    await user.type(screen.getByRole('textbox'), 'hello{Enter}');
    expect(onSend).toHaveBeenCalledWith('hello');
  });

  it('calls onSend when a suggested question is clicked', async () => {
    const onSend = vi.fn();
    const user = userEvent.setup();
    render(
      <ChatMainArea
        messages={[]}
        gameName="Azul"
        suggestedQuestions={['Come si vince?']}
        onSend={onSend}
      />
    );
    await user.click(screen.getByRole('button', { name: 'Come si vince?' }));
    expect(onSend).toHaveBeenCalledWith('Come si vince?');
  });

  // ─── #4168 (5g-b): la quota del piano ───────────────────────

  const QUOTA_SENTENCE =
    'Hai usato tutte le domande di oggi previste dal tuo piano (3 su 3). Il conteggio riparte alle 02:00 (mezzanotte UTC).';

  /**
   * Una domanda fermata dalla quota non è un guasto: niente riquadro rosso, ma l'avviso con i
   * numeri del piano e il link ai piani, come nelle altre viste della chat.
   */
  it('shows the plan quota as a notice with the way to the plans, not as an error', () => {
    render(
      <ChatMainArea
        messages={[]}
        suggestedQuestions={[]}
        onSend={() => {}}
        error={QUOTA_SENTENCE}
        tierLimit={{ current: 3, max: 3, upgradeUrl: '/pricing' }}
      />
    );

    expect(screen.getByTestId('tier-limit-exceeded')).toHaveTextContent(
      'Hai usato tutte le domande di oggi previste dal tuo piano (3 su 3)'
    );
    expect(screen.getByRole('link', { name: 'Vedi i piani' })).toHaveAttribute('href', '/pricing');
    // Il riquadro d'errore ripeterebbe la stessa frase in rosso: non c'è.
    expect(screen.queryByTestId('chat-stream-error')).not.toBeInTheDocument();
  });

  /** Il posto dell'avviso è nella pagina da prima, vuoto. */
  it('keeps the place for the quota notice in the page, empty', () => {
    render(<ChatMainArea messages={[]} suggestedQuestions={[]} onSend={() => {}} />);

    const region = screen.getByTestId('tier-limit-region');
    expect(region).toHaveAttribute('role', 'status');
    expect(region).toBeEmptyDOMElement();
  });

  /** Un guasto vero resta un errore, nel suo riquadro. */
  it('still shows another error as an error', () => {
    render(
      <ChatMainArea
        messages={[]}
        suggestedQuestions={[]}
        onSend={() => {}}
        error="Il servizio AI è temporaneamente non disponibile."
      />
    );

    expect(screen.getByTestId('chat-stream-error')).toHaveTextContent(
      'Il servizio AI è temporaneamente non disponibile.'
    );
    expect(screen.queryByTestId('tier-limit-exceeded')).not.toBeInTheDocument();
  });
});

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { NotificationCard } from './notification-card';

describe('NotificationCard', () => {
  it('renders title', () => {
    render(<NotificationCard entity="agent" title="AI ha risposto" timestamp="2h fa" />);
    expect(screen.getByText('AI ha risposto')).toBeInTheDocument();
  });

  it('renders body when provided', () => {
    render(
      <NotificationCard
        entity="agent"
        title="AI ha risposto"
        body="Dettagli sul porto speciale"
        timestamp="2h fa"
      />
    );
    expect(screen.getByText('Dettagli sul porto speciale')).toBeInTheDocument();
  });

  it('does not render body when absent', () => {
    const { container } = render(
      <NotificationCard entity="agent" title="AI ha risposto" timestamp="2h fa" />
    );
    // body slot absent — only title + timestamp text
    expect(container.querySelector('[data-testid="notification-body"]')).toBeNull();
  });

  it('renders timestamp', () => {
    render(<NotificationCard entity="agent" title="AI ha risposto" timestamp="2h fa" />);
    expect(screen.getByText('2h fa')).toBeInTheDocument();
  });

  it('renders icon when provided', () => {
    render(
      <NotificationCard
        entity="session"
        title="Sessione avviata"
        timestamp="3h fa"
        icon={<span data-testid="n-icon">🎲</span>}
      />
    );
    expect(screen.getByTestId('n-icon')).toBeInTheDocument();
  });

  it('entity=game sets border-left to --e-game', () => {
    const { container } = render(<NotificationCard entity="game" title="x" timestamp="ora" />);
    const el = container.querySelector<HTMLElement>('[data-entity="game"]');
    expect(el?.className).toMatch(/border-l-4/);
    expect(el?.style.borderLeftColor).toBe('hsl(var(--e-game))');
  });

  it('entity=kb maps to --e-document css var', () => {
    const { container } = render(
      <NotificationCard entity="kb" title="Nuova regola" timestamp="ieri" />
    );
    const el = container.querySelector<HTMLElement>('[data-entity="kb"]');
    expect(el?.style.borderLeftColor).toBe('hsl(var(--e-document))');
  });

  it('unread=true shows unread dot pip', () => {
    render(<NotificationCard entity="agent" title="x" timestamp="ora" unread />);
    expect(screen.getByTestId('unread-dot')).toBeInTheDocument();
  });

  it('unread=false (default) does NOT show unread dot pip', () => {
    render(<NotificationCard entity="agent" title="x" timestamp="ora" />);
    expect(screen.queryByTestId('unread-dot')).toBeNull();
  });

  it('unread=true applies font-semibold to title', () => {
    render(<NotificationCard entity="agent" title="Title-Z" timestamp="ora" unread />);
    expect(screen.getByText('Title-Z').className).toMatch(/font-semibold/);
  });

  it('unread=false applies font-normal to title', () => {
    render(<NotificationCard entity="agent" title="Title-Q" timestamp="ora" />);
    expect(screen.getByText('Title-Q').className).toMatch(/font-normal/);
  });

  it('onClick fires when card is clicked', async () => {
    const onClick = vi.fn();
    render(<NotificationCard entity="agent" title="Click me" timestamp="ora" onClick={onClick} />);
    await userEvent.click(screen.getByRole('button', { name: /click me/i }));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('dismiss button appears when onDismiss provided', () => {
    render(<NotificationCard entity="agent" title="x" timestamp="ora" onDismiss={() => {}} />);
    expect(screen.getByRole('button', { name: /rimuovi notifica/i })).toBeInTheDocument();
  });

  it('dismiss button does NOT appear when onDismiss absent', () => {
    render(<NotificationCard entity="agent" title="x" timestamp="ora" />);
    expect(screen.queryByRole('button', { name: /rimuovi notifica/i })).toBeNull();
  });

  it('dismiss onClick fires onDismiss', async () => {
    const onDismiss = vi.fn();
    render(<NotificationCard entity="agent" title="x" timestamp="ora" onDismiss={onDismiss} />);
    await userEvent.click(screen.getByRole('button', { name: /rimuovi notifica/i }));
    expect(onDismiss).toHaveBeenCalledTimes(1);
  });

  it('dismiss stopPropagation: clicking dismiss does NOT fire card onClick', async () => {
    const onClick = vi.fn();
    const onDismiss = vi.fn();
    render(
      <NotificationCard
        entity="agent"
        title="Outer"
        timestamp="ora"
        onClick={onClick}
        onDismiss={onDismiss}
      />
    );
    await userEvent.click(screen.getByRole('button', { name: /rimuovi notifica/i }));
    expect(onDismiss).toHaveBeenCalledTimes(1);
    expect(onClick).not.toHaveBeenCalled();
  });

  it('keyboard Enter on dismiss button does NOT bubble up and fire card onClick', async () => {
    const onClick = vi.fn();
    const onDismiss = vi.fn();
    render(
      <NotificationCard
        entity="agent"
        title="Outer"
        timestamp="ora"
        onClick={onClick}
        onDismiss={onDismiss}
      />
    );
    const dismissBtn = screen.getByRole('button', { name: /rimuovi notifica/i });
    dismissBtn.focus();
    await userEvent.keyboard('{Enter}');
    // Native button handles Enter as click → onDismiss called once
    expect(onDismiss).toHaveBeenCalledTimes(1);
    // Card-level onClick must NOT fire from descendant keydown
    expect(onClick).not.toHaveBeenCalled();
  });

  it('keyboard Enter on outer card div still fires card onClick', async () => {
    const onClick = vi.fn();
    const onDismiss = vi.fn();
    const { container } = render(
      <NotificationCard
        entity="agent"
        title="Outer"
        timestamp="ora"
        onClick={onClick}
        onDismiss={onDismiss}
      />
    );
    const card = container.querySelector<HTMLElement>('[role="button"][data-entity="agent"]');
    expect(card).not.toBeNull();
    card!.focus();
    await userEvent.keyboard('{Enter}');
    expect(onClick).toHaveBeenCalledTimes(1);
    expect(onDismiss).not.toHaveBeenCalled();
  });

  it('renders as <button> when onClick present', () => {
    const { container } = render(
      <NotificationCard entity="agent" title="Clicky" timestamp="ora" onClick={() => {}} />
    );
    const el = container.querySelector<HTMLElement>('[data-entity="agent"]');
    expect(el?.tagName).toBe('BUTTON');
  });

  it('renders as non-button element when onClick absent', () => {
    const { container } = render(
      <NotificationCard entity="agent" title="Static" timestamp="ora" />
    );
    const el = container.querySelector<HTMLElement>('[data-entity="agent"]');
    expect(el?.tagName).not.toBe('BUTTON');
  });

  it('custom dismissAriaLabel is applied', () => {
    render(
      <NotificationCard
        entity="agent"
        title="x"
        timestamp="ora"
        onDismiss={() => {}}
        dismissAriaLabel="Archivia messaggio"
      />
    );
    expect(screen.getByRole('button', { name: 'Archivia messaggio' })).toBeInTheDocument();
  });

  it('merges className', () => {
    const { container } = render(
      <NotificationCard entity="agent" title="x" timestamp="ora" className="custom-nclass" />
    );
    const el = container.querySelector<HTMLElement>('[data-entity="agent"]');
    expect(el?.className).toMatch(/custom-nclass/);
  });
});

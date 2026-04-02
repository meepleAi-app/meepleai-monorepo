import { render, screen } from '@testing-library/react';
import { SymbolStrip } from './SymbolStrip';

describe('SymbolStrip', () => {
  it('renders identity chips when provided', () => {
    render(<SymbolStrip entity="game" identityChip1="Strategia" identityChip2="Euro" />);
    expect(screen.getByText('Strategia')).toBeInTheDocument();
    expect(screen.getByText('Euro')).toBeInTheDocument();
  });

  it('renders game metric pills', () => {
    render(<SymbolStrip entity="game" playerCountDisplay="2-4" playTimeDisplay="45min" />);
    expect(screen.getByText(/2-4/)).toBeInTheDocument();
    expect(screen.getByText(/45min/)).toBeInTheDocument();
  });

  it('renders player metric pills', () => {
    render(<SymbolStrip entity="player" gamesPlayed={42} winRate={67} />);
    expect(screen.getByText(/42/)).toBeInTheDocument();
    expect(screen.getByText(/67%/)).toBeInTheDocument();
  });

  it('renders session metric pills', () => {
    render(<SymbolStrip entity="session" winnerScore="128 pts" sessionDate="12 Mar" />);
    expect(screen.getByText(/128 pts/)).toBeInTheDocument();
    expect(screen.getByText(/12 Mar/)).toBeInTheDocument();
  });

  it('renders agent metric pills with 3 pills', () => {
    render(
      <SymbolStrip entity="agent" conversationCount={34} agentAccuracy={94} linkedKbCount={3} />
    );
    expect(screen.getByText(/34/)).toBeInTheDocument();
    expect(screen.getByText(/94%/)).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('renders kb metric pills', () => {
    render(<SymbolStrip entity="kb" pageCount={48} chunkCount={312} />);
    expect(screen.getByText(/48/)).toBeInTheDocument();
    expect(screen.getByText(/312/)).toBeInTheDocument();
  });

  it('renders container when no chips or pills provided', () => {
    const { container } = render(<SymbolStrip entity="game" />);
    expect(container.querySelector('[data-symbol-strip]')).toBeInTheDocument();
  });
});

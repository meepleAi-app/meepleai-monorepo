import { render, screen } from '@testing-library/react';
import { ActiveSessions } from '../ActiveSessions';

describe('ActiveSessions', () => {
  it('renders section title "Sessioni Attive"', () => {
    render(<ActiveSessions />);
    expect(screen.getByText(/Sessioni Attive/i)).toBeInTheDocument();
  });

  it('renders empty state with CTA text when no sessions provided', () => {
    render(<ActiveSessions />);
    expect(screen.getByText('Nessuna partita in corso')).toBeInTheDocument();
    expect(screen.getByText('Inizia a giocare →')).toBeInTheDocument();
  });

  it('renders loading skeleton when loading=true', () => {
    const { container } = render(<ActiveSessions loading={true} />);
    const skeleton = container.querySelector('.animate-pulse');
    expect(skeleton).toBeInTheDocument();
    expect(screen.queryByText('Nessuna partita in corso')).not.toBeInTheDocument();
  });

  it('renders empty state when sessions is empty array', () => {
    render(<ActiveSessions sessions={[]} />);
    expect(screen.getByText('Nessuna partita in corso')).toBeInTheDocument();
    expect(screen.getByText('Inizia a giocare →')).toBeInTheDocument();
  });

  it('renders root element with data-testid="active-sessions"', () => {
    render(<ActiveSessions />);
    expect(screen.getByTestId('active-sessions')).toBeInTheDocument();
  });
});

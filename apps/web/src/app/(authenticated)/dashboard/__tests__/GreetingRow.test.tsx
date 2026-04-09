import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { GreetingRow } from '../sections/GreetingRow';

describe('GreetingRow', () => {
  it('renders the greeting with user display name', () => {
    render(
      <GreetingRow
        displayName="Marco"
        subtitle="Hai una partita in corso"
        stats={[
          { label: 'Partite mese', value: '24' },
          { label: 'Win rate', value: '68%' },
          { label: 'Tempo gioco', value: '42h' },
        ]}
      />
    );
    expect(screen.getByText(/Ciao/)).toBeInTheDocument();
    expect(screen.getByText('Marco')).toBeInTheDocument();
    expect(screen.getByText('Hai una partita in corso')).toBeInTheDocument();
  });

  it('renders all provided quick stats', () => {
    render(
      <GreetingRow
        displayName="Anna"
        subtitle="subtitle"
        stats={[
          { label: 'Partite mese', value: '24' },
          { label: 'Win rate', value: '68%' },
        ]}
      />
    );
    expect(screen.getByText('24')).toBeInTheDocument();
    expect(screen.getByText('Partite mese')).toBeInTheDocument();
    expect(screen.getByText('68%')).toBeInTheDocument();
    expect(screen.getByText('Win rate')).toBeInTheDocument();
  });

  it('omits the stats cluster when no stats provided', () => {
    const { container } = render(<GreetingRow displayName="X" subtitle="Y" stats={[]} />);
    expect(container.querySelector('[data-testid="greet-stats"]')).not.toBeInTheDocument();
  });
});

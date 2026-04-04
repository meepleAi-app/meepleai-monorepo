import { render, screen } from '@testing-library/react';
import { HeroBanner } from '../HeroBanner';

describe('HeroBanner', () => {
  it('renders title and subtitle', () => {
    render(<HeroBanner title="Stasera Catan!" subtitle="4 giocatori" />);
    expect(screen.getByText('Stasera Catan!')).toBeInTheDocument();
    expect(screen.getByText('4 giocatori')).toBeInTheDocument();
  });

  it('renders badge when provided', () => {
    render(<HeroBanner title="Test" subtitle="Sub" badge={{ text: 'LIVE', variant: 'live' }} />);
    expect(screen.getByText('LIVE')).toBeInTheDocument();
  });

  it('renders CTA link when provided', () => {
    render(<HeroBanner title="Test" subtitle="Sub" cta={{ label: 'Entra', href: '/session/1' }} />);
    expect(screen.getByRole('link', { name: /entra/i })).toHaveAttribute('href', '/session/1');
  });

  it('renders without CTA or badge (minimal)', () => {
    render(<HeroBanner title="Benvenuto" subtitle="Esplora" />);
    expect(screen.getByText('Benvenuto')).toBeInTheDocument();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });
});

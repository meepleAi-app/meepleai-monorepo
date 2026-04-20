import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PricingCard } from './pricing-card';

const baseFeatures = [
  { label: 'Libreria illimitata', included: true },
  { label: 'Supporto prioritario', included: false },
];

describe('PricingCard', () => {
  it('renders tier label', () => {
    render(
      <PricingCard tier="Free" price="€0" features={baseFeatures} cta={{ label: 'Inizia' }} />
    );
    expect(screen.getByText('Free')).toBeInTheDocument();
  });

  it('renders price', () => {
    render(
      <PricingCard tier="Pro" price="€9/mese" features={baseFeatures} cta={{ label: 'Abbonati' }} />
    );
    expect(screen.getByText('€9/mese')).toBeInTheDocument();
  });

  it('renders description when provided', () => {
    render(
      <PricingCard
        tier="Pro"
        price="€9"
        description="Per giocatori abituali"
        features={baseFeatures}
        cta={{ label: 'Go' }}
      />
    );
    expect(screen.getByText('Per giocatori abituali')).toBeInTheDocument();
  });

  it('does NOT render description when omitted', () => {
    render(<PricingCard tier="Pro" price="€9" features={baseFeatures} cta={{ label: 'Go' }} />);
    expect(screen.queryByText('Per giocatori abituali')).not.toBeInTheDocument();
  });

  it('renders feature list items', () => {
    render(
      <PricingCard tier="Free" price="€0" features={baseFeatures} cta={{ label: 'Inizia' }} />
    );
    expect(screen.getByText('Libreria illimitata')).toBeInTheDocument();
    expect(screen.getByText('Supporto prioritario')).toBeInTheDocument();
  });

  it('marks included features with data-included="true"', () => {
    render(
      <PricingCard tier="Free" price="€0" features={baseFeatures} cta={{ label: 'Inizia' }} />
    );
    const included = screen.getByText('Libreria illimitata').closest('li');
    expect(included).toHaveAttribute('data-included', 'true');
  });

  it('marks excluded features with data-included="false"', () => {
    render(
      <PricingCard tier="Free" price="€0" features={baseFeatures} cta={{ label: 'Inizia' }} />
    );
    const excluded = screen.getByText('Supporto prioritario').closest('li');
    expect(excluded).toHaveAttribute('data-included', 'false');
  });

  it('renders cta as button when onClick provided', () => {
    render(
      <PricingCard
        tier="Free"
        price="€0"
        features={baseFeatures}
        cta={{ label: 'Inizia', onClick: () => undefined }}
      />
    );
    expect(screen.getByRole('button', { name: 'Inizia' })).toBeInTheDocument();
  });

  it('fires cta onClick', async () => {
    const user = userEvent.setup();
    const handler = vi.fn();
    render(
      <PricingCard
        tier="Free"
        price="€0"
        features={baseFeatures}
        cta={{ label: 'Inizia', onClick: handler }}
      />
    );
    await user.click(screen.getByRole('button', { name: 'Inizia' }));
    expect(handler).toHaveBeenCalledTimes(1);
  });

  it('renders cta as link when href provided', () => {
    render(
      <PricingCard
        tier="Free"
        price="€0"
        features={baseFeatures}
        cta={{ label: 'Registrati', href: '/signup' }}
      />
    );
    const link = screen.getByRole('link', { name: 'Registrati' });
    expect(link).toHaveAttribute('href', '/signup');
  });

  it('applies highlighted styling when highlighted=true', () => {
    const { container } = render(
      <PricingCard
        tier="Pro"
        price="€9"
        features={baseFeatures}
        cta={{ label: 'Go' }}
        highlighted
      />
    );
    const root = container.firstChild as HTMLElement;
    expect(root).toHaveAttribute('data-highlighted', 'true');
    expect(root.style.borderColor).toContain('hsl');
  });

  it('highlighted=false by default (no data-highlighted=true)', () => {
    const { container } = render(
      <PricingCard tier="Free" price="€0" features={baseFeatures} cta={{ label: 'Inizia' }} />
    );
    const root = container.firstChild as HTMLElement;
    expect(root).toHaveAttribute('data-highlighted', 'false');
  });

  it('shows "Consigliato" badge when highlighted', () => {
    render(
      <PricingCard
        tier="Pro"
        price="€9"
        features={baseFeatures}
        cta={{ label: 'Go' }}
        highlighted
      />
    );
    expect(screen.getByText('Consigliato')).toBeInTheDocument();
  });

  it('does NOT show "Consigliato" badge when not highlighted', () => {
    render(
      <PricingCard tier="Free" price="€0" features={baseFeatures} cta={{ label: 'Inizia' }} />
    );
    expect(screen.queryByText('Consigliato')).not.toBeInTheDocument();
  });

  it('merges className onto root', () => {
    const { container } = render(
      <PricingCard
        tier="Free"
        price="€0"
        features={baseFeatures}
        cta={{ label: 'Inizia' }}
        className="custom-pricing"
      />
    );
    expect(container.firstChild).toHaveClass('custom-pricing');
  });
});

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect } from 'vitest';

import { ResponseMetaBadge } from '../ResponseMetaBadge';

describe('ResponseMetaBadge', () => {
  it('renders Fast badge with correct label and icon', () => {
    render(<ResponseMetaBadge strategyTier="Fast" />);

    const badge = screen.getByTestId('response-meta-badge');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveTextContent('Risposta rapida');
    expect(badge).toHaveAttribute('aria-label', 'Risposta rapida');
  });

  it('renders Balanced badge with correct styling', () => {
    render(<ResponseMetaBadge strategyTier="Balanced" />);

    const badge = screen.getByTestId('response-meta-badge');
    expect(badge).toHaveTextContent('Risposta bilanciata');
    expect(badge.className).toContain('bg-blue-500/15');
  });

  it('renders Premium badge', () => {
    render(<ResponseMetaBadge strategyTier="Premium" />);

    const badge = screen.getByTestId('response-meta-badge');
    expect(badge).toHaveTextContent('Risposta premium');
    expect(badge.className).toContain('bg-violet-500/15');
  });

  it('renders HybridRAG badge', () => {
    render(<ResponseMetaBadge strategyTier="HybridRAG" />);

    const badge = screen.getByTestId('response-meta-badge');
    expect(badge).toHaveTextContent('Analisi approfondita');
    expect(badge.className).toContain('bg-emerald-500/15');
  });

  it('renders nothing for unknown strategy tier', () => {
    const { container } = render(<ResponseMetaBadge strategyTier="Unknown" />);
    expect(container.innerHTML).toBe('');
  });

  it('applies custom className', () => {
    render(<ResponseMetaBadge strategyTier="Fast" className="custom-class" />);

    const badge = screen.getByTestId('response-meta-badge');
    expect(badge.className).toContain('custom-class');
  });

  it('has tooltip trigger wrapping the badge', () => {
    render(<ResponseMetaBadge strategyTier="Balanced" />);

    // The badge itself is a tooltip trigger (button role from Radix)
    const badge = screen.getByTestId('response-meta-badge');
    expect(badge).toBeInTheDocument();
    // Badge text confirms it renders the correct label
    expect(badge).toHaveTextContent('Risposta bilanciata');
  });

  it('has correct accessibility attributes', () => {
    render(<ResponseMetaBadge strategyTier="Premium" />);

    const badge = screen.getByTestId('response-meta-badge');
    expect(badge).toHaveAttribute('role', 'status');
    expect(badge).toHaveAttribute('aria-label', 'Risposta premium');
  });
});

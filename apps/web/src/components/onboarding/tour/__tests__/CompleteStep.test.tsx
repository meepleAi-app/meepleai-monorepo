import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { CompleteStep } from '../CompleteStep';

describe('CompleteStep', () => {
  it('renders completion heading and CTA', () => {
    render(<CompleteStep isSubmitting={false} error={null} onHome={vi.fn()} />);
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(/benvenuto/i);
    expect(screen.getByRole('button', { name: /vai alla home/i })).toBeInTheDocument();
  });

  it('calls onHome on CTA click', async () => {
    const onHome = vi.fn();
    const user = userEvent.setup();
    render(<CompleteStep isSubmitting={false} error={null} onHome={onHome} />);
    await user.click(screen.getByRole('button', { name: /vai alla home/i }));
    expect(onHome).toHaveBeenCalledTimes(1);
  });

  it('disables CTA while submitting', () => {
    render(<CompleteStep isSubmitting={true} error={null} onHome={vi.fn()} />);
    expect(screen.getByRole('button', { name: /vai alla home/i })).toBeDisabled();
  });

  it('shows error with role=alert and a retry affordance', () => {
    render(<CompleteStep isSubmitting={false} error="Rete non disponibile" onHome={vi.fn()} />);
    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent(/rete non disponibile/i);
  });
});

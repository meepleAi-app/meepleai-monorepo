import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { WelcomeStep } from '../WelcomeStep';

describe('WelcomeStep', () => {
  it('greets by userName when provided', () => {
    render(<WelcomeStep userName="Luca" onStart={vi.fn()} onSkip={vi.fn()} />);
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(
      /Benvenuto in MeepleAI, Luca/
    );
  });

  it('renders greeting without userName', () => {
    render(<WelcomeStep userName={null} onStart={vi.fn()} onSkip={vi.fn()} />);
    const heading = screen.getByRole('heading', { level: 1 });
    expect(heading).toHaveTextContent(/Benvenuto in MeepleAI!/);
    expect(heading.textContent).not.toMatch(/,/);
  });

  it('fires onStart on CTA click', async () => {
    const onStart = vi.fn();
    const user = userEvent.setup();
    render(<WelcomeStep userName={null} onStart={onStart} onSkip={vi.fn()} />);
    await user.click(screen.getByRole('button', { name: /inizia il tour/i }));
    expect(onStart).toHaveBeenCalledTimes(1);
  });

  it('fires onSkip on skip link', async () => {
    const onSkip = vi.fn();
    const user = userEvent.setup();
    render(<WelcomeStep userName={null} onStart={vi.fn()} onSkip={onSkip} />);
    await user.click(screen.getByRole('button', { name: /salta/i }));
    expect(onSkip).toHaveBeenCalledTimes(1);
  });
});

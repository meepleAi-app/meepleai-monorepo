import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe, toHaveNoViolations } from 'jest-axe';
import { OAuthButton } from './oauth-buttons';

expect.extend(toHaveNoViolations);

describe('OAuthButton', () => {
  it('renders default label "Continua con Google" for google provider', () => {
    render(<OAuthButton provider="google" />);
    expect(screen.getByRole('button', { name: /Continua con Google/i })).toBeInTheDocument();
  });

  it('renders default label "Continua con Discord" for discord provider', () => {
    render(<OAuthButton provider="discord" />);
    expect(screen.getByRole('button', { name: /Continua con Discord/i })).toBeInTheDocument();
  });

  it('renders default label "Continua con GitHub" for github provider', () => {
    render(<OAuthButton provider="github" />);
    expect(screen.getByRole('button', { name: /Continua con GitHub/i })).toBeInTheDocument();
  });

  it('custom label overrides the default', () => {
    render(<OAuthButton provider="google" label="Accedi con Google" />);
    expect(screen.getByRole('button', { name: 'Accedi con Google' })).toBeInTheDocument();
    expect(screen.queryByText('Continua con Google')).toBeNull();
  });

  it('onClick fires on click', async () => {
    const onClick = vi.fn();
    render(<OAuthButton provider="google" onClick={onClick} />);
    await userEvent.click(screen.getByRole('button'));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('onClick does NOT fire when disabled', async () => {
    const onClick = vi.fn();
    render(<OAuthButton provider="discord" onClick={onClick} disabled />);
    await userEvent.click(screen.getByRole('button'));
    expect(onClick).not.toHaveBeenCalled();
  });

  it('onClick does NOT fire when loading', async () => {
    const onClick = vi.fn();
    render(<OAuthButton provider="github" onClick={onClick} loading />);
    await userEvent.click(screen.getByRole('button'));
    expect(onClick).not.toHaveBeenCalled();
  });

  it('loading shows spinner and sets aria-busy=true', () => {
    render(<OAuthButton provider="google" loading />);
    const btn = screen.getByRole('button');
    expect(btn).toHaveAttribute('aria-busy', 'true');
    expect(btn.querySelector('svg.animate-spin')).not.toBeNull();
  });

  it('fullWidth defaults to true (w-full class present)', () => {
    render(<OAuthButton provider="google" />);
    expect(screen.getByRole('button')).toHaveClass('w-full');
  });

  it('fullWidth={false} omits w-full', () => {
    render(<OAuthButton provider="google" fullWidth={false} />);
    expect(screen.getByRole('button')).not.toHaveClass('w-full');
  });

  it('renders provider icon (svg element present)', () => {
    render(<OAuthButton provider="discord" />);
    const btn = screen.getByRole('button');
    // At least one svg (the brand icon)
    expect(btn.querySelector('svg')).not.toBeNull();
  });

  it('disabled prop sets disabled attribute', () => {
    render(<OAuthButton provider="github" disabled />);
    expect(screen.getByRole('button')).toBeDisabled();
  });

  it('forwards className', () => {
    render(<OAuthButton provider="google" className="custom-oauth" />);
    expect(screen.getByRole('button')).toHaveClass('custom-oauth');
  });

  it('has no a11y violations', async () => {
    const { container } = render(<OAuthButton provider="google" />);
    expect(await axe(container)).toHaveNoViolations();
  });
});

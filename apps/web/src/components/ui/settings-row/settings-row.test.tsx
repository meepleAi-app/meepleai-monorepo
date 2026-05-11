import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { SettingsRow } from './settings-row';

describe('SettingsRow', () => {
  it('renders the label', () => {
    render(<SettingsRow label="Notifications" />);
    expect(screen.getByText('Notifications')).toBeInTheDocument();
  });

  it('renders description when provided', () => {
    render(<SettingsRow label="Theme" description="Light or dark" />);
    expect(screen.getByText('Light or dark')).toBeInTheDocument();
  });

  it('renders icon when provided', () => {
    render(<SettingsRow label="Theme" icon={<span>🎨</span>} />);
    const icon = screen.getByTestId('settings-row-icon');
    expect(icon).toBeInTheDocument();
    expect(icon).toHaveTextContent('🎨');
  });

  it('renders trailing when provided', () => {
    render(<SettingsRow label="2FA" trailing={<span>On</span>} />);
    expect(screen.getByTestId('settings-row-trailing')).toHaveTextContent('On');
  });

  it('fires onClick when interactive and enabled', async () => {
    const onClick = vi.fn();
    render(<SettingsRow label="Click me" onClick={onClick} />);
    await userEvent.click(screen.getByRole('button', { name: /click me/i }));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('does not fire onClick when disabled', async () => {
    const onClick = vi.fn();
    render(<SettingsRow label="Disabled" onClick={onClick} disabled />);
    const btn = screen.getByRole('button', { name: /disabled/i });
    await userEvent.click(btn);
    expect(onClick).not.toHaveBeenCalled();
    expect(btn).toBeDisabled();
  });

  it('renders a button when onClick is provided', () => {
    render(<SettingsRow label="Button" onClick={() => {}} />);
    const btn = screen.getByRole('button', { name: /button/i });
    expect(btn.tagName).toBe('BUTTON');
    expect(btn).toHaveAttribute('type', 'button');
  });

  it('renders a link when href is provided without onClick', () => {
    render(<SettingsRow label="Link" href="/settings/account" />);
    const link = screen.getByRole('link', { name: /link/i });
    expect(link.tagName).toBe('A');
    expect(link).toHaveAttribute('href', '/settings/account');
  });

  it('prefers onClick when both onClick and href provided', () => {
    const onClick = vi.fn();
    render(<SettingsRow label="Both" href="/ignored" onClick={onClick} />);
    expect(screen.getByRole('button', { name: /both/i })).toBeInTheDocument();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });

  it('renders as a div when neither onClick nor href is provided', () => {
    render(<SettingsRow label="Static" />);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
    expect(screen.getByText('Static')).toBeInTheDocument();
  });

  it('auto-renders chevron when interactive and no trailing provided', () => {
    const { container } = render(<SettingsRow label="Go" onClick={() => {}} />);
    expect(container.querySelector('svg')).toBeInTheDocument();
  });

  it('does not auto-render chevron when trailing is provided', () => {
    const { container } = render(
      <SettingsRow label="Go" onClick={() => {}} trailing={<span>X</span>} />
    );
    expect(container.querySelector('svg')).not.toBeInTheDocument();
  });

  it('does not auto-render chevron when not interactive', () => {
    const { container } = render(<SettingsRow label="Plain" />);
    expect(container.querySelector('svg')).not.toBeInTheDocument();
  });

  it('applies destructive color to label when destructive=true', () => {
    render(<SettingsRow label="Delete account" destructive />);
    const label = screen.getByText('Delete account');
    expect(label.className).toMatch(/destructive/);
  });

  it('applies entity color to icon wrapper via inline style', () => {
    render(<SettingsRow label="KB" icon={<span>📚</span>} entity="kb" />);
    const icon = screen.getByTestId('settings-row-icon');
    // kb maps to --e-document
    expect(icon.getAttribute('style')).toMatch(/--e-document/);
  });

  it('merges className on the li wrapper', () => {
    const { container } = render(<SettingsRow label="X" className="my-li" />);
    const li = container.querySelector('li');
    expect(li).toHaveClass('my-li');
  });
});

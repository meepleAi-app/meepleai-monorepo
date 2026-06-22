import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { TwoFactorStatusCard } from '../two-factor/TwoFactorStatusCard';

describe('TwoFactorStatusCard', () => {
  it('shows Enabled badge + 2fa-status data-testid when on', () => {
    render(
      <TwoFactorStatusCard
        status={{ isEnabled: true, enabledAt: '2026-01-01T00:00:00Z', unusedBackupCodesCount: 10 }}
        onSetup={() => {}}
        onDisable={() => {}}
      />
    );
    const badge = screen.getByTestId('2fa-status');
    expect(badge).toHaveTextContent(/enabled/i);
  });

  it('shows enable-2fa CTA + data-testid when off', () => {
    render(
      <TwoFactorStatusCard
        status={{ isEnabled: false, enabledAt: null, unusedBackupCodesCount: 0 }}
        onSetup={() => {}}
        onDisable={() => {}}
      />
    );
    expect(screen.getByTestId('enable-2fa')).toBeInTheDocument();
    expect(screen.queryByTestId('disable-2fa')).toBeNull();
  });

  it('shows disable-2fa CTA when enabled', () => {
    render(
      <TwoFactorStatusCard
        status={{ isEnabled: true, enabledAt: '2026-01-01T00:00:00Z', unusedBackupCodesCount: 10 }}
        onSetup={() => {}}
        onDisable={() => {}}
      />
    );
    expect(screen.getByTestId('disable-2fa')).toBeInTheDocument();
  });

  it('calls onSetup when Enable button clicked', () => {
    const onSetup = vi.fn();
    render(
      <TwoFactorStatusCard
        status={{ isEnabled: false, enabledAt: null, unusedBackupCodesCount: 0 }}
        onSetup={onSetup}
        onDisable={() => {}}
      />
    );
    screen.getByTestId('enable-2fa').click();
    expect(onSetup).toHaveBeenCalled();
  });

  it('calls onDisable when Disable button clicked', () => {
    const onDisable = vi.fn();
    render(
      <TwoFactorStatusCard
        status={{ isEnabled: true, enabledAt: '2026-01-01T00:00:00Z', unusedBackupCodesCount: 10 }}
        onSetup={() => {}}
        onDisable={onDisable}
      />
    );
    screen.getByTestId('disable-2fa').click();
    expect(onDisable).toHaveBeenCalled();
  });
});

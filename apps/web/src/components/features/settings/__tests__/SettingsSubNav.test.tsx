import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { SettingsSubNav } from '../SettingsSubNav';

describe('SettingsSubNav', () => {
  it('renders all 7 section labels', () => {
    render(<SettingsSubNav active="profile" onSelect={() => {}} twoFactorEnabled={false} />);
    [
      'Profile',
      'Security',
      'AI & data consent',
      'Notifications',
      'Preferences',
      'API keys',
      'Connected services',
    ].forEach(l => expect(screen.getByText(l)).toBeInTheDocument());
  });
  it('shows 2FA-off badge on Security when disabled', () => {
    render(<SettingsSubNav active="profile" onSelect={() => {}} twoFactorEnabled={false} />);
    expect(screen.getByTestId('subnav-2fa-badge')).toHaveTextContent(/2fa off/i);
  });
  it('hides 2FA-off badge when 2FA is enabled', () => {
    render(<SettingsSubNav active="profile" onSelect={() => {}} twoFactorEnabled />);
    expect(screen.queryByTestId('subnav-2fa-badge')).toBeNull();
  });
  it('calls onSelect with section id on click', () => {
    const onSelect = vi.fn();
    render(<SettingsSubNav active="profile" onSelect={onSelect} twoFactorEnabled={false} />);
    fireEvent.click(screen.getByText('Security'));
    expect(onSelect).toHaveBeenCalledWith('security');
  });
});

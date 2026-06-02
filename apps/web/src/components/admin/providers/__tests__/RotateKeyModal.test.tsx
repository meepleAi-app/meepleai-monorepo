import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { RotateKeyModal } from '../RotateKeyModal';

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: vi.fn(),
}));

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';

const mockedUseCurrentUser = vi.mocked(useCurrentUser);

describe('RotateKeyModal', () => {
  it('button is disabled when user is not superadmin', () => {
    mockedUseCurrentUser.mockReturnValue({
      data: { id: 'u1', email: 'admin@x.com', role: 'admin', tier: 'plus' },
    } as ReturnType<typeof useCurrentUser>);

    render(<RotateKeyModal providerName="deepseek" />);
    const button = screen.getByTestId('rotate-key-button-deepseek');
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('title', expect.stringMatching(/superadmin/i));
  });

  it('button is disabled even when user is superadmin (BE endpoint pending)', () => {
    mockedUseCurrentUser.mockReturnValue({
      data: { id: 'u1', email: 'admin@x.com', role: 'superadmin', tier: 'plus' },
    } as ReturnType<typeof useCurrentUser>);

    render(<RotateKeyModal providerName="deepseek" />);
    const button = screen.getByTestId('rotate-key-button-deepseek');
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('data-be-available', 'false');
    expect(button).toHaveAttribute('title', expect.stringMatching(/BE/i));
  });
});

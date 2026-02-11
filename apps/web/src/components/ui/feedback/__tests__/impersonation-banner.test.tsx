/**
 * ImpersonationBanner Tests (Issue #3700)
 *
 * Tests for the impersonation banner component:
 * - Renders when impersonating
 * - Hides when not impersonating
 * - Displays user info correctly
 * - End button interaction
 * - Loading state
 * - Accessibility (role, aria-live)
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ImpersonationBanner } from '../impersonation-banner';

const mockUser = {
  id: 'user-123',
  displayName: 'John Doe',
  email: 'john@example.com',
};

describe('ImpersonationBanner', () => {
  describe('visibility', () => {
    it('should not render when not impersonating', () => {
      const { container } = render(
        <ImpersonationBanner
          isImpersonating={false}
          impersonatedUser={null}
          onEndImpersonation={vi.fn()}
        />
      );

      expect(container.firstChild).toBeNull();
    });

    it('should not render when impersonating but no user', () => {
      const { container } = render(
        <ImpersonationBanner
          isImpersonating={true}
          impersonatedUser={null}
          onEndImpersonation={vi.fn()}
        />
      );

      expect(container.firstChild).toBeNull();
    });

    it('should render when impersonating with user', () => {
      render(
        <ImpersonationBanner
          isImpersonating={true}
          impersonatedUser={mockUser}
          onEndImpersonation={vi.fn()}
        />
      );

      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });

  describe('user info display', () => {
    it('should display impersonated user name', () => {
      render(
        <ImpersonationBanner
          isImpersonating={true}
          impersonatedUser={mockUser}
          onEndImpersonation={vi.fn()}
        />
      );

      expect(screen.getByText('John Doe')).toBeInTheDocument();
    });

    it('should display impersonated user email', () => {
      render(
        <ImpersonationBanner
          isImpersonating={true}
          impersonatedUser={mockUser}
          onEndImpersonation={vi.fn()}
        />
      );

      expect(screen.getByText('(john@example.com)')).toBeInTheDocument();
    });

    it('should display "Impersonating:" label', () => {
      render(
        <ImpersonationBanner
          isImpersonating={true}
          impersonatedUser={mockUser}
          onEndImpersonation={vi.fn()}
        />
      );

      expect(screen.getByText('Impersonating:')).toBeInTheDocument();
    });

    it('should display audit warning text', () => {
      render(
        <ImpersonationBanner
          isImpersonating={true}
          impersonatedUser={mockUser}
          onEndImpersonation={vi.fn()}
        />
      );

      expect(screen.getByText('Actions are logged with your admin ID')).toBeInTheDocument();
    });
  });

  describe('end impersonation button', () => {
    it('should display "End Impersonation" button', () => {
      render(
        <ImpersonationBanner
          isImpersonating={true}
          impersonatedUser={mockUser}
          onEndImpersonation={vi.fn()}
        />
      );

      expect(screen.getByRole('button', { name: /end impersonation/i })).toBeInTheDocument();
    });

    it('should call onEndImpersonation when clicked', async () => {
      const user = userEvent.setup();
      const onEnd = vi.fn();

      render(
        <ImpersonationBanner
          isImpersonating={true}
          impersonatedUser={mockUser}
          onEndImpersonation={onEnd}
        />
      );

      await user.click(screen.getByRole('button', { name: /end impersonation/i }));
      expect(onEnd).toHaveBeenCalledTimes(1);
    });

    it('should disable button when loading', () => {
      render(
        <ImpersonationBanner
          isImpersonating={true}
          impersonatedUser={mockUser}
          onEndImpersonation={vi.fn()}
          isLoading={true}
        />
      );

      expect(screen.getByRole('button')).toBeDisabled();
    });

    it('should show loading text when loading', () => {
      render(
        <ImpersonationBanner
          isImpersonating={true}
          impersonatedUser={mockUser}
          onEndImpersonation={vi.fn()}
          isLoading={true}
        />
      );

      expect(screen.getByText('Ending...')).toBeInTheDocument();
    });
  });

  describe('accessibility', () => {
    it('should have role="alert"', () => {
      render(
        <ImpersonationBanner
          isImpersonating={true}
          impersonatedUser={mockUser}
          onEndImpersonation={vi.fn()}
        />
      );

      expect(screen.getByRole('alert')).toBeInTheDocument();
    });

    it('should have aria-live="polite"', () => {
      render(
        <ImpersonationBanner
          isImpersonating={true}
          impersonatedUser={mockUser}
          onEndImpersonation={vi.fn()}
        />
      );

      expect(screen.getByRole('alert')).toHaveAttribute('aria-live', 'polite');
    });

    it('should have aria-hidden on decorative icons', () => {
      render(
        <ImpersonationBanner
          isImpersonating={true}
          impersonatedUser={mockUser}
          onEndImpersonation={vi.fn()}
        />
      );

      const hiddenIcons = screen.getByRole('alert').querySelectorAll('[aria-hidden="true"]');
      expect(hiddenIcons.length).toBeGreaterThanOrEqual(2);
    });
  });

  describe('styling', () => {
    it('should accept additional className', () => {
      render(
        <ImpersonationBanner
          isImpersonating={true}
          impersonatedUser={mockUser}
          onEndImpersonation={vi.fn()}
          className="custom-class"
        />
      );

      expect(screen.getByRole('alert')).toHaveClass('custom-class');
    });
  });
});

/**
 * GreetingSection Component Tests (Issue #2861)
 *
 * Test Coverage:
 * - Time-based greeting (Buongiorno, Buon pomeriggio, Buonasera, Buonanotte)
 * - User display name rendering
 * - Email fallback when displayName is null
 * - Accessibility (aria-label, heading structure)
 *
 * Target: >=85% coverage
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { GreetingSection } from '../GreetingSection';
import type { AuthUser } from '@/types';

// ============================================================================
// Test Data
// ============================================================================

const mockUserWithDisplayName: AuthUser = {
  id: 'user-1',
  email: 'mario.rossi@example.com',
  displayName: 'Mario Rossi',
  role: 'user',
};

const mockUserWithoutDisplayName: AuthUser = {
  id: 'user-2',
  email: 'luigi.verdi@example.com',
  displayName: null,
  role: 'user',
};

const mockUserWithEmptyDisplayName: AuthUser = {
  id: 'user-3',
  email: 'anna.bianchi@example.com',
  displayName: '',
  role: 'user',
};

// ============================================================================
// Test Suite
// ============================================================================

describe('GreetingSection', () => {
  // Store original Date
  const RealDate = Date;

  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    // Restore Date
    vi.useRealTimers();
    global.Date = RealDate;
  });

  // Helper to mock system time
  const mockTime = (hour: number) => {
    vi.useFakeTimers();
    const mockDate = new Date(2024, 0, 15, hour, 30, 0);
    vi.setSystemTime(mockDate);
  };

  // ============================================================================
  // Time-based Greeting Tests
  // ============================================================================

  describe('Time-based Greetings', () => {
    it('renders "Buongiorno" in the morning (5:00-11:59)', () => {
      mockTime(8);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      expect(screen.getByText(/Buongiorno/)).toBeInTheDocument();
    });

    it('renders "Buongiorno" at 5:00 AM', () => {
      mockTime(5);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      expect(screen.getByText(/Buongiorno/)).toBeInTheDocument();
    });

    it('renders "Buon pomeriggio" in the afternoon (12:00-17:59)', () => {
      mockTime(14);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      expect(screen.getByText(/Buon pomeriggio/)).toBeInTheDocument();
    });

    it('renders "Buon pomeriggio" at noon', () => {
      mockTime(12);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      expect(screen.getByText(/Buon pomeriggio/)).toBeInTheDocument();
    });

    it('renders "Buonasera" in the evening (18:00-21:59)', () => {
      mockTime(19);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      expect(screen.getByText(/Buonasera/)).toBeInTheDocument();
    });

    it('renders "Buonasera" at 18:00', () => {
      mockTime(18);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      expect(screen.getByText(/Buonasera/)).toBeInTheDocument();
    });

    it('renders "Buonanotte" at night (22:00-4:59)', () => {
      mockTime(23);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      expect(screen.getByText(/Buonanotte/)).toBeInTheDocument();
    });

    it('renders "Buonanotte" at midnight', () => {
      mockTime(0);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      expect(screen.getByText(/Buonanotte/)).toBeInTheDocument();
    });

    it('renders "Buonanotte" at 4:00 AM', () => {
      mockTime(4);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      expect(screen.getByText(/Buonanotte/)).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Display Name Tests
  // ============================================================================

  describe('Display Name Rendering', () => {
    it('renders user display name when available', () => {
      mockTime(10);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      expect(screen.getByText(/Mario Rossi/)).toBeInTheDocument();
    });

    it('renders email username when displayName is null', () => {
      mockTime(10);
      render(<GreetingSection user={mockUserWithoutDisplayName} />);

      expect(screen.getByText(/luigi.verdi/)).toBeInTheDocument();
      expect(screen.queryByText('@example.com')).not.toBeInTheDocument();
    });

    it('renders email username when displayName is empty string', () => {
      mockTime(10);
      render(<GreetingSection user={mockUserWithEmptyDisplayName} />);

      expect(screen.getByText(/anna.bianchi/)).toBeInTheDocument();
    });

    it('includes wave emoji in greeting', () => {
      mockTime(10);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      const heading = screen.getByRole('heading', { level: 1 });
      expect(heading.textContent).toContain('👋');
    });
  });

  // ============================================================================
  // Subtitle Tests
  // ============================================================================

  describe('Subtitle', () => {
    it('renders welcome subtitle', () => {
      mockTime(10);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      expect(
        screen.getByText('Benvenuto nel tuo dashboard. Ecco cosa c\'è di nuovo.')
      ).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    it('has correct aria-label on section', () => {
      mockTime(10);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      expect(screen.getByLabelText('Greeting')).toBeInTheDocument();
    });

    it('has correct data-testid', () => {
      mockTime(10);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      expect(screen.getByTestId('dashboard-greeting')).toBeInTheDocument();
    });

    it('has correct heading structure (h1)', () => {
      mockTime(10);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      const heading = screen.getByRole('heading', { level: 1 });
      expect(heading).toBeInTheDocument();
    });

    it('heading contains complete greeting', () => {
      mockTime(14);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      const heading = screen.getByRole('heading', { level: 1 });
      expect(heading.textContent).toMatch(/Buon pomeriggio.*Mario Rossi.*👋/);
    });
  });

  // ============================================================================
  // CSS Classes Tests
  // ============================================================================

  describe('Styling', () => {
    it('applies font-quicksand to heading', () => {
      mockTime(10);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      const heading = screen.getByRole('heading', { level: 1 });
      expect(heading).toHaveClass('font-quicksand');
    });

    it('applies text-muted-foreground to subtitle', () => {
      mockTime(10);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      const subtitle = screen.getByText(/Benvenuto nel tuo dashboard/);
      expect(subtitle).toHaveClass('text-muted-foreground');
    });

    it('applies space-y-2 to section', () => {
      mockTime(10);
      const { container } = render(<GreetingSection user={mockUserWithDisplayName} />);

      const section = container.querySelector('section');
      expect(section).toHaveClass('space-y-2');
    });
  });

  // ============================================================================
  // Edge Cases
  // ============================================================================

  describe('Edge Cases', () => {
    it('handles user with special characters in email', () => {
      mockTime(10);
      const userWithSpecialEmail: AuthUser = {
        id: 'user-special',
        email: 'user+test@example.com',
        displayName: null,
        role: 'user',
      };

      render(<GreetingSection user={userWithSpecialEmail} />);

      expect(screen.getByText(/user\+test/)).toBeInTheDocument();
    });

    it('handles user with undefined displayName', () => {
      mockTime(10);
      const userWithUndefinedName: AuthUser = {
        id: 'user-undefined',
        email: 'undefined.user@example.com',
        displayName: undefined,
        role: 'user',
      };

      render(<GreetingSection user={userWithUndefinedName} />);

      expect(screen.getByText(/undefined.user/)).toBeInTheDocument();
    });
  });
});

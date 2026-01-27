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
 *
 * Updated for i18n compliance (Issue #3096): Uses data-testid pattern
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
    it('renders morning greeting (5:00-11:59)', () => {
      mockTime(8);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      const greetingText = screen.getByTestId('greeting-text');
      expect(greetingText).toBeInTheDocument();
      expect(greetingText.textContent).toMatch(/Buongiorno/);
    });

    it('renders morning greeting at 5:00 AM', () => {
      mockTime(5);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      const greetingText = screen.getByTestId('greeting-text');
      expect(greetingText.textContent).toMatch(/Buongiorno/);
    });

    it('renders afternoon greeting (12:00-17:59)', () => {
      mockTime(14);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      const greetingText = screen.getByTestId('greeting-text');
      expect(greetingText.textContent).toMatch(/Buon pomeriggio/);
    });

    it('renders afternoon greeting at noon', () => {
      mockTime(12);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      const greetingText = screen.getByTestId('greeting-text');
      expect(greetingText.textContent).toMatch(/Buon pomeriggio/);
    });

    it('renders evening greeting (18:00-21:59)', () => {
      mockTime(19);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      const greetingText = screen.getByTestId('greeting-text');
      expect(greetingText.textContent).toMatch(/Buonasera/);
    });

    it('renders evening greeting at 18:00', () => {
      mockTime(18);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      const greetingText = screen.getByTestId('greeting-text');
      expect(greetingText.textContent).toMatch(/Buonasera/);
    });

    it('renders night greeting (22:00-4:59)', () => {
      mockTime(23);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      const greetingText = screen.getByTestId('greeting-text');
      expect(greetingText.textContent).toMatch(/Buonanotte/);
    });

    it('renders night greeting at midnight', () => {
      mockTime(0);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      const greetingText = screen.getByTestId('greeting-text');
      expect(greetingText.textContent).toMatch(/Buonanotte/);
    });

    it('renders night greeting at 4:00 AM', () => {
      mockTime(4);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      const greetingText = screen.getByTestId('greeting-text');
      expect(greetingText.textContent).toMatch(/Buonanotte/);
    });
  });

  // ============================================================================
  // Display Name Tests
  // ============================================================================

  describe('Display Name Rendering', () => {
    it('renders user display name when available', () => {
      mockTime(10);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      const nameElement = screen.getByTestId('greeting-name');
      expect(nameElement).toHaveTextContent('Mario Rossi');
    });

    it('renders email username when displayName is null', () => {
      mockTime(10);
      render(<GreetingSection user={mockUserWithoutDisplayName} />);

      const nameElement = screen.getByTestId('greeting-name');
      expect(nameElement).toHaveTextContent('luigi.verdi');
    });

    it('renders email username when displayName is empty string', () => {
      mockTime(10);
      render(<GreetingSection user={mockUserWithEmptyDisplayName} />);

      const nameElement = screen.getByTestId('greeting-name');
      expect(nameElement).toHaveTextContent('anna.bianchi');
    });

    it('includes wave emoji in greeting', () => {
      mockTime(10);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      const heading = screen.getByTestId('greeting-title');
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

      const subtitle = screen.getByTestId('greeting-subtitle');
      expect(subtitle).toBeInTheDocument();
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
      expect(heading).toHaveAttribute('data-testid', 'greeting-title');
    });

    it('heading contains greeting and name elements', () => {
      mockTime(14);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      const heading = screen.getByTestId('greeting-title');
      expect(heading).toContainElement(screen.getByTestId('greeting-text'));
      expect(heading).toContainElement(screen.getByTestId('greeting-name'));
    });
  });

  // ============================================================================
  // CSS Classes Tests
  // ============================================================================

  describe('Styling', () => {
    it('applies font-quicksand to heading', () => {
      mockTime(10);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      const heading = screen.getByTestId('greeting-title');
      expect(heading).toHaveClass('font-quicksand');
    });

    it('applies text-muted-foreground to subtitle', () => {
      mockTime(10);
      render(<GreetingSection user={mockUserWithDisplayName} />);

      const subtitle = screen.getByTestId('greeting-subtitle');
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

      const nameElement = screen.getByTestId('greeting-name');
      expect(nameElement).toHaveTextContent('user+test');
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

      const nameElement = screen.getByTestId('greeting-name');
      expect(nameElement).toHaveTextContent('undefined.user');
    });
  });
});

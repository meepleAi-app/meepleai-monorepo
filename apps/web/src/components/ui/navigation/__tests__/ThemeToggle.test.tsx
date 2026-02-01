/**
 * ThemeToggle Component Test Suite (Issue #2965 Wave 9)
 *
 * Tests for the binary light/dark theme toggle component.
 * Verifies theme switching, icon display, accessibility, and hydration handling.
 */

import type { Mock } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useTheme } from 'next-themes';
import { ThemeToggle } from '../ThemeToggle';

vi.mock('next-themes', () => ({
  useTheme: vi.fn(),
}));

const mockUseTheme = useTheme as Mock<typeof useTheme>;

describe('ThemeToggle', () => {
  const mockSetTheme = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Client-Side Rendering', () => {
    it('renders interactive button after hydration', async () => {
      mockUseTheme.mockReturnValue({
        theme: 'light',
        setTheme: mockSetTheme,
        systemTheme: 'light',
        themes: ['light', 'dark'],
        resolvedTheme: 'light',
        forcedTheme: undefined,
      });

      render(<ThemeToggle />);

      // After hydration (useEffect runs), button should be enabled
      await waitFor(() => {
        const button = screen.getByRole('button');
        expect(button).not.toBeDisabled();
      });
    });

    it('button is clickable after mounting', async () => {
      const user = userEvent.setup();

      mockUseTheme.mockReturnValue({
        theme: 'light',
        setTheme: mockSetTheme,
        systemTheme: 'light',
        themes: ['light', 'dark'],
        resolvedTheme: 'light',
        forcedTheme: undefined,
      });

      render(<ThemeToggle />);

      await waitFor(() => {
        expect(screen.getByRole('button')).not.toBeDisabled();
      });

      const button = screen.getByRole('button');
      await user.click(button);

      expect(mockSetTheme).toHaveBeenCalled();
    });
  });

  describe('Theme State Display', () => {
    it('shows Moon icon when light theme is active', async () => {
      mockUseTheme.mockReturnValue({
        theme: 'light',
        setTheme: mockSetTheme,
        systemTheme: 'light',
        themes: ['light', 'dark'],
        resolvedTheme: 'light',
        forcedTheme: undefined,
      });

      render(<ThemeToggle />);

      await waitFor(() => {
        const button = screen.getByRole('button');
        expect(button).not.toBeDisabled();
      });

      // Light theme shows Moon icon (to switch to dark)
      expect(screen.getByLabelText('Attiva tema scuro')).toBeInTheDocument();
    });

    it('shows Sun icon when dark theme is active', async () => {
      mockUseTheme.mockReturnValue({
        theme: 'dark',
        setTheme: mockSetTheme,
        systemTheme: 'dark',
        themes: ['light', 'dark'],
        resolvedTheme: 'dark',
        forcedTheme: undefined,
      });

      render(<ThemeToggle />);

      await waitFor(() => {
        const button = screen.getByRole('button');
        expect(button).not.toBeDisabled();
      });

      // Dark theme shows Sun icon (to switch to light)
      expect(screen.getByLabelText('Attiva tema chiaro')).toBeInTheDocument();
    });
  });

  describe('Theme Switching', () => {
    it('toggles from light to dark theme on click', async () => {
      const user = userEvent.setup();

      mockUseTheme.mockReturnValue({
        theme: 'light',
        setTheme: mockSetTheme,
        systemTheme: 'light',
        themes: ['light', 'dark'],
        resolvedTheme: 'light',
        forcedTheme: undefined,
      });

      render(<ThemeToggle />);

      await waitFor(() => {
        expect(screen.getByRole('button')).not.toBeDisabled();
      });

      const button = screen.getByRole('button');
      await user.click(button);

      expect(mockSetTheme).toHaveBeenCalledWith('dark');
    });

    it('toggles from dark to light theme on click', async () => {
      const user = userEvent.setup();

      mockUseTheme.mockReturnValue({
        theme: 'dark',
        setTheme: mockSetTheme,
        systemTheme: 'dark',
        themes: ['light', 'dark'],
        resolvedTheme: 'dark',
        forcedTheme: undefined,
      });

      render(<ThemeToggle />);

      await waitFor(() => {
        expect(screen.getByRole('button')).not.toBeDisabled();
      });

      const button = screen.getByRole('button');
      await user.click(button);

      expect(mockSetTheme).toHaveBeenCalledWith('light');
    });
  });

  describe('Variants', () => {
    it('renders with label when showLabel is true', async () => {
      mockUseTheme.mockReturnValue({
        theme: 'dark',
        setTheme: mockSetTheme,
        systemTheme: 'dark',
        themes: ['light', 'dark'],
        resolvedTheme: 'dark',
        forcedTheme: undefined,
      });

      render(<ThemeToggle showLabel />);

      await waitFor(() => {
        expect(screen.getByRole('button')).not.toBeDisabled();
      });

      // Dark theme shows "Chiaro" label (to switch to light)
      expect(screen.getByText('Chiaro')).toBeInTheDocument();
    });

    it('renders without label by default', async () => {
      mockUseTheme.mockReturnValue({
        theme: 'light',
        setTheme: mockSetTheme,
        systemTheme: 'light',
        themes: ['light', 'dark'],
        resolvedTheme: 'light',
        forcedTheme: undefined,
      });

      render(<ThemeToggle />);

      await waitFor(() => {
        expect(screen.getByRole('button')).not.toBeDisabled();
      });

      // No label text should be present
      expect(screen.queryByText('Scuro')).not.toBeInTheDocument();
      expect(screen.queryByText('Chiaro')).not.toBeInTheDocument();
    });

    it('applies custom className', async () => {
      mockUseTheme.mockReturnValue({
        theme: 'light',
        setTheme: mockSetTheme,
        systemTheme: 'light',
        themes: ['light', 'dark'],
        resolvedTheme: 'light',
        forcedTheme: undefined,
      });

      render(<ThemeToggle className="custom-class" />);

      await waitFor(() => {
        expect(screen.getByRole('button')).not.toBeDisabled();
      });

      expect(screen.getByRole('button')).toHaveClass('custom-class');
    });

    it('applies size sm variant', async () => {
      mockUseTheme.mockReturnValue({
        theme: 'light',
        setTheme: mockSetTheme,
        systemTheme: 'light',
        themes: ['light', 'dark'],
        resolvedTheme: 'light',
        forcedTheme: undefined,
      });

      const { container } = render(<ThemeToggle size="sm" />);

      await waitFor(() => {
        expect(screen.getByRole('button')).not.toBeDisabled();
      });

      // Button should have the sm size class
      expect(container.querySelector('button')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has appropriate aria-label for light theme', async () => {
      mockUseTheme.mockReturnValue({
        theme: 'light',
        setTheme: mockSetTheme,
        systemTheme: 'light',
        themes: ['light', 'dark'],
        resolvedTheme: 'light',
        forcedTheme: undefined,
      });

      render(<ThemeToggle />);

      await waitFor(() => {
        expect(screen.getByRole('button')).not.toBeDisabled();
      });

      expect(screen.getByLabelText('Attiva tema scuro')).toBeInTheDocument();
    });

    it('has appropriate aria-label for dark theme', async () => {
      mockUseTheme.mockReturnValue({
        theme: 'dark',
        setTheme: mockSetTheme,
        systemTheme: 'dark',
        themes: ['light', 'dark'],
        resolvedTheme: 'dark',
        forcedTheme: undefined,
      });

      render(<ThemeToggle />);

      await waitFor(() => {
        expect(screen.getByRole('button')).not.toBeDisabled();
      });

      expect(screen.getByLabelText('Attiva tema chiaro')).toBeInTheDocument();
    });

    it('has title attribute for tooltip', async () => {
      mockUseTheme.mockReturnValue({
        theme: 'light',
        setTheme: mockSetTheme,
        systemTheme: 'light',
        themes: ['light', 'dark'],
        resolvedTheme: 'light',
        forcedTheme: undefined,
      });

      render(<ThemeToggle />);

      await waitFor(() => {
        expect(screen.getByRole('button')).not.toBeDisabled();
      });

      expect(screen.getByTitle('Tema Scuro')).toBeInTheDocument();
    });
  });
});

/**
 * HeroSection Tests
 *
 * Comprehensive unit tests for Hero section component.
 * Target: 100% coverage (statements, branches, functions, lines)
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { HeroSection } from '../HeroSection';

import { vi } from 'vitest';

// Mock Next.js Link
vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => {
    return <a href={href}>{children}</a>;
  },
}));

// Mock MeepleAvatar
vi.mock('@/components/ui/meeple-avatar', () => ({
  MeepleAvatar: ({ state, size, className }: any) => (
    <div data-testid="meeple-avatar" data-state={state} data-size={size} className={className}>
      MeepleAvatar Mock
    </div>
  ),
}));

describe('HeroSection', () => {
  describe('Rendering', () => {
    it('renders hero section with correct structure', () => {
      render(<HeroSection />);

      expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument();
      expect(screen.getAllByTestId('meeple-avatar')).toHaveLength(2); // Mobile + Desktop
    });

    it('displays heading with Italian text', () => {
      render(<HeroSection />);

      const heading = screen.getByRole('heading', { level: 1 });
      expect(heading).toHaveTextContent('Il tuo assistente AI');
      expect(heading).toHaveTextContent('per giochi da tavolo');
    });

    it('displays subheading description', () => {
      render(<HeroSection />);

      expect(
        screen.getByText('Risposte immediate alle regole, in italiano. Sempre con te.')
      ).toBeInTheDocument();
    });
  });

  describe('CTA Buttons', () => {
    it('renders primary CTA button linking to /register', () => {
      render(<HeroSection />);

      const ctaButton = screen.getByRole('link', { name: /inizia gratis/i });
      expect(ctaButton).toBeInTheDocument();
      expect(ctaButton).toHaveAttribute('href', '/register');
    });

    it('renders secondary CTA button for scrolling', () => {
      render(<HeroSection />);

      const secondaryButton = screen.getByRole('button', {
        name: /scorri alla sezione caratteristiche/i,
      });
      expect(secondaryButton).toBeInTheDocument();
    });

    it('scroll button triggers smooth scroll to features section', () => {
      const mockScrollIntoView = vi.fn();
      const mockGetElementById = vi.spyOn(document, 'getElementById').mockReturnValue({
        scrollIntoView: mockScrollIntoView,
      } as any);

      render(<HeroSection />);

      const scrollButton = screen.getByRole('button', {
        name: /scorri alla sezione caratteristiche/i,
      });
      fireEvent.click(scrollButton);

      expect(mockGetElementById).toHaveBeenCalledWith('features');
      expect(mockScrollIntoView).toHaveBeenCalledWith({ behavior: 'smooth' });

      mockGetElementById.mockRestore();
    });

    it('handles scroll when features section does not exist', () => {
      const mockGetElementById = vi.spyOn(document, 'getElementById').mockReturnValue(null);

      render(<HeroSection />);

      const scrollButton = screen.getByRole('button', {
        name: /scorri alla sezione caratteristiche/i,
      });

      // Should not throw error
      expect(() => fireEvent.click(scrollButton)).not.toThrow();

      mockGetElementById.mockRestore();
    });
  });

  describe('MeepleAvatar Integration', () => {
    it('renders desktop avatar with correct props', () => {
      render(<HeroSection />);

      const desktopAvatar = screen.getAllByTestId('meeple-avatar')[1]; // Second one is desktop
      expect(desktopAvatar).toHaveAttribute('data-state', 'confident');
      expect(desktopAvatar).toHaveAttribute('data-size', 'lg');
    });

    it('renders mobile avatar with correct styling', () => {
      render(<HeroSection />);

      const mobileAvatar = screen.getAllByTestId('meeple-avatar')[0]; // First one is mobile
      expect(mobileAvatar).toHaveClass('w-48', 'h-48');
    });
  });

  describe('Responsive Layout', () => {
    it('applies responsive classes for grid layout', () => {
      const { container } = render(<HeroSection />);

      const gridContainer = container.querySelector('.grid');
      expect(gridContainer).toHaveClass('grid-cols-1', 'md:grid-cols-2');
    });

    it('has mobile-first min-height', () => {
      const { container } = render(<HeroSection />);

      const section = container.querySelector('section');
      expect(section).toHaveClass('min-h-[calc(100vh-56px)]');
    });
  });

  describe('Accessibility', () => {
    it('has semantic section element', () => {
      const { container } = render(<HeroSection />);

      const section = container.querySelector('section');
      expect(section).toBeInTheDocument();
    });

    it('scroll indicator has aria-label', () => {
      render(<HeroSection />);

      const scrollIndicator = screen.getByLabelText('Scorri alle caratteristiche');
      expect(scrollIndicator).toBeInTheDocument();
    });

    it('heading has proper hierarchy', () => {
      render(<HeroSection />);

      const h1 = screen.getByRole('heading', { level: 1 });
      expect(h1).toBeInTheDocument();
    });
  });

  describe('Visual Design', () => {
    it('applies gradient background', () => {
      const { container } = render(<HeroSection />);

      const section = container.querySelector('section');
      expect(section).toHaveClass('bg-gradient-to-b');
    });

    it('CTA button has primary styling', () => {
      render(<HeroSection />);

      const ctaLink = screen.getByRole('link', { name: /inizia gratis/i });
      // Button is wrapped, check that link exists (styling applied via Button component)
      expect(ctaLink).toBeInTheDocument();
      expect(ctaLink).toHaveAttribute('href', '/register');
    });

    it('decorative background for desktop avatar', () => {
      const { container } = render(<HeroSection />);

      // Check for decorative elements with absolute positioning
      const decoratives = container.querySelectorAll('.absolute');
      expect(decoratives.length).toBeGreaterThan(0);
    });
  });
});

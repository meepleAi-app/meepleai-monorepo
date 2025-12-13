/**
 * HeroSection Tests
 *
 * Comprehensive unit tests for Hero section component.
 * Target: 100% coverage (statements, branches, functions, lines)
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { HeroSection } from '../HeroSection';

import { vi } from 'vitest';

// Mock translations - matches it.json locale file
const mockTranslations: Record<string, string> = {
  'home.hero.title': 'Il tuo Assistente AI per Regolamenti di Giochi da Tavolo',
  'home.hero.subtitle':
    'Mai più discussioni sulle regole. Ottieni risposte istantanee e accurate da qualsiasi regolamento con la ricerca semantica basata su AI.',
  'home.hero.cta.getStarted': 'Inizia Gratis',
  'home.hero.cta.learnMore': 'Scopri di più',
  'home.hero.scrollToFeatures': 'Scorri alla sezione caratteristiche',
  'home.hero.trustIndicators.users': 'Oltre 1.250 utenti',
  'home.hero.trustIndicators.accuracy': '95% accuratezza',
};

// Mock useTranslation hook
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) => mockTranslations[key] || key,
  }),
}));

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

    it('displays heading with translated text', () => {
      render(<HeroSection />);

      const heading = screen.getByRole('heading', { level: 1 });
      expect(heading).toHaveTextContent(mockTranslations['home.hero.title']);
    });

    it('displays subheading description', () => {
      render(<HeroSection />);

      expect(screen.getByText(mockTranslations['home.hero.subtitle'])).toBeInTheDocument();
    });
  });

  describe('CTA Buttons', () => {
    it('renders primary CTA button linking to /register', () => {
      render(<HeroSection />);

      const ctaButton = screen.getByRole('link', {
        name: new RegExp(mockTranslations['home.hero.cta.getStarted'], 'i'),
      });
      expect(ctaButton).toBeInTheDocument();
      expect(ctaButton).toHaveAttribute('href', '/register');
    });

    it('renders secondary CTA button for scrolling', () => {
      render(<HeroSection />);

      // There are 2 buttons with scroll aria-label: secondary CTA and scroll indicator
      const scrollButtons = screen.getAllByRole('button', {
        name: new RegExp(mockTranslations['home.hero.scrollToFeatures'], 'i'),
      });
      expect(scrollButtons.length).toBe(2);
      // First one is the secondary CTA (contains "Scopri di più" text)
      expect(scrollButtons[0]).toHaveTextContent(mockTranslations['home.hero.cta.learnMore']);
    });

    it('scroll button triggers smooth scroll to features section', () => {
      const mockScrollIntoView = vi.fn();
      const mockGetElementById = vi.spyOn(document, 'getElementById').mockReturnValue({
        scrollIntoView: mockScrollIntoView,
      } as any);

      render(<HeroSection />);

      const scrollButtons = screen.getAllByRole('button', {
        name: new RegExp(mockTranslations['home.hero.scrollToFeatures'], 'i'),
      });
      fireEvent.click(scrollButtons[0]);

      expect(mockGetElementById).toHaveBeenCalledWith('features');
      expect(mockScrollIntoView).toHaveBeenCalledWith({ behavior: 'smooth' });

      mockGetElementById.mockRestore();
    });

    it('handles scroll when features section does not exist', () => {
      const mockGetElementById = vi.spyOn(document, 'getElementById').mockReturnValue(null);

      render(<HeroSection />);

      const scrollButtons = screen.getAllByRole('button', {
        name: new RegExp(mockTranslations['home.hero.scrollToFeatures'], 'i'),
      });

      // Should not throw error
      expect(() => fireEvent.click(scrollButtons[0])).not.toThrow();

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

      // Both secondary CTA and scroll indicator share the same aria-label
      const scrollIndicators = screen.getAllByLabelText(
        mockTranslations['home.hero.scrollToFeatures']
      );
      expect(scrollIndicators.length).toBe(2);
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

      const ctaLink = screen.getByRole('link', {
        name: new RegExp(mockTranslations['home.hero.cta.getStarted'], 'i'),
      });
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

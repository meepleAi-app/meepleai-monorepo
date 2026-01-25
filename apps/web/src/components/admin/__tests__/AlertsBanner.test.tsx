import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { AlertsBanner } from '../AlertsBanner';
import * as nextNavigation from 'next/navigation';
import * as framerMotion from 'framer-motion';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: vi.fn(),
}));

// Mock framer-motion
vi.mock('framer-motion', async () => {
  const actual = await vi.importActual<typeof import('framer-motion')>('framer-motion');
  return {
    ...actual,
    motion: {
      div: ({ children, className, ...props }: any) => (
        <div className={className} {...props}>
          {children}
        </div>
      ),
      p: ({ children, className, ...props }: any) => (
        <p className={className} {...props}>
          {children}
        </p>
      ),
    },
    AnimatePresence: ({ children }: any) => <>{children}</>,
    useReducedMotion: vi.fn(() => false),
  };
});

describe('AlertsBanner', () => {
  const mockPush = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(nextNavigation.useRouter).mockReturnValue({
      push: mockPush,
      replace: vi.fn(),
      prefetch: vi.fn(),
      back: vi.fn(),
      forward: vi.fn(),
      refresh: vi.fn(),
    } as any);
  });

  describe('Rendering', () => {
    it('renders all healthy state correctly', () => {
      render(
        <AlertsBanner criticalCount={0} healthyServices={10} totalServices={10} />
      );

      expect(screen.getByTestId('alerts-primary-message')).toHaveTextContent('Tutti i sistemi operativi');
      expect(screen.getByTestId('alerts-secondary-message')).toHaveTextContent('10/10 servizi in salute');
      expect(screen.getByRole('button', { name: /Visualizza pannello alert di sistema/i })).toBeInTheDocument();
    });

    it('renders has issues state correctly', () => {
      render(
        <AlertsBanner criticalCount={5} healthyServices={8} totalServices={10} />
      );

      expect(screen.getByTestId('alerts-primary-message')).toHaveTextContent('5 alert critici attivi');
      expect(screen.getByTestId('alerts-secondary-message')).toHaveTextContent('8/10 servizi operativi');
    });

    it('renders button with correct navigation label for issues state', () => {
      render(
        <AlertsBanner criticalCount={3} healthyServices={7} totalServices={10} />
      );

      const button = screen.getByRole('button', { name: /Visualizza pannello alert di sistema \(3 critici attivi\)/i });
      expect(button).toBeInTheDocument();
    });

    it('handles singular alert message correctly', () => {
      render(
        <AlertsBanner criticalCount={1} healthyServices={9} totalServices={10} />
      );

      expect(screen.getByTestId('alerts-primary-message')).toHaveTextContent('1 alert critico attivo');
    });

    it('handles plural alerts message correctly', () => {
      render(
        <AlertsBanner criticalCount={2} healthyServices={8} totalServices={10} />
      );

      expect(screen.getByTestId('alerts-primary-message')).toHaveTextContent('2 alert critici attivi');
    });
  });

  describe('State Logic', () => {
    it('shows issues state when critical count > 0', () => {
      render(
        <AlertsBanner criticalCount={1} healthyServices={10} totalServices={10} />
      );

      expect(screen.getByTestId('alerts-primary-message').textContent).toMatch(/alert critic/i);
    });

    it('shows issues state when healthy services < total services', () => {
      render(
        <AlertsBanner criticalCount={0} healthyServices={8} totalServices={10} />
      );

      expect(screen.getByTestId('alerts-secondary-message')).toHaveTextContent('8/10 servizi operativi');
    });

    it('shows healthy state when all conditions met', () => {
      render(
        <AlertsBanner criticalCount={0} healthyServices={10} totalServices={10} />
      );

      expect(screen.getByTestId('alerts-primary-message')).toHaveTextContent('Tutti i sistemi operativi');
    });
  });

  describe('Edge Cases', () => {
    it('handles zero services correctly', () => {
      render(
        <AlertsBanner criticalCount={0} healthyServices={0} totalServices={0} />
      );

      expect(screen.getByTestId('alerts-primary-message')).toHaveTextContent('Nessun servizio configurato');
      expect(screen.getByTestId('alerts-secondary-message')).toHaveTextContent('Configura i servizi da monitorare');
    });

    it('handles negative critical count (normalized to 0)', () => {
      render(
        <AlertsBanner criticalCount={-5} healthyServices={10} totalServices={10} />
      );

      expect(screen.getByTestId('alerts-primary-message')).toHaveTextContent('Tutti i sistemi operativi');
    });

    it('handles negative healthy services (normalized to 0)', () => {
      render(
        <AlertsBanner criticalCount={0} healthyServices={-5} totalServices={10} />
      );

      expect(screen.getByTestId('alerts-secondary-message')).toHaveTextContent('0/10 servizi operativi');
    });

    it('handles healthy services > total services (clamped to total)', () => {
      render(
        <AlertsBanner criticalCount={0} healthyServices={15} totalServices={10} />
      );

      expect(screen.getByTestId('alerts-secondary-message')).toHaveTextContent('10/10 servizi in salute');
    });

    it('handles negative total services (normalized to 0)', () => {
      render(
        <AlertsBanner criticalCount={0} healthyServices={0} totalServices={-10} />
      );

      expect(screen.getByTestId('alerts-primary-message')).toHaveTextContent('Nessun servizio configurato');
    });
  });

  describe('Styling', () => {
    it('applies has issues styling classes', () => {
      const { container } = render(
        <AlertsBanner criticalCount={5} healthyServices={8} totalServices={10} />
      );

      const banner = container.firstChild as HTMLElement;
      expect(banner.className).toContain('border-amber-200');
    });

    it('applies all healthy styling classes', () => {
      const { container } = render(
        <AlertsBanner criticalCount={0} healthyServices={10} totalServices={10} />
      );

      const banner = container.firstChild as HTMLElement;
      expect(banner.className).toContain('border-green-200');
    });

    it('applies custom className prop', () => {
      const { container } = render(
        <AlertsBanner
          criticalCount={0}
          healthyServices={10}
          totalServices={10}
          className="custom-class"
        />
      );

      const banner = container.firstChild as HTMLElement;
      expect(banner.className).toContain('custom-class');
    });

    it('includes dark mode classes', () => {
      const { container } = render(
        <AlertsBanner criticalCount={5} healthyServices={8} totalServices={10} />
      );

      const banner = container.firstChild as HTMLElement;
      expect(banner.className).toContain('dark:border-amber-800');
      expect(banner.className).toContain('dark:bg-stone-900');
    });
  });

  describe('Navigation', () => {
    it('navigates to /admin/alerts on button click', async () => {
      const user = userEvent.setup();
      render(
        <AlertsBanner criticalCount={0} healthyServices={10} totalServices={10} />
      );

      const button = screen.getByRole('button', { name: /Visualizza pannello alert/i });
      await user.click(button);

      expect(mockPush).toHaveBeenCalledWith('/admin/alerts');
    });

    it('navigates to /admin/alerts from issues state', async () => {
      const user = userEvent.setup();
      render(
        <AlertsBanner criticalCount={5} healthyServices={8} totalServices={10} />
      );

      const button = screen.getByRole('button', { name: /Visualizza pannello alert/i });
      await user.click(button);

      expect(mockPush).toHaveBeenCalledWith('/admin/alerts');
    });
  });

  describe('Accessibility', () => {
    it('includes aria-label for button in healthy state', () => {
      render(
        <AlertsBanner criticalCount={0} healthyServices={10} totalServices={10} />
      );

      const button = screen.getByRole('button', { name: 'Visualizza pannello alert di sistema' });
      expect(button).toBeInTheDocument();
    });

    it('includes aria-label with critical count in issues state', () => {
      render(
        <AlertsBanner criticalCount={3} healthyServices={7} totalServices={10} />
      );

      const button = screen.getByRole('button', {
        name: 'Visualizza pannello alert di sistema (3 critici attivi)',
      });
      expect(button).toBeInTheDocument();
    });

    it('marks decorative elements as aria-hidden', () => {
      const { container } = render(
        <AlertsBanner criticalCount={5} healthyServices={8} totalServices={10} />
      );

      const decorativeElements = container.querySelectorAll('[aria-hidden="true"]');
      expect(decorativeElements.length).toBeGreaterThan(0);
    });
  });

  describe('Reduced Motion', () => {
    it('disables animations when reduced motion is preferred', () => {
      vi.mocked(framerMotion.useReducedMotion).mockReturnValue(true);

      const { container } = render(
        <AlertsBanner criticalCount={5} healthyServices={8} totalServices={10} />
      );

      // Blur decoration should not render with reduced motion
      const blurDecoration = container.querySelector('.blur-3xl');
      expect(blurDecoration).not.toBeInTheDocument();
    });

    it('shows pulsating dot without animation when reduced motion', () => {
      vi.mocked(framerMotion.useReducedMotion).mockReturnValue(true);

      const { container } = render(
        <AlertsBanner criticalCount={5} healthyServices={8} totalServices={10} />
      );

      // Pulsating ring should not render with reduced motion
      const pulsatingRing = container.querySelector('.animate-ping');
      expect(pulsatingRing).not.toBeInTheDocument();

      // Static dot should still be present
      const staticDot = container.querySelector('.bg-amber-500');
      expect(staticDot).toBeInTheDocument();
    });
  });

  describe('Component Integration', () => {
    it('renders without crashing', () => {
      expect(() => {
        render(
          <AlertsBanner criticalCount={0} healthyServices={10} totalServices={10} />
        );
      }).not.toThrow();
    });

    it('updates when props change', () => {
      const { rerender } = render(
        <AlertsBanner criticalCount={0} healthyServices={10} totalServices={10} />
      );

      expect(screen.getByTestId('alerts-primary-message')).toHaveTextContent('Tutti i sistemi operativi');

      rerender(
        <AlertsBanner criticalCount={5} healthyServices={8} totalServices={10} />
      );

      expect(screen.getByTestId('alerts-primary-message')).toHaveTextContent('5 alert critici attivi');
    });
  });
});

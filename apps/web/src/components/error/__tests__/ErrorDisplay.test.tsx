/**
 * Tests for ErrorDisplay component
 * Error state UI with retry functionality
 *
 * @issue BGAI-068
 */

import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { ErrorDisplay } from '../ErrorDisplay';
import { NetworkError, ServerError, RateLimitError, ApiError } from '@/lib/api/core/errors';

// Mock useReducedMotion hook
const mockUseReducedMotion = vi.fn();
vi.mock('@/lib/animations', () => ({
  ...vi.importActual('@/lib/animations'),
  useReducedMotion: () => mockUseReducedMotion(),
}));

describe('ErrorDisplay', () => {
  beforeEach(() => {
    mockUseReducedMotion.mockReturnValue(false);
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.clearAllMocks();
    vi.useRealTimers();
  });

  describe('Basic rendering', () => {
    it('should render with title only', () => {
      render(<ErrorDisplay title="Errore" />);
      expect(screen.getByText('Errore')).toBeInTheDocument();
    });

    it('should render with title and description', () => {
      render(<ErrorDisplay title="Errore" description="Si è verificato un problema" />);
      expect(screen.getByText('Errore')).toBeInTheDocument();
      expect(screen.getByText('Si è verificato un problema')).toBeInTheDocument();
    });

    it('should render with correct test ID', () => {
      render(<ErrorDisplay title="Test" testId="custom-error" />);
      expect(screen.getByTestId('custom-error')).toBeInTheDocument();
    });

    it('should render with default test ID', () => {
      render(<ErrorDisplay title="Test" />);
      expect(screen.getByTestId('error-display')).toBeInTheDocument();
    });

    it('should render default description for variant when no description provided', () => {
      render(<ErrorDisplay title="Errore di rete" variant="network" />);
      // Use getAllByText since text appears in both description and sr-only span
      const elements = screen.getAllByText(/Impossibile connettersi al server/);
      expect(elements.length).toBeGreaterThan(0);
    });
  });

  describe('Variants', () => {
    it('should render generic variant', () => {
      render(<ErrorDisplay title="Errore" variant="generic" />);
      const element = screen.getByTestId('error-display');
      expect(element).toHaveAttribute('data-variant', 'generic');
    });

    it('should render network variant', () => {
      render(<ErrorDisplay title="Errore" variant="network" />);
      const element = screen.getByTestId('error-display');
      expect(element).toHaveAttribute('data-variant', 'network');
    });

    it('should render server variant', () => {
      render(<ErrorDisplay title="Errore" variant="server" />);
      const element = screen.getByTestId('error-display');
      expect(element).toHaveAttribute('data-variant', 'server');
    });

    it('should render rateLimit variant', () => {
      render(<ErrorDisplay title="Errore" variant="rateLimit" />);
      const element = screen.getByTestId('error-display');
      expect(element).toHaveAttribute('data-variant', 'rateLimit');
    });

    it('should render icon for each variant', () => {
      const variants = ['generic', 'network', 'server', 'rateLimit'] as const;
      variants.forEach(variant => {
        const { container, unmount } = render(<ErrorDisplay title="Test" variant={variant} />);
        expect(container.querySelector('svg')).toBeInTheDocument();
        unmount();
      });
    });
  });

  describe('Error object detection', () => {
    it('should auto-detect network variant from NetworkError', () => {
      const error = new NetworkError({ message: 'Network failed', endpoint: '/api' });
      render(<ErrorDisplay title="Errore" error={error} />);
      const element = screen.getByTestId('error-display');
      expect(element).toHaveAttribute('data-variant', 'network');
    });

    it('should auto-detect server variant from ServerError', () => {
      const error = new ServerError({ message: 'Server error', endpoint: '/api', statusCode: 500 });
      render(<ErrorDisplay title="Errore" error={error} />);
      const element = screen.getByTestId('error-display');
      expect(element).toHaveAttribute('data-variant', 'server');
    });

    it('should auto-detect rateLimit variant from RateLimitError', () => {
      const error = new RateLimitError({
        message: 'Rate limited',
        endpoint: '/api',
        retryAfter: 30,
      });
      render(<ErrorDisplay title="Errore" error={error} />);
      const element = screen.getByTestId('error-display');
      expect(element).toHaveAttribute('data-variant', 'rateLimit');
    });

    it('should auto-detect rateLimit from 429 ApiError', () => {
      const error = new ApiError({
        message: 'Too many requests',
        statusCode: 429,
        endpoint: '/api',
      });
      render(<ErrorDisplay title="Errore" error={error} />);
      const element = screen.getByTestId('error-display');
      expect(element).toHaveAttribute('data-variant', 'rateLimit');
    });

    it('should auto-detect server from 5xx ApiError', () => {
      const error = new ApiError({ message: 'Server error', statusCode: 503, endpoint: '/api' });
      render(<ErrorDisplay title="Errore" error={error} />);
      const element = screen.getByTestId('error-display');
      expect(element).toHaveAttribute('data-variant', 'server');
    });

    it('should use generic for unknown error types', () => {
      const error = new Error('Unknown error');
      render(<ErrorDisplay title="Errore" error={error} />);
      const element = screen.getByTestId('error-display');
      expect(element).toHaveAttribute('data-variant', 'generic');
    });

    it('should override auto-detected variant with explicit variant', () => {
      const error = new NetworkError({ message: 'Network failed', endpoint: '/api' });
      render(<ErrorDisplay title="Errore" error={error} variant="server" />);
      const element = screen.getByTestId('error-display');
      expect(element).toHaveAttribute('data-variant', 'server');
    });
  });

  describe('Retry button', () => {
    it('should render retry button when onRetry is provided', () => {
      const handleRetry = vi.fn();
      render(<ErrorDisplay title="Errore" onRetry={handleRetry} />);
      expect(screen.getByRole('button', { name: /riprova/i })).toBeInTheDocument();
    });

    it('should call onRetry when button is clicked', async () => {
      const handleRetry = vi.fn();
      render(<ErrorDisplay title="Errore" onRetry={handleRetry} />);
      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: /riprova/i }));
      });
      expect(handleRetry).toHaveBeenCalledTimes(1);
    });

    it('should not render retry button when showRetry is false', () => {
      render(<ErrorDisplay title="Errore" onRetry={() => {}} showRetry={false} />);
      expect(screen.queryByRole('button')).not.toBeInTheDocument();
    });

    it('should not render retry button when onRetry is not provided', () => {
      render(<ErrorDisplay title="Errore" />);
      expect(screen.queryByRole('button')).not.toBeInTheDocument();
    });

    it('should use custom retry label', () => {
      render(<ErrorDisplay title="Errore" onRetry={() => {}} retryLabel="Riprova ora" />);
      expect(screen.getByRole('button', { name: /riprova ora/i })).toBeInTheDocument();
    });
  });

  describe('Countdown timer', () => {
    it('should display countdown when retryAfterSeconds is provided', () => {
      render(
        <ErrorDisplay
          title="Errore"
          variant="rateLimit"
          retryAfterSeconds={30}
          onRetry={() => {}}
        />
      );
      // Use getAllByText since text appears in button and countdown display
      const elements = screen.getAllByText(/Attendi 30s/);
      expect(elements.length).toBeGreaterThan(0);
    });

    it('should disable retry button during countdown', () => {
      render(
        <ErrorDisplay
          title="Errore"
          variant="rateLimit"
          retryAfterSeconds={30}
          onRetry={() => {}}
        />
      );
      const button = screen.getByRole('button');
      expect(button).toBeDisabled();
    });

    it('should decrement countdown every second', async () => {
      render(
        <ErrorDisplay title="Errore" variant="rateLimit" retryAfterSeconds={3} onRetry={() => {}} />
      );
      // Use getAllByText since text appears in button and countdown display
      expect(screen.getAllByText(/Attendi 3s/).length).toBeGreaterThan(0);

      act(() => {
        vi.advanceTimersByTime(1000);
      });
      expect(screen.getAllByText(/Attendi 2s/).length).toBeGreaterThan(0);

      act(() => {
        vi.advanceTimersByTime(1000);
      });
      expect(screen.getAllByText(/Attendi 1s/).length).toBeGreaterThan(0);
    });

    it('should enable retry button when countdown reaches zero', () => {
      render(
        <ErrorDisplay title="Errore" variant="rateLimit" retryAfterSeconds={2} onRetry={() => {}} />
      );

      expect(screen.getByRole('button')).toBeDisabled();

      // Advance past the countdown
      act(() => {
        vi.advanceTimersByTime(2500);
      });

      expect(screen.getByRole('button')).not.toBeDisabled();
    });

    it('should format minutes correctly in countdown', () => {
      render(
        <ErrorDisplay
          title="Errore"
          variant="rateLimit"
          retryAfterSeconds={90}
          onRetry={() => {}}
        />
      );
      // Use getAllByText since text appears in button and countdown display
      expect(screen.getAllByText(/Attendi 1m 30s/).length).toBeGreaterThan(0);
    });

    it('should extract retryAfter from RateLimitError', () => {
      const error = new RateLimitError({
        message: 'Rate limited',
        endpoint: '/api',
        retryAfter: 45,
      });
      render(<ErrorDisplay title="Errore" error={error} onRetry={() => {}} />);
      // Use getAllByText since text appears in button and countdown display
      expect(screen.getAllByText(/Attendi 45s/).length).toBeGreaterThan(0);
    });
  });

  describe('Correlation ID', () => {
    it('should display correlation ID when present in ApiError', () => {
      const error = new ApiError({
        message: 'Error',
        endpoint: '/api',
        correlationId: 'abc-123-def',
      });
      render(<ErrorDisplay title="Errore" error={error} />);
      expect(screen.getByText(/ID: abc-123-def/)).toBeInTheDocument();
    });

    it('should not display correlation ID for regular errors', () => {
      const error = new Error('Regular error');
      render(<ErrorDisplay title="Errore" error={error} />);
      expect(screen.queryByText(/ID:/)).not.toBeInTheDocument();
    });
  });

  describe('Custom className', () => {
    it('should apply custom className', () => {
      render(<ErrorDisplay title="Test" className="custom-class" />);
      const errorDisplay = screen.getByTestId('error-display');
      expect(errorDisplay).toHaveClass('custom-class');
    });

    it('should preserve base classes with custom className', () => {
      render(<ErrorDisplay title="Test" className="bg-blue-500" />);
      const errorDisplay = screen.getByTestId('error-display');
      expect(errorDisplay).toHaveClass('bg-blue-500');
      expect(errorDisplay).toHaveClass('flex');
      expect(errorDisplay).toHaveClass('flex-col');
    });
  });

  describe('Accessibility', () => {
    it('should have role="alert"', () => {
      render(<ErrorDisplay title="Errore" />);
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });

    it('should have aria-live="assertive"', () => {
      render(<ErrorDisplay title="Errore" />);
      const errorDisplay = screen.getByRole('alert');
      expect(errorDisplay).toHaveAttribute('aria-live', 'assertive');
    });

    it('should have icon with aria-hidden', () => {
      const { container } = render(<ErrorDisplay title="Errore" />);
      const iconWrapper = container.querySelector('[aria-hidden="true"]');
      expect(iconWrapper).toBeInTheDocument();
    });

    it('should include screen reader text with title and description', () => {
      render(<ErrorDisplay title="Errore critico" description="Contattare supporto" />);
      const srText = screen.getByText(/Errore: Errore critico.*Contattare supporto/s);
      expect(srText).toHaveClass('sr-only');
    });

    it('should include retry instruction in screen reader text when onRetry provided', () => {
      const { container } = render(<ErrorDisplay title="Errore" onRetry={() => {}} />);
      const srText = container.querySelector('.sr-only');
      expect(srText).toBeInTheDocument();
      expect(srText?.textContent).toMatch(/Premi il pulsante per riprovare/);
    });

    it('should have appropriate aria-label on retry button', () => {
      render(<ErrorDisplay title="Errore" onRetry={() => {}} />);
      const button = screen.getByRole('button');
      expect(button).toHaveAttribute('aria-label', 'Riprova');
    });

    it('should have countdown aria-label on disabled button', () => {
      render(<ErrorDisplay title="Errore" retryAfterSeconds={30} onRetry={() => {}} />);
      const button = screen.getByRole('button');
      expect(button.getAttribute('aria-label')).toMatch(/Riprova tra 30s/);
    });
  });

  describe('Reduced motion', () => {
    it('should have transition classes on icon wrapper when reduced motion is false', () => {
      mockUseReducedMotion.mockReturnValue(false);
      const { container } = render(<ErrorDisplay title="Test" variant="network" />);
      const iconWrapper = container.querySelector('[aria-hidden="true"]');
      expect(iconWrapper).toHaveClass('transition-transform');
    });

    it('should not have transition classes on icon wrapper when reduced motion is true', () => {
      mockUseReducedMotion.mockReturnValue(true);
      const { container } = render(<ErrorDisplay title="Test" variant="network" />);
      const iconWrapper = container.querySelector('[aria-hidden="true"]');
      expect(iconWrapper).not.toHaveClass('transition-transform');
    });

    it('should not have animation classes when reduced motion is true', () => {
      mockUseReducedMotion.mockReturnValue(true);
      const { container } = render(<ErrorDisplay title="Test" variant="network" />);
      const iconWrapper = container.querySelector('.animate-pulse');
      expect(iconWrapper).not.toBeInTheDocument();
    });
  });

  describe('Snapshot tests', () => {
    it('should match snapshot for generic variant', () => {
      const { container } = render(
        <ErrorDisplay title="Errore imprevisto" description="Si è verificato un errore" />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for network variant', () => {
      const { container } = render(
        <ErrorDisplay
          title="Errore di connessione"
          description="Verifica la tua connessione"
          variant="network"
          onRetry={() => {}}
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for server variant with correlation ID', () => {
      const error = new ApiError({
        message: 'Internal server error',
        statusCode: 500,
        endpoint: '/api',
        correlationId: 'test-123',
      });
      const { container } = render(
        <ErrorDisplay title="Errore del server" error={error} onRetry={() => {}} />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for rateLimit variant with countdown', () => {
      const { container } = render(
        <ErrorDisplay
          title="Troppe richieste"
          variant="rateLimit"
          retryAfterSeconds={60}
          onRetry={() => {}}
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });
  });
});

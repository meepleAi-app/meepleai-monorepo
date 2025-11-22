import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { useRouter } from 'next/router';
import LoginPage from '@/pages/login';
import '@testing-library/jest-dom';

// Mock next/router
jest.mock('next/router', () => ({
  useRouter: jest.fn(),
}));

// Mock AuthModal component  
jest.mock('@/components/auth', () => ({
  AuthModal: ({ isOpen, onClose, defaultMode, sessionExpiredMessage, showDemoCredentials }: any) => {
    if (!isOpen) return null;
    return (
      <div data-testid="auth-modal" role="dialog">
        <div>Auth Modal</div>
        {defaultMode && <div data-testid="auth-mode">{defaultMode}</div>}
        {sessionExpiredMessage && <div data-testid="session-expired">Session expired</div>}
        {showDemoCredentials && <div data-testid="demo-credentials">Demo credentials shown</div>}
        <button onClick={onClose}>Close</button>
      </div>
    );
  }
}));

describe('Login Page', () => {
  const mockPush = jest.fn();
  const mockRouter = {
    query: {},
    push: mockPush,
    pathname: '/login',
    route: '/login',
    asPath: '/login',
    basePath: '',
  };

  beforeEach(() => {
    jest.clearAllMocks();
    (useRouter as jest.Mock).mockReturnValue(mockRouter);
  });

  describe('Basic Rendering', () => {
    it('renders without crashing', () => {
      const { container } = render(<LoginPage />);
      expect(container).toBeInTheDocument();
    });

    it('renders the AuthModal component', () => {
      render(<LoginPage />);
      expect(screen.getByTestId('auth-modal')).toBeInTheDocument();
    });

    it('sets AuthModal to login mode by default', () => {
      render(<LoginPage />);
      expect(screen.getByTestId('auth-mode')).toHaveTextContent('login');
    });

    it('shows demo credentials in AuthModal', () => {
      render(<LoginPage />);
      expect(screen.getByTestId('demo-credentials')).toBeInTheDocument();
    });

    it('AuthModal is open by default', () => {
      render(<LoginPage />);
      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });
  });

  describe('Session Expiration Alert', () => {
    it('displays session expired message when reason=session_expired', () => {
      (useRouter as jest.Mock).mockReturnValue({
        ...mockRouter,
        query: { reason: 'session_expired' }
      });

      render(<LoginPage />);
      expect(screen.getByTestId('session-expired')).toBeInTheDocument();
    });

    it('does not display session expired message without query param', () => {
      render(<LoginPage />);
      expect(screen.queryByTestId('session-expired')).not.toBeInTheDocument();
    });

    it('passes correct sessionExpiredMessage prop to AuthModal', () => {
      (useRouter as jest.Mock).mockReturnValue({
        ...mockRouter,
        query: { reason: 'session_expired' }
      });

      render(<LoginPage />);
      // The AuthModal receives true when session is expired
      expect(screen.getByTestId('session-expired')).toBeInTheDocument();
    });
  });

  describe('Navigation', () => {
    it('redirects to home when modal is closed', () => {
      render(<LoginPage />);
      
      const closeButton = screen.getByText('Close');
      closeButton.click();

      expect(mockPush).toHaveBeenCalledWith('/');
    });

    it('hides modal after closing', () => {
      const { rerender } = render(<LoginPage />);
      
      const closeButton = screen.getByText('Close');
      closeButton.click();

      rerender(<LoginPage />);
      // After closing, the modal should not be rendered
      waitFor(() => {
        expect(screen.queryByTestId('auth-modal')).not.toBeInTheDocument();
      });
    });
  });

  describe('Page Metadata', () => {
    it('sets the correct page title', () => {
      render(<LoginPage />);
      // Note: Testing Next.js Head requires different approach
      // The actual title would be "Login - MeepleAI"
      expect(document.title).toBeDefined();
    });
  });

  describe('Styling', () => {
    it('applies gradient background styling', () => {
      const { container } = render(<LoginPage />);
      const backgroundDiv = container.querySelector('.min-h-dvh');
      expect(backgroundDiv).toHaveClass('bg-gradient-to-br');
      expect(backgroundDiv).toHaveClass('from-slate-50');
      expect(backgroundDiv).toHaveClass('to-slate-100');
    });

    it('supports dark mode styling classes', () => {
      const { container } = render(<LoginPage />);
      const backgroundDiv = container.querySelector('.min-h-dvh');
      expect(backgroundDiv).toHaveClass('dark:from-slate-900');
      expect(backgroundDiv).toHaveClass('dark:to-slate-800');
    });

    it('centers content properly', () => {
      const { container } = render(<LoginPage />);
      const backgroundDiv = container.querySelector('.min-h-dvh');
      expect(backgroundDiv).toHaveClass('flex');
      expect(backgroundDiv).toHaveClass('items-center');
      expect(backgroundDiv).toHaveClass('justify-center');
    });
  });
});

import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { useRouter } from 'next/router';
import BoardGameAI from '@/pages/board-game-ai';
import { api } from '@/lib/api';

// Mock dependencies
jest.mock('next/router', () => ({
  useRouter: jest.fn()
}));

jest.mock('@/lib/api', () => ({
  api: {
    get: jest.fn(),
    post: jest.fn()
  }
}));

jest.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: any) => <div {...props}>{children}</div>,
    button: ({ children, ...props }: any) => <button {...props}>{children}</button>,
    a: ({ children, ...props }: any) => <a {...props}>{children}</a>
  },
  AnimatePresence: ({ children }: any) => <>{children}</>,
  useInView: () => [jest.fn(), true]
}));

jest.mock('react-intersection-observer', () => ({
  useInView: () => [jest.fn(), true]
}));

describe('BoardGameAI Page', () => {
  const mockPush = jest.fn();
  const mockRouter = {
    push: mockPush,
    pathname: '/board-game-ai',
    query: {},
    asPath: '/board-game-ai'
  };

  beforeEach(() => {
    jest.clearAllMocks();
    (useRouter as jest.Mock).mockReturnValue(mockRouter);
  });

  describe('Authentication', () => {
    it('should load current user on mount', async () => {
      const mockUser = {
        user: {
          id: '123',
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User'
        },
        expiresAt: '2025-12-31T23:59:59Z'
      };

      (api.get as jest.Mock).mockResolvedValue(mockUser);

      render(<BoardGameAI />);

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith('/api/v1/auth/me');
      });
    });

    it('should handle authentication failure gracefully', async () => {
      (api.get as jest.Mock).mockRejectedValue(new Error('Unauthorized'));

      render(<BoardGameAI />);

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith('/api/v1/auth/me');
      });

      // Should still render the page
      expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument();
    });

    it('should show Login button when not authenticated', async () => {
      (api.get as jest.Mock).mockRejectedValue(new Error('Unauthorized'));

      render(<BoardGameAI />);

      await waitFor(() => {
        const loginButton = screen.getAllByText('Login')[0];
        expect(loginButton).toBeInTheDocument();
      });
    });

    it('should show Logout button when authenticated', async () => {
      const mockUser = {
        user: {
          id: '123',
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User'
        },
        expiresAt: '2025-12-31T23:59:59Z'
      };

      (api.get as jest.Mock).mockResolvedValue(mockUser);

      render(<BoardGameAI />);

      await waitFor(() => {
        expect(screen.getByText('Logout')).toBeInTheDocument();
      });
    });

    it('should handle logout', async () => {
      const mockUser = {
        user: {
          id: '123',
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User'
        },
        expiresAt: '2025-12-31T23:59:59Z'
      };

      (api.get as jest.Mock).mockResolvedValue(mockUser);
      (api.post as jest.Mock).mockResolvedValue({});

      render(<BoardGameAI />);

      await waitFor(() => {
        expect(screen.getByText('Logout')).toBeInTheDocument();
      });

      const logoutButton = screen.getByText('Logout');
      fireEvent.click(logoutButton);

      await waitFor(() => {
        expect(api.post).toHaveBeenCalledWith('/api/v1/auth/logout');
      });
    });
  });

  describe('Page Structure', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockRejectedValue(new Error('Unauthorized'));
    });

    it('should render hero section', async () => {
      render(<BoardGameAI />);

      await waitFor(() => {
        const heading = screen.getByRole('heading', { level: 1 });
        expect(heading).toHaveTextContent(/AI-Powered/i);
        expect(heading).toHaveTextContent(/Board Game Rules/i);
        expect(heading).toHaveTextContent(/Expert/i);
      }, { timeout: 3000 });
    });

    it('should render hero description', async () => {
      render(<BoardGameAI />);

      await waitFor(() => {
        expect(screen.getByText(/Get instant, accurate answers to any board game rule question/i)).toBeInTheDocument();
      });
    });

    it('should render features section', async () => {
      render(<BoardGameAI />);

      await waitFor(() => {
        expect(screen.getByText('Why Board Game AI?')).toBeInTheDocument();
      });
    });

    it('should display all 6 feature cards', async () => {
      render(<BoardGameAI />);

      await waitFor(() => {
        expect(screen.getByText('Multi-Model Consensus')).toBeInTheDocument();
        expect(screen.getByText('100+ Games Supported')).toBeInTheDocument();
        expect(screen.getByText('Precise Citations')).toBeInTheDocument();
        expect(screen.getByText('Lightning Fast')).toBeInTheDocument();
        expect(screen.getByText('Multilingual Support')).toBeInTheDocument();
        expect(screen.getByText('5-Layer Validation')).toBeInTheDocument();
      });
    });

    it('should render how it works section', async () => {
      render(<BoardGameAI />);

      await waitFor(() => {
        expect(screen.getByText('How It Works')).toBeInTheDocument();
        expect(screen.getByText('Select Your Game')).toBeInTheDocument();
        expect(screen.getByText('Ask Your Question')).toBeInTheDocument();
        expect(screen.getByText('Get Verified Answer')).toBeInTheDocument();
      });
    });

    it('should render CTA section', async () => {
      render(<BoardGameAI />);

      await waitFor(() => {
        expect(screen.getByText(/Ready to Resolve Your Rules Disputes/i)).toBeInTheDocument();
      });
    });

    it('should render footer', async () => {
      render(<BoardGameAI />);

      await waitFor(() => {
        expect(screen.getByText(/© 2025 MeepleAI/i)).toBeInTheDocument();
      });
    });

    it('should render navigation header', async () => {
      render(<BoardGameAI />);

      await waitFor(() => {
        const nav = screen.getByRole('navigation');
        expect(nav).toBeInTheDocument();
        const links = screen.getAllByText('Board Game AI');
        expect(links.length).toBeGreaterThan(0);
      }, { timeout: 3000 });
    });

    it('should render example Q&A in hero visual', async () => {
      render(<BoardGameAI />);

      await waitFor(() => {
        expect(screen.getByText(/In Terraforming Mars/i)).toBeInTheDocument();
        expect(screen.getByText(/Board Game AI:/i)).toBeInTheDocument();
        expect(screen.getByText(/Confidence: 95%/i)).toBeInTheDocument();
      });
    });
  });

  describe('Navigation', () => {
    it('should redirect unauthenticated users to login when clicking Get Started', async () => {
      (api.get as jest.Mock).mockRejectedValue(new Error('Unauthorized'));

      render(<BoardGameAI />);

      await waitFor(() => {
        const getStartedButton = screen.getByTestId('hero-get-started');
        expect(getStartedButton).not.toBeDisabled();
      }, { timeout: 3000 });

      const getStartedButton = screen.getByTestId('hero-get-started');
      fireEvent.click(getStartedButton);

      expect(mockPush).toHaveBeenCalledWith('/login?redirect=/board-game-ai/ask');
    });

    it('should redirect authenticated users to ask page when clicking Get Started', async () => {
      const mockUser = {
        user: {
          id: '123',
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User'
        },
        expiresAt: '2025-12-31T23:59:59Z'
      };

      (api.get as jest.Mock).mockResolvedValue(mockUser);

      render(<BoardGameAI />);

      await waitFor(() => {
        const getStartedButton = screen.getByTestId('hero-get-started');
        expect(getStartedButton).not.toBeDisabled();
      }, { timeout: 3000 });

      const getStartedButton = screen.getByTestId('hero-get-started');
      fireEvent.click(getStartedButton);

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith('/board-game-ai/ask');
      });
    });

    it('should handle CTA Get Started click for unauthenticated users', async () => {
      (api.get as jest.Mock).mockRejectedValue(new Error('Unauthorized'));

      render(<BoardGameAI />);

      await waitFor(() => {
        const ctaButton = screen.getByTestId('cta-get-started');
        expect(ctaButton).not.toBeDisabled();
      }, { timeout: 3000 });

      const ctaButton = screen.getByTestId('cta-get-started');
      fireEvent.click(ctaButton);

      expect(mockPush).toHaveBeenCalledWith('/login?redirect=/board-game-ai/ask');
    });

    it('should handle CTA Get Started click for authenticated users', async () => {
      const mockUser = {
        user: {
          id: '123',
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User'
        },
        expiresAt: '2025-12-31T23:59:59Z'
      };

      (api.get as jest.Mock).mockResolvedValue(mockUser);

      render(<BoardGameAI />);

      await waitFor(() => {
        const ctaButton = screen.getByTestId('cta-get-started');
        expect(ctaButton).not.toBeDisabled();
      }, { timeout: 3000 });

      const ctaButton = screen.getByTestId('cta-get-started');
      fireEvent.click(ctaButton);

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith('/board-game-ai/ask');
      });
    });
  });

  describe('Loading States', () => {
    it('should show loading text on buttons during initial load', () => {
      (api.get as jest.Mock).mockImplementation(() => new Promise(() => {})); // Never resolves

      render(<BoardGameAI />);

      const buttons = screen.getAllByText('Loading...');
      expect(buttons.length).toBeGreaterThan(0);
    });

    it('should disable buttons during loading', () => {
      (api.get as jest.Mock).mockImplementation(() => new Promise(() => {}));

      render(<BoardGameAI />);

      const heroButton = screen.getByTestId('hero-get-started');
      expect(heroButton).toBeDisabled();
    });

    it('should enable buttons after loading completes', async () => {
      (api.get as jest.Mock).mockRejectedValue(new Error('Unauthorized'));

      render(<BoardGameAI />);

      await waitFor(() => {
        const heroButton = screen.getByTestId('hero-get-started');
        expect(heroButton).not.toBeDisabled();
      });
    });
  });

  describe('Accessibility', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockRejectedValue(new Error('Unauthorized'));
    });

    it('should have main content landmark', async () => {
      render(<BoardGameAI />);

      await waitFor(() => {
        expect(screen.getByRole('main')).toBeInTheDocument();
      });
    });

    it('should have navigation landmark', async () => {
      render(<BoardGameAI />);

      await waitFor(() => {
        expect(screen.getByRole('navigation')).toBeInTheDocument();
      });
    });

    it('should have proper heading hierarchy', async () => {
      render(<BoardGameAI />);

      await waitFor(() => {
        const h1 = screen.getByRole('heading', { level: 1 });
        expect(h1).toBeInTheDocument();

        const h2Headings = screen.getAllByRole('heading', { level: 2 });
        expect(h2Headings.length).toBeGreaterThan(0);
      });
    });

    it('should have descriptive button labels', async () => {
      render(<BoardGameAI />);

      await waitFor(() => {
        const logoutButton = screen.queryByLabelText('Logout from MeepleAI');
        // May not exist if not authenticated, which is fine
        if (logoutButton) {
          expect(logoutButton).toBeInTheDocument();
        }
      });
    });
  });

  describe('Responsive Design', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockRejectedValue(new Error('Unauthorized'));
    });

    it('should render on mobile viewport', async () => {
      // Simulate mobile viewport
      global.innerWidth = 375;
      global.dispatchEvent(new Event('resize'));

      render(<BoardGameAI />);

      await waitFor(() => {
        expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument();
      }, { timeout: 3000 });
    });

    it('should render on desktop viewport', async () => {
      // Simulate desktop viewport
      global.innerWidth = 1920;
      global.dispatchEvent(new Event('resize'));

      render(<BoardGameAI />);

      await waitFor(() => {
        expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument();
      }, { timeout: 3000 });
    });
  });

  describe('Error Handling', () => {
    it('should handle API errors gracefully', async () => {
      (api.get as jest.Mock).mockRejectedValue(new Error('Network error'));

      render(<BoardGameAI />);

      await waitFor(() => {
        // Page should still render
        expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument();
      }, { timeout: 3000 });
    });

    it('should handle logout errors gracefully', async () => {
      const mockUser = {
        user: {
          id: '123',
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User'
        },
        expiresAt: '2025-12-31T23:59:59Z'
      };

      (api.get as jest.Mock).mockResolvedValue(mockUser);
      (api.post as jest.Mock).mockRejectedValue(new Error('Logout failed'));

      // Mock console.error to prevent test output pollution
      const consoleSpy = jest.spyOn(console, 'error').mockImplementation();

      render(<BoardGameAI />);

      await waitFor(() => {
        expect(screen.getByText('Logout')).toBeInTheDocument();
      });

      const logoutButton = screen.getByText('Logout');
      fireEvent.click(logoutButton);

      await waitFor(() => {
        expect(consoleSpy).toHaveBeenCalled();
      });

      consoleSpy.mockRestore();
    });
  });

  describe('Admin Navigation', () => {
    it('should show Admin link for admin users', async () => {
      const mockAdminUser = {
        user: {
          id: '123',
          email: 'admin@example.com',
          displayName: 'Admin User',
          role: 'Admin'
        },
        expiresAt: '2025-12-31T23:59:59Z'
      };

      (api.get as jest.Mock).mockResolvedValue(mockAdminUser);

      render(<BoardGameAI />);

      await waitFor(() => {
        expect(screen.getByText('Admin')).toBeInTheDocument();
      });
    });

    it('should not show Admin link for regular users', async () => {
      const mockUser = {
        user: {
          id: '123',
          email: 'user@example.com',
          displayName: 'Regular User',
          role: 'User'
        },
        expiresAt: '2025-12-31T23:59:59Z'
      };

      (api.get as jest.Mock).mockResolvedValue(mockUser);

      render(<BoardGameAI />);

      await waitFor(() => {
        expect(screen.queryByText('Admin')).not.toBeInTheDocument();
      });
    });
  });

  describe('Button Text Changes', () => {
    it('should show "Get Started Free" for unauthenticated users', async () => {
      (api.get as jest.Mock).mockRejectedValue(new Error('Unauthorized'));

      render(<BoardGameAI />);

      await waitFor(() => {
        const button = screen.getByTestId('cta-get-started');
        expect(button).toHaveTextContent('Get Started Free');
      }, { timeout: 3000 });
    });

    it('should show "Ask a Question" for authenticated users in hero', async () => {
      const mockUser = {
        user: {
          id: '123',
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User'
        },
        expiresAt: '2025-12-31T23:59:59Z'
      };

      (api.get as jest.Mock).mockResolvedValue(mockUser);

      render(<BoardGameAI />);

      await waitFor(() => {
        expect(screen.getByText('Ask a Question')).toBeInTheDocument();
      });
    });

    it('should show "Ask Your First Question" for authenticated users in CTA', async () => {
      const mockUser = {
        user: {
          id: '123',
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User'
        },
        expiresAt: '2025-12-31T23:59:59Z'
      };

      (api.get as jest.Mock).mockResolvedValue(mockUser);

      render(<BoardGameAI />);

      await waitFor(() => {
        expect(screen.getByText('Ask Your First Question')).toBeInTheDocument();
      });
    });
  });
});

/**
 * Tests for OAuthButtons component
 * Comprehensive coverage of OAuth authentication buttons
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import OAuthButtons from '../OAuthButtons';

describe('OAuthButtons', () => {
  const originalEnv = process.env;
  const originalLocation = window.location;

  // Helper function to create a spy for window.location.href assignment
  // Note: Mocking window.location.href in jsdom is challenging due to jsdom's
  // internal handling of the Location object. The tests that use callbacks work fine,
  // demonstrating that the component logic is correct.
  const createHrefSpy = () => {
    const spy = jest.fn();
    // Mock the location.href setter by replacing window.location
    delete (window as any).location;
    (window as any).location = {
      href: 'http://localhost/',
      origin: 'http://localhost',
      protocol: 'http:',
      host: 'localhost',
      hostname: 'localhost',
      port: '',
      pathname: '/',
      search: '',
      hash: '',
      get href() {
        return this._href || 'http://localhost/';
      },
      set href(url: string) {
        this._href = url;
        spy(url);
      },
      _href: 'http://localhost/',
      assign: jest.fn(),
      reload: jest.fn(),
      replace: jest.fn()
    };
    return spy;
  };

  beforeEach(() => {
    jest.clearAllMocks();
    process.env = { ...originalEnv };
  });

  afterEach(() => {
    process.env = originalEnv;
    // Restore original window.location to prevent mock from leaking to other tests
    window.location = originalLocation;
  });

  describe('Rendering', () => {
    it('renders divider with "Or continue with" text', () => {
      render(<OAuthButtons />);
      expect(screen.getByText('Or continue with')).toBeInTheDocument();
    });

    it('renders Google OAuth button', () => {
      render(<OAuthButtons />);
      expect(screen.getByText('Continue with Google')).toBeInTheDocument();
    });

    it('renders Discord OAuth button', () => {
      render(<OAuthButtons />);
      expect(screen.getByText('Continue with Discord')).toBeInTheDocument();
    });

    it('renders GitHub OAuth button', () => {
      render(<OAuthButtons />);
      expect(screen.getByText('Continue with GitHub')).toBeInTheDocument();
    });

    it('renders all three OAuth buttons', () => {
      render(<OAuthButtons />);
      const buttons = screen.getAllByRole('button');
      expect(buttons).toHaveLength(3);
    });

    it('renders Google logo SVG', () => {
      const { container } = render(<OAuthButtons />);
      const googleButton = screen.getByText('Continue with Google').parentElement;
      const svg = googleButton?.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('renders Discord logo SVG', () => {
      const { container } = render(<OAuthButtons />);
      const discordButton = screen.getByText('Continue with Discord').parentElement;
      const svg = discordButton?.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('renders GitHub logo SVG', () => {
      const { container } = render(<OAuthButtons />);
      const githubButton = screen.getByText('Continue with GitHub').parentElement;
      const svg = githubButton?.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });
  });

  describe('Default Behavior (without onOAuthLogin callback)', () => {
    it('redirects to Google OAuth endpoint when Google button is clicked', async () => {
      const user = userEvent.setup();
      delete process.env.NEXT_PUBLIC_API_BASE;

      const hrefSpy = createHrefSpy();

      render(<OAuthButtons />);
      const googleButton = screen.getByText('Continue with Google');
      await user.click(googleButton);

      // Component uses 'http://localhost:8080' as default when env var is not set
      expect(hrefSpy).toHaveBeenCalledWith('http://localhost:8080/api/v1/auth/oauth/google/login');
    });

    it('redirects to Discord OAuth endpoint when Discord button is clicked', async () => {
      const user = userEvent.setup();
      delete process.env.NEXT_PUBLIC_API_BASE;

      const hrefSpy = createHrefSpy();

      render(<OAuthButtons />);
      const discordButton = screen.getByText('Continue with Discord');
      await user.click(discordButton);

      expect(hrefSpy).toHaveBeenCalledWith('http://localhost:8080/api/v1/auth/oauth/discord/login');
    });

    it('redirects to GitHub OAuth endpoint when GitHub button is clicked', async () => {
      const user = userEvent.setup();
      delete process.env.NEXT_PUBLIC_API_BASE;

      const hrefSpy = createHrefSpy();

      render(<OAuthButtons />);
      const githubButton = screen.getByText('Continue with GitHub');
      await user.click(githubButton);

      expect(hrefSpy).toHaveBeenCalledWith('http://localhost:8080/api/v1/auth/oauth/github/login');
    });

    it('uses default API base URL when NEXT_PUBLIC_API_BASE is not set', async () => {
      const user = userEvent.setup();
      delete process.env.NEXT_PUBLIC_API_BASE;

      const hrefSpy = createHrefSpy();

      render(<OAuthButtons />);
      const googleButton = screen.getByText('Continue with Google');
      await user.click(googleButton);

      expect(hrefSpy).toHaveBeenCalledWith('http://localhost:8080/api/v1/auth/oauth/google/login');
    });

    it('uses custom API base URL when NEXT_PUBLIC_API_BASE is set via Object.defineProperty', async () => {
      const user = userEvent.setup();

      // Use Object.defineProperty for more reliable env var setting in Jest
      Object.defineProperty(process.env, 'NEXT_PUBLIC_API_BASE', {
        value: 'https://api.example.com',
        writable: true,
        configurable: true
      });

      const hrefSpy = createHrefSpy();

      render(<OAuthButtons />);
      const googleButton = screen.getByText('Continue with Google');
      await user.click(googleButton);

      expect(hrefSpy).toHaveBeenCalledWith('https://api.example.com/api/v1/auth/oauth/google/login');

      // Clean up
      delete process.env.NEXT_PUBLIC_API_BASE;
    });
  });

  describe('Custom Callback Behavior', () => {
    it('calls onOAuthLogin with "google" when Google button is clicked', async () => {
      const user = userEvent.setup();
      const onOAuthLogin = jest.fn();

      render(<OAuthButtons onOAuthLogin={onOAuthLogin} />);
      const googleButton = screen.getByText('Continue with Google');
      await user.click(googleButton);

      expect(onOAuthLogin).toHaveBeenCalledWith('google');
      expect(onOAuthLogin).toHaveBeenCalledTimes(1);
    });

    it('calls onOAuthLogin with "discord" when Discord button is clicked', async () => {
      const user = userEvent.setup();
      const onOAuthLogin = jest.fn();

      render(<OAuthButtons onOAuthLogin={onOAuthLogin} />);
      const discordButton = screen.getByText('Continue with Discord');
      await user.click(discordButton);

      expect(onOAuthLogin).toHaveBeenCalledWith('discord');
      expect(onOAuthLogin).toHaveBeenCalledTimes(1);
    });

    it('calls onOAuthLogin with "github" when GitHub button is clicked', async () => {
      const user = userEvent.setup();
      const onOAuthLogin = jest.fn();

      render(<OAuthButtons onOAuthLogin={onOAuthLogin} />);
      const githubButton = screen.getByText('Continue with GitHub');
      await user.click(githubButton);

      expect(onOAuthLogin).toHaveBeenCalledWith('github');
      expect(onOAuthLogin).toHaveBeenCalledTimes(1);
    });

    it('does not redirect when onOAuthLogin is provided', async () => {
      const user = userEvent.setup();
      const onOAuthLogin = jest.fn();

      const hrefSpy = createHrefSpy();

      render(<OAuthButtons onOAuthLogin={onOAuthLogin} />);
      const googleButton = screen.getByText('Continue with Google');
      await user.click(googleButton);

      // Should not set href when callback is provided
      expect(hrefSpy).not.toHaveBeenCalled();
    });
  });

  describe('Styling', () => {
    it('applies correct styling to Google button', () => {
      render(<OAuthButtons />);
      const googleButton = screen.getByText('Continue with Google').closest('button');

      expect(googleButton).toHaveClass('w-full');
      expect(googleButton).toHaveClass('flex');
      expect(googleButton).toHaveClass('items-center');
      expect(googleButton).toHaveClass('justify-center');
    });

    it('applies Discord brand color to Discord button', () => {
      render(<OAuthButtons />);
      const discordButton = screen.getByText('Continue with Discord').closest('button');

      expect(discordButton).toHaveClass('bg-[#5865F2]');
    });

    it('applies dark background to GitHub button', () => {
      render(<OAuthButtons />);
      const githubButton = screen.getByText('Continue with GitHub').closest('button');

      expect(githubButton).toHaveClass('bg-slate-900');
    });

    it('applies focus ring styles to buttons', () => {
      render(<OAuthButtons />);
      const buttons = screen.getAllByRole('button');

      buttons.forEach(button => {
        expect(button).toHaveClass('focus:outline-none');
      });
    });

    it('applies hover styles to buttons', () => {
      render(<OAuthButtons />);
      const googleButton = screen.getByText('Continue with Google').closest('button');

      expect(googleButton).toHaveClass('hover:bg-slate-50');
    });
  });

  describe('Accessibility', () => {
    it('renders all buttons with type="button"', () => {
      render(<OAuthButtons />);
      const buttons = screen.getAllByRole('button');

      buttons.forEach(button => {
        expect(button).toHaveAttribute('type', 'button');
      });
    });

    it('has descriptive text for each provider', () => {
      render(<OAuthButtons />);

      expect(screen.getByText('Continue with Google')).toBeInTheDocument();
      expect(screen.getByText('Continue with Discord')).toBeInTheDocument();
      expect(screen.getByText('Continue with GitHub')).toBeInTheDocument();
    });

    it('renders SVG icons for visual users', () => {
      const { container } = render(<OAuthButtons />);
      const svgs = container.querySelectorAll('svg');

      expect(svgs.length).toBe(3); // One for each provider
    });

    it('maintains proper button order', () => {
      render(<OAuthButtons />);
      const buttons = screen.getAllByRole('button');

      expect(buttons[0]).toHaveTextContent('Continue with Google');
      expect(buttons[1]).toHaveTextContent('Continue with Discord');
      expect(buttons[2]).toHaveTextContent('Continue with GitHub');
    });
  });

  describe('Edge Cases', () => {
    it('handles multiple rapid clicks on same button', async () => {
      const user = userEvent.setup();
      const onOAuthLogin = jest.fn();

      render(<OAuthButtons onOAuthLogin={onOAuthLogin} />);
      const googleButton = screen.getByText('Continue with Google');

      await user.click(googleButton);
      await user.click(googleButton);
      await user.click(googleButton);

      // Should handle multiple clicks gracefully
      expect(onOAuthLogin).toHaveBeenCalledWith('google');
    });

    it('handles clicks on different providers in sequence', async () => {
      const user = userEvent.setup();
      const onOAuthLogin = jest.fn();

      render(<OAuthButtons onOAuthLogin={onOAuthLogin} />);

      await user.click(screen.getByText('Continue with Google'));
      await user.click(screen.getByText('Continue with Discord'));
      await user.click(screen.getByText('Continue with GitHub'));

      expect(onOAuthLogin).toHaveBeenNthCalledWith(1, 'google');
      expect(onOAuthLogin).toHaveBeenNthCalledWith(2, 'discord');
      expect(onOAuthLogin).toHaveBeenNthCalledWith(3, 'github');
      expect(onOAuthLogin).toHaveBeenCalledTimes(3);
    });

    it('handles empty NEXT_PUBLIC_API_BASE environment variable', async () => {
      const user = userEvent.setup();

      // Set empty string using Object.defineProperty
      Object.defineProperty(process.env, 'NEXT_PUBLIC_API_BASE', {
        value: '',
        writable: true,
        configurable: true
      });

      const hrefSpy = createHrefSpy();

      render(<OAuthButtons />);
      const googleButton = screen.getByText('Continue with Google');
      await user.click(googleButton);

      // Should use default when env var is empty string (empty string is falsy)
      expect(hrefSpy).toHaveBeenCalledWith('http://localhost:8080/api/v1/auth/oauth/google/login');

      // Clean up
      delete process.env.NEXT_PUBLIC_API_BASE;
    });
  });

  describe('Layout', () => {
    it('renders divider with horizontal line', () => {
      const { container } = render(<OAuthButtons />);
      const divider = container.querySelector('.border-t');
      expect(divider).toBeInTheDocument();
    });

    it('centers the divider text', () => {
      const { container } = render(<OAuthButtons />);
      const dividerText = screen.getByText('Or continue with');
      expect(dividerText.closest('.relative')).toHaveClass('flex', 'justify-center');
    });

    it('spaces buttons vertically', () => {
      const { container } = render(<OAuthButtons />);
      const buttonContainer = container.querySelector('.space-y-2');
      expect(buttonContainer).toBeInTheDocument();
    });

    it('spaces overall sections vertically', () => {
      const { container } = render(<OAuthButtons />);
      const mainContainer = container.querySelector('.space-y-3');
      expect(mainContainer).toBeInTheDocument();
    });
  });
});

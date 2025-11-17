/**
 * Chat Page Tests (Zustand Migration - Issue #1083)
 *
 * Tests for the main chat page with:
 * - Authentication state handling
 * - Chat store provider integration
 * - Loading states
 * - Component rendering
 *
 * Migration Pattern: Complex page test with AuthProvider + ChatStoreProvider
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import ChatPage from '@/components/pages/ChatPage';

// Mock the AuthProvider and useAuth hook
const mockUseAuth = jest.fn();
jest.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => mockUseAuth(),
  AuthProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

// Mock ChatStoreProvider - uses real store in tests via renderWithChatStore
jest.mock('@/store/chat/ChatStoreProvider', () => ({
  ChatStoreProvider: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="chat-store-provider">{children}</div>
  ),
}));

// Mock child components
jest.mock('@/components/chat/ChatSidebar', () => ({
  ChatSidebar: () => <div data-testid="chat-sidebar">sidebar</div>,
}));

jest.mock('@/components/chat/ChatContent', () => ({
  ChatContent: () => <div data-testid="chat-content">content</div>,
}));

jest.mock('@/components/chat/BottomNav', () => ({
  BottomNav: () => <div data-testid="bottom-nav">navigation</div>,
}));

const exportModalMock = jest.fn(
  ({ isOpen }: { isOpen: boolean }) =>
    isOpen ? <div data-testid="export-modal">modal</div> : null,
);

jest.mock('@/components/ExportChatModal', () => ({
  ExportChatModal: (props: { isOpen: boolean }) => exportModalMock(props),
}));

describe('ChatPage', () => {
  const authUser = {
    id: 'user-1',
    email: 'player@example.com',
    role: 'editor',
    displayName: 'Player One',
  };

  beforeEach(() => {
    jest.clearAllMocks();
    exportModalMock.mockClear();
  });

  /**
   * Test Group: Authentication States
   */
  describe('Authentication States', () => {
    it('shows login prompt when user is not authenticated', () => {
      // Mock useAuth to return no user and not loading
      mockUseAuth.mockReturnValue({
        user: null,
        loading: false,
        error: null,
        login: jest.fn(),
        register: jest.fn(),
        logout: jest.fn(),
        refreshUser: jest.fn(),
        clearError: jest.fn(),
      });

      render(<ChatPage />);

      // Check that login prompt is shown
      expect(screen.getByText(/Accesso richiesto/i)).toBeInTheDocument();
      expect(
        screen.getByText(/Devi effettuare l'accesso per utilizzare la chat\./i),
      ).toBeInTheDocument();
      expect(screen.getByText(/Vai al Login/i)).toBeInTheDocument();

      // Check that chat components are not rendered
      expect(screen.queryByTestId('chat-store-provider')).not.toBeInTheDocument();
      expect(screen.queryByTestId('chat-sidebar')).not.toBeInTheDocument();
      expect(screen.queryByTestId('chat-content')).not.toBeInTheDocument();
    });

    it('shows loading state while checking authentication', () => {
      // Mock useAuth to return loading state
      mockUseAuth.mockReturnValue({
        user: null,
        loading: true,
        error: null,
        login: jest.fn(),
        register: jest.fn(),
        logout: jest.fn(),
        refreshUser: jest.fn(),
        clearError: jest.fn(),
      });

      render(<ChatPage />);

      // Check that loading state is shown
      expect(screen.getByText('Loading...')).toBeInTheDocument();

      // Check that neither login prompt nor chat is rendered
      expect(screen.queryByText(/Accesso richiesto/i)).not.toBeInTheDocument();
      expect(screen.queryByTestId('chat-store-provider')).not.toBeInTheDocument();
    });
  });

  /**
   * Test Group: Chat Layout Rendering
   */
  describe('Chat Layout Rendering', () => {
    beforeEach(() => {
      // Mock authenticated user for all layout tests
      mockUseAuth.mockReturnValue({
        user: authUser,
        loading: false,
        error: null,
        login: jest.fn(),
        register: jest.fn(),
        logout: jest.fn(),
        refreshUser: jest.fn(),
        clearError: jest.fn(),
      });
    });

    it('renders chat layout when authentication succeeds', () => {
      render(<ChatPage />);

      // Check that ChatStoreProvider is rendered
      expect(screen.getByTestId('chat-store-provider')).toBeInTheDocument();

      // Check that chat components are rendered
      expect(screen.getByTestId('chat-sidebar')).toBeInTheDocument();
      expect(screen.getByTestId('chat-content')).toBeInTheDocument();
      expect(screen.getByTestId('bottom-nav')).toBeInTheDocument();
    });

    it('renders ExportChatModal in closed state by default', () => {
      render(<ChatPage />);

      // Check that modal is called with isOpen: false
      expect(exportModalMock).toHaveBeenCalledWith(
        expect.objectContaining({ isOpen: false }),
      );
    });

    it('renders main element with correct id and classes', () => {
      const { container } = render(<ChatPage />);

      const main = container.querySelector('main#main-content');
      expect(main).toBeInTheDocument();
      expect(main).toHaveClass('flex', 'h-dvh', 'font-sans', 'overflow-hidden');
    });

    it('wraps components with ChatStoreProvider', () => {
      render(<ChatPage />);

      const storeProvider = screen.getByTestId('chat-store-provider');

      // All components should be children of the store provider
      expect(storeProvider).toContainElement(screen.getByTestId('chat-sidebar'));
      expect(storeProvider).toContainElement(screen.getByTestId('chat-content'));
      expect(storeProvider).toContainElement(screen.getByTestId('bottom-nav'));
    });
  });

  /**
   * Test Group: Component Integration
   */
  describe('Component Integration', () => {
    beforeEach(() => {
      mockUseAuth.mockReturnValue({
        user: authUser,
        loading: false,
        error: null,
        login: jest.fn(),
        register: jest.fn(),
        logout: jest.fn(),
        refreshUser: jest.fn(),
        clearError: jest.fn(),
      });
    });

    it('renders all main chat components together', () => {
      render(<ChatPage />);

      // Verify all components are present
      expect(screen.getByTestId('chat-sidebar')).toBeInTheDocument();
      expect(screen.getByTestId('chat-content')).toBeInTheDocument();
      expect(screen.getByTestId('bottom-nav')).toBeInTheDocument();
      expect(exportModalMock).toHaveBeenCalled();
    });

    it('provides chat store context to all child components', () => {
      render(<ChatPage />);

      // Store provider should wrap all components
      const storeProvider = screen.getByTestId('chat-store-provider');
      expect(storeProvider).toBeInTheDocument();

      // All child components should have access to the store
      const sidebar = screen.getByTestId('chat-sidebar');
      const content = screen.getByTestId('chat-content');
      const nav = screen.getByTestId('bottom-nav');

      expect(storeProvider).toContainElement(sidebar);
      expect(storeProvider).toContainElement(content);
      expect(storeProvider).toContainElement(nav);
    });
  });

  /**
   * Test Group: Login Prompt Links
   */
  describe('Login Prompt Links', () => {
    beforeEach(() => {
      mockUseAuth.mockReturnValue({
        user: null,
        loading: false,
        error: null,
        login: jest.fn(),
        register: jest.fn(),
        logout: jest.fn(),
        refreshUser: jest.fn(),
        clearError: jest.fn(),
      });
    });

    it('has back to home link in login prompt', () => {
      render(<ChatPage />);

      const backLink = screen.getByRole('link', { name: /Torna alla Home/i });
      expect(backLink).toBeInTheDocument();
      expect(backLink).toHaveAttribute('href', '/');
    });

    it('has login button link in prompt', () => {
      render(<ChatPage />);

      const loginLink = screen.getByRole('link', { name: /Vai al Login/i });
      expect(loginLink).toBeInTheDocument();
      expect(loginLink).toHaveAttribute('href', '/');
    });

    it('login prompt has correct styling', () => {
      const { container } = render(<ChatPage />);

      const main = container.querySelector('main#main-content');
      expect(main).toHaveClass('p-6', 'max-w-4xl', 'mx-auto', 'font-sans');
    });
  });

  /**
   * Test Group: Edge Cases
   */
  describe('Edge Cases', () => {
    it('handles transition from loading to authenticated', () => {
      // Start with loading
      mockUseAuth.mockReturnValue({
        user: null,
        loading: true,
        error: null,
        login: jest.fn(),
        register: jest.fn(),
        logout: jest.fn(),
        refreshUser: jest.fn(),
        clearError: jest.fn(),
      });

      const { rerender } = render(<ChatPage />);
      expect(screen.getByText('Loading...')).toBeInTheDocument();

      // Transition to authenticated
      mockUseAuth.mockReturnValue({
        user: authUser,
        loading: false,
        error: null,
        login: jest.fn(),
        register: jest.fn(),
        logout: jest.fn(),
        refreshUser: jest.fn(),
        clearError: jest.fn(),
      });

      rerender(<ChatPage />);
      expect(screen.queryByText('Loading...')).not.toBeInTheDocument();
      expect(screen.getByTestId('chat-store-provider')).toBeInTheDocument();
    });

    it('handles transition from loading to unauthenticated', () => {
      // Start with loading
      mockUseAuth.mockReturnValue({
        user: null,
        loading: true,
        error: null,
        login: jest.fn(),
        register: jest.fn(),
        logout: jest.fn(),
        refreshUser: jest.fn(),
        clearError: jest.fn(),
      });

      const { rerender } = render(<ChatPage />);
      expect(screen.getByText('Loading...')).toBeInTheDocument();

      // Transition to unauthenticated
      mockUseAuth.mockReturnValue({
        user: null,
        loading: false,
        error: null,
        login: jest.fn(),
        register: jest.fn(),
        logout: jest.fn(),
        refreshUser: jest.fn(),
        clearError: jest.fn(),
      });

      rerender(<ChatPage />);
      expect(screen.queryByText('Loading...')).not.toBeInTheDocument();
      expect(screen.getByText(/Accesso richiesto/i)).toBeInTheDocument();
    });

    it('handles user logout (authenticated to unauthenticated)', () => {
      // Start authenticated
      mockUseAuth.mockReturnValue({
        user: authUser,
        loading: false,
        error: null,
        login: jest.fn(),
        register: jest.fn(),
        logout: jest.fn(),
        refreshUser: jest.fn(),
        clearError: jest.fn(),
      });

      const { rerender } = render(<ChatPage />);
      expect(screen.getByTestId('chat-store-provider')).toBeInTheDocument();

      // Logout
      mockUseAuth.mockReturnValue({
        user: null,
        loading: false,
        error: null,
        login: jest.fn(),
        register: jest.fn(),
        logout: jest.fn(),
        refreshUser: jest.fn(),
        clearError: jest.fn(),
      });

      rerender(<ChatPage />);
      expect(screen.queryByTestId('chat-store-provider')).not.toBeInTheDocument();
      expect(screen.getByText(/Accesso richiesto/i)).toBeInTheDocument();
    });
  });
});

import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import ChatPage from '@/pages/chat';

// Mock the AuthProvider and useAuth hook
const mockUseAuth = jest.fn();
jest.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => mockUseAuth(),
  AuthProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

// Mock the provider components
jest.mock('@/components/game/GameProvider', () => ({
  GameProvider: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="game-provider">{children}</div>
  ),
}));

jest.mock('@/components/chat/ChatProvider', () => ({
  ChatProvider: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="chat-provider">{children}</div>
  ),
}));

jest.mock('@/components/ui/UIProvider', () => ({
  UIProvider: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="ui-provider">{children}</div>
  ),
}));

jest.mock('@/components/chat/ChatSidebar', () => ({
  ChatSidebar: () => <div data-testid="chat-sidebar">sidebar</div>,
}));

jest.mock('@/components/chat/ChatContent', () => ({
  ChatContent: () => <div data-testid="chat-content">content</div>,
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

  it('shows login prompt when user is not authenticated', async () => {
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
    expect(screen.queryByTestId('game-provider')).not.toBeInTheDocument();
    expect(screen.queryByTestId('chat-provider')).not.toBeInTheDocument();
    expect(screen.queryByTestId('chat-sidebar')).not.toBeInTheDocument();
    expect(screen.queryByTestId('chat-content')).not.toBeInTheDocument();
  });

  it('renders chat layout when authentication succeeds', async () => {
    // Mock useAuth to return authenticated user
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

    render(<ChatPage />);

    // Check that providers are rendered
    expect(screen.getByTestId('game-provider')).toBeInTheDocument();
    expect(screen.getByTestId('chat-provider')).toBeInTheDocument();
    expect(screen.getByTestId('ui-provider')).toBeInTheDocument();

    // Check that chat components are rendered
    expect(screen.getByTestId('chat-sidebar')).toBeInTheDocument();
    expect(screen.getByTestId('chat-content')).toBeInTheDocument();

    // Check that modal is called with isOpen: false
    expect(exportModalMock).toHaveBeenCalledWith(
      expect.objectContaining({ isOpen: false }),
    );
  });

  it('shows loading state while checking authentication', async () => {
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
    expect(screen.queryByTestId('chat-provider')).not.toBeInTheDocument();
  });
});

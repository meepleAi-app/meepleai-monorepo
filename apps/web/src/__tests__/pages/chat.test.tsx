import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import ChatPage from '@/pages/chat';
import { api } from '@/lib/api';
import { createWrapper } from '../utils/test-providers';

jest.mock('@/components/chat/ChatProvider', () => ({
  ChatProvider: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="chat-provider">{children}</div>
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

jest.mock('@/lib/api', () => ({
  api: {
    get: jest.fn(),
  },
}));

const mockedApiGet = api.get as jest.MockedFunction<typeof api.get>;

describe('ChatPage', () => {
  const userResponse = {
    user: {
      id: 'user-1',
      email: 'player@example.com',
      role: 'editor',
      displayName: 'Player One',
    },
    expiresAt: new Date(Date.now() + 60_000).toISOString(),
  };

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('shows login prompt when user is not authenticated', async () => {
    mockedApiGet.mockResolvedValueOnce(null as any);

    render(<ChatPage />, { wrapper: createWrapper() });

    await waitFor(() =>
      expect(mockedApiGet).toHaveBeenCalledWith('/api/v1/auth/me'),
    );
    await waitFor(() =>
      expect(screen.getByText(/Accesso richiesto/i)).toBeInTheDocument(),
    );
    expect(
      screen.getByText(/Devi effettuare l'accesso per utilizzare la chat\./i),
    ).toBeInTheDocument();
    expect(screen.queryByTestId('chat-provider')).not.toBeInTheDocument();
  });

  it('renders chat layout when authentication succeeds', async () => {
    mockedApiGet.mockResolvedValueOnce(userResponse as any);

    render(<ChatPage />, { wrapper: createWrapper() });

    await waitFor(() =>
      expect(screen.getByTestId('chat-provider')).toBeInTheDocument(),
    );
    expect(screen.getByTestId('chat-sidebar')).toBeInTheDocument();
    expect(screen.getByTestId('chat-content')).toBeInTheDocument();
    expect(exportModalMock).toHaveBeenCalledWith(
      expect.objectContaining({ isOpen: false }),
    );
  });

  it('handles authentication errors by falling back to login prompt', async () => {
    mockedApiGet.mockRejectedValueOnce(new Error('network error'));

    render(<ChatPage />, { wrapper: createWrapper() });

    await waitFor(() =>
      expect(screen.getByText(/Accesso richiesto/i)).toBeInTheDocument(),
    );
    expect(screen.queryByTestId('chat-provider')).not.toBeInTheDocument();
  });
});

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CommentThread } from '../CommentThread';
import { api } from '../../lib/api';

jest.mock('../../lib/api', () => ({
  api: {
    ruleSpecComments: {
      getComments: jest.fn(),
      createComment: jest.fn(),
      updateComment: jest.fn(),
      deleteComment: jest.fn()
    }
  }
}));

const mockedApi = api.ruleSpecComments as {
  getComments: jest.MockedFunction<typeof api.ruleSpecComments.getComments>;
  createComment: jest.MockedFunction<typeof api.ruleSpecComments.createComment>;
  updateComment: jest.MockedFunction<typeof api.ruleSpecComments.updateComment>;
  deleteComment: jest.MockedFunction<typeof api.ruleSpecComments.deleteComment>;
};

describe('CommentThread', () => {
  const mockComments = [
    {
      id: 'comment-1',
      gameId: 'chess',
      version: 'v1',
      atomId: 'atom-1',
      userId: 'user-1',
      userDisplayName: 'User One',
      commentText: 'This is a test comment',
      createdAt: '2025-10-15T12:00:00Z',
      updatedAt: null
    },
    {
      id: 'comment-2',
      gameId: 'chess',
      version: 'v1',
      atomId: null,
      userId: 'user-2',
      userDisplayName: 'User Two',
      commentText: 'Another comment',
      createdAt: '2025-10-15T13:00:00Z',
      updatedAt: null
    }
  ];

  beforeEach(() => {
    mockedApi.getComments.mockReset();
    mockedApi.createComment.mockReset();
    mockedApi.updateComment.mockReset();
    mockedApi.deleteComment.mockReset();
  });

  it('loads and displays comments', async () => {
    mockedApi.getComments.mockResolvedValue({
      gameId: 'chess',
      version: 'v1',
      comments: mockComments,
      totalComments: 2
    });

    render(
      <CommentThread
        gameId="chess"
        version="v1"
        currentUserId="user-1"
        currentUserRole="Editor"
      />
    );

    await waitFor(() => {
      expect(screen.getByText('This is a test comment')).toBeInTheDocument();
      expect(screen.getByText('Another comment')).toBeInTheDocument();
    });

    expect(screen.getByText('Commenti (2)')).toBeInTheDocument();
  });

  it('displays message when no comments exist', async () => {
    mockedApi.getComments.mockResolvedValue({
      gameId: 'chess',
      version: 'v1',
      comments: [],
      totalComments: 0
    });

    render(
      <CommentThread
        gameId="chess"
        version="v1"
        currentUserId="user-1"
        currentUserRole="Editor"
      />
    );

    await waitFor(() => {
      expect(screen.getByText(/Nessun commento ancora/)).toBeInTheDocument();
    });
  });

  it('allows Editor to create comment', async () => {
    const user = userEvent.setup();

    mockedApi.getComments.mockResolvedValue({
      gameId: 'chess',
      version: 'v1',
      comments: [],
      totalComments: 0
    });

    mockedApi.createComment.mockResolvedValue({
      id: 'comment-new',
      gameId: 'chess',
      version: 'v1',
      atomId: null,
      userId: 'user-1',
      userDisplayName: 'Editor User',
      commentText: 'New comment',
      createdAt: '2025-10-15T14:00:00Z',
      updatedAt: null
    });

    render(
      <CommentThread
        gameId="chess"
        version="v1"
        currentUserId="user-1"
        currentUserRole="Editor"
      />
    );

    await waitFor(() => {
      expect(screen.getByPlaceholderText(/Aggiungi un commento/)).toBeInTheDocument();
    });

    const textarea = screen.getByPlaceholderText(/Aggiungi un commento/);
    await user.type(textarea, 'New comment');

    const submitButton = screen.getByRole('button', { name: /Aggiungi Commento/ });
    await user.click(submitButton);

    await waitFor(() => {
      expect(mockedApi.createComment).toHaveBeenCalledWith(
        'chess',
        'v1',
        {
          atomId: null,
          commentText: 'New comment'
        }
      );
    });
  });

  it('does not show comment form for regular User', async () => {
    mockedApi.getComments.mockResolvedValue({
      gameId: 'chess',
      version: 'v1',
      comments: [],
      totalComments: 0
    });

    render(
      <CommentThread
        gameId="chess"
        version="v1"
        currentUserId="user-1"
        currentUserRole="User"
      />
    );

    await waitFor(() => {
      expect(screen.getByText(/Nessun commento ancora/)).toBeInTheDocument();
    });

    expect(screen.queryByPlaceholderText(/Aggiungi un commento/)).not.toBeInTheDocument();
  });

  it('allows Admin to create comment', async () => {
    mockedApi.getComments.mockResolvedValue({
      gameId: 'chess',
      version: 'v1',
      comments: [],
      totalComments: 0
    });

    render(
      <CommentThread
        gameId="chess"
        version="v1"
        currentUserId="user-1"
        currentUserRole="Admin"
      />
    );

    await waitFor(() => {
      expect(screen.getByPlaceholderText(/Aggiungi un commento/)).toBeInTheDocument();
    });
  });

  it('displays error message when loading comments fails', async () => {
    const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});

    mockedApi.getComments.mockRejectedValue(
      new Error()
    );

    render(
      <CommentThread
        gameId="chess"
        version="v1"
        currentUserId="user-1"
        currentUserRole="Editor"
      />
    );

    await waitFor(() => {
      expect(screen.getByText(/Impossibile caricare i commenti/)).toBeInTheDocument();
    });

    consoleErrorSpy.mockRestore();
  });
});

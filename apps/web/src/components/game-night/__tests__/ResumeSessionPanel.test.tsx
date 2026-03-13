import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

const mockGetResumeContext = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: {
      getResumeContext: (...args: unknown[]) => mockGetResumeContext(...args),
    },
  },
}));

import { ResumeSessionPanel } from '../ResumeSessionPanel';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

describe('ResumeSessionPanel', () => {
  const onResume = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders resume context with scores and recap', async () => {
    mockGetResumeContext.mockResolvedValue({
      sessionId: 'sess-1',
      gameTitle: 'Agricola',
      lastSnapshotIndex: 3,
      currentTurn: 5,
      currentPhase: null,
      pausedAt: new Date(Date.now() - 3600000).toISOString(), // 1h ago
      recap: 'Partita salvata al turno 5.',
      playerScores: [
        { playerId: 'p1', name: 'Marco', totalScore: 45, rank: 1 },
        { playerId: 'p2', name: 'Luca', totalScore: 38, rank: 2 },
      ],
      photos: [],
    });

    render(<ResumeSessionPanel sessionId="sess-1" onResume={onResume} />, {
      wrapper: createWrapper(),
    });

    // Wait for data
    expect(await screen.findByText('Agricola')).toBeInTheDocument();
    expect(screen.getByText('Partita salvata al turno 5.')).toBeInTheDocument();
    expect(screen.getByText(/Marco: 45/)).toBeInTheDocument();
    expect(screen.getByText(/Luca: 38/)).toBeInTheDocument();
    const resumeBtn = screen.getByTestId('resume-session-button');
    expect(resumeBtn).toBeInTheDocument();
    expect(resumeBtn).toHaveTextContent(/turno 5/);
  });

  it('calls onResume when button is clicked', async () => {
    const user = userEvent.setup();
    mockGetResumeContext.mockResolvedValue({
      sessionId: 'sess-1',
      gameTitle: 'Catan',
      lastSnapshotIndex: 1,
      currentTurn: 3,
      currentPhase: null,
      pausedAt: new Date().toISOString(),
      recap: 'Partita salvata.',
      playerScores: [],
      photos: [],
    });

    render(<ResumeSessionPanel sessionId="sess-1" onResume={onResume} />, {
      wrapper: createWrapper(),
    });

    const resumeBtn = await screen.findByTestId('resume-session-button');
    await user.click(resumeBtn);

    expect(onResume).toHaveBeenCalledTimes(1);
  });

  it('renders photo thumbnails when available', async () => {
    mockGetResumeContext.mockResolvedValue({
      sessionId: 'sess-1',
      gameTitle: 'Carcassonne',
      lastSnapshotIndex: 2,
      currentTurn: 4,
      currentPhase: null,
      pausedAt: new Date().toISOString(),
      recap: 'Partita salvata.',
      playerScores: [],
      photos: [
        {
          attachmentId: 'a1',
          thumbnailUrl: '/thumb1.jpg',
          caption: 'Board',
          attachmentType: 'BoardState',
        },
        {
          attachmentId: 'a2',
          thumbnailUrl: '/thumb2.jpg',
          caption: null,
          attachmentType: 'PlayerArea',
        },
      ],
    });

    render(<ResumeSessionPanel sessionId="sess-1" onResume={onResume} />, {
      wrapper: createWrapper(),
    });

    const photos = await screen.findByTestId('resume-photos');
    expect(photos).toBeInTheDocument();

    const images = photos.querySelectorAll('img');
    expect(images).toHaveLength(2);
  });

  it('returns null while loading', () => {
    mockGetResumeContext.mockReturnValue(new Promise(() => {})); // Never resolves

    const { container } = render(<ResumeSessionPanel sessionId="sess-1" onResume={onResume} />, {
      wrapper: createWrapper(),
    });

    expect(container.firstChild).toBeNull();
  });
});

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { VotingPanel } from '../VotingPanel';

// ── Hook mocks ────────────────────────────────────────────────────────────────
const tallyMock = vi.fn();
const castMutateMock = vi.fn();
const retractMutateMock = vi.fn();
const resolveMutateMock = vi.fn();

vi.mock('@/hooks/queries/useGameNights', () => ({
  useGameNightVoteTally: () => tallyMock(),
  useCastGameNightVote: () => ({ mutate: castMutateMock, isPending: false, variables: undefined }),
  useRetractGameNightVote: () => ({
    mutate: retractMutateMock,
    isPending: false,
    variables: undefined,
  }),
  useResolveGameNightVotingTie: () => ({
    mutate: resolveMutateMock,
    isPending: false,
    variables: undefined,
  }),
}));

vi.mock('@/hooks/useToast', () => ({ useToast: () => ({ toast: vi.fn() }) }));
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key, locale: 'it-IT' }),
}));

const titles = { 'game-a': 'Catan', 'game-b': 'Wingspan' };

const openTally = {
  isVotingClosed: false,
  isTie: false,
  winnerGameId: null as string | null,
  leadingCandidateGameIds: ['game-a'],
  candidates: [
    { gameId: 'game-a', voteCount: 2, votedByMe: true },
    { gameId: 'game-b', voteCount: 1, votedByMe: false },
  ],
};

function renderPanel(overrides: { isOrganizer?: boolean } = {}) {
  return render(
    <VotingPanel
      gameNightId="gn-1"
      isOrganizer={overrides.isOrganizer ?? false}
      gameTitleById={titles}
    />
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  tallyMock.mockReturnValue({ data: openTally, isLoading: false, isError: false });
});

describe('VotingPanel', () => {
  it('renders a vote card per candidate with resolved titles', () => {
    renderPanel();
    expect(screen.getByTestId('vote-card-game-a')).toBeInTheDocument();
    expect(screen.getByTestId('vote-card-game-b')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  it('shows the leader badge on the leading candidate', () => {
    renderPanel();
    expect(screen.getByTestId('leader-badge-game-a')).toBeInTheDocument();
    expect(screen.queryByTestId('leader-badge-game-b')).not.toBeInTheDocument();
  });

  it('casts an approval when toggling an un-voted candidate', async () => {
    const user = userEvent.setup();
    renderPanel();

    await user.click(screen.getByTestId('vote-toggle-game-b'));

    expect(castMutateMock).toHaveBeenCalledWith(
      { id: 'gn-1', candidateGameId: 'game-b' },
      expect.any(Object)
    );
    expect(retractMutateMock).not.toHaveBeenCalled();
  });

  it('retracts an approval when toggling an already-voted candidate', async () => {
    const user = userEvent.setup();
    renderPanel();

    await user.click(screen.getByTestId('vote-toggle-game-a'));

    expect(retractMutateMock).toHaveBeenCalledWith(
      { id: 'gn-1', candidateGameId: 'game-a' },
      expect.any(Object)
    );
    expect(castMutateMock).not.toHaveBeenCalled();
  });

  it('disables toggles once voting has closed', () => {
    tallyMock.mockReturnValue({
      data: { ...openTally, isVotingClosed: true },
      isLoading: false,
      isError: false,
    });
    renderPanel();
    expect(screen.getByTestId('vote-toggle-game-a')).toBeDisabled();
  });

  it('renders the loading state', () => {
    tallyMock.mockReturnValue({ data: undefined, isLoading: true, isError: false });
    renderPanel();
    expect(screen.getByTestId('voting-loading')).toBeInTheDocument();
  });

  it('renders the error state', () => {
    tallyMock.mockReturnValue({ data: undefined, isLoading: false, isError: true });
    renderPanel();
    expect(screen.getByTestId('voting-error')).toBeInTheDocument();
  });

  it('renders the empty state when there are no candidates', () => {
    tallyMock.mockReturnValue({
      data: { ...openTally, candidates: [], leadingCandidateGameIds: [] },
      isLoading: false,
      isError: false,
    });
    renderPanel();
    expect(screen.getByTestId('voting-empty')).toBeInTheDocument();
  });

  it('shows the tie resolver to the organizer once voting closes on a tie', async () => {
    const user = userEvent.setup();
    tallyMock.mockReturnValue({
      data: {
        ...openTally,
        isTie: true,
        isVotingClosed: true,
        leadingCandidateGameIds: ['game-a', 'game-b'],
      },
      isLoading: false,
      isError: false,
    });
    renderPanel({ isOrganizer: true });

    expect(screen.getByTestId('voting-tied-resolver')).toBeInTheDocument();
    await user.click(screen.getByTestId('resolve-tie-game-a'));
    expect(resolveMutateMock).toHaveBeenCalledWith(
      { id: 'gn-1', winningCandidateGameId: 'game-a' },
      expect.any(Object)
    );
  });

  it('hides the tie resolver once a winner has been resolved', () => {
    tallyMock.mockReturnValue({
      data: {
        ...openTally,
        isTie: true,
        isVotingClosed: true,
        winnerGameId: 'game-a',
        leadingCandidateGameIds: ['game-a', 'game-b'],
      },
      isLoading: false,
      isError: false,
    });
    renderPanel({ isOrganizer: true });
    expect(screen.queryByTestId('voting-tied-resolver')).not.toBeInTheDocument();
  });

  it('hides the tie resolver from non-organizers', () => {
    tallyMock.mockReturnValue({
      data: {
        ...openTally,
        isTie: true,
        isVotingClosed: true,
        leadingCandidateGameIds: ['game-a', 'game-b'],
      },
      isLoading: false,
      isError: false,
    });
    renderPanel({ isOrganizer: false });
    expect(screen.queryByTestId('voting-tied-resolver')).not.toBeInTheDocument();
  });
});

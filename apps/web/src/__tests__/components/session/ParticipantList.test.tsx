import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('@/components/ui/data-display/badge', () => ({
  Badge: ({
    children,
    className,
    ...props
  }: { children: React.ReactNode; className?: string } & Record<string, unknown>) => (
    <span className={className} {...props}>
      {children}
    </span>
  ),
}));

vi.mock('@/components/ui/forms/switch', () => ({
  Switch: ({
    id,
    checked,
    onCheckedChange,
    ...props
  }: {
    id?: string;
    checked?: boolean;
    onCheckedChange?: (checked: boolean) => void;
    [key: string]: unknown;
  }) => (
    <button
      id={id}
      role="switch"
      aria-checked={checked}
      onClick={() => onCheckedChange?.(!checked)}
      data-testid={props['data-testid'] as string}
    />
  ),
}));

import { ParticipantList, type SessionParticipant } from '@/components/session/ParticipantList';

// ============================================================================
// Test Data
// ============================================================================

const SESSION_ID = '123e4567-e89b-12d3-a456-426614174000';

const PARTICIPANTS: SessionParticipant[] = [
  {
    id: 'p1',
    displayName: 'Alice',
    role: 'Host',
    agentAccessEnabled: true,
    joinedAt: '2026-03-13T10:00:00Z',
  },
  {
    id: 'p2',
    displayName: 'Bob',
    role: 'Player',
    agentAccessEnabled: false,
    joinedAt: '2026-03-13T10:01:00Z',
  },
  {
    id: 'p3',
    displayName: 'Charlie',
    role: 'Guest',
    agentAccessEnabled: false,
    joinedAt: '2026-03-13T10:02:00Z',
    leftAt: '2026-03-13T10:30:00Z',
  },
];

// ============================================================================
// Tests
// ============================================================================

describe('ParticipantList', () => {
  const defaultProps = {
    sessionId: SESSION_ID,
    isHost: false,
    participants: PARTICIPANTS,
    onToggleAgentAccess: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders participant names and roles', () => {
    render(<ParticipantList {...defaultProps} />);

    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('Charlie')).toBeInTheDocument();

    expect(screen.getByText('Host')).toBeInTheDocument();
    expect(screen.getByText('Player')).toBeInTheDocument();
    expect(screen.getByText('Guest')).toBeInTheDocument();
  });

  it('shows correct participant count (active only)', () => {
    render(<ParticipantList {...defaultProps} />);

    // 2 active (Alice + Bob), Charlie has leftAt
    expect(screen.getByText('Participants (2)')).toBeInTheDocument();
  });

  it('shows agent toggle only when isHost=true', () => {
    render(<ParticipantList {...defaultProps} isHost={true} />);

    // Alice (Host, active) and Bob (Player, active) should have toggles
    expect(screen.getByTestId('agent-toggle-p1')).toBeInTheDocument();
    expect(screen.getByTestId('agent-toggle-p2')).toBeInTheDocument();
  });

  it('hides agent toggle when isHost=false', () => {
    render(<ParticipantList {...defaultProps} isHost={false} />);

    expect(screen.queryByTestId('agent-toggle-p1')).not.toBeInTheDocument();
    expect(screen.queryByTestId('agent-toggle-p2')).not.toBeInTheDocument();
  });

  it('does not show agent toggle for participants who left', () => {
    render(<ParticipantList {...defaultProps} isHost={true} />);

    // Charlie has leftAt, so no toggle
    expect(screen.queryByTestId('agent-toggle-p3')).not.toBeInTheDocument();
  });

  it('shows "Left" indicator for disconnected participants', () => {
    render(<ParticipantList {...defaultProps} />);

    // Charlie has leftAt
    const charlieRow = screen.getByTestId('participant-p3');
    expect(within(charlieRow).getByText('Left')).toBeInTheDocument();

    // Alice and Bob should NOT show "Left"
    const aliceRow = screen.getByTestId('participant-p1');
    expect(within(aliceRow).queryByText('Left')).not.toBeInTheDocument();
  });

  it('calls onToggleAgentAccess when switch is clicked', async () => {
    const onToggle = vi.fn();
    const user = userEvent.setup();

    render(<ParticipantList {...defaultProps} isHost={true} onToggleAgentAccess={onToggle} />);

    // Bob's toggle (currently off, agentAccessEnabled=false)
    await user.click(screen.getByTestId('agent-toggle-p2'));

    expect(onToggle).toHaveBeenCalledTimes(1);
    expect(onToggle).toHaveBeenCalledWith('p2', true);
  });

  it('shows avatar with first letter of name', () => {
    render(<ParticipantList {...defaultProps} />);

    // Check that the avatar initials are rendered
    const aliceRow = screen.getByTestId('participant-p1');
    expect(within(aliceRow).getByText('A')).toBeInTheDocument();

    const bobRow = screen.getByTestId('participant-p2');
    expect(within(bobRow).getByText('B')).toBeInTheDocument();
  });

  it('renders empty state when no participants', () => {
    render(<ParticipantList {...defaultProps} participants={[]} />);

    expect(screen.getByText(/no participants yet/i)).toBeInTheDocument();
    expect(screen.getByText('Participants (0)')).toBeInTheDocument();
  });
});

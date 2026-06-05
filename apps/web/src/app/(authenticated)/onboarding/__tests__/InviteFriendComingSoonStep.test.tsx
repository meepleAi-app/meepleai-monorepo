/**
 * InviteFriendComingSoonStep — unit tests.
 *
 * Asse D follow-up P3 (umbrella #1895 sub-issue #1899 follow-up). Verifies
 * the skip-only placeholder mounts with the expected heading + copy + a
 * test-id hook used by `OnboardingGenericWizard` integration tests.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { InviteFriendComingSoonStep } from '../InviteFriendComingSoonStep';

describe('InviteFriendComingSoonStep (Asse D P3 #1899)', () => {
  it('renders the placeholder container with a stable test-id', () => {
    render(<InviteFriendComingSoonStep />);
    expect(screen.getByTestId('invite-friend-coming-soon')).toBeInTheDocument();
  });

  it('renders the "Invita un amico" heading at level 2', () => {
    render(<InviteFriendComingSoonStep />);
    expect(screen.getByRole('heading', { level: 2, name: /invita un amico/i })).toBeInTheDocument();
  });

  it('explains the feature is deferred to a future release', () => {
    render(<InviteFriendComingSoonStep />);
    expect(screen.getByText(/disponibile in una release futura/i)).toBeInTheDocument();
  });

  it('hints both Skip and Complete buttons as exit paths', () => {
    render(<InviteFriendComingSoonStep />);
    const skipHint = screen.getByText(/skip/i, { selector: 'strong' });
    const completeHint = screen.getByText(/complete/i, { selector: 'strong' });
    expect(skipHint).toBeInTheDocument();
    expect(completeHint).toBeInTheDocument();
  });
});

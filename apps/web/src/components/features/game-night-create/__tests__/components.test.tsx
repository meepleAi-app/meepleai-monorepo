/**
 * Smoke tests for the SP7 game-night-create components.
 * Issue #950 W3 (Commit 2).
 *
 * Each component is exercised via a small render + a critical interaction
 * (button click / select option / typing) to lock the prop contract. Full
 * ≥85% coverage is deferred to W3-PR2 (orchestrator wiring) where the
 * components are exercised in their realistic compositions.
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import {
  GameCandidatesPicker,
  GameNightDateTimePicker,
  GameNightLocationToggle,
  PlayerInviteAutocomplete,
  RSVPCardLivePreview,
} from '../index';
import { initialWizardState } from '@/lib/game-nights/wizard-reducer';

const dateLabels = {
  label: 'Quando',
  helper: 'Almeno 1 ora nel futuro',
  conflictWarningTitle: 'Hai già un impegno',
  conflictWarningBody: (n: number) => `${n} eventi`,
  conflictRoleOrganizer: 'Organizzi',
  conflictRoleInvitee: 'Invitato',
  continueAnyway: 'Continua comunque',
  checking: 'Verifica…',
};

const locationLabels = {
  label: 'Dove',
  kindHome: 'Casa',
  kindFriend: 'Casa di un amico',
  kindOnline: 'Online',
  kindTbd: 'Da definire',
  detailsLabel: 'Dettagli',
  detailsPlaceholder: 'Indirizzo',
  detailsHelper: 'Visibili solo agli invitati',
};

const playerLabels = {
  label: 'Chi',
  searchPlaceholder: 'Cerca…',
  searchAriaLabel: 'Cerca giocatori',
  regularsTitle: 'Abituali',
  regularsHelper: 'Per frequenza',
  regularsEmpty: 'Nessuno',
  addRegular: 'Aggiungi',
  remove: 'Rimuovi',
  addEmail: 'Invita per email',
  emailInvalid: 'Email non valida',
  emailDuplicate: 'Email già aggiunta',
  inviteeCount: (n: number) => `${n} invitati`,
  limitWarning: (max: number) => `Max ${max} invitati`,
  searching: 'Ricerca…',
};

const gameLabels = {
  label: 'Cosa',
  decideAtGroupLabel: 'Decide il gruppo',
  decideAtGroupHelper: 'Decideremo insieme',
  selectedCount: (n: number) => `${n} giochi`,
  libraryHeader: 'La tua libreria',
  libraryEmpty: 'Vuota',
  selectGame: (name: string) => `Seleziona ${name}`,
  deselectGame: (name: string) => `Deseleziona ${name}`,
};

const previewLabels = {
  title: 'Anteprima',
  subtitle: 'Cosa vedono gli invitati',
  rsvpAccept: 'Partecipo',
  rsvpDecline: 'Non posso',
  noDate: 'Data da scegliere',
  noLocation: 'Luogo da definire',
  gamesTbd: 'Giochi: al gruppo',
  gamesNone: 'Nessun gioco',
  sectionWhen: 'Quando',
  sectionWhere: 'Dove',
  sectionWhat: 'Cosa',
  sectionWho: 'Chi',
  kindHome: 'Casa mia',
  kindFriend: 'Casa di un amico',
  kindOnline: 'Online',
};

describe('GameNightDateTimePicker', () => {
  it('renders the helper text', () => {
    render(
      <GameNightDateTimePicker
        iso={null}
        onSetDate={vi.fn()}
        conflictResult={null}
        labels={dateLabels}
      />
    );
    expect(screen.getByText(dateLabels.helper)).toBeInTheDocument();
  });

  it('surfaces a conflict warning when conflictResult.hasConflict is true', () => {
    render(
      <GameNightDateTimePicker
        iso="2099-12-31T20:00:00Z"
        onSetDate={vi.fn()}
        conflictResult={{
          hasConflict: true,
          conflicts: [
            {
              id: '00000000-0000-4000-8000-000000000301',
              title: 'Catan',
              scheduledAt: '2099-12-31T19:00:00Z',
              role: 'organizer',
            },
          ],
        }}
        onContinueAnyway={vi.fn()}
        labels={dateLabels}
      />
    );
    expect(screen.getByRole('alert')).toHaveTextContent(dateLabels.conflictWarningTitle);
    expect(screen.getByRole('button', { name: dateLabels.continueAnyway })).toBeInTheDocument();
  });

  it('fires onContinueAnyway when override button is clicked', async () => {
    const user = userEvent.setup();
    const onContinueAnyway = vi.fn();
    render(
      <GameNightDateTimePicker
        iso="2099-12-31T20:00:00Z"
        onSetDate={vi.fn()}
        conflictResult={{
          hasConflict: true,
          conflicts: [
            {
              id: '00000000-0000-4000-8000-000000000301',
              title: 'Catan',
              scheduledAt: '2099-12-31T19:00:00Z',
              role: 'invitee',
            },
          ],
        }}
        onContinueAnyway={onContinueAnyway}
        labels={dateLabels}
      />
    );
    await user.click(screen.getByRole('button', { name: dateLabels.continueAnyway }));
    expect(onContinueAnyway).toHaveBeenCalledOnce();
  });
});

describe('GameNightLocationToggle', () => {
  it('renders 4 radio options', () => {
    render(
      <GameNightLocationToggle
        kind="home"
        details=""
        onSetLocation={vi.fn()}
        labels={locationLabels}
      />
    );
    expect(screen.getAllByRole('radio')).toHaveLength(4);
  });

  it('selecting "tbd" hides the details textarea', () => {
    const onSetLocation = vi.fn();
    render(
      <GameNightLocationToggle
        kind="tbd"
        details=""
        onSetLocation={onSetLocation}
        labels={locationLabels}
      />
    );
    expect(screen.queryByLabelText(locationLabels.detailsLabel)).not.toBeInTheDocument();
  });

  it('emits onSetLocation with new kind when option clicked', async () => {
    const user = userEvent.setup();
    const onSetLocation = vi.fn();
    render(
      <GameNightLocationToggle
        kind="home"
        details=""
        onSetLocation={onSetLocation}
        labels={locationLabels}
      />
    );
    await user.click(screen.getByRole('radio', { name: locationLabels.kindOnline }));
    expect(onSetLocation).toHaveBeenCalledWith('online', '');
  });
});

describe('PlayerInviteAutocomplete', () => {
  it('renders the regulars list when query is empty', () => {
    render(
      <PlayerInviteAutocomplete
        invitees={[]}
        searchResults={[]}
        regulars={[
          {
            id: '00000000-0000-4000-8000-000000000101',
            displayName: 'Laura',
            email: 'laura@example.com',
            eventCount: 3,
            lastInvitedAt: '2025-01-01T00:00:00Z',
          },
        ]}
        query=""
        onQueryChange={vi.fn()}
        onAddInvitee={vi.fn()}
        onRemoveInvitee={vi.fn()}
        labels={playerLabels}
      />
    );
    expect(screen.getByText('Laura')).toBeInTheDocument();
  });

  it('emits onAddInvitee when adding a regular', async () => {
    const user = userEvent.setup();
    const onAddInvitee = vi.fn();
    render(
      <PlayerInviteAutocomplete
        invitees={[]}
        searchResults={[]}
        regulars={[
          {
            id: '00000000-0000-4000-8000-000000000101',
            displayName: 'Laura',
            email: 'laura@example.com',
            eventCount: 3,
            lastInvitedAt: '2025-01-01T00:00:00Z',
          },
        ]}
        query=""
        onQueryChange={vi.fn()}
        onAddInvitee={onAddInvitee}
        onRemoveInvitee={vi.fn()}
        labels={playerLabels}
      />
    );
    await user.click(screen.getByRole('button', { name: playerLabels.addRegular }));
    expect(onAddInvitee).toHaveBeenCalledWith({
      kind: 'user',
      id: '00000000-0000-4000-8000-000000000101',
      displayName: 'Laura',
      email: 'laura@example.com',
    });
  });

  it('shows email-add button only when query looks like an email', () => {
    render(
      <PlayerInviteAutocomplete
        invitees={[]}
        searchResults={[]}
        regulars={[]}
        query="guest@example.com"
        onQueryChange={vi.fn()}
        onAddInvitee={vi.fn()}
        onRemoveInvitee={vi.fn()}
        labels={playerLabels}
      />
    );
    expect(screen.getByRole('button', { name: playerLabels.addEmail })).toBeInTheDocument();
  });
});

describe('GameCandidatesPicker', () => {
  it('toggling decideAtGroup emits the action', async () => {
    const user = userEvent.setup();
    const onToggle = vi.fn();
    render(
      <GameCandidatesPicker
        games={[]}
        selected={[]}
        decideAtGroup={false}
        onToggleGame={vi.fn()}
        onToggleDecideAtGroup={onToggle}
        labels={gameLabels}
      />
    );
    // The checkbox sits inside a <label> wrapping both its primary text and
    // a helper line — accessible name is the concatenation. Use the
    // single-checkbox role lookup instead of a brittle name match.
    await user.click(screen.getByRole('checkbox'));
    expect(onToggle).toHaveBeenCalledOnce();
  });

  it('emits onToggleGame when a card is clicked', async () => {
    const user = userEvent.setup();
    const onToggleGame = vi.fn();
    render(
      <GameCandidatesPicker
        games={[{ id: '00000000-0000-4000-8000-000000000201', title: 'Catan' }]}
        selected={[]}
        decideAtGroup={false}
        onToggleGame={onToggleGame}
        onToggleDecideAtGroup={vi.fn()}
        labels={gameLabels}
      />
    );
    await user.click(screen.getByRole('button', { name: gameLabels.selectGame('Catan') }));
    expect(onToggleGame).toHaveBeenCalledWith('00000000-0000-4000-8000-000000000201');
  });
});

describe('RSVPCardLivePreview', () => {
  it('renders the title fallback when title is empty', () => {
    render(
      <RSVPCardLivePreview
        state={initialWizardState}
        title=""
        organizerName="Marco"
        labels={previewLabels}
      />
    );
    expect(screen.getByText('Marco')).toBeInTheDocument();
    expect(screen.getByText(previewLabels.noDate)).toBeInTheDocument();
  });

  it('renders games TBD label when decideAtGroup is true', () => {
    render(
      <RSVPCardLivePreview
        state={{
          ...initialWizardState,
          games: { decideAtGroup: true, selected: [] },
        }}
        title="Friday Catan"
        organizerName="Marco"
        labels={previewLabels}
      />
    );
    expect(screen.getByText(previewLabels.gamesTbd)).toBeInTheDocument();
  });
});

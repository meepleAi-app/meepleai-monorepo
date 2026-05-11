/**
 * LiveSessionNotes unit tests — Wave D.2 Interactions sub-PR (Issue #750)
 *
 * Coverage:
 * - Render shape (data-slot, notes list, empty state)
 * - Role variant: Spectator = read-only (no add form)
 * - Player+Host: add form visible with visibility toggle
 * - onAddNote fires with correct content + visibility
 * - aria-pressed on visibility toggle buttons
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type { LiveSessionNotesLabels, LiveSessionNotesProps, NoteEntry } from '../LiveSessionNotes';
import { LiveSessionNotes } from '../LiveSessionNotes';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const LABELS: LiveSessionNotesLabels = {
  title: 'Note di Sessione',
  inputAriaLabel: 'Scrivi una nota',
  addAriaLabel: 'Aggiungi nota',
  visibilityPrivate: 'Privata',
  visibilityShared: 'Condivisa',
  emptyMessage: 'Nessuna nota',
};

const NOTES: ReadonlyArray<NoteEntry> = [
  {
    id: 'note-1',
    authorId: 'user-a',
    authorName: 'Alice',
    content: 'Prima nota',
    visibility: 'shared',
    timestamp: '2026-05-06T10:00:00Z',
  },
  {
    id: 'note-2',
    authorId: 'viewer-id',
    authorName: 'Me',
    content: 'Nota privata',
    visibility: 'private',
    timestamp: '2026-05-06T10:01:00Z',
  },
];

function renderNotes(overrides: Partial<LiveSessionNotesProps> = {}) {
  const onAddNote = vi.fn();
  const props: LiveSessionNotesProps = {
    notes: [],
    viewerRole: 'Player',
    viewerId: 'viewer-id',
    onAddNote,
    labels: LABELS,
    ...overrides,
  };
  const result = render(<LiveSessionNotes {...props} />);
  return { ...result, onAddNote };
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('LiveSessionNotes — render shape', () => {
  it('renders data-slot="live-session-notes"', () => {
    renderNotes();
    expect(screen.getByRole('region', { name: 'Note di Sessione' })).toHaveAttribute(
      'data-slot',
      'live-session-notes'
    );
  });

  it('renders data-viewer-role attribute', () => {
    renderNotes({ viewerRole: 'Host' });
    expect(screen.getByRole('region', { name: 'Note di Sessione' })).toHaveAttribute(
      'data-viewer-role',
      'Host'
    );
  });

  it('renders empty message when no notes', () => {
    renderNotes({ notes: [] });
    expect(screen.getByText('Nessuna nota')).toBeInTheDocument();
  });

  it('renders notes list', () => {
    renderNotes({ notes: NOTES });
    expect(screen.getByText('Prima nota')).toBeInTheDocument();
    expect(screen.getByText('Nota privata')).toBeInTheDocument();
  });

  it('renders author names', () => {
    renderNotes({ notes: NOTES });
    expect(screen.getByText('Alice')).toBeInTheDocument();
  });
});

describe('LiveSessionNotes — role variant: Spectator', () => {
  it('renders section (read-only list visible to Spectator)', () => {
    renderNotes({ viewerRole: 'Spectator', notes: NOTES });
    expect(screen.getByRole('region', { name: 'Note di Sessione' })).toBeInTheDocument();
    expect(screen.getByText('Prima nota')).toBeInTheDocument();
  });

  it('hides add-note form for Spectator', () => {
    renderNotes({ viewerRole: 'Spectator' });
    expect(screen.queryByRole('textbox', { name: 'Scrivi una nota' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Aggiungi nota' })).not.toBeInTheDocument();
  });
});

describe('LiveSessionNotes — role variant: Player + Host', () => {
  it.each(['Player', 'Host'] as const)('shows add form for %s', role => {
    renderNotes({ viewerRole: role });
    expect(screen.getByRole('textbox', { name: 'Scrivi una nota' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Aggiungi nota' })).toBeInTheDocument();
  });

  it.each(['Player', 'Host'] as const)('shows visibility toggle for %s', role => {
    renderNotes({ viewerRole: role });
    expect(screen.getByRole('button', { name: /Privata/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Condivisa/i })).toBeInTheDocument();
  });
});

describe('LiveSessionNotes — onAddNote', () => {
  it('calls onAddNote with content and "shared"', async () => {
    const user = userEvent.setup();
    const { onAddNote } = renderNotes({ viewerRole: 'Player' });

    await user.type(screen.getByRole('textbox', { name: 'Scrivi una nota' }), 'My note');
    await user.click(screen.getByRole('button', { name: 'Aggiungi nota' }));

    expect(onAddNote).toHaveBeenCalledOnce();
    expect(onAddNote).toHaveBeenCalledWith('My note', 'shared');
  });

  it('calls onAddNote with "private" when switched', async () => {
    const user = userEvent.setup();
    const { onAddNote } = renderNotes({ viewerRole: 'Player' });

    await user.click(screen.getByRole('button', { name: /Privata/i }));
    await user.type(screen.getByRole('textbox', { name: 'Scrivi una nota' }), 'Private note');
    await user.click(screen.getByRole('button', { name: 'Aggiungi nota' }));

    expect(onAddNote).toHaveBeenCalledWith('Private note', 'private');
  });

  it('does not call onAddNote when input is empty', async () => {
    const user = userEvent.setup();
    const { onAddNote } = renderNotes({ viewerRole: 'Player' });

    await user.click(screen.getByRole('button', { name: 'Aggiungi nota' }));
    expect(onAddNote).not.toHaveBeenCalled();
  });

  it('clears input after adding note', async () => {
    const user = userEvent.setup();
    renderNotes({ viewerRole: 'Player' });

    const textarea = screen.getByRole('textbox', { name: 'Scrivi una nota' });
    await user.type(textarea, 'A note');
    await user.click(screen.getByRole('button', { name: 'Aggiungi nota' }));

    expect(textarea).toHaveValue('');
  });
});

describe('LiveSessionNotes — aria', () => {
  it('aria-pressed reflects current visibility selection', async () => {
    const user = userEvent.setup();
    renderNotes({ viewerRole: 'Player' });

    const sharedBtn = screen.getByRole('button', { name: /Condivisa/i });
    expect(sharedBtn).toHaveAttribute('aria-pressed', 'true');

    await user.click(screen.getByRole('button', { name: /Privata/i }));
    expect(screen.getByRole('button', { name: /Privata/i })).toHaveAttribute(
      'aria-pressed',
      'true'
    );
    expect(sharedBtn).toHaveAttribute('aria-pressed', 'false');
  });
});

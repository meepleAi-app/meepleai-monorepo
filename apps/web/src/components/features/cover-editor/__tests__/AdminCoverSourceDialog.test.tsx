/**
 * @vitest-environment jsdom
 */
import { render, screen, fireEvent, within } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/hooks/admin/useCoverCandidates', () => ({ useCoverCandidates: vi.fn() }));
vi.mock('@/hooks/admin/useAssignCover', () => ({ useAssignCover: vi.fn() }));
vi.mock('@/hooks/admin/useRemoveCoverAssignment', () => ({ useRemoveCoverAssignment: vi.fn() }));
vi.mock('@/hooks/admin/useSetManualCover', () => ({ useSetManualCover: vi.fn() }));

import { useCoverCandidates } from '@/hooks/admin/useCoverCandidates';
import { useAssignCover } from '@/hooks/admin/useAssignCover';
import { useRemoveCoverAssignment } from '@/hooks/admin/useRemoveCoverAssignment';
import { useSetManualCover } from '@/hooks/admin/useSetManualCover';

import { AdminCoverSourceDialog } from '../AdminCoverSourceDialog';

const GID = '550e8400-e29b-41d4-a716-446655440000';

const mockUseCandidates = useCoverCandidates as unknown as ReturnType<typeof vi.fn>;
const mockUseAssign = useAssignCover as unknown as ReturnType<typeof vi.fn>;
const mockUseRemove = useRemoveCoverAssignment as unknown as ReturnType<typeof vi.fn>;
const mockUseSetManual = useSetManualCover as unknown as ReturnType<typeof vi.fn>;

const candidatesData = {
  gameId: GID,
  candidates: [
    {
      source: 'Pdf',
      previewUrl: 'https://r2.example/pdf.webp',
      license: null,
      attribution: null,
      sourceUrl: null,
    },
    {
      source: 'Wikidata',
      previewUrl: 'https://r2.example/wiki.webp',
      license: 'CC BY-SA 4.0',
      attribution: 'Jane Artist',
      sourceUrl: 'https://commons.wikimedia.org/wiki/File:x.jpg',
    },
  ],
  assignments: { card: { source: 'Pdf', focalX: 0.25, focalY: 0.75 }, hero: null, social: null },
};

let assignMutate: ReturnType<typeof vi.fn>;
let removeMutate: ReturnType<typeof vi.fn>;
let setManualMutate: ReturnType<typeof vi.fn>;

function setCandidates(over: Partial<ReturnType<typeof mockUseCandidates>> = {}) {
  mockUseCandidates.mockReturnValue({
    data: candidatesData,
    isLoading: false,
    isError: false,
    ...over,
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  assignMutate = vi.fn();
  removeMutate = vi.fn();
  setManualMutate = vi.fn();
  mockUseAssign.mockReturnValue({ mutate: assignMutate, isPending: false });
  mockUseRemove.mockReturnValue({ mutate: removeMutate, isPending: false });
  mockUseSetManual.mockReturnValue({
    mutate: setManualMutate,
    isPending: false,
    isError: false,
    isSuccess: false,
    reset: vi.fn(),
  });
  setCandidates();
});

function renderDialog(props: Partial<React.ComponentProps<typeof AdminCoverSourceDialog>> = {}) {
  return render(
    <AdminCoverSourceDialog gameId={GID} title="Catan" open onClose={vi.fn()} {...props} />
  );
}

describe('AdminCoverSourceDialog', () => {
  // Epic #3470 Slice 3d — the "from URL / Manuale" form: materializes a Manual candidate.
  it('sets a manual cover from a URL with the whitelisted license and attribution', () => {
    renderDialog();

    fireEvent.change(screen.getByLabelText(/URL immagine/i), {
      target: { value: 'https://commons.example.org/img.png' },
    });
    fireEvent.change(screen.getByLabelText(/^Licenza$/i), { target: { value: 'CC-BY-4.0' } });
    fireEvent.change(screen.getByLabelText(/Attribuzione/i), { target: { value: 'Jane Artist' } });
    fireEvent.click(screen.getByRole('button', { name: /Carica da URL/i }));

    expect(setManualMutate).toHaveBeenCalledTimes(1);
    expect(setManualMutate.mock.calls[0][0]).toEqual({
      gameId: GID,
      body: {
        sourceUrl: 'https://commons.example.org/img.png',
        license: 'CC-BY-4.0',
        attribution: 'Jane Artist',
      },
    });
  });

  it('submits a null attribution when the attribution field is left blank', () => {
    renderDialog();

    fireEvent.change(screen.getByLabelText(/URL immagine/i), {
      target: { value: 'https://commons.example.org/pd.png' },
    });
    fireEvent.click(screen.getByRole('button', { name: /Carica da URL/i }));

    expect(setManualMutate.mock.calls[0][0].body.attribution).toBeNull();
  });

  it('disables the manual-cover submit until a URL is entered', () => {
    renderDialog();
    const submit = screen.getByRole('button', { name: /Carica da URL/i });
    expect(submit).toBeDisabled();

    fireEvent.change(screen.getByLabelText(/URL immagine/i), {
      target: { value: 'https://commons.example.org/img.png' },
    });
    expect(submit).toBeEnabled();
  });

  it('clears the manual-cover form fields when the dialog is closed and reopened', () => {
    const { rerender } = renderDialog();
    fireEvent.change(screen.getByLabelText(/URL immagine/i), {
      target: { value: 'https://commons.example.org/img.png' },
    });
    expect(screen.getByLabelText(/URL immagine/i)).toHaveValue(
      'https://commons.example.org/img.png'
    );

    // Close then reopen: a half-typed URL must not resurface (form state is reset on close).
    rerender(<AdminCoverSourceDialog gameId={GID} title="Catan" open={false} onClose={vi.fn()} />);
    rerender(<AdminCoverSourceDialog gameId={GID} title="Catan" open onClose={vi.fn()} />);

    expect(screen.getByLabelText(/URL immagine/i)).toHaveValue('');
  });

  it('preserves a staged grid pick when the candidates reload after adding a manual cover', () => {
    const { rerender } = renderDialog();
    // Stage (do NOT apply) the Wikidata candidate.
    fireEvent.click(screen.getByRole('button', { name: /Copertina Wikidata/i }));
    expect(screen.getByRole('button', { name: /Copertina Wikidata/i })).toHaveAttribute(
      'aria-pressed',
      'true'
    );

    // Simulate the candidates refetch after a Manual source is added (new candidate appended).
    setCandidates({
      data: {
        ...candidatesData,
        candidates: [
          ...candidatesData.candidates,
          {
            source: 'Manual',
            previewUrl: 'https://r2.example/manual.webp',
            license: 'CC0',
            attribution: null,
            sourceUrl: null,
          },
        ],
      },
    });
    rerender(<AdminCoverSourceDialog gameId={GID} title="Catan" open onClose={vi.fn()} />);

    // The staged, un-applied pick must survive the refetch (not silently reset).
    expect(screen.getByRole('button', { name: /Copertina Wikidata/i })).toHaveAttribute(
      'aria-pressed',
      'true'
    );
  });

  it('renders a titled dialog when open', () => {
    renderDialog();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText(/Catan/)).toBeInTheDocument();
  });

  it('pre-fills the saved focal for the active context (#3470 Slice 2e)', () => {
    // The Card context has a saved focal of 0.25 / 0.75; the picker must pre-fill it
    // (25% / 75%) instead of starting at the 0.5 / 0.5 default.
    renderDialog();
    const handle = screen.getByRole('button', { name: /punto focale/i });
    expect(handle).toHaveStyle({ left: '25%', top: '75%' });
  });

  it('renders nothing when closed', () => {
    renderDialog({ open: false });
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('shows a loading state while candidates are fetching', () => {
    setCandidates({ data: undefined, isLoading: true });
    renderDialog();
    expect(screen.getByText(/caricamento/i)).toBeInTheDocument();
  });

  it('renders the three UI-context tabs', () => {
    renderDialog();
    expect(screen.getByRole('tab', { name: /card/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /hero/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /social/i })).toBeInTheDocument();
  });

  it('renders candidate thumbnails from previewUrl with provenance + license chips', () => {
    renderDialog();
    const wikiCard = screen.getByRole('button', { name: /wikidata/i });
    const img = within(wikiCard).getByRole('img');
    expect(img).toHaveAttribute('src', 'https://r2.example/wiki.webp');
    expect(within(wikiCard).getByText('CC BY-SA 4.0')).toBeInTheDocument();
    // never a BGG host
    expect(img.getAttribute('src')).not.toMatch(/geekdo|boardgamegeek/);
  });

  it('marks the currently-assigned source as pressed for the active context', () => {
    renderDialog(); // default active = Card, assignments.card = 'Pdf'
    expect(screen.getByRole('button', { name: /pdf/i })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: /wikidata/i })).toHaveAttribute(
      'aria-pressed',
      'false'
    );
  });

  it('assigns the selected source with the default focal point on Applica', () => {
    renderDialog();
    fireEvent.click(screen.getByRole('button', { name: /wikidata/i }));
    fireEvent.click(screen.getByRole('button', { name: /applica/i }));
    expect(assignMutate).toHaveBeenCalledWith({
      gameId: GID,
      context: 'Card',
      body: { source: 'Wikidata', focalX: 0.5, focalY: 0.5 },
    });
  });

  it('resets the active context to implicit precedence', () => {
    renderDialog(); // Card has an assignment → reset available
    fireEvent.click(screen.getByRole('button', { name: /automatico|reimposta/i }));
    expect(removeMutate).toHaveBeenCalledWith({ gameId: GID, context: 'Card' });
  });

  it('shows an empty message when there are no candidates', () => {
    setCandidates({ data: { ...candidatesData, candidates: [] } });
    renderDialog();
    expect(screen.getByText(/nessuna sorgente|nessun candidato/i)).toBeInTheDocument();
  });

  it('keeps Applica disabled until the admin picks a candidate or moves the focal', () => {
    renderDialog(); // Card already assigned to Pdf, no interaction yet
    expect(screen.getByRole('button', { name: /applica/i })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: /wikidata/i }));
    expect(screen.getByRole('button', { name: /applica/i })).toBeEnabled();
  });

  it('resets the pending selection when the dialog is closed and reopened', () => {
    const onClose = vi.fn();
    const { rerender } = render(
      <AdminCoverSourceDialog gameId={GID} title="Catan" open onClose={onClose} />
    );
    fireEvent.click(screen.getByRole('button', { name: /wikidata/i }));
    expect(screen.getByRole('button', { name: /wikidata/i })).toHaveAttribute(
      'aria-pressed',
      'true'
    );

    rerender(<AdminCoverSourceDialog gameId={GID} title="Catan" open={false} onClose={onClose} />);
    rerender(<AdminCoverSourceDialog gameId={GID} title="Catan" open onClose={onClose} />);

    // Stale pending pick cleared → the persisted assignment (Pdf) is selected again.
    expect(screen.getByRole('button', { name: /pdf/i })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: /wikidata/i })).toHaveAttribute(
      'aria-pressed',
      'false'
    );
  });
});

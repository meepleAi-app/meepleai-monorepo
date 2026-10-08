import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { FirstRulebookStep } from '../FirstRulebookStep';

const uploadPdf = vi.fn();
const acceptDisclaimer = vi.fn();

// Deliberately WITHOUT an `agents` key. Issue #4138: the step this replaced
// called `api.agents.createUserAgent` with `agentType: 'RuleExpert'`, which the
// backend rejects. Leaving `agents` undefined means any such call throws a
// TypeError and fails whichever test triggers it — a real guard, which is why
// there is no separate "does not create an agent" case asserting on the mock.
vi.mock('@/lib/api', () => ({
  api: {
    pdf: {
      uploadPdf: (...args: unknown[]) => uploadPdf(...args),
    },
    documents: {
      acceptDisclaimer: (...args: unknown[]) => acceptDisclaimer(...args),
    },
  },
}));

vi.mock('@/lib/analytics/flywheel-events', () => ({
  trackPdfUploaded: vi.fn(),
}));

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

// The disclaimer modal is exercised through its contract, not its markup: the
// step must not reach the upload zone until `onAccept` fires.
vi.mock('@/components/pdf/CopyrightDisclaimerModal', () => ({
  CopyrightDisclaimerModal: ({
    open,
    onAccept,
    onCancel,
  }: {
    open: boolean;
    onAccept: () => void;
    onCancel: () => void;
  }) =>
    open ? (
      <div data-testid="disclaimer">
        <button onClick={onAccept}>Accetta</button>
        <button onClick={onCancel}>Annulla</button>
      </div>
    ) : null,
}));

vi.mock('@/components/library/add-game-sheet/steps/PdfUploadZone', () => ({
  PdfUploadZone: ({
    onUploadComplete,
  }: {
    onUploadComplete: (id: string, fileName: string, bytes: number) => void;
  }) => (
    <div data-testid="upload-zone">
      <button onClick={() => onUploadComplete('doc-1', 'regolamento.pdf', 2048)}>
        Simula upload
      </button>
    </div>
  ),
}));

describe('FirstRulebookStep', () => {
  const onComplete = vi.fn();
  const onSkip = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    acceptDisclaimer.mockResolvedValue({ success: true, message: 'ok' });
  });

  const renderStep = () =>
    render(
      <FirstRulebookStep gameId="g1" gameName="Catan" onComplete={onComplete} onSkip={onSkip} />
    );

  it('names the game whose rulebook is being uploaded', () => {
    renderStep();

    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  // The gate, asserted in the direction that matters: without acceptance the
  // upload zone must not be reachable. A test that only checked the happy path
  // would stay green if the gate were removed entirely.
  it('does not reach the upload zone before the disclaimer is accepted', async () => {
    const user = userEvent.setup();
    renderStep();

    await user.click(screen.getByTestId('rulebook-upload-start'));

    expect(screen.getByTestId('disclaimer')).toBeInTheDocument();
    expect(screen.queryByTestId('upload-zone')).not.toBeInTheDocument();
  });

  it('cancelling the disclaimer leaves the upload zone unreachable', async () => {
    const user = userEvent.setup();
    renderStep();

    await user.click(screen.getByTestId('rulebook-upload-start'));
    await user.click(screen.getByText('Annulla'));

    expect(screen.queryByTestId('upload-zone')).not.toBeInTheDocument();
    expect(screen.getByTestId('rulebook-upload-start')).toBeInTheDocument();
  });

  it('shows the upload zone once the disclaimer is accepted', async () => {
    const user = userEvent.setup();
    renderStep();

    await user.click(screen.getByTestId('rulebook-upload-start'));
    await user.click(screen.getByText('Accetta'));

    expect(screen.getByTestId('upload-zone')).toBeInTheDocument();
  });

  it('records disclaimer acceptance for the uploaded document', async () => {
    const user = userEvent.setup();
    renderStep();

    await user.click(screen.getByTestId('rulebook-upload-start'));
    await user.click(screen.getByText('Accetta'));
    await user.click(screen.getByText('Simula upload'));

    await waitFor(() => expect(acceptDisclaimer).toHaveBeenCalledWith('doc-1'));
  });

  it('shows the uploaded file and lets the user continue', async () => {
    const user = userEvent.setup();
    renderStep();

    await user.click(screen.getByTestId('rulebook-upload-start'));
    await user.click(screen.getByText('Accetta'));
    await user.click(screen.getByText('Simula upload'));

    expect(screen.getByTestId('upload-done')).toBeInTheDocument();
    expect(screen.getByText('regolamento.pdf')).toBeInTheDocument();

    await user.click(screen.getByTestId('rulebook-continue'));
    expect(onComplete).toHaveBeenCalledTimes(1);
  });

  // The step is optional: onboarding must not be a dead end for a user who has
  // no PDF at hand.
  it('can be skipped without uploading anything', async () => {
    const user = userEvent.setup();
    renderStep();

    await user.click(screen.getByTestId('rulebook-skip'));

    expect(onSkip).toHaveBeenCalledTimes(1);
    expect(uploadPdf).not.toHaveBeenCalled();
  });
});

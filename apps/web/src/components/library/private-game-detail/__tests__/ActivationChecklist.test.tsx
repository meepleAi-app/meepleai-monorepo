import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { ActivationChecklist, type PdfStatus } from '../ActivationChecklist';

/**
 * Issue #4138. This folder had no tests at all, which is part of why the
 * third step — "Agente AI pronto" — could sit there unreachable: its host sent
 * an `agentType` the backend rejects, so the step never completed, and nothing
 * asserted that it ever should.
 */
describe('ActivationChecklist', () => {
  const renderChecklist = (pdfStatus: PdfStatus, overrides = {}) => {
    const props = {
      gameAdded: true,
      pdfStatus,
      onUploadPdf: vi.fn(),
      onStartGame: vi.fn(),
      ...overrides,
    };
    render(<ActivationChecklist {...props} />);
    return props;
  };

  it('shows two steps and no agent step', () => {
    renderChecklist('ready');

    expect(screen.getByTestId('step-game-added')).toBeInTheDocument();
    expect(screen.getByTestId('step-pdf')).toBeInTheDocument();
    expect(screen.queryByTestId('step-agent')).not.toBeInTheDocument();
  });

  // The vocabulary is part of the decision: no user-facing surface offers to
  // create an agent any more.
  it('never offers to create an agent', () => {
    renderChecklist('ready');

    expect(screen.queryByText(/Crea agente/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Creazione in corso/i)).not.toBeInTheDocument();
  });

  it('offers the upload action while no document exists', async () => {
    const user = userEvent.setup();
    const props = renderChecklist('none');

    await user.click(screen.getByRole('button', { name: /Carica regolamento/i }));
    expect(props.onUploadPdf).toHaveBeenCalledTimes(1);
  });

  it('offers a retry when processing failed', async () => {
    const user = userEvent.setup();
    const props = renderChecklist('failed');

    await user.click(screen.getByRole('button', { name: /Riprova/i }));
    expect(props.onUploadPdf).toHaveBeenCalledTimes(1);
  });

  // The "try a question" CTA used to hang off the agent step, which could never
  // complete. It now hangs off the knowledge base being ready — the thing that
  // actually determines whether a question can be answered.
  it('surfaces the try-a-question CTA once the knowledge base is ready', async () => {
    const user = userEvent.setup();
    const onTryQuestion = vi.fn();
    renderChecklist('ready', { onTryQuestion });

    await user.click(screen.getByRole('button', { name: /Prova una domanda/i }));
    expect(onTryQuestion).toHaveBeenCalledTimes(1);
  });

  it('does not surface the try-a-question CTA before the knowledge base is ready', () => {
    renderChecklist('processing', { onTryQuestion: vi.fn() });

    expect(screen.queryByRole('button', { name: /Prova una domanda/i })).not.toBeInTheDocument();
  });

  // Asserted in both directions: the gate is the knowledge base, and it is a
  // gate. A single-direction test would stay green if `disabled` were dropped.
  it('blocks Inizia Partita until the knowledge base is ready', () => {
    renderChecklist('processing');

    expect(screen.getByRole('button', { name: /Inizia Partita/i })).toBeDisabled();
  });

  it('enables Inizia Partita once the knowledge base is ready', async () => {
    const user = userEvent.setup();
    const props = renderChecklist('ready');

    const start = screen.getByRole('button', { name: /Inizia Partita/i });
    expect(start).toBeEnabled();

    await user.click(start);
    expect(props.onStartGame).toHaveBeenCalledTimes(1);
  });

  it('blocks Inizia Partita when the game is not in the library', () => {
    renderChecklist('ready', { gameAdded: false });

    expect(screen.getByRole('button', { name: /Inizia Partita/i })).toBeDisabled();
  });
});

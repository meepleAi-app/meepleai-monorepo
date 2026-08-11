/**
 * AgentSettingsForm unit tests — editable via AgentConfigFields (Issue #2732).
 *
 * The form evolves from display-only to a real editor: editable state renders
 * the shared config fields + per-game note + Save/Cancel; read-only disables
 * the fields and shows a reason-specific banner (archived vs standalone).
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { AgentSettingsForm } from '../AgentSettingsForm';

const LABELS = {
  title: 'Impostazioni agente',
  strategyLabel: 'Strategia RAG',
  parametersLabel: 'Parametri strategia',
  readOnlyBanner: 'Le impostazioni sono in sola lettura per gli agenti archiviati.',
  readOnlyStandalone: 'Configurazione disponibile solo per agenti associati a un gioco.',
  perGameNote: 'Queste impostazioni valgono per tutti gli agenti di questo gioco.',
  saveCta: 'Salva impostazioni',
  cancelCta: 'Annulla',
  saveSuccess: 'Impostazioni salvate.',
  saveError: 'Impossibile salvare. Riprova.',
  loadingLabel: 'Caricamento impostazioni...',
  errorLabel: 'Impossibile caricare le impostazioni.',
  retryLabel: 'Riprova',
};

const SAMPLE_VALUE = {
  llmModel: 'llama-3.3-70b-free' as const,
  temperature: 0.7,
  maxTokens: 4096,
  personality: 'Amichevole' as const,
  detailLevel: 'Normale' as const,
  personalNotes: 'note',
};

describe('AgentSettingsForm', () => {
  it('renders data-slot attribute', () => {
    render(
      <AgentSettingsForm
        state={{ kind: 'editable', value: SAMPLE_VALUE }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    expect(document.querySelector('[data-slot="agent-detail-settings-form"]')).toBeTruthy();
  });

  it('editable: renders the config fields, per-game note and Save/Cancel', () => {
    render(
      <AgentSettingsForm
        state={{ kind: 'editable', value: SAMPLE_VALUE }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    expect(screen.getByLabelText(/Max Tokens/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Max Tokens/)).not.toBeDisabled();
    expect(screen.getByText(/valgono per tutti gli agenti/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /salva impostazioni/i })).toBeInTheDocument();
  });

  it('editable: Save emits the edited value', () => {
    const onSave = vi.fn();
    render(
      <AgentSettingsForm
        state={{ kind: 'editable', value: SAMPLE_VALUE }}
        labels={LABELS}
        onSave={onSave}
        onCancel={vi.fn()}
      />
    );

    fireEvent.change(screen.getByLabelText(/Max Tokens/), { target: { value: '2048' } });
    fireEvent.click(screen.getByRole('button', { name: /salva impostazioni/i }));

    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({ maxTokens: 2048, llmModel: 'llama-3.3-70b-free' })
    );
  });

  it('read-only archived: banner + disabled fields + no Save button', () => {
    render(
      <AgentSettingsForm
        state={{ kind: 'read-only', value: SAMPLE_VALUE, readOnlyReason: 'archived' }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    expect(screen.getByText(/sola lettura/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Max Tokens/)).toBeDisabled();
    expect(screen.queryByRole('button', { name: /salva impostazioni/i })).not.toBeInTheDocument();
  });

  it('read-only standalone: renders the standalone banner', () => {
    render(
      <AgentSettingsForm
        state={{ kind: 'read-only', value: SAMPLE_VALUE, readOnlyReason: 'standalone' }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    expect(screen.getByText(/solo per agenti associati a un gioco/i)).toBeInTheDocument();
  });

  it('loading kind: no Save button', () => {
    render(
      <AgentSettingsForm
        state={{ kind: 'loading' }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    expect(screen.queryByRole('button', { name: /salva impostazioni/i })).not.toBeInTheDocument();
  });

  it('error kind: renders retry button', () => {
    const retry = vi.fn();
    render(
      <AgentSettingsForm
        state={{ kind: 'error', retry }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    expect(screen.getByRole('button', { name: /riprova/i })).toBeInTheDocument();
  });

  it('read-only: config fields including radios are disabled', () => {
    render(
      <AgentSettingsForm
        state={{ kind: 'read-only', value: SAMPLE_VALUE, readOnlyReason: 'archived' }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    expect(screen.getByLabelText(/Max Tokens/)).toBeDisabled();
    expect(screen.getByRole('radio', { name: /Amichevole/ })).toBeDisabled();
  });

  it('editable: Cancel resets edits and calls onCancel', () => {
    const onCancel = vi.fn();
    render(
      <AgentSettingsForm
        state={{ kind: 'editable', value: SAMPLE_VALUE }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={onCancel}
      />
    );

    fireEvent.change(screen.getByLabelText(/Max Tokens/), { target: { value: '2048' } });
    fireEvent.click(screen.getByRole('button', { name: /annulla/i }));

    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(screen.getByLabelText(/Max Tokens/)).toHaveValue(4096);
  });

  it('editable: preserves in-progress edits when the source value refetches', () => {
    // Simulates refetchOnWindowFocus bringing different server data mid-edit:
    // the local edit buffer must NOT be clobbered.
    const { rerender } = render(
      <AgentSettingsForm
        state={{ kind: 'editable', value: SAMPLE_VALUE }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    fireEvent.change(screen.getByLabelText(/Max Tokens/), { target: { value: '2048' } });

    rerender(
      <AgentSettingsForm
        state={{ kind: 'editable', value: { ...SAMPLE_VALUE, maxTokens: 8000 } }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    expect(screen.getByLabelText(/Max Tokens/)).toHaveValue(2048);
  });
});

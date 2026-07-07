/**
 * AgentSettingsForm unit tests — real editor (Issue #2732).
 *
 * The form now renders the shared AgentConfigFields editor. Value-flow
 * assertions go through the maxTokens number input (Radix Select/Slider are
 * flaky in jsdom).
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { AgentSettingsForm } from '../AgentSettingsForm';
import type { AgentConfig } from '../AgentSettingsForm';

const LABELS = {
  title: 'Impostazioni agente',
  strategyLabel: 'Strategia RAG',
  parametersLabel: 'Parametri strategia',
  readOnlyBanner: 'Le impostazioni sono in sola lettura per gli agenti archiviati.',
  perGameNote: 'Queste impostazioni valgono per tutti gli agenti di questo gioco.',
  standaloneBanner:
    'Questo agente non è associato a un gioco: le impostazioni sono in sola lettura.',
  saveCta: 'Salva impostazioni',
  cancelCta: 'Annulla',
  saveSuccess: 'Impostazioni salvate.',
  saveError: 'Impossibile salvare. Riprova.',
  loadingLabel: 'Caricamento impostazioni...',
  errorLabel: 'Impossibile caricare le impostazioni.',
  retryLabel: 'Riprova',
};

const SAMPLE_CONFIG: AgentConfig = {
  llmModel: 'llama-3.3-70b-free',
  temperature: 0.7,
  maxTokens: 4096,
  personality: 'Amichevole',
  detailLevel: 'Normale',
  personalNotes: 'x',
};

describe('AgentSettingsForm', () => {
  it('renders data-slot attribute', () => {
    render(
      <AgentSettingsForm
        state={{ kind: 'editable', config: SAMPLE_CONFIG }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    expect(document.querySelector('[data-slot="agent-detail-settings-form"]')).toBeTruthy();
  });

  it('editable kind: renders editable fields + Save/Cancel buttons', () => {
    render(
      <AgentSettingsForm
        state={{ kind: 'editable', config: SAMPLE_CONFIG }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    // Editable field (not read-only display)
    const input = screen.getByRole('spinbutton');
    expect(input).toBeInTheDocument();
    expect(input).not.toBeDisabled();

    expect(screen.getByRole('button', { name: LABELS.saveCta })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: LABELS.cancelCta })).toBeInTheDocument();
  });

  it('editable kind: edit-then-Save calls onSave with the edited config', () => {
    const onSave = vi.fn();
    render(
      <AgentSettingsForm
        state={{ kind: 'editable', config: SAMPLE_CONFIG }}
        labels={LABELS}
        onSave={onSave}
        onCancel={vi.fn()}
      />
    );

    fireEvent.change(screen.getByRole('spinbutton'), { target: { value: '8192' } });
    fireEvent.click(screen.getByRole('button', { name: LABELS.saveCta }));

    expect(onSave).toHaveBeenCalledTimes(1);
    expect(onSave.mock.calls[0][0].maxTokens).toBe(8192);
  });

  it('editable kind: shows the per-game note', () => {
    render(
      <AgentSettingsForm
        state={{ kind: 'editable', config: SAMPLE_CONFIG }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    expect(screen.getByText(LABELS.perGameNote)).toBeInTheDocument();
  });

  it('read-only archived: renders read-only banner, disabled fields, no Save', () => {
    render(
      <AgentSettingsForm
        state={{ kind: 'read-only', config: SAMPLE_CONFIG, reason: 'archived' }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    expect(screen.getByText(LABELS.readOnlyBanner)).toBeInTheDocument();
    expect(screen.getByRole('spinbutton')).toBeDisabled();
    expect(screen.queryByRole('button', { name: LABELS.saveCta })).not.toBeInTheDocument();
  });

  it('read-only standalone: renders the standalone banner, no Save', () => {
    render(
      <AgentSettingsForm
        state={{ kind: 'read-only', config: SAMPLE_CONFIG, reason: 'standalone' }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    expect(screen.getByText(LABELS.standaloneBanner)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: LABELS.saveCta })).not.toBeInTheDocument();
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
    expect(screen.queryByRole('button', { name: LABELS.saveCta })).not.toBeInTheDocument();
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
    const retryBtn = screen.getByRole('button', { name: /riprova/i });
    expect(retryBtn).toBeInTheDocument();
    fireEvent.click(retryBtn);
    expect(retry).toHaveBeenCalledTimes(1);
  });
});

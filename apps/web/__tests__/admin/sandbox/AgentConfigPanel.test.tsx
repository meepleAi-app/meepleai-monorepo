import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { SandboxSessionProvider } from '@/components/admin/sandbox/contexts/SandboxSessionContext';
import { RagStrategyForm } from '@/components/admin/sandbox/RagStrategyForm';
import { LlmSettingsForm } from '@/components/admin/sandbox/LlmSettingsForm';
import { ChunkingParamsForm } from '@/components/admin/sandbox/ChunkingParamsForm';
import { ReprocessConfirmDialog } from '@/components/admin/sandbox/ReprocessConfirmDialog';
import { AgentConfigPanel } from '@/components/admin/sandbox/AgentConfigPanel';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function renderWithProvider(ui: React.ReactElement) {
  return render(<SandboxSessionProvider>{ui}</SandboxSessionProvider>);
}

// ---------------------------------------------------------------------------
// RagStrategyForm
// ---------------------------------------------------------------------------

describe('RagStrategyForm', () => {
  it('renders strategy select and sliders', () => {
    renderWithProvider(<RagStrategyForm />);

    expect(screen.getByText('Strategia di retrieval')).toBeInTheDocument();
    expect(screen.getByText('Peso dense')).toBeInTheDocument();
    expect(screen.getByText('Peso sparse')).toBeInTheDocument();
    expect(screen.getByText('Top-K risultati')).toBeInTheDocument();
    expect(screen.getByText('Reranking')).toBeInTheDocument();
    expect(screen.getByText('Confidenza minima')).toBeInTheDocument();
  });

  it('displays default dense weight value', () => {
    renderWithProvider(<RagStrategyForm />);

    // Default dense weight is 0.7
    expect(screen.getByText('0.7')).toBeInTheDocument();
  });

  it('displays sparse weight as auto-calculated', () => {
    renderWithProvider(<RagStrategyForm />);

    // Default sparse weight = 1 - 0.7 = 0.3
    expect(screen.getByText('0.3')).toBeInTheDocument();
    expect(screen.getByText('Calcolato automaticamente (1 - dense weight)')).toBeInTheDocument();
  });

  it('shows reranker model select when reranking is on', () => {
    renderWithProvider(<RagStrategyForm />);

    // Reranking is on by default
    expect(screen.getByText('Modello reranker')).toBeInTheDocument();
  });

  it('hides reranker model select when reranking is off', async () => {
    const user = userEvent.setup();
    renderWithProvider(<RagStrategyForm />);

    const rerankingSwitch = screen.getByRole('switch');
    await user.click(rerankingSwitch);

    expect(screen.queryByText('Modello reranker')).not.toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// LlmSettingsForm
// ---------------------------------------------------------------------------

describe('LlmSettingsForm', () => {
  it('renders model select and temperature slider', () => {
    renderWithProvider(<LlmSettingsForm />);

    expect(screen.getByText('Modello')).toBeInTheDocument();
    expect(screen.getByText('Temperatura')).toBeInTheDocument();
    expect(screen.getByText('Max tokens')).toBeInTheDocument();
  });

  it('renders collapsible system prompt section', () => {
    renderWithProvider(<LlmSettingsForm />);

    expect(screen.getByText('Prompt di sistema personalizzato')).toBeInTheDocument();
  });

  it('expands system prompt textarea on click', async () => {
    const user = userEvent.setup();
    renderWithProvider(<LlmSettingsForm />);

    const trigger = screen.getByText('Prompt di sistema personalizzato');
    await user.click(trigger);

    expect(screen.getByPlaceholderText(/Override del prompt di sistema/)).toBeInTheDocument();
  });

  it('displays default temperature value', () => {
    renderWithProvider(<LlmSettingsForm />);

    // Default temperature is 0.3
    expect(screen.getByText('0.3')).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// ChunkingParamsForm
// ---------------------------------------------------------------------------

describe('ChunkingParamsForm', () => {
  it('renders chunk strategy select and sliders', () => {
    renderWithProvider(<ChunkingParamsForm />);

    expect(screen.getByText('Strategia di chunking')).toBeInTheDocument();
    expect(screen.getByText('Dimensione chunk (tokens)')).toBeInTheDocument();
    expect(screen.getByText('Overlap (tokens)')).toBeInTheDocument();
    expect(screen.getByText('Rispetta confini pagina')).toBeInTheDocument();
  });

  it('displays default chunk size value', () => {
    renderWithProvider(<ChunkingParamsForm />);

    // Default chunk size is 512
    expect(screen.getByText('512')).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// ReprocessConfirmDialog
// ---------------------------------------------------------------------------

describe('ReprocessConfirmDialog', () => {
  it('shows counts and buttons when open', () => {
    const onConfirm = vi.fn();
    const onOpenChange = vi.fn();

    render(
      <ReprocessConfirmDialog
        open={true}
        onOpenChange={onOpenChange}
        onConfirm={onConfirm}
        chunksToDelete={150}
        vectorsToDelete={320}
        estimatedTimeSeconds={45}
      />
    );

    expect(screen.getByText('Conferma rielaborazione')).toBeInTheDocument();
    expect(screen.getByTestId('chunks-count')).toHaveTextContent('150');
    expect(screen.getByTestId('vectors-count')).toHaveTextContent('320');
    expect(screen.getByTestId('estimated-time')).toHaveTextContent('45s');
    expect(screen.getByText('Annulla')).toBeInTheDocument();
    expect(screen.getByText('Rielabora e Applica')).toBeInTheDocument();
  });

  it('formats time with minutes', () => {
    render(
      <ReprocessConfirmDialog
        open={true}
        onOpenChange={vi.fn()}
        onConfirm={vi.fn()}
        chunksToDelete={10}
        vectorsToDelete={20}
        estimatedTimeSeconds={90}
      />
    );

    expect(screen.getByTestId('estimated-time')).toHaveTextContent('1m 30s');
  });

  it('calls onConfirm when confirm button is clicked', async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn();

    render(
      <ReprocessConfirmDialog
        open={true}
        onOpenChange={vi.fn()}
        onConfirm={onConfirm}
        chunksToDelete={10}
        vectorsToDelete={20}
        estimatedTimeSeconds={5}
      />
    );

    await user.click(screen.getByText('Rielabora e Applica'));
    expect(onConfirm).toHaveBeenCalledOnce();
  });

  it('does not render content when closed', () => {
    render(
      <ReprocessConfirmDialog
        open={false}
        onOpenChange={vi.fn()}
        onConfirm={vi.fn()}
        chunksToDelete={10}
        vectorsToDelete={20}
        estimatedTimeSeconds={5}
      />
    );

    expect(screen.queryByText('Conferma rielaborazione')).not.toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// AgentConfigPanel
// ---------------------------------------------------------------------------

describe('AgentConfigPanel', () => {
  it('renders panel title with settings icon', () => {
    renderWithProvider(<AgentConfigPanel />);

    expect(screen.getByText('Agent Config')).toBeInTheDocument();
  });

  it('renders 3 accordion sections', () => {
    renderWithProvider(<AgentConfigPanel />);

    expect(screen.getByText('Strategia RAG')).toBeInTheDocument();
    expect(screen.getByText('Impostazioni LLM')).toBeInTheDocument();
    expect(screen.getByText('Parametri Chunking')).toBeInTheDocument();
  });

  it('has RAG strategy section open by default', () => {
    renderWithProvider(<AgentConfigPanel />);

    // RagStrategyForm content should be visible
    expect(screen.getByText('Strategia di retrieval')).toBeInTheDocument();
  });

  it('shows Apply button disabled when config is clean', () => {
    renderWithProvider(<AgentConfigPanel />);

    const applyButton = screen.getByRole('button', { name: /Applica/i });
    expect(applyButton).toBeDisabled();
  });

  it('shows Reset button disabled when config is clean', () => {
    renderWithProvider(<AgentConfigPanel />);

    const resetButton = screen.getByRole('button', { name: /Reset/i });
    expect(resetButton).toBeDisabled();
  });

  it('enables Apply button when config is dirty', async () => {
    const user = userEvent.setup();
    renderWithProvider(<AgentConfigPanel />);

    // Toggle reranking off to make config dirty
    const rerankingSwitch = screen.getByRole('switch');
    await user.click(rerankingSwitch);

    const applyButton = screen.getByRole('button', { name: /Applica/i });
    expect(applyButton).toBeEnabled();
  });

  it('shows pending changes count badge when dirty', async () => {
    const user = userEvent.setup();
    renderWithProvider(<AgentConfigPanel />);

    // Toggle reranking off — changes reranking field
    const rerankingSwitch = screen.getByRole('switch');
    await user.click(rerankingSwitch);

    // The badge should show the pending count
    const applyButton = screen.getByRole('button', { name: /Applica/i });
    const badge = within(applyButton).getByText(/\d+/);
    expect(badge).toBeInTheDocument();
  });

  it('opens reprocess dialog when Apply is clicked with chunking changes', async () => {
    const user = userEvent.setup();
    renderWithProvider(<AgentConfigPanel />);

    // Open chunking section
    const chunkingTrigger = screen.getByText('Parametri Chunking');
    await user.click(chunkingTrigger);

    // Toggle respect page boundaries to make chunking dirty
    const switches = screen.getAllByRole('switch');
    // The last switch should be respectPageBoundaries (reranking is first)
    const pageBoundarySwitch = switches[switches.length - 1];
    await user.click(pageBoundarySwitch);

    // Click Apply
    const applyButton = screen.getByRole('button', { name: /Applica/i });
    await user.click(applyButton);

    // Confirm dialog should appear
    expect(screen.getByText('Conferma rielaborazione')).toBeInTheDocument();
  });

  it('applies config without dialog when only non-chunking params change', async () => {
    const user = userEvent.setup();
    renderWithProvider(<AgentConfigPanel />);

    // Toggle reranking off (non-chunking change)
    const rerankingSwitch = screen.getByRole('switch');
    await user.click(rerankingSwitch);

    // Click Apply
    const applyButton = screen.getByRole('button', { name: /Applica/i });
    await user.click(applyButton);

    // No dialog should appear
    expect(screen.queryByText('Conferma rielaborazione')).not.toBeInTheDocument();

    // Apply should have happened — button should be disabled again
    expect(applyButton).toBeDisabled();
  });

  it('resets config to defaults when Reset is clicked', async () => {
    const user = userEvent.setup();
    renderWithProvider(<AgentConfigPanel />);

    // Toggle reranking off
    const rerankingSwitch = screen.getByRole('switch');
    await user.click(rerankingSwitch);

    // Click Reset
    const resetButton = screen.getByRole('button', { name: /Reset/i });
    await user.click(resetButton);

    // Config should be clean again
    expect(resetButton).toBeDisabled();
    expect(screen.getByRole('button', { name: /Applica/i })).toBeDisabled();
  });
});

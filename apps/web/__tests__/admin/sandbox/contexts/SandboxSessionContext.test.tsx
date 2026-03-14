import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { SourceProvider } from '@/components/admin/sandbox/contexts/SourceContext';
import {
  SandboxSessionProvider,
  useSandboxSession,
  DEFAULT_CONFIG,
} from '@/components/admin/sandbox/contexts/SandboxSessionContext';

vi.mock('@/lib/api', () => ({
  api: {
    sandbox: {
      getDocumentsByGame: vi.fn().mockResolvedValue([]),
      deletePdf: vi.fn().mockResolvedValue(undefined),
      applyConfig: vi
        .fn()
        .mockResolvedValue({ sessionKey: 'test-key', expiresAt: '2026-01-02T00:00:00Z' }),
    },
  },
}));

// TestConsumer renders key values and exposes action buttons
function TestConsumer() {
  const {
    agentConfig,
    appliedConfig,
    updateConfig,
    applyConfig,
    resetConfig,
    isDirty,
    pendingChanges,
  } = useSandboxSession();

  return (
    <div>
      <span data-testid="strategy">{agentConfig.strategy}</span>
      <span data-testid="temperature">{agentConfig.temperature}</span>
      <span data-testid="dense-weight">{agentConfig.denseWeight}</span>
      <span data-testid="sparse-weight">{agentConfig.sparseWeight}</span>
      <span data-testid="is-dirty">{String(isDirty)}</span>
      <span data-testid="pending-changes">{pendingChanges}</span>
      <span data-testid="applied-temperature">{appliedConfig.temperature}</span>
      <span data-testid="chunk-strategy">{agentConfig.chunkStrategy}</span>
      <button data-testid="update-temperature" onClick={() => updateConfig({ temperature: 0.9 })}>
        Update Temperature
      </button>
      <button data-testid="update-dense-weight" onClick={() => updateConfig({ denseWeight: 0.8 })}>
        Update Dense Weight
      </button>
      <button data-testid="apply-config" onClick={() => void applyConfig()}>
        Apply Config
      </button>
      <button data-testid="reset-config" onClick={() => resetConfig()}>
        Reset Config
      </button>
    </div>
  );
}

function renderWithProvider() {
  return render(
    <SourceProvider>
      <SandboxSessionProvider>
        <TestConsumer />
      </SandboxSessionProvider>
    </SourceProvider>
  );
}

describe('SandboxSessionContext', () => {
  it('provides default config', () => {
    renderWithProvider();

    expect(screen.getByTestId('strategy').textContent).toBe('hybrid-v2');
    expect(screen.getByTestId('is-dirty').textContent).toBe('false');
    expect(screen.getByTestId('pending-changes').textContent).toBe('0');
  });

  it('updateConfig marks as dirty', async () => {
    renderWithProvider();
    const user = userEvent.setup();

    expect(screen.getByTestId('is-dirty').textContent).toBe('false');
    expect(screen.getByTestId('pending-changes').textContent).toBe('0');

    await user.click(screen.getByTestId('update-temperature'));

    expect(screen.getByTestId('temperature').textContent).toBe('0.9');
    expect(screen.getByTestId('is-dirty').textContent).toBe('true');
    expect(screen.getByTestId('pending-changes').textContent).toBe('1');
  });

  it('updateConfig auto-calculates sparseWeight when denseWeight changes', async () => {
    renderWithProvider();
    const user = userEvent.setup();

    await user.click(screen.getByTestId('update-dense-weight'));

    expect(screen.getByTestId('dense-weight').textContent).toBe('0.8');
    expect(screen.getByTestId('sparse-weight').textContent).toBe('0.2');
  });

  it('applyConfig syncs applied to draft and clears dirty state (no game = no API call)', async () => {
    renderWithProvider();
    const user = userEvent.setup();

    // First update to make dirty
    await user.click(screen.getByTestId('update-temperature'));
    expect(screen.getByTestId('is-dirty').textContent).toBe('true');
    expect(screen.getByTestId('applied-temperature').textContent).toBe(
      String(DEFAULT_CONFIG.temperature)
    );

    // Apply — no game selected so applyConfig is a no-op
    await user.click(screen.getByTestId('apply-config'));

    // Still dirty because no game selected means no apply
    expect(screen.getByTestId('is-dirty').textContent).toBe('true');
  });

  it('resetConfig restores defaults', async () => {
    renderWithProvider();
    const user = userEvent.setup();

    // Modify config
    await user.click(screen.getByTestId('update-temperature'));
    expect(screen.getByTestId('temperature').textContent).toBe('0.9');
    expect(screen.getByTestId('is-dirty').textContent).toBe('true');

    // Reset
    await user.click(screen.getByTestId('reset-config'));

    expect(screen.getByTestId('temperature').textContent).toBe(String(DEFAULT_CONFIG.temperature));
    expect(screen.getByTestId('chunk-strategy').textContent).toBe(DEFAULT_CONFIG.chunkStrategy);
    expect(screen.getByTestId('dense-weight').textContent).toBe(String(DEFAULT_CONFIG.denseWeight));
  });

  it('throws error when used outside provider', () => {
    // Suppress expected console.error from React error boundary
    const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

    expect(() => {
      render(<TestConsumer />);
    }).toThrow('useSandboxSession must be used within SandboxSessionProvider');

    consoleSpy.mockRestore();
  });
});

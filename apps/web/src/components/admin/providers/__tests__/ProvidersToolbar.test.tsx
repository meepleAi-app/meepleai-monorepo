import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { ProvidersToolbar } from '../ProvidersToolbar';

function renderWithQuery() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');
  const utils = render(
    <QueryClientProvider client={qc}>
      <ProvidersToolbar />
    </QueryClientProvider>
  );
  return { ...utils, qc, invalidateSpy };
}

describe('ProvidersToolbar', () => {
  it('renders title + subtitle + refresh button', () => {
    renderWithQuery();
    expect(screen.getByTestId('providers-toolbar')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: /LLM Providers/i })).toBeInTheDocument();
    expect(screen.getByTestId('providers-refresh')).toBeInTheDocument();
  });

  it('invalidates provider queries on refresh click', async () => {
    const { invalidateSpy } = renderWithQuery();
    fireEvent.click(screen.getByTestId('providers-refresh'));
    await waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalled();
    });
    // Verify it targets the right keys (circuit-breakers + llm config + providers root)
    const keys = invalidateSpy.mock.calls.map(c => (c[0] as { queryKey: unknown[] }).queryKey);
    expect(keys.some(k => Array.isArray(k) && k.includes('circuit-breakers'))).toBe(true);
    expect(keys.some(k => Array.isArray(k) && k.join(',').includes('llm'))).toBe(true);
  });
});

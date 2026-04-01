import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { LogsTab } from '../LogsTab';

vi.mock('@/lib/api', () => ({
  api: { admin: { getDockerContainers: vi.fn().mockResolvedValue([]) } },
}));
vi.mock('@/hooks/useToast', () => ({ useToast: () => ({ toast: vi.fn() }) }));

describe('LogsTab', () => {
  it('renders LogViewer with loading state initially', () => {
    render(<LogsTab />);
    expect(screen.getByTestId('log-viewer-loading')).toBeInTheDocument();
  });

  it('shows empty state when no containers', async () => {
    render(<LogsTab />);
    await waitFor(() => {
      expect(screen.getByTestId('log-viewer-empty')).toBeInTheDocument();
    });
  });
});

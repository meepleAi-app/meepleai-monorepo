import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { LogsTab } from '../LogsTab';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getDockerContainers: vi.fn().mockResolvedValue([]),
      getContainerLogs: vi.fn().mockResolvedValue(null),
    },
  },
}));

describe('LogsTab', () => {
  it('renders LogViewer and shows empty state when no containers', async () => {
    render(<LogsTab />);

    // Initially shows the loading spinner
    expect(screen.getByTestId('log-viewer-loading')).toBeTruthy();

    // After async fetch resolves, shows empty state
    await waitFor(() => {
      expect(screen.getByTestId('log-viewer-empty')).toBeTruthy();
    });
  });
});

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ContainersTab } from '../ContainersTab';

vi.mock('@/lib/api', () => ({
  api: { admin: { getDockerContainers: vi.fn().mockResolvedValue([]) } },
}));
vi.mock('@/hooks/useToast', () => ({ useToast: () => ({ toast: vi.fn() }) }));

describe('ContainersTab', () => {
  it('renders ContainerDashboard', () => {
    render(<ContainersTab />);
    expect(screen.getByTestId('container-dashboard')).toBeInTheDocument();
  });
});

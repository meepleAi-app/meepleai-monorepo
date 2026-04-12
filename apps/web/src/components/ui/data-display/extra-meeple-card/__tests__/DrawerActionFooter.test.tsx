import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { Play, ExternalLink } from 'lucide-react';
import { DrawerActionFooter } from '../DrawerActionFooter';

describe('DrawerActionFooter', () => {
  it('renders enabled actions', () => {
    render(
      <DrawerActionFooter
        actions={[
          { icon: Play, label: 'Gioca', onClick: vi.fn(), variant: 'primary', enabled: true },
          {
            icon: ExternalLink,
            label: 'Apri',
            onClick: vi.fn(),
            variant: 'secondary',
            enabled: true,
          },
        ]}
      />
    );
    expect(screen.getByText('Gioca')).toBeInTheDocument();
    expect(screen.getByText('Apri')).toBeInTheDocument();
  });

  it('hides disabled actions', () => {
    render(
      <DrawerActionFooter
        actions={[
          { icon: Play, label: 'Gioca', onClick: vi.fn(), variant: 'primary', enabled: false },
          {
            icon: ExternalLink,
            label: 'Apri',
            onClick: vi.fn(),
            variant: 'secondary',
            enabled: true,
          },
        ]}
      />
    );
    expect(screen.queryByText('Gioca')).not.toBeInTheDocument();
    expect(screen.getByText('Apri')).toBeInTheDocument();
  });

  it('calls onClick when action clicked', async () => {
    const handler = vi.fn();
    render(
      <DrawerActionFooter
        actions={[
          { icon: Play, label: 'Gioca', onClick: handler, variant: 'primary', enabled: true },
        ]}
      />
    );
    await userEvent.click(screen.getByText('Gioca'));
    expect(handler).toHaveBeenCalledTimes(1);
  });

  it('renders nothing when all actions disabled', () => {
    const { container } = render(
      <DrawerActionFooter
        actions={[
          { icon: Play, label: 'Gioca', onClick: vi.fn(), variant: 'primary', enabled: false },
        ]}
      />
    );
    expect(container.firstChild).toBeNull();
  });
});

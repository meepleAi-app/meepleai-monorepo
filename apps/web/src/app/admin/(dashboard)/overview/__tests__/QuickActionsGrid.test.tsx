import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockPush = vi.hoisted(() => vi.fn());

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }));
redirect: (vi.fn(),
  vi.mock('@/components/admin/invitations/InviteUserDialog', () => ({
    InviteUserDialog: () => null,
  })));

import { QuickActionsGrid } from '../QuickActionsGrid';

describe('QuickActionsGrid', () => {
  beforeEach(() => {
    mockPush.mockClear();
  });

  it('renders all 6 action labels', () => {
    render(<QuickActionsGrid />);

    expect(screen.getByText('Crea Gioco')).toBeInTheDocument();
    expect(screen.getByText('Invita Utente')).toBeInTheDocument();
    expect(screen.getByText('Gestisci Giochi')).toBeInTheDocument();
    expect(screen.getByText('Gestisci Utenti')).toBeInTheDocument();
    expect(screen.getByText('Upload PDF')).toBeInTheDocument();
    expect(screen.getByText('Vedi Coda')).toBeInTheDocument();
  });

  it('renders all 6 action descriptions', () => {
    render(<QuickActionsGrid />);

    expect(screen.getByText('Aggiungi al catalogo')).toBeInTheDocument();
    expect(screen.getByText('Invia invito email')).toBeInTheDocument();
    expect(screen.getByText('Catalogo e filtri')).toBeInTheDocument();
    expect(screen.getByText('Lista e ruoli')).toBeInTheDocument();
    expect(screen.getByText('Carica regolamento')).toBeInTheDocument();
    expect(screen.getByText('Stato processing')).toBeInTheDocument();
  });

  it('navigates to /admin/shared-games/new when create-game is clicked', async () => {
    const user = userEvent.setup();
    render(<QuickActionsGrid />);

    await user.click(screen.getByTestId('quick-action-create-game'));

    expect(mockPush).toHaveBeenCalledWith('/admin/shared-games/new');
  });

  it('navigates to /admin/shared-games/all when manage-games is clicked', async () => {
    const user = userEvent.setup();
    render(<QuickActionsGrid />);

    await user.click(screen.getByTestId('quick-action-manage-games'));

    expect(mockPush).toHaveBeenCalledWith('/admin/shared-games/all');
  });

  it('navigates to /admin/users when manage-users is clicked', async () => {
    const user = userEvent.setup();
    render(<QuickActionsGrid />);

    await user.click(screen.getByTestId('quick-action-manage-users'));

    expect(mockPush).toHaveBeenCalledWith('/admin/users');
  });

  it('navigates to /admin/knowledge-base/upload when upload-pdf is clicked', async () => {
    const user = userEvent.setup();
    render(<QuickActionsGrid />);

    await user.click(screen.getByTestId('quick-action-upload-pdf'));

    expect(mockPush).toHaveBeenCalledWith('/admin/knowledge-base/upload');
  });

  it('navigates to /admin/knowledge-base/queue when view-queue is clicked', async () => {
    const user = userEvent.setup();
    render(<QuickActionsGrid />);

    await user.click(screen.getByTestId('quick-action-view-queue'));

    expect(mockPush).toHaveBeenCalledWith('/admin/knowledge-base/queue');
  });

  it('does not navigate when invite-user is clicked (opens dialog instead)', async () => {
    const user = userEvent.setup();
    render(<QuickActionsGrid />);

    await user.click(screen.getByTestId('quick-action-invite-user'));

    expect(mockPush).not.toHaveBeenCalled();
  });

  it('renders the grid container with correct test id', () => {
    render(<QuickActionsGrid />);

    expect(screen.getByTestId('quick-actions-grid')).toBeInTheDocument();
  });
});

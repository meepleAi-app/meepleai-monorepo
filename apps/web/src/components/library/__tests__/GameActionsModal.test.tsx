/**
 * GameActionsModal Component Tests (Issue #3026)
 *
 * Test Coverage:
 * - Modal open/close behavior
 * - Game header display
 * - Primary action (Chatta) navigation
 * - Agent-related actions
 * - State change submenu
 * - Favorite toggle
 * - Share and session management
 * - Remove action
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { GameActionsModal, type GameActionsModalProps } from '../GameActionsModal';

describe('GameActionsModal', () => {
  const defaultProps: GameActionsModalProps = {
    isOpen: true,
    onClose: vi.fn(),
    gameId: 'game-123',
    gameTitle: 'Settlers of Catan',
    hasAgent: false,
    isFavorite: false,
    onConfigureAgent: vi.fn(),
    onUploadPdf: vi.fn(),
    onEditNotes: vi.fn(),
    onRemove: vi.fn(),
    onChangeState: vi.fn(),
    onToggleFavorite: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Modal Behavior', () => {
    it('renders when isOpen is true', () => {
      render(<GameActionsModal {...defaultProps} />);

      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it('does not render when isOpen is false', () => {
      render(<GameActionsModal {...defaultProps} isOpen={false} />);

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('calls onClose when closing', () => {
      const onClose = vi.fn();
      render(<GameActionsModal {...defaultProps} onClose={onClose} />);

      const closeButton = screen.getByRole('button', { name: /close/i });
      fireEvent.click(closeButton);

      expect(onClose).toHaveBeenCalled();
    });
  });

  describe('Game Header', () => {
    it('displays game title', () => {
      render(<GameActionsModal {...defaultProps} />);

      expect(screen.getByText('Settlers of Catan')).toBeInTheDocument();
    });

    it('displays Azioni Rapide subtitle', () => {
      render(<GameActionsModal {...defaultProps} />);

      expect(screen.getByText('Azioni Rapide')).toBeInTheDocument();
    });

    it('renders game image when provided', () => {
      render(
        <GameActionsModal {...defaultProps} gameImageUrl="https://example.com/catan.jpg" />
      );

      const img = screen.getByRole('img', { name: 'Settlers of Catan' });
      expect(img).toBeInTheDocument();
    });

    it('does not render image when not provided', () => {
      render(<GameActionsModal {...defaultProps} gameImageUrl={null} />);

      expect(screen.queryByRole('img', { name: 'Settlers of Catan' })).not.toBeInTheDocument();
    });
  });

  describe('Primary Action - Chatta', () => {
    it('renders Chatta button', () => {
      render(<GameActionsModal {...defaultProps} />);

      expect(screen.getByText('Chatta')).toBeInTheDocument();
    });

    it('displays Chatta description', () => {
      render(<GameActionsModal {...defaultProps} />);

      expect(
        screen.getByText('Seleziona modalità (tutor, Q&A, etc.) e PDF da utilizzare')
      ).toBeInTheDocument();
    });

    it('links to game detail page', () => {
      render(<GameActionsModal {...defaultProps} />);

      const link = screen.getByRole('link', { name: /chatta/i });
      expect(link).toHaveAttribute('href', '/library/games/game-123');
    });
  });

  describe('Agent Actions', () => {
    it('does not show Usa agente when hasAgent is false', () => {
      render(<GameActionsModal {...defaultProps} hasAgent={false} />);

      expect(screen.queryByText('Usa agente')).not.toBeInTheDocument();
    });

    it('shows Usa agente when hasAgent is true', () => {
      render(<GameActionsModal {...defaultProps} hasAgent={true} />);

      expect(screen.getByText('Usa agente')).toBeInTheDocument();
    });

    it('links to game page with autoStart when using agent', () => {
      render(<GameActionsModal {...defaultProps} hasAgent={true} />);

      const link = screen.getByRole('link', { name: /usa agente/i });
      expect(link).toHaveAttribute('href', '/library/games/game-123?autoStart=true');
    });

    it('calls onConfigureAgent when clicking Config agente', () => {
      const onConfigureAgent = vi.fn();
      render(<GameActionsModal {...defaultProps} onConfigureAgent={onConfigureAgent} />);

      fireEvent.click(screen.getByText('Config agente'));

      expect(onConfigureAgent).toHaveBeenCalled();
    });
  });

  describe('Notes and PDF Actions', () => {
    it('calls onEditNotes when clicking Note', () => {
      const onEditNotes = vi.fn();
      render(<GameActionsModal {...defaultProps} onEditNotes={onEditNotes} />);

      fireEvent.click(screen.getByText('Note'));

      expect(onEditNotes).toHaveBeenCalled();
    });

    it('calls onUploadPdf when clicking Aggiungi PDF', () => {
      const onUploadPdf = vi.fn();
      render(<GameActionsModal {...defaultProps} onUploadPdf={onUploadPdf} />);

      fireEvent.click(screen.getByText('Aggiungi PDF'));

      expect(onUploadPdf).toHaveBeenCalled();
    });
  });

  describe('State Change Submenu', () => {
    it('shows state submenu when clicking Cambia Stato', () => {
      render(<GameActionsModal {...defaultProps} />);

      fireEvent.click(screen.getByText('Cambia Stato'));

      expect(screen.getByText('Segna come Nuovo')).toBeInTheDocument();
      expect(screen.getByText('Segna In Prestito')).toBeInTheDocument();
      expect(screen.getByText('Segna come Posseduto')).toBeInTheDocument();
      expect(screen.getByText('Aggiungi a Wishlist')).toBeInTheDocument();
    });

    it('calls onChangeState with Nuovo', () => {
      const onChangeState = vi.fn();
      render(<GameActionsModal {...defaultProps} onChangeState={onChangeState} />);

      fireEvent.click(screen.getByText('Cambia Stato'));
      fireEvent.click(screen.getByText('Segna come Nuovo'));

      expect(onChangeState).toHaveBeenCalledWith('Nuovo');
    });

    it('calls onChangeState with InPrestito', () => {
      const onChangeState = vi.fn();
      render(<GameActionsModal {...defaultProps} onChangeState={onChangeState} />);

      fireEvent.click(screen.getByText('Cambia Stato'));
      fireEvent.click(screen.getByText('Segna In Prestito'));

      expect(onChangeState).toHaveBeenCalledWith('InPrestito');
    });

    it('calls onChangeState with Owned', () => {
      const onChangeState = vi.fn();
      render(<GameActionsModal {...defaultProps} onChangeState={onChangeState} />);

      fireEvent.click(screen.getByText('Cambia Stato'));
      fireEvent.click(screen.getByText('Segna come Posseduto'));

      expect(onChangeState).toHaveBeenCalledWith('Owned');
    });

    it('calls onChangeState with Wishlist', () => {
      const onChangeState = vi.fn();
      render(<GameActionsModal {...defaultProps} onChangeState={onChangeState} />);

      fireEvent.click(screen.getByText('Cambia Stato'));
      fireEvent.click(screen.getByText('Aggiungi a Wishlist'));

      expect(onChangeState).toHaveBeenCalledWith('Wishlist');
    });

    it('disables current state option', () => {
      render(<GameActionsModal {...defaultProps} currentState="Nuovo" />);

      fireEvent.click(screen.getByText('Cambia Stato'));

      const nuovoButton = screen.getByText('Segna come Nuovo').closest('button');
      expect(nuovoButton).toBeDisabled();
    });
  });

  describe('Favorite Toggle', () => {
    it('shows "Aggiungi ai preferiti" when not favorite', () => {
      render(<GameActionsModal {...defaultProps} isFavorite={false} />);

      expect(screen.getByText('Aggiungi ai preferiti')).toBeInTheDocument();
    });

    it('shows "Rimuovi dai preferiti" when favorite', () => {
      render(<GameActionsModal {...defaultProps} isFavorite={true} />);

      expect(screen.getByText('Rimuovi dai preferiti')).toBeInTheDocument();
    });

    it('calls onToggleFavorite when clicking favorite button', () => {
      const onToggleFavorite = vi.fn();
      render(<GameActionsModal {...defaultProps} onToggleFavorite={onToggleFavorite} />);

      fireEvent.click(screen.getByText('Aggiungi ai preferiti'));

      expect(onToggleFavorite).toHaveBeenCalled();
    });
  });

  describe('Share Action', () => {
    it('does not show Share when onShare is not provided', () => {
      render(<GameActionsModal {...defaultProps} onShare={undefined} />);

      expect(screen.queryByText('Share Community')).not.toBeInTheDocument();
    });

    it('shows Share when onShare is provided', () => {
      render(<GameActionsModal {...defaultProps} onShare={vi.fn()} />);

      expect(screen.getByText('Share Community')).toBeInTheDocument();
    });

    it('calls onShare when clicking Share', () => {
      const onShare = vi.fn();
      render(<GameActionsModal {...defaultProps} onShare={onShare} />);

      fireEvent.click(screen.getByText('Share Community'));

      expect(onShare).toHaveBeenCalled();
    });
  });

  describe('Session Management', () => {
    it('does not show session management when not provided', () => {
      render(<GameActionsModal {...defaultProps} onManageSession={undefined} />);

      expect(screen.queryByText('Gestione partita')).not.toBeInTheDocument();
    });

    it('shows session management when provided', () => {
      render(<GameActionsModal {...defaultProps} onManageSession={vi.fn()} />);

      expect(screen.getByText('Gestione partita')).toBeInTheDocument();
    });

    it('shows session submenu when clicking Gestione partita', () => {
      render(<GameActionsModal {...defaultProps} onManageSession={vi.fn()} />);

      fireEvent.click(screen.getByText('Gestione partita'));

      expect(screen.getByText('Crea nuova partita')).toBeInTheDocument();
      expect(screen.getByText('Carica partita salvata')).toBeInTheDocument();
      expect(screen.getByText('Elimina partite')).toBeInTheDocument();
    });

    it('calls onManageSession when clicking session option', () => {
      const onManageSession = vi.fn();
      render(<GameActionsModal {...defaultProps} onManageSession={onManageSession} />);

      fireEvent.click(screen.getByText('Gestione partita'));
      fireEvent.click(screen.getByText('Crea nuova partita'));

      expect(onManageSession).toHaveBeenCalled();
    });
  });

  describe('Remove Action', () => {
    it('shows remove button', () => {
      render(<GameActionsModal {...defaultProps} />);

      expect(screen.getByText('Rimuovi dalla libreria')).toBeInTheDocument();
    });

    it('shows warning text', () => {
      render(<GameActionsModal {...defaultProps} />);

      expect(screen.getByText('Azione irreversibile')).toBeInTheDocument();
    });

    it('calls onRemove when clicking remove', () => {
      const onRemove = vi.fn();
      render(<GameActionsModal {...defaultProps} onRemove={onRemove} />);

      fireEvent.click(screen.getByText('Rimuovi dalla libreria'));

      expect(onRemove).toHaveBeenCalled();
    });
  });

  describe('Action Flow', () => {
    it('closes modal after action', () => {
      const onClose = vi.fn();
      render(<GameActionsModal {...defaultProps} onClose={onClose} />);

      fireEvent.click(screen.getByText('Note'));

      expect(onClose).toHaveBeenCalled();
    });

    it('resets submenus when closing', () => {
      const onClose = vi.fn();
      render(<GameActionsModal {...defaultProps} onClose={onClose} />);

      // Open submenu
      fireEvent.click(screen.getByText('Cambia Stato'));
      expect(screen.getByText('Segna come Nuovo')).toBeInTheDocument();

      // Close modal
      const closeButton = screen.getByRole('button', { name: /close/i });
      fireEvent.click(closeButton);

      expect(onClose).toHaveBeenCalled();
    });
  });
});

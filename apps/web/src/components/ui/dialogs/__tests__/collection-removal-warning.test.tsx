/**
 * CollectionRemovalWarning Component Tests
 * Issue #4259 - Collection Quick Actions for MeepleCard
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { CollectionRemovalWarning } from '../collection-removal-warning';
import type { AssociatedData } from '@/hooks/use-collection-actions';

describe('CollectionRemovalWarning', () => {
  const mockAssociatedData: AssociatedData = {
    hasCustomAgent: true,
    hasPrivatePdf: true,
    chatSessionsCount: 3,
    gameSessionsCount: 5,
    checklistItemsCount: 2,
    labelsCount: 1,
  };

  it('should render warning dialog when open', () => {
    // Arrange
    const onConfirm = vi.fn();
    const onCancel = vi.fn();

    // Act
    render(
      <CollectionRemovalWarning
        entityName="Twilight Imperium"
        associatedData={mockAssociatedData}
        open={true}
        onConfirm={onConfirm}
        onCancel={onCancel}
      />
    );

    // Assert
    expect(screen.getByText(/Rimuovi "Twilight Imperium" dalla Collezione/i)).toBeInTheDocument();
    expect(screen.getByText(/Questa azione rimuoverà/i)).toBeInTheDocument();
  });

  it('should display all associated data items', () => {
    // Arrange
    const onConfirm = vi.fn();
    const onCancel = vi.fn();

    // Act
    render(
      <CollectionRemovalWarning
        entityName="Test Game"
        associatedData={mockAssociatedData}
        open={true}
        onConfirm={onConfirm}
        onCancel={onCancel}
      />
    );

    // Assert
    expect(screen.getByText('Agente AI personalizzato')).toBeInTheDocument();
    expect(screen.getByText('PDF privato caricato')).toBeInTheDocument();
    expect(screen.getByText('3 chat con l\'agente')).toBeInTheDocument();
    expect(screen.getByText('5 sessioni registrate')).toBeInTheDocument();
    expect(screen.getByText('2 task nella checklist')).toBeInTheDocument();
    expect(screen.getByText('1 etichette personalizzate')).toBeInTheDocument();
  });

  it('should show message when no associated data', () => {
    // Arrange
    const emptyData: AssociatedData = {
      hasCustomAgent: false,
      hasPrivatePdf: false,
      chatSessionsCount: 0,
      gameSessionsCount: 0,
      checklistItemsCount: 0,
      labelsCount: 0,
    };

    // Act
    render(
      <CollectionRemovalWarning
        entityName="Test Game"
        associatedData={emptyData}
        open={true}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText(/Non ci sono dati associati/i)).toBeInTheDocument();
  });

  it('should call onConfirm when confirm button clicked', async () => {
    // Arrange
    const user = userEvent.setup();
    const onConfirm = vi.fn();
    const onCancel = vi.fn();

    // Act
    render(
      <CollectionRemovalWarning
        entityName="Test Game"
        associatedData={mockAssociatedData}
        open={true}
        onConfirm={onConfirm}
        onCancel={onCancel}
      />
    );

    const confirmButton = screen.getByText('Rimuovi Definitivamente');
    await user.click(confirmButton);

    // Assert
    expect(onConfirm).toHaveBeenCalledOnce();
    expect(onCancel).not.toHaveBeenCalled();
  });

  it('should call onCancel when cancel button clicked', async () => {
    // Arrange
    const user = userEvent.setup();
    const onConfirm = vi.fn();
    const onCancel = vi.fn();

    // Act
    render(
      <CollectionRemovalWarning
        entityName="Test Game"
        associatedData={mockAssociatedData}
        open={true}
        onConfirm={onConfirm}
        onCancel={onCancel}
      />
    );

    const cancelButton = screen.getByText('Annulla');
    await user.click(cancelButton);

    // Assert
    expect(onCancel).toHaveBeenCalledOnce();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('should display custom entity type', () => {
    // Act
    render(
      <CollectionRemovalWarning
        entityName="Marco Rossi"
        entityType="player"
        associatedData={mockAssociatedData}
        open={true}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText(/rimuoverà il player e tutti i dati/i)).toBeInTheDocument();
  });

  it('should not render when closed', () => {
    // Act
    const { container } = render(
      <CollectionRemovalWarning
        entityName="Test Game"
        associatedData={mockAssociatedData}
        open={false}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    // Assert
    expect(container.querySelector('[role="alertdialog"]')).not.toBeInTheDocument();
  });
});

/**
 * ConflictResolutionModal Tests (Issue #2055)
 *
 * Tests for conflict resolution UI component.
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ConflictResolutionModal } from '../ConflictResolutionModal';
import type { RuleSpecConflict } from '@/lib/api/schemas';

const mockConflict: RuleSpecConflict = {
  localVersion: {
    id: '1',
    gameId: 'game-123',
    version: '1.0',
    createdAt: new Date().toISOString(),
    createdByUserId: null,
    parentVersionId: null,
    atoms: [
      { id: 'atom-1', text: 'Local rule 1', section: null, page: null, line: null },
      { id: 'atom-2', text: 'Local rule 2', section: null, page: null, line: null },
    ],
  },
  remoteVersion: {
    id: '2',
    gameId: 'game-123',
    version: '1.1',
    createdAt: new Date().toISOString(),
    createdByUserId: null,
    parentVersionId: null,
    atoms: [
      { id: 'atom-1', text: 'Remote rule 1', section: null, page: null, line: null },
      { id: 'atom-3', text: 'Remote rule 3', section: null, page: null, line: null },
    ],
  },
  conflictReason: 'Il RuleSpec è stato modificato da un altro utente.',
};

describe('ConflictResolutionModal', () => {
  describe('rendering', () => {
    it('should not render when conflict is null', () => {
      const { container } = render(
        <ConflictResolutionModal
          open={true}
          onOpenChange={() => {}}
          conflict={null}
          onResolve={() => {}}
        />
      );

      expect(container.firstChild).toBeNull();
    });

    it('should render conflict information when open', () => {
      render(
        <ConflictResolutionModal
          open={true}
          onOpenChange={() => {}}
          conflict={mockConflict}
          onResolve={() => {}}
        />
      );

      expect(screen.getByText('Conflitto Rilevato')).toBeInTheDocument();
      expect(
        screen.getByText('Il RuleSpec è stato modificato da un altro utente.')
      ).toBeInTheDocument();
    });

    it('should display local version information', () => {
      render(
        <ConflictResolutionModal
          open={true}
          onOpenChange={() => {}}
          conflict={mockConflict}
          onResolve={() => {}}
        />
      );

      expect(screen.getByText('Le Tue Modifiche (Locale)')).toBeInTheDocument();
      expect(screen.getByText('1.0')).toBeInTheDocument();
    });

    it('should display remote version information', () => {
      render(
        <ConflictResolutionModal
          open={true}
          onOpenChange={() => {}}
          conflict={mockConflict}
          onResolve={() => {}}
        />
      );

      expect(screen.getByText('Versione Server (Remoto)')).toBeInTheDocument();
      expect(screen.getByText('1.1')).toBeInTheDocument();
    });

    it('should display atom counts', () => {
      render(
        <ConflictResolutionModal
          open={true}
          onOpenChange={() => {}}
          conflict={mockConflict}
          onResolve={() => {}}
        />
      );

      // Both versions have 2 atoms
      const regoleCells = screen.getAllByText('2');
      expect(regoleCells.length).toBeGreaterThanOrEqual(2);
    });

    it('should show diff summary', () => {
      render(
        <ConflictResolutionModal
          open={true}
          onOpenChange={() => {}}
          conflict={mockConflict}
          onResolve={() => {}}
        />
      );

      // Local has 1 unique (atom-2), Remote has 1 unique (atom-3), 1 in common (atom-1)
      // Text is split across elements, so use a function matcher
      expect(
        screen.getByText((content, element) => {
          return element?.textContent === '1 regole in comune tra le due versioni';
        })
      ).toBeInTheDocument();
    });
  });

  describe('resolution actions', () => {
    it('should call onResolve with "local" when local button clicked', () => {
      const onResolve = vi.fn();

      render(
        <ConflictResolutionModal
          open={true}
          onOpenChange={() => {}}
          conflict={mockConflict}
          onResolve={onResolve}
        />
      );

      const localButton = screen.getByText('Mantieni Le Mie Modifiche');
      fireEvent.click(localButton);

      expect(onResolve).toHaveBeenCalledWith('local');
    });

    it('should call onResolve with "remote" when remote button clicked', () => {
      const onResolve = vi.fn();

      render(
        <ConflictResolutionModal
          open={true}
          onOpenChange={() => {}}
          conflict={mockConflict}
          onResolve={onResolve}
        />
      );

      const remoteButton = screen.getByText('Usa Versione Remota');
      fireEvent.click(remoteButton);

      expect(onResolve).toHaveBeenCalledWith('remote');
    });

    it('should call onResolve with "merge" when merge button clicked', () => {
      const onResolve = vi.fn();

      render(
        <ConflictResolutionModal
          open={true}
          onOpenChange={() => {}}
          conflict={mockConflict}
          onResolve={onResolve}
        />
      );

      const mergeButton = screen.getByText('Unisci Versioni');
      fireEvent.click(mergeButton);

      expect(onResolve).toHaveBeenCalledWith('merge');
    });
  });

  describe('loading state', () => {
    it('should disable buttons when resolving', () => {
      render(
        <ConflictResolutionModal
          open={true}
          onOpenChange={() => {}}
          conflict={mockConflict}
          onResolve={() => {}}
          isResolving={true}
        />
      );

      const localButton = screen.getByText('Salvando...');
      const remoteButton = screen.getByText('Usa Versione Remota');
      const mergeButton = screen.getByText('Unisci Versioni');

      expect(localButton).toBeDisabled();
      expect(remoteButton).toBeDisabled();
      expect(mergeButton).toBeDisabled();
    });

    it('should show loading text when resolving', () => {
      render(
        <ConflictResolutionModal
          open={true}
          onOpenChange={() => {}}
          conflict={mockConflict}
          onResolve={() => {}}
          isResolving={true}
        />
      );

      expect(screen.getByText('Salvando...')).toBeInTheDocument();
    });
  });

  describe('accessibility', () => {
    it('should have proper dialog structure', () => {
      render(
        <ConflictResolutionModal
          open={true}
          onOpenChange={() => {}}
          conflict={mockConflict}
          onResolve={() => {}}
        />
      );

      // Dialog should have role="dialog"
      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it('should have descriptive title', () => {
      render(
        <ConflictResolutionModal
          open={true}
          onOpenChange={() => {}}
          conflict={mockConflict}
          onResolve={() => {}}
        />
      );

      expect(screen.getByText('Conflitto Rilevato')).toBeInTheDocument();
    });
  });
});

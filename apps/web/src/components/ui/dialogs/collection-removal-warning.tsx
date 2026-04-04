/**
 * CollectionRemovalWarning - Modal warning for collection removal
 *
 * Displays a warning dialog before removing an entity from the collection,
 * showing all associated data that will be permanently deleted.
 *
 * @module components/ui/dialogs/collection-removal-warning
 * @see Issue #4259 - Collection Quick Actions for MeepleCard
 */

'use client';

import React from 'react';

import { Bot, CheckSquare, FileText, MessageSquare, Play, Tag } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import type { AssociatedData } from '@/hooks/useCollectionActions';

// ============================================================================
// Types
// ============================================================================

export interface CollectionRemovalWarningProps {
  /** Name of the entity being removed */
  entityName: string;
  /** Type of entity (e.g., 'game', 'player', 'event') */
  entityType?: string;
  /** Associated data that will be lost */
  associatedData: AssociatedData;
  /** Whether the dialog is open */
  open: boolean;
  /** Callback when user confirms removal */
  onConfirm: () => void;
  /** Callback when user cancels removal */
  onCancel: () => void;
}

interface DataLossItemProps {
  icon: typeof Bot;
  label: string;
}

// ============================================================================
// Subcomponents
// ============================================================================

/**
 * Individual data loss item with icon
 */
function DataLossItem({ icon: Icon, label }: DataLossItemProps) {
  return (
    <div className="flex items-center gap-3 p-2 rounded-lg bg-destructive/10 border border-destructive/20">
      <Icon className="w-4 h-4 text-destructive flex-shrink-0" aria-hidden="true" />
      <span className="text-sm text-destructive font-medium">{label}</span>
    </div>
  );
}

// ============================================================================
// Main Component
// ============================================================================

/**
 * Warning dialog for collection removal with data loss details
 *
 * @example
 * ```tsx
 * <CollectionRemovalWarning
 *   entityName="Twilight Imperium"
 *   entityType="game"
 *   associatedData={data}
 *   open={isOpen}
 *   onConfirm={handleConfirm}
 *   onCancel={() => setIsOpen(false)}
 * />
 * ```
 */
export function CollectionRemovalWarning({
  entityName,
  entityType = 'gioco',
  associatedData,
  open,
  onConfirm,
  onCancel,
}: CollectionRemovalWarningProps) {
  // Build list of data loss items
  const dataLossItems: Array<{ icon: typeof Bot; label: string }> = [];

  if (associatedData.hasCustomAgent) {
    dataLossItems.push({
      icon: Bot,
      label: 'Agente AI personalizzato',
    });
  }

  if (associatedData.chatSessionsCount > 0) {
    dataLossItems.push({
      icon: MessageSquare,
      label: `${associatedData.chatSessionsCount} chat con l'agente`,
    });
  }

  if (associatedData.hasPrivatePdf) {
    dataLossItems.push({
      icon: FileText,
      label: 'PDF privato caricato',
    });
  }

  if (associatedData.gameSessionsCount > 0) {
    dataLossItems.push({
      icon: Play,
      label: `${associatedData.gameSessionsCount} sessioni registrate`,
    });
  }

  if (associatedData.checklistItemsCount > 0) {
    dataLossItems.push({
      icon: CheckSquare,
      label: `${associatedData.checklistItemsCount} task nella checklist`,
    });
  }

  if (associatedData.labelsCount > 0) {
    dataLossItems.push({
      icon: Tag,
      label: `${associatedData.labelsCount} etichette personalizzate`,
    });
  }

  return (
    <Dialog open={open} onOpenChange={(isOpen: boolean) => !isOpen && onCancel()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <span className="text-2xl">⚠️</span>
            <span>Rimuovi &quot;{entityName}&quot; dalla Collezione</span>
          </DialogTitle>
          <DialogDescription className="text-base">
            Questa azione rimuoverà il {entityType} e tutti i dati collegati:
          </DialogDescription>
        </DialogHeader>

        {dataLossItems.length > 0 && (
          <div className="space-y-2 my-4 max-h-[300px] overflow-y-auto">
            {dataLossItems.map((item, index) => (
              <DataLossItem key={index} icon={item.icon} label={item.label} />
            ))}
          </div>
        )}

        {dataLossItems.length === 0 && (
          <p className="text-sm text-muted-foreground my-4">
            Non ci sono dati associati che verranno persi.
          </p>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={onCancel}>
            Annulla
          </Button>
          <Button variant="destructive" onClick={onConfirm}>
            Rimuovi Definitivamente
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/**
 * BulkCollectionWarning - Modal warning for bulk collection removal
 *
 * Displays a warning dialog before removing multiple entities from the collection,
 * showing aggregated associated data that will be permanently deleted.
 *
 * @module components/ui/dialogs/bulk-collection-warning
 * @see Issue #4268 - Phase 3: Bulk Collection Actions
 */

'use client';

import React from 'react';

import {
  Bot,
  CheckSquare,
  FileText,
  MessageSquare,
  Play,
  Tag,
} from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import type { BulkAssociatedDataDto } from '@/lib/api/schemas/collections.schemas';

// ============================================================================
// Types
// ============================================================================

export interface BulkCollectionWarningProps {
  /** Number of entities being removed */
  entityCount: number;
  /** Type of entities (e.g., 'game', 'player', 'event') */
  entityType?: string;
  /** Aggregated associated data that will be lost */
  aggregatedData: BulkAssociatedDataDto;
  /** Whether the dialog is open */
  open: boolean;
  /** Whether the operation is loading */
  loading?: boolean;
  /** Callback when user confirms removal */
  onConfirm: () => void;
  /** Callback when user cancels removal */
  onCancel: () => void;
}

interface AggregatedDataItemProps {
  icon: typeof Bot;
  label: string;
}

// ============================================================================
// Subcomponents
// ============================================================================

/**
 * Individual aggregated data loss item with icon
 */
function AggregatedDataItem({ icon: Icon, label }: AggregatedDataItemProps) {
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
 * Warning dialog for bulk collection removal with aggregated data loss details
 *
 * @example
 * ```tsx
 * <BulkCollectionWarning
 *   entityCount={12}
 *   entityType="giochi"
 *   aggregatedData={data}
 *   open={isOpen}
 *   onConfirm={handleConfirm}
 *   onCancel={() => setIsOpen(false)}
 * />
 * ```
 */
export function BulkCollectionWarning({
  entityCount,
  entityType = 'elementi',
  aggregatedData,
  open,
  loading = false,
  onConfirm,
  onCancel,
}: BulkCollectionWarningProps) {
  // Build list of aggregated data loss items
  const dataLossItems: Array<{ icon: typeof Bot; label: string }> = [];

  if (aggregatedData.totalCustomAgents > 0) {
    dataLossItems.push({
      icon: Bot,
      label: `${aggregatedData.totalCustomAgents} agenti AI personalizzati`,
    });
  }

  if (aggregatedData.totalChatSessions > 0) {
    dataLossItems.push({
      icon: MessageSquare,
      label: `${aggregatedData.totalChatSessions} chat con l'agente`,
    });
  }

  if (aggregatedData.totalPrivatePdfs > 0) {
    dataLossItems.push({
      icon: FileText,
      label: `${aggregatedData.totalPrivatePdfs} PDF privati caricati`,
    });
  }

  if (aggregatedData.totalGameSessions > 0) {
    dataLossItems.push({
      icon: Play,
      label: `${aggregatedData.totalGameSessions} sessioni registrate`,
    });
  }

  if (aggregatedData.totalChecklistItems > 0) {
    dataLossItems.push({
      icon: CheckSquare,
      label: `${aggregatedData.totalChecklistItems} task nella checklist`,
    });
  }

  if (aggregatedData.totalLabels > 0) {
    dataLossItems.push({
      icon: Tag,
      label: `${aggregatedData.totalLabels} etichette personalizzate`,
    });
  }

  return (
    <Dialog open={open} onOpenChange={(isOpen: boolean) => !isOpen && onCancel()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <span className="text-2xl">⚠️</span>
            <span>
              Rimuovi {entityCount} {entityType} dalla Collezione
            </span>
          </DialogTitle>
          <DialogDescription className="text-base">
            Questa azione rimuoverà {entityCount} {entityType} e tutti i dati collegati:
          </DialogDescription>
        </DialogHeader>

        {dataLossItems.length > 0 && (
          <div className="space-y-2 my-4 max-h-[300px] overflow-y-auto">
            {dataLossItems.map((item, index) => (
              <AggregatedDataItem key={index} icon={item.icon} label={item.label} />
            ))}
          </div>
        )}

        {dataLossItems.length === 0 && (
          <p className="text-sm text-muted-foreground my-4">
            Non ci sono dati associati che verranno persi.
          </p>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={onCancel} disabled={loading}>
            Annulla
          </Button>
          <Button
            variant="destructive"
            onClick={onConfirm}
            disabled={loading}
          >
            {loading ? 'Rimozione...' : 'Rimuovi Definitivamente'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

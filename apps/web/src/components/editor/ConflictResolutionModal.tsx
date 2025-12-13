/**
 * Conflict Resolution Modal (Issue #2055)
 *
 * Displays when concurrent edits are detected and provides options to:
 * - Keep local changes (overwrite remote)
 * - Accept remote changes (discard local)
 * - Merge both versions (smart merge strategy)
 *
 * Shows diff summary between local and remote versions.
 */

'use client';

import * as React from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { RuleSpec, RuleSpecConflict } from '@/lib/api/schemas';

export interface ConflictResolutionModalProps {
  /** Whether the modal is open */
  open: boolean;
  /** Callback when modal is closed */
  onOpenChange: (open: boolean) => void;
  /** Conflict information */
  conflict: RuleSpecConflict | null;
  /** Callback when user chooses resolution */
  onResolve: (choice: 'local' | 'remote' | 'merge') => void;
  /** Whether resolution is in progress */
  isResolving?: boolean;
}

export function ConflictResolutionModal({
  open,
  onOpenChange,
  conflict,
  onResolve,
  isResolving = false,
}: ConflictResolutionModalProps) {
  if (!conflict) {
    return null;
  }

  const localAtomCount = conflict.localVersion.atoms.length;
  const remoteAtomCount = conflict.remoteVersion.atoms.length;
  const localVersion = conflict.localVersion.version;
  const remoteVersion = conflict.remoteVersion.version;

  // Calculate diff summary
  const localAtomIds = new Set(conflict.localVersion.atoms.map(a => a.id));
  const remoteAtomIds = new Set(conflict.remoteVersion.atoms.map(a => a.id));

  const onlyInLocal = conflict.localVersion.atoms.filter(a => !remoteAtomIds.has(a.id)).length;
  const onlyInRemote = conflict.remoteVersion.atoms.filter(a => !localAtomIds.has(a.id)).length;
  const inBoth = conflict.localVersion.atoms.filter(a => remoteAtomIds.has(a.id)).length;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2 text-orange-600">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              className="h-6 w-6"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
              />
            </svg>
            Conflitto Rilevato
          </DialogTitle>
          <DialogDescription>{conflict.conflictReason}</DialogDescription>
        </DialogHeader>

        <div className="grid gap-4 py-4">
          {/* Version Comparison */}
          <div className="grid grid-cols-2 gap-4">
            {/* Local Version */}
            <div className="rounded-lg border border-blue-200 bg-blue-50 p-4">
              <h4 className="font-semibold text-blue-700 mb-2">Le Tue Modifiche (Locale)</h4>
              <dl className="text-sm space-y-1">
                <div className="flex justify-between">
                  <dt className="text-gray-600">Versione:</dt>
                  <dd className="font-mono">{localVersion}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-gray-600">Regole:</dt>
                  <dd>{localAtomCount}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-gray-600">Solo locali:</dt>
                  <dd className="text-blue-600 font-semibold">+{onlyInLocal}</dd>
                </div>
              </dl>
            </div>

            {/* Remote Version */}
            <div className="rounded-lg border border-green-200 bg-green-50 p-4">
              <h4 className="font-semibold text-green-700 mb-2">Versione Server (Remoto)</h4>
              <dl className="text-sm space-y-1">
                <div className="flex justify-between">
                  <dt className="text-gray-600">Versione:</dt>
                  <dd className="font-mono">{remoteVersion}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-gray-600">Regole:</dt>
                  <dd>{remoteAtomCount}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-gray-600">Solo remote:</dt>
                  <dd className="text-green-600 font-semibold">+{onlyInRemote}</dd>
                </div>
              </dl>
            </div>
          </div>

          {/* Common elements info */}
          <div className="text-center text-sm text-gray-500">
            <span className="font-medium">{inBoth}</span> regole in comune tra le due versioni
          </div>

          {/* Resolution Options Description */}
          <div className="rounded-lg border bg-gray-50 p-4 space-y-3">
            <h4 className="font-semibold text-gray-700">Opzioni di Risoluzione:</h4>
            <ul className="text-sm space-y-2">
              <li className="flex items-start gap-2">
                <span className="text-blue-600 font-semibold min-w-[80px]">Locale:</span>
                <span className="text-gray-600">
                  Sovrascrive la versione server con le tue modifiche. Le modifiche remote andranno
                  perse.
                </span>
              </li>
              <li className="flex items-start gap-2">
                <span className="text-green-600 font-semibold min-w-[80px]">Remoto:</span>
                <span className="text-gray-600">
                  Accetta la versione server. Le tue modifiche locali andranno perse.
                </span>
              </li>
              <li className="flex items-start gap-2">
                <span className="text-purple-600 font-semibold min-w-[80px]">Unisci:</span>
                <span className="text-gray-600">
                  Combina entrambe le versioni. Mantiene tutte le regole uniche da entrambi.
                </span>
              </li>
            </ul>
          </div>
        </div>

        <DialogFooter className="flex-col sm:flex-row gap-2">
          <Button
            variant="outline"
            onClick={() => onResolve('remote')}
            disabled={isResolving}
            className="border-green-300 text-green-700 hover:bg-green-50"
          >
            Usa Versione Remota
          </Button>
          <Button
            variant="outline"
            onClick={() => onResolve('merge')}
            disabled={isResolving}
            className="border-purple-300 text-purple-700 hover:bg-purple-50"
          >
            Unisci Versioni
          </Button>
          <Button
            onClick={() => onResolve('local')}
            disabled={isResolving}
            className="bg-blue-600 hover:bg-blue-700"
          >
            {isResolving ? 'Salvando...' : 'Mantieni Le Mie Modifiche'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default ConflictResolutionModal;

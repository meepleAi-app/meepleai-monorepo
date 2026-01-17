/**
 * SetPrimaryModelDialog Component (Issue #2521)
 *
 * Confirmation dialog for setting primary AI model with:
 * - Model name display
 * - Cost change warning (if applicable)
 * - Confirmation buttons
 */

'use client';

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Loader2, Star } from 'lucide-react';

interface SetPrimaryModelDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: () => void;
  modelName: string;
  isLoading?: boolean;
}

export function SetPrimaryModelDialog({
  isOpen,
  onClose,
  onConfirm,
  modelName,
  isLoading,
}: SetPrimaryModelDialogProps) {
  return (
    <AlertDialog open={isOpen} onOpenChange={onClose}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Set Primary Model?</AlertDialogTitle>
          <AlertDialogDescription>
            Sei sicuro di voler impostare <strong>{modelName}</strong> come modello primario?
            <br />
            <br />
            Questo modello verrà utilizzato per tutte le risposte AI future.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isLoading}>Annulla</AlertDialogCancel>
          <AlertDialogAction onClick={onConfirm} disabled={isLoading}>
            {isLoading ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Setting...
              </>
            ) : (
              <>
                <Star className="mr-2 h-4 w-4" />
                Set as Primary
              </>
            )}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

'use client';
import { jsxDEV } from "react/jsx-dev-runtime";
import { Button } from '@/components/ui/primitives/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle
} from "@/components/ui/overlays/dialog";
import { AlertCircle } from 'lucide-react';

/**
 * Review Conflict Dialog Component
 *
 * Shows when attempting to start a review that's already locked by another admin.
 * Displays the admin name who currently holds the lock.
 *
 * Triggered by 409 Conflict responses from startReview endpoint.
 *
 * Issue #2748: Frontend - Admin Review Lock UI
 */

export interface ReviewConflictDialogProps {
  /** Whether the dialog is open */
  open: boolean;
  /** Callback when dialog is closed */
  onClose: () => void;
  /** Details about the admin who owns the lock */
  conflictDetails: {
    adminName: string;
    adminId: string;
  } | null;
}

export function ReviewConflictDialog({
  open,
  onClose,
  conflictDetails,
}: ReviewConflictDialogProps): JSX.Element {
  return (
    <Dialog open={open} onOpenChange={onClose}>
      <DialogContent>
        <DialogHeader>
          <div className="flex items-center gap-2">
            <AlertCircle className="h-5 w-5 text-orange-600" />
            <DialogTitle>Review Already In Progress</DialogTitle>
          </div>
          <DialogDescription>
            This share request is currently being reviewed by{' '}
            <strong>{conflictDetails?.adminName ?? 'another admin'}</strong>. Please wait for them
            to finish or choose another request to review.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button onClick={onClose}>OK</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
import { useState } from 'react';
import { Button } from '@/components/ui';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui';
import { Textarea } from '@/components/ui';
import { Label } from '@/components/ui';
import { CheckCircle } from 'lucide-react';

/**
 * Approve Button Component
 *
 * Displays button to approve a share request with optional admin notes.
 *
 * Features:
 * - Confirmation dialog with notes textarea
 * - Optional admin notes (not shown to user)
 * - Handles approval submission
 *
 * Issue #2745: Frontend - Admin Review Interface
 */

interface ApproveButtonProps {
  onApprove: (notes: string) => void;
  disabled?: boolean;
  isPending?: boolean;
}

export function ApproveButton({ onApprove, disabled, isPending }: ApproveButtonProps){
  const [isOpen, setIsOpen] = useState(false);
  const [notes, setNotes] = useState('');

  const handleApprove = () => {
    onApprove(notes);
    setIsOpen(false);
    setNotes(''); // Reset for next time
  };

  return (
    <>
      <Button
        className="w-full"
        onClick={() => setIsOpen(true)}
        disabled={disabled || isPending}
        data-testid="approve-button"
      >
        <CheckCircle className="w-4 h-4 mr-2" />
        Approve & Publish
      </Button>

      <Dialog open={isOpen} onOpenChange={setIsOpen}>
        <DialogContent data-testid="approve-dialog">
          <DialogHeader>
            <DialogTitle>Approve Share Request</DialogTitle>
            <DialogDescription>
              This will approve the request and add the game to the shared catalog. The
              contributor will receive an approval notification.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-2">
            <Label htmlFor="admin-notes">Admin Notes (Optional)</Label>
            <Textarea
              id="admin-notes"
              data-testid="approve-notes-textarea"
              placeholder="Internal notes for other admins (not visible to contributor)..."
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              rows={3}
              maxLength={2000}
            />
            <p className="text-xs text-muted-foreground">
              {notes.length}/2000 characters
            </p>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setIsOpen(false)} disabled={isPending}>
              Cancel
            </Button>
            <Button onClick={handleApprove} disabled={isPending} data-testid="confirm-approve-button">
              {isPending ? 'Approving...' : 'Approve & Publish'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

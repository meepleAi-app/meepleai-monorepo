import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { XCircle } from 'lucide-react';
import { toast } from 'sonner';

/**
 * Reject Button Component
 *
 * Displays button to reject a share request with required reason.
 *
 * Features:
 * - Confirmation dialog with reason textarea
 * - Required reason (min 10 characters)
 * - Validation before submission
 * - Contributor receives rejection notification
 *
 * Issue #2745: Frontend - Admin Review Interface
 */

interface RejectButtonProps {
  onReject: (reason: string) => void;
  disabled?: boolean;
  isPending?: boolean;
}

export function RejectButton({ onReject, disabled, isPending }: RejectButtonProps): JSX.Element {
  const [isOpen, setIsOpen] = useState(false);
  const [reason, setReason] = useState('');

  const handleReject = () => {
    // Validate reason length
    if (reason.trim().length < 10) {
      toast.error('Rejection reason must be at least 10 characters');
      return;
    }

    onReject(reason);
    setIsOpen(false);
    setReason(''); // Reset for next time
  };

  const isReasonValid = reason.trim().length >= 10;

  return (
    <>
      <Button
        variant="destructive"
        className="w-full"
        onClick={() => setIsOpen(true)}
        disabled={disabled || isPending}
      >
        <XCircle className="w-4 h-4 mr-2" />
        Reject
      </Button>

      <Dialog open={isOpen} onOpenChange={setIsOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reject Share Request</DialogTitle>
            <DialogDescription>
              This will reject the request. The contributor will receive a notification with your
              rejection reason. Please provide clear feedback.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-2">
            <Label htmlFor="reject-reason">
              Rejection Reason <span className="text-destructive">*</span>
            </Label>
            <Textarea
              id="reject-reason"
              placeholder="Explain why this request is being rejected (min 10 characters)..."
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              rows={4}
              maxLength={2000}
              className={!isReasonValid && reason.length > 0 ? 'border-destructive' : ''}
            />
            <p className="text-xs text-muted-foreground">
              {reason.length}/2000 characters{' '}
              {!isReasonValid && reason.length > 0 && (
                <span className="text-destructive">(min 10 required)</span>
              )}
            </p>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setIsOpen(false)} disabled={isPending}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={handleReject}
              disabled={!isReasonValid || isPending}
            >
              {isPending ? 'Rejecting...' : 'Reject Request'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

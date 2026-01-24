"use client";
import { useState } from 'react';
import { Button } from '@/components/ui/primitives/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Textarea } from '@/components/ui/primitives/textarea';
import { Label } from '@/components/ui/primitives/label';
import { MessageSquare } from 'lucide-react';
import { toast } from 'sonner';

/**
 * Request Changes Button Component
 *
 * Displays button to request changes from the contributor with required feedback.
 *
 * Features:
 * - Confirmation dialog with feedback textarea
 * - Required feedback (min 10 characters)
 * - Validation before submission
 * - Contributor can resubmit after making changes
 *
 * Issue #2745: Frontend - Admin Review Interface
 */

interface RequestChangesButtonProps {
  onRequestChanges: (feedback: string) => void;
  disabled?: boolean;
  isPending?: boolean;
}

export function RequestChangesButton({
  onRequestChanges,
  disabled,
  isPending,
}: RequestChangesButtonProps): JSX.Element {
  const [isOpen, setIsOpen] = useState(false);
  const [feedback, setFeedback] = useState('');

  const handleRequestChanges = () => {
    // Validate feedback length
    if (feedback.trim().length < 10) {
      toast.error('Feedback must be at least 10 characters');
      return;
    }

    onRequestChanges(feedback);
    setIsOpen(false);
    setFeedback(''); // Reset for next time
  };

  const isFeedbackValid = feedback.trim().length >= 10;

  return (
    <>
      <Button
        variant="outline"
        className="w-full"
        onClick={() => setIsOpen(true)}
        disabled={disabled || isPending}
      >
        <MessageSquare className="w-4 h-4 mr-2" />
        Request Changes
      </Button>

      <Dialog open={isOpen} onOpenChange={setIsOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Request Changes</DialogTitle>
            <DialogDescription>
              Ask the contributor to make changes before approval. They will receive a notification
              with your feedback and can resubmit the request.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-2">
            <Label htmlFor="feedback">
              Feedback for Contributor <span className="text-destructive">*</span>
            </Label>
            <Textarea
              id="feedback"
              placeholder="Provide detailed feedback about what needs to be changed (min 10 characters)..."
              value={feedback}
              onChange={(e) => setFeedback(e.target.value)}
              rows={4}
              maxLength={2000}
              className={!isFeedbackValid && feedback.length > 0 ? 'border-destructive' : ''}
            />
            <p className="text-xs text-muted-foreground">
              {feedback.length}/2000 characters{' '}
              {!isFeedbackValid && feedback.length > 0 && (
                <span className="text-destructive">(min 10 required)</span>
              )}
            </p>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setIsOpen(false)} disabled={isPending}>
              Cancel
            </Button>
            <Button onClick={handleRequestChanges} disabled={!isFeedbackValid || isPending}>
              {isPending ? 'Sending...' : 'Request Changes'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
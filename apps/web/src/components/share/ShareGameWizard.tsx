import { useState, useEffect } from 'react';

import { Loader2, Share2, ArrowLeft, ArrowRight, Send } from 'lucide-react';

import { Progress } from '@/components/ui/feedback/progress';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { useCreateShareRequest } from '@/hooks/queries/useShareRequests';
import type { UserLibraryEntry, PdfDocumentDto } from '@/lib/api';
import { cn } from '@/lib/utils';

import { Step1GamePreview } from './Step1GamePreview';
import { Step2DocumentSelection } from './Step2DocumentSelection';
import { Step3NotesAndConfirm } from './Step3NotesAndConfirm';


/**
 * ShareGameWizard - 3-step modal for sharing games to the community catalog
 * Issue #2743: Frontend - UI Condivisione da Libreria
 */

interface ShareGameWizardProps {
  game: UserLibraryEntry;
  open: boolean;
  onClose: () => void;
  onSuccess?: (shareRequestId: string) => void;
  existingInCatalog?: boolean;
  documents?: PdfDocumentDto[];
}

type Step = 1 | 2 | 3;

const TOTAL_STEPS = 3;

export function ShareGameWizard({
  game,
  open,
  onClose,
  onSuccess,
  existingInCatalog = false,
  documents = [],
}: ShareGameWizardProps) {
  const [step, setStep] = useState<Step>(1);
  const [selectedDocumentIds, setSelectedDocumentIds] = useState<string[]>([]);
  const [notes, setNotes] = useState('');

  const { mutate: createShareRequest, isPending, isSuccess } = useCreateShareRequest();

  // Reset state when modal opens/closes
  useEffect(() => {
    if (!open) {
      setTimeout(() => {
        setStep(1);
        setSelectedDocumentIds([]);
        setNotes('');
      }, 200); // Delay to avoid flash during close animation
    }
  }, [open]);

  // Close on success
  useEffect(() => {
    if (isSuccess) {
      setTimeout(() => {
        onClose();
      }, 1500);
    }
  }, [isSuccess, onClose]);

  const handleNext = () => {
    if (step < TOTAL_STEPS) {
      setStep((prev) => (prev + 1) as Step);
    }
  };

  const handleBack = () => {
    if (step > 1) {
      setStep((prev) => (prev - 1) as Step);
    }
  };

  const handleSubmit = () => {
    createShareRequest(
      {
        sourceGameId: game.gameId,
        notes: notes.trim() || null,
        attachedDocumentIds: selectedDocumentIds.length > 0 ? selectedDocumentIds : [],
      },
      {
        onSuccess: (response) => {
          onSuccess?.(response.shareRequestId);
        },
      }
    );
  };

  const progressPercentage = (step / TOTAL_STEPS) * 100;

  return (
    <Dialog open={open} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[600px]" data-testid="share-game-wizard">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2" data-testid="wizard-title">
            <Share2 className="h-5 w-5" />
            Share &quot;{game.gameTitle}&quot; with the Community
          </DialogTitle>
          <DialogDescription data-testid="wizard-step-indicator">
            Step {step} of {TOTAL_STEPS}
          </DialogDescription>
        </DialogHeader>

        {/* Progress Indicator */}
        <div className="mb-4">
          <Progress value={progressPercentage} className="h-2" data-testid="wizard-progress" />
          <div className="mt-2 flex justify-between text-xs text-muted-foreground">
            <span className={cn(step === 1 && 'font-semibold text-foreground')}>
              1. Preview
            </span>
            <span className={cn(step === 2 && 'font-semibold text-foreground')}>
              2. Documents
            </span>
            <span className={cn(step === 3 && 'font-semibold text-foreground')}>
              3. Confirm
            </span>
          </div>
        </div>

        {/* Step Content */}
        <div className="min-h-[300px] py-4" data-testid={`wizard-step-${step}`}>
          {step === 1 && <Step1GamePreview game={game} existingInCatalog={existingInCatalog} />}

          {step === 2 && (
            <Step2DocumentSelection
              gameId={game.gameId}
              documents={documents}
              selectedDocumentIds={selectedDocumentIds}
              onSelectionChange={setSelectedDocumentIds}
            />
          )}

          {step === 3 && (
            <Step3NotesAndConfirm
              game={game}
              notes={notes}
              onNotesChange={setNotes}
              documentCount={selectedDocumentIds.length}
            />
          )}
        </div>

        {/* Navigation Footer */}
        <DialogFooter className="gap-2 sm:gap-2">
          <Button variant="outline" onClick={onClose} disabled={isPending} data-testid="wizard-cancel">
            Cancel
          </Button>

          {step > 1 && (
            <Button variant="ghost" onClick={handleBack} disabled={isPending} data-testid="wizard-back">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Back
            </Button>
          )}

          {step < TOTAL_STEPS ? (
            <Button onClick={handleNext} disabled={isPending} data-testid="wizard-next">
              Continue
              <ArrowRight className="ml-2 h-4 w-4" />
            </Button>
          ) : (
            <Button onClick={handleSubmit} disabled={isPending} data-testid="wizard-submit">
              {isPending ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Submitting...
                </>
              ) : (
                <>
                  <Send className="mr-2 h-4 w-4" />
                  Submit Request
                </>
              )}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

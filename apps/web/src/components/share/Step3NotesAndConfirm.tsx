import { Info } from 'lucide-react';

import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import type { UserLibraryEntry } from '@/lib/api';
import { cn } from '@/lib/utils';

/**
 * Step 3: Notes and Confirmation
 * Issue #2743: Frontend - UI Condivisione da Libreria
 */

interface Step3NotesAndConfirmProps {
  game: UserLibraryEntry;
  notes: string;
  onNotesChange: (notes: string) => void;
  documentCount: number;
}

const MAX_LENGTH = 2000;

export function Step3NotesAndConfirm({
  game,
  notes,
  onNotesChange,
  documentCount,
}: Step3NotesAndConfirmProps) {
  const remaining = MAX_LENGTH - notes.length;

  return (
    <div className="space-y-4">
      {/* Notes Input */}
      <div className="space-y-2">
        <Label htmlFor="notes">Notes for Reviewer (Optional)</Label>
        <Textarea
          id="notes"
          data-testid="wizard-notes-textarea"
          value={notes}
          onChange={(e) => onNotesChange(e.target.value)}
          placeholder="Any additional context about your contribution...&#10;&#10;Examples:&#10;- This is the second edition with updated rules&#10;- Includes custom player aids I created&#10;- PDF is in English, original game is Italian"
          className="min-h-[120px] resize-none"
          maxLength={MAX_LENGTH}
        />
        <div className="flex justify-between text-xs">
          <span className="text-muted-foreground">
            Provide context to help admins review your contribution
          </span>
          <span
            className={cn(
              'font-medium',
              remaining < 50 ? 'text-destructive' : 'text-muted-foreground'
            )}
            data-testid="wizard-notes-remaining"
          >
            {remaining} characters remaining
          </span>
        </div>
      </div>

      {/* Summary Box */}
      <div className="rounded-lg border bg-muted/50 p-4" data-testid="wizard-summary">
        <h4 className="mb-3 font-semibold">Summary</h4>
        <ul className="space-y-2 text-sm">
          <li className="flex items-start gap-2">
            <span className="text-muted-foreground">•</span>
            <span>
              <strong>Game:</strong> {game.gameTitle}
              {game.gamePublisher && ` (${game.gamePublisher})`}
            </span>
          </li>
          <li className="flex items-start gap-2">
            <span className="text-muted-foreground">•</span>
            <span>
              <strong>Documents:</strong> {documentCount} attached
            </span>
          </li>
          <li className="flex items-start gap-2">
            <span className="text-muted-foreground">•</span>
            <span>
              <strong>Status:</strong> Will be reviewed by an admin
            </span>
          </li>
          <li className="flex items-start gap-2">
            <span className="text-muted-foreground">•</span>
            <span>
              <strong>Notes:</strong> {notes.trim() ? `${notes.length} characters` : 'None'}
            </span>
          </li>
        </ul>
      </div>

      {/* Contribution Guidelines Alert */}
      <Alert className="border-blue-500/50 bg-blue-50 dark:bg-blue-950/20">
        <Info className="h-4 w-4 text-blue-600 dark:text-blue-400" />
        <AlertDescription className="text-sm text-blue-800 dark:text-blue-200">
          By submitting, you confirm that you have the right to share these materials and agree to
          our contribution guidelines.
        </AlertDescription>
      </Alert>
    </div>
  );
}

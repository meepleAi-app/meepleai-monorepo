/**
 * Migration Choice Modal Component
 * Issue #3669: Phase 8 - Frontend Integration (Task 8.6)
 *
 * Modal for choosing post-approval migration action.
 * Two options: Keep Private or Migrate to Shared.
 */

'use client';

import { useState } from 'react';

import { ExternalLink } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Label } from '@/components/ui/primitives/label';
import { RadioGroup, RadioGroupItem } from '@/components/ui/primitives/radio-group';
import type { PendingMigrationDto, MigrationAction } from '@/lib/api/schemas/migrations.schemas';

export interface MigrationChoiceModalProps {
  isOpen: boolean;
  onClose: () => void;
  migration: PendingMigrationDto | null;
  onChoose: (migrationId: string, action: MigrationAction) => Promise<void>;
}

export function MigrationChoiceModal({
  isOpen,
  onClose,
  migration,
  onChoose,
}: MigrationChoiceModalProps) {
  const [selectedAction, setSelectedAction] = useState<MigrationAction>('MigrateToShared');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async () => {
    if (!migration) return;

    setIsSubmitting(true);
    try {
      await onChoose(migration.id, selectedAction);
      onClose();
    } catch (error) {
      console.error('Migration choice error:', error);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Your Proposal Was Approved!</DialogTitle>
          <DialogDescription>
            "{migration?.gameTitle}" has been approved. Choose what to do with your private copy:
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <RadioGroup value={selectedAction} onValueChange={(v) => setSelectedAction(v as MigrationAction)}>
            <div className="flex items-start space-x-3 space-y-0">
              <RadioGroupItem value="MigrateToShared" id="migrate" />
              <Label htmlFor="migrate" className="font-normal cursor-pointer">
                <div>
                  <p className="font-semibold">Migrate to Shared Catalog</p>
                  <p className="text-sm text-muted-foreground">
                    Remove your private copy and use the shared catalog version. Your notes and state
                    will be preserved.
                  </p>
                </div>
              </Label>
            </div>

            <div className="flex items-start space-x-3 space-y-0">
              <RadioGroupItem value="KeepPrivate" id="keep" />
              <Label htmlFor="keep" className="font-normal cursor-pointer">
                <div>
                  <p className="font-semibold">Keep Private Copy</p>
                  <p className="text-sm text-muted-foreground">
                    Keep your private game and continue using it. The shared catalog version will
                    also be available.
                  </p>
                </div>
              </Label>
            </div>
          </RadioGroup>

          {migration?.sharedGameUrl && (
            <Button variant="link" size="sm" asChild className="pl-0">
              <a href={migration.sharedGameUrl} target="_blank" rel="noopener noreferrer">
                <ExternalLink className="h-4 w-4 mr-1" />
                View game in catalog
              </a>
            </Button>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={isSubmitting}>
            Decide Later
          </Button>
          <Button onClick={handleSubmit} disabled={isSubmitting || !migration}>
            {isSubmitting ? 'Processing...' : 'Confirm Choice'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

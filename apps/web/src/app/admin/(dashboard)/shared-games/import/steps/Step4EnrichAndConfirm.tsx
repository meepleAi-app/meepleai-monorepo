'use client';

/**
 * Step 4: Enrich & Confirm
 * Issue #4165: Merge BGG+PDF data with conflict resolution
 *
 * Features:
 * - Auto-merge BGG + PDF metadata
 * - Conflict detection and resolution UI
 * - Final preview with MeepleCard
 * - Submit to import game
 * - Success redirect to game detail
 * - Comprehensive error handling
 */

import { useCallback, useEffect, useMemo, useState } from 'react';
import type { JSX } from 'react';

import { CheckCircle2, AlertCircle, Loader2, Save } from 'lucide-react';

import { DuplicateWarningDialog } from '@/components/admin/games/import/DuplicateWarningDialog';
import { toast } from '@/components/layout';
import { Card } from '@/components/ui/data-display/card';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { RadioGroup, RadioGroupItem } from '@/components/ui/primitives/radio-group';
import { useCheckDuplicate } from '@/hooks/wizard/useCheckDuplicate';
import { logger } from '@/lib/logger';
import { cn } from '@/lib/utils';
import { useGameImportWizardStore, type EnrichedGameData } from '@/stores/useGameImportWizardStore';

export interface Step4EnrichAndConfirmProps {
  /** Callback when import is successful */
  onComplete?: (gameId: string) => void;
}

/**
 * Conflict field source: BGG, PDF, or Custom
 */
type ConflictSource = 'bgg' | 'pdf' | 'custom';

/**
 * Conflict resolution state for a single field
 */
interface ConflictResolution {
  source: ConflictSource;
  customValue?: string | number;
}

/**
 * Map of field name to conflict resolution
 */
type ConflictResolutions = Record<string, ConflictResolution>;

export function Step4EnrichAndConfirm({ onComplete }: Step4EnrichAndConfirmProps): JSX.Element {
  const {
    extractedMetadata,
    bggGameData,
    selectedBggId,
    resolveConflicts,
    submitWizard,
    isProcessing,
    error: storeError,
  } = useGameImportWizardStore();

  // Duplicate check
  const { data: duplicateCheck } = useCheckDuplicate({
    bggId: selectedBggId,
    enabled: !!selectedBggId,
  });
  const [showDuplicateModal, setShowDuplicateModal] = useState(false);
  const [forceCreate, setForceCreate] = useState(false);

  // Conflict resolution state
  const [conflictResolutions, setConflictResolutions] = useState<ConflictResolutions>({});
  const [customValues, setCustomValues] = useState<Record<string, string>>({});

  // Detect conflicts between BGG and PDF data
  const conflicts = useMemo(() => {
    if (!extractedMetadata || !bggGameData) return {};

    const detected: Record<string, { bgg: string | number; pdf: string | number }> = {};

    // Title conflict (BGG name vs PDF title) - normalize whitespace
    if (bggGameData.name && extractedMetadata.title) {
      const bggName = bggGameData.name.trim();
      const pdfTitle = extractedMetadata.title.trim();
      if (bggName !== pdfTitle && bggName.length > 0 && pdfTitle.length > 0) {
        detected.title = { bgg: bggGameData.name, pdf: extractedMetadata.title };
      }
    }

    // Year conflict
    if (
      bggGameData.yearPublished &&
      extractedMetadata.yearPublished &&
      bggGameData.yearPublished !== extractedMetadata.yearPublished
    ) {
      detected.yearPublished = {
        bgg: bggGameData.yearPublished,
        pdf: extractedMetadata.yearPublished,
      };
    }

    // Min players conflict
    if (
      bggGameData.minPlayers &&
      extractedMetadata.minPlayers &&
      bggGameData.minPlayers !== extractedMetadata.minPlayers
    ) {
      detected.minPlayers = { bgg: bggGameData.minPlayers, pdf: extractedMetadata.minPlayers };
    }

    // Max players conflict
    if (
      bggGameData.maxPlayers &&
      extractedMetadata.maxPlayers &&
      bggGameData.maxPlayers !== extractedMetadata.maxPlayers
    ) {
      detected.maxPlayers = { bgg: bggGameData.maxPlayers, pdf: extractedMetadata.maxPlayers };
    }

    // Play time conflict
    // Note: BGG API uses 'playingTime', PDF extraction uses 'playTime'
    // Conflict stored under 'playTime' key for consistency with EnrichedGameData
    if (
      bggGameData.playingTime &&
      extractedMetadata.playTime &&
      bggGameData.playingTime !== extractedMetadata.playTime
    ) {
      detected.playTime = { bgg: bggGameData.playingTime, pdf: extractedMetadata.playTime };
    }

    // Min age conflict
    if (
      bggGameData.minAge &&
      extractedMetadata.minAge &&
      bggGameData.minAge !== extractedMetadata.minAge
    ) {
      detected.minAge = { bgg: bggGameData.minAge, pdf: extractedMetadata.minAge };
    }

    // Description conflict (if both exist)
    if (bggGameData.description && extractedMetadata.description) {
      // Show conflict only if significantly different (not just whitespace)
      const bggDesc = bggGameData.description.trim();
      const pdfDesc = extractedMetadata.description.trim();
      if (bggDesc !== pdfDesc && bggDesc.length > 0 && pdfDesc.length > 0) {
        detected.description = { bgg: bggDesc, pdf: pdfDesc };
      }
    }

    return detected;
  }, [extractedMetadata, bggGameData]);

  // Initialize conflict resolutions with defaults (prefer BGG)
  useEffect(() => {
    const initialResolutions: ConflictResolutions = {};

    Object.keys(conflicts).forEach(field => {
      // Default to BGG for all conflicts
      initialResolutions[field] = { source: 'bgg' };
    });

    setConflictResolutions(initialResolutions);
  }, [conflicts]);

  // Build enriched data based on conflict resolutions
  const enrichedData = useMemo<EnrichedGameData | null>(() => {
    if (!extractedMetadata && !bggGameData) return null;

    // Helper to get resolved value for a field
    const getResolvedValue = (
      field: string,
      bggValue: string | number | undefined,
      pdfValue: string | number | undefined
    ): string | number | undefined => {
      const resolution = conflictResolutions[field];

      if (!resolution) {
        // No conflict, prefer BGG if exists, else PDF
        return bggValue !== undefined ? bggValue : pdfValue;
      }

      if (resolution.source === 'bgg') return bggValue;
      if (resolution.source === 'pdf') return pdfValue;
      if (resolution.source === 'custom') return resolution.customValue;

      return bggValue !== undefined ? bggValue : pdfValue;
    };

    // Build enriched object
    const enriched: EnrichedGameData = {
      // Title (REQUIRED) - ensure string
      title: String(
        getResolvedValue('title', bggGameData?.name, extractedMetadata?.title) || 'Untitled Game'
      ),

      // Year - ensure number or undefined
      yearPublished:
        typeof getResolvedValue(
          'yearPublished',
          bggGameData?.yearPublished,
          extractedMetadata?.yearPublished
        ) === 'number'
          ? Number(
              getResolvedValue(
                'yearPublished',
                bggGameData?.yearPublished,
                extractedMetadata?.yearPublished
              )
            )
          : undefined,

      // Players - ensure number or undefined
      minPlayers:
        typeof getResolvedValue(
          'minPlayers',
          bggGameData?.minPlayers,
          extractedMetadata?.minPlayers
        ) === 'number'
          ? Number(
              getResolvedValue('minPlayers', bggGameData?.minPlayers, extractedMetadata?.minPlayers)
            )
          : undefined,
      maxPlayers:
        typeof getResolvedValue(
          'maxPlayers',
          bggGameData?.maxPlayers,
          extractedMetadata?.maxPlayers
        ) === 'number'
          ? Number(
              getResolvedValue('maxPlayers', bggGameData?.maxPlayers, extractedMetadata?.maxPlayers)
            )
          : undefined,

      // Play time - ensure number or undefined
      playTime:
        typeof getResolvedValue(
          'playTime',
          bggGameData?.playingTime,
          extractedMetadata?.playTime
        ) === 'number'
          ? Number(
              getResolvedValue('playTime', bggGameData?.playingTime, extractedMetadata?.playTime)
            )
          : undefined,

      // Age - ensure number or undefined
      minAge:
        typeof getResolvedValue('minAge', bggGameData?.minAge, extractedMetadata?.minAge) ===
        'number'
          ? Number(getResolvedValue('minAge', bggGameData?.minAge, extractedMetadata?.minAge))
          : undefined,

      // Description - ensure string or undefined
      description:
        typeof getResolvedValue(
          'description',
          bggGameData?.description,
          extractedMetadata?.description
        ) === 'string'
          ? String(
              getResolvedValue(
                'description',
                bggGameData?.description,
                extractedMetadata?.description
              )
            )
          : undefined,

      // Complexity (only in PDF)
      complexity: extractedMetadata?.complexity,

      // BGG ID
      bggId: selectedBggId ?? undefined,

      // Image (only from BGG)
      imageUrl: bggGameData?.imageUrl,
    };

    return enriched;
  }, [extractedMetadata, bggGameData, selectedBggId, conflictResolutions]);

  // Update conflict resolution
  const handleConflictChange = useCallback(
    (field: string, source: ConflictSource) => {
      setConflictResolutions(prev => ({
        ...prev,
        [field]: { source, customValue: source === 'custom' ? customValues[field] : undefined },
      }));
    },
    [customValues]
  );

  // Update custom value
  const handleCustomValueChange = useCallback((field: string, value: string) => {
    setCustomValues(prev => ({ ...prev, [field]: value }));

    // Numeric fields need type coercion
    const numericFields = ['yearPublished', 'minPlayers', 'maxPlayers', 'playTime', 'minAge'];
    const coercedValue: string | number = numericFields.includes(field)
      ? Number(value) || 0
      : value;

    // If custom source is selected, update resolution with coerced value
    setConflictResolutions(prev => {
      if (prev[field]?.source === 'custom') {
        return {
          ...prev,
          [field]: { source: 'custom', customValue: coercedValue },
        };
      }
      return prev;
    });
  }, []);

  // Handle submit with duplicate check
  const handleSubmit = useCallback(async () => {
    if (!enrichedData) {
      toast.error('No data to submit');
      return;
    }

    // Check for duplicate (if not force creating)
    if (!forceCreate && duplicateCheck?.isDuplicate) {
      setShowDuplicateModal(true);
      return;
    }

    try {
      // Save enriched data to store
      resolveConflicts(enrichedData);

      // Submit wizard (calls API)
      await submitWizard();

      // Success handled by store (toast + redirect)
      // If onComplete callback provided, call it
      if (onComplete && selectedBggId) {
        onComplete(selectedBggId.toString());
      }
    } catch (err) {
      // Error handled by store (toast + state)
      logger.error('Failed to submit wizard:', err);
    }
  }, [
    enrichedData,
    resolveConflicts,
    submitWizard,
    onComplete,
    selectedBggId,
    forceCreate,
    duplicateCheck,
  ]);

  // Handle duplicate dialog actions
  const handleDuplicateCancel = useCallback(() => {
    setShowDuplicateModal(false);
  }, []);

  const handleDuplicateReplace = useCallback(() => {
    setShowDuplicateModal(false);
    // TODO: Implement replace logic (Issue #4167 - Replace existing game)
    toast.info('Replace functionality will be implemented in a future update');
  }, []);

  const handleDuplicateCreateAnyway = useCallback(() => {
    setShowDuplicateModal(false);
    setForceCreate(true);
    // Will trigger handleSubmit again with forceCreate=true
    setTimeout(() => {
      handleSubmit();
    }, 100);
  }, [handleSubmit]);

  // Render conflict field
  const renderConflictField = (field: string, label: string) => {
    const conflict = conflicts[field];
    if (!conflict) return null;

    const resolution = conflictResolutions[field];
    const currentValue = resolution?.source === 'custom' ? customValues[field] : '';

    return (
      <Card key={field} className="p-4">
        <div className="space-y-3">
          <Label className="text-sm font-semibold">{label}</Label>

          <RadioGroup
            value={resolution?.source || 'bgg'}
            onValueChange={(value: string) => handleConflictChange(field, value as ConflictSource)}
          >
            {/* BGG Option */}
            <div className="flex items-center space-x-2">
              <RadioGroupItem value="bgg" id={`${field}-bgg`} />
              <Label htmlFor={`${field}-bgg`} className="flex-1 cursor-pointer font-normal">
                <div className="flex items-center justify-between">
                  <span className="text-sm">Use BGG:</span>
                  <span className="font-medium">{String(conflict.bgg)}</span>
                </div>
              </Label>
            </div>

            {/* PDF Option */}
            <div className="flex items-center space-x-2">
              <RadioGroupItem value="pdf" id={`${field}-pdf`} />
              <Label htmlFor={`${field}-pdf`} className="flex-1 cursor-pointer font-normal">
                <div className="flex items-center justify-between">
                  <span className="text-sm">Use PDF:</span>
                  <span className="font-medium">{String(conflict.pdf)}</span>
                </div>
              </Label>
            </div>

            {/* Custom Option */}
            <div className="flex items-start space-x-2">
              <RadioGroupItem value="custom" id={`${field}-custom`} className="mt-2" />
              <div className="flex-1 space-y-2">
                <Label htmlFor={`${field}-custom`} className="cursor-pointer font-normal">
                  Custom:
                </Label>
                <Input
                  type={typeof conflict.bgg === 'number' ? 'number' : 'text'}
                  value={currentValue}
                  onChange={e => handleCustomValueChange(field, e.target.value)}
                  placeholder="Enter custom value..."
                  disabled={resolution?.source !== 'custom'}
                  className={cn(resolution?.source !== 'custom' && 'opacity-50')}
                  data-testid={`${field}-custom-input`}
                />
              </div>
            </div>
          </RadioGroup>
        </div>
      </Card>
    );
  };

  // Validate required data
  if (!extractedMetadata && !bggGameData) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Missing Data</AlertTitle>
        <AlertDescription>
          No extracted metadata or BGG data available. Please go back to previous steps.
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h3 className="text-lg font-semibold">Review & Confirm</h3>
        <p className="text-sm text-muted-foreground">
          Resolve any conflicts between PDF and BGG data, then review the final game details before
          importing.
        </p>
      </div>

      {/* Conflicts Section */}
      {Object.keys(conflicts).length > 0 && (
        <div className="space-y-4">
          <div>
            <h4 className="text-sm font-semibold">Resolve Conflicts</h4>
            <p className="text-xs text-muted-foreground">
              {Object.keys(conflicts).length} conflict
              {Object.keys(conflicts).length !== 1 ? 's' : ''} detected between BGG and PDF data.
            </p>
          </div>

          <div className="space-y-3">
            {renderConflictField('title', 'Game Title')}
            {renderConflictField('yearPublished', 'Year Published')}
            {renderConflictField('minPlayers', 'Min Players')}
            {renderConflictField('maxPlayers', 'Max Players')}
            {renderConflictField('playTime', 'Play Time (minutes)')}
            {renderConflictField('minAge', 'Minimum Age')}
            {renderConflictField('description', 'Description')}
          </div>
        </div>
      )}

      {/* No Conflicts Message */}
      {Object.keys(conflicts).length === 0 && (
        <Alert>
          <CheckCircle2 className="h-4 w-4 text-green-500" />
          <AlertTitle>No Conflicts</AlertTitle>
          <AlertDescription className="text-sm">
            BGG and PDF data have been automatically merged with no conflicts detected.
          </AlertDescription>
        </Alert>
      )}

      {/* Final Preview */}
      {enrichedData && (
        <div className="space-y-4">
          <div>
            <h4 className="text-sm font-semibold">Final Preview</h4>
            <p className="text-xs text-muted-foreground">
              Review how the game will appear after import.
            </p>
          </div>

          <MeepleCard
            entity="game"
            variant="grid"
            title={enrichedData.title}
            subtitle={enrichedData.yearPublished ? `(${enrichedData.yearPublished})` : undefined}
            imageUrl={enrichedData.imageUrl}
            metadata={[
              ...(enrichedData.minPlayers && enrichedData.maxPlayers
                ? [{ label: `${enrichedData.minPlayers}-${enrichedData.maxPlayers} players` }]
                : []),
              ...(enrichedData.playTime ? [{ label: `${enrichedData.playTime} min` }] : []),
              ...(enrichedData.minAge ? [{ label: `Age ${enrichedData.minAge}+` }] : []),
              ...(enrichedData.complexity
                ? [{ label: `Complexity ${enrichedData.complexity.toFixed(1)}` }]
                : []),
            ]}
            showPreview={!!enrichedData.description}
            previewData={{
              description: enrichedData.description?.substring(0, 200),
              complexity: enrichedData.complexity,
            }}
            data-testid="final-preview-card"
          />
        </div>
      )}

      {/* Error Alert */}
      {storeError && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Import Failed</AlertTitle>
          <AlertDescription className="text-sm">{storeError}</AlertDescription>
        </Alert>
      )}

      {/* Submit Button */}
      <div className="flex justify-end">
        <Button
          onClick={handleSubmit}
          disabled={isProcessing || !enrichedData}
          size="lg"
          className="min-w-[200px]"
          data-testid="confirm-import-btn"
        >
          {isProcessing ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              Importing...
            </>
          ) : (
            <>
              <Save className="mr-2 h-4 w-4" />
              Confirm Import
            </>
          )}
        </Button>
      </div>

      {/* Duplicate Warning Dialog */}
      {duplicateCheck?.isDuplicate && (
        <DuplicateWarningDialog
          open={showDuplicateModal}
          existingGame={duplicateCheck.existingGame}
          newGameTitle={enrichedData?.title || 'Unknown'}
          bggId={selectedBggId || 0}
          onCancel={handleDuplicateCancel}
          onReplace={handleDuplicateReplace}
          onCreateAnyway={handleDuplicateCreateAnyway}
        />
      )}
    </div>
  );
}

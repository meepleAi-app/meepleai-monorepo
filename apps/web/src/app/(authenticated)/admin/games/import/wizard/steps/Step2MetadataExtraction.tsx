'use client';

/**
 * Step 2: Metadata Extraction
 * Issue #4163: AI-powered metadata extraction with manual editing
 *
 * Features:
 * - AI extraction with loading UX
 * - Confidence badge (green/yellow/red)
 * - Editable fields for manual correction
 * - Integration with wizard store
 */

import { useEffect, useState, useCallback } from 'react';
import type { JSX } from 'react';

import { Loader2, Save, AlertCircle } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/data-display/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { ConfidenceBadge } from '@/components/ui/feedback/ConfidenceBadge';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { useExtractMetadata } from '@/hooks/queries/useExtractMetadata';
import { useGameImportWizardStore, type ExtractedMetadata } from '@/stores/useGameImportWizardStore';

export interface Step2MetadataExtractionProps {
  /** Callback when extraction and editing are complete */
  onComplete?: (metadata: ExtractedMetadata) => void;
}

export function Step2MetadataExtraction({ onComplete }: Step2MetadataExtractionProps): JSX.Element {
  const { uploadedPdf, extractedMetadata, setExtractedMetadata } = useGameImportWizardStore();

  const { mutate: extractMetadata, isPending: isExtracting, error: extractionError } = useExtractMetadata();

  // Local state for editable fields
  const [formData, setFormData] = useState<ExtractedMetadata>(
    extractedMetadata || {
      title: '',
      yearPublished: undefined,
      minPlayers: undefined,
      maxPlayers: undefined,
      playTime: undefined,
      minAge: undefined,
      description: '',
      confidence: undefined,
    }
  );

  const [hasExtracted, setHasExtracted] = useState(!!extractedMetadata);
  const [hasChanges, setHasChanges] = useState(false);

  // Auto-trigger extraction on mount if not already done
  useEffect(() => {
    if (!uploadedPdf || hasExtracted) return;

    extractMetadata(
      { documentId: uploadedPdf.id },
      {
        onSuccess: metadata => {
          setFormData(metadata);
          setHasExtracted(true);
          setExtractedMetadata(metadata);
          onComplete?.(metadata);
        },
      }
    );
  }, [uploadedPdf, hasExtracted, extractMetadata, setExtractedMetadata, onComplete]);

  // Handle field changes
  const handleFieldChange = useCallback((field: keyof ExtractedMetadata, value: string | number | undefined) => {
    setFormData(prev => ({
      ...prev,
      [field]: value,
    }));
    setHasChanges(true);
  }, []);

  // Save edited metadata
  const handleSave = useCallback(() => {
    setExtractedMetadata(formData);
    setHasChanges(false);
    onComplete?.(formData);
  }, [formData, setExtractedMetadata, onComplete]);

  // Loading state during extraction
  if (isExtracting) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-3">
          <Loader2 className="h-5 w-5 animate-spin text-primary" />
          <div>
            <h3 className="text-lg font-semibold">Extracting Metadata...</h3>
            <p className="text-sm text-muted-foreground">
              AI is analyzing the PDF to extract game information. This may take a few moments.
            </p>
          </div>
        </div>

        <Card className="p-6">
          <div className="space-y-4">
            <Skeleton className="h-8 w-3/4" />
            <Skeleton className="h-20 w-full" />
            <div className="grid grid-cols-2 gap-4">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
            </div>
          </div>
        </Card>
      </div>
    );
  }

  // Error state
  if (extractionError && !hasExtracted) {
    return (
      <div className="space-y-6">
        <div>
          <h3 className="text-lg font-semibold">Metadata Extraction</h3>
          <p className="text-sm text-muted-foreground">
            Extract game information from uploaded PDF using AI.
          </p>
        </div>

        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Extraction Failed</AlertTitle>
          <AlertDescription className="text-sm">
            {extractionError instanceof Error ? extractionError.message : 'An error occurred during metadata extraction'}
          </AlertDescription>
        </Alert>

        <Button
          onClick={() => {
            if (uploadedPdf) {
              extractMetadata({ documentId: uploadedPdf.id });
            }
          }}
          disabled={!uploadedPdf}
        >
          Retry Extraction
        </Button>
      </div>
    );
  }

  // Main form view
  return (
    <div className="space-y-6">
      {/* Header with confidence badge */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <h3 className="text-lg font-semibold">Review Extracted Metadata</h3>
          <p className="text-sm text-muted-foreground">
            AI has extracted the following game information. You can edit any field manually.
          </p>
        </div>
        {formData.confidence !== undefined && (
          <ConfidenceBadge confidence={formData.confidence} size="md" />
        )}
      </div>

      {/* Editable Fields */}
      <Card className="p-6">
        <div className="space-y-6">
          {/* Title */}
          <div className="space-y-2">
            <Label htmlFor="title">
              Game Title <span className="text-destructive">*</span>
            </Label>
            <Input
              id="title"
              type="text"
              value={formData.title || ''}
              onChange={e => handleFieldChange('title', e.target.value)}
              placeholder="Enter game title"
              required
            />
          </div>

          {/* Year and Age */}
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="year">Publication Year</Label>
              <Input
                id="year"
                type="number"
                min="1900"
                max="2100"
                value={formData.yearPublished || ''}
                onChange={e => handleFieldChange('yearPublished', e.target.value ? Number(e.target.value) : undefined)}
                placeholder="e.g., 2024"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="minAge">Minimum Age</Label>
              <div className="flex items-center gap-2">
                <Input
                  id="minAge"
                  type="number"
                  min="0"
                  max="99"
                  value={formData.minAge || ''}
                  onChange={e => handleFieldChange('minAge', e.target.value ? Number(e.target.value) : undefined)}
                  placeholder="e.g., 12"
                />
                <span className="text-sm text-muted-foreground">+</span>
              </div>
            </div>
          </div>

          {/* Players */}
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="minPlayers">Min Players</Label>
              <Input
                id="minPlayers"
                type="number"
                min="1"
                max="99"
                value={formData.minPlayers || ''}
                onChange={e => handleFieldChange('minPlayers', e.target.value ? Number(e.target.value) : undefined)}
                placeholder="e.g., 2"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="maxPlayers">Max Players</Label>
              <Input
                id="maxPlayers"
                type="number"
                min="1"
                max="99"
                value={formData.maxPlayers || ''}
                onChange={e => handleFieldChange('maxPlayers', e.target.value ? Number(e.target.value) : undefined)}
                placeholder="e.g., 4"
              />
            </div>
          </div>

          {/* Playing Time */}
          <div className="space-y-2">
            <Label htmlFor="playTime">Playing Time (minutes)</Label>
            <Input
              id="playTime"
              type="number"
              min="1"
              max="9999"
              value={formData.playTime || ''}
              onChange={e => handleFieldChange('playTime', e.target.value ? Number(e.target.value) : undefined)}
              placeholder="e.g., 60"
            />
          </div>

          {/* Description */}
          <div className="space-y-2">
            <Label htmlFor="description">Description</Label>
            <Textarea
              id="description"
              value={formData.description || ''}
              onChange={e => handleFieldChange('description', e.target.value)}
              placeholder="Enter game description"
              rows={4}
              className="resize-none"
            />
          </div>

          {/* Save Button (visible only when changes made) */}
          {hasChanges && (
            <div className="flex items-center gap-3 rounded-md border border-amber-200 bg-amber-50 p-4 dark:border-amber-800 dark:bg-amber-950">
              <AlertCircle className="h-5 w-5 text-amber-600 dark:text-amber-400" />
              <p className="flex-1 text-sm text-amber-900 dark:text-amber-100">
                You have unsaved changes to the metadata.
              </p>
              <Button onClick={handleSave} size="sm" className="gap-2">
                <Save className="h-4 w-4" />
                Save Changes
              </Button>
            </div>
          )}
        </div>
      </Card>

      {/* Warning for low confidence */}
      {formData.confidence !== undefined && formData.confidence < 50 && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Low Confidence Extraction</AlertTitle>
          <AlertDescription className="text-sm">
            The AI extraction has low confidence ({formData.confidence}%). Please review all fields carefully and make
            corrections as needed.
          </AlertDescription>
        </Alert>
      )}
    </div>
  );
}

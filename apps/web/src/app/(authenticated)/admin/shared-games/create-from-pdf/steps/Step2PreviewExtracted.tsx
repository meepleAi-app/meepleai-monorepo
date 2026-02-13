/**
 * Step2PreviewExtracted - Issue #4141
 *
 * Preview extracted data and collect manual fields.
 */

'use client';

import { useState } from 'react';

import { AlertTriangle } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { usePdfWizardStore } from '@/lib/stores/pdf-wizard-store';
import { cn } from '@/lib/utils';

interface Step2PreviewExtractedProps {
  onNext: () => void;
  onSkipBgg: () => void;
  onBack: () => void;
}

/**
 * Step 2: Preview Extracted Data Component
 *
 * Features:
 * - Display extracted title (readonly)
 * - Quality score badge with color coding
 * - Manual input fields with defaults
 * - Duplicate game warnings
 * - Skip BGG option
 */
export function Step2PreviewExtracted({
  onNext,
  onSkipBgg,
  onBack,
}: Step2PreviewExtractedProps) {
  const wizardData = usePdfWizardStore((state) => ({
    extractedTitle: state.extractedTitle,
    qualityScore: state.qualityScore,
    duplicateWarnings: state.duplicateWarnings,
  }));

  const setStep2Data = usePdfWizardStore((state) => state.setStep2Data);

  const [minPlayers, setMinPlayers] = useState<string>('');
  const [maxPlayers, setMaxPlayers] = useState<string>('');
  const [playingTime, setPlayingTime] = useState<string>('');
  const [minAge, setMinAge] = useState<string>('');
  const [description, setDescription] = useState<string>('');

  const getQualityBadgeColor = (score: number) => {
    if (score >= 0.8) return 'bg-green-100 text-green-900 border-green-300';
    if (score >= 0.5) return 'bg-yellow-100 text-yellow-900 border-yellow-300';
    return 'bg-red-100 text-red-900 border-red-300';
  };

  const handleNext = () => {
    // Save manual fields to store
    setStep2Data({
      manualFields: {
        minPlayers: minPlayers ? parseInt(minPlayers, 10) : undefined,
        maxPlayers: maxPlayers ? parseInt(maxPlayers, 10) : undefined,
        playingTime: playingTime ? parseInt(playingTime, 10) : undefined,
        minAge: minAge ? parseInt(minAge, 10) : undefined,
        description: description || undefined,
      },
    });
    onNext();
  };

  const handleSkipBgg = () => {
    // Save manual fields and skip BGG
    setStep2Data({
      manualFields: {
        minPlayers: minPlayers ? parseInt(minPlayers, 10) : undefined,
        maxPlayers: maxPlayers ? parseInt(maxPlayers, 10) : undefined,
        playingTime: playingTime ? parseInt(playingTime, 10) : undefined,
        minAge: minAge ? parseInt(minAge, 10) : undefined,
        description: description || undefined,
      },
    });
    onSkipBgg();
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-2xl font-bold text-gray-900 mb-2">
          Preview Extracted Data
        </h2>
        <p className="font-nunito text-sm text-gray-600">
          Review the extracted title and provide additional details.
        </p>
      </div>

      {/* Extracted Title (readonly) */}
      <div className="bg-white/70 backdrop-blur-md rounded-lg p-6 border-2 border-gray-200">
        <div className="flex items-start justify-between mb-4">
          <div className="flex-1">
            <Label className="font-nunito text-sm font-semibold text-gray-700 mb-2 block">
              Extracted Title
            </Label>
            <h3 className="font-quicksand text-2xl font-bold text-gray-900">
              {wizardData.extractedTitle || 'No title extracted'}
            </h3>
          </div>

          {/* Quality Badge */}
          <div
            className={cn(
              'px-3 py-1 rounded-full text-sm font-semibold border-2',
              getQualityBadgeColor(wizardData.qualityScore)
            )}
          >
            Quality: {Math.round(wizardData.qualityScore * 100)}%
          </div>
        </div>
      </div>

      {/* Duplicate Warnings */}
      {wizardData.duplicateWarnings.length > 0 && (
        <Alert variant="default" className="bg-yellow-50/70 backdrop-blur-md border-yellow-300">
          <AlertTriangle className="h-4 w-4 text-yellow-600" />
          <AlertTitle className="font-quicksand font-bold text-yellow-900">
            Similar Games Found
          </AlertTitle>
          <AlertDescription className="font-nunito text-sm text-yellow-800 mt-2">
            <ul className="list-disc list-inside space-y-1">
              {wizardData.duplicateWarnings.map((warning, index) => (
                <li key={index}>{warning}</li>
              ))}
            </ul>
            <Button variant="outline" size="sm" className="mt-3" disabled>
              Merge with Existing (Coming Soon)
            </Button>
          </AlertDescription>
        </Alert>
      )}

      {/* Manual Fields Form */}
      <div className="bg-white/70 backdrop-blur-md rounded-lg p-6 border-2 border-gray-200 space-y-4">
        <h3 className="font-quicksand text-lg font-bold text-gray-900 mb-4">
          Game Details
        </h3>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {/* Min Players */}
          <div>
            <Label htmlFor="minPlayers" className="font-nunito text-sm font-semibold">
              Min Players
            </Label>
            <Input
              id="minPlayers"
              type="number"
              min="1"
              placeholder="Default: 1"
              value={minPlayers}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setMinPlayers(e.target.value)}
              className="mt-1"
            />
          </div>

          {/* Max Players */}
          <div>
            <Label htmlFor="maxPlayers" className="font-nunito text-sm font-semibold">
              Max Players
            </Label>
            <Input
              id="maxPlayers"
              type="number"
              min="1"
              placeholder="Default: 4"
              value={maxPlayers}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setMaxPlayers(e.target.value)}
              className="mt-1"
            />
          </div>

          {/* Playing Time */}
          <div>
            <Label htmlFor="playingTime" className="font-nunito text-sm font-semibold">
              Playing Time (minutes)
            </Label>
            <Input
              id="playingTime"
              type="number"
              min="1"
              placeholder="Default: 60"
              value={playingTime}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setPlayingTime(e.target.value)}
              className="mt-1"
            />
          </div>

          {/* Min Age */}
          <div>
            <Label htmlFor="minAge" className="font-nunito text-sm font-semibold">
              Minimum Age
            </Label>
            <Input
              id="minAge"
              type="number"
              min="1"
              placeholder="Default: 8"
              value={minAge}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setMinAge(e.target.value)}
              className="mt-1"
            />
          </div>
        </div>

        {/* Description */}
        <div>
          <Label htmlFor="description" className="font-nunito text-sm font-semibold">
            Description (Optional)
          </Label>
          <Textarea
            id="description"
            placeholder="Provide a brief description of the game..."
            value={description}
            onChange={(e: React.ChangeEvent<HTMLTextAreaElement>) => setDescription(e.target.value)}
            rows={4}
            className="mt-1"
          />
        </div>
      </div>

      {/* Actions */}
      <div className="flex items-center justify-between gap-4">
        <Button variant="outline" onClick={onBack} size="lg">
          Back
        </Button>

        <div className="flex items-center gap-3">
          <Button variant="secondary" onClick={handleSkipBgg} size="lg">
            Skip BGG Match
          </Button>
          <Button onClick={handleNext} size="lg">
            Next: BGG Match
          </Button>
        </div>
      </div>
    </div>
  );
}

/**
 * Step4Confirm - Issue #4141
 *
 * Confirm and submit wizard with role-based approval workflow.
 */

'use client';

import { useState } from 'react';

import { Info, CheckCircle2, Users, Clock, Calendar, Loader2 } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { toast } from '@/components/layout';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { usePdfWizardStore } from '@/lib/stores/pdf-wizard-store';
import { cn } from '@/lib/utils';

interface Step4ConfirmProps {
  onBack: () => void;
  userRole: 'Admin' | 'Editor'; // TODO: Get from auth context
}

/**
 * Step 4: Confirm & Submit Component
 *
 * Features:
 * - Summary cards (game info, BGG match, quality score)
 * - Approval notice for Editors
 * - Role-based submit button label
 * - Success handling with redirect
 */
export function Step4Confirm({ onBack, userRole = 'Admin' }: Step4ConfirmProps) {
  const router = useRouter();
  const [isSubmitting, setIsSubmitting] = useState(false);

  const wizardData = usePdfWizardStore((state) => ({
    pdfDocumentId: state.pdfDocumentId,
    qualityScore: state.qualityScore,
    extractedTitle: state.extractedTitle,
    manualFields: state.manualFields,
    selectedBggId: state.selectedBggId,
    bggDetails: state.bggDetails,
  }));

  const reset = usePdfWizardStore((state) => state.reset);

  const getQualityBadgeColor = (score: number) => {
    if (score >= 0.8) return 'bg-green-100 text-green-900 border-green-300';
    if (score >= 0.5) return 'bg-yellow-100 text-yellow-900 border-yellow-300';
    return 'bg-red-100 text-red-900 border-red-300';
  };

  const handleSubmit = async () => {
    setIsSubmitting(true);

    try {
      // TODO: Replace with actual API call
      // const response = await api.sharedGames.createFromPdf({
      //   pdfDocumentId: wizardData.pdfDocumentId,
      //   ...wizardData.manualFields,
      //   bggId: wizardData.selectedBggId,
      // });

      // Mock API call
      await new Promise((resolve) => setTimeout(resolve, 1500));

      const mockGameId = 'game-' + Math.random().toString(36).substring(7);
      const mockRequestId = 'request-' + Math.random().toString(36).substring(7);

      // Success handling
      if (userRole === 'Admin') {
        toast.success('Game published successfully!');
        reset();
        router.push(`/games/${mockGameId}`);
      } else {
        toast.success('Game submitted for approval!');
        reset();
        router.push(`/admin/shared-games/pending/${mockRequestId}`);
      }
    } catch (error) {
      console.error('Submit error:', error);
      toast.error(
        error instanceof Error ? error.message : 'Failed to create game. Please try again.'
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  const submitButtonLabel = userRole === 'Admin' ? 'Publish Game' : 'Submit for Approval';

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-2xl font-bold text-gray-900 mb-2">
          Review & Confirm
        </h2>
        <p className="font-nunito text-sm text-gray-600">
          Review all details before {userRole === 'Admin' ? 'publishing' : 'submitting'}.
        </p>
      </div>

      {/* Approval Notice for Editors */}
      {userRole === 'Editor' && (
        <Alert variant="default" className="bg-blue-50/70 backdrop-blur-md border-blue-300">
          <Info className="h-4 w-4 text-blue-600" />
          <AlertTitle className="font-quicksand font-bold text-blue-900">
            Approval Required
          </AlertTitle>
          <AlertDescription className="font-nunito text-sm text-blue-800">
            This game will require Admin approval before it can be embedded and made
            available to users.
          </AlertDescription>
        </Alert>
      )}

      {/* Summary Cards */}
      <div className="space-y-4">
        {/* Game Information */}
        <Card className="bg-white/70 backdrop-blur-md border-2 border-gray-200">
          <CardHeader>
            <CardTitle className="font-quicksand text-lg flex items-center justify-between">
              Game Information
              <div
                className={cn(
                  'px-3 py-1 rounded-full text-xs font-semibold border-2',
                  getQualityBadgeColor(wizardData.qualityScore)
                )}
              >
                Quality: {Math.round(wizardData.qualityScore * 100)}%
              </div>
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <div>
              <p className="font-nunito text-sm font-semibold text-gray-700">Title</p>
              <p className="font-quicksand text-xl font-bold text-gray-900 mt-1">
                {wizardData.extractedTitle}
              </p>
            </div>

            <div className="grid grid-cols-2 md:grid-cols-4 gap-4 pt-3 border-t border-gray-200">
              <div className="flex items-center gap-2">
                <Users className="w-4 h-4 text-gray-500" aria-hidden="true" />
                <div>
                  <p className="font-nunito text-xs text-gray-600">Players</p>
                  <p className="font-nunito text-sm font-semibold text-gray-900">
                    {wizardData.manualFields.minPlayers || 1}-
                    {wizardData.manualFields.maxPlayers || 4}
                  </p>
                </div>
              </div>

              <div className="flex items-center gap-2">
                <Clock className="w-4 h-4 text-gray-500" aria-hidden="true" />
                <div>
                  <p className="font-nunito text-xs text-gray-600">Time</p>
                  <p className="font-nunito text-sm font-semibold text-gray-900">
                    {wizardData.manualFields.playingTime || 60} min
                  </p>
                </div>
              </div>

              <div className="flex items-center gap-2">
                <Calendar className="w-4 h-4 text-gray-500" aria-hidden="true" />
                <div>
                  <p className="font-nunito text-xs text-gray-600">Min Age</p>
                  <p className="font-nunito text-sm font-semibold text-gray-900">
                    {wizardData.manualFields.minAge || 8}+
                  </p>
                </div>
              </div>
            </div>

            {wizardData.manualFields.description && (
              <div className="pt-3 border-t border-gray-200">
                <p className="font-nunito text-xs text-gray-600 mb-1">Description</p>
                <p className="font-nunito text-sm text-gray-700">
                  {wizardData.manualFields.description}
                </p>
              </div>
            )}
          </CardContent>
        </Card>

        {/* BGG Match Card */}
        <Card className="bg-white/70 backdrop-blur-md border-2 border-gray-200">
          <CardHeader>
            <CardTitle className="font-quicksand text-lg">
              BoardGameGeek Match
            </CardTitle>
          </CardHeader>
          <CardContent>
            {wizardData.bggDetails ? (
              <div className="flex items-center gap-3">
                <CheckCircle2 className="w-5 h-5 text-green-600" aria-hidden="true" />
                <div>
                  <p className="font-nunito text-sm font-semibold text-gray-900">
                    {wizardData.bggDetails.name} ({wizardData.bggDetails.yearPublished})
                  </p>
                  <p className="font-nunito text-xs text-gray-600">
                    BGG ID: {wizardData.selectedBggId}
                  </p>
                </div>
              </div>
            ) : (
              <p className="font-nunito text-sm text-gray-600 italic">
                No BGG match selected
              </p>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Actions */}
      <div className="flex items-center justify-between gap-4">
        <Button variant="outline" onClick={onBack} size="lg" disabled={isSubmitting}>
          Back
        </Button>

        <Button onClick={handleSubmit} size="lg" disabled={isSubmitting}>
          {isSubmitting && (
            <Loader2 className="w-4 h-4 mr-2 animate-spin" aria-hidden="true" />
          )}
          {submitButtonLabel}
        </Button>
      </div>
    </div>
  );
}

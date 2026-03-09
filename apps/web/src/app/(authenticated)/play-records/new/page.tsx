/**
 * Create Play Record Page
 *
 * Multi-step wizard for creating new play records.
 * Issue #3892: Play Records Frontend UI
 */

'use client';

import { ArrowLeft } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { SessionCreateForm } from '@/components/play-records/SessionCreateForm';
import { Button } from '@/components/ui/primitives/button';
import type { SessionCreateForm as SessionFormData } from '@/lib/api/schemas/play-records.schemas';
import { useCreatePlayRecord } from '@/lib/hooks/use-play-records';

export default function NewPlayRecordPage() {
  const router = useRouter();
  const createRecord = useCreatePlayRecord();

  const handleSubmit = async (data: SessionFormData) => {
    try {
      const recordId = await createRecord.mutateAsync({
        gameId: data.gameId,
        gameName: data.gameName,
        sessionDate: data.sessionDate.toISOString(),
        visibility: data.visibility,
        groupId: data.groupId,
        scoringDimensions: data.enableScoring ? data.scoringDimensions : undefined,
        dimensionUnits: data.enableScoring ? data.dimensionUnits : undefined,
      });

      toast.success('Session Created', {
        description: 'Your play record has been created successfully',
      });

      router.push(`/play-records/${recordId}`);
    } catch (error) {
      toast.error('Error', {
        description: error instanceof Error ? error.message : 'Failed to create session',
      });
    }
  };

  const handleCancel = () => {
    router.push('/play-records');
  };

  return (
    <div className="container mx-auto p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" onClick={handleCancel} aria-label="Back to history">
          <ArrowLeft className="w-5 h-5" />
        </Button>
        <div>
          <h1 className="text-3xl font-bold">New Play Session</h1>
          <p className="text-muted-foreground mt-1">Record a new game session</p>
        </div>
      </div>

      {/* Form */}
      <SessionCreateForm
        onSubmit={handleSubmit}
        onCancel={handleCancel}
        isSubmitting={createRecord.isPending}
      />
    </div>
  );
}

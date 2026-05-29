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
import { useTranslation } from '@/hooks/useTranslation';
import type { SessionCreateForm as SessionFormData } from '@/lib/api/schemas/play-records.schemas';
import { useCreatePlayRecord } from '@/lib/domain-hooks/usePlayRecords';

export default function NewPlayRecordPage() {
  const router = useRouter();
  const { t } = useTranslation();
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

      toast.success(t('playRecords.new.success.toast'), {
        description: t('playRecords.new.success.toastDescription'),
      });

      router.push(`/play-records/${recordId}`);
    } catch (error) {
      toast.error(t('playRecords.new.error.saveFailed'), {
        description:
          error instanceof Error ? error.message : t('playRecords.new.error.saveFailedDescription'),
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
        <Button
          variant="ghost"
          size="icon"
          onClick={handleCancel}
          aria-label={t('playRecords.new.a11y.backToList')}
        >
          <ArrowLeft className="w-5 h-5" />
        </Button>
        <div>
          <h1 className="text-3xl font-bold">{t('playRecords.new.pageTitle')}</h1>
          <p className="text-muted-foreground mt-1">{t('playRecords.new.pageSubtitle')}</p>
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

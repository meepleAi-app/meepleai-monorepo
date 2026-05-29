/**
 * Edit Play Record Page — K5 gate readonly
 *
 * AC-4.1  Layout identico a `new` (riusa SessionCreateForm con prop `mode='edit'`)
 * AC-4.2  Pre-fill: caricamento via `usePlayRecord(id)` + setValues su form
 * AC-4.3  K5 gate readonly: ONLY sessionDate/notes/location editabili
 * AC-4.4  Banner inline sopra form: "Per modificare..." + "Cancella partita" link
 * AC-4.5  Submit → PUT /api/v1/play-records/{id} con UpdatePlayRecordRequest
 * AC-4.6  Delete CTA: confirmation dialog → DELETE → redirect /play-records
 * AC-4.7  K11 cache invalidation post-update/delete
 * AC-4.8  K16 a11y: aria-readonly sui campi non editabili
 *
 * Issue #1488: Play Records reskin — Task 4
 */

'use client';

import { useState } from 'react';

import { useQueryClient } from '@tanstack/react-query';
import { ArrowLeft } from 'lucide-react';
import { useParams, useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { EditGateBanner } from '@/components/play-records/EditGateBanner';
import { SessionCreateForm } from '@/components/play-records/SessionCreateForm';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import {
  type SessionCreateForm as SessionCreateFormData,
  UpdatePlayRecordRequestSchema,
} from '@/lib/api/schemas/play-records.schemas';
import { usePlayRecord, useUpdateRecord, useDeleteRecord } from '@/lib/domain-hooks/usePlayRecords';

export default function EditPlayRecordPage() {
  const params = useParams();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { t } = useTranslation();

  const recordId = typeof params?.id === 'string' ? params.id : '';
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

  const { data: record, isLoading, error } = usePlayRecord(recordId);
  const updateMutation = useUpdateRecord(recordId);
  const deleteMutation = useDeleteRecord(recordId);

  const handleSubmit = async (data: SessionCreateFormData) => {
    try {
      // Extract only the editable fields (K5 gate)
      const updateData = {
        sessionDate: data.sessionDate,
        notes: data.notes,
        location: data.location,
      };

      // Validate against schema
      const validated = UpdatePlayRecordRequestSchema.parse(updateData);
      await updateMutation.mutateAsync(validated);

      // K11 cache invalidation
      queryClient.invalidateQueries({ queryKey: ['play-records', 'detail', recordId] });
      queryClient.invalidateQueries({ queryKey: ['play-records', 'history'] });
      queryClient.invalidateQueries({ queryKey: ['play-records', 'stats'] });

      toast.success(t('playRecords.edit.success.toast'), {
        description: t('playRecords.edit.success.toastDescription'),
      });
      router.push(`/play-records/${recordId}`);
    } catch (error) {
      toast.error(t('playRecords.edit.error.updateFailed'), {
        description: error instanceof Error ? error.message : 'Unknown error',
      });
    }
  };

  const handleDelete = async () => {
    try {
      await deleteMutation.mutateAsync();

      // K11 cache invalidation
      queryClient.invalidateQueries({ queryKey: ['play-records'] });

      toast.success(t('playRecords.edit.success.deleteToast'), {
        description: t('playRecords.edit.success.deleteToastDescription'),
      });
      router.push('/play-records');
    } catch (error) {
      toast.error(t('playRecords.edit.error.deleteFailed'), {
        description: error instanceof Error ? error.message : 'Unknown error',
      });
    }
  };

  const handleCancel = () => {
    router.push(`/play-records/${recordId}`);
  };

  // Loading state
  if (isLoading) {
    return (
      <div className="container mx-auto p-6">
        <div className="h-8 bg-muted animate-pulse rounded w-48 mb-6" />
        <div className="space-y-4">
          <div className="h-12 bg-muted animate-pulse rounded" />
          <div className="h-12 bg-muted animate-pulse rounded" />
          <div className="h-32 bg-muted animate-pulse rounded" />
        </div>
      </div>
    );
  }

  // Error state
  if (error || !record) {
    return (
      <div className="container mx-auto p-6">
        <Alert variant="destructive">
          <AlertDescription>
            {error instanceof Error ? error.message : t('playRecords.edit.error.loadFailed')}
          </AlertDescription>
        </Alert>
        <Button variant="outline" className="mt-4" onClick={() => router.push('/play-records')}>
          <ArrowLeft className="w-4 h-4 mr-2" />
          {t('playRecords.edit.actions.back')}
        </Button>
      </div>
    );
  }

  // Archived state
  if (record.status === 'Archived') {
    return (
      <div className="container mx-auto p-6">
        <Alert>
          <AlertDescription>
            Archived sessions cannot be edited. This is a read-only view.
          </AlertDescription>
        </Alert>
        <Button variant="outline" className="mt-4" onClick={handleCancel}>
          <ArrowLeft className="w-4 h-4 mr-2" />
          {t('playRecords.edit.actions.back')}
        </Button>
      </div>
    );
  }

  // AC-4.1: Layout identical to `new` using SessionCreateForm with mode='edit'
  return (
    <div role="main" aria-label={t('playRecords.edit.a11y.formLabel')}>
      {/* AC-4.4: Banner inline sopra form */}
      <div className="px-4 py-6 md:px-6">
        <EditGateBanner
          onDelete={() => setShowDeleteConfirm(true)}
          isDeleting={deleteMutation.isPending}
        />
      </div>

      {/* AC-4.1: SessionCreateForm with mode='edit' + editable fields whitelist */}
      <SessionCreateForm
        mode="edit"
        onSubmit={handleSubmit}
        onCancel={handleCancel}
        isSubmitting={updateMutation.isPending}
      />

      {/* Delete confirmation dialog */}
      {showDeleteConfirm && (
        // eslint-disable-next-line local/no-hardcoded-color-utility
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
          <div className="bg-background rounded-lg shadow-lg max-w-sm w-full p-6 space-y-4">
            <h2 className="text-lg font-bold text-foreground">
              {t('playRecords.edit.delete.title')}
            </h2>
            <p className="text-sm text-muted-foreground">
              {t('playRecords.edit.delete.description')}
            </p>

            <div className="flex gap-3 justify-end pt-4 border-t border-border">
              <Button
                type="button"
                variant="outline"
                onClick={() => setShowDeleteConfirm(false)}
                disabled={deleteMutation.isPending}
              >
                {t('playRecords.edit.delete.cancel')}
              </Button>
              <Button
                type="button"
                variant="destructive"
                onClick={handleDelete}
                disabled={deleteMutation.isPending}
              >
                {deleteMutation.isPending
                  ? `${t('playRecords.edit.delete.confirm')}…`
                  : t('playRecords.edit.delete.confirm')}
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

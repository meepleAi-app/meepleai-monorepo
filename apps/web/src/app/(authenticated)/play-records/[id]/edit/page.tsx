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

import { useEffect, useRef, useState } from 'react';

import { useQueryClient } from '@tanstack/react-query';
import { ArrowLeft } from 'lucide-react';
import { useParams, useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { FormPageContainer } from '@/components/layout/PageContainer';
import { EditGateBanner } from '@/components/play-records/EditGateBanner';
import { PlayRecordConflictDialog } from '@/components/play-records/PlayRecordConflictDialog';
import {
  SessionCreateForm,
  type SessionCreateFormProps,
} from '@/components/play-records/SessionCreateForm';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import { UpdatePlayRecordError, playRecordsApi } from '@/lib/api/play-records.api';
import {
  type PlayRecordDto,
  type SessionCreateForm as SessionCreateFormData,
  type UpdatePlayRecordRequest,
  UpdatePlayRecordRequestSchema,
} from '@/lib/api/schemas/play-records.schemas';
import { usePlayRecord, useUpdateRecord, useDeleteRecord } from '@/lib/domain-hooks/usePlayRecords';
import { usePlayRecordsStore } from '@/lib/stores/play-records-store';

export default function EditPlayRecordPage() {
  const params = useParams();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { t } = useTranslation();

  const recordId = typeof params?.id === 'string' ? params.id : '';
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [conflictForm, setConflictForm] = useState<SessionCreateFormData | null>(null);

  const { data: record, isLoading, error } = usePlayRecord(recordId);
  // The record the form's values were drawn from (#4801). The form takes `initialValues` only when it
  // mounts, so «Salva» has to be checked against THAT record's `xmin`, not against whatever the cache holds
  // by then: a refetch (window focus, «Ricarica») moves `record.xmin` while the form still shows the old
  // values, and saving the old values with the new `xmin` passes the concurrency check and overwrites the
  // other edit without anyone choosing to. Set once, when the record first arrives; «Ricarica» replaces it
  // and draws the form again (`formKey`). It carries the route id it was taken for, so it can never be
  // read as the record of another id.
  const [baseState, setBaseState] = useState<{ recordId: string; record: PlayRecordDto } | null>(
    null
  );
  const base = baseState?.recordId === recordId ? baseState.record : null;
  const [formKey, setFormKey] = useState(0);
  const [isReloading, setIsReloading] = useState(false);
  const [isOverwriting, setIsOverwriting] = useState(false);
  const isMountedRef = useRef(true);
  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
    };
  }, []);
  // The wizard's step lives in a module-level store that the create page reads too. A conflict used to be
  // answered «saved», so the form reset the step itself; it is now answered «not saved» (#4805) and nothing
  // would: after «Sovrascrivi», or «Continua a modificare» and then leaving, the next wizard would open on
  // the last step. Leaving this page leaves the step where a new wizard starts.
  const { resetSessionCreation } = usePlayRecordsStore();
  useEffect(() => () => resetSessionCreation(), [resetSessionCreation]);
  if (record && base === null) setBaseState({ recordId, record });
  const updateMutation = useUpdateRecord(recordId);
  const deleteMutation = useDeleteRecord(recordId);

  const submitUpdate = async (data: SessionCreateFormData, xmin?: number) => {
    // The form keeps `sessionDate` as a `Date`, the request wants an ISO string. `parse` takes
    // `unknown`, so it is `satisfies` that makes a `Date` here a compile error (#4791).
    const validated = UpdatePlayRecordRequestSchema.parse({
      sessionDate: data.sessionDate.toISOString(),
      notes: data.notes,
      location: data.location,
      xmin,
    } satisfies UpdatePlayRecordRequest);
    const result = await updateMutation.mutateAsync(validated);
    queryClient.invalidateQueries({ queryKey: ['play-records', 'detail', recordId] });
    queryClient.invalidateQueries({ queryKey: ['play-records', 'history'] });
    queryClient.invalidateQueries({ queryKey: ['play-records', 'stats'] });
    toast.success(t('playRecords.edit.success.toast'), {
      description: t('playRecords.edit.success.toastDescription'),
    });
    // #13 / Invariante 4: non-blocking amber warning when the save happened while the
    // user had another session live. Additive to the success toast; the deep-link jumps
    // to that live session so they can double-check the recording order.
    if (result?.warningCode === 'SAVED_WHILE_LIVE_ACTIVE') {
      toast.warning(t('playRecords.edit.savedWhileLive.toast'), {
        duration: 6000,
        ...(result.liveSessionId
          ? {
              action: {
                label: t('playRecords.edit.savedWhileLive.goToLive'),
                onClick: () => router.push(`/sessions/${result.liveSessionId}/live`),
              },
            }
          : {}),
      });
    }
    router.push(`/play-records/${recordId}`);
  };

  // `false` tells the form nothing was saved so it keeps what the user typed (#4793). A conflict answers
  // `false` too (#4805): `conflictForm` is a snapshot for «overwrite», but the typed values stay in the form,
  // so the dialog can be left without losing them. It used to answer «saved» because the form had to be
  // wiped for «reload» to drop the old values; since #4801 «reload» draws the form again (`formKey`) and
  // no longer needs that. Typed with the form's own contract so that what it answers is checked where
  // the contract is defined.
  const handleSubmit: SessionCreateFormProps['onSubmit'] = async data => {
    try {
      await submitUpdate(data, (base ?? record)?.xmin);
    } catch (error) {
      if (error instanceof UpdatePlayRecordError && error.kind === 'conflict') {
        setConflictForm(data);
        return false;
      }
      toast.error(t('playRecords.edit.error.updateFailed'), {
        description: error instanceof Error ? error.message : 'Unknown error',
      });
      return false;
    }
  };

  // «Ricarica» reads the record and draws the form again from it: the old values are dropped on purpose,
  // and the next «Salva» is checked against the `xmin` of what the form now shows. The buttons are off
  // while it reads, so a second click cannot draw the form twice and «Sovrascrivi» cannot run beside it.
  // If the read fails the dialog closes anyway, as it always did, and the query is invalidated: a record
  // that is gone ends in the page's own error state, with a way back. The form keeps its old `base`, so a
  // «Salva» from there is still checked against the old `xmin`.
  const handleConflictReload = async () => {
    setIsReloading(true);
    try {
      const fresh = await playRecordsApi.getRecord(recordId);
      queryClient.setQueryData(['play-records', 'detail', recordId], fresh);
      setBaseState({ recordId, record: fresh });
      // Drawn again from the top, as it was when a conflict reset the step (#4805).
      resetSessionCreation();
      setFormKey(key => key + 1);
    } catch (error) {
      queryClient.invalidateQueries({ queryKey: ['play-records', 'detail', recordId] });
      toast.error(t('playRecords.edit.error.loadFailed'), {
        description: error instanceof Error ? error.message : 'Unknown error',
      });
    } finally {
      setConflictForm(null);
      setIsReloading(false);
    }
  };

  // «Continua a modificare», ESC and a click outside the dialog: leave it and keep editing. The form still
  // holds what was typed (the conflict answered `false`) and the next «Salva» is checked against the `xmin`
  // of `base`, so it meets the conflict again instead of overwriting anything (#4805).
  const handleConflictContinue = () => setConflictForm(null);

  // One «Sovrascrivi» at a time. The read that precedes the send is covered by neither
  // `updateMutation.isPending` nor `isReloading`, so `isOverwriting` turns the buttons off for it too.
  const handleConflictOverwrite = async () => {
    if (!conflictForm) return;
    setIsOverwriting(true);
    try {
      const fresh = await playRecordsApi.getRecord(recordId);
      await submitUpdate(conflictForm, fresh.xmin);
      setConflictForm(null);
    } catch (error) {
      if (error instanceof UpdatePlayRecordError && error.kind === 'conflict') {
        toast.warning(t('playRecords.edit.conflict.stillConflict'));
        return;
      }
      // The dialog stays and so does `conflictForm`, so it can be retried; «Continua a modificare» is the
      // way out that keeps what was typed (#4805). «Ricarica» is the one that drops it.
      toast.error(t('playRecords.edit.error.updateFailed'), {
        description: error instanceof Error ? error.message : 'Unknown error',
      });
    } finally {
      // A successful save has already pushed the route: the page may be gone by now.
      if (isMountedRef.current) setIsOverwriting(false);
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
      <FormPageContainer className="p-6">
        <div className="h-8 bg-muted animate-pulse rounded w-48 mb-6" />
        <div className="space-y-4">
          <div className="h-12 bg-muted animate-pulse rounded" />
          <div className="h-12 bg-muted animate-pulse rounded" />
          <div className="h-32 bg-muted animate-pulse rounded" />
        </div>
      </FormPageContainer>
    );
  }

  // Error state
  if (error || !record) {
    return (
      <FormPageContainer className="p-6">
        <Alert variant="destructive">
          <AlertDescription>
            {error instanceof Error ? error.message : t('playRecords.edit.error.loadFailed')}
          </AlertDescription>
        </Alert>
        <Button variant="outline" className="mt-4" onClick={() => router.push('/play-records')}>
          <ArrowLeft className="w-4 h-4 mr-2" />
          {t('playRecords.edit.actions.back')}
        </Button>
      </FormPageContainer>
    );
  }

  // Archived state
  if (record.status === 'Archived') {
    return (
      <FormPageContainer className="p-6">
        <Alert>
          <AlertDescription>
            Archived sessions cannot be edited. This is a read-only view.
          </AlertDescription>
        </Alert>
        <Button variant="outline" className="mt-4" onClick={handleCancel}>
          <ArrowLeft className="w-4 h-4 mr-2" />
          {t('playRecords.edit.actions.back')}
        </Button>
      </FormPageContainer>
    );
  }

  // AC-4.1: Layout identical to `new` using SessionCreateForm with mode='edit'
  // #2349 AC-4.2: map the loaded record into the form's initial shape.
  // From `base`, not the live `record`: what the form is drawn with has to be what «Salva» is checked against.
  const source = base ?? record;
  const initialValues: Partial<SessionCreateFormData> = {
    gameType: source.gameId ? 'catalog' : 'freeform',
    gameId: source.gameId ?? undefined,
    gameName: source.gameName,
    sessionDate: new Date(source.sessionDate),
    visibility: source.visibility,
    notes: source.notes ?? '',
    location: source.location ?? '',
  };
  const initialPlayers = source.players.map(p => ({
    id: p.id,
    name: p.displayName,
    score: String(p.totalScore ?? p.scores.find(s => s.dimension === 'points')?.value ?? ''),
  }));

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
        key={formKey}
        mode="edit"
        initialValues={initialValues}
        initialPlayers={initialPlayers}
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

      {/* #2437-1: Conflict resolution dialog */}
      <PlayRecordConflictDialog
        open={conflictForm !== null}
        isOverwriting={updateMutation.isPending || isReloading || isOverwriting}
        labels={{
          title: t('playRecords.edit.conflict.title'),
          description: t('playRecords.edit.conflict.description'),
          reload: t('playRecords.edit.conflict.reload'),
          overwrite: t('playRecords.edit.conflict.overwrite'),
          continueEditing: t('playRecords.edit.conflict.continueEditing'),
        }}
        onReload={handleConflictReload}
        onOverwrite={handleConflictOverwrite}
        onContinueEditing={handleConflictContinue}
      />
    </div>
  );
}

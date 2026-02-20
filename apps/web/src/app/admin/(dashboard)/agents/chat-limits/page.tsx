'use client';

/**
 * Admin: Chat History Tier Limits Configuration
 * Issue #4918: Admin system config — chat history tier limits configurable by admin
 *
 * Provides UI for admins to configure maximum chat session counts per user tier.
 * Free=10 | Normal=100 | Premium=1000 (configurable, Premium ≥ Normal ≥ Free)
 * Admin users have unlimited chat history regardless of this setting.
 */

import { useState, useEffect } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import { MessageSquare, RefreshCw, Save } from 'lucide-react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { api } from '@/lib/api';
import type { ChatHistoryLimitsDto } from '@/lib/api/schemas/config.schemas';

// ─── Schema ─────────────────────────────────────────────────────────────────

const chatHistoryLimitsSchema = z
  .object({
    freeTierLimit: z.number().int().min(1, 'Minimo 1'),
    normalTierLimit: z.number().int().min(1, 'Minimo 1'),
    premiumTierLimit: z.number().int().min(1, 'Minimo 1'),
  })
  .refine((data) => data.normalTierLimit >= data.freeTierLimit, {
    message: 'Normal deve essere ≥ Free',
    path: ['normalTierLimit'],
  })
  .refine((data) => data.premiumTierLimit >= data.normalTierLimit, {
    message: 'Premium deve essere ≥ Normal',
    path: ['premiumTierLimit'],
  });

type ChatHistoryLimitsForm = z.infer<typeof chatHistoryLimitsSchema>;

// ─── Page ────────────────────────────────────────────────────────────────────

export default function ChatLimitsPage() {
  const [limits, setLimits] = useState<ChatHistoryLimitsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isDirty },
  } = useForm<ChatHistoryLimitsForm>({
    resolver: zodResolver(chatHistoryLimitsSchema),
    defaultValues: { freeTierLimit: 10, normalTierLimit: 100, premiumTierLimit: 1000 },
  });

  const loadLimits = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await api.config.getChatHistoryLimits();
      setLimits(data);
      reset({
        freeTierLimit: data.freeTierLimit,
        normalTierLimit: data.normalTierLimit,
        premiumTierLimit: data.premiumTierLimit,
      });
    } catch {
      setError('Errore nel caricamento dei limiti chat history.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadLimits();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const onSubmit = async (data: ChatHistoryLimitsForm) => {
    setSubmitting(true);
    setError(null);
    setSuccessMsg(null);
    try {
      const updated = await api.config.updateChatHistoryLimits(data);
      setLimits(updated);
      reset(data);
      setSuccessMsg('Limiti aggiornati con successo.');
    } catch {
      setError('Errore nel salvataggio dei limiti.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Chat History Limits
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Configura il numero massimo di chat salvate per tier utente
          </p>
        </div>
        <Button variant="outline" size="icon" onClick={() => void loadLimits()} disabled={loading}>
          <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
        </Button>
      </div>

      {/* Feedback */}
      {error && (
        <Alert variant="destructive">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}
      {successMsg && (
        <Alert>
          <AlertDescription className="text-emerald-600">{successMsg}</AlertDescription>
        </Alert>
      )}

      {/* Form */}
      <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <MessageSquare className="h-4 w-4 text-amber-500" />
            Limiti per Tier
          </CardTitle>
          <CardDescription>
            Le chat più vecchie vengono archiviate automaticamente quando si supera il limite
            (non cancellate). Gli admin hanno storia illimitata.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="space-y-4">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
            </div>
          ) : (
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
              {/* Free Tier */}
              <div className="space-y-1.5">
                <Label htmlFor="freeTierLimit" className="font-semibold">
                  Free tier
                </Label>
                <div className="flex items-center gap-3">
                  <Input
                    id="freeTierLimit"
                    type="number"
                    min={1}
                    className="w-36"
                    {...register('freeTierLimit', { valueAsNumber: true })}
                  />
                  <span className="text-sm text-muted-foreground">chat</span>
                </div>
                {errors.freeTierLimit && (
                  <p className="text-xs text-destructive">{errors.freeTierLimit.message}</p>
                )}
              </div>

              {/* Normal Tier */}
              <div className="space-y-1.5">
                <Label htmlFor="normalTierLimit" className="font-semibold">
                  Normal tier
                </Label>
                <div className="flex items-center gap-3">
                  <Input
                    id="normalTierLimit"
                    type="number"
                    min={1}
                    className="w-36"
                    {...register('normalTierLimit', { valueAsNumber: true })}
                  />
                  <span className="text-sm text-muted-foreground">chat</span>
                </div>
                {errors.normalTierLimit && (
                  <p className="text-xs text-destructive">{errors.normalTierLimit.message}</p>
                )}
              </div>

              {/* Premium Tier */}
              <div className="space-y-1.5">
                <Label htmlFor="premiumTierLimit" className="font-semibold">
                  Premium tier
                </Label>
                <div className="flex items-center gap-3">
                  <Input
                    id="premiumTierLimit"
                    type="number"
                    min={1}
                    className="w-36"
                    {...register('premiumTierLimit', { valueAsNumber: true })}
                  />
                  <span className="text-sm text-muted-foreground">chat</span>
                </div>
                {errors.premiumTierLimit && (
                  <p className="text-xs text-destructive">{errors.premiumTierLimit.message}</p>
                )}
              </div>

              {/* Admin row */}
              <div className="rounded-lg border border-border/50 bg-muted/30 px-4 py-3">
                <div className="flex items-center gap-3">
                  <span className="font-semibold text-sm">Admin</span>
                  <span className="text-sm text-muted-foreground">Illimitato</span>
                </div>
              </div>

              {/* Actions */}
              <div className="flex items-center justify-between pt-2 border-t border-border/50">
                <Button type="submit" disabled={!isDirty || submitting} className="gap-2">
                  {submitting ? (
                    <RefreshCw className="h-4 w-4 animate-spin" />
                  ) : (
                    <Save className="h-4 w-4" />
                  )}
                  {submitting ? 'Salvataggio...' : 'Salva'}
                </Button>

                {limits && (
                  <p className="text-xs text-muted-foreground">
                    Aggiornato il{' '}
                    {new Date(limits.lastUpdatedAt).toLocaleString('it-IT', {
                      day: '2-digit',
                      month: '2-digit',
                      year: 'numeric',
                      hour: '2-digit',
                      minute: '2-digit',
                    })}
                  </p>
                )}
              </div>
            </form>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

'use client';

/**
 * Game Library Limits Configuration Page - Client Component
 * Issue #2444: Admin UI - Configure Game Library Tier Limits
 *
 * Provides UI for admins to configure maximum game counts per user tier.
 * Features:
 * - Real-time configuration loading
 * - Form validation (min=1, max=1000, tier hierarchy)
 * - Automatic cache invalidation after updates
 * - Audit trail display (last updated by/at)
 */

import { useState, useEffect } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';

import { AdminAuthGuard } from '@/components/admin/AdminAuthGuard';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { ErrorDisplay } from '@/components/errors';
import { toast } from '@/components/layout';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { api, type GameLibraryLimitsDto } from '@/lib/api';
import { categorizeError } from '@/lib/errorUtils';

// Form validation schema with business rules
const gameLibraryLimitsSchema = z.object({
  freeTierLimit: z.number().int().min(1, 'Minimum 1 game').max(1000, 'Maximum 1000 games'),
  normalTierLimit: z.number().int().min(1, 'Minimum 1 game').max(1000, 'Maximum 1000 games'),
  premiumTierLimit: z.number().int().min(1, 'Minimum 1 game').max(1000, 'Maximum 1000 games'),
}).refine(
  (data) => data.normalTierLimit >= data.freeTierLimit,
  {
    message: 'Normal tier limit must be >= Free tier limit',
    path: ['normalTierLimit'],
  }
).refine(
  (data) => data.premiumTierLimit >= data.normalTierLimit,
  {
    message: 'Premium tier limit must be >= Normal tier limit',
    path: ['premiumTierLimit'],
  }
);

type GameLibraryLimitsForm = z.infer<typeof gameLibraryLimitsSchema>;

export function GameLibraryLimitsClient() {
  const { user, loading: authLoading } = useAuthUser();

  const [limits, setLimits] = useState<GameLibraryLimitsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isDirty },
  } = useForm<GameLibraryLimitsForm>({
    resolver: zodResolver(gameLibraryLimitsSchema),
  });

  // Load initial data
  useEffect(() => {
    loadLimits();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const loadLimits = async () => {
    try {
      setLoading(true);
      setError(null);

      const data = await api.config.getGameLibraryLimits();
      setLimits(data);

      // Reset form with fetched values
      reset({
        freeTierLimit: data.freeTierLimit,
        normalTierLimit: data.normalTierLimit,
        premiumTierLimit: data.premiumTierLimit,
      });
    } catch (err) {
      const { message } = categorizeError(err);
      setError(message);

      if (message.includes('Unauthorized') || message.includes('403')) {
        toast.error('Admin access required');
        setTimeout(() => {
          window.location.href = '/login';
        }, 2000);
      } else {
        toast.error(message);
      }
    } finally {
      setLoading(false);
    }
  };

  const onSubmit = async (data: GameLibraryLimitsForm) => {
    try {
      setSubmitting(true);
      setError(null);

      const updated = await api.config.updateGameLibraryLimits({
        freeTierLimit: data.freeTierLimit,
        normalTierLimit: data.normalTierLimit,
        premiumTierLimit: data.premiumTierLimit,
      });

      setLimits(updated);
      reset(data); // Mark form as pristine after successful save

      toast.success('Game library limits updated successfully');
    } catch (err) {
      const { userMessage } = categorizeError(err);
      setError(userMessage);
      toast.error(userMessage);
    } finally {
      setSubmitting(false);
    }
  };

  const handleReset = () => {
    if (limits) {
      reset({
        freeTierLimit: limits.freeTierLimit,
        normalTierLimit: limits.normalTierLimit,
        premiumTierLimit: limits.premiumTierLimit,
      });
      toast.info('Form reset to current values');
    }
  };

  if (!user) return null;

  // Loading state
  if (loading) {
    return (
      <AdminAuthGuard
        loading={authLoading}
        user={user}
        backgroundClass="min-h-dvh flex items-center justify-center bg-slate-50 dark:bg-slate-900"
      >
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto mb-4" />
          <p className="text-slate-600 dark:text-slate-400">Loading game library limits...</p>
        </div>
      </AdminAuthGuard>
    );
  }

  // Error state (if not auth error)
  if (error && !error.includes('Unauthorized') && !error.includes('403')) {
    return (
      <AdminAuthGuard
        loading={authLoading}
        user={user}
        backgroundClass="min-h-dvh bg-slate-50 dark:bg-slate-900"
      >
        <div className="container mx-auto px-4 py-8">
          <ErrorDisplay
            error={error}
            onRetry={loadLimits}
            actionLabel="Retry Loading"
          />
        </div>
      </AdminAuthGuard>
    );
  }

  return (
    <AdminAuthGuard
      loading={authLoading}
      user={user}
      backgroundClass="min-h-dvh bg-slate-50 dark:bg-slate-900"
    >
      <div className="container mx-auto px-4 py-8 max-w-4xl">
        {/* Header */}
        <div className="mb-6">
          <h1 className="text-3xl font-bold text-slate-900 dark:text-slate-100">
            Game Library Tier Limits
          </h1>
          <p className="text-slate-600 dark:text-slate-400 mt-2">
            Configure maximum game counts for Free, Normal, and Premium user tiers
          </p>
        </div>

        {/* Configuration Form */}
        <Card>
          <CardHeader>
            <CardTitle>Tier Limits Configuration</CardTitle>
            <CardDescription>
              Set maximum number of games users can add to their library based on their subscription tier.
              Changes propagate within 5 minutes.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
              {/* Free Tier */}
              <div className="space-y-2">
                <Label htmlFor="freeTierLimit" className="text-base font-semibold">
                  Free Tier Limit
                </Label>
                <Input
                  id="freeTierLimit"
                  type="number"
                  min={1}
                  max={1000}
                  {...register('freeTierLimit', { valueAsNumber: true })}
                  className={errors.freeTierLimit ? 'border-red-500' : ''}
                />
                {errors.freeTierLimit && (
                  <p className="text-sm text-red-500">{errors.freeTierLimit.message}</p>
                )}
                <p className="text-sm text-slate-500">Maximum games for free users (1-1000)</p>
              </div>

              {/* Normal Tier */}
              <div className="space-y-2">
                <Label htmlFor="normalTierLimit" className="text-base font-semibold">
                  Normal Tier Limit
                </Label>
                <Input
                  id="normalTierLimit"
                  type="number"
                  min={1}
                  max={1000}
                  {...register('normalTierLimit', { valueAsNumber: true })}
                  className={errors.normalTierLimit ? 'border-red-500' : ''}
                />
                {errors.normalTierLimit && (
                  <p className="text-sm text-red-500">{errors.normalTierLimit.message}</p>
                )}
                <p className="text-sm text-slate-500">
                  Maximum games for normal tier users (must be ≥ Free tier)
                </p>
              </div>

              {/* Premium Tier */}
              <div className="space-y-2">
                <Label htmlFor="premiumTierLimit" className="text-base font-semibold">
                  Premium Tier Limit
                </Label>
                <Input
                  id="premiumTierLimit"
                  type="number"
                  min={1}
                  max={1000}
                  {...register('premiumTierLimit', { valueAsNumber: true })}
                  className={errors.premiumTierLimit ? 'border-red-500' : ''}
                />
                {errors.premiumTierLimit && (
                  <p className="text-sm text-red-500">{errors.premiumTierLimit.message}</p>
                )}
                <p className="text-sm text-slate-500">
                  Maximum games for premium users (must be ≥ Normal tier)
                </p>
              </div>

              {/* Action Buttons */}
              <div className="flex items-center justify-between pt-4 border-t">
                <div className="flex gap-2">
                  <Button
                    type="submit"
                    disabled={!isDirty || submitting}
                    className="min-w-[120px]"
                  >
                    {submitting ? (
                      <>
                        <span className="animate-spin mr-2">⏳</span>
                        Saving...
                      </>
                    ) : (
                      'Save Changes'
                    )}
                  </Button>
                  <Button
                    type="button"
                    variant="outline"
                    onClick={handleReset}
                    disabled={!isDirty || submitting}
                  >
                    Reset
                  </Button>
                </div>

                {limits && (
                  <div className="text-sm text-slate-500">
                    Last updated: {new Date(limits.lastUpdatedAt).toLocaleString()}
                    {limits.lastUpdatedByUserId && (
                      <span className="ml-2">by {limits.lastUpdatedByUserId.slice(0, 8)}</span>
                    )}
                  </div>
                )}
              </div>
            </form>
          </CardContent>
        </Card>

        {/* Info Card */}
        <Card className="mt-6">
          <CardHeader>
            <CardTitle className="text-lg">About Tier Limits</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm text-slate-600 dark:text-slate-400">
            <p>
              <strong>Free Tier:</strong> Basic users with limited game library capacity
            </p>
            <p>
              <strong>Normal Tier:</strong> Standard users with expanded library access
            </p>
            <p>
              <strong>Premium Tier:</strong> Premium subscribers with maximum library capacity
            </p>
            <p className="pt-2 border-t text-xs">
              Note: Admin and Editor roles have unlimited library capacity regardless of tier.
              Configuration changes are cached for 5 minutes and broadcast via domain events.
            </p>
          </CardContent>
        </Card>
      </div>
    </AdminAuthGuard>
  );
}

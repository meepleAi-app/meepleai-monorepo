'use client';

/**
 * PDF Tier Upload Limits Configuration Page - Client Component
 * Issue #3333: PDF Upload Limits Configuration UI
 *
 * Provides UI for admins to configure daily/weekly PDF upload limits per user tier.
 * Features:
 * - Real-time configuration loading
 * - Form validation (min=1, max limits, tier hierarchy)
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
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { api, type PdfTierUploadLimitsDto } from '@/lib/api';
import { categorizeError, type CategorizedError } from '@/lib/errorUtils';

// Form validation schema with business rules
const pdfTierLimitsSchema = z
  .object({
    freeDailyLimit: z
      .number()
      .int()
      .min(1, 'Minimum 1')
      .max(1000, 'Maximum 1000'),
    freeWeeklyLimit: z
      .number()
      .int()
      .min(1, 'Minimum 1')
      .max(5000, 'Maximum 5000'),
    normalDailyLimit: z
      .number()
      .int()
      .min(1, 'Minimum 1')
      .max(1000, 'Maximum 1000'),
    normalWeeklyLimit: z
      .number()
      .int()
      .min(1, 'Minimum 1')
      .max(5000, 'Maximum 5000'),
    premiumDailyLimit: z
      .number()
      .int()
      .min(1, 'Minimum 1')
      .max(1000, 'Maximum 1000'),
    premiumWeeklyLimit: z
      .number()
      .int()
      .min(1, 'Minimum 1')
      .max(5000, 'Maximum 5000'),
  })
  .refine((data) => data.freeWeeklyLimit >= data.freeDailyLimit, {
    message: 'Weekly limit must be >= daily limit',
    path: ['freeWeeklyLimit'],
  })
  .refine((data) => data.normalWeeklyLimit >= data.normalDailyLimit, {
    message: 'Weekly limit must be >= daily limit',
    path: ['normalWeeklyLimit'],
  })
  .refine((data) => data.premiumWeeklyLimit >= data.premiumDailyLimit, {
    message: 'Weekly limit must be >= daily limit',
    path: ['premiumWeeklyLimit'],
  })
  .refine((data) => data.normalDailyLimit >= data.freeDailyLimit, {
    message: 'Normal tier must be >= Free tier',
    path: ['normalDailyLimit'],
  })
  .refine((data) => data.premiumDailyLimit >= data.normalDailyLimit, {
    message: 'Premium tier must be >= Normal tier',
    path: ['premiumDailyLimit'],
  })
  .refine((data) => data.normalWeeklyLimit >= data.freeWeeklyLimit, {
    message: 'Normal tier must be >= Free tier',
    path: ['normalWeeklyLimit'],
  })
  .refine((data) => data.premiumWeeklyLimit >= data.normalWeeklyLimit, {
    message: 'Premium tier must be >= Normal tier',
    path: ['premiumWeeklyLimit'],
  });

type PdfTierLimitsForm = z.infer<typeof pdfTierLimitsSchema>;

export function PdfTierLimitsClient() {
  const { user, loading: authLoading } = useAuthUser();

  const [limits, setLimits] = useState<PdfTierUploadLimitsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<CategorizedError | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isDirty },
  } = useForm<PdfTierLimitsForm>({
    resolver: zodResolver(pdfTierLimitsSchema),
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

      const data = await api.config.getPdfTierUploadLimits();
      setLimits(data);

      // Reset form with fetched values
      reset({
        freeDailyLimit: data.freeDailyLimit,
        freeWeeklyLimit: data.freeWeeklyLimit,
        normalDailyLimit: data.normalDailyLimit,
        normalWeeklyLimit: data.normalWeeklyLimit,
        premiumDailyLimit: data.premiumDailyLimit,
        premiumWeeklyLimit: data.premiumWeeklyLimit,
      });
    } catch (err) {
      const categorized = categorizeError(err);
      setError(categorized);

      if (
        categorized.message.includes('Unauthorized') ||
        categorized.message.includes('403')
      ) {
        toast.error('Admin access required');
        setTimeout(() => {
          window.location.href = '/login';
        }, 2000);
      } else {
        toast.error(categorized.message);
      }
    } finally {
      setLoading(false);
    }
  };

  const onSubmit = async (data: PdfTierLimitsForm) => {
    try {
      setSubmitting(true);
      setError(null);

      const updated = await api.config.updatePdfTierUploadLimits({
        freeDailyLimit: data.freeDailyLimit,
        freeWeeklyLimit: data.freeWeeklyLimit,
        normalDailyLimit: data.normalDailyLimit,
        normalWeeklyLimit: data.normalWeeklyLimit,
        premiumDailyLimit: data.premiumDailyLimit,
        premiumWeeklyLimit: data.premiumWeeklyLimit,
      });

      setLimits(updated);
      reset(data); // Mark form as pristine after successful save

      toast.success('PDF tier upload limits updated successfully');
    } catch (err) {
      const categorized = categorizeError(err);
      setError(categorized);
      toast.error(categorized.message);
    } finally {
      setSubmitting(false);
    }
  };

  const handleReset = () => {
    if (limits) {
      reset({
        freeDailyLimit: limits.freeDailyLimit,
        freeWeeklyLimit: limits.freeWeeklyLimit,
        normalDailyLimit: limits.normalDailyLimit,
        normalWeeklyLimit: limits.normalWeeklyLimit,
        premiumDailyLimit: limits.premiumDailyLimit,
        premiumWeeklyLimit: limits.premiumWeeklyLimit,
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
          <p className="text-slate-600 dark:text-slate-400">
            Loading PDF upload tier limits...
          </p>
        </div>
      </AdminAuthGuard>
    );
  }

  // Error state (if not auth error)
  if (
    error &&
    !error.message.includes('Unauthorized') &&
    !error.message.includes('403')
  ) {
    return (
      <AdminAuthGuard
        loading={authLoading}
        user={user}
        backgroundClass="min-h-dvh bg-slate-50 dark:bg-slate-900"
      >
        <div className="container mx-auto px-4 py-8">
          <ErrorDisplay error={error} onRetry={loadLimits} />
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
            PDF Upload Tier Limits
          </h1>
          <p className="text-slate-600 dark:text-slate-400 mt-2">
            Configure daily and weekly PDF upload limits per subscription tier
          </p>
        </div>

        {/* Configuration Form */}
        <Card>
          <CardHeader>
            <CardTitle>Upload Limits Per Tier</CardTitle>
            <CardDescription>
              Set maximum PDF uploads per day and week for each subscription
              tier. Changes take effect immediately.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-8">
              {/* Free Tier Section */}
              <div className="space-y-4 p-4 bg-slate-50 dark:bg-slate-800 rounded-lg">
                <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">
                  Free Tier
                </h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="freeDailyLimit">Daily Limit</Label>
                    <Input
                      id="freeDailyLimit"
                      type="number"
                      min={1}
                      max={1000}
                      {...register('freeDailyLimit', { valueAsNumber: true })}
                      className={errors.freeDailyLimit ? 'border-red-500' : ''}
                    />
                    {errors.freeDailyLimit && (
                      <p className="text-sm text-red-500">
                        {errors.freeDailyLimit.message}
                      </p>
                    )}
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="freeWeeklyLimit">Weekly Limit</Label>
                    <Input
                      id="freeWeeklyLimit"
                      type="number"
                      min={1}
                      max={5000}
                      {...register('freeWeeklyLimit', { valueAsNumber: true })}
                      className={errors.freeWeeklyLimit ? 'border-red-500' : ''}
                    />
                    {errors.freeWeeklyLimit && (
                      <p className="text-sm text-red-500">
                        {errors.freeWeeklyLimit.message}
                      </p>
                    )}
                  </div>
                </div>
              </div>

              {/* Normal Tier Section */}
              <div className="space-y-4 p-4 bg-blue-50 dark:bg-blue-900/20 rounded-lg">
                <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">
                  Normal Tier
                </h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="normalDailyLimit">Daily Limit</Label>
                    <Input
                      id="normalDailyLimit"
                      type="number"
                      min={1}
                      max={1000}
                      {...register('normalDailyLimit', { valueAsNumber: true })}
                      className={errors.normalDailyLimit ? 'border-red-500' : ''}
                    />
                    {errors.normalDailyLimit && (
                      <p className="text-sm text-red-500">
                        {errors.normalDailyLimit.message}
                      </p>
                    )}
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="normalWeeklyLimit">Weekly Limit</Label>
                    <Input
                      id="normalWeeklyLimit"
                      type="number"
                      min={1}
                      max={5000}
                      {...register('normalWeeklyLimit', { valueAsNumber: true })}
                      className={
                        errors.normalWeeklyLimit ? 'border-red-500' : ''
                      }
                    />
                    {errors.normalWeeklyLimit && (
                      <p className="text-sm text-red-500">
                        {errors.normalWeeklyLimit.message}
                      </p>
                    )}
                  </div>
                </div>
              </div>

              {/* Premium Tier Section */}
              <div className="space-y-4 p-4 bg-amber-50 dark:bg-amber-900/20 rounded-lg">
                <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">
                  Premium Tier
                </h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="premiumDailyLimit">Daily Limit</Label>
                    <Input
                      id="premiumDailyLimit"
                      type="number"
                      min={1}
                      max={1000}
                      {...register('premiumDailyLimit', { valueAsNumber: true })}
                      className={
                        errors.premiumDailyLimit ? 'border-red-500' : ''
                      }
                    />
                    {errors.premiumDailyLimit && (
                      <p className="text-sm text-red-500">
                        {errors.premiumDailyLimit.message}
                      </p>
                    )}
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="premiumWeeklyLimit">Weekly Limit</Label>
                    <Input
                      id="premiumWeeklyLimit"
                      type="number"
                      min={1}
                      max={5000}
                      {...register('premiumWeeklyLimit', {
                        valueAsNumber: true,
                      })}
                      className={
                        errors.premiumWeeklyLimit ? 'border-red-500' : ''
                      }
                    />
                    {errors.premiumWeeklyLimit && (
                      <p className="text-sm text-red-500">
                        {errors.premiumWeeklyLimit.message}
                      </p>
                    )}
                  </div>
                </div>
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
                      <span className="ml-2">
                        by {limits.lastUpdatedByUserId.slice(0, 8)}
                      </span>
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
            <CardTitle className="text-lg">About PDF Upload Limits</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm text-slate-600 dark:text-slate-400">
            <p>
              <strong>Daily Limit:</strong> Maximum PDF uploads allowed per day
              (resets at midnight UTC)
            </p>
            <p>
              <strong>Weekly Limit:</strong> Maximum PDF uploads allowed per
              week (resets on Monday UTC)
            </p>
            <p>
              <strong>Tier Hierarchy:</strong> Higher tiers must have limits
              equal to or greater than lower tiers
            </p>
            <p className="pt-2 border-t text-xs">
              Note: Admin and Editor roles have unlimited upload capacity
              regardless of tier. These limits are enforced in real-time via
              Redis counters.
            </p>
          </CardContent>
        </Card>
      </div>
    </AdminAuthGuard>
  );
}

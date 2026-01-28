'use client';

/**
 * PDF Upload Limits Configuration Component
 * Issue #3078: Admin UI - PDF Limits Configuration
 *
 * Provides UI for admins to configure PDF upload restrictions.
 * Features:
 * - File size input with MB/GB unit selector
 * - Numeric inputs for pages and documents limits
 * - Multi-select for allowed MIME types
 * - Form validation with React Hook Form + Zod
 * - Audit trail display (last updated by/at)
 */

import { useState, useEffect } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import { useForm, Controller } from 'react-hook-form';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { api, type PdfUploadLimitsDto } from '@/lib/api';
import { categorizeError, type CategorizedError } from '@/lib/errorUtils';

// Available MIME types for PDF documents
const AVAILABLE_MIME_TYPES = [
  { value: 'application/pdf', label: 'PDF (application/pdf)' },
  { value: 'application/x-pdf', label: 'PDF Legacy (application/x-pdf)' },
  { value: 'application/acrobat', label: 'Acrobat (application/acrobat)' },
  { value: 'application/vnd.pdf', label: 'PDF Vendor (application/vnd.pdf)' },
  { value: 'text/pdf', label: 'Text PDF (text/pdf)' },
  { value: 'text/x-pdf', label: 'Text PDF Legacy (text/x-pdf)' },
];

// Size units for file size conversion
type SizeUnit = 'MB' | 'GB';

const BYTES_PER_MB = 1024 * 1024;
const BYTES_PER_GB = 1024 * 1024 * 1024;

// Helper to convert bytes to display value
function bytesToDisplayValue(bytes: number, unit: SizeUnit): number {
  return unit === 'GB' ? bytes / BYTES_PER_GB : bytes / BYTES_PER_MB;
}

// Helper to convert display value to bytes
function displayValueToBytes(value: number, unit: SizeUnit): number {
  return Math.round(unit === 'GB' ? value * BYTES_PER_GB : value * BYTES_PER_MB);
}

// Form validation schema
const pdfLimitsFormSchema = z.object({
  fileSizeValue: z.number().min(1, 'Minimum 1').max(10000, 'Maximum 10000'),
  fileSizeUnit: z.enum(['MB', 'GB']),
  maxPagesPerDocument: z
    .number()
    .int()
    .min(1, 'Minimum 1 page')
    .max(10000, 'Maximum 10,000 pages'),
  maxDocumentsPerGame: z
    .number()
    .int()
    .min(1, 'Minimum 1 document')
    .max(1000, 'Maximum 1,000 documents'),
  allowedMimeTypes: z.array(z.string()).min(1, 'Select at least one MIME type'),
});

type PdfLimitsForm = z.infer<typeof pdfLimitsFormSchema>;

export function PdfLimitsConfig() {
  const { user, loading: authLoading } = useAuthUser();

  const [limits, setLimits] = useState<PdfUploadLimitsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<CategorizedError | null>(null);

  const {
    register,
    control,
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { errors, isDirty },
  } = useForm<PdfLimitsForm>({
    resolver: zodResolver(pdfLimitsFormSchema),
    defaultValues: {
      fileSizeValue: 100,
      fileSizeUnit: 'MB',
      maxPagesPerDocument: 500,
      maxDocumentsPerGame: 10,
      allowedMimeTypes: ['application/pdf'],
    },
  });

  const watchedMimeTypes = watch('allowedMimeTypes');
  const watchedUnit = watch('fileSizeUnit');

  // Load initial data
  useEffect(() => {
    loadLimits();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const loadLimits = async () => {
    try {
      setLoading(true);
      setError(null);

      const data = await api.config.getPdfUploadLimits();
      setLimits(data);

      // Determine best unit for display
      const unit: SizeUnit = data.maxFileSizeBytes >= BYTES_PER_GB ? 'GB' : 'MB';
      const displayValue = bytesToDisplayValue(data.maxFileSizeBytes, unit);

      // Reset form with fetched values
      reset({
        fileSizeValue: Math.round(displayValue * 100) / 100,
        fileSizeUnit: unit,
        maxPagesPerDocument: data.maxPagesPerDocument,
        maxDocumentsPerGame: data.maxDocumentsPerGame,
        allowedMimeTypes: data.allowedMimeTypes,
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

  const onSubmit = async (data: PdfLimitsForm) => {
    try {
      setSubmitting(true);
      setError(null);

      const maxFileSizeBytes = displayValueToBytes(
        data.fileSizeValue,
        data.fileSizeUnit
      );

      const updated = await api.config.updatePdfUploadLimits({
        maxFileSizeBytes,
        maxPagesPerDocument: data.maxPagesPerDocument,
        maxDocumentsPerGame: data.maxDocumentsPerGame,
        allowedMimeTypes: data.allowedMimeTypes,
      });

      setLimits(updated);
      reset(data); // Mark form as pristine after successful save

      toast.success('PDF upload limits updated successfully');
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
      const unit: SizeUnit =
        limits.maxFileSizeBytes >= BYTES_PER_GB ? 'GB' : 'MB';
      const displayValue = bytesToDisplayValue(limits.maxFileSizeBytes, unit);

      reset({
        fileSizeValue: Math.round(displayValue * 100) / 100,
        fileSizeUnit: unit,
        maxPagesPerDocument: limits.maxPagesPerDocument,
        maxDocumentsPerGame: limits.maxDocumentsPerGame,
        allowedMimeTypes: limits.allowedMimeTypes,
      });
      toast.info('Form reset to current values');
    }
  };

  const handleMimeTypeToggle = (mimeType: string, checked: boolean) => {
    const current = watchedMimeTypes || [];
    if (checked) {
      setValue('allowedMimeTypes', [...current, mimeType], {
        shouldDirty: true,
      });
    } else {
      setValue(
        'allowedMimeTypes',
        current.filter((t) => t !== mimeType),
        { shouldDirty: true }
      );
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
            Loading PDF upload limits...
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
            PDF Upload Limits
          </h1>
          <p className="text-slate-600 dark:text-slate-400 mt-2">
            Configure restrictions for PDF document uploads
          </p>
        </div>

        {/* Configuration Form */}
        <Card>
          <CardHeader>
            <CardTitle>Upload Limits Configuration</CardTitle>
            <CardDescription>
              Set maximum file size, pages per document, documents per game, and
              allowed MIME types. Changes propagate within 5 minutes.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
              {/* File Size with Unit Selector */}
              <div className="space-y-2">
                <Label
                  htmlFor="fileSizeValue"
                  className="text-base font-semibold"
                >
                  Maximum File Size
                </Label>
                <div className="flex gap-2">
                  <div className="flex-1">
                    <Input
                      id="fileSizeValue"
                      type="number"
                      min={1}
                      step={watchedUnit === 'GB' ? 0.1 : 1}
                      {...register('fileSizeValue', { valueAsNumber: true })}
                      className={errors.fileSizeValue ? 'border-red-500' : ''}
                    />
                  </div>
                  <Controller
                    name="fileSizeUnit"
                    control={control}
                    render={({ field }) => (
                      <Select
                        value={field.value}
                        onValueChange={(value: SizeUnit) =>
                          field.onChange(value)
                        }
                      >
                        <SelectTrigger className="w-24">
                          <SelectValue placeholder="Unit" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="MB">MB</SelectItem>
                          <SelectItem value="GB">GB</SelectItem>
                        </SelectContent>
                      </Select>
                    )}
                  />
                </div>
                {errors.fileSizeValue && (
                  <p className="text-sm text-red-500">
                    {errors.fileSizeValue.message}
                  </p>
                )}
                <p className="text-sm text-slate-500">
                  Maximum allowed file size for PDF uploads
                </p>
              </div>

              {/* Max Pages Per Document */}
              <div className="space-y-2">
                <Label
                  htmlFor="maxPagesPerDocument"
                  className="text-base font-semibold"
                >
                  Maximum Pages Per Document
                </Label>
                <Input
                  id="maxPagesPerDocument"
                  type="number"
                  min={1}
                  max={10000}
                  {...register('maxPagesPerDocument', { valueAsNumber: true })}
                  className={errors.maxPagesPerDocument ? 'border-red-500' : ''}
                />
                {errors.maxPagesPerDocument && (
                  <p className="text-sm text-red-500">
                    {errors.maxPagesPerDocument.message}
                  </p>
                )}
                <p className="text-sm text-slate-500">
                  Maximum number of pages allowed per PDF document (1-10,000)
                </p>
              </div>

              {/* Max Documents Per Game */}
              <div className="space-y-2">
                <Label
                  htmlFor="maxDocumentsPerGame"
                  className="text-base font-semibold"
                >
                  Maximum Documents Per Game
                </Label>
                <Input
                  id="maxDocumentsPerGame"
                  type="number"
                  min={1}
                  max={1000}
                  {...register('maxDocumentsPerGame', { valueAsNumber: true })}
                  className={errors.maxDocumentsPerGame ? 'border-red-500' : ''}
                />
                {errors.maxDocumentsPerGame && (
                  <p className="text-sm text-red-500">
                    {errors.maxDocumentsPerGame.message}
                  </p>
                )}
                <p className="text-sm text-slate-500">
                  Maximum number of PDF documents allowed per game (1-1,000)
                </p>
              </div>

              {/* Allowed MIME Types Multi-Select */}
              <div className="space-y-2">
                <Label className="text-base font-semibold">
                  Allowed MIME Types
                </Label>
                <div className="border rounded-md p-4 space-y-3 bg-slate-50 dark:bg-slate-800">
                  {AVAILABLE_MIME_TYPES.map((mimeType) => (
                    <div
                      key={mimeType.value}
                      className="flex items-center space-x-3"
                    >
                      <Checkbox
                        id={`mime-${mimeType.value}`}
                        checked={watchedMimeTypes?.includes(mimeType.value)}
                        onCheckedChange={(checked) =>
                          handleMimeTypeToggle(mimeType.value, checked === true)
                        }
                      />
                      <Label
                        htmlFor={`mime-${mimeType.value}`}
                        className="text-sm font-normal cursor-pointer"
                      >
                        {mimeType.label}
                      </Label>
                    </div>
                  ))}
                </div>
                {errors.allowedMimeTypes && (
                  <p className="text-sm text-red-500">
                    {errors.allowedMimeTypes.message}
                  </p>
                )}
                <p className="text-sm text-slate-500">
                  Select which MIME types are accepted for PDF uploads
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
              <strong>Maximum File Size:</strong> The largest PDF file that can
              be uploaded. Consider storage and processing capacity.
            </p>
            <p>
              <strong>Maximum Pages:</strong> Limits the number of pages per
              document to manage processing time and resource usage.
            </p>
            <p>
              <strong>Documents Per Game:</strong> Restricts how many rulebooks
              or manuals can be attached to each game.
            </p>
            <p>
              <strong>MIME Types:</strong> Controls which PDF format variations
              are accepted during upload validation.
            </p>
            <p className="pt-2 border-t text-xs">
              Note: These limits apply to all users uploading PDF documents.
              Configuration changes are cached for 5 minutes.
            </p>
          </CardContent>
        </Card>
      </div>
    </AdminAuthGuard>
  );
}

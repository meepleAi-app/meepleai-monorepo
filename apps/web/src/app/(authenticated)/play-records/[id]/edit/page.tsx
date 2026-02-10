/**
 * Edit Play Record Page
 *
 * Edit session details (creator only).
 * Issue #3892: Play Records Frontend UI
 */

'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { ArrowLeft, Save } from 'lucide-react';
import { useParams, useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';

import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/forms/form';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Textarea } from '@/components/ui/primitives/textarea';
import { UpdatePlayRecordRequestSchema, type UpdatePlayRecordRequest } from '@/lib/api/schemas/play-records.schemas';
import { usePlayRecord, useUpdateRecord } from '@/lib/hooks/use-play-records';

export default function EditPlayRecordPage() {
  const params = useParams();
  const router = useRouter();

  const recordId = typeof params?.id === 'string' ? params.id : '';

  const { data: record, isLoading, error } = usePlayRecord(recordId);
  const updateRecord = useUpdateRecord(recordId);

  const form = useForm<UpdatePlayRecordRequest>({
    resolver: zodResolver(UpdatePlayRecordRequestSchema),
    values: record
      ? {
          gameName: record.gameName,
          sessionDate: record.sessionDate,
          notes: record.notes || undefined,
          location: record.location || undefined,
        }
      : undefined,
  });

  const handleSubmit = form.handleSubmit(async (data) => {
    try {
      await updateRecord.mutateAsync(data);
      toast.success('Session Updated', {
        description: 'Your changes have been saved',
      });
      router.push(`/play-records/${recordId}`);
    } catch (error) {
      toast.error('Error', {
        description: error instanceof Error ? error.message : 'Failed to update session',
      });
    }
  });

  const handleCancel = () => {
    router.push(`/play-records/${recordId}`);
  };

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

  if (error || !record) {
    return (
      <div className="container mx-auto p-6">
        <Alert variant="destructive">
          <AlertDescription>
            {error instanceof Error ? error.message : 'Play record not found'}
          </AlertDescription>
        </Alert>
        <Button variant="outline" className="mt-4" onClick={() => router.push('/play-records')}>
          <ArrowLeft className="w-4 h-4 mr-2" />
          Back to History
        </Button>
      </div>
    );
  }

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
          Back to Details
        </Button>
      </div>
    );
  }

  return (
    <div className="container mx-auto p-6 space-y-6 max-w-2xl">
      {/* Header */}
      <div className="flex items-center gap-4">
        <Button
          variant="ghost"
          size="icon"
          onClick={handleCancel}
          aria-label="Back to details"
        >
          <ArrowLeft className="w-5 h-5" />
        </Button>
        <div>
          <h1 className="text-3xl font-bold">Edit Session</h1>
          <p className="text-muted-foreground mt-1">{record.gameName}</p>
        </div>
      </div>

      {/* Edit Form */}
      <Form {...form}>
        <form onSubmit={handleSubmit} className="space-y-6">
          <FormField
            control={form.control}
            name="gameName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Game Name</FormLabel>
                <FormControl>
                  <Input {...field} value={field.value || ''} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="sessionDate"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Session Date</FormLabel>
                <FormControl>
                  <Input
                    type="datetime-local"
                    value={field.value ? new Date(field.value).toISOString().slice(0, 16) : ''}
                    onChange={(e) => field.onChange(new Date(e.target.value).toISOString())}
                    max={new Date().toISOString().slice(0, 16)}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="location"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Location (Optional)</FormLabel>
                <FormControl>
                  <Input placeholder="Where did you play?" {...field} value={field.value || ''} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="notes"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Notes (Optional)</FormLabel>
                <FormControl>
                  <Textarea
                    placeholder="Any memorable moments or notes..."
                    rows={4}
                    {...field}
                    value={field.value || ''}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <div className="flex justify-end gap-2 pt-4 border-t">
            <Button type="button" variant="outline" onClick={handleCancel}>
              Cancel
            </Button>
            <Button type="submit" disabled={updateRecord.isPending}>
              <Save className="w-4 h-4 mr-2" />
              {updateRecord.isPending ? 'Saving...' : 'Save Changes'}
            </Button>
          </div>
        </form>
      </Form>
    </div>
  );
}

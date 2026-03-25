'use client';

import React from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Check, X, Clock, Layers } from 'lucide-react';
import { toast } from 'sonner';

import { EmptyFeatureState } from '@/components/admin/EmptyFeatureState';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api } from '@/lib/api';
import { isNotFoundError } from '@/lib/api/core/errors';
import type { GameToolkitTemplateDto } from '@/lib/api/schemas/toolkit.schemas';

function ReviewCard({
  template,
  onApprove,
  onReject,
  isActing,
}: {
  template: GameToolkitTemplateDto;
  onApprove: (id: string, notes?: string) => void;
  onReject: (id: string, notes: string) => void;
  isActing: boolean;
}) {
  const [notes, setNotes] = React.useState('');

  const toolCount =
    template.diceTools.length +
    template.cardTools.length +
    template.timerTools.length +
    template.counterTools.length;

  return (
    <Card data-testid="review-card">
      <CardHeader>
        <div className="flex items-start justify-between">
          <div className="flex items-center gap-2">
            <Layers className="h-5 w-5 text-muted-foreground" />
            <CardTitle className="text-base">{template.name}</CardTitle>
          </div>
          <Badge variant="outline" className="text-xs">
            <Clock className="mr-1 h-3 w-3" />
            Pending Review
          </Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {template.stateTemplate?.description && (
          <p className="text-sm text-muted-foreground">{template.stateTemplate.description}</p>
        )}

        <div className="flex flex-wrap gap-1.5">
          {template.diceTools.length > 0 && (
            <Badge variant="secondary" className="text-xs">
              {template.diceTools.length} dice
            </Badge>
          )}
          {template.cardTools.length > 0 && (
            <Badge variant="secondary" className="text-xs">
              {template.cardTools.length} cards
            </Badge>
          )}
          {template.timerTools.length > 0 && (
            <Badge variant="secondary" className="text-xs">
              {template.timerTools.length} timers
            </Badge>
          )}
          {template.counterTools.length > 0 && (
            <Badge variant="secondary" className="text-xs">
              {template.counterTools.length} counters
            </Badge>
          )}
          {toolCount === 0 && (
            <Badge variant="secondary" className="text-xs text-muted-foreground">
              No tools configured
            </Badge>
          )}
        </div>

        <div className="space-y-2">
          <Textarea
            placeholder="Review notes (required for rejection)"
            value={notes}
            onChange={e => setNotes(e.target.value)}
            rows={2}
          />
          <div className="flex gap-2">
            <Button
              size="sm"
              variant="default"
              className="flex-1"
              disabled={isActing}
              onClick={() => onApprove(template.id, notes || undefined)}
            >
              <Check className="mr-1.5 h-3.5 w-3.5" />
              Approve
            </Button>
            <Button
              size="sm"
              variant="destructive"
              className="flex-1"
              disabled={isActing || notes.trim().length === 0}
              onClick={() => onReject(template.id, notes)}
            >
              <X className="mr-1.5 h-3.5 w-3.5" />
              Reject
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

export default function AdminTemplateReviewPage() {
  const queryClient = useQueryClient();

  const {
    data: templates,
    isLoading,
    error,
  } = useQuery({
    queryKey: ['pending-review-templates'],
    queryFn: () => api.gameToolkit.getPendingReviewTemplates(),
    retry: (failureCount, err) => {
      if (isNotFoundError(err)) return false;
      return failureCount < 3;
    },
  });

  const approveMutation = useMutation({
    mutationFn: ({ id, notes }: { id: string; notes?: string }) =>
      api.gameToolkit.approveTemplate(id, notes),
    onSuccess: () => {
      toast.success('Template approved');
      queryClient.invalidateQueries({ queryKey: ['pending-review-templates'] });
    },
    onError: () => toast.error('Failed to approve template'),
  });

  const rejectMutation = useMutation({
    mutationFn: ({ id, notes }: { id: string; notes: string }) =>
      api.gameToolkit.rejectTemplate(id, notes),
    onSuccess: () => {
      toast.success('Template rejected');
      queryClient.invalidateQueries({ queryKey: ['pending-review-templates'] });
    },
    onError: () => toast.error('Failed to reject template'),
  });

  const isActing = approveMutation.isPending || rejectMutation.isPending;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Template Review Queue
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Review and approve toolkit templates submitted by users
        </p>
      </div>

      {isNotFoundError(error) && (
        <EmptyFeatureState
          title="Funzionalità non disponibile"
          description="Endpoint template review non ancora implementato nel backend."
        />
      )}

      {isLoading && (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          {[1, 2, 3].map(i => (
            <Skeleton key={i} className="h-64 w-full" />
          ))}
        </div>
      )}

      {!isLoading && templates?.length === 0 && (
        <Card>
          <CardContent className="py-12 text-center">
            <Check className="mx-auto mb-3 h-8 w-8 text-green-500" />
            <p className="text-muted-foreground">No templates pending review</p>
          </CardContent>
        </Card>
      )}

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2" data-testid="review-grid">
        {templates?.map(t => (
          <ReviewCard
            key={t.id}
            template={t}
            isActing={isActing}
            onApprove={(id, notes) => approveMutation.mutate({ id, notes })}
            onReject={(id, notes) => rejectMutation.mutate({ id, notes })}
          />
        ))}
      </div>
    </div>
  );
}

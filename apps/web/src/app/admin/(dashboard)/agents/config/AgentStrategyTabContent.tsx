'use client';

/**
 * #4169 (ADR-095 D8, fetta 6d): the Strategy tab edits only what the runtime reads.
 *
 * Removed: the Retrieval, Generation and Confidence sections, whose values were never sent (the save
 * button sent only the tier matrix, then said «Changes saved»), the overview cards with hard-coded
 * numbers («HybridRAG», «Claude 3.5», «743ms»), and the «Edit» button of the model mappings, which had
 * no handler. Retrieval and generation settings belong to the agent profile (ADR-095 D1), edited on
 * the Agent page.
 */
import { useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link as LinkIcon, Lock } from 'lucide-react';

import { EmptyFeatureState } from '@/components/admin/EmptyFeatureState';
import { StrategyBadge } from '@/components/admin/rag/StrategyBadge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { useToast } from '@/hooks/useToast';
import { createAdminClient, type StrategyModelMappingDto } from '@/lib/api/clients/adminClient';
import { isNotFoundError } from '@/lib/api/core/errors';
import { HttpClient } from '@/lib/api/core/httpClient';

// ─── Module-level client ──────────────────────────────────────────────────────

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

// ─── Component ────────────────────────────────────────────────────────────────

export function AgentStrategyTabContent() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [tierAccessChanges, setTierAccessChanges] = useState<Map<string, boolean>>(new Map());

  const {
    data: matrix,
    isLoading: matrixLoading,
    error: matrixError,
    refetch: refetchMatrix,
  } = useQuery({
    queryKey: ['tierStrategyMatrix'],
    queryFn: () => adminClient.getTierStrategyMatrix(),
    retry: (failureCount, err) => {
      if (isNotFoundError(err)) return false;
      return failureCount < 3;
    },
  });

  const { data: modelMappings, isLoading: mappingsLoading } = useQuery({
    queryKey: ['strategyModelMappings'],
    queryFn: () => adminClient.getStrategyModelMappings(),
    retry: (failureCount, err) => {
      if (isNotFoundError(err)) return false;
      return failureCount < 3;
    },
  });

  const updateAccessMutation = useMutation({
    mutationFn: (payload: { tier: string; strategy: string; isEnabled: boolean }) =>
      adminClient.updateTierStrategyAccess(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['tierStrategyMatrix'] });
    },
  });

  const handleTierAccessChange = (tier: string, strategy: string, isEnabled: boolean) => {
    const key = `${tier}-${strategy}`;
    setTierAccessChanges(prev => new Map(prev).set(key, isEnabled));
  };

  const handleSave = async () => {
    try {
      for (const [key, isEnabled] of tierAccessChanges.entries()) {
        const [tier, strategy] = key.split('-');
        await updateAccessMutation.mutateAsync({ tier, strategy, isEnabled });
      }
      toast({
        title: 'Tier access saved',
        description: `${tierAccessChanges.size} change(s) applied`,
      });
      setTierAccessChanges(new Map());
    } catch (error) {
      toast({
        title: 'Save failed',
        description: error instanceof Error ? error.message : 'Unknown error',
        variant: 'destructive',
      });
    }
  };

  const handleDiscard = () => {
    setTierAccessChanges(new Map());
    void refetchMatrix();
  };

  return (
    <div className="space-y-6">
      {isNotFoundError(matrixError) && (
        <EmptyFeatureState
          title="Funzionalità non disponibile"
          description="Endpoint strategy configuration non ancora implementato nel backend."
        />
      )}

      <p className="rounded-lg border border-border bg-muted/40 p-3 text-sm text-muted-foreground">
        Retrieval and generation settings (top K, minimum score, reranker, temperature, max tokens)
        are not configured here: they belong to the agent profile and move to the Agent page
        (ADR-095). This tab edits which strategies each tier may use.
      </p>

      {/* Tier Access Matrix */}
      <Card className="bg-card/90 backdrop-blur-xl border-border/60">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-sm font-semibold">
            <Lock className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
            Tier Access Matrix
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <p className="text-xs text-muted-foreground">
            Answers in chat run as tier <strong>User</strong> with strategy{' '}
            <strong>Balanced</strong>: today that is the only cell they read, and turning it off
            stops every answer.
          </p>
          {matrixLoading ? (
            <Skeleton className="h-64 w-full" />
          ) : matrix ? (
            <div className="overflow-x-auto">
              <table className="w-full border-collapse">
                <thead>
                  <tr>
                    <th className="text-left p-3 text-xs font-bold text-muted-foreground uppercase tracking-wider border-b-2">
                      Role
                    </th>
                    {matrix.strategies.map((strategy: { name: string; displayName: string }) => (
                      <th
                        key={strategy.name}
                        className="text-center p-3 text-xs font-bold text-muted-foreground uppercase tracking-wider border-b-2"
                      >
                        {strategy.displayName}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {matrix.tiers.map((tier: string) => (
                    <tr key={tier}>
                      <td className="p-3 font-bold text-sm border-b">{tier}</td>
                      {matrix.strategies.map((strategy: { name: string; displayName: string }) => {
                        const access = matrix.accessMatrix.find(
                          (a: { tier: string; strategy: string; isEnabled: boolean }) =>
                            a.tier === tier && a.strategy === strategy.name
                        );
                        const key = `${tier}-${strategy.name}`;
                        const isEnabled = tierAccessChanges.has(key)
                          ? (tierAccessChanges.get(key) ?? false)
                          : (access?.isEnabled ?? false);
                        return (
                          <td key={strategy.name} className="text-center p-3 border-b">
                            <Checkbox
                              checked={isEnabled}
                              aria-label={`${tier} can use ${strategy.displayName}`}
                              onCheckedChange={checked =>
                                handleTierAccessChange(tier, strategy.name, checked as boolean)
                              }
                              className="mx-auto"
                            />
                          </td>
                        );
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="text-center text-muted-foreground py-8">No data available</div>
          )}
          {tierAccessChanges.size > 0 && (
            <div className="flex items-center justify-end gap-2">
              <span className="text-xs text-muted-foreground">
                {tierAccessChanges.size} unsaved change(s)
              </span>
              <Button variant="outline" size="sm" onClick={handleDiscard}>
                Discard
              </Button>
              <Button size="sm" onClick={() => void handleSave()}>
                Save tier access
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Strategy-Model Mappings (read-only) */}
      <Card className="bg-card/90 backdrop-blur-xl border-border/60">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-sm font-semibold">
            <LinkIcon className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
            Strategy-Model Mappings
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <p className="text-xs text-muted-foreground">
            Read-only. The model of each strategy comes from these mappings, and from the defaults
            in code while none is saved. Choosing the answer model moves to the agent profile.
          </p>
          {mappingsLoading ? (
            <Skeleton className="h-64 w-full" />
          ) : modelMappings && modelMappings.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full border-collapse">
                <thead>
                  <tr>
                    <th className="text-left p-3 text-xs font-bold text-muted-foreground uppercase tracking-wider border-b-2">
                      Strategy
                    </th>
                    <th className="text-left p-3 text-xs font-bold text-muted-foreground uppercase tracking-wider border-b-2">
                      Provider
                    </th>
                    <th className="text-left p-3 text-xs font-bold text-muted-foreground uppercase tracking-wider border-b-2">
                      Primary Model
                    </th>
                    <th className="text-left p-3 text-xs font-bold text-muted-foreground uppercase tracking-wider border-b-2">
                      Fallback Models
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {modelMappings.map((mapping: StrategyModelMappingDto) => (
                    <tr key={mapping.strategy}>
                      <td className="p-3 border-b">
                        <StrategyBadge strategy={mapping.strategy} />
                      </td>
                      <td className="p-3 text-sm border-b">{mapping.provider}</td>
                      <td className="p-3 text-sm font-mono border-b">{mapping.primaryModel}</td>
                      <td className="p-3 text-sm font-mono border-b">
                        {mapping.fallbackModels.join(', ')}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="text-center text-muted-foreground py-8">No mappings found</div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

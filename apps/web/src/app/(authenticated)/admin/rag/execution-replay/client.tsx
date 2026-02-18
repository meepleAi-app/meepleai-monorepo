'use client';

/**
 * RAG Execution Replay Client Component
 * Issue #4459
 *
 * Provides:
 * - Replay form with execution ID + optional config overrides
 * - Real-time SSE streaming of replay events
 * - Side-by-side comparison of two executions
 * - Diff highlighting for metrics and document selection
 */

import { useState, useCallback } from 'react';

import type { ReplayExecutionRequest } from '@/lib/api/schemas/rag-execution.schemas';
import type { BlockComparison, MetricsDelta, DocumentDiff } from '@/lib/api/schemas/rag-execution.schemas';
import { useRagExecutionComparison } from '@/lib/hooks/useRagExecutionComparison';
import { useRagExecutionReplay } from '@/lib/hooks/useRagExecutionReplay';

// =============================================================================
// Main Component
// =============================================================================

export function ExecutionReplayClient() {
  const [activeTab, setActiveTab] = useState<'replay' | 'compare'>('replay');

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">RAG Execution Replay</h1>
        <p className="text-muted-foreground mt-1">
          Replay past RAG executions with optional config overrides, or compare two executions side-by-side.
        </p>
      </div>

      {/* Tab Navigation */}
      <div className="flex gap-2 border-b pb-2">
        <button
          onClick={() => setActiveTab('replay')}
          className={`px-4 py-2 rounded-t-md text-sm font-medium transition-colors ${
            activeTab === 'replay'
              ? 'bg-primary text-primary-foreground'
              : 'text-muted-foreground hover:text-foreground hover:bg-muted'
          }`}
        >
          Replay Execution
        </button>
        <button
          onClick={() => setActiveTab('compare')}
          className={`px-4 py-2 rounded-t-md text-sm font-medium transition-colors ${
            activeTab === 'compare'
              ? 'bg-primary text-primary-foreground'
              : 'text-muted-foreground hover:text-foreground hover:bg-muted'
          }`}
        >
          Compare Executions
        </button>
      </div>

      {activeTab === 'replay' ? <ReplayPanel /> : <ComparePanel />}
    </div>
  );
}

// =============================================================================
// Replay Panel
// =============================================================================

function ReplayPanel() {
  const [executionId, setExecutionId] = useState('');
  const [showOverrides, setShowOverrides] = useState(false);
  const [overrides, setOverrides] = useState<ReplayExecutionRequest>({});

  const replay = useRagExecutionReplay();

  const handleReplay = useCallback(() => {
    if (!executionId.trim()) return;
    const cleanOverrides: ReplayExecutionRequest = {};
    if (overrides.strategy) cleanOverrides.strategy = overrides.strategy;
    if (overrides.topK) cleanOverrides.topK = overrides.topK;
    if (overrides.model) cleanOverrides.model = overrides.model;
    if (overrides.temperature !== undefined) cleanOverrides.temperature = overrides.temperature;

    replay.startReplay(executionId.trim(), Object.keys(cleanOverrides).length > 0 ? cleanOverrides : undefined);
  }, [executionId, overrides, replay]);

  return (
    <div className="space-y-6">
      {/* Input Form */}
      <div className="rounded-lg border p-4 space-y-4">
        <div>
          <label className="block text-sm font-medium mb-1">Execution ID</label>
          <input
            type="text"
            value={executionId}
            onChange={(e) => setExecutionId(e.target.value)}
            placeholder="Enter execution UUID..."
            className="w-full px-3 py-2 border rounded-md bg-background text-sm"
            disabled={replay.isRunning}
          />
        </div>

        <div>
          <button
            onClick={() => setShowOverrides(!showOverrides)}
            className="text-sm text-primary hover:underline"
          >
            {showOverrides ? 'Hide' : 'Show'} Config Overrides
          </button>
        </div>

        {showOverrides && (
          <div className="grid grid-cols-2 gap-4 p-3 bg-muted/50 rounded-md">
            <div>
              <label className="block text-xs font-medium mb-1">Strategy</label>
              <input
                type="text"
                value={overrides.strategy || ''}
                onChange={(e) => setOverrides((prev) => ({ ...prev, strategy: e.target.value || undefined }))}
                placeholder="e.g., hybrid-search"
                className="w-full px-2 py-1 border rounded text-sm bg-background"
              />
            </div>
            <div>
              <label className="block text-xs font-medium mb-1">Top K</label>
              <input
                type="number"
                value={overrides.topK || ''}
                onChange={(e) => setOverrides((prev) => ({ ...prev, topK: e.target.value ? parseInt(e.target.value) : undefined }))}
                placeholder="e.g., 5"
                className="w-full px-2 py-1 border rounded text-sm bg-background"
              />
            </div>
            <div>
              <label className="block text-xs font-medium mb-1">Model</label>
              <input
                type="text"
                value={overrides.model || ''}
                onChange={(e) => setOverrides((prev) => ({ ...prev, model: e.target.value || undefined }))}
                placeholder="e.g., gpt-4o"
                className="w-full px-2 py-1 border rounded text-sm bg-background"
              />
            </div>
            <div>
              <label className="block text-xs font-medium mb-1">Temperature</label>
              <input
                type="number"
                step="0.1"
                min="0"
                max="2"
                value={overrides.temperature ?? ''}
                onChange={(e) => setOverrides((prev) => ({ ...prev, temperature: e.target.value ? parseFloat(e.target.value) : undefined }))}
                placeholder="e.g., 0.7"
                className="w-full px-2 py-1 border rounded text-sm bg-background"
              />
            </div>
          </div>
        )}

        <div className="flex gap-2">
          <button
            onClick={handleReplay}
            disabled={!executionId.trim() || replay.isRunning}
            className="px-4 py-2 bg-primary text-primary-foreground rounded-md text-sm font-medium disabled:opacity-50"
          >
            {replay.isRunning ? 'Replaying...' : 'Replay Execution'}
          </button>
          {replay.isRunning && (
            <button
              onClick={replay.cancelReplay}
              className="px-4 py-2 bg-destructive text-destructive-foreground rounded-md text-sm"
            >
              Cancel
            </button>
          )}
          {(replay.events.length > 0 || replay.error) && !replay.isRunning && (
            <button
              onClick={replay.reset}
              className="px-4 py-2 border rounded-md text-sm"
            >
              Reset
            </button>
          )}
        </div>
      </div>

      {/* Progress */}
      {replay.isRunning && replay.progress.totalBlocks > 0 && (
        <div className="rounded-lg border p-4 space-y-2">
          <div className="flex justify-between text-sm">
            <span>
              Block {replay.progress.completedBlocks}/{replay.progress.totalBlocks}
              {replay.progress.currentBlock && (
                <span className="text-muted-foreground ml-2">
                  ({replay.progress.currentBlock.name})
                </span>
              )}
            </span>
            <span>{replay.progress.percentage}%</span>
          </div>
          <div className="w-full bg-muted rounded-full h-2">
            <div
              className="bg-primary h-2 rounded-full transition-all duration-300"
              style={{ width: `${replay.progress.percentage}%` }}
            />
          </div>
        </div>
      )}

      {/* Error */}
      {replay.error && (
        <div className="rounded-lg border border-destructive bg-destructive/10 p-4 text-destructive text-sm">
          {replay.error.message}
        </div>
      )}

      {/* Results */}
      {!replay.isRunning && replay.events.length > 0 && (
        <div className="space-y-4">
          {/* Metrics Summary */}
          <div className="grid grid-cols-5 gap-3">
            <MetricCard label="Duration" value={`${replay.metrics.totalDurationMs}ms`} />
            <MetricCard label="Tokens" value={replay.metrics.totalTokensUsed.toLocaleString()} />
            <MetricCard label="Cost" value={`$${replay.metrics.totalCost.toFixed(4)}`} />
            <MetricCard label="Blocks OK" value={String(replay.metrics.blocksExecuted)} />
            <MetricCard label="Failed" value={String(replay.metrics.blocksFailed)} variant={replay.metrics.blocksFailed > 0 ? 'danger' : 'default'} />
          </div>

          {/* Block Results */}
          <div className="rounded-lg border">
            <div className="p-3 border-b bg-muted/50">
              <h3 className="text-sm font-medium">Block Results</h3>
            </div>
            <div className="divide-y">
              {Array.from(replay.blockResults.values()).map((block) => (
                <div key={block.blockId} className="p-3 flex items-center gap-3 text-sm">
                  <span className={`w-2 h-2 rounded-full ${block.success ? 'bg-green-500' : 'bg-red-500'}`} />
                  <span className="font-medium w-48">{block.blockName}</span>
                  <span className="text-muted-foreground">{block.blockType}</span>
                  <span className="ml-auto text-muted-foreground">{block.durationMs}ms</span>
                  <span className="text-muted-foreground">{block.tokensUsed} tokens</span>
                </div>
              ))}
            </div>
          </div>

          {/* Final Response */}
          {replay.finalResponse && (
            <div className="rounded-lg border p-4">
              <h3 className="text-sm font-medium mb-2">Final Response</h3>
              <p className="text-sm text-muted-foreground whitespace-pre-wrap">{replay.finalResponse}</p>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

// =============================================================================
// Compare Panel
// =============================================================================

function ComparePanel() {
  const [executionId1, setExecutionId1] = useState('');
  const [executionId2, setExecutionId2] = useState('');

  const comparison = useRagExecutionComparison();

  const handleCompare = useCallback(() => {
    if (!executionId1.trim() || !executionId2.trim()) return;
    comparison.mutate({ executionIds: [executionId1.trim(), executionId2.trim()] });
  }, [executionId1, executionId2, comparison]);

  return (
    <div className="space-y-6">
      {/* Input Form */}
      <div className="rounded-lg border p-4 space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium mb-1">Execution 1 (Original)</label>
            <input
              type="text"
              value={executionId1}
              onChange={(e) => setExecutionId1(e.target.value)}
              placeholder="Enter execution UUID..."
              className="w-full px-3 py-2 border rounded-md bg-background text-sm"
            />
          </div>
          <div>
            <label className="block text-sm font-medium mb-1">Execution 2 (Replay)</label>
            <input
              type="text"
              value={executionId2}
              onChange={(e) => setExecutionId2(e.target.value)}
              placeholder="Enter execution UUID..."
              className="w-full px-3 py-2 border rounded-md bg-background text-sm"
            />
          </div>
        </div>
        <button
          onClick={handleCompare}
          disabled={!executionId1.trim() || !executionId2.trim() || comparison.isPending}
          className="px-4 py-2 bg-primary text-primary-foreground rounded-md text-sm font-medium disabled:opacity-50"
        >
          {comparison.isPending ? 'Comparing...' : 'Compare'}
        </button>
      </div>

      {/* Error */}
      {comparison.error && (
        <div className="rounded-lg border border-destructive bg-destructive/10 p-4 text-destructive text-sm">
          {comparison.error.message}
        </div>
      )}

      {/* Comparison Results */}
      {comparison.data && (
        <div className="space-y-6">
          {/* Overall Assessment */}
          <OverallAssessment delta={comparison.data.metricsDelta} />

          {/* Side-by-side Metrics */}
          <div className="grid grid-cols-2 gap-4">
            <ExecutionSummaryCard
              title="Execution 1"
              data={comparison.data.execution1}
            />
            <ExecutionSummaryCard
              title="Execution 2"
              data={comparison.data.execution2}
            />
          </div>

          {/* Block Comparisons */}
          <BlockComparisonTable blocks={comparison.data.blockComparisons} />

          {/* Document Diffs */}
          {comparison.data.documentDiffs.length > 0 && (
            <DocumentDiffSection diffs={comparison.data.documentDiffs} />
          )}
        </div>
      )}
    </div>
  );
}

// =============================================================================
// Sub-components
// =============================================================================

function MetricCard({ label, value, variant = 'default' }: { label: string; value: string; variant?: 'default' | 'danger' }) {
  return (
    <div className="rounded-lg border p-3 text-center">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className={`text-lg font-semibold ${variant === 'danger' ? 'text-destructive' : ''}`}>{value}</div>
    </div>
  );
}

function OverallAssessment({ delta }: { delta: MetricsDelta }) {
  const colors = {
    improved: 'bg-green-100 text-green-800 border-green-200',
    degraded: 'bg-red-100 text-red-800 border-red-200',
    unchanged: 'bg-gray-100 text-gray-800 border-gray-200',
  };

  return (
    <div className={`rounded-lg border p-4 ${colors[delta.overallAssessment]}`}>
      <div className="flex items-center justify-between">
        <span className="font-medium text-sm capitalize">{delta.overallAssessment}</span>
        <div className="flex gap-4 text-sm">
          <DeltaValue label="Duration" value={delta.durationDeltaMs} unit="ms" lowerIsBetter />
          <DeltaValue label="Tokens" value={delta.tokensDelta} lowerIsBetter />
          <DeltaValue label="Cost" value={Number(delta.costDelta.toFixed(6))} prefix="$" lowerIsBetter />
        </div>
      </div>
    </div>
  );
}

function DeltaValue({
  label,
  value,
  unit = '',
  prefix = '',
  lowerIsBetter = false,
}: {
  label: string;
  value: number;
  unit?: string;
  prefix?: string;
  lowerIsBetter?: boolean;
}) {
  const isPositive = value > 0;
  const isBetter = lowerIsBetter ? value < 0 : value > 0;
  const color = value === 0 ? '' : isBetter ? 'text-green-700' : 'text-red-700';

  return (
    <span className={color}>
      {label}: {isPositive ? '+' : ''}{prefix}{value}{unit}
    </span>
  );
}

function ExecutionSummaryCard({ title, data }: { title: string; data: { testQuery: string; success: boolean; totalDurationMs: number; totalTokensUsed: number; totalCost: number; blocksExecuted: number; blocksFailed: number; executedAt: string; configOverridesJson?: string | null } }) {
  return (
    <div className="rounded-lg border p-4">
      <h3 className="text-sm font-medium mb-3">{title}</h3>
      <div className="space-y-2 text-sm">
        <div className="flex justify-between">
          <span className="text-muted-foreground">Status</span>
          <span className={data.success ? 'text-green-600' : 'text-red-600'}>{data.success ? 'Success' : 'Failed'}</span>
        </div>
        <div className="flex justify-between">
          <span className="text-muted-foreground">Duration</span>
          <span>{data.totalDurationMs}ms</span>
        </div>
        <div className="flex justify-between">
          <span className="text-muted-foreground">Tokens</span>
          <span>{data.totalTokensUsed.toLocaleString()}</span>
        </div>
        <div className="flex justify-between">
          <span className="text-muted-foreground">Cost</span>
          <span>${data.totalCost.toFixed(4)}</span>
        </div>
        <div className="flex justify-between">
          <span className="text-muted-foreground">Blocks</span>
          <span>{data.blocksExecuted} ok / {data.blocksFailed} failed</span>
        </div>
        <div className="flex justify-between">
          <span className="text-muted-foreground">Query</span>
          <span className="text-right truncate max-w-48">{data.testQuery}</span>
        </div>
        {data.configOverridesJson && (
          <div className="flex justify-between">
            <span className="text-muted-foreground">Overrides</span>
            <span className="text-xs font-mono bg-muted px-1 rounded">{data.configOverridesJson}</span>
          </div>
        )}
      </div>
    </div>
  );
}

function BlockComparisonTable({ blocks }: { blocks: BlockComparison[] }) {
  const statusColors: Record<string, string> = {
    improved: 'bg-green-100 text-green-800',
    degraded: 'bg-red-100 text-red-800',
    unchanged: 'bg-gray-100 text-gray-800',
    added: 'bg-blue-100 text-blue-800',
    removed: 'bg-yellow-100 text-yellow-800',
  };

  return (
    <div className="rounded-lg border overflow-hidden">
      <div className="p-3 border-b bg-muted/50">
        <h3 className="text-sm font-medium">Block Comparison</h3>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="bg-muted/30">
            <tr>
              <th className="px-3 py-2 text-left font-medium">Block</th>
              <th className="px-3 py-2 text-left font-medium">Type</th>
              <th className="px-3 py-2 text-center font-medium">Exec 1</th>
              <th className="px-3 py-2 text-center font-medium">Exec 2</th>
              <th className="px-3 py-2 text-center font-medium">Status</th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {blocks.map((block) => (
              <tr key={block.blockId}>
                <td className="px-3 py-2 font-medium">{block.blockName}</td>
                <td className="px-3 py-2 text-muted-foreground">{block.blockType}</td>
                <td className="px-3 py-2 text-center">
                  {block.execution1 ? (
                    <span>{block.execution1.durationMs}ms / {block.execution1.tokensUsed}t</span>
                  ) : (
                    <span className="text-muted-foreground">-</span>
                  )}
                </td>
                <td className="px-3 py-2 text-center">
                  {block.execution2 ? (
                    <span>{block.execution2.durationMs}ms / {block.execution2.tokensUsed}t</span>
                  ) : (
                    <span className="text-muted-foreground">-</span>
                  )}
                </td>
                <td className="px-3 py-2 text-center">
                  <span className={`px-2 py-0.5 rounded-full text-xs ${statusColors[block.status] || ''}`}>
                    {block.status}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function DocumentDiffSection({ diffs }: { diffs: DocumentDiff[] }) {
  return (
    <div className="rounded-lg border">
      <div className="p-3 border-b bg-muted/50">
        <h3 className="text-sm font-medium">Document Selection Diffs</h3>
      </div>
      <div className="divide-y">
        {diffs.map((diff) => (
          <div key={diff.blockId} className="p-3 space-y-2">
            <div className="text-sm font-medium">Block: {diff.blockId}</div>
            <div className="grid grid-cols-3 gap-2 text-xs">
              {diff.onlyInExecution1.length > 0 && (
                <div className="bg-red-50 p-2 rounded">
                  <div className="font-medium text-red-700 mb-1">Only in Exec 1</div>
                  {diff.onlyInExecution1.map((id) => (
                    <div key={id} className="text-red-600">{id}</div>
                  ))}
                </div>
              )}
              {diff.onlyInExecution2.length > 0 && (
                <div className="bg-green-50 p-2 rounded">
                  <div className="font-medium text-green-700 mb-1">Only in Exec 2</div>
                  {diff.onlyInExecution2.map((id) => (
                    <div key={id} className="text-green-600">{id}</div>
                  ))}
                </div>
              )}
              {diff.scoreChanges.length > 0 && (
                <div className="bg-yellow-50 p-2 rounded">
                  <div className="font-medium text-yellow-700 mb-1">Score Changes</div>
                  {diff.scoreChanges.map((sc) => (
                    <div key={sc.documentId} className={sc.delta > 0 ? 'text-green-600' : sc.delta < 0 ? 'text-red-600' : ''}>
                      {sc.documentId}: {sc.score1.toFixed(3)} &rarr; {sc.score2.toFixed(3)} ({sc.delta > 0 ? '+' : ''}{sc.delta.toFixed(3)})
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

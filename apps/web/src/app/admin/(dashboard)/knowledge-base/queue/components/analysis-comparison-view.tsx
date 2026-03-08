'use client';

import { useState } from 'react';

import {
  ArrowRightIcon,
  ChevronDownIcon,
  ChevronRightIcon,
  MinusIcon,
  PlusIcon,
  RefreshCwIcon,
} from 'lucide-react';

import { cn } from '@/lib/utils';

import { useAnalysisComparison } from '../lib/analysis-comparison-api';

import type { AnalysisComparisonDto, FaqModified, ListDiff } from '../lib/analysis-comparison-api';

function DiffBadge({ type }: { type: 'added' | 'removed' | 'modified' | 'unchanged' }) {
  const styles = {
    added: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/30 dark:text-emerald-300',
    removed: 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300',
    modified: 'bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-300',
    unchanged: 'bg-slate-100 text-slate-600 dark:bg-slate-800/30 dark:text-slate-400',
  };
  return (
    <span className={cn('px-1.5 py-0.5 rounded text-xs font-medium', styles[type])}>{type}</span>
  );
}

function ListDiffSection({ title, diff }: { title: string; diff: ListDiff<string> }) {
  const [expanded, setExpanded] = useState(true);
  const total = diff.added.length + diff.removed.length + diff.unchanged.length;

  if (total === 0) return null;

  return (
    <div className="border border-slate-200/50 dark:border-zinc-700/50 rounded-lg">
      <button
        type="button"
        onClick={() => setExpanded(!expanded)}
        className="w-full flex items-center justify-between px-4 py-2.5 text-left hover:bg-slate-50 dark:hover:bg-zinc-800/50 rounded-t-lg"
      >
        <div className="flex items-center gap-2">
          {expanded ? (
            <ChevronDownIcon className="h-4 w-4" />
          ) : (
            <ChevronRightIcon className="h-4 w-4" />
          )}
          <span className="font-quicksand font-semibold text-sm">{title}</span>
        </div>
        <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
          {diff.added.length > 0 && <span className="text-emerald-600">+{diff.added.length}</span>}
          {diff.removed.length > 0 && <span className="text-red-600">-{diff.removed.length}</span>}
          {diff.unchanged.length > 0 && (
            <span className="text-slate-500">{diff.unchanged.length} unchanged</span>
          )}
        </div>
      </button>
      {expanded && (
        <div className="px-4 pb-3 space-y-1">
          {diff.added.map((item, i) => (
            <div key={`added-${i}`} className="flex items-center gap-2 text-sm">
              <PlusIcon className="h-3.5 w-3.5 text-emerald-600 shrink-0" />
              <span className="text-emerald-700 dark:text-emerald-300">{item}</span>
              <DiffBadge type="added" />
            </div>
          ))}
          {diff.removed.map((item, i) => (
            <div key={`removed-${i}`} className="flex items-center gap-2 text-sm">
              <MinusIcon className="h-3.5 w-3.5 text-red-600 shrink-0" />
              <span className="text-red-700 dark:text-red-300 line-through">{item}</span>
              <DiffBadge type="removed" />
            </div>
          ))}
          {diff.unchanged.map((item, i) => (
            <div
              key={`unchanged-${i}`}
              className="flex items-center gap-2 text-sm text-muted-foreground"
            >
              <span className="w-3.5" />
              <span>{item}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function FaqDiffSection({ data }: { data: AnalysisComparisonDto }) {
  const [expanded, setExpanded] = useState(true);
  const { faqDiff } = data;
  const total =
    faqDiff.added.length +
    faqDiff.removed.length +
    faqDiff.modified.length +
    faqDiff.unchanged.length;

  if (total === 0) return null;

  return (
    <div className="border border-slate-200/50 dark:border-zinc-700/50 rounded-lg">
      <button
        type="button"
        onClick={() => setExpanded(!expanded)}
        className="w-full flex items-center justify-between px-4 py-2.5 text-left hover:bg-slate-50 dark:hover:bg-zinc-800/50 rounded-t-lg"
      >
        <div className="flex items-center gap-2">
          {expanded ? (
            <ChevronDownIcon className="h-4 w-4" />
          ) : (
            <ChevronRightIcon className="h-4 w-4" />
          )}
          <span className="font-quicksand font-semibold text-sm">FAQs</span>
        </div>
        <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
          {faqDiff.added.length > 0 && (
            <span className="text-emerald-600">+{faqDiff.added.length}</span>
          )}
          {faqDiff.removed.length > 0 && (
            <span className="text-red-600">-{faqDiff.removed.length}</span>
          )}
          {faqDiff.modified.length > 0 && (
            <span className="text-amber-600">~{faqDiff.modified.length}</span>
          )}
        </div>
      </button>
      {expanded && (
        <div className="px-4 pb-3 space-y-2">
          {faqDiff.added.map((faq, i) => (
            <div
              key={`faq-added-${i}`}
              className="p-2 rounded bg-emerald-50/50 dark:bg-emerald-900/10 border border-emerald-200/50 dark:border-emerald-800/30"
            >
              <div className="flex items-center gap-2 mb-1">
                <PlusIcon className="h-3.5 w-3.5 text-emerald-600" />
                <span className="text-sm font-medium text-emerald-800 dark:text-emerald-200">
                  {faq.question}
                </span>
                <DiffBadge type="added" />
              </div>
              <p className="text-xs text-emerald-700 dark:text-emerald-300 ml-5.5">{faq.answer}</p>
            </div>
          ))}
          {faqDiff.removed.map((faq, i) => (
            <div
              key={`faq-removed-${i}`}
              className="p-2 rounded bg-red-50/50 dark:bg-red-900/10 border border-red-200/50 dark:border-red-800/30"
            >
              <div className="flex items-center gap-2 mb-1">
                <MinusIcon className="h-3.5 w-3.5 text-red-600" />
                <span className="text-sm font-medium text-red-800 dark:text-red-200 line-through">
                  {faq.question}
                </span>
                <DiffBadge type="removed" />
              </div>
              <p className="text-xs text-red-700 dark:text-red-300 ml-5.5">{faq.answer}</p>
            </div>
          ))}
          {faqDiff.modified.map((faq: FaqModified, i: number) => (
            <div
              key={`faq-modified-${i}`}
              className="p-2 rounded bg-amber-50/50 dark:bg-amber-900/10 border border-amber-200/50 dark:border-amber-800/30"
            >
              <div className="flex items-center gap-2 mb-1">
                <RefreshCwIcon className="h-3.5 w-3.5 text-amber-600" />
                <span className="text-sm font-medium text-amber-800 dark:text-amber-200">
                  {faq.question}
                </span>
                <DiffBadge type="modified" />
              </div>
              <div className="ml-5.5 space-y-1">
                <div className="flex items-start gap-2 text-xs">
                  <span className="text-red-500 shrink-0 mt-0.5">-</span>
                  <span className="text-red-700 dark:text-red-300">{faq.leftAnswer}</span>
                </div>
                <div className="flex items-start gap-2 text-xs">
                  <span className="text-emerald-500 shrink-0 mt-0.5">+</span>
                  <span className="text-emerald-700 dark:text-emerald-300">{faq.rightAnswer}</span>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export function AnalysisComparisonView({
  leftId,
  rightId,
}: {
  leftId: string | null;
  rightId: string | null;
}) {
  const { data, isLoading, error } = useAnalysisComparison(leftId, rightId);

  if (!leftId || !rightId) {
    return (
      <div className="text-center text-muted-foreground py-12" data-testid="comparison-empty">
        <p className="text-sm">Select two analysis versions to compare</p>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="text-center py-12" data-testid="comparison-loading">
        <RefreshCwIcon className="h-5 w-5 animate-spin mx-auto text-muted-foreground" />
        <p className="text-sm text-muted-foreground mt-2">Loading comparison...</p>
      </div>
    );
  }

  if (error || !data) {
    return (
      <div className="text-center text-red-600 py-12" data-testid="comparison-error">
        <p className="text-sm">Failed to load comparison</p>
      </div>
    );
  }

  const scoreDelta = data.confidenceScoreDelta;
  const scoreColor =
    scoreDelta > 0 ? 'text-emerald-600' : scoreDelta < 0 ? 'text-red-600' : 'text-slate-500';

  return (
    <div className="space-y-4" data-testid="comparison-view">
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/50 dark:border-zinc-700/50">
        <div className="flex items-center gap-3">
          <div className="text-center">
            <p className="text-xs text-muted-foreground">Version</p>
            <p className="font-quicksand font-bold text-lg">{data.leftVersion}</p>
            <p className="text-xs text-muted-foreground">
              {new Date(data.leftAnalyzedAt).toLocaleDateString()}
            </p>
          </div>
          <ArrowRightIcon className="h-5 w-5 text-muted-foreground" />
          <div className="text-center">
            <p className="text-xs text-muted-foreground">Version</p>
            <p className="font-quicksand font-bold text-lg">{data.rightVersion}</p>
            <p className="text-xs text-muted-foreground">
              {new Date(data.rightAnalyzedAt).toLocaleDateString()}
            </p>
          </div>
        </div>
        <div className="text-right">
          <p className="text-xs text-muted-foreground">Confidence Delta</p>
          <p
            className={cn('font-quicksand font-bold text-lg', scoreColor)}
            data-testid="confidence-delta"
          >
            {scoreDelta > 0 ? '+' : ''}
            {(scoreDelta * 100).toFixed(1)}%
          </p>
        </div>
      </div>

      {/* Summary change */}
      {data.summaryChanged && (
        <div
          className="px-4 py-3 bg-amber-50/50 dark:bg-amber-900/10 rounded-lg border border-amber-200/50 dark:border-amber-800/30"
          data-testid="summary-changed"
        >
          <p className="text-xs font-medium text-amber-800 dark:text-amber-200 mb-2">
            Summary Changed
          </p>
          <div className="space-y-1">
            <div className="flex items-start gap-2 text-xs">
              <span className="text-red-500 shrink-0">Before:</span>
              <span className="text-muted-foreground">{data.leftSummary}</span>
            </div>
            <div className="flex items-start gap-2 text-xs">
              <span className="text-emerald-500 shrink-0">After:</span>
              <span>{data.rightSummary}</span>
            </div>
          </div>
        </div>
      )}

      {/* Diff sections */}
      <ListDiffSection title="Key Mechanics" diff={data.mechanicsDiff} />
      <ListDiffSection title="Key Concepts" diff={data.keyConceptsDiff} />
      <ListDiffSection title="Common Questions" diff={data.commonQuestionsDiff} />
      <FaqDiffSection data={data} />
    </div>
  );
}

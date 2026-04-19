'use client';

import { Suspense } from 'react';

import { useQuery } from '@tanstack/react-query';
import {
  ArrowLeftIcon,
  BookOpenIcon,
  CheckCircleIcon,
  DownloadIcon,
  HelpCircleIcon,
  ListIcon,
  TrophyIcon,
  PackageIcon,
  PlayIcon,
} from 'lucide-react';
import { useSearchParams } from 'next/navigation';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

interface VictoryConditions {
  primary: string;
  alternatives?: string[];
  isPointBased?: boolean;
  targetPoints?: number | null;
}

interface Resource {
  name: string;
  type: string;
  usage?: string;
  isLimited: boolean;
}

interface GamePhase {
  name: string;
  description: string;
  order: number;
  isOptional?: boolean;
}

function safeParseJson<T>(json: string, fallback: T): T {
  if (!json) return fallback;
  try {
    return JSON.parse(json) as T;
  } catch {
    return fallback;
  }
}

function ReviewContent() {
  const searchParams = useSearchParams();
  const sharedGameId = searchParams.get('sharedGameId') ?? '';
  const pdfDocumentId = searchParams.get('pdfDocumentId') ?? '';

  const { data: draft, isLoading } = useQuery({
    queryKey: ['admin', 'mechanic-draft', sharedGameId, pdfDocumentId],
    queryFn: () => adminClient.getMechanicDraft(sharedGameId, pdfDocumentId),
    enabled: !!sharedGameId && !!pdfDocumentId,
  });

  if (isLoading) {
    return (
      <div className="mx-auto max-w-[900px] space-y-6">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-24 w-full" />
        <Skeleton className="h-48 w-full" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  if (!draft) {
    return (
      <div className="flex h-[50vh] items-center justify-center text-muted-foreground">
        <p>Draft non trovato. Seleziona un gioco e un PDF dall&apos;editor.</p>
      </div>
    );
  }

  const mechanics: string[] = safeParseJson(draft.mechanicsDraft, []);
  const victory: VictoryConditions = safeParseJson(draft.victoryDraft, { primary: '' });
  const resources: Resource[] = safeParseJson(draft.resourcesDraft, []);
  const phases: GamePhase[] = safeParseJson(draft.phasesDraft, []);
  const questions: string[] = safeParseJson(draft.questionsDraft, []);

  const completedSections = [
    draft.summaryDraft,
    draft.mechanicsDraft,
    draft.victoryDraft,
    draft.resourcesDraft,
    draft.phasesDraft,
    draft.questionsDraft,
  ].filter(Boolean).length;

  return (
    <div className="mx-auto max-w-[900px] space-y-6 print:max-w-full">
      {/* Header */}
      <div className="flex items-start justify-between gap-4 flex-wrap">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            {draft.gameTitle} — Anteprima Analisi
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Revisione finale prima dell&apos;attivazione nella Knowledge Base
          </p>
        </div>
        <Badge
          variant="outline"
          className={
            completedSections === 6
              ? 'border-green-300 bg-green-50 text-green-800 dark:bg-green-950/30 dark:text-green-300'
              : 'border-amber-300 bg-amber-50 text-amber-800 dark:bg-amber-950/30 dark:text-amber-300'
          }
        >
          <CheckCircleIcon className="mr-1 h-3 w-3" />
          {completedSections}/6 sezioni completate
        </Badge>
      </div>

      {/* Stats Bar */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <StatCard value={completedSections} label="Sezioni completate" />
        <StatCard value={mechanics.length} label="Meccaniche estratte" />
        <StatCard value={resources.length} label="Risorse catalogate" />
        <StatCard value={draft.totalTokensUsed ?? 0} label="Token AI utilizzati" />
      </div>

      {/* Summary */}
      {draft.summaryDraft && (
        <ReviewCard title="Sommario del Gioco" icon={<BookOpenIcon className="h-4 w-4" />}>
          <p className="text-sm leading-relaxed">{draft.summaryDraft}</p>
        </ReviewCard>
      )}

      {/* Mechanics */}
      {mechanics.length > 0 && (
        <ReviewCard
          title="Meccaniche di Gioco"
          icon={<ListIcon className="h-4 w-4" />}
          badge={`${mechanics.length} meccaniche`}
        >
          <div className="flex flex-wrap gap-2">
            {mechanics.map((m, i) => (
              <span
                key={i}
                className="rounded-md border border-amber-200 bg-amber-50 px-2.5 py-1 text-xs font-semibold text-amber-900 dark:border-amber-700 dark:bg-amber-950/30 dark:text-amber-300"
              >
                {m}
              </span>
            ))}
          </div>
        </ReviewCard>
      )}

      {/* Victory */}
      {victory.primary && (
        <ReviewCard title="Condizioni di Vittoria" icon={<TrophyIcon className="h-4 w-4" />}>
          <p className="text-sm">
            <strong>Obiettivo principale:</strong> {victory.primary}
          </p>
          {victory.alternatives && victory.alternatives.length > 0 && (
            <div className="mt-2">
              <p className="text-xs font-semibold uppercase tracking-wide text-amber-600 dark:text-amber-400">
                Fonti di Punti
              </p>
              <ul className="mt-1 list-inside list-disc space-y-0.5 text-sm text-foreground">
                {victory.alternatives.map((alt, i) => (
                  <li key={i}>{alt}</li>
                ))}
              </ul>
            </div>
          )}
        </ReviewCard>
      )}

      {/* Resources */}
      {resources.length > 0 && (
        <ReviewCard
          title="Risorse"
          icon={<PackageIcon className="h-4 w-4" />}
          badge={`${resources.length} risorse`}
        >
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                <th className="py-2 pr-4">Risorsa</th>
                <th className="py-2 pr-4">Tipo</th>
                <th className="py-2 pr-4">Utilizzo</th>
                <th className="py-2">Limitata</th>
              </tr>
            </thead>
            <tbody>
              {resources.map((r, i) => (
                <tr key={i} className="border-b border-slate-100 dark:border-zinc-700/50">
                  <td className="py-2 pr-4 font-medium">{r.name}</td>
                  <td className="py-2 pr-4">{r.type}</td>
                  <td className="py-2 pr-4">{r.usage ?? '\u2014'}</td>
                  <td className="py-2">{r.isLimited ? 'Si' : 'No'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </ReviewCard>
      )}

      {/* Phases */}
      {phases.length > 0 && (
        <ReviewCard
          title="Fasi di Gioco"
          icon={<PlayIcon className="h-4 w-4" />}
          badge={`${phases.length} fasi`}
        >
          <div className="space-y-3">
            {phases
              .sort((a, b) => a.order - b.order)
              .map(p => (
                <div key={p.order} className="flex gap-3">
                  <div className="flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-amber-500 to-orange-500 text-xs font-bold text-white">
                    {p.order}
                  </div>
                  <div>
                    <p className="font-semibold text-sm">{p.name}</p>
                    <p className="text-xs text-muted-foreground">{p.description}</p>
                  </div>
                </div>
              ))}
          </div>
        </ReviewCard>
      )}

      {/* FAQ */}
      {questions.length > 0 && (
        <ReviewCard
          title="Domande Frequenti"
          icon={<HelpCircleIcon className="h-4 w-4" />}
          badge={`${questions.length} FAQ`}
        >
          <div className="space-y-3">
            {questions.map((q, i) => (
              <div key={i} className="text-sm">
                <p className="font-semibold">{q}</p>
              </div>
            ))}
          </div>
        </ReviewCard>
      )}

      {/* Copyright Footer */}
      <div className="rounded-lg border border-green-200 bg-green-50/50 p-4 text-center text-xs text-green-800 dark:border-green-800 dark:bg-green-950/20 dark:text-green-300 print:border-green-400">
        <strong>&copy; 2026 MeepleAI</strong> — Contenuto originale. Meccaniche di gioco descritte
        indipendentemente.
        <br />
        <span className="opacity-70">
          Generato con Variant C (human + AI assistant). L&apos;AI non ha mai letto il testo del PDF
          originale.
        </span>
        {(draft.totalTokensUsed ?? 0) > 0 && (
          <span className="ml-2 opacity-70">
            | {draft.totalTokensUsed} tokens, ${(draft.estimatedCostUsd ?? 0).toFixed(4)}
          </span>
        )}
      </div>

      {/* Action Bar */}
      <div className="flex items-center justify-between border-t pt-4 print:hidden">
        <Button variant="outline" asChild>
          <a href="/admin/knowledge-base/mechanic-extractor">
            <ArrowLeftIcon className="mr-2 h-4 w-4" />
            Torna all&apos;editor
          </a>
        </Button>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => window.print()}>
            <DownloadIcon className="mr-2 h-4 w-4" />
            Esporta PDF
          </Button>
        </div>
      </div>
    </div>
  );
}

function StatCard({ value, label }: { value: number; label: string }) {
  return (
    <div className="rounded-lg border border-slate-200 bg-white p-4 text-center transition-shadow hover:shadow-md dark:border-zinc-700 dark:bg-zinc-800">
      <div className="font-quicksand text-2xl font-bold text-amber-500">{value}</div>
      <div className="mt-0.5 text-xs text-muted-foreground">{label}</div>
    </div>
  );
}

function ReviewCard({
  title,
  icon,
  badge,
  children,
}: {
  title: string;
  icon: React.ReactNode;
  badge?: string;
  children: React.ReactNode;
}) {
  return (
    <Card className="bg-white/70 backdrop-blur-md border-slate-200/60 dark:bg-zinc-800/70 dark:border-zinc-700/60 print:shadow-none print:border-slate-300">
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <CardTitle className="flex items-center gap-2 text-base font-quicksand">
            {icon}
            {title}
          </CardTitle>
          {badge && (
            <Badge
              variant="outline"
              className="text-xs border-amber-300 text-amber-700 dark:text-amber-300"
            >
              {badge}
            </Badge>
          )}
        </div>
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  );
}

export default function MechanicExtractorReviewPage() {
  return (
    <Suspense fallback={<Skeleton className="h-96 w-full" />}>
      <ReviewContent />
    </Suspense>
  );
}

'use client';

import { useState, useEffect } from 'react';

import { AlertTriangleIcon, RefreshCwIcon } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { ChatAnalyticsDto } from '@/lib/api/schemas/chat-analytics.schemas';
import type { ModelPerformanceDto } from '@/lib/api/schemas/model-performance.schemas';
import type { PdfAnalyticsDto } from '@/lib/api/schemas/pdf.schemas';

interface MetricCardProps {
  title: string;
  children: React.ReactNode;
}

function MetricCard({ title, children }: MetricCardProps) {
  return (
    <div className="rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-md p-6">
      <h3 className="font-quicksand text-base font-semibold text-foreground">{title}</h3>
      <div className="mt-3 space-y-2">{children}</div>
    </div>
  );
}

function MetricRow({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="flex items-center justify-between text-sm">
      <span className="text-muted-foreground">{label}</span>
      <span className="font-medium text-foreground">{value}</span>
    </div>
  );
}

export function AiUsageTab() {
  const [pdfData, setPdfData] = useState<PdfAnalyticsDto | null>(null);
  const [chatData, setChatData] = useState<ChatAnalyticsDto | null>(null);
  const [modelData, setModelData] = useState<ModelPerformanceDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  const loadData = () => {
    setLoading(true);
    setError(false);
    Promise.all([
      api.admin.getPdfAnalytics(30).catch(() => null),
      api.admin.getChatAnalytics(30).catch(() => null),
      api.admin.getModelPerformance(30).catch(() => null),
    ])
      .then(([pdf, chat, model]) => {
        setPdfData(pdf);
        setChatData(chat);
        setModelData(model);
        if (!pdf && !chat && !model) setError(true);
      })
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadData();
  }, []);

  if (loading) {
    return (
      <div className="space-y-6 animate-pulse">
        <div className="h-8 w-48 rounded bg-muted/50" />
        <div className="grid gap-6 md:grid-cols-3">
          <div className="h-48 rounded-2xl bg-muted/50" />
          <div className="h-48 rounded-2xl bg-muted/50" />
          <div className="h-48 rounded-2xl bg-muted/50" />
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Analitiche Utilizzo AI
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Elaborazione PDF, attività chat e performance dei modelli negli ultimi 30 giorni.
        </p>
      </div>

      {error && (
        <div className="flex items-center gap-3 rounded-xl border border-amber-200 dark:border-amber-800/40 bg-amber-50 dark:bg-amber-950/20 px-4 py-3">
          <AlertTriangleIcon className="h-5 w-5 text-amber-600 dark:text-amber-400 shrink-0" />
          <p className="text-sm text-amber-800 dark:text-amber-200 flex-1">
            Impossibile caricare alcune metriche AI. I dati potrebbero essere incompleti.
          </p>
          <Button variant="outline" size="sm" onClick={loadData}>
            <RefreshCwIcon className="h-3.5 w-3.5 mr-1.5" />
            Riprova
          </Button>
        </div>
      )}

      <div className="grid gap-6 md:grid-cols-3">
        <MetricCard title="PDF Processing">
          <MetricRow label="Total Uploaded" value={pdfData?.totalUploaded ?? 0} />
          <MetricRow
            label="Success Rate"
            value={`${((pdfData?.successRate ?? 0) * 100).toFixed(1)}%`}
          />
          <MetricRow label="Failed" value={pdfData?.failedCount ?? 0} />
          <MetricRow
            label="Storage"
            value={`${((pdfData?.totalStorageBytes ?? 0) / (1024 * 1024)).toFixed(1)} MB`}
          />
        </MetricCard>

        <MetricCard title="Chat Activity">
          <MetricRow label="Total Threads" value={chatData?.totalThreads ?? 0} />
          <MetricRow label="Active Threads" value={chatData?.activeThreads ?? 0} />
          <MetricRow label="Total Messages" value={chatData?.totalMessages ?? 0} />
          <MetricRow label="Unique Users" value={chatData?.uniqueUsers ?? 0} />
        </MetricCard>

        <MetricCard title="Model Performance">
          <MetricRow label="Total Requests" value={modelData?.totalRequests ?? 0} />
          <MetricRow label="Total Cost" value={`$${(modelData?.totalCost ?? 0).toFixed(2)}`} />
          <MetricRow
            label="Avg Latency"
            value={`${(modelData?.avgLatencyMs ?? 0).toFixed(0)} ms`}
          />
          <MetricRow
            label="Success Rate"
            value={`${((modelData?.successRate ?? 0) * 100).toFixed(1)}%`}
          />
        </MetricCard>
      </div>
    </div>
  );
}

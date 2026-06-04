'use client';
import { useState } from 'react';

import { CsvImportModal } from './components/CsvImportModal';
import { ExportCatalogButton } from './components/ExportCatalogButton';
import { FailedItemsPanel } from './components/FailedItemsPanel';
import { LogStream } from './components/LogStream';
import { ManualAssignModal } from './components/ManualAssignModal';
import { QueuePendingPanel } from './components/QueuePendingPanel';
import { SyncRunTimeline } from './components/SyncRunTimeline';
import { SyncStatusHero } from './components/SyncStatusHero';

export default function CatalogIngestionPage() {
  const [csvOpen, setCsvOpen] = useState(false);
  const [manualOpen, setManualOpen] = useState(false);
  const [drillDownRunId, setDrillDownRunId] = useState<string | null>(null);

  return (
    <div className="space-y-4">
      {/* Header */}
      <header className="flex items-start justify-between">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Catalog ingestion
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">Admin · Catalog · BoardGameGeek sync</p>
        </div>
        <ExportCatalogButton />
      </header>

      <SyncStatusHero
        onOpenCsvModal={() => setCsvOpen(true)}
        onOpenManualModal={() => setManualOpen(true)}
      />

      <SyncRunTimeline onDrillDown={setDrillDownRunId} />

      <div className="grid gap-3.5 md:grid-cols-2">
        <QueuePendingPanel />
        <FailedItemsPanel />
      </div>

      {drillDownRunId !== null && (
        <LogStream runId={drillDownRunId} onClose={() => setDrillDownRunId(null)} />
      )}

      <CsvImportModal open={csvOpen} onOpenChange={setCsvOpen} />
      <ManualAssignModal open={manualOpen} onOpenChange={setManualOpen} />
    </div>
  );
}

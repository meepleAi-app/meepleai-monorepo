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
    <div className="flex flex-col gap-4">
      {/* Header — mockup `.admin-top` (12px 24px padding, sticky) */}
      <header className="flex items-center gap-4 border-b border-border bg-background/80 px-6 py-3 backdrop-blur-md">
        <div>
          <h1 className="font-quicksand text-[20px] font-extrabold leading-tight tracking-tight text-foreground">
            Catalog ingestion
          </h1>
          <p className="mt-0.5 font-mono text-[11px] text-muted-foreground">
            Admin · Catalog · BoardGameGeek sync
          </p>
        </div>
        <div className="ml-auto">
          <ExportCatalogButton />
        </div>
      </header>

      {/* Body — mockup `.admin-body` flex column gap 16px */}
      <div className="flex flex-col gap-4 px-6">
        <SyncStatusHero
          onOpenCsvModal={() => setCsvOpen(true)}
          onOpenManualModal={() => setManualOpen(true)}
        />

        <SyncRunTimeline onDrillDown={setDrillDownRunId} />

        <div className="grid grid-cols-2 gap-3.5">
          <QueuePendingPanel />
          <FailedItemsPanel />
        </div>

        {drillDownRunId !== null && (
          <LogStream runId={drillDownRunId} onClose={() => setDrillDownRunId(null)} />
        )}
      </div>

      <CsvImportModal open={csvOpen} onOpenChange={setCsvOpen} />
      <ManualAssignModal open={manualOpen} onOpenChange={setManualOpen} />
    </div>
  );
}

'use client';

import { DatabaseIcon, Download, Upload } from 'lucide-react';

import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';

import { EnrichmentQueueTab } from './components/EnrichmentQueueTab';
import { ExcelImportTab } from './components/ExcelImportTab';
import { ExportTab } from './components/ExportTab';

export default function CatalogIngestionPage() {
  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Catalog Ingestion
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Import games from Excel, enqueue enrichment, and export the catalog
        </p>
      </div>

      {/* Tabs */}
      <Tabs defaultValue="import">
        <TabsList>
          <TabsTrigger value="import" className="gap-1.5">
            <Upload className="h-3.5 w-3.5" />
            Import
          </TabsTrigger>
          <TabsTrigger value="enrichment" className="gap-1.5">
            <DatabaseIcon className="h-3.5 w-3.5" />
            Enrichment Queue
          </TabsTrigger>
          <TabsTrigger value="export" className="gap-1.5">
            <Download className="h-3.5 w-3.5" />
            Export
          </TabsTrigger>
        </TabsList>

        <TabsContent value="import">
          <ExcelImportTab />
        </TabsContent>

        <TabsContent value="enrichment">
          <EnrichmentQueueTab />
        </TabsContent>

        <TabsContent value="export">
          <ExportTab />
        </TabsContent>
      </Tabs>
    </div>
  );
}

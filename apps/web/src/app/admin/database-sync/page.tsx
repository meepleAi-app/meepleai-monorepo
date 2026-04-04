'use client';

import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';

import { DataComparisonTab } from './components/DataComparisonTab';
import { HistoryTab } from './components/HistoryTab';
import { SchemaComparisonTab } from './components/SchemaComparisonTab';
import { TunnelStatusBanner } from './components/TunnelStatusBanner';

export default function DatabaseSyncPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-quicksand text-2xl font-semibold tracking-tight text-foreground">
          Database Sync
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Compare and synchronize databases between local and staging.
        </p>
      </div>
      <TunnelStatusBanner />
      <Tabs defaultValue="schema">
        <TabsList>
          <TabsTrigger value="schema">Schema</TabsTrigger>
          <TabsTrigger value="data">Data</TabsTrigger>
          <TabsTrigger value="history">History</TabsTrigger>
        </TabsList>
        <TabsContent value="schema">
          <SchemaComparisonTab />
        </TabsContent>
        <TabsContent value="data">
          <DataComparisonTab />
        </TabsContent>
        <TabsContent value="history">
          <HistoryTab />
        </TabsContent>
      </Tabs>
    </div>
  );
}

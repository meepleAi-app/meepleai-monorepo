'use client';

import { useState } from 'react';

import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';

import { ConfigTab } from './config-tab';
import { EmbeddingTab } from './embedding-tab';
import { HistoryTab } from './history-tab';
import { UploadAndQueueTab } from './upload-and-queue-tab';

export function RagPipelineClient() {
  const [activeTab, setActiveTab] = useState('upload-queue');

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">RAG Pipeline</h1>
        <p className="text-muted-foreground">Gestisci upload, processing e embedding dei PDF</p>
      </div>

      <Tabs value={activeTab} onValueChange={setActiveTab}>
        <TabsList>
          <TabsTrigger value="upload-queue">Upload & Coda</TabsTrigger>
          <TabsTrigger value="history">Storico & Analytics</TabsTrigger>
          <TabsTrigger value="embedding">Embedding Service</TabsTrigger>
          <TabsTrigger value="config">Configurazione</TabsTrigger>
        </TabsList>

        <TabsContent value="upload-queue" className="mt-4">
          <UploadAndQueueTab />
        </TabsContent>
        <TabsContent value="history" className="mt-4">
          <HistoryTab />
        </TabsContent>
        <TabsContent value="embedding" className="mt-4">
          <EmbeddingTab />
        </TabsContent>
        <TabsContent value="config" className="mt-4">
          <ConfigTab />
        </TabsContent>
      </Tabs>
    </div>
  );
}

'use client';

/**
 * RAG Strategy Builder Client Component
 *
 * Wraps the StrategyBuilder component with admin-specific functionality:
 * - User tier detection
 * - Save/load callbacks
 * - Toast notifications
 *
 * @see Epic #3453 - Visual RAG Strategy Builder
 */

import { useState, useCallback } from 'react';

import { ArrowLeft, Upload, Download } from 'lucide-react';
import Link from 'next/link';

import { toast } from '@/components/layout';
import {
  StrategyBuilder,
  type PipelineDefinition,
} from '@/components/rag-dashboard/builder';
import { Button } from '@/components/ui/primitives/button';

export function StrategyBuilderClient() {
  const [savedPipelines, setSavedPipelines] = useState<PipelineDefinition[]>(
    []
  );

  // Handle save - stores locally for now
  // TODO: Integrate with backend API when #3464 (save/load custom strategies) is implemented
  const handleSave = useCallback(
    (pipeline: PipelineDefinition) => {
      const existingIndex = savedPipelines.findIndex(
        (p) => p.id === pipeline.id
      );
      if (existingIndex >= 0) {
        setSavedPipelines((prev) => {
          const updated = [...prev];
          updated[existingIndex] = pipeline;
          return updated;
        });
        toast.success(`"${pipeline.name}" has been updated.`);
      } else {
        setSavedPipelines((prev) => [...prev, pipeline]);
        toast.success(`"${pipeline.name}" has been saved.`);
      }

      // Export to localStorage for persistence (temporary solution)
      try {
        const stored = JSON.parse(
          localStorage.getItem('rag-custom-strategies') || '[]'
        );
        const updatedStored =
          existingIndex >= 0
            ? stored.map((p: PipelineDefinition) =>
                p.id === pipeline.id ? pipeline : p
              )
            : [...stored, pipeline];
        localStorage.setItem(
          'rag-custom-strategies',
          JSON.stringify(updatedStored)
        );
      } catch {
        // localStorage not available
      }
    },
    [savedPipelines]
  );

  // Handle test - triggers live testing panel
  const handleTest = useCallback((pipeline: PipelineDefinition) => {
    toast.info(`Running "${pipeline.name}" with sample query...`);
  }, []);

  // Export pipeline as JSON
  const handleExport = useCallback(() => {
    if (savedPipelines.length === 0) {
      toast.error('Save a strategy first before exporting.');
      return;
    }

    const dataStr = JSON.stringify(savedPipelines, null, 2);
    const dataBlob = new Blob([dataStr], { type: 'application/json' });
    const url = URL.createObjectURL(dataBlob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `rag-strategies-${new Date().toISOString().split('T')[0]}.json`;
    link.click();
    URL.revokeObjectURL(url);

    toast.success(`Exported ${savedPipelines.length} strategy(s).`);
  }, [savedPipelines]);

  // Import pipeline from JSON
  const handleImport = useCallback(() => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json';
    input.onchange = async (e) => {
      const file = (e.target as HTMLInputElement).files?.[0];
      if (!file) return;

      try {
        const text = await file.text();
        const imported = JSON.parse(text);
        const pipelines = Array.isArray(imported) ? imported : [imported];
        const updatedPipelines = [...savedPipelines, ...pipelines];
        setSavedPipelines(updatedPipelines);
        localStorage.setItem(
          'rag-custom-strategies',
          JSON.stringify(updatedPipelines)
        );
        toast.success(`Imported ${pipelines.length} strategy(s).`);
      } catch {
        toast.error('Invalid JSON file.');
      }
    };
    input.click();
  }, [savedPipelines]);

  return (
    <div className="flex h-screen flex-col" data-testid="strategy-builder-page">
      {/* Header */}
      <header className="flex h-14 items-center justify-between border-b bg-background px-4">
        <div className="flex items-center gap-3">
          <Link href="/admin/rag/tier-strategy-config">
            <Button variant="ghost" size="icon" data-testid="back-button">
              <ArrowLeft className="h-4 w-4" />
            </Button>
          </Link>
          <div>
            <h1 className="text-lg font-semibold" data-testid="page-title">
              RAG Strategy Builder
            </h1>
            <p className="text-xs text-muted-foreground">
              Visual pipeline designer with 23 building blocks
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={handleImport}
            data-testid="import-button"
          >
            <Upload className="mr-1 h-4 w-4" />
            Import
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={handleExport}
            data-testid="export-button"
          >
            <Download className="mr-1 h-4 w-4" />
            Export
          </Button>
          {savedPipelines.length > 0 && (
            <span
              className="text-xs text-muted-foreground"
              data-testid="saved-count"
            >
              {savedPipelines.length} saved
            </span>
          )}
        </div>
      </header>

      {/* Builder Canvas */}
      <div className="flex-1 overflow-hidden">
        <StrategyBuilder
          userTier="Admin" // Admin has full access
          onSave={handleSave}
          onTest={handleTest}
          showValidation={true}
          showConfig={true}
        />
      </div>
    </div>
  );
}

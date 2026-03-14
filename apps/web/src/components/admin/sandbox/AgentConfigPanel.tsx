'use client';

import { useState, useCallback, useMemo } from 'react';

import { Settings, RotateCcw } from 'lucide-react';

import { ChunkingParamsForm } from '@/components/admin/sandbox/ChunkingParamsForm';
import { useSandboxSession } from '@/components/admin/sandbox/contexts/SandboxSessionContext';
import { LlmSettingsForm } from '@/components/admin/sandbox/LlmSettingsForm';
import { RagStrategyForm } from '@/components/admin/sandbox/RagStrategyForm';
import { ReprocessConfirmDialog } from '@/components/admin/sandbox/ReprocessConfirmDialog';
import { Button } from '@/components/ui/button';
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/data-display/accordion';
import { Badge } from '@/components/ui/data-display/badge';

const CHUNKING_KEYS = ['chunkStrategy', 'chunkSize', 'overlap', 'respectPageBoundaries'] as const;

export function AgentConfigPanel() {
  const {
    agentConfig,
    appliedConfig,
    applyConfig,
    resetConfig,
    isDirty,
    pendingChanges,
    isApplying,
  } = useSandboxSession();

  const [reprocessDialogOpen, setReprocessDialogOpen] = useState(false);

  const hasChunkingChanges = useMemo(() => {
    return CHUNKING_KEYS.some(key => agentConfig[key] !== appliedConfig[key]);
  }, [agentConfig, appliedConfig]);

  const handleApply = useCallback(() => {
    if (hasChunkingChanges) {
      setReprocessDialogOpen(true);
    } else {
      applyConfig();
    }
  }, [hasChunkingChanges, applyConfig]);

  const handleConfirmReprocess = useCallback(() => {
    setReprocessDialogOpen(false);
    applyConfig();
  }, [applyConfig]);

  return (
    <div className="flex h-full flex-col">
      {/* Header */}
      <div className="flex items-center gap-2 border-b px-4 py-3">
        <Settings className="h-4 w-4 text-muted-foreground" />
        <h2 className="text-sm font-semibold">Agent Config</h2>
      </div>

      {/* Accordion sections */}
      <div className="flex-1 overflow-y-auto px-4">
        <Accordion type="multiple" defaultValue={['rag-strategy']}>
          <AccordionItem value="rag-strategy">
            <AccordionTrigger>Strategia RAG</AccordionTrigger>
            <AccordionContent>
              <RagStrategyForm />
            </AccordionContent>
          </AccordionItem>

          <AccordionItem value="llm-settings">
            <AccordionTrigger>Impostazioni LLM</AccordionTrigger>
            <AccordionContent>
              <LlmSettingsForm />
            </AccordionContent>
          </AccordionItem>

          <AccordionItem value="chunking-params">
            <AccordionTrigger>Parametri Chunking</AccordionTrigger>
            <AccordionContent>
              <ChunkingParamsForm />
            </AccordionContent>
          </AccordionItem>
        </Accordion>
      </div>

      {/* Bottom action bar */}
      <div className="flex items-center justify-between border-t px-4 py-3">
        <Button variant="ghost" size="sm" disabled={!isDirty} onClick={resetConfig}>
          <RotateCcw className="mr-1.5 h-3.5 w-3.5" />
          Reset
        </Button>
        <Button size="sm" disabled={!isDirty || isApplying} onClick={handleApply}>
          Applica
          {pendingChanges > 0 && (
            <Badge variant="secondary" className="ml-1.5 h-5 min-w-5 px-1.5 text-xs">
              {pendingChanges}
            </Badge>
          )}
        </Button>
      </div>

      {/* Reprocess confirmation dialog */}
      <ReprocessConfirmDialog
        open={reprocessDialogOpen}
        onOpenChange={setReprocessDialogOpen}
        onConfirm={handleConfirmReprocess}
        chunksToDelete={42}
        vectorsToDelete={42}
        estimatedTimeSeconds={30}
      />
    </div>
  );
}

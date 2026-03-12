/**
 * Agent Setup Panel - Admin RAG Dashboard
 *
 * Allows admin to create a RAG agent with selected Ready documents.
 * Shows cost estimation and document selection checklist.
 */

'use client';

import { useState, useMemo } from 'react';

import { Bot, CheckCircle2, Loader2, DollarSign } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { useEstimateAgentCost } from '@/hooks/queries/useEstimateAgentCost';
import { api } from '@/lib/api';
import type { DocumentStatus, AgentInfo } from '@/lib/api/schemas/rag-setup.schemas';

interface AgentSetupPanelProps {
  gameId: string;
  gameTitle: string;
  documents: DocumentStatus[];
  existingAgent: AgentInfo | null;
  onAgentCreated: (info: { agentId: string; chatThreadId: string }) => void;
}

export function AgentSetupPanel({
  gameId,
  gameTitle,
  documents,
  existingAgent,
  onAgentCreated,
}: AgentSetupPanelProps) {
  const [agentName, setAgentName] = useState(`${gameTitle} Assistant`);
  const [selectedDocIds, setSelectedDocIds] = useState<string[]>(() =>
    documents
      .filter((d) => d.processingState === 'Ready' && d.isActiveForRag)
      .map((d) => d.documentId)
  );
  const [creating, setCreating] = useState(false);

  const readyDocs = useMemo(
    () => documents.filter((d) => d.processingState === 'Ready'),
    [documents]
  );

  const { data: costEstimate, isLoading: costLoading } = useEstimateAgentCost(
    gameId,
    selectedDocIds,
    selectedDocIds.length > 0
  );

  const toggleDoc = (docId: string) => {
    setSelectedDocIds((prev) =>
      prev.includes(docId) ? prev.filter((id) => id !== docId) : [...prev, docId]
    );
  };

  const handleCreate = async () => {
    if (selectedDocIds.length === 0) return;

    setCreating(true);
    try {
      const result = await api.agents.createAgentWithSetup({
        userId: '', // Set by backend from session
        userTier: 'premium',
        userRole: 'admin',
        gameId,
        addToCollection: false,
        agentType: 'RAG',
        agentName: agentName.trim() || undefined,
        strategyName: 'HybridSearch',
        sharedGameId: gameId,
        documentIds: selectedDocIds,
      });

      if (result) {
        toast.success('Agente creato con successo!');
        onAgentCreated({
          agentId: result.agentId,
          chatThreadId: result.threadId,
        });
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore sconosciuto';
      toast.error(`Creazione fallita: ${message}`);
    } finally {
      setCreating(false);
    }
  };

  // Show existing agent info if already linked
  if (existingAgent) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Bot className="h-5 w-5" />
            Agente RAG
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex items-center justify-between rounded-lg border border-green-200 bg-green-50 p-4 dark:border-green-800 dark:bg-green-900/20">
            <div className="flex items-center gap-3">
              <CheckCircle2 className="h-5 w-5 text-green-600 dark:text-green-400" />
              <div>
                <p className="font-medium text-green-900 dark:text-green-100">
                  {existingAgent.name}
                </p>
                <p className="text-sm text-green-700 dark:text-green-300">
                  {existingAgent.type} •{' '}
                  {existingAgent.isActive ? 'Attivo' : 'Inattivo'}
                </p>
              </div>
            </div>
            <Badge
              variant="default"
              className={
                existingAgent.isReady
                  ? 'bg-green-600 hover:bg-green-700'
                  : 'bg-amber-600 hover:bg-amber-700'
              }
            >
              {existingAgent.isReady ? 'Pronto' : 'Non pronto'}
            </Badge>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Bot className="h-5 w-5" />
          Crea Agente RAG
        </CardTitle>
        <CardDescription>
          Seleziona i documenti indicizzati e crea un agente per la chat
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Agent Name */}
        <div className="space-y-2">
          <Label htmlFor="agent-name">Nome Agente</Label>
          <Input
            id="agent-name"
            value={agentName}
            onChange={(e) => setAgentName(e.target.value)}
            placeholder="es. Catan Assistant"
          />
        </div>

        {/* Document Selection */}
        <div className="space-y-2">
          <Label>Documenti per RAG</Label>
          {documents.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              Nessun documento caricato. Carica un PDF per iniziare.
            </p>
          ) : (
            <div className="space-y-2">
              {documents.map((doc) => {
                const isReady = doc.processingState === 'Ready';
                const isSelected = selectedDocIds.includes(doc.documentId);

                return (
                  <label
                    key={doc.documentId}
                    className="flex items-center gap-3 rounded-lg border p-3 transition-colors hover:bg-muted/50"
                  >
                    <Checkbox
                      checked={isSelected}
                      onCheckedChange={() => toggleDoc(doc.documentId)}
                      disabled={!isReady}
                    />
                    <span
                      className={`flex-1 truncate text-sm ${
                        isReady ? 'font-medium' : 'text-muted-foreground'
                      }`}
                    >
                      {doc.fileName}
                    </span>
                    <Badge
                      variant={isReady ? 'default' : 'secondary'}
                      className={
                        isReady
                          ? 'bg-green-600 text-xs hover:bg-green-700'
                          : 'text-xs'
                      }
                    >
                      {isReady ? 'Pronto' : doc.processingState}
                    </Badge>
                  </label>
                );
              })}
            </div>
          )}
        </div>

        {/* Cost Estimation */}
        {selectedDocIds.length > 0 && (
          <div className="rounded-lg border bg-muted/30 p-3">
            <div className="flex items-center gap-2 text-sm font-medium">
              <DollarSign className="h-4 w-4" />
              Stima Costi
            </div>
            {costLoading ? (
              <div className="mt-2 flex items-center gap-2 text-xs text-muted-foreground">
                <Loader2 className="h-3 w-3 animate-spin" />
                Calcolo in corso...
              </div>
            ) : costEstimate ? (
              <div className="mt-2 space-y-1 text-xs text-muted-foreground">
                <p>
                  Chunks totali: <strong>{costEstimate.totalChunks}</strong>
                </p>
                <p>
                  Costo per query:{' '}
                  <strong>
                    ${costEstimate.estimatedCostPerQuery.toFixed(6)}{' '}
                    {costEstimate.currency}
                  </strong>
                </p>
                <p>Modello: {costEstimate.model}</p>
                <p className="italic">{costEstimate.note}</p>
              </div>
            ) : null}
          </div>
        )}

        {/* Create Button */}
        <Button
          onClick={handleCreate}
          disabled={
            creating || selectedDocIds.length === 0 || readyDocs.length === 0
          }
          className="w-full"
        >
          {creating ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              Creazione in corso...
            </>
          ) : (
            <>
              <Bot className="mr-2 h-4 w-4" />
              Crea Agente ({selectedDocIds.length} doc
              {selectedDocIds.length !== 1 ? 'umenti' : 'umento'})
            </>
          )}
        </Button>
      </CardContent>
    </Card>
  );
}

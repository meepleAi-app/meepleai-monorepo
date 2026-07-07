/**
 * AgentConfigModal Component (Issue #2518)
 *
 * Modal for configuring per-game AI agent with:
 * - Model selection dropdown
 * - Temperature slider (0-2)
 * - Max tokens input (512-8192)
 * - Personality radio buttons
 * - Detail level radio buttons
 * - Custom instructions textarea
 * - Actions: Save, Test Agent, Reset to Default, Cancel
 */

'use client';

import { useState, useEffect } from 'react';

import { useQueryClient } from '@tanstack/react-query';
import { Loader2, Check, RotateCcw, MessageCircle, Bot } from 'lucide-react';

import { AgentConfigFields, StrategySelector, TypologySelector } from '@/components/agent/config';
import { toast } from '@/components/layout/Toast';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { useAgentConfig, useUpdateAgentConfig, agentConfigKeys } from '@/hooks/queries';
import {
  api,
  DEFAULT_AGENT_CONFIG,
  type AIModel,
  type AgentPersonality,
  type DetailLevel,
  type UpdateAgentConfigRequest,
} from '@/lib/api';

interface AgentConfigModalProps {
  isOpen: boolean;
  onClose: () => void;
  gameId: string;
  gameTitle: string;
}

export function AgentConfigModal({ isOpen, onClose, gameId, gameTitle }: AgentConfigModalProps) {
  // Fetch current configuration
  const { data: currentConfig, isLoading: configLoading } = useAgentConfig(gameId, isOpen);
  const updateMutation = useUpdateAgentConfig();
  const queryClient = useQueryClient();

  // Create mode state
  const [mode, setMode] = useState<'create' | 'edit'>('edit');
  const [agentDefinitionId, setagentDefinitionId] = useState<string>();
  const [strategyName, setStrategyName] = useState('Balanced');
  const [isCreating, setIsCreating] = useState(false);

  // Determine mode when modal opens / config loads
  useEffect(() => {
    if (isOpen && !configLoading) {
      setMode(currentConfig ? 'edit' : 'create');
    }
  }, [isOpen, configLoading, currentConfig]);

  // Form state
  const [llmModel, setLlmModel] = useState<AIModel>(DEFAULT_AGENT_CONFIG.llmModel);
  const [temperature, setTemperature] = useState(DEFAULT_AGENT_CONFIG.temperature);
  const [maxTokens, setMaxTokens] = useState(DEFAULT_AGENT_CONFIG.maxTokens);
  const [personality, setPersonality] = useState<AgentPersonality>(
    DEFAULT_AGENT_CONFIG.personality
  );
  const [detailLevel, setDetailLevel] = useState<DetailLevel>(DEFAULT_AGENT_CONFIG.detailLevel);
  const [personalNotes, setPersonalNotes] = useState('');

  // Load current config when modal opens
  useEffect(() => {
    if (isOpen && currentConfig) {
      setLlmModel(currentConfig.llmModel);
      setTemperature(currentConfig.temperature);
      setMaxTokens(currentConfig.maxTokens);
      setPersonality(currentConfig.personality);
      setDetailLevel(currentConfig.detailLevel);
      setPersonalNotes(currentConfig.personalNotes || '');
    } else if (isOpen && !currentConfig) {
      // Reset to defaults if no config exists (inline to avoid dependency)
      setLlmModel(DEFAULT_AGENT_CONFIG.llmModel);
      setTemperature(DEFAULT_AGENT_CONFIG.temperature);
      setMaxTokens(DEFAULT_AGENT_CONFIG.maxTokens);
      setPersonality(DEFAULT_AGENT_CONFIG.personality);
      setDetailLevel(DEFAULT_AGENT_CONFIG.detailLevel);
      setPersonalNotes('');
    }
  }, [isOpen, currentConfig]);

  const handleSave = async () => {
    const request: UpdateAgentConfigRequest = {
      llmModel,
      temperature,
      maxTokens,
      personality,
      detailLevel,
      personalNotes: personalNotes || null,
    };

    try {
      await updateMutation.mutateAsync({ gameId, request });
      toast.success(`Configurazione agente per "${gameTitle}" salvata con successo!`);
      onClose();
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Errore durante il salvataggio della configurazione'
      );
    }
  };

  const handleResetToDefault = () => {
    setLlmModel(DEFAULT_AGENT_CONFIG.llmModel);
    setTemperature(DEFAULT_AGENT_CONFIG.temperature);
    setMaxTokens(DEFAULT_AGENT_CONFIG.maxTokens);
    setPersonality(DEFAULT_AGENT_CONFIG.personality);
    setDetailLevel(DEFAULT_AGENT_CONFIG.detailLevel);
    setPersonalNotes('');
    toast.info('Configurazione ripristinata ai valori predefiniti');
  };

  const handleTestAgent = () => {
    // TODO: Implement test agent functionality when endpoint available
    toast.info('Funzionalità "Testa Agente" in arrivo!');
  };

  const handleCancel = () => {
    onClose();
  };

  const handleCreateAgent = async () => {
    if (!agentDefinitionId) {
      toast.error('Seleziona un tipo di agente');
      return;
    }

    setIsCreating(true);
    try {
      await api.library.createGameAgent(gameId, {
        agentDefinitionId,
        strategyName,
        strategyParameters: null,
      });

      toast.success(`Agente AI per "${gameTitle}" creato con successo!`);
      // Invalidate agent config cache to refetch
      queryClient.invalidateQueries({ queryKey: agentConfigKeys.byGame(gameId) });
      setMode('edit');
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Creazione agente fallita';
      toast.error(`Errore: ${message}`);
    } finally {
      setIsCreating(false);
    }
  };

  const isSaving = updateMutation.isPending;

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[600px] max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>
            {mode === 'create' ? 'Crea Agente AI' : 'Configurazione Agente AI'}
          </DialogTitle>
          <DialogDescription>
            {mode === 'create' ? (
              <>
                Seleziona tipologia e strategia per creare un agente AI per{' '}
                <strong>{gameTitle}</strong>
              </>
            ) : (
              <>
                Personalizza l'agente AI per <strong>{gameTitle}</strong>
              </>
            )}
          </DialogDescription>
        </DialogHeader>

        {configLoading ? (
          <div className="flex items-center justify-center py-12">
            <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
          </div>
        ) : mode === 'create' ? (
          <div className="space-y-6">
            <TypologySelector
              value={agentDefinitionId}
              onChange={setagentDefinitionId}
              disabled={isCreating}
            />

            <StrategySelector
              value={strategyName}
              onChange={setStrategyName}
              disabled={isCreating}
            />

            <div className="p-3 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg">
              <p className="text-sm text-blue-800 dark:text-blue-200">
                <Bot className="inline h-4 w-4 mr-1" />
                L'agente verrà creato con la tipologia e strategia selezionate. Potrai
                personalizzare modello e parametri dopo la creazione.
              </p>
            </div>

            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={handleCancel} disabled={isCreating}>
                Annulla
              </Button>
              <Button onClick={handleCreateAgent} disabled={!agentDefinitionId || isCreating}>
                {isCreating ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Creazione...
                  </>
                ) : (
                  <>
                    <Bot className="mr-2 h-4 w-4" />
                    Crea Agente
                  </>
                )}
              </Button>
            </div>
          </div>
        ) : (
          <AgentConfigFields
            value={{
              llmModel,
              temperature,
              maxTokens,
              personality,
              detailLevel,
              personalNotes,
            }}
            onChange={v => {
              setLlmModel(v.llmModel);
              setTemperature(v.temperature);
              setMaxTokens(v.maxTokens);
              setPersonality(v.personality);
              setDetailLevel(v.detailLevel);
              setPersonalNotes(v.personalNotes);
            }}
          />
        )}

        {mode === 'edit' && (
          <DialogFooter className="flex flex-col sm:flex-row gap-2">
            <div className="flex-1 flex gap-2">
              <Button variant="outline" onClick={handleResetToDefault} disabled={isSaving}>
                <RotateCcw className="mr-2 h-4 w-4" />
                Reset
              </Button>
              <Button variant="outline" onClick={handleTestAgent} disabled={isSaving}>
                <MessageCircle className="mr-2 h-4 w-4" />
                Testa
              </Button>
            </div>
            <div className="flex gap-2">
              <Button variant="outline" onClick={handleCancel} disabled={isSaving}>
                Annulla
              </Button>
              <Button onClick={handleSave} disabled={isSaving || configLoading}>
                {isSaving ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Salvataggio...
                  </>
                ) : (
                  <>
                    <Check className="mr-2 h-4 w-4" />
                    Salva Configurazione
                  </>
                )}
              </Button>
            </div>
          </DialogFooter>
        )}
      </DialogContent>
    </Dialog>
  );
}

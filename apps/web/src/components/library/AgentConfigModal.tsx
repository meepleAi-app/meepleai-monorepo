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

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Slider } from '@/components/ui/slider';
import { toast } from '@/components/layout/Toast';
import { Loader2, Check, RotateCcw, MessageCircle } from 'lucide-react';

import { useAgentConfig, useUpdateAgentConfig } from '@/hooks/queries';
import {
  MODEL_OPTIONS,
  PERSONALITY_OPTIONS,
  DETAIL_LEVEL_OPTIONS,
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

  // Form state
  const [modelType, setModelType] = useState<AIModel>(DEFAULT_AGENT_CONFIG.modelType);
  const [temperature, setTemperature] = useState(DEFAULT_AGENT_CONFIG.temperature);
  const [maxTokens, setMaxTokens] = useState(DEFAULT_AGENT_CONFIG.maxTokens);
  const [personality, setPersonality] = useState<AgentPersonality>(
    DEFAULT_AGENT_CONFIG.personality
  );
  const [detailLevel, setDetailLevel] = useState<DetailLevel>(DEFAULT_AGENT_CONFIG.detailLevel);
  const [customInstructions, setCustomInstructions] = useState('');

  // Load current config when modal opens
  useEffect(() => {
    if (isOpen && currentConfig) {
      setModelType(currentConfig.modelType);
      setTemperature(currentConfig.temperature);
      setMaxTokens(currentConfig.maxTokens);
      setPersonality(currentConfig.personality);
      setDetailLevel(currentConfig.detailLevel);
      setCustomInstructions(currentConfig.customInstructions || '');
    } else if (isOpen && !currentConfig) {
      // Reset to defaults if no config exists (inline to avoid dependency)
      setModelType(DEFAULT_AGENT_CONFIG.modelType);
      setTemperature(DEFAULT_AGENT_CONFIG.temperature);
      setMaxTokens(DEFAULT_AGENT_CONFIG.maxTokens);
      setPersonality(DEFAULT_AGENT_CONFIG.personality);
      setDetailLevel(DEFAULT_AGENT_CONFIG.detailLevel);
      setCustomInstructions('');
    }
  }, [isOpen, currentConfig]);

  const handleSave = async () => {
    const request: UpdateAgentConfigRequest = {
      modelType,
      temperature,
      maxTokens,
      personality,
      detailLevel,
      customInstructions: customInstructions || null,
    };

    try {
      await updateMutation.mutateAsync({ gameId, request });
      toast.success(`Configurazione agente per "${gameTitle}" salvata con successo!`);
      onClose();
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Errore durante il salvataggio della configurazione'
      );
    }
  };

  const handleResetToDefault = () => {
    setModelType(DEFAULT_AGENT_CONFIG.modelType);
    setTemperature(DEFAULT_AGENT_CONFIG.temperature);
    setMaxTokens(DEFAULT_AGENT_CONFIG.maxTokens);
    setPersonality(DEFAULT_AGENT_CONFIG.personality);
    setDetailLevel(DEFAULT_AGENT_CONFIG.detailLevel);
    setCustomInstructions('');
    toast.info('Configurazione ripristinata ai valori predefiniti');
  };

  const handleTestAgent = () => {
    // TODO: Implement test agent functionality when endpoint available
    toast.info('Funzionalità "Testa Agente" in arrivo!');
  };

  const handleCancel = () => {
    onClose();
  };

  const isSaving = updateMutation.isPending;

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[600px] max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Configurazione Agente AI</DialogTitle>
          <DialogDescription>
            Personalizza l'agente AI per <strong>{gameTitle}</strong>
          </DialogDescription>
        </DialogHeader>

        {configLoading ? (
          <div className="flex items-center justify-center py-12">
            <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
          </div>
        ) : (
          <div className="space-y-6">
            {/* Model Selection */}
            <div className="space-y-2">
              <Label htmlFor="model" className="text-base font-semibold">
                🤖 Modello AI
              </Label>
              <Select value={modelType} onValueChange={(value) => setModelType(value as AIModel)}>
                <SelectTrigger id="model">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {MODEL_OPTIONS.map((model) => (
                    <SelectItem key={model.value} value={model.value}>
                      {model.label} ({model.costLevel}){' '}
                      {model.isDefault && '⭐'}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-sm text-muted-foreground">
                {MODEL_OPTIONS.find((m) => m.value === modelType)?.description}
              </p>
            </div>

            {/* Temperature Slider */}
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <Label htmlFor="temperature" className="text-base font-semibold">
                  ⚙️ Temperatura
                </Label>
                <span className="text-sm text-muted-foreground">{temperature.toFixed(2)}</span>
              </div>
              <Slider
                id="temperature"
                min={0}
                max={2}
                step={0.1}
                value={[temperature]}
                onValueChange={([value]) => setTemperature(value)}
                className="w-full"
              />
              <div className="flex justify-between text-xs text-muted-foreground">
                <span>0.0 (Preciso)</span>
                <span>2.0 (Creativo)</span>
              </div>
            </div>

            {/* Max Tokens Input */}
            <div className="space-y-2">
              <Label htmlFor="maxTokens" className="text-base font-semibold">
                📏 Max Tokens
              </Label>
              <Input
                id="maxTokens"
                type="number"
                min={512}
                max={8192}
                step={256}
                value={maxTokens}
                onChange={(e) => setMaxTokens(Number(e.target.value))}
              />
              <p className="text-sm text-muted-foreground">
                Lunghezza massima della risposta (512-8192)
              </p>
            </div>

            {/* Personality Radio Buttons */}
            <div className="space-y-2">
              <Label className="text-base font-semibold">🎭 Personalità Agente</Label>
              <RadioGroup value={personality} onValueChange={(v) => setPersonality(v as AgentPersonality)}>
                {PERSONALITY_OPTIONS.map((option) => (
                  <div key={option.value} className="flex items-center space-x-2">
                    <RadioGroupItem value={option.value} id={`personality-${option.value}`} />
                    <Label
                      htmlFor={`personality-${option.value}`}
                      className="font-normal cursor-pointer"
                    >
                      <span className="font-medium">{option.label}</span>
                      <span className="text-sm text-muted-foreground ml-2">
                        ({option.description})
                      </span>
                    </Label>
                  </div>
                ))}
              </RadioGroup>
            </div>

            {/* Detail Level Radio Buttons */}
            <div className="space-y-2">
              <Label className="text-base font-semibold">📊 Livello Dettaglio</Label>
              <RadioGroup value={detailLevel} onValueChange={(v) => setDetailLevel(v as DetailLevel)}>
                {DETAIL_LEVEL_OPTIONS.map((option) => (
                  <div key={option.value} className="flex items-center space-x-2">
                    <RadioGroupItem value={option.value} id={`detail-${option.value}`} />
                    <Label
                      htmlFor={`detail-${option.value}`}
                      className="font-normal cursor-pointer"
                    >
                      <span className="font-medium">{option.label}</span>
                      <span className="text-sm text-muted-foreground ml-2">
                        ({option.description})
                      </span>
                    </Label>
                  </div>
                ))}
              </RadioGroup>
            </div>

            {/* Custom Instructions Textarea */}
            <div className="space-y-2">
              <Label htmlFor="instructions" className="text-base font-semibold">
                📝 Note Personali
              </Label>
              <Textarea
                id="instructions"
                placeholder="Es: Spiega sempre le regole come se fossi principiante"
                value={customInstructions}
                onChange={(e) => setCustomInstructions(e.target.value)}
                maxLength={1000}
                rows={4}
              />
              <div className="text-xs text-right text-muted-foreground">
                {1000 - customInstructions.length} caratteri rimanenti
              </div>
            </div>
          </div>
        )}

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
      </DialogContent>
    </Dialog>
  );
}

/**
 * Typology Form Component - Issue #3180
 *
 * Form for creating and editing AI agent typologies.
 * Features:
 * - Monaco editor for prompt templates with variable autocomplete
 * - Live preview with sample data
 * - Strategy selection with descriptions
 * - Optional JSON editor for advanced parameters
 * - Full validation with Zod + react-hook-form
 */

'use client';

import { useState, useEffect } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import Editor from '@monaco-editor/react';
import { AlertCircle, Code, EyeOff } from 'lucide-react';
import { useForm, Controller, type Resolver } from 'react-hook-form';
import { toast } from 'sonner';

import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { ConfirmDialog } from '@/components/ui/feedback/confirm-dialog';
import { Separator } from '@/components/ui/navigation/separator';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { agentTypologiesApi } from '@/lib/api/agent-typologies.api';
import {
  createTypologySchema,
  type CreateTypology,
  type Typology,
} from '@/lib/api/schemas/agent-typologies.schemas';
import { logger } from '@/lib/logger';

import {
  PhaseModelConfiguration,
  type StrategyPhaseModels,
  type StrategyOptions,
} from './PhaseModelConfiguration';
import { PromptPreview } from './PromptPreview';
import { StrategySelector } from './StrategySelector';
import { TypologyPromptEditor } from './TypologyPromptEditor';

// ========== Component Props ==========

export interface TypologyFormProps {
  /** Existing typology data for edit mode */
  typology?: Typology | null;
  /** Callback on successful form submission */
  onSubmit: (typologyId: string) => void;
  /** Callback to cancel form */
  onCancel: () => void;
  /** Whether to show loading state */
  isLoading?: boolean;
  /** Whether this is an editor proposal (uses /propose endpoint) - Issue #3182 */
  isProposal?: boolean;
}

// ========== Component ==========

export function TypologyForm({
  typology,
  onSubmit,
  onCancel,
  isLoading = false,
  isProposal = false,
}: TypologyFormProps) {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [showAdvancedParams, setShowAdvancedParams] = useState(
    !!typology?.defaultStrategyParameters
  );
  const [showCancelConfirm, setShowCancelConfirm] = useState(false);
  const [jsonError, setJsonError] = useState<string | null>(null);
  const [promptError, setPromptError] = useState<string | null>(null);
  // Issue #3245: Phase-Model Configuration
  const [phaseModels, setPhaseModels] = useState<StrategyPhaseModels>({});
  const [strategyOptions, setStrategyOptions] = useState<StrategyOptions>({});

  const isEditMode = !!typology;

  const {
    register,
    handleSubmit,
    control,
    watch,
    formState: { errors, isDirty },
  } = useForm<CreateTypology>({
    resolver: zodResolver(createTypologySchema) as Resolver<CreateTypology>,
    defaultValues: typology
      ? {
          name: typology.name,
          description: typology.description,
          basePrompt: typology.basePrompt,
          defaultStrategyName: typology.defaultStrategyName,
          defaultStrategyParameters: typology.defaultStrategyParameters || undefined,
        }
      : {
          name: '',
          description: '',
          basePrompt: '',
          defaultStrategyName: 'HybridSearch',
          defaultStrategyParameters: undefined,
        },
  });

  // Watch fields for preview
  const basePrompt = watch('basePrompt');
  const defaultStrategyParameters = watch('defaultStrategyParameters');

  // Parse strategy parameters for JSON editor
  const [jsonValue, setJsonValue] = useState<string>(
    defaultStrategyParameters ? JSON.stringify(defaultStrategyParameters, null, 2) : '{}'
  );

  // Update JSON value when strategy parameters change from form
  useEffect(() => {
    if (defaultStrategyParameters && typeof defaultStrategyParameters === 'object') {
      setJsonValue(JSON.stringify(defaultStrategyParameters, null, 2));
    }
  }, [defaultStrategyParameters]);

  // Handle form submission
  const onFormSubmit = async (data: CreateTypology) => {
    // Validate no errors before submitting
    if (jsonError || promptError) {
      toast.error('Correggi gli errori prima di salvare');
      return;
    }

    setIsSubmitting(true);
    try {
      let typologyId: string;
      if (isEditMode) {
        await agentTypologiesApi.update(typology.id, data);
        typologyId = typology.id;
        toast.success(
          isProposal ? 'Proposta aggiornata con successo' : 'Tipologia aggiornata con successo'
        );
      } else {
        // Issue #3182: Use propose endpoint for editor proposals
        const result = isProposal
          ? await agentTypologiesApi.propose(data)
          : await agentTypologiesApi.create(data);
        typologyId = result.id;
        toast.success(isProposal ? 'Proposta creata come Draft' : 'Tipologia creata con successo');
      }

      onSubmit(typologyId);
    } catch (error) {
      logger.error('Failed to save typology:', error);
      toast.error(
        isProposal
          ? 'Errore nel salvataggio della proposta'
          : 'Errore nel salvataggio della tipologia',
        {
          description: error instanceof Error ? error.message : 'Errore sconosciuto',
        }
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  // Handle cancel with confirmation if form is dirty
  const handleCancel = () => {
    if (isDirty) {
      setShowCancelConfirm(true);
    } else {
      onCancel();
    }
  };

  const handleConfirmCancel = () => {
    setShowCancelConfirm(false);
    onCancel();
  };

  // Handle JSON editor change
  const handleJsonChange = (value: string | undefined, onChange: (val: unknown) => void) => {
    if (!value) {
      setJsonValue('{}');
      onChange(undefined);
      setJsonError(null);
      return;
    }

    setJsonValue(value);
    try {
      const parsed = JSON.parse(value);
      onChange(parsed);
      setJsonError(null);
    } catch {
      setJsonError('JSON non valido');
      onChange(undefined);
    }
  };

  return (
    <form onSubmit={handleSubmit(onFormSubmit)} className="space-y-6" noValidate>
      {/* Basic Information Card */}
      <Card>
        <CardHeader>
          <CardTitle>Informazioni Base</CardTitle>
          <CardDescription>Nome e descrizione della tipologia</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* Name */}
          <div>
            <Label htmlFor="name">Nome *</Label>
            <Input
              id="name"
              {...register('name')}
              placeholder="Es: Rules Assistant, Setup Helper, Strategy Guide"
              disabled={isSubmitting || isLoading}
            />
            {errors.name && <p className="text-sm text-destructive mt-1">{errors.name.message}</p>}
            <p className="text-xs text-muted-foreground mt-1">3-100 caratteri, deve essere unico</p>
          </div>

          {/* Description */}
          <div>
            <Label htmlFor="description">Descrizione</Label>
            <Textarea
              id="description"
              {...register('description')}
              placeholder="Descrivi quando questa tipologia dovrebbe essere utilizzata..."
              rows={3}
              disabled={isSubmitting || isLoading}
            />
            {errors.description && (
              <p className="text-sm text-destructive mt-1">{errors.description.message}</p>
            )}
            <p className="text-xs text-muted-foreground mt-1">Massimo 500 caratteri</p>
          </div>

          {/* Status Info (Read-only, managed by approval workflow) */}
          {isEditMode && typology && (
            <div className="p-4 bg-muted/50 rounded-lg">
              <Label>Stato Approvazione</Label>
              <p className="text-sm text-muted-foreground mt-1">
                Stato corrente: <span className="font-medium">{typology.status}</span>
              </p>
              <p className="text-xs text-muted-foreground mt-2">
                Lo stato viene gestito dal workflow di approvazione (Draft → PendingReview →
                Approved)
              </p>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Prompt Template Card */}
      <Card>
        <CardHeader>
          <CardTitle>Template Prompt</CardTitle>
          <CardDescription>
            Definisci il prompt template con variabili dinamiche per questa tipologia
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div>
            <Label htmlFor="basePrompt">Prompt Base *</Label>
            <div className="mt-2">
              <Controller
                name="basePrompt"
                control={control}
                render={({ field }) => (
                  <TypologyPromptEditor
                    value={field.value}
                    onChange={field.onChange}
                    readonly={isSubmitting || isLoading}
                    maxLength={5000}
                    onValidationError={setPromptError}
                  />
                )}
              />
            </div>
            {errors.basePrompt && (
              <p className="text-sm text-destructive mt-1">{errors.basePrompt.message}</p>
            )}
            {promptError && (
              <Alert variant="destructive" className="mt-2">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>{promptError}</AlertDescription>
              </Alert>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Live Preview */}
      <PromptPreview prompt={basePrompt} />

      {/* Strategy Configuration Card */}
      <Card>
        <CardHeader>
          <CardTitle>Configurazione Strategia RAG</CardTitle>
          <CardDescription>
            Seleziona la strategia di retrieval e configura parametri avanzati (opzionale)
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* Strategy Selector */}
          <Controller
            name="defaultStrategyName"
            control={control}
            render={({ field }) => (
              <StrategySelector
                value={field.value}
                onChange={field.onChange}
                disabled={isSubmitting || isLoading}
              />
            )}
          />
        </CardContent>
      </Card>

      {/* Issue #3245: Phase-Model Configuration for RAG Strategies */}
      {['FAST', 'BALANCED', 'PRECISE', 'EXPERT', 'CONSENSUS', 'CUSTOM'].includes(
        watch('defaultStrategyName')
      ) && (
        <PhaseModelConfiguration
          strategy={watch('defaultStrategyName')}
          phaseModels={phaseModels}
          strategyOptions={strategyOptions}
          onChange={(newPhaseModels, newOptions) => {
            setPhaseModels(newPhaseModels);
            if (newOptions) setStrategyOptions(newOptions);
          }}
          disabled={isSubmitting || isLoading}
        />
      )}

      {/* Advanced Parameters Card (for legacy strategies) */}
      <Card>
        <CardHeader>
          <CardTitle>Parametri Avanzati</CardTitle>
          <CardDescription>
            Configurazione JSON per parametri specifici della strategia (opzionale)
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <Separator />

          {/* Advanced Parameters Toggle */}
          <div className="flex items-center justify-between">
            <div className="space-y-0.5">
              <Label>Parametri Avanzati (opzionale)</Label>
              <p className="text-sm text-muted-foreground">
                Configurazione JSON per parametri specifici della strategia
              </p>
            </div>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setShowAdvancedParams(!showAdvancedParams)}
              className="gap-2"
            >
              {showAdvancedParams ? (
                <>
                  <EyeOff className="h-4 w-4" />
                  Nascondi
                </>
              ) : (
                <>
                  <Code className="h-4 w-4" />
                  Mostra Editor JSON
                </>
              )}
            </Button>
          </div>

          {/* JSON Editor */}
          {showAdvancedParams && (
            <div>
              <Label htmlFor="defaultStrategyParameters">Parametri Strategia (JSON)</Label>
              <div className="mt-2 border rounded-md overflow-hidden">
                <Controller
                  name="defaultStrategyParameters"
                  control={control}
                  render={({ field }) => (
                    <Editor
                      height="200px"
                      defaultLanguage="json"
                      value={jsonValue}
                      onChange={value => handleJsonChange(value, field.onChange)}
                      theme="vs-light"
                      options={{
                        readOnly: isSubmitting || isLoading,
                        minimap: { enabled: false },
                        lineNumbers: 'on',
                        fontSize: 14,
                        wordWrap: 'on',
                        scrollBeyondLastLine: false,
                        automaticLayout: true,
                        padding: { top: 10, bottom: 10 },
                        formatOnPaste: true,
                        formatOnType: true,
                      }}
                    />
                  )}
                />
              </div>
              {jsonError && (
                <Alert variant="destructive" className="mt-2">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>{jsonError}</AlertDescription>
                </Alert>
              )}
              {errors.defaultStrategyParameters &&
                typeof errors.defaultStrategyParameters.message === 'string' && (
                  <p className="text-sm text-destructive mt-1">
                    {errors.defaultStrategyParameters.message}
                  </p>
                )}
              <p className="text-xs text-muted-foreground mt-1">
                Esempio: <code>{`{"topK": 5, "scoreThreshold": 0.7, "rerank": true}`}</code>
              </p>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Form Actions */}
      <div className="flex justify-end gap-3 pt-4 border-t">
        <Button
          type="button"
          variant="outline"
          onClick={handleCancel}
          disabled={isSubmitting || isLoading}
        >
          Annulla
        </Button>
        <Button type="submit" disabled={isSubmitting || isLoading || !!jsonError || !!promptError}>
          {isSubmitting ? 'Salvataggio...' : isEditMode ? 'Aggiorna Tipologia' : 'Crea Tipologia'}
        </Button>
      </div>

      {/* Cancel Confirmation Dialog */}
      <ConfirmDialog
        open={showCancelConfirm}
        onOpenChange={setShowCancelConfirm}
        title="Modifiche non salvate"
        message="Ci sono modifiche non salvate. Vuoi davvero annullare?"
        variant="destructive"
        confirmText="Annulla modifiche"
        cancelText="Continua a modificare"
        onConfirm={handleConfirmCancel}
      />
    </form>
  );
}

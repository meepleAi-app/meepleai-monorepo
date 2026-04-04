/**
 * AgentConfigForm - Agent Configuration Form
 * Issue #4948: Agent config page redesign
 *
 * Right column of the 2-column agent config layout.
 * Agent type radio group + strategy dropdown + cost estimate
 * + primary "Salva & Avvia Chat" CTA.
 */

'use client';

import { useState } from 'react';

import { AlertCircle, BotOff, Loader2, MessageSquare, Sparkles } from 'lucide-react';
import { useRouter } from 'next/navigation';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Label } from '@/components/ui/primitives/label';
import { logger } from '@/lib/logger';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export type AgentTypology = 'tutor' | 'arbitro' | 'strategist';
export type AgentStrategy = 'balanced' | 'creative' | 'precise';

export interface AgentConfigFormProps {
  gameId: string;
  hasIndexedKb: boolean;
  initialTypology?: AgentTypology;
  initialStrategy?: AgentStrategy;
  onSave?: (typology: AgentTypology, strategy: AgentStrategy) => Promise<void>;
  className?: string;
}

// ============================================================================
// Constants
// ============================================================================

const TYPOLOGIES: { value: AgentTypology; label: string; description: string; icon: string }[] = [
  {
    value: 'tutor',
    label: 'Tutor',
    description: 'Spiega regole e meccaniche in modo semplice e didattico',
    icon: '🎓',
  },
  {
    value: 'arbitro',
    label: 'Arbitro',
    description: 'Risponde a domande precise sulle regole durante il gioco',
    icon: '⚖️',
  },
  {
    value: 'strategist',
    label: 'Strategist',
    description: 'Suggerisce tattiche e ottimizzazioni per vincere',
    icon: '♟️',
  },
];

const STRATEGIES: { value: AgentStrategy; label: string }[] = [
  { value: 'balanced', label: 'Bilanciato' },
  { value: 'creative', label: 'Creativo' },
  { value: 'precise', label: 'Preciso' },
];

const COST_ESTIMATES: Record<AgentTypology, Record<AgentStrategy, string>> = {
  tutor: { balanced: '~0.01€', creative: '~0.02€', precise: '~0.01€' },
  arbitro: { balanced: '~0.01€', creative: '~0.01€', precise: '~0.02€' },
  strategist: { balanced: '~0.02€', creative: '~0.03€', precise: '~0.02€' },
};

// ============================================================================
// Typology Radio Card
// ============================================================================

function TypologyCard({
  option,
  selected,
  onSelect,
}: {
  option: (typeof TYPOLOGIES)[number];
  selected: boolean;
  onSelect: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onSelect}
      className={cn(
        'flex items-start gap-3 p-3 rounded-lg border text-left w-full transition-all duration-150',
        selected
          ? 'border-primary bg-primary/5 ring-1 ring-primary'
          : 'border-border hover:border-muted-foreground/40 hover:bg-muted/50'
      )}
      role="radio"
      aria-checked={selected}
    >
      {/* Radio indicator */}
      <div
        className={cn(
          'mt-0.5 h-4 w-4 rounded-full border-2 flex items-center justify-center shrink-0',
          selected ? 'border-primary' : 'border-muted-foreground/40'
        )}
      >
        {selected && <div className="h-2 w-2 rounded-full bg-primary" />}
      </div>
      <div>
        <div className="flex items-center gap-1.5">
          <span>{option.icon}</span>
          <span className="text-sm font-medium">{option.label}</span>
        </div>
        <p className="text-xs text-muted-foreground mt-0.5">{option.description}</p>
      </div>
    </button>
  );
}

// ============================================================================
// Main Component
// ============================================================================

export function AgentConfigForm({
  gameId,
  hasIndexedKb,
  initialTypology = 'arbitro',
  initialStrategy = 'balanced',
  onSave,
  className,
}: AgentConfigFormProps) {
  const router = useRouter();
  const [typology, setTypology] = useState<AgentTypology>(initialTypology);
  const [strategy, setStrategy] = useState<AgentStrategy>(initialStrategy);
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  const costEstimate = COST_ESTIMATES[typology][strategy];

  const handleSaveAndChat = async () => {
    if (!hasIndexedKb) return;

    setSaveError(null);
    setIsSaving(true);
    try {
      if (onSave) {
        // When onSave is provided, the caller owns navigation (e.g. after agent creation)
        await onSave(typology, strategy);
      } else {
        // Fallback: navigate directly to game chat without creating a new agent
        router.push(`/chat?gameId=${encodeURIComponent(gameId)}`);
      }
    } catch (error) {
      logger.error('Failed to save agent config:', error);
      setSaveError('Errore durante la configurazione. Riprova.');
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className={cn('flex flex-col gap-5', className)}>
      {/* Header */}
      <div>
        <h2 className="text-lg font-semibold flex items-center gap-2">
          <Sparkles className="h-5 w-5 text-primary" />
          Agente AI
        </h2>
        <p className="text-sm text-muted-foreground mt-1">
          Configura il comportamento dell&apos;assistente
        </p>
      </div>

      {/* Agent typology radio group */}
      <div>
        <Label className="text-sm font-medium mb-2 block">Tipo di agente</Label>
        <div className="flex flex-col gap-2" role="radiogroup" aria-label="Tipo di agente">
          {TYPOLOGIES.map(option => (
            <TypologyCard
              key={option.value}
              option={option}
              selected={typology === option.value}
              onSelect={() => setTypology(option.value)}
            />
          ))}
        </div>
      </div>

      {/* Strategy dropdown */}
      <div>
        <Label htmlFor="strategy-select" className="text-sm font-medium mb-2 block">
          Strategia
        </Label>
        <Select value={strategy} onValueChange={(v: string) => setStrategy(v as AgentStrategy)}>
          <SelectTrigger id="strategy-select" className="w-full">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {STRATEGIES.map(s => (
              <SelectItem key={s.value} value={s.value}>
                {s.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Cost estimate */}
      <div className="flex items-center justify-between text-sm bg-muted/50 rounded-lg px-4 py-3">
        <span className="text-muted-foreground">Stima costo per sessione</span>
        <span className="font-medium">{costEstimate}</span>
      </div>

      {/* Save error */}
      {saveError && (
        <div
          className="flex items-center gap-2 rounded-lg border border-red-200 dark:border-red-800/40 bg-red-50 dark:bg-red-900/20 px-3 py-2 text-sm text-red-700 dark:text-red-300"
          role="alert"
          data-testid="save-error"
        >
          <AlertCircle className="h-4 w-4 shrink-0" />
          {saveError}
        </div>
      )}

      {/* CTA */}
      {hasIndexedKb ? (
        <Button
          size="lg"
          onClick={handleSaveAndChat}
          disabled={isSaving}
          className="w-full"
          data-testid="save-and-chat-btn"
        >
          {isSaving ? (
            <>
              <Loader2 className="h-4 w-4 mr-2 animate-spin" />
              Configurazione…
            </>
          ) : (
            <>
              <MessageSquare className="h-4 w-4 mr-2" />
              Salva &amp; Avvia Chat →
            </>
          )}
        </Button>
      ) : (
        <div className="flex flex-col gap-2">
          <Button
            size="lg"
            disabled
            className="w-full"
            aria-describedby="no-kb-hint"
            data-testid="save-and-chat-btn-disabled"
          >
            <BotOff className="h-4 w-4 mr-2" />
            Salva &amp; Avvia Chat →
          </Button>
          <p id="no-kb-hint" className="text-xs text-muted-foreground text-center">
            Aggiungi un documento indicizzato per abilitare la chat
          </p>
        </div>
      )}
    </div>
  );
}

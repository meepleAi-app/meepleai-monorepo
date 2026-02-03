'use client';

import { Info } from 'lucide-react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Label } from '@/components/ui/primitives/label';

export interface StrategySelectorProps {
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
}

// Strategy options with descriptions
const STRATEGY_OPTIONS = [
  // RAG Strategies (Issue #3245)
  {
    value: 'FAST',
    label: 'FAST (~2K tokens)',
    description: 'Query semplici, risposte rapide. Richiede solo Synthesis.',
    category: 'rag',
  },
  {
    value: 'BALANCED',
    label: 'BALANCED (~3K tokens)',
    description: 'Query standard con validazione CRAG. Synthesis + CragEvaluation.',
    category: 'rag',
  },
  {
    value: 'PRECISE',
    label: 'PRECISE (~22K tokens)',
    description: 'Multi-agent pipeline per query critiche. 4 fasi: Retrieval, Analysis, Synthesis, Validation.',
    category: 'rag',
  },
  {
    value: 'EXPERT',
    label: 'EXPERT (~15K tokens)',
    description: 'Web search + ragionamento multi-hop. Per query che richiedono conoscenza esterna.',
    category: 'rag',
  },
  {
    value: 'CONSENSUS',
    label: 'CONSENSUS (~18K tokens)',
    description: 'Multi-LLM voting. 3 modelli diversi votano, aggregatore decide. Alta affidabilità.',
    category: 'rag',
  },
  {
    value: 'CUSTOM',
    label: 'CUSTOM (variabile)',
    description: 'Configurazione personalizzata. Minimo Synthesis, combinazione libera di fasi.',
    category: 'rag',
  },
  // Legacy retrieval strategies
  {
    value: 'HybridSearch',
    label: 'Hybrid Search',
    description: 'Combina ricerca vettoriale e keyword search per risultati bilanciati',
    category: 'retrieval',
  },
  {
    value: 'VectorOnly',
    label: 'Vector Only',
    description: 'Ricerca semantica pura basata su embeddings vettoriali',
    category: 'retrieval',
  },
  {
    value: 'KeywordOnly',
    label: 'Keyword Only',
    description: 'Ricerca tradizionale basata su keyword matching',
    category: 'retrieval',
  },
  {
    value: 'MultiModel',
    label: 'Multi-Model',
    description: 'Utilizza più modelli di embedding per risultati più accurati',
    category: 'retrieval',
  },
  {
    value: 'Reranked',
    label: 'Reranked',
    description: 'Applica reranking ai risultati per migliorare la rilevanza',
    category: 'retrieval',
  },
] as const;

/**
 * Strategy selector component with descriptions and tooltips
 */
export function StrategySelector({ value, onChange, disabled = false }: StrategySelectorProps) {
  const selectedStrategy = STRATEGY_OPTIONS.find(opt => opt.value === value);

  return (
    <div className="space-y-2">
      <div className="flex items-center gap-2">
        <Label htmlFor="strategy">Strategia RAG *</Label>
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger>
              <Info className="h-4 w-4 text-muted-foreground" />
            </TooltipTrigger>
            <TooltipContent className="max-w-xs">
              <p>
                La strategia determina come l&apos;AI recupera le informazioni dal knowledge base.
                Diverse strategie sono ottimali per diversi tipi di domande.
              </p>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      </div>

      <Select value={value} onValueChange={onChange} disabled={disabled}>
        <SelectTrigger id="strategy">
          <SelectValue placeholder="Seleziona una strategia RAG" />
        </SelectTrigger>
        <SelectContent>
          {/* RAG Strategies (Issue #3245) */}
          <div className="px-2 py-1.5 text-xs font-semibold text-muted-foreground">Strategie RAG</div>
          {STRATEGY_OPTIONS.filter(opt => opt.category === 'rag').map(option => (
            <SelectItem key={option.value} value={option.value}>
              <div className="flex flex-col">
                <span className="font-medium">{option.label}</span>
                <span className="text-xs text-muted-foreground">{option.description}</span>
              </div>
            </SelectItem>
          ))}
          {/* Legacy Retrieval Strategies */}
          <div className="px-2 py-1.5 text-xs font-semibold text-muted-foreground mt-2 border-t pt-2">
            Strategie Retrieval (legacy)
          </div>
          {STRATEGY_OPTIONS.filter(opt => opt.category === 'retrieval').map(option => (
            <SelectItem key={option.value} value={option.value}>
              <div className="flex flex-col">
                <span className="font-medium">{option.label}</span>
                <span className="text-xs text-muted-foreground">{option.description}</span>
              </div>
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      {selectedStrategy && (
        <p className="text-sm text-muted-foreground">{selectedStrategy.description}</p>
      )}
    </div>
  );
}

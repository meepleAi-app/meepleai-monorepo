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
  {
    value: 'HybridSearch',
    label: 'Hybrid Search',
    description: 'Combina ricerca vettoriale e keyword search per risultati bilanciati',
  },
  {
    value: 'VectorOnly',
    label: 'Vector Only',
    description: 'Ricerca semantica pura basata su embeddings vettoriali',
  },
  {
    value: 'KeywordOnly',
    label: 'Keyword Only',
    description: 'Ricerca tradizionale basata su keyword matching',
  },
  {
    value: 'MultiModel',
    label: 'Multi-Model',
    description: 'Utilizza più modelli di embedding per risultati più accurati',
  },
  {
    value: 'Reranked',
    label: 'Reranked',
    description: 'Applica reranking ai risultati per migliorare la rilevanza',
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
          {STRATEGY_OPTIONS.map(option => (
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

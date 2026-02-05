/**
 * ConfigSelector Component
 * Issue #3380
 *
 * Selects strategy and model for a comparison configuration slot.
 */

'use client';

import { Trash2, Zap, Scale, Target, Sparkles, Users } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

interface Strategy {
  value: string;
  label: string;
  description: string;
  color: string;
}

interface Model {
  value: string;
  label: string;
  tier: string;
}

interface ComparisonConfig {
  id: string;
  strategy: string;
  model: string;
}

export interface ConfigSelectorProps {
  config: ComparisonConfig;
  index: number;
  strategies: readonly Strategy[];
  models: readonly Model[];
  onUpdate: (id: string, field: 'strategy' | 'model', value: string) => void;
  onRemove?: (id: string) => void;
}

const STRATEGY_ICONS: Record<string, React.ReactNode> = {
  FAST: <Zap className="h-4 w-4" />,
  BALANCED: <Scale className="h-4 w-4" />,
  PRECISE: <Target className="h-4 w-4" />,
  EXPERT: <Sparkles className="h-4 w-4" />,
  CONSENSUS: <Users className="h-4 w-4" />,
};

const STRATEGY_COLORS: Record<string, string> = {
  FAST: 'border-green-500/50 bg-green-500/5',
  BALANCED: 'border-blue-500/50 bg-blue-500/5',
  PRECISE: 'border-purple-500/50 bg-purple-500/5',
  EXPERT: 'border-orange-500/50 bg-orange-500/5',
  CONSENSUS: 'border-red-500/50 bg-red-500/5',
};

const CONFIG_LABELS = ['A', 'B', 'C', 'D'];

export function ConfigSelector({
  config,
  index,
  strategies,
  models,
  onUpdate,
  onRemove,
}: ConfigSelectorProps) {
  const selectedStrategy = strategies.find(s => s.value === config.strategy);
  const selectedModel = models.find(m => m.value === config.model);

  return (
    <Card className={cn('border-2', STRATEGY_COLORS[config.strategy] ?? 'border-muted')}>
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Badge
              variant="outline"
              className={cn(
                'font-bold text-lg px-3 py-1',
                config.strategy === 'FAST' && 'border-green-500 text-green-500',
                config.strategy === 'BALANCED' && 'border-blue-500 text-blue-500',
                config.strategy === 'PRECISE' && 'border-purple-500 text-purple-500',
                config.strategy === 'EXPERT' && 'border-orange-500 text-orange-500',
                config.strategy === 'CONSENSUS' && 'border-red-500 text-red-500'
              )}
            >
              {CONFIG_LABELS[index]}
            </Badge>
            <CardTitle className="text-base">Config {CONFIG_LABELS[index]}</CardTitle>
          </div>
          {onRemove && (
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8 text-muted-foreground hover:text-destructive"
              onClick={() => onRemove(config.id)}
            >
              <Trash2 className="h-4 w-4" />
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Strategy Selector */}
        <div className="space-y-2">
          <label className="text-sm font-medium">Strategy</label>
          <Select
            value={config.strategy}
            onValueChange={(value) => onUpdate(config.id, 'strategy', value)}
          >
            <SelectTrigger>
              <SelectValue>
                <div className="flex items-center gap-2">
                  {STRATEGY_ICONS[config.strategy]}
                  <span>{selectedStrategy?.label ?? config.strategy}</span>
                </div>
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              {strategies.map(strategy => (
                <SelectItem key={strategy.value} value={strategy.value}>
                  <div className="flex items-center gap-2">
                    {STRATEGY_ICONS[strategy.value]}
                    <div className="flex flex-col">
                      <span>{strategy.label}</span>
                      <span className="text-xs text-muted-foreground">
                        {strategy.description}
                      </span>
                    </div>
                  </div>
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* Model Selector */}
        <div className="space-y-2">
          <label className="text-sm font-medium">Model</label>
          <Select
            value={config.model}
            onValueChange={(value) => onUpdate(config.id, 'model', value)}
          >
            <SelectTrigger>
              <SelectValue>
                <div className="flex items-center gap-2">
                  <span>{selectedModel?.label ?? config.model}</span>
                  <Badge variant="outline" className="text-xs">
                    {selectedModel?.tier ?? 'unknown'}
                  </Badge>
                </div>
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              {models.map(model => (
                <SelectItem key={model.value} value={model.value}>
                  <div className="flex items-center gap-2">
                    <span>{model.label}</span>
                    <Badge variant="outline" className="text-xs">
                      {model.tier}
                    </Badge>
                  </div>
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* Summary */}
        <div className="pt-2 text-xs text-muted-foreground">
          <p>{selectedStrategy?.description}</p>
        </div>
      </CardContent>
    </Card>
  );
}

/**
 * LlmProviderSelector Component - Issue #2391 Sprint 2
 *
 * Selector for LLM provider (OpenRouter cloud vs Ollama local).
 */

'use client';

import { Cloud, Server } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { cn } from '@/lib/utils';

export interface LlmProviderSelectorProps {
  value: 'OpenRouter' | 'Ollama';
  onChange: (provider: 'OpenRouter' | 'Ollama') => void;
  disabled?: boolean;
}

const PROVIDER_CONFIG = {
  OpenRouter: {
    icon: Cloud,
    label: 'OpenRouter',
    description: 'Cloud-based, accesso a modelli multipli (GPT-4, Claude, etc.)',
    color: 'text-blue-500',
  },
  Ollama: {
    icon: Server,
    label: 'Ollama',
    description: 'Self-hosted locale, privacy-focused, zero costi',
    color: 'text-green-500',
  },
} as const;

export function LlmProviderSelector({
  value,
  onChange,
  disabled = false,
}: LlmProviderSelectorProps) {
  return (
    <div className="space-y-4">
      <Label>Provider LLM</Label>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {Object.entries(PROVIDER_CONFIG).map(([provider, config]) => {
          const Icon = config.icon;
          const isSelected = value === provider;

          return (
            <Button
              key={provider}
              type="button"
              variant="outline"
              onClick={() => !disabled && onChange(provider as 'OpenRouter' | 'Ollama')}
              disabled={disabled}
              className="h-auto p-0"
            >
              <Card
                className={cn(
                  'w-full transition-all border-0',
                  isSelected && 'ring-2 ring-primary',
                  disabled && 'opacity-50'
                )}
              >
                <CardHeader className="pb-3">
                  <div className="flex items-center gap-3">
                    <Icon className={cn('h-5 w-5', config.color)} />
                    <CardTitle className="text-base">{config.label}</CardTitle>
                  </div>
                </CardHeader>
                <CardContent>
                  <CardDescription className="text-left">{config.description}</CardDescription>
                </CardContent>
              </Card>
            </Button>
          );
        })}
      </div>
    </div>
  );
}

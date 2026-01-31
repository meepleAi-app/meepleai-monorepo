/**
 * AgentModeSelector Component - Issue #2391 Sprint 2
 *
 * Visual selector for agent operation modes (Chat, Player, Ledger).
 */

'use client';

import { MessageCircle, Gamepad2, BookOpen } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { Label } from '@/components/ui/primitives/label';
import { cn } from '@/lib/utils';

export interface AgentModeSelectorProps {
  value: 'Chat' | 'Player' | 'Ledger';
  onChange: (mode: 'Chat' | 'Player' | 'Ledger') => void;
  disabled?: boolean;
}

const MODE_CONFIG = {
  Chat: {
    icon: MessageCircle,
    label: 'Chat Mode',
    description: 'Risponde a domande sulle regole del gioco',
    color: 'text-blue-500',
  },
  Player: {
    icon: Gamepad2,
    label: 'Player Mode',
    description: 'Suggerisce mosse ottimali durante la partita',
    color: 'text-green-500',
  },
  Ledger: {
    icon: BookOpen,
    label: 'Ledger Mode',
    description: 'Traccia lo stato completo del gioco dalla conversazione',
    color: 'text-purple-500',
  },
} as const;

export function AgentModeSelector({ value, onChange, disabled = false }: AgentModeSelectorProps) {
  return (
    <div className="space-y-4">
      <Label>Modalità Agente</Label>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {Object.entries(MODE_CONFIG).map(([mode, config]) => {
          const Icon = config.icon;
          const isSelected = value === mode;

          return (
            <Button
              key={mode}
              type="button"
              variant="outline"
              onClick={() => !disabled && onChange(mode as 'Chat' | 'Player' | 'Ledger')}
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

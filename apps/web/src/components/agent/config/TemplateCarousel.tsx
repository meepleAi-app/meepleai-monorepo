/**
 * Template Carousel - Horizontal scroll template selector
 * Issue #3239 (FRONT-003)
 */

'use client';

import { Info } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/primitives/button';
import { useAgentStore } from '@/stores/agentStore';

interface Template {
  id: string;
  name: string;
  icon: string;
  description: string;
}

export function TemplateCarousel() {
  const { selectedTypologyId, setSelectedTypology } = useAgentStore();
  const [showInfo, setShowInfo] = useState<string | null>(null);

  const templates: Template[] = [
    { id: '1', name: 'Rules Helper', icon: '📖', description: 'Answer rules questions' },
    { id: '2', name: 'Strategy Guide', icon: '🎯', description: 'Provide strategy tips' },
    { id: '3', name: 'Setup Assistant', icon: '🛠️', description: 'Help with game setup' },
  ];

  return (
    <div className="space-y-3">
      <label className="block text-sm font-medium text-slate-200">
        Agent Template
        <span className="ml-1 text-red-400">*</span>
      </label>

      <div className="relative">
        <div className="flex gap-3 overflow-x-auto pb-2 snap-x snap-mandatory scrollbar-thin">
          {templates.map(template => (
            <button
              key={template.id}
              onClick={() => setSelectedTypology(template.id)}
              className={`
                relative min-w-[120px] snap-center rounded-lg border-2 p-4 transition-all
                ${
                  selectedTypologyId === template.id
                    ? 'border-cyan-400 bg-cyan-500/10 shadow-[0_0_20px_rgba(0,255,255,0.3)] agent-pulse-cyan'
                    : 'border-slate-700 bg-slate-900 hover:border-slate-600'
                }
              `}
            >
              <div className="text-3xl mb-2">{template.icon}</div>
              <div className="text-sm font-medium text-white">{template.name}</div>

              <Button
                variant="ghost"
                size="icon"
                className="absolute top-1 right-1 h-6 w-6"
                onClick={e => {
                  e.stopPropagation();
                  setShowInfo(template.id);
                }}
              >
                <Info className="h-3 w-3" />
              </Button>
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

'use client';

import React, { useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { Layers, Copy, Filter } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { GameToolkitTemplateDto } from '@/lib/api/schemas/toolkit.schemas';

const CATEGORIES = ['All', 'Strategy', 'Party', 'CardGames', 'Cooperative'];

function TemplateCard({
  template,
  onClone: _onClone,
}: {
  template: GameToolkitTemplateDto;
  onClone: (templateId: string) => void;
}) {
  const toolCount =
    template.diceTools.length +
    template.cardTools.length +
    template.timerTools.length +
    template.counterTools.length;

  return (
    <div
      className="flex flex-col gap-3 rounded-lg border bg-card p-4 shadow-sm"
      data-testid="template-card"
    >
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-2">
          <Layers className="h-5 w-5 text-muted-foreground" />
          <h3 className="font-semibold">{template.name}</h3>
        </div>
        {template.stateTemplate?.category && (
          <Badge variant="secondary" className="text-xs">
            {template.stateTemplate.category}
          </Badge>
        )}
      </div>

      {template.stateTemplate?.description && (
        <p className="text-sm text-muted-foreground line-clamp-2">
          {template.stateTemplate.description}
        </p>
      )}

      <div className="flex flex-wrap gap-1.5">
        {template.diceTools.length > 0 && (
          <Badge variant="outline" className="text-xs">
            {template.diceTools.length} dice
          </Badge>
        )}
        {template.cardTools.length > 0 && (
          <Badge variant="outline" className="text-xs">
            {template.cardTools.length} cards
          </Badge>
        )}
        {template.timerTools.length > 0 && (
          <Badge variant="outline" className="text-xs">
            {template.timerTools.length} timers
          </Badge>
        )}
        {template.counterTools.length > 0 && (
          <Badge variant="outline" className="text-xs">
            {template.counterTools.length} counters
          </Badge>
        )}
        {toolCount === 0 && (
          <Badge variant="outline" className="text-xs text-muted-foreground">
            No tools configured
          </Badge>
        )}
      </div>

      <Button size="sm" className="mt-auto w-full" disabled title="Coming soon">
        <Copy className="mr-1.5 h-3.5 w-3.5" />
        Use This Template
      </Button>
    </div>
  );
}

export default function ToolkitTemplatesPage() {
  const [category, setCategory] = useState('All');

  const { data: templates, isLoading } = useQuery({
    queryKey: ['toolkit-templates', category],
    queryFn: () => api.gameToolkit.getApprovedTemplates(category === 'All' ? undefined : category),
  });

  const handleClone = (_templateId: string) => {
    // Clone modal will be implemented when game selection UX is ready
  };

  return (
    <div className="container max-w-6xl space-y-6 py-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Toolkit Templates</h1>
          <p className="text-sm text-muted-foreground">
            Browse approved templates and clone them for your games
          </p>
        </div>
      </div>

      <div className="flex items-center gap-3">
        <Filter className="h-4 w-4 text-muted-foreground" />
        <Select value={category} onValueChange={setCategory}>
          <SelectTrigger className="w-48" aria-label="Filter by category">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {CATEGORIES.map(c => (
              <SelectItem key={c} value={c}>
                {c}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {isLoading && <p className="text-sm text-muted-foreground">Loading templates...</p>}

      {!isLoading && templates?.length === 0 && (
        <p className="text-sm text-muted-foreground">No approved templates found.</p>
      )}

      <div
        className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3"
        data-testid="templates-grid"
      >
        {templates?.map(t => (
          <TemplateCard key={t.id} template={t} onClone={handleClone} />
        ))}
      </div>
    </div>
  );
}

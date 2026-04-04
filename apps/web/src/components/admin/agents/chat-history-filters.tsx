'use client';

import { useState } from 'react';

import { DownloadIcon } from 'lucide-react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';

export interface ChatHistoryFiltersProps {
  onAgentChange?: (value: string) => void;
  onDateChange?: (value: string) => void;
  onSatisfactionChange?: (value: string) => void;
  onExport?: () => void;
}

export function ChatHistoryFilters({
  onAgentChange,
  onDateChange,
  onSatisfactionChange,
  onExport,
}: ChatHistoryFiltersProps) {
  const [agent, setAgent] = useState('all');
  const [date, setDate] = useState('');
  const [minSatisfaction, setMinSatisfaction] = useState('all');

  const handleAgentChange = (value: string) => {
    setAgent(value);
    onAgentChange?.(value);
  };

  const handleDateChange = (value: string) => {
    setDate(value);
    onDateChange?.(value);
  };

  const handleSatisfactionChange = (value: string) => {
    setMinSatisfaction(value);
    onSatisfactionChange?.(value);
  };

  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-white/20 dark:border-zinc-700/40 shadow-lg">
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        {/* Agent */}
        <div>
          <Label htmlFor="agent-filter" className="text-sm font-semibold mb-2">
            Agent
          </Label>
          <Select value={agent} onValueChange={handleAgentChange}>
            <SelectTrigger id="agent-filter" className="bg-white/90 dark:bg-zinc-900">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Agents</SelectItem>
              <SelectItem value="rules">Rules Expert</SelectItem>
              <SelectItem value="strategy">Strategy Advisor</SelectItem>
              <SelectItem value="recommender">Game Recommender</SelectItem>
              <SelectItem value="faq">FAQ Assistant</SelectItem>
            </SelectContent>
          </Select>
        </div>

        {/* Date Range */}
        <div>
          <Label htmlFor="date-filter" className="text-sm font-semibold mb-2">
            Date Range
          </Label>
          <Input
            id="date-filter"
            type="date"
            value={date}
            onChange={e => handleDateChange(e.target.value)}
            className="bg-white/90 dark:bg-zinc-900"
          />
        </div>

        {/* Min Satisfaction */}
        <div>
          <Label htmlFor="satisfaction-filter" className="text-sm font-semibold mb-2">
            Min Satisfaction
          </Label>
          <Select value={minSatisfaction} onValueChange={handleSatisfactionChange}>
            <SelectTrigger id="satisfaction-filter" className="bg-white/90 dark:bg-zinc-900">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Any Rating</SelectItem>
              <SelectItem value="5">5 Stars</SelectItem>
              <SelectItem value="4">4+ Stars</SelectItem>
              <SelectItem value="3">3+ Stars</SelectItem>
            </SelectContent>
          </Select>
        </div>

        {/* Export Button */}
        <div className="flex items-end">
          <Button
            onClick={onExport}
            className="w-full bg-amber-100 hover:bg-amber-200 text-amber-900 dark:bg-amber-900/30 dark:hover:bg-amber-900/50 dark:text-amber-300"
          >
            <DownloadIcon className="h-4 w-4 mr-2" />
            Export CSV
          </Button>
        </div>
      </div>
    </div>
  );
}

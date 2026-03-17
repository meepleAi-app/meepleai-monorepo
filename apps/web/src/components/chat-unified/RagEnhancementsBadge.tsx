'use client';

import { useQuery } from '@tanstack/react-query';
import { Sparkles } from 'lucide-react';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/data-display/tooltip';
import { HttpClient } from '@/lib/api/core/httpClient';
import { cn } from '@/lib/utils';

const httpClient = new HttpClient();

interface EnhancementEstimate {
  activeEnhancements: string;
  activeFlags: string[];
  extraCreditsPerQuery: number;
  useBalancedAuxModel: boolean;
}

export function RagEnhancementsBadge() {
  const { data } = useQuery({
    queryKey: ['rag', 'enhancements', 'estimate'],
    queryFn: () => httpClient.get<EnhancementEstimate>('/api/v1/rag/enhancements/estimate'),
    staleTime: 60_000, // Refresh every minute
    retry: false, // Don't retry if endpoint doesn't exist yet
  });

  // Don't render if no enhancements active or fetch failed
  if (!data || data.activeFlags.length === 0) return null;

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <div
            className={cn(
              'flex items-center gap-1.5 px-2.5 py-1 rounded-lg',
              'bg-amber-50 dark:bg-amber-900/20 border border-amber-200/50 dark:border-amber-800/50',
              'text-xs text-amber-700 dark:text-amber-300',
              'transition-opacity duration-200'
            )}
          >
            <Sparkles className="h-3 w-3" />
            <span>
              {data.activeFlags.length} enhancement{data.activeFlags.length > 1 ? 's' : ''}
              {data.extraCreditsPerQuery > 0 && (
                <span className="ml-1 font-medium">+{data.extraCreditsPerQuery} credits</span>
              )}
            </span>
          </div>
        </TooltipTrigger>
        <TooltipContent side="top" className="max-w-xs">
          <div className="space-y-1">
            <p className="font-medium text-xs">RAG Enhancements attivi:</p>
            <ul className="text-xs space-y-0.5">
              {data.activeFlags.map(flag => (
                <li key={flag} className="flex items-center gap-1">
                  <span className="h-1.5 w-1.5 rounded-full bg-green-400" />
                  {flag}
                </li>
              ))}
            </ul>
            {data.extraCreditsPerQuery > 0 && (
              <p className="text-xs text-muted-foreground pt-1 border-t">
                +{data.extraCreditsPerQuery} crediti extra per query (
                {data.useBalancedAuxModel ? 'BALANCED' : 'FAST'} model)
              </p>
            )}
            {data.extraCreditsPerQuery === 0 && (
              <p className="text-xs text-green-600 dark:text-green-400 pt-1 border-t">
                Nessun costo aggiuntivo (FAST model)
              </p>
            )}
          </div>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}

'use client';

/**
 * Parallel Agent Execution Node - Specialized ReactFlow node
 *
 * Enhanced visualization for parallel agent execution with result merging.
 * Shows: agent count, merge strategy.
 *
 * @see #3466 - Implement Tier 3-4 experimental blocks
 */

import { memo } from 'react';

import { GitMerge, Users, Zap } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function ParallelAgentNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const agentCount = (params.agentCount as number) || 3;
  const mergeStrategy = (params.mergeStrategy as string) || 'vote';

  // Merge strategy display
  const mergeLabels: Record<string, { label: string; icon: string }> = {
    vote: { label: 'Majority Vote', icon: '🗳️' },
    concatenate: { label: 'Concatenate', icon: '📎' },
    'llm-synthesis': { label: 'LLM Synthesis', icon: '🧠' },
  };

  const mergeInfo = mergeLabels[mergeStrategy] || mergeLabels.vote;

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <Zap className="h-3.5 w-3.5 text-purple-600" />
          <span className="text-sm font-medium">Parallel Agent</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Multiple agents work simultaneously, merge results
          </p>

          {/* Parameters */}
          <div className="grid grid-cols-2 gap-2 text-xs">
            <div className="flex items-center gap-1">
              <Users className="h-3 w-3 text-muted-foreground" />
              <span className="text-muted-foreground">Agents:</span>
              <Badge variant="outline" className="text-[10px] px-1">
                {agentCount}
              </Badge>
            </div>
            <div className="flex items-center gap-1">
              <GitMerge className="h-3 w-3 text-muted-foreground" />
              <Badge variant="secondary" className="text-[10px] px-1 truncate">
                {mergeInfo.icon}
              </Badge>
            </div>
          </div>

          {/* Visual parallel execution */}
          <div className="p-2 bg-muted/50 rounded">
            <div className="flex items-center justify-center gap-2">
              <div className="flex flex-col gap-0.5">
                {Array.from({ length: Math.min(agentCount, 4) }).map((_, i) => (
                  <div
                    key={i}
                    className="w-14 h-4 rounded bg-purple-100 dark:bg-purple-900/30 flex items-center justify-center text-[8px] font-medium text-purple-600"
                  >
                    Agent {i + 1}
                  </div>
                ))}
                {agentCount > 4 && (
                  <div className="text-[8px] text-muted-foreground text-center">
                    +{agentCount - 4} more
                  </div>
                )}
              </div>
              <GitMerge className="h-4 w-4 text-purple-500" />
              <div className="w-14 h-6 rounded bg-purple-200 dark:bg-purple-800/50 flex items-center justify-center text-[8px] font-medium text-purple-700 dark:text-purple-300">
                Result
              </div>
            </div>
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-purple-600 font-medium">⚡ Parallel</span>
          <span className="text-muted-foreground">~3s • ~12k tok</span>
        </div>
      }
    />
  );
}

export const ParallelAgentNode = memo(ParallelAgentNodeComponent);
export default ParallelAgentNode;

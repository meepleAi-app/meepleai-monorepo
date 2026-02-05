'use client';

/**
 * Sequential Agent Chain Node - Specialized ReactFlow node
 *
 * Enhanced visualization for sequential agent execution.
 * Shows: agent chain configuration, handoff mode.
 *
 * @see #3466 - Implement Tier 3-4 experimental blocks
 */

import { memo } from 'react';

import { ArrowRight, GitBranch, Users } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function SequentialAgentNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const agents = (params.agents as string) || 'analyzer-synthesizer';
  const handoff = (params.handoff as string) || 'automatic';

  // Parse agent chain for display
  const agentLabels: Record<string, string[]> = {
    'analyzer-synthesizer': ['Analyzer', 'Synthesizer'],
    'analyzer-validator-synthesizer': ['Analyzer', 'Validator', 'Synthesizer'],
    'planner-executor-validator': ['Planner', 'Executor', 'Validator'],
  };

  const agentList = agentLabels[agents] || ['Agent 1', 'Agent 2'];

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <Users className="h-3.5 w-3.5 text-purple-600" />
          <span className="text-sm font-medium">Sequential Agent</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Agents execute in sequence, each processes previous output
          </p>

          {/* Handoff mode indicator */}
          <div className="flex items-center gap-2 text-xs">
            <GitBranch className="h-3 w-3 text-muted-foreground" />
            <span className="text-muted-foreground">Handoff:</span>
            <Badge
              variant={handoff === 'automatic' ? 'default' : 'secondary'}
              className="text-[10px] px-1"
            >
              {handoff === 'automatic' ? '⚡ Auto' : '🔀 Conditional'}
            </Badge>
          </div>

          {/* Visual agent chain */}
          <div className="p-2 bg-muted/50 rounded">
            <div className="text-[10px] text-muted-foreground mb-1">Chain:</div>
            <div className="flex items-center gap-1 flex-wrap">
              {agentList.map((agent, i) => (
                <div key={i} className="flex items-center">
                  <Badge
                    variant="outline"
                    className="text-[9px] px-1.5 py-0.5 bg-purple-50 dark:bg-purple-900/30 border-purple-200 dark:border-purple-800"
                  >
                    {agent}
                  </Badge>
                  {i < agentList.length - 1 && (
                    <ArrowRight className="h-3 w-3 text-purple-400 mx-0.5" />
                  )}
                </div>
              ))}
            </div>
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-purple-600 font-medium">➡️ Sequential</span>
          <span className="text-muted-foreground">~5s • ~8k tok</span>
        </div>
      }
    />
  );
}

export const SequentialAgentNode = memo(SequentialAgentNodeComponent);
export default SequentialAgentNode;

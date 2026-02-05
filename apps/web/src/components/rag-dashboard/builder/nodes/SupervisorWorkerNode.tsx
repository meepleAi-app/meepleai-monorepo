'use client';

/**
 * Supervisor-Worker Node - Specialized ReactFlow node
 *
 * Enhanced visualization for supervisor routing to specialized workers.
 * Shows: worker types, routing logic.
 *
 * @see #3466 - Implement Tier 3-4 experimental blocks
 */

import { memo } from 'react';

import { Network, Route, UserCog } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function SupervisorWorkerNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const workers = (params.workers as string) || 'rules-strategy-setup';
  const routingLogic = (params.routingLogic as string) || 'classification';

  // Worker configurations
  const workerLabels: Record<string, string[]> = {
    'rules-strategy-setup': ['Rules', 'Strategy', 'Setup'],
    'faq-rules-expert': ['FAQ', 'Rules', 'Expert'],
    custom: ['Custom'],
  };

  const workerList = workerLabels[workers] || ['Worker 1', 'Worker 2', 'Worker 3'];

  // Routing display
  const routingLabels: Record<string, string> = {
    classification: 'Classification',
    confidence: 'Confidence',
    hybrid: 'Hybrid',
  };

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <UserCog className="h-3.5 w-3.5 text-purple-600" />
          <span className="text-sm font-medium">Supervisor-Worker</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Coordinator routes tasks to specialized workers
          </p>

          {/* Routing logic indicator */}
          <div className="flex items-center gap-2 text-xs">
            <Route className="h-3 w-3 text-muted-foreground" />
            <span className="text-muted-foreground">Routing:</span>
            <Badge variant="secondary" className="text-[10px] px-1">
              {routingLabels[routingLogic] || routingLogic}
            </Badge>
          </div>

          {/* Visual hierarchy */}
          <div className="p-2 bg-muted/50 rounded">
            <div className="flex flex-col items-center gap-1">
              {/* Supervisor */}
              <Badge
                variant="default"
                className="text-[9px] px-2 py-0.5 bg-purple-600"
              >
                👔 Supervisor
              </Badge>

              {/* Connection lines */}
              <div className="flex items-center gap-0.5">
                <Network className="h-3 w-3 text-purple-400" />
              </div>

              {/* Workers */}
              <div className="flex gap-1 flex-wrap justify-center">
                {workerList.map((worker, i) => (
                  <Badge
                    key={i}
                    variant="outline"
                    className="text-[8px] px-1.5 py-0 bg-purple-50 dark:bg-purple-900/30"
                  >
                    {worker}
                  </Badge>
                ))}
              </div>
            </div>
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-purple-600 font-medium">👔 Orchestrate</span>
          <span className="text-muted-foreground">~6s • ~10k tok</span>
        </div>
      }
    />
  );
}

export const SupervisorWorkerNode = memo(SupervisorWorkerNodeComponent);
export default SupervisorWorkerNode;

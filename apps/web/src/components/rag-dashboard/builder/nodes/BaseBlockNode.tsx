'use client';

/**
 * Base Block Node Component
 *
 * Shared base component for all specialized RAG block nodes.
 * Provides consistent styling, ports, and interaction patterns.
 *
 * @see #3458 - Implement Tier 1 blocks (7 essential RAG blocks)
 */

import { memo, useCallback, type ReactNode } from 'react';

import { Handle, Position, useReactFlow } from '@xyflow/react';
import {
  Settings,
  Trash2,
  AlertCircle,
  CheckCircle,
  Loader2,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import type {
  RagNodeData,
  ConnectionPort,
  NodeStatus,
  RagBlock,
  NodeMetrics,
} from '../types';

// =============================================================================
// Types
// =============================================================================

export interface BaseBlockNodeProps {
  id: string;
  data: RagNodeData;
  selected?: boolean;
  /** Custom header content */
  headerContent?: ReactNode;
  /** Custom body content */
  bodyContent?: ReactNode;
  /** Custom footer content */
  footerContent?: ReactNode;
  /** Callback when settings clicked */
  onSettingsClick?: (nodeId: string) => void;
}

// =============================================================================
// Status Indicator
// =============================================================================

function StatusIndicator({ status }: { status: NodeStatus }) {
  const statusConfig = {
    idle: { icon: null, color: 'bg-muted', label: 'Idle' },
    running: {
      icon: <Loader2 className="h-3 w-3 animate-spin" />,
      color: 'bg-blue-500',
      label: 'Running',
    },
    success: {
      icon: <CheckCircle className="h-3 w-3" />,
      color: 'bg-green-500',
      label: 'Success',
    },
    error: {
      icon: <AlertCircle className="h-3 w-3" />,
      color: 'bg-red-500',
      label: 'Error',
    },
    warning: {
      icon: <AlertCircle className="h-3 w-3" />,
      color: 'bg-yellow-500',
      label: 'Warning',
    },
    disabled: { icon: null, color: 'bg-muted-foreground/30', label: 'Disabled' },
  };

  const config = statusConfig[status];

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <div
            className={cn(
              'absolute -top-1 -right-1 w-4 h-4 rounded-full flex items-center justify-center text-white',
              config.color
            )}
          >
            {config.icon}
          </div>
        </TooltipTrigger>
        <TooltipContent side="top" className="text-xs">
          {config.label}
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}

// =============================================================================
// Port Handle
// =============================================================================

interface PortHandleProps {
  port: ConnectionPort;
  isConnectable: boolean;
}

const DATA_TYPE_COLORS: Record<string, string> = {
  query: 'bg-blue-500',
  documents: 'bg-green-500',
  embeddings: 'bg-purple-500',
  scores: 'bg-yellow-500',
  response: 'bg-orange-500',
  evaluation: 'bg-red-500',
  any: 'bg-gray-500',
};

function PortHandle({ port, isConnectable }: PortHandleProps) {
  const positionMap: Record<ConnectionPort['position'], Position> = {
    top: Position.Top,
    right: Position.Right,
    bottom: Position.Bottom,
    left: Position.Left,
  };

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <Handle
            type={port.type === 'input' ? 'target' : 'source'}
            position={positionMap[port.position]}
            id={port.id}
            isConnectable={isConnectable}
            className={cn(
              '!w-3 !h-3 !border-2 !border-background',
              DATA_TYPE_COLORS[port.dataType] || 'bg-gray-500'
            )}
          />
        </TooltipTrigger>
        <TooltipContent
          side={port.position === 'left' ? 'left' : 'right'}
          className="text-xs"
        >
          <p className="font-medium">{port.name}</p>
          <p className="text-muted-foreground">{port.dataType}</p>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}

// =============================================================================
// Main Component
// =============================================================================

function BaseBlockNodeComponent({
  id,
  data,
  selected,
  headerContent,
  bodyContent,
  footerContent,
  onSettingsClick,
}: BaseBlockNodeProps) {
  const block = data.block as RagBlock;
  const status = data.status as NodeStatus;
  const metrics = data.metrics as NodeMetrics | undefined;
  const { setNodes } = useReactFlow();

  const handleDelete = useCallback(() => {
    setNodes((nodes) => nodes.filter((n) => n.id !== id));
  }, [id, setNodes]);

  const handleSettings = useCallback(() => {
    onSettingsClick?.(id);
  }, [id, onSettingsClick]);

  return (
    <div
      className={cn(
        'relative min-w-[200px] max-w-[280px] bg-card rounded-lg border-2 shadow-md transition-all',
        selected ? 'border-primary ring-2 ring-primary/20' : 'border-border',
        status === 'disabled' && 'opacity-60'
      )}
      style={{
        borderTopColor: block.color,
        borderTopWidth: '4px',
      }}
    >
      {/* Status Indicator */}
      <StatusIndicator status={status} />

      {/* Header */}
      <div className="px-3 py-2 border-b flex items-center gap-2">
        <span className="text-lg flex-shrink-0" aria-hidden="true">
          {block.icon}
        </span>
        <div className="flex-1 min-w-0">
          {headerContent || (
            <p className="text-sm font-medium truncate">{block.name}</p>
          )}
        </div>
        <div className="flex items-center gap-1 flex-shrink-0">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-6 w-6"
                  onClick={(e) => {
                    e.stopPropagation();
                    handleSettings();
                  }}
                >
                  <Settings className="h-3.5 w-3.5" />
                </Button>
              </TooltipTrigger>
              <TooltipContent side="top">Configure</TooltipContent>
            </Tooltip>
          </TooltipProvider>
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-6 w-6 text-destructive hover:text-destructive"
                  onClick={(e) => {
                    e.stopPropagation();
                    handleDelete();
                  }}
                >
                  <Trash2 className="h-3.5 w-3.5" />
                </Button>
              </TooltipTrigger>
              <TooltipContent side="top">Delete</TooltipContent>
            </Tooltip>
          </TooltipProvider>
        </div>
      </div>

      {/* Body */}
      <div className="px-3 py-2 space-y-2">
        {bodyContent || (
          <p className="text-xs text-muted-foreground line-clamp-2">
            {block.description}
          </p>
        )}

        {/* Metrics or Estimates */}
        {metrics ? (
          <div className="flex gap-2 text-xs">
            <Badge variant="secondary" className="text-[10px]">
              {metrics.tokensUsed} tok
            </Badge>
            <Badge variant="secondary" className="text-[10px]">
              {metrics.latencyMs}ms
            </Badge>
          </div>
        ) : (
          <div className="flex gap-2 text-[10px] text-muted-foreground">
            <span>~{block.estimatedTokens} tok</span>
            <span>~{block.estimatedLatencyMs}ms</span>
            <span>${block.estimatedCost.toFixed(4)}</span>
          </div>
        )}
      </div>

      {/* Footer */}
      {footerContent && (
        <div className="px-3 py-2 border-t bg-muted/30">{footerContent}</div>
      )}

      {/* Input Ports */}
      {block.inputs.map((port: ConnectionPort) => (
        <PortHandle key={port.id} port={port} isConnectable={true} />
      ))}

      {/* Output Ports */}
      {block.outputs.map((port: ConnectionPort) => (
        <PortHandle key={port.id} port={port} isConnectable={true} />
      ))}
    </div>
  );
}

export const BaseBlockNode = memo(BaseBlockNodeComponent);
export default BaseBlockNode;

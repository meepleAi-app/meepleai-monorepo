/**
 * Agent Status Badge Component
 * Epic #4068 - Issue #4184
 *
 * Displays agent status with color-coded indicators
 */

'use client';

import React from 'react';

import { Circle, Loader2, AlertCircle, CheckCircle } from 'lucide-react';

import { cn } from '@/lib/utils';

export type AgentStatus = 'active' | 'idle' | 'training' | 'error';

interface AgentStatusBadgeProps {
  status: AgentStatus;
  className?: string;
  showLabel?: boolean;
}

const STATUS_CONFIG: Record<AgentStatus, {
  label: string;
  icon: typeof Circle;
  color: string;
  bgColor: string;
  pulseAnimation?: boolean;
}> = {
  active: {
    label: 'Active',
    icon: CheckCircle,
    color: 'hsl(142 76% 36%)',      // Green
    bgColor: 'hsl(142 76% 95%)',
    pulseAnimation: true
  },
  idle: {
    label: 'Idle',
    icon: Circle,
    color: 'hsl(0 0% 50%)',         // Gray
    bgColor: 'hsl(0 0% 96%)'
  },
  training: {
    label: 'Training',
    icon: Loader2,
    color: 'hsl(38 92% 50%)',       // Amber/Yellow
    bgColor: 'hsl(38 92% 95%)',
    pulseAnimation: true
  },
  error: {
    label: 'Error',
    icon: AlertCircle,
    color: 'hsl(0 84% 60%)',        // Red
    bgColor: 'hsl(0 84% 95%)'
  }
};

/**
 * AgentStatusBadge displays the current agent status
 * - Active: Green with pulse (agent is actively processing)
 * - Idle: Gray (agent is ready but not processing)
 * - Training: Amber with spin animation (agent is training/updating)
 * - Error: Red (agent encountered an error)
 */
export function AgentStatusBadge({
  status,
  className,
  showLabel = true
}: AgentStatusBadgeProps) {
  const config = STATUS_CONFIG[status];
  const Icon = config.icon;

  return (
    <div
      className={cn(
        'inline-flex items-center gap-1.5',
        showLabel && 'px-2.5 py-1',
        !showLabel && 'p-1',
        'rounded-md text-xs font-semibold',
        'border backdrop-blur-sm',
        className
      )}
      style={{
        color: config.color,
        backgroundColor: config.bgColor,
        borderColor: config.color
      }}
      role="status"
      aria-label={`Agent status: ${config.label}`}
    >
      <Icon
        className={cn(
          'w-3.5 h-3.5',
          config.pulseAnimation && status === 'active' && 'animate-pulse',
          status === 'training' && 'animate-spin'
        )}
      />
      {showLabel && <span>{config.label}</span>}
    </div>
  );
}

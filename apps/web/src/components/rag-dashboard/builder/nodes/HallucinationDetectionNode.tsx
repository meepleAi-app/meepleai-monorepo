'use client';

/**
 * Hallucination Detection Node - Specialized ReactFlow node
 *
 * Enhanced visualization for detecting fabricated/ungrounded information.
 * Shows: forbidden phrases mode, fact checker toggle.
 *
 * @see #3465 - Implement Tier 2 advanced blocks
 */

import { memo } from 'react';

import { AlertTriangle, CheckCircle2, Shield, XCircle } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function HallucinationDetectionNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const forbiddenPhrases = (params.forbiddenPhrases as string) || 'standard';
  const useFactChecker = (params.useFactChecker as boolean) ?? true;

  // Forbidden phrases mode info
  const modeInfo: Record<string, { label: string; examples: string[] }> = {
    standard: {
      label: 'Standard',
      examples: ['I think', 'probably', 'possibly']
    },
    strict: {
      label: 'Strict',
      examples: ['maybe', 'might', 'could be', 'uncertain']
    },
    custom: {
      label: 'Custom',
      examples: ['user-defined']
    },
  };

  const info = modeInfo[forbiddenPhrases] || modeInfo.standard;

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <Shield className="h-3.5 w-3.5 text-red-600" />
          <span className="text-sm font-medium">Hallucination Detection</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Detect fabricated or ungrounded information
          </p>

          {/* Parameters visualization */}
          <div className="space-y-1.5 text-xs">
            <div className="flex items-center gap-1">
              <AlertTriangle className="h-3 w-3 text-muted-foreground" />
              <span className="text-muted-foreground">Mode:</span>
              <Badge
                variant={forbiddenPhrases === 'strict' ? 'destructive' : 'secondary'}
                className="text-[10px] px-1"
              >
                {info.label}
              </Badge>
            </div>
            <div className="flex items-center gap-1">
              {useFactChecker ? (
                <CheckCircle2 className="h-3 w-3 text-green-600" />
              ) : (
                <XCircle className="h-3 w-3 text-muted-foreground" />
              )}
              <span className="text-muted-foreground">Fact Checker:</span>
              <Badge
                variant={useFactChecker ? 'default' : 'outline'}
                className="text-[10px] px-1"
              >
                {useFactChecker ? 'Enabled' : 'Disabled'}
              </Badge>
            </div>
          </div>

          {/* Visual detection representation */}
          <div className="p-2 bg-muted/50 rounded">
            <div className="text-[10px] text-muted-foreground mb-1">Flagged phrases:</div>
            <div className="flex flex-wrap gap-1">
              {info.examples.slice(0, 3).map((phrase, i) => (
                <Badge
                  key={i}
                  variant="outline"
                  className="text-[9px] px-1 py-0 text-red-600 border-red-200 dark:border-red-900"
                >
                  <XCircle className="h-2 w-2 mr-0.5" />
                  {phrase}
                </Badge>
              ))}
            </div>
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-red-600 font-medium">🛡️ Safety</span>
          <span className="text-muted-foreground">~100ms • ~300 tok</span>
        </div>
      }
    />
  );
}

export const HallucinationDetectionNode = memo(HallucinationDetectionNodeComponent);
export default HallucinationDetectionNode;

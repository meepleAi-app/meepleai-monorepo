'use client';

/**
 * Citation Verification Node - Specialized ReactFlow node
 *
 * Enhanced visualization for citation checking.
 * Shows: verification method, coverage threshold, grounding status.
 *
 * @see #3458 - Implement Tier 1 blocks (7 essential RAG blocks)
 */

import { memo } from 'react';

import { FileCheck, Quote, ShieldCheck } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Progress } from '@/components/ui/feedback/progress';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function CitationVerificationNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const method = (params.method as string) || 'nli';
  const minCoverage = (params.minCoverage as [number, number]) || [0.8, 1.0];

  const methodLabels: Record<string, { label: string; description: string }> = {
    nli: { label: 'NLI Model', description: 'Natural Language Inference' },
    fuzzy: { label: 'Fuzzy Match', description: 'Text similarity matching' },
    llm: { label: 'LLM Judge', description: 'LLM-based verification' },
  };

  const methodInfo = methodLabels[method] || { label: method, description: '' };

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <FileCheck className="h-3.5 w-3.5 text-red-600" />
          <span className="text-sm font-medium">Citation Check</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Verify claims are grounded in sources
          </p>

          {/* Method */}
          <div className="flex items-center gap-2">
            <Badge variant="secondary" className="text-[10px]">
              <ShieldCheck className="h-3 w-3 mr-1" />
              {methodInfo.label}
            </Badge>
          </div>

          {/* Coverage requirement */}
          <div className="space-y-1">
            <div className="flex items-center justify-between text-[10px]">
              <div className="flex items-center gap-1 text-muted-foreground">
                <Quote className="h-3 w-3" />
                <span>Min Coverage</span>
              </div>
              <span className="font-medium">{(minCoverage[0] * 100).toFixed(0)}%</span>
            </div>
            <Progress value={minCoverage[0] * 100} className="h-1.5" />
          </div>

          {/* Grounding indicator */}
          <div className="grid grid-cols-3 gap-1 text-[9px]">
            <div className="text-center p-1 bg-green-100 dark:bg-green-900/30 rounded">
              <span className="text-green-700 dark:text-green-400">Grounded</span>
            </div>
            <div className="text-center p-1 bg-yellow-100 dark:bg-yellow-900/30 rounded">
              <span className="text-yellow-700 dark:text-yellow-400">Partial</span>
            </div>
            <div className="text-center p-1 bg-red-100 dark:bg-red-900/30 rounded">
              <span className="text-red-700 dark:text-red-400">Ungrounded</span>
            </div>
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-blue-600 font-medium">📊 Validation</span>
          <span className="text-muted-foreground">~200ms • ~500 tok</span>
        </div>
      }
    />
  );
}

export const CitationVerificationNode = memo(CitationVerificationNodeComponent);
export default CitationVerificationNode;

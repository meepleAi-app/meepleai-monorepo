'use client';

import { useCallback, useState } from 'react';

import { ChevronDown, ChevronRight, Copy, FileText, Route, Shield } from 'lucide-react';
import { Server } from 'lucide-react';

import { Button, Card, CardContent, CardHeader, CardTitle } from '@/components/ui';
import type {
  AgentConfigSnapshot,
  PromptTemplateInfo,
  StrategyInfo,
  TierInfo,
} from '@/lib/agent/playground-sse-parser';
import { cn } from '@/lib/utils';

interface DebugPanelAgentConfigProps {
  agentConfig: AgentConfigSnapshot | null;
  activeStrategy: string | null;
  strategyInfo: StrategyInfo | null;
  resolvedSystemPrompt: string | null;
  promptTemplateInfo: PromptTemplateInfo | null;
  tierInfo: TierInfo | null;
}

export function DebugPanelAgentConfig({
  agentConfig,
  activeStrategy,
  strategyInfo,
  resolvedSystemPrompt,
  promptTemplateInfo,
  tierInfo,
}: DebugPanelAgentConfigProps) {
  const [agentConfigExpanded, setAgentConfigExpanded] = useState(false);
  const [systemPromptExpanded, setSystemPromptExpanded] = useState(false);
  const [systemPromptCopied, setSystemPromptCopied] = useState(false);

  const handleCopySystemPrompt = useCallback(() => {
    if (!resolvedSystemPrompt) return;
    navigator.clipboard.writeText(resolvedSystemPrompt).then(() => {
      setSystemPromptCopied(true);
      setTimeout(() => setSystemPromptCopied(false), 2000);
    });
  }, [resolvedSystemPrompt]);

  return (
    <>
      {/* Agent Config (Issue #4470) */}
      {agentConfig &&
        (() => {
          const isNonDefaultTemp = agentConfig.temperature !== 0.7;
          const isNonDefaultMaxTokens = agentConfig.maxTokens !== 2048;
          const hasOverrides =
            agentConfig.isModelOverride || isNonDefaultTemp || isNonDefaultMaxTokens;

          return (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm flex items-center gap-2">
                  <Server className="h-4 w-4" />
                  Model Config
                  {hasOverrides && (
                    <span className="text-[10px] bg-amber-100 text-amber-800 px-1 py-0.5 rounded leading-none">
                      customized
                    </span>
                  )}
                  <button
                    type="button"
                    className="ml-auto"
                    onClick={() => setAgentConfigExpanded(!agentConfigExpanded)}
                  >
                    {agentConfigExpanded ? (
                      <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />
                    ) : (
                      <ChevronRight className="h-3.5 w-3.5 text-muted-foreground" />
                    )}
                  </button>
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="space-y-2 text-sm">
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Provider</span>
                    <span className="font-mono">{agentConfig.provider}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Model</span>
                    <span className="flex items-center gap-1.5">
                      <span
                        className="font-mono text-xs truncate max-w-[140px]"
                        title={agentConfig.model}
                      >
                        {agentConfig.model}
                      </span>
                      {agentConfig.isModelOverride && (
                        <span className="text-[10px] bg-amber-100 text-amber-800 px-1 py-0.5 rounded leading-none">
                          override
                        </span>
                      )}
                    </span>
                  </div>
                  {agentConfigExpanded && (
                    <>
                      <div className="flex justify-between">
                        <span className="text-muted-foreground">Temperature</span>
                        <span
                          className={cn(
                            'font-mono',
                            isNonDefaultTemp && 'text-amber-600 font-semibold'
                          )}
                        >
                          {agentConfig.temperature}
                          {isNonDefaultTemp && (
                            <span className="text-[10px] ml-1 text-muted-foreground">
                              (default: 0.7)
                            </span>
                          )}
                        </span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-muted-foreground">Max Tokens</span>
                        <span
                          className={cn(
                            'font-mono',
                            isNonDefaultMaxTokens && 'text-amber-600 font-semibold'
                          )}
                        >
                          {agentConfig.maxTokens.toLocaleString()}
                          {isNonDefaultMaxTokens && (
                            <span className="text-[10px] ml-1 text-muted-foreground">
                              (default: 2048)
                            </span>
                          )}
                        </span>
                      </div>
                    </>
                  )}
                  {!agentConfigExpanded && (
                    <p className="text-[11px] text-muted-foreground">
                      T:{agentConfig.temperature} · Max:{agentConfig.maxTokens.toLocaleString()}
                    </p>
                  )}
                </div>
              </CardContent>
            </Card>
          );
        })()}

      {/* Strategy Info */}
      {(activeStrategy || strategyInfo) && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <Route className="h-4 w-4" />
              Strategy
              {strategyInfo?.type && (
                <span
                  className={cn(
                    'text-[10px] px-1.5 py-0.5 rounded leading-none',
                    strategyInfo.type === 'retrieval' && 'bg-blue-100 text-blue-800',
                    strategyInfo.type === 'generation' && 'bg-amber-100 text-amber-800',
                    strategyInfo.type === 'consensus' && 'bg-purple-100 text-purple-800',
                    strategyInfo.type === 'validation' && 'bg-green-100 text-green-800',
                    strategyInfo.type === 'custom' && 'bg-gray-100 text-gray-800'
                  )}
                >
                  {strategyInfo.type}
                </span>
              )}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Execution</span>
                <span className="font-mono">{activeStrategy}</span>
              </div>
              {strategyInfo && (
                <>
                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Agent Strategy</span>
                    <span className="font-mono">{strategyInfo.name}</span>
                  </div>
                  {Object.keys(strategyInfo.parameters).length > 0 && (
                    <div className="border-t pt-2 space-y-1.5">
                      <span className="text-xs font-medium text-muted-foreground">Parameters</span>
                      {Object.entries(strategyInfo.parameters).map(([key, value]) => (
                        <div key={key} className="flex justify-between text-xs">
                          <span className="text-muted-foreground">{key}</span>
                          <span className="font-mono truncate max-w-[120px]" title={String(value)}>
                            {Array.isArray(value) ? value.join(', ') : String(value)}
                          </span>
                        </div>
                      ))}
                    </div>
                  )}
                </>
              )}
              {!strategyInfo && (
                <p className="text-xs text-muted-foreground mt-1">
                  {activeStrategy === 'RetrievalOnly' && 'RAG chunks only, no LLM cost'}
                  {activeStrategy === 'SingleModel' && 'RAG + single LLM call (POC)'}
                  {activeStrategy === 'MultiModelConsensus' && 'RAG + dual-model consensus'}
                </p>
              )}
              {/* Prompt Template Info (Issue #4469) */}
              {promptTemplateInfo && (
                <div className="border-t pt-2 space-y-1.5">
                  <span className="text-xs font-medium text-muted-foreground">Prompt Template</span>
                  <div className="flex justify-between text-xs">
                    <span className="text-muted-foreground">Role</span>
                    <span className="flex items-center gap-1.5">
                      <span className="font-mono">{promptTemplateInfo.role}</span>
                      <span className="text-[10px] bg-blue-100 text-blue-800 px-1 py-0.5 rounded leading-none">
                        {promptTemplateInfo.promptCount} prompt
                        {promptTemplateInfo.promptCount !== 1 ? 's' : ''}
                      </span>
                    </span>
                  </div>
                  {promptTemplateInfo.lastModified && (
                    <div className="flex justify-between text-xs">
                      <span className="text-muted-foreground">Modified</span>
                      <span
                        className="font-mono text-[11px]"
                        title={new Date(promptTemplateInfo.lastModified).toISOString()}
                      >
                        {new Date(promptTemplateInfo.lastModified).toLocaleDateString()}
                      </span>
                    </div>
                  )}
                </div>
              )}
              {/* Tier Access Info (Issue #4471) */}
              {tierInfo && (
                <div className="border-t pt-2 space-y-1.5">
                  <span className="text-xs font-medium text-muted-foreground flex items-center gap-1">
                    <Shield className="h-3 w-3" />
                    Tier Access
                  </span>
                  <div className="flex justify-between text-xs">
                    <span className="text-muted-foreground">Required</span>
                    <span
                      className={cn(
                        'text-[10px] px-1.5 py-0.5 rounded leading-none font-semibold',
                        tierInfo.requiredTier === 'free' && 'bg-green-100 text-green-800',
                        tierInfo.requiredTier === 'premium' && 'bg-purple-100 text-purple-800',
                        tierInfo.requiredTier === 'pro' && 'bg-amber-100 text-amber-800',
                        tierInfo.requiredTier === 'enterprise' && 'bg-blue-100 text-blue-800'
                      )}
                    >
                      {tierInfo.requiredTier}
                    </span>
                  </div>
                  <div className="flex justify-between text-xs">
                    <span className="text-muted-foreground">Your tier</span>
                    <span className="font-mono">{tierInfo.userTier}</span>
                  </div>
                  <div className="flex justify-between text-xs">
                    <span className="text-muted-foreground">Access</span>
                    <span
                      className={cn(
                        'text-[10px] px-1.5 py-0.5 rounded leading-none font-semibold',
                        tierInfo.hasAccess
                          ? 'bg-green-100 text-green-800'
                          : 'bg-red-100 text-red-800'
                      )}
                    >
                      {tierInfo.hasAccess ? 'Granted' : 'Denied'}
                    </span>
                  </div>
                </div>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      {/* System Prompt Preview (Issue #4468) */}
      {resolvedSystemPrompt && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <FileText className="h-4 w-4" />
              System Prompt
              <Button
                variant="ghost"
                size="icon"
                className="h-5 w-5 ml-auto"
                onClick={handleCopySystemPrompt}
                title={systemPromptCopied ? 'Copied!' : 'Copy to clipboard'}
              >
                <Copy className={cn('h-3 w-3', systemPromptCopied && 'text-green-600')} />
              </Button>
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              <p className="text-xs font-mono whitespace-pre-wrap break-words text-muted-foreground leading-relaxed">
                {systemPromptExpanded
                  ? resolvedSystemPrompt
                  : resolvedSystemPrompt.length > 200
                    ? `${resolvedSystemPrompt.slice(0, 200)}...`
                    : resolvedSystemPrompt}
              </p>
              {resolvedSystemPrompt.length > 200 && (
                <button
                  type="button"
                  onClick={() => setSystemPromptExpanded(!systemPromptExpanded)}
                  className="text-[11px] text-primary hover:underline"
                >
                  {systemPromptExpanded ? 'Show less' : 'Show more'}
                </button>
              )}
            </div>
          </CardContent>
        </Card>
      )}
    </>
  );
}

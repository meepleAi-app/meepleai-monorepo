'use client';

import { usePlaygroundStore } from '@/stores/playground-store';

import { DebugPanelAgentConfig } from './DebugPanelAgentConfig';
import { DebugPanelCache } from './DebugPanelCache';
import { DebugPanelConsole } from './DebugPanelConsole';
import { DebugPanelCost } from './DebugPanelCost';
import { DebugPanelDataFlow } from './DebugPanelDataFlow';
import { DebugPanelMetrics } from './DebugPanelMetrics';
import { DebugPanelNetwork } from './DebugPanelNetwork';

export function DebugPanel() {
  const {
    messages,
    tokenBreakdown,
    confidence,
    latencyMs,
    pipelineSteps,
    agentConfig,
    latencyBreakdown,
    costBreakdown,
    sessionTotalCost,
    activeStrategy,
    strategyInfo,
    pipelineTimings,
    cacheInfo,
    sessionCacheHits,
    sessionCacheRequests,
    apiTraces,
    logEntries,
    tomacLayers,
    resolvedSystemPrompt,
    promptTemplateInfo,
    tierInfo,
    costEstimate,
    dataFlowSteps,
  } = usePlaygroundStore();

  return (
    <div className="space-y-4">
      <h3 className="text-lg font-semibold">Debug Information</h3>

      <DebugPanelMetrics
        messages={messages}
        tokenBreakdown={tokenBreakdown}
        confidence={confidence}
        latencyMs={latencyMs}
        latencyBreakdown={latencyBreakdown}
        pipelineTimings={pipelineTimings}
        pipelineSteps={pipelineSteps}
      />

      <DebugPanelCost
        costBreakdown={costBreakdown}
        sessionTotalCost={sessionTotalCost}
        costEstimate={costEstimate}
      />

      <DebugPanelCache
        cacheInfo={cacheInfo}
        sessionCacheHits={sessionCacheHits}
        sessionCacheRequests={sessionCacheRequests}
      />

      <DebugPanelAgentConfig
        agentConfig={agentConfig}
        activeStrategy={activeStrategy}
        strategyInfo={strategyInfo}
        resolvedSystemPrompt={resolvedSystemPrompt}
        promptTemplateInfo={promptTemplateInfo}
        tierInfo={tierInfo}
      />

      <DebugPanelDataFlow dataFlowSteps={dataFlowSteps} tomacLayers={tomacLayers} />

      <DebugPanelNetwork apiTraces={apiTraces} />

      <DebugPanelConsole logEntries={logEntries} messages={messages} />
    </div>
  );
}

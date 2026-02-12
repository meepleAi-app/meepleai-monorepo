/**
 * ISSUE-3709: Agent Builder - Tools & Strategy Step
 * Step 3: Tool selection and retrieval strategy configuration
 */

'use client';

import type { AgentForm } from '@/lib/schemas/agent-definition-schema';
import { AVAILABLE_STRATEGIES } from '@/lib/schemas/agent-definition-schema';
import { Label } from '@/components/ui/label';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Input } from '@/components/ui/primitives/input';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

interface ToolsStrategyStepProps {
  agent: AgentForm;
  onChange: (agent: AgentForm) => void;
}

// Available tools in the platform
const AVAILABLE_TOOLS = [
  { id: 'web_search', name: 'Web Search', description: 'Search the internet for information' },
  { id: 'hybrid_search', name: 'Hybrid Search', description: 'Search knowledge base with vector + keyword' },
  { id: 'vector_search', name: 'Vector Search', description: 'Semantic similarity search' },
  { id: 'rules_lookup', name: 'Rules Lookup', description: 'Look up specific game rules' },
  { id: 'game_catalog', name: 'Game Catalog', description: 'Access BGG game database' },
  { id: 'move_validator', name: 'Move Validator', description: 'Validate game moves (Arbitro)' },
  { id: 'strategy_analyzer', name: 'Strategy Analyzer', description: 'Analyze positions (Decisore)' },
] as const;

export function ToolsStrategyStep({ agent, onChange }: ToolsStrategyStepProps) {
  const selectedToolIds = new Set(agent.tools?.map((t) => t.name) || []);

  const toggleTool = (toolId: string, checked: boolean) => {
    const newTools = checked
      ? [...(agent.tools || []), { name: toolId, settings: {} }]
      : (agent.tools || []).filter((t) => t.name !== toolId);

    onChange({ ...agent, tools: newTools });
  };

  const updateStrategyParam = (key: string, value: unknown) => {
    const newParams = { ...agent.strategyParameters, [key]: value };
    onChange({ ...agent, strategyParameters: newParams });
  };

  return (
    <div className="space-y-6">
      {/* Retrieval Strategy */}
      <Card>
        <CardHeader>
          <CardTitle>Retrieval Strategy</CardTitle>
          <CardDescription>
            Configure how the agent retrieves information from the knowledge base
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* Strategy Selection */}
          <div className="space-y-2">
            <Label htmlFor="strategy-name">Strategy</Label>
            <Select
              value={agent.strategyName || 'HybridSearch'}
              onValueChange={(value) => onChange({ ...agent, strategyName: value })}
            >
              <SelectTrigger id="strategy-name">
                <SelectValue placeholder="Select strategy" />
              </SelectTrigger>
              <SelectContent>
                {AVAILABLE_STRATEGIES.map((strategy) => (
                  <SelectItem key={strategy.value} value={strategy.value}>
                    <div>
                      <div className="font-medium">{strategy.label}</div>
                      <div className="text-xs text-muted-foreground">{strategy.description}</div>
                    </div>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Strategy Parameters */}
          <div className="grid gap-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="topK">Top K Results</Label>
                <Input
                  id="topK"
                  type="number"
                  min={1}
                  max={50}
                  value={String(agent.strategyParameters?.topK || 10)}
                  onChange={(e: React.ChangeEvent<HTMLInputElement>) => updateStrategyParam('topK', Number(e.target.value))}
                />
                <p className="text-xs text-muted-foreground">
                  Number of documents to retrieve (1-50)
                </p>
              </div>

              <div className="space-y-2">
                <Label htmlFor="minScore">Minimum Score</Label>
                <Input
                  id="minScore"
                  type="number"
                  min={0}
                  max={1}
                  step={0.05}
                  value={String(agent.strategyParameters?.minScore || 0.55)}
                  onChange={(e: React.ChangeEvent<HTMLInputElement>) => updateStrategyParam('minScore', Number(e.target.value))}
                />
                <p className="text-xs text-muted-foreground">
                  Relevance threshold (0.0-1.0)
                </p>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Tools Selection */}
      <Card>
        <CardHeader>
          <CardTitle>Available Tools</CardTitle>
          <CardDescription>
            Select tools the agent can use during conversations
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-3">
            {AVAILABLE_TOOLS.map((tool) => (
              <div key={tool.id} className="flex items-start space-x-3">
                <Checkbox
                  id={tool.id}
                  checked={selectedToolIds.has(tool.id)}
                  onCheckedChange={(checked) => toggleTool(tool.id, !!checked)}
                />
                <div className="grid gap-1.5 leading-none">
                  <label
                    htmlFor={tool.id}
                    className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70"
                  >
                    {tool.name}
                  </label>
                  <p className="text-sm text-muted-foreground">{tool.description}</p>
                </div>
              </div>
            ))}
          </div>

          {agent.tools && agent.tools.length > 0 && (
            <div className="mt-4 pt-4 border-t">
              <p className="text-sm text-muted-foreground">
                Selected: {agent.tools.length} tool{agent.tools.length !== 1 ? 's' : ''}
              </p>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

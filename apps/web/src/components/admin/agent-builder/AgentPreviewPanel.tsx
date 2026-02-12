/**
 * ISSUE-3709: Agent Builder - Preview Panel (Sidebar)
 * Sticky sidebar preview panel shown during all builder steps
 */

'use client';

import { Code2, Eye } from 'lucide-react';

import type { AgentForm } from '@/lib/schemas/agent-definition-schema';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { Badge } from '@/components/ui/data-display/badge';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';

interface AgentPreviewPanelProps {
  agent: AgentForm;
}

export function AgentPreviewPanel({ agent }: AgentPreviewPanelProps) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Preview</CardTitle>
      </CardHeader>
      <CardContent>
        <Tabs defaultValue="preview" className="w-full">
          <TabsList className="grid w-full grid-cols-2">
            <TabsTrigger value="preview">
              <Eye className="h-3 w-3 mr-1" />
              Preview
            </TabsTrigger>
            <TabsTrigger value="json">
              <Code2 className="h-3 w-3 mr-1" />
              JSON
            </TabsTrigger>
          </TabsList>

          <TabsContent value="preview" className="mt-4">
            <ScrollArea className="h-[600px]">
              <div className="space-y-4 pr-4">
                {/* Name */}
                <div>
                  <p className="text-xs text-muted-foreground mb-1">Name</p>
                  <p className="text-sm font-medium">
                    {agent.name || <span className="italic text-muted-foreground">Not set</span>}
                  </p>
                </div>

                {/* Type & Model */}
                <div>
                  <p className="text-xs text-muted-foreground mb-1">Type & Model</p>
                  <div className="flex gap-2">
                    <Badge variant="secondary">{agent.type}</Badge>
                    <Badge variant="outline" className="font-mono text-xs">
                      {agent.model}
                    </Badge>
                  </div>
                </div>

                {/* Parameters */}
                <div>
                  <p className="text-xs text-muted-foreground mb-1">Parameters</p>
                  <div className="grid grid-cols-2 gap-2 text-xs">
                    <div>
                      <span className="text-muted-foreground">Temp:</span>
                      <span className="ml-1 font-medium">{agent.temperature.toFixed(1)}</span>
                    </div>
                    <div>
                      <span className="text-muted-foreground">Tokens:</span>
                      <span className="ml-1 font-medium">{agent.maxTokens}</span>
                    </div>
                  </div>
                </div>

                {/* Prompts */}
                <div>
                  <p className="text-xs text-muted-foreground mb-1">
                    Prompts ({agent.prompts.length})
                  </p>
                  <div className="space-y-2">
                    {agent.prompts.length === 0 ? (
                      <p className="text-xs italic text-muted-foreground">No prompts</p>
                    ) : (
                      agent.prompts.map((prompt, i) => (
                        <div key={i} className="text-xs">
                          <Badge variant="outline" className="capitalize mb-1">
                            {prompt.role}
                          </Badge>
                          <p className="text-muted-foreground line-clamp-2">
                            {prompt.content || '(empty)'}
                          </p>
                        </div>
                      ))
                    )}
                  </div>
                </div>

                {/* Strategy */}
                {agent.strategyName && (
                  <div>
                    <p className="text-xs text-muted-foreground mb-1">Strategy</p>
                    <Badge>{agent.strategyName}</Badge>
                    {agent.strategyParameters && (
                      <pre className="mt-1 text-xs bg-muted p-2 rounded">
                        {JSON.stringify(agent.strategyParameters, null, 2)}
                      </pre>
                    )}
                  </div>
                )}

                {/* Tools */}
                {agent.tools && agent.tools.length > 0 && (
                  <div>
                    <p className="text-xs text-muted-foreground mb-1">
                      Tools ({agent.tools.length})
                    </p>
                    <div className="flex flex-wrap gap-1">
                      {agent.tools.map((tool) => (
                        <Badge key={tool.name} variant="secondary" className="text-xs">
                          {tool.name}
                        </Badge>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            </ScrollArea>
          </TabsContent>

          <TabsContent value="json" className="mt-4">
            <ScrollArea className="h-[600px]">
              <pre className="text-xs bg-muted p-4 rounded whitespace-pre-wrap">
                {JSON.stringify(agent, null, 2)}
              </pre>
            </ScrollArea>
          </TabsContent>
        </Tabs>
      </CardContent>
    </Card>
  );
}

/**
 * Playground Test Scenario Types
 * Issue #4396 - Backend entity: PlaygroundTestScenario
 * Issue #4391 - Frontend CRUD UI
 */

export interface PlaygroundTestScenarioDto {
  id: string;
  name: string;
  description: string;
  agentDefinitionId: string;
  userMessage: string;
  systemMessage: string | null;
  expectedTopics: string[];
  tags: string[];
  createdAt: string;
  updatedAt: string | null;
}

export interface CreatePlaygroundTestScenario {
  name: string;
  description: string;
  agentDefinitionId: string;
  userMessage: string;
  systemMessage?: string;
  expectedTopics?: string[];
  tags?: string[];
}

export interface UpdatePlaygroundTestScenario {
  name: string;
  description: string;
  userMessage: string;
  systemMessage?: string;
  expectedTopics?: string[];
  tags?: string[];
}

/**
 * Agent Typologies Schemas — compatibility shim
 *
 * Typologies were collapsed into AgentDefinition during the agent system
 * simplification. These re-exports preserve existing call sites while
 * routing to the canonical agent-definitions types.
 */

import { agentDefinitionDtoSchema, createAgentDefinitionSchema } from './agent-definitions.schemas';

export type { AgentDefinitionDto as Typology } from './agent-definitions.schemas';
export type { CreateAgentDefinition as CreateTypology } from './agent-definitions.schemas';
export type { UpdateAgentDefinition as UpdateTypology } from './agent-definitions.schemas';

// Zod schema alias for call sites that pass the schema to a validator
export const typologySchema = agentDefinitionDtoSchema;
export const createTypologySchema = createAgentDefinitionSchema;
export const updateTypologySchema = createAgentDefinitionSchema;

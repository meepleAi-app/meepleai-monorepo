/**
 * Agent Store Types (Issue #3188)
 *
 * Shared types for agent store slices to avoid circular dependencies
 */

import { ConfigSlice } from '../slices/configSlice';
import { ConversationSlice } from '../slices/conversationSlice';
import { SessionSlice } from '../slices/sessionSlice';
import { UISlice } from '../slices/uiSlice';

/**
 * Combined Agent Store Type
 *
 * Combines all slices into a single store interface
 */
export type AgentStore = ConfigSlice & SessionSlice & ConversationSlice & UISlice;

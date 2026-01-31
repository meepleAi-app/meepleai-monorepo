/**
 * Agent Store (Issue #3187)
 *
 * Zustand store for agent chat state
 */

import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';

import { createAgentChatSlice, AgentChatSlice } from './slices/agentChatSlice';

export type AgentStore = AgentChatSlice;

export const useAgentChatStore = create<AgentStore>()(
  immer((...a) => ({
    ...createAgentChatSlice(...a),
  }))
);

/**
 * Test helpers for AgentSelector component tests
 */

/**
 * Helper to setup mock context with default values
 */
export const setupMockContext = (overrides?: any) => {
  const mockUseChatContext = vi.fn();
  mockUseChatContext.mockReturnValue({
    agents: [],
    selectedAgentId: null,
    selectAgent: vi.fn(),
    selectedGameId: null,
    loading: { agents: false },
    ...overrides,
  });
  return mockUseChatContext;
};

/**
 * Sample agent data for testing
 */
export const sampleAgents = {
  single: [{ id: 'agent-1', name: 'Chess Expert' }],
  multiple: [
    { id: 'agent-1', name: 'Chess Expert' },
    { id: 'agent-2', name: 'Catan Helper' },
    { id: 'agent-3', name: 'Risk Strategist' },
  ],
  duplicateNames: [
    { id: 'agent-1', name: 'Chess Expert' },
    { id: 'agent-2', name: 'Chess Expert' },
  ],
  specialCharacters: [
    { id: 'agent-1', name: "Chess Expert: Beginner's Guide" },
    { id: 'agent-2', name: 'Catan Helper (Advanced)' },
  ],
  unsortedNames: [
    { id: 'agent-1', name: 'Zzz Agent' },
    { id: 'agent-2', name: 'Aaa Agent' },
    { id: 'agent-3', name: 'Mmm Agent' },
  ],
  largeSet: Array.from({ length: 100 }, (_, i) => ({
    id: `agent-${i}`,
    name: `Agent ${i}`,
  })),
};

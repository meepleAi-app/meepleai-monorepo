/**
 * Demo Data - Barrel Export
 * Issue #4781: Mock data for 4 demo games
 */

export {
  DEMO_GAME_IDS,
  DEMO_GAMES,
  DEMO_GAMES_BY_ID,
  demoCatan,
  demoDescent,
  demoTicketToRide,
  demoPandemic,
} from './demo-games';
export type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';

export {
  DEMO_AGENT_SUGGESTIONS,
  getAgentSuggestionsForGame,
  getPrimarySuggestion,
} from './demo-agent-suggestions';
export type { AgentSuggestion, GameAgentSuggestions } from './demo-agent-suggestions';

export {
  DEMO_GAME_CHATS,
  getDemoGameChat,
  findDemoResponse,
} from './demo-chat-responses';
export type { DemoChatQA, DemoGameChat } from './demo-chat-responses';

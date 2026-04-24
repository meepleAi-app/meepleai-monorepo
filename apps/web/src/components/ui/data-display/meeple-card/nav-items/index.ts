export { navIcons, type NavIconKey } from './icons';

export {
  buildGameConnections,
  buildGameNavItems,
  type GameConnectionsCounts,
  type GameConnectionsHandlers,
  type GameNavCounts,
  type GameNavHandlers,
} from './buildGameNavItems';
export {
  buildPlayerConnections,
  buildPlayerNavItems,
  type PlayerConnectionsCounts,
  type PlayerConnectionsHandlers,
  type PlayerNavCounts,
  type PlayerNavHandlers,
} from './buildPlayerNavItems';
export {
  buildSessionConnections,
  buildSessionNavItems,
  type SessionConnectionsCounts,
  type SessionConnectionsHandlers,
  type SessionNavCounts,
  type SessionNavHandlers,
} from './buildSessionNavItems';
export {
  buildAgentConnections,
  buildAgentNavItems,
  type AgentConnectionsCounts,
  type AgentConnectionsHandlers,
  type AgentNavCounts,
  type AgentNavHandlers,
} from './buildAgentNavItems';
export {
  buildKbConnections,
  buildKbNavItems,
  type KbConnectionsCounts,
  type KbConnectionsHandlers,
  type KbNavCounts,
  type KbNavHandlers,
} from './buildKbNavItems';
export {
  buildChatConnections,
  buildChatNavItems,
  type ChatConnectionsCounts,
  type ChatConnectionsHandlers,
  type ChatNavCounts,
  type ChatNavHandlers,
} from './buildChatNavItems';
export {
  buildEventConnections,
  buildEventNavItems,
  type EventConnectionsCounts,
  type EventConnectionsHandlers,
  type EventNavCounts,
  type EventNavHandlers,
} from './buildEventNavItems';
export {
  buildToolkitConnections,
  buildToolkitNavItems,
  type ToolkitConnectionsCounts,
  type ToolkitConnectionsHandlers,
  type ToolkitNavCounts,
  type ToolkitNavHandlers,
} from './buildToolkitNavItems';
// NOTE: buildToolNavItems has no Counts parameter — tools are pure action
// containers. Only the Handlers type is exported.
export {
  buildToolConnections,
  buildToolNavItems,
  type ToolConnectionsHandlers,
  type ToolNavHandlers,
} from './buildToolNavItems';

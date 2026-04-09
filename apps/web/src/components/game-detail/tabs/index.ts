/**
 * Barrel export for the shared game-detail tab components.
 * See `types.ts` for the shared tab component contract (§4.4.1).
 */
export { GameInfoTab } from './GameInfoTab';
export { GameAiChatTab } from './GameAiChatTab';
export { GameToolboxTab } from './GameToolboxTab';
export { GameHouseRulesTab } from './GameHouseRulesTab';
export { GamePartiteTab } from './GamePartiteTab';
export type { GameTabId, GameTabVariant, GameTabProps, GameTabDescriptor } from './types';
export { GAME_TABS, isGameTabId } from './types';

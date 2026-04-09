import type { ToggleConfig } from './types';
import type { HttpHandler } from 'msw';

export interface HandlerGroup {
  name: string;
  handlers: HttpHandler[];
}

/**
 * Build the list of MSW handlers that should be active for the given toggle state.
 * A group is active unless its toggle is explicitly false.
 * (Missing toggle entries are treated as enabled — safer default.)
 */
export function buildActiveHandlers(groups: HandlerGroup[], toggles: ToggleConfig): HttpHandler[] {
  const active: HttpHandler[] = [];
  for (const group of groups) {
    const enabled = toggles.groups[group.name] !== false;
    if (enabled) {
      active.push(...group.handlers);
    }
  }
  return active;
}

/**
 * Generate an endpoint override key from a handler's method and path.
 * Format: "<group>.<METHOD> <path>" — example: "games.POST /api/v1/games"
 */
export function endpointKey(groupName: string, method: string, path: string): string {
  return `${groupName}.${method.toUpperCase()} ${path}`;
}

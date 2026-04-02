import { ToolkitConfig } from '@/lib/types/standalone-toolkit';

export const DEFAULT_TOOLKIT: ToolkitConfig = {
  dice: [
    { name: 'D6', sides: 6, count: 1 },
    { name: '2D6', sides: 6, count: 2 },
    { name: 'D20', sides: 20, count: 1 },
    { name: 'D4', sides: 4, count: 1 },
    { name: 'D8', sides: 8, count: 1 },
    { name: 'D10', sides: 10, count: 1 },
    { name: 'D12', sides: 12, count: 1 },
  ],
  timers: [
    { name: 'Timer', type: 'countdown', defaultSeconds: 60 },
    { name: 'Timer turno', type: 'turn', defaultSeconds: 120 },
  ],
  cards: [],
  counters: [{ id: 'default-counter', name: 'Punti', initialValue: 0 }],
  randomizer: { name: 'Randomizzatore', items: [] },
};

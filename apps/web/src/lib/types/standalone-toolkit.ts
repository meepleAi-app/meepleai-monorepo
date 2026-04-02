export type DiceFace = string | number;

export interface StandardDiceConfig {
  name: string;
  sides: number;
  count: number;
  customFaces?: undefined;
}

export interface CustomDiceConfig {
  name: string;
  sides?: undefined;
  customFaces: string[];
  count: number;
  description?: string;
}

export type DiceConfig = StandardDiceConfig | CustomDiceConfig;

export interface TimerConfig {
  name: string;
  type: 'countdown' | 'countup' | 'turn';
  defaultSeconds: number;
}

export interface CardConfig {
  name: string;
  totalCards: number;
  cardFaces?: string[];
  reshuffleDiscardOnEmpty: boolean;
}

export interface CounterConfig {
  id: string;
  name: string;
  initialValue: number;
  min?: number;
  max?: number;
}

export interface RandomizerConfig {
  name: string;
  items: string[];
}

export interface ToolkitConfig {
  dice: DiceConfig[];
  timers: TimerConfig[];
  cards: CardConfig[];
  counters: CounterConfig[];
  randomizer: RandomizerConfig;
}

// Log entry salvato in localStorage
export interface ToolLogEntry {
  id: string;
  timestamp: string; // ISO 8601
  toolType: 'dice' | 'timer' | 'card' | 'counter' | 'randomizer';
  action: string;
  actorLabel?: string;
  result: string;
}

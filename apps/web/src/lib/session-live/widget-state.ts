'use client';

/**
 * Widget state types for #2376 G5c ToolkitRenderer.
 *
 * Discriminated union covering all 6 WidgetType variants. Backend stores
 * config as JSON string; FE parses into typed union via `parseWidgetConfig`.
 *
 * @see docs/superpowers/specs/2026-06-16-issue-2376-g5c-toolkit-renderer-design.md §4
 */

export type WidgetType =
  | 'RandomGenerator'
  | 'TurnManager'
  | 'ScoreTracker'
  | 'ResourceManager'
  | 'NoteManager'
  | 'Whiteboard';

export interface RandomGeneratorConfig {
  readonly name: string;
  readonly faces: ReadonlyArray<string | number>;
  readonly quantity: number;
  readonly last: string | number | null;
}

export interface TurnManagerConfig {
  readonly phaseBased: boolean;
  readonly phases?: ReadonlyArray<string>;
  readonly activeIndex: number;
}

export interface ScoreTrackerConfig {
  readonly scores: Readonly<Record<string, number>>;
}

export interface ResourceItem {
  readonly label: string;
  readonly value: number;
  readonly max: number;
  readonly danger?: boolean;
}

export interface ResourceManagerConfig {
  readonly shared: boolean;
  readonly resources: ReadonlyArray<ResourceItem>;
}

export interface NoteManagerConfig {
  readonly text: string;
}

export interface WhiteboardStroke {
  readonly tool: string;
  readonly points: ReadonlyArray<readonly [number, number]>;
  readonly color: string;
}

export interface WhiteboardConfig {
  readonly strokes: ReadonlyArray<WhiteboardStroke>;
}

export type WidgetConfigByType = {
  RandomGenerator: RandomGeneratorConfig;
  TurnManager: TurnManagerConfig;
  ScoreTracker: ScoreTrackerConfig;
  ResourceManager: ResourceManagerConfig;
  NoteManager: NoteManagerConfig;
  Whiteboard: WhiteboardConfig;
};

export type ParsedWidget = {
  [K in WidgetType]: {
    readonly id: string;
    readonly type: K;
    readonly isEnabled: boolean;
    readonly displayOrder: number;
    readonly config: WidgetConfigByType[K];
  };
}[WidgetType];

export function defaultConfigFor<T extends WidgetType>(type: T): WidgetConfigByType[T] {
  switch (type) {
    case 'RandomGenerator':
      return {
        name: 'Generatore',
        faces: [1, 2, 3, 4, 5, 6],
        quantity: 1,
        last: null,
      } as unknown as WidgetConfigByType[T];
    case 'TurnManager':
      return { phaseBased: false, activeIndex: 0 } as unknown as WidgetConfigByType[T];
    case 'ScoreTracker':
      return { scores: {} } as unknown as WidgetConfigByType[T];
    case 'ResourceManager':
      return { shared: false, resources: [] } as unknown as WidgetConfigByType[T];
    case 'NoteManager':
      return { text: '' } as unknown as WidgetConfigByType[T];
    case 'Whiteboard':
      return { strokes: [] } as unknown as WidgetConfigByType[T];
    default: {
      const _exhaustive: never = type;
      return _exhaustive;
    }
  }
}

export function parseWidgetConfig<T extends WidgetType>(
  type: T,
  json: string
): WidgetConfigByType[T] {
  try {
    const parsed = JSON.parse(json) as Partial<WidgetConfigByType[T]>;
    return { ...defaultConfigFor(type), ...parsed };
  } catch (err) {
    console.warn('[ToolkitRenderer] parseWidgetConfig failed:', type, err);
    return defaultConfigFor(type);
  }
}

/**
 * Frontend DTO types for GameToolkit (Issue #4976).
 *
 * Maps to C# backend record GameToolkitDto in:
 *   Api.BoundedContexts.GameToolkit.Application.DTOs.ToolkitDtos
 */

export interface DiceToolDto {
  name: string;
  diceType: string;
  quantity: number;
  customFaces?: string[] | null;
  isInteractive: boolean;
  color?: string | null;
}

export interface CardToolDto {
  name: string;
  deckType: string;
  cardCount: number;
  shuffleable: boolean;
  allowDraw: boolean;
  allowDiscard: boolean;
  allowPeek: boolean;
  allowReturnToDeck: boolean;
}

export interface TimerToolDto {
  name: string;
  durationSeconds: number;
  timerType: string;
  autoStart: boolean;
  color?: string | null;
  isPerPlayer: boolean;
  warningThresholdSeconds?: number | null;
}

export interface CounterToolDto {
  name: string;
  minValue: number;
  maxValue: number;
  defaultValue: number;
  isPerPlayer: boolean;
  icon?: string | null;
  color?: string | null;
}

export interface GameToolkitDto {
  id: string;
  gameId: string | null;
  privateGameId: string | null;
  name: string;
  version: number;
  /** Only published toolkits are applied to live sessions. */
  isPublished: boolean;
  /** When true, the base TurnOrderTool is hidden and the TurnTemplate config is used. */
  overridesTurnOrder: boolean;
  /** When true, the base Scoreboard is hidden and the ScoringTemplate config is used. */
  overridesScoreboard: boolean;
  /** When true, the base DiceRoller is hidden and custom diceTools are shown instead. */
  overridesDiceSet: boolean;
  diceTools: DiceToolDto[];
  cardTools: CardToolDto[];
  timerTools: TimerToolDto[];
  counterTools: CounterToolDto[];
}

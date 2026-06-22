/**
 * TurnIndicatorRendererLabels — Issue #2378 G5b.
 *
 * Shared labels interface consumed by all branch components.
 * Task 3 (TurnIndicatorRenderer) re-exports this from the dispatcher.
 */

export interface TurnIndicatorRendererLabels {
  readonly roundRobinHeading: string;
  readonly sequentialHeading: string;
  readonly simultaneousHeading: string;
  readonly realtimeHeading: string;
  readonly noneHeading: string;
  readonly customHeading: string;
  readonly firstPlayerTokenHeading: string;
  readonly unknownTitle: string;
  readonly unknownBody: string;
  readonly yourTurnLabel: string;
  readonly waitingLabel: string;
  readonly roundCountTemplate: string;
  readonly playOrderHeading: string;
  readonly firstPlayerTokenHolderTemplate: string;
}

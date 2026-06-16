/**
 * ToolkitRendererLabels — i18n surface for #2376 G5c ToolkitRenderer.
 *
 * Declared here so Task 3 widgets can import it; Task 4 (ToolkitRenderer)
 * will pass a concrete implementation. Task 5 will wire real i18n keys.
 *
 * @see docs/superpowers/specs/2026-06-16-issue-2376-g5c-toolkit-renderer-design.md §3
 */

export interface ToolkitRendererLabels {
  readonly title: string;
  readonly emptyTitle: string;
  readonly emptyBody: string;
  readonly unknownTitle: string;
  readonly unknownBody: string;
  readonly expandAriaTemplate: string; // "Espandi widget {name}"
  readonly collapseAriaTemplate: string; // "Comprimi widget {name}"
  readonly randomGenerator: {
    readonly heading: string;
    readonly rollLabel: string;
    readonly lastLabel: string;
  };
  readonly turnManager: {
    readonly heading: string;
    readonly prevLabel: string;
    readonly nextLabel: string;
    readonly turnOfLabel: string;
    readonly phaseLabel: string;
  };
  readonly scoreTracker: {
    readonly heading: string;
    readonly incrementAriaTemplate: string; // "Incrementa punteggio {name}"
    readonly decrementAriaTemplate: string; // "Decrementa punteggio {name}"
  };
  readonly resourceManager: {
    readonly heading: string;
    readonly sharedHeading: string;
    readonly incrementAriaTemplate: string; // "Incrementa {label}"
    readonly decrementAriaTemplate: string; // "Decrementa {label}"
  };
  readonly noteManager: {
    readonly heading: string;
    readonly inputAriaLabel: string;
    readonly savingLabel: string;
    readonly savedLabel: string;
  };
  readonly whiteboard: {
    readonly heading: string;
    readonly toolPenLabel: string;
    readonly toolEraserLabel: string;
    readonly toolCircleLabel: string;
    readonly placeholderLabel: string;
  };
}

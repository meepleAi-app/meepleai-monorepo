/**
 * View Transitions API Type Declarations
 *
 * The View Transitions API is not yet included in TypeScript's standard DOM
 * lib. These declarations cover the subset used by the useViewTransition hook.
 *
 * @see https://developer.mozilla.org/en-US/docs/Web/API/View_Transitions_API
 * @see https://drafts.csswg.org/css-view-transitions-1/
 */

interface ViewTransition {
  finished: Promise<void>;
  ready: Promise<void>;
  updateCallbackDone: Promise<void>;
}

interface Document {
  startViewTransition?(callback: () => void | Promise<void>): ViewTransition;
}

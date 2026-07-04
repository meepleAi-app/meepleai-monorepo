/**
 * Fixtures for GamebookIndexView story — DS-17 Phase D-2 (sub-issue #2174).
 *
 * @mockup admin-mockups/design_files/librogame-runthrough-library-search.html
 *
 * State-driving strategy (Template Mf — URL fixture override):
 *   All 6 FSM states are driven via `?fixture=<kind>` — no MSW handlers
 *   needed. The `?fixture=` URL override is enabled whenever
 *   `STATE_OVERRIDE_ENABLED` is true (dev + Storybook + visual-test builds).
 *
 *   The `loading` and `error` states short-circuit the FSM upstream
 *   (GamebookIndexView.tsx:197-200) before the fixture data is consumed,
 *   but the `?fixture=loading` / `?fixture=error` param still gates them
 *   into those cells deterministically.
 *
 *   | Frame                | FSM cell    | Driver               |
 *   |----------------------|-------------|----------------------|
 *   | Frame01_Default      | default     | ?fixture=default     |
 *   | Frame02_Empty        | empty       | ?fixture=empty       |
 *   | Frame03_QuotaSoft    | quota-soft  | ?fixture=quota-soft  |
 *   | Frame04_QuotaHard    | quota-hard  | ?fixture=quota-hard  |
 *   | Frame05_Loading      | loading     | ?fixture=loading     |
 *   | Frame06_Error        | error       | ?fixture=error       |
 *
 * MSW handlers: none — all states routed via the ?fixture= URL hatch.
 *
 * Refs: umbrella #2063, sub-issue #2174 (Phase D-2).
 */

// No exports — all states use the ?fixture= URL override via Storybook
// parameters.nextjs.navigation.query.
export {};

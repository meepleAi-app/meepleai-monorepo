/**
 * Visual level of a live event row. Maps to mockup .event-log .lvl colors:
 * - 'info' → blue (text-entity-chat or text-muted-foreground)
 * - 'ok'   → green (text-entity-toolkit) — successful completions
 * - 'warn' → amber/yellow (text-amber-600) — removals, retries
 * - 'err'  → red (text-entity-event) — failures, errors
 *
 * Derivation rules (suffix-based matching on eventType, lowercase):
 * - *.failed, *.error → 'err'
 * - *.created, *.indexed, *.finalized → 'ok'
 * - *.removed → 'warn'
 * - default → 'info'
 */
export type EventLevel = 'info' | 'ok' | 'warn' | 'err';

export function mapEventLevel(eventType: string): EventLevel {
  const lower = eventType.toLowerCase();

  // Error first (highest precedence)
  if (lower.endsWith('.failed') || lower.endsWith('.error')) {
    return 'err';
  }
  // Success / completion
  if (lower.endsWith('.created') || lower.endsWith('.indexed') || lower.endsWith('.finalized')) {
    return 'ok';
  }
  // Warning / removal
  if (lower.endsWith('.removed')) {
    return 'warn';
  }
  // Default
  return 'info';
}

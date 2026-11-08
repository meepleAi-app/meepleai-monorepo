/**
 * formatExecutionQuery — #4634.
 *
 * The server blanks the question of a RAG execution once it is older than the retention window
 * (`BackgroundJobs:RagExecutionTextRetention:RetentionDays`, 90 by default) and keeps the row with its metrics.
 * `query` is NOT NULL in the database and `RagExecution.Create` rejects a blank question, so an empty string
 * is the one sign that the text expired. Rendered as is it reads as "the question was empty" (a table cell with
 * nothing in it, or `""` in a label), so the admin pages show this instead.
 */
export const EXPIRED_QUERY_LABEL = '(testo scaduto)';

export function formatExecutionQuery(query: string | null | undefined): string {
  return query ? query : EXPIRED_QUERY_LABEL;
}

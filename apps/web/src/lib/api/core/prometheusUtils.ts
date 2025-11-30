/**
 * Prometheus Metrics Utilities
 *
 * Security-focused utilities for safely formatting Prometheus metrics.
 * Prevents injection attacks through proper escaping of special characters.
 *
 * References:
 * - Prometheus Data Model: https://prometheus.io/docs/concepts/data_model/
 * - Label naming: https://prometheus.io/docs/practices/naming/
 * - CodeQL CWE-116: Incomplete string escaping or encoding
 */

/**
 * Maximum allowed length for Prometheus label values.
 * Prometheus doesn't have a hard limit, but extremely long labels:
 * - Degrade query performance
 * - Increase memory usage
 * - Can indicate DoS attempts
 *
 * This is a reasonable limit that handles legitimate use cases while
 * protecting against abuse.
 */
export const MAX_LABEL_VALUE_LENGTH = 10000;

/**
 * Escapes a string value for use as a Prometheus label value.
 *
 * According to Prometheus specification, label values can contain any Unicode
 * characters, but the following must be escaped:
 * - Backslash (\) → Must be escaped FIRST to prevent double-escaping
 * - Double quote (") → Must be escaped SECOND
 * - Newline (\n) → Must be escaped to prevent label injection
 *
 * **Security Note:**
 * The order of escaping is critical! Backslashes must be escaped first,
 * otherwise we would escape the escape sequences themselves.
 *
 * Example attack without proper escaping:
 * ```
 * Input:  test\"
 * Wrong:  test\\" (backslash not escaped, quote remains unescaped when parsed)
 * Right:  test\\\\" (both backslash and quote properly escaped)
 * ```
 *
 * @param value - The string value to escape
 * @returns Safely escaped string for Prometheus label value
 *
 * @example
 * ```typescript
 * escapePrometheusLabelValue('api/users')       // 'api/users'
 * escapePrometheusLabelValue('test"quote')      // 'test\\"quote'
 * escapePrometheusLabelValue('test\\path')      // 'test\\\\path'
 * escapePrometheusLabelValue('test\\"attack')   // 'test\\\\\\"attack'
 * escapePrometheusLabelValue('line1\nline2')    // 'line1\\nline2'
 * ```
 */
export function escapePrometheusLabelValue(value: string): string {
  if (typeof value !== 'string') {
    throw new TypeError(
      `escapePrometheusLabelValue expects a string, got ${typeof value}`
    );
  }

  // Validate length to prevent DoS attacks via extremely long labels
  if (value.length > MAX_LABEL_VALUE_LENGTH) {
    throw new RangeError(
      `Label value too long (${value.length} characters). Maximum allowed: ${MAX_LABEL_VALUE_LENGTH}. ` +
        `This limit prevents performance degradation and potential DoS attacks.`
    );
  }

  // Note: This function IS the secure implementation.
  // The ESLint rule flags the .replace() pattern, but here it's correct
  // because backslashes are escaped FIRST, then quotes.
  /* eslint-disable local/no-incomplete-sanitization */
  return (
    value
      // 1. Escape backslashes FIRST (\ → \\)
      .replace(/\\/g, '\\\\')
      // 2. Escape double quotes SECOND (" → \")
      .replace(/"/g, '\\"')
      // 3. Escape newlines to prevent label injection (\n → \\n)
      .replace(/\n/g, '\\n')
      // 4. Escape carriage returns (\r → \\r)
      .replace(/\r/g, '\\r')
  );
  /* eslint-enable local/no-incomplete-sanitization */
}

/**
 * Escapes a string value for use as a Prometheus metric name or label name.
 *
 * According to Prometheus specification:
 * - Metric names: [a-zA-Z_:][a-zA-Z0-9_:]*
 * - Label names: [a-zA-Z_][a-zA-Z0-9_]*
 *
 * This function sanitizes invalid characters by replacing them with underscores.
 *
 * @param name - The metric or label name to sanitize
 * @returns Sanitized name that conforms to Prometheus naming rules
 *
 * @example
 * ```typescript
 * sanitizePrometheusName('my-metric')      // 'my_metric'
 * sanitizePrometheusName('123invalid')     // '_123invalid'
 * sanitizePrometheusName('test.name')      // 'test_name'
 * ```
 */
export function sanitizePrometheusName(name: string): string {
  if (typeof name !== 'string') {
    throw new TypeError(
      `sanitizePrometheusName expects a string, got ${typeof name}`
    );
  }

  // Replace invalid characters with underscores
  let sanitized = name.replace(/[^a-zA-Z0-9_:]/g, '_');

  // Ensure first character is valid ([a-zA-Z_:] for metrics, [a-zA-Z_] for labels)
  if (!/^[a-zA-Z_:]/.test(sanitized)) {
    sanitized = '_' + sanitized;
  }

  return sanitized;
}

/**
 * Formats a complete Prometheus metric line with proper escaping.
 *
 * @param metricName - The metric name (will be sanitized)
 * @param labels - Key-value pairs for labels (values will be escaped)
 * @param value - The metric value (number)
 * @param timestamp - Optional timestamp in milliseconds
 * @returns Formatted Prometheus metric line
 *
 * @example
 * ```typescript
 * formatPrometheusMetric('http_requests_total', { endpoint: '/api/users', method: 'GET' }, 42)
 * // 'http_requests_total{endpoint="/api/users",method="GET"} 42'
 *
 * formatPrometheusMetric('response_time', { path: 'test"quote' }, 123.45)
 * // 'response_time{path="test\\"quote"} 123.45'
 * ```
 */
export function formatPrometheusMetric(
  metricName: string,
  labels: Record<string, string | number>,
  value: number,
  timestamp?: number
): string {
  const sanitizedName = sanitizePrometheusName(metricName);

  const labelPairs = Object.entries(labels)
    .map(([key, val]) => {
      const sanitizedKey = sanitizePrometheusName(key);
      const escapedValue = escapePrometheusLabelValue(String(val));
      return `${sanitizedKey}="${escapedValue}"`;
    })
    .join(',');

  const labelsStr = labelPairs ? `{${labelPairs}}` : '';
  const timestampStr = timestamp ? ` ${timestamp}` : '';

  return `${sanitizedName}${labelsStr} ${value}${timestampStr}`;
}

/**
 * Validates that a string is safe for use as a Prometheus label value.
 *
 * This is useful for additional validation when accepting user input
 * that will be used in metrics.
 *
 * @param value - The value to validate
 * @returns true if the value is safe (doesn't need escaping)
 */
export function isPrometheusLabelValueSafe(value: string): boolean {
  // Safe values contain no special characters that need escaping
  return !/["\\\n\r]/.test(value);
}

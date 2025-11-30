/**
 * Tests for Prometheus Utilities
 *
 * Comprehensive security-focused tests for Prometheus metric formatting
 * to prevent injection attacks and ensure correct escaping.
 */

import {
  escapePrometheusLabelValue,
  sanitizePrometheusName,
  formatPrometheusMetric,
  isPrometheusLabelValueSafe,
} from '../prometheusUtils';

describe('escapePrometheusLabelValue', () => {
  describe('Basic functionality', () => {
    it('should not modify safe strings', () => {
      expect(escapePrometheusLabelValue('safe_string')).toBe('safe_string');
      expect(escapePrometheusLabelValue('api/users/123')).toBe('api/users/123');
      expect(escapePrometheusLabelValue('GET')).toBe('GET');
    });

    it('should escape double quotes', () => {
      expect(escapePrometheusLabelValue('test"quote')).toBe('test\\"quote');
      expect(escapePrometheusLabelValue('multiple"quotes"here')).toBe(
        'multiple\\"quotes\\"here'
      );
    });

    it('should escape backslashes', () => {
      expect(escapePrometheusLabelValue('test\\path')).toBe('test\\\\path');
      expect(escapePrometheusLabelValue('C:\\Windows\\System32')).toBe(
        'C:\\\\Windows\\\\System32'
      );
    });

    it('should escape newlines', () => {
      expect(escapePrometheusLabelValue('line1\nline2')).toBe('line1\\nline2');
      expect(escapePrometheusLabelValue('multi\nline\ntext')).toBe(
        'multi\\nline\\ntext'
      );
    });

    it('should escape carriage returns', () => {
      expect(escapePrometheusLabelValue('line1\rline2')).toBe('line1\\rline2');
      expect(escapePrometheusLabelValue('windows\r\nline')).toBe(
        'windows\\r\\nline'
      );
    });
  });

  describe('Security: Injection attack prevention (CWE-116)', () => {
    it('should prevent quote injection attacks', () => {
      // Attack: Trying to close the label and inject new labels
      const attack1 = 'value",malicious="attack';
      expect(escapePrometheusLabelValue(attack1)).toBe(
        'value\\",malicious=\\"attack'
      );

      // Attack: Trying to inject a new metric
      const attack2 = 'value"\nmalicious_metric 1';
      expect(escapePrometheusLabelValue(attack2)).toBe(
        'value\\"\\nmalicious_metric 1'
      );
    });

    it('should prevent backslash-quote injection attacks', () => {
      // Critical: This is the main vulnerability CodeQL detected
      // Input: test\" (backslash-quote)
      // Wrong escaping: test\\" (backslash escapes itself, quote is still unescaped)
      // Correct escaping: test\\\\" (both are properly escaped)

      const attack = 'test\\"';
      const escaped = escapePrometheusLabelValue(attack);

      // Should escape backslash first (\ → \\), then quote (" → \")
      // Result: test → test\\ → test\\\\ then " → \\"
      expect(escaped).toBe('test\\\\\\"');

      // Verify the escaped string doesn't allow injection
      // When Prometheus parses: test\\\\" → test\"
      // The quote is properly escaped and won't close the label
    });

    it('should handle complex injection attempts', () => {
      // Multiple backslashes followed by quote
      expect(escapePrometheusLabelValue('test\\\\"')).toBe('test\\\\\\\\\\"');

      // Backslash at end followed by quote in next call
      expect(escapePrometheusLabelValue('test\\')).toBe('test\\\\');

      // Mixed special characters
      expect(escapePrometheusLabelValue('path\\to"file\nline2')).toBe(
        'path\\\\to\\"file\\nline2'
      );
    });

    it('should prevent metric injection via newlines', () => {
      // Attack: Inject a new metric line
      const attack = 'value\n# HELP malicious_metric\nmalicious_metric 1';
      expect(escapePrometheusLabelValue(attack)).toBe(
        'value\\n# HELP malicious_metric\\nmalicious_metric 1'
      );
    });

    it('should handle empty strings', () => {
      expect(escapePrometheusLabelValue('')).toBe('');
    });

    it('should handle strings with only special characters', () => {
      // Input: quote, backslash, newline char, CR char
      // Output: \", \\, \n (literal), \r (literal)
      const result = escapePrometheusLabelValue('"\\\n\r');
      expect(result).toBe('\\"\\\\\\n\\r');  // Fixed: removed extra backslash that created real \n in expected
    });
  });

  describe('Edge cases', () => {
    it('should handle Unicode characters', () => {
      expect(escapePrometheusLabelValue('тест')).toBe('тест');
      expect(escapePrometheusLabelValue('测试')).toBe('测试');
      expect(escapePrometheusLabelValue('🚀')).toBe('🚀');
    });

    it('should handle very long strings up to MAX_LABEL_VALUE_LENGTH', () => {
      // At the limit (10000 chars) - should work
      const longString = 'a'.repeat(10000);
      expect(escapePrometheusLabelValue(longString)).toBe(longString);

      // With special char but still at limit (4999 + 1 + 5000 = 10000)
      const longWithSpecial = 'a'.repeat(4999) + '"' + 'b'.repeat(5000);
      expect(escapePrometheusLabelValue(longWithSpecial)).toContain('\\"');
    });

    it('should throw RangeError for strings exceeding MAX_LABEL_VALUE_LENGTH', () => {
      // Over the limit (10001 chars) - should throw
      const tooLongString = 'a'.repeat(10001);
      expect(() => escapePrometheusLabelValue(tooLongString)).toThrow(RangeError);
      expect(() => escapePrometheusLabelValue(tooLongString)).toThrow(
        /Label value too long \(10001 characters\)/
      );

      // Way over the limit - potential DoS attempt
      const maliciousLongString = 'a'.repeat(1000000);
      expect(() => escapePrometheusLabelValue(maliciousLongString)).toThrow(RangeError);
    });

    it('should throw TypeError for non-string input', () => {
      expect(() => escapePrometheusLabelValue(null as any)).toThrow(TypeError);
      expect(() => escapePrometheusLabelValue(undefined as any)).toThrow(
        TypeError
      );
      expect(() => escapePrometheusLabelValue(123 as any)).toThrow(TypeError);
      expect(() => escapePrometheusLabelValue({} as any)).toThrow(TypeError);
    });
  });

  describe('Real-world scenarios', () => {
    it('should handle API endpoint paths', () => {
      expect(escapePrometheusLabelValue('/api/v1/users')).toBe(
        '/api/v1/users'
      );
      expect(escapePrometheusLabelValue('/api/search?q="test"')).toBe(
        '/api/search?q=\\"test\\"'
      );
    });

    it('should handle error messages', () => {
      const errorMsg = 'Error: File "config.json" not found';
      expect(escapePrometheusLabelValue(errorMsg)).toBe(
        'Error: File \\"config.json\\" not found'
      );
    });

    it('should handle file paths (Windows)', () => {
      const windowsPath = 'C:\\Program Files\\App\\config.ini';
      expect(escapePrometheusLabelValue(windowsPath)).toBe(
        'C:\\\\Program Files\\\\App\\\\config.ini'
      );
    });

    it('should handle file paths (Unix)', () => {
      const unixPath = '/var/log/app.log';
      expect(escapePrometheusLabelValue(unixPath)).toBe('/var/log/app.log');
    });
  });
});

describe('sanitizePrometheusName', () => {
  it('should not modify valid names', () => {
    expect(sanitizePrometheusName('valid_name')).toBe('valid_name');
    expect(sanitizePrometheusName('http_requests_total')).toBe(
      'http_requests_total'
    );
    expect(sanitizePrometheusName('metric:colon:name')).toBe(
      'metric:colon:name'
    );
  });

  it('should replace invalid characters with underscores', () => {
    expect(sanitizePrometheusName('invalid-name')).toBe('invalid_name');
    expect(sanitizePrometheusName('name.with.dots')).toBe('name_with_dots');
    expect(sanitizePrometheusName('name with spaces')).toBe(
      'name_with_spaces'
    );
  });

  it('should ensure first character is valid', () => {
    expect(sanitizePrometheusName('123invalid')).toBe('_123invalid');
    expect(sanitizePrometheusName('-invalid')).toBe('_invalid');
  });

  it('should handle empty strings', () => {
    expect(sanitizePrometheusName('')).toBe('_');
  });

  it('should throw TypeError for non-string input', () => {
    expect(() => sanitizePrometheusName(null as any)).toThrow(TypeError);
    expect(() => sanitizePrometheusName(undefined as any)).toThrow(TypeError);
  });
});

describe('formatPrometheusMetric', () => {
  it('should format metric without labels', () => {
    expect(formatPrometheusMetric('my_metric', {}, 42)).toBe('my_metric 42');
  });

  it('should format metric with labels', () => {
    const result = formatPrometheusMetric(
      'http_requests_total',
      { endpoint: '/api/users', method: 'GET' },
      100
    );

    // Note: Object.entries() order may vary, so check both possibilities
    const expected1 =
      'http_requests_total{endpoint="/api/users",method="GET"} 100';
    const expected2 =
      'http_requests_total{method="GET",endpoint="/api/users"} 100';

    expect([expected1, expected2]).toContain(result);
  });

  it('should format metric with timestamp', () => {
    expect(formatPrometheusMetric('my_metric', {}, 42, 1234567890)).toBe(
      'my_metric 42 1234567890'
    );
  });

  it('should escape label values', () => {
    const result = formatPrometheusMetric(
      'error_count',
      { message: 'Error: "file not found"' },
      1
    );

    expect(result).toContain('message="Error: \\"file not found\\""');
  });

  it('should sanitize metric name', () => {
    const result = formatPrometheusMetric('invalid-name', {}, 42);
    expect(result).toBe('invalid_name 42');
  });

  it('should sanitize label keys', () => {
    const result = formatPrometheusMetric(
      'my_metric',
      { 'invalid-key': 'value' },
      42
    );

    expect(result).toContain('invalid_key="value"');
  });

  it('should handle numeric label values', () => {
    const result = formatPrometheusMetric(
      'my_metric',
      { status_code: 200 },
      42
    );

    expect(result).toContain('status_code="200"');
  });

  it('should handle complex security scenario', () => {
    const result = formatPrometheusMetric(
      'api_errors',
      {
        endpoint: '/api/test\\"malicious',
        error: 'Error\n"injection"',
      },
      1
    );

    // Both label values should be properly escaped
    expect(result).toContain('/api/test\\\\\\"malicious');
    expect(result).toContain('Error\\n\\"injection\\"');
  });
});

describe('isPrometheusLabelValueSafe', () => {
  it('should return true for safe strings', () => {
    expect(isPrometheusLabelValueSafe('safe_string')).toBe(true);
    expect(isPrometheusLabelValueSafe('api/users/123')).toBe(true);
    expect(isPrometheusLabelValueSafe('GET')).toBe(true);
  });

  it('should return false for strings with special characters', () => {
    expect(isPrometheusLabelValueSafe('test"quote')).toBe(false);
    expect(isPrometheusLabelValueSafe('test\\path')).toBe(false);
    expect(isPrometheusLabelValueSafe('line1\nline2')).toBe(false);
    expect(isPrometheusLabelValueSafe('line1\rline2')).toBe(false);
  });

  it('should handle empty strings', () => {
    expect(isPrometheusLabelValueSafe('')).toBe(true);
  });
});

describe('Integration: Real-world Prometheus output', () => {
  it('should generate valid Prometheus metrics for circuit breaker', () => {
    const metrics = [
      formatPrometheusMetric(
        'http_circuit_breaker_state',
        { endpoint: '/api/users', state: 'CLOSED' },
        0
      ),
      formatPrometheusMetric(
        'http_circuit_breaker_requests_total',
        { endpoint: '/api/users' },
        100
      ),
    ];

    const output = metrics.join('\n');

    expect(output).toContain('http_circuit_breaker_state');
    expect(output).toContain('endpoint="/api/users"');
    expect(output).not.toContain('endpoint=\\"'); // Should not double-escape
  });

  it('should handle malicious endpoint names safely', () => {
    // Simulate an attack where someone tries to inject malicious metrics
    const maliciousEndpoint = '/api/test"\nmalicious_metric{} 999\n# ';

    const result = formatPrometheusMetric(
      'http_requests',
      { endpoint: maliciousEndpoint },
      1
    );

    // The malicious content should be escaped and rendered harmless
    expect(result).toContain('\\n');
    expect(result).toContain('\\"');
    expect(result).not.toContain('\nmalicious_metric');
  });

  it('should produce parseable Prometheus format', () => {
    const lines = [
      '# HELP http_requests_total Total HTTP requests',
      '# TYPE http_requests_total counter',
      formatPrometheusMetric(
        'http_requests_total',
        { endpoint: '/api/users', method: 'GET' },
        42
      ),
      formatPrometheusMetric(
        'http_requests_total',
        { endpoint: '/api/games', method: 'POST' },
        13
      ),
    ];

    const output = lines.join('\n') + '\n';

    // Basic sanity checks that output looks like valid Prometheus format
    expect(output).toContain('# HELP');
    expect(output).toContain('# TYPE');
    expect(output).toContain('http_requests_total{');
    expect(output).toContain('} 42');
    expect(output).toContain('} 13');
  });
});

describe('Performance: Benchmark Tests', () => {
  it('should escape strings efficiently (< 1ms for typical strings)', () => {
    const typicalString = '/api/v1/users?query="test"&page=1';
    const iterations = 1000;

    const start = performance.now();
    for (let i = 0; i < iterations; i++) {
      escapePrometheusLabelValue(typicalString);
    }
    const end = performance.now();

    const avgTime = (end - start) / iterations;

    // Should be very fast (< 0.01ms per call on modern hardware)
    expect(avgTime).toBeLessThan(0.1);

    // Log for informational purposes (will show in verbose test output)
    if (avgTime > 0.01) {
      console.warn(
        `Performance warning: escapePrometheusLabelValue took ${avgTime.toFixed(4)}ms on average (expected < 0.01ms)`
      );
    }
  });

  it('should handle high-volume metrics formatting efficiently', () => {
    const endpoints = [
      '/api/v1/users',
      '/api/v1/games',
      '/api/v1/chat',
      '/api/v1/auth',
      '/api/v1/admin',
    ];

    const iterations = 100;

    const start = performance.now();
    for (let i = 0; i < iterations; i++) {
      endpoints.forEach((endpoint) => {
        formatPrometheusMetric(
          'http_requests_total',
          { endpoint, method: 'GET', status: '200' },
          i
        );
      });
    }
    const end = performance.now();

    const totalTime = end - start;
    const avgTime = totalTime / (iterations * endpoints.length);

    // Should handle 500 metrics in reasonable time (< 50ms total)
    expect(totalTime).toBeLessThan(50);

    // Log throughput for informational purposes
    const throughput = (iterations * endpoints.length) / (totalTime / 1000);
    if (totalTime > 20) {
      console.warn(
        `Performance info: Formatted ${iterations * endpoints.length} metrics in ${totalTime.toFixed(2)}ms (${throughput.toFixed(0)} metrics/sec)`
      );
    }
  });

  it('should maintain performance with realistic workload', () => {
    // Simulate real-world Prometheus export with 50 metrics
    const metricCount = 50;
    const lines: string[] = [];

    const start = performance.now();

    lines.push('# HELP http_requests_total Total HTTP requests');
    lines.push('# TYPE http_requests_total counter');

    for (let i = 0; i < metricCount; i++) {
      const endpoint = `/api/v${i % 3}/endpoint${i}`;
      lines.push(
        formatPrometheusMetric(
          'http_requests_total',
          { endpoint, method: 'GET' },
          Math.floor(Math.random() * 1000)
        )
      );
    }

    const output = lines.join('\n') + '\n';
    const end = performance.now();

    const totalTime = end - start;

    // Should generate 50 metrics in < 5ms
    expect(totalTime).toBeLessThan(5);

    // Verify output is correct
    expect(output.split('\n').length).toBeGreaterThan(50);
  });
});

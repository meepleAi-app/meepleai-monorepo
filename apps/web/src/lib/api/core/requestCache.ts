/**
 * Request Deduplication Cache (Issue #1454, #1453)
 *
 * Prevents identical simultaneous requests from hitting the backend multiple times,
 * reducing server load and improving performance.
 *
 * Features:
 * - TTL-based deduplication (default: 100ms)
 * - LRU eviction (max 100 concurrent requests)
 * - Cache key: method + URL + body hash + auth context + request options
 * - Metrics for cache hits/misses
 * - Opt-in/opt-out per request
 * - Option-sensitive caching (circuit breaker, retry config)
 */

/**
 * Configuration for request cache
 */
export interface RequestCacheConfig {
  /**
   * Enable/disable request deduplication
   * @default true
   */
  enabled: boolean;

  /**
   * Time-to-live for cached requests in milliseconds
   * @default 100
   */
  ttl: number;

  /**
   * Maximum number of concurrent cached requests
   * @default 100
   */
  maxSize: number;
}

/**
 * Cache entry storing pending promise and metadata
 */
interface CacheEntry<T> {
  /** Pending promise */
  promise: Promise<T>;
  /** Timestamp when entry was created */
  timestamp: number;
  /** Cache key for LRU tracking */
  key: string;
}

/**
 * Metrics tracking cache performance
 */
export interface CacheMetrics {
  /** Number of cache hits */
  hits: number;
  /** Number of cache misses */
  misses: number;
  /** Current cache size */
  size: number;
  /** Number of evictions due to size limit */
  evictions: number;
  /** Number of expirations due to TTL */
  expirations: number;
}

/**
 * Maximum size for request body hashing to prevent DoS
 */
const MAX_HASH_INPUT_SIZE = 10000;

/**
 * Option-sensitive fields that should be included in cache key
 * to prevent requests with different options from sharing cached promises.
 *
 * This is a subset of RequestOptions (from httpClient.ts) that contains only
 * the fields that affect request behavior (circuit breaker, retry logic).
 * Custom callbacks (like onRetry) are handled separately by disabling deduplication
 * when they are present.
 *
 * @see RequestOptions in httpClient.ts
 */
export interface CacheKeyOptions {
  /** Whether circuit breaker is disabled for this request */
  skipCircuitBreaker?: boolean;
  /** Whether retry is disabled for this request */
  skipRetry?: boolean;
  /** Retry configuration that affects request behavior */
  retryConfig?: {
    maxAttempts?: number;
    baseDelay?: number;
    maxDelay?: number;
    enabled?: boolean;
    jitter?: number;
  };
}

/**
 * Request deduplication cache with TTL and LRU eviction
 */
export class RequestCache {
  private cache: Map<string, CacheEntry<unknown>> = new Map();
  private accessOrder: Map<string, boolean> = new Map();
  private timeouts: Map<string, ReturnType<typeof setTimeout>> = new Map();
  private config: RequestCacheConfig;
  private metrics: CacheMetrics = {
    hits: 0,
    misses: 0,
    size: 0,
    evictions: 0,
    expirations: 0,
  };

  constructor(config?: Partial<RequestCacheConfig>) {
    const rawConfig = {
      enabled: this.getBooleanEnv('NEXT_PUBLIC_REQUEST_DEDUP_ENABLED', true),
      ttl: this.getNumberEnv('NEXT_PUBLIC_REQUEST_DEDUP_TTL', 100),
      maxSize: this.getNumberEnv('NEXT_PUBLIC_REQUEST_DEDUP_MAX_SIZE', 100),
      ...config,
    };

    // Validate configuration
    this.config = this.validateConfig(rawConfig);
  }

  /**
   * Get cached promise or execute request function
   */
  async dedupe<T>(
    cacheKey: string,
    requestFn: () => Promise<T>,
    skipDedup = false
  ): Promise<T> {
    // Skip deduplication if disabled or explicitly skipped
    if (!this.config.enabled || skipDedup) {
      return requestFn();
    }

    // Check for existing cached request
    const cached = this.get<T>(cacheKey);
    if (cached) {
      this.metrics.hits++;
      this.updateAccessOrder(cacheKey);
      return cached;
    }

    // Execute request and cache the promise
    this.metrics.misses++;
    const promise = requestFn();

    // Cache the promise
    this.set(cacheKey, promise);

    // Clean up cache entry after promise settles (success or failure)
    promise
      .then(() => this.scheduleCleanup(cacheKey))
      .catch(() => {
        // Remove failed requests immediately to allow retry
        this.delete(cacheKey);
      });

    return promise;
  }

  /**
   * Generate cache key from request parameters
   */
  generateKey(
    method: string,
    url: string,
    body?: unknown,
    authContext?: string,
    options?: CacheKeyOptions
  ): string {
    const parts = [method.toUpperCase(), url];

    // Add body hash if present
    if (body !== undefined && body !== null) {
      const bodyHash = this.hashObject(body);
      parts.push(bodyHash);
    }

    // Add auth context if present
    if (authContext) {
      parts.push(authContext);
    }

    // Add option-sensitive fields to prevent requests with different options
    // from sharing cached promises (Issue #1453)
    if (options) {
      const optionsHash = this.hashOptions(options);
      if (optionsHash) {
        parts.push(optionsHash);
      }
    }

    return parts.join('::');
  }

  /**
   * Get metrics for monitoring
   */
  getMetrics(): CacheMetrics {
    return {
      ...this.metrics,
      size: this.cache.size,
    };
  }

  /**
   * Clear all cached entries and reset metrics
   */
  clear(): void {
    // Clear all pending timeouts to prevent memory leaks
    for (const timeout of this.timeouts.values()) {
      clearTimeout(timeout);
    }

    this.cache.clear();
    this.accessOrder.clear();
    this.timeouts.clear();

    // Reset all metrics
    this.metrics = {
      hits: 0,
      misses: 0,
      size: 0,
      evictions: 0,
      expirations: 0,
    };
  }

  /**
   * Get configuration
   */
  getConfig(): RequestCacheConfig {
    return { ...this.config };
  }

  /**
   * Update configuration (validates new values)
   */
  updateConfig(config: Partial<RequestCacheConfig>): void {
    this.config = this.validateConfig({ ...this.config, ...config });
  }

  /**
   * Validate configuration values
   */
  private validateConfig(config: RequestCacheConfig): RequestCacheConfig {
    const validated = { ...config };

    if (validated.ttl < 0) {
      console.warn(
        `RequestCache: TTL cannot be negative (${validated.ttl}), using default 100ms`
      );
      validated.ttl = 100;
    }

    if (validated.maxSize < 1) {
      console.warn(
        `RequestCache: maxSize must be >= 1 (${validated.maxSize}), using default 100`
      );
      validated.maxSize = 100;
    }

    return validated;
  }

  /**
   * Get cached entry if not expired
   */
  private get<T>(key: string): Promise<T> | null {
    const entry = this.cache.get(key) as CacheEntry<T> | undefined;

    if (!entry) {
      return null;
    }

    // Check if entry has expired
    const age = Date.now() - entry.timestamp;
    if (age > this.config.ttl) {
      this.delete(key);
      this.metrics.expirations++;
      return null;
    }

    return entry.promise;
  }

  /**
   * Set cache entry with LRU eviction
   */
  private set<T>(key: string, promise: Promise<T>): void {
    // Evict oldest entry if cache is full
    if (this.cache.size >= this.config.maxSize) {
      this.evictOldest();
    }

    const entry: CacheEntry<T> = {
      promise,
      timestamp: Date.now(),
      key,
    };

    this.cache.set(key, entry as CacheEntry<unknown>);
    this.accessOrder.set(key, true);
  }

  /**
   * Delete cache entry and cleanup timeout
   */
  private delete(key: string): void {
    // Clear timeout if present
    const timeout = this.timeouts.get(key);
    if (timeout) {
      clearTimeout(timeout);
      this.timeouts.delete(key);
    }

    this.cache.delete(key);
    this.accessOrder.delete(key);
  }

  /**
   * Schedule cleanup after TTL
   */
  private scheduleCleanup(key: string): void {
    // Clear existing timeout if present
    const existingTimeout = this.timeouts.get(key);
    if (existingTimeout) {
      clearTimeout(existingTimeout);
    }

    // Schedule new timeout
    const timeout = setTimeout(() => {
      this.delete(key);
    }, this.config.ttl);

    this.timeouts.set(key, timeout);
  }

  /**
   * Evict least recently used entry
   */
  private evictOldest(): void {
    // Map maintains insertion order, first key is oldest
    const oldestKey = this.accessOrder.keys().next().value;
    if (oldestKey) {
      this.delete(oldestKey);
      this.metrics.evictions++;
    }
  }

  /**
   * Update access order for LRU tracking (O(1) operation)
   */
  private updateAccessOrder(key: string): void {
    // Map maintains insertion order
    // Delete and re-add to move to end (most recently used)
    this.accessOrder.delete(key);
    this.accessOrder.set(key, true);
  }

  /**
   * Hash option-sensitive fields for cache key generation
   */
  private hashOptions(options: CacheKeyOptions): string {
    // Only include fields that affect request behavior
    const normalized: Record<string, unknown> = {};

    if (options.skipCircuitBreaker !== undefined) {
      normalized.skipCircuitBreaker = options.skipCircuitBreaker;
    }

    if (options.skipRetry !== undefined) {
      normalized.skipRetry = options.skipRetry;
    }

    if (options.retryConfig) {
      normalized.retryConfig = options.retryConfig;
    }

    // If no option-sensitive fields are present, return empty string
    if (Object.keys(normalized).length === 0) {
      return '';
    }

    // Use the same hashing logic as for request bodies
    return this.hashObject(normalized);
  }

  /**
   * Hash object for cache key generation with collision prevention
   */
  private hashObject(obj: unknown): string {
    try {
      // Sort object keys for consistent hashing
      const sortedStr = this.stringifyWithSortedKeys(obj);

      // Prevent DoS via large request bodies
      if (sortedStr.length > MAX_HASH_INPUT_SIZE) {
        console.warn(
          `RequestCache: Request body too large for caching (${sortedStr.length} chars), forcing cache miss`
        );
        // Return random hash to force cache miss
        return Math.random().toString(36).substring(2);
      }

      // Simple hash function (djb2)
      let hash = 0;
      for (let i = 0; i < sortedStr.length; i++) {
        const char = sortedStr.charCodeAt(i);
        hash = (hash << 5) - hash + char;
        hash = hash & hash; // Convert to 32-bit integer
      }

      return hash.toString(36);
    } catch (error) {
      // Handle circular references or other JSON errors
      console.warn('RequestCache: Failed to hash object', error);
      // Return random hash to force cache miss
      return Math.random().toString(36).substring(2);
    }
  }

  /**
   * Stringify object with sorted keys for consistent hashing
   */
  private stringifyWithSortedKeys(obj: unknown): string {
    if (obj === null || obj === undefined) {
      return JSON.stringify(obj);
    }

    if (typeof obj !== 'object') {
      return JSON.stringify(obj);
    }

    if (Array.isArray(obj)) {
      return `[${obj.map((item) => this.stringifyWithSortedKeys(item)).join(',')}]`;
    }

    // Sort object keys
    const sortedKeys = Object.keys(obj).sort();
    const pairs = sortedKeys.map((key) => {
      const value = (obj as Record<string, unknown>)[key];
      return `"${key}":${this.stringifyWithSortedKeys(value)}`;
    });

    return `{${pairs.join(',')}}`;
  }

  /**
   * Get boolean environment variable
   */
  private getBooleanEnv(key: string, defaultValue: boolean): boolean {
    const value = process.env[key];
    if (value === undefined || value === null) {
      return defaultValue;
    }
    return value.toLowerCase() === 'true';
  }

  /**
   * Get number environment variable
   */
  private getNumberEnv(key: string, defaultValue: number): number {
    const value = process.env[key];
    if (value === undefined || value === null) {
      return defaultValue;
    }
    const parsed = parseInt(value, 10);
    return isNaN(parsed) ? defaultValue : parsed;
  }

  /**
   * Export metrics in Prometheus format
   */
  toPrometheusFormat(): string {
    const lines: string[] = [];
    const metrics = this.getMetrics();

    // Cache hits counter
    lines.push('# HELP http_client_cache_hits_total Total number of cache hits');
    lines.push('# TYPE http_client_cache_hits_total counter');
    lines.push(`http_client_cache_hits_total ${metrics.hits}`);

    // Cache misses counter
    lines.push('# HELP http_client_cache_misses_total Total number of cache misses');
    lines.push('# TYPE http_client_cache_misses_total counter');
    lines.push(`http_client_cache_misses_total ${metrics.misses}`);

    // Cache hit rate gauge (computed metric)
    const totalRequests = metrics.hits + metrics.misses;
    const hitRate = totalRequests > 0 ? (metrics.hits / totalRequests) * 100 : 0;
    lines.push('# HELP http_client_cache_hit_rate_percent Cache hit rate percentage');
    lines.push('# TYPE http_client_cache_hit_rate_percent gauge');
    lines.push(`http_client_cache_hit_rate_percent ${hitRate.toFixed(2)}`);

    // Cache size gauge
    lines.push('# HELP http_client_cache_size Current number of cached entries');
    lines.push('# TYPE http_client_cache_size gauge');
    lines.push(`http_client_cache_size ${metrics.size}`);

    // Cache evictions counter
    lines.push('# HELP http_client_cache_evictions_total Total number of cache evictions (LRU)');
    lines.push('# TYPE http_client_cache_evictions_total counter');
    lines.push(`http_client_cache_evictions_total ${metrics.evictions}`);

    // Cache expirations counter
    lines.push('# HELP http_client_cache_expirations_total Total number of cache expirations (TTL)');
    lines.push('# TYPE http_client_cache_expirations_total counter');
    lines.push(`http_client_cache_expirations_total ${metrics.expirations}`);

    return lines.join('\n') + '\n';
  }
}

/**
 * Singleton instance for global request cache
 */
export const globalRequestCache = new RequestCache();

/**
 * Export cache metrics in Prometheus format
 */
export function exportCacheMetricsPrometheus(): string {
  return globalRequestCache.toPrometheusFormat();
}

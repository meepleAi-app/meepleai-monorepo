import fs from 'fs';
import path from 'path';

/**
 * Memory Monitor for E2E Test Suite
 *
 * Issue #2008: Phase 2 - Memory Monitoring
 * Tracks heap memory usage during test execution to detect memory pressure
 * and provide visibility into resource consumption patterns.
 *
 * Uses file-based persistence to work across globalSetup/globalTeardown process boundaries.
 */

export interface MemorySample {
  timestamp: number;
  heapUsed: number;
  heapTotal: number;
  external: number;
  rss: number;
}

export interface MemoryReport {
  peakHeapUsed: number;
  avgHeapUsed: number;
  peakHeapTotal: number;
  samples: number;
  duration: number;
  alerts: string[];
}

export class MemoryMonitor {
  private intervalId: NodeJS.Timeout | null = null;
  private logFilePath: string;
  private alertThreshold: number; // in bytes

  constructor(
    logFilePath: string = path.join(__dirname, '../.memory-log.json'),
    alertThreshold: number = 3.5 * 1024 * 1024 * 1024 // 3.5GB
  ) {
    this.logFilePath = logFilePath;
    this.alertThreshold = alertThreshold;
  }

  /**
   * Start monitoring memory usage at the specified interval
   */
  start(intervalMs: number = 10000): void {
    console.log(`[MemoryMonitor] Starting memory monitoring (interval: ${intervalMs}ms)`);

    try {
      // Ensure directory exists
      const dir = path.dirname(this.logFilePath);
      if (!fs.existsSync(dir)) {
        fs.mkdirSync(dir, { recursive: true });
      }

      // Initialize log file with empty samples array
      const initialData = {
        startTime: Date.now(),
        samples: [] as MemorySample[],
        alerts: [] as string[],
      };
      fs.writeFileSync(this.logFilePath, JSON.stringify(initialData, null, 2));

      // Start periodic sampling
      this.intervalId = setInterval(() => {
        this.recordSample();
      }, intervalMs);

      // Record initial sample immediately
      this.recordSample();
    } catch (error) {
      console.error('[MemoryMonitor] Failed to start monitoring:', error);
      throw error; // Fail fast if monitoring can't initialize
    }
  }

  /**
   * Record a single memory sample using atomic write pattern
   */
  private recordSample(): void {
    try {
      const usage = process.memoryUsage();
      const sample: MemorySample = {
        timestamp: Date.now(),
        heapUsed: usage.heapUsed,
        heapTotal: usage.heapTotal,
        external: usage.external,
        rss: usage.rss,
      };

      // Read current data
      const data = JSON.parse(fs.readFileSync(this.logFilePath, 'utf-8'));
      data.samples.push(sample);

      // Check for threshold alerts
      if (usage.heapUsed > this.alertThreshold) {
        const alert = `⚠️ Memory usage approaching limit: ${(usage.heapUsed / 1024 / 1024 / 1024).toFixed(2)}GB / ${(this.alertThreshold / 1024 / 1024 / 1024).toFixed(2)}GB`;
        console.warn(`[MemoryMonitor] ${alert}`);
        data.alerts.push(alert);
      }

      // Atomic write: write to temp file, then rename (atomic operation on all platforms)
      const tmpPath = this.logFilePath + '.tmp';
      fs.writeFileSync(tmpPath, JSON.stringify(data, null, 2));
      fs.renameSync(tmpPath, this.logFilePath);

      // Log current usage (every 5 samples to reduce noise)
      if (data.samples.length % 5 === 0) {
        console.log(
          `[MemoryMonitor] Heap: ${(usage.heapUsed / 1024 / 1024).toFixed(2)}MB / ${(usage.heapTotal / 1024 / 1024).toFixed(2)}MB (Samples: ${data.samples.length})`
        );
      }
    } catch (error) {
      // Don't crash the test suite if monitoring fails
      console.error('[MemoryMonitor] Failed to record sample:', error);
    }
  }

  /**
   * Stop monitoring
   */
  stop(): void {
    if (this.intervalId) {
      clearInterval(this.intervalId);
      this.intervalId = null;
      console.warn('[MemoryMonitor] Stopped memory monitoring');
    }
  }

  /**
   * Generate memory report from log file
   * Static method to work across process boundaries
   */
  static getReport(
    logFilePath: string = path.join(__dirname, '../.memory-log.json')
  ): MemoryReport | null {
    try {
      if (!fs.existsSync(logFilePath)) {
        console.warn('[MemoryMonitor] No memory log file found');
        return null;
      }

      const data = JSON.parse(fs.readFileSync(logFilePath, 'utf-8'));
      const samples = data.samples as MemorySample[];

      if (samples.length === 0) {
        return {
          peakHeapUsed: 0,
          avgHeapUsed: 0,
          peakHeapTotal: 0,
          samples: 0,
          duration: 0,
          alerts: data.alerts || [],
        };
      }

      const peakHeapUsed = Math.max(...samples.map(s => s.heapUsed));
      const avgHeapUsed = samples.reduce((sum, s) => sum + s.heapUsed, 0) / samples.length;
      const peakHeapTotal = Math.max(...samples.map(s => s.heapTotal));
      const duration = samples[samples.length - 1].timestamp - samples[0].timestamp;

      return {
        peakHeapUsed,
        avgHeapUsed,
        peakHeapTotal,
        samples: samples.length,
        duration,
        alerts: data.alerts || [],
      };
    } catch (error) {
      console.error('[MemoryMonitor] Error reading memory log:', error);
      return null;
    }
  }

  /**
   * Clean up log file
   */
  static cleanup(logFilePath: string = path.join(__dirname, '../.memory-log.json')): void {
    try {
      if (fs.existsSync(logFilePath)) {
        fs.unlinkSync(logFilePath);
        console.warn('[MemoryMonitor] Cleaned up memory log file');
      }
    } catch (error) {
      console.error('[MemoryMonitor] Error cleaning up memory log:', error);
    }
  }
}

'use client';

import { Suspense } from 'react';

import { Skeleton } from '@/components/ui/feedback/skeleton';

/**
 * System Health Page
 * Route: /admin/overview/system
 *
 * Shows service status grid, resource usage, and error rates.
 *
 * Issue: #4629
 */

// Mock service data - replace with API
const services = [
  { name: 'API Server', status: 'healthy', uptime: 99.9, latency: 45, category: 'Core' },
  { name: 'PostgreSQL', status: 'healthy', uptime: 99.8, latency: 12, category: 'Database' },
  { name: 'Redis', status: 'warning', uptime: 98.5, latency: 8, category: 'Cache' },
  { name: 'Qdrant', status: 'healthy', uptime: 99.5, latency: 35, category: 'Vector DB' },
  { name: 'Embedding Service', status: 'healthy', uptime: 99.2, latency: 120, category: 'AI' },
  { name: 'Reranker', status: 'healthy', uptime: 99.0, latency: 80, category: 'AI' },
];

const resources = [
  { name: 'CPU', usage: 45, max: 100, unit: '%' },
  { name: 'Memory', usage: 62, max: 100, unit: '%' },
  { name: 'Disk', usage: 38, max: 100, unit: '%' },
];

export default function SystemHealthPage() {
  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-quicksand font-bold text-foreground">
          System Health
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Monitor infrastructure services and resource usage
        </p>
      </div>

      {/* Service Grid */}
      <div>
        <h2 className="text-lg font-quicksand font-semibold mb-4">Services</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {services.map((service) => (
            <div
              key={service.name}
              className="bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md rounded-xl border border-slate-200/60 dark:border-zinc-700/60 p-6 hover:shadow-lg transition-shadow"
            >
              <div className="flex items-start justify-between mb-4">
                <div>
                  <h3 className="font-quicksand font-bold text-lg">{service.name}</h3>
                  <p className="text-xs text-muted-foreground">{service.category}</p>
                </div>
                <div
                  className={`flex items-center gap-2 px-3 py-1 rounded-full text-xs font-semibold ${
                    service.status === 'healthy'
                      ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400'
                      : 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400'
                  }`}
                >
                  <div
                    className={`w-2 h-2 rounded-full ${
                      service.status === 'healthy' ? 'bg-green-500' : 'bg-amber-500'
                    }`}
                  />
                  {service.status === 'healthy' ? 'Healthy' : 'Warning'}
                </div>
              </div>
              <div className="space-y-2 text-sm">
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Uptime</span>
                  <span className="font-semibold font-mono">{service.uptime}%</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Latency</span>
                  <span className="font-semibold font-mono">{service.latency}ms</span>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Resource Usage */}
      <div>
        <h2 className="text-lg font-quicksand font-semibold mb-4">Resource Usage</h2>
        <div className="bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md rounded-xl border border-slate-200/60 dark:border-zinc-700/60 p-6">
          <div className="space-y-6">
            {resources.map((resource) => (
              <div key={resource.name}>
                <div className="flex justify-between mb-2">
                  <span className="text-sm font-medium">{resource.name}</span>
                  <span className="text-sm font-mono font-semibold">
                    {resource.usage}{resource.unit}
                  </span>
                </div>
                <div className="h-3 bg-slate-200/60 dark:bg-zinc-700/60 rounded-full overflow-hidden">
                  <div
                    className={`h-full rounded-full transition-all ${
                      resource.usage < 50
                        ? 'bg-gradient-to-r from-blue-500 to-blue-600'
                        : resource.usage < 80
                          ? 'bg-gradient-to-r from-amber-500 to-amber-600'
                          : 'bg-gradient-to-r from-red-500 to-red-600'
                    }`}
                    style={{ width: `${resource.usage}%` }}
                  />
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

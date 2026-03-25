'use client';

import { type LucideIcon, Shield, MessageSquare, Upload, Loader2 } from 'lucide-react';

import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { useAdminConfig, parseAllConfigs } from '@/hooks/useAdminConfig';

// Icon lookup for dynamic config
const ICON_MAP: Record<string, LucideIcon> = {
  Shield,
  MessageSquare,
  Upload,
};

interface RateLimitCategory {
  icon: string;
  title: string;
  description: string;
  limits: { label: string; value: string }[];
}

// Fallback constants used when the API is unreachable
const FALLBACK_RATE_LIMIT_CATEGORIES: RateLimitCategory[] = [
  {
    icon: 'Shield',
    title: 'API Rate Limits',
    description: 'Global and per-endpoint request throttling for REST API calls.',
    limits: [
      { label: 'Global', value: '1000 req/min' },
      { label: 'Per User', value: '100 req/min' },
      { label: 'Auth Endpoints', value: '20 req/min' },
    ],
  },
  {
    icon: 'MessageSquare',
    title: 'Chat Rate Limits',
    description: 'Message throughput limits for AI chat sessions per tier.',
    limits: [
      { label: 'Free Tier', value: '10 msg/hour' },
      { label: 'Normal Tier', value: '50 msg/hour' },
      { label: 'Premium Tier', value: '200 msg/hour' },
    ],
  },
  {
    icon: 'Upload',
    title: 'Upload Rate Limits',
    description: 'File upload frequency and bandwidth restrictions.',
    limits: [
      { label: 'Free Tier', value: '5 uploads/day' },
      { label: 'Normal Tier', value: '20 uploads/day' },
      { label: 'Premium Tier', value: '100 uploads/day' },
    ],
  },
];

export function RateLimitsTab() {
  const { data: rateLimitsConfig, isLoading } = useAdminConfig('rate-limits');

  const parsed = parseAllConfigs<RateLimitCategory>(rateLimitsConfig);
  const categories =
    Object.keys(parsed).length > 0 ? Object.values(parsed) : FALLBACK_RATE_LIMIT_CATEGORIES;

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold text-foreground">Rate Limits</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Configure request throttling and usage quotas across API, chat, and upload operations.
        </p>
      </div>
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {categories.map(category => {
          const Icon = ICON_MAP[category.icon] ?? Shield;
          return (
            <Card
              key={category.title}
              className="bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40"
            >
              <CardHeader className="pb-3">
                <CardTitle className="flex items-center gap-2 text-base font-quicksand">
                  <Icon className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
                  {category.title}
                </CardTitle>
                <CardDescription className="text-xs">{category.description}</CardDescription>
              </CardHeader>
              <CardContent>
                <div className="space-y-2">
                  {category.limits.map(limit => (
                    <div
                      key={limit.label}
                      className="flex items-center justify-between rounded-md bg-muted/50 px-3 py-2 text-sm"
                    >
                      <span className="text-muted-foreground">{limit.label}</span>
                      <span className="font-mono font-medium text-foreground">{limit.value}</span>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>
          );
        })}
      </div>
      <p className="text-xs text-muted-foreground text-center">
        Editable rate limit configuration will be available in a future update.
      </p>
    </div>
  );
}

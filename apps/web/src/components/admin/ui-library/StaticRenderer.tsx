'use client';

import { ComponentType, Suspense } from 'react';

import type { MockVariant } from '@/config/component-registry';

interface StaticRendererProps {
  component: ComponentType<Record<string, unknown>> | undefined;
  mockProps?: Record<string, unknown>;
  mockVariants?: MockVariant[];
}

function RendererSkeleton() {
  return (
    <div className="space-y-4">
      {[1, 2].map(i => (
        <div key={i} className="animate-pulse rounded-xl border border-border/50 p-4">
          <div className="mb-3 h-4 w-24 rounded bg-muted" />
          <div className="h-16 rounded bg-muted/50" />
        </div>
      ))}
    </div>
  );
}

function VariantFrame({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="overflow-hidden rounded-xl border border-border/60 bg-background/60 backdrop-blur-sm">
      <div className="border-b border-border/40 bg-muted/30 px-3 py-1.5">
        <span className="font-nunito text-xs font-medium text-muted-foreground">{label}</span>
      </div>
      <div className="p-4">{children}</div>
    </div>
  );
}

function RendererContent({ component: Comp, mockProps, mockVariants }: StaticRendererProps) {
  if (!Comp) {
    return (
      <div className="rounded-xl border border-dashed border-border/50 p-8 text-center">
        <p className="font-nunito text-sm text-muted-foreground">Component not found.</p>
      </div>
    );
  }

  const hasVariants = mockVariants && mockVariants.length > 0;

  if (!mockProps && !hasVariants) {
    return (
      <div className="rounded-xl border border-dashed border-border/50 p-8 text-center">
        <p className="font-nunito text-sm text-muted-foreground">No mock data available.</p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {mockProps && !hasVariants && (
        <VariantFrame label="Default">
          <Comp {...mockProps} />
        </VariantFrame>
      )}

      {hasVariants &&
        mockVariants.map(variant => (
          <VariantFrame key={variant.name} label={variant.name}>
            <Comp {...(mockProps ?? {})} {...variant.props} />
          </VariantFrame>
        ))}
    </div>
  );
}

export function StaticRenderer(props: StaticRendererProps) {
  return (
    <Suspense fallback={<RendererSkeleton />}>
      <RendererContent {...props} />
    </Suspense>
  );
}

'use client';

import dynamic from 'next/dynamic';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import type { ComponentComposition } from '@/config/component-compositions';

// Cache to avoid re-creating dynamic components on re-render
const sceneCache = new Map<string, React.ComponentType>();

function getScene(composition: ComponentComposition): React.ComponentType {
  if (!sceneCache.has(composition.id)) {
    sceneCache.set(
      composition.id,
      dynamic(composition.render, {
        loading: () => <Skeleton className="h-96 w-full" />,
      })
    );
  }
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
  return sceneCache.get(composition.id)!;
}

interface CompositionSceneProps {
  composition: ComponentComposition;
}

export function CompositionScene({ composition }: CompositionSceneProps) {
  const Scene = getScene(composition);
  return (
    <div className="rounded-xl border border-border/60 bg-background p-6">
      <Scene />
    </div>
  );
}

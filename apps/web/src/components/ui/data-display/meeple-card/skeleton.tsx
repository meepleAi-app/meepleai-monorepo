import type { MeepleCardVariant } from './types';

interface MeepleCardSkeletonProps {
  variant?: MeepleCardVariant;
  className?: string;
}

const skeletonHeight: Record<MeepleCardVariant, string> = {
  grid: 'h-[378px]',
  list: 'h-[72px]',
  compact: 'h-[48px]',
  featured: 'h-[340px]',
  hero: 'h-[320px]',
};

export function MeepleCardSkeleton({ variant = 'grid', className = '' }: MeepleCardSkeletonProps) {
  return <div className={`animate-pulse rounded-2xl bg-[var(--mc-bg-muted)] ${skeletonHeight[variant]} ${className}`} />;
}

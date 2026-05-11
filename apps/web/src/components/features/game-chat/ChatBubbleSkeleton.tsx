/**
 * ChatBubbleSkeleton — N bubble skeleton alternati user/agent durante hydrate.
 *
 * Pattern: indice pari = user (right, ~70% width), dispari = agent (left, ~80% width).
 *
 * Spec: docs/superpowers/specs/2026-05-10-fast-resume-audit-g2-design.md §3.2
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

import { Skeleton } from '@/components/ui/feedback/skeleton';

export interface ChatBubbleSkeletonProps {
  readonly count?: number;
  readonly className?: string;
}

export function ChatBubbleSkeleton({
  count = 3,
  className,
}: ChatBubbleSkeletonProps): ReactElement {
  return (
    <div
      data-slot="chat-bubble-skeleton-list"
      className={clsx('flex flex-col gap-3 p-2', className)}
    >
      {Array.from({ length: count }, (_, idx) => {
        const isUser = idx % 2 === 0;
        return (
          <div
            key={idx}
            data-testid="chat-bubble-skeleton"
            className={clsx('flex', isUser ? 'justify-end' : 'justify-start')}
          >
            <Skeleton
              className={clsx(
                'h-12 rounded-2xl',
                isUser ? 'w-[70%] rounded-br-sm' : 'w-[80%] rounded-bl-sm'
              )}
            />
          </div>
        );
      })}
    </div>
  );
}

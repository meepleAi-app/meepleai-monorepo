import type { JSX } from 'react';

import clsx from 'clsx';

export type StepStatus = 'completed' | 'current' | 'pending';

export type StepEntityKey =
  | 'game'
  | 'player'
  | 'session'
  | 'agent'
  | 'kb'
  | 'chat'
  | 'event'
  | 'toolkit'
  | 'tool';

export interface Step {
  readonly label: string;
  readonly status?: StepStatus;
}

export interface StepProgressProps {
  readonly steps: Step[];
  readonly currentIndex: number;
  readonly entity?: StepEntityKey;
  readonly className?: string;
  readonly ariaLabel?: string;
}

function deriveStatus(index: number, currentIndex: number, explicit?: StepStatus): StepStatus {
  if (explicit) return explicit;
  if (index < currentIndex) return 'completed';
  if (index === currentIndex) return 'current';
  return 'pending';
}

export function StepProgress({
  steps,
  currentIndex,
  entity = 'game',
  className,
  ariaLabel = 'Progresso',
}: StepProgressProps): JSX.Element {
  const statuses: StepStatus[] = steps.map((s, i) => deriveStatus(i, currentIndex, s.status));

  return (
    <div
      role="progressbar"
      aria-label={ariaLabel}
      aria-valuemin={1}
      aria-valuemax={steps.length}
      aria-valuenow={currentIndex + 1}
      data-entity={entity}
      className={clsx('w-full', className)}
    >
      <ol className="flex items-start justify-between gap-0">
        {steps.map((step, i) => {
          const status = statuses[i];
          const isLast = i === steps.length - 1;

          // Connector color logic: a connector between step i and i+1 is "filled"
          // when both sides are past pending (completed→completed, or completed→current).
          const nextStatus = statuses[i + 1];
          const connectorFilled =
            status === 'completed' && (nextStatus === 'completed' || nextStatus === 'current');

          const circleClasses = clsx(
            'flex h-8 w-8 items-center justify-center rounded-full text-xs font-semibold transition-transform',
            status === 'completed' && 'bg-primary text-primary-foreground',
            status === 'current' &&
              'bg-primary text-primary-foreground ring-2 ring-primary ring-offset-2 ring-offset-background scale-110',
            status === 'pending' && 'bg-muted text-muted-foreground'
          );

          return (
            <li
              key={`${step.label}-${i}`}
              data-step-status={status}
              aria-current={status === 'current' ? 'step' : undefined}
              className={clsx('flex flex-col items-center', isLast ? 'flex-none' : 'flex-1')}
            >
              <div className="flex w-full items-center">
                <div data-step-circle className={circleClasses} aria-hidden="true">
                  {status === 'completed' ? (
                    <svg
                      xmlns="http://www.w3.org/2000/svg"
                      viewBox="0 0 20 20"
                      fill="currentColor"
                      className="h-4 w-4"
                    >
                      <path
                        fillRule="evenodd"
                        d="M16.704 5.296a1 1 0 0 1 0 1.408l-7.5 7.5a1 1 0 0 1-1.408 0l-3.5-3.5a1 1 0 1 1 1.408-1.408L8.5 12.092l6.796-6.796a1 1 0 0 1 1.408 0z"
                        clipRule="evenodd"
                      />
                    </svg>
                  ) : (
                    i + 1
                  )}
                </div>
                {!isLast && (
                  <div
                    data-step-connector
                    data-step-connector-filled={connectorFilled}
                    className={clsx(
                      'mx-2 h-0.5 flex-1 rounded-full',
                      connectorFilled ? 'bg-primary' : 'bg-muted'
                    )}
                    aria-hidden="true"
                  />
                )}
              </div>
              <span
                className={clsx(
                  'mt-2 hidden text-center text-xs sm:block',
                  status === 'pending' ? 'text-muted-foreground' : 'text-foreground'
                )}
              >
                {step.label}
              </span>
            </li>
          );
        })}
      </ol>
    </div>
  );
}

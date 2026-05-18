import type { JSX } from 'react';

export interface SetupChecklistItem {
  readonly icon?: string;
  readonly text: string;
  readonly done?: boolean;
}

export interface SetupChecklistProps {
  readonly items: ReadonlyArray<SetupChecklistItem>;
  readonly title?: string;
  readonly className?: string;
}

export function SetupChecklist({
  items,
  title = 'Setup checklist',
  className,
}: SetupChecklistProps): JSX.Element {
  const doneCount = items.filter(i => i.done).length;

  return (
    <div className={['flex flex-col gap-1.5', className ?? ''].filter(Boolean).join(' ')}>
      <div className="flex items-center gap-1.5">
        <span aria-hidden="true" className="text-[13px] leading-none">
          📋
        </span>
        <span className="font-mono text-[10px] font-extrabold uppercase tracking-wider text-muted-foreground">
          {title} · {doneCount}/{items.length}
        </span>
      </div>
      <ul className="flex flex-col gap-1 m-0 p-0 list-none">
        {items.map((item, i) => (
          <li
            key={i}
            className={[
              'flex items-center gap-2.5 rounded-sm px-2.5 py-1',
              item.done
                ? 'bg-entity-toolkit/[0.06] border border-entity-toolkit/30'
                : 'bg-muted border border-border opacity-90',
            ].join(' ')}
          >
            <span
              aria-hidden="true"
              className={[
                'inline-flex h-[18px] w-[18px] shrink-0 items-center justify-center rounded-sm',
                'text-[11px] font-extrabold leading-none text-white',
                item.done
                  ? 'bg-entity-toolkit border-0'
                  : 'border-[1.5px] border-border-strong bg-transparent',
              ].join(' ')}
            >
              {item.done ? '✓' : ''}
            </span>
            {item.icon ? (
              <span aria-hidden="true" className="text-[13px] leading-none">
                {item.icon}
              </span>
            ) : null}
            <span
              className={[
                'min-w-0 flex-1 font-body text-[11.5px] font-semibold',
                item.done
                  ? 'text-foreground line-through decoration-muted-foreground decoration-1'
                  : 'text-muted-foreground',
              ].join(' ')}
            >
              {item.text}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

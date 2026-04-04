'use client';

import { memo } from 'react';

interface NotesBlockProps {
  title: string;
  entityColor: string;
  data: {
    type: 'notes';
    content: string;
    updatedAt: string;
  };
}

export const NotesBlock = memo(function NotesBlock({ title, entityColor, data }: NotesBlockProps) {
  const { content, updatedAt } = data;

  return (
    <div className="flex flex-col gap-2">
      {title && (
        <>
          <h4
            className="text-xs font-semibold uppercase tracking-wide"
            style={{ color: `hsl(${entityColor})` }}
          >
            {title}
          </h4>
          <div className="h-px w-full" style={{ backgroundColor: `hsl(${entityColor} / 0.2)` }} />
        </>
      )}

      {!content ? (
        <p className="text-xs text-muted-foreground italic">No data yet</p>
      ) : (
        <div className="flex flex-col gap-1.5">
          <p className="whitespace-pre-wrap text-xs text-foreground">{content}</p>
          <p className="text-[10px] text-muted-foreground">Aggiornato: {updatedAt}</p>
        </div>
      )}
    </div>
  );
});

NotesBlock.displayName = 'NotesBlock';

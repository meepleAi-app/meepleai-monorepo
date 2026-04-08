'use client';

/**
 * ScoreCategoryHeader — Column header for score category with remove action.
 */

import React from 'react';

export interface ScoreCategoryHeaderProps {
  category: string;
  onRemove: () => void;
}

export function ScoreCategoryHeader({ category, onRemove }: ScoreCategoryHeaderProps) {
  const handleRemove = () => {
    if (confirm(`Rimuovere la categoria "${category}"? I punteggi rimarranno nel diario.`)) {
      onRemove();
    }
  };

  return (
    <th className="min-w-[56px] border-b border-gray-200 px-1 pb-1 pt-1 text-center">
      <div className="flex flex-col items-center gap-0.5">
        <span className="max-w-[72px] truncate text-[10px] font-bold uppercase tracking-wider text-gray-600">
          {category}
        </span>
        <button
          type="button"
          onClick={handleRemove}
          className="text-[9px] text-gray-300 hover:text-red-500"
          title="Rimuovi categoria"
          data-testid={`remove-category-${category}`}
        >
          ×
        </button>
      </div>
    </th>
  );
}

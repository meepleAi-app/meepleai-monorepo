'use client';

/**
 * MediaTab - Photo gallery and notes for ExtraMeepleCard
 * Issue #4762 - ExtraMeepleCard: Media Tab + AI Tab + Other Entity Types
 */

import React, { useState } from 'react';

import { Camera, FileText, Filter, ImageOff } from 'lucide-react';

import { cn } from '@/lib/utils';

import type { MediaTabData, MediaItem } from '../types';

interface MediaTabProps {
  data?: MediaTabData;
}

/** Single media card (photo or note) */
function MediaItemCard({ item }: { item: MediaItem }) {
  if (item.type === 'photo') {
    return (
      <div
        className="group relative aspect-square overflow-hidden rounded-lg bg-slate-100 cursor-pointer"
        data-testid={`media-photo-${item.id}`}
      >
        {item.thumbnailUrl || item.url ? (
          <img
            src={item.thumbnailUrl || item.url}
            alt={item.title || 'Session photo'}
            className="h-full w-full object-cover transition-transform duration-200 group-hover:scale-105"
          />
        ) : (
          <div className="flex h-full items-center justify-center text-slate-300">
            <ImageOff className="h-8 w-8" />
          </div>
        )}
        {item.turnNumber != null && (
          <span className="absolute top-1.5 right-1.5 rounded-full bg-black/60 px-1.5 py-0.5 text-[10px] font-bold text-white font-nunito">
            T{item.turnNumber}
          </span>
        )}
        <div className="absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/60 to-transparent p-2 opacity-0 transition-opacity group-hover:opacity-100">
          <p className="text-xs text-white font-nunito line-clamp-1">{item.title || 'Photo'}</p>
        </div>
      </div>
    );
  }

  // Note card
  return (
    <div
      className="rounded-lg border border-slate-200/60 bg-white/50 p-3 space-y-1.5"
      data-testid={`media-note-${item.id}`}
    >
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-1.5">
          <FileText className="h-3.5 w-3.5 text-indigo-500" />
          <span className="font-nunito text-xs font-semibold text-slate-700">
            {item.title || 'Note'}
          </span>
        </div>
        {item.turnNumber != null && (
          <span className="text-[10px] text-slate-400 font-nunito">Turn {item.turnNumber}</span>
        )}
      </div>
      <p className="font-nunito text-xs text-slate-600 line-clamp-3">{item.content || ''}</p>
      <p className="font-nunito text-[10px] text-slate-400">
        {item.createdBy && `${item.createdBy} · `}
        {new Date(item.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
      </p>
    </div>
  );
}

export function MediaTab({ data }: MediaTabProps) {
  const [filter, setFilter] = useState<'all' | 'photos' | 'notes'>('all');

  if (!data) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-slate-400">
        <Camera className="h-10 w-10 mb-2 opacity-50" />
        <p className="font-nunito text-sm">No media yet</p>
        <p className="font-nunito text-xs mt-1">Photos and notes will appear here</p>
      </div>
    );
  }

  const filteredItems = data.items.filter(item => {
    if (filter === 'photos') return item.type === 'photo';
    if (filter === 'notes') return item.type === 'note';
    return true;
  });

  const photos = filteredItems.filter(i => i.type === 'photo');
  const notes = filteredItems.filter(i => i.type === 'note');

  return (
    <div className="space-y-3" data-testid="media-tab">
      {/* Filter bar */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Filter className="h-3.5 w-3.5 text-slate-400" aria-hidden="true" />
          <div className="flex gap-1">
            {(['all', 'photos', 'notes'] as const).map(f => (
              <button
                key={f}
                onClick={() => setFilter(f)}
                className={cn(
                  'rounded-full px-2.5 py-0.5 text-xs font-nunito font-medium transition-colors',
                  filter === f
                    ? 'bg-indigo-100 text-indigo-700'
                    : 'text-slate-500 hover:bg-slate-100'
                )}
                data-testid={`media-filter-${f}`}
              >
                {f === 'all'
                  ? 'All'
                  : f === 'photos'
                    ? `Photos (${data.totalPhotos})`
                    : `Notes (${data.totalNotes})`}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* Photo gallery grid */}
      {photos.length > 0 && (filter === 'all' || filter === 'photos') && (
        <div>
          {filter === 'all' && (
            <h3 className="font-quicksand text-xs font-bold text-slate-500 uppercase tracking-wider mb-2">
              Photos
            </h3>
          )}
          <div className="grid grid-cols-3 gap-2">
            {photos.map(item => (
              <MediaItemCard key={item.id} item={item} />
            ))}
          </div>
        </div>
      )}

      {/* Notes list */}
      {notes.length > 0 && (filter === 'all' || filter === 'notes') && (
        <div>
          {filter === 'all' && (
            <h3 className="font-quicksand text-xs font-bold text-slate-500 uppercase tracking-wider mb-2">
              Notes
            </h3>
          )}
          <div className="space-y-2">
            {notes.map(item => (
              <MediaItemCard key={item.id} item={item} />
            ))}
          </div>
        </div>
      )}

      {/* Empty filtered state */}
      {filteredItems.length === 0 && (
        <div className="flex flex-col items-center py-8 text-slate-400">
          <p className="font-nunito text-sm">
            No {filter === 'photos' ? 'photos' : filter === 'notes' ? 'notes' : 'media'} found
          </p>
        </div>
      )}
    </div>
  );
}

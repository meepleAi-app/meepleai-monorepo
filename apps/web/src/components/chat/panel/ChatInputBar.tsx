'use client';

import { useState, useRef, type KeyboardEvent } from 'react';

import { X } from 'lucide-react';

import { useChatImageAttachments } from '@/hooks/useChatImageAttachments';

interface ChatInputBarProps {
  placeholder?: string;
  onSend: (value: string) => void;
  onSendWithImages?: (text: string, images: File[]) => void;
}

export function ChatInputBar({
  placeholder = 'Chiedi una regola…',
  onSend,
  onSendWithImages,
}: ChatInputBarProps) {
  const [value, setValue] = useState('');
  const fileInputRef = useRef<HTMLInputElement>(null);
  const { images, addImage, removeImage, clearImages, hasImages, canAddMore } =
    useChatImageAttachments();

  const send = () => {
    const trimmed = value.trim();
    if (trimmed.length === 0 && !hasImages) return;

    if (hasImages && onSendWithImages) {
      onSendWithImages(
        trimmed,
        images.map(img => img.file)
      );
    } else {
      onSend(trimmed);
    }
    setValue('');
    clearImages();
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      send();
    }
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (!files) return;
    for (let i = 0; i < files.length; i++) {
      const error = addImage(files[i]);
      if (error) {
        // Could show a toast, for now silently skip
        console.warn('[ChatInputBar]', error);
      }
    }
    // Reset so same files can be re-selected
    e.target.value = '';
  };

  return (
    <div className="flex-shrink-0 border-t border-[var(--nh-border-default)] bg-[rgba(255,252,248,0.8)] px-5 py-4 backdrop-blur-md">
      {/* Image previews */}
      {hasImages && (
        <div className="mb-3 flex flex-wrap gap-2">
          {images.map((img, idx) => (
            <div key={img.previewUrl} className="group relative">
              <img
                src={img.previewUrl}
                alt={`Allegato ${idx + 1}`}
                className="h-16 w-20 rounded-lg border border-[var(--nh-border-default)] object-cover shadow-sm"
              />
              <button
                type="button"
                onClick={() => removeImage(idx)}
                aria-label={`Rimuovi immagine ${idx + 1}`}
                className="absolute -right-1.5 -top-1.5 flex h-5 w-5 items-center justify-center rounded-full bg-red-500 text-white opacity-0 shadow-sm transition-opacity group-hover:opacity-100"
              >
                <X className="h-3 w-3" />
              </button>
            </div>
          ))}
        </div>
      )}

      <div className="flex items-end gap-2.5 rounded-2xl border border-[var(--nh-border-default)] bg-[var(--nh-bg-elevated)] py-2 pl-4 pr-2 shadow-[var(--shadow-warm-sm)] transition-all focus-within:border-[hsla(220,80%,55%,0.3)] focus-within:bg-white focus-within:shadow-[0_0_0_3px_hsla(220,80%,55%,0.1),var(--shadow-warm-md)]">
        <textarea
          value={value}
          onChange={e => setValue(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={placeholder}
          rows={1}
          className="min-h-6 flex-1 resize-none border-none bg-transparent py-2 font-nunito text-[0.9rem] leading-relaxed text-[var(--nh-text-primary)] outline-none placeholder:text-[var(--nh-text-muted)]"
        />
        <div className="flex items-center gap-1">
          <button
            type="button"
            aria-label="Allega PDF"
            className="flex h-9 w-9 items-center justify-center rounded-[10px] text-[var(--nh-text-muted)] transition-all hover:bg-[var(--nh-bg-base)] hover:text-[var(--nh-text-primary)]"
          >
            📎
          </button>
          <button
            type="button"
            aria-label="Allega immagine"
            onClick={() => canAddMore && fileInputRef.current?.click()}
            className="flex h-9 w-9 items-center justify-center rounded-[10px] text-[var(--nh-text-muted)] transition-all hover:bg-[var(--nh-bg-base)] hover:text-[var(--nh-text-primary)] disabled:opacity-40"
            disabled={!canAddMore}
          >
            🖼️
          </button>
          <button
            type="button"
            aria-label="Dettatura"
            className="flex h-9 w-9 items-center justify-center rounded-[10px] text-[var(--nh-text-muted)] transition-all hover:bg-[var(--nh-bg-base)] hover:text-[var(--nh-text-primary)]"
          >
            🎤
          </button>
          <button
            type="button"
            aria-label="Invia messaggio"
            onClick={send}
            className="flex h-9 w-9 items-center justify-center rounded-[10px] text-white transition-all hover:-translate-y-px"
            style={{
              background: 'linear-gradient(135deg, hsl(220 80% 58%), hsl(220 80% 42%))',
              boxShadow: '0 2px 8px hsla(220, 80%, 55%, 0.35)',
            }}
          >
            ➤
          </button>
        </div>
      </div>

      {/* Hidden file input for image selection */}
      <input
        ref={fileInputRef}
        type="file"
        accept="image/jpeg,image/png,image/webp"
        multiple
        className="hidden"
        onChange={handleFileChange}
        data-testid="chat-image-input"
      />

      <div className="mt-2 flex items-center justify-between px-1.5 text-[0.68rem] text-[var(--nh-text-muted)]">
        <div className="flex gap-3">
          <span>
            <kbd className="rounded border border-[var(--nh-border-default)] bg-[var(--nh-bg-base)] px-1.5 py-0.5 font-mono text-[9px] font-bold">
              Enter
            </kbd>{' '}
            invia
          </span>
          <span>
            <kbd className="rounded border border-[var(--nh-border-default)] bg-[var(--nh-bg-base)] px-1.5 py-0.5 font-mono text-[9px] font-bold">
              ⇧ Enter
            </kbd>{' '}
            nuova riga
          </span>
        </div>
        <span>✦ Risposte basate sulla KB caricata</span>
      </div>
    </div>
  );
}

'use client';

import { BellIcon, MailIcon } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface ChannelSelectorProps {
  selected: string[];
  onChange: (channels: string[]) => void;
}

const CHANNELS = [
  { id: 'inapp', label: 'In-App', icon: BellIcon, description: 'Notification center' },
  { id: 'email', label: 'Email', icon: MailIcon, description: 'Email queue' },
] as const;

export function ChannelSelector({ selected, onChange }: ChannelSelectorProps) {
  function toggle(channelId: string) {
    if (selected.includes(channelId)) {
      onChange(selected.filter(c => c !== channelId));
    } else {
      onChange([...selected, channelId]);
    }
  }

  return (
    <div className="flex gap-3">
      {CHANNELS.map(channel => {
        const Icon = channel.icon;
        const isSelected = selected.includes(channel.id);

        return (
          <button
            key={channel.id}
            type="button"
            role="switch"
            aria-checked={isSelected}
            aria-label={`${channel.label} channel`}
            onClick={() => toggle(channel.id)}
            className={cn(
              'flex items-center gap-2 px-4 py-3 rounded-lg border-2 transition-all',
              'text-sm font-medium',
              isSelected
                ? 'border-primary bg-primary/5 text-primary'
                : 'border-border bg-background text-muted-foreground hover:border-muted-foreground/50'
            )}
          >
            <Icon className="h-4 w-4" />
            <div className="text-left">
              <div>{channel.label}</div>
              <div className="text-xs font-normal opacity-70">{channel.description}</div>
            </div>
          </button>
        );
      })}
    </div>
  );
}

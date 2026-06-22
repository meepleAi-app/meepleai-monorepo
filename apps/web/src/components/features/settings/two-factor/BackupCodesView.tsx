'use client';

import { useState } from 'react';

import { Copy, Download, Check } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

interface Props {
  readonly codes: readonly string[];
  readonly acked: boolean;
  readonly onAck: (next: boolean) => void;
}

export function BackupCodesView({ codes, acked, onAck }: Props): React.JSX.Element {
  const [copiedAt, setCopiedAt] = useState<number | null>(null);

  async function handleCopy(): Promise<void> {
    await navigator.clipboard.writeText(codes.join('\n'));
    setCopiedAt(Date.now());
    setTimeout(() => setCopiedAt(null), 1800);
  }

  function handleDownload(): void {
    const body = `MeepleAI · Recovery codes\n${new Date().toISOString()}\n\n${codes.join('\n')}\n`;
    const blob = new Blob([body], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'meepleai-recovery-codes.txt';
    a.click();
    URL.revokeObjectURL(url);
  }

  return (
    <div>
      <div className="grid grid-cols-2 gap-x-3 gap-y-2 p-3 rounded-md bg-muted border border-border">
        {codes.map((c, i) => (
          <div
            key={i}
            className="font-mono text-sm font-semibold py-1.5 rounded-sm text-foreground tracking-wide text-center bg-card border border-border"
          >
            {c}
          </div>
        ))}
      </div>

      <div className="flex gap-2 mt-3 flex-wrap">
        <Button size="sm" variant="outline" onClick={handleCopy}>
          {copiedAt ? <Check className="h-3 w-3 mr-1" /> : <Copy className="h-3 w-3 mr-1" />}
          {copiedAt ? 'Copied!' : 'Copy all'}
        </Button>
        <Button size="sm" variant="outline" onClick={handleDownload}>
          <Download className="h-3 w-3 mr-1" />
          Download .txt
        </Button>
      </div>

      <label className="flex items-center gap-2.5 mt-4 p-3 rounded-md bg-muted cursor-pointer select-none">
        <input
          type="checkbox"
          checked={acked}
          onChange={e => onAck(e.target.checked)}
          className="w-[18px] h-[18px] cursor-pointer shrink-0"
          style={{ accentColor: 'hsl(var(--c-success, 142 71% 45%))' }}
        />
        <span className="text-sm text-foreground">
          Ho salvato i recovery codes in un posto sicuro
        </span>
      </label>
    </div>
  );
}

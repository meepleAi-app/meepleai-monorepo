'use client';

import { useState } from 'react';

import { Download, Search, Terminal } from 'lucide-react';

import {
  Button,
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  Input,
  ScrollArea,
} from '@/components/ui';
import type { LogEntry } from '@/lib/agent/playground-sse-parser';
import { cn } from '@/lib/utils';
import type { Message } from '@/stores/playground-store';

interface DebugPanelConsoleProps {
  logEntries: LogEntry[];
  messages: Message[];
}

export function DebugPanelConsole({ logEntries, messages }: DebugPanelConsoleProps) {
  const [logLevelFilter, setLogLevelFilter] = useState<Set<string>>(
    new Set(['info', 'warn', 'error', 'debug'])
  );
  const [logSourceFilter, setLogSourceFilter] = useState<string>('all');
  const [logSearch, setLogSearch] = useState('');

  return (
    <>
      {/* Developer Console (Issue #4445) */}
      {logEntries.length > 0 &&
        (() => {
          const sources = Array.from(new Set(logEntries.map(e => e.source)));
          const filtered = logEntries.filter(e => {
            if (!logLevelFilter.has(e.level)) return false;
            if (logSourceFilter !== 'all' && e.source !== logSourceFilter) return false;
            if (logSearch && !e.message.toLowerCase().includes(logSearch.toLowerCase()))
              return false;
            return true;
          });
          const levelCounts = { info: 0, warn: 0, error: 0, debug: 0 };
          for (const e of logEntries) {
            if (e.level in levelCounts) levelCounts[e.level as keyof typeof levelCounts]++;
          }

          const handleExportLogs = () => {
            const blob = new Blob([JSON.stringify(logEntries, null, 2)], {
              type: 'application/json',
            });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `playground-logs-${Date.now()}.json`;
            a.click();
            URL.revokeObjectURL(url);
          };

          return (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm flex items-center gap-2">
                  <Terminal className="h-4 w-4" />
                  Developer Console
                  <span className="text-[10px] text-muted-foreground font-normal ml-auto">
                    {filtered.length}/{logEntries.length}
                  </span>
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-2">
                {/* Filters row */}
                <div className="flex items-center gap-1.5 flex-wrap">
                  {(['info', 'warn', 'error', 'debug'] as const).map(level => (
                    <button
                      key={level}
                      type="button"
                      onClick={() =>
                        setLogLevelFilter(prev => {
                          const next = new Set(prev);
                          if (next.has(level)) next.delete(level);
                          else next.add(level);
                          return next;
                        })
                      }
                      className={cn(
                        'text-[10px] px-1.5 py-0.5 rounded border transition-colors',
                        logLevelFilter.has(level)
                          ? {
                              info: 'bg-blue-100 text-blue-800 border-blue-200',
                              warn: 'bg-amber-100 text-amber-800 border-amber-200',
                              error: 'bg-red-100 text-red-800 border-red-200',
                              debug: 'bg-gray-100 text-gray-800 border-gray-200',
                            }[level]
                          : 'bg-transparent text-muted-foreground border-muted opacity-50'
                      )}
                    >
                      {level} ({levelCounts[level]})
                    </button>
                  ))}
                  <select
                    value={logSourceFilter}
                    onChange={e => setLogSourceFilter(e.target.value)}
                    className="text-[10px] h-5 px-1 border rounded bg-background"
                  >
                    <option value="all">All sources</option>
                    {sources.map(s => (
                      <option key={s} value={s}>
                        {s}
                      </option>
                    ))}
                  </select>
                  <Button
                    variant="ghost"
                    size="icon"
                    className="h-5 w-5 ml-auto"
                    onClick={handleExportLogs}
                    title="Export logs"
                  >
                    <Download className="h-3 w-3" />
                  </Button>
                </div>
                {/* Search */}
                <div className="relative">
                  <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-3 w-3 text-muted-foreground" />
                  <Input
                    placeholder="Search logs..."
                    value={logSearch}
                    onChange={e => setLogSearch(e.target.value)}
                    className="h-6 text-[11px] pl-6 py-0"
                  />
                </div>
                {/* Log entries */}
                <ScrollArea className="max-h-52">
                  <div className="space-y-0.5 font-mono text-[11px]">
                    {filtered.length === 0 && (
                      <p className="text-xs text-muted-foreground italic py-2">
                        No matching log entries
                      </p>
                    )}
                    {filtered.map((entry, idx) => (
                      <div
                        key={idx}
                        className="flex items-start gap-1.5 px-1 py-0.5 hover:bg-muted/40 rounded"
                      >
                        <span
                          className={cn(
                            'shrink-0 text-[9px] font-semibold uppercase w-10 text-right',
                            entry.level === 'info' && 'text-blue-600',
                            entry.level === 'warn' && 'text-amber-600',
                            entry.level === 'error' && 'text-red-600',
                            entry.level === 'debug' && 'text-gray-500'
                          )}
                        >
                          {entry.level}
                        </span>
                        <span
                          className={cn(
                            'shrink-0 text-[9px] px-1 rounded',
                            'bg-muted text-muted-foreground'
                          )}
                        >
                          {entry.source}
                        </span>
                        <span className="flex-1 break-all">{entry.message}</span>
                      </div>
                    ))}
                  </div>
                </ScrollArea>
              </CardContent>
            </Card>
          );
        })()}

      {/* Recent Messages */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm">Recent Messages</CardTitle>
        </CardHeader>
        <CardContent>
          <ScrollArea className="max-h-48">
            <div className="space-y-2">
              {messages.length === 0 && (
                <p className="text-sm text-muted-foreground">No messages yet</p>
              )}
              {messages
                .slice(-5)
                .reverse()
                .map(msg => (
                  <div key={msg.id} className="text-xs space-y-1 pb-2 border-b last:border-0">
                    <div className="font-medium">
                      {msg.role}: {msg.content.slice(0, 60)}
                      {msg.content.length > 60 ? '...' : ''}
                    </div>
                    {msg.metadata && (
                      <div className="text-muted-foreground">
                        {msg.metadata.tokens ? `${msg.metadata.tokens} tok` : ''}
                        {msg.metadata.latency ? ` · ${msg.metadata.latency}ms` : ''}
                      </div>
                    )}
                    {msg.feedback && (
                      <div className="text-muted-foreground">
                        Feedback: {msg.feedback === 'up' ? '👍' : '👎'}
                      </div>
                    )}
                  </div>
                ))}
            </div>
          </ScrollArea>
        </CardContent>
      </Card>
    </>
  );
}

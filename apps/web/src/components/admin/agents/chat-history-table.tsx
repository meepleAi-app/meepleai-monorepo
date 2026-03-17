'use client';

import { Fragment, useEffect, useState, type KeyboardEvent } from 'react';

import { ChevronDownIcon, ChevronRightIcon, Loader2 } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { adminDashboardClient } from '@/lib/api/clients/adminDashboardClient';
import { logger } from '@/lib/logger';

interface ChatSession {
  id: string;
  userId: string;
  userName: string;
  agent: string;
  messageCount: number;
  durationSeconds: number;
  date: string;
  preview: Array<{ role: 'user' | 'assistant'; content: string }>;
}

interface ChatHistoryResponse {
  sessions: ChatSession[];
  total: number;
  page: number;
  pageSize: number;
}

function formatDuration(seconds: number): string {
  if (seconds < 60) return `${seconds}s`;
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return s > 0 ? `${m}m ${s}s` : `${m}m`;
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleString('it-IT', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return iso;
  }
}

export function ChatHistoryTable() {
  const [sessions, setSessions] = useState<ChatSession[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [expandedRow, setExpandedRow] = useState<string | null>(null);

  useEffect(() => {
    setIsLoading(true);
    adminDashboardClient
      .getChatHistory()
      .then(res => {
        const data = res as ChatHistoryResponse;
        setSessions(data?.sessions ?? []);
      })
      .catch(err => {
        logger.error('Failed to load chat history:', err);
        setError('Impossibile caricare la cronologia chat');
      })
      .finally(() => setIsLoading(false));
  }, []);

  const toggleRow = (id: string) => {
    setExpandedRow(prev => (prev === id ? null : id));
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLTableRowElement>, id: string) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      toggleRow(id);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-16 bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-amber-200/50 dark:border-zinc-700/50">
        <Loader2 className="w-6 h-6 animate-spin text-amber-500 mr-2" />
        <span className="text-muted-foreground text-sm">Caricamento sessioni...</span>
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center justify-center py-16 bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-red-200/50 dark:border-red-700/50">
        <span className="text-red-600 dark:text-red-400 text-sm">{error}</span>
      </div>
    );
  }

  if (sessions.length === 0) {
    return (
      <div className="flex items-center justify-center py-16 bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-amber-200/50 dark:border-zinc-700/50">
        <span className="text-muted-foreground text-sm">Nessuna sessione chat trovata</span>
      </div>
    );
  }

  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-amber-200/50 dark:border-zinc-700/50 overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full">
          <thead className="bg-amber-100/50 dark:bg-zinc-900/50 border-b border-amber-200/50 dark:border-zinc-700/50">
            <tr>
              <th className="w-10"></th>
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Session ID
              </th>
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                User
              </th>
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Agent
              </th>
              <th className="text-right py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Messages
              </th>
              <th className="text-right py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Duration
              </th>
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Date
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 dark:divide-zinc-700">
            {sessions.map(session => {
              const isExpanded = expandedRow === session.id;
              return (
                <Fragment key={session.id}>
                  <tr
                    className="hover:bg-slate-50/50 dark:hover:bg-zinc-900/50 cursor-pointer"
                    onClick={() => toggleRow(session.id)}
                    onKeyDown={e => handleKeyDown(e, session.id)}
                    role="button"
                    tabIndex={0}
                  >
                    <td className="py-3 px-4">
                      {isExpanded ? (
                        <ChevronDownIcon className="w-4 h-4 text-gray-600 dark:text-zinc-400" />
                      ) : (
                        <ChevronRightIcon className="w-4 h-4 text-gray-600 dark:text-zinc-400" />
                      )}
                    </td>
                    <td className="py-3 px-4 font-mono text-xs text-slate-600 dark:text-zinc-400">
                      {session.id.slice(0, 8)}...
                    </td>
                    <td className="py-3 px-4 text-sm font-medium text-slate-900 dark:text-zinc-100">
                      {session.userName}
                    </td>
                    <td className="py-3 px-4">
                      <Badge
                        variant="outline"
                        className="bg-blue-100 text-blue-900 dark:bg-blue-900/30 dark:text-blue-300"
                      >
                        {session.agent}
                      </Badge>
                    </td>
                    <td className="py-3 px-4 text-right text-sm text-slate-600 dark:text-zinc-400">
                      {session.messageCount}
                    </td>
                    <td className="py-3 px-4 text-right font-mono text-sm text-slate-600 dark:text-zinc-400">
                      {formatDuration(session.durationSeconds)}
                    </td>
                    <td className="py-3 px-4 font-mono text-xs text-slate-600 dark:text-zinc-400">
                      {formatDate(session.date)}
                    </td>
                  </tr>
                  {isExpanded && (
                    <tr>
                      <td colSpan={7} className="bg-slate-50/50 dark:bg-zinc-900/30">
                        <div className="p-4 space-y-3">
                          <h4 className="text-sm font-semibold text-slate-700 dark:text-zinc-300">
                            Chat Preview
                          </h4>
                          {session.preview.length === 0 ? (
                            <p className="text-xs text-muted-foreground">
                              Nessun messaggio da visualizzare
                            </p>
                          ) : (
                            session.preview.map((msg, idx) => (
                              <div
                                key={idx}
                                className={`flex ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}
                              >
                                <div
                                  className={`max-w-[70%] px-4 py-2 rounded-lg text-sm ${
                                    msg.role === 'user'
                                      ? 'bg-amber-100 dark:bg-amber-900/30 text-amber-900 dark:text-amber-300'
                                      : 'bg-white dark:bg-zinc-800 text-slate-900 dark:text-zinc-100'
                                  }`}
                                >
                                  {msg.content}
                                </div>
                              </div>
                            ))
                          )}
                        </div>
                      </td>
                    </tr>
                  )}
                </Fragment>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

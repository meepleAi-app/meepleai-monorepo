'use client';

import { useState, Fragment, type KeyboardEvent } from 'react';

import { ChevronDownIcon, ChevronRightIcon, StarIcon } from 'lucide-react';

import { Badge } from '@/components/ui/badge';

interface ChatSession {
  id: string;
  userId: string;
  userName: string;
  agent: string;
  messageCount: number;
  duration: number;
  satisfaction: number;
  date: string;
  preview: Array<{ role: 'user' | 'assistant'; content: string }>;
}

const MOCK_SESSIONS: ChatSession[] = [
  {
    id: 'chat-001',
    userId: 'user-123',
    userName: 'Sarah Chen',
    agent: 'Rules Expert',
    messageCount: 12,
    duration: 245,
    satisfaction: 5,
    date: '2026-02-18 14:30',
    preview: [
      { role: 'user', content: 'What happens when you roll doubles in Catan?' },
      { role: 'assistant', content: 'In Catan, rolling doubles has no special effect...' },
    ],
  },
  {
    id: 'chat-002',
    userId: 'user-456',
    userName: 'Mike Johnson',
    agent: 'Strategy Advisor',
    messageCount: 8,
    duration: 180,
    satisfaction: 4,
    date: '2026-02-18 13:15',
    preview: [
      { role: 'user', content: 'Best opening strategy for Wingspan?' },
      { role: 'assistant', content: 'For Wingspan, focus on bird powers in early rounds...' },
    ],
  },
  {
    id: 'chat-003',
    userId: 'user-789',
    userName: 'Emily Rodriguez',
    agent: 'Game Recommender',
    messageCount: 15,
    duration: 320,
    satisfaction: 5,
    date: '2026-02-18 11:45',
    preview: [
      { role: 'user', content: 'Games similar to Pandemic for 3 players?' },
      { role: 'assistant', content: 'For 3-player cooperative games similar to Pandemic...' },
    ],
  },
  {
    id: 'chat-004',
    userId: 'user-234',
    userName: 'Alex Kim',
    agent: 'Rules Expert',
    messageCount: 6,
    duration: 95,
    satisfaction: 3,
    date: '2026-02-18 10:20',
    preview: [
      { role: 'user', content: 'Combat rules in Scythe?' },
      { role: 'assistant', content: 'Scythe combat works as follows...' },
    ],
  },
];

function StarRating({ rating }: { rating: number }) {
  return (
    <div className="flex gap-1">
      {Array.from({ length: 5 }).map((_, i) => (
        <StarIcon
          key={i}
          className={`w-4 h-4 ${
            i < rating
              ? 'fill-amber-500 text-amber-500'
              : 'text-gray-300 dark:text-zinc-600'
          }`}
        />
      ))}
    </div>
  );
}

export function ChatHistoryTable() {
  const [expandedRow, setExpandedRow] = useState<string | null>(null);

  const toggleRow = (id: string) => {
    setExpandedRow((prev) => (prev === id ? null : id));
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLTableRowElement>, id: string) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      toggleRow(id);
    }
  };

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
              <th className="text-center py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Satisfaction
              </th>
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Date
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 dark:divide-zinc-700">
            {MOCK_SESSIONS.map((session) => {
              const isExpanded = expandedRow === session.id;
              return (
                <Fragment key={session.id}>
                  <tr
                    className="hover:bg-slate-50/50 dark:hover:bg-zinc-900/50 cursor-pointer"
                    onClick={() => toggleRow(session.id)}
                    onKeyDown={(e) => handleKeyDown(e, session.id)}
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
                      {session.id}
                    </td>
                    <td className="py-3 px-4 text-sm font-medium text-slate-900 dark:text-zinc-100">
                      {session.userName}
                    </td>
                    <td className="py-3 px-4">
                      <Badge variant="outline" className="bg-blue-100 text-blue-900 dark:bg-blue-900/30 dark:text-blue-300">
                        {session.agent}
                      </Badge>
                    </td>
                    <td className="py-3 px-4 text-right text-sm text-slate-600 dark:text-zinc-400">
                      {session.messageCount}
                    </td>
                    <td className="py-3 px-4 text-right font-mono text-sm text-slate-600 dark:text-zinc-400">
                      {Math.floor(session.duration / 60)}m {session.duration % 60}s
                    </td>
                    <td className="py-3 px-4 flex justify-center">
                      <StarRating rating={session.satisfaction} />
                    </td>
                    <td className="py-3 px-4 font-mono text-xs text-slate-600 dark:text-zinc-400">
                      {session.date}
                    </td>
                  </tr>
                  {isExpanded && (
                    <tr>
                      <td colSpan={8} className="bg-slate-50/50 dark:bg-zinc-900/30">
                        <div className="p-4 space-y-3">
                          <h4 className="text-sm font-semibold text-slate-700 dark:text-zinc-300">
                            Chat Preview
                          </h4>
                          {session.preview.map((msg, idx) => (
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
                          ))}
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

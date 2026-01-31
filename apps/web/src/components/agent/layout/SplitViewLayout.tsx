/**
 * Split View Layout - Chat + PDF Side-by-Side
 * Issue #3254 (FRONT-016)
 *
 * Desktop: 50/50 split (resizable)
 * Mobile: Tabs switcher
 */

'use client';

import { useState } from 'react';

interface SplitViewLayoutProps {
  chatPanel: React.ReactNode;
  pdfPanel: React.ReactNode;
}

export function SplitViewLayout({ chatPanel, pdfPanel }: SplitViewLayoutProps) {
  const [activeTab, setActiveTab] = useState<'chat' | 'pdf'>('chat');

  return (
    <>
      {/* Desktop: Split View */}
      <div className="hidden lg:grid lg:grid-cols-2 lg:gap-4 h-full">
        <div className="border-r border-slate-800">{chatPanel}</div>
        <div>{pdfPanel}</div>
      </div>

      {/* Mobile: Tabs */}
      <div className="lg:hidden flex flex-col h-full">
        <div className="flex border-b border-slate-800">
          <button
            onClick={() => setActiveTab('chat')}
            className={`flex-1 py-3 text-sm font-medium transition-colors ${
              activeTab === 'chat'
                ? 'text-cyan-400 border-b-2 border-cyan-400'
                : 'text-slate-400'
            }`}
          >
            Chat
          </button>
          <button
            onClick={() => setActiveTab('pdf')}
            className={`flex-1 py-3 text-sm font-medium transition-colors ${
              activeTab === 'pdf'
                ? 'text-cyan-400 border-b-2 border-cyan-400'
                : 'text-slate-400'
            }`}
          >
            PDF
          </button>
        </div>
        <div className="flex-1 overflow-hidden">
          {activeTab === 'chat' ? chatPanel : pdfPanel}
        </div>
      </div>
    </>
  );
}

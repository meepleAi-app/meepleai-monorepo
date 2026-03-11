'use client';

import { BookOpen, HelpCircle, Bot, X, PanelRightClose } from 'lucide-react';

import { cn } from '@/lib/utils';
import { useQuickViewStore, type QuickViewTab as TabType } from '@/store/quick-view';

import { AIQuickViewContent } from './AIQuickViewContent';
import { QuickViewTab } from './QuickViewTab';

const TABS: { tab: TabType; label: string; icon: typeof BookOpen }[] = [
  { tab: 'rules', label: 'Regole', icon: BookOpen },
  { tab: 'faq', label: 'FAQ', icon: HelpCircle },
  { tab: 'ai', label: 'AI', icon: Bot },
];

export function QuickView() {
  const { isOpen, isCollapsed, activeTab, setActiveTab, close, toggleCollapsed } =
    useQuickViewStore();

  if (!isOpen) return null;

  if (isCollapsed) {
    return (
      <aside
        data-testid="quick-view"
        className="hidden xl:flex flex-col items-center w-[44px] border-l border-border bg-card"
      >
        {TABS.map(({ tab, icon: Icon }) => (
          <button
            key={tab}
            onClick={() => {
              setActiveTab(tab);
              toggleCollapsed();
            }}
            className="p-2.5 text-muted-foreground hover:text-foreground transition-colors"
            aria-label={tab}
          >
            <Icon className="h-4 w-4" />
          </button>
        ))}
      </aside>
    );
  }

  return (
    <aside
      data-testid="quick-view"
      className={cn(
        'hidden xl:flex flex-col',
        'w-[300px] border-l border-border bg-card',
        'h-[calc(100vh-var(--top-bar-height,48px))]'
      )}
    >
      {/* Header */}
      <div className="flex items-center justify-between px-3 py-2 border-b border-border">
        <div role="tablist" className="flex gap-1 flex-1">
          {TABS.map(({ tab, label }) => (
            <QuickViewTab
              key={tab}
              tab={tab}
              label={label}
              isActive={activeTab === tab}
              onClick={setActiveTab}
            />
          ))}
        </div>
        <div className="flex items-center gap-1 ml-2">
          <button
            onClick={toggleCollapsed}
            aria-label="Comprimi pannello"
            className="p-1.5 rounded-md text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
          >
            <PanelRightClose className="h-4 w-4" />
          </button>
          <button
            onClick={close}
            aria-label="Chiudi pannello"
            className="p-1.5 rounded-md text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
          >
            <X className="h-4 w-4" />
          </button>
        </div>
      </div>

      {/* Tab content */}
      <div className="flex-1 overflow-y-auto p-4">
        {activeTab === 'rules' && (
          <p className="text-sm text-muted-foreground">Seleziona un gioco per vederne le regole</p>
        )}
        {activeTab === 'faq' && (
          <p className="text-sm text-muted-foreground">Domande frequenti sul gioco</p>
        )}
        {activeTab === 'ai' && <AIQuickViewContent gameId="" gameName="Gioco" />}
      </div>
    </aside>
  );
}

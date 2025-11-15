/**
 * CommandPalette Component
 * Global search with Cmd+K integration (Issue #1101)
 */

import React, { useState, useEffect, useCallback } from 'react';
import { Command } from 'cmdk';
import {
  Search,
  MessageSquare,
  FileText,
  Gamepad2,
  Bot,
  Clock,
  X,
  Filter
} from 'lucide-react';
import { useSearch } from '@/hooks/useSearch';
import { SearchFilters as SearchFiltersComponent } from './SearchFilters';
import type { SearchResult, SearchFilters, Game, Agent } from '@/types';
import './CommandPalette.css';

interface CommandPaletteProps {
  isOpen: boolean;
  onClose: () => void;
  onSelectResult?: (result: SearchResult) => void;
  dataSources: {
    messages?: any[];
    chats?: any[];
    games?: Game[];
    agents?: Agent[];
  };
}

/**
 * Get icon for search result type
 */
function getResultIcon(type: SearchResult['type']) {
  switch (type) {
    case 'message':
      return <MessageSquare className="w-4 h-4" />;
    case 'chat':
      return <MessageSquare className="w-4 h-4" />;
    case 'game':
      return <Gamepad2 className="w-4 h-4" />;
    case 'agent':
      return <Bot className="w-4 h-4" />;
    case 'pdf':
      return <FileText className="w-4 h-4" />;
  }
}

/**
 * Format timestamp for display
 */
function formatTimestamp(timestamp?: Date): string {
  if (!timestamp) return '';
  const now = new Date();
  const diff = now.getTime() - timestamp.getTime();
  const days = Math.floor(diff / (1000 * 60 * 60 * 24));

  if (days === 0) return 'Today';
  if (days === 1) return 'Yesterday';
  if (days < 7) return `${days} days ago`;
  if (days < 30) return `${Math.floor(days / 7)} weeks ago`;
  if (days < 365) return `${Math.floor(days / 30)} months ago`;
  return timestamp.toLocaleDateString();
}

export const CommandPalette: React.FC<CommandPaletteProps> = ({
  isOpen,
  onClose,
  onSelectResult,
  dataSources,
}) => {
  const [query, setQuery] = useState('');
  const [showFilters, setShowFilters] = useState(false);
  const [selectedFilters, setSelectedFilters] = useState<SearchFilters>({});
  const [results, setResults] = useState<SearchResult[]>([]);

  const {
    search,
    recentSearches,
    addToRecentSearches,
    removeRecentSearch,
  } = useSearch(dataSources);

  // Handle search
  const performSearch = useCallback((searchQuery: string, filters: SearchFilters) => {
    const searchResults = search({
      query: searchQuery,
      filters,
      limit: 50
    });
    setResults(searchResults);

    // Add to recent searches if query is not empty
    if (searchQuery.trim()) {
      addToRecentSearches(searchQuery, filters, searchResults.length);
    }
  }, [search, addToRecentSearches]);

  // Update search when query or filters change
  useEffect(() => {
    performSearch(query, selectedFilters);
  }, [query, selectedFilters, performSearch]);

  // Handle keyboard shortcut (Cmd+K / Ctrl+K)
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault();
        if (isOpen) {
          onClose();
        }
      }
      if (e.key === 'Escape' && isOpen) {
        onClose();
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  // Handle result selection
  const handleSelectResult = useCallback((result: SearchResult) => {
    onSelectResult?.(result);
    onClose();
  }, [onSelectResult, onClose]);

  // Handle recent search selection
  const handleSelectRecentSearch = useCallback((recentSearch: typeof recentSearches[number]) => {
    setQuery(recentSearch.query);
    setSelectedFilters(recentSearch.filters || {});
  }, []);

  if (!isOpen) return null;

  return (
    <div className="command-palette-overlay" onClick={onClose}>
      <div className="command-palette-container" onClick={(e) => e.stopPropagation()}>
        <Command className="command-palette">
          <div className="command-palette-header">
            <Search className="command-palette-search-icon" />
            <Command.Input
              value={query}
              onValueChange={setQuery}
              placeholder="Search messages, chats, games, agents..."
              className="command-palette-input"
              autoFocus
            />
            <button
              onClick={() => setShowFilters(!showFilters)}
              className="command-palette-filter-button"
              aria-label="Toggle advanced search filters"
            >
              <Filter className={`w-4 h-4 ${showFilters ? 'text-blue-500' : ''}`} />
            </button>
            <button
              onClick={onClose}
              className="command-palette-close-button"
              aria-label="Close"
            >
              <X className="w-4 h-4" />
            </button>
          </div>

          {showFilters && (
            <div className="command-palette-filters">
              <SearchFiltersComponent
                filters={selectedFilters}
                onFiltersChange={setSelectedFilters}
                games={dataSources.games}
                agents={dataSources.agents}
              />
            </div>
          )}

          <Command.List className="command-palette-list">
            <Command.Empty className="command-palette-empty">
              No results found.
            </Command.Empty>

            {query === '' && recentSearches.length > 0 && (
              <Command.Group heading="Recent Searches" className="command-palette-group">
                {recentSearches.slice(0, 5).map((recent) => (
                  <Command.Item
                    key={recent.id}
                    onSelect={() => handleSelectRecentSearch(recent)}
                    className="command-palette-item"
                  >
                    <Clock className="w-4 h-4" />
                    <div className="command-palette-item-content">
                      <div className="command-palette-item-title">{recent.query}</div>
                      <div className="command-palette-item-subtitle">
                        {recent.resultCount} results • {formatTimestamp(recent.timestamp)}
                      </div>
                    </div>
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        removeRecentSearch(recent.id);
                      }}
                      className="command-palette-item-action"
                      aria-label="Remove from recent"
                    >
                      <X className="w-3 h-3" />
                    </button>
                  </Command.Item>
                ))}
              </Command.Group>
            )}

            {results.length > 0 && (
              <Command.Group heading="Search Results" className="command-palette-group">
                {results.map((result) => (
                  <Command.Item
                    key={result.id}
                    onSelect={() => handleSelectResult(result)}
                    className="command-palette-item"
                  >
                    {getResultIcon(result.type)}
                    <div className="command-palette-item-content">
                      <div className="command-palette-item-title">{result.title}</div>
                      <div className="command-palette-item-subtitle">
                        {result.subtitle}
                        {result.timestamp && ` • ${formatTimestamp(result.timestamp)}`}
                      </div>
                    </div>
                    {result.relevanceScore !== undefined && (
                      <div className="command-palette-item-score">
                        {Math.round(result.relevanceScore * 100)}%
                      </div>
                    )}
                  </Command.Item>
                ))}
              </Command.Group>
            )}
          </Command.List>

          <div className="command-palette-footer">
            <div className="command-palette-footer-shortcuts">
              <kbd>↑↓</kbd> Navigate
              <kbd>Enter</kbd> Select
              <kbd>Esc</kbd> Close
            </div>
          </div>
        </Command>
      </div>
    </div>
  );
};

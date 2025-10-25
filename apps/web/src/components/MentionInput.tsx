import { useState, useRef, useEffect, useCallback } from "react";
import { api, type UserSearchResult } from "@/lib/api";
import { useDebounce } from "@/hooks/useDebounce";

interface MentionInputProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
  minLength?: number; // Minimum chars after @ to trigger search (default: 2)
}

interface MentionState {
  isOpen: boolean;
  query: string;
  startPos: number; // Position of @ character in textarea
  selectedIndex: number;
}

export function MentionInput({
  value,
  onChange,
  placeholder = "Write a comment and mention users with @username",
  disabled = false,
  minLength = 2
}: MentionInputProps) {
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const [mentionState, setMentionState] = useState<MentionState>({
    isOpen: false,
    query: "",
    startPos: -1,
    selectedIndex: 0
  });

  const [searchResults, setSearchResults] = useState<UserSearchResult[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const [dropdownPosition, setDropdownPosition] = useState<{ top: number; left: number }>({ top: 0, left: 0 });

  // Debounce the search query
  const debouncedQuery = useDebounce(mentionState.query, 300);

  // Detect @ mention and extract query
  const detectMention = useCallback((text: string, cursorPos: number): MentionState | null => {
    if (cursorPos === 0) return null;

    // Look backwards from cursor to find @ character
    let startPos = -1;
    for (let i = cursorPos - 1; i >= 0; i--) {
      const char = text[i];

      // If we hit whitespace or newline before @, no mention
      if (char === ' ' || char === '\n' || char === '\r') {
        break;
      }

      // Found @ character
      if (char === '@') {
        startPos = i;
        break;
      }
    }

    // No @ found
    if (startPos === -1) return null;

    // Extract query between @ and cursor
    const query = text.substring(startPos + 1, cursorPos);

    // Check if query meets minimum length
    if (query.length < minLength) return null;

    return {
      isOpen: true,
      query,
      startPos,
      selectedIndex: 0
    };
  }, [minLength]);

  // Handle textarea input change
  const handleTextChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    const newValue = e.target.value;
    const cursorPos = e.target.selectionStart;

    onChange(newValue);

    // Detect mention
    const mention = detectMention(newValue, cursorPos);

    if (mention) {
      setMentionState(mention);
      updateDropdownPosition();
    } else {
      setMentionState({
        isOpen: false,
        query: "",
        startPos: -1,
        selectedIndex: 0
      });
      setSearchResults([]);
    }
  };

  // Calculate dropdown position based on cursor position
  const updateDropdownPosition = () => {
    if (!textareaRef.current) return;

    const textarea = textareaRef.current;
    const cursorPos = textarea.selectionStart;

    // Create temporary element to measure text
    const tempDiv = document.createElement('div');
    const computedStyle = window.getComputedStyle(textarea);

    // Copy textarea styles to temp div
    tempDiv.style.position = 'absolute';
    tempDiv.style.visibility = 'hidden';
    tempDiv.style.width = computedStyle.width;
    tempDiv.style.font = computedStyle.font;
    tempDiv.style.padding = computedStyle.padding;
    tempDiv.style.border = computedStyle.border;
    tempDiv.style.whiteSpace = 'pre-wrap';
    tempDiv.style.wordWrap = 'break-word';

    // Set text up to cursor
    const textBeforeCursor = value.substring(0, cursorPos);
    tempDiv.textContent = textBeforeCursor;

    document.body.appendChild(tempDiv);

    // Get dimensions
    const textHeight = tempDiv.offsetHeight;
    const textareaRect = textarea.getBoundingClientRect();

    document.body.removeChild(tempDiv);

    // Position dropdown below cursor
    const top = textareaRect.top + textHeight - textarea.scrollTop + 20; // 20px offset
    const left = textareaRect.left + 12; // 12px padding

    setDropdownPosition({ top, left });
  };

  // Search users via API
  useEffect(() => {
    if (!mentionState.isOpen || !debouncedQuery || debouncedQuery.length < minLength) {
      setSearchResults([]);
      return;
    }

    const searchUsers = async () => {
      setIsSearching(true);
      try {
        const results = await api.get<UserSearchResult[]>(
          `/api/v1/users/search?query=${encodeURIComponent(debouncedQuery)}`
        );
        setSearchResults(results || []);
      } catch (error) {
        console.error("Failed to search users:", error);
        setSearchResults([]);
      } finally {
        setIsSearching(false);
      }
    };

    searchUsers();
  }, [debouncedQuery, mentionState.isOpen, minLength]);

  // Handle user selection
  const selectUser = useCallback((user: UserSearchResult) => {
    if (!textareaRef.current) return;

    const textarea = textareaRef.current;
    const beforeMention = value.substring(0, mentionState.startPos);
    const afterCursor = value.substring(textarea.selectionStart);

    // Replace @query with @DisplayName followed by space
    const newValue = `${beforeMention}@${user.displayName} ${afterCursor}`;
    onChange(newValue);

    // Close dropdown
    setMentionState({
      isOpen: false,
      query: "",
      startPos: -1,
      selectedIndex: 0
    });
    setSearchResults([]);

    // Set cursor after mention
    const newCursorPos = beforeMention.length + user.displayName.length + 2; // +2 for @ and space
    setTimeout(() => {
      if (textareaRef.current) {
        textareaRef.current.focus();
        textareaRef.current.setSelectionRange(newCursorPos, newCursorPos);
      }
    }, 0);
  }, [value, mentionState.startPos, onChange]);

  // Handle keyboard navigation
  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (!mentionState.isOpen || searchResults.length === 0) return;

    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        setMentionState(prev => ({
          ...prev,
          selectedIndex: Math.min(prev.selectedIndex + 1, searchResults.length - 1)
        }));
        break;

      case 'ArrowUp':
        e.preventDefault();
        setMentionState(prev => ({
          ...prev,
          selectedIndex: Math.max(prev.selectedIndex - 1, 0)
        }));
        break;

      case 'Enter':
        if (mentionState.isOpen && searchResults.length > 0) {
          e.preventDefault();
          selectUser(searchResults[mentionState.selectedIndex]);
        }
        break;

      case 'Escape':
        e.preventDefault();
        setMentionState({
          isOpen: false,
          query: "",
          startPos: -1,
          selectedIndex: 0
        });
        setSearchResults([]);
        break;

      case 'Tab':
        // Close dropdown on Tab
        setMentionState({
          isOpen: false,
          query: "",
          startPos: -1,
          selectedIndex: 0
        });
        setSearchResults([]);
        break;
    }
  };

  // Scroll selected item into view
  useEffect(() => {
    if (!dropdownRef.current || !mentionState.isOpen) return;

    const selectedElement = dropdownRef.current.children[mentionState.selectedIndex] as HTMLElement;
    if (selectedElement) {
      selectedElement.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
    }
  }, [mentionState.selectedIndex, mentionState.isOpen]);

  // Highlight matching text in search results
  const highlightMatch = (text: string, query: string) => {
    const index = text.toLowerCase().indexOf(query.toLowerCase());
    if (index === -1) return text;

    return (
      <>
        {text.substring(0, index)}
        <strong style={{ background: '#fff3cd' }}>
          {text.substring(index, index + query.length)}
        </strong>
        {text.substring(index + query.length)}
      </>
    );
  };

  return (
    <div style={{ position: 'relative', width: '100%' }}>
      <textarea
        ref={textareaRef}
        value={value}
        onChange={handleTextChange}
        onKeyDown={handleKeyDown}
        placeholder={placeholder}
        disabled={disabled}
        role="combobox"
        aria-expanded={mentionState.isOpen}
        aria-controls={mentionState.isOpen ? "mention-dropdown" : undefined}
        aria-autocomplete="list"
        aria-activedescendant={
          mentionState.isOpen && searchResults.length > 0
            ? `mention-option-${mentionState.selectedIndex}`
            : undefined
        }
        style={{
          width: "100%",
          minHeight: 80,
          padding: 12,
          border: "1px solid #ccc",
          borderRadius: 4,
          fontSize: 14,
          fontFamily: "inherit",
          marginBottom: 8,
          resize: "vertical"
        }}
      />

      {/* Dropdown for mention autocomplete */}
      {mentionState.isOpen && (
        <div
          id="mention-dropdown"
          ref={dropdownRef}
          role="listbox"
          style={{
            position: 'fixed',
            top: dropdownPosition.top,
            left: dropdownPosition.left,
            zIndex: 1000,
            background: 'white',
            border: '1px solid #ccc',
            borderRadius: 4,
            boxShadow: '0 4px 12px rgba(0, 0, 0, 0.15)',
            maxHeight: 200,
            overflowY: 'auto',
            minWidth: 250,
            maxWidth: 400
          }}
        >
          {isSearching ? (
            <div style={{ padding: 12, fontSize: 14, color: '#666' }}>
              Searching users...
            </div>
          ) : searchResults.length === 0 ? (
            <div style={{ padding: 12, fontSize: 14, color: '#666' }}>
              No users found
            </div>
          ) : (
            searchResults.map((user, index) => (
              <div
                key={user.id}
                id={`mention-option-${index}`}
                role="option"
                aria-selected={index === mentionState.selectedIndex}
                onClick={() => selectUser(user)}
                onMouseEnter={() => setMentionState(prev => ({ ...prev, selectedIndex: index }))}
                style={{
                  padding: '10px 12px',
                  cursor: 'pointer',
                  background: index === mentionState.selectedIndex ? '#e3f2fd' : 'transparent',
                  borderBottom: index < searchResults.length - 1 ? '1px solid #f0f0f0' : 'none',
                  transition: 'background 0.1s'
                }}
              >
                <div style={{ fontSize: 14, fontWeight: 500, marginBottom: 2 }}>
                  {highlightMatch(user.displayName, mentionState.query)}
                </div>
                <div style={{ fontSize: 12, color: '#666' }}>
                  {highlightMatch(user.email, mentionState.query)}
                </div>
              </div>
            ))
          )}
        </div>
      )}
    </div>
  );
}

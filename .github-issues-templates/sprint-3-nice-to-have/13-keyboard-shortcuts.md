# [ENHANCEMENT] Keyboard Shortcuts System

## 🎯 Objective
Add keyboard shortcuts for power users.

## ✅ Acceptance Criteria
- [ ] Command palette (Cmd+K / Ctrl+K)
- [ ] Shortcuts for common actions:
  - `Cmd+N`: New chat
  - `Cmd+U`: Upload PDF
  - `Cmd+/`: Focus search
  - `Esc`: Close modal
  - `Cmd+Enter`: Send message
- [ ] Shortcuts help modal (`?`)
- [ ] Visual indicators for keyboard shortcuts

## 🏗️ Implementation
Use `cmdk` library for command palette:
```tsx
import { Command } from 'cmdk';

<Command>
  <CommandInput placeholder="Type a command or search..." />
  <CommandList>
    <CommandGroup heading="Actions">
      <CommandItem onSelect={() => router.push('/chat')}>
        <MessageSquare className="mr-2" />
        New Chat
        <CommandShortcut>⌘N</CommandShortcut>
      </CommandItem>
      ...
    </CommandGroup>
  </CommandList>
</Command>
```

## ⏱️ Effort: **1 day** | **Sprint 3** | **Priority**: 🟢 Low

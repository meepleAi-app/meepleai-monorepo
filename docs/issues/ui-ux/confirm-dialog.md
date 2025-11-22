# Issue: Replace window.confirm with Dialog Component

**ID**: UX-001
**Category**: UI/UX
**Priority**: 🟡 **HIGH**
**Status**: 🔴 Open
**Created**: 2025-11-19

---

## 📋 Summary

Sostituire `window.confirm()` con un componente Dialog personalizzato per migliorare UX e testability.

---

## 🎯 Problem Statement

### Current Code
```typescript
// ❌ PROBLEMA: window.confirm in store
deleteMessage: async (messageId) => {
  if (!confirm('Sei sicuro?')) return;
  await api.chat.deleteMessage(threadId, messageId);
}
```

**Issues**:
- ⚠️ **Bad UX** - Browser confirm è brutto
- ⚠️ **Not testable** - confirm() non mockabile
- ⚠️ **Tight coupling** - Store dipende da browser API
- ⚠️ **SSR issue** - window non disponibile in SSR

---

## 🔧 Solution

### 1. Create ConfirmDialog Component

**File**: `apps/web/src/components/ui/confirm-dialog.tsx`
```typescript
interface ConfirmDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  message: string;
  onConfirm: () => void | Promise<void>;
  variant?: 'default' | 'destructive';
}

export function ConfirmDialog({ ... }: ConfirmDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{message}</DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Annulla
          </Button>
          <Button variant={variant} onClick={onConfirm}>
            Conferma
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

### 2. Create useConfirmDialog Hook

```typescript
export function useConfirmDialog() {
  const [state, setState] = useState({ open: false, ... });

  const confirm = (options) => {
    return new Promise<boolean>((resolve) => {
      setState({ ...options, open: true, onConfirm: () => resolve(true) });
    });
  };

  return { confirm, dialog: <ConfirmDialog {...state} /> };
}
```

### 3. Update Store (Remove UI Logic)

```typescript
// AFTER: No window.confirm
deleteMessage: async (messageId) => {
  try {
    await api.chat.deleteMessage(threadId, messageId);
    await loadMessages(threadId);
  } catch (err) {
    logger.error('Failed to delete', err);
    throw err; // Propagate to component
  }
}
```

### 4. Component Usage

```typescript
function MessageActions({ message }) {
  const { confirm, dialog } = useConfirmDialog();
  const deleteMessage = useChatStore(state => state.deleteMessage);

  const handleDelete = async () => {
    await confirm({
      title: 'Elimina messaggio',
      message: 'Sei sicuro?',
      variant: 'destructive',
      onConfirm: () => deleteMessage(message.id),
    });
  };

  return (
    <>
      <Button onClick={handleDelete}>Elimina</Button>
      {dialog}
    </>
  );
}
```

---

## 📝 Implementation Checklist

- [ ] Create ConfirmDialog component
- [ ] Create useConfirmDialog hook
- [ ] Remove window.confirm from messagesSlice
- [ ] Update all delete actions (~5 components)
- [ ] Write tests
- [ ] Update Storybook

---

## ✅ Acceptance Criteria

- [ ] No window.confirm in codebase
- [ ] Custom dialog component created
- [ ] Better UX (styled dialogs)
- [ ] Testable (mockable)
- [ ] SSR-safe

---

## 📊 Effort: 8 hours

---

**Last Updated**: 2025-11-19
**Status**: 🔴 Open

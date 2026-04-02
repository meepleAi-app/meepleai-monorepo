# Admin Workflow: Complete SharedGame Creation with Agent

**Workflow**: Admin Dashboard → Shared Game Management → Bulk Upload → PDF Processing → Agent Creation

---

## 🔄 Complete Flow

```
1. Login Admin
   ↓
2. Admin Dashboard
   ↓
3. Gestisci Shared Game (Manage Shared Games)
   ↓
4. Crea/Carica Bulk in SharedGame (Create/Upload Bulk)
   ↓
5. Inserimento con PDF (Insert with PDF)
   ↓
6. PDF Preview
   ↓
7. Upload PDF
   ↓
8. Embedding Process (si vede il progress - visible progress)
   ↓
9. Si vede in lista KB della card del Shared Game (Visible in KB list of SharedGame card)
   ↓
10. Creo Agente (Create Agent)
    ↓
11. In Shared Game visualizzo gioco creato (View created game in SharedGame)
```

---

## 🎯 Key Steps

### Step 1-2: Authentication & Navigation
- Admin login required
- Navigate to Admin Dashboard
- Access Shared Game management section

### Step 3-4: Bulk Creation/Upload
- Manage existing shared games OR
- Create new shared games in bulk
- Upload multiple games at once

### Step 5-7: PDF Upload & Preview
- Insert game with PDF documentation
- Preview PDF before upload
- Confirm and upload

### Step 8: Embedding Process
- **Progress visible** (real-time feedback)
- PDF processed and embedded
- Vector embeddings created for RAG

### Step 9: KB Verification
- Knowledge Base list shows processed PDF
- Visible in SharedGame card KB section
- Confirmation of successful processing

### Step 10: Agent Creation
- Create AI agent for the game
- Link agent to SharedGame
- Configure agent parameters

### Step 11: Verification
- View created game in SharedGame list
- Verify all components linked correctly
- Complete workflow validation

---

## 📋 Related Issues/PRs

**Epic #4068** (Complete ✅):
- Permission system (controls admin access)
- Tag system (visual game categorization)
- Agent metadata (status display)

**PR #4270** (In Progress):
- Bulk collection actions backend
- Supports bulk operations in this workflow

**Related Features**:
- PDF upload and processing
- Embedding progress tracking
- Knowledge Base integration
- Agent creation and linking

---

## 🔧 Technical Components

**Backend**:
- SharedGame entity
- PDF processing pipeline
- Embedding service
- Agent creation commands
- Bulk operation handlers

**Frontend**:
- Admin dashboard
- SharedGame management UI
- PDF upload with preview
- Progress indicators
- Agent builder integration
- KB list display

---

**Workflow Status**: Documented ✅
**Implementation**: Partially complete (components exist, integration ongoing)
**Related Work**: PR #4270, Epic #4136 (Admin flows)

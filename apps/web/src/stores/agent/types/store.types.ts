/**
 * Agent Store Types (Issue #3188)
 * Issue #3251 (FRONT-015) - Added PdfViewerSlice
 *
 * Shared types for agent store slices to avoid circular dependencies
 */

import { ConfigSlice } from '../slices/configSlice';
import { ConversationSlice } from '../slices/conversationSlice';
import { PdfViewerSlice } from '../slices/pdfViewerSlice';
import { SessionSlice } from '../slices/sessionSlice';
import { UISlice } from '../slices/uiSlice';

/**
 * Combined Agent Store Type
 *
 * Combines all slices into a single store interface
 */
export type AgentStore = ConfigSlice & SessionSlice & ConversationSlice & UISlice & PdfViewerSlice;

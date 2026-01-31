// Barrel exports for prompt module
// Issue #994: Use lazy-loaded PromptEditor for ~800KB bundle savings
export { default as PromptEditor } from './LazyPromptEditor';
export { default as PromptVersionCard } from './PromptVersionCard';
// Direct import available if SSR needed: import PromptEditor from '@/components/prompt/PromptEditor';

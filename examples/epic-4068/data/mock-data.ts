/**
 * Mock data for Epic #4068 examples
 */

import { Sparkles, Tag as TagIcon, Check, Heart, Brain, Eye, Code2, Zap, MessageSquare } from 'lucide-react';
import type { Tag } from '@/types/tags';
import type { AgentMetadata, AgentStatus } from '@/types/agent';
import type { UserPermissions, UserTier, UserRole } from '@/types/permissions';

// ============================================================================
// Mock Users with Different Tiers/Roles
// ============================================================================

export const MOCK_USERS: Record<string, UserPermissions> = {
  free: {
    tier: 'free',
    role: 'user',
    status: 'Active',
    limits: {
      maxGames: 50,
      storageQuotaMB: 100
    },
    accessibleFeatures: ['wishlist']
  },

  normal: {
    tier: 'normal',
    role: 'user',
    status: 'Active',
    limits: {
      maxGames: 100,
      storageQuotaMB: 500
    },
    accessibleFeatures: ['wishlist', 'drag-drop', 'collection.manage', 'document.upload', 'filters.advanced']
  },

  pro: {
    tier: 'pro',
    role: 'user',
    status: 'Active',
    limits: {
      maxGames: 500,
      storageQuotaMB: 5000
    },
    accessibleFeatures: [
      'wishlist',
      'bulk-select',
      'drag-drop',
      'collection.manage',
      'document.upload',
      'agent.create',
      'analytics.view',
      'filters.advanced'
    ]
  },

  enterprise: {
    tier: 'enterprise',
    role: 'user',
    status: 'Active',
    limits: {
      maxGames: 999999,
      storageQuotaMB: 999999
    },
    accessibleFeatures: [
      'wishlist',
      'bulk-select',
      'drag-drop',
      'collection.manage',
      'document.upload',
      'agent.create',
      'analytics.view',
      'filters.advanced'
    ]
  },

  editor: {
    tier: 'free',
    role: 'editor',
    status: 'Active',
    limits: {
      maxGames: 50,
      storageQuotaMB: 100
    },
    accessibleFeatures: ['wishlist', 'bulk-select'] // Editor role grants bulk-select
  },

  creator: {
    tier: 'normal',
    role: 'creator',
    status: 'Active',
    limits: {
      maxGames: 100,
      storageQuotaMB: 500
    },
    accessibleFeatures: [
      'wishlist',
      'drag-drop',
      'collection.manage',
      'document.upload',
      'filters.advanced',
      'agent.create', // Creator role grants agent creation
      'quick-action.edit'
    ]
  },

  admin: {
    tier: 'free',
    role: 'admin',
    status: 'Active',
    limits: {
      maxGames: 50,
      storageQuotaMB: 100
    },
    accessibleFeatures: [
      'wishlist',
      'bulk-select', // Admin > Editor (role hierarchy)
      'quick-action.edit',
      'quick-action.delete',
      'analytics.view'
    ]
  },

  suspended: {
    tier: 'pro',
    role: 'user',
    status: 'Suspended',
    limits: {
      maxGames: 500,
      storageQuotaMB: 5000
    },
    accessibleFeatures: [] // Suspended users have no access
  }
};

// ============================================================================
// Mock Games with Tags
// ============================================================================

export const MOCK_GAME_TAGS: Record<string, Tag[]> = {
  wingspan: [
    { id: 'new', label: 'New', icon: Sparkles, bgColor: 'hsl(142 76% 36%)', color: 'hsl(0 0% 100%)', tooltip: 'Recently added to catalog' },
    { id: 'owned', label: 'Owned', icon: Check, bgColor: 'hsl(221 83% 53%)', color: 'hsl(0 0% 100%)', tooltip: 'In your collection' }
  ],

  azul: [
    { id: 'sale', label: 'Sale', icon: TagIcon, bgColor: 'hsl(0 84% 60%)', color: 'hsl(0 0% 100%)', tooltip: 'On sale: 20% off' },
    { id: 'wishlisted', label: 'Wishlist', icon: Heart, bgColor: 'hsl(350 89% 60%)', color: 'hsl(0 0% 100%)', tooltip: 'On your wishlist' }
  ],

  catan: [
    { id: 'owned', label: 'Owned', icon: Check, bgColor: 'hsl(221 83% 53%)', color: 'hsl(0 0% 100%)' }
  ],

  gloomhaven: [
    { id: 'new', label: 'New', icon: Sparkles, bgColor: 'hsl(142 76% 36%)', color: 'hsl(0 0% 100%)' },
    { id: 'sale', label: 'Sale', icon: TagIcon, bgColor: 'hsl(0 84% 60%)', color: 'hsl(0 0% 100%)' },
    { id: 'exclusive', label: 'Exclusive', bgColor: 'hsl(262 83% 58%)', color: 'hsl(0 0% 100%)' },
    { id: 'wishlisted', label: 'Wishlist', icon: Heart, bgColor: 'hsl(350 89% 60%)', color: 'hsl(0 0% 100%)' },
    { id: 'preorder', label: 'Pre-order', bgColor: 'hsl(38 92% 50%)', color: 'hsl(0 0% 100%)' }
  ]
};

// ============================================================================
// Mock Agents with Metadata
// ============================================================================

export const MOCK_AGENTS: Array<{
  id: string;
  name: string;
  strategy: string;
  metadata: AgentMetadata;
}> = [
  {
    id: 'agent-1',
    name: 'Azul Rules Expert',
    strategy: 'RAG Strategy',
    metadata: {
      status: 'active' as AgentStatus,
      model: {
        name: 'GPT-4o-mini',
        temperature: 0.7,
        maxTokens: 2000
      },
      invocationCount: 342,
      lastExecuted: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(), // 2 hours ago
      avgResponseTime: 450,
      capabilities: ['RAG', 'Vision']
    }
  },

  {
    id: 'agent-2',
    name: 'Strategy Analyzer',
    strategy: 'Analysis Agent',
    metadata: {
      status: 'idle' as AgentStatus,
      model: {
        name: 'Claude-3-Sonnet',
        temperature: 0.5,
        maxTokens: 4000
      },
      invocationCount: 1523,
      lastExecuted: new Date(Date.now() - 4 * 60 * 60 * 1000).toISOString(), // 4 hours ago
      capabilities: ['RAG', 'Code']
    }
  },

  {
    id: 'agent-3',
    name: 'Image Describer',
    strategy: 'Vision Model',
    metadata: {
      status: 'training' as AgentStatus,
      model: {
        name: 'GPT-4-Vision',
        temperature: 0.3,
        maxTokens: 1000
      },
      invocationCount: 45,
      capabilities: ['Vision']
    }
  },

  {
    id: 'agent-4',
    name: 'Broken Agent',
    strategy: 'Error State Demo',
    metadata: {
      status: 'error' as AgentStatus,
      model: {
        name: 'GPT-3.5-Turbo'
      },
      invocationCount: 12,
      lastExecuted: new Date(Date.now() - 30 * 60 * 1000).toISOString(), // 30 minutes ago
      capabilities: []
    }
  },

  {
    id: 'agent-5',
    name: 'Ultimate Agent',
    strategy: 'All Capabilities',
    metadata: {
      status: 'active' as AgentStatus,
      model: {
        name: 'GPT-4o',
        temperature: 0.8,
        maxTokens: 4000
      },
      invocationCount: 5432,
      lastExecuted: new Date(Date.now() - 5 * 60 * 1000).toISOString(), // 5 minutes ago
      avgResponseTime: 1200,
      capabilities: ['RAG', 'Vision', 'Code', 'Functions', 'MultiTurn']
    }
  }
];

// ============================================================================
// Mock Agent Capability Tags
// ============================================================================

export const MOCK_AGENT_TAGS: Record<string, Tag[]> = {
  rag: [
    { id: 'rag', label: 'RAG', icon: Brain, bgColor: 'hsl(38 92% 50%)', color: 'hsl(0 0% 100%)' }
  ],

  vision: [
    { id: 'vision', label: 'Vision', icon: Eye, bgColor: 'hsl(262 83% 58%)', color: 'hsl(0 0% 100%)' }
  ],

  code: [
    { id: 'code', label: 'Code', icon: Code2, bgColor: 'hsl(210 40% 55%)', color: 'hsl(0 0% 100%)' }
  ],

  functions: [
    { id: 'functions', label: 'Functions', icon: Zap, bgColor: 'hsl(220 80% 55%)', color: 'hsl(0 0% 100%)' }
  ],

  multiTurn: [
    { id: 'multiTurn', label: 'Multi-turn', icon: MessageSquare, bgColor: 'hsl(142 76% 36%)', color: 'hsl(0 0% 100%)' }
  ]
};

// ============================================================================
// Mock Collection Stats
// ============================================================================

export const MOCK_COLLECTIONS = {
  free_low: {
    tier: 'free' as UserTier,
    gameCount: 12,
    maxGames: 50,
    storageMB: 25,
    storageQuotaMB: 100
  },

  normal_medium: {
    tier: 'normal' as UserTier,
    gameCount: 60,
    maxGames: 100,
    storageMB: 275,
    storageQuotaMB: 500
  },

  pro_high: {
    tier: 'pro' as UserTier,
    gameCount: 420,
    maxGames: 500,
    storageMB: 3750,
    storageQuotaMB: 5000
  },

  pro_critical: {
    tier: 'pro' as UserTier,
    gameCount: 475,
    maxGames: 500,
    storageMB: 4600,
    storageQuotaMB: 5000
  },

  enterprise_unlimited: {
    tier: 'enterprise' as UserTier,
    gameCount: 1523,
    maxGames: 999999,
    storageMB: 15000,
    storageQuotaMB: 999999
  }
};

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Get mock user by email
 */
export function getMockUser(email: string): UserPermissions | null {
  const key = email.split('@')[0]; // Extract: free@example.com → free
  return MOCK_USERS[key] ?? null;
}

/**
 * Get mock game tags by game name
 */
export function getMockGameTags(gameName: string): Tag[] {
  return MOCK_GAME_TAGS[gameName.toLowerCase()] ?? [];
}

/**
 * Get mock agent by ID
 */
export function getMockAgent(agentId: string) {
  return MOCK_AGENTS.find(a => a.id === agentId);
}

/**
 * Get mock collection stats by scenario
 */
export function getMockCollectionStats(scenario: keyof typeof MOCK_COLLECTIONS) {
  return MOCK_COLLECTIONS[scenario];
}

/**
 * Simulate permission check
 */
export function simulatePermissionCheck(
  user: UserPermissions,
  feature: string
): { hasAccess: boolean; reason: string } {
  const hasAccess = user.accessibleFeatures.includes(feature);

  if (hasAccess) {
    return {
      hasAccess: true,
      reason: 'Tier or role sufficient'
    };
  }

  // Determine why denied
  const featureRequirements: Record<string, { tier?: UserTier; role?: UserRole }> = {
    'bulk-select': { tier: 'pro', role: 'editor' },
    'agent.create': { tier: 'pro', role: 'creator' },
    'quick-action.delete': { role: 'admin' },
    'analytics.view': { tier: 'pro', role: 'admin' }
  };

  const required = featureRequirements[feature];

  if (required) {
    return {
      hasAccess: false,
      reason: `Requires ${required.tier ?? 'any tier'} tier or ${required.role ?? 'higher'} role`
    };
  }

  return {
    hasAccess: false,
    reason: 'Unknown feature or insufficient permissions'
  };
}

/**
 * Agent Discovery Page (Task #9 - Issue #11)
 *
 * Public page to discover and explore available AI agents.
 * Features:
 * - Agent cards with info (name, description, capabilities)
 * - "Chat with" buttons linking to chat with pre-selected agent
 * - Filter by agent type
 * - Search functionality
 */

'use client';

import { useState, useMemo } from 'react';

import { useQuery } from '@tanstack/react-query';
import { Bot, Shield, Brain, MessageCircle, Search } from 'lucide-react';
import Link from 'next/link';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';

// ============================================================================
// Types & Constants
// ============================================================================

interface AgentInfo {
  id: string;
  name: string;
  type: 'tutor' | 'arbitro' | 'decisore';
  description: string;
  capabilities: string[];
  icon: typeof Bot;
  color: string;
}

const AVAILABLE_AGENTS: AgentInfo[] = [
  {
    id: 'tutor',
    name: 'Tutor Agent',
    type: 'tutor',
    description: 'Interactive tutorials, setup guidance, and rules Q&A',
    capabilities: ['Game setup help', 'Rules clarification', 'Tutorial mode', 'FAQ answers'],
    icon: Bot,
    color: 'blue',
  },
  {
    id: 'arbitro',
    name: 'Arbitro Agent',
    type: 'arbitro',
    description: 'Real-time move validation and rules arbitration',
    capabilities: ['Move validation', 'Rules arbitration', 'Conflict resolution', 'Legal move checking'],
    icon: Shield,
    color: 'purple',
  },
  {
    id: 'decisore',
    name: 'Decisore Agent',
    type: 'decisore',
    description: 'Strategic move suggestions and position analysis',
    capabilities: ['Move suggestions', 'Position analysis', 'Strategy advice', 'Victory paths'],
    icon: Brain,
    color: 'orange',
  },
];

const COLOR_CLASSES = {
  blue: {
    card: 'border-l-blue-500',
    badge: 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400',
    button: 'bg-blue-600 hover:bg-blue-700',
  },
  purple: {
    card: 'border-l-purple-500',
    badge: 'bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-400',
    button: 'bg-purple-600 hover:bg-purple-700',
  },
  orange: {
    card: 'border-l-orange-500',
    badge: 'bg-orange-100 text-orange-800 dark:bg-orange-900/30 dark:text-orange-400',
    button: 'bg-orange-600 hover:bg-orange-700',
  },
};

// ============================================================================
// Main Component
// ============================================================================

export default function DiscoverAgentsPage() {
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedType, setSelectedType] = useState<string | null>(null);

  // Fetch agent availability status (optional, for real-time status)
  const { data: agentStatuses } = useQuery({
    queryKey: ['agents', 'status'],
    queryFn: async () => {
      // In POC, all agents are available
      // Real implementation: await api.agents.getAvailable()
      return AVAILABLE_AGENTS.map(a => ({ id: a.id, status: 'online' }));
    },
    refetchInterval: 30000, // Refresh every 30s
  });

  // Filter agents
  const filteredAgents = useMemo(() => {
    return AVAILABLE_AGENTS.filter((agent) => {
      const matchesSearch =
        !searchQuery ||
        agent.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
        agent.description.toLowerCase().includes(searchQuery.toLowerCase());

      const matchesType = !selectedType || agent.type === selectedType;

      return matchesSearch && matchesType;
    });
  }, [searchQuery, selectedType]);

  // ============================================================================
  // Render
  // ============================================================================

  return (
    <div className="min-h-screen bg-[#FAF8F5] py-12 px-4">
      <div className="container mx-auto max-w-6xl">
        {/* Header */}
        <div className="text-center mb-12">
          <h1 className="text-4xl font-bold font-quicksand mb-4">
            Discover AI Agents
          </h1>
          <p className="text-lg text-muted-foreground font-nunito max-w-2xl mx-auto">
            Explore our specialized AI agents designed to help you with board games
          </p>
        </div>

        {/* Search & Filters */}
        <div className="mb-8 flex flex-col sm:flex-row gap-4">
          <div className="relative flex-1">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <Input
              type="text"
              placeholder="Search agents..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="pl-10 font-nunito"
            />
          </div>
          <div className="flex gap-2">
            <Button
              variant={selectedType === null ? 'default' : 'outline'}
              onClick={() => setSelectedType(null)}
              size="sm"
              className="font-nunito"
            >
              All
            </Button>
            <Button
              variant={selectedType === 'tutor' ? 'default' : 'outline'}
              onClick={() => setSelectedType('tutor')}
              size="sm"
              className="font-nunito"
            >
              Tutor
            </Button>
            <Button
              variant={selectedType === 'arbitro' ? 'default' : 'outline'}
              onClick={() => setSelectedType('arbitro')}
              size="sm"
              className="font-nunito"
            >
              Arbitro
            </Button>
            <Button
              variant={selectedType === 'decisore' ? 'default' : 'outline'}
              onClick={() => setSelectedType('decisore')}
              size="sm"
              className="font-nunito"
            >
              Decisore
            </Button>
          </div>
        </div>

        {/* Agent Cards Grid */}
        <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-6">
          {filteredAgents.map((agent) => {
            const Icon = agent.icon;
            const colors = COLOR_CLASSES[agent.color as keyof typeof COLOR_CLASSES];

            return (
              <Card
                key={agent.id}
                className={`border-l-4 ${colors.card} shadow-lg hover:shadow-xl transition-shadow`}
              >
                <CardHeader>
                  <div className="flex items-center gap-3 mb-2">
                    <div className={`flex h-12 w-12 items-center justify-center rounded-full bg-${agent.color}-500/10`}>
                      <Icon className={`h-6 w-6 text-${agent.color}-600`} />
                    </div>
                    <div className="flex-1 min-w-0">
                      <CardTitle className="font-quicksand">{agent.name}</CardTitle>
                      <Badge className={colors.badge}>{agent.type}</Badge>
                    </div>
                  </div>
                  <CardDescription className="font-nunito">
                    {agent.description}
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  {/* Capabilities */}
                  <div>
                    <h4 className="text-sm font-medium mb-2 font-quicksand">Capabilities:</h4>
                    <ul className="space-y-1">
                      {agent.capabilities.map((cap, idx) => (
                        <li key={idx} className="text-sm text-muted-foreground flex items-center gap-2">
                          <span className="h-1 w-1 rounded-full bg-primary" />
                          {cap}
                        </li>
                      ))}
                    </ul>
                  </div>

                  {/* Chat Button (Issue #11) */}
                  <Button
                    asChild
                    className={`w-full ${colors.button} text-white font-nunito`}
                  >
                    <Link href={`/chat?agent=${agent.id}`}>
                      <MessageCircle className="mr-2 h-4 w-4" />
                      Chat with {agent.name}
                    </Link>
                  </Button>
                </CardContent>
              </Card>
            );
          })}
        </div>

        {/* No Results */}
        {filteredAgents.length === 0 && (
          <div className="text-center py-12">
            <p className="text-muted-foreground font-nunito">
              No agents found matching your search
            </p>
          </div>
        )}
      </div>
    </div>
  );
}

"use client";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { useState } from "react";
import { Search, X } from "lucide-react";

interface Activity {
  id: string;
  type: "game_added" | "session" | "chat" | "wishlist";
  title: string;
  timestamp: Date;
}

interface ActivityFilters {
  types?: string[];
  search?: string;
  dateFrom?: Date;
  dateTo?: Date;
}

export function AdvancedActivityTimeline({ initialActivities = [], filters, onFilterChange }: {
  initialActivities?: Activity[];
  filters?: ActivityFilters;
  onFilterChange?: (filters: ActivityFilters) => void;
}) {
  const [search, setSearch] = useState(filters?.search ?? "");
  const [selectedTypes, setSelectedTypes] = useState<string[]>(filters?.types ?? []);

  const typeOptions = [
    { value: "game_added", label: "Giochi" },
    { value: "session", label: "Sessioni" },
    { value: "chat", label: "Chat" },
    { value: "wishlist", label: "Wishlist" },
  ];

  const handleTypeToggle = (type: string) => {
    const newTypes = selectedTypes.includes(type)
      ? selectedTypes.filter((t) => t !== type)
      : [...selectedTypes, type];
    setSelectedTypes(newTypes);
    onFilterChange?.({ ...filters, types: newTypes });
  };

  const handleSearchChange = (value: string) => {
    setSearch(value);
    onFilterChange?.({ ...filters, search: value });
  };

  const resetFilters = () => {
    setSearch("");
    setSelectedTypes([]);
    onFilterChange?.({});
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="font-quicksand">Attività Recente</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex flex-wrap gap-2 items-center">
          {typeOptions.map((option) => (
            <label key={option.value} className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={selectedTypes.includes(option.value)}
                onCheckedChange={() => handleTypeToggle(option.value)}
              />
              {option.label}
            </label>
          ))}
        </div>
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-stone-400" />
          <Input
            value={search}
            onChange={(e) => handleSearchChange(e.target.value)}
            placeholder="Cerca attività..."
            className="pl-9"
          />
          {search && (
            <button
              onClick={() => handleSearchChange("")}
              className="absolute right-3 top-1/2 -translate-y-1/2"
            >
              <X className="h-4 w-4 text-stone-400" />
            </button>
          )}
        </div>
        {(selectedTypes.length > 0 || search) && (
          <Button variant="ghost" size="sm" onClick={resetFilters}>
            Reset Filtri
          </Button>
        )}
        <div className="space-y-2">
          {initialActivities.map((activity) => (
            <div key={activity.id} className="p-2 border-l-2 border-primary/20">
              <p className="text-sm font-medium">{activity.title}</p>
              <p className="text-xs text-muted-foreground">{activity.type}</p>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

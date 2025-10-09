# RuleSpec Schema Documentation

This directory contains JSON Schema definitions for the **RuleSpec** format - a formal specification for normalized board game rules.

## Schemas

### `rulespec.v0.schema.json`

**Version**: v0.1
**Status**: ✅ Validated with example

The RuleSpec v0 schema provides a comprehensive structure for representing board game rules in a machine-readable format.

#### Key Sections

1. **Metadata** - Game information (name, players, time, age)
2. **Setup** - Initial game setup instructions and components
3. **Phases** - Game flow structure (turns, rounds, steps)
4. **Actions** - All possible player actions with prerequisites and effects
5. **Scoring** - Scoring methods, sources, and tiebreakers
6. **End Conditions** - Conditions that trigger game end
7. **Edge Cases** - Exceptions, clarifications, variants, FAQs
8. **Glossary** - Game-specific terminology

#### Schema Structure

```json
{
  "gameId": "unique-game-id",
  "version": "v0.1",
  "metadata": { ... },
  "setup": {
    "steps": [ ... ],
    "components": [ ... ]
  },
  "phases": [ ... ],
  "actions": [ ... ],
  "scoring": {
    "method": "points|elimination|objective|hybrid",
    "sources": [ ... ],
    "tiebreakers": [ ... ]
  },
  "endConditions": [ ... ],
  "edgeCases": [ ... ],
  "glossary": [ ... ]
}
```

## Examples

### `examples/tic-tac-toe.rulespec.json`

A complete RuleSpec example for Tic-Tac-Toe demonstrating all schema features:
- 2 players
- Simple setup (3x3 grid)
- 1 phase (player turn)
- 1 action (place mark)
- Objective-based scoring
- Win/draw conditions
- Edge cases and clarifications
- Glossary terms

## Validation

### Automated Validation Script

The repository includes a validation script that checks all RuleSpec examples:

```bash
# Validate all examples in schemas/examples/
node schemas/validate-all-examples.js
```

This script:
- ✅ Validates all `.rulespec.json` files in `examples/`
- ✅ Checks required fields and structure
- ✅ Verifies version format (`vX.Y`)
- ✅ Validates scoring methods and other enums
- ✅ Provides detailed summary for each example
- ✅ Runs automatically in CI on every PR

### Online Validators

Use these tools to validate RuleSpec JSON against the schema:

- [JSON Schema Validator](https://www.jsonschemavalidator.net/)
- [JSON Editor Online](https://jsoneditoronline.org/)

### Command Line (Advanced)

```bash
# Using ajv-cli (install with: npm install -g ajv-cli)
ajv validate -s schemas/rulespec.v0.schema.json -d schemas/examples/tic-tac-toe.rulespec.json

# Validate all examples with ajv-cli
ajv validate -s schemas/rulespec.v0.schema.json -d "schemas/examples/*.rulespec.json"
```

### Programmatic Validation (C#)

```csharp
using System.Text.Json;
using Api.Models;

// Deserialize JSON to C# model
var json = File.ReadAllText("schemas/examples/tic-tac-toe.rulespec.json");
var ruleSpec = JsonSerializer.Deserialize<RuleSpecV0>(json);

// Models in: apps/api/src/Api/Models/RuleSpecV0.cs
```

## Design Principles

1. **Completeness**: Captures all essential game rules in one document
2. **Clarity**: Human-readable structure with clear field names
3. **Extensibility**: Optional fields allow for simple to complex games
4. **Validation**: JSON Schema ensures structural correctness
5. **Machine-Readable**: Enables AI/LLM processing and rule querying

## Use Cases

- **AI Game Assistants**: LLMs can parse RuleSpec to answer rules questions
- **Rule Validation**: Check if a game state or action is valid
- **Tutorial Generation**: Auto-generate setup guides and tutorials
- **Digital Implementation**: Convert RuleSpec to game engine rules
- **Rules Comparison**: Analyze differences between game versions

## Future Enhancements

- **RuleSpec v1.0**: Add probabilities, card decks, dice mechanics
- **Visual Diagrams**: Auto-generate flowcharts from phases/actions
- **Localization**: Support for multi-language rules
- **Validation Rules**: Semantic validation beyond structural checks

## Related Files

- **C# Models**: `apps/api/src/Api/Models/RuleSpecV0.cs`
- **Legacy Schema**: `rulespec.schema.json` (deprecated, use v0 schema)
- **Database Entity**: `apps/api/src/Api/Infrastructure/Entities/RuleSpecEntity.cs`

## Contributing

When adding new examples:
1. Validate against `rulespec.v0.schema.json`
2. Use descriptive `gameId` (lowercase, hyphenated)
3. Include all required fields
4. Add edge cases and glossary terms
5. Document any game-specific conventions

## Questions?

See issue #13 (RULE-01) for discussion and feedback.
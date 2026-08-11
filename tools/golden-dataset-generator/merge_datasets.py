#!/usr/bin/env python3
"""
Script to merge board_game_qa_batch*.json files into golden_dataset.json
"""

import json
from pathlib import Path
from collections import defaultdict

def load_json(filepath):
    """Load JSON file"""
    with open(filepath, 'r', encoding='utf-8') as f:
        return json.load(f)

def save_json(filepath, data):
    """Save JSON file with pretty formatting"""
    with open(filepath, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, indent=2)

def merge_datasets():
    """Merge all batch files into golden dataset"""
    data_dir = Path(__file__).parent

    # Load golden dataset
    golden = load_json(data_dir / 'golden_dataset.json')

    # Load batch files
    batch_files = sorted(data_dir.glob('board_game_qa_batch*.json'))

    print(f"Found {len(batch_files)} batch files to merge")

    # Dictionary to aggregate test cases by game_id or game_name
    games_dict = {}
    # Set to track seen test case IDs
    seen_ids = set()

    # Process each batch file
    total_test_cases = 0
    difficulty_counts = defaultdict(int)
    category_counts = defaultdict(int)

    for batch_file in batch_files:
        print(f"Processing {batch_file.name}...")
        batch = load_json(batch_file)

        for game in batch.get('games', []):
            game_key = game.get('game_id') or game.get('game_name')

            if game_key not in games_dict:
                # New game, add it
                games_dict[game_key] = {
                    'game_id': game.get('game_id', ''),
                    'game_name': game.get('game_name', ''),
                    'language': game.get('language', 'it'),
                    'test_cases': []
                }

            # Add test cases (skip duplicates)
            for test_case in game.get('test_cases', []):
                test_id = test_case.get('id')
                if test_id and test_id in seen_ids:
                    print(f"  Skipping duplicate: {test_id}")
                    continue

                games_dict[game_key]['test_cases'].append(test_case)
                if test_id:
                    seen_ids.add(test_id)
                total_test_cases += 1

                # Update statistics
                difficulty = test_case.get('difficulty', 'unknown')
                category = test_case.get('category', 'unknown')
                difficulty_counts[difficulty] += 1
                category_counts[category] += 1

    # Convert back to list and sort by game_name
    merged_games = sorted(games_dict.values(), key=lambda g: g.get('game_name', ''))

    # Update golden dataset
    golden['games'] = merged_games
    golden['metadata']['total_test_cases'] = total_test_cases
    golden['metadata']['games_count'] = len(merged_games)
    golden['metadata']['description'] = f"Golden dataset for RAG quality testing - {total_test_cases} Q&A pairs across {len(merged_games)} board games (merged from batches)"

    # Update statistics
    golden['statistics']['difficulty_distribution'] = dict(difficulty_counts)
    golden['statistics']['category_distribution'] = dict(category_counts)

    # Save merged dataset
    output_file = data_dir / 'golden_dataset.json'
    save_json(output_file, golden)

    print(f"\nMerge complete!")
    print(f"   Total games: {len(merged_games)}")
    print(f"   Total test cases: {total_test_cases}")
    print(f"   Difficulty distribution: {dict(difficulty_counts)}")
    print(f"   Category distribution: {dict(category_counts)}")
    print(f"   Output: {output_file}")

if __name__ == '__main__':
    merge_datasets()

#!/bin/bash

# Docker Compose Resource Monitoring Script
# Purpose: Monitor resource usage of all containers and help identify resource limit issues
# Usage: ./docker-resource-monitor.sh [options]
#
# Options:
#   --baseline              Run once and save baseline.txt (peak usage)
#   --watch [seconds]       Watch resource usage in real-time (default: 2s)
#   --export FILE           Export stats to CSV file
#   --analyze FILE          Analyze saved baseline.txt
#   --threshold PERCENT     Alert if container exceeds threshold (default: 85)
#   --help                  Show this help

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
BASELINE_FILE="${PROJECT_ROOT}/infra/resource-baseline.txt"
WATCH_INTERVAL=2
MEMORY_THRESHOLD=85

# Color codes
RED='\033[0;31m'
YELLOW='\033[1;33m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Functions

show_help() {
    echo "Docker Compose Resource Monitoring Tool"
    echo ""
    echo "Usage: $0 [options]"
    echo ""
    echo "Options:"
    echo "  --baseline              Capture baseline (one-time snapshot)"
    echo "  --watch [seconds]       Watch in real-time (default: 2 seconds)"
    echo "  --export FILE           Export to CSV file"
    echo "  --analyze FILE          Analyze saved snapshot"
    echo "  --threshold PERCENT     Alert threshold (default: 85%)"
    echo "  --help                  Show this help"
    echo ""
    echo "Examples:"
    echo "  # Capture baseline for later comparison"
    echo "  $0 --baseline"
    echo ""
    echo "  # Watch in real-time (updates every 2 seconds)"
    echo "  $0 --watch"
    echo ""
    echo "  # Watch with 5-second intervals"
    echo "  $0 --watch 5"
    echo ""
    echo "  # Export stats to CSV"
    echo "  $0 --export stats.csv"
    echo ""
    echo "  # Analyze previously saved baseline"
    echo "  $0 --analyze resource-baseline.txt"
    echo ""
}

# Get resource stats in JSON format for parsing
get_stats_json() {
    docker stats --all --no-stream --format="json" 2>/dev/null || echo "[]"
}

# Parse memory percentage from stats
parse_memory_percent() {
    echo "$1" | sed 's/%.*$//' | grep -o '[0-9.]*$'
}

# Format memory value
format_memory() {
    local value=$1
    if (( $(echo "$value > 1024" | bc -l) )); then
        echo "${value} MB" | awk '{printf "%.2f GB", $1/1024}'
    else
        echo "${value} MB"
    fi
}

# Parse CPU percentage
parse_cpu_percent() {
    echo "$1" | sed 's/%$//' | sed 's/.*[^0-9.]//'
}

# Display formatted stats table
display_stats_table() {
    echo -e "${BLUE}╔════════════════════════════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BLUE}║ Container            │ Memory %-Memory/Limit  │ CPU%  │ Net I/O  │ Status        ║${NC}"
    echo -e "${BLUE}╠════════════════════════════════════════════════════════════════════════════════════╣${NC}"

    docker stats --all --no-stream --format "table {{.Container}}\t{{.MemPerc}}\t{{.MemUsage}}\t{{.CPUPerc}}\t{{.NetIO}}\t{{.Status}}" 2>/dev/null | tail -n +2 | while read -r line; do
        container=$(echo "$line" | awk '{print $1}')
        mem_percent=$(echo "$line" | awk '{print $2}' | sed 's/%$//')
        mem_usage=$(echo "$line" | awk '{print $3}')
        cpu_percent=$(echo "$line" | awk '{print $4}' | sed 's/%$//')
        net_io=$(echo "$line" | awk '{print $5}')
        status=$(echo "$line" | awk '{print $6}')

        # Color code high usage
        if (( $(echo "$mem_percent > $MEMORY_THRESHOLD" | bc -l) )); then
            mem_color="${RED}"
        elif (( $(echo "$mem_percent > 70" | bc -l) )); then
            mem_color="${YELLOW}"
        else
            mem_color="${GREEN}"
        fi

        # Truncate container name
        container_short=$(echo "$container" | cut -c1-20)

        printf "%-20s │ %s%-6s${NC}%-16s │ %-5s │ %-8s │ %-13s │\n" \
            "$container_short" "$mem_color" "${mem_percent}%" "$mem_usage" "$cpu_percent" "$net_io" "$status"
    done

    echo -e "${BLUE}╚════════════════════════════════════════════════════════════════════════════════════╝${NC}"
}

# Capture baseline
capture_baseline() {
    echo -e "${BLUE}📊 Capturing resource baseline...${NC}"
    echo "Timestamp: $(date)" > "$BASELINE_FILE"
    echo "" >> "$BASELINE_FILE"
    docker stats --all --no-stream >> "$BASELINE_FILE" 2>/dev/null
    echo "" >> "$BASELINE_FILE"
    echo "Saved to: $BASELINE_FILE"
    echo -e "${GREEN}✓ Baseline captured${NC}"
}

# Watch resources in real-time
watch_resources() {
    local interval=$1
    echo -e "${BLUE}📊 Watching resource usage (interval: ${interval}s)...${NC}"
    echo "Press Ctrl+C to stop"
    echo ""

    while true; do
        clear
        echo -e "${BLUE}Resource Usage Monitor - $(date '+%Y-%m-%d %H:%M:%S')${NC}"
        echo ""

        display_stats_table

        echo ""
        echo -e "${YELLOW}Note: Memory usage includes OS page cache. Watch % increase over time for leaks.${NC}"
        echo "Threshold for alerts: ${MEMORY_THRESHOLD}%"

        sleep "$interval"
    done
}

# Export to CSV
export_to_csv() {
    local output_file=$1
    echo -e "${BLUE}📊 Exporting stats to CSV: $output_file${NC}"

    echo "Timestamp,Container,CPUUsage%,MemoryUsage,MemoryUsage%,MemoryLimit,NetInput,NetOutput" > "$output_file"

    docker stats --all --no-stream --format "{{.Container}},{{.CPUPerc}},{{.MemUsage}},{{.MemPerc}}" 2>/dev/null | while read -r line; do
        echo "$(date '+%Y-%m-%d %H:%M:%S'),$line" >> "$output_file"
    done

    echo -e "${GREEN}✓ Exported to $output_file${NC}"
}

# Analyze baseline
analyze_baseline() {
    local file=$1

    if [ ! -f "$file" ]; then
        echo -e "${RED}✗ File not found: $file${NC}"
        return 1
    fi

    echo -e "${BLUE}📊 Analyzing: $file${NC}"
    echo ""

    # Extract stats section
    tail -n +5 "$file" | grep -E '^[a-zA-Z0-9]' | while read -r line; do
        container=$(echo "$line" | awk '{print $1}')
        mem_percent=$(echo "$line" | awk '{print $2}' | sed 's/%$//')
        cpu_percent=$(echo "$line" | awk '{print $3}' | sed 's/%$//')

        echo "$container: Memory=$mem_percent%, CPU=$cpu_percent%"

        # Check for concerns
        if (( $(echo "$mem_percent > 80" | bc -l) )); then
            echo -e "  ${RED}⚠️  Memory usage high: ${mem_percent}%${NC}"
        fi

        if (( $(echo "$mem_percent > 90" | bc -l) )); then
            echo -e "  ${RED}🚨 CRITICAL: Memory usage very high: ${mem_percent}%${NC}"
        fi
    done
}

# Main script logic

if [ $# -eq 0 ]; then
    show_help
    exit 0
fi

while [[ $# -gt 0 ]]; do
    case $1 in
        --baseline)
            capture_baseline
            exit 0
            ;;
        --watch)
            if [[ $2 =~ ^[0-9]+$ ]]; then
                WATCH_INTERVAL=$2
                shift
            fi
            watch_resources "$WATCH_INTERVAL"
            exit 0
            ;;
        --export)
            if [ -z "$2" ]; then
                echo -e "${RED}✗ --export requires a filename${NC}"
                exit 1
            fi
            export_to_csv "$2"
            exit 0
            ;;
        --analyze)
            if [ -z "$2" ]; then
                echo -e "${RED}✗ --analyze requires a filename${NC}"
                exit 1
            fi
            analyze_baseline "$2"
            exit 0
            ;;
        --threshold)
            if [ -z "$2" ]; then
                echo -e "${RED}✗ --threshold requires a number${NC}"
                exit 1
            fi
            MEMORY_THRESHOLD=$2
            shift
            ;;
        --help)
            show_help
            exit 0
            ;;
        *)
            echo -e "${RED}✗ Unknown option: $1${NC}"
            show_help
            exit 1
            ;;
    esac
    shift
done

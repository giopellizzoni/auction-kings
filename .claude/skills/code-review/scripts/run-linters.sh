#!/bin/bash
#
# AuctionKings Code Linting and Formatting Script
# Runs dotnet format and code analyzers on target files
#
# Usage: ./run-linters.sh [file-or-directory]
#
# If no argument provided, lints and formats the entire project.
# Outputs results in compact format for easy parsing.

set -e

TARGET="${1:-.}"
EXIT_CODE=0

echo "=== AuctionKings Linter Report ==="
echo "Target: $TARGET"
echo "Date: $(date '+%Y-%m-%d %H:%M:%S')"
echo ""

# Check if dotnet CLI is available
if ! command -v dotnet &> /dev/null; then
    echo "Error: dotnet CLI not found. Please install .NET SDK."
    exit 1
fi

# Run dotnet format on C# files
echo "--- C# Code Formatting (dotnet format) ---"
if [ -f "$TARGET" ]; then
    # Single file - check if it's C#
    if [[ "$TARGET" =~ \.cs$ ]]; then
        dotnet format "$TARGET" --verify-no-changes --verbosity diagnostic 2>/dev/null || EXIT_CODE=1
    else
        echo "Skipped: Not a C# file"
    fi
else
    # Directory or project - format all C# files
    dotnet format "$TARGET" --verify-no-changes --verbosity diagnostic 2>/dev/null || EXIT_CODE=1
fi

echo ""

# Run code analysis
echo "--- Code Analysis (dotnet analyzers) ---"
if [ -f "$TARGET" ]; then
    echo "Skipped: Code analysis runs on projects, not individual files"
else
    # Find and analyze all C# projects
    while read -r project; do
        echo "Analyzing: $project"
        dotnet build "$project" /p:EnforceCodeStyleInBuild=true /p:EnableNETAnalyzers=true -v minimal 2>/dev/null || EXIT_CODE=1
    done < <(find "$TARGET" -name "*.csproj" -type f)
fi

echo ""
echo "=== Linting Complete ==="

if [ $EXIT_CODE -eq 0 ]; then
    echo "✓ No issues found"
else
    echo "✗ Issues detected - see above for details"
fi

exit $EXIT_CODE

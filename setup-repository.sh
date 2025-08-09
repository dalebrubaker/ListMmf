#!/bin/bash

echo "Setting up ListMmf as a standalone repository..."

# Initialize git repository
git init

# Add all files
git add .

# Create initial commit
git commit -m "Initial commit: Extract ListMmf from BruTrader22

- High-performance memory-mapped file implementation of IList<T>
- Full test suite and benchmarks
- GitHub Actions CI/CD pipeline
- Ready for NuGet publishing"

echo ""
echo "Repository initialized! Next steps:"
echo ""
echo "1. Create a new repository on GitHub: https://github.com/new"
echo "   - Name: ListMmf"
echo "   - Description: High-performance memory-mapped file implementation of .NET's IList<T>"
echo "   - Public repository"
echo ""
echo "2. Add the remote and push:"
echo "   git remote add origin https://github.com/dalebrubaker/ListMmf.git"
echo "   git branch -M main"
echo "   git push -u origin main"
echo ""
echo "3. Configure GitHub repository settings:"
echo "   - Add NUGET_API_KEY secret for automated publishing"
echo "   - Enable branch protection for main branch"
echo "   - Add topics: dotnet, memory-mapped-files, ipc, performance"
echo ""
echo "4. To publish a release:"
echo "   git tag v1.0.0"
echo "   git push origin v1.0.0"
echo "   (This will trigger the CI/CD pipeline to publish to NuGet)"
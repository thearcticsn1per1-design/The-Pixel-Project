# Contributing to The Pixel Project

Thank you for your interest in contributing to The Pixel Project! This document provides guidelines and information for contributors.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [How to Contribute](#how-to-contribute)
  - [Reporting Bugs](#reporting-bugs)
  - [Suggesting Features](#suggesting-features)
  - [Submitting Changes](#submitting-changes)
- [Development Guidelines](#development-guidelines)
  - [Coding Standards](#coding-standards)
  - [Unity Best Practices](#unity-best-practices)
  - [Asset Guidelines](#asset-guidelines)
- [Pull Request Process](#pull-request-process)

## Code of Conduct

Please be respectful and constructive in all interactions. We are committed to providing a welcoming and inclusive environment for all contributors.

## Getting Started

1. Fork the repository
2. Clone your fork locally
3. Set up the development environment as described in the [README](README.md)
4. Create a new branch for your changes

## How to Contribute

### Reporting Bugs

When reporting bugs, please include:

- A clear, descriptive title
- Steps to reproduce the issue
- Expected behavior vs actual behavior
- Unity version and platform
- Screenshots or video if applicable
- Console error messages if any

### Suggesting Features

Feature suggestions are welcome! Please provide:

- A clear description of the feature
- The problem it solves or value it adds
- Any mockups or examples if applicable
- Consideration of how it fits with the existing game

### Submitting Changes

1. Create a feature branch from `main`:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. Make your changes following our development guidelines

3. Test your changes thoroughly

4. Commit with clear, descriptive messages:
   ```bash
   git commit -m "Add player dash ability with cooldown"
   ```

5. Push to your fork and submit a pull request

## Development Guidelines

### Coding Standards

- Follow C# naming conventions
  - PascalCase for public members and methods
  - camelCase for private fields (with underscore prefix: `_privateField`)
  - ALL_CAPS for constants
- Use meaningful, descriptive names
- Keep methods focused and concise
- Add XML documentation comments for public APIs
- Avoid magic numbers; use named constants

Example:
```csharp
public class PlayerController : MonoBehaviour
{
    private const float DEFAULT_MOVE_SPEED = 5f;

    [SerializeField]
    private float _moveSpeed = DEFAULT_MOVE_SPEED;

    /// <summary>
    /// Moves the player in the specified direction.
    /// </summary>
    /// <param name="direction">Normalized movement direction.</param>
    public void Move(Vector2 direction)
    {
        transform.Translate(direction * _moveSpeed * Time.deltaTime);
    }
}
```

### Unity Best Practices

- Use `[SerializeField]` instead of public fields for Inspector access
- Prefer composition over inheritance
- Cache component references in `Awake()` or `Start()`
- Use object pooling for frequently spawned objects
- Keep Update() methods lightweight
- Use ScriptableObjects for shared data
- Organize scripts into appropriate namespaces

### Asset Guidelines

**Sprites:**
- Use consistent pixel density (e.g., 16x16, 32x32)
- Export as PNG with transparency
- Follow the established color palette
- Name files descriptively: `player_idle_01.png`

**Audio:**
- Use OGG format for music
- Use WAV format for short sound effects
- Keep file sizes reasonable
- Normalize audio levels

**Scenes:**
- Keep scenes organized with empty GameObjects as folders
- Use prefabs for reusable elements
- Document scene-specific setup in comments

## Pull Request Process

1. Ensure your code follows the development guidelines
2. Update documentation if needed
3. Test on all target platforms if possible
4. Fill out the pull request template completely
5. Request review from maintainers
6. Address any feedback promptly
7. Squash commits if requested before merge

### PR Checklist

- [ ] Code follows project style guidelines
- [ ] Changes are tested and working
- [ ] No new warnings or errors in Console
- [ ] Documentation updated if needed
- [ ] Commit messages are clear and descriptive

---

Thank you for contributing to The Pixel Project!

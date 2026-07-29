# AI Deck Designer & Construction Planner (.NET Edition)

# Overview

The goal is to build a professional desktop application for designing wooden decks and automatically generating construction geometry, framing, material reports, optimized cut lists, construction drawings, and eventually 3D visualization.

The application should resemble lightweight CAD software but be specialized entirely for timber deck construction.

This is **not** a generic drawing application.

The software should automatically generate construction details according to configurable building rules.

---

# Technology Stack

Language

- C# 13

Framework

- .NET 10

UI

Preferred:

- Avalonia UI

Alternative:

- WPF

Geometry

- NetTopologySuite

Rendering

- SkiaSharp

Dependency Injection

- Microsoft.Extensions.DependencyInjection

Serialization

- System.Text.Json

Logging

- Microsoft.Extensions.Logging

Testing

- xUnit

---

# Architecture

The application follows Clean Architecture.

```

Presentation (UI)
↓

Application

↓

Domain

↓

Infrastructure

```

No UI logic may exist inside the Domain layer.

---

# Solution Structure

```

DeckDesigner.sln

src/

DeckDesigner.UI/

DeckDesigner.Application/

DeckDesigner.Domain/

DeckDesigner.Infrastructure/

DeckDesigner.Rendering/

DeckDesigner.Export/

tests/

DeckDesigner.Tests/

```

---

# Layer Responsibilities

## Domain

Contains only business logic.

No UI.

No rendering.

No persistence.

Contains:

```

Deck

Board

Joist

Beam

Post

Shapes

Material calculation

Geometry generation

Optimization

```

---

## Application

Contains services.

Examples:

```

GeometryService

MaterialReportService

CutOptimizer

JointAnalyzer

SelectionService

UndoRedoService

```

Coordinates workflows.

---

## Infrastructure

Contains:

```

Persistence

File IO

JSON

DXF

PDF

Future IFC

```

---

## Rendering

Contains renderers only.

Examples:

```

BoardRenderer

JoistRenderer

BeamRenderer

GridRenderer

SelectionRenderer

PreviewRenderer

```

Renderers never generate geometry.

---

## UI

Contains:

```

Views

ViewModels

Commands

Toolbars

Panels

Canvas

```

Uses MVVM.

---

# Fundamental Rule

Geometry generation never occurs inside rendering.

Correct:

```

User edits deck

↓

GeometryService.Rebuild(deck)

↓

Boards

Joists

Beams

Posts

↓

UI redraw

```

Incorrect:

```

Paint()

↓

Generate boards

↓

Draw boards

```

Rendering must be passive.

---

# Core Domain

## Deck

Deck is the central model.

```

Deck

├── Shape

├── Boards

├── Joists

├── Beams

├── Posts

├── Settings

└── Metadata

```

The Deck owns all generated geometry.

Deck exposes:

```csharp
void RebuildGeometry();
```

Nothing else generates geometry.

---

# Shapes

Supported:

- Rectangle
- Triangle
- Polygon

Future:

- Rounded corners
- Holes
- Boolean operations

Interface:

```csharp
public interface IShape
{
    double Area { get; }

    IReadOnlyList<Point2D> Points { get; }

    void Move(Vector2D offset);

    void Rotate(double angle);

    BoundingBox GetBounds();
}
```

---

# Geometry Service

Single entry point.

```text

Deck

↓

GeometryService

↓

BoardGenerator

↓

JoistGenerator

↓

BeamGenerator

↓

PostGenerator

```

Every geometry rebuild starts here.

---

# Board Generator

Converts polygons into decking boards.

Input

```

Polygon

Board direction

Gap

Board width

```

Output

```

Board[]

```

Boards contain:

```csharp
Start

End

Length

NeedsJoint
```

---

# Joist Generator

Creates framing members.

Uses configurable spacing.

Produces:

```csharp
Joist[]
```

Each joist stores:

- Start
- End
- Width
- Height
- Length

---

# Beam Generator

Future.

Creates supporting beams.

Considers:

- spans
- overhang
- engineering rules

---

# Post Generator

Future.

Creates foundation posts.

Considers:

- beam layout
- spacing
- edge distance

---

# Rendering

Each object has its own renderer.

```

BoardRenderer

JoistRenderer

BeamRenderer

PostRenderer

GridRenderer

SelectionRenderer

```

Canvas simply calls:

```csharp
renderer.Render(context);
```

Nothing else.

---

# Coordinate System

Everything inside Domain uses:

Millimeters.

Rendering converts

World

↓

Screen

using a Camera.

---

# Camera

Supports:

- Zoom
- Pan
- Fit
- WorldToScreen()
- ScreenToWorld()

---

# Snapping

Supports:

- Grid
- Endpoints
- Midpoints
- Intersections
- Polygon corners

Future:

- Smart guides

---

# Selection

Supports:

- Single
- Multi
- Drag
- Rotate
- Delete

Future:

- Mirror
- Scale
- Copy

---

# Material Report

Generated entirely from Domain objects.

Contains:

Area

Boards

Running meters

Joists

Beams

Posts

Stock boards

Waste

Screws

Cut list

Never calculated from rendering.

---

# Cut Optimization

Input

Board lengths.

Stock lengths:

3000

3600

4200

4800

5400

Uses:

Best Fit Decreasing.

Outputs:

Stock boards

Cuts

Waste

Remaining

---

# Event Flow

```

User action

↓

Deck modified

↓

GeometryService.Rebuild()

↓

MaterialReportService

↓

Notify ViewModels

↓

Canvas redraw

```

No polling.

Everything reacts to events.

---

# MVVM

Views

↓

ViewModels

↓

Application Services

↓

Domain

Commands:

```

DrawRectangle

DrawTriangle

MoveSelection

RotateSelection

DeleteSelection

```

---

# UI Layout

```

+-------------------------------------------------------+

Toolbar

--------------------------------------------------------

Canvas

--------------------------------------------------------

Material Panel

Properties Panel

Status Bar

+-------------------------------------------------------+

```

---

# Live Preview

While drawing:

Display:

Width

Height

Area

Updates continuously.

---

# Properties Panel

Changing any setting immediately calls:

```csharp
GeometryService.Rebuild(deck);
```

---

# Material Panel

Automatically refreshes whenever geometry changes.

Shows:

Area

Boards

Running meters

Joists

Beams

Posts

Waste

Screws

Cut list

---

# Persistence

Projects saved as JSON.

```

DeckProject

↓

Deck[]

↓

Shapes

↓

Settings

↓

Metadata

```

---

# Export

Future:

PDF

DXF

IFC

OBJ

STL

Construction drawings

---

# Performance

Support:

Thousands of boards.

Hundreds of joists.

Large decks.

Geometry cached.

Incremental rebuilds.

Minimal allocations.

---

# Code Style

Use:

- Records where appropriate
- Immutable value objects
- Dependency Injection
- SOLID principles
- Composition over inheritance
- Small focused classes
- Async only where beneficial
- Strong typing

Avoid:

- Static global state
- UI logic in Domain
- Geometry generation during rendering
- Large God classes

---

# Future Features

- Structural engineering calculations
- Multi-level decks
- Stairs
- Railings
- Pergolas
- Roofs
- Terrain modelling
- Cost estimation
- BIM
- IFC export
- 3D visualization
- CNC optimization
- AI-assisted deck design

---

# Design Philosophy

This application should function as a specialized CAD/BIM system for wooden deck construction.

Users draw only the deck geometry.

Everything else—boards, framing, beams, posts, materials, cut lists, engineering rules, and exports—is automatically generated from the model.

The architecture must remain modular, scalable, testable, and maintainable throughout the project's lifetime.
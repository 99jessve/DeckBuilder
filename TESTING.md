# Deck designer: editing and validation

## Run the application

From the repository root with the .NET 10 SDK installed, run `dotnet run --project src/DeckDesigner.UI/DeckDesigner.UI.csproj`, or start DeckDesigner.UI in Visual Studio.

1. Draw a rectangle or a three-click right triangle. Use **Select** to edit it.
2. Select a shape and drag its square corner handles. Rectangle sides stay perpendicular; triangles created with the triangle tool retain their right angle. Existing unconstrained polygons remain free polygons. Coordinates snap to a 10 mm grid, with a 100 mm minimum edge length.
3. Drag inside a shape to move it. Edge snapping requires parallel edges sharing at least 100 mm; corner-only contact does not snap or join the shapes.
4. A span longer than available stock produces evenly distributed, full-width **breaker boards**, rather than alternating-row joint overlays. With one divider it is centered on the interior span. Triangles use a continuous clipped breaker across the interior too.
5. Drag a breaker back and forth along the plank direction. Both sides receive bearing support, reusing nearby framing where coverage permits. Boards and framing rebuild together.
6. Click either side of a breaker, then use **Selected section > Section runs horizontally**. Unchecked means vertical. Other sections retain their directions, and framing follows the selected section's direction.
7. Use **Reset to automatic layout** to discard manual divider positions and section directions for the selected connected deck.

Edits that self-intersect, cross dividers, move a divider outside the border, or exceed stock length are rejected with a status message; the last valid design and material report remain unchanged. Changing a section direction can therefore be rejected if the newly oriented boards would be too long. The UI does not yet provide arbitrary additional divider insertion.

Customized connected decks retain edits when their source shapes resize without changing group membership. Joining or separating a customized group requires an explicit reset first; edits are never silently discarded. Dragging a single customized shape moves its divider positions with it.

## Automated tests (no window required)

- Full solution build: `dotnet build DeckDesigner.slnx`
- Full suite: `dotnet test tests/DeckDesigner.Tests/DeckDesigner.Tests.csproj`
- New layout/edit workflows: `dotnet test tests/DeckDesigner.Tests/DeckDesigner.Tests.csproj --filter "FullyQualifiedName~DeckLayoutTests|FullyQualifiedName~DeckEditingTests|FullyQualifiedName~DeckInteractionTests"`
- Machine-readable results: `dotnet test tests/DeckDesigner.Tests/DeckDesigner.Tests.csproj --logger "trx;LogFileName=deck-designer.trx" --results-directory artifacts/test-results`

The same tests run in Visual Studio Test Explorer. No browser, desktop session, application startup or screenshot comparison is needed for these tests.

### Where to add regressions

- `DesignFixtures.cs`: deterministic rectangle, triangle, concave-outline and edge-contact fixtures.
- `DeckLayoutTests.cs`: physical board geometry, triangle-wide breakers, stock limits, support placement and invalid rebuilds.
- `DeckEditingTests.cs`: constrained corners, divider dragging/order, hit testing, translation, independent section direction and persistence round trips.
- `DeckInteractionTests.cs`: window-free view-model workflows, merged design retention, report updates and transactional rejection.
- `EdgeContactTests.cs` / `SnapServiceTests.cs`: 0/99/100 mm contact thresholds and angled edge snapping.
- `EdgeBoardGeneratorTests.cs` / `DeckCompositionServiceTests.cs`: earlier concave and thin-neck rendering regressions.

`DeckEditingService` has no UI dependency and returns new validated designs. A rejected edit leaves its input untouched. `Deck.RebuildGeometry` publishes generated geometry only after successful generation. `ComposeDesigns` retains authored layouts using stable source-membership keys. These are reusable boundaries for a future web front end; a web migration is not necessary to test them now.

The old `ButtJointGenerator` tests cover the legacy helper, not the active layout. Active decks use `DeckLayoutGenerator` and render physical breaker boards instead of `ButtJoints` overlays.

## Manual visual smoke check

Automated tests cover geometry and commands, not actual mouse events or pixel output. Before release:

- Draw a 9000 x 3000 mm rectangle, switch to Select and resize each corner.
- Move its centered breaker toward each end; confirm overlong-board edits stop with a status message.
- Select one side, change direction, then change joist spacing; confirm the selected direction and moved divider persist.
- Repeat on a large triangle; the breaker should continue across its full clipped span, with no alternate-row gaps.
- Place two shapes with only corner contact, then 99 mm and 100 mm edge contact. Only the last should snap/join.
- Verify selecting, moving, zooming and panning at different zoom levels, including releasing pointer capture outside the canvas.

## Construction and persistence limits

This is a layout/planning aid, not a building-code certification. Bearing geometry is simplified; beam and post generators are still placeholders. Span/load checks, timber grades, local rules, fasteners, foundations and complete structural detailing require further implementation and professional review. The existing screw report remains an approximation.

The JSON repository persists authored divider/section settings and regenerates geometry on load. This work does not add a save/open UI or persistence of the entire interactive source-shape/composition workspace. Serializing composed decks preserves their layouts but flattens the original source-shape grouping.

Avalonia remains the presentation layer. A future web transition, browser interaction tests, screenshot regressions and full workspace save/open are separate follow-up work.

# com.reboot.mazegrid — Package Manual

A reusable Unity package providing a 2D grid puzzle system with A* pathfinding, spawners, separators, hidden cell reveal, and a full level editor module.

**Version:** 0.1.0
**Unity:** 2021.3+
**Namespace (runtime):** `MazeGrid`
**Namespace (editor):** `MazeGrid.Editor`
**Dependencies:** `com.reboot.objecttype` (for editor type color/name lookups)

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Package Structure](#package-structure)
3. [Runtime API](#runtime-api)
   - [Data Types](#data-types)
   - [Grid2D<T>](#grid2dt)
   - [GridPathfinder](#gridpathfinder)
   - [IMazeItem](#imazeitem)
   - [MazeGrid (MonoBehaviour)](#mazegrid-monobehaviour)
   - [MazeSpawner](#mazespawner)
   - [MazeSeparatorCreator](#mazeseparatorcreator)
4. [Editor API](#editor-api)
   - [MazeGridEditorModule](#mazegrideditormodule)
   - [TypeAllocation](#typeallocation)
   - [Public Static Helpers](#public-static-helpers)
5. [Integration Guide](#integration-guide)
   - [Step 1: Implement IMazeItem](#step-1-implement-imazeitem)
   - [Step 2: Subclass MazeGrid](#step-2-subclass-mazegrid)
   - [Step 3: Subclass MazeSpawner (optional)](#step-3-subclass-mazespawner-optional)
   - [Step 4: Scene Setup](#step-4-scene-setup)
   - [Step 5: Level Editor Integration](#step-5-level-editor-integration)
6. [Data Flow](#data-flow)
7. [Grid Coordinate System](#grid-coordinate-system)
8. [Separator System](#separator-system)
9. [Hidden Item Reveal System](#hidden-item-reveal-system)
10. [Complete Example: HarvestBus](#complete-example-harvestbus)

---

## Architecture Overview

The package separates **generic grid logic** (pathfinding, spawning, cell management) from **game-specific behavior** (what items look like, how they animate, what types mean).

```
┌─────────────────────────────────────────────────┐
│                 Your Game Code                   │
│                                                  │
│  MyGameGrid : MazeGrid     MyItem : IMazeItem   │
│  MySpawner : MazeSpawner                        │
│  MyLevelEditor → MazeGridEditorModule           │
└────────────────────┬────────────────────────────┘
                     │ overrides / uses
┌────────────────────▼────────────────────────────┐
│              com.reboot.mazegrid                 │
│                                                  │
│  Runtime: MazeGrid, MazeSpawner, Grid2D<T>,     │
│           GridPathfinder, MazeSeparatorCreator,  │
│           IMazeItem, MazeGridConfig, MazeCellData│
│                                                  │
│  Editor:  MazeGridEditorModule, TypeAllocation   │
└─────────────────────────────────────────────────┘
```

**Key design pattern:** MazeGrid has 4 virtual factory methods. Your game subclass overrides these to spawn game-specific prefabs. Everything else (pathfinding, separator blocking, hidden reveal, spawner queue management, active item detection) works automatically.

---

## Package Structure

```
Packages/com.reboot.mazegrid/
├── package.json
├── MANUAL.md                          ← this file
├── Scripts/
│   ├── Reboot.MazeGrid.asmdef        ← runtime assembly (zero dependencies)
│   ├── Data/
│   │   ├── Grid2D.cs                  ← generic 2D grid with world-space conversion
│   │   ├── MazeGridEnums.cs           ← GridCellState, SpawnerDirection enums
│   │   ├── MazeCellData.cs            ← serializable cell data (state, typeId, hidden, spawner queue)
│   │   └── MazeGridConfig.cs          ← serializable grid config (rows, cols, cells, separators)
│   ├── Core/
│   │   ├── IMazeItem.cs               ← interface for grid-occupying items
│   │   ├── MazeGrid.cs                ← main MonoBehaviour: grid management + pathfinding
│   │   ├── MazeSpawner.cs             ← spawner MonoBehaviour with queue + virtual hooks
│   │   ├── MazeSeparatorCreator.cs    ← visual separator spawner
│   │   └── GridPathfinder.cs          ← A* pathfinding (static, 4-directional)
│   └── Editor/
│       ├── Reboot.MazeGrid.Editor.asmdef  ← editor assembly (refs: MazeGrid, ObjectType)
│       ├── TypeAllocation.cs              ← struct: typeIndex + instanceCount
│       ├── MazeGridEditorModule.cs        ← public API, state, sub-stage coordination
│       ├── MazeGridEditorModule.GridBuilding.cs   ← cell state painting, resize, spawner queues
│       ├── MazeGridEditorModule.Separator.cs      ← separator toggle UI
│       ├── MazeGridEditorModule.TypePainting.cs   ← type drag-drop, random distribute, validation
│       ├── MazeGridEditorModule.Visualization.cs  ← shared grid rendering, color helpers
│       └── MazeGridEditorModule.DragDrop.cs       ← drag state machine, ghost, trash area
```

---

## Runtime API

### Data Types

#### `GridCellState` (enum)
```csharp
namespace MazeGrid
{
    public enum GridCellState { Empty, Full, Spawner, GridWall }
}
```

#### `SpawnerDirection` (enum)
```csharp
namespace MazeGrid
{
    public enum SpawnerDirection { Up, Down, Left, Right }
}
```

#### `MazeCellData` (serializable class)
Represents one cell in the grid config. Stored in `MazeGridConfig.cells[]` as a flat row-major array.

| Field | Type | Description |
|-------|------|-------------|
| `state` | `GridCellState` | Cell type: Empty, Full, Spawner, or GridWall |
| `itemTypeId` | `int` | Integer type identifier (-1 = unassigned). Game interprets this as an index into ObjectTypeLibrary or any type system |
| `isHidden` | `bool` | If true, item starts hidden and reveals when adjacent cell empties |
| `direction` | `SpawnerDirection` | Direction the spawner pushes items (only for Spawner cells) |
| `spawnerQueue` | `List<int>` | Queue of type IDs to spawn (only for Spawner cells) |

#### `MazeGridConfig` (serializable class)
The complete grid configuration. Designed to be a field on your game's level data ScriptableObject.

| Field | Type | Description |
|-------|------|-------------|
| `rows` | `int` | Number of rows (default: 6) |
| `columns` | `int` | Number of columns (default: 4) |
| `exitRow` | `int` | Row index that serves as the exit (default: 0 = top) |
| `cells` | `MazeCellData[]` | Flat array, size = rows × columns, row-major order |
| `horizontalSeparators` | `bool[]` | Walls between rows. Size = (rows-1) × columns |
| `verticalSeparators` | `bool[]` | Walls between columns. Size = rows × (columns-1) |

**Key methods:**
- `InitializeCells()` — creates cells array and separators if null or wrong size
- `InitializeSeparators()` — creates separator arrays if null or wrong size
- `GetCellIndex(row, col)` → flat index, or -1 if out of bounds
- `GetHorizontalSeparatorIndex(row, col)` → separator index below cell (row, col)
- `GetVerticalSeparatorIndex(row, col)` → separator index right of cell (row, col)

---

### Grid2D<T>

Generic 2D grid with world-space coordinate conversion. Grid[0,0] is the top-left corner.

```csharp
var grid = new Grid2D<MyItem>(width, height, cellSizeX, cellSizeZ, origin, negativeZDirection: true);

// Access
grid[x, z] = item;
grid[new Vector2Int(x, z)] = item;

// Conversion
Vector2Int gridPos = grid.WorldToGrid(worldPosition);
Vector3 worldPos = grid.GridToWorld(gridPos);

// Query
bool valid = grid.IsValidPosition(x, z);
MyItem item = grid.GetCellAtWorldPosition(worldPos);
grid.ForEach((x, z, item) => { /* iterate all */ });
grid.Clear();
```

**Properties:** `Width`, `Height`, `CellSizeX`, `CellSizeZ`, `Origin`

---

### GridPathfinder

Static A* pathfinding for 2D grids. Supports 4-directional movement with optional separator/wall checks.

```csharp
// Basic pathfinding
List<Vector2Int> path = GridPathfinder.FindPath(start, goal, isWalkable, gridWidth, gridHeight);
bool exists = GridPathfinder.HasPath(start, goal, isWalkable, gridWidth, gridHeight);

// Path to any cell in a row (used for exit paths)
List<Vector2Int> path = GridPathfinder.FindPathToRow(start, targetRow, isWalkable, gridWidth, gridHeight);

// With separator support (additional delegate checks movement between adjacent cells)
List<Vector2Int> path = GridPathfinder.FindPath(start, goal, isWalkable, canMoveBetween, gridWidth, gridHeight);
List<Vector2Int> path = GridPathfinder.FindPathToRow(start, targetRow, isWalkable, canMoveBetween, gridWidth, gridHeight);
```

**Delegate types:**
- `Func<Vector2Int, bool> isWalkable` — returns true if position is passable
- `GridPathfinder.CanMoveBetweenDelegate canMoveBetween` — returns true if movement between two adjacent cells is allowed (for separator checks)

---

### IMazeItem

Interface that your game's grid-occupying items must implement.

```csharp
public interface IMazeItem
{
    Transform transform { get; }
    GameObject gameObject { get; }
    void OnBecameActive();        // Called when item has a valid path to exit
    void OnSpawnedBySpawner();    // Called when spawned by a MazeSpawner
    void OnRevealed();            // Called when hidden item is revealed
    bool IsHidden { get; set; }   // Whether item is currently hidden
}
```

Your MonoBehaviour just needs to add `: IMazeItem` and implement these 4 members. `transform` and `gameObject` are already provided by MonoBehaviour.

---

### MazeGrid (MonoBehaviour)

The core component. Manages a 2D grid of `IMazeItem` objects with pathfinding, spawners, separators, hidden reveal, and active detection.

#### Inspector Settings

| Field | Default | Description |
|-------|---------|-------------|
| `gridWidth` | 4 | Number of columns |
| `gridHeight` | 6 | Number of rows |
| `cellSizeX` | 1.0 | Cell width in world units |
| `cellSizeZ` | 1.0 | Cell depth in world units |
| `borderOffsetCount` | 0 | Wall border cells added on sides/bottom |
| `exitRow` | 0 | Exit row index (typically 0 = top) |
| `initializeOnAwake` | true | Set to false if `BuildFromConfig()` will be called |

#### Events

| Event | Signature | When |
|-------|-----------|------|
| `OnInitialized` | `Action` | After grid is built (Awake or BuildFromConfig) |
| `OnCellCleared` | `Action<Vector2Int>` | When an item leaves a cell |
| `OnItemRegistered` | `Action<IMazeItem>` | When a new item is added to the grid |
| `OnItemRevealed` | `Action<IMazeItem>` | When a hidden item is revealed |

#### Public Methods

```csharp
// Pathfinding queries
bool CanSendItem(IMazeItem item)              // Has valid path to exit row?
bool TrySendItem(IMazeItem item)              // Remove from grid if path exists
List<Vector2Int> FindExitPath(IMazeItem item) // Get the full path to exit

// Grid queries
IMazeItem GetItemAt(Vector2Int gridPos)
IMazeItem GetItemAtWorldPosition(Vector3 worldPos)
Vector3 GetCellWorldPosition(Vector2Int gridPos)
Vector2Int WorldToGrid(Vector3 worldPos)
bool IsCellEmpty(Vector2Int gridPos)

// Registration
void RegisterItem(IMazeItem item, Vector2Int gridPos)  // Used by spawners

// Building
void BuildFromConfig(MazeGridConfig config)  // Build grid from serialized config
virtual void ClearGrid()                     // Destroy all children and reset state
```

#### Virtual Factory Methods (override in your subclass)

```csharp
// Called during BuildFromConfig for Full cells
protected virtual IMazeItem CreateItem(MazeCellData cellData, Vector2Int gridPos, Vector3 worldPos)

// Called during BuildFromConfig for Spawner cells
protected virtual MazeSpawner CreateSpawner(MazeCellData cellData, Vector2Int gridPos, Vector3 worldPos)

// Called during BuildFromConfig for GridWall cells
protected virtual GameObject CreateWall(Vector2Int gridPos, Vector3 worldPos)

// Called by MazeSpawner when spawning a new item at runtime
public virtual IMazeItem CreateItemForSpawner(int itemTypeId, Vector2Int gridPos, Vector3 worldPos)
```

#### Protected Helper Methods (available to subclasses)

```csharp
protected static void SafeDestroy(GameObject go)          // Editor/runtime safe destroy
protected static T SpawnPrefab<T>(T prefab, ...)          // Editor/runtime safe instantiate
protected static GameObject SpawnGameObject(GameObject, ...)
```

#### Automatic Behaviors

These happen automatically — you don't need to implement them:

- **Active detection:** After every cell clear, all items are checked for valid exit paths. Items with paths receive `OnBecameActive()`.
- **Hidden reveal:** When a cell clears, adjacent hidden items are revealed if no separator blocks access and the cleared cell isn't about to be filled by a spawner.
- **Spawner triggering:** After `BuildFromConfig`, all spawners attempt their initial spawn. Thereafter, spawners auto-trigger when their target cell clears.
- **Separator-aware pathfinding:** A* respects both cell occupancy and separator walls between cells.

---

### MazeSpawner

Occupies a grid cell (impassable) and spawns items into an adjacent cell based on direction.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `GridPosition` | `Vector2Int` | The cell this spawner occupies |
| `TargetCellPosition` | `Vector2Int` | The cell items spawn into (based on direction) |
| `HasItemsToSpawn` | `bool` | Queue is not empty |
| `RemainingCount` | `int` | Items left in queue |
| `ParentGrid` | `MazeGrid` | The grid this spawner belongs to (protected) |

#### Virtual Hooks (override in your subclass)

```csharp
protected virtual void OnInitialized()                          // After Initialize() — update UI
protected virtual void OnItemCreated(IMazeItem item, Vector2Int gridPos)  // After spawn — play effects
protected virtual void OnQueueChanged()                         // After dequeue — update count display
protected virtual void OnQueueExhausted()                       // Queue empty — trigger event
```

#### Lifecycle
1. `MazeGrid.BuildFromConfig()` calls `CreateSpawner()` on your subclass
2. MazeGrid calls `spawner.Initialize(grid, position, direction, queue)`
3. `OnInitialized()` fires on your subclass
4. MazeGrid calls `spawner.TrySpawn()` for initial spawn
5. On each `OnCellCleared`, spawner checks if its target cell cleared and spawns next item
6. Spawner calls `parentGrid.CreateItemForSpawner()` (your subclass) to create the item
7. `OnItemCreated()` fires on your subclass — play effects, update UI

---

### MazeSeparatorCreator

Spawns visual separator prefabs based on MazeGrid separator data. Attach to the same GameObject as MazeGrid.

#### Inspector Fields

| Field | Description |
|-------|-------------|
| `horizontalSeparatorPrefab` | Prefab for walls between rows |
| `verticalSeparatorPrefab` | Prefab for walls between columns |
| `mazeGrid` | Reference to MazeGrid (auto-finds on same GameObject) |

Automatically subscribes to `MazeGrid.OnInitialized` and rebuilds separators when the grid is built.

---

## Editor API

### MazeGridEditorModule

A non-EditorWindow class that provides the full grid editor UI. Instantiate in your level editor and call `DrawGUI()`.

#### Constructor

```csharp
var module = new MazeGridEditorModule(
    objectTypeLibrary,           // ObjectTypeLibrary for type colors/names
    () => SetDirty(),            // Called when module modifies the config
    () => Repaint()              // Optional: called during drag for ghost rendering
);
```

#### Main API

```csharp
// Full UI with sub-stage tabs (Grid Building | Separator | Type Painting)
module.DrawGUI(gridConfig, typeAllocations);

// Individual sub-stages (for custom tab layouts)
module.DrawGridBuilding(gridConfig);
module.DrawSeparators(gridConfig);
module.DrawTypePainting(gridConfig, typeAllocations);

// Grid log panel (standalone, for use outside the module)
module.DrawGridLogPanel(gridConfig, typeAllocations, GUILayout.ExpandWidth(true));

// Update library reference after hot-reload
module.SetObjectTypeLibrary(newLibrary);
```

#### Sub-Stages

**Grid Building** — Set cell states (Empty/Full/Spawner/GridWall), resize grid (add/remove rows/columns from any edge), manage spawner queues (slot count, direction, queue slots), bulk operations (make all empty/full/etc).

**Separator Mode** — Click between cells to toggle walls. Statistics panel shows counts. Bulk clear/fill.

**Type Painting** — Drag types from the palette onto Full cells and spawner queue slots. Drag between cells to swap. Drag to trash to remove. Hidden toggle checkbox per cell. Random distribute fills all slots from the type pool. Type log shows placed vs expected counts. Grid log shows cell count vs type allocation validation.

---

### TypeAllocation

Simple struct mapping a type index to an expected count. Used by the editor module for:
- Type source palette buttons (what types are available to paint)
- Grid log validation (expected cells vs available cells)
- Random distribute (pool of types to fill)

```csharp
var allocations = new TypeAllocation[]
{
    new TypeAllocation(typeIndex: 0, instanceCount: 5),
    new TypeAllocation(typeIndex: 1, instanceCount: 3),
    new TypeAllocation(typeIndex: 2, instanceCount: 4),
};
```

If `allocations` is null, the module shows all types from `ObjectTypeLibrary` with no count tracking.

---

### Public Static Helpers

These are available on `MazeGridEditorModule` for use outside the module (e.g., in your game's bus/seat editor):

```csharp
// Cell background color by state
Color color = MazeGridEditorModule.GetCellColor(GridCellState.Full);

// Direction arrow symbol
string arrow = MazeGridEditorModule.GetDirectionSymbol(SpawnerDirection.Up); // "↑"

// Draw cell border
MazeGridEditorModule.DrawCellBorder(rect);

// Draw colored type circle (or hollow if unassigned)
MazeGridEditorModule.DrawTypeCircle(rect, typeIndex, objectTypeLibrary);

// Spawner index mapping (cell index → spawner ordinal or -1)
int[] indices = MazeGridEditorModule.CalculateSpawnerIndices(gridConfig);

// Drag ghost circle at mouse position
MazeGridEditorModule.RenderDragGhostStatic(typeIndex, objectTypeLibrary);

// Trash drop zone
MazeGridEditorModule.DrawTrashArea(isDragging, canAccept, onDrop);

// Type source button row from allocations
MazeGridEditorModule.DrawTypeSourceButtonsFromAllocations(allocations, library, onClicked, showInfinite);
```

---

## Integration Guide

### Step 1: Implement IMazeItem

Create your game's grid item MonoBehaviour:

```csharp
using MazeGrid;
using UnityEngine;

public class MyPuzzlePiece : MonoBehaviour, IMazeItem
{
    private bool isHidden;

    public bool IsHidden
    {
        get => isHidden;
        set => isHidden = value;
    }

    public void OnBecameActive()
    {
        // Item has a valid path to exit — show glow, enable tap, etc.
    }

    public void OnSpawnedBySpawner()
    {
        // Just spawned by a spawner — play spawn animation
    }

    public void OnRevealed()
    {
        // Was hidden, now revealed — play reveal effect
    }
}
```

### Step 2: Subclass MazeGrid

Override the 4 factory methods to create your game's prefabs:

```csharp
using MazeGrid;
using UnityEngine;

public class MyGameGrid : MazeGrid.MazeGrid
{
    [SerializeField] private MyPuzzlePiece itemPrefab;
    [SerializeField] private MySpawner spawnerPrefab;
    [SerializeField] private GameObject wallPrefab;

    protected override IMazeItem CreateItem(MazeCellData cellData, Vector2Int gridPos, Vector3 worldPos)
    {
        var item = SpawnPrefab(itemPrefab, worldPos, Quaternion.identity, transform);
        item.name = $"Item_{gridPos.y}_{gridPos.x}";
        // Use cellData.itemTypeId to set type/color/variant
        // Use cellData.isHidden — MazeGrid handles the hidden tracking,
        // but you may want to set visual hidden state here too
        return item;
    }

    protected override MazeSpawner CreateSpawner(MazeCellData cellData, Vector2Int gridPos, Vector3 worldPos)
    {
        var spawner = SpawnPrefab(spawnerPrefab, worldPos, Quaternion.identity, transform);
        spawner.name = $"Spawner_{gridPos.y}_{gridPos.x}";
        return spawner;
    }

    protected override GameObject CreateWall(Vector2Int gridPos, Vector3 worldPos)
    {
        var wall = SpawnGameObject(wallPrefab, worldPos, Quaternion.identity, transform);
        wall.name = $"Wall_{gridPos.y}_{gridPos.x}";
        return wall;
    }

    public override IMazeItem CreateItemForSpawner(int itemTypeId, Vector2Int gridPos, Vector3 worldPos)
    {
        var item = Instantiate(itemPrefab, worldPos, Quaternion.identity, transform);
        item.name = $"Item_Spawned_{gridPos.y}_{gridPos.x}";
        // Set type from itemTypeId
        return item;
    }
}
```

### Step 3: Subclass MazeSpawner (optional)

Override hooks for custom effects:

```csharp
using MazeGrid;
using UnityEngine;

public class MySpawner : MazeSpawner
{
    [SerializeField] private TMPro.TMP_Text countText;

    protected override void OnInitialized()
    {
        countText.text = RemainingCount.ToString();
    }

    protected override void OnItemCreated(IMazeItem item, Vector2Int gridPos)
    {
        // Play spawn animation on the item
    }

    protected override void OnQueueChanged()
    {
        countText.text = RemainingCount.ToString();
    }

    protected override void OnQueueExhausted()
    {
        // All items spawned — hide counter, play effect
    }
}
```

### Step 4: Scene Setup

1. Create a GameObject, add your `MyGameGrid` component
2. Set `initializeOnAwake = false` (since you'll call `BuildFromConfig`)
3. Assign prefab references in the inspector
4. Optionally add `MazeSeparatorCreator` on the same GameObject with separator prefabs
5. In your level builder / game manager:

```csharp
void BuildLevel(MyLevelData levelData)
{
    myGameGrid.BuildFromConfig(levelData.gridConfig);
}
```

### Step 5: Level Editor Integration

In your custom EditorWindow:

```csharp
using MazeGrid;
using MazeGrid.Editor;
using ObjectType;
using UnityEditor;

public class MyLevelEditor : EditorWindow
{
    private MazeGridEditorModule _mazeModule;
    private ObjectTypeLibrary _library;

    private void OnEnable()
    {
        _library = ObjectTypeLibrary.Find();
        _mazeModule = new MazeGridEditorModule(
            _library,
            () => EditorUtility.SetDirty(myLevelAsset),
            () => Repaint()
        );
    }

    private void OnGUI()
    {
        // Map your game's type config to TypeAllocation[]
        var allocations = myLevel.typeConfigs
            .Select(t => new TypeAllocation(t.typeIndex, t.count))
            .ToArray();

        // Draw the full maze grid editor
        _mazeModule.DrawGUI(myLevel.gridConfig, allocations);
    }
}
```

---

## Data Flow

### Build Phase (Editor → Runtime)

```
MazeGridConfig (ScriptableObject field)
        │
        ▼
MazeGrid.BuildFromConfig(config)
        │
        ├─ For each Full cell:   YourGrid.CreateItem(cellData, pos, worldPos)
        ├─ For each Spawner:     YourGrid.CreateSpawner(cellData, pos, worldPos)
        ├─ For each GridWall:    YourGrid.CreateWall(pos, worldPos)
        ├─ Registers hidden items for reveal tracking
        ├─ Fires OnInitialized
        ├─ Checks active items (OnBecameActive on items with exit paths)
        └─ Triggers initial spawner spawns
```

### Gameplay Phase (Runtime)

```
Player taps item
    │
    ▼
grid.CanSendItem(item) → A* pathfinding with separators
    │ true
    ▼
grid.TrySendItem(item) → removes from grid, fires OnCellCleared
    │
    ├─ Spawners check if their target cell cleared → TrySpawn()
    │       └─ grid.CreateItemForSpawner() → RegisterItem → OnItemRegistered
    │
    ├─ Hidden items check if adjacent cell is now accessible → OnRevealed
    │
    └─ All items re-checked for active paths → OnBecameActive
```

---

## Grid Coordinate System

- **Grid[0,0]** = top-left corner
- **X axis** = columns (increasing right)
- **Z axis** = rows (increasing downward in grid space, but -Z in world space when `negativeZDirection = true`)
- **Exit row** is typically row 0 (top)
- `BuildFromConfig` adds 1 extra row at the top as the exit row (not included in config data)
- `borderOffsetCount` adds wall columns on left/right and wall rows on bottom

### Config ↔ Runtime Mapping

```
Config cell [row, col] → Runtime grid [col + borderOffset, row + 1]
```

The +1 accounts for the exit row added at runtime.

---

## Separator System

Separators are walls between cells that block pathfinding movement.

- **Horizontal separators** block vertical movement (between rows). Array index: `row * columns + col` (separator below cell at row, col).
- **Vertical separators** block horizontal movement (between columns). Array index: `row * (columns-1) + col` (separator right of cell at row, col).

Separators are stored as flat boolean arrays on `MazeGridConfig`. The `MazeSeparatorCreator` component reads these arrays and spawns visual prefabs at the correct world positions.

Pathfinding automatically respects separators via the `CanMoveBetweenCells` delegate.

---

## Hidden Item Reveal System

Items marked `isHidden = true` in `MazeCellData` start hidden. The system reveals them when:

1. An adjacent cell becomes empty (item sent or destroyed)
2. No separator blocks access between the hidden item and the empty cell
3. The empty cell is not a spawner's target with items remaining (to prevent premature reveal)

On reveal, `IMazeItem.IsHidden` is set to false, `IMazeItem.OnRevealed()` is called, and `MazeGrid.OnItemRevealed` fires.

---

## Complete Example: HarvestBus

This package was extracted from the HarvestBus puzzle game. Here's how it integrates:

### Runtime

```csharp
// PersonGroup implements IMazeItem
public class PersonGroup : MonoBehaviour, IMazeItem { ... }

// HarvestBusMazeGrid overrides factory methods to spawn PersonGroups with ObjectType
public class HarvestBusMazeGrid : MazeGrid.MazeGrid
{
    protected override IMazeItem CreateItem(MazeCellData cellData, Vector2Int gridPos, Vector3 worldPos)
    {
        var group = SpawnPrefab(personGroupPrefab, worldPos, Quaternion.identity, transform);
        // Set ObjectType color/material from cellData.itemTypeId
        return group;
    }

    public override IMazeItem CreateItemForSpawner(int itemTypeId, Vector2Int gridPos, Vector3 worldPos)
    {
        var group = Instantiate(personGroupPrefab, worldPos, Quaternion.identity, transform);
        // Set type from itemTypeId
        return group;
    }
}

// HarvestBusSpawner adds DOTween effects
public class HarvestBusSpawner : MazeSpawner
{
    protected override void OnItemCreated(IMazeItem item, Vector2Int gridPos)
    {
        StartCoroutine(PlaySpawnAnimation(item as PersonGroup));
    }
}
```

### Level Builder

```csharp
void BuildLevel(BusPuzzleLevelData levelData)
{
    harvestBusMazeGrid.SetObjectTypeLibrary(objectTypeLibrary);
    harvestBusMazeGrid.BuildFromConfig(levelData.gridConfig);
}
```

### Level Editor

```csharp
// In HarvestBusLevelEditor
private MazeGridEditorModule _mazeModule;

protected override void LoadManagers()
{
    _mazeModule = new MazeGridEditorModule(_objectTypeLibrary, () => SetDirty(), () => Repaint());
}

private void DrawStage2(BusPuzzleLevelData busLevel)
{
    var allocations = busLevel.typeInstances
        .Select(t => new TypeAllocation(t.typeIndex, t.instanceCount))
        .ToArray();

    _mazeModule.DrawGUI(busLevel.gridConfig, allocations);
}
```

### Input Handling

```csharp
void HandleTap(PersonGroup personGroup)
{
    var grid = personGroup.GetComponentInParent<MazeGrid.MazeGrid>();
    if (grid.CanSendItem(personGroup))
    {
        grid.TrySendItem(personGroup);
        personGroup.SendGroup(); // game-specific travel logic
    }
}
```

### Subscribing to Events

```csharp
void Start()
{
    mazeGrid.OnInitialized += () => SubscribeToAllItems();
    mazeGrid.OnItemRegistered += (item) => SubscribeToItem(item);
    mazeGrid.OnCellCleared += (pos) => CheckWinCondition();
}
```

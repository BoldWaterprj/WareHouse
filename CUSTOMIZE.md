# Warehouse – how to tweak look & scoring

Everything below can be changed **without touching the code logic**.

## 1. Object colours

Open `Assets/Scripts/Level/PlaceableCatalog.cs`, method `Ensure()`.
Each line looks like:

```csharp
Add(PlaceableType.Wall, "Wall", new Color(0.35f, 0.48f, 0.88f), new Vector2(1f, 1f), true, false, false, 0);
//                            ^^^^^^^^  ^^^^^^^^^^^^^^^^^^^^^^^^^^^^
//                            name      colour (R, G, B in 0..1)
```

- **Wall** colour: change `new Color(0.35f, 0.48f, 0.88f)` (soft royal blue).
- **Column** colour: currently `Color.white`.
- **Floor** colour: currently `Color.white` (it shows the floor texture as-is).
- **Player** colour: the last `Add(...)`.
- Box / Shelf defaults are `Color.white` because they are tinted **per instance**
  by the colour code you set in the editor.

RGB → 0..1: divide the 0–255 value by 255 (`#4169E1` → `0.255, 0.412, 0.882`).

## 2. Textures

Sprites live in `Assets/Resources/Art/` and are loaded by name:

| Object | File |
|---|---|
| Floor | `Floor.jpg` (a copy of `Assets/Depozitul/Scenes/834825_preview.jpg`) |
| Wall, Column, Box, Shelf | `Block.png` (the "4 sections" cube) |
| Player | `Player.png` |

To change them:
1. Replace the PNG (keep the same file name), **or** add a new PNG and point the
   loader at it in `PlaceableCatalog` (the `static PlaceableCatalog()` method):
   ```csharp
   TryOverride(PlaceableType.Wall, "Art/Block");   // "Art/<file name without .png>"
   ```
2. In Unity, select the texture and set:
   - **Texture Type**: `Sprite (2D and UI)`
   - **Sprite Mode**: `Single`
   - **Pixels Per Unit**: `64`
   - **Filter Mode**: `Point (no filter)`
   - **Pivot**: `Center`

The object is automatically scaled so the sprite exactly fills its footprint
(`Floor` = 1×1, `Wall/Column/Box` = 1×1, `Shelf` = 2×1), so any sprite size works.

## 3. Colour-matching strictness

Open `Assets/Scripts/Level/ColorScore.cs`:

```csharp
public const float MaxDistance = 0.6f;
```

- Colours are compared with a straight RGB distance.
- If `distance >= MaxDistance` → **0 points** (no match).
- Otherwise the score multiplier is `1 - (distance / MaxDistance)²`.
- **Increase** for a more forgiving match, **decrease** for stricter.
- Maximum possible RGB distance is `sqrt(3) ≈ 1.732`.

## 4. Player name

Edit `Worker/worker.json`:

```json
{ "name": "Your Name" }
```

This name is written into every session file.

## 5. Folders (at the project root, next to the build)

| Folder | Purpose |
|---|---|
| `Working on projects` | levels being edited (Save) |
| `Active levels` | exported playable levels (Export) |
| `Sessions` | one report per finished game |
| `Worker` | who is playing |

## 6. Scoring (for reference)

- `boxScore = Σ round(100 × colourMultiplier)` for every box.
- `timeBonus = floor(timer − elapsed)` → `+remaining` seconds, or `−overtime` seconds.
- `score = boxScore + timeBonus`.
- The report stores per-channel R/G/B precision and `timePercent` (elapsed ÷ level time × 100).

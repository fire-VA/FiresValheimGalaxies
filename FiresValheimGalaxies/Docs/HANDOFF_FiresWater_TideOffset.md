# Handoff — add `_FiresTideOffset` to Custom/FiresWater

**For:** the conversation/owner that maintains `Assets/Shaders/Fires/FiresWater.shader`
(Unity project `NewProject2026/Fires2026Project`).
**From:** the FiresValheimGalaxies tide/current work.
**Risk:** very low. Two lines. No change to waves, normals, foam, refraction,
stencil, or either pass's fragment. Read "Why it's safe" before you doubt it.

---

## What this is

FiresValheimGalaxies adds **moon-driven tides**: the ocean surface should rise
and fall over the day/night cycle (and more at spring/blood moons). The C# side
is already done and shipping:

- It computes a single world-space vertical offset in **metres** (e.g. `-5` at a
  spring low, `+2` at a blood-moon high, `0` at the normal high-water mark = vanilla y≈30).
- It applies that offset to **physics/logic** by patching `WaterVolume.GetWaterSurface`
  (buoyancy, swimming, depth all already follow the tide).
- It publishes that same offset to the GPU **every frame** as a global float:

  ```csharp
  Shader.SetGlobalFloat("_FiresTideOffset", offset);   // metres; 0 when tides off/disabled
  ```

The **only** missing piece is visual: the rendered FiresWater sheet must shift by
the same offset so the rendered surface stays welded to the (already-shifted)
collision surface. Right now physics tides work but the rendered ocean stays at y≈30.

This is **not** a double-apply. Physics and visuals are two parallel
representations of the same surface; both must move by the identical offset to
stay welded. (Same principle as the file's existing `_NetRefPos` warning: the
visual wave must not drift from the collision wave.)

---

## The change (2 lines)

### 1. Declare the global (CGINCLUDE, with the other live `_Fires*` globals)

It is a **global**, set from C# via `SetGlobalFloat` — **do NOT add it to the
Properties block** (per the project's documented rule: Properties-block defaults
bake and can't be overridden by a global). Put it next to the other live-tuning
globals, e.g. right after `_FiresCausticUseTex;` (~line 269):

```hlsl
        // World-space vertical tide offset in metres, set every frame by the
        // Galaxies tide system (Shader.SetGlobalFloat). 0 = normal high water
        // (vanilla y≈30). Negative = low tide (surface drops, more beach shows);
        // positive = above vanilla (flood). Mirrors the C# WaterVolume.GetWaterSurface
        // patch so the rendered surface stays welded to the collision surface.
        float _FiresTideOffset;
```

### 2. Apply it in `BuildOutput` (the shared vertex builder)

In `BuildOutput(...)`, immediately **after** the existing wave displacement line
`worldPos.y += waveY;` (~line 517), add one line:

```hlsl
            float waveY = CalcWave(worldPos, waveAttn, 1.0) * _WaveAmplitude;
            worldPos.y += waveY;
            worldPos.y += _FiresTideOffset;   // <-- ADD THIS: tide raises/lowers the whole sheet
```

That's it. `BuildOutput` is called by `dsMain`, which is the `#pragma domain` for
**both** the TopSurface and UnderwaterView passes, so both inherit the shift from
this one line.

---

## Why it's safe (please verify against the source)

1. **Both passes share this code.** TopSurface and UnderwaterView both declare
   `#pragma vertex tessVert / hull hsMain / domain dsMain`, and `dsMain` returns
   `BuildOutput(...)`. One edit, both passes, guaranteed in lockstep.

2. **Wave shape/phase/normals are unaffected.** `CalcWave → CalcWaveFromWind →
   CreateWave` reads only `worldPos.z` and `worldPos.x`
   (`float2 v = -(worldPos.z*dir + worldPos.x*tan_)`). It never reads `worldPos.y`.
   So shifting `worldPos.y` does **not** change wave height, phase, or the
   finite-difference normal taps (`hL/hR/hB/hF` perturb only x/z). The sheet
   translates vertically; the waves on it are identical. This is exactly what
   keeps it welded to vanilla CPU collision wave shape.

3. **Foam is unaffected.** Foam UVs use `worldPos.xz` only.

4. **Ashlands is unaffected.** `AshlandsFactor` tests `worldPos.y` against
   `_AshlandsYOffset` (=4000). A few metres of tide never crosses that.

5. **Underwater depth tint stays correct — it's a feature, not a bug.** In
   `fragUnder`, `camDepthRaw = i.worldPos.y - _WorldSpaceCameraPos.y`. Because
   `i.worldPos.y` now includes the tide offset, the underwater tint tracks the
   true (shifted) surface — consistent with the C# `GetWaterSurface` shift that
   already decides when the player is submerged.

6. **Refraction/grab is screen-space** (`ComputeGrabScreenPos`), unaffected by a
   world-Y translate. `SampleDepth` returns a constant `1.0`, unaffected.

7. **Default-zero is inert.** Unbound globals read 0, and the C# pushes 0 whenever
   tides are off / height-apply is disabled. With offset 0 the added line is
   `+= 0` — byte-identical behaviour to today.

## Do NOT
- Do **not** add `_FiresTideOffset` to the wave functions or to `CalcWave` inputs.
- Do **not** put it in the Properties block.
- Do **not** subtract it anywhere or gate it per-pass — it must be the same value
  on both passes (the shared `BuildOutput` guarantees that).
- Do **not** touch the stencil, queue, or fragment math.

---

## Rebuild + deploy
1. Rebuild the `fireswater` AssetBundle (`Valheim/Build AssetBundles`).
2. Copy the bundle to the mod(s) that own ocean rendering. In the current test
   setup **FiresAdminTerrain owns the ocean swap**, so FAT's `fireswater` bundle
   is the one that matters for the visible surface. (Galaxies embeds its own copy
   too, but in coexist mode it doesn't drive the ocean.)

## Verify in-game
- Console-set the offset live to confirm the wire-up before trusting tide timing:
  `Shader.SetGlobalFloat("_FiresTideOffset", -4)` → the whole ocean visibly drops
  ~4 m, exposing beach; `+3` floods it; `0` returns to vanilla. (Or just watch the
  Galaxies tide cycle.)
- Stand a boat near shore: at low tide it should sit lower **and** the rendered
  surface should drop with it (no gap between hull waterline and the rendered
  sheet). A visible gap = the shader offset and the C# physics offset disagree in
  sign or magnitude — they must be identical (`worldPos.y += _FiresTideOffset`,
  same global the C# sets).

---

## Sign convention (must match C#)
`_FiresTideOffset = currentWaterY − vanillaBaselineY` (baseline = `ZoneSystem.m_waterLevel`, ≈30).
- Low tide → **negative** → surface **drops**.
- Above vanilla high (blood moon) → **positive** → surface **rises**.
Add it (`+=`); never negate.

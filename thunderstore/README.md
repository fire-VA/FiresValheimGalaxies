# Fires Valheim Galaxies

A real moon for Valheim, and the sea that follows it.

Valheim's moon is a picture painted on the sky. Galaxies replaces it with a 3D moon that has phases, orbits the
world, and pulls the ocean with it: tides, currents and storm whirlpools, the same for every player on a server.

## What it does

**The moon**
- A 3D moon crosses the night sky, high at midnight and gone at noon, opposite the sun.
- It goes through real phases, new to full and back, over a lunar month you can set (30 game days by default).
- A blood-moon look can be switched on for events.

**Orbitals**
- Up to four bodies (A to D) on tilted orbits around the world: the moon itself, or any ship, piece or blueprint by
  prefab name.
- You can fly out to them and stand on them.

**Tides**
- The whole ocean rises and falls with the moon, so shorelines walk in and out.
- Spring tides at full and new moon swing further both ways.
- Rivers and swamps feel only part of the tide, so they never drain dry.
- The tide is worked out from world time, so every player sees the same water.

**Currents**
- Rivers push downhill: drift downstream, work your way up.
- Open-ocean gyres: slow, wide currents worth riding on long voyages.
- River mouths breathe with the tide: outbound on the ebb, inbound on the flood.
- Ships, swimmers and creatures all feel the drift. Sailing with the current and the wind is fast; fighting both is slow.
- Admins can exempt themselves from currents.

**Whirlpools**
- In storms, funnels can open in deep water: a visible swirl, an inward pull, and a core that drags ships down and
  grinds their hulls. Sail wide.

**Diving**
- The water surface is no longer a wall: swim down. Stamina drains while you are under, and sea creatures can follow.

## Settings

Everything is in the F8 config window (or `BepInEx\config\com.Fire.FiresValheimGalaxies.cfg`) under these groups:

| Group | What it sets |
|---|---|
| Galaxies - General | the master switch for the moon, tide and current systems |
| Galaxies - Moon | size, distance, lunar month length, look (brightness, phase edge) |
| Galaxies - Orbital A to D | each orbiting body: prefab, orbit size and shape, tilt, speed, scale |
| Galaxies - Tides, Tides (Cycle), Tides (Range) | tides on/off, high and low water levels, tides per day |
| Galaxies - Tidal Currents, Rivers, Open Ocean, Ships, Wind | current strengths, river detection, wind drag on ships |
| Galaxies - Whirlpools | spawn chance, lifetime, size, pull and damage, which storms spawn them |
| Galaxies - Diving | underwater movement and stamina |
| Galaxies - Debug Visualization | live current arrows and orbit traces, for setting things up |

Server settings sync to every client.

## Console commands

- `fires_moondiag`: a full report on the moon and the orbitals.
- `galaxies_waterdiag`: every water surface near you and the tide check.
- `galaxies_whirl here | clear | status`: spawn, clear or inspect whirlpools.

## Requirements

- BepInExPack Valheim
- Fires Unified Core
- Works with Fires Admin Terrain: when it is installed, Galaxies runs its sky and diving systems and leaves terrain and
  water tools to Admin Terrain.

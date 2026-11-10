# Fires Valheim Galaxies

A real moon for Valheim, and the sea that follows it. Galaxies swaps the painted moon for a 3D one with phases that
orbits the world and pulls the ocean with it: tides, currents and storm whirlpools, the same for every player on a server.

## What's new in 0.1.75

- **Floating plants sit on the water.** Lily pads and other floating plants now sit on the water you see instead of
  under it at low tide, in ponds and inland water too.
- **Water feels the way it looks.** Swimming, the camera and the underwater fog now agree on where the water surface
  is, so you no longer go "underwater" above the drawn surface or the other way round.
- **Faster quitting.** Galaxies no longer holds up the game while it closes.

## What you get

**The moon**
- A 3D moon crosses the night sky, high at midnight and gone at noon, opposite the sun.
- Real phases, new to full and back, over a lunar month (30 game days by default).
- Blood moons: about one full moon in ten turns red and the tides swing harder.

**Orbitals**
- Up to four bodies on tilted orbits around the world: the moon itself, or any ship, piece or build.
- You can fly out to them and stand on them.

**Tides**
- The whole ocean rises and falls with the moon, so shorelines move in and out.
- Full and new moons bring bigger tides.
- Rivers and swamps only feel part of the tide, so they never drain dry.
- Tides hold still under ice.

**Currents**
- Rivers push downhill: drift downstream, work your way up.
- Slow, wide ocean currents worth riding on long voyages.
- River mouths flow out on the ebb and in on the flood.
- Ships, swimmers and creatures all feel the drift. Sailing with current and wind is fast; fighting both is slow.

**Whirlpools**
- In storms, whirlpools can open in deep water. They pull ships in and grind their hulls. Sail wide.

**Diving**
- Swim down below the surface. Stamina drains while you are under.
- Creatures diving after you is now part of FiresMonsterOverhaul, not Galaxies.

## How to use

It all works on its own once installed. To change things, open the Fires config window (F1 > Config settings):
moon size and month length, tide heights, current strength, whirlpool chance, diving, blood moons and more.
Server settings apply to every player.

Admins can turn currents off for themselves, and use `galaxies_whirl here | clear | status` to spawn, clear or
check whirlpools.

## Install

Install on every player's game and on the server (if you use one), with a mod manager or by copying the plugin
folder into `BepInEx/plugins`.

## Requires

- BepInExPack Valheim
- Fires Unified Core 0.2.1223 or newer

## Good to know

- Works with Fires Admin Terrain: with both installed, Galaxies runs the sky and diving and leaves terrain and water
  tools to Admin Terrain.
- Water looks (colour, foam, reflections) come from Fires Tossin Shade.
- With Fires Advanced Player Movement installed, it handles the camera when your head goes underwater; Galaxies still
  handles the camera on rolling ship swells.
- On curved ring worlds, tides, currents and whirlpools switch themselves off; normal worlds are unchanged.

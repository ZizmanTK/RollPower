# Roll Power

The upgrade of **Roll Power**, my GMTK Game Jam 2022 game ("Roll of the Dice"). You are Pip-6, a maintenance robot
shaped like a die, hovering on its own anti-grav field. Each face carries a gun module and the face on top fires by
itself: **roll to choose your weapon**. Every enemy shows what protects it, and only the right kind of gun gets through.

Unity **6000.6.3f1**, URP. Meshes for robots, guns, ammo, effects and props come from a Blender script
(`Tools/Blender/roster.py` → `Assets/Resources/Models`); materials, UI, sound and music are generated in code
(`Assets/Scripts/DiceHero`). The only scene is `Assets/Scenes/Main.unity`.

## Controls

| | Keyboard / mouse | Gamepad |
|---|---|---|
| Move (hover) | WASD / arrows | Left stick / d-pad |
| Roll (tip one face the way you move) | Space / Shift | A / RB |
| Pause | Esc / P | Start |
| Menus | Arrows + Enter, or the mouse | D-pad + A, B to go back |
| Workshop (station map) | W | |

## How it plays

- **Roll to choose your gun.** Markers on the floor show which gun each roll brings to the top. The start of a roll
  can't be hurt; the landing crushes bare robots and mites.
- **You can tell what hurts an enemy by looking at it.** Six damage types, one look per defence:

  | Enemy looks like | Gets through |
  |---|---|
  | Bare circuit board | any gun, or a landing |
  | Rotors (flying) | seekers (missiles), shock |
  | Riveted steel | piercing (railgun), explosive |
  | Wrapped in vines / ice shell | fire (flamer) |
  | Energy dome | shock (arc lance) |
  | Underground | explosive |
  | Flat mite under your barrels | only a roll onto it |

- **First contact.** The first time each enemy appears, a card explains it and the fight slows until the right gun is on top.
- **Obstacles.** Tall stoppers block you; low pipes trip you over onto the opposite face.

## Modes

- **Campaign "Pip and the House"**: 5 decks, 19 stages (tutorial, Scrap Bay, Hydroponics, Cryo Mines, Foundry, the Core),
  a foreman boss per deck and the High Roller at the end. Modules earned along the way, the Workshop to arrange them,
  Assist, and a hard campaign after the ending.
- **Endless run**: arm your die with guns unlocked by chips and survive waves, with the High Roller every 5th wave.

## Tools

- `DiceHeroSetup` batch methods (Unity `-executeMethod`): `TestCampaign`, `TestButtonRoll`, `TestRollGuide`,
  `PlayTest -stage id -bot novice|average|expert`, `ArtShots -stage id`, `ExportMusic`, `BuildWindows`, `BuildWebGL`.
- Player build flags: `-capture dir` with `-campaign` (walk-through) or `-stagewalk id` (one stage, frequent shots).
- `blender -b --factory-startup -P Tools/Blender/roster.py -- sheet|portraits|ammo|export <dir>`.

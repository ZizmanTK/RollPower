# Roll Power 2.0

The upgrade of **Roll Power**, my GMTK Game Jam 2022 game ("Roll of the Dice"). You are a die: rolling is your
superpower. Slam into barriers to roll, the number on top picks your gun, and shove the barrel bombs off the
platform before they blow.

Unity **6000.6.3f1**, URP. Everything (meshes, materials, UI, sound) is generated from code in
`Assets/Scripts/DiceHero`; the only scene is `Assets/Scenes/Main.unity`.

## Controls

| | Keyboard / mouse | Gamepad |
|---|---|---|
| Glide | WASD / arrows | Left stick / d-pad |
| Dash | Space / Shift | A / RB |
| Pause | Esc / P | Start |
| Menus | Arrows + Enter, or the mouse | D-pad + A, B to go back |
| Pick upgrade | 1 / 2 / 3 or click | Left/right + A |
| Restart (game over) | R | Y |

## What's new in 2.0

- **Bombs are back** (from the jam version): barrel bombs drop in, their lights go green → yellow → red.
  Shove them, or land a roll next to them, to send them off the edge. Blasts hurt enemies too and chain other bombs.
- **High Roller boss** every 5th wave: a giant die that tumbles toward you. Only the gun matching its top number hurts it.
- **Bomber** enemy, **combo multiplier** (x5), **12 stackable upgrades** picked between waves.
- **Game feel**: hit-stop, trauma camera shake, debris and sparks, damage numbers, post-FX pulses, 14 new sounds.
- **Roll guide**: when your gun can't hurt what's on the field, an arrow marks the barrier to slam and one line says "ROLL → FOR TRI-SHOT".
- **Minimal HUD**: just your gun, hearts, score (and the boss bar on boss waves).
- **Menus**: title, how to play, pause, settings (music, SFX, shake, fullscreen, hints), game over with best score.
- **New look**: gold hero die, lunar platform floating in space, red barriers and blue conduits like the original.

## Your music

Put the original FL Studio track at `Assets/Resources/Music/RollPower.ogg` (or `.mp3` / `.wav`); the game uses it
automatically instead of the synthesized loop. Loop point: the whole clip.

## Building (menu **Roll Power** in the editor, or batch mode)

```
Unity.exe -batchmode -quit -projectPath . -executeMethod DiceHeroSetup.BuildWindows   # Builds/RollPower-Windows
Unity.exe -batchmode -quit -projectPath . -executeMethod DiceHeroSetup.BuildWebGL     # Builds/RollPower-WebGL
```

Other batch commands (`-executeMethod DiceHeroSetup.<name>`):

- `Setup`: rebuild the scene, URP asset and post-FX profile
- `Preview -previewOut out.png`: render the arena
- `PlayTest -previewOut dir [-playSeconds 240] [-godmode]`: the autopilot plays the real game, logs every wave
- `TestRollGuide`: for drones, tanks and the boss, and every starting face, follows the on-screen roll guide and checks it lands a gun that hurts them (exit code 1 on failure)
- `MakeItchAssets -assetsIn captures -assetsOut Builds/itch`: cover (630x500), icon and 1920x1080 screenshots

## Capture mode (screenshots / smoke test of the real player)

```
RollPower.exe -capture <folder> [-godmode] [-mute] [-captureTime 150] -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
```

The autopilot plays while the director visits every screen (cover, title, how-to, pause, upgrades, boss, game over)
and saves a PNG of each, then quits.

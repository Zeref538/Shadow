# Shadow — Unity 2D assignment

A 2D platformer where the player character is made from my own photos.
Idle, Run and Jump animations, a spike hazard to jump over, a background and
background music.

## Open it after cloning

1. Install **Unity 6000.5.10f1** from Unity Hub (same version this was built with).
2. Unity Hub → **Add** → pick the `ShadowGame` folder (not this one).
3. Open it. First open takes a few minutes while Unity rebuilds `Library/`.
4. Open `Assets/Scenes/Game.unity` and press **Play**.

If the scene is missing or looks broken, rebuild it from scratch:
menu bar → **Shadow → Build Game Scene**. That one command recreates the
animations, the Animator controller, the spikes, the music and the scene
from the raw sprites in `Assets/Sprites`.

## Controls

| Key | Action |
|---|---|
| A / D or arrow keys | run left / right |
| Space | jump |

## What lives where

- `ShadowGame/Assets/Sprites/idle,run,jump` — my photo frames
- `ShadowGame/Assets/Scripts` — `CharacterController2D`, `CameraFollow`, `playermovement`
- `ShadowGame/Assets/Editor/BuildGameScene.cs` — the scene builder
- `ShadowGame/Assets/Animations` — generated clips + `Player.controller`
- `idle/`, `run/`, `jump/`, `bg.png`, `ground.png` at the repo root — the original raw files

## Notes

- `Library/` is not committed. Unity rebuilds it on first open, so a clone is small.
- The provided `CharacterController2D.cs` used `Rigidbody2D.velocity`, which Unity 6
  renamed to `linearVelocity`. The copy in `Assets/Scripts` is patched.

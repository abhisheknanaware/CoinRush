# Coin Rush (Unity 2D)

A 2D platformer mini-game made in Unity. Collect coins, avoid patrolling enemies, keep your health and reach the goal flag before the 60-second timer runs out.

## Features
- Main menu with **Play** and **How to Play**
- Rigidbody2D player movement with ground check and jumping
- 14 spinning coins (+10 each) with sparkle particles
- 5 patrolling enemies: knockback, hit flash and hit particles
- 3 health, 60-second timer, Win / Game Over / Time Up screens with Restart
- Pulsing goal flag, victory confetti, sound effects
- Exports to **WebGL**

## Controls
| Input | Action |
|---|---|
| A / Left Arrow | Move left |
| D / Right Arrow | Move right |
| Space | Jump |

## Open the project
1. Install **Unity 6000.4.0f1** (Unity 6) with Unity Hub (add **WebGL Build Support** for browser builds).
2. Unity Hub -> **Add -> Add project from disk** -> select this folder.
3. Open `Assets/Scenes/MainMenu.unity` and press **Play**.

## Build
Menu **CoinRush -> Build WebGL**. Serve `Builds/WebGL` with a local web server, e.g. `python -m http.server`.

## Main scripts
`PlayerController`, `Coin`, `EnemyController`, `GameManager`, `UIManager`, `Goal`, `AudioManager`, `CameraFollow` (in `Assets/Scripts`). The editor builder in `Assets/Scripts/Editor` generates the scene, materials and prefabs.

Built with **Unity 6000.4.0f1**.

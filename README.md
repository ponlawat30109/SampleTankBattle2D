# Tank Battle 2D

## Overview

Tank Battle 2D is a small 2D multiplayer sample game that demonstrates basic networked gameplay using FishNet. The project also uses Unity's UI Toolkit for UI and the Unity Input System for player input.

## Key Features

- Max players: 2 (per session)
- Auto discovery / Auto join: clients will try to discover a host on the local network and join automatically. If none found, the player can become the host.
- Session-full notice: when the server is full, clients see a short center-screen message and countdown.
- Visual feedback: tanks blink when hit to indicate damage.
- Networking: implemented with FishNet (high-level networking library).
- UI: uses Unity UI Toolkit (UXML / USS / UIDocument).
- Input: uses the Unity Input System (check `Assets/Scripts/Tank/InputMap/PlayerInputMap.inputactions`).

## Important Files

- `Assets/Scripts/Manager/NetworkManagerController.cs` — discovery, server/client start logic, and max-player enforcement.
- `Assets/Scripts/UI/ArenaUI/ClientRoundUI.cs` — center-screen messages and countdown (UI Toolkit).
- `Assets/Scripts/Manager/ServerMessageRelay.cs` — small helper used by the server to notify local players before kicking.

## Unity Version

This project targets **Unity 6.3 LTS**. If you are using a different Unity editor version, please make sure packages (FishNet, Input System) are compatible with your editor.

## Quick Start

1. Open this project with **Unity 6.3 LTS** (set the editor to 6.3 LTS in Unity Hub or use the matching editor build).
2. Open the main scene in `Assets/Scenes` (or the project's default scene).
3. Press `Play` in the Editor to test:
   - If no host exists on the network, the instance will try to start a server and then start a local client.
   - If a host exists, the client will attempt to discover and connect automatically.

## Controls

- Input actions are defined in `Assets/Scripts/Tank/InputMap/PlayerInputMap.inputactions` (Unity Input System). Check tank scripts under `Assets/Scripts/Tank/` to see the mapped actions.

## Troubleshooting

- Tank does not spawn:

  - Check Unity Console for server/client startup logs.
  - Verify `NetworkManagerController` calls `StartConnection` / `StartHost` and that `_netManager.IsServerStarted` or `_netManager.IsHostStarted` becomes true before the client attempts to spawn.
  - If you recently refactored code, ensure the local client is started only after the server is ready.

- UI message not appearing on kick:
  - Ensure `ClientRoundUI` has a `UIDocument` with `Label` elements named `centerText` and `countdownText`.
  - For remote clients, verify notifications reach the client (or rely on the fallback behavior that shows the UI when a connection attempt fails shortly after it started).

## Dependencies & Packages

- FishNet: install via Unity Package Manager or include as a package. Use a FishNet release compatible with Unity 6.3 LTS — check the FishNet repo/releases or docs for recommended versions.
- Unity Input System: install via Package Manager (search `Input System`) and enable it in Project Settings -> Player if required.
- Unity UI Toolkit: included in modern Unity editors; use the UI Toolkit workflow (UXML, USS, UIDocument).

## Development Notes / Ideas

- Add a simple lobby (player ready/character selection)
- Add spectate mode for extra players
- Add game settings (match length, map selection)
- Improve logging for connection events to help debug timing issues

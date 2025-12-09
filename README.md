# Tank Battle 2D

## Overview

Tank Battle 2D is a small 2D multiplayer sample game that demonstrates networked gameplay using **Unity Relay** and **FishNet**. Players can host or join games using Unity Relay join codes, eliminating the need for port forwarding or same-network requirements. The project also uses Unity's UI Toolkit for UI and the Unity Input System for player input.

## Key Features

- **Max players**: 2 (per session)
- **Unity Relay networking**: Host and join games over the internet using join codes
- **Session-full notice**: When the server is full, clients see a center-screen message and countdown
- **Visual feedback**: Tanks blink when hit to indicate damage
- **Networking**: Implemented with FishNet (high-level networking library) + Unity Relay
- **UI**: Uses Unity UI Toolkit (UXML / USS / UIDocument)
- **Input**: Uses the Unity Input System (check `Assets/Scripts/Tank/InputMap/PlayerInputMap.inputactions`)

## Architecture

### Networking Components

- **`RelayManager.cs`** — Handles Unity Services authentication and Unity Relay allocation/joining
- **`NetworkManagerController.cs`** — Manages FishNet server/client lifecycle, integrates with RelayManager
- **`ClientRoundUI.cs`** — UI for join code input, host button, and connection status messages
- **`ServerMessageRelay.cs`** — Helper used by the server to notify local players before kicking

### How It Works

1. **Hosting**: Player clicks "Host" → RelayManager creates a Relay allocation → Join code is displayed → FishNet server starts
2. **Joining**: Player enters join code → RelayManager joins the Relay allocation → FishNet client connects to host

## Unity Version

This project targets **Unity 6.3 LTS**. If you are using a different Unity editor version, please make sure packages (FishNet, Unity Transport, Unity Services) are compatible with your editor.

## Quick Start

1. Open this project with **Unity 6.3 LTS**
2. Open the main scene in `Assets/Scenes`
3. Press `Play` in the Editor:
   - You'll see a UI with a join code input field and two buttons: **Join** and **Host**
   - To **host a game**: Click the "Host" button. A join code will appear on screen.
   - To **join a game**: Enter the host's join code in the text field and click "Join"

### Testing Multiplayer Locally

1. Build the project (File → Build and Run)
2. Run one instance as the **host** (click "Host" button)
3. Copy the displayed join code
4. Run another instance or the Editor as the **client** (enter join code and click "Join")

## Network Settings

The `NetworkManagerController` component exposes:
- **MaxClients**: Maximum number of players (default: 2)
- **DebugLogs**: Enable verbose connection logging

The `RelayManager` automatically handles Unity Services authentication and Relay setup.

## Controls

Input actions are defined in `Assets/Scripts/Tank/InputMap/PlayerInputMap.inputactions` (Unity Input System). Check tank scripts under `Assets/Scripts/Tank/` to see the mapped actions.

## Troubleshooting

### Authentication Issues
- **Error**: "RelayManager: Initialization failed"
- **Solution**: Ensure your Unity project is linked to a Unity Gaming Services project. Go to Edit → Project Settings → Services and link your project.

### Join Code Not Displaying
- **Issue**: Host button clicked but no join code appears
- **Solution**: Check the Console for RelayManager errors. Verify Unity Services authentication succeeded.

### Connection Failed
- **Issue**: Client can't connect even with correct join code
- **Solution**: 
  - Verify both players have stable internet connections
  - Check Console logs for Relay errors
  - Ensure the join code is copied correctly (case-sensitive)
  - Try hosting again to get a fresh join code

### Session Full
- When a third player tries to join, they'll see "Session full — please try again later" and be disconnected after 5 seconds

### UI Elements Missing
- Ensure `ClientRoundUI` has a `UIDocument` with elements named:
  - `centerText` (Label)
  - `countdownText` (Label)
  - `joinCodeLabel` (Label)
  - `joinCodeInput` (TextField)
  - `joinButton` (Button)
  - `hostButton` (Button)

## Dependencies & Packages

- **FishNet**: High-level networking library compatible with Unity 6.3 LTS
- **Unity Transport (UTP)**: Required for FishNet + Unity Relay integration
- **Unity Services Core**: Required for Unity Gaming Services authentication
- **Unity Relay**: Provides relay server allocation and join codes
- **Unity Input System**: Install via Package Manager (search `Input System`)
- **Unity UI Toolkit**: Included in modern Unity editors

## Development Notes / Ideas

- Add a simple lobby with player ready states
- Add spectate mode for extra players
- Add game settings (match length, map selection)
- Implement reconnection logic for dropped connections
- Add join code clipboard copy button
- Show player count in UI (e.g., "1/2 players")

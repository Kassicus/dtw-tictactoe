# Super Tic-Tac-Toe 3D - Project Plan

## Living Development Document
**Last Updated:** [Update this as you progress]  
**Team:** [Your team names]  
**Deadline:** 2 weeks from start  
**Engine:** Godot 4.2+ with C# (.NET 6.0)

---

## Table of Contents
1. [Project Overview](#1-project-overview)
2. [Game Rules & Mechanics](#2-game-rules--mechanics)
3. [Technical Architecture](#3-technical-architecture)
4. [Physics System Design](#4-physics-system-design)
5. [Audio Design](#5-audio-design)
6. [UI/UX Design](#6-uiux-design)
7. [Cross-Platform Strategy](#7-cross-platform-strategy)
8. [Development Timeline](#8-development-timeline)
9. [Task Breakdown](#9-task-breakdown)
10. [Resources & References](#10-resources--references)
11. [Risk Assessment](#11-risk-assessment)
12. [Progress Tracking](#12-progress-tracking)

---

## 1. Project Overview

### Vision Statement
A visually stunning 3D Super Tic-Tac-Toe game featuring jello-like game pieces with soft body physics, immersive audio, and polished UI - playable on Windows, Linux, and macOS.

### Core Features
| Feature | Priority | Status |
|---------|----------|--------|
| Super Tic-Tac-Toe game logic | Critical | ⬜ Not Started |
| 3D game board rendering | Critical | ⬜ Not Started |
| Jello X/O pieces with physics | High | ⬜ Not Started |
| Piece drop animation | High | ⬜ Not Started |
| Background music system | Medium | ⬜ Not Started |
| Sound effects | Medium | ⬜ Not Started |
| Main menu system | High | ⬜ Not Started |
| Settings menu | Medium | ⬜ Not Started |
| Cross-platform builds | Critical | ⬜ Not Started |
| Win/lose animations | Medium | ⬜ Not Started |
| AI opponent (stretch) | Low | ⬜ Not Started |

### Target Platforms
- Windows 10/11 (x64)
- Linux (x64)
- macOS (x64 + Apple Silicon via Universal)

---

## 2. Game Rules & Mechanics

### Super Tic-Tac-Toe Rules
Super Tic-Tac-Toe (also called Ultimate Tic-Tac-Toe) is played on a 3×3 grid of 3×3 tic-tac-toe boards.

#### Basic Rules:
1. **Board Structure:** 9 small boards arranged in a 3×3 grid (81 total cells)
2. **First Move:** X plays first, can choose any cell in any small board
3. **Subsequent Moves:** Your move determines which small board your opponent must play in
   - If you play in the top-right cell of any small board, opponent must play in the top-right small board
4. **Winning a Small Board:** Standard tic-tac-toe rules (3 in a row)
5. **Winning the Game:** Win 3 small boards in a row (horizontally, vertically, or diagonally)
6. **Special Rules:**
   - If sent to an already-won or full small board, opponent can play anywhere
   - A tied small board counts as neither player's

#### Visual Representation:
```
┌─────────┬─────────┬─────────┐
│ ┌─┬─┬─┐ │ ┌─┬─┬─┐ │ ┌─┬─┬─┐ │
│ ├─┼─┼─┤ │ ├─┼─┼─┤ │ ├─┼─┼─┤ │
│ ├─┼─┼─┤ │ ├─┼─┼─┤ │ ├─┼─┼─┤ │
│ └─┴─┴─┘ │ └─┴─┴─┘ │ └─┴─┴─┘ │
├─────────┼─────────┼─────────┤
│ ┌─┬─┬─┐ │ ┌─┬─┬─┐ │ ┌─┬─┬─┐ │
│ ├─┼─┼─┤ │ ├─┼─┼─┤ │ ├─┼─┼─┤ │
│ ├─┼─┼─┤ │ ├─┼─┼─┤ │ ├─┼─┼─┤ │
│ └─┴─┴─┘ │ └─┴─┴─┘ │ └─┴─┴─┘ │
├─────────┼─────────┼─────────┤
│ ┌─┬─┬─┐ │ ┌─┬─┬─┐ │ ┌─┬─┬─┐ │
│ ├─┼─┼─┤ │ ├─┼─┼─┤ │ ├─┼─┼─┤ │
│ ├─┼─┼─┤ │ ├─┼─┼─┤ │ ├─┼─┼─┤ │
│ └─┴─┴─┘ │ └─┴─┴─┘ │ └─┴─┴─┘ │
└─────────┴─────────┴─────────┘
```

### Game States
```
GameState:
  - MainMenu
  - Playing
  - Paused
  - GameOver (with Winner/Draw substates)
  
TurnState:
  - WaitingForInput
  - PieceDropping
  - EvaluatingMove
  - SwitchingTurns
```

---

## 3. Technical Architecture

### Project Structure
```
SuperTicTacToe/
├── project.godot
├── SuperTicTacToe.csproj
├── SuperTicTacToe.sln
│
├── Scenes/
│   ├── Main.tscn                    # Root scene
│   ├── Game/
│   │   ├── GameBoard.tscn           # Main 3D game board
│   │   ├── SmallBoard.tscn          # Individual 3×3 board
│   │   ├── Cell.tscn                # Single cell (clickable)
│   │   └── GameCamera.tscn          # Camera rig
│   ├── Menu/
│   │   ├── MainMenu.tscn
│   │   ├── PauseMenu.tscn
│   │   ├── SettingsMenu.tscn
│   │   └── GameOverScreen.tscn
│   ├── Pieces/
│   │   ├── XPiece.tscn              # Jello X with soft body
│   │   └── OPiece.tscn              # Jello O with soft body
│   └── UI/
│       ├── HUD.tscn
│       └── TurnIndicator.tscn
│
├── Scripts/
│   ├── Core/
│   │   ├── GameManager.cs           # Singleton - game state
│   │   ├── SceneManager.cs          # Singleton - scene transitions
│   │   └── EventBus.cs              # Signal-based events
│   ├── Game/
│   │   ├── SuperTicTacToeGame.cs    # Game logic
│   │   ├── BoardController.cs       # Board visual management
│   │   ├── SmallBoard.cs            # Small board logic
│   │   ├── Cell.cs                  # Cell interaction
│   │   ├── MoveValidator.cs         # Move validation
│   │   └── WinChecker.cs            # Win condition checking
│   ├── Pieces/
│   │   ├── GamePiece.cs             # Base piece class
│   │   ├── XPiece.cs                # X-specific behavior
│   │   ├── OPiece.cs                # O-specific behavior
│   │   └── JelloPhysics.cs          # Soft body controller
│   ├── Audio/
│   │   ├── AudioManager.cs          # Singleton - audio control
│   │   ├── MusicPlayer.cs           # Background music
│   │   └── SFXPlayer.cs             # Sound effects
│   ├── UI/
│   │   ├── MainMenuController.cs
│   │   ├── PauseMenuController.cs
│   │   ├── SettingsController.cs
│   │   └── HUDController.cs
│   └── Utils/
│       ├── Constants.cs             # Game constants
│       └── SaveSystem.cs            # Settings persistence
│
├── Resources/
│   ├── Materials/
│   │   ├── JelloMaterial_X.tres     # Translucent jello for X
│   │   ├── JelloMaterial_O.tres     # Translucent jello for O
│   │   ├── BoardMaterial.tres
│   │   └── CellHighlight.tres
│   ├── Audio/
│   │   ├── Music/
│   │   │   └── [background tracks]
│   │   └── SFX/
│   │       ├── piece_drop.wav
│   │       ├── piece_land.wav
│   │       ├── piece_jiggle.wav
│   │       ├── win_small.wav
│   │       ├── win_game.wav
│   │       ├── button_hover.wav
│   │       └── button_click.wav
│   ├── Themes/
│   │   └── DefaultTheme.tres        # UI theme
│   └── Fonts/
│       └── [custom fonts]
│
├── Assets/
│   ├── Models/
│   │   ├── x_piece.glb              # 3D X model
│   │   └── o_piece.glb              # 3D O model (torus)
│   └── Textures/
│       └── [texture files]
│
└── Export/
    ├── Windows/
    ├── Linux/
    └── macOS/
```

### Singleton Autoloads
These are registered in project.godot and available globally:

| Singleton | Purpose |
|-----------|---------|
| `GameManager` | Game state, turn management, player data |
| `AudioManager` | Music/SFX playback, volume control |
| `SceneManager` | Scene loading with transitions |

### Key Classes Design

#### GameManager.cs (Singleton)
```csharp
// Responsibilities:
// - Track current game state (Menu, Playing, Paused, GameOver)
// - Track current player turn
// - Track which small board is active
// - Emit signals for state changes

// Key Signals:
// - GameStateChanged(GameState newState)
// - TurnChanged(Player player)
// - ActiveBoardChanged(Vector2I boardIndex)
// - GameWon(Player winner)
// - GameDraw()
```

#### SuperTicTacToeGame.cs
```csharp
// Responsibilities:
// - Implement game rules
// - Validate moves
// - Check win conditions
// - Track board state (81 cells + 9 small board winners)

// Data Structure:
// Player[,] cells = new Player[9, 9];     // All 81 cells
// Player[] smallBoardWinners = new Player[9];  // Winner of each small board
// int activeSmallBoard = -1;  // -1 = any board valid
```

---

## 4. Physics System Design

### Soft Body / Jello Physics Approach

Godot 4 has limited native soft body support for complex jello physics. Here are your options ranked by feasibility:

#### Option A: SoftBody3D Node (Recommended for 2-week timeline)
**Pros:** Built into Godot, reasonable jello effect  
**Cons:** Can be performance-heavy, requires mesh preparation

```
Implementation:
1. Create high-poly mesh for X and O pieces
2. Use SoftBody3D node
3. Configure simulation parameters:
   - Simulation Precision: 5-10
   - Total Mass: 1.0
   - Linear Stiffness: 0.5 (lower = more jello)
   - Pressure Coefficient: 1.0
   - Damping Coefficient: 0.01
4. Pin top vertices during drop, release on landing
```

#### Option B: Shader-Based Fake Jello (Easier, Better Performance)
**Pros:** Great visual effect, high performance, easier to implement  
**Cons:** Not "real" physics, less interactive

```
Implementation:
1. Use RigidBody3D for drop physics
2. Apply vertex shader that:
   - Simulates wobble on impact
   - Uses sine waves with decay for jiggle
   - Responds to collision signals
3. Track velocity changes to trigger wobble intensity
```

#### Option C: Skeleton-Based Jiggle
**Pros:** Good control, moderate performance  
**Cons:** Requires rigged models

```
Implementation:
1. Create armature with bones throughout piece
2. Use physics simulation on bones
3. Or: Animate bones procedurally based on velocity
```

### Recommended Approach: Hybrid (B + elements of A)
For a 2-week timeline, I recommend:
1. **RigidBody3D** for drop/landing physics
2. **Vertex shader** for jello wobble effect
3. **Particle effects** for impact splash
4. Save full SoftBody3D as stretch goal

### Jello Shader Concept
```glsl
// Simplified jello vertex shader concept
uniform float wobble_intensity = 0.0;
uniform float wobble_decay = 5.0;
uniform float wobble_speed = 10.0;
uniform float time_since_impact = 0.0;

void vertex() {
    float decay = exp(-wobble_decay * time_since_impact);
    float wobble = sin(TIME * wobble_speed + VERTEX.y * 3.0) * wobble_intensity * decay;
    VERTEX.x += wobble * 0.1;
    VERTEX.z += wobble * 0.1;
}
```

### Drop Animation Sequence
```
1. Spawn piece 5-10 units above target cell
2. Apply gravity (RigidBody3D or manual)
3. On collision with board:
   a. Trigger wobble shader
   b. Play landing sound
   c. Spawn particle effect
   d. Reduce bounce each iteration
4. After settling (1-2 seconds):
   a. Lock piece position
   b. Emit "piece_placed" signal
   c. Allow next turn
```

---

## 5. Audio Design

### Audio Architecture

```
AudioManager (Singleton)
├── MusicBus
│   └── BackgroundMusic (AudioStreamPlayer)
└── SFXBus
    ├── UISounds (AudioStreamPlayer)
    └── GameSounds (AudioStreamPlayer - pooled)
```

### Audio Bus Layout
Configure in Godot: Project → Project Settings → Audio → Buses

```
Master (0 dB)
├── Music (-6 dB default)
│   └── Low-pass filter for pause menu
└── SFX (0 dB)
    ├── UI
    └── Game
```

### Sound Effects List

| Sound | Trigger | Notes |
|-------|---------|-------|
| `piece_whoosh.wav` | Piece spawned/falling | Doppler-like falling sound |
| `piece_land_soft.wav` | Piece lands | Soft thud with jello squish |
| `piece_jiggle.wav` | During wobble | Subtle wobble sound |
| `cell_hover.wav` | Mouse over valid cell | Subtle feedback |
| `cell_invalid.wav` | Click invalid cell | Error feedback |
| `win_small.wav` | Win a small board | Achievement sound |
| `win_game.wav` | Win the game | Triumphant fanfare |
| `draw.wav` | Game is draw | Neutral sound |
| `button_hover.wav` | UI hover | Subtle |
| `button_click.wav` | UI click | Satisfying click |

### Music Requirements
- **Main Menu:** Calm, inviting, loopable (60-120 seconds)
- **Gameplay:** Light tension, not distracting, loopable (120-180 seconds)
- **Victory:** Short triumphant sting (5-10 seconds)
- **Defeat:** Short subdued sting (5-10 seconds)

### Free Music Resources
- [OpenGameArt.org](https://opengameart.org/art-search-advanced?keys=&field_art_type_tid%5B%5D=12)
- [FreePD.com](https://freepd.com/)
- [Incompetech](https://incompetech.com/music/royalty-free/)
- [FreeSFX](https://freesfx.co.uk/)

### Audio Manager Features
```csharp
// Key methods:
void PlayMusic(string trackName, float fadeTime = 1.0f)
void StopMusic(float fadeTime = 1.0f)
void PlaySFX(string soundName)
void SetMusicVolume(float linear)  // 0.0 - 1.0
void SetSFXVolume(float linear)    // 0.0 - 1.0
void PauseMusic(bool paused)       // With low-pass filter
```

---

## 6. UI/UX Design

### Screen Flow
```
┌─────────────────────────────────────────────────────┐
│                    MAIN MENU                         │
│  ┌─────────────────────────────────────────────┐    │
│  │           SUPER TIC-TAC-TOE                 │    │
│  │                                             │    │
│  │           [ Play Local ]                    │    │
│  │           [ Settings ]                      │    │
│  │           [ How to Play ]                   │    │
│  │           [ Quit ]                          │    │
│  └─────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────┘
                         │
           ┌─────────────┼─────────────┐
           ▼             ▼             ▼
      ┌─────────┐  ┌──────────┐  ┌──────────┐
      │  GAME   │  │ SETTINGS │  │ HOW TO   │
      │         │  │          │  │  PLAY    │
      └────┬────┘  └──────────┘  └──────────┘
           │
           ▼
    ┌─────────────┐
    │ PAUSE MENU  │ (ESC during game)
    │             │
    │ [ Resume ]  │
    │ [ Settings ]│
    │ [ Quit ]    │
    └─────────────┘
           │
           ▼
    ┌─────────────┐
    │  GAME OVER  │
    │             │
    │ "X Wins!"   │
    │             │
    │ [Play Again]│
    │ [Main Menu] │
    └─────────────┘
```

### HUD Layout (During Game)
```
┌────────────────────────────────────────────────────────────┐
│ [≡]                    TURN: X                    [⚙] [||] │
├────────────────────────────────────────────────────────────┤
│                                                            │
│                                                            │
│                    [3D GAME BOARD]                         │
│                                                            │
│                                                            │
├────────────────────────────────────────────────────────────┤
│ Player X: 2 boards    │    Active: Top-Right    │ Player O │
└────────────────────────────────────────────────────────────┘

Legend:
[≡] = Menu button
[⚙] = Quick settings
[||] = Pause
```

### Visual Feedback Requirements
| Element | Feedback |
|---------|----------|
| Valid cell hover | Glow/highlight + cursor change |
| Invalid cell hover | Red tint or no highlight |
| Active small board | Bright border/glow |
| Inactive small board | Dimmed |
| Won small board | Large X/O overlay with player color |
| Current turn | Animated indicator in HUD |

### Color Palette Suggestion
```
Player X:     #FF6B6B (Coral Red) - Jello tint
Player O:     #4ECDC4 (Teal) - Jello tint
Board:        #2C3E50 (Dark Blue-Gray)
Background:   #1A1A2E (Dark Purple-Blue)
UI Accent:    #F39C12 (Gold)
UI Text:      #ECF0F1 (Off-White)
Valid Cell:   #27AE60 (Green glow)
Invalid:      #E74C3C (Red)
```

### Animation Timings
| Animation | Duration | Easing |
|-----------|----------|--------|
| Button hover | 0.15s | ease-out |
| Button press | 0.1s | ease-in |
| Scene transition | 0.5s | ease-in-out |
| Turn indicator | 0.3s | bounce |
| Piece drop | 0.8-1.2s | physics-based |
| Piece wobble | 1.5s | decay |
| Win celebration | 2.0s | elastic |

---

## 7. Cross-Platform Strategy

### Godot Export Setup

#### Prerequisites
1. Download export templates: Editor → Manage Export Templates → Download
2. For macOS: Need to sign/notarize (or users allow unsigned apps)

#### Export Presets Configuration

**Windows (project.godot → Export)**
```
Platform: Windows Desktop
Architecture: x86_64
Binary Format: Embedded PCK
Executable: SuperTicTacToe.exe
```

**Linux**
```
Platform: Linux/X11
Architecture: x86_64
Binary Format: Embedded PCK
Executable: SuperTicTacToe.x86_64
```

**macOS**
```
Platform: macOS
Architecture: Universal (x86_64 + arm64)
Binary Format: App Bundle
Codesign: Ad-hoc (or proper signing)
Notarization: Skip for testing
```

### C#/.NET Considerations
```xml
<!-- In .csproj, ensure cross-platform compatibility -->
<PropertyGroup>
  <TargetFramework>net6.0</TargetFramework>
  <EnableDynamicLoading>true</EnableDynamicLoading>
</PropertyGroup>
```

### Platform-Specific Code (if needed)
```csharp
// Use OS.GetName() for platform detection
string platform = OS.GetName(); // "Windows", "Linux", "macOS"

// File paths
string savePath = OS.GetUserDataDir(); // Cross-platform
```

### Testing Matrix
| Feature | Windows | Linux | macOS |
|---------|---------|-------|-------|
| Launch | ⬜ | ⬜ | ⬜ |
| Audio playback | ⬜ | ⬜ | ⬜ |
| Fullscreen toggle | ⬜ | ⬜ | ⬜ |
| Save/Load settings | ⬜ | ⬜ | ⬜ |
| Input handling | ⬜ | ⬜ | ⬜ |
| Performance (60fps) | ⬜ | ⬜ | ⬜ |

### Build Script (Optional)
```bash
#!/bin/bash
# build_all.sh - Place in project root

GODOT_PATH="/path/to/godot"
PROJECT_PATH="."

# Windows
$GODOT_PATH --headless --export-release "Windows" "$PROJECT_PATH/Export/Windows/SuperTicTacToe.exe"

# Linux
$GODOT_PATH --headless --export-release "Linux" "$PROJECT_PATH/Export/Linux/SuperTicTacToe.x86_64"

# macOS
$GODOT_PATH --headless --export-release "macOS" "$PROJECT_PATH/Export/macOS/SuperTicTacToe.app"

echo "Build complete!"
```

---

## 8. Development Timeline

### Two-Week Sprint Overview

```
Week 1: Foundation & Core Gameplay
├── Days 1-2: Project setup, architecture, basic game logic
├── Days 3-4: 3D board, cell interaction, turn system
└── Days 5-7: Piece creation, drop physics, basic jello effect

Week 2: Polish & Packaging
├── Days 8-9: Audio system, sound effects, music
├── Days 10-11: Menu system, UI polish
├── Days 12-13: Testing, bug fixes, cross-platform builds
└── Day 14: Final testing, documentation, submission prep
```

### Detailed Daily Plan

#### Week 1

**Day 1 - Project Setup**
- [ ] Create Godot project with C# support
- [ ] Set up folder structure
- [ ] Create singletons (GameManager, AudioManager, SceneManager)
- [ ] Configure project settings
- [ ] Set up version control (Git)
- [ ] Create basic Constants.cs

**Day 2 - Core Game Logic**
- [ ] Implement SuperTicTacToeGame.cs (rules engine)
- [ ] Create data structures for board state
- [ ] Implement MoveValidator.cs
- [ ] Implement WinChecker.cs
- [ ] Write unit tests for game logic (optional but recommended)

**Day 3 - 3D Game Board**
- [ ] Create GameBoard.tscn (main 3D scene)
- [ ] Create SmallBoard.tscn (prefab for 3×3 grid)
- [ ] Set up board materials
- [ ] Position camera
- [ ] Add basic lighting

**Day 4 - Cell Interaction**
- [ ] Create Cell.tscn with collision
- [ ] Implement raycasting for mouse input
- [ ] Add hover highlighting
- [ ] Connect cell clicks to game logic
- [ ] Visual feedback for valid/invalid moves
- [ ] Highlight active small board

**Day 5 - Basic Pieces**
- [ ] Create/find 3D models for X and O
- [ ] Create GamePiece base class
- [ ] Implement piece spawning above board
- [ ] Basic RigidBody3D drop physics
- [ ] Piece placement at correct cell position

**Day 6 - Jello Effect**
- [ ] Create jello material (translucent, subsurface scattering)
- [ ] Implement wobble shader OR
- [ ] Set up SoftBody3D for pieces
- [ ] Tune physics parameters for satisfying jello feel
- [ ] Add impact detection for wobble trigger

**Day 7 - Week 1 Integration**
- [ ] Connect all systems together
- [ ] Full game loop working
- [ ] Turn switching functional
- [ ] Win/draw detection working
- [ ] Fix major bugs
- [ ] Playtest and note issues

#### Week 2

**Day 8 - Audio Foundation**
- [ ] Implement AudioManager fully
- [ ] Set up audio buses
- [ ] Add placeholder sounds
- [ ] Music playback with volume control
- [ ] Implement audio pooling for SFX

**Day 9 - Sound Polish**
- [ ] Find/create all sound effects
- [ ] Find/select background music
- [ ] Implement all game sound triggers
- [ ] Add UI sounds
- [ ] Test audio balance
- [ ] Add low-pass filter for pause

**Day 10 - Menu System**
- [ ] Create MainMenu.tscn
- [ ] Create PauseMenu.tscn
- [ ] Create SettingsMenu.tscn
- [ ] Implement scene transitions
- [ ] Settings persistence (audio levels)

**Day 11 - UI Polish**
- [ ] Create GameOverScreen.tscn
- [ ] Implement HUD
- [ ] Add animations to UI elements
- [ ] Create consistent UI theme
- [ ] Add "How to Play" screen

**Day 12 - Cross-Platform Builds**
- [ ] Configure export presets
- [ ] Test Windows build
- [ ] Test Linux build
- [ ] Test macOS build
- [ ] Fix platform-specific issues

**Day 13 - Bug Fixes & Polish**
- [ ] Comprehensive playtesting
- [ ] Fix all critical bugs
- [ ] Performance optimization
- [ ] Visual polish pass
- [ ] Audio polish pass

**Day 14 - Final Day**
- [ ] Final testing on all platforms
- [ ] Create README for submission
- [ ] Package final builds
- [ ] Backup everything
- [ ] Submit!

---

## 9. Task Breakdown

### Assignable Tasks (for team distribution)

#### Programming Tasks
| Task | Est. Hours | Assigned To | Status |
|------|------------|-------------|--------|
| GameManager singleton | 2 | | ⬜ |
| SceneManager singleton | 2 | | ⬜ |
| AudioManager singleton | 3 | | ⬜ |
| SuperTicTacToeGame logic | 4 | | ⬜ |
| MoveValidator | 2 | | ⬜ |
| WinChecker | 2 | | ⬜ |
| BoardController | 3 | | ⬜ |
| Cell interaction | 3 | | ⬜ |
| GamePiece + dropping | 4 | | ⬜ |
| Jello physics/shader | 6 | | ⬜ |
| MainMenu controller | 2 | | ⬜ |
| PauseMenu controller | 1 | | ⬜ |
| Settings controller | 2 | | ⬜ |
| HUD controller | 2 | | ⬜ |
| Save system | 2 | | ⬜ |

#### Art/Design Tasks
| Task | Est. Hours | Assigned To | Status |
|------|------------|-------------|--------|
| X piece 3D model | 2 | | ⬜ |
| O piece 3D model | 2 | | ⬜ |
| Board model/design | 2 | | ⬜ |
| Jello material | 2 | | ⬜ |
| UI theme design | 3 | | ⬜ |
| Scene lighting | 2 | | ⬜ |
| Particle effects | 2 | | ⬜ |

#### Audio Tasks
| Task | Est. Hours | Assigned To | Status |
|------|------------|-------------|--------|
| Source/create SFX | 3 | | ⬜ |
| Source background music | 2 | | ⬜ |
| Audio implementation | 2 | | ⬜ |
| Audio balancing | 1 | | ⬜ |

#### QA/Build Tasks
| Task | Est. Hours | Assigned To | Status |
|------|------------|-------------|--------|
| Windows build & test | 1 | | ⬜ |
| Linux build & test | 1 | | ⬜ |
| macOS build & test | 1 | | ⬜ |
| Bug fixing buffer | 4 | | ⬜ |

---

## 10. Resources & References

### Official Documentation
- [Godot 4 Documentation](https://docs.godotengine.org/en/stable/)
- [Godot C# Documentation](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/index.html)
- [SoftBody3D](https://docs.godotengine.org/en/stable/classes/class_softbody3d.html)
- [Exporting Projects](https://docs.godotengine.org/en/stable/tutorials/export/index.html)

### Tutorials
- [Godot 4 C# Basics](https://www.youtube.com/results?search_query=godot+4+c%23+tutorial)
- [Godot 3D Game Tutorial](https://docs.godotengine.org/en/stable/getting_started/first_3d_game/index.html)
- [Godot Shaders](https://docs.godotengine.org/en/stable/tutorials/shaders/index.html)

### Assets Resources
**3D Models:**
- [Kenney.nl](https://kenney.nl/) - Free game assets
- [OpenGameArt](https://opengameart.org/) - Free assets
- [Sketchfab](https://sketchfab.com/) - Some free models

**Audio:**
- [Freesound.org](https://freesound.org/) - Sound effects
- [OpenGameArt](https://opengameart.org/) - Music and SFX
- [Incompetech](https://incompetech.com/) - Royalty-free music

**Fonts:**
- [Google Fonts](https://fonts.google.com/)
- [DaFont](https://www.dafont.com/)

### Inspiration
- Look up "Ultimate Tic-Tac-Toe" apps for UI/UX reference
- Jello physics references: World of Goo, Jelly Car

---

## 11. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Soft body physics too complex | Medium | High | Use shader-based jello as fallback |
| Performance issues | Medium | Medium | Profile early, optimize meshes |
| C# Godot quirks | Medium | Low | Consult documentation, community |
| Cross-platform issues | Low | High | Test builds early (Day 10, not Day 14) |
| Scope creep | High | High | Stick to MVP features, save extras for "if time allows" |
| Audio licensing issues | Low | Medium | Only use clearly licensed assets |
| Team coordination | Medium | Medium | Daily standups, clear task assignment |

### Fallback Plans
1. **If soft body is too hard:** Shader-based wobble looks great and is much simpler
2. **If running out of time:** Cut "How to Play" screen, simplify menu animations
3. **If cross-platform issues:** Prioritize Windows, Linux second, macOS last

---

## 12. Progress Tracking

### Daily Standup Template
```
Date: ___________
What I completed yesterday:
- 

What I'm working on today:
-

Blockers:
-
```

### Milestone Checkpoints

**End of Day 2:** ✅/❌
- [ ] Game logic complete and tested

**End of Day 4:** ✅/❌
- [ ] Can click cells and see moves registered

**End of Day 7 (Week 1):** ✅/❌
- [ ] Full game playable with dropping jello pieces
- [ ] Win/loss detection working

**End of Day 11:** ✅/❌
- [ ] Audio complete
- [ ] Menus complete
- [ ] UI polished

**End of Day 14:** ✅/❌
- [ ] All platforms tested
- [ ] Builds packaged
- [ ] Submitted!

### Bug Tracker
| ID | Description | Severity | Status | Assigned |
|----|-------------|----------|--------|----------|
| 001 | | | | |

### Notes & Learnings
_Use this space to document things you learn along the way:_

---

## Quick Reference: Key Godot C# Patterns

### Singleton Access
```csharp
// Accessing autoload singletons
var gameManager = GetNode<GameManager>("/root/GameManager");
// Or use a static Instance pattern in your singleton
GameManager.Instance.DoSomething();
```

### Signals in C#
```csharp
// Declaring
[Signal] public delegate void TurnChangedEventHandler(int player);

// Emitting
EmitSignal(SignalName.TurnChanged, currentPlayer);

// Connecting
someNode.TurnChanged += OnTurnChanged;
```

### Scene Instantiation
```csharp
var pieceScene = GD.Load<PackedScene>("res://Scenes/Pieces/XPiece.tscn");
var piece = pieceScene.Instantiate<XPiece>();
AddChild(piece);
```

### Input Handling
```csharp
public override void _Input(InputEvent @event)
{
    if (@event.IsActionPressed("click"))
    {
        // Handle click
    }
}
```

---

**Good luck with your project! You've got this! 🎮**

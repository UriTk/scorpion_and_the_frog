# Point & Click Detective: Frog & Scorpion

A Unity framework for a 2D point-and-click detective game featuring dual protagonists with unique abilities, clue discovery, and deductive reasoning.

---

## Table of Contents

1. [Overview](#overview)
2. [Requirements](#requirements)
3. [File Structure](#file-structure)
4. [Script Reference](#script-reference)
5. [Setup Guide](#setup-guide)
6. [Example Flow: Frog & Scorpion](#example-flow-frog--scorpion)
7. [Keyboard Controls](#keyboard-controls)
8. [Events Reference](#events-reference)

---

## Overview

**Story Summary:** A murder in the town square. Scorpion and Frog investigate the scene, interrogate witnesses at the cafe, and follow clues through the sewers to the catacombs—where the shocking truth is revealed.

**Core Mechanics:**
- **Dual Characters**: Scorpion (reads text, analytical) and Frog (sees colors, social)
- **Clue Discovery**: Find evidence scattered across scenes, character-specific visibility
- **Deduction Board**: Fill in blanks to reconstruct what happened
- **Location Locking**: Characters can be in different places simultaneously (Cafe scenario)

---

## Requirements

- Unity 2021.3 or newer
- TextMeshPro (Window > TextMeshPro > Import TMP Essential Resources)
- Input System package (Window > Package Manager > Input System)
- Set Active Input Handling to "Both" (Edit > Project Settings > Player)

---

## File Structure

```
PointClickDetective/
├── README.md
└── Scripts/
    ├── PointClickDetective.asmdef          # Assembly definition
    │
    ├── Core/
    │   ├── CoreTypes.cs                    # Enums and data structures
    │   └── GameManager.cs                  # Central game state
    │
    ├── Clues/
    │   ├── ClueSO.cs                       # Clue ScriptableObject
    │   ├── QuestionSO.cs                   # Question ScriptableObject
    │   ├── ClueManager.cs                  # Clue tracking
    │   └── DeductionManager.cs             # Deduction board logic
    │
    ├── Interaction/
    │   ├── Interactable.cs                 # Clickable objects
    │   └── InteractionManager.cs           # Popup UI handling
    │
    ├── Dialogue/
    │   └── DialogueManager.cs              # Speech bubbles & typewriter
    │
    ├── Audio/
    │   └── MusicManager.cs                 # Music crossfading
    │
    ├── Scene/
    │   ├── GameSceneContainer.cs           # Scene data holder
    │   └── GameSceneManager.cs             # Scene transitions
    │
    ├── UI/
    │   ├── CharacterSwitchUI.cs            # Character swap button
    │   ├── JournalUI.cs                    # Clue journal
    │   ├── DeductionUI.cs                  # Deduction board
    │   ├── WorldMapUI.cs                   # Location navigation
    │   ├── PauseManager.cs                 # Pause menu
    │   └── MainMenuManager.cs              # Main menu
    │
    └── Editor/
        ├── PointClickDetective.Editor.asmdef
        └── InteractableEditor.cs           # Custom inspectors
```

---

## Script Reference

### Core Scripts

#### `CoreTypes.cs`
**Location:** `Scripts/Core/`  
**Purpose:** Defines all shared data types used across the system.

| Type | Description |
|------|-------------|
| `CharacterType` | Enum: `Scorpion`, `Frog` |
| `CharacterInteractionData` | Per-character settings for Interactables (visibility, dialogue, clues, effects) |
| `InteractionPrerequisite` | Conditions that must be met (flag, clue, looked at, interacted) |
| `PrerequisiteType` | Enum: `RequiresFlag`, `RequiresClue`, `RequiresLookedAt`, `RequiresInteracted` |
| `InteractionResult` | Struct returned when player looks at or interacts with something |

**Usage:** Automatically used by other scripts. No setup required.

---

#### `GameManager.cs`
**Location:** `Scripts/Core/`  
**Purpose:** Central singleton managing game state, character switching, scene tracking, and flags.

**Key Features:**
- Character switching with location rule support
- Game flags (for progression/unlocks)
- Looked-at and interacted tracking
- Save/Load functionality

**Inspector Fields:**
| Field | Description |
|-------|-------------|
| `Current Character` | Starting character (Scorpion/Frog) |
| `Current Scene Id` | Starting scene ID |
| `Scorpion/Frog Default Portrait` | Fallback portraits for dialogue |
| `Location Rules` | Array of `CharacterLocationRule` for split-location scenarios |

**Location Rules Setup:**
```
Rule Name: "Cafe Investigation"
Trigger Scene Id: "cafe_outside"
Scorpion Scene Id: "cafe_outside"
Frog Scene Id: "cafe_inside"
Lock Character Switch: false
Required Flag: "reached_cafe"
Disabling Flag: "cafe_complete"
```

**Code Examples:**
```csharp
// Switch character
GameManager.Instance.SwitchCharacter();

// Check if can switch (respects location rules)
if (GameManager.Instance.CanSwitchCharacter()) { ... }

// Change scene
GameManager.Instance.ChangeScene("alley");

// Flags
GameManager.Instance.SetFlag("found_weapon");
bool hasFlag = GameManager.Instance.HasFlag("found_weapon");

// Check interaction history
bool looked = GameManager.Instance.HasLookedAt("desk_drawer");
bool interacted = GameManager.Instance.HasInteracted("desk_drawer");
```

---

### Clue Scripts

#### `ClueSO.cs`
**Location:** `Scripts/Clues/`  
**Purpose:** ScriptableObject defining a single clue.

**Create:** Right-click > Create > Point Click Detective > Clue

**Inspector Fields:**
| Field | Description |
|-------|-------------|
| `Clue Id` | Unique string identifier |
| `Clue Number` | Number from spreadsheet (1-20) |
| `Clue Name` | Display name |
| `Description` | What the player reads |
| `Additional Description` | Extra context (italicized) |
| `Icon` | Sprite for journal |
| `Visible To` | `Both`, `ScorpionOnly`, or `FrogOnly` |
| `Visibility Reason` | Why (e.g., "can see colors") |
| `Location Scene Id` | Where this clue is found |
| `Related Question Ids` | Which questions this helps answer |
| `Is Fake Clue` | Marks misleading evidence |
| `Fake Clue Explanation` | Why it's fake |

**Example - Clue #1:**
```
Clue Number: 1
Clue Name: "Purple Items"
Description: "A purple hammer, purple coat, and purple paint bucket. The bucket's rim is blue—the purple is dried blood."
Visible To: FrogOnly
Visibility Reason: "can see colors"
Location Scene Id: "alley"
Related Question Ids: [1, 3, 4]
Is Fake Clue: false
```

---

#### `QuestionSO.cs`
**Location:** `Scripts/Clues/`  
**Purpose:** ScriptableObject defining a deduction question.

**Create:** Right-click > Create > Point Click Detective > Deduction Question

**Inspector Fields:**
| Field | Description |
|-------|-------------|
| `Question Id` | Number (1-13) |
| `Question Text` | Text with blank, e.g., "The victim was injured using ___" |
| `Correct Answer` | The right answer |
| `Fake Answers` | Wrong options shown to player |
| `Additional Description` | Hint or context |
| `Difficulty` | `Easy`, `Medium`, `MediumHard`, `Hard` |
| `Positive Clue Ids` | Finding ALL these unlocks AND allows correct answer |
| `Negative Clue Ids` | Finding ALL these unlocks question early (but can't answer correctly) |
| `Category` | `Murder`, `Timeline`, `PlannedRoute`, `ActualRoute`, `Killer` |
| `Display Order` | Sort order within category |

**Unlock Logic:**
- Question unlocks when ALL positive clues found OR ALL negative clues found
- Player can only answer correctly if they have ALL positive clues

**Example - Question #1:**
```
Question Id: 1
Question Text: "The victim was injured using ___"
Correct Answer: "a hammer"
Fake Answers: ["a knife", "a hat", "a street lamp", "the cold"]
Positive Clue Ids: [1, 2]
Negative Clue Ids: [18]
Category: Murder
```

---

#### `ClueManager.cs`
**Location:** `Scripts/Clues/`  
**Purpose:** Singleton tracking which clues have been discovered.

**Inspector Fields:**
| Field | Description |
|-------|-------------|
| `All Clues` | Array of all ClueSO assets in the game |

**Code Examples:**
```csharp
// Discover a clue (usually called by Interactable)
ClueManager.Instance.DiscoverClue(clueSO);
ClueManager.Instance.DiscoverClue(1); // By number

// Check if found
bool found = ClueManager.Instance.HasClue(1);

// Get all found clues
List<ClueSO> clues = ClueManager.Instance.GetFoundClues();

// Get clues for a specific question
List<ClueSO> relevant = ClueManager.Instance.GetCluesForQuestion(1);

// Check if current character can see a clue
bool canSee = ClueManager.Instance.CanCurrentCharacterSee(clueSO);
```

---

#### `DeductionManager.cs`
**Location:** `Scripts/Clues/`  
**Purpose:** Singleton managing the deduction board state.

**Inspector Fields:**
| Field | Description |
|-------|-------------|
| `All Questions` | Array of all QuestionSO assets |
| `Final Question` | The "who is the killer" question (Q11) |

**Code Examples:**
```csharp
// Check if question is unlocked
bool unlocked = DeductionManager.Instance.IsQuestionUnlocked(1);

// Check if player has evidence to answer correctly
bool canAnswer = DeductionManager.Instance.CanAnswerCorrectly(1);

// Submit an answer
DeductionManager.Instance.SubmitAnswer(1, "a hammer");

// Get player's current answer
string answer = DeductionManager.Instance.GetPlayerAnswer(1);

// Check if specific answer is correct
bool correct = DeductionManager.Instance.IsAnswerCorrect(1);

// Try to submit full solution (checks all regular questions)
bool allCorrect = DeductionManager.Instance.TrySubmitSolution();

// Submit final answer (killer identity)
bool solved = DeductionManager.Instance.TrySubmitFinalAnswer("frog");

// Get progress
var (answered, total, correct) = DeductionManager.Instance.GetProgress();
```

---

### Interaction Scripts

#### `Interactable.cs`
**Location:** `Scripts/Interaction/`  
**Purpose:** Component for clickable objects. Handles per-character visibility, dialogue, and clue discovery.

**Required:** `Collider2D` (auto-added via `[RequireComponent]`)

**Inspector Fields:**
| Field | Description |
|-------|-------------|
| `Object Id` | Unique identifier (auto-generated if empty) |
| `Display Name` | Human-readable name |
| `Belongs To Scene Id` | Scene this object exists in |
| `Scorpion Data` | Settings for when Scorpion is active |
| `Frog Data` | Settings for when Frog is active |

**CharacterInteractionData Fields:**
| Field | Description |
|-------|-------------|
| `Is Visible To This Character` | Can this character see the object? |
| `Look At Dialogue` | Text when clicking eye icon |
| `Look At Portrait` | Portrait override (or uses default) |
| `Interact Dialogue` | Text when clicking hand icon |
| `Interact Portrait` | Portrait override |
| `Prerequisites` | Conditions to unlock interaction |
| `Clue On Look` | ClueSO discovered when looking |
| `Clue On Interact` | ClueSO discovered when interacting |
| `Question Revealed` | QuestionSO revealed on interact |
| `Triggers Scene Change` | Does interacting change scenes? |
| `Target Scene Id` | Where to go |
| `Unlock Flag On Look/Interact` | Flag to set |

**Example - Club Sign (Clue #4):**
```
Object Id: "club_sign"
Display Name: "Club Sign"
Belongs To Scene Id: "alley"

Scorpion Data:
  Is Visible: true
  Look At Dialogue: "The sign says 'NO ENTRY IF WET'. Interesting policy."
  Clue On Look: Clue_04_ClubSign
  
Frog Data:
  Is Visible: false  # Frog can't read
```

---

#### `InteractionManager.cs`
**Location:** `Scripts/Interaction/`  
**Purpose:** Singleton managing the interaction popup (Look/Interact buttons) and cursor states.

**Inspector Fields:**
| Field | Description |
|-------|-------------|
| `Interaction Popup` | The popup panel GameObject |
| `Look Button` | Button for "Look" action |
| `Interact Button` | Button for "Interact" action |
| `Popup Rect Transform` | For positioning |
| `Eye Icon / Hand Icon` | Button sprites |
| `Popup Offset` | Offset from click position |
| `Popup Fade Duration` | Animation time |
| `Close On Miss Click` | Close when clicking elsewhere |
| `Default/Hover Cursor` | Cursor textures |
| `Clue Notification Panel` | Shows "Clue Found!" message |
| `Clue Notification Text` | TMP text for notification |
| `Notification Duration` | How long notification shows |

**Code Examples:**
```csharp
// Close the popup programmatically
InteractionManager.Instance.ClosePopup();

// Check if popup is open
bool open = InteractionManager.Instance.IsPopupOpen;
```

---

### Dialogue Scripts

#### `DialogueManager.cs`
**Location:** `Scripts/Dialogue/`  
**Purpose:** Singleton for displaying dialogue with portraits, typewriter effect, and queuing.

**Inspector Fields:**
| Field | Description |
|-------|-------------|
| `Dialogue Panel` | Main panel GameObject |
| `Portrait Display` | Image for character portrait |
| `Dialogue Text` | TMP text component |
| `Continue Indicator` | "Click to continue" indicator |
| `Use Typewriter` | Enable typewriter effect |
| `Characters Per Second` | Typewriter speed |
| `Typing Audio Source` | AudioSource for typing sounds |
| `Typing Sounds` | Array of typing sound clips |
| `Fade In/Out Duration` | Panel animation |
| `Animate Portrait` | Bob portrait while typing |
| `Skip Key / Advance Key` | Input keys |
| `Click To Advance` | Allow mouse click to advance |

**Code Examples:**
```csharp
// Show single dialogue
DialogueManager.Instance.ShowDialogue("Hello, detective.", portrait);

// Show sequence
DialogueManager.Instance.ShowDialogueSequence(
    ("First line.", portrait1),
    ("Second line.", portrait2),
    ("Third line.", portrait1)
);

// Force close
DialogueManager.Instance.ForceClose();

// Check state
bool showing = DialogueManager.Instance.IsShowing;
bool typing = DialogueManager.Instance.IsTyping;
```

---

### Audio Scripts

#### `MusicManager.cs`
**Location:** `Scripts/Audio/`  
**Purpose:** Singleton managing background music with crossfading between character themes.

**Inspector Fields:**
| Field | Description |
|-------|-------------|
| `Scorpion Track` | Music when Scorpion is active |
| `Frog Track` | Music when Frog is active |
| `Character Crossfade Duration` | Blend time when switching |
| `Scene Crossfade Duration` | Blend time for scene changes |
| `Maintain Position On Character Switch` | Keep beat position when switching |

**Code Examples:**
```csharp
// Set scene-specific tracks
MusicManager.Instance.SetSceneTracks(scorpionClip, frogClip);

// Volume control
MusicManager.Instance.MasterVolume = 0.8f;
MusicManager.Instance.MusicVolume = 0.5f;
```

---

### Scene Scripts

#### `GameSceneContainer.cs`
**Location:** `Scripts/Scene/`  
**Purpose:** Component marking a GameObject as a "scene" container. All children belong to this scene.

**Inspector Fields:**
| Field | Description |
|-------|-------------|
| `Scene Id` | Unique identifier (e.g., "alley") |
| `Display Name` | Human-readable name |
| `Description` | For world map tooltip |
| `Map Icon` | Sprite for world map |
| `Starts Locked` | Hidden on world map initially |
| `Unlock Flag Name` | Flag that unlocks this scene |
| `Scorpion Music / Frog Music` | Scene-specific music |

**Scene IDs for this game:**
- `office`
- `alley`
- `murder_scene`
- `cafe_outside`
- `cafe_inside`
- `club_outside`
- `sewer_entrance`
- `sewer_tunnel`
- `catacombs`

---

#### `GameSceneManager.cs`
**Location:** `Scripts/Scene/`  
**Purpose:** Singleton managing scene transitions (enabling/disabling scene containers).

**Inspector Fields:**
| Field | Description |
|-------|-------------|
| `Scene Containers` | Array of all GameSceneContainer references |
| `Transition Overlay` | Image for fade effect |
| `Transition Duration` | Fade time |
| `Starting Scene Id` | Initial scene |

**Code Examples:**
```csharp
// Load scene with fade
GameSceneManager.Instance.LoadScene("alley");

// Load instantly
GameSceneManager.Instance.LoadSceneInstant("alley");

// Get unlocked scenes
List<GameSceneContainer> scenes = GameSceneManager.Instance.GetUnlockedScenes();

// Check if transitioning
bool transitioning = GameSceneManager.Instance.IsTransitioning;
```

---

### UI Scripts

#### `CharacterSwitchUI.cs`
**Location:** `Scripts/UI/`  
**Purpose:** Button for switching between Scorpion and Frog.

**Setup:** Add to a Button GameObject. The script auto-finds the Button component.

**Inspector Fields:**
| Field | Description |
|-------|-------------|
| `Scorpion Icon` | Sprite when Scorpion is active |
| `Frog Icon` | Sprite when Frog is active |
| `Animate Switch` | Enable scale/fade animation |
| `Switch Animation Duration` | Animation time |
| `Switch Key` | Keyboard shortcut (default: Tab) |

---

#### `JournalUI.cs`
**Location:** `Scripts/UI/`  
**Purpose:** UI panel for viewing collected clues.

**Inspector Fields:**
| Field | Description |
|-------|-------------|
| `Journal Panel` | Main panel |
| `Clue List Panel` | Panel showing clue list |
| `Clue Detail Panel` | Panel showing selected clue |
| `Clue List Container` | Parent for spawned entries |
| `Clue Entry Prefab` | Prefab for each clue entry |
| `Clue Icon/Name/Description/Location Text` | Detail view elements |
| `Fake Clue Indicator` | Shows if clue is misleading |
| `Close/Back/Deduction Board Buttons` | Navigation |
| `Toggle Key` | Keyboard shortcut (default: J) |

---

#### `DeductionUI.cs`
**Location:** `Scripts/UI/`  
**Purpose:** The deduction board where players fill in blanks.

**Inspector Fields:**
| Field | Description |
|-------|-------------|
| `Deduction Panel` | Main panel |
| `Story Text` | TMP text with the fill-in-blank story |
| `Story Template` | Template with {Q1}, {Q2}, etc. placeholders |
| `Answer Selection Panel` | Panel for choosing answers |
| `Current Question Text` | Shows the question being answered |
| `Answer Button Container` | Parent for answer buttons |
| `Answer Button Prefab` | Prefab for each answer option |
| `Final Question Panel` | Panel for "who is the killer" |
| `Submit Button` | Check all answers |
| `Correct/Incorrect Feedback` | Visual feedback |

**Story Template:**
```
The murder happened in the {Q12}. The victim was first injured by {Q1}. 
It led to the victim's death and the body was {Q2}.

The killer knew there was a chance they'd be caught, so they planned a route. 
They would exit the scene going {Q13} and slip through the streets, making 
their way to the safety of the {Q3}. It was a perfect spot to {Q4}. 
And the next place would be the {Q5}, where they could blend in for an alibi.

However, not everything went to plan. The killer wasn't able to go to the 
{Q6} due to the {Q7}, so they opted to go to the {Q8} instead. 
Due to the change of plans the killer made a mistake—they didn't account 
for {Q9}.

At last, once the streets emptied, the killer took their chance to escape. 
They snuck through the {Q10} and into their hideout.

And our killer is: {Q11}
```

---

#### `WorldMapUI.cs`
**Location:** `Scripts/UI/`  
**Purpose:** Navigation between locations.

**Inspector Fields:**
| Field | Description |
|-------|-------------|
| `Map Panel` | Main panel |
| `Location Buttons` | Manual button assignments, or... |
| `Button Prefab / Button Container` | Auto-generate from scenes |
| `Info Panel` | Shows selected location info |
| `Travel Button` | Confirms travel |
| `Toggle Key` | Keyboard shortcut (default: M) |

---

#### `PauseManager.cs`
**Location:** `Scripts/UI/`  
**Purpose:** Pause menu with options.

**Inspector Fields:**
| Field | Description |
|-------|-------------|
| `Pause Menu Panel` | Main pause panel |
| `Options Panel` | Settings panel |
| `Resume/Options/Main Menu/Quit Buttons` | Navigation |
| `Master/Music/SFX Volume Sliders` | Audio settings |
| `Pause Key` | Keyboard shortcut (default: Escape) |
| `Main Menu Scene Name` | Scene to load when returning to menu |
| `Pause Time When Open` | Set timeScale to 0 |

---

#### `MainMenuManager.cs`
**Location:** `Scripts/UI/`  
**Purpose:** Main menu for starting/loading games.

**Inspector Fields:**
| Field | Description |
|-------|-------------|
| `Main Menu Panel` | Main panel |
| `Options/Credits/Load Game Panels` | Sub-panels |
| `New Game/Continue/Load/Options/Credits/Quit Buttons` | Navigation |
| `Volume Sliders` | Audio settings |
| `Game Scene Name` | Scene to load when starting |
| `Save Key` | PlayerPrefs key for saves |

---

### Editor Scripts

#### `InteractableEditor.cs`
**Location:** `Scripts/Editor/`  
**Purpose:** Custom inspector for Interactable and GameSceneContainer.

**Features:**
- Organized foldouts for Scorpion/Frog data
- Hides irrelevant fields when character can't see object
- "Auto-Set Scene ID" button on GameSceneContainer

---

## Setup Guide

### Step 1: Create Manager Objects

Create empty GameObject `GameManagers` with these components:
- GameManager
- ClueManager
- DeductionManager
- InteractionManager
- DialogueManager
- MusicManager
- GameSceneManager
- PauseManager

### Step 2: Create ScriptableObjects

**Clues (20 total):**
1. Right-click > Create > Point Click Detective > Clue
2. Create one for each clue from the spreadsheet
3. Assign to ClueManager's `All Clues` array

**Questions (13 total):**
1. Right-click > Create > Point Click Detective > Deduction Question
2. Create Q1-Q13 from the spreadsheet
3. Assign to DeductionManager's `All Questions` array
4. Assign Q11 to `Final Question`

### Step 3: Create Scene Containers

For each location, create:
```
Scene_Office (GameObject)
├── GameSceneContainer (component)
│   └── Scene Id: "office"
├── Background (SpriteRenderer)
├── Desk (Interactable + Collider2D)
├── Pinboard (Interactable + Collider2D)
└── Door_ToAlley (Interactable + Collider2D)
```

Add all scene containers to GameSceneManager's `Scene Containers` array.

### Step 4: Setup Location Rules

In GameManager, add location rules:

**Cafe Split:**
```
Rule Name: "Cafe Investigation"
Trigger Scene Id: "cafe_outside"
Scorpion Scene Id: "cafe_outside"
Frog Scene Id: "cafe_inside"
Lock Character Switch: false
Required Flag: "reached_cafe"
Disabling Flag: "cafe_complete"
```

**Catacombs Lock:**
```
Rule Name: "Catacombs Finale"
Trigger Scene Id: "catacombs"
Scorpion Scene Id: "catacombs"
Frog Scene Id: "catacombs"
Lock Character Switch: true
Required Flag: "entered_catacombs"
```

### Step 5: Create UI

**Interaction Popup:**
```
Canvas
└── InteractionPopup (Panel + RectTransform)
    ├── LookButton (Button + Image)
    └── InteractButton (Button + Image)
```

**Dialogue Panel:**
```
Canvas
└── DialoguePanel (Panel + CanvasGroup)
    ├── Portrait (Image)
    ├── DialogueText (TextMeshProUGUI)
    └── ContinueIndicator (Image)
```

**Journal:**
```
Canvas
└── JournalPanel
    ├── ClueListPanel
    │   └── ClueListContainer (Vertical Layout)
    ├── ClueDetailPanel
    │   ├── ClueIcon, ClueName, ClueDescription...
    │   └── BackButton
    └── CloseButton
```

**Deduction Board:**
```
Canvas
└── DeductionPanel
    ├── StoryText (TextMeshProUGUI)
    ├── AnswerSelectionPanel
    │   ├── QuestionText
    │   └── AnswerButtonContainer
    ├── FinalQuestionPanel
    ├── SubmitButton
    └── CloseButton
```

---

## Example Flow: Frog & Scorpion

### Scene 1: Office (Start)
```
Location: office
Active: Both characters freely switchable

Interactables:
- Pinboard: Look reveals case history, interact sets flag "reviewed_case"
- Door: Interact triggers scene change to "alley"

Setup:
  Door_ToAlley:
    Scorpion & Frog Data:
      Interact Dialogue: "Time to investigate."
      Triggers Scene Change: true
      Target Scene Id: "alley"
```

### Scene 2: Alley (Hub)
```
Location: alley
Active: Both characters

Navigation to:
- murder_scene (via "Go to Town Square" door)
- sewer_entrance (via sewer grate - requires flag)
- club_outside (via club door)

Clues here:
- #1 Purple Items (Frog only - sees colors)
- #3 Club Sign (Scorpion only - reads text)
- #4 "No Entry If Wet" Sign (Scorpion only)
- #18 Red Painted Knife (Frog only - FAKE CLUE)

Setup:
  PurpleItems:
    Scorpion Data:
      Is Visible: false
    Frog Data:
      Is Visible: true
      Look Dialogue: "Purple hammer, coat, bucket... wait, the bucket rim is blue!"
      Clue On Look: Clue_01
```

### Scene 3: Murder Scene
```
Location: murder_scene
Active: Both characters

Clues here:
- #2 Blood Splatter (Frog only - sees color)
- #6 Heavy Snow observation (Scorpion only)
- #20 Insect Leg (Scorpion only - already there)

Transition:
- Door to cafe triggers "reached_cafe" flag
- This activates the Cafe location rule
```

### Scene 4: Cafe (SPLIT LOCATION)
```
Location Rule Active: "Cafe Investigation"
- Scorpion locked to: cafe_outside
- Frog locked to: cafe_inside
- Can still switch characters (teleports to their location)

cafe_outside (Scorpion):
  Clues: #5 (damp coat observation)
  
cafe_inside (Frog):
  Clues: #8, #9, #10, #11, #12, #13, #15, #16
  NPCs: Herring, Laurie, Regular patron
  
Setup:
  NPC_Herring:
    Frog Data:
      Is Visible: true
      Look Dialogue: "He seems defensive..."
      Clue On Look: Clue_10
      Interact Dialogue: "He asks for another drink."
      Clue On Interact: Clue_11
```

### Scene 5: Sewers
```
Location: sewer_entrance → sewer_tunnel
Unlock: Requires flag "found_sewer_entrance"

Clues:
- #17 Bloody Receipt (Scorpion only)

Transition:
- End of tunnel leads to catacombs
- Sets flag "entered_catacombs"
```

### Scene 6: Catacombs (FINALE)
```
Location: catacombs
Location Rule: Character switch LOCKED to Scorpion

Clue:
- #19 Photograph (Scorpion only - final clue)

Setup:
  Photograph:
    Scorpion Data:
      Is Visible: true
      Prerequisites:
        - Type: RequiresFlag
        - Flag Name: "all_other_clues_found"
      Interact Dialogue: "This photo... it's Frog's sister. The girl who died when I got my transplant."
      Clue On Interact: Clue_19
    Frog Data:
      Is Visible: false  # Frog has... left
```

### Deduction Board Unlock Flow

1. Player finds clues throughout investigation
2. As clue sets complete, questions unlock:
   - Q1 unlocks when clues 1+2 found (or 18 for early unlock)
   - Q11 (killer) unlocks when 16+19 found OR 10+11+12+13 found
3. Player fills in blanks on deduction board
4. Submit checks all answers simultaneously
5. If all correct → Final question reveals
6. Answer "frog" → Case solved → Trigger ending

---

## Keyboard Controls

| Key | Action | Managed By |
|-----|--------|------------|
| Tab | Switch character | CharacterSwitchUI |
| J | Toggle journal | JournalUI |
| M | Toggle world map | WorldMapUI |
| Space | Skip typewriter | DialogueManager |
| Enter | Advance dialogue | DialogueManager |
| Escape | Pause / Close menus | PauseManager, various |
| Right-click | Close popup | InteractionManager |

---

## Events Reference

### GameManager
```csharp
OnCharacterChanged(CharacterType)    // Character switched
OnSceneChanged(string)               // Scene changed
OnFlagSet(string)                    // Flag was set
OnCharacterSwitchBlocked()           // Switch attempted but blocked
```

### ClueManager
```csharp
OnClueFound(ClueSO)                  // New clue discovered
OnCluesChanged()                     // Clue collection modified
```

### DeductionManager
```csharp
OnQuestionUnlocked(QuestionSO)       // Question became available
OnQuestionAnswered(QuestionSO, bool) // Answer submitted (question, wasCorrect)
OnAllQuestionsCorrect()              // All regular questions right
OnFinalQuestionRevealed()            // Q11 unlocked
OnCaseSolved()                       // Final answer correct
```

### DialogueManager
```csharp
OnDialogueStarted()
OnDialogueEnded()
OnLineStarted()
OnLineFinished()
```

### InteractionManager
```csharp
OnInteractionStarted(Interactable)
OnInteractionEnded()
OnClueDiscovered(ClueSO)
```

### MusicManager
```csharp
OnCrossfadeStarted()
OnCrossfadeComplete()
```

### PauseManager
```csharp
OnPaused()
OnResumed()
OnOptionsOpened()
OnOptionsClosed()
```

---

## Tips

1. **Test both characters** in every scene to ensure correct visibility
2. **Use the Editor button** "Auto-Set Scene ID on Child Interactables" to quickly assign scene IDs
3. **Fake clues** should have convincing descriptions but lead to wrong answers
4. **Location rules** can be chained with flags for complex scenarios
5. **Save often** during development - use the built-in save/load in GameManager

---

Happy investigating! 🦂🐸
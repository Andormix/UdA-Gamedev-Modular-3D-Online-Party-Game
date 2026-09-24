
# Rush Hour — Modular 3D Online Party Game

[![Unity](https://img.shields.io/badge/Engine-Unity%206-000000?style=for-the-badge&logo=unity&logoColor=white)](#)
[![C#](https://img.shields.io/badge/Language-C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)](#)
[![Multiplayer](https://img.shields.io/badge/Multiplayer-Netcode%20for%20GameObjects-4B8BBE?style=for-the-badge)](#)
[![Rendering](https://img.shields.io/badge/Rendering-URP-8A2BE2?style=for-the-badge)](#)
[![Architecture](https://img.shields.io/badge/Architecture-Modular%20Hybrid-FF6F00?style=for-the-badge)](#)
[![Academic](https://img.shields.io/badge/Academic-Universitat%20d'Andorra-003366?style=for-the-badge)](#)

<p align="center">
  <a href="https://drive.google.com/file/d/1iE0Ge40X2oEtWlI6x-TtFlCsE7gm76Ua/view?usp=sharing" target="_blank">
    <img
      width="100%"
      alt="Rush Hour gameplay"
      src="https://github.com/user-attachments/assets/3c0cada2-b745-4d69-a217-31f70ee62acc"
    />
  </a>
</p>

> **Final Degree Project in Computer Engineering**  
> **Universitat d'Andorra — Academic Year 2025–2026**  
> **Author:** Eric Torrontera Ruiz  
> **Tutors:** Jan Sau Batlle and Josep Ribó Ferriz

**Rush Hour** is a modular 3D online party game developed with **Unity 6** and **C#**.

Set in a busy café in Andorra, the game challenges players to cooperate under time pressure by preparing orders, serving customers, managing resources, and coordinating their actions across a shared environment.

This repository contains the game's source code, Unity project structure, technical documentation, and architectural implementation. The playable development build is distributed separately from the repository.

---

## Table of Contents

- [Project Overview](#project-overview)
- [Playable Development Build](#playable-development-build)
- [Gameplay Concept](#gameplay-concept)
- [Main Features](#main-features)
- [Technical Highlights](#technical-highlights)
- [Architecture](#architecture)
- [Design Patterns](#design-patterns)
- [Multiplayer](#multiplayer)
- [Rendering and Visual Technology](#rendering-and-visual-technology)
- [Gameplay Systems](#gameplay-systems)
- [Repository Structure](#repository-structure)
- [Requirements for Developers](#requirements-for-developers)
- [Opening the Unity Project](#opening-the-unity-project)
- [Running the Project in Unity](#running-the-project-in-unity)
- [Multiplayer Testing](#multiplayer-testing)
- [Controls](#controls)
- [Project Documentation](#project-documentation)
- [Source Code and Build Distribution](#source-code-and-build-distribution)
- [Copyright and Asset Notice](#copyright-and-asset-notice)
- [Future Improvements](#future-improvements)
- [Academic Context](#academic-context)
- [License](#license)

---

## Project Overview

Rush Hour is a cooperative online party game focused on teamwork, task management, and fast decision-making.

Players work together in a café environment to:

- Receive customer orders.
- Collect the required ingredients or objects.
- Prepare requested items.
- Interact with workstations.
- Deliver completed orders.
- Manage time and resources.
- Earn points and rewards.
- Complete shared objectives.

The project follows a **coop-first architecture**, meaning multiplayer interaction is considered from the beginning of the design process rather than being added after the single-player systems have already been implemented.

The main objective of the project is to demonstrate how a complex multiplayer game can be developed using reusable, modular, and scalable software components.

---

## Playable Development Build

A playable development build of Rush Hour is available separately from this source-code repository.

The build allows users to experience the implemented gameplay without installing Unity or opening the project in the Unity Editor.

### Download the Development Build

> **Development build link:**  
> Ask me for it if you want to try it. I will provide you the build link

```text
https://drive.google.com/drive/folders/1LWDcCUO_mqptqqfOhNbFuWpDe6j5OyAp?usp=sharing
```

### Important Information

The downloadable build is intended for demonstration and evaluation purposes.

It may contain:

- Experimental gameplay.
- Incomplete features.
- Temporary assets.
- Debug systems.
- Unfinished menus.
- Multiplayer functionality under development.
- Performance limitations.

The source repository and the playable build serve different purposes:

| Resource | Purpose |
| :--- | :--- |
| GitHub repository | Source code, Unity project structure, systems, and documentation |
| Development build | Playable version for testing and demonstration |
| Technical documentation | Architecture, design decisions, diagrams, and project analysis |

---

## Gameplay Concept

The game takes place in a busy café where players must coordinate to satisfy customer requests.

A typical gameplay loop is:

```text
1. A customer creates an order.
2. The order is displayed to the team.
3. Players collect the required ingredients or objects.
4. Players interact with workstations and gameplay objects.
5. The order is prepared and completed.
6. The completed order is delivered to the customer.
7. The team receives a score or reward.
8. New orders and challenges are generated.
```

The gameplay is based on:

- Cooperation.
- Communication.
- Resource management.
- Spatial awareness.
- Time management.
- Task prioritization.
- Multiplayer coordination.

---

## Main Features

- 3D cooperative party-game gameplay.
- Online multiplayer using Netcode for GameObjects.
- Modular hybrid software architecture.
- Finite State Machine-based behavior.
- Type Object pattern for configurable gameplay data.
- ScriptableObject-based configuration.
- Extensible player and NPC systems.
- Reusable interaction interfaces.
- Customer order management.
- Café and kitchen-style gameplay objects.
- Character selection system.
- Multiplayer lobby flow.
- Score and economy systems.
- Tutorial and hint systems.
- Audio and visual effects.
- User interface and HUD systems.
- Universal Render Pipeline graphics.
- Unity Input System integration.
- Dedicated network folders for static and dynamic objects.
- Structured project organization for future expansion.

---

## Technical Highlights

| Area | Implementation |
| :--- | :--- |
| Game engine | Unity 6 |
| Main language | C# |
| Rendering | Universal Render Pipeline |
| Networking | Netcode for GameObjects |
| Input | Unity Input System |
| Architecture | Modular hybrid architecture |
| Behavior control | Finite State Machines |
| Data modeling | Type Object pattern and ScriptableObjects |
| Multiplayer model | Networked dynamic and static objects |
| Project type | 3D online cooperative party game |
| Repository purpose | Source code and technical documentation |
| Playable version | Distributed separately as a development build |

---

## Architecture

The project uses a modular hybrid architecture that combines different approaches depending on the type of system being implemented.

```text
┌──────────────────────────────────┐
│          Presentation Layer      │
│                                  │
│  - UI and HUD                    │
│  - Audio                         │
│  - Visual effects                │
│  - Animations                    │
└─────────────────┬────────────────┘
                  │
┌─────────────────▼────────────────┐
│          Gameplay Systems        │
│                                  │
│  - Player interaction            │
│  - Orders                        │
│  - NPC behavior                  │
│  - Economy                       │
│  - Score                         │
│  - Tutorial and hints            │
└─────────────────┬────────────────┘
                  │
┌─────────────────▼────────────────┐
│       Domain and Data Layer      │
│                                  │
│  - ScriptableObjects             │
│  - Type Objects                  │
│  - Shared interfaces             │
│  - Configuration data            │
└─────────────────┬────────────────┘
                  │
┌─────────────────▼────────────────┐
│       Multiplayer Layer          │
│                                  │
│  - Network variables             │
│  - RPC communication             │
│  - Network spawning              │
│  - Ownership and authority       │
│  - Synchronized state            │
└──────────────────────────────────┘
```

The architecture is organized so new gameplay features can be added without requiring major changes to unrelated systems.

Examples of systems that can be extended include:

- New customer types.
- New recipes.
- New ingredients.
- New maps.
- New game modes.
- New interactable objects.
- New player abilities.
- New progression systems.

---

## Design Patterns

### Finite State Machine

Finite State Machines are used to represent behavior that changes depending on the current state of an entity.

A state machine can represent behaviors such as:

```text
Idle
  ↓
Searching
  ↓
Moving
  ↓
Interacting
  ↓
Completed
```

FSMs are useful for:

- NPC behavior.
- Player interaction flow.
- Order progression.
- Tutorial sequences.
- Gameplay object states.
- Menu and lobby transitions.

Each state is responsible for its own behavior, while transitions determine when the system moves to another state.

### Type Object Pattern

The Type Object pattern is used to define configurable types without creating a new class for every gameplay variant.

This is useful for:

- Items.
- Orders.
- Ingredients.
- Character types.
- Customers.
- Interactable objects.
- Gameplay configuration.

In Unity, this approach works well with `ScriptableObject` assets because designers can create and modify data directly in the Unity Editor.

### Component-Based Design

Unity's component model is used to keep behaviors modular.

Instead of placing all functionality in a single large class, responsibilities are separated into components such as:

- Movement.
- Interaction.
- Animation.
- Audio.
- Network synchronization.
- Scoring.
- State control.

### Interface-Based Interaction

Shared interfaces allow different objects to respond to common interactions without requiring them to inherit from the same concrete class.

Examples may include:

```text
IInteractable
ICollectable
IDeliverable
IOrderSource
IUsable
```

This allows the player to interact with different objects using a common interaction workflow.

---

## Multiplayer

The project uses **Netcode for GameObjects (NGO)** to implement online multiplayer functionality.

Networking is organized around the distinction between static and dynamic content.

### Dynamic Networked Objects

Dynamic objects are spawned or modified during gameplay.

Examples include:

- Player characters.
- Held objects.
- Customer orders.
- Collectable items.
- Interactable gameplay objects.
- Temporary effects.
- Networked state changes.

### Static Networked Objects

Static objects exist in the scene and are configured as part of the level.

Examples include:

- Workstations.
- Counters.
- Tables.
- Fixed interaction points.
- Level geometry.
- Permanent gameplay objects.

### Multiplayer Responsibilities

The networking layer manages:

- Player connections.
- Player disconnections.
- Network object spawning.
- Object ownership.
- Server and client communication.
- Remote procedure calls.
- Network variables.
- Shared gameplay state.
- Player actions.
- Common objectives.
- Multiplayer interactions.
- Scene transitions.

### Authority Model

Network authority should be respected when modifying shared gameplay state.

A typical flow is:

```text
Client input
    ↓
Network request or RPC
    ↓
Server validation
    ↓
Authoritative state change
    ↓
State synchronization
    ↓
All clients update their local representation
```

This prevents clients from independently modifying shared game state without validation.

---

## Rendering and Visual Technology

The project uses Unity's **Universal Render Pipeline (URP)**.

URP provides a flexible rendering pipeline suitable for:

- Stylized 3D graphics.
- Cross-platform rendering.
- Lighting configuration.
- Performance optimization.
- Shader customization.
- Post-processing.
- Material effects.

The repository also includes ShaderLab and HLSL-related content for custom visual behavior and rendering configuration.

### Rendering Configuration

Rendering-related assets are organized under:

```text
00_RENDER SETTINGS/
```

This area includes:

- Input System configuration.
- Lighting settings.
- Unity project settings.
- TextMesh Pro resources.
- Rendering-related assets.
- Texture resources.
- Project configuration assets.

---

## Gameplay Systems

### Player System

The player system manages:

- Character movement.
- Camera interaction.
- Player input.
- Object interaction.
- Carrying and delivering objects.
- Animation states.
- Multiplayer synchronization.

### Character Selection

The character-selection system allows players to choose or configure their character before starting gameplay.

Possible responsibilities include:

- Displaying available characters.
- Selecting a character.
- Synchronizing player selections.
- Loading the selected character into the gameplay scene.

### Lobby System

The lobby system manages the transition between joining a session and starting a game.

Typical lobby responsibilities include:

- Creating a session.
- Joining a session.
- Displaying connected players.
- Waiting for all players.
- Selecting or confirming a map.
- Starting the gameplay scene.

### Order System

The order system represents customer requests and their progression through different stages.

An order may contain:

- Required items.
- Current status.
- Customer information.
- Preparation requirements.
- Completion state.
- Score or reward value.

Example order flow:

```text
Created
  ↓
Displayed
  ↓
Being Prepared
  ↓
Ready for Delivery
  ↓
Delivered
  ↓
Rewarded
```

### NPC System

Non-player characters can represent:

- Customers.
- Service agents.
- Interactive characters.
- Tutorial characters.
- Environmental actors.

Their behavior can be controlled through state machines and reusable behavior components.

### Score System

The score system evaluates gameplay performance based on factors such as:

- Successfully completed orders.
- Delivery speed.
- Correctness of the order.
- Team performance.
- Failed or expired orders.
- Level objectives.

### Economy System

The economy system can manage:

- Currency.
- Rewards.
- Item values.
- Purchases.
- Progression.
- Unlockable content.

### Tutorial and Hints

The tutorial and hint systems help players understand:

- Movement.
- Interaction.
- Order preparation.
- Object usage.
- Team coordination.
- Game objectives.

### Audio System

The audio system provides:

- Background music.
- Interface sounds.
- Interaction sounds.
- Customer feedback.
- Order completion sounds.
- Environmental ambience.

### UI and HUD

The interface and HUD display:

- Player information.
- Current orders.
- Scores.
- Timers.
- Objectives.
- Interaction prompts.
- Multiplayer status.
- Menus and lobby information.

---

## Project Structure

```text
.
├── 00_RENDER SETTINGS/
│   ├── InputSystem_Actions.inputactions
│   ├── New Lighting Settings.lighting
│   ├── Settings/
│   ├── TextMesh Pro/
│   ├── Tree_Textures/
│   └── TutorialInfo/
│
├── 02_CODE/
│   ├── Animations/
│   ├── Audio/
│   ├── BUTTONS/
│   ├── CATS/
│   ├── Campaign/
│   ├── CharacterSelect/
│   ├── Credits/
│   ├── Debugging/
│   ├── Economy/
│   ├── FX/
│   ├── GUI + HUD/
│   ├── General SO Code/
│   ├── Interface/
│   ├── Lobby/
│   ├── Managers/
│   ├── Multiplayer/
│   ├── NETCODE/
│   ├── NPCs/
│   ├── Objects Scripts/
│   ├── Orders/
│   ├── Player/
│   ├── SCORE/
│   ├── Team Interaction/
│   ├── TUTORIAL + HINTS/
│   ├── Unused Code/
│   └── camera/
│
├── 05_NETWORK/
│   ├── DYNAMICS/
│   ├── STATICS/
│   └── UTILITY/
│
├── Documentació completa del projecte - MEMORIA.pdf
├── TFB_EricTorronteraRuiz_Presentació.pdf
└── README.md
```

### Directory Descriptions

| Directory | Purpose |
| :--- | :--- |
| `00_RENDER SETTINGS/` | Unity input, rendering, lighting, and project settings |
| `02_CODE/Animations/` | Character and object animation logic |
| `02_CODE/Audio/` | Audio-related scripts and systems |
| `02_CODE/CharacterSelect/` | Character selection functionality |
| `02_CODE/Economy/` | Currency, rewards, and progression logic |
| `02_CODE/GUI + HUD/` | User interface and heads-up display |
| `02_CODE/Lobby/` | Lobby and session preparation systems |
| `02_CODE/Managers/` | Shared managers and application-level orchestration |
| `02_CODE/Multiplayer/` | General multiplayer systems |
| `02_CODE/NETCODE/` | Netcode for GameObjects-specific components |
| `02_CODE/NPCs/` | Non-player character logic |
| `02_CODE/Orders/` | Customer order systems |
| `02_CODE/Player/` | Player movement and interaction logic |
| `02_CODE/SCORE/` | Scoring and performance systems |
| `02_CODE/Team Interaction/` | Cooperative interaction features |
| `02_CODE/TUTORIAL + HINTS/` | Tutorials, hints, and onboarding |
| `05_NETWORK/DYNAMICS/` | Dynamic networked objects |
| `05_NETWORK/STATICS/` | Static networked scene objects |
| `05_NETWORK/UTILITY/` | Networking utilities and helper assets |

---

## Requirements for Developers

To open and modify the project, you need:

- Unity Hub.
- Unity 6.
- A Unity Editor version compatible with the project.
- Git.
- A computer capable of running Unity's URP renderer.
- Network access for multiplayer testing.
- A compatible development environment.

Recommended tools include:

- Visual Studio.
- JetBrains Rider.
- Visual Studio Code.
- Git LFS, if required by the project's asset configuration.
- Android or desktop build tools, depending on the target platform.

The project may also require Unity packages such as:

- Universal Render Pipeline.
- Netcode for GameObjects.
- Unity Input System.
- TextMesh Pro.
- Unity Transport.
- Multiplayer-related Unity packages.

---

## Opening the Unity Project

1. Clone the repository:

```bash
git clone https://github.com/Andormix/UdA-gamedev-modular-Unity-3D-online-party-game.git
```

2. Open Unity Hub.

3. Select **Add** or **Open Project**.

4. Select the cloned repository directory.

5. Open the project using the compatible Unity 6 Editor version.

6. Allow Unity to import and index the project assets.

7. Open the relevant gameplay or lobby scene.

> If Unity reports missing packages, open the Package Manager and restore the packages required by the project.

---

## Running the Project in Unity

After opening the project:

1. Confirm that all required packages are installed.
2. Open the main gameplay or lobby scene.
3. Verify the configured input actions.
4. Check the network configuration.
5. Press **Play** in the Unity Editor.
6. Test the player controller and object interactions.
7. Start a multiplayer session if the required networking configuration is available.

For a standalone build:

1. Open **File > Build Profiles** or **File > Build Settings**.
2. Select the target platform.
3. Add the required scenes.
4. Configure the build settings.
5. Build and run the project.

The repository itself is primarily intended for development and technical review. For users who only want to play the game, use the separately distributed development build.

---

## Multiplayer Testing

To test multiplayer functionality locally:

1. Open the project in Unity.
2. Configure the required Netcode and transport settings.
3. Start one instance as the host.
4. Start one or more additional instances as clients.
5. Connect each client to the host.
6. Verify player spawning and synchronized interactions.
7. Test shared objects, orders, scores, and state transitions.

A typical local testing configuration is:

```text
Instance 1: Host
Instance 2: Client
Instance 3: Client
```

Multiplayer testing should verify:

- Player connection.
- Player disconnection.
- Player spawning.
- Ownership rules.
- Networked interactions.
- Shared order state.
- Score synchronization.
- Scene transitions.
- Static and dynamic network objects.
- Client-side visual updates.
- Shared gameplay objectives.

---

## Controls

The project uses the Unity Input System.

The input configuration is stored in:

```text
00_RENDER SETTINGS/InputSystem_Actions.inputactions
```

The exact controls may vary according to the current input action map.

Typical gameplay actions may include:

| Action | Purpose |
| :--- | :--- |
| Movement | Move the player character |
| Camera | Control the camera |
| Interact | Interact with nearby objects |
| Carry or use | Pick up, carry, or use an object |
| Submit | Confirm menus or selections |
| Pause | Open the pause menu |

Check the configured Input Action Asset in Unity for the current keyboard, controller, or other device bindings.

---

## Project Documentation

The repository includes additional documentation related to the project.

### Technical Memory

```text
Documentació completa del projecte - MEMORIA.pdf
```

The technical memory contains information about:

- Software architecture.
- System diagrams.
- C4 model diagrams.
- Finite State Machine design.
- Engineering decisions.
- Multiplayer architecture.
- Project implementation.
- Development process.
- Design patterns.
- Technical evaluation.

### Project Presentation

```text
TFB_EricTorronteraRuiz_Presentació.pdf
```

The presentation provides a visual overview of:

- The game concept.
- Gameplay objectives.
- Technical architecture.
- Software design.
- Multiplayer features.
- Development results.

### Gameplay Demonstration

Click the image below to access the gameplay demonstration:

<a href="https://drive.google.com/drive/folders/1LWDcCUO_mqptqqfOhNbFuWpDe6j5OyAp" target="_blank">
  <img
    width="1024"
    height="579"
    alt="Rush Hour gameplay demonstration"
    src="https://github.com/user-attachments/assets/3c0cada2-b745-4d69-a217-31f70ee62acc"
  />
</a>

---

## Source Code and Build Distribution

This GitHub repository is intended to expose the technical implementation of Rush Hour.

It contains:

- C# scripts.
- Unity project folders.
- Gameplay systems.
- Networking systems.
- Input configuration.
- Rendering configuration.
- Technical documentation.
- Project architecture.

The playable development build is distributed separately because:

- The repository is primarily intended for code and technical review.
- Unity project files are not the most convenient way to distribute a playable game.
- Some assets may have redistribution restrictions.
- The build may require platform-specific packaging.
- Different builds may be produced for different testing purposes.

The development build and the source repository should therefore be considered complementary project resources.

---

## Copyright and Asset Notice

Due to commercial licenses and the terms of use of the **Unity Asset Store EULA**, some project assets and multimedia resources may not be redistributed publicly.

This repository primarily serves as:

- A technical portfolio.
- A software architecture reference.
- An academic project record.
- A demonstration of gameplay systems.
- A source-code repository.
- A documentation resource.

Some proprietary assets, packages, source files, or multimedia resources may be excluded from public distribution.

The playable development build may contain assets that are not intended to be extracted, reused, or redistributed independently.

If you are a recruiter, evaluator, or technical reviewer and require access to additional materials, please contact the author directly.

---

## Future Improvements

Potential future improvements include:

- Additional maps and café environments.
- More game modes.
- Larger player sessions.
- Matchmaking and online session discovery.
- Improved lobby management.
- Dedicated server support.
- Network latency compensation.
- Reconnection support.
- Player progression.
- More customer types.
- Additional recipes and orders.
- Expanded economy systems.
- Persistent player profiles.
- Improved accessibility options.
- Gamepad and console support.
- Performance profiling and optimization.
- Automated gameplay tests.
- Automated multiplayer integration tests.
- CI/CD build pipelines.
- Production-ready multiplayer hosting.
- More advanced customer AI.
- Additional tutorials and onboarding tools.
- Improved network error handling.

---

## Academic Context

Rush Hour was developed as a **Final Degree Project in Computer Engineering** at the **Universitat d'Andorra** during the 2025–2026 academic year.

The project combines:

- Game development.
- Software architecture.
- Object-oriented programming.
- Design patterns.
- Multiplayer networking.
- Real-time interaction systems.
- Rendering pipelines.
- User interface design.
- Modular development.
- Technical documentation.

The main academic objective is to demonstrate how a complex multiplayer game can be designed using reusable, maintainable, and scalable software components.

---

## Author and Tutors

### Author

**Eric Torrontera Ruiz**

### Tutors

- **Jan Sau Batlle**
- **Josep Ribó Ferriz**

### Institution

**Universitat d'Andorra**

---

## License

This project is intended for academic, educational, and portfolio purposes.

The source code and documentation may be subject to the licenses of Unity, Unity Asset Store packages, third-party plugins, and other included dependencies.

Do not redistribute commercial assets or proprietary packages without verifying their respective licenses.

The separately distributed development build is also subject to the licensing terms of its included assets and dependencies.

---

## Repository

```text
https://github.com/Andormix/UdA-gamedev-modular-Unity-3D-online-party-game
```

<table>
  <tr>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/a61d918e-204a-4e48-a2b3-1634a18b6114" width="100%" alt="Diapositiva 1" />
    </td>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/ee486c7d-125f-4685-a181-fba04cd4cf9d" width="100%" alt="Diapositiva 2" />
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/a3271c59-aca1-4ca7-9e3a-fd8fb045bfe9" width="100%" alt="Diapositiva 3" />
    </td>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/060a9946-a9fc-4286-a5ff-45f6b8551ddc" width="100%" alt="Diapositiva 4" />
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/35a4cda8-671f-4b05-a181-7f85bfb523c7" width="100%" alt="Diapositiva 5" />
    </td>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/92ecc19c-67be-4e6c-91d6-9ecb6b606f61" width="100%" alt="Diapositiva 6" />
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/18dd8fcf-1969-4e6e-a337-ca9d548b7ba0" width="100%" alt="Diapositiva 7" />
    </td>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/5bdd4181-c618-4ff9-9c89-2917fea03d19" width="100%" alt="Diapositiva 8" />
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/bf16fa84-f591-47f5-ba9d-b08971fb5f0b" width="100%" alt="Diapositiva 9" />
    </td>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/d5131cda-b3bd-41bf-aeec-f5f2781f7f4e" width="100%" alt="Diapositiva 10" />
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/4f39139b-bdc0-4bee-a95a-106aaaa28f05" width="100%" alt="Diapositiva 11" />
    </td>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/453a09ef-0114-47cf-8c3a-e5a85eac61cb" width="100%" alt="Diapositiva 12" />
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/ba2def99-ad51-4f5f-9186-2cd838d4fc0c" width="100%" alt="Diapositiva 13" />
    </td>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/1f6d251f-5200-42df-9caf-e08838ca42d9" width="100%" alt="Diapositiva 14" />
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/f1a02167-67dd-417c-b42d-0a43d384292a" width="100%" alt="Diapositiva 15" />
    </td>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/bb1009a7-3dfc-4b4f-a6ae-0718eecfc1d0" width="100%" alt="Diapositiva 16" />
    </td>
  </tr>
</table>

<img width="1243" height="699" alt="image" src="https://github.com/user-attachments/assets/9a99f773-5e49-4746-9903-f590a9bac691" />


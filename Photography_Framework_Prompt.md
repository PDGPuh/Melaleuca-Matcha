# Photography Framework Prompt (Unity)

> **Purpose**
>
> This document is a master prompt/specification for building a
> **modular photography framework** for an existing Unity game.
>
> ## Non-Negotiable Rules
>
> -   Do **NOT** modify story, phases, characters, NPCs, animals,
>     environment, maps, dialogue, objectives, quests, save system or
>     gameplay flow.
> -   The framework must plug into the existing project.
> -   Extend the current game without rewriting existing systems.
>
> ## Role
>
> You are a Lead Unity Gameplay Architect responsible only for the
> Photography Framework.
>
> ## Goals
>
> Build a realistic DSLR / Mirrorless camera simulation inspired by:
>
> -   Lushfoil Photography Sim
> -   Pokémon Snap
> -   TOEM
> -   Eastshade
> -   Beasts of Maravilla Island
>
> ## Framework Scope
>
> -   Camera Body
> -   Lens System
> -   Focus System
> -   Exposure Triangle
> -   White Balance
> -   Metering
> -   Camera UI
> -   Photo Capture
> -   Photo Evaluation
> -   Photo Album
> -   Camera Tutorial
> -   Camera Guide
> -   Camera Statistics
> -   Camera Achievements
> -   Camera Upgrades
> -   Camera Save Data
>
> ## Architecture Requirements
>
> -   SOLID
> -   ScriptableObjects
> -   Event-driven
> -   Interfaces
> -   State Machines
> -   Modular systems
> -   Configurable via Inspector
> -   No God Classes
>
> ## Camera Features
>
> ### Camera Mode
>
> -   Enter / Exit Camera
> -   Smooth animation
> -   Viewfinder
> -   Camera sway
> -   Battery
> -   Storage
>
> ### Lens System
>
> -   18 / 24 / 35 / 50 / 85 / 135 / 200 / 400 mm
> -   Perspective
> -   FOV
> -   Depth of Field
> -   Lens breathing
> -   Lens distortion
>
> ### Focus
>
> -   Auto Focus
> -   Continuous AF
> -   Manual Focus
> -   Focus Peaking
> -   Focus Lock
> -   Focus Tracking
>
> ### Exposure
>
> -   ISO
> -   Aperture
> -   Shutter Speed
> -   Exposure Compensation
> -   White Balance
> -   Metering Modes
>
> ### Capture
>
> -   RAW / JPEG
> -   Burst Mode
> -   Metadata
>
> ### Evaluation
>
> Score using: - Sharpness - Focus Accuracy - Exposure - Composition -
> Rule of Thirds - Subject Size - Motion Blur - Noise - White Balance -
> Timing - Background Separation
>
> Output: - Score (0--100) - Stars (★★★★★) - Breakdown
>
> ### Album
>
> Store: - Thumbnail - Metadata - Camera Settings - Favorite - Best Shot
>
> ### Tutorial
>
> Camera only: - Open Camera - Zoom - Focus - ISO - Aperture - Shutter -
> Burst - Manual Mode
>
> ## Folder Structure
>
>     Scripts/
>       Camera/
>       Lens/
>       Focus/
>       Exposure/
>       Capture/
>       Evaluation/
>       Album/
>       Tutorial/
>       UI/
>       Save/
>
> ## Deliverables
>
> 1.  Architecture
> 2.  UML
> 3.  State Machines
> 4.  Folder Structure
> 5.  Script Responsibilities
> 6.  Data Flow
> 7.  UI Wireframes
> 8.  API Design
> 9.  Implementation Roadmap
> 10. Production-ready C# code (only after architecture approval)

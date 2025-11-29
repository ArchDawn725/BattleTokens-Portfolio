# BattleTokens-Portfolio

***Battle Tokens (2025)***

Turn-Based Multiplayer Strategy • Mobile + Steam • Free to Play

Steam Page: https://store.steampowered.com/app/3585790/Battle_Tokens/

Google Play: https://play.google.com/store/apps/details?id=com.archdawn.battletokens&pcampaignid=web_share

Availability: Free to Play (Optional DLC)

**⭐ Project Overview**

Battle Tokens is my most recent release and the clearest example of my current engineering standards, coding style, and development philosophy. Released on October 9th, 2025, this 2D turn-based multiplayer tactics game began as a personal idea created for my wedding—something I could use to play D&D-like combat with friends without needing to run a session. The concept grew far beyond its original scope and became a fully featured released product for both Steam and Mobile.

This is the project that best showcases my modern programming level:

Zero god scripts

Strict separation of responsibilities

Single-purpose scripts and clean modularity

Async-based turn flow

High readability and consistency

Clear architecture and maintainability

Production-quality multiplayer implementation

The only remaining flaw is mild overuse of public variables in late-stage systems due to rapid feature expansion. Otherwise, Battle Tokens is the strongest representation of my current skill set.

**🎮 Gameplay Summary**

Battle Tokens captures the tabletop combat feel of D&D and other turn-based RPGs but delivers it in a lightweight, fast-paced digital form playable with up to 9 total players.

*Start Menu & Progression*

The game opens with music composed by the same musician from Line Wars and transitions into a clean main menu allowing players to:

Visit the DLC shop (mobile only)

Buy items with in-game gold

Change player name

Adjust settings (language, speed, volume, brightness, etc.)

*Multiplayer Lobby System*

Using Unity Netcode for GameObjects, Lobby, and Relay, players can:

Browse open lobbies

Create private/public lobbies

Set:

Player count

Number of AI helpers

Quest selection

Privacy settings

Inside the lobby, character classes appear around the table in a circular layout, and players select from base classes or DLC classes. The host can also assign classes to AI helpers.

*Upgrade Phase*

Before each encounter:

Players spend upgrade points equal to their class level

Points can unlock new abilities or boost core stats

Everyone upgrades simultaneously

*Combat Phase*

Combat unfolds across two grids:

Ally grid (player placement)

Enemy grid (enemy placement)

Turn order cycles through:

Players

AI allies

Enemies

Each class has up to 5 unique abilities, with a wide variety of damage types, buffs, debuffs, crowd control, and targeting patterns. Position matters—tokens closer to the enemy are more likely to be targeted.

The combat loop progresses through multiple waves until the final boss fight, completing the quest.

*Content & Visuals*

~110+ unique character tokens and designs

Cartoon-inspired clean visual style

Mix of personal artwork and contracted 2D art

Smooth UI animations and polished presentation

**🧩 Key Features**

Fully multiplayer turn-based combat (up to 9 players)

Cross-platform: Steam + Android

Free-to-play

Clean async-based turn sequencing

Multiple quests and difficulty curves

Fast-paced but deeply tactical combat

110+ token designs and ability variations

Polished UI and responsive menus

DLC support (mobile)

AI helper characters with dynamic logic

P2P multiplayer via Unity Relay

Frequent post-launch updates, patches, and additions

**🏗️ Architecture Overview**

Battle Tokens is your most modern codebase and demonstrates:

*✔ Clean Single-Purpose Script Architecture*

Every class has a focused responsibility. No script performs multiple unrelated tasks.

*✔ Async-Based Turn System*

Turns flow smoothly using async/await:

Reduces complexity

Prevents nested coroutine chains

Eliminates frame delay issues

Makes the turn order extremely clear and maintainable

*✔ Modular Character & Ability Framework*

Classes are built using:

Base class templates

Modular ability definitions

Data-driven targeting rules

Flexible stat systems

*✔ Fully Networked Architecture*

Uses Unity NGO (Netcode for GameObjects) along with:

Lobby service

Relay service

Host migration handling

Syncing turn states, actions, and abilities

*✔ Expanded Coding Standards*

Consistent naming

Reusable utilities

Clean UI separation

Virtually no Update() spam

Event-driven and async-driven patterns

Script decomposition during late development

*✔ Very Maintainable Codebase*

Battle Tokens is your most industry-aligned repository in terms of:

Structure

Readability

Modularity

Networking discipline

Feature scalability

**🗂️ Key Scripts to Review**

(Filenames can be matched once the repo is uploaded.)

*Core*

TurnFlowController (async)

GameStateManager

LobbyManager / NetworkManager

*Combat*

AbilityExecutor

TargetingSystem

GridPlacementController

DamageResolver

StatSystem

*UI*

LobbyUIController

UpgradeMenuController

BattleUIController

SettingsUI

ShopUI / DLCUI

*Systems*

QuestSystem

WaveSystem

AICompanionController

EnemyBehaviorSystem

*Networking*

MultiplayerActionsRelay

PlayerSyncManager

TokenSyncController

*Utilities*

AsyncUtilities

AudioManager

LocalizationSystem

SaveManager (player data)

**🧪 Development Notes**
*Post-Launch Support*

After launch, the game received:

Multiple patches

New free content

A full DLC pack

UI improvements

Bug fixes

Expanded character classes

*Scaling & Performance*

Designed for mobile first

Minimal memory footprint

Lightweight 2D assets

Async turn flow prevents frame spikes

Clean networking reduces desync issues

*User Experience*

New players quickly understood the loop

Fast, satisfying tactical turns

Mobile-friendly interactions

**🚧 Why This Project Matters**

Battle Tokens is the best demonstration of your 2025-level programming quality. It shows:

Mastery of Unity’s modern networking stack

Fully async game flow

Strict architecture and modularity

Professional delivery on Steam & Mobile

Clean code suitable for mid/senior-level review

A full product lifecycle (development → polish → release → updates)

Production-ready UI and UX

Strongest example of system scalability and maintainability

Ability to integrate store systems, P2P multiplayer, DLC features, and more

Among all your projects, this is the one that a studio like Ludeon (or any senior-level Unity employer) should review first.

**📚 Lessons Learned**

Strict modularity pays off massively in later development

Async solves many turn-flow and timing issues

Networking must be designed early to avoid rewrites

Public variables should be minimized in future work

Mixing mobile-first & PC-first design requires careful UI planning

Maintaining clean architecture speeds up post-launch support

**🛠️ Tech Stack**

Unity 6.xx

C#

Async/await architecture

Unity Netcode for GameObjects (NGO)

Lobby & Relay

2D sprite-based rendering

Google Play + Steam release pipelines

ScriptableObject data containers

Custom audio + visual assets

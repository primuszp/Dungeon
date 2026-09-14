# The Sunken Keep

A modern, atmospheric dark-fantasy dungeon renderer built with .NET 8 and OpenTK 4.

## Features

- OpenGL 3.3 core-profile renderer using indexed VAO/VBO/EBO meshes
- GLSL 330 normal mapping with localized, flickering dynamic lights
- Dense distance-based dungeon fog and HDR tone mapping
- Image-generated wet basalt albedo texture
- Runtime normal-map generation from the albedo texture
- Deterministic maze generation with hand-shaped chambers
- Collision detection and frame-rate-independent FPS movement
- Fullscreen support and captured mouse look

## Requirements

- Windows
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An OpenGL 3.3-compatible graphics device

NuGet dependencies are restored automatically during the build.

## Build and run

From the repository root:

```powershell
dotnet run --project Dungeon/Dungeon.csproj -c Release
```

To create a Release build without launching the game:

```powershell
dotnet build Dungeon.sln -c Release
```

The executable is generated at:

```text
Dungeon/bin/Release/net8.0-windows/Dungeon.exe
```

## Controls

| Input | Action |
| --- | --- |
| `W` / `S` | Move forward / backward |
| `A` / `D` | Strafe left / right |
| Mouse | Look around |
| `Shift` | Sprint |
| `F11` | Toggle fullscreen |
| `Esc` | Release the mouse; press again to quit |
| Left click | Capture the mouse again |

## Material workflow

The source albedo is stored at `Dungeon/Assets/dark_basalt.png`. A tangent-space normal map is generated from it when the game starts, so replacing the albedo at the same path automatically produces matching surface detail without requiring a separate normal-map asset.

## Project structure

```text
Dungeon/
├── Assets/dark_basalt.png
├── Dungeon.csproj
├── DungeonGame.cs
└── Program.cs
```

# AnimeGames Bar

Windows desktop monitor for anime game account status.

## First MVP

The first adapter targets Arknights through the official Skland community surface.

Tracked fields:

- Sanity
- Base drones
- Training room operator, skill, and remaining time
- Weekly annihilation progress
- Stationary Security Service reward progress

## Security

Account-specific tokens, cookies, and Skland credentials are stored locally in Windows Credential Locker through `Windows.Security.Credentials.PasswordVault`.

## Project Layout

- `src/AnimeGamesBar.App` - WinUI 3 desktop app
- `src/AnimeGamesBar.App/Services/Arknights` - Arknights monitor adapter
- `src/AnimeGamesBar.App/Services/Skland` - Skland HTTP/auth/credential infrastructure

## Build Notes

This repository expects Visual Studio 2022 with .NET desktop development, Windows application development, and the Windows App SDK toolchain. If `dotnet` is not in PATH, open the solution from Visual Studio or install/configure the .NET SDK first.

On this machine, the local build script uses:

- .NET SDK: `D:\Users\unnat\dotnet`
- Visual Studio Build Tools: `D:\Program Files\Microsoft Visual Studio\2022\BuildTools`
- NuGet package cache: `D:\Users\unnat\.nuget\packages`

```powershell
.\scripts\build.ps1
.\scripts\run.ps1
```

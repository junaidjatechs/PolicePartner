# Police Partner — LSPDFR Plugin

A standalone Police Partner mod for GTA V LSPDFR. Spawn a partner who follows you on patrol, covers the passenger side during traffic stops, watches detained suspects, and draws their weapon on threats.

---

## Requirements

| Dependency | Where to get |
|---|---|
| GTA V | Steam / Rockstar |
| RAGEPluginHook | [ragepluginhook.net](https://ragepluginhook.net) |
| LSPDFR | [lcpdfr.com](https://www.lcpdfr.com) |
| RageNativeUI | [github.com/alexguirre/RAGENativeUI](https://github.com/alexguirre/RAGENativeUI/releases) — drop `RAGENativeUI.dll` in your GTA V root |

---

## Installation

1. Build the project in Visual Studio (see below).
2. Copy `PolicePartner.dll` → `GTA V\Plugins\`
3. Copy `PolicePartner.ini` → `GTA V\Plugins\PolicePartner\`
4. Make sure `LemonUI.RPH.dll` is in `GTA V\Plugins\`

---

## Building from Source

1. Open `PolicePartner.csproj` in Visual Studio 2019/2022.
2. Edit the `<GTAVPath>` property in the `.csproj` to match your GTA V folder.
3. Build → Release → x64.
4. Copy output `.dll` as above.

---

## Default Controls

| Key | Action |
|---|---|
| **F6** | Open / close the Police Partner menu |
| **Y** | Force partner to regroup with player |

*(Change in `PolicePartner.ini`)*

---

## Features

### Menu (F6)
- Choose **Male** or **Female** partner
- Select specific **uniform/model** (LSPD, Highway Patrol, Sheriff, Park Ranger, SWAT)
- **Spawn**, **Dismiss**, or **Regroup** partner

### Following
- Partner stays close behind you on foot
- Press **Y** anytime to force them to regroup

### Traffic Stops (auto-detected via LSPDFR API)
- When you initiate a pullover, partner automatically moves to the **passenger side** of the stopped vehicle
- Partner **watches** the nearest occupant / detained ped
- If the suspect **runs or becomes hostile** → partner draws weapon and aims
- When the stop ends → partner automatically rejoins you

---

## INI Options

```ini
[Keys]
MenuKey=F6        ; Menu toggle key
FollowKey=Y       ; Regroup key

[Settings]
SpawnDistance=3.0         ; Metres ahead of player for spawn point
AutoEquipWeapon=true      ; Give partner a pistol on spawn
DefaultWeapon=WEAPON_PISTOL  ; Any WEAPON_* hash
```

---

## Folder Structure

```
GTA V\
├── RAGENativeUI.dll             ← dependency (GTA V root)
├── Plugins\
│   ├── PolicePartner.dll        ← compiled plugin
│   └── PolicePartner\
│       └── PolicePartner.ini    ← config
```

---

## Known Limitations / Future Ideas

- [ ] Partner vehicle (spawn in a cop car together)
- [ ] Voice/radio lines via LSPDFR audio
- [ ] Backup callout integration
- [ ] Persistent partner across sessions (save/load name)
- [ ] Partner health bar HUD element

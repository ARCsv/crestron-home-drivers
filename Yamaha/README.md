# Yamaha R-N2000A → Crestron Home (YXC)

Crestron Home driver for Jonathan’s Yamaha **R-N2000A** (“Audio Room”). The unit ignores classic **YNCA** (TCP 50000). It speaks **Yamaha Extended Control (YXC)**: HTTP GET, JSON, port **80**.

**Live unit:** `192.168.78.22` (wired, static, API 2.17). Proven `2026-09-06`.

## What we are building (two steps)

**Step 1 (now) — room basics.** Power, volume, mute, input. This is what you use day to day.

**Step 2 (later) — MusicCast in Home.** Now-playing, transport, tuner, presets. The YXC client and CLI already call those endpoints; they are not on the Home receiver UI yet.

Keep using the MusicCast app for browsing until step 2.

## Layout

| Path | Role |
|---|---|
| `src/Yamaha.Yxc` | Protocol client. First place to change command URLs or JSON fields. |
| `src/Yamaha.Yxc.Cli` | Talk to the real receiver without Crestron. |
| `src/Yamaha.RN2000A.Crestron` | Crestron Certified Driver (`ABasicAVReceiver` + `ITcp` on port 80). |
| `tests/Yamaha.Yxc.Tests` | Mock-HTTP unit tests. |
| `docs/live-*.json` | Snapshots from this R-N2000A. |
| `tools/probe-receiver.ps1` | Same handshake from PowerShell. |

## This receiver’s facts

From `getDeviceInfo` / `getStatus` / `getFeatures`:

- Model `R-N2000A`, network name **Audio Room**
- One zone: `main`
- Volume **0–154**, step **1** (Crestron Home percent 0–100 is scaled in `VolumeScaler`)
- Current example: power on, volume 114, mute off, input `optical1` renamed **TV Audio**
- **No HDMI input ID.** Video/TV is `tv`. Analog CD is `line_cd`. USB is `usb_dac`.

Inputs the unit reports: `optical1`, `optical2`, `coaxial`, `line1`, `line2`, `line_cd`, `phono`, `usb_dac`, `tv`, `tuner`, `bluetooth`, `airplay`, `spotify`, `tidal`, `qobuz`, `deezer`, `net_radio`, `server`, `usb` services, `mc_link`, etc.

## How YXC is used

Base URL: `http://192.168.78.22/YamahaExtendedControl/v1/`

| Action | Request |
|---|---|
| Identity | `GET system/getDeviceInfo` |
| Inputs + volume range | `GET system/getFeatures` |
| Zone state | `GET main/getStatus` |
| Power | `GET main/setPower?power=on\|standby` |
| Volume | `GET main/setVolume?volume={n}\|up\|down` |
| Mute | `GET main/setMute?enable=true\|false` |
| Input | `GET main/setInput?input={id}` |
| Now playing | `GET netusb/getPlayInfo` |

Success is `"response_code": 0`. Network Standby should stay on.

## Run

```powershell
cd "C:\cursor Crestron project\Yamaha"
dotnet test YamahaRn2000a.sln
dotnet run --project src/Yamaha.Yxc.Cli -- 192.168.78.22 info
dotnet run --project src/Yamaha.Yxc.Cli -- 192.168.78.22 features
dotnet run --project src/Yamaha.Yxc.Cli -- 192.168.78.22 status
.\tools\probe-receiver.ps1
```

Commands that **change** the receiver (only when you want them):

```powershell
dotnet run --project src/Yamaha.Yxc.Cli -- 192.168.78.22 mute on
dotnet run --project src/Yamaha.Yxc.Cli -- 192.168.78.22 volume 114
dotnet run --project src/Yamaha.Yxc.Cli -- 192.168.78.22 input optical1
```

## Crestron Home package

ManifestUtil is:

`C:\Crestron\crestron_drivers_sdk_28.0000.0015\ManifestUtil\ManifestUtil.exe`

A Release build runs it and copies the package to `pkg\`:

```powershell
cd "C:\cursor Crestron project\Yamaha"
dotnet build src\Yamaha.RN2000A.Crestron\Yamaha.RN2000A.Crestron.csproj -c Release
```

Output: `pkg\Yamaha.RN2000A.Crestron.pkg`

1. SFTP that `.pkg` to the processor: `\User\ThirdPartyDrivers\Import`
2. Setup app → room → Drivers → Yamaha **R-N2000A**
3. IP `192.168.78.22`, port **80**

Home sees an **AV Receiver**. `ITcp` is how Setup asks for IP/port; `YxcHttpTransport` issues HTTP GETs instead of a YNCA TCP socket.

## First change

- Command URLs / JSON: `src/Yamaha.Yxc/YxcClient.cs`
- Percent volume and poll feedback: `src/Yamaha.RN2000A.Crestron/YamahaYxcProtocol.cs`

Crestron SDK libraries are licensed by Crestron. The YXC client and driver glue are for this system.

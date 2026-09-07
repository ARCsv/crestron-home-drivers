# Yamaha R-N2000A (YXC)

Crestron Home AV Receiver driver. This model ignores **YNCA** (TCP 50000) and speaks **Yamaha Extended Control (YXC)** on HTTP port **80**.

See the [repository README](../README.md) for what works, sideload steps, SDK versions, and Home limitations.

## Layout

| Path | Role |
|---|---|
| `src/Yamaha.Yxc` | YXC HTTP client |
| `src/Yamaha.Yxc.Cli` | Talk to a receiver without Crestron (`dotnet run --project src/Yamaha.Yxc.Cli -- <ip> info`) |
| `src/Yamaha.RN2000A.Crestron` | Home driver (`ABasicAVReceiver` + `ITcp` port 80, HTTP not YNCA) |
| `tests/Yamaha.Yxc.Tests` | Mock-HTTP unit tests |
| `docs/live-*.json` | Example YXC responses (identifiers redacted) |
| `pkg/` | Built `.pkg` |

Volume range on this chassis is **0–154**, step 1. Home 0–100 is scaled in `VolumeScaler`. There is no HDMI input ID; TV is `tv`, CD is `line_cd`, Roon analog is `usb_dac`.

# Roon Core

Crestron Home packages for a Roon Core zone.

- `pkg/Roon.Crestron.pkg` — Streaming Player (room source; route to Yamaha USB DAC)
- `pkg/Roon.Ui.Crestron.pkg` — Media Player now-playing tile (not a source)

See the [repository README](../README.md) for setup, the two-package split, and Home SDK limits.

Build:

```powershell
dotnet build src\Roon.Crestron\Roon.Crestron.csproj -c Release
dotnet build src\Roon.Ui.Crestron\Roon.Ui.Crestron.csproj -c Release
```

Enable the matching extension in Roon → Settings → Extensions. Default API port is **9330**. Volume stays on the Yamaha driver.

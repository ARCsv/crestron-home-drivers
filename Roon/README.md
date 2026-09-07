# Roon Core → Crestron Home

Crestron Home **Streaming Player** (Video Server) driver for Jonathan’s Roon Core. That native type is what Home can **source-route** to the Yamaha.

**Live Core:** `192.168.78.12`, API port **9330**. Zone **Living Room**.

Media Player in Home is an extension tile. It does not show up in source routing. This package stays a Video Server so it can be routed to USB DAC.

## Run

```powershell
cd "C:\cursor Crestron project\Roon"
dotnet build src\Roon.Crestron\Roon.Crestron.csproj -c Release
```

Output: `pkg\Roon.Crestron.pkg` (`1.0002.0000`)

1. SFTP to `\User\ThirdPartyDrivers\Import`
2. Remove any Media Player Roon device
3. Add **Streaming Player → Roon Core (Cadence Works/Cursor)**
4. IP `192.168.78.12`, port **9330**
5. Source-route it to the Yamaha **USB DAC** input
6. Enable **Cadence Works Crestron Roon** in Roon if asked

Volume stays on the Yamaha driver.

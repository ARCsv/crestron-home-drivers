using Yamaha.Yxc;

if (args.Length < 2)
{
    Console.WriteLine("""
        Yamaha R-N2000A YXC probe

        Usage:
          Yamaha.Yxc.Cli <ip> info
          Yamaha.Yxc.Cli <ip> features
          Yamaha.Yxc.Cli <ip> status
          Yamaha.Yxc.Cli <ip> now
          Yamaha.Yxc.Cli <ip> power on|standby|toggle
          Yamaha.Yxc.Cli <ip> volume <0-n|up|down>
          Yamaha.Yxc.Cli <ip> mute on|off
          Yamaha.Yxc.Cli <ip> input <id>
          Yamaha.Yxc.Cli <ip> play|stop|pause|next|previous
        """);
    return 1;
}

var host = args[0];
var command = args[1].ToLowerInvariant();
using var client = new YxcClient(host);

try
{
    switch (command)
    {
        case "info":
            var info = await client.GetDeviceInfoAsync();
            Console.WriteLine($"{info.ModelName}  api={info.ApiVersion}  id={info.DeviceId}");
            Console.WriteLine(info.RawJson);
            break;
        case "features":
            var features = await client.GetFeaturesAsync();
            Console.WriteLine($"volume {features.VolumeMin}..{features.VolumeMax} step {features.VolumeStep}");
            foreach (var id in features.InputIds)
            {
                Console.WriteLine($"  {id}\t{InputCatalog.Label(id)}");
            }
            break;
        case "status":
            var status = await client.GetStatusAsync();
            Console.WriteLine($"power={(status.PowerOn ? "on" : "standby")}  vol={status.Volume}/{status.MaxVolume}  mute={status.Mute}  input={status.Input} ({status.InputText})");
            Console.WriteLine(status.RawJson);
            break;
        case "now":
            var now = await client.GetPlayInfoAsync();
            Console.WriteLine($"{now.Playback}  {now.Artist} — {now.Track}  ({now.Album})");
            break;
        case "power":
            await client.SetPowerAsync(Need(args, 2));
            Console.WriteLine("ok");
            break;
        case "volume":
            var vol = Need(args, 2);
            if (vol is "up" or "down")
            {
                await client.NudgeVolumeAsync(vol);
            }
            else
            {
                await client.SetVolumeAsync(int.Parse(vol));
            }
            Console.WriteLine("ok");
            break;
        case "mute":
            await client.SetMuteAsync(Need(args, 2).Equals("on", StringComparison.OrdinalIgnoreCase));
            Console.WriteLine("ok");
            break;
        case "input":
            await client.SetInputAsync(Need(args, 2));
            Console.WriteLine("ok");
            break;
        case "play":
        case "stop":
        case "pause":
        case "next":
        case "previous":
            await client.SetPlaybackAsync(command);
            Console.WriteLine("ok");
            break;
        default:
            Console.Error.WriteLine("Unknown command: " + command);
            return 1;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

return 0;

static string Need(string[] a, int index)
{
    if (a.Length <= index)
    {
        throw new InvalidOperationException("Missing argument.");
    }

    return a[index];
}

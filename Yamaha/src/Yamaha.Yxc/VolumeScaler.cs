using System;

namespace Yamaha.Yxc
{
    public static class VolumeScaler
    {
        /// <summary>
        /// Yamaha 0–154 is linear in device units but not in loudness.
        /// Crestron 50% should land near a normal listening level (~110), not 77.
        /// </summary>
        public const double UiCurve = 0.42;

        public static uint ToPercent(int deviceVolume, int min, int max)
        {
            return ToUiPercent(deviceVolume, min, max);
        }

        public static int FromPercent(uint percent, int min, int max, int step)
        {
            return FromUiPercent(percent, min, max, step);
        }

        public static uint ToUiPercent(int deviceVolume, int min, int max)
        {
            if (max <= min)
            {
                return 0;
            }

            var clamped = Math.Max(min, Math.Min(max, deviceVolume));
            var linear = (clamped - min) / (double)(max - min);
            var ui = 100.0 * Math.Pow(linear, 1.0 / UiCurve);
            if (ui < 0)
            {
                return 0;
            }

            if (ui > 100)
            {
                return 100;
            }

            return (uint)Math.Round(ui);
        }

        public static int FromUiPercent(uint percent, int min, int max, int step)
        {
            if (step < 1)
            {
                step = 1;
            }

            if (max <= min)
            {
                return min;
            }

            var p = NormalizeUiPercent(percent);
            var linear = Math.Pow(p / 100.0, UiCurve);
            var raw = min + (int)Math.Round(linear * (max - min));
            var aligned = min + ((raw - min) / step) * step;
            if (aligned > max)
            {
                aligned = max;
            }

            if (aligned < min)
            {
                aligned = min;
            }

            return aligned;
        }

        public static double NormalizeUiPercent(uint volume)
        {
            if (volume <= 100)
            {
                return volume;
            }

            return volume * 100.0 / 65535.0;
        }
    }
}

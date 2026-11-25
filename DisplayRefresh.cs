using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace Kartamania
{
    public static class DisplayRefresh
    {
        const int CurrentSettings = -1;
        const int SizeFieldOffset = 68;
        const int WidthOffset = 172;
        const int HeightOffset = 176;
        const int FrequencyOffset = 184;
        const int DevModeSize = 220;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool EnumDisplaySettings(string deviceName, int modeNum, byte[] devMode);

        static bool Read(int mode, out int width, out int height, out int frequency)
        {
            width = height = frequency = 0;
            try
            {
                var dm = new byte[256];
                BitConverter.GetBytes((short)DevModeSize).CopyTo(dm, SizeFieldOffset);
                if (!EnumDisplaySettings(null, mode, dm)) return false;
                width = BitConverter.ToInt32(dm, WidthOffset);
                height = BitConverter.ToInt32(dm, HeightOffset);
                frequency = BitConverter.ToInt32(dm, FrequencyOffset);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static int Current()
        {
            int w, h, r;
            return Read(CurrentSettings, out w, out h, out r) ? r : 0;
        }

        public static Dictionary<Point, int> AllModes()
        {
            var result = new Dictionary<Point, int>();
            for (int i = 0; ; i++)
            {
                int w, h, r;
                if (!Read(i, out w, out h, out r)) break;
                if (w < 1024 || h < 576 || r <= 0) continue;
                var p = new Point(w, h);
                int old;
                if (!result.TryGetValue(p, out old) || r > old) result[p] = r;
            }
            return result;
        }
    }
}

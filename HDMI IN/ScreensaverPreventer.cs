using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HDMI_IN
{
    internal class ScreensaverPreventer
    {
        [DllImport("user32.dll")]
        private static extern bool SetThreadExecutionState(uint esFlags);

        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;

        public static void PreventScreensaver()
        {
            SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED);
        }

        public static void AllowScreensaver()
        {
            SetThreadExecutionState(ES_CONTINUOUS);
        }
    }
}

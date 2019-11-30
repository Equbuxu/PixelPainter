using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GUIPixelPainter.GUI
{
    class LicenseHelper
    {
        public static string GetHwId()
        {
            return "1234";
        }

        public static string LoadSavedKey()
        {
            return "savedkey";
        }

        public static bool CheckKey(string hwId, string key)
        {
            return true;
        }
    }
}

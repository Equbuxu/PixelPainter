using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GUIPixelPainter
{
    class Logger
    {
        private static string GetTime()
        {
            DateTime time = DateTime.Now;
            return string.Format("[{0:00}.{1:00}.{2:00}:{3:000}] ", time.Hour, time.Minute, time.Second, time.Millisecond);
        }

        public static void Info(string text, params object[] args)
        {
            string result = string.Format(text, args);
            string final = GetTime() + result;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(final);
        }

        public static void Warning(string text, params object[] args)
        {
            string result = string.Format(text, args);
            string final = GetTime() + result;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(final);
        }

        public static void Error(string text, params object[] args)
        {
            string result = string.Format(text, args);
            string final = GetTime() + result;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(final);
        }
    }
}

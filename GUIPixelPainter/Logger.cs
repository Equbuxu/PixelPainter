using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GUIPixelPainter
{
    public class Logger
    {
        private static Logger instance;

        public static Logger Instance
        {
            get
            {
                if (instance == null)
                    instance = new Logger();
                return instance;
            }
        }

        public static void Info(string text, params object[] args)
        {
            Instance.LogInfo(text, args);
        }

        public static void Warning(string text, params object[] args)
        {
            Instance.LogWarning(text, args);
        }

        public static void Error(string text, params object[] args)
        {
            Instance.LogError(text, args);
        }

        private StreamWriter output = null;
        private FileStream latestFile = null;

        private string GetTime()
        {
            DateTime time = DateTime.Now;
            return string.Format("[{0:00}.{1:00}.{2:00}:{3:000}] ", time.Hour, time.Minute, time.Second, time.Millisecond);
        }

        private Logger()
        {
#if DEBUG
            Directory.CreateDirectory("logs");
            latestFile = File.Create("logs/" + DateTime.Now.ToString("dd-mm-yyyy__hh-mm-ss") + ".txt");
            output = new StreamWriter(latestFile);
#endif
        }

        public void LogInfo(string text, params object[] args)
        {
            string result;
            if (args.Length > 0)
                result = string.Format(text, args);
            else
                result = text;

            string final = GetTime() + result;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(final);
            WriteFile(final);
        }

        public void LogWarning(string text, params object[] args)
        {
            string result;
            if (args.Length > 0)
                result = string.Format(text, args);
            else
                result = text;
            string final = GetTime() + result;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(final);
            WriteFile(final);
        }

        public void LogError(string text, params object[] args)
        {
            string result;
            if (args.Length > 0)
                result = string.Format(text, args);
            else
                result = text;
            string final = GetTime() + result;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(final);
            WriteFile(final);
        }

        private void WriteFile(string text)
        {
            if (output != null)
            {
                output.WriteLine(text);
                output.Flush();
            }
        }

    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GUIPixelPainter
{
    public class ArgsParser
    {
        bool parsed = false;
        string[] storedArgs;
        Dictionary<ArgName, Dictionary<string, object>> result;

        private static Dictionary<ArgName, Dictionary<string, Type>> attributeTypes = new Dictionary<ArgName, Dictionary<string, Type>>()
        {
            {
                ArgName.Timelapse, new Dictionary<string, Type>()
                {
                    {"fps", typeof(int) },
                    {"speed", typeof(double) },
                    {"time", typeof(TimeSpan) }
                }
            },
            {
                ArgName.Canvas, new Dictionary<string, Type>()
                {
                }
            },
            {
                ArgName.NoUsers, new Dictionary<string, Type>()
                {
                }
            },
        };

        public enum ArgName
        {
            Timelapse, Canvas, Unknown, NoUsers
        }

        public ArgsParser(string[] args)
        {
            this.storedArgs = args;
        }

        public Dictionary<ArgName, Dictionary<string, object>> Parse()
        {
            if (parsed)
                return result;
            Dictionary<ArgName, Dictionary<string, object>> localResult = new Dictionary<ArgName, Dictionary<string, object>>();
            var segments = SeparateSegments(storedArgs);

            foreach (var arg in segments)
            {
                ArgName name = ParseArgName(arg);
                if (name == ArgName.Unknown)
                    continue;
                if (localResult.ContainsKey(name))
                    continue;
                Dictionary<string, string> attributes = SeparateAttributes(arg);
                Dictionary<string, object> parsedAttrs = ParseAttributes(name, attributes);
                localResult.Add(name, parsedAttrs);
            }
            result = localResult;
            return result;
        }

        private ArgName ParseArgName(List<string> segment)
        {
            string actualName = segment[0].Remove(0, 1);
            //get around the issue where numeric values are being successfully parsed even though they aren't in the enum
            if (actualName.Where(a => char.IsDigit(a)).Count() > 0)
                return ArgName.Unknown;
            try
            {
                return (ArgName)Enum.Parse(typeof(ArgName), actualName, true);
            }
            catch (ArgumentException)
            {
                return ArgName.Unknown;
            }
        }

        private Dictionary<string, object> ParseAttributes(ArgName arg, Dictionary<string, string> attrs)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            foreach (KeyValuePair<string, string> pair in attrs)
            {
                if (pair.Value == null)
                    result.Add(pair.Key, null);
                else if (attributeTypes[arg].ContainsKey(pair.Key))
                {
                    Type type = attributeTypes[arg][pair.Key];
                    object attrValue = ParseType(type, pair.Value);
                    result.Add(pair.Key, attrValue);
                }
            }
            return result;
        }

        private object ParseType(Type type, string str)
        {
            if (type == typeof(int))
            {
                int result;
                if (int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                    return result;
                else
                    return null;
            }
            else if (type == typeof(double))
            {
                double result;
                if (double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                    return result;
                else
                    return null;
            }
            else if (type == typeof(TimeSpan))
            {
                TimeSpan result;
                if (TimeSpan.TryParse(str, CultureInfo.InvariantCulture, out result))
                    return result;
                else
                    return null;
            }
            throw new Exception("unknown type");
        }


        private Dictionary<string, string> SeparateAttributes(List<string> segment)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            for (int i = 1; i < segment.Count; i++)
            {
                if (segment[i].Contains("="))
                {
                    string[] attributes = segment[i].Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                    if (attributes.Length != 2)
                        continue;
                    result.Add(attributes[0], attributes[1]);
                }
                else
                {
                    result.Add(segment[i], null);
                }
            }
            return result;
        }

        private List<List<string>> SeparateSegments(string[] args)
        {
            List<List<string>> segments = new List<List<string>>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-"))
                    segments.Add(new List<string>());
                if (segments.Count > 0)
                    segments[segments.Count - 1].Add(args[i]);
            }

            return segments;
        }
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GUIPixelPainter
{
    class ArgsProcessor
    {
        private Dictionary<ArgsParser.ArgName, Dictionary<string, object>> savedArgs;
        private GUIDataExchange dataExchange;
        public ArgsProcessor(Dictionary<ArgsParser.ArgName, Dictionary<string, object>> args, GUIDataExchange dataExchange)
        {
            this.savedArgs = args;
            this.dataExchange = dataExchange;
        }

        public void ApplyArgs()
        {
            foreach (var pair in savedArgs)
            {
                switch (pair.Key)
                {
                    case ArgsParser.ArgName.Canvas:
                        if (pair.Value.Count != 1)
                            continue;
                        int id;
                        if (!int.TryParse(pair.Value.First().Key, out id))
                            continue;
                        dataExchange.PushCanvasId(id);
                        break;
                    case ArgsParser.ArgName.NoUsers:
                        dataExchange.PushDisableAllUsers();
                        dataExchange.PushEnqueueTimelapseStart();
                        break;
                    case ArgsParser.ArgName.Timelapse:
                        if (!pair.Value.ContainsKey("fps") || !pair.Value.ContainsKey("speed") || !pair.Value.ContainsKey("time"))
                            continue;
                        dataExchange.PushTimelapseSettings((int)pair.Value["fps"], (double)pair.Value["speed"], (TimeSpan)pair.Value["time"], pair.Value.ContainsKey("restart"));
                        break;
                }
            }
        }


    }
}

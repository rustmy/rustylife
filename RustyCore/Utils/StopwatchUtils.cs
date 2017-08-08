using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RustyCore.Utils
{
    public static class StopwatchUtils
    {
        static Dictionary<string, Stopwatch> watches = new Dictionary<string, Stopwatch>();

        /// <summary>
        /// Start Stopwatch
        /// </summary>
        /// <param name="name">KEY</param>
        public static void StopwatchStart(string name)
        {
            watches[name] = Stopwatch.StartNew();
        }

        /// <summary>
        /// Get Elapsed Milliseconds
        /// </summary>
        /// <param name="name">KEY</param>
        /// <returns></returns>
        public static long StopwatchElapsedMilliseconds(string name) => watches[name].ElapsedMilliseconds;

        /// <summary>
        /// Remove StopWatch
        /// </summary>
        /// <param name="name"></param>
        public static void StopwatchStop(string name)
        {
            watches.Remove(name);
        }
    }
}

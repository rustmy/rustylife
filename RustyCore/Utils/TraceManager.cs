using Oxide.Core;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace RustyCore.Utils
{
    public static class TraceManager
    {
        internal class TraceInfo
        {
            [JsonProperty("function")]
            public string Function;
            [JsonProperty("time")]
            public double Time;
        }
        internal class RecordSecond
        {
            [JsonProperty("fps")]
            public int Fps;
            [JsonProperty("traces")]
            public List<TraceInfo> Traces;
        }

        internal static List<RecordSecond> record = new List<RecordSecond>();

        private static Dictionary<string, Stopwatch> watchers = new Dictionary<string, Stopwatch>();
        private static bool work = false;
        private static int seconds;
        private static int currentIndex = -1;

        static TraceManager()
        {
            Interface.Oxide.OnFrame(d=>OnFrame());
        }

        internal static void StartRecord(int secs)
        {
            seconds = secs;
            record.Clear();
            work = true;
            Logger.Info("Запись начата!");
        }

        private static long lastSecond = 0;
        private static int Frames = 0;
        private static void OnFrame()
        {
            if (!work) return;
            var curSecond = (long) Time.realtimeSinceStartup;
            Frames++;
            if (lastSecond != curSecond)
            {
                lastSecond = curSecond;
                if (record.Count == 0)
                {
                    Frames = 1;
                    currentIndex = 0;
                    record.Add(new RecordSecond(){ Traces = new List<TraceInfo>()});
                    return;
                }
                else
                {
                    record.Last().Fps = Frames;
                    Frames = 0;
                    if (currentIndex == seconds-1)
                    {
                        work = false;
                        seconds = 0;
                        currentIndex = -1;
                        SaveJson();
                        Logger.Info($"Запись завершена! Длительность: {seconds}сек.");
                        return;
                    }
                    record.Add(new RecordSecond() { Traces = new List<TraceInfo>() });
                    currentIndex++;
                }
            }
        }
        
        public static void TraceStart(string plugin, string hook)
        {
            if (!work || currentIndex < 0) return;
            var name = $"{plugin}.{hook}";
            Stopwatch oldStopwatch;
            if (watchers.TryGetValue(name, out oldStopwatch))
            {
                oldStopwatch?.Stop();
            }
            watchers[name] = Stopwatch.StartNew();
        }

        public static void TraceEnd(string plugin, string hook)
        {
            if (!work || currentIndex < 0) return;
            var name = $"{plugin}.{hook}";
            Stopwatch stopwatch;
            if (!watchers.TryGetValue(name, out stopwatch)) return;

            stopwatch.Stop();

            var ms = System.Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3);
            if (ms < 0.01) return;
            record[currentIndex].Traces.Add(new TraceInfo() {Function = name, Time = ms});
        }

        public static void SaveJson()
        {
            Interface.Oxide.DataFileSystem.GetFile("TraceManager").WriteObject(record);
        }
    }
}

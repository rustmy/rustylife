using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using Oxide.Core.Configuration;
using System.IO;

namespace RustyCore.Utils
{
    public static class FilterWords
    {
        static Dictionary<string, List<string>> swearWords;
        static string abc;
        static DynamicConfigFile config;
        
        static FilterWords()
        {
            string path = Path.Combine(Interface.Oxide.ConfigDirectory, "SwearWords.json");
            config = new DynamicConfigFile(path);
            if (File.Exists(path))
                config.Load();
            else config.Save();
            LoadConfig();
        }

        public static void LoadConfig()
        {
            config.Load();
            config["Список доступных символов в нике"] = abc = GetConfig("Список доступных символов в нике", " _-()[]=+!abcdefghijklmnopqrstuvwxyzабвгдеёжзийклмнопрстуфхцчшщъыьэюя0123456789");
            config["Список начальных букв нецензурных слов или слова целиком | список исключений"] = swearWords = GetConfig("Список начальных букв нецензурных слов или слова целиком | список исключений", new Dictionary<string, object>()).ToDictionary(p => p.Key, p => ((List<object>)p.Value).Cast<string>().ToList());
            config.Save();
        }

        static T  GetConfig<T>(string name, T defaultValue)
            => config[name] == null ? defaultValue : (T)Convert.ChangeType(config[name], typeof(T));

        public static bool IsBadWord(this string input)
        {
            var temp = input.ToLower();
            foreach (var swear in swearWords)
            {
                var firstIndex = temp.IndexOf(swear.Key);
                if (firstIndex >= 0 && swear.Value.All(exception => temp.IndexOf(exception) < 0))
                        return true;
            }
            return false;
        }
        
        public static string CensorBadWords(this string input, out bool found)
        {
            found = false;
            string temp = input.ToLower();
            foreach (var swear in swearWords)
            {
                var firstIndex = temp.IndexOf(swear.Key);
                if (firstIndex >= 0 && swear.Value.All(exception => temp.IndexOf(exception) < 0))
                        while (firstIndex < input.Length && input[firstIndex] != ' ')
                        {
                            input = input.Remove(firstIndex, 1);
                            input = input.Insert(firstIndex, "*");
                            firstIndex++;
                            found = true;
                        }
            }
            return input;
        }

        public static string RemoveBadSymbols(this string input) => new string(input.Where(p => abc.Contains(char.ToLower(p))).ToArray());
        public static bool IsBadSymbols(this string input) => !input.ToLower().All(symbol => abc.Contains(symbol));
    
        public static bool IsLink(this string input)=> input.ToLower().ContainsAny(".ru", ".com",".org" , ".рф", "csgohappy", "furyrust");
    }
}

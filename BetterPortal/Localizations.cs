using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using LitJson;

namespace BetterPortal
{
    internal static class L10N
    {
        private static readonly Regex WordPattern;

        static L10N()
        {
            WordPattern = new Regex(@"(\$|@)((?:\w|\d|[^\s(){}[\]+\-!?/\\&%,.:=<>])+)", RegexOptions.Compiled);
        }

        internal static Localization ValheimL10N => Localization.instance;

        private static void InvokeMethod(string methodName, params object[] args) =>
            Traverse.Create(ValheimL10N).Method(methodName, args).GetValue(args);

        private static T InvokeMethod<T>(string methodName, params object[] args) =>
            Traverse.Create(ValheimL10N).Method(methodName, args).GetValue<T>(args);

        public static bool IsInternalName(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            switch (text[0])
            {
                case '$':
                case '@':
                    return true;
                default:
                    return false;
            }
        }

        public static string Translate(string word)
        {
            if (string.IsNullOrEmpty(word)) return "";
            switch (word[0])
            {
                case '$':
                    return InvokeTranslate(word.Substring(1));
                case '@':
                    return InvokeTranslate($"better_portal_{word.Substring(1)}");
                default:
                    return InvokeTranslate(word);
            }
        }

        public static string TranslateInternalNameOnly(string internalName)
        {
            switch (internalName[0])
            {
                case '$':
                    return InvokeTranslate(internalName.Substring(1));
                case '@':
                    return InvokeTranslate($"better_portal_{internalName.Substring(1)}");
                default:
                    return internalName;
            }
        }

        public static string Localize(string text)
        {
            var sb = new StringBuilder();
            var offset = 0;
            foreach (Match match in WordPattern.Matches(text))
            {
                var groups = match.Groups;
                var word = groups[1].Value == "@" ? $"better_portal_{groups[2].Value}" : groups[2].Value;

                sb.Append(text.Substring(offset, groups[0].Index - offset));
                sb.Append(InvokeTranslate(word));
                offset = groups[0].Index + groups[0].Value.Length;
            }

            return sb.ToString();
        }

        public static string Localize(string text, params object[] words)
        {
            return InvokeInsertWords(Localize(text), Array.ConvertAll(words, x =>
            {
                if (!(x is string s)) return x.ToString();
                return TranslateInternalNameOnly(s);
            }));
        }

        public static string LocalizeWithoutTranslateWords(string text, params object[] words)
        {
            return InvokeInsertWords(Localize(text), Array.ConvertAll(words, x => x as string ?? x.ToString()));
        }

        public static void AddWord(string key, string word) => InvokeMethod("AddWord", key, word);

        private static string InvokeTranslate(string word) => InvokeMethod<string>("Translate", word);

        private static string InvokeInsertWords(string text, string[] words) =>
            InvokeMethod<string>("InsertWords", text, words);
    }

    internal static class TranslationsLoader
    {
        private static readonly Dictionary<string, TranslationFile> Cache;

        static TranslationsLoader()
        {
            Cache = new Dictionary<string, TranslationFile>();
        }

        public static void LoadFromJson(string languagesDir)
        {
            const string defaultLanguage = "English";
            var language = L10N.ValheimL10N.GetSelectedLanguage();

            if (language != defaultLanguage)
            {
                if (!ReadAllJson(languagesDir, defaultLanguage))
                    BetterPortal.ModLogger.LogError(
                        $"Failed to load {defaultLanguage} language file from {languagesDir}");
            }

            if (!ReadAllJson(languagesDir, language))
                BetterPortal.ModLogger.LogWarning($"Failed to load {language} language file from {languagesDir}");

            Cache.Clear();
        }

        private static bool ReadAllJson(string directory, string language)
        {
            BetterPortal.ModLogger.LogDebug($"Try to load {language} translations from {directory}");
            if (!Directory.Exists(directory)) return false;
            return Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories)
                .Count(x => ReadJsonFile(x, language)) > 0;
        }

        private static bool ReadJsonFile(string path, string language)
        {
            if (Cache.TryGetValue(path, out var x)) return Load(x, language, path);
            if (!File.Exists(path)) return false;

            try
            {
                var translationFile = JsonMapper.ToObject<TranslationFile>(new JsonReader(File.ReadAllText(path))
                {
                    AllowComments = true,
                });
                Cache[path] = translationFile;
                return Load(translationFile, language, path);
            }
            catch (Exception e)
            {
                BetterPortal.ModLogger.LogError($"File read error: {e}");
                return false;
            }
        }

        private static bool Load(TranslationFile translationFile, string language, string path)
        {
            if (!string.Equals(translationFile.language, language, StringComparison.OrdinalIgnoreCase)) return false;
            if (translationFile.translations == null) return false;

            BetterPortal.ModLogger.LogDebug($"Load translations from {path}");
            foreach (var translation in translationFile.translations)
            {
                var key = translation.Key.StartsWith("@")
                    ? $"better_portal_{translation.Key.Substring(1)}"
                    : translation.Key;
                var value = translation.Value;
                L10N.AddWord(key, value);
            }

            return true;
        }
    }

    [Serializable]
    public struct TranslationFile
    {
        public string language;

        // ReSharper disable once UnassignedField.Global, InconsistentNaming
        public Dictionary<string, string> translations;
    }
}
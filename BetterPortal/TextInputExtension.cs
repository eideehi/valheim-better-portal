using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BetterPortal
{
    internal static class TextInputExtension
    {
        private static bool _keyPressed;
        private static bool _keyHold;
        private static KeyCode _pressedKey = KeyCode.None;
        private static string _originalWord;
        private static string _previousResult;

        private static List<string> GetPortalTags()
        {
            return Portals.GetAll()
                .Select(x => x.GetString("tag"))
                .OrderBy(x => x)
                .Distinct()
                .ToList();
        }

        private static void UpdateTextInput(TextInput input, string text)
        {
            var textField = input.m_textField;
            if (text == textField.text) return;

            textField.text = string.IsNullOrEmpty(text) ? "" : text;
            textField.MoveTextEnd(false);
        }

        private static void InputUpdate()
        {
            if (!_keyPressed)
            {
                if (Input.GetKeyDown(KeyCode.Insert))
                    _pressedKey = KeyCode.Insert;
                else if (Input.GetKeyDown(KeyCode.UpArrow))
                    _pressedKey = KeyCode.UpArrow;
                else if (Input.GetKeyDown(KeyCode.DownArrow))
                    _pressedKey = KeyCode.DownArrow;
                else
                    _pressedKey = KeyCode.None;

                _keyPressed = _pressedKey != KeyCode.None;
            }
            else
            {
                if (!Input.GetKeyUp(_pressedKey))
                {
                    _keyHold = true;
                    return;
                }

                _keyPressed = false;
                _keyHold = false;
                _pressedKey = KeyCode.None;
            }
        }

        public static void Update(TextInput input)
        {
            InputUpdate();

            if (!_keyPressed || _keyHold) return;

            var textField = input.m_textField;
            if (textField is null) return;

            if (_pressedKey == KeyCode.Insert)
                UpdateTextInput(input, AutoComplete(textField.text));
            else if (_pressedKey == KeyCode.UpArrow || _pressedKey == KeyCode.DownArrow)
                UpdateTextInput(input, Rotate(textField.text, _pressedKey == KeyCode.DownArrow));
        }

        private static string AutoComplete(string word)
        {
            var tags = GetPortalTags();

            if (word == _previousResult)
            {
                var index = tags.IndexOf(_previousResult);
                for (var i = index + 1; i < tags.Count; i++)
                {
                    if (!tags[i].StartsWith(_originalWord, StringComparison.OrdinalIgnoreCase))
                        continue;

                    _previousResult = tags[i];
                    return _previousResult;
                }

                _previousResult = null;
                return _originalWord;
            }

            _originalWord = null;
            _previousResult = null;
            foreach (var tag in tags.Where(tag =>
                         tag.StartsWith(word, StringComparison.OrdinalIgnoreCase)))
            {
                _originalWord = word;
                _previousResult = tag;
                return _previousResult;
            }

            return word;
        }

        private static string Rotate(string current, bool ascending)
        {
            var tags = GetPortalTags();

            if (string.IsNullOrEmpty(current))
                return ascending
                    ? tags.FirstOrDefault(x => !string.IsNullOrEmpty(x))
                    : tags.LastOrDefault(x => !string.IsNullOrEmpty(x));

            var index = tags.FindIndex(x => x.Equals(current, StringComparison.OrdinalIgnoreCase));
            if (index == -1)
                index = tags.FindIndex(x =>
                    x.StartsWith(current, StringComparison.OrdinalIgnoreCase));

            if (index == -1) return ascending ? tags.FirstOrDefault() : tags.LastOrDefault();

            index += ascending ? 1 : -1;
            if (index < 0)
                return tags.LastOrDefault();
            if (index >= tags.Count)
                return tags.FirstOrDefault();
            return tags[index];
        }
    }
}
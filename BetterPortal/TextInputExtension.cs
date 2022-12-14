using System.Linq;
using UnityEngine;

namespace BetterPortal
{
    internal static class TextInputExtension
    {
        private static bool _keyPressed;
        private static bool _keyHold;
        private static KeyCode _pressedKey = KeyCode.None;
        private static string _prevText;
        private static string _searchKeyword;

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

            if (_pressedKey == KeyCode.Insert)
                AutoComplete(input);
            else if (_pressedKey == KeyCode.UpArrow || _pressedKey == KeyCode.DownArrow)
                SetNextTag(input, _pressedKey == KeyCode.DownArrow);
        }

        private static void AutoComplete(TextInput input)
        {
            var textField = input.m_textField;
            var text = textField.text.Trim();

            if (_prevText != text)
                _searchKeyword = null;

            if (_searchKeyword is null)
                _searchKeyword = text;

            var tags = Portals.GetAll()
                .Select(x => x.GetString("tag"))
                .Where(x => x.StartsWith(_searchKeyword))
                .OrderBy(x => x)
                .Distinct().ToList();

            if (!tags.Any()) return;

            var next = tags[Mathf.Clamp(tags.IndexOf(text) + 1, 0, tags.Count - 1)];
            if (next == text)
                next = tags.FirstOrDefault();

            text = next;
            _prevText = text;

            if (text == textField.text) return;

            textField.text = text;
            textField.MoveTextEnd(false);
        }

        private static void SetNextTag(TextInput input, bool ascending)
        {
            var textField = input.m_textField;
            var text = textField.text.Trim();

            var tags = Portals.GetAll().Select(x => x.GetString("tag")).Distinct();
            tags = (ascending
                ? tags.OrderBy(x => x)
                : tags.OrderByDescending(x => x)).ToList();

            string next = null;
            string nextCandidate = null;

            var tagFound = false;
            foreach (var tag in tags)
            {
                if (tagFound)
                {
                    next = tag;
                    break;
                }

                tagFound = tag == text;
                if (!tagFound && nextCandidate is null && tag.StartsWith(text))
                    nextCandidate = tag;
            }

            if (!(next is null))
                text = next;
            else if (!(nextCandidate is null))
                text = nextCandidate;
            else
                text = tags.FirstOrDefault();

            if (text == textField.text) return;

            textField.text = text;
            textField.MoveTextEnd(false);
        }
    }
}
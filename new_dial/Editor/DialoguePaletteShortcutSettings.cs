// Copyright (c) 2026 Danil Kashulin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor
{
    [Serializable]
    internal struct DialoguePaletteShortcut : IEquatable<DialoguePaletteShortcut>
    {
        public KeyCode KeyCode;
        public bool Action;
        public bool Shift;
        public bool Alt;

        public DialoguePaletteShortcut(KeyCode keyCode, bool action = false, bool shift = false, bool alt = false)
        {
            KeyCode = keyCode;
            Action = action;
            Shift = shift;
            Alt = alt;
        }

        public bool IsAssigned => KeyCode != KeyCode.None;

        public static DialoguePaletteShortcut FromEvent(KeyDownEvent evt)
        {
            return evt == null
                ? default
                : new DialoguePaletteShortcut(evt.keyCode, evt.actionKey, evt.shiftKey, evt.altKey);
        }

        public bool Equals(DialoguePaletteShortcut other)
        {
            return KeyCode == other.KeyCode &&
                   Action == other.Action &&
                   Shift == other.Shift &&
                   Alt == other.Alt;
        }

        public override bool Equals(object obj)
        {
            return obj is DialoguePaletteShortcut other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)KeyCode;
                hash = (hash * 397) ^ Action.GetHashCode();
                hash = (hash * 397) ^ Shift.GetHashCode();
                hash = (hash * 397) ^ Alt.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(DialoguePaletteShortcut left, DialoguePaletteShortcut right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DialoguePaletteShortcut left, DialoguePaletteShortcut right)
        {
            return !left.Equals(right);
        }
    }

    internal static class DialoguePaletteShortcutSettings
    {
        internal const string EditorPrefsKey = "NewDial.DialogueEditor.PaletteShortcuts";

        private static readonly DialoguePaletteItemType[] ItemOrder =
        {
            DialoguePaletteItemType.TextNode,
            DialoguePaletteItemType.Comment,
            DialoguePaletteItemType.Function,
            DialoguePaletteItemType.Scene,
            DialoguePaletteItemType.Debug,
            DialoguePaletteItemType.Choice
        };

        private static readonly HashSet<KeyCode> UnbindableKeys = new()
        {
            KeyCode.None,
            KeyCode.LeftAlt,
            KeyCode.RightAlt,
            KeyCode.LeftControl,
            KeyCode.RightControl,
            KeyCode.LeftCommand,
            KeyCode.RightCommand,
            KeyCode.LeftShift,
            KeyCode.RightShift,
            KeyCode.CapsLock,
            KeyCode.Escape,
            KeyCode.Delete,
            KeyCode.Backspace
        };

        private static readonly HashSet<KeyCode> ReservedPlainCanvasMovementKeys = new()
        {
            KeyCode.W,
            KeyCode.A,
            KeyCode.S,
            KeyCode.D
        };

        public static IReadOnlyList<DialoguePaletteItemType> OrderedItemTypes => ItemOrder;

        public static DialoguePaletteShortcut GetShortcut(DialoguePaletteItemType itemType)
        {
            return LoadMap().TryGetValue(itemType, out var shortcut)
                ? shortcut
                : default;
        }

        public static void SetShortcut(DialoguePaletteItemType itemType, DialoguePaletteShortcut shortcut)
        {
            if (!IsBindable(shortcut))
            {
                return;
            }

            var map = LoadMap();
            foreach (var key in ItemOrder)
            {
                if (key != itemType && shortcut.IsAssigned && map.TryGetValue(key, out var existing) && existing == shortcut)
                {
                    map[key] = default;
                }
            }

            map[itemType] = shortcut;
            SaveMap(map);
        }

        public static void ClearShortcut(DialoguePaletteItemType itemType)
        {
            var map = LoadMap();
            map[itemType] = default;
            SaveMap(map);
        }

        public static DialoguePaletteItemType? FindMatchingItem(DialoguePaletteShortcut shortcut)
        {
            if (!IsBindable(shortcut))
            {
                return null;
            }

            var map = LoadMap();
            foreach (var itemType in ItemOrder)
            {
                if (map.TryGetValue(itemType, out var candidate) && candidate == shortcut)
                {
                    return itemType;
                }
            }

            return null;
        }

        public static bool IsBindable(DialoguePaletteShortcut shortcut)
        {
            return shortcut.IsAssigned &&
                   !UnbindableKeys.Contains(shortcut.KeyCode) &&
                   !IsReservedPlainMovementShortcut(shortcut);
        }

        public static string FormatShortcut(DialoguePaletteShortcut shortcut)
        {
            if (!shortcut.IsAssigned || !IsBindable(shortcut))
            {
                return DialogueEditorLocalization.Text("Unassigned");
            }

            var parts = new List<string>();
            if (shortcut.Action)
            {
                parts.Add(Application.platform == RuntimePlatform.OSXEditor ? "Cmd" : "Ctrl");
            }

            if (shortcut.Shift)
            {
                parts.Add("Shift");
            }

            if (shortcut.Alt)
            {
                parts.Add("Alt");
            }

            parts.Add(FormatKey(shortcut.KeyCode));
            return string.Join("+", parts);
        }

        internal static void ResetForTests()
        {
            EditorPrefs.DeleteKey(EditorPrefsKey);
        }

        private static Dictionary<DialoguePaletteItemType, DialoguePaletteShortcut> LoadMap()
        {
            var map = CreateDefaultMap();
            var json = EditorPrefs.GetString(EditorPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return map;
            }

            try
            {
                var data = JsonUtility.FromJson<PaletteShortcutPrefs>(json);
                if (data?.Items == null)
                {
                    return map;
                }

                foreach (var item in data.Items)
                {
                    if (!Enum.TryParse(item.ItemType, out DialoguePaletteItemType itemType) ||
                        !ItemOrder.Contains(itemType) ||
                        !Enum.TryParse(item.KeyCode, out KeyCode keyCode))
                    {
                        continue;
                    }

                    var shortcut = new DialoguePaletteShortcut(keyCode, item.Action, item.Shift, item.Alt);
                    map[itemType] = IsBindable(shortcut) ? shortcut : default;
                }
            }
            catch
            {
                return map;
            }

            return map;
        }

        private static bool IsReservedPlainMovementShortcut(DialoguePaletteShortcut shortcut)
        {
            return ReservedPlainCanvasMovementKeys.Contains(shortcut.KeyCode) &&
                   !shortcut.Action &&
                   !shortcut.Shift &&
                   !shortcut.Alt;
        }

        private static void SaveMap(Dictionary<DialoguePaletteItemType, DialoguePaletteShortcut> map)
        {
            var data = new PaletteShortcutPrefs
            {
                Items = ItemOrder.Select(itemType =>
                {
                    map.TryGetValue(itemType, out var shortcut);
                    return new PaletteShortcutPrefsItem
                    {
                        ItemType = itemType.ToString(),
                        KeyCode = shortcut.KeyCode.ToString(),
                        Action = shortcut.Action,
                        Shift = shortcut.Shift,
                        Alt = shortcut.Alt
                    };
                }).ToList()
            };
            EditorPrefs.SetString(EditorPrefsKey, JsonUtility.ToJson(data));
        }

        private static Dictionary<DialoguePaletteItemType, DialoguePaletteShortcut> CreateDefaultMap()
        {
            return new Dictionary<DialoguePaletteItemType, DialoguePaletteShortcut>
            {
                [DialoguePaletteItemType.TextNode] = new(KeyCode.Alpha1, alt: true),
                [DialoguePaletteItemType.Comment] = new(KeyCode.Alpha2, alt: true),
                [DialoguePaletteItemType.Function] = new(KeyCode.Alpha3, alt: true),
                [DialoguePaletteItemType.Scene] = new(KeyCode.Alpha4, alt: true),
                [DialoguePaletteItemType.Debug] = new(KeyCode.Alpha5, alt: true),
                [DialoguePaletteItemType.Choice] = new(KeyCode.Alpha6, alt: true)
            };
        }

        private static string FormatKey(KeyCode keyCode)
        {
            return keyCode switch
            {
                KeyCode.Alpha0 => "0",
                KeyCode.Alpha1 => "1",
                KeyCode.Alpha2 => "2",
                KeyCode.Alpha3 => "3",
                KeyCode.Alpha4 => "4",
                KeyCode.Alpha5 => "5",
                KeyCode.Alpha6 => "6",
                KeyCode.Alpha7 => "7",
                KeyCode.Alpha8 => "8",
                KeyCode.Alpha9 => "9",
                KeyCode.Keypad0 => "Num 0",
                KeyCode.Keypad1 => "Num 1",
                KeyCode.Keypad2 => "Num 2",
                KeyCode.Keypad3 => "Num 3",
                KeyCode.Keypad4 => "Num 4",
                KeyCode.Keypad5 => "Num 5",
                KeyCode.Keypad6 => "Num 6",
                KeyCode.Keypad7 => "Num 7",
                KeyCode.Keypad8 => "Num 8",
                KeyCode.Keypad9 => "Num 9",
                _ => keyCode.ToString()
            };
        }

        [Serializable]
        private sealed class PaletteShortcutPrefs
        {
            public List<PaletteShortcutPrefsItem> Items;
        }

        [Serializable]
        private sealed class PaletteShortcutPrefsItem
        {
            public string ItemType;
            public string KeyCode;
            public bool Action;
            public bool Shift;
            public bool Alt;
        }
    }
}

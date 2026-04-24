using System;
using UnityEditor;

namespace NewDial.DialogueEditor
{
    internal static class DialogueContentLanguageSettings
    {
        private const string EditorPrefsKey = "NewDial.DialogueEditor.ContentLanguage";

        public static event Action LanguageChanged;

        public static string CurrentLanguageCode
        {
            get => EditorPrefs.GetString(EditorPrefsKey, DialogueTextLocalizationUtility.DefaultLanguageCode);
            set
            {
                var normalized = DialogueTextLocalizationUtility.NormalizeLanguageCode(value);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    normalized = DialogueTextLocalizationUtility.DefaultLanguageCode;
                }

                if (CurrentLanguageCode == normalized)
                {
                    return;
                }

                EditorPrefs.SetString(EditorPrefsKey, normalized);
                LanguageChanged?.Invoke();
            }
        }

        internal static void ResetForTests()
        {
            EditorPrefs.DeleteKey(EditorPrefsKey);
            LanguageChanged?.Invoke();
        }

        internal static void SetCurrentLanguageCodeWithoutNotify(string languageCode)
        {
            var normalized = DialogueTextLocalizationUtility.NormalizeLanguageCode(languageCode);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                normalized = DialogueTextLocalizationUtility.DefaultLanguageCode;
            }

            EditorPrefs.SetString(EditorPrefsKey, normalized);
        }
    }
}

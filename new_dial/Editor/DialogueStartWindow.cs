using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor
{
    public class DialogueStartWindow : EditorWindow
    {
        private const string EditorStyleSheetSearchQuery = "DialogueEditorStyles t:StyleSheet";
        internal static readonly Vector2 CompactMinSize = new(420f, 340f);
        internal static readonly Vector2 CompactMaxSize = new(520f, 400f);
        internal static readonly Vector2 ExpandedMinSize = new(520f, 560f);
        internal static readonly Vector2 ExpandedMaxSize = new(620f, 560f);
        private bool _stylesApplied;
        private bool _localizationFoldoutExpanded;
        private DialogueEditorWindow _localizationOwner;
        private DialogueDatabaseAsset _localizationDatabase;
        private NpcEntry _localizationNpc;
        private DialogueEntry _localizationDialogue;
        private bool _hasLastValidWindowRect;
        private Rect _lastValidWindowRect;
        private int _initialCenterRequestVersion;

        [MenuItem("Tools/New Dial/Dialogue Editor")]
        [MenuItem("Window/New Dial/Dialogue Editor")]
        public static void ShowWindow()
        {
            var existingWindow = Resources.FindObjectsOfTypeAll<DialogueStartWindow>();
            if (existingWindow.Length > 0 && existingWindow[0] != null)
            {
                existingWindow[0].RememberCurrentWindowRectIfUsable();
                existingWindow[0].SetLocalizationContext(null, null, null, null, expanded: false);
                existingWindow[0].Rebuild();
                existingWindow[0].ApplyWindowSizeIfReady();
                existingWindow[0].Focus();
                return;
            }

            var window = CreateInstance<DialogueStartWindow>();
            window.PrepareForDisplay();
            window.ShowUtility();
            window.CenterOnMainWindow();
            window.CenterOnMainWindowDelayed();
        }

        public static void OpenLocalization(
            DialogueEditorWindow owner,
            DialogueDatabaseAsset database,
            NpcEntry selectedNpc,
            DialogueEntry selectedDialogue)
        {
            var existingWindow = Resources.FindObjectsOfTypeAll<DialogueStartWindow>();
            var window = existingWindow.Length > 0 && existingWindow[0] != null
                ? existingWindow[0]
                : CreateInstance<DialogueStartWindow>();

            window.SetLocalizationContext(owner, database, selectedNpc, selectedDialogue, expanded: true);
            window.Rebuild();
            window.PrepareForDisplay();
            if (existingWindow.Length > 0 && existingWindow[0] != null)
            {
                window.RememberCurrentWindowRectIfUsable();
                window.ApplyWindowSizeIfReady();
                window.Focus();
                return;
            }

            window.ShowUtility();
            window.CenterOnMainWindow();
            window.CenterOnMainWindowDelayed();
        }

        internal void InitializeForTests(
            DialogueEditorWindow owner = null,
            DialogueDatabaseAsset database = null,
            NpcEntry selectedNpc = null,
            DialogueEntry selectedDialogue = null,
            bool localizationExpanded = false)
        {
            SetLocalizationContext(owner, database, selectedNpc, selectedDialogue, localizationExpanded);
            ApplyStyles();
            Rebuild();
            ApplySizeConstraints();
        }

        private void OnEnable()
        {
            DialogueEditorLanguageSettings.LanguageChanged += Rebuild;
        }

        private void OnDisable()
        {
            DialogueEditorLanguageSettings.LanguageChanged -= Rebuild;
        }

        private void CreateGUI()
        {
            ApplyStyles();
            Rebuild();
            ApplySizeConstraints();
        }

        private void PrepareForDisplay()
        {
            titleContent = new GUIContent(DialogueEditorLocalization.Text("Dialogue Editor"));
            ApplySizeConstraints();
        }

        private void Rebuild()
        {
            titleContent = new GUIContent(DialogueEditorLocalization.Text("Dialogue Editor"));
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("dialogue-start");

            var card = new VisualElement { name = "dialogue-start-card" };
            card.AddToClassList("dialogue-start__card");
            rootVisualElement.Add(card);

            var title = new Label(DialogueEditorLocalization.Text("Dialogue Editor")) { name = "dialogue-start-title" };
            title.AddToClassList("dialogue-start__title");
            card.Add(title);

            var subtitle = new Label(DialogueEditorLocalization.Text("Create a dialogue database or open an existing one."))
            {
                name = "dialogue-start-subtitle"
            };
            subtitle.AddToClassList("dialogue-start__subtitle");
            card.Add(subtitle);

            card.Add(CreateButton(DialogueEditorLocalization.Text("New File"), CreateNewDatabase, "dialogue-start__button--primary"));
            card.Add(CreateButton(DialogueEditorLocalization.Text("Load File"), LoadExistingDatabase, "dialogue-start__button--neutral"));
            card.Add(CreateButton(DialogueEditorLocalization.Text("Exit"), Close, "dialogue-start__button--exit"));

            var divider = new VisualElement { name = "dialogue-start-import-export-divider" };
            divider.AddToClassList("dialogue-start__divider");
            card.Add(divider);

            var importExportFoldout = new Foldout
            {
                text = DialogueEditorLocalization.Text("Import / Export"),
                value = _localizationFoldoutExpanded,
                name = "dialogue-start-import-export-foldout"
            };
            importExportFoldout.AddToClassList("dialogue-start__import-export-foldout");
            importExportFoldout.RegisterValueChangedCallback(evt =>
            {
                CancelPendingInitialCenter();
                RememberCurrentWindowRectIfUsable();
                _localizationFoldoutExpanded = evt.newValue;
                Rebuild();
                ApplyWindowSizeIfReady();
            });
            card.Add(importExportFoldout);

            if (_localizationFoldoutExpanded)
            {
                var panel = new DialogueLocalizationPanel(message => ShowNotification(new GUIContent(message)));
                panel.Initialize(_localizationOwner, _localizationDatabase, _localizationNpc, _localizationDialogue);
                importExportFoldout.Add(panel);
            }
        }

        private Button CreateButton(string text, System.Action onClick, string variantClass)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("dialogue-start__button");
            button.AddToClassList(variantClass);
            return button;
        }

        private void ApplySizeConstraints()
        {
            var min = _localizationFoldoutExpanded ? ExpandedMinSize : CompactMinSize;
            var max = _localizationFoldoutExpanded ? ExpandedMaxSize : CompactMaxSize;
            minSize = min;
            maxSize = max;
        }

        private void ApplyWindowSizeIfReady()
        {
            ApplySizeConstraints();
            var nextPosition = CalculateWindowRectForCurrentMode(position);
            position = nextPosition;
            RememberWindowRect(nextPosition);
        }

        internal Rect CalculateWindowRectForTests(Rect currentPosition, bool expanded, Rect? lastValidPosition = null)
        {
            _localizationFoldoutExpanded = expanded;
            if (lastValidPosition.HasValue)
            {
                RememberWindowRect(lastValidPosition.Value);
            }

            return CalculateWindowRectForCurrentMode(currentPosition);
        }

        private Rect CalculateWindowRectForCurrentMode(Rect currentPosition)
        {
            var min = _localizationFoldoutExpanded ? ExpandedMinSize : CompactMinSize;
            var max = _localizationFoldoutExpanded ? ExpandedMaxSize : CompactMaxSize;
            var stablePosition = GetStableWindowRect(currentPosition);
            var width = Mathf.Clamp(stablePosition.width <= 0f ? min.x : stablePosition.width, min.x, max.x);
            var height = Mathf.Clamp(stablePosition.height <= 0f ? min.y : stablePosition.height, min.y, max.y);
            return new Rect(stablePosition.x, stablePosition.y, width, height);
        }

        private Rect GetStableWindowRect(Rect currentPosition)
        {
            if (IsUsableWindowRect(currentPosition) && !IsLikelyUnityDefaultOrigin(currentPosition))
            {
                RememberWindowRect(currentPosition);
                return currentPosition;
            }

            if (_hasLastValidWindowRect)
            {
                return _lastValidWindowRect;
            }

            return GetCenteredRectForCurrentMode();
        }

        private void CenterOnMainWindow()
        {
            var centeredPosition = GetCenteredRectForCurrentMode();
            position = centeredPosition;
            RememberWindowRect(centeredPosition);
        }

        private void CenterOnMainWindowDelayed()
        {
            var requestVersion = ++_initialCenterRequestVersion;
            EditorApplication.delayCall += () =>
            {
                if (this == null || requestVersion != _initialCenterRequestVersion)
                {
                    return;
                }

                CenterOnMainWindow();
                Focus();
            };
        }

        private void CancelPendingInitialCenter()
        {
            _initialCenterRequestVersion++;
        }

        private Rect GetCenteredRectForCurrentMode()
        {
            var mainWindow = EditorGUIUtility.GetMainWindowPosition();
            var size = _localizationFoldoutExpanded ? ExpandedMinSize : CompactMinSize;
            return new Rect(
                mainWindow.center.x - (size.x * 0.5f),
                mainWindow.center.y - (size.y * 0.5f),
                size.x,
                size.y);
        }

        private void RememberCurrentWindowRectIfUsable()
        {
            var currentPosition = position;
            if (IsUsableWindowRect(currentPosition) && !IsLikelyUnityDefaultOrigin(currentPosition))
            {
                RememberWindowRect(currentPosition);
            }
        }

        private void RememberWindowRect(Rect windowRect)
        {
            if (!IsUsableWindowRect(windowRect))
            {
                return;
            }

            _lastValidWindowRect = windowRect;
            _hasLastValidWindowRect = true;
        }

        private bool IsLikelyUnityDefaultOrigin(Rect windowRect)
        {
            return _hasLastValidWindowRect &&
                   Mathf.Approximately(windowRect.x, 0f) &&
                   Mathf.Approximately(windowRect.y, 0f) &&
                   (!Mathf.Approximately(_lastValidWindowRect.x, 0f) ||
                    !Mathf.Approximately(_lastValidWindowRect.y, 0f));
        }

        private static bool IsUsableWindowRect(Rect windowRect)
        {
            return IsFinite(windowRect.x) &&
                   IsFinite(windowRect.y) &&
                   IsFinite(windowRect.width) &&
                   IsFinite(windowRect.height) &&
                   windowRect.width > 1f &&
                   windowRect.height > 1f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void SetLocalizationContext(
            DialogueEditorWindow owner,
            DialogueDatabaseAsset database,
            NpcEntry selectedNpc,
            DialogueEntry selectedDialogue,
            bool expanded)
        {
            _localizationOwner = owner;
            _localizationDatabase = database;
            _localizationNpc = selectedNpc;
            _localizationDialogue = selectedDialogue;
            _localizationFoldoutExpanded = expanded;
        }

        private void ApplyStyles()
        {
            if (_stylesApplied)
            {
                return;
            }

            var styleSheet = FindStyleSheet();
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
                _stylesApplied = true;
            }
        }

        private static StyleSheet FindStyleSheet()
        {
            var guids = AssetDatabase.FindAssets(EditorStyleSheetSearchQuery);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (styleSheet != null)
                {
                    return styleSheet;
                }
            }

            return null;
        }

        private void CreateNewDatabase()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                DialogueEditorLocalization.Text("Create Dialogue Database"),
                "DialogueDatabase",
                "asset",
                DialogueEditorLocalization.Text("Choose a location for the dialogue database."));

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var asset = CreateInstance<DialogueDatabaseAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            DialogueEditorWindow.Open(asset);
            Close();
        }

        private void LoadExistingDatabase()
        {
            var absolutePath = EditorUtility.OpenFilePanel(DialogueEditorLocalization.Text("Load Dialogue Database"), Application.dataPath, "asset");
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return;
            }

            var assetPath = FileUtil.GetProjectRelativePath(absolutePath);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                EditorUtility.DisplayDialog(
                    DialogueEditorLocalization.Text("Load failed"),
                    DialogueEditorLocalization.Text("The selected asset must be inside the current Unity project."),
                    DialogueEditorLocalization.Text("OK"));
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<DialogueDatabaseAsset>(assetPath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog(
                    DialogueEditorLocalization.Text("Load failed"),
                    DialogueEditorLocalization.Text("The selected asset is not a DialogueDatabaseAsset."),
                    DialogueEditorLocalization.Text("OK"));
                return;
            }

            DialogueEditorWindow.Open(asset);
            Close();
        }
    }
}

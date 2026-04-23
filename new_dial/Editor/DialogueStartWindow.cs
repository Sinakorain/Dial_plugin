using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor
{
    public class DialogueStartWindow : EditorWindow
    {
        [MenuItem("Window/New Dial/Dialogue Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<DialogueStartWindow>("Dialogue Editor");
            window.minSize = new Vector2(360f, 220f);
        }

        private void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 16f;
            rootVisualElement.style.paddingRight = 16f;
            rootVisualElement.style.paddingTop = 16f;
            rootVisualElement.style.paddingBottom = 16f;

            var title = new Label("Dialogue & Cutscene Node Editor");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 18;
            title.style.marginBottom = 8f;
            rootVisualElement.Add(title);

            var subtitle = new Label("Create a new dialogue database or load an existing one.");
            subtitle.style.marginBottom = 16f;
            rootVisualElement.Add(subtitle);

            rootVisualElement.Add(CreateButton("New File", CreateNewDatabase));
            rootVisualElement.Add(CreateButton("Load File", LoadExistingDatabase));
            rootVisualElement.Add(CreateButton("Exit", Close));
        }

        private Button CreateButton(string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.height = 34f;
            button.style.marginBottom = 8f;
            return button;
        }

        private void CreateNewDatabase()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Dialogue Database",
                "DialogueDatabase",
                "asset",
                "Choose a location for the dialogue database.");

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
            var absolutePath = EditorUtility.OpenFilePanel("Load Dialogue Database", Application.dataPath, "asset");
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return;
            }

            var assetPath = FileUtil.GetProjectRelativePath(absolutePath);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                EditorUtility.DisplayDialog("Load failed", "The selected asset must be inside the current Unity project.", "OK");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<DialogueDatabaseAsset>(assetPath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("Load failed", "The selected asset is not a DialogueDatabaseAsset.", "OK");
                return;
            }

            DialogueEditorWindow.Open(asset);
            Close();
        }
    }
}

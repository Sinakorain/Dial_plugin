using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor
{
    public class DialogueLocalizationWindow : EditorWindow
    {
        private DialogueEditorWindow _owner;
        private DialogueDatabaseAsset _database;
        private NpcEntry _selectedNpc;
        private DialogueEntry _selectedDialogue;
        private DialogueLocalizationTable _table;
        private PopupField<string> _conversationField;
        private Toggle _createDialogueToggle;
        private Label _summaryLabel;
        private TextField _exportPrefixField;

        [MenuItem("Tools/New Dial/Localization Import Export")]
        public static void ShowWindow()
        {
            Open(null, null, null, null);
        }

        public static void Open(
            DialogueEditorWindow owner,
            DialogueDatabaseAsset database,
            NpcEntry selectedNpc,
            DialogueEntry selectedDialogue)
        {
            var window = GetWindow<DialogueLocalizationWindow>(DialogueEditorLocalization.Text("Localization Import/Export"));
            window.minSize = new Vector2(520f, 420f);
            window.Initialize(owner, database, selectedNpc, selectedDialogue);
        }

        private void Initialize(
            DialogueEditorWindow owner,
            DialogueDatabaseAsset database,
            NpcEntry selectedNpc,
            DialogueEntry selectedDialogue)
        {
            _owner = owner;
            _database = database;
            _selectedNpc = selectedNpc;
            _selectedDialogue = selectedDialogue;
            BuildLayout();
        }

        private void CreateGUI()
        {
            BuildLayout();
        }

        private void BuildLayout()
        {
            if (rootVisualElement == null)
            {
                return;
            }

            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 12f;
            rootVisualElement.style.paddingRight = 12f;
            rootVisualElement.style.paddingTop = 12f;
            rootVisualElement.style.paddingBottom = 12f;

            rootVisualElement.Add(new Label(DialogueEditorLocalization.Text("Localization Import/Export"))
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 16f,
                    marginBottom = 8f
                }
            });

            var databaseField = new IMGUIContainer(() =>
            {
                var nextDatabase = (DialogueDatabaseAsset)EditorGUILayout.ObjectField(
                    DialogueEditorLocalization.Text("Dialogue Database"),
                    _database,
                    typeof(DialogueDatabaseAsset),
                    false);
                if (nextDatabase == _database)
                {
                    return;
                }

                _database = nextDatabase;
                _selectedNpc = _database?.Npcs?.FirstOrDefault();
                _selectedDialogue = _selectedNpc?.Dialogues?.FirstOrDefault();
                BuildLayout();
            });
            databaseField.name = "localization-database-field";
            rootVisualElement.Add(databaseField);

            _summaryLabel = new Label(BuildContextSummary());
            _summaryLabel.style.whiteSpace = WhiteSpace.Normal;
            rootVisualElement.Add(_summaryLabel);

            rootVisualElement.Add(CreateSectionTitle(DialogueEditorLocalization.Text("Import")));
            rootVisualElement.Add(new Button(LoadTableFromFile)
            {
                text = DialogueEditorLocalization.Text("Load TSV/CSV"),
                name = "localization-load-table-button"
            });

            var conversations = _table?.GetConversations().Select(conversation => conversation.Id).ToList() ?? new List<string>();
            if (conversations.Count == 0)
            {
                conversations.Add(DialogueEditorLocalization.Text("No conversations loaded"));
            }

            _conversationField = new PopupField<string>(
                DialogueEditorLocalization.Text("Conversation"),
                conversations,
                0)
            {
                name = "localization-conversation-field"
            };
            _conversationField.SetEnabled(_table?.Rows.Count > 0);
            rootVisualElement.Add(_conversationField);

            _createDialogueToggle = new Toggle(DialogueEditorLocalization.Text("Create New Dialogue"))
            {
                value = _selectedDialogue == null,
                name = "localization-create-dialogue-toggle"
            };
            rootVisualElement.Add(_createDialogueToggle);

            rootVisualElement.Add(new Button(ApplyImport)
            {
                text = DialogueEditorLocalization.Text("Apply Import"),
                name = "localization-apply-import-button"
            });

            rootVisualElement.Add(CreateSectionTitle(DialogueEditorLocalization.Text("Export")));
            _exportPrefixField = new TextField(DialogueEditorLocalization.Text("Conversation Prefix"))
            {
                value = _selectedDialogue?.Id ?? string.Empty,
                name = "localization-export-prefix-field"
            };
            rootVisualElement.Add(_exportPrefixField);
            rootVisualElement.Add(new Button(ExportDialogue)
            {
                text = DialogueEditorLocalization.Text("Export TSV"),
                name = "localization-export-button"
            });
        }

        private void LoadTableFromFile()
        {
            var path = EditorUtility.OpenFilePanel(DialogueEditorLocalization.Text("Load TSV/CSV"), string.Empty, "tsv,csv");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            _table = DialogueLocalizationTableParser.Parse(File.ReadAllText(path));
            BuildLayout();
            ShowNotification(new GUIContent(DialogueEditorLocalization.Format("Loaded {0} dialogue row(s).", _table.Rows.Count)));
        }

        private void ApplyImport()
        {
            if (_database == null || _table == null || _table.Rows.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    DialogueEditorLocalization.Text("Import failed"),
                    DialogueEditorLocalization.Text("Load a dialogue database and TSV/CSV table first."),
                    DialogueEditorLocalization.Text("OK"));
                return;
            }

            var conversation = _table.GetConversation(_conversationField.value);
            if (conversation == null)
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(_database, "Import Dialogue Localization");
            var targetNpc = EnsureTargetNpc();
            var targetDialogue = _createDialogueToggle.value || _selectedDialogue == null
                ? CreateDialogue(targetNpc, conversation.Id)
                : _selectedDialogue;

            var report = DialogueLocalizationImportUtility.ApplyConversationToDialogue(targetDialogue, conversation);
            EditorUtility.SetDirty(_database);
            _selectedNpc = targetNpc;
            _selectedDialogue = targetDialogue;
            _owner?.RefreshAfterLocalizationImport(targetNpc, targetDialogue);
            BuildLayout();
            ShowNotification(new GUIContent(DialogueEditorLocalization.Format(
                "Import: {0} created, {1} updated, {2} missing.",
                report.Created,
                report.Updated,
                report.Missing)));
        }

        private void ExportDialogue()
        {
            if (_selectedDialogue == null)
            {
                EditorUtility.DisplayDialog(
                    DialogueEditorLocalization.Text("Export failed"),
                    DialogueEditorLocalization.Text("Select a dialogue before exporting."),
                    DialogueEditorLocalization.Text("OK"));
                return;
            }

            var path = EditorUtility.SaveFilePanel(
                DialogueEditorLocalization.Text("Export TSV"),
                string.Empty,
                $"{_selectedDialogue.Name}.tsv",
                "tsv");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var report = DialogueLocalizationImportUtility.ExportDialogue(
                _selectedDialogue,
                _exportPrefixField?.value,
                out var tsv);
            File.WriteAllText(path, tsv);
            ShowNotification(new GUIContent(DialogueEditorLocalization.Format(
                "Export: {0} row(s), {1} skipped.",
                report.Exported,
                report.SkippedMissingKey)));
        }

        private NpcEntry EnsureTargetNpc()
        {
            _database.Npcs ??= new List<NpcEntry>();
            if (_selectedNpc != null && _database.Npcs.Contains(_selectedNpc))
            {
                return _selectedNpc;
            }

            var npc = _database.Npcs.FirstOrDefault();
            if (npc != null)
            {
                return npc;
            }

            npc = new NpcEntry { Name = "Imported NPC" };
            _database.Npcs.Add(npc);
            return npc;
        }

        private static DialogueEntry CreateDialogue(NpcEntry npc, string conversationId)
        {
            npc.Dialogues ??= new List<DialogueEntry>();
            var dialogue = new DialogueEntry
            {
                Id = conversationId,
                Name = conversationId
            };
            npc.Dialogues.Add(dialogue);
            return dialogue;
        }

        private string BuildContextSummary()
        {
            var databaseName = _database == null ? DialogueEditorLocalization.Text("None") : _database.name;
            var dialogueName = _selectedDialogue == null ? DialogueEditorLocalization.Text("None") : _selectedDialogue.Name;
            var rowCount = _table?.Rows.Count ?? 0;
            return DialogueEditorLocalization.Format("Database: {0}. Dialogue: {1}. Loaded rows: {2}.", databaseName, dialogueName, rowCount);
        }

        private static Label CreateSectionTitle(string text)
        {
            return new Label(text)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginTop = 12f,
                    marginBottom = 4f
                }
            };
        }
    }
}

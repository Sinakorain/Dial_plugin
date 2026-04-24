using System;
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
        private readonly Dictionary<string, bool> _conversationSelections = new(StringComparer.OrdinalIgnoreCase);
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
                SyncConversationSelections();
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

            AddConversationSelectionUi();

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
            _conversationSelections.Clear();
            SyncConversationSelections(selectAllNewConversations: true);
            BuildLayout();
            ShowNotification(new GUIContent(DialogueEditorLocalization.Format("Loaded {0} dialogue row(s).", _table.Rows.Count)));
        }

        private void ApplySelectedImport()
        {
            ApplyConversations(GetSelectedConversations());
        }

        private void ApplyAllImport()
        {
            ApplyConversations(_table?.GetConversations() ?? new List<DialogueLocalizationConversation>());
        }

        private void ApplyConversations(IReadOnlyList<DialogueLocalizationConversation> conversations)
        {
            if (!CanImport())
            {
                return;
            }

            if (conversations == null || conversations.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    DialogueEditorLocalization.Text("Import failed"),
                    DialogueEditorLocalization.Text("Select at least one conversation to import."),
                    DialogueEditorLocalization.Text("OK"));
                return;
            }

            Undo.RegisterCompleteObjectUndo(_database, "Import Dialogue Localization");
            var report = DialogueLocalizationImportUtility.ApplyConversationsToDatabase(
                _database,
                _selectedNpc,
                conversations,
                out var targetNpc,
                out var targetDialogue);
            EditorUtility.SetDirty(_database);
            if (targetNpc != null)
            {
                _selectedNpc = targetNpc;
            }

            if (targetDialogue != null)
            {
                _selectedDialogue = targetDialogue;
            }

            _owner?.RefreshAfterLocalizationImport(targetNpc, targetDialogue);
            SyncConversationSelections();
            BuildLayout();
            ShowNotification(new GUIContent(DialogueEditorLocalization.Format(
                "Import: {0} conversation(s), {1} dialogue(s) created, {2} dialogue(s) updated, {3} node(s) created, {4} updated, {5} missing.",
                report.ConversationsImported,
                report.DialoguesCreated,
                report.DialoguesUpdated,
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

        private void AddConversationSelectionUi()
        {
            var conversations = _table?.GetConversations() ?? new List<DialogueLocalizationConversation>();
            if (conversations.Count == 0)
            {
                rootVisualElement.Add(new Label(DialogueEditorLocalization.Text("No conversations loaded"))
                {
                    name = "localization-empty-conversation-label"
                });
                return;
            }

            var toolsRow = new VisualElement
            {
                name = "localization-selection-tools"
            };
            toolsRow.style.flexDirection = FlexDirection.Row;
            toolsRow.style.marginTop = 6f;
            toolsRow.style.marginBottom = 4f;
            toolsRow.Add(new Button(() => SetAllConversationSelections(true))
            {
                text = DialogueEditorLocalization.Text("Select All"),
                name = "localization-select-all-button"
            });
            toolsRow.Add(new Button(() => SetAllConversationSelections(false))
            {
                text = DialogueEditorLocalization.Text("Clear"),
                name = "localization-clear-selection-button"
            });
            rootVisualElement.Add(toolsRow);

            var scroll = new ScrollView
            {
                name = "localization-conversation-list"
            };
            scroll.style.maxHeight = 180f;
            scroll.style.marginBottom = 6f;
            foreach (var conversation in conversations)
            {
                var toggle = new Toggle($"{conversation.Id} ({conversation.Rows.Count})")
                {
                    value = IsConversationSelected(conversation.Id),
                    name = "localization-conversation-toggle"
                };
                toggle.RegisterValueChangedCallback(evt =>
                {
                    _conversationSelections[conversation.Id] = evt.newValue;
                });
                scroll.Add(toggle);
            }

            rootVisualElement.Add(scroll);

            var importRow = new VisualElement
            {
                name = "localization-import-actions"
            };
            importRow.style.flexDirection = FlexDirection.Row;
            importRow.style.marginTop = 4f;
            importRow.Add(new Button(ApplySelectedImport)
            {
                text = DialogueEditorLocalization.Text("Import Selected"),
                name = "localization-import-selected-button"
            });
            importRow.Add(new Button(ApplyAllImport)
            {
                text = DialogueEditorLocalization.Text("Import All"),
                name = "localization-import-all-button"
            });
            rootVisualElement.Add(importRow);
        }

        private bool CanImport()
        {
            if (_database != null && _table != null && _table.Rows.Count > 0)
            {
                return true;
            }

            EditorUtility.DisplayDialog(
                DialogueEditorLocalization.Text("Import failed"),
                DialogueEditorLocalization.Text("Load a dialogue database and TSV/CSV table first."),
                DialogueEditorLocalization.Text("OK"));
            return false;
        }

        private IReadOnlyList<DialogueLocalizationConversation> GetSelectedConversations()
        {
            return (_table?.GetConversations() ?? new List<DialogueLocalizationConversation>())
                .Where(conversation => IsConversationSelected(conversation.Id))
                .ToList();
        }

        private bool IsConversationSelected(string conversationId)
        {
            return !string.IsNullOrWhiteSpace(conversationId) &&
                   _conversationSelections.TryGetValue(conversationId, out var selected) &&
                   selected;
        }

        private void SetAllConversationSelections(bool selected)
        {
            foreach (var conversation in _table?.GetConversations() ?? new List<DialogueLocalizationConversation>())
            {
                _conversationSelections[conversation.Id] = selected;
            }

            BuildLayout();
        }

        private void SyncConversationSelections(bool selectAllNewConversations = false)
        {
            var conversationIds = new HashSet<string>(
                (_table?.GetConversations() ?? new List<DialogueLocalizationConversation>())
                .Select(conversation => conversation.Id),
                StringComparer.OrdinalIgnoreCase);

            foreach (var id in _conversationSelections.Keys.ToList())
            {
                if (!conversationIds.Contains(id))
                {
                    _conversationSelections.Remove(id);
                }
            }

            foreach (var id in conversationIds)
            {
                if (!_conversationSelections.ContainsKey(id))
                {
                    _conversationSelections[id] = selectAllNewConversations;
                }
            }
        }

        private string BuildContextSummary()
        {
            var databaseName = _database == null ? DialogueEditorLocalization.Text("None") : _database.name;
            var dialogueName = _selectedDialogue == null ? DialogueEditorLocalization.Text("None") : _selectedDialogue.Name;
            var rowCount = _table?.Rows.Count ?? 0;
            var conversationCount = _table?.GetConversations().Count ?? 0;
            return DialogueEditorLocalization.Format("Database: {0}. Dialogue: {1}. Loaded rows: {2}. Conversations: {3}.", databaseName, dialogueName, rowCount, conversationCount);
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

using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor
{
    public class DialogueEditorWindow : EditorWindow
    {
        private DialogueDatabaseAsset _database;
        private NpcEntry _selectedNpc;
        private DialogueEntry _selectedDialogue;
        private BaseNodeData _selectedNode;
        private bool _hasUnsavedChanges;

        private DialogueGraphView _graphView;
        private ScrollView _hierarchyView;
        private ScrollView _inspectorView;
        private Label _statusLabel;

        public static void Open(DialogueDatabaseAsset asset)
        {
            var window = GetWindow<DialogueEditorWindow>("Dialogue Graph");
            window.minSize = new Vector2(1200f, 700f);
            window.LoadDatabase(asset);
        }

        private void OnDisable()
        {
            SaveAutosave();
        }

        private void CreateGUI()
        {
            BuildLayout();
            RefreshAll();
        }

        private void BuildLayout()
        {
            rootVisualElement.Clear();

            var toolbar = new Toolbar();
            toolbar.Add(CreateToolbarButton("New", DialogueStartWindow.ShowWindow));
            toolbar.Add(CreateToolbarButton("Save", SaveDatabase));
            toolbar.Add(CreateToolbarButton("Load", LoadDatabaseFromDialog));
            toolbar.Add(CreateToolbarButton("Preview", OpenPreview));

            _statusLabel = new Label("No database loaded");
            _statusLabel.style.marginLeft = 12f;
            toolbar.Add(_statusLabel);
            rootVisualElement.Add(toolbar);

            var mainSplit = new TwoPaneSplitView(0, 200f, TwoPaneSplitViewOrientation.Horizontal);
            var palette = BuildPalette();
            mainSplit.Add(palette);

            var contentSplit = new TwoPaneSplitView(1, 340f, TwoPaneSplitViewOrientation.Horizontal);
            _graphView = new DialogueGraphView
            {
                GraphChangedAction = OnGraphChanged,
                SelectionChangedAction = OnNodeSelectionChanged
            };
            contentSplit.Add(_graphView);

            var rightSplit = new TwoPaneSplitView(0, 280f, TwoPaneSplitViewOrientation.Vertical);
            _hierarchyView = new ScrollView();
            _hierarchyView.style.flexGrow = 1f;
            rightSplit.Add(_hierarchyView);

            _inspectorView = new ScrollView();
            _inspectorView.style.flexGrow = 1f;
            rightSplit.Add(_inspectorView);

            contentSplit.Add(rightSplit);
            mainSplit.Add(contentSplit);
            rootVisualElement.Add(mainSplit);
        }

        private ToolbarButton CreateToolbarButton(string text, System.Action onClick)
        {
            return new ToolbarButton(onClick) { text = text };
        }

        private VisualElement BuildPalette()
        {
            var palette = new VisualElement();
            palette.style.paddingLeft = 10f;
            palette.style.paddingRight = 10f;
            palette.style.paddingTop = 10f;
            palette.style.paddingBottom = 10f;

            var title = new Label("Palette");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 8f;
            palette.Add(title);

            palette.Add(CreatePaletteButton("NODE", () =>
            {
                EnsureDialogueSelected();
                if (_selectedDialogue == null)
                {
                    return;
                }

                _graphView?.CreateTextNode(_graphView.GetCanvasCenter());
            }));

            palette.Add(CreatePaletteButton("COMMENT", () =>
            {
                EnsureDialogueSelected();
                if (_selectedDialogue == null)
                {
                    return;
                }

                _graphView?.CreateCommentNode(_graphView.GetCanvasCenter());
            }));

            palette.Add(CreatePaletteButton("FUNCTION (Not in MVP)", null, false));
            palette.Add(CreatePaletteButton("SCENE (Not in MVP)", null, false));
            palette.Add(CreatePaletteButton("DEBUG (Not in MVP)", null, false));

            return palette;
        }

        private Button CreatePaletteButton(string text, System.Action onClick, bool enabled = true)
        {
            var button = new Button(onClick) { text = text };
            button.SetEnabled(enabled);
            button.style.marginBottom = 8f;
            button.style.height = 32f;
            return button;
        }

        private Button CreateActionButton(string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.marginRight = 6f;
            button.style.marginBottom = 6f;
            return button;
        }

        private void LoadDatabase(DialogueDatabaseAsset asset)
        {
            _database = asset;
            _selectedNode = null;
            var restoredAutosave = false;

            if (_database != null)
            {
                restoredAutosave = DialogueEditorAutosaveStore.TryLoadSnapshot(_database, DialogueEditorAutosaveStore.GetStorageKey(_database));
                _selectedNpc = _database.Npcs.FirstOrDefault();
                _selectedDialogue = _selectedNpc?.Dialogues.FirstOrDefault();
            }
            else
            {
                _selectedNpc = null;
                _selectedDialogue = null;
            }

            _hasUnsavedChanges = restoredAutosave;
            RefreshAll();
        }

        private void LoadDatabaseFromDialog()
        {
            var absolutePath = EditorUtility.OpenFilePanel("Load Dialogue Database", Application.dataPath, "asset");
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return;
            }

            var relativePath = FileUtil.GetProjectRelativePath(absolutePath);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                EditorUtility.DisplayDialog("Load failed", "The selected asset must live inside the current Unity project.", "OK");
                return;
            }

            LoadDatabase(AssetDatabase.LoadAssetAtPath<DialogueDatabaseAsset>(relativePath));
        }

        private void SaveDatabase()
        {
            if (_database == null)
            {
                return;
            }

            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
            DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(_database));
            _hasUnsavedChanges = false;
            RefreshStatus();
            ShowNotification(new GUIContent("Dialogue database saved."));
        }

        private void SaveAutosave()
        {
            if (_database == null || !_hasUnsavedChanges)
            {
                return;
            }

            DialogueEditorAutosaveStore.SaveSnapshot(_database, DialogueEditorAutosaveStore.GetStorageKey(_database));
        }

        private void RefreshAll()
        {
            if (_graphView == null)
            {
                return;
            }

            RefreshStatus();
            RefreshHierarchy();
            _graphView.LoadGraph(_selectedDialogue?.Graph);
            RefreshInspector();
        }

        private void RefreshStatus()
        {
            _statusLabel.text = _database == null
                ? "No database loaded"
                : $"{_database.name}{(_hasUnsavedChanges ? " (unsaved changes)" : string.Empty)}";
        }

        private void RefreshHierarchy()
        {
            if (_hierarchyView == null)
            {
                return;
            }

            _hierarchyView.Clear();

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginBottom = 10f;

            actions.Add(CreateActionButton("Create NPC", CreateNpc));
            actions.Add(CreateActionButton("Create Dialogue", CreateDialogue));
            actions.Add(CreateActionButton("Delete", DeleteSelection));
            actions.Add(CreateActionButton("Cond", ShowDialogueConditions));
            _hierarchyView.Add(actions);

            if (_database == null)
            {
                _hierarchyView.Add(new Label("Load or create a dialogue database to begin."));
                return;
            }

            foreach (var npc in _database.Npcs)
            {
                var npcBox = new Box();
                npcBox.style.marginBottom = 8f;

                var npcHeader = new VisualElement();
                npcHeader.style.flexDirection = FlexDirection.Row;
                npcHeader.style.alignItems = Align.Center;

                var npcSelect = new Button(() =>
                {
                    _selectedNpc = npc;
                    if (_selectedDialogue == null)
                    {
                        _selectedDialogue = npc.Dialogues.FirstOrDefault();
                    }
                    _selectedNode = null;
                    RefreshAll();
                })
                {
                    text = _selectedNpc == npc ? "Selected NPC" : "Select NPC"
                };
                npcSelect.style.marginRight = 6f;
                npcHeader.Add(npcSelect);

                var npcName = new TextField { value = npc.Name };
                npcName.style.flexGrow = 1f;
                npcName.RegisterValueChangedCallback(evt =>
                {
                    npc.Name = evt.newValue;
                    MarkChanged();
                });
                npcHeader.Add(npcName);
                npcBox.Add(npcHeader);

                foreach (var dialogue in npc.Dialogues)
                {
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;
                    row.style.marginTop = 6f;

                    var selectButton = new Button(() =>
                    {
                        _selectedNpc = npc;
                        _selectedDialogue = dialogue;
                        _selectedNode = null;
                        RefreshAll();
                    })
                    {
                        text = _selectedDialogue == dialogue ? "Selected" : "Open"
                    };
                    selectButton.style.marginRight = 6f;
                    row.Add(selectButton);

                    var nameField = new TextField { value = dialogue.Name };
                    nameField.style.flexGrow = 1f;
                    nameField.RegisterValueChangedCallback(evt =>
                    {
                        dialogue.Name = evt.newValue;
                        MarkChanged();
                    });
                    row.Add(nameField);
                    npcBox.Add(row);
                }

                _hierarchyView.Add(npcBox);
            }
        }

        private void RefreshInspector()
        {
            if (_inspectorView == null)
            {
                return;
            }

            _inspectorView.Clear();

            if (_database == null)
            {
                _inspectorView.Add(new Label("No database loaded."));
                return;
            }

            if (_selectedNode is DialogueTextNodeData textNode)
            {
                BuildTextNodeInspector(textNode);
                return;
            }

            if (_selectedNode is CommentNodeData commentNode)
            {
                BuildCommentNodeInspector(commentNode);
                return;
            }

            if (_selectedDialogue != null)
            {
                BuildDialogueInspector(_selectedDialogue);
                return;
            }

            _inspectorView.Add(new Label("Select an NPC, dialogue, or node to inspect it."));
        }

        private void BuildDialogueInspector(DialogueEntry dialogue)
        {
            _inspectorView.Add(CreateSectionTitle("Dialogue"));

            var nameField = new TextField("Name") { value = dialogue.Name };
            nameField.RegisterValueChangedCallback(evt =>
            {
                dialogue.Name = evt.newValue;
                MarkChanged();
            });
            _inspectorView.Add(nameField);

            _inspectorView.Add(new Label($"Id: {dialogue.Id}"));
            _inspectorView.Add(new Label($"Start node: {DialogueGraphUtility.FindStartNode(dialogue)?.Title ?? "None"}"));
            BuildConditionEditor(dialogue.StartCondition, "Start Condition");
        }

        private void BuildTextNodeInspector(DialogueTextNodeData node)
        {
            _inspectorView.Add(CreateSectionTitle("Text Node"));

            var titleField = new TextField("Title") { value = node.Title };
            titleField.RegisterValueChangedCallback(evt =>
            {
                node.Title = evt.newValue;
                _graphView.RefreshNodeVisuals();
                MarkChanged();
            });
            _inspectorView.Add(titleField);

            var bodyField = new TextField("BodyText")
            {
                value = node.BodyText,
                multiline = true
            };
            bodyField.style.minHeight = 120f;
            bodyField.RegisterValueChangedCallback(evt =>
            {
                node.BodyText = evt.newValue;
                MarkChanged();
            });
            _inspectorView.Add(bodyField);

            var startToggle = new Toggle("IsStartNode") { value = node.IsStartNode };
            startToggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    DialogueGraphUtility.EnsureSingleStartNode(_selectedDialogue.Graph, node.Id);
                }
                else if (DialogueGraphUtility.FindStartNode(_selectedDialogue) == node)
                {
                    node.IsStartNode = false;
                }

                _graphView.RefreshNodeVisuals();
                MarkChanged();
                RefreshInspector();
            });
            _inspectorView.Add(startToggle);

            var choiceToggle = new Toggle("UseOutputsAsChoices") { value = node.UseOutputsAsChoices };
            choiceToggle.RegisterValueChangedCallback(evt =>
            {
                node.UseOutputsAsChoices = evt.newValue;
                MarkChanged();
            });
            _inspectorView.Add(choiceToggle);

            BuildConditionEditor(node.Condition, "Condition");
            BuildLinksInspector(node);

            var deleteButton = new Button(() => _graphView.DeleteNode(node)) { text = "Delete Node" };
            deleteButton.style.marginTop = 10f;
            _inspectorView.Add(deleteButton);
        }

        private void BuildCommentNodeInspector(CommentNodeData node)
        {
            _inspectorView.Add(CreateSectionTitle("Comment Node"));

            var titleField = new TextField("Title") { value = node.Title };
            titleField.RegisterValueChangedCallback(evt =>
            {
                node.Title = evt.newValue;
                _graphView.RefreshNodeVisuals();
                MarkChanged();
            });
            _inspectorView.Add(titleField);

            var commentField = new TextField("Comment")
            {
                value = node.Comment,
                multiline = true
            };
            commentField.style.minHeight = 120f;
            commentField.RegisterValueChangedCallback(evt =>
            {
                node.Comment = evt.newValue;
                _graphView.RefreshNodeVisuals();
                MarkChanged();
            });
            _inspectorView.Add(commentField);

            var areaField = new RectField("Area") { value = node.Area };
            areaField.RegisterValueChangedCallback(evt =>
            {
                node.Area = evt.newValue;
                node.Position = evt.newValue.position;
                _graphView.LoadGraph(_selectedDialogue?.Graph);
                MarkChanged();
            });
            _inspectorView.Add(areaField);

            var deleteButton = new Button(() => _graphView.DeleteNode(node)) { text = "Delete Comment" };
            deleteButton.style.marginTop = 10f;
            _inspectorView.Add(deleteButton);
        }

        private void BuildLinksInspector(DialogueTextNodeData node)
        {
            _inspectorView.Add(CreateSectionTitle("Links"));

            var links = DialogueGraphUtility.GetOutgoingLinks(_selectedDialogue.Graph, node.Id);
            if (links.Count == 0)
            {
                _inspectorView.Add(new Label("No outputs yet. Use the + button on the node or connect a port on the graph."));
                return;
            }

            foreach (var link in links)
            {
                var box = new Box();
                box.style.marginBottom = 8f;

                var target = DialogueGraphUtility.GetTextNode(_selectedDialogue.Graph, link.ToNodeId);
                box.Add(new Label($"Target: {target?.Title ?? "Unconnected"}"));

                var orderField = new IntegerField("Order") { value = link.Order };
                orderField.RegisterValueChangedCallback(evt =>
                {
                    link.Order = Mathf.Max(0, evt.newValue);
                    DialogueGraphUtility.NormalizeLinkOrder(_selectedDialogue.Graph, node.Id);
                    _graphView.LoadGraph(_selectedDialogue.Graph);
                    MarkChanged();
                    RefreshInspector();
                });
                box.Add(orderField);

                var choiceField = new TextField("ChoiceText") { value = link.ChoiceText };
                choiceField.RegisterValueChangedCallback(evt =>
                {
                    link.ChoiceText = evt.newValue;
                    _graphView.RefreshNodeVisuals();
                    MarkChanged();
                });
                box.Add(choiceField);

                var removeButton = new Button(() =>
                {
                    _graphView.RemoveOutput(link);
                    RefreshInspector();
                })
                {
                    text = "Remove Output"
                };
                box.Add(removeButton);

                _inspectorView.Add(box);
            }
        }

        private void BuildConditionEditor(ConditionData condition, string title)
        {
            _inspectorView.Add(CreateSectionTitle(title));

            var typeField = new EnumField("Type", condition.Type);
            typeField.RegisterValueChangedCallback(evt =>
            {
                condition.Type = (ConditionType)evt.newValue;
                MarkChanged();
            });
            _inspectorView.Add(typeField);

            var keyField = new TextField("Key") { value = condition.Key };
            keyField.RegisterValueChangedCallback(evt =>
            {
                condition.Key = evt.newValue;
                MarkChanged();
            });
            _inspectorView.Add(keyField);

            var operatorField = new TextField("Operator") { value = condition.Operator };
            operatorField.RegisterValueChangedCallback(evt =>
            {
                condition.Operator = evt.newValue;
                MarkChanged();
            });
            _inspectorView.Add(operatorField);

            var valueField = new TextField("Value") { value = condition.Value };
            valueField.RegisterValueChangedCallback(evt =>
            {
                condition.Value = evt.newValue;
                MarkChanged();
            });
            _inspectorView.Add(valueField);
        }

        private Label CreateSectionTitle(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 10f;
            label.style.marginBottom = 6f;
            return label;
        }

        private void CreateNpc()
        {
            if (!EnsureDatabaseLoaded())
            {
                return;
            }

            var npc = new NpcEntry
            {
                Name = $"NPC {_database.Npcs.Count + 1}"
            };

            _database.Npcs.Add(npc);
            _selectedNpc = npc;
            _selectedDialogue = npc.Dialogues.FirstOrDefault();
            _selectedNode = null;
            MarkChanged();
            RefreshAll();
        }

        private void CreateDialogue()
        {
            if (!EnsureDatabaseLoaded())
            {
                return;
            }

            if (_selectedNpc == null)
            {
                CreateNpc();
            }

            var dialogue = new DialogueEntry
            {
                Name = $"Dialogue {_selectedNpc.Dialogues.Count + 1}"
            };

            var startNode = new DialogueTextNodeData
            {
                Title = "Start",
                BodyText = "New dialogue starts here.",
                IsStartNode = true,
                Position = new Vector2(100f, 100f)
            };
            dialogue.Graph.Nodes.Add(startNode);
            _selectedNpc.Dialogues.Add(dialogue);
            _selectedDialogue = dialogue;
            _selectedNode = startNode;
            MarkChanged();
            RefreshAll();
        }

        private void DeleteSelection()
        {
            if (_selectedNode != null)
            {
                _graphView.DeleteNode(_selectedNode);
                return;
            }

            if (_selectedDialogue != null && _selectedNpc != null)
            {
                _selectedNpc.Dialogues.Remove(_selectedDialogue);
                _selectedDialogue = _selectedNpc.Dialogues.FirstOrDefault();
                _selectedNode = null;
                MarkChanged();
                RefreshAll();
                return;
            }

            if (_selectedNpc != null && _database != null)
            {
                _database.Npcs.Remove(_selectedNpc);
                _selectedNpc = _database.Npcs.FirstOrDefault();
                _selectedDialogue = _selectedNpc?.Dialogues.FirstOrDefault();
                _selectedNode = null;
                MarkChanged();
                RefreshAll();
            }
        }

        private void ShowDialogueConditions()
        {
            _selectedNode = null;
            RefreshInspector();
        }

        private void OpenPreview()
        {
            if (_selectedDialogue == null)
            {
                EditorUtility.DisplayDialog("No dialogue selected", "Select a dialogue to preview it.", "OK");
                return;
            }

            DialoguePreviewWindow.ShowWindow(_selectedDialogue);
        }

        private void OnGraphChanged()
        {
            if (_selectedDialogue == null)
            {
                return;
            }

            foreach (var textNode in _selectedDialogue.Graph.Nodes.OfType<DialogueTextNodeData>())
            {
                DialogueGraphUtility.NormalizeLinkOrder(_selectedDialogue.Graph, textNode.Id);
            }

            MarkChanged();
            RefreshInspector();
        }

        private void OnNodeSelectionChanged(BaseNodeData node)
        {
            _selectedNode = node;
            RefreshInspector();
        }

        private bool EnsureDatabaseLoaded()
        {
            if (_database != null)
            {
                return true;
            }

            EditorUtility.DisplayDialog("No database loaded", "Create or load a dialogue database first.", "OK");
            return false;
        }

        private void EnsureDialogueSelected()
        {
            if (_selectedDialogue == null)
            {
                if (_database == null)
                {
                    EditorUtility.DisplayDialog("No database loaded", "Create or load a dialogue database first.", "OK");
                    return;
                }

                if (_selectedNpc == null)
                {
                    CreateNpc();
                }

                CreateDialogue();
            }
        }

        private void MarkChanged()
        {
            _hasUnsavedChanges = true;
            SaveAutosave();
            RefreshStatus();
        }
    }
}

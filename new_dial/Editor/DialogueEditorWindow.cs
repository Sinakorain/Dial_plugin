using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor
{
    public class DialogueEditorWindow : EditorWindow
    {
        private const string EditorStyleSheetSearchQuery = "DialogueEditorStyles t:StyleSheet";

        private DialogueDatabaseAsset _database;
        private NpcEntry _selectedNpc;
        private DialogueEntry _selectedDialogue;
        private BaseNodeData _selectedNode;
        private bool _hasUnsavedChanges;
        private bool _stylesApplied;
        private bool _detailsCollapsed;

        private DialogueGraphView _graphView;
        private ScrollView _projectView;
        private ScrollView _inspectorView;
        private Label _statusLabel;
        private Label _detailsTitleLabel;
        private Button _detailsToggleButton;

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
            ApplyStyles();
            BuildLayout();
            RefreshAll();
        }

        private void BuildLayout()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("dialogue-editor");

            rootVisualElement.Add(BuildToolbar());

            var shell = new TwoPaneSplitView(0, 270f, TwoPaneSplitViewOrientation.Horizontal);
            shell.AddToClassList("dialogue-editor__shell");
            shell.Add(BuildLeftDock());

            var contentSplit = new TwoPaneSplitView(1, 360f, TwoPaneSplitViewOrientation.Horizontal);
            contentSplit.AddToClassList("dialogue-editor__content-split");

            var graphHost = new VisualElement();
            graphHost.AddToClassList("dialogue-editor__graph-host");

            var graphHeader = new VisualElement();
            graphHeader.AddToClassList("dialogue-editor__panel-header");
            graphHeader.Add(new Label("Graph Canvas") { name = "graph-title" });
            graphHost.Add(graphHeader);

            _graphView = new DialogueGraphView
            {
                GraphChangedAction = OnGraphChanged,
                SelectionChangedAction = OnNodeSelectionChanged
            };
            _graphView.AddToClassList("dialogue-editor__graph-surface");
            graphHost.Add(_graphView);
            contentSplit.Add(graphHost);

            contentSplit.Add(BuildDetailsPanel());
            shell.Add(contentSplit);

            rootVisualElement.Add(shell);
            SetDetailsCollapsed(_detailsCollapsed);
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("dialogue-editor__toolbar");

            toolbar.Add(CreateToolbarButton("New", DialogueStartWindow.ShowWindow));
            toolbar.Add(CreateToolbarButton("Load", LoadDatabaseFromDialog));
            toolbar.Add(CreateToolbarButton("Save", SaveDatabase));
            toolbar.Add(CreateToolbarButton("Preview", OpenPreview));
            toolbar.Add(CreateToolbarButton("Create NPC", CreateNpc));
            toolbar.Add(CreateToolbarButton("Create Dialogue", CreateDialogue));
            toolbar.Add(CreateToolbarButton("Delete", DeleteSelection));
            toolbar.Add(CreateToolbarButton("Dialogue Settings", ShowDialogueSettings));

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            toolbar.Add(spacer);

            _statusLabel = new Label("No database loaded");
            _statusLabel.AddToClassList("dialogue-editor__status");
            toolbar.Add(_statusLabel);
            return toolbar;
        }

        private VisualElement BuildLeftDock()
        {
            var dock = new VisualElement();
            dock.AddToClassList("dialogue-editor__left-dock");

            var projectSection = new VisualElement();
            projectSection.AddToClassList("dialogue-editor__panel");
            projectSection.Add(BuildPanelHeader("Project"));

            _projectView = new ScrollView();
            _projectView.AddToClassList("dialogue-editor__project-scroll");
            projectSection.Add(_projectView);
            dock.Add(projectSection);

            var paletteSection = BuildPalette();
            dock.Add(paletteSection);

            return dock;
        }

        private VisualElement BuildPalette()
        {
            var panel = new VisualElement();
            panel.AddToClassList("dialogue-editor__panel");
            panel.Add(BuildPanelHeader("Palette"));

            var content = new VisualElement();
            content.AddToClassList("dialogue-editor__palette");

            content.Add(new PaletteItem(this, DialoguePaletteItemType.TextNode, "Text Node", "Click to add at center or drag onto the graph."));
            content.Add(new PaletteItem(this, DialoguePaletteItemType.Comment, "Comment", "Click to add at center or drag onto the graph."));
            content.Add(CreatePaletteButton("Function (Not in MVP)", null, false));
            content.Add(CreatePaletteButton("Scene (Not in MVP)", null, false));
            content.Add(CreatePaletteButton("Debug (Not in MVP)", null, false));

            panel.Add(content);
            return panel;
        }

        private VisualElement BuildDetailsPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("dialogue-editor__panel");
            panel.AddToClassList("dialogue-editor__details-panel");

            var header = new VisualElement();
            header.AddToClassList("dialogue-editor__panel-header");

            _detailsTitleLabel = new Label("Details");
            _detailsTitleLabel.AddToClassList("dialogue-editor__details-title");
            header.Add(_detailsTitleLabel);

            _detailsToggleButton = new Button(() => SetDetailsCollapsed(!_detailsCollapsed))
            {
                text = "Hide"
            };
            _detailsToggleButton.AddToClassList("dialogue-editor__panel-toggle");
            header.Add(_detailsToggleButton);
            panel.Add(header);

            _inspectorView = new ScrollView();
            _inspectorView.AddToClassList("dialogue-editor__details-scroll");
            panel.Add(_inspectorView);

            return panel;
        }

        private ToolbarButton CreateToolbarButton(string text, System.Action onClick)
        {
            var button = new ToolbarButton(onClick) { text = text };
            button.AddToClassList("dialogue-editor__toolbar-button");
            return button;
        }

        private Button CreatePaletteButton(string text, System.Action onClick, bool enabled = true)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("dialogue-editor__palette-button");
            button.SetEnabled(enabled);
            return button;
        }

        private Button CreateActionButton(string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("dialogue-editor__action-button");
            return button;
        }

        private VisualElement BuildPanelHeader(string title)
        {
            var header = new VisualElement();
            header.AddToClassList("dialogue-editor__panel-header");

            var label = new Label(title);
            label.AddToClassList("dialogue-editor__panel-title");
            header.Add(label);
            return header;
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
            RefreshProjectPanel();
            _graphView.LoadGraph(_selectedDialogue?.Graph);
            RefreshInspector();
        }

        private void RefreshStatus()
        {
            _statusLabel.text = _database == null
                ? "No database loaded"
                : $"{_database.name}{(_hasUnsavedChanges ? " (unsaved changes)" : string.Empty)}";
        }

        private void RefreshProjectPanel()
        {
            if (_projectView == null)
            {
                return;
            }

            _projectView.Clear();

            if (_database == null)
            {
                _projectView.Add(CreateEmptyPanelMessage("Load or create a dialogue database to begin."));
                return;
            }

            foreach (var npc in _database.Npcs)
            {
                var npcCard = new Box();
                npcCard.AddToClassList("dialogue-editor__project-card");
                npcCard.EnableInClassList("is-selected", _selectedNpc == npc);

                var npcHeader = new VisualElement();
                npcHeader.AddToClassList("dialogue-editor__row");

                var npcSelect = CreateActionButton(_selectedNpc == npc ? "Selected NPC" : "Select NPC", () =>
                {
                    _selectedNpc = npc;
                    if (_selectedDialogue == null || !_selectedNpc.Dialogues.Contains(_selectedDialogue))
                    {
                        _selectedDialogue = npc.Dialogues.FirstOrDefault();
                    }

                    _selectedNode = null;
                    RefreshAll();
                });
                npcSelect.EnableInClassList("is-selected", _selectedNpc == npc);
                npcHeader.Add(npcSelect);

                var npcName = new TextField("Name") { value = npc.Name };
                npcName.AddToClassList("dialogue-editor__grow");
                npcName.RegisterValueChangedCallback(evt =>
                {
                    npc.Name = evt.newValue;
                    MarkChanged();
                    RefreshProjectPanel();
                });
                npcHeader.Add(npcName);
                npcCard.Add(npcHeader);

                if (npc.Dialogues.Count == 0)
                {
                    npcCard.Add(CreateInlineHelp("No dialogues yet. Use Create Dialogue from the toolbar."));
                }

                foreach (var dialogue in npc.Dialogues)
                {
                    var dialogueRow = new VisualElement();
                    dialogueRow.AddToClassList("dialogue-editor__row");
                    dialogueRow.AddToClassList("dialogue-editor__dialogue-row");
                    dialogueRow.EnableInClassList("is-selected", _selectedDialogue == dialogue);

                    var openButton = CreateActionButton(_selectedDialogue == dialogue ? "Editing" : "Open", () =>
                    {
                        _selectedNpc = npc;
                        _selectedDialogue = dialogue;
                        _selectedNode = null;
                        RefreshAll();
                    });
                    openButton.EnableInClassList("is-selected", _selectedDialogue == dialogue);
                    dialogueRow.Add(openButton);

                    var nameField = new TextField("Dialogue") { value = dialogue.Name };
                    nameField.AddToClassList("dialogue-editor__grow");
                    nameField.RegisterValueChangedCallback(evt =>
                    {
                        dialogue.Name = evt.newValue;
                        MarkChanged();
                        RefreshProjectPanel();
                    });
                    dialogueRow.Add(nameField);

                    npcCard.Add(dialogueRow);
                }

                _projectView.Add(npcCard);
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
                _detailsTitleLabel.text = "Details";
                _inspectorView.Add(CreateEmptyPanelMessage("No database loaded."));
                return;
            }

            if (_selectedNode is DialogueTextNodeData textNode)
            {
                _detailsTitleLabel.text = "Node Details";
                BuildTextNodeInspector(textNode);
                return;
            }

            if (_selectedNode is CommentNodeData commentNode)
            {
                _detailsTitleLabel.text = "Comment Details";
                BuildCommentNodeInspector(commentNode);
                return;
            }

            if (_selectedDialogue != null)
            {
                _detailsTitleLabel.text = "Dialogue Settings";
                BuildDialogueInspector(_selectedDialogue);
                return;
            }

            _detailsTitleLabel.text = "Details";
            _inspectorView.Add(CreateEmptyPanelMessage("Select an NPC, a dialogue, or a node to inspect it."));
        }

        private void BuildDialogueInspector(DialogueEntry dialogue)
        {
            _inspectorView.Add(CreateSectionTitle("Dialogue Settings"));

            var nameField = new TextField("Name") { value = dialogue.Name };
            nameField.RegisterValueChangedCallback(evt =>
            {
                dialogue.Name = evt.newValue;
                MarkChanged();
                RefreshProjectPanel();
            });
            _inspectorView.Add(nameField);

            var summaryCard = new Box();
            summaryCard.AddToClassList("dialogue-editor__inspector-card");
            summaryCard.Add(new Label($"Id: {dialogue.Id}"));
            summaryCard.Add(new Label($"Nodes: {dialogue.Graph.Nodes.Count}"));
            summaryCard.Add(new Label($"Start node: {DialogueGraphUtility.FindStartNode(dialogue)?.Title ?? "None"}"));
            _inspectorView.Add(summaryCard);

            BuildConditionEditor(dialogue.StartCondition, "Start Condition");
            _inspectorView.Add(CreateInlineHelp("Click empty graph space to return here after editing a node."));
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

            var bodyField = new TextField("Body Text")
            {
                value = node.BodyText,
                multiline = true
            };
            bodyField.AddToClassList("dialogue-editor__multiline-field");
            bodyField.RegisterValueChangedCallback(evt =>
            {
                node.BodyText = evt.newValue;
                _graphView.RefreshNodeVisuals();
                MarkChanged();
            });
            _inspectorView.Add(bodyField);

            var startToggle = new Toggle("Is Start Node") { value = node.IsStartNode };
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

            var choiceToggle = new Toggle("Use Outputs As Choices") { value = node.UseOutputsAsChoices };
            choiceToggle.RegisterValueChangedCallback(evt =>
            {
                node.UseOutputsAsChoices = evt.newValue;
                _graphView.RefreshNodeVisuals();
                MarkChanged();
            });
            _inspectorView.Add(choiceToggle);

            BuildConditionEditor(node.Condition, "Condition");
            BuildLinksInspector(node);

            var deleteButton = new Button(() => _graphView.DeleteNode(node)) { text = "Delete Node" };
            deleteButton.AddToClassList("dialogue-editor__danger-button");
            _inspectorView.Add(deleteButton);
        }

        private void BuildCommentNodeInspector(CommentNodeData node)
        {
            _inspectorView.Add(CreateSectionTitle("Comment"));

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
            commentField.AddToClassList("dialogue-editor__multiline-field");
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
            deleteButton.AddToClassList("dialogue-editor__danger-button");
            _inspectorView.Add(deleteButton);
        }

        private void BuildLinksInspector(DialogueTextNodeData node)
        {
            _inspectorView.Add(CreateSectionTitle("Connected Links"));

            var links = DialogueGraphUtility
                .GetOutgoingLinks(_selectedDialogue.Graph, node.Id)
                .Where(link => !string.IsNullOrWhiteSpace(link.ToNodeId))
                .ToList();

            if (links.Count == 0)
            {
                _inspectorView.Add(CreateInlineHelp("No linked outputs yet. Drag from the bottom of this node to the top of another node."));
                return;
            }

            foreach (var link in links)
            {
                var box = new Box();
                box.AddToClassList("dialogue-editor__inspector-card");

                var target = DialogueGraphUtility.GetTextNode(_selectedDialogue.Graph, link.ToNodeId);
                box.Add(new Label($"Target: {target?.Title ?? "Unconnected"}"));

                var orderField = new IntegerField("Order") { value = link.Order };
                orderField.RegisterValueChangedCallback(evt =>
                {
                    link.Order = Mathf.Max(0, evt.newValue);
                    DialogueGraphUtility.NormalizeLinkOrder(_selectedDialogue.Graph, node.Id);
                    _graphView.RefreshNodeVisuals();
                    MarkChanged();
                    RefreshInspector();
                });
                box.Add(orderField);

                var choiceField = new TextField("Choice Text") { value = link.ChoiceText };
                choiceField.RegisterValueChangedCallback(evt =>
                {
                    link.ChoiceText = evt.newValue;
                    _graphView.RefreshNodeVisuals();
                    MarkChanged();
                });
                box.Add(choiceField);

                var removeButton = new Button(() =>
                {
                    _graphView.DeleteLink(link, true, true);
                    RefreshInspector();
                })
                {
                    text = "Remove Link"
                };
                removeButton.AddToClassList("dialogue-editor__danger-button");
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
            label.AddToClassList("dialogue-editor__section-title");
            return label;
        }

        private VisualElement CreateEmptyPanelMessage(string text)
        {
            var label = new Label(text);
            label.AddToClassList("dialogue-editor__empty-message");
            return label;
        }

        private VisualElement CreateInlineHelp(string text)
        {
            var label = new Label(text);
            label.AddToClassList("dialogue-editor__inline-help");
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

        private void AddNodeFromToolbar()
        {
            CreateNodeFromPalette(DialoguePaletteItemType.TextNode);
        }

        private void AddCommentFromToolbar()
        {
            CreateNodeFromPalette(DialoguePaletteItemType.Comment);
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

        private void ShowDialogueSettings()
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

            DialoguePreviewWindow.ShowWindow(_selectedDialogue, this);
        }

        public bool FocusDialogueNode(DialogueEntry dialogue, string nodeId)
        {
            if (_database == null || dialogue == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            if (!TryResolveDialogue(dialogue, out var npc, out var resolvedDialogue))
            {
                return false;
            }

            var resolvedNode = DialogueGraphUtility.GetNode(resolvedDialogue.Graph, nodeId);
            if (resolvedNode == null)
            {
                return false;
            }

            _selectedNpc = npc;
            _selectedDialogue = resolvedDialogue;
            _selectedNode = resolvedNode;
            RefreshAll();
            _graphView?.FrameAndSelectNode(nodeId);
            Focus();
            return true;
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

        private void CreateNodeFromPalette(DialoguePaletteItemType itemType)
        {
            EnsureDialogueSelected();
            if (_selectedDialogue == null)
            {
                return;
            }

            switch (itemType)
            {
                case DialoguePaletteItemType.TextNode:
                    _graphView?.CreateTextNode(_graphView.GetCanvasCenter());
                    break;
                case DialoguePaletteItemType.Comment:
                    _graphView?.CreateCommentNode(_graphView.GetCanvasCenter());
                    break;
            }
        }

        private bool BeginPalettePlacement(DialoguePaletteItemType itemType, Vector2 worldPointerPosition)
        {
            EnsureDialogueSelected();
            if (_selectedDialogue == null || _graphView == null)
            {
                return false;
            }

            return _graphView.BeginNodePlacement(itemType, worldPointerPosition);
        }

        private void UpdatePalettePlacement(Vector2 worldPointerPosition)
        {
            _graphView?.UpdateNodePlacement(worldPointerPosition);
        }

        private void CommitPalettePlacement(Vector2 worldPointerPosition)
        {
            if (_graphView == null)
            {
                return;
            }

            if (!_graphView.CommitNodePlacement(worldPointerPosition))
            {
                _graphView.CancelNodePlacement();
            }
        }

        private void CancelPalettePlacement()
        {
            _graphView?.CancelNodePlacement();
        }

        private void MarkChanged()
        {
            _hasUnsavedChanges = true;
            SaveAutosave();
            RefreshStatus();
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

        private void SetDetailsCollapsed(bool collapsed)
        {
            _detailsCollapsed = collapsed;

            if (_inspectorView != null)
            {
                _inspectorView.style.display = collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (_detailsToggleButton != null)
            {
                _detailsToggleButton.text = collapsed ? "Show" : "Hide";
            }
        }

        private bool TryResolveDialogue(DialogueEntry dialogue, out NpcEntry ownerNpc, out DialogueEntry resolvedDialogue)
        {
            foreach (var npc in _database.Npcs)
            {
                foreach (var candidate in npc.Dialogues)
                {
                    if (ReferenceEquals(candidate, dialogue) || candidate.Id == dialogue.Id)
                    {
                        ownerNpc = npc;
                        resolvedDialogue = candidate;
                        return true;
                    }
                }
            }

            ownerNpc = null;
            resolvedDialogue = null;
            return false;
        }

        private sealed class PaletteItem : VisualElement
        {
            private const float DragThreshold = 6f;

            private readonly DialogueEditorWindow _owner;
            private readonly DialoguePaletteItemType _itemType;
            private bool _pressed;
            private bool _placementStarted;
            private Vector2 _pressWorldPosition;

            public PaletteItem(DialogueEditorWindow owner, DialoguePaletteItemType itemType, string title, string hint)
            {
                _owner = owner;
                _itemType = itemType;

                AddToClassList("dialogue-editor__palette-item");

                var titleLabel = new Label(title);
                titleLabel.AddToClassList("dialogue-editor__palette-item-title");
                Add(titleLabel);

                var hintLabel = new Label(hint);
                hintLabel.AddToClassList("dialogue-editor__palette-item-hint");
                Add(hintLabel);

                RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
                RegisterCallback<MouseMoveEvent>(OnMouseMove, TrickleDown.TrickleDown);
                RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
                RegisterCallback<MouseCaptureOutEvent>(_ => ResetDragState());
            }

            private void OnMouseDown(MouseDownEvent evt)
            {
                if (evt.button != 0)
                {
                    return;
                }

                _pressed = true;
                _placementStarted = false;
                _pressWorldPosition = this.LocalToWorld(evt.localMousePosition);
                this.CaptureMouse();
                evt.StopImmediatePropagation();
            }

            private void OnMouseMove(MouseMoveEvent evt)
            {
                if (!_pressed)
                {
                    return;
                }

                var worldPointerPosition = this.LocalToWorld(evt.localMousePosition);
                if (!_placementStarted &&
                    Vector2.Distance(_pressWorldPosition, worldPointerPosition) >= DragThreshold &&
                    _owner.BeginPalettePlacement(_itemType, worldPointerPosition))
                {
                    _placementStarted = true;
                    AddToClassList("is-dragging");
                }

                if (_placementStarted)
                {
                    _owner.UpdatePalettePlacement(worldPointerPosition);
                }

                evt.StopImmediatePropagation();
            }

            private void OnMouseUp(MouseUpEvent evt)
            {
                if (!_pressed || evt.button != 0)
                {
                    return;
                }

                var worldPointerPosition = this.LocalToWorld(evt.localMousePosition);
                if (_placementStarted)
                {
                    _owner.CommitPalettePlacement(worldPointerPosition);
                }
                else
                {
                    _owner.CreateNodeFromPalette(_itemType);
                }

                ResetDragState();
                evt.StopImmediatePropagation();
            }

            private void ResetDragState()
            {
                if (_placementStarted)
                {
                    _owner.CancelPalettePlacement();
                }

                _pressed = false;
                _placementStarted = false;
                RemoveFromClassList("is-dragging");
                if (this.HasMouseCapture())
                {
                    this.ReleaseMouse();
                }
            }
        }
    }
}

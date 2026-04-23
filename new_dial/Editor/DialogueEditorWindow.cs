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
        private bool _isAssemblyReloading;
        private bool _isEditorQuitting;
        private bool _hasHandledClosePrompt;
        private bool _lifecycleHooksRegistered;

        private DialogueGraphView _graphView;
        private ScrollView _projectView;
        private ScrollView _inspectorView;
        private Label _statusLabel;
        private Label _detailsTitleLabel;
        private Button _detailsToggleButton;
        private Button _focusStartButton;
        private VisualElement _graphHost;

        public static void Open(DialogueDatabaseAsset asset)
        {
            var window = GetWindow<DialogueEditorWindow>("Dialogue Graph");
            window.minSize = new Vector2(1200f, 700f);
            window.TryOpenDatabase(asset);
        }

        private void OnEnable()
        {
            _isAssemblyReloading = false;
            _isEditorQuitting = false;
            _hasHandledClosePrompt = false;
            RegisterLifecycleHooks();
        }

        private void OnDisable()
        {
            if (!ShouldPromptForSaveOnClose())
            {
                SaveAutosave();
            }

            UnregisterLifecycleHooks();
        }

        private void OnDestroy()
        {
            if (!ShouldPromptForSaveOnClose())
            {
                return;
            }

            _hasHandledClosePrompt = true;
            var shouldDiscard = SaveChangesPromptWindow.ShowDialog(position);

            if (!shouldDiscard)
            {
                SaveDatabase(false);
                return;
            }

            ClearAutosaveSnapshot();
            _hasUnsavedChanges = false;
        }

        private void CreateGUI()
        {
            RegisterLifecycleHooks();
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

            _graphHost = new VisualElement();
            _graphHost.AddToClassList("dialogue-editor__graph-host");

            var graphHeader = new VisualElement();
            graphHeader.AddToClassList("dialogue-editor__panel-header");
            graphHeader.Add(new Label("Graph Canvas") { name = "graph-title" });
            _graphHost.Add(graphHeader);

            _graphView = new DialogueGraphView
            {
                GraphChangedAction = OnGraphChanged,
                SelectionChangedAction = OnNodeSelectionChanged,
                CanvasFocusChangedAction = focused => _graphHost?.EnableInClassList("is-focused", focused)
            };
            _graphView.AddToClassList("dialogue-editor__graph-surface");
            _graphHost.Add(_graphView);
            contentSplit.Add(_graphHost);

            contentSplit.Add(BuildDetailsPanel());
            shell.Add(contentSplit);

            rootVisualElement.Add(shell);
            SetDetailsCollapsed(_detailsCollapsed);
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("dialogue-editor__toolbar");
            AttachGraphBlurOnInteraction(toolbar);

            var mainActions = new VisualElement();
            mainActions.AddToClassList("dialogue-editor__toolbar-group");
            mainActions.AddToClassList("dialogue-editor__toolbar-group--main");
            mainActions.Add(CreateToolbarButton("New", DialogueStartWindow.ShowWindow));
            mainActions.Add(CreateToolbarButton("Load", LoadDatabaseFromDialog));
            var saveButton = CreateToolbarButton("Save", SaveDatabase);
            saveButton.AddToClassList("dialogue-editor__toolbar-button--save");
            mainActions.Add(saveButton);
            mainActions.Add(CreateToolbarButton("Preview", OpenPreview));
            var deleteButton = CreateToolbarButton("Delete", DeleteSelection);
            deleteButton.AddToClassList("dialogue-editor__toolbar-button--danger");
            mainActions.Add(deleteButton);
            mainActions.Add(CreateToolbarButton("Dialogue Settings", ShowDialogueSettings));
            toolbar.Add(mainActions);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            toolbar.Add(spacer);

            var utilityActions = new VisualElement();
            utilityActions.AddToClassList("dialogue-editor__toolbar-group");
            utilityActions.AddToClassList("dialogue-editor__toolbar-group--utility");

            _statusLabel = new Label("No database loaded");
            _statusLabel.AddToClassList("dialogue-editor__status");
            utilityActions.Add(_statusLabel);

            _focusStartButton = new Button(() => TryFocusStartNode())
            {
                text = "⚑ Start"
            };
            _focusStartButton.AddToClassList("dialogue-editor__toolbar-button");
            _focusStartButton.AddToClassList("dialogue-editor__toolbar-button--utility");
            AttachGraphBlurOnInteraction(_focusStartButton);
            utilityActions.Add(_focusStartButton);

            toolbar.Add(utilityActions);
            return toolbar;
        }

        private VisualElement BuildLeftDock()
        {
            var dock = new VisualElement();
            dock.AddToClassList("dialogue-editor__left-dock");
            AttachGraphBlurOnInteraction(dock);

            var projectSection = new VisualElement();
            projectSection.AddToClassList("dialogue-editor__panel");
            projectSection.Add(BuildPanelHeader("Project"));
            projectSection.Add(BuildProjectActionsRow());

            _projectView = new ScrollView();
            _projectView.AddToClassList("dialogue-editor__project-scroll");
            _projectView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
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

            var scroll = new ScrollView();
            scroll.AddToClassList("dialogue-editor__palette-scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            var content = new VisualElement();
            content.AddToClassList("dialogue-editor__palette");

            content.Add(new PaletteItem(this, DialoguePaletteItemType.TextNode, "Text Node", "Click to add at center. Drag onto the graph to place."));
            content.Add(new PaletteItem(this, DialoguePaletteItemType.Comment, "Comment", "Click to add at center. Drag onto the graph to place."));
            content.Add(CreatePaletteButton("Function (Not in MVP)", null, false));
            content.Add(CreatePaletteButton("Scene (Not in MVP)", null, false));
            content.Add(CreatePaletteButton("Debug (Not in MVP)", null, false));

            scroll.Add(content);
            panel.Add(scroll);
            return panel;
        }

        private VisualElement BuildProjectActionsRow()
        {
            var row = new VisualElement();
            row.AddToClassList("dialogue-editor__project-actions-row");

            var createNpcButton = CreateProjectActionButton("Create NPC", CreateNpc);
            createNpcButton.AddToClassList("dialogue-editor__project-action-button--leading");
            row.Add(createNpcButton);

            var createDialogueButton = CreateProjectActionButton("Create Dialogue", CreateDialogue);
            row.Add(createDialogueButton);

            return row;
        }

        private VisualElement BuildDetailsPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("dialogue-editor__panel");
            panel.AddToClassList("dialogue-editor__details-panel");
            AttachGraphBlurOnInteraction(panel);

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
            _inspectorView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            panel.Add(_inspectorView);

            return panel;
        }

        private ToolbarButton CreateToolbarButton(string text, System.Action onClick)
        {
            var button = new ToolbarButton(onClick) { text = text };
            button.AddToClassList("dialogue-editor__toolbar-button");
            AttachGraphBlurOnInteraction(button);
            return button;
        }

        private Button CreatePaletteButton(string text, System.Action onClick, bool enabled = true)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("dialogue-editor__palette-button");
            button.SetEnabled(enabled);
            AttachGraphBlurOnInteraction(button);
            return button;
        }

        private Button CreateActionButton(string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("dialogue-editor__action-button");
            AttachGraphBlurOnInteraction(button);
            return button;
        }

        private Button CreateProjectActionButton(string text, System.Action onClick)
        {
            var button = CreateActionButton(text, onClick);
            button.AddToClassList("dialogue-editor__project-action-button");
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

        private bool TryOpenDatabase(DialogueDatabaseAsset asset)
        {
            if (!ConfirmDatabaseSwitch(asset))
            {
                return false;
            }

            LoadDatabase(asset);
            return true;
        }

        private bool ConfirmDatabaseSwitch(DialogueDatabaseAsset nextAsset)
        {
            if (_database == nextAsset)
            {
                return true;
            }

            if (_database == null || !_hasUnsavedChanges)
            {
                return true;
            }

            var shouldDiscard = SaveChangesPromptWindow.ShowDialog(
                position,
                "Do you want to save changes before opening another dialogue database?");

            if (!shouldDiscard)
            {
                SaveDatabase(false);
                return true;
            }

            ClearAutosaveSnapshot();
            _hasUnsavedChanges = false;
            RefreshStatus();
            return true;
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

            TryOpenDatabase(AssetDatabase.LoadAssetAtPath<DialogueDatabaseAsset>(relativePath));
        }

        private void SaveDatabase()
        {
            SaveDatabase(true);
        }

        private void SaveDatabase(bool showNotification)
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
            if (showNotification)
            {
                ShowNotification(new GUIContent("Dialogue database saved."));
            }
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

            _focusStartButton?.SetEnabled(_selectedDialogue != null && DialogueGraphUtility.FindStartNode(_selectedDialogue) != null);
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
                npcHeader.AddToClassList("dialogue-editor__project-card-actions");

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
                npcCard.Add(npcHeader);

                var npcName = CreateDelayedNameField("NPC Name", npc.Name, newValue =>
                {
                    npc.Name = newValue;
                    MarkChanged();
                    RefreshProjectPanel();
                });
                npcName.AddToClassList("dialogue-editor__project-name-field");
                npcCard.Add(npcName);

                if (npc.Dialogues.Count == 0)
                {
                    npcCard.Add(CreateInlineHelp("No dialogues yet. Use Create Dialogue above to add one."));
                }

                foreach (var dialogue in npc.Dialogues)
                {
                    var dialogueCard = new Box();
                    dialogueCard.AddToClassList("dialogue-editor__project-subcard");
                    dialogueCard.EnableInClassList("is-selected", _selectedDialogue == dialogue);

                    var dialogueRow = new VisualElement();
                    dialogueRow.AddToClassList("dialogue-editor__row");
                    dialogueRow.AddToClassList("dialogue-editor__project-card-actions");

                    var openButton = CreateActionButton(_selectedDialogue == dialogue ? "Editing" : "Open", () =>
                    {
                        _selectedNpc = npc;
                        _selectedDialogue = dialogue;
                        _selectedNode = null;
                        RefreshAll();
                    });
                    openButton.EnableInClassList("is-selected", _selectedDialogue == dialogue);
                    dialogueRow.Add(openButton);

                    dialogueCard.Add(dialogueRow);

                    var nameField = CreateDelayedNameField("Dialogue Name", dialogue.Name, newValue =>
                    {
                        dialogue.Name = newValue;
                        MarkChanged();
                        RefreshProjectPanel();
                        if (_selectedDialogue == dialogue && _selectedNode == null)
                        {
                            RefreshInspector();
                        }
                    });
                    nameField.AddToClassList("dialogue-editor__project-name-field");
                    dialogueCard.Add(nameField);

                    npcCard.Add(dialogueCard);
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

            var nameField = CreateDelayedNameField("Name", dialogue.Name, newValue =>
            {
                dialogue.Name = newValue;
                MarkChanged();
                RefreshProjectPanel();
            });
            nameField.AddToClassList("dialogue-editor__name-field");
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

            var tintField = new ColorField("Tint")
            {
                value = node.Tint,
                showAlpha = true,
                hdr = false
            };
            tintField.RegisterValueChangedCallback(evt =>
            {
                node.Tint = evt.newValue;
                _graphView.RefreshNodeVisuals();
                MarkChanged();
            });
            _inspectorView.Add(tintField);

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

            _selectedNpc.Dialogues.Add(dialogue);
            _selectedDialogue = dialogue;
            _selectedNode = null;
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
            _graphView?.ReleaseCanvasFocus();
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

        public bool TryFocusStartNode()
        {
            if (_selectedDialogue == null)
            {
                return false;
            }

            var startNode = DialogueGraphUtility.FindStartNode(_selectedDialogue);
            return startNode != null && FocusDialogueNode(_selectedDialogue, startNode.Id);
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

        private TextField CreateDelayedNameField(string label, string value, System.Action<string> onCommitted)
        {
            var field = new TextField(label)
            {
                value = value ?? string.Empty,
                isDelayed = true
            };
            field.AddToClassList("dialogue-editor__grow");
            field.AddToClassList("dialogue-editor__name-field");
            AttachGraphBlurOnInteraction(field);
            field.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == evt.previousValue)
                {
                    return;
                }

                onCommitted?.Invoke(evt.newValue);
            });
            return field;
        }

        private void AttachGraphBlurOnInteraction(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            element.RegisterCallback<MouseDownEvent>(_ => _graphView?.ReleaseCanvasFocus(), TrickleDown.TrickleDown);
            element.RegisterCallback<FocusInEvent>(_ => _graphView?.ReleaseCanvasFocus(), TrickleDown.TrickleDown);
        }

        private void MarkChanged()
        {
            _hasUnsavedChanges = true;
            SaveAutosave();
            RefreshStatus();
        }

        private void ClearAutosaveSnapshot()
        {
            if (_database == null)
            {
                return;
            }

            DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(_database));
        }

        private bool ShouldPromptForSaveOnClose()
        {
            return _database != null &&
                   _hasUnsavedChanges &&
                   !_isAssemblyReloading &&
                   !_isEditorQuitting &&
                   !_hasHandledClosePrompt;
        }

        private void RegisterLifecycleHooks()
        {
            if (_lifecycleHooksRegistered)
            {
                return;
            }

            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.quitting += OnEditorQuitting;
            _lifecycleHooksRegistered = true;
        }

        private void UnregisterLifecycleHooks()
        {
            if (!_lifecycleHooksRegistered)
            {
                return;
            }

            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.quitting -= OnEditorQuitting;
            _lifecycleHooksRegistered = false;
        }

        private void OnBeforeAssemblyReload()
        {
            _isAssemblyReloading = true;
        }

        private void OnEditorQuitting()
        {
            _isEditorQuitting = true;
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

    internal sealed class SaveChangesPromptWindow : EditorWindow
    {
        private string _bodyText = "Do you want to save changes before closing Dialogue Graph?";
        private bool _discardChanges = true;

        public static bool ShowDialog(Rect ownerPosition, string bodyText = "Do you want to save changes before closing Dialogue Graph?")
        {
            var window = CreateInstance<SaveChangesPromptWindow>();
            window._bodyText = string.IsNullOrWhiteSpace(bodyText)
                ? "Do you want to save changes before closing Dialogue Graph?"
                : bodyText;
            window.titleContent = new GUIContent("Save changes?");
            window.minSize = new Vector2(360f, 150f);
            window.maxSize = window.minSize;
            window.position = new Rect(
                ownerPosition.x + ((ownerPosition.width - window.minSize.x) * 0.5f),
                ownerPosition.y + ((ownerPosition.height - window.minSize.y) * 0.5f),
                window.minSize.x,
                window.minSize.y);
            window.ShowModal();
            return window._discardChanges;
        }

        private void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 14f;
            rootVisualElement.style.paddingRight = 14f;
            rootVisualElement.style.paddingTop = 14f;
            rootVisualElement.style.paddingBottom = 14f;
            rootVisualElement.style.backgroundColor = new Color(0.15f, 0.17f, 0.21f, 1f);

            var title = new Label("Save changes?");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 16;
            title.style.marginBottom = 6f;
            rootVisualElement.Add(title);

            var body = new Label(_bodyText);
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.color = new Color(0.84f, 0.88f, 0.93f, 1f);
            body.style.marginBottom = 14f;
            rootVisualElement.Add(body);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.Center;
            rootVisualElement.Add(buttons);

            var saveButton = new Button(() =>
            {
                _discardChanges = false;
                Close();
            })
            {
                text = "Yes"
            };
            saveButton.style.minWidth = 104f;
            saveButton.style.height = 32f;
            saveButton.style.marginRight = 10f;
            saveButton.style.backgroundColor = new Color(0.2f, 0.44f, 0.83f, 1f);
            saveButton.style.color = Color.white;
            saveButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            buttons.Add(saveButton);

            var discardButton = new Button(() =>
            {
                _discardChanges = true;
                Close();
            })
            {
                text = "No"
            };
            discardButton.style.minWidth = 104f;
            discardButton.style.height = 32f;
            buttons.Add(discardButton);
        }
    }
}

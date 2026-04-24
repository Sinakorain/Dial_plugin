using System;
using System.Collections.Generic;
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
        private const string RichTextTextColorsPrefsKey = "NewDial.DialogueEditor.RichText.TextColors";
        private const string RichTextHighlightColorsPrefsKey = "NewDial.DialogueEditor.RichText.HighlightColors";
        private delegate bool TryNormalizeRichTextColor(string color, out string normalized);

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
        private bool _isProcessingUndoRedo;
        private int _activeUndoGestureGroup = -1;
        private string _savedStateSnapshot = string.Empty;

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
            var window = GetWindow<DialogueEditorWindow>(DialogueEditorLocalization.Text("Dialogue Graph"));
            window.minSize = new Vector2(1200f, 700f);
            window.TryOpenDatabase(asset);
        }

        internal DialogueGraphView GraphViewForTests => _graphView;
        internal bool HasUnsavedChangesForTests => _hasUnsavedChanges;
        internal string SelectedNodeIdForTests => _selectedNode?.Id;
        internal bool SuppressIdentifierWarningsForTests { get; set; }

        internal void InitializeForTests(DialogueDatabaseAsset asset)
        {
            RegisterLifecycleHooks();
            ApplyStyles();
            BuildLayout();
            LoadDatabase(asset);
        }

        internal void SaveBaselineForTests()
        {
            _savedStateSnapshot = _database == null
                ? string.Empty
                : DialogueEditorAutosaveStore.CaptureSnapshotJson(_database);
            SyncDirtyState(false);
        }

        private void OnEnable()
        {
            _isAssemblyReloading = false;
            _isEditorQuitting = false;
            _hasHandledClosePrompt = false;
            RegisterLifecycleHooks();
            DialogueEditorLanguageSettings.LanguageChanged += OnEditorLanguageChanged;
        }

        private void OnDisable()
        {
            if (!ShouldPromptForSaveOnClose())
            {
                SaveAutosave();
            }

            UnregisterLifecycleHooks();
            DialogueEditorLanguageSettings.LanguageChanged -= OnEditorLanguageChanged;
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

        private void OnEditorLanguageChanged()
        {
            titleContent = new GUIContent(DialogueEditorLocalization.Text("Dialogue Graph"));
            BuildLayout();
            RefreshAll();
            DialoguePreviewWindow.RefreshOpenWindows(this);
        }

        private void BuildLayout()
        {
            rootVisualElement.Clear();
            titleContent = new GUIContent(DialogueEditorLocalization.Text("Dialogue Graph"));
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
            graphHeader.Add(new Label(DialogueEditorLocalization.Text("Graph Canvas")) { name = "graph-title" });
            _graphHost.Add(graphHeader);

            _graphView = new DialogueGraphView
            {
                GraphChangedAction = OnGraphChanged,
                SelectionChangedAction = OnNodeSelectionChanged,
                CanvasFocusChangedAction = focused => _graphHost?.EnableInClassList("is-focused", focused),
                ApplyUndoableChangeAction = ApplyUndoableNodeChange,
                BeginUndoGestureAction = BeginUndoGesture,
                EndUndoGestureAction = EndUndoGesture,
                SpeakerNameResolver = node => DialogueSpeakerUtility.ResolveSpeakerName(_selectedDialogue, node)
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
            mainActions.Add(CreateToolbarButton(DialogueEditorLocalization.Text("New"), DialogueStartWindow.ShowWindow));
            mainActions.Add(CreateToolbarButton(DialogueEditorLocalization.Text("Load"), LoadDatabaseFromDialog));
            var saveButton = CreateToolbarButton(DialogueEditorLocalization.Text("Save"), SaveDatabase);
            saveButton.AddToClassList("dialogue-editor__toolbar-button--save");
            mainActions.Add(saveButton);
            mainActions.Add(CreateToolbarButton(DialogueEditorLocalization.Text("Preview"), OpenPreview));
            var deleteButton = CreateToolbarButton(DialogueEditorLocalization.Text("Delete"), DeleteSelection);
            deleteButton.AddToClassList("dialogue-editor__toolbar-button--danger");
            mainActions.Add(deleteButton);
            mainActions.Add(CreateToolbarButton(DialogueEditorLocalization.Text("Dialogue Settings"), ShowDialogueSettings));
            toolbar.Add(mainActions);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            toolbar.Add(spacer);

            var utilityActions = new VisualElement();
            utilityActions.AddToClassList("dialogue-editor__toolbar-group");
            utilityActions.AddToClassList("dialogue-editor__toolbar-group--utility");

            _statusLabel = new Label(DialogueEditorLocalization.Text("No database loaded"));
            _statusLabel.AddToClassList("dialogue-editor__status");
            utilityActions.Add(_statusLabel);

            var languageField = new PopupField<string>(
                new List<string> { "EN", "RU" },
                DialogueEditorLanguageSettings.CurrentLanguage == DialogueEditorLanguage.Russian ? 1 : 0)
            {
                name = "editor-language-field"
            };
            languageField.RegisterValueChangedCallback(evt =>
            {
                DialogueEditorLanguageSettings.CurrentLanguage = evt.newValue == "RU"
                    ? DialogueEditorLanguage.Russian
                    : DialogueEditorLanguage.English;
            });
            AttachGraphBlurOnInteraction(languageField);
            utilityActions.Add(languageField);

            _focusStartButton = new Button(() => TryFocusStartNode())
            {
                text = $"⚑ {DialogueEditorLocalization.Text("Start")}"
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
            projectSection.AddToClassList("dialogue-editor__project-panel");
            projectSection.name = "project-panel";
            projectSection.Add(BuildPanelHeader(DialogueEditorLocalization.Text("Project")));
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
            panel.AddToClassList("dialogue-editor__palette-panel");
            panel.name = "palette-panel";
            panel.Add(BuildPanelHeader(DialogueEditorLocalization.Text("Palette")));

            var content = new VisualElement();
            content.AddToClassList("dialogue-editor__palette");
            content.name = "palette-content";

            content.Add(new PaletteItem(this, DialoguePaletteItemType.TextNode, DialogueEditorLocalization.Text("Text Node"), DialogueEditorLocalization.Text("Click to add at center. Drag onto the graph to place.")));
            content.Add(new PaletteItem(this, DialoguePaletteItemType.Comment, DialogueEditorLocalization.Text("Comment"), DialogueEditorLocalization.Text("Click to add at center. Drag onto the graph to place.")));
            content.Add(new PaletteItem(this, DialoguePaletteItemType.Function, DialogueEditorLocalization.Text("Function"), DialogueEditorLocalization.Text("Execute a project-provided function and continue.")));
            content.Add(new PaletteItem(this, DialoguePaletteItemType.Scene, DialogueEditorLocalization.Text("Scene"), DialogueEditorLocalization.Text("Request project scene loading and continue.")));
            content.Add(new PaletteItem(this, DialoguePaletteItemType.Debug, DialogueEditorLocalization.Text("Debug"), DialogueEditorLocalization.Text("Write a diagnostic log entry and continue.")));

            panel.Add(content);
            return panel;
        }

        private VisualElement BuildProjectActionsRow()
        {
            var row = new VisualElement();
            row.AddToClassList("dialogue-editor__project-actions-row");

            var createNpcButton = CreateProjectActionButton(DialogueEditorLocalization.Text("Create NPC"), CreateNpc);
            createNpcButton.AddToClassList("dialogue-editor__project-action-button--leading");
            row.Add(createNpcButton);

            var createDialogueButton = CreateProjectActionButton(DialogueEditorLocalization.Text("Create Dialogue"), CreateDialogue);
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

            _detailsTitleLabel = new Label(DialogueEditorLocalization.Text("Details"));
            _detailsTitleLabel.AddToClassList("dialogue-editor__details-title");
            header.Add(_detailsTitleLabel);

            _detailsToggleButton = new Button(() => SetDetailsCollapsed(!_detailsCollapsed))
            {
                text = DialogueEditorLocalization.Text("Hide")
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

            var label = new Label(DialogueEditorLocalization.Text(title));
            label.AddToClassList("dialogue-editor__panel-title");
            header.Add(label);
            return header;
        }

        private void LoadDatabase(DialogueDatabaseAsset asset)
        {
            _database = asset;
            _selectedNode = null;

            if (_database != null)
            {
                DialogueEditorAutosaveStore.TryLoadSnapshot(_database, DialogueEditorAutosaveStore.GetStorageKey(_database));
                _savedStateSnapshot = DialogueEditorAutosaveStore.CaptureSnapshotJson(_database);
                var migratedSpeakers = EnsureDialogueSpeakers(_database);
                _selectedNpc = _database.Npcs.FirstOrDefault();
                _selectedDialogue = _selectedNpc?.Dialogues.FirstOrDefault();
                SyncDirtyState(migratedSpeakers);
            }
            else
            {
                _savedStateSnapshot = string.Empty;
                _selectedNpc = null;
                _selectedDialogue = null;
                SyncDirtyState(false);
            }

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
                DialogueEditorLocalization.Text("Do you want to save changes before opening another dialogue database?"));

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
            var absolutePath = EditorUtility.OpenFilePanel(DialogueEditorLocalization.Text("Load Dialogue Database"), Application.dataPath, "asset");
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return;
            }

            var relativePath = FileUtil.GetProjectRelativePath(absolutePath);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                EditorUtility.DisplayDialog(
                    DialogueEditorLocalization.Text("Load failed"),
                    DialogueEditorLocalization.Text("The selected asset must live inside the current Unity project."),
                    DialogueEditorLocalization.Text("OK"));
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
            _savedStateSnapshot = DialogueEditorAutosaveStore.CaptureSnapshotJson(_database);
            SyncDirtyState(false);
            if (showNotification)
            {
                ShowNotification(new GUIContent(DialogueEditorLocalization.Text("Dialogue database saved.")));
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
            var selectedNodeId = _selectedNode?.Id;
            _graphView.SpeakerNameResolver = node => DialogueSpeakerUtility.ResolveSpeakerName(_selectedDialogue, node);
            _graphView.LoadGraph(_selectedDialogue?.Graph);
            if (!string.IsNullOrWhiteSpace(selectedNodeId) && !_graphView.RestoreSelection(selectedNodeId))
            {
                _selectedNode = null;
            }

            RefreshInspector();
        }

        private void RefreshStatus()
        {
            _statusLabel.text = _database == null
                ? DialogueEditorLocalization.Text("No database loaded")
                : $"{_database.name}{(_hasUnsavedChanges ? $" ({DialogueEditorLocalization.Text("unsaved changes")})" : string.Empty)}";

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
                _projectView.Add(CreateEmptyPanelMessage(DialogueEditorLocalization.Text("Load or create a dialogue database to begin.")));
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

                var npcSelect = CreateActionButton(DialogueEditorLocalization.Text(_selectedNpc == npc ? "Selected NPC" : "Select NPC"), () =>
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

                var npcName = CreateDelayedNameField(DialogueEditorLocalization.Text("NPC Name"), npc.Name, newValue =>
                {
                    npc.Name = newValue;
                    MarkChanged();
                    RefreshProjectPanel();
                });
                npcName.AddToClassList("dialogue-editor__project-name-field");
                npcCard.Add(npcName);
                npcCard.Add(BuildNpcIdEditor(npc));
                if (_selectedNpc == npc)
                {
                    npcCard.Add(BuildWhereUsedSection(DialogueWhereUsedUtility.GetWhereUsed(_database, npc)));
                }

                if (npc.Dialogues.Count == 0)
                {
                    npcCard.Add(CreateInlineHelp(DialogueEditorLocalization.Text("No dialogues yet. Use Create Dialogue above to add one.")));
                }

                foreach (var dialogue in npc.Dialogues)
                {
                    var dialogueCard = new Box();
                    dialogueCard.AddToClassList("dialogue-editor__project-subcard");
                    dialogueCard.EnableInClassList("is-selected", _selectedDialogue == dialogue);

                    var dialogueRow = new VisualElement();
                    dialogueRow.AddToClassList("dialogue-editor__row");
                    dialogueRow.AddToClassList("dialogue-editor__project-card-actions");

                    var openButton = CreateActionButton(DialogueEditorLocalization.Text(_selectedDialogue == dialogue ? "Editing" : "Open"), () =>
                    {
                        _selectedNpc = npc;
                        _selectedDialogue = dialogue;
                        _selectedNode = null;
                        RefreshAll();
                    });
                    openButton.EnableInClassList("is-selected", _selectedDialogue == dialogue);
                    dialogueRow.Add(openButton);

                    dialogueCard.Add(dialogueRow);

                    var nameField = CreateDelayedNameField(DialogueEditorLocalization.Text("Dialogue Name"), dialogue.Name, newValue =>
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
                    dialogueCard.Add(BuildDialogueIdEditor(dialogue));
                    if (_selectedDialogue == dialogue)
                    {
                        dialogueCard.Add(BuildWhereUsedSection(DialogueWhereUsedUtility.GetWhereUsed(_database, npc, dialogue)));
                    }

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
                _detailsTitleLabel.text = DialogueEditorLocalization.Text("Details");
                _inspectorView.Add(CreateEmptyPanelMessage(DialogueEditorLocalization.Text("No database loaded.")));
                return;
            }

            if (_selectedNode is DialogueTextNodeData textNode)
            {
                _detailsTitleLabel.text = DialogueEditorLocalization.Text("Node Details");
                BuildTextNodeInspector(textNode);
                return;
            }

            if (_selectedNode is CommentNodeData commentNode)
            {
                _detailsTitleLabel.text = DialogueEditorLocalization.Text("Comment Details");
                BuildCommentNodeInspector(commentNode);
                return;
            }

            if (_selectedNode is FunctionNodeData functionNode)
            {
                _detailsTitleLabel.text = DialogueEditorLocalization.Text("Function Details");
                BuildFunctionNodeInspector(functionNode);
                return;
            }

            if (_selectedNode is SceneNodeData sceneNode)
            {
                _detailsTitleLabel.text = DialogueEditorLocalization.Text("Scene Details");
                BuildSceneNodeInspector(sceneNode);
                return;
            }

            if (_selectedNode is DebugNodeData debugNode)
            {
                _detailsTitleLabel.text = DialogueEditorLocalization.Text("Debug Details");
                BuildDebugNodeInspector(debugNode);
                return;
            }

            if (_selectedDialogue != null)
            {
                _detailsTitleLabel.text = DialogueEditorLocalization.Text("Dialogue Settings");
                BuildDialogueInspector(_selectedDialogue);
                return;
            }

            _detailsTitleLabel.text = DialogueEditorLocalization.Text("Details");
            _inspectorView.Add(CreateEmptyPanelMessage(DialogueEditorLocalization.Text("Select an NPC, a dialogue, or a node to inspect it.")));
        }

        private void BuildDialogueInspector(DialogueEntry dialogue)
        {
            _inspectorView.Add(CreateSectionTitle(DialogueEditorLocalization.Text("Dialogue Settings")));

            var nameField = CreateDelayedNameField(DialogueEditorLocalization.Text("Name"), dialogue.Name, newValue =>
            {
                dialogue.Name = newValue;
                MarkChanged();
                RefreshProjectPanel();
            });
            nameField.AddToClassList("dialogue-editor__name-field");
            _inspectorView.Add(nameField);

            var summaryCard = new Box();
            summaryCard.AddToClassList("dialogue-editor__inspector-card");
            summaryCard.Add(new Label(DialogueEditorLocalization.Format("Nodes: {0}", dialogue.Graph.Nodes.Count)));
            summaryCard.Add(new Label(DialogueEditorLocalization.Format("Start node: {0}", DialogueGraphUtility.FindStartNode(dialogue)?.Title ?? DialogueEditorLocalization.Text("None"))));
            _inspectorView.Add(summaryCard);

            BuildSpeakerRosterEditor(dialogue);
            _inspectorView.Add(BuildDialogueIdEditor(dialogue));
            _inspectorView.Add(BuildWhereUsedSection(DialogueWhereUsedUtility.GetWhereUsed(_database, _selectedNpc, dialogue)));
            BuildConditionEditor(dialogue.StartCondition, DialogueEditorLocalization.Text("Start Condition"));
            _inspectorView.Add(CreateInlineHelp(DialogueEditorLocalization.Text("Click empty graph space to return here after editing a node.")));
        }

        private void BuildSpeakerRosterEditor(DialogueEntry dialogue)
        {
            EnsureDialogueSpeakers(_selectedNpc, dialogue);

            _inspectorView.Add(CreateSectionTitle(DialogueEditorLocalization.Text("Speakers")));
            var box = new Box();
            box.AddToClassList("dialogue-editor__inspector-card");

            var speakers = dialogue.Speakers.Where(speaker => speaker != null).ToList();
            foreach (var speaker in speakers)
            {
                var row = new VisualElement();
                row.AddToClassList("dialogue-editor__row");

                var nameField = new TextField(DialogueEditorLocalization.Text("Speaker Name"))
                {
                    value = speaker.Name,
                    isDelayed = true,
                    name = "dialogue-speaker-name-field"
                };
                nameField.RegisterValueChangedCallback(evt =>
                {
                    PerformDialogueScopedChange("Rename Speaker", () => speaker.Name = evt.newValue, refreshNodeVisuals: true, refreshInspector: true);
                });
                row.Add(nameField);

                var removeButton = new Button(() => RemoveSpeaker(dialogue, speaker))
                {
                    text = DialogueEditorLocalization.Text("Remove"),
                    name = "dialogue-speaker-remove-button"
                };
                removeButton.SetEnabled(speakers.Count > 1);
                removeButton.AddToClassList("dialogue-editor__danger-button");
                row.Add(removeButton);
                box.Add(row);
            }

            var addButton = new Button(() =>
            {
                AddSpeaker(dialogue);
            })
            {
                text = DialogueEditorLocalization.Text("Add Speaker"),
                name = "dialogue-speaker-add-button"
            };
            box.Add(addButton);
            _inspectorView.Add(box);
        }

        private void BuildTextNodeInspector(DialogueTextNodeData node)
        {
            _inspectorView.Add(CreateSectionTitle(DialogueEditorLocalization.Text("Text Node")));

            var titleField = new TextField(DialogueEditorLocalization.Text("Title")) { value = node.Title, name = "node-title-field" };
            titleField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Node Title", () => node.Title = evt.newValue, refreshNodeVisuals: true);
            });
            _inspectorView.Add(titleField);

            _inspectorView.Add(BuildNodeIdEditor(node));
            _inspectorView.Add(BuildWhereUsedSection(DialogueWhereUsedUtility.GetWhereUsed(_database, _selectedNpc, _selectedDialogue, node)));

            BuildTextNodeSpeakerField(node);

            var bodyPreview = CreateRichTextPreview(node.BodyText);
            var bodyField = new TextField(DialogueEditorLocalization.Text("Body Text"))
            {
                value = node.BodyText,
                multiline = true,
                name = "node-body-field"
            };
            var bodySelectionState = new RichTextSelectionState();
            RegisterRichTextSelectionTracking(bodyField, bodySelectionState);
            _inspectorView.Add(BuildRichTextToolbar(bodyField, bodyPreview, node, bodySelectionState));

            bodyField.AddToClassList("dialogue-editor__multiline-field");
            bodyField.AddToClassList("dialogue-editor__body-field");
            bodyField.style.whiteSpace = WhiteSpace.Normal;
            bodyField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Node Body", () => node.BodyText = evt.newValue, refreshNodeVisuals: true);
                bodySelectionState.Capture(bodyField);
                UpdateRichTextPreview(bodyPreview, evt.newValue);
                DialoguePreviewWindow.RefreshOpenWindows(this);
            });
            _inspectorView.Add(bodyField);
            _inspectorView.Add(bodyPreview);

            var voiceKeyField = new TextField(DialogueEditorLocalization.Text("Voice Key"))
            {
                value = node.VoiceKey,
                name = "node-voice-key-field"
            };
            voiceKeyField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Node Voice Key", () => node.VoiceKey = evt.newValue);
            });
            _inspectorView.Add(voiceKeyField);

            var startToggle = new Toggle(DialogueEditorLocalization.Text("Is Start Node")) { value = node.IsStartNode, name = "node-start-toggle" };
            startToggle.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Toggle Start Node", () =>
                {
                    if (evt.newValue)
                    {
                        DialogueGraphUtility.EnsureSingleStartNode(_selectedDialogue.Graph, node.Id);
                    }
                    else if (DialogueGraphUtility.FindStartNode(_selectedDialogue) == node)
                    {
                        node.IsStartNode = false;
                    }
                }, refreshNodeVisuals: true, refreshInspector: true);
            });
            _inspectorView.Add(startToggle);

            var choiceToggle = new Toggle(DialogueEditorLocalization.Text("Use Outputs As Choices")) { value = node.UseOutputsAsChoices, name = "node-choice-toggle" };
            choiceToggle.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Toggle Node Choice Mode", () => node.UseOutputsAsChoices = evt.newValue, refreshNodeVisuals: true);
            });
            _inspectorView.Add(choiceToggle);

            BuildChoiceFlowDiagnostics(node);
            BuildConditionEditor(node.Condition, DialogueEditorLocalization.Text("Condition"), "Edit Node Condition");
            BuildLinksInspector(node);

            var deleteButton = new Button(() => _graphView.DeleteNode(node)) { text = DialogueEditorLocalization.Text("Delete Node") };
            deleteButton.AddToClassList("dialogue-editor__danger-button");
            _inspectorView.Add(deleteButton);
        }

        private void BuildTextNodeSpeakerField(DialogueTextNodeData node)
        {
            EnsureDialogueSpeakers(_selectedNpc, _selectedDialogue);
            var speakers = _selectedDialogue?.Speakers?.Where(speaker => speaker != null).ToList() ?? new List<DialogueSpeakerEntry>();
            if (speakers.Count == 0)
            {
                _inspectorView.Add(CreateInlineHelp(DialogueEditorLocalization.Text("No speakers configured for this dialogue.")));
                return;
            }

            var selectedSpeaker = DialogueSpeakerUtility.ResolveSpeaker(_selectedDialogue, node);
            var selectedIndex = Mathf.Max(0, speakers.IndexOf(selectedSpeaker));
            var labels = BuildSpeakerOptionLabels(speakers);
            var speakerField = new PopupField<string>(DialogueEditorLocalization.Text("Speaker"), labels, selectedIndex)
            {
                name = "node-speaker-field"
            };
            speakerField.RegisterValueChangedCallback(evt =>
            {
                var index = labels.IndexOf(evt.newValue);
                if (index < 0 || index >= speakers.Count)
                {
                    return;
                }

                PerformNodeScopedChange("Edit Node Speaker", () => node.SpeakerId = speakers[index].Id, refreshNodeVisuals: true);
                DialoguePreviewWindow.RefreshOpenWindows(this);
            });
            _inspectorView.Add(speakerField);
        }

        private VisualElement BuildRichTextToolbar(
            TextField bodyField,
            VisualElement bodyPreview,
            DialogueTextNodeData node,
            RichTextSelectionState selectionState)
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("dialogue-editor__rich-text-toolbar");

            var formatRow = new VisualElement();
            formatRow.AddToClassList("dialogue-editor__rich-text-format-row");

            var boldButton = CreateRichTextButton("B", "Bold", () =>
                ApplyRichTextFormat(bodyField, bodyPreview, node, selectionState, DialogueRichTextFormat.Bold(), "Format Node Body Bold"), bodyField, selectionState);
            boldButton.name = "rich-text-bold-button";
            formatRow.Add(boldButton);

            var italicButton = CreateRichTextButton("I", "Italic", () =>
                ApplyRichTextFormat(bodyField, bodyPreview, node, selectionState, DialogueRichTextFormat.Italic(), "Format Node Body Italic"), bodyField, selectionState);
            italicButton.name = "rich-text-italic-button";
            formatRow.Add(italicButton);
            toolbar.Add(formatRow);

            var colorStack = new VisualElement();
            colorStack.AddToClassList("dialogue-editor__rich-text-color-stack");
            colorStack.Add(BuildRichTextColorList(
                DialogueEditorLocalization.Text("Text Color"),
                "rich-text-color-list",
                "rich-text-color-add-button",
                "rich-text-color-field",
                "rich-text-color-apply",
                "rich-text-color-remove",
                RichTextTextColorsPrefsKey,
                7,
                "Format Node Body Color",
                DialogueRichTextUtility.TryNormalizeTextColorCode,
                normalized => DialogueRichTextFormat.TextColor(normalized),
                bodyField,
                bodyPreview,
                node,
                selectionState));

            colorStack.Add(BuildRichTextColorList(
                DialogueEditorLocalization.Text("Highlight"),
                "rich-text-highlight-list",
                "rich-text-highlight-add-button",
                "rich-text-highlight-field",
                "rich-text-highlight-apply",
                "rich-text-highlight-remove",
                RichTextHighlightColorsPrefsKey,
                9,
                "Format Node Body Highlight",
                DialogueRichTextUtility.TryNormalizeHighlightColorCode,
                normalized => DialogueRichTextFormat.Highlight(normalized),
                bodyField,
                bodyPreview,
                node,
                selectionState));
            toolbar.Add(colorStack);

            var clearButton = CreateRichTextButton(DialogueEditorLocalization.Text("Clear Formatting"), "Clear Formatting", () =>
                ClearRichTextFormatting(bodyField, bodyPreview, node, selectionState), bodyField, selectionState);
            clearButton.name = "rich-text-clear-button";
            toolbar.Add(clearButton);
            return toolbar;
        }

        private VisualElement BuildRichTextColorList(
            string title,
            string listName,
            string addButtonName,
            string fieldNamePrefix,
            string applyButtonNamePrefix,
            string removeButtonNamePrefix,
            string prefsKey,
            int maxLength,
            string actionName,
            TryNormalizeRichTextColor tryNormalize,
            Func<string, DialogueRichTextFormat> createFormat,
            TextField bodyField,
            VisualElement bodyPreview,
            DialogueTextNodeData node,
            RichTextSelectionState selectionState)
        {
            var list = LoadRichTextColorList(prefsKey);
            var group = new VisualElement
            {
                name = listName
            };
            group.AddToClassList("dialogue-editor__rich-text-color-list");

            var header = new VisualElement();
            header.AddToClassList("dialogue-editor__rich-text-color-list-header");
            header.Add(new Label(title));
            var rows = new VisualElement();
            rows.AddToClassList("dialogue-editor__rich-text-color-list-rows");
            var addButton = CreateRichTextButton("+", "Add Color", () =>
            {
                list.Colors.Add(string.Empty);
                SaveRichTextColorList(prefsKey, list);
                RebuildRichTextColorListRows(rows, list, prefsKey, fieldNamePrefix, applyButtonNamePrefix, removeButtonNamePrefix, maxLength, actionName, tryNormalize, createFormat, bodyField, bodyPreview, node, selectionState);
            }, bodyField, selectionState);
            addButton.name = addButtonName;
            header.Add(addButton);
            group.Add(header);
            group.Add(rows);

            RebuildRichTextColorListRows(rows, list, prefsKey, fieldNamePrefix, applyButtonNamePrefix, removeButtonNamePrefix, maxLength, actionName, tryNormalize, createFormat, bodyField, bodyPreview, node, selectionState);
            return group;
        }

        private void RebuildRichTextColorListRows(
            VisualElement rows,
            RichTextColorList list,
            string prefsKey,
            string fieldNamePrefix,
            string applyButtonNamePrefix,
            string removeButtonNamePrefix,
            int maxLength,
            string actionName,
            TryNormalizeRichTextColor tryNormalize,
            Func<string, DialogueRichTextFormat> createFormat,
            TextField bodyField,
            VisualElement bodyPreview,
            DialogueTextNodeData node,
            RichTextSelectionState selectionState)
        {
            rows.Clear();
            for (var index = 0; index < list.Colors.Count; index++)
            {
                rows.Add(CreateRichTextColorRow(index, list, prefsKey, fieldNamePrefix, applyButtonNamePrefix, removeButtonNamePrefix, maxLength, actionName, tryNormalize, createFormat, bodyField, bodyPreview, node, selectionState, rows));
            }
        }

        private VisualElement CreateRichTextColorRow(
            int index,
            RichTextColorList list,
            string prefsKey,
            string fieldNamePrefix,
            string applyButtonNamePrefix,
            string removeButtonNamePrefix,
            int maxLength,
            string actionName,
            TryNormalizeRichTextColor tryNormalize,
            Func<string, DialogueRichTextFormat> createFormat,
            TextField bodyField,
            VisualElement bodyPreview,
            DialogueTextNodeData node,
            RichTextSelectionState selectionState,
            VisualElement rows)
        {
            var row = new VisualElement();
            row.AddToClassList("dialogue-editor__rich-text-color-row");

            void AddRemoveButton()
            {
                var removeButton = CreateRichTextButton("X", "Remove", () =>
                {
                    if (index >= 0 && index < list.Colors.Count)
                    {
                        list.Colors.RemoveAt(index);
                        SaveRichTextColorList(prefsKey, list);
                        RebuildRichTextColorListRows(rows, list, prefsKey, fieldNamePrefix, applyButtonNamePrefix, removeButtonNamePrefix, maxLength, actionName, tryNormalize, createFormat, bodyField, bodyPreview, node, selectionState);
                    }
                }, bodyField, selectionState);
                removeButton.name = $"{removeButtonNamePrefix}-{index}";
                removeButton.AddToClassList("dialogue-editor__rich-text-remove-button");
                row.Add(removeButton);
            }

            void ShowSwatch(string normalized)
            {
                row.Clear();
                Button swatchButton = null;
                swatchButton = CreateRichTextButton(string.Empty, "Select Color", () =>
                    SelectRichTextColorIcon(rows, swatchButton), bodyField, selectionState);
                swatchButton.name = $"{fieldNamePrefix}-swatch-{index}";
                swatchButton.AddToClassList("dialogue-editor__rich-text-color-icon");
                swatchButton.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.clickCount < 2)
                    {
                        return;
                    }

                    ShowEditor();
                    evt.StopImmediatePropagation();
                }, TrickleDown.TrickleDown);
                if (ColorUtility.TryParseHtmlString(normalized, out var color))
                {
                    swatchButton.style.backgroundColor = color;
                }

                row.Add(swatchButton);

                var applyButton = CreateRichTextButton(DialogueEditorLocalization.Text("Apply"), "Apply", () =>
                    ApplyRichTextFormat(bodyField, bodyPreview, node, selectionState, createFormat(normalized), actionName), bodyField, selectionState);
                applyButton.name = $"{applyButtonNamePrefix}-{index}";
                applyButton.AddToClassList("dialogue-editor__rich-text-apply-button");
                row.Add(applyButton);
                AddRemoveButton();
            }

            void ShowEditor()
            {
                row.Clear();
                var field = new TextField
                {
                    name = $"{fieldNamePrefix}-{index}",
                    value = list.Colors[index],
                    maxLength = maxLength
                };
                field.AddToClassList("dialogue-editor__rich-text-color-field");
                field.RegisterValueChangedCallback(evt =>
                {
                    var value = evt.newValue ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        list.Colors[index] = string.Empty;
                        SaveRichTextColorList(prefsKey, list);
                        ClearCustomColorError(field);
                        return;
                    }

                    if (!tryNormalize(value, out var normalized))
                    {
                        ShowCustomColorError(field);
                        return;
                    }

                    list.Colors[index] = normalized;
                    SaveRichTextColorList(prefsKey, list);
                    ShowSwatch(normalized);
                });
                field.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                    {
                        return;
                    }

                    ApplyCustomRichTextColor(field, bodyField, bodyPreview, node, selectionState, tryNormalize, createFormat, actionName);
                    evt.StopPropagation();
                });
                row.Add(field);

                var applyButton = CreateRichTextButton(DialogueEditorLocalization.Text("Apply"), "Apply", () =>
                    ApplyCustomRichTextColor(field, bodyField, bodyPreview, node, selectionState, tryNormalize, createFormat, actionName), bodyField, selectionState);
                applyButton.name = $"{applyButtonNamePrefix}-{index}";
                applyButton.AddToClassList("dialogue-editor__rich-text-apply-button");
                row.Add(applyButton);
                AddRemoveButton();
            }

            if (tryNormalize(list.Colors[index], out var existingNormalized))
            {
                list.Colors[index] = existingNormalized;
                ShowSwatch(existingNormalized);
            }
            else
            {
                ShowEditor();
            }

            return row;
        }

        private static void SelectRichTextColorIcon(VisualElement rows, Button selectedButton)
        {
            foreach (var button in rows.Query<Button>(className: "dialogue-editor__rich-text-color-icon").ToList())
            {
                button.RemoveFromClassList("dialogue-editor__rich-text-color-icon--selected");
            }

            selectedButton.AddToClassList("dialogue-editor__rich-text-color-icon--selected");
        }

        private void ApplyCustomRichTextColor(
            TextField colorField,
            TextField bodyField,
            VisualElement bodyPreview,
            DialogueTextNodeData node,
            RichTextSelectionState selectionState,
            TryNormalizeRichTextColor tryNormalize,
            Func<string, DialogueRichTextFormat> createFormat,
            string actionName)
        {
            if (!tryNormalize(colorField.value, out var normalized))
            {
                ShowCustomColorError(colorField);
                return;
            }

            ClearCustomColorError(colorField);
            ApplyRichTextFormat(bodyField, bodyPreview, node, selectionState, createFormat(normalized), actionName);
        }

        private static void ShowCustomColorError(TextField colorField)
        {
            colorField.AddToClassList("dialogue-editor__rich-text-color-field--invalid");
            colorField.tooltip = DialogueEditorLocalization.Text("Use strict hex color code.");
        }

        private static void ClearCustomColorError(TextField colorField)
        {
            colorField.RemoveFromClassList("dialogue-editor__rich-text-color-field--invalid");
            colorField.tooltip = DialogueEditorLocalization.Text("Use strict hex color code.");
        }

        private static RichTextColorList LoadRichTextColorList(string prefsKey)
        {
            var json = EditorPrefs.GetString(prefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new RichTextColorList();
            }

            try
            {
                var list = JsonUtility.FromJson<RichTextColorList>(json) ?? new RichTextColorList();
                list.Colors ??= new List<string>();
                for (var index = 0; index < list.Colors.Count; index++)
                {
                    list.Colors[index] ??= string.Empty;
                }

                return list;
            }
            catch (ArgumentException)
            {
                return new RichTextColorList();
            }
        }

        private static void SaveRichTextColorList(string prefsKey, RichTextColorList list)
        {
            list ??= new RichTextColorList();
            list.Colors ??= new List<string>();
            EditorPrefs.SetString(prefsKey, JsonUtility.ToJson(list));
        }

        private Button CreateRichTextButton(
            string text,
            string tooltipKey,
            Action onClick,
            TextField bodyField = null,
            RichTextSelectionState selectionState = null)
        {
            var button = new Button(onClick)
            {
                text = text,
                tooltip = DialogueEditorLocalization.Text(tooltipKey),
                focusable = false
            };
            button.AddToClassList("dialogue-editor__rich-text-button");
            AttachRichTextSelectionCapture(button, bodyField, selectionState);
            AttachGraphBlurOnInteraction(button);
            return button;
        }

        private static void AttachRichTextSelectionCapture(
            VisualElement element,
            TextField bodyField,
            RichTextSelectionState selectionState)
        {
            if (element == null || bodyField == null || selectionState == null)
            {
                return;
            }

            element.RegisterCallback<MouseDownEvent>(_ => selectionState.CaptureForToolbarAction(bodyField), TrickleDown.TrickleDown);
            element.RegisterCallback<PointerDownEvent>(_ => selectionState.CaptureForToolbarAction(bodyField), TrickleDown.TrickleDown);
        }

        private static void RegisterRichTextSelectionTracking(TextField bodyField, RichTextSelectionState selectionState)
        {
            selectionState.Capture(bodyField);
            bodyField.RegisterCallback<FocusInEvent>(_ => ScheduleRichTextSelectionCapture(bodyField, selectionState));
            bodyField.RegisterCallback<KeyUpEvent>(_ => ScheduleRichTextSelectionCapture(bodyField, selectionState));
            bodyField.RegisterCallback<MouseUpEvent>(_ => ScheduleRichTextSelectionCapture(bodyField, selectionState));
            bodyField.RegisterCallback<PointerUpEvent>(_ => ScheduleRichTextSelectionCapture(bodyField, selectionState));
        }

        private static void ScheduleRichTextSelectionCapture(TextField bodyField, RichTextSelectionState selectionState)
        {
            bodyField.schedule.Execute(() => selectionState.Capture(bodyField));
        }

        private VisualElement CreateRichTextPreview(string text)
        {
            var preview = DialogueRichTextRenderer.Create("node-body-rich-preview", "dialogue-editor__rich-text-preview");
            UpdateRichTextPreview(preview, text);
            return preview;
        }

        private static void UpdateRichTextPreview(VisualElement preview, string text)
        {
            DialogueRichTextRenderer.SetText(preview, text, DialogueEditorLocalization.Text("This node has no dialogue text yet."));
        }

        private void ApplyRichTextFormat(
            TextField bodyField,
            VisualElement bodyPreview,
            DialogueTextNodeData node,
            RichTextSelectionState selectionState,
            DialogueRichTextFormat format,
            string actionName)
        {
            var rawText = bodyField.value ?? string.Empty;
            selectionState.GetRange(bodyField, rawText.Length, out var start, out var end);
            var updatedText = DialogueRichTextUtility.WrapSelection(rawText, start, end, format);
            var cursorIndex = start == end
                ? start + format.OpeningTag.Length
                : end + format.OpeningTag.Length + format.ClosingTag.Length;
            SetBodyTextFromRichTextTool(bodyField, bodyPreview, node, selectionState, updatedText, cursorIndex, actionName);
        }

        private void ClearRichTextFormatting(
            TextField bodyField,
            VisualElement bodyPreview,
            DialogueTextNodeData node,
            RichTextSelectionState selectionState)
        {
            var rawText = bodyField.value ?? string.Empty;
            selectionState.GetRange(bodyField, rawText.Length, out var start, out var end);
            var updatedText = DialogueRichTextUtility.StripSupportedRichText(rawText, start, end);
            SetBodyTextFromRichTextTool(bodyField, bodyPreview, node, selectionState, updatedText, start, "Clear Node Body Formatting");
        }

        private void SetBodyTextFromRichTextTool(
            TextField bodyField,
            VisualElement bodyPreview,
            DialogueTextNodeData node,
            RichTextSelectionState selectionState,
            string updatedText,
            int cursorIndex,
            string actionName)
        {
            PerformNodeScopedChange(actionName, () => node.BodyText = updatedText, refreshNodeVisuals: true);
            bodyField.SetValueWithoutNotify(updatedText);
            UpdateRichTextPreview(bodyPreview, updatedText);
            DialoguePreviewWindow.RefreshOpenWindows(this);
            bodyField.Focus();
            var clampedCursor = Mathf.Clamp(cursorIndex, 0, updatedText.Length);
            bodyField.cursorIndex = clampedCursor;
            bodyField.selectIndex = clampedCursor;
            selectionState.SetCollapsed(clampedCursor);
        }

        private void BuildCommentNodeInspector(CommentNodeData node)
        {
            _inspectorView.Add(CreateSectionTitle(DialogueEditorLocalization.Text("Comment")));

            var titleField = new TextField(DialogueEditorLocalization.Text("Title")) { value = node.Title, name = "comment-title-field" };
            titleField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Comment Title", () => node.Title = evt.newValue, refreshNodeVisuals: true);
            });
            _inspectorView.Add(titleField);

            var commentField = new TextField(DialogueEditorLocalization.Text("Comment"))
            {
                value = node.Comment,
                multiline = true,
                name = "comment-body-field"
            };
            commentField.AddToClassList("dialogue-editor__multiline-field");
            commentField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Comment Body", () => node.Comment = evt.newValue, refreshNodeVisuals: true);
            });
            _inspectorView.Add(commentField);

            var tintField = new ColorField(DialogueEditorLocalization.Text("Tint"))
            {
                value = node.Tint,
                showAlpha = true,
                hdr = false,
                name = "comment-tint-field"
            };
            tintField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Comment Tint", () => node.Tint = evt.newValue, refreshNodeVisuals: true);
            });
            _inspectorView.Add(tintField);

            var areaField = new RectField(DialogueEditorLocalization.Text("Area")) { value = node.Area, name = "comment-area-field" };
            areaField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Comment Area", () =>
                {
                    node.Area = evt.newValue;
                    node.Position = evt.newValue.position;
                }, reloadGraph: true);
            });
            _inspectorView.Add(areaField);

            var deleteButton = new Button(() => _graphView.DeleteNode(node)) { text = DialogueEditorLocalization.Text("Delete Comment") };
            deleteButton.AddToClassList("dialogue-editor__danger-button");
            _inspectorView.Add(deleteButton);
        }

        private void BuildFunctionNodeInspector(FunctionNodeData node)
        {
            _inspectorView.Add(CreateSectionTitle(DialogueEditorLocalization.Text("Function")));
            BuildExecutableCommonFields(node, DialogueEditorLocalization.Text("Function"));

            var functions = DialogueExecutionRegistry.GetFunctions();
            var descriptor = functions.FirstOrDefault(function => function.Id == node.FunctionId);
            if (functions.Count > 0)
            {
                var labels = functions
                    .Select(function => string.IsNullOrWhiteSpace(function.Category)
                        ? function.DisplayName
                        : $"{function.Category}: {function.DisplayName}")
                    .ToList();
                var selectedIndex = Mathf.Max(0, functions.Select((function, index) => new { function, index })
                    .FirstOrDefault(item => item.function.Id == node.FunctionId)?.index ?? 0);
                var functionField = new PopupField<string>(DialogueEditorLocalization.Text("Known Function"), labels, selectedIndex)
                {
                    name = "function-descriptor-field"
                };
                functionField.RegisterValueChangedCallback(evt =>
                {
                    var index = labels.IndexOf(evt.newValue);
                    if (index < 0 || index >= functions.Count)
                    {
                        return;
                    }

                    var selected = functions[index];
                    PerformNodeScopedChange("Select Function", () =>
                    {
                        node.FunctionId = selected.Id;
                        node.FailurePolicy = selected.DefaultFailurePolicy;
                        EnsureDescriptorArguments(node.Arguments, selected.Parameters);
                    }, refreshNodeVisuals: true, refreshInspector: true);
                });
                _inspectorView.Add(functionField);
            }

            var functionIdField = new TextField(DialogueEditorLocalization.Text("FunctionId")) { value = node.FunctionId, name = "function-id-field" };
            functionIdField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Function Id", () => node.FunctionId = evt.newValue, refreshNodeVisuals: true, refreshInspector: true);
            });
            _inspectorView.Add(functionIdField);

            if (!string.IsNullOrWhiteSpace(descriptor.Description))
            {
                _inspectorView.Add(CreateInlineHelp(descriptor.Description));
            }

            var closeToggle = new Toggle(DialogueEditorLocalization.Text("Close Dialogue Before Execute")) { value = node.CloseDialogueBeforeExecute, name = "function-close-toggle" };
            closeToggle.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Toggle Function Close", () => node.CloseDialogueBeforeExecute = evt.newValue, refreshNodeVisuals: true);
            });
            _inspectorView.Add(closeToggle);

            var waitToggle = new Toggle(DialogueEditorLocalization.Text("Wait For Completion")) { value = node.WaitForCompletion, name = "function-wait-toggle" };
            waitToggle.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Toggle Function Wait", () => node.WaitForCompletion = evt.newValue, refreshNodeVisuals: true);
            });
            _inspectorView.Add(waitToggle);

            var policyField = new EnumField(DialogueEditorLocalization.Text("Failure Policy"), node.FailurePolicy) { name = "function-failure-policy-field" };
            policyField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Function Failure Policy", () => node.FailurePolicy = (DialogueExecutionFailurePolicy)evt.newValue);
            });
            _inspectorView.Add(policyField);

            BuildArgumentsEditor(DialogueEditorLocalization.Text("Arguments"), node.Arguments, descriptor.Parameters, DialogueEditorLocalization.Text("Function Arguments"));
            BuildExecutableValidation(node);
            BuildConditionEditor(node.Condition, DialogueEditorLocalization.Text("Condition"), "Edit Function Condition");
            BuildLinksInspector(node, false);
            BuildDeleteButton(node, DialogueEditorLocalization.Text("Delete Function"));
        }

        private void BuildSceneNodeInspector(SceneNodeData node)
        {
            _inspectorView.Add(CreateSectionTitle(DialogueEditorLocalization.Text("Scene")));
            BuildExecutableCommonFields(node, DialogueEditorLocalization.Text("Scene"));

            var scenes = DialogueExecutionRegistry.GetScenes();
            if (scenes.Count > 0 && string.IsNullOrWhiteSpace(node.SceneKey))
            {
                var defaultScene = scenes[0];
                PerformNodeScopedChange("Select Scene", () =>
                {
                    node.SceneKey = defaultScene.SceneKey;
                    EnsureDescriptorArguments(node.Parameters, defaultScene.Parameters);
                }, refreshNodeVisuals: true);
            }

            var descriptor = scenes.FirstOrDefault(scene => scene.SceneKey == node.SceneKey);
            if (scenes.Count > 0)
            {
                var labels = scenes
                    .Select(scene => string.IsNullOrWhiteSpace(scene.Category)
                        ? scene.DisplayName
                        : $"{scene.Category}: {scene.DisplayName}")
                    .ToList();
                var selectedIndex = Mathf.Max(0, scenes.Select((scene, index) => new { scene, index })
                    .FirstOrDefault(item => item.scene.SceneKey == node.SceneKey)?.index ?? 0);
                var sceneField = new PopupField<string>(DialogueEditorLocalization.Text("Known Scene"), labels, selectedIndex)
                {
                    name = "scene-descriptor-field"
                };
                sceneField.RegisterValueChangedCallback(evt =>
                {
                    var index = labels.IndexOf(evt.newValue);
                    if (index < 0 || index >= scenes.Count)
                    {
                        return;
                    }

                    var selected = scenes[index];
                    PerformNodeScopedChange("Select Scene", () =>
                    {
                        node.SceneKey = selected.SceneKey;
                        EnsureDescriptorArguments(node.Parameters, selected.Parameters);
                    }, refreshNodeVisuals: true, refreshInspector: true);
                });
                _inspectorView.Add(sceneField);
            }

            var sceneKeyField = new TextField(DialogueEditorLocalization.Text("SceneKey")) { value = node.SceneKey, name = "scene-key-field" };
            sceneKeyField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Scene Key", () => node.SceneKey = evt.newValue, refreshNodeVisuals: true, refreshInspector: true);
            });
            _inspectorView.Add(sceneKeyField);

            var modeField = new EnumField(DialogueEditorLocalization.Text("Load Mode"), node.LoadMode) { name = "scene-load-mode-field" };
            modeField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Scene Load Mode", () => node.LoadMode = (DialogueSceneLoadMode)evt.newValue, refreshNodeVisuals: true);
            });
            _inspectorView.Add(modeField);

            var entryField = new TextField(DialogueEditorLocalization.Text("EntryPointId")) { value = node.EntryPointId, name = "scene-entry-point-field" };
            entryField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Scene Entry Point", () => node.EntryPointId = evt.newValue, refreshNodeVisuals: true);
            });
            _inspectorView.Add(entryField);

            var transitionField = new TextField(DialogueEditorLocalization.Text("TransitionId")) { value = node.TransitionId, name = "scene-transition-field" };
            transitionField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Scene Transition", () => node.TransitionId = evt.newValue, refreshNodeVisuals: true);
            });
            _inspectorView.Add(transitionField);

            var closeToggle = new Toggle(DialogueEditorLocalization.Text("Close Dialogue Before Execute")) { value = node.CloseDialogueBeforeExecute, name = "scene-close-toggle" };
            closeToggle.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Toggle Scene Close", () => node.CloseDialogueBeforeExecute = evt.newValue, refreshNodeVisuals: true);
            });
            _inspectorView.Add(closeToggle);

            var waitToggle = new Toggle(DialogueEditorLocalization.Text("Wait For Completion")) { value = node.WaitForCompletion, name = "scene-wait-toggle" };
            waitToggle.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Toggle Scene Wait", () => node.WaitForCompletion = evt.newValue, refreshNodeVisuals: true);
            });
            _inspectorView.Add(waitToggle);

            if (!string.IsNullOrWhiteSpace(descriptor.Description))
            {
                _inspectorView.Add(CreateInlineHelp(descriptor.Description));
            }

            BuildArgumentsEditor(DialogueEditorLocalization.Text("Parameters"), node.Parameters, descriptor.Parameters, DialogueEditorLocalization.Text("Scene Parameters"));
            BuildExecutableValidation(node);
            BuildConditionEditor(node.Condition, DialogueEditorLocalization.Text("Condition"), "Edit Scene Condition");
            BuildLinksInspector(node, false);
            BuildDeleteButton(node, DialogueEditorLocalization.Text("Delete Scene"));
        }

        private void BuildDebugNodeInspector(DebugNodeData node)
        {
            _inspectorView.Add(CreateSectionTitle(DialogueEditorLocalization.Text("Debug")));
            BuildExecutableCommonFields(node, DialogueEditorLocalization.Text("Debug"));

            var messageField = new TextField(DialogueEditorLocalization.Text("Message Template"))
            {
                value = node.MessageTemplate,
                multiline = true,
                name = "debug-message-field"
            };
            messageField.AddToClassList("dialogue-editor__multiline-field");
            messageField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Debug Message", () => node.MessageTemplate = evt.newValue, refreshNodeVisuals: true);
            });
            _inspectorView.Add(messageField);

            var levelField = new EnumField(DialogueEditorLocalization.Text("Log Level"), node.LogLevel) { name = "debug-log-level-field" };
            levelField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Debug Log Level", () => node.LogLevel = (DialogueDebugLogLevel)evt.newValue, refreshNodeVisuals: true);
            });
            _inspectorView.Add(levelField);

            var includeToggle = new Toggle(DialogueEditorLocalization.Text("Include Arguments")) { value = node.IncludeArguments, name = "debug-include-arguments-toggle" };
            includeToggle.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Toggle Debug Arguments", () => node.IncludeArguments = evt.newValue, refreshNodeVisuals: true);
            });
            _inspectorView.Add(includeToggle);

            var policyField = new EnumField(DialogueEditorLocalization.Text("Failure Policy"), node.FailurePolicy) { name = "debug-failure-policy-field" };
            policyField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Debug Failure Policy", () => node.FailurePolicy = (DialogueExecutionFailurePolicy)evt.newValue);
            });
            _inspectorView.Add(policyField);

            BuildArgumentsEditor(DialogueEditorLocalization.Text("Arguments"), node.Arguments, Array.Empty<DialogueParameterDescriptor>(), DialogueEditorLocalization.Text("Debug Arguments"));
            BuildExecutableValidation(node);
            BuildConditionEditor(node.Condition, DialogueEditorLocalization.Text("Condition"), "Edit Debug Condition");
            BuildLinksInspector(node, false);
            BuildDeleteButton(node, DialogueEditorLocalization.Text("Delete Debug"));
        }

        private void BuildExecutableCommonFields(BaseNodeData node, string label)
        {
            var titleField = new TextField(DialogueEditorLocalization.Text("Title")) { value = node.Title, name = "executable-title-field" };
            titleField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange($"Edit {label} Title", () => node.Title = evt.newValue, refreshNodeVisuals: true);
            });
            _inspectorView.Add(titleField);

            _inspectorView.Add(BuildNodeIdEditor(node));
            _inspectorView.Add(BuildWhereUsedSection(DialogueWhereUsedUtility.GetWhereUsed(_database, _selectedNpc, _selectedDialogue, node)));
        }

        private void BuildExecutableValidation(BaseNodeData node)
        {
            var issues = DialogueExecutableValidator.ValidateNode(node);
            if (issues.Count == 0)
            {
                return;
            }

            _inspectorView.Add(CreateSectionTitle(DialogueEditorLocalization.Text("Validation")));
            var box = new Box();
            box.AddToClassList("dialogue-editor__inspector-card");
            foreach (var issue in issues)
            {
                var label = new Label(LocalizeExecutableIssue(issue.Message));
                label.AddToClassList("dialogue-editor__choice-diagnostic");
                label.AddToClassList(issue.Severity == DialogueExecutableValidationSeverity.Error
                    ? "dialogue-editor__choice-diagnostic--error"
                    : "dialogue-editor__choice-diagnostic--warning");
                box.Add(label);
            }

            _inspectorView.Add(box);
        }

        private static string LocalizeExecutableIssue(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Empty;
            }

            if (message == "FunctionId is empty." ||
                message == "SceneKey is empty." ||
                message == "Argument has invalid serialized value data.")
            {
                return DialogueEditorLocalization.Text(message);
            }

            if (message.StartsWith("Function '", StringComparison.Ordinal) && message.EndsWith("' is not registered.", StringComparison.Ordinal))
            {
                var id = message.Substring("Function '".Length, message.Length - "Function '".Length - "' is not registered.".Length);
                return DialogueEditorLocalization.Format("Function '{0}' is not registered.", id);
            }

            if (message.StartsWith("Scene '", StringComparison.Ordinal) && message.EndsWith("' is not registered.", StringComparison.Ordinal))
            {
                var id = message.Substring("Scene '".Length, message.Length - "Scene '".Length - "' is not registered.".Length);
                return DialogueEditorLocalization.Format("Scene '{0}' is not registered.", id);
            }

            if (message.StartsWith("Required argument '", StringComparison.Ordinal) && message.EndsWith("' is missing.", StringComparison.Ordinal))
            {
                var name = message.Substring("Required argument '".Length, message.Length - "Required argument '".Length - "' is missing.".Length);
                return DialogueEditorLocalization.Format("Required argument '{0}' is missing.", name);
            }

            if (message.StartsWith("Argument '", StringComparison.Ordinal) && message.Contains("' expects ") && message.EndsWith(".", StringComparison.Ordinal))
            {
                var afterPrefix = message.Substring("Argument '".Length);
                var nameEnd = afterPrefix.IndexOf("' expects ", StringComparison.Ordinal);
                if (nameEnd >= 0)
                {
                    var name = afterPrefix.Substring(0, nameEnd);
                    var rest = afterPrefix.Substring(nameEnd + "' expects ".Length).TrimEnd('.');
                    var parts = rest.Split(new[] { " but is " }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        return DialogueEditorLocalization.Format("Argument '{0}' expects {1} but is {2}.", name, parts[0], parts[1]);
                    }
                }
            }

            return DialogueEditorLocalization.Text(message);
        }


        private void BuildArgumentsEditor(
            string title,
            List<DialogueArgumentEntry> arguments,
            IReadOnlyList<DialogueParameterDescriptor> descriptorParameters,
            string undoLabel)
        {
            _inspectorView.Add(CreateSectionTitle(title));
            arguments ??= new List<DialogueArgumentEntry>();
            var descriptorLookup = (descriptorParameters ?? Array.Empty<DialogueParameterDescriptor>())
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Name))
                .ToDictionary(parameter => parameter.Name, parameter => parameter);

            if (arguments.Count == 0)
            {
                _inspectorView.Add(CreateInlineHelp(DialogueEditorLocalization.Text("No arguments configured.")));
            }

            for (var index = 0; index < arguments.Count; index++)
            {
                var argumentIndex = index;
                var argument = arguments[index] ?? new DialogueArgumentEntry();
                arguments[index] = argument;

                var box = new Box();
                box.AddToClassList("dialogue-editor__inspector-card");

                var nameField = new TextField(DialogueEditorLocalization.Text("Name")) { value = argument.Name, name = "argument-name-field" };
                nameField.RegisterValueChangedCallback(evt =>
                {
                    PerformNodeScopedChange($"Edit {undoLabel} Name", () => argument.Name = evt.newValue, refreshInspector: true);
                });
                box.Add(nameField);

                var expectedType = descriptorLookup.TryGetValue(argument.Name, out var descriptor)
                    ? descriptor.Type
                    : argument.Value?.Type ?? DialogueArgumentType.String;
                var typeField = new EnumField(DialogueEditorLocalization.Text("Type"), argument.Value?.Type ?? expectedType) { name = "argument-type-field" };
                typeField.RegisterValueChangedCallback(evt =>
                {
                    PerformNodeScopedChange($"Edit {undoLabel} Type", () =>
                    {
                        argument.Value ??= new DialogueArgumentValue();
                        argument.Value.Type = (DialogueArgumentType)evt.newValue;
                    }, refreshInspector: true);
                });
                box.Add(typeField);

                AddArgumentValueField(box, argument, undoLabel);
                if (!string.IsNullOrWhiteSpace(descriptor.Hint))
                {
                    box.Add(CreateInlineHelp(descriptor.Hint));
                }

                var removeButton = new Button(() =>
                {
                    PerformNodeScopedChange($"Remove {undoLabel}", () => arguments.RemoveAt(argumentIndex), refreshNodeVisuals: true, refreshInspector: true);
                })
                {
                    text = DialogueEditorLocalization.Text("Remove Argument")
                };
                removeButton.AddToClassList("dialogue-editor__danger-button");
                box.Add(removeButton);
                _inspectorView.Add(box);
            }

            var addButton = new Button(() =>
            {
                PerformNodeScopedChange($"Add {undoLabel}", () => arguments.Add(new DialogueArgumentEntry()), refreshNodeVisuals: true, refreshInspector: true);
            })
            {
                text = DialogueEditorLocalization.Text("Add Argument")
            };
            _inspectorView.Add(addButton);
        }

        private void AddArgumentValueField(VisualElement box, DialogueArgumentEntry argument, string undoLabel)
        {
            argument.Value ??= new DialogueArgumentValue();
            switch (argument.Value.Type)
            {
                case DialogueArgumentType.Int:
                    var intField = new IntegerField(DialogueEditorLocalization.Text("Value")) { value = argument.Value.IntValue, name = "argument-int-value-field" };
                    intField.RegisterValueChangedCallback(evt =>
                    {
                        PerformNodeScopedChange($"Edit {undoLabel} Value", () => argument.Value.IntValue = evt.newValue);
                    });
                    box.Add(intField);
                    break;
                case DialogueArgumentType.Float:
                    var floatField = new FloatField(DialogueEditorLocalization.Text("Value")) { value = argument.Value.FloatValue, name = "argument-float-value-field" };
                    floatField.RegisterValueChangedCallback(evt =>
                    {
                        PerformNodeScopedChange($"Edit {undoLabel} Value", () => argument.Value.FloatValue = evt.newValue);
                    });
                    box.Add(floatField);
                    break;
                case DialogueArgumentType.Bool:
                    var boolField = new Toggle(DialogueEditorLocalization.Text("Value")) { value = argument.Value.BoolValue, name = "argument-bool-value-field" };
                    boolField.RegisterValueChangedCallback(evt =>
                    {
                        PerformNodeScopedChange($"Edit {undoLabel} Value", () => argument.Value.BoolValue = evt.newValue);
                    });
                    box.Add(boolField);
                    break;
                default:
                    var stringField = new TextField(DialogueEditorLocalization.Text("Value")) { value = argument.Value.StringValue, name = "argument-string-value-field" };
                    stringField.RegisterValueChangedCallback(evt =>
                    {
                        PerformNodeScopedChange($"Edit {undoLabel} Value", () => argument.Value.StringValue = evt.newValue);
                    });
                    box.Add(stringField);
                    break;
            }
        }

        private static void EnsureDescriptorArguments(
            IList<DialogueArgumentEntry> arguments,
            IReadOnlyList<DialogueParameterDescriptor> parameters)
        {
            if (arguments == null || parameters == null)
            {
                return;
            }

            foreach (var parameter in parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter.Name) ||
                    arguments.Any(argument => argument != null && argument.Name == parameter.Name))
                {
                    continue;
                }

                arguments.Add(new DialogueArgumentEntry
                {
                    Name = parameter.Name,
                    Value = parameter.DefaultValue?.Clone() ?? new DialogueArgumentValue { Type = parameter.Type }
                });
            }
        }

        private void BuildDeleteButton(BaseNodeData node, string text)
        {
            var deleteButton = new Button(() => _graphView.DeleteNode(node)) { text = text };
            deleteButton.AddToClassList("dialogue-editor__danger-button");
            _inspectorView.Add(deleteButton);
        }

        private void BuildLinksInspector(BaseNodeData node, bool showChoiceText = true)
        {
            _inspectorView.Add(CreateSectionTitle(DialogueEditorLocalization.Text("Connected Links")));

            var links = DialogueGraphUtility
                .GetOutgoingLinks(_selectedDialogue.Graph, node.Id)
                .Where(link => !string.IsNullOrWhiteSpace(link.ToNodeId))
                .ToList();

            if (links.Count == 0)
            {
                _inspectorView.Add(CreateInlineHelp(DialogueEditorLocalization.Text("No linked outputs yet. Drag from the bottom of this node to the top of another node.")));
                return;
            }

            foreach (var link in links)
            {
                var box = new Box();
                box.AddToClassList("dialogue-editor__inspector-card");

                var target = DialogueGraphUtility.GetNode(_selectedDialogue.Graph, link.ToNodeId);
                box.Add(new Label(DialogueEditorLocalization.Format("Target: {0}", target?.Title ?? DialogueEditorLocalization.Text("Unconnected"))));
                if (showChoiceText && node is DialogueTextNodeData textNode)
                {
                    AddChoiceFlowDiagnostics(box, DialogueChoiceFlowDiagnostics.Analyze(_selectedDialogue, textNode)
                        .Where(diagnostic => diagnostic.Link == link));
                }

                var orderField = new IntegerField(DialogueEditorLocalization.Text("Order")) { value = link.Order, name = "link-order-field" };
                orderField.RegisterValueChangedCallback(evt =>
                {
                    PerformNodeScopedChange("Reorder Link", () =>
                    {
                        link.Order = Mathf.Max(0, evt.newValue);
                        DialogueGraphUtility.NormalizeLinkOrder(_selectedDialogue.Graph, node.Id);
                    }, refreshNodeVisuals: true, refreshInspector: true);
                });
                box.Add(orderField);

                if (showChoiceText)
                {
                    var choiceField = new TextField(DialogueEditorLocalization.Text("Choice Text")) { value = link.ChoiceText, name = "link-choice-field" };
                    choiceField.RegisterValueChangedCallback(evt =>
                    {
                        PerformNodeScopedChange("Edit Link Choice Text", () => link.ChoiceText = evt.newValue, refreshNodeVisuals: true);
                    });
                    box.Add(choiceField);
                }
                else
                {
                    box.Add(CreateInlineHelp(DialogueEditorLocalization.Text("Executable links ignore ChoiceText and use Order only.")));
                }

                var removeButton = new Button(() =>
                {
                    _graphView.DeleteLink(link, true, true);
                    RefreshInspector();
                })
                {
                    text = DialogueEditorLocalization.Text("Remove Link")
                };
                removeButton.AddToClassList("dialogue-editor__danger-button");
                box.Add(removeButton);

                _inspectorView.Add(box);
            }
        }

        private void BuildChoiceFlowDiagnostics(DialogueTextNodeData node)
        {
            var diagnostics = DialogueChoiceFlowDiagnostics.Analyze(_selectedDialogue, node);
            if (diagnostics.Count == 0)
            {
                return;
            }

            _inspectorView.Add(CreateSectionTitle(DialogueEditorLocalization.Text("Choice Flow Diagnostics")));
            var box = new Box();
            box.AddToClassList("dialogue-editor__inspector-card");
            AddChoiceFlowDiagnostics(box, diagnostics);
            _inspectorView.Add(box);
        }

        private void AddChoiceFlowDiagnostics(
            VisualElement container,
            IEnumerable<DialogueChoiceFlowDiagnostic> diagnostics)
        {
            if (container == null || diagnostics == null)
            {
                return;
            }

            foreach (var diagnostic in diagnostics)
            {
                var label = new Label(diagnostic.Message);
                label.AddToClassList("dialogue-editor__choice-diagnostic");
                label.AddToClassList(diagnostic.Severity == DialogueChoiceFlowSeverity.Error
                    ? "dialogue-editor__choice-diagnostic--error"
                    : "dialogue-editor__choice-diagnostic--warning");
                container.Add(label);
            }
        }

        private VisualElement BuildNpcIdEditor(NpcEntry npc)
        {
            return BuildIdEditor(
                DialogueEditorLocalization.Text("NPC Id"),
                "npc-id-field",
                npc?.Id,
                DialogueIdentifierUtility.GetIssues(_database, npc),
                () => ChangeNpcId(npc, GuidUtility.NewGuid()),
                () => ChangeNpcId(npc, GuidUtility.NewGuid()),
                newValue => ChangeNpcId(npc, newValue));
        }

        private VisualElement BuildDialogueIdEditor(DialogueEntry dialogue)
        {
            return BuildIdEditor(
                DialogueEditorLocalization.Text("Dialogue Id"),
                "dialogue-id-field",
                dialogue?.Id,
                DialogueIdentifierUtility.GetIssues(_database, dialogue),
                () => ChangeDialogueId(dialogue, GuidUtility.NewGuid()),
                () => ChangeDialogueId(dialogue, GuidUtility.NewGuid()),
                newValue => ChangeDialogueId(dialogue, newValue));
        }

        private VisualElement BuildNodeIdEditor(BaseNodeData node)
        {
            return BuildIdEditor(
                DialogueEditorLocalization.Text("Node Id"),
                "node-id-field",
                node?.Id,
                DialogueIdentifierUtility.GetIssues(_selectedDialogue?.Graph, node),
                () => ChangeNodeId(node, GuidUtility.NewGuid()),
                () => ChangeNodeId(node, GuidUtility.NewGuid()),
                newValue => ChangeNodeId(node, newValue));
        }

        private VisualElement BuildIdEditor(
            string label,
            string fieldName,
            string value,
            IReadOnlyList<string> issues,
            Action generate,
            Action safeRegenerate,
            Action<string> commit)
        {
            var box = new Box();
            box.AddToClassList("dialogue-editor__id-card");

            var field = new TextField(label)
            {
                value = value ?? string.Empty,
                isDelayed = true,
                name = fieldName
            };
            field.AddToClassList("dialogue-editor__id-field");
            AttachGraphBlurOnInteraction(field);
            field.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == evt.previousValue)
                {
                    return;
                }

                commit?.Invoke(evt.newValue);
            });
            box.Add(field);

            var actionRow = new VisualElement();
            actionRow.AddToClassList("dialogue-editor__id-actions");

            var generateButton = CreateActionButton(DialogueEditorLocalization.Text("Generate"), generate);
            generateButton.AddToClassList("dialogue-editor__id-button");
            actionRow.Add(generateButton);

            var safeButton = CreateActionButton(DialogueEditorLocalization.Text("Safe Regenerate"), safeRegenerate);
            safeButton.AddToClassList("dialogue-editor__id-button");
            actionRow.Add(safeButton);

            box.Add(actionRow);
            AddIdentifierIssues(box, issues);
            return box;
        }

        private void AddIdentifierIssues(VisualElement container, IReadOnlyList<string> issues)
        {
            if (container == null || issues == null)
            {
                return;
            }

            foreach (var issue in issues)
            {
                var label = new Label(issue);
                label.AddToClassList("dialogue-editor__id-issue");
                container.Add(label);
            }
        }

        private VisualElement BuildWhereUsedSection(IReadOnlyList<DialogueWhereUsedResult> results)
        {
            var box = new Box();
            box.AddToClassList("dialogue-editor__where-used-card");

            var title = new Label(DialogueEditorLocalization.Text("Where Used"));
            title.AddToClassList("dialogue-editor__where-used-title");
            box.Add(title);

            var internalResults = results?
                .Where(result => result.Kind == DialogueReferenceKind.Internal)
                .ToList() ?? new List<DialogueWhereUsedResult>();
            var externalResults = results?
                .Where(result => result.Kind == DialogueReferenceKind.External)
                .ToList() ?? new List<DialogueWhereUsedResult>();

            AddWhereUsedGroup(box, DialogueEditorLocalization.Text("Internal References"), internalResults);
            AddWhereUsedGroup(box, DialogueEditorLocalization.Text("External References"), externalResults, DialogueEditorLocalization.Text("No external references reported."));
            return box;
        }

        private void AddWhereUsedGroup(
            VisualElement container,
            string title,
            IReadOnlyList<DialogueWhereUsedResult> results,
            string emptyText = null)
        {
            emptyText ??= DialogueEditorLocalization.Text("No internal references.");
            var groupTitle = new Label(title);
            groupTitle.AddToClassList("dialogue-editor__where-used-group-title");
            container.Add(groupTitle);

            if (results == null || results.Count == 0)
            {
                var empty = new Label(emptyText);
                empty.AddToClassList("dialogue-editor__where-used-empty");
                container.Add(empty);
                return;
            }

            foreach (var result in results)
            {
                var label = new Label(string.IsNullOrWhiteSpace(result.Detail)
                    ? result.Label
                    : $"{result.Label}: {result.Detail}");
                label.AddToClassList("dialogue-editor__where-used-entry");
                container.Add(label);
            }
        }

        private void BuildConditionEditor(ConditionData condition, string title, string undoActionPrefix = null)
        {
            _inspectorView.Add(CreateSectionTitle(title));
            if (condition == null)
            {
                _inspectorView.Add(CreateInlineHelp(DialogueEditorLocalization.Text("No condition data is available.")));
                return;
            }

            var metadata = DialogueConditionMetadataRegistry.GetMetadata(condition.Type);
            var typeField = new EnumField(DialogueEditorLocalization.Text("Type"), condition.Type) { name = "condition-type-field" };
            typeField.RegisterValueChangedCallback(evt =>
            {
                ApplyConditionChange(undoActionPrefix, "Type", () =>
                {
                    condition.Type = (ConditionType)evt.newValue;
                    var nextMetadata = DialogueConditionMetadataRegistry.GetMetadata(condition.Type);
                    if (nextMetadata.Operators.Count > 0 &&
                        !nextMetadata.Operators.Contains(condition.Operator))
                    {
                        condition.Operator = nextMetadata.DefaultOperator;
                    }

                    if (condition.Type == ConditionType.None)
                    {
                        condition.Key = string.Empty;
                        condition.Value = string.Empty;
                    }
                });
                RefreshInspector();
            });
            _inspectorView.Add(typeField);

            _inspectorView.Add(CreateInlineHelp(metadata.ValueHint));
            if (!metadata.ShowKey)
            {
                return;
            }

            var keySuggestions = DialogueConditionMetadataRegistry.GetKeySuggestions(condition.Type);
            if (keySuggestions.Count > 0)
            {
                var suggestionLabels = keySuggestions
                    .Select(suggestion => string.IsNullOrWhiteSpace(suggestion.Category)
                        ? suggestion.Label
                        : $"{suggestion.Category}: {suggestion.Label}")
                    .ToList();
                var currentSuggestionIndex = keySuggestions
                    .Select((suggestion, index) => new { suggestion, index })
                    .FirstOrDefault(item => item.suggestion.Key == condition.Key)?.index ?? 0;
                var suggestionField = new PopupField<string>(
                    DialogueEditorLocalization.Text("Known Key"),
                    suggestionLabels,
                    Mathf.Max(0, currentSuggestionIndex))
                {
                    name = "condition-key-suggestion-field"
                };
                suggestionField.RegisterValueChangedCallback(evt =>
                {
                    var selectedIndex = suggestionLabels.IndexOf(evt.newValue);
                    if (selectedIndex < 0 || selectedIndex >= keySuggestions.Count)
                    {
                        return;
                    }

                    ApplyConditionChange(undoActionPrefix, "Key", () => condition.Key = keySuggestions[selectedIndex].Key);
                    RefreshInspector();
                });
                _inspectorView.Add(suggestionField);
            }

            var keyField = new TextField(DialogueEditorLocalization.Text("Key")) { value = condition.Key, name = "condition-key-field" };
            keyField.RegisterValueChangedCallback(evt =>
            {
                ApplyConditionChange(undoActionPrefix, "Key", () => condition.Key = evt.newValue);
            });
            _inspectorView.Add(keyField);

            if (metadata.ShowOperator && metadata.Operators.Count > 0)
            {
                var operators = metadata.Operators.ToList();
                var selectedOperator = operators.Contains(condition.Operator)
                    ? condition.Operator
                    : metadata.DefaultOperator;
                var operatorField = new PopupField<string>(
                    DialogueEditorLocalization.Text("Operator"),
                    operators,
                    operators.IndexOf(selectedOperator))
                {
                    name = "condition-operator-field"
                };
                operatorField.RegisterValueChangedCallback(evt =>
                {
                    ApplyConditionChange(undoActionPrefix, "Operator", () => condition.Operator = evt.newValue);
                    RefreshInspector();
                });
                _inspectorView.Add(operatorField);
            }

            if (!metadata.ShowValue || string.Equals(condition.Operator, "Truthy", StringComparison.Ordinal))
            {
                _inspectorView.Add(CreateInlineHelp(DialogueEditorLocalization.Text("The selected operator does not use an expected value.")));
                return;
            }

            var valueField = new TextField(DialogueEditorLocalization.Text("Value")) { value = condition.Value, name = "condition-value-field" };
            valueField.RegisterValueChangedCallback(evt =>
            {
                ApplyConditionChange(undoActionPrefix, "Value", () => condition.Value = evt.newValue);
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
            EnsureDialogueSpeakers(_selectedNpc, dialogue);

            _selectedNpc.Dialogues.Add(dialogue);
            _selectedDialogue = dialogue;
            _selectedNode = null;
            MarkChanged();
            RefreshAll();
        }

        private static bool EnsureDialogueSpeakers(DialogueDatabaseAsset database)
        {
            var changed = false;
            if (database?.Npcs == null)
            {
                return false;
            }

            foreach (var npc in database.Npcs)
            {
                if (npc?.Dialogues == null)
                {
                    continue;
                }

                foreach (var dialogue in npc.Dialogues)
                {
                    changed |= EnsureDialogueSpeakers(npc, dialogue);
                }
            }

            return changed;
        }

        private static bool EnsureDialogueSpeakers(NpcEntry ownerNpc, DialogueEntry dialogue)
        {
            if (dialogue == null)
            {
                return false;
            }

            var changed = false;
            if (dialogue.Speakers == null)
            {
                dialogue.Speakers = new List<DialogueSpeakerEntry>();
                changed = true;
            }

            var removed = dialogue.Speakers.RemoveAll(speaker => speaker == null);
            changed |= removed > 0;

            if (dialogue.Speakers.Count == 0)
            {
                dialogue.Speakers.Add(new DialogueSpeakerEntry
                {
                    Name = string.IsNullOrWhiteSpace(ownerNpc?.Name) ? "NPC" : ownerNpc.Name
                });
                changed = true;
            }

            return changed;
        }

        internal void AddSpeaker(DialogueEntry dialogue)
        {
            if (dialogue?.Speakers == null)
            {
                return;
            }

            PerformDialogueScopedChange("Add Speaker", () =>
            {
                dialogue.Speakers.Add(new DialogueSpeakerEntry
                {
                    Name = DialogueEditorLocalization.Format("Speaker {0}", dialogue.Speakers.Count + 1)
                });
            }, refreshNodeVisuals: true, refreshInspector: true);
        }

        internal void RemoveSpeaker(DialogueEntry dialogue, DialogueSpeakerEntry speaker)
        {
            if (dialogue?.Speakers == null || speaker == null || dialogue.Speakers.Count <= 1)
            {
                return;
            }

            var speakerId = speaker.Id;
            PerformDialogueScopedChange("Remove Speaker", () =>
            {
                dialogue.Speakers.Remove(speaker);
                foreach (var textNode in dialogue.Graph?.Nodes?.OfType<DialogueTextNodeData>() ?? Enumerable.Empty<DialogueTextNodeData>())
                {
                    if (textNode.SpeakerId == speakerId)
                    {
                        textNode.SpeakerId = string.Empty;
                    }
                }
            }, refreshNodeVisuals: true, refreshInspector: true);
        }

        private static List<string> BuildSpeakerOptionLabels(IReadOnlyList<DialogueSpeakerEntry> speakers)
        {
            var nameCounts = speakers
                .Select(speaker => string.IsNullOrWhiteSpace(speaker.Name) ? DialogueEditorLocalization.Text("Speaker") : speaker.Name)
                .GroupBy(name => name)
                .ToDictionary(group => group.Key, group => group.Count());

            return speakers
                .Select((speaker, index) =>
                {
                    var name = string.IsNullOrWhiteSpace(speaker.Name) ? DialogueEditorLocalization.Text("Speaker") : speaker.Name;
                    return nameCounts[name] > 1 ? $"{name} ({index + 1})" : name;
                })
                .ToList();
        }

        [Serializable]
        private sealed class RichTextColorList
        {
            public List<string> Colors = new();
        }

        private sealed class RichTextSelectionState
        {
            private int _start;
            private int _end;

            public void Capture(TextField field)
            {
                if (field == null)
                {
                    _start = 0;
                    _end = 0;
                    return;
                }

                var textLength = field.value?.Length ?? 0;
                _start = Mathf.Clamp(Mathf.Min(field.cursorIndex, field.selectIndex), 0, textLength);
                _end = Mathf.Clamp(Mathf.Max(field.cursorIndex, field.selectIndex), 0, textLength);
            }

            public void CaptureForToolbarAction(TextField field)
            {
                if (field == null)
                {
                    return;
                }

                var textLength = field.value?.Length ?? 0;
                var start = Mathf.Clamp(Mathf.Min(field.cursorIndex, field.selectIndex), 0, textLength);
                var end = Mathf.Clamp(Mathf.Max(field.cursorIndex, field.selectIndex), 0, textLength);
                if (start != end || field.focusController?.focusedElement == field)
                {
                    _start = start;
                    _end = end;
                }
            }

            public void SetCollapsed(int cursorIndex)
            {
                _start = cursorIndex;
                _end = cursorIndex;
            }

            public void GetRange(TextField field, int textLength, out int start, out int end)
            {
                if (field != null)
                {
                    var fieldStart = Mathf.Clamp(Mathf.Min(field.cursorIndex, field.selectIndex), 0, textLength);
                    var fieldEnd = Mathf.Clamp(Mathf.Max(field.cursorIndex, field.selectIndex), 0, textLength);
                    if (fieldStart != fieldEnd)
                    {
                        _start = fieldStart;
                        _end = fieldEnd;
                    }
                }

                start = Mathf.Clamp(Mathf.Min(_start, _end), 0, textLength);
                end = Mathf.Clamp(Mathf.Max(_start, _end), 0, textLength);
            }
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
                EditorUtility.DisplayDialog(
                    DialogueEditorLocalization.Text("No dialogue selected"),
                    DialogueEditorLocalization.Text("Select a dialogue to preview it."),
                    DialogueEditorLocalization.Text("OK"));
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

            EditorUtility.DisplayDialog(
                DialogueEditorLocalization.Text("No database loaded"),
                DialogueEditorLocalization.Text("Create or load a dialogue database first."),
                DialogueEditorLocalization.Text("OK"));
            return false;
        }

        private void EnsureDialogueSelected()
        {
            if (_selectedDialogue == null)
            {
                if (_database == null)
                {
                    EditorUtility.DisplayDialog(
                        DialogueEditorLocalization.Text("No database loaded"),
                        DialogueEditorLocalization.Text("Create or load a dialogue database first."),
                        DialogueEditorLocalization.Text("OK"));
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
                case DialoguePaletteItemType.Function:
                    _graphView?.CreateFunctionNode(_graphView.GetCanvasCenter());
                    break;
                case DialoguePaletteItemType.Scene:
                    _graphView?.CreateSceneNode(_graphView.GetCanvasCenter());
                    break;
                case DialoguePaletteItemType.Debug:
                    _graphView?.CreateDebugNode(_graphView.GetCanvasCenter());
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
            if (_database != null && !_isProcessingUndoRedo)
            {
                EditorUtility.SetDirty(_database);
            }

            SyncDirtyState(_activeUndoGestureGroup == -1);
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
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
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
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
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

        private void OnUndoRedoPerformed()
        {
            if (_database == null)
            {
                return;
            }

            var selectedNpcId = _selectedNpc?.Id;
            var selectedDialogueId = _selectedDialogue?.Id;
            var selectedNodeId = _selectedNode?.Id;

            _activeUndoGestureGroup = -1;
            _isProcessingUndoRedo = true;
            try
            {
                ResolveSelectionState(selectedNpcId, selectedDialogueId, selectedNodeId);
                RefreshAll();
                if (_graphView == null || !_graphView.RestoreSelection(selectedNodeId))
                {
                    _selectedNode = null;
                    RefreshInspector();
                }
            }
            finally
            {
                _isProcessingUndoRedo = false;
            }

            SyncDirtyState(false);
            DialoguePreviewWindow.RefreshOpenWindows(this);
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
                _detailsToggleButton.text = DialogueEditorLocalization.Text(collapsed ? "Show" : "Hide");
            }
        }

        internal bool TryResolveDialogueById(string dialogueId, out NpcEntry ownerNpc, out DialogueEntry resolvedDialogue)
        {
            if (_database == null || string.IsNullOrWhiteSpace(dialogueId))
            {
                ownerNpc = null;
                resolvedDialogue = null;
                return false;
            }

            foreach (var npc in _database.Npcs)
            {
                foreach (var candidate in npc.Dialogues)
                {
                    if (candidate?.Id == dialogueId)
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

        private bool TryResolveDialogue(DialogueEntry dialogue, out NpcEntry ownerNpc, out DialogueEntry resolvedDialogue)
        {
            if (dialogue == null)
            {
                ownerNpc = null;
                resolvedDialogue = null;
                return false;
            }

            return TryResolveDialogueById(dialogue.Id, out ownerNpc, out resolvedDialogue);
        }

        private void ApplyUndoableNodeChange(string actionName, Action mutate)
        {
            if (_database == null || mutate == null)
            {
                return;
            }

            if (_isProcessingUndoRedo)
            {
                mutate();
                return;
            }

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(actionName);
            Undo.RegisterCompleteObjectUndo(_database, actionName);
            mutate();
            Undo.CollapseUndoOperations(group);
        }

        private void PerformNodeScopedChange(
            string actionName,
            Action mutate,
            bool refreshNodeVisuals = false,
            bool reloadGraph = false,
            bool refreshInspector = false,
            bool refreshProjectPanel = false)
        {
            ApplyUndoableNodeChange(actionName, () =>
            {
                mutate();

                if (reloadGraph)
                {
                    _graphView?.LoadGraph(_selectedDialogue?.Graph);
                }
                else if (refreshNodeVisuals)
                {
                    _graphView?.RefreshNodeVisuals();
                }

                MarkChanged();

                if (refreshProjectPanel)
                {
                    RefreshProjectPanel();
                }

                if (refreshInspector)
                {
                    RefreshInspector();
                }
            });
        }

        private void PerformDialogueScopedChange(
            string actionName,
            Action mutate,
            bool refreshNodeVisuals = false,
            bool reloadGraph = false,
            bool refreshInspector = false,
            bool refreshProjectPanel = false)
        {
            ApplyUndoableNodeChange(actionName, () =>
            {
                mutate();

                if (reloadGraph)
                {
                    _graphView?.LoadGraph(_selectedDialogue?.Graph);
                }
                else if (refreshNodeVisuals)
                {
                    _graphView?.RefreshNodeVisuals();
                }

                MarkChanged();
                DialoguePreviewWindow.RefreshOpenWindows(this);

                if (refreshProjectPanel)
                {
                    RefreshProjectPanel();
                }

                if (refreshInspector)
                {
                    RefreshInspector();
                }
            });
        }

        private void ApplyConditionChange(string undoActionPrefix, string fieldName, Action mutate)
        {
            if (mutate == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(undoActionPrefix))
            {
                mutate();
                MarkChanged();
                return;
            }

            PerformNodeScopedChange($"{undoActionPrefix} {fieldName}", mutate);
        }

        private void ChangeNpcId(NpcEntry npc, string newId)
        {
            if (npc == null || string.Equals(npc.Id, newId ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            if (!ConfirmIdentifierChange(DialogueEditorLocalization.Text("NPC")))
            {
                RefreshProjectPanel();
                RefreshInspector();
                return;
            }

            npc.Id = newId ?? string.Empty;
            MarkChanged();
            RefreshProjectPanel();
            RefreshInspector();
            DialoguePreviewWindow.RefreshOpenWindows(this);
        }

        private void ChangeDialogueId(DialogueEntry dialogue, string newId)
        {
            if (dialogue == null || string.Equals(dialogue.Id, newId ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            if (!ConfirmIdentifierChange(DialogueEditorLocalization.Text("Dialogue")))
            {
                RefreshProjectPanel();
                RefreshInspector();
                return;
            }

            dialogue.Id = newId ?? string.Empty;
            MarkChanged();
            RefreshProjectPanel();
            RefreshInspector();
            DialoguePreviewWindow.RefreshOpenWindows(this);
        }

        private void ChangeNodeId(BaseNodeData node, string newId)
        {
            if (node == null || _selectedDialogue?.Graph == null ||
                string.Equals(node.Id, newId ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            var oldId = node.Id;
            if (!ConfirmIdentifierChange(DialogueEditorLocalization.Text("Node"), DialogueEditorLocalization.Text("Internal graph links that target this node will be updated automatically.")))
            {
                RefreshInspector();
                return;
            }

            PerformNodeScopedChange(
                "Edit Node Id",
                () => DialogueIdentifierUtility.RenameNodeId(_selectedDialogue.Graph, node, newId),
                reloadGraph: true,
                refreshInspector: true);

            _graphView?.FrameAndSelectNode(node.Id);
            DialoguePreviewWindow.RefreshOpenWindows(this);

            if (string.IsNullOrWhiteSpace(oldId))
            {
                RefreshStatus();
            }
        }

        private bool ConfirmIdentifierChange(string ownerLabel, string detail = null)
        {
            if (SuppressIdentifierWarningsForTests)
            {
                return true;
            }

            var message = DialogueEditorLocalization.Format("{0} Id values may be referenced outside this dialogue database.", ownerLabel);
            if (!string.IsNullOrWhiteSpace(detail))
            {
                message += $"\n\n{detail}";
            }

            message += $"\n\n{DialogueEditorLocalization.Text("Continue changing this Id?")}";
            return EditorUtility.DisplayDialog(
                DialogueEditorLocalization.Text("Change Id"),
                message,
                DialogueEditorLocalization.Text("Change Id Button"),
                DialogueEditorLocalization.Text("Cancel"));
        }

        private void BeginUndoGesture(string actionName)
        {
            if (_database == null || _isProcessingUndoRedo || _activeUndoGestureGroup != -1)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            _activeUndoGestureGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(actionName);
            Undo.RegisterCompleteObjectUndo(_database, actionName);
        }

        private void EndUndoGesture()
        {
            if (_activeUndoGestureGroup == -1)
            {
                return;
            }

            var group = _activeUndoGestureGroup;
            _activeUndoGestureGroup = -1;
            Undo.CollapseUndoOperations(group);
            MarkChanged();
        }

        private void SyncDirtyState(bool persistAutosave)
        {
            if (_database == null)
            {
                _hasUnsavedChanges = false;
                RefreshStatus();
                return;
            }

            var currentSnapshot = DialogueEditorAutosaveStore.CaptureSnapshotJson(_database);
            _hasUnsavedChanges = !string.Equals(currentSnapshot, _savedStateSnapshot, StringComparison.Ordinal);

            if (_hasUnsavedChanges)
            {
                if (persistAutosave)
                {
                    SaveAutosave();
                }
            }
            else
            {
                ClearAutosaveSnapshot();
            }

            RefreshStatus();
        }

        private void ResolveSelectionState(string selectedNpcId, string selectedDialogueId, string selectedNodeId)
        {
            _selectedNode = null;
            _selectedDialogue = null;
            _selectedNpc = null;

            if (_database == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(selectedDialogueId) &&
                TryResolveDialogueById(selectedDialogueId, out var ownerNpc, out var resolvedDialogue))
            {
                _selectedNpc = ownerNpc;
                _selectedDialogue = resolvedDialogue;
            }
            else if (!string.IsNullOrWhiteSpace(selectedNpcId))
            {
                _selectedNpc = _database.Npcs.FirstOrDefault(npc => npc?.Id == selectedNpcId) ?? _database.Npcs.FirstOrDefault();
                _selectedDialogue = _selectedNpc?.Dialogues.FirstOrDefault();
            }
            else
            {
                _selectedNpc = _database.Npcs.FirstOrDefault();
                _selectedDialogue = _selectedNpc?.Dialogues.FirstOrDefault();
            }

            if (_selectedDialogue != null && !string.IsNullOrWhiteSpace(selectedNodeId))
            {
                _selectedNode = DialogueGraphUtility.GetNode(_selectedDialogue.Graph, selectedNodeId);
            }
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
                tooltip = hint;

                AddToClassList("dialogue-editor__palette-item");

                var titleLabel = new Label(title);
                titleLabel.AddToClassList("dialogue-editor__palette-item-title");
                Add(titleLabel);

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

    internal static class SaveChangesPromptWindow
    {
        public static bool ShowDialog(Rect ownerPosition, string bodyText = null)
        {
            var resolvedBodyText = string.IsNullOrWhiteSpace(bodyText)
                ? DialogueEditorLocalization.Text("Do you want to save changes before closing Dialogue Graph?")
                : bodyText;

            var shouldSave = EditorUtility.DisplayDialog(
                DialogueEditorLocalization.Text("Save changes?"),
                resolvedBodyText,
                DialogueEditorLocalization.Text("Yes"),
                DialogueEditorLocalization.Text("No"));
            return !shouldSave;
        }
    }
}

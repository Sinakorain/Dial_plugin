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
        private const int RichTextColorWheelSize = 96;
        private const int RichTextColorWheelTextureSize = 192;
        private const int RichTextBrightnessStripWidth = 96;
        private const int RichTextBrightnessStripHeight = 12;
        private delegate bool TryNormalizeRichTextColor(string color, out string normalized);
        private static Texture2D _richTextColorWheelTexture;

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
        private readonly Dictionary<DialoguePaletteItemType, PaletteItem> _paletteItems = new();
        private DialoguePaletteItemType? _activePaletteShortcutRebind;

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
        private bool IsPaletteShortcutRebinding => _activePaletteShortcutRebind != null;

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
            DialogueContentLanguageSettings.LanguageChanged += OnContentLanguageChanged;
        }

        private void OnDisable()
        {
            if (!ShouldPromptForSaveOnClose())
            {
                SaveAutosave();
            }

            UnregisterLifecycleHooks();
            DialogueEditorLanguageSettings.LanguageChanged -= OnEditorLanguageChanged;
            DialogueContentLanguageSettings.LanguageChanged -= OnContentLanguageChanged;
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

        private void OnContentLanguageChanged()
        {
            _graphView?.RefreshNodeVisuals();
            RefreshInspector();
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
                NodeInspectorRequestedAction = OnNodeInspectorRequested,
                CanvasFocusChangedAction = focused => _graphHost?.EnableInClassList("is-focused", focused),
                ApplyUndoableChangeAction = ApplyUndoableNodeChange,
                BeginUndoGestureAction = BeginUndoGesture,
                EndUndoGestureAction = EndUndoGesture,
                PaletteShortcutResolver = DialoguePaletteShortcutSettings.FindMatchingItem,
                PaletteShortcutAction = CreateNodeFromPaletteShortcut,
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
            mainActions.Add(CreateToolbarButton(DialogueEditorLocalization.Text("Localization"), OpenLocalizationWindow));
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

            var contentLanguages = GetAvailableContentLanguages();
            var currentContentLanguage = DialogueContentLanguageSettings.CurrentLanguageCode;
            if (!contentLanguages.Contains(currentContentLanguage, StringComparer.OrdinalIgnoreCase))
            {
                currentContentLanguage = DialogueTextLocalizationUtility.DefaultLanguageCode;
                DialogueContentLanguageSettings.SetCurrentLanguageCodeWithoutNotify(currentContentLanguage);
            }

            var contentLanguageIndex = Mathf.Max(0, contentLanguages.FindIndex(language =>
                string.Equals(language, currentContentLanguage, StringComparison.OrdinalIgnoreCase)));
            var contentLanguageField = new PopupField<string>(
                contentLanguages,
                contentLanguageIndex)
            {
                name = "content-language-field",
                tooltip = DialogueEditorLocalization.Text("Content Language")
            };
            contentLanguageField.RegisterValueChangedCallback(evt =>
            {
                DialogueContentLanguageSettings.CurrentLanguageCode = evt.newValue;
            });
            AttachGraphBlurOnInteraction(contentLanguageField);
            utilityActions.Add(contentLanguageField);

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

        private List<string> GetAvailableContentLanguages()
        {
            var languages = new SortedSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                DialogueTextLocalizationUtility.DefaultLanguageCode
            };

            foreach (var node in _database?.Npcs?
                         .Where(npc => npc != null)
                         .SelectMany(npc => npc.Dialogues ?? Enumerable.Empty<DialogueEntry>())
                         .Where(dialogue => dialogue?.Graph?.Nodes != null)
                         .SelectMany(dialogue => dialogue.Graph.Nodes) ?? Enumerable.Empty<BaseNodeData>())
            {
                var localizedEntries = node switch
                {
                    DialogueTextNodeData textNode => textNode.LocalizedBodyText,
                    DialogueChoiceNodeData choiceNode => choiceNode.LocalizedBodyText,
                    _ => null
                };
                foreach (var entry in localizedEntries ?? Enumerable.Empty<DialogueLocalizedTextEntry>())
                {
                    var normalized = DialogueTextLocalizationUtility.NormalizeLanguageCode(entry?.LanguageCode);
                    if (!string.IsNullOrWhiteSpace(normalized))
                    {
                        languages.Add(normalized);
                    }
                }
            }

            return languages
                .OrderBy(language => language == DialogueTextLocalizationUtility.DefaultLanguageCode ? 0 : 1)
                .ThenBy(language => language, StringComparer.OrdinalIgnoreCase)
                .ToList();
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
            _paletteItems.Clear();
            _activePaletteShortcutRebind = null;

            var panel = new VisualElement();
            panel.AddToClassList("dialogue-editor__panel");
            panel.AddToClassList("dialogue-editor__palette-panel");
            panel.name = "palette-panel";
            panel.Add(BuildPanelHeader(DialogueEditorLocalization.Text("Palette")));

            var content = new VisualElement();
            content.AddToClassList("dialogue-editor__palette");
            content.name = "palette-content";

            AddPaletteItem(content, DialoguePaletteItemType.TextNode, DialogueEditorLocalization.Text("Text Node"), DialogueEditorLocalization.Text("Click to add at center. Drag onto the graph to place."));
            AddPaletteItem(content, DialoguePaletteItemType.Choice, DialogueEditorLocalization.Text("Answer"), DialogueEditorLocalization.Text("A player answer button. Use Add Choice on a text node for the guided flow."));
            AddPaletteItem(content, DialoguePaletteItemType.Comment, DialogueEditorLocalization.Text("Comment"), DialogueEditorLocalization.Text("Click to add at center. Drag onto the graph to place."));
            AddPaletteItem(content, DialoguePaletteItemType.Function, DialogueEditorLocalization.Text("Function"), DialogueEditorLocalization.Text("Execute a project-provided function and continue."));
            AddPaletteItem(content, DialoguePaletteItemType.Scene, DialogueEditorLocalization.Text("Scene"), DialogueEditorLocalization.Text("Request project scene loading and continue."));
            AddPaletteItem(content, DialoguePaletteItemType.Debug, DialogueEditorLocalization.Text("Debug"), DialogueEditorLocalization.Text("Write a diagnostic log entry and continue."));

            panel.Add(content);
            return panel;
        }

        private void AddPaletteItem(VisualElement content, DialoguePaletteItemType itemType, string title, string hint)
        {
            var item = new PaletteItem(this, itemType, title, hint);
            _paletteItems[itemType] = item;
            content.Add(item);
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
                npcCard.Add(BuildNpcIdEditor(npc, true));
                if (_selectedNpc == npc)
                {
                    npcCard.Add(BuildWhereUsedSection(DialogueWhereUsedUtility.GetWhereUsed(_database, npc), true));
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
                    dialogueCard.Add(BuildDialogueIdEditor(dialogue, true));
                    if (_selectedDialogue == dialogue)
                    {
                        dialogueCard.Add(BuildWhereUsedSection(DialogueWhereUsedUtility.GetWhereUsed(_database, npc, dialogue), true));
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

            if (_selectedNode is DialogueChoiceNodeData choiceNode)
            {
                _detailsTitleLabel.text = DialogueEditorLocalization.Text("Answer Details");
                BuildChoiceNodeInspector(choiceNode);
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
                row.AddToClassList("dialogue-editor__speaker-row");

                var nameField = new TextField(DialogueEditorLocalization.Text("Speaker Name"))
                {
                    value = speaker.Name,
                    isDelayed = true,
                    name = "dialogue-speaker-name-field"
                };
                nameField.AddToClassList("dialogue-editor__speaker-name-field");
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
                removeButton.AddToClassList("dialogue-editor__speaker-remove-button");
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

            var activeContentLanguage = DialogueContentLanguageSettings.CurrentLanguageCode;
            var bodyText = DialogueTextLocalizationUtility.GetBodyText(node, activeContentLanguage);
            var localizationKeyField = new TextField(DialogueEditorLocalization.Text("Localization Key"))
            {
                value = node.LocalizationKey,
                name = "node-localization-key-field"
            };
            localizationKeyField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Node Localization Key", () => node.LocalizationKey = evt.newValue, refreshNodeVisuals: true);
            });

            var bodyPreview = CreateRichTextPreview(bodyText);
            var bodyField = new TextField(DialogueEditorLocalization.Text("Body Text"))
            {
                value = bodyText,
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
                PerformNodeScopedChange("Edit Node Body", () =>
                    DialogueTextLocalizationUtility.SetBodyText(node, activeContentLanguage, evt.newValue), refreshNodeVisuals: true);
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
            startToggle.AddToClassList("dialogue-editor__node-toggle");
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

            BuildAnswersSection(node);
            BuildChoiceFlowDiagnostics(node);
            BuildConditionEditor(node.Condition, DialogueEditorLocalization.Text("Condition"), "Edit Node Condition");
            BuildLinksInspector(node);

            _inspectorView.Add(localizationKeyField);

            var deleteButton = new Button(() => _graphView.DeleteNode(node)) { text = DialogueEditorLocalization.Text("Delete Node") };
            deleteButton.AddToClassList("dialogue-editor__danger-button");
            _inspectorView.Add(deleteButton);
        }

        private void BuildAnswersSection(DialogueTextNodeData node)
        {
            _inspectorView.Add(CreateSectionTitle(DialogueEditorLocalization.Text("Answers")));

            var box = new VisualElement();
            box.AddToClassList("dialogue-editor__links-list");
            box.name = "node-answers-list";

            var addButton = new Button(() => _graphView?.CreateChoiceBranch(node))
            {
                text = DialogueEditorLocalization.Text("Add Choice"),
                name = "node-add-choice-button"
            };
            addButton.AddToClassList("dialogue-editor__action-button");
            box.Add(addButton);

            var links = DialogueGraphUtility.GetChoiceCandidateLinks(_selectedDialogue?.Graph, node);
            if (links.Count == 0)
            {
                box.Add(CreateInlineHelp(DialogueEditorLocalization.Text("Add a choice to create an answer button with its own reply text.")));
                _inspectorView.Add(box);
                return;
            }

            foreach (var link in links)
            {
                var target = DialogueGraphUtility.GetNode(_selectedDialogue.Graph, link.ToNodeId);
                var choiceNode = target as DialogueChoiceNodeData;
                var answerCard = new VisualElement();
                answerCard.AddToClassList("dialogue-editor__link-card");
                answerCard.name = choiceNode == null ? "legacy-answer-card" : "answer-card";

                var answerText = choiceNode == null
                    ? GetLegacyChoiceLabel(link, target)
                    : string.IsNullOrWhiteSpace(choiceNode.ChoiceText)
                        ? DialogueEditorLocalization.Text("Untitled answer")
                        : choiceNode.ChoiceText;
                var answerLabel = new Label(DialogueEditorLocalization.Format("Answer: {0}", answerText));
                answerLabel.AddToClassList("dialogue-editor__link-label");
                answerCard.Add(answerLabel);

                var destination = choiceNode == null ? target : GetFirstOutgoingTarget(choiceNode);
                var targetLabel = new Label(choiceNode == null
                    ? DialogueEditorLocalization.Format("Target: {0}", GetNodeDisplayName(destination))
                    : DialogueEditorLocalization.Format("Next: {0}", destination == null ? DialogueEditorLocalization.Text("End") : GetNodeDisplayName(destination)));
                targetLabel.AddToClassList("dialogue-editor__link-target");
                answerCard.Add(targetLabel);

                if (choiceNode != null)
                {
                    var editButton = new Button(() => FocusDialogueNode(_selectedDialogue, choiceNode.Id))
                    {
                        text = DialogueEditorLocalization.Text("Edit Answer"),
                        name = "answer-edit-button"
                    };
                    answerCard.Add(editButton);
                }
                else
                {
                    answerCard.Add(CreateInlineHelp(DialogueEditorLocalization.Text("Legacy answer link. Create new answers with Add Choice.")));
                }

                box.Add(answerCard);
            }

            _inspectorView.Add(box);
        }

        private void BuildTextNodeSpeakerField(BaseNodeData node)
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

                PerformNodeScopedChange("Edit Node Speaker", () => SetNodeSpeakerId(node, speakers[index].Id), refreshNodeVisuals: true);
                DialoguePreviewWindow.RefreshOpenWindows(this);
            });
            _inspectorView.Add(speakerField);
        }

        private void BuildChoiceNodeInspector(DialogueChoiceNodeData node)
        {
            _inspectorView.Add(CreateSectionTitle(DialogueEditorLocalization.Text("Answer")));

            var titleField = new TextField(DialogueEditorLocalization.Text("Title")) { value = node.Title, name = "choice-title-field" };
            titleField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Answer Title", () => node.Title = evt.newValue, refreshNodeVisuals: true);
            });
            _inspectorView.Add(titleField);

            _inspectorView.Add(BuildNodeIdEditor(node));
            _inspectorView.Add(BuildWhereUsedSection(DialogueWhereUsedUtility.GetWhereUsed(_database, _selectedNpc, _selectedDialogue, node)));

            var answerField = new TextField(DialogueEditorLocalization.Text("Button Text"))
            {
                value = node.ChoiceText,
                name = "choice-button-text-field"
            };
            answerField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Button Text", () => node.ChoiceText = evt.newValue, refreshNodeVisuals: true);
                DialoguePreviewWindow.RefreshOpenWindows(this);
            });
            _inspectorView.Add(answerField);

            BuildTextNodeSpeakerField(node);

            var activeContentLanguage = DialogueContentLanguageSettings.CurrentLanguageCode;
            var bodyText = DialogueTextLocalizationUtility.GetBodyText(node, activeContentLanguage);
            var bodyPreview = CreateRichTextPreview(bodyText);
            var bodyField = new TextField(DialogueEditorLocalization.Text("Body Text"))
            {
                value = bodyText,
                multiline = true,
                name = "choice-body-field"
            };
            var bodySelectionState = new RichTextSelectionState();
            RegisterRichTextSelectionTracking(bodyField, bodySelectionState);
            _inspectorView.Add(BuildRichTextToolbar(bodyField, bodyPreview, node, bodySelectionState));

            bodyField.AddToClassList("dialogue-editor__multiline-field");
            bodyField.AddToClassList("dialogue-editor__body-field");
            bodyField.style.whiteSpace = WhiteSpace.Normal;
            bodyField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Answer Body", () =>
                    DialogueTextLocalizationUtility.SetBodyText(node, activeContentLanguage, evt.newValue), refreshNodeVisuals: true);
                bodySelectionState.Capture(bodyField);
                UpdateRichTextPreview(bodyPreview, evt.newValue);
                DialoguePreviewWindow.RefreshOpenWindows(this);
            });
            _inspectorView.Add(bodyField);
            _inspectorView.Add(bodyPreview);

            var voiceKeyField = new TextField(DialogueEditorLocalization.Text("Voice Key"))
            {
                value = node.VoiceKey,
                name = "choice-voice-key-field"
            };
            voiceKeyField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Answer Voice Key", () => node.VoiceKey = evt.newValue);
            });
            _inspectorView.Add(voiceKeyField);

            var localizationKeyField = new TextField(DialogueEditorLocalization.Text("Localization Key"))
            {
                value = node.LocalizationKey,
                name = "choice-localization-key-field"
            };
            localizationKeyField.RegisterValueChangedCallback(evt =>
            {
                PerformNodeScopedChange("Edit Answer Localization Key", () => node.LocalizationKey = evt.newValue, refreshNodeVisuals: true);
            });
            _inspectorView.Add(localizationKeyField);

            BuildConditionEditor(node.Condition, DialogueEditorLocalization.Text("Condition"), "Edit Answer Condition");
            BuildLinksInspector(node);

            var deleteButton = new Button(() => _graphView.DeleteNode(node)) { text = DialogueEditorLocalization.Text("Delete Answer") };
            deleteButton.AddToClassList("dialogue-editor__danger-button");
            _inspectorView.Add(deleteButton);
        }

        private VisualElement BuildRichTextToolbar(
            TextField bodyField,
            VisualElement bodyPreview,
            BaseNodeData node,
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
                TryNormalizeEditorTextColorCode,
                normalized => DialogueRichTextFormat.TextColor(normalized),
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
            BaseNodeData node,
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
            BaseNodeData node,
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
            BaseNodeData node,
            RichTextSelectionState selectionState,
            VisualElement rows)
        {
            var row = new VisualElement();
            row.AddToClassList("dialogue-editor__rich-text-color-row");

            var controlRow = new VisualElement();
            controlRow.AddToClassList("dialogue-editor__rich-text-color-row-controls");
            row.Add(controlRow);

            VisualElement pickerPanel = null;
            VisualElement pickerHandle = null;
            VisualElement brightnessTrack = null;
            var suppressFieldChange = false;

            Button swatchButton = null;
            swatchButton = CreateRichTextButton(string.Empty, "Select Color", () =>
                SelectRichTextColorIcon(rows, swatchButton), bodyField, selectionState);
            swatchButton.name = $"{fieldNamePrefix}-swatch-{index}";
            swatchButton.AddToClassList("dialogue-editor__rich-text-color-icon");
            controlRow.Add(swatchButton);

            var field = new TextField
            {
                name = $"{fieldNamePrefix}-{index}",
                value = list.Colors[index],
                maxLength = maxLength
            };
            field.AddToClassList("dialogue-editor__rich-text-color-field");
            controlRow.Add(field);

            void UpdatePickerFromNormalized(string normalized)
            {
                if (pickerHandle == null && brightnessTrack == null)
                {
                    return;
                }

                if (!TryParseRichTextColor(normalized, out var color))
                {
                    return;
                }

                Color.RGBToHSV(color, out var hue, out var saturation, out var value);
                if (brightnessTrack?.userData is RichTextColorPickerState pickerState)
                {
                    pickerState.Hue = hue;
                    pickerState.Saturation = saturation;
                    pickerState.Value = value;
                    UpdateRichTextBrightnessTrack(brightnessTrack, hue, saturation);
                    UpdateRichTextBrightnessHandle(brightnessTrack, value);
                }

                if (pickerHandle != null)
                {
                    UpdateRichTextColorPickerHandle(pickerHandle, hue, saturation);
                }
            }

            void PersistNormalizedColor(string normalized)
            {
                if (index < 0 || index >= list.Colors.Count)
                {
                    return;
                }

                if (!string.Equals(field.value, normalized, StringComparison.Ordinal))
                {
                    suppressFieldChange = true;
                    field.SetValueWithoutNotify(normalized);
                    suppressFieldChange = false;
                }

                list.Colors[index] = normalized;
                SaveRichTextColorList(prefsKey, list);
                SetRichTextSwatchColor(swatchButton, normalized);
                ClearCustomColorError(field);
                UpdatePickerFromNormalized(normalized);
            }

            void SetFieldFromPicker(string normalized)
            {
                suppressFieldChange = true;
                field.value = normalized;
                suppressFieldChange = false;
                PersistNormalizedColor(normalized);
            }

            string GetPickerBaseColor()
            {
                if (tryNormalize(field.value, out var normalized))
                {
                    return normalized;
                }

                if (tryNormalize(list.Colors[index], out normalized))
                {
                    return normalized;
                }

                return "#FFFFFF";
            }

            void TogglePicker()
            {
                if (pickerPanel != null)
                {
                    pickerPanel.RemoveFromHierarchy();
                    pickerPanel = null;
                    pickerHandle = null;
                    brightnessTrack = null;
                    return;
                }

                pickerPanel = CreateRichTextColorPicker(
                    index,
                    fieldNamePrefix,
                    GetPickerBaseColor(),
                    normalized => SetFieldFromPicker(normalized),
                    out pickerHandle,
                    out brightnessTrack);
                AttachRichTextSelectionCapture(pickerPanel, bodyField, selectionState);
                row.Add(pickerPanel);
            }

            field.RegisterValueChangedCallback(evt =>
            {
                if (suppressFieldChange)
                {
                    return;
                }

                var value = evt.newValue ?? string.Empty;
                if (string.IsNullOrWhiteSpace(value))
                {
                    list.Colors[index] = string.Empty;
                    SaveRichTextColorList(prefsKey, list);
                    SetRichTextSwatchColor(swatchButton, string.Empty);
                    ClearCustomColorError(field);
                    return;
                }

                if (!tryNormalize(value, out var normalized))
                {
                    ShowCustomColorError(field);
                    return;
                }

                PersistNormalizedColor(normalized);
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

            var pickerButton = CreateRichTextButton("...", "Open Color Palette", TogglePicker, bodyField, selectionState);
            pickerButton.name = $"{fieldNamePrefix}-picker-toggle-{index}";
            pickerButton.AddToClassList("dialogue-editor__rich-text-picker-button");
            controlRow.Add(pickerButton);

            var applyButton = CreateRichTextButton(DialogueEditorLocalization.Text("Apply"), "Apply", () =>
                ApplyCustomRichTextColor(field, bodyField, bodyPreview, node, selectionState, tryNormalize, createFormat, actionName), bodyField, selectionState);
            applyButton.name = $"{applyButtonNamePrefix}-{index}";
            applyButton.AddToClassList("dialogue-editor__rich-text-apply-button");
            controlRow.Add(applyButton);

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
                controlRow.Add(removeButton);
            }

            if (tryNormalize(list.Colors[index], out var existingNormalized))
            {
                suppressFieldChange = true;
                field.value = existingNormalized;
                suppressFieldChange = false;
                PersistNormalizedColor(existingNormalized);
            }
            else
            {
                SetRichTextSwatchColor(swatchButton, string.Empty);
            }

            AddRemoveButton();
            return row;
        }

        private static VisualElement CreateRichTextColorPicker(
            int index,
            string fieldNamePrefix,
            string normalized,
            Action<string> setColor,
            out VisualElement pickerHandle,
            out VisualElement brightnessTrack)
        {
            var color = TryParseRichTextColor(normalized, out var parsedColor)
                ? parsedColor
                : Color.white;
            Color.RGBToHSV(color, out var hue, out var saturation, out var value);
            var pickerState = new RichTextColorPickerState
            {
                Hue = hue,
                Saturation = saturation,
                Value = value
            };

            var panel = new VisualElement
            {
                name = $"{fieldNamePrefix}-picker-{index}"
            };
            panel.AddToClassList("dialogue-editor__rich-text-picker");

            var wheel = new VisualElement
            {
                name = $"{fieldNamePrefix}-picker-wheel-{index}"
            };
            wheel.AddToClassList("dialogue-editor__rich-text-picker-wheel");
            wheel.style.backgroundImage = new StyleBackground(GetRichTextColorWheelTexture());

            var handle = new VisualElement
            {
                name = $"{fieldNamePrefix}-picker-handle-{index}"
            };
            handle.AddToClassList("dialogue-editor__rich-text-picker-handle");
            wheel.Add(handle);
            panel.Add(wheel);

            var track = new VisualElement
            {
                name = $"{fieldNamePrefix}-picker-brightness-{index}"
            };
            track.AddToClassList("dialogue-editor__rich-text-picker-brightness");
            track.AddToClassList("dialogue-editor__rich-text-picker-brightness-track");
            pickerState.BrightnessTexture = CreateRichTextBrightnessTexture(hue, saturation);
            track.style.backgroundImage = new StyleBackground(pickerState.BrightnessTexture);

            var brightnessHandle = new VisualElement
            {
                name = $"{fieldNamePrefix}-picker-brightness-handle-{index}"
            };
            brightnessHandle.AddToClassList("dialogue-editor__rich-text-picker-brightness-handle");
            track.Add(brightnessHandle);
            panel.Add(track);

            void ApplyCurrentColor()
            {
                var next = RichTextHsvToHex(pickerState.Hue, pickerState.Saturation, pickerState.Value);
                setColor(next);
                UpdateRichTextColorPickerHandle(handle, pickerState.Hue, pickerState.Saturation);
                UpdateRichTextBrightnessTrack(track, pickerState.Hue, pickerState.Saturation);
                UpdateRichTextBrightnessHandle(track, pickerState.Value);
            }

            void PickWheelColor(Vector2 localPosition)
            {
                var center = new Vector2(RichTextColorWheelSize / 2f, RichTextColorWheelSize / 2f);
                var offset = localPosition - center;
                var distance = offset.magnitude;
                var radius = RichTextColorWheelSize / 2f;
                if (distance > radius)
                {
                    return;
                }

                pickerState.Hue = Mathf.Repeat(Mathf.Atan2(-offset.y, offset.x) / (Mathf.PI * 2f), 1f);
                pickerState.Saturation = Mathf.Clamp01(distance / radius);
                ApplyCurrentColor();
            }

            void PickBrightness(Vector2 localPosition)
            {
                pickerState.Value = Mathf.Clamp01(localPosition.x / RichTextBrightnessStripWidth);
                ApplyCurrentColor();
            }

            wheel.RegisterCallback<PointerDownEvent>(evt =>
            {
                PickWheelColor(evt.localPosition);
                evt.StopPropagation();
            });
            wheel.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if ((evt.pressedButtons & 1) == 0)
                {
                    return;
                }

                PickWheelColor(evt.localPosition);
                evt.StopPropagation();
            });
            track.RegisterCallback<PointerDownEvent>(evt =>
            {
                PickBrightness(evt.localPosition);
                evt.StopPropagation();
            });
            track.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if ((evt.pressedButtons & 1) == 0)
                {
                    return;
                }

                PickBrightness(evt.localPosition);
                evt.StopPropagation();
            });
            pickerState.SetBrightnessForTests = normalizedValue =>
            {
                pickerState.Value = Mathf.Clamp01(normalizedValue);
                ApplyCurrentColor();
            };
            track.userData = pickerState;

            UpdateRichTextColorPickerHandle(handle, pickerState.Hue, pickerState.Saturation);
            UpdateRichTextBrightnessHandle(track, pickerState.Value);
            pickerHandle = handle;
            brightnessTrack = track;
            return panel;
        }

        private static Texture2D GetRichTextColorWheelTexture()
        {
            if (_richTextColorWheelTexture != null)
            {
                return _richTextColorWheelTexture;
            }

            var texture = new Texture2D(RichTextColorWheelTextureSize, RichTextColorWheelTextureSize, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var center = new Vector2((RichTextColorWheelTextureSize - 1) / 2f, (RichTextColorWheelTextureSize - 1) / 2f);
            var radius = (RichTextColorWheelTextureSize - 1) / 2f;
            const float edgeFeather = 3f;
            for (var y = 0; y < RichTextColorWheelTextureSize; y++)
            {
                for (var x = 0; x < RichTextColorWheelTextureSize; x++)
                {
                    var offset = new Vector2(x, y) - center;
                    var distance = offset.magnitude;
                    var alpha = Mathf.Clamp01(radius - distance + edgeFeather);
                    if (alpha <= 0f)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    var saturation = Mathf.Clamp01(distance / radius);
                    var hue = Mathf.Repeat(Mathf.Atan2(offset.y, offset.x) / (Mathf.PI * 2f), 1f);
                    var color = Color.HSVToRGB(hue, saturation, 1f);
                    color.a = alpha;
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            _richTextColorWheelTexture = texture;
            return _richTextColorWheelTexture;
        }

        private static Texture2D CreateRichTextBrightnessTexture(float hue, float saturation)
        {
            var texture = new Texture2D(RichTextBrightnessStripWidth, RichTextBrightnessStripHeight, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            UpdateRichTextBrightnessTexture(texture, hue, saturation);
            return texture;
        }

        private static void UpdateRichTextBrightnessTrack(VisualElement brightnessTrack, float hue, float saturation)
        {
            if (brightnessTrack?.userData is not RichTextColorPickerState pickerState ||
                pickerState.BrightnessTexture == null)
            {
                return;
            }

            UpdateRichTextBrightnessTexture(pickerState.BrightnessTexture, hue, saturation);
        }

        private static void UpdateRichTextBrightnessTexture(Texture2D texture, float hue, float saturation)
        {
            for (var x = 0; x < RichTextBrightnessStripWidth; x++)
            {
                var value = RichTextBrightnessStripWidth <= 1
                    ? 1f
                    : x / (RichTextBrightnessStripWidth - 1f);
                var color = Color.HSVToRGB(hue, saturation, value);
                for (var y = 0; y < RichTextBrightnessStripHeight; y++)
                {
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
        }

        private static void UpdateRichTextColorPickerHandle(VisualElement pickerHandle, float hue, float saturation)
        {
            if (pickerHandle == null)
            {
                return;
            }

            var angle = hue * Mathf.PI * 2f;
            var radius = saturation * RichTextColorWheelSize / 2f;
            var center = RichTextColorWheelSize / 2f;
            pickerHandle.style.left = center + Mathf.Cos(angle) * radius - 4f;
            pickerHandle.style.top = center - Mathf.Sin(angle) * radius - 4f;
        }

        private static void UpdateRichTextBrightnessHandle(VisualElement brightnessTrack, float value)
        {
            var brightnessHandle = brightnessTrack?.Q<VisualElement>(className: "dialogue-editor__rich-text-picker-brightness-handle");
            if (brightnessHandle == null)
            {
                return;
            }

            brightnessHandle.style.left = Mathf.Clamp01(value) * RichTextBrightnessStripWidth - 4f;
        }

        private static bool TryParseRichTextColor(string normalized, out Color color)
        {
            color = Color.white;
            return !string.IsNullOrWhiteSpace(normalized) && ColorUtility.TryParseHtmlString(normalized, out color);
        }

        private static string RichTextHsvToHex(float hue, float saturation, float value)
        {
            var color = Color.HSVToRGB(hue, saturation, value);
            var color32 = (Color32)color;
            return $"#{color32.r:X2}{color32.g:X2}{color32.b:X2}";
        }

        private static void SetRichTextSwatchColor(Button swatchButton, string normalized)
        {
            if (swatchButton == null)
            {
                return;
            }

            swatchButton.style.backgroundColor = TryParseRichTextColor(normalized, out var color)
                ? color
                : Color.clear;
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
            BaseNodeData node,
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

        private static bool TryNormalizeEditorTextColorCode(string color, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(color))
            {
                return false;
            }

            var value = color.Trim();
            if (!value.StartsWith("#", StringComparison.Ordinal))
            {
                value = $"#{value}";
            }

            return DialogueRichTextUtility.TryNormalizeTextColorCode(value, out normalized);
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
            BaseNodeData node,
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
            BaseNodeData node,
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
            BaseNodeData node,
            RichTextSelectionState selectionState,
            string updatedText,
            int cursorIndex,
            string actionName)
        {
            var activeContentLanguage = DialogueContentLanguageSettings.CurrentLanguageCode;
            PerformNodeScopedChange(actionName, () =>
                DialogueTextLocalizationUtility.SetBodyText(node, activeContentLanguage, updatedText), refreshNodeVisuals: true);
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

            var deleteButton = new Button(() => _graphView.DeleteCommentOnly(node))
            {
                text = DialogueEditorLocalization.Text("Delete Node"),
                name = "comment-delete-node-button"
            };
            deleteButton.AddToClassList("dialogue-editor__danger-button");
            _inspectorView.Add(deleteButton);

            var deleteGroupButton = new Button(() => _graphView.DeleteCommentGroup(node))
            {
                text = DialogueEditorLocalization.Text("Delete Comment With Contents"),
                name = "comment-delete-group-button"
            };
            deleteGroupButton.AddToClassList("dialogue-editor__danger-button");
            _inspectorView.Add(deleteGroupButton);
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
            BuildLinksInspector(node);
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
            BuildLinksInspector(node);
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
            BuildLinksInspector(node);
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

        private BaseNodeData GetFirstOutgoingTarget(BaseNodeData node)
        {
            var link = DialogueGraphUtility.GetOutgoingLinks(_selectedDialogue?.Graph, node?.Id).FirstOrDefault();
            return link == null ? null : DialogueGraphUtility.GetNode(_selectedDialogue.Graph, link.ToNodeId);
        }

        private string GetLegacyChoiceLabel(NodeLinkData link, BaseNodeData target)
        {
            if (!string.IsNullOrWhiteSpace(link?.ChoiceText))
            {
                return link.ChoiceText;
            }

            return string.IsNullOrWhiteSpace(target?.Title)
                ? DialogueEditorLocalization.Text("Choice")
                : target.Title;
        }

        private string GetNodeDisplayName(BaseNodeData node)
        {
            if (node == null)
            {
                return DialogueEditorLocalization.Text("Missing target");
            }

            return string.IsNullOrWhiteSpace(node.Title)
                ? DialogueEditorLocalization.Text("Untitled")
                : node.Title;
        }

        private void BuildLinksInspector(BaseNodeData node)
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

                box.Add(CreateInlineHelp(DialogueEditorLocalization.Text("This link uses Order for traversal.")));

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

        private VisualElement BuildNpcIdEditor(NpcEntry npc, bool compact = false)
        {
            return BuildIdEditor(
                DialogueEditorLocalization.Text("NPC Id"),
                "npc-id-field",
                npc?.Id,
                DialogueIdentifierUtility.GetIssues(_database, npc),
                () => ChangeNpcId(npc, GuidUtility.NewGuid()),
                () => ChangeNpcId(npc, GuidUtility.NewGuid()),
                newValue => ChangeNpcId(npc, newValue),
                compact);
        }

        private VisualElement BuildDialogueIdEditor(DialogueEntry dialogue, bool compact = false)
        {
            return BuildIdEditor(
                DialogueEditorLocalization.Text("Dialogue Id"),
                "dialogue-id-field",
                dialogue?.Id,
                DialogueIdentifierUtility.GetIssues(_database, dialogue),
                () => ChangeDialogueId(dialogue, GuidUtility.NewGuid()),
                () => ChangeDialogueId(dialogue, GuidUtility.NewGuid()),
                newValue => ChangeDialogueId(dialogue, newValue),
                compact);
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
            Action<string> commit,
            bool compact = false)
        {
            var box = new Box();
            box.AddToClassList("dialogue-editor__id-card");
            box.EnableInClassList("dialogue-editor__id-card--compact", compact);

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

        private VisualElement BuildWhereUsedSection(IReadOnlyList<DialogueWhereUsedResult> results, bool compact = false)
        {
            var box = new Box();
            box.AddToClassList("dialogue-editor__where-used-card");
            box.EnableInClassList("dialogue-editor__where-used-card--compact", compact);

            var foldout = new Foldout
            {
                text = DialogueEditorLocalization.Text("Where Used"),
                value = false,
                name = "where-used-foldout"
            };
            foldout.AddToClassList("dialogue-editor__where-used-foldout");
            box.Add(foldout);

            var internalResults = results?
                .Where(result => result.Kind == DialogueReferenceKind.Internal)
                .ToList() ?? new List<DialogueWhereUsedResult>();
            var externalResults = results?
                .Where(result => result.Kind == DialogueReferenceKind.External)
                .ToList() ?? new List<DialogueWhereUsedResult>();

            AddWhereUsedGroup(foldout, DialogueEditorLocalization.Text("Internal References"), internalResults);
            AddWhereUsedGroup(foldout, DialogueEditorLocalization.Text("External References"), externalResults, DialogueEditorLocalization.Text("No external references reported."));
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

                foreach (var choiceNode in dialogue.Graph?.Nodes?.OfType<DialogueChoiceNodeData>() ?? Enumerable.Empty<DialogueChoiceNodeData>())
                {
                    if (choiceNode.SpeakerId == speakerId)
                    {
                        choiceNode.SpeakerId = string.Empty;
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

        private static void SetNodeSpeakerId(BaseNodeData node, string speakerId)
        {
            switch (node)
            {
                case DialogueTextNodeData textNode:
                    textNode.SpeakerId = speakerId ?? string.Empty;
                    break;
                case DialogueChoiceNodeData choiceNode:
                    choiceNode.SpeakerId = speakerId ?? string.Empty;
                    break;
            }
        }

        [Serializable]
        private sealed class RichTextColorList
        {
            public List<string> Colors = new();
        }

        internal sealed class RichTextColorPickerState
        {
            public float Hue;
            public float Saturation;
            public float Value;
            public Texture2D BrightnessTexture;
            public Action<float> SetBrightnessForTests;
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

        private void OpenLocalizationWindow()
        {
            DialogueStartWindow.OpenLocalization(this, _database, _selectedNpc, _selectedDialogue);
        }

        public void RefreshAfterLocalizationImport(NpcEntry npc, DialogueEntry dialogue)
        {
            _selectedNpc = npc;
            _selectedDialogue = dialogue;
            _selectedNode = null;
            MarkChanged();
            BuildLayout();
            RefreshAll();
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
            SetDetailsCollapsed(false);
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

            foreach (var choiceNode in _selectedDialogue.Graph.Nodes.OfType<DialogueChoiceNodeData>())
            {
                DialogueGraphUtility.NormalizeLinkOrder(_selectedDialogue.Graph, choiceNode.Id);
            }

            MarkChanged();
            RefreshInspector();
            DialoguePreviewWindow.RefreshOpenWindows(this);
        }

        private void OnNodeSelectionChanged(BaseNodeData node)
        {
            _selectedNode = node;
        }

        private void OnNodeInspectorRequested(BaseNodeData node)
        {
            _selectedNode = node;
            SetDetailsCollapsed(false);
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
                case DialoguePaletteItemType.Choice:
                    _graphView?.CreateChoiceNode(_graphView.GetCanvasCenter());
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

        private void CreateNodeFromPaletteShortcut(DialoguePaletteItemType itemType)
        {
            EnsureDialogueSelected();
            if (_selectedDialogue == null)
            {
                return;
            }

            _graphView?.CreateNodeFromPaletteShortcut(itemType);
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

        private void BeginPaletteShortcutRebind(DialoguePaletteItemType itemType)
        {
            CancelPendingPaletteClicks();
            _activePaletteShortcutRebind = itemType;
            RefreshPaletteShortcutBadges();
        }

        private void CancelPaletteShortcutRebind()
        {
            _activePaletteShortcutRebind = null;
            RefreshPaletteShortcutBadges();
        }

        private void ClearPaletteShortcut(DialoguePaletteItemType itemType)
        {
            DialoguePaletteShortcutSettings.ClearShortcut(itemType);
            _activePaletteShortcutRebind = null;
            RefreshPaletteShortcutBadges();
        }

        private void SetPaletteShortcut(DialoguePaletteItemType itemType, DialoguePaletteShortcut shortcut)
        {
            if (!DialoguePaletteShortcutSettings.IsBindable(shortcut))
            {
                return;
            }

            DialoguePaletteShortcutSettings.SetShortcut(itemType, shortcut);
            _activePaletteShortcutRebind = null;
            RefreshPaletteShortcutBadges();
        }

        private bool HandlePaletteShortcutRebindKey(DialoguePaletteItemType itemType, KeyDownEvent evt)
        {
            if (_activePaletteShortcutRebind != itemType)
            {
                return false;
            }

            if (evt.keyCode == KeyCode.Escape)
            {
                CancelPaletteShortcutRebind();
                return true;
            }

            if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
            {
                ClearPaletteShortcut(itemType);
                return true;
            }

            var shortcut = DialoguePaletteShortcut.FromEvent(evt);
            if (!DialoguePaletteShortcutSettings.IsBindable(shortcut))
            {
                return true;
            }

            SetPaletteShortcut(itemType, shortcut);
            return true;
        }

        private void RefreshPaletteShortcutBadges()
        {
            foreach (var pair in _paletteItems)
            {
                var listening = _activePaletteShortcutRebind == pair.Key;
                var shortcut = DialoguePaletteShortcutSettings.GetShortcut(pair.Key);
                pair.Value.SetShortcutText(DialoguePaletteShortcutSettings.FormatShortcut(shortcut), listening);
            }
        }

        private void CancelPendingPaletteClicks()
        {
            foreach (var item in _paletteItems.Values)
            {
                item.CancelPendingClick();
            }
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
            private const long SingleClickDelayMs = 300;

            private readonly DialogueEditorWindow _owner;
            private readonly DialoguePaletteItemType _itemType;
            private readonly Label _shortcutLabel;
            private bool _pressed;
            private bool _placementStarted;
            private Vector2 _pressWorldPosition;
            private IVisualElementScheduledItem _pendingClick;

            public PaletteItem(DialogueEditorWindow owner, DialoguePaletteItemType itemType, string title, string hint)
            {
                _owner = owner;
                _itemType = itemType;
                focusable = true;
                tooltip = $"{hint}\n{DialogueEditorLocalization.Text("Double-click to rebind shortcut")}";
                name = $"palette-item-{GetItemName(itemType)}";

                AddToClassList("dialogue-editor__palette-item");

                _shortcutLabel = new Label();
                _shortcutLabel.AddToClassList("dialogue-editor__palette-shortcut");
                _shortcutLabel.name = $"palette-shortcut-{GetItemName(itemType)}";
                Add(_shortcutLabel);
                SetShortcutText(DialoguePaletteShortcutSettings.FormatShortcut(DialoguePaletteShortcutSettings.GetShortcut(itemType)), false);

                var titleLabel = new Label(title);
                titleLabel.AddToClassList("dialogue-editor__palette-item-title");
                Add(titleLabel);

                var spacer = new VisualElement();
                spacer.AddToClassList("dialogue-editor__palette-shortcut-spacer");
                Add(spacer);

                RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
                RegisterCallback<MouseMoveEvent>(OnMouseMove, TrickleDown.TrickleDown);
                RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
                RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
                RegisterCallback<MouseCaptureOutEvent>(_ => ResetDragState());
            }

            public void SetShortcutText(string shortcutText, bool listening)
            {
                _shortcutLabel.text = listening
                    ? DialogueEditorLocalization.Text("Press")
                    : shortcutText;
                _shortcutLabel.tooltip = listening
                    ? DialogueEditorLocalization.Text("Esc to cancel, Delete to clear")
                    : DialogueEditorLocalization.Text("Double-click to rebind shortcut");
                EnableInClassList("is-rebinding", listening);
                if (listening)
                {
                    Focus();
                }
            }

            private void OnMouseDown(MouseDownEvent evt)
            {
                if (evt.button != 0)
                {
                    return;
                }

                if (evt.clickCount >= 2)
                {
                    CancelPendingClick();
                    ResetDragState();
                    _owner.BeginPaletteShortcutRebind(_itemType);
                    Focus();
                    evt.StopImmediatePropagation();
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
                else if (evt.clickCount <= 1)
                {
                    ScheduleSingleClick();
                }
                else
                {
                    CancelPendingClick();
                    _owner.BeginPaletteShortcutRebind(_itemType);
                    Focus();
                }

                ResetDragState();
                evt.StopImmediatePropagation();
            }

            private void OnKeyDown(KeyDownEvent evt)
            {
                if (_owner.HandlePaletteShortcutRebindKey(_itemType, evt))
                {
                    evt.StopImmediatePropagation();
                }
            }

            private void ScheduleSingleClick()
            {
                CancelPendingClick();
                _pendingClick = schedule.Execute(() =>
                {
                    _pendingClick = null;
                    if (_owner.IsPaletteShortcutRebinding)
                    {
                        return;
                    }

                    _owner.CreateNodeFromPalette(_itemType);
                });
                _pendingClick.ExecuteLater(SingleClickDelayMs);
            }

            public void CancelPendingClick()
            {
                _pendingClick?.Pause();
                _pendingClick = null;
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

            private static string GetItemName(DialoguePaletteItemType itemType)
            {
                return itemType switch
                {
                    DialoguePaletteItemType.TextNode => "textnode",
                    DialoguePaletteItemType.Choice => "choice",
                    DialoguePaletteItemType.Comment => "comment",
                    DialoguePaletteItemType.Function => "function",
                    DialoguePaletteItemType.Scene => "scene",
                    DialoguePaletteItemType.Debug => "debug",
                    _ => "unknown"
                };
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

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor
{
    public class DialoguePreviewWindow : EditorWindow
    {
        private const string EditorStyleSheetSearchQuery = "DialogueEditorStyles t:StyleSheet";

        private DialogueEntry _dialogue;
        private DialogueEditorWindow _ownerWindow;
        private DialoguePreviewSession _session;
        private bool _stylesApplied;

        private Label _titleLabel;
        private Label _statusLabel;
        private Label _historyHintLabel;
        private Label _currentNodeLabel;
        private VisualElement _bodyLabel;
        private Label _choicesTitleLabel;
        private Label _sceneEmptyLabel;
        private Label _reasonLabel;
        private ScrollView _transcriptScrollView;
        private ScrollView _sceneScrollView;
        private VisualElement _transcriptContainer;
        private VisualElement _sceneCard;
        private VisualElement _sceneContent;
        private VisualElement _choicesContainer;
        private VisualElement _variablesContainer;
        private Button _nextButton;
        private Button _backButton;
        private Button _restartButton;
        private Button _jumpButton;
        private readonly List<DialoguePreviewVariable> _testVariables = new();

        public static void ShowWindow(DialogueEntry dialogue, DialogueEditorWindow owner)
        {
            var window = GetWindow<DialoguePreviewWindow>(DialogueEditorLocalization.Text("Dialogue Preview"));
            var preferredSize = new Vector2(760f, 780f);
            window.minSize = new Vector2(640f, 700f);
            if (window.position.width < preferredSize.x || window.position.height < preferredSize.y)
            {
                window.position = new Rect(window.position.position, preferredSize);
            }

            window.SetDialogue(dialogue, owner);
        }

        internal static void RefreshOpenWindows(DialogueEditorWindow owner)
        {
            if (owner == null)
            {
                return;
            }

            foreach (var window in Resources.FindObjectsOfTypeAll<DialoguePreviewWindow>())
            {
                window.RefreshDialogueReference(owner);
            }
        }

        private void OnEnable()
        {
            DialogueEditorLanguageSettings.LanguageChanged += OnEditorLanguageChanged;
        }

        private void CreateGUI()
        {
            ApplyStyles();
            BuildLayout();
            RefreshView();
        }

        private void OnDisable()
        {
            DialogueEditorLanguageSettings.LanguageChanged -= OnEditorLanguageChanged;
        }

        private void OnEditorLanguageChanged()
        {
            BuildLayout();
            RefreshView();
        }

        public void SetDialogue(DialogueEntry dialogue, DialogueEditorWindow owner)
        {
            _dialogue = dialogue;
            _ownerWindow = owner;
            _session = dialogue == null ? null : new DialoguePreviewSession(dialogue, BuildVariableMap());
            RefreshView();
        }

        private void RefreshDialogueReference(DialogueEditorWindow owner)
        {
            if (_ownerWindow != owner)
            {
                return;
            }

            if (_dialogue == null)
            {
                RefreshView();
                return;
            }

            if (owner.TryResolveDialogueById(_dialogue.Id, out _, out var resolvedDialogue))
            {
                SetDialogue(resolvedDialogue, owner);
                return;
            }

            SetDialogue(null, owner);
        }

        private void BuildLayout()
        {
            rootVisualElement.Clear();
            titleContent = new GUIContent(DialogueEditorLocalization.Text("Dialogue Preview"));
            rootVisualElement.AddToClassList("dialogue-preview");

            var shell = new VisualElement();
            shell.AddToClassList("dialogue-preview__shell");
            rootVisualElement.Add(shell);

            var header = new VisualElement();
            header.AddToClassList("dialogue-preview__header");
            shell.Add(header);

            var titleBlock = new VisualElement();
            titleBlock.AddToClassList("dialogue-preview__title-block");
            header.Add(titleBlock);

            _titleLabel = new Label(DialogueEditorLocalization.Text("No dialogue selected"));
            _titleLabel.AddToClassList("dialogue-preview__title");
            titleBlock.Add(_titleLabel);

            _statusLabel = new Label(DialogueEditorLocalization.Text("Preview uses the current dialogue asset state."));
            _statusLabel.AddToClassList("dialogue-preview__status");
            titleBlock.Add(_statusLabel);

            shell.Add(BuildVariablesPanel());

            var historyPanel = new VisualElement();
            historyPanel.AddToClassList("dialogue-preview__panel");
            historyPanel.AddToClassList("dialogue-preview__history-panel");
            shell.Add(historyPanel);

            var historyHeader = new VisualElement();
            historyHeader.AddToClassList("dialogue-preview__panel-header");
            historyPanel.Add(historyHeader);

            var historyTitle = new Label(DialogueEditorLocalization.Text("History"));
            historyTitle.AddToClassList("dialogue-preview__panel-title");
            historyHeader.Add(historyTitle);

            _historyHintLabel = new Label(DialogueEditorLocalization.Text("The full run is recorded here."));
            _historyHintLabel.AddToClassList("dialogue-preview__panel-hint");
            historyHeader.Add(_historyHintLabel);

            _transcriptScrollView = new ScrollView();
            _transcriptScrollView.AddToClassList("dialogue-preview__history-scroll");
            _transcriptScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _transcriptContainer = new VisualElement();
            _transcriptContainer.AddToClassList("dialogue-preview__history-list");
            _transcriptScrollView.Add(_transcriptContainer);
            historyPanel.Add(_transcriptScrollView);

            _sceneCard = new VisualElement();
            _sceneCard.AddToClassList("dialogue-preview__scene-card");
            shell.Add(_sceneCard);

            _sceneScrollView = new ScrollView();
            _sceneScrollView.AddToClassList("dialogue-preview__scene-scroll");
            _sceneScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _sceneCard.Add(_sceneScrollView);

            _sceneContent = new VisualElement();
            _sceneContent.AddToClassList("dialogue-preview__scene-content");
            _sceneScrollView.Add(_sceneContent);

            var sceneHeader = new VisualElement();
            sceneHeader.AddToClassList("dialogue-preview__scene-header");
            _sceneContent.Add(sceneHeader);

            var sceneLabel = new Label(DialogueEditorLocalization.Text("Current Scene"));
            sceneLabel.AddToClassList("dialogue-preview__section-kicker");
            sceneHeader.Add(sceneLabel);

            _currentNodeLabel = new Label();
            _currentNodeLabel.AddToClassList("dialogue-preview__scene-title");
            _sceneContent.Add(_currentNodeLabel);

            _bodyLabel = DialogueRichTextRenderer.Create("dialogue-preview-scene-body", "dialogue-preview__scene-body");
            _sceneContent.Add(_bodyLabel);

            _sceneEmptyLabel = new Label();
            _sceneEmptyLabel.AddToClassList("dialogue-preview__scene-empty");
            _sceneContent.Add(_sceneEmptyLabel);

            _reasonLabel = new Label();
            _reasonLabel.AddToClassList("dialogue-preview__reason");
            _sceneContent.Add(_reasonLabel);

            var choicesSection = new VisualElement();
            choicesSection.AddToClassList("dialogue-preview__choices-section");
            _sceneContent.Add(choicesSection);

            _choicesTitleLabel = new Label(DialogueEditorLocalization.Text("Choices"));
            _choicesTitleLabel.AddToClassList("dialogue-preview__choices-title");
            choicesSection.Add(_choicesTitleLabel);

            _choicesContainer = new VisualElement();
            _choicesContainer.AddToClassList("dialogue-preview__choices-list");
            choicesSection.Add(_choicesContainer);

            var controls = new VisualElement();
            controls.AddToClassList("dialogue-preview__controls");
            shell.Add(controls);

            var navGroup = new VisualElement();
            navGroup.AddToClassList("dialogue-preview__controls-group");
            controls.Add(navGroup);

            var spacer = new VisualElement();
            spacer.AddToClassList("dialogue-preview__controls-spacer");
            controls.Add(spacer);

            var utilityGroup = new VisualElement();
            utilityGroup.AddToClassList("dialogue-preview__controls-group");
            utilityGroup.AddToClassList("dialogue-preview__controls-group--utility");
            controls.Add(utilityGroup);

            _backButton = new Button(() =>
            {
                _session?.Back();
                RefreshView();
            })
            {
                text = DialogueEditorLocalization.Text("Back")
            };
            _backButton.AddToClassList("dialogue-preview__button");
            navGroup.Add(_backButton);

            _nextButton = new Button(() =>
            {
                _session?.Advance();
                RefreshView();
            })
            {
                text = DialogueEditorLocalization.Text("Next")
            };
            _nextButton.AddToClassList("dialogue-preview__button");
            _nextButton.AddToClassList("dialogue-preview__button--primary");
            navGroup.Add(_nextButton);

            _restartButton = new Button(() =>
            {
                _session?.Restart();
                RefreshView();
            })
            {
                text = DialogueEditorLocalization.Text("Restart")
            };
            _restartButton.AddToClassList("dialogue-preview__button");
            utilityGroup.Add(_restartButton);

            _jumpButton = new Button(JumpToActiveNode)
            {
                text = DialogueEditorLocalization.Text("Jump To Active Node")
            };
            _jumpButton.AddToClassList("dialogue-preview__button");
            _jumpButton.AddToClassList("dialogue-preview__button--utility");
            utilityGroup.Add(_jumpButton);
        }

        private VisualElement BuildVariablesPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("dialogue-preview__panel");
            panel.AddToClassList("dialogue-preview__variables-panel");

            var header = new VisualElement();
            header.AddToClassList("dialogue-preview__panel-header");
            panel.Add(header);

            var title = new Label(DialogueEditorLocalization.Text("Test Variables"));
            title.AddToClassList("dialogue-preview__panel-title");
            header.Add(title);

            var actions = new VisualElement();
            actions.AddToClassList("dialogue-preview__variables-actions");
            header.Add(actions);

            var addButton = new Button(AddTestVariable) { text = DialogueEditorLocalization.Text("Add") };
            addButton.AddToClassList("dialogue-preview__mini-button");
            actions.Add(addButton);

            var resetButton = new Button(ResetTestVariables) { text = DialogueEditorLocalization.Text("Reset") };
            resetButton.AddToClassList("dialogue-preview__mini-button");
            actions.Add(resetButton);

            _variablesContainer = new VisualElement();
            _variablesContainer.AddToClassList("dialogue-preview__variables-list");
            panel.Add(_variablesContainer);
            return panel;
        }

        private void RefreshView()
        {
            if (_titleLabel == null)
            {
                return;
            }

            if (_session == null || _dialogue == null)
            {
                RebuildVariables();
                _titleLabel.text = DialogueEditorLocalization.Text("No dialogue selected");
                _statusLabel.text = DialogueEditorLocalization.Text("Open a dialogue in the editor to preview it.");
                _historyHintLabel.text = DialogueEditorLocalization.Text("The full run will appear here after a dialogue starts.");
                _currentNodeLabel.text = DialogueEditorLocalization.Text("No active scene");
                DialogueRichTextRenderer.SetText(_bodyLabel, string.Empty);
                _sceneEmptyLabel.text = DialogueEditorLocalization.Text("Select a dialogue in the editor to begin previewing it here.");
                _sceneEmptyLabel.style.display = DisplayStyle.Flex;
                _reasonLabel.style.display = DisplayStyle.None;
                _bodyLabel.style.display = DisplayStyle.None;
                _choicesTitleLabel.style.display = DisplayStyle.None;
                _choicesContainer.style.display = DisplayStyle.None;
                _transcriptContainer?.Clear();
                _choicesContainer?.Clear();
                _nextButton?.SetEnabled(false);
                _backButton?.SetEnabled(false);
                _restartButton?.SetEnabled(false);
                _jumpButton?.SetEnabled(false);
                return;
            }

            RebuildVariables();
            _titleLabel.text = _dialogue.Name;
            _restartButton.SetEnabled(true);
            _backButton.SetEnabled(_session.CanGoBack);
            _nextButton.SetEnabled(_session.CanAdvance);
            _jumpButton.SetEnabled(_ownerWindow != null && _session.CurrentLineNode != null);

            if (_session.CurrentLineNode == null)
            {
                _statusLabel.text = _session.IsEnded
                    ? DialogueEditorLocalization.Text("Run complete. History remains above, and Back can rewind the latest action.")
                    : DialogueEditorLocalization.Text("Unable to start this dialogue with the current test variables.");
                _historyHintLabel.text = DialogueEditorLocalization.Text("Scroll through the full run history above.");
                _currentNodeLabel.text = _session.IsEnded ? DialogueEditorLocalization.Text("Dialogue Complete") : DialogueEditorLocalization.Text("No active scene");
                DialogueRichTextRenderer.SetText(_bodyLabel, string.Empty);
                _bodyLabel.style.display = DisplayStyle.None;
                _sceneEmptyLabel.text = _session.IsEnded
                    ? DialogueEditorLocalization.Text("This preview reached its end. Use Back to revisit the previous step or Restart to run it again.")
                    : DialogueEditorLocalization.Text("No valid start node could be activated for the current dialogue.");
                _sceneEmptyLabel.style.display = DisplayStyle.Flex;
                _reasonLabel.text = _session.CurrentReason;
                _reasonLabel.style.display = string.IsNullOrWhiteSpace(_session.CurrentReason)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
                _choicesTitleLabel.style.display = DisplayStyle.None;
                _choicesContainer.style.display = DisplayStyle.None;
            }
            else
            {
                _statusLabel.text = _session.CurrentLineNode is DialogueTextNodeData textNode &&
                                    DialogueGraphUtility.UsesChoices(_dialogue?.Graph, textNode)
                    ? DialogueEditorLocalization.Text("Choice mode: pick a response below while the upper rail keeps the full conversation history.")
                    : DialogueEditorLocalization.Text("Linear mode: use Back and Next to move through the current run.");
                _historyHintLabel.text = DialogueEditorLocalization.Text("Newest entries collect at the bottom, like a running case log.");
                _currentNodeLabel.text = string.IsNullOrWhiteSpace(_session.CurrentSpeakerName)
                    ? string.IsNullOrWhiteSpace(_session.CurrentLineNode.Title)
                        ? DialogueEditorLocalization.Text("Untitled Node")
                        : _session.CurrentLineNode.Title
                    : _session.CurrentSpeakerName;
                DialogueRichTextRenderer.SetText(
                    _bodyLabel,
                    DialogueTextLocalizationUtility.GetBodyText(_session.CurrentLineNode, DialogueContentLanguageSettings.CurrentLanguageCode),
                    DialogueEditorLocalization.Text("This node has no dialogue text yet."));
                _bodyLabel.style.display = DisplayStyle.Flex;
                _sceneEmptyLabel.style.display = DisplayStyle.None;
                _reasonLabel.text = _session.CurrentReason;
                _reasonLabel.style.display = string.IsNullOrWhiteSpace(_session.CurrentReason)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
                var hasChoiceRows = _session.CanChoose || _session.BlockedChoices.Count > 0;
                _choicesTitleLabel.style.display = hasChoiceRows ? DisplayStyle.Flex : DisplayStyle.None;
                _choicesContainer.style.display = hasChoiceRows ? DisplayStyle.Flex : DisplayStyle.None;
            }

            RebuildTranscript();
            RebuildChoices();
        }

        private void RebuildTranscript()
        {
            _transcriptContainer.Clear();

            foreach (var entry in _session.Transcript)
            {
                var card = new VisualElement();
                card.AddToClassList("dialogue-preview__transcript-entry");
                card.AddToClassList(entry.Kind == DialoguePreviewTranscriptEntryKind.Choice
                    ? "dialogue-preview__transcript-entry--choice"
                    : "dialogue-preview__transcript-entry--node");

                var title = new Label(entry.Title);
                title.AddToClassList("dialogue-preview__transcript-title");
                card.Add(title);

                var divider = new VisualElement();
                divider.AddToClassList("dialogue-preview__transcript-divider");
                card.Add(divider);

                if (entry.Kind == DialoguePreviewTranscriptEntryKind.Choice &&
                    !string.IsNullOrWhiteSpace(entry.ChoiceText))
                {
                    var choiceRow = new VisualElement();
                    choiceRow.AddToClassList("dialogue-preview__transcript-choice-row");

                    var choiceChip = new Label(entry.ChoiceText);
                    choiceChip.AddToClassList("dialogue-preview__transcript-choice-chip");
                    choiceRow.Add(choiceChip);

                    var colon = new Label(":");
                    colon.AddToClassList("dialogue-preview__transcript-choice-colon");
                    choiceRow.Add(colon);

                    card.Add(choiceRow);
                }

                var body = DialogueRichTextRenderer.Create(string.Empty, "dialogue-preview__transcript-body");
                DialogueRichTextRenderer.SetText(body, entry.Body);
                card.Add(body);

                _transcriptContainer.Add(card);
            }

            if (_transcriptContainer.childCount > 0)
            {
                _transcriptScrollView.ScrollTo(_transcriptContainer[_transcriptContainer.childCount - 1]);
            }
        }

        private void RebuildChoices()
        {
            _choicesContainer.Clear();

            if (!_session.CanChoose && _session.BlockedChoices.Count == 0)
            {
                return;
            }

            for (var index = 0; index < _session.CurrentChoices.Count; index++)
            {
                var choiceIndex = index;
                var choice = _session.CurrentChoices[index];
                var choiceButton = new Button(() =>
                {
                    _session.Choose(choiceIndex);
                    RefreshView();
                })
                {
                    text = choice.Text
                };

                choiceButton.AddToClassList("dialogue-preview__choice-button");
                _choicesContainer.Add(choiceButton);

                var explanation = _session.GetChoiceExplanation(choice);
                if (!string.IsNullOrWhiteSpace(explanation))
                {
                    var explanationLabel = new Label(explanation);
                    explanationLabel.AddToClassList("dialogue-preview__choice-reason");
                    _choicesContainer.Add(explanationLabel);
                }
            }

            foreach (var blockedChoice in _session.BlockedChoices)
            {
                var blocked = new Label($"{blockedChoice.Label}: {blockedChoice.Reason}");
                blocked.AddToClassList("dialogue-preview__blocked-choice");
                _choicesContainer.Add(blocked);
            }
        }

        private void RebuildVariables()
        {
            if (_variablesContainer == null)
            {
                return;
            }

            _variablesContainer.Clear();
            if (_testVariables.Count == 0)
            {
                var empty = new Label(DialogueEditorLocalization.Text("No test variables."));
                empty.AddToClassList("dialogue-preview__variables-empty");
                _variablesContainer.Add(empty);
                return;
            }

            for (var index = 0; index < _testVariables.Count; index++)
            {
                var variable = _testVariables[index];
                var variableIndex = index;
                var row = new VisualElement();
                row.AddToClassList("dialogue-preview__variable-row");

                var keyField = new TextField(DialogueEditorLocalization.Text("Key")) { value = variable.Key };
                keyField.AddToClassList("dialogue-preview__variable-key");
                keyField.RegisterValueChangedCallback(evt =>
                {
                    variable.Key = evt.newValue;
                    RestartPreviewWithVariables();
                });
                row.Add(keyField);

                var typeField = new EnumField(DialogueEditorLocalization.Text("Type"), variable.Type);
                typeField.AddToClassList("dialogue-preview__variable-type");
                typeField.RegisterValueChangedCallback(evt =>
                {
                    variable.Type = (DialoguePreviewVariableType)evt.newValue;
                    if (variable.Type == DialoguePreviewVariableType.Bool &&
                        !bool.TryParse(variable.Value, out _))
                    {
                        variable.Value = "false";
                    }

                    RestartPreviewWithVariables();
                });
                row.Add(typeField);

                if (variable.Type == DialoguePreviewVariableType.Bool)
                {
                    var boolField = new Toggle(DialogueEditorLocalization.Text("Value"))
                    {
                        value = bool.TryParse(variable.Value, out var boolValue) && boolValue
                    };
                    boolField.RegisterValueChangedCallback(evt =>
                    {
                        variable.Value = evt.newValue ? "true" : "false";
                        RestartPreviewWithVariables();
                    });
                    row.Add(boolField);
                }
                else
                {
                    var valueField = new TextField(DialogueEditorLocalization.Text("Value")) { value = variable.Value };
                    valueField.AddToClassList("dialogue-preview__variable-value");
                    valueField.RegisterValueChangedCallback(evt =>
                    {
                        variable.Value = evt.newValue;
                        RestartPreviewWithVariables();
                    });
                    row.Add(valueField);
                }

                var removeButton = new Button(() =>
                {
                    _testVariables.RemoveAt(variableIndex);
                    RestartPreviewWithVariables();
                })
                {
                    text = DialogueEditorLocalization.Text("Remove")
                };
                removeButton.AddToClassList("dialogue-preview__mini-button");
                row.Add(removeButton);

                _variablesContainer.Add(row);
            }
        }

        private void AddTestVariable()
        {
            _testVariables.Add(new DialoguePreviewVariable
            {
                Key = $"variable_{_testVariables.Count + 1}",
                Type = DialoguePreviewVariableType.String,
                Value = string.Empty
            });
            RestartPreviewWithVariables();
        }

        private void ResetTestVariables()
        {
            _testVariables.Clear();
            RestartPreviewWithVariables();
        }

        private void RestartPreviewWithVariables()
        {
            _session = _dialogue == null ? null : new DialoguePreviewSession(_dialogue, BuildVariableMap());
            RefreshView();
        }

        private Dictionary<string, string> BuildVariableMap()
        {
            var map = new Dictionary<string, string>();
            foreach (var variable in _testVariables)
            {
                if (!string.IsNullOrWhiteSpace(variable.Key))
                {
                    map[variable.Key] = variable.Value ?? string.Empty;
                }
            }

            return map;
        }

        private void JumpToActiveNode()
        {
            if (_ownerWindow == null || _session?.CurrentNodeId == null)
            {
                return;
            }

            _ownerWindow.FocusDialogueNode(_dialogue, _session.CurrentNodeId);
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
    }

    internal enum DialoguePreviewVariableType
    {
        Bool,
        Number,
        String
    }

    internal sealed class DialoguePreviewVariable
    {
        public string Key;
        public DialoguePreviewVariableType Type;
        public string Value;
    }
}

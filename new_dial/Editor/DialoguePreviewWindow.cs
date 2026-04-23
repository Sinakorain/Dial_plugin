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
        private Label _bodyLabel;
        private Label _choicesTitleLabel;
        private Label _sceneEmptyLabel;
        private ScrollView _transcriptScrollView;
        private ScrollView _sceneScrollView;
        private VisualElement _transcriptContainer;
        private VisualElement _sceneCard;
        private VisualElement _sceneContent;
        private VisualElement _choicesContainer;
        private Button _nextButton;
        private Button _backButton;
        private Button _restartButton;
        private Button _jumpButton;

        public static void ShowWindow(DialogueEntry dialogue, DialogueEditorWindow owner)
        {
            var window = GetWindow<DialoguePreviewWindow>("Dialogue Preview");
            var preferredSize = new Vector2(760f, 780f);
            window.minSize = new Vector2(640f, 700f);
            if (window.position.width < preferredSize.x || window.position.height < preferredSize.y)
            {
                window.position = new Rect(window.position.position, preferredSize);
            }

            window.SetDialogue(dialogue, owner);
        }

        private void CreateGUI()
        {
            ApplyStyles();
            BuildLayout();
            RefreshView();
        }

        public void SetDialogue(DialogueEntry dialogue, DialogueEditorWindow owner)
        {
            _dialogue = dialogue;
            _ownerWindow = owner;
            _session = dialogue == null ? null : new DialoguePreviewSession(dialogue);
            RefreshView();
        }

        private void BuildLayout()
        {
            rootVisualElement.Clear();
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

            _titleLabel = new Label("No dialogue selected");
            _titleLabel.AddToClassList("dialogue-preview__title");
            titleBlock.Add(_titleLabel);

            _statusLabel = new Label("Preview uses the current dialogue asset state.");
            _statusLabel.AddToClassList("dialogue-preview__status");
            titleBlock.Add(_statusLabel);

            var historyPanel = new VisualElement();
            historyPanel.AddToClassList("dialogue-preview__panel");
            historyPanel.AddToClassList("dialogue-preview__history-panel");
            shell.Add(historyPanel);

            var historyHeader = new VisualElement();
            historyHeader.AddToClassList("dialogue-preview__panel-header");
            historyPanel.Add(historyHeader);

            var historyTitle = new Label("History");
            historyTitle.AddToClassList("dialogue-preview__panel-title");
            historyHeader.Add(historyTitle);

            _historyHintLabel = new Label("The full run is recorded here.");
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

            var sceneLabel = new Label("Current Scene");
            sceneLabel.AddToClassList("dialogue-preview__section-kicker");
            sceneHeader.Add(sceneLabel);

            _currentNodeLabel = new Label();
            _currentNodeLabel.AddToClassList("dialogue-preview__scene-title");
            _sceneContent.Add(_currentNodeLabel);

            _bodyLabel = new Label();
            _bodyLabel.AddToClassList("dialogue-preview__scene-body");
            _sceneContent.Add(_bodyLabel);

            _sceneEmptyLabel = new Label();
            _sceneEmptyLabel.AddToClassList("dialogue-preview__scene-empty");
            _sceneContent.Add(_sceneEmptyLabel);

            var choicesSection = new VisualElement();
            choicesSection.AddToClassList("dialogue-preview__choices-section");
            _sceneContent.Add(choicesSection);

            _choicesTitleLabel = new Label("Choices");
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
                text = "Back"
            };
            _backButton.AddToClassList("dialogue-preview__button");
            navGroup.Add(_backButton);

            _nextButton = new Button(() =>
            {
                _session?.Advance();
                RefreshView();
            })
            {
                text = "Next"
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
                text = "Restart"
            };
            _restartButton.AddToClassList("dialogue-preview__button");
            utilityGroup.Add(_restartButton);

            _jumpButton = new Button(JumpToActiveNode)
            {
                text = "Jump To Active Node"
            };
            _jumpButton.AddToClassList("dialogue-preview__button");
            _jumpButton.AddToClassList("dialogue-preview__button--utility");
            utilityGroup.Add(_jumpButton);
        }

        private void RefreshView()
        {
            if (_titleLabel == null)
            {
                return;
            }

            if (_session == null || _dialogue == null)
            {
                _titleLabel.text = "No dialogue selected";
                _statusLabel.text = "Open a dialogue in the editor to preview it.";
                _historyHintLabel.text = "The full run will appear here after a dialogue starts.";
                _currentNodeLabel.text = "No active scene";
                _bodyLabel.text = string.Empty;
                _sceneEmptyLabel.text = "Select a dialogue in the editor to begin previewing it here.";
                _sceneEmptyLabel.style.display = DisplayStyle.Flex;
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

            _titleLabel.text = _dialogue.Name;
            _restartButton.SetEnabled(true);
            _backButton.SetEnabled(_session.CanGoBack);
            _nextButton.SetEnabled(_session.CanAdvance);
            _jumpButton.SetEnabled(_ownerWindow != null && _session.CurrentNode != null);

            if (_session.CurrentNode == null)
            {
                _statusLabel.text = _session.IsEnded
                    ? "Run complete. History remains above, and Back can rewind the latest action."
                    : "Unable to start this dialogue with the current conditions.";
                _historyHintLabel.text = "Scroll through the full run history above.";
                _currentNodeLabel.text = _session.IsEnded ? "Dialogue Complete" : "No active scene";
                _bodyLabel.text = string.Empty;
                _bodyLabel.style.display = DisplayStyle.None;
                _sceneEmptyLabel.text = _session.IsEnded
                    ? "This preview reached its end. Use Back to revisit the previous step or Restart to run it again."
                    : "No valid start node could be activated for the current dialogue.";
                _sceneEmptyLabel.style.display = DisplayStyle.Flex;
                _choicesTitleLabel.style.display = DisplayStyle.None;
                _choicesContainer.style.display = DisplayStyle.None;
            }
            else
            {
                _statusLabel.text = _session.CurrentNode.UseOutputsAsChoices
                    ? "Choice mode: pick a response below while the upper rail keeps the full conversation history."
                    : "Linear mode: use Back and Next to move through the current run.";
                _historyHintLabel.text = "Newest entries collect at the bottom, like a running case log.";
                _currentNodeLabel.text = string.IsNullOrWhiteSpace(_session.CurrentNode.Title)
                    ? "Untitled Node"
                    : _session.CurrentNode.Title;
                _bodyLabel.text = string.IsNullOrWhiteSpace(_session.CurrentNode.BodyText)
                    ? "This node has no dialogue text yet."
                    : _session.CurrentNode.BodyText;
                _bodyLabel.style.display = DisplayStyle.Flex;
                _sceneEmptyLabel.style.display = DisplayStyle.None;
                _choicesTitleLabel.style.display = _session.CanChoose ? DisplayStyle.Flex : DisplayStyle.None;
                _choicesContainer.style.display = _session.CanChoose ? DisplayStyle.Flex : DisplayStyle.None;
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

                var body = new Label(entry.Body);
                body.AddToClassList("dialogue-preview__transcript-body");
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

            if (!_session.CanChoose)
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
            }
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
}

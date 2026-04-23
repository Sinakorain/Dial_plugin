using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor
{
    public class DialoguePreviewWindow : EditorWindow
    {
        private DialogueEntry _dialogue;
        private DialogueEditorWindow _ownerWindow;
        private DialoguePreviewSession _session;

        private Label _titleLabel;
        private Label _statusLabel;
        private Label _currentNodeLabel;
        private Label _bodyLabel;
        private ScrollView _transcriptScrollView;
        private VisualElement _transcriptContainer;
        private VisualElement _choicesContainer;
        private Button _nextButton;
        private Button _backButton;
        private Button _restartButton;
        private Button _jumpButton;

        public static void ShowWindow(DialogueEntry dialogue, DialogueEditorWindow owner)
        {
            var window = GetWindow<DialoguePreviewWindow>("Dialogue Preview");
            window.minSize = new Vector2(420f, 420f);
            window.SetDialogue(dialogue, owner);
        }

        private void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 12f;
            rootVisualElement.style.paddingRight = 12f;
            rootVisualElement.style.paddingTop = 12f;
            rootVisualElement.style.paddingBottom = 12f;
            rootVisualElement.style.backgroundColor = new Color(0.09f, 0.11f, 0.15f, 1f);

            _titleLabel = new Label("No dialogue selected");
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.fontSize = 16;
            _titleLabel.style.marginBottom = 6f;
            rootVisualElement.Add(_titleLabel);

            _statusLabel = new Label("Preview uses the current dialogue asset state.");
            _statusLabel.style.marginBottom = 10f;
            _statusLabel.style.color = new Color(0.76f, 0.81f, 0.88f, 1f);
            rootVisualElement.Add(_statusLabel);

            var transcriptTitle = new Label("Transcript");
            transcriptTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            transcriptTitle.style.marginBottom = 6f;
            rootVisualElement.Add(transcriptTitle);

            _transcriptScrollView = new ScrollView();
            _transcriptScrollView.style.height = 220f;
            _transcriptScrollView.style.marginBottom = 12f;
            _transcriptContainer = new VisualElement();
            _transcriptScrollView.Add(_transcriptContainer);
            rootVisualElement.Add(_transcriptScrollView);

            _currentNodeLabel = new Label();
            _currentNodeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _currentNodeLabel.style.marginBottom = 6f;
            rootVisualElement.Add(_currentNodeLabel);

            _bodyLabel = new Label();
            _bodyLabel.style.whiteSpace = WhiteSpace.Normal;
            _bodyLabel.style.marginBottom = 12f;
            rootVisualElement.Add(_bodyLabel);

            _choicesContainer = new ScrollView();
            _choicesContainer.style.flexGrow = 1f;
            _choicesContainer.style.marginBottom = 12f;
            rootVisualElement.Add(_choicesContainer);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.flexWrap = Wrap.Wrap;

            _nextButton = new Button(() =>
            {
                _session?.Advance();
                RefreshView();
            })
            {
                text = "Next"
            };
            _nextButton.style.marginRight = 8f;
            _nextButton.style.marginBottom = 8f;

            _backButton = new Button(() =>
            {
                _session?.Back();
                RefreshView();
            })
            {
                text = "Back"
            };
            _backButton.style.marginRight = 8f;
            _backButton.style.marginBottom = 8f;

            _restartButton = new Button(() =>
            {
                _session?.Restart();
                RefreshView();
            })
            {
                text = "Restart"
            };
            _restartButton.style.marginRight = 8f;
            _restartButton.style.marginBottom = 8f;

            _jumpButton = new Button(JumpToActiveNode)
            {
                text = "Jump To Active Node"
            };
            _jumpButton.style.marginBottom = 8f;

            buttons.Add(_nextButton);
            buttons.Add(_backButton);
            buttons.Add(_restartButton);
            buttons.Add(_jumpButton);
            rootVisualElement.Add(buttons);

            RefreshView();
        }

        public void SetDialogue(DialogueEntry dialogue, DialogueEditorWindow owner)
        {
            _dialogue = dialogue;
            _ownerWindow = owner;
            _session = dialogue == null ? null : new DialoguePreviewSession(dialogue);
            RefreshView();
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
                _currentNodeLabel.text = string.Empty;
                _bodyLabel.text = string.Empty;
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
                    ? "Dialogue ended. Use Back to revisit the previous step."
                    : "Unable to start this dialogue with the current conditions.";
                _currentNodeLabel.text = "No active node";
                _bodyLabel.text = string.Empty;
            }
            else
            {
                _statusLabel.text = _session.CurrentNode.UseOutputsAsChoices
                    ? "Choice mode: select an answer below. Transcript keeps the full run history."
                    : "Linear mode: use Next to continue, Back to rewind one action.";
                _currentNodeLabel.text = $"Active Node: {_session.CurrentNode.Title}";
                _bodyLabel.text = _session.CurrentNode.BodyText;
            }

            RebuildTranscript();
            RebuildChoices();
        }

        private void RebuildTranscript()
        {
            _transcriptContainer.Clear();

            foreach (var entry in _session.Transcript)
            {
                var box = new Box();
                box.style.marginBottom = 8f;
                box.style.paddingLeft = 10f;
                box.style.paddingRight = 10f;
                box.style.paddingTop = 8f;
                box.style.paddingBottom = 8f;
                box.style.backgroundColor = entry.Kind == DialoguePreviewTranscriptEntryKind.Choice
                    ? new Color(0.16f, 0.22f, 0.32f, 1f)
                    : new Color(0.18f, 0.19f, 0.24f, 1f);

                var title = new Label(entry.Title);
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.marginBottom = 4f;
                box.Add(title);

                var body = new Label(entry.Body);
                body.style.whiteSpace = WhiteSpace.Normal;
                box.Add(body);

                _transcriptContainer.Add(box);
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

                choiceButton.style.marginBottom = 6f;
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
    }
}

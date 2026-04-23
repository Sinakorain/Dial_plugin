using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor
{
    public class DialoguePreviewWindow : EditorWindow
    {
        private readonly DialoguePlayer _player = new();

        private DialogueEntry _dialogue;
        private Label _titleLabel;
        private Label _bodyLabel;
        private Label _statusLabel;
        private VisualElement _choicesContainer;
        private Button _nextButton;
        private Button _restartButton;

        public static void ShowWindow(DialogueEntry dialogue)
        {
            var window = GetWindow<DialoguePreviewWindow>("Dialogue Preview");
            window.minSize = new Vector2(360f, 300f);
            window.SetDialogue(dialogue);
        }

        private void OnEnable()
        {
            _player.NodeChanged += OnNodeChanged;
            _player.DialogueEnded += RefreshView;
        }

        private void OnDisable()
        {
            _player.NodeChanged -= OnNodeChanged;
            _player.DialogueEnded -= RefreshView;
        }

        private void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 12f;
            rootVisualElement.style.paddingRight = 12f;
            rootVisualElement.style.paddingTop = 12f;
            rootVisualElement.style.paddingBottom = 12f;

            _titleLabel = new Label("No dialogue selected");
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.fontSize = 16;
            _titleLabel.style.marginBottom = 8f;
            rootVisualElement.Add(_titleLabel);

            _statusLabel = new Label("Preview uses the current dialogue asset state.");
            _statusLabel.style.marginBottom = 8f;
            rootVisualElement.Add(_statusLabel);

            _bodyLabel = new Label();
            _bodyLabel.style.whiteSpace = WhiteSpace.Normal;
            _bodyLabel.style.flexGrow = 1f;
            _bodyLabel.style.marginBottom = 12f;
            rootVisualElement.Add(_bodyLabel);

            _choicesContainer = new ScrollView();
            _choicesContainer.style.flexGrow = 1f;
            _choicesContainer.style.marginBottom = 12f;
            rootVisualElement.Add(_choicesContainer);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;

            _nextButton = new Button(() =>
            {
                _player.Next();
                RefreshView();
            })
            {
                text = "Next"
            };
            _nextButton.style.marginRight = 8f;

            _restartButton = new Button(() =>
            {
                Restart();
            })
            {
                text = "Restart"
            };

            buttons.Add(_nextButton);
            buttons.Add(_restartButton);
            rootVisualElement.Add(buttons);

            RefreshView();
        }

        public void SetDialogue(DialogueEntry dialogue)
        {
            _dialogue = dialogue;
            Restart();
        }

        private void Restart()
        {
            if (_dialogue == null)
            {
                RefreshView();
                return;
            }

            _player.Start(_dialogue);
            RefreshView();
        }

        private void OnNodeChanged(DialogueTextNodeData _)
        {
            RefreshView();
        }

        private void RefreshView()
        {
            if (_titleLabel == null)
            {
                return;
            }

            if (_dialogue == null)
            {
                _titleLabel.text = "No dialogue selected";
                _statusLabel.text = "Open a dialogue in the editor to preview it.";
                _bodyLabel.text = string.Empty;
                _choicesContainer.Clear();
                _nextButton.SetEnabled(false);
                _restartButton.SetEnabled(false);
                return;
            }

            _restartButton.SetEnabled(true);
            _titleLabel.text = _dialogue.Name;

            if (_player.CurrentNode == null)
            {
                _statusLabel.text = "Dialogue ended.";
                _bodyLabel.text = string.Empty;
                _choicesContainer.Clear();
                _nextButton.SetEnabled(false);
                return;
            }

            _statusLabel.text = _player.CurrentNode.UseOutputsAsChoices
                ? "Choice mode: select one of the available outputs."
                : "Linear mode: use Next to follow the first valid output.";

            _bodyLabel.text = _player.CurrentNode.BodyText;
            _nextButton.SetEnabled(!_player.CurrentNode.UseOutputsAsChoices);

            _choicesContainer.Clear();
            if (_player.CurrentChoices.Count == 0)
            {
                return;
            }

            for (var index = 0; index < _player.CurrentChoices.Count; index++)
            {
                var choiceIndex = index;
                var choice = _player.CurrentChoices[index];
                var choiceButton = new Button(() =>
                {
                    _player.Choose(choiceIndex);
                    RefreshView();
                })
                {
                    text = choice.Text
                };

                choiceButton.style.marginBottom = 6f;
                _choicesContainer.Add(choiceButton);
            }
        }
    }
}

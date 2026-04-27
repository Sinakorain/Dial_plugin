// Copyright (c) 2026 Danil Kashulin. All rights reserved.

using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor
{
    public class DialogueExecutableNodeView : Node, IDialogueRuntimeNodeView
    {
        private const float LinkAnchorInset = 18f;
        private const float InlineTextMinHeight = 58f;

        private readonly DialogueGraphView _graphView;
        private readonly Label _kindBadge;
        private readonly Label _summaryLabel;
        private readonly Label _metaLabel;
        private readonly TextField _titleField;
        private readonly TextField _choiceTextField;
        private readonly TextField _bodyField;

        public DialogueExecutableNodeView(BaseNodeData data, DialogueGraphView graphView)
        {
            Data = data;
            _graphView = graphView;
            viewDataKey = data.Id;
            capabilities |= Capabilities.Deletable | Capabilities.Movable | Capabilities.Selectable;

            AddToClassList("dialogue-node");
            AddToClassList("dialogue-node--executable");
            AddToClassList(GetNodeClass(data));
            mainContainer.AddToClassList("dialogue-node__surface");
            topContainer.AddToClassList("dialogue-node__top-container");
            titleContainer.AddToClassList("dialogue-node__title-row");
            extensionContainer.AddToClassList("dialogue-node__content");
            inputContainer.style.display = DisplayStyle.None;
            outputContainer.style.display = DisplayStyle.None;

            _titleField = CreateHeaderTitleField("runtime-node-header-title-field");
            _titleField.RegisterValueChangedCallback(evt =>
            {
                _graphView.ApplyInlineNodeEdit($"Edit {GetKindLabel(Data)} Title", () => Data.Title = evt.newValue);
            });
            _titleField.RegisterCallback<FocusInEvent>(_ => _graphView.SelectRuntimeNode(this));
            titleContainer.Clear();
            titleContainer.Add(_titleField);
            titleContainer.Add(titleButtonContainer);

            _kindBadge = new Label(GetKindLabel(data));
            _kindBadge.AddToClassList("dialogue-node__start-badge");
            titleButtonContainer.Insert(0, _kindBadge);

            var deleteButton = new Button(() => _graphView.DeleteNode(Data))
            {
                text = "X"
            };
            deleteButton.AddToClassList("dialogue-node__delete-button");
            titleButtonContainer.Add(deleteButton);

            if (data is DialogueChoiceNodeData choiceNode)
            {
                _choiceTextField = CreateInlineTextField("answer-node-inline-button-text-field", true);
                _choiceTextField.AddToClassList("dialogue-node__inline-field--compact-multiline");
                _choiceTextField.RegisterValueChangedCallback(evt =>
                {
                    _graphView.ApplyInlineNodeEdit("Edit Button Text", () => choiceNode.ChoiceText = evt.newValue);
                });
                AddAnswerInlineSection(DialogueEditorLocalization.Text("Button Text"), _choiceTextField);

                _bodyField = CreateInlineTextField("answer-node-inline-body-field", true);
                _bodyField.RegisterValueChangedCallback(evt =>
                {
                    var activeContentLanguage = DialogueContentLanguageSettings.CurrentLanguageCode;
                    _graphView.ApplyInlineNodeEdit("Edit Answer Body", () =>
                        DialogueTextLocalizationUtility.SetBodyText(choiceNode, activeContentLanguage, evt.newValue));
                });
                AddAnswerInlineSection(DialogueEditorLocalization.Text("Text"), _bodyField);
            }
            else
            {
                _summaryLabel = new Label();
                _summaryLabel.AddToClassList("dialogue-node__body-preview");
                _summaryLabel.style.whiteSpace = WhiteSpace.Normal;
                extensionContainer.Add(_summaryLabel);
            }

            _metaLabel = new Label();
            _metaLabel.AddToClassList("dialogue-node__meta");
            extensionContainer.Add(_metaLabel);

            RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove, TrickleDown.TrickleDown);
            RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
            RegisterCallback<MouseCaptureOutEvent>(_ =>
            {
                _graphView.EndSelectionPointerDrag();
                _graphView.EndUndoGesture();
            });
            RefreshFromData();
        }

        public BaseNodeData Data { get; }

        public BaseNodeData NodeData => Data;

        public GraphElement Element => this;

        public Rect WorldBound => worldBound;

        public override void SetPosition(Rect newPos)
        {
            newPos = _graphView.AdjustSelectionPointerDragPosition(this, newPos);
            if (Data.Position != newPos.position)
            {
                _graphView.BeginUndoGesture("Move Node");
            }

            base.SetPosition(newPos);
            Data.Position = newPos.position;
            _graphView.NotifyNodeMoved();
        }

        public void RefreshFromData()
        {
            var outgoingCount = _graphView
                .GetOutgoingLinks(Data.Id)
                .Count(link => !string.IsNullOrWhiteSpace(link.ToNodeId));

            title = string.Empty;
            _titleField.SetValueWithoutNotify(Data.Title ?? string.Empty);
            _kindBadge.text = GetKindLabel(Data).ToUpperInvariant();
            if (Data is DialogueChoiceNodeData choiceNode)
            {
                _choiceTextField?.SetValueWithoutNotify(choiceNode.ChoiceText ?? string.Empty);
                if (_choiceTextField != null)
                {
                    _choiceTextField.style.height = EstimateTextFieldHeight(choiceNode.ChoiceText, 34f);
                }

                var bodyText = DialogueTextLocalizationUtility.GetBodyText(choiceNode, DialogueContentLanguageSettings.CurrentLanguageCode);
                if (_bodyField != null)
                {
                    _bodyField.SetValueWithoutNotify(bodyText);
                    _bodyField.style.height = EstimateTextFieldHeight(bodyText, InlineTextMinHeight);
                }

                var speakerName = _graphView.ResolveSpeakerName(Data);
                var linkSummary = outgoingCount == 0
                    ? DialogueEditorLocalization.Text("No next")
                    : DialogueEditorLocalization.Format("{0} link{1}", outgoingCount, outgoingCount == 1 ? string.Empty : "s");
                _metaLabel.text = string.IsNullOrWhiteSpace(speakerName)
                    ? linkSummary
                    : $"{speakerName} | {linkSummary}";
            }
            else
            {
                _summaryLabel.text = BuildSummary(Data);
                _metaLabel.text = outgoingCount == 0
                    ? DialogueEditorLocalization.Text("Ends here")
                    : DialogueEditorLocalization.Format("{0} exec link{1}", outgoingCount, outgoingCount == 1 ? string.Empty : "s");
            }

            var nodeHeight = Data is DialogueChoiceNodeData answerNode
                ? EstimateAnswerNodeHeight(
                    answerNode.ChoiceText,
                    DialogueTextLocalizationUtility.GetBodyText(answerNode, DialogueContentLanguageSettings.CurrentLanguageCode))
                : DialogueGraphView.TextNodeInitialSize.y;
            base.SetPosition(new Rect(
                Data.Position,
                new Vector2(DialogueGraphView.TextNodeInitialSize.x, nodeHeight)));
            RefreshExpandedState();
        }

        public bool IsPointerInTopHalf(Vector2 worldPointerPosition)
        {
            return ToLocalPoint(worldPointerPosition).y <= (layout.height * 0.5f);
        }

        public bool IsPointerInBottomHalf(Vector2 worldPointerPosition)
        {
            return ToLocalPoint(worldPointerPosition).y >= (layout.height * 0.5f);
        }

        public Vector2 GetBottomAnchorWorld(float worldX)
        {
            var x = Mathf.Clamp(worldX, worldBound.xMin + LinkAnchorInset, worldBound.xMax - LinkAnchorInset);
            return new Vector2(x, worldBound.yMax - 2f);
        }

        public Vector2 GetTopAnchorWorld(float worldX)
        {
            var x = Mathf.Clamp(worldX, worldBound.xMin + LinkAnchorInset, worldBound.xMax - LinkAnchorInset);
            return new Vector2(x, worldBound.yMin + 2f);
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            var worldPointerPosition = this.LocalToWorld(evt.localMousePosition);
            _graphView.SelectRuntimeNodeForPointerDrag(this);
            _graphView.RequestNodeInspector(Data);
            if (DialogueGraphView.IsInlineInteractiveTarget(evt.target as VisualElement))
            {
                return;
            }

            if (IsPointerInBottomHalf(worldPointerPosition))
            {
                _graphView.BeginLinkDrag(this, worldPointerPosition);
                evt.StopImmediatePropagation();
                return;
            }

            _graphView.BeginSelectionPointerDrag(worldPointerPosition);
            this.CaptureMouse();
            evt.StopImmediatePropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (!_graphView.IsSelectionPointerDragActive)
            {
                return;
            }

            _graphView.ContinueSelectionPointerDrag(this.LocalToWorld(evt.localMousePosition));
            evt.StopImmediatePropagation();
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            _graphView.EndSelectionPointerDrag();
            _graphView.EndUndoGesture();
            if (this.HasMouseCapture())
            {
                this.ReleaseMouse();
            }
        }

        private Vector2 ToLocalPoint(Vector2 worldPointerPosition)
        {
            return this.WorldToLocal(worldPointerPosition);
        }

        private static string BuildSummary(BaseNodeData data)
        {
            return data switch
            {
                DialogueChoiceNodeData choiceNode => string.IsNullOrWhiteSpace(choiceNode.ChoiceText)
                    ? DialogueEditorLocalization.Text("Button text is empty.")
                    : BuildPreviewText(choiceNode.ChoiceText),
                FunctionNodeData functionNode => string.IsNullOrWhiteSpace(functionNode.FunctionId)
                    ? DialogueEditorLocalization.Text("No function selected")
                    : DialogueEditorLocalization.Format(
                        "{0} ({1} arg{2})",
                        functionNode.FunctionId,
                        functionNode.Arguments?.Count ?? 0,
                        (functionNode.Arguments?.Count ?? 0) == 1 ? string.Empty : "s"),
                SceneNodeData sceneNode => string.IsNullOrWhiteSpace(sceneNode.SceneKey)
                    ? DialogueEditorLocalization.Text("No scene selected")
                    : $"{sceneNode.SceneKey} · {sceneNode.LoadMode}",
                DebugNodeData debugNode => string.IsNullOrWhiteSpace(debugNode.MessageTemplate)
                    ? DialogueEditorLocalization.Format("{0} debug log", debugNode.LogLevel)
                    : BuildPreviewText($"{debugNode.LogLevel}: {debugNode.MessageTemplate}"),
                _ => DialogueEditorLocalization.Text("Executable node")
            };
        }

        private static string BuildPreviewText(string text)
        {
            var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return normalized.Length <= 120 ? normalized : $"{normalized.Substring(0, 117)}...";
        }

        private TextField CreateInlineTextField(string fieldName, bool multiline)
        {
            var field = new TextField
            {
                name = fieldName,
                multiline = multiline,
                isDelayed = false
            };
            field.AddToClassList("dialogue-node__inline-field");
            if (multiline)
            {
                field.AddToClassList("dialogue-node__inline-field--multiline");
                ConfigureSoftWrappedInlineField(field);
            }

            field.RegisterCallback<FocusInEvent>(_ => _graphView.SelectRuntimeNode(this));
            return field;
        }

        private TextField CreateHeaderTitleField(string fieldName)
        {
            var field = new TextField
            {
                name = fieldName,
                isDelayed = false
            };
            field.AddToClassList("dialogue-node__header-title-field");
            return field;
        }

        private void AddAnswerInlineSection(string labelText, TextField field)
        {
            var label = new Label(labelText);
            label.AddToClassList("dialogue-node__inline-label");
            extensionContainer.Add(label);
            extensionContainer.Add(field);
        }

        private static float EstimateAnswerNodeHeight(string buttonText, string bodyText)
        {
            return Mathf.Max(
                DialogueGraphView.TextNodeInitialSize.y,
                128f + EstimateTextFieldHeight(buttonText, 34f) + EstimateTextFieldHeight(bodyText, InlineTextMinHeight));
        }

        private static float EstimateTextFieldHeight(string text, float minHeight)
        {
            var explicitLines = string.IsNullOrEmpty(text)
                ? 1
                : text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Sum(line =>
                    Mathf.Max(1, Mathf.CeilToInt(line.Length / 32f)));
            return Mathf.Max(minHeight, 28f + explicitLines * 18f);
        }

        private static void ConfigureSoftWrappedInlineField(TextField field)
        {
            field.style.whiteSpace = WhiteSpace.Normal;
            field.style.overflow = Overflow.Hidden;
            field.RegisterCallback<AttachToPanelEvent>(_ => ApplySoftWrapToTextInput(field));
            field.RegisterCallback<GeometryChangedEvent>(_ => ApplySoftWrapToTextInput(field));
        }

        private static void ApplySoftWrapToTextInput(TextField field)
        {
            field.Query<VisualElement>(className: "unity-text-input").ForEach(element =>
            {
                element.style.whiteSpace = WhiteSpace.Normal;
                element.style.overflow = Overflow.Hidden;
                element.style.minWidth = 0f;
                element.style.maxWidth = Length.Percent(100f);
            });

            field.Query<VisualElement>(className: "unity-base-text-field__input").ForEach(element =>
            {
                element.style.overflow = Overflow.Hidden;
                element.style.minWidth = 0f;
                element.style.maxWidth = Length.Percent(100f);
            });
        }

        private static string GetKindLabel(BaseNodeData data)
        {
            return data switch
            {
                DialogueChoiceNodeData => DialogueEditorLocalization.Text("Answer"),
                FunctionNodeData => DialogueEditorLocalization.Text("Function"),
                SceneNodeData => DialogueEditorLocalization.Text("Scene"),
                DebugNodeData => DialogueEditorLocalization.Text("Debug"),
                _ => DialogueEditorLocalization.Text("Exec")
            };
        }

        private static string GetNodeClass(BaseNodeData data)
        {
            return data switch
            {
                DialogueChoiceNodeData => "dialogue-node--choice",
                FunctionNodeData => "dialogue-node--function",
                SceneNodeData => "dialogue-node--scene",
                DebugNodeData => "dialogue-node--debug",
                _ => "dialogue-node--executable"
            };
        }
    }
}

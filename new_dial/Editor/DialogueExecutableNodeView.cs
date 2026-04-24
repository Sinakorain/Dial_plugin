using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor
{
    public class DialogueExecutableNodeView : Node, IDialogueRuntimeNodeView
    {
        private const float LinkAnchorInset = 18f;

        private readonly DialogueGraphView _graphView;
        private readonly Label _kindBadge;
        private readonly Label _summaryLabel;
        private readonly Label _metaLabel;

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

            _kindBadge = new Label(GetKindLabel(data));
            _kindBadge.AddToClassList("dialogue-node__start-badge");
            titleButtonContainer.Insert(0, _kindBadge);

            var deleteButton = new Button(() => _graphView.DeleteNode(Data))
            {
                text = "X"
            };
            deleteButton.AddToClassList("dialogue-node__delete-button");
            titleButtonContainer.Add(deleteButton);

            _summaryLabel = new Label();
            _summaryLabel.AddToClassList("dialogue-node__body-preview");
            _summaryLabel.style.whiteSpace = WhiteSpace.Normal;
            extensionContainer.Add(_summaryLabel);

            _metaLabel = new Label();
            _metaLabel.AddToClassList("dialogue-node__meta");
            extensionContainer.Add(_metaLabel);

            RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
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

            title = string.IsNullOrWhiteSpace(Data.Title) ? GetKindLabel(Data) : Data.Title;
            _kindBadge.text = GetKindLabel(Data).ToUpperInvariant();
            _summaryLabel.text = BuildSummary(Data);
            _metaLabel.text = outgoingCount == 0
                ? DialogueEditorLocalization.Text("Executes immediately, then ends unless connected.")
                : DialogueEditorLocalization.Format("{0} execution link{1}", outgoingCount, outgoingCount == 1 ? string.Empty : "s");

            base.SetPosition(new Rect(
                Data.Position,
                GetPosition().size == Vector2.zero ? DialogueGraphView.TextNodeInitialSize : GetPosition().size));
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
            if (evt.button != 0 || IsInsideButton(evt.target as VisualElement))
            {
                return;
            }

            var worldPointerPosition = this.LocalToWorld(evt.localMousePosition);
            _graphView.SelectRuntimeNode(this);

            if (IsPointerInBottomHalf(worldPointerPosition))
            {
                _graphView.BeginLinkDrag(this, worldPointerPosition);
                evt.StopImmediatePropagation();
                return;
            }

            _graphView.BeginSelectionPointerDrag();
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            _graphView.EndSelectionPointerDrag();
            _graphView.EndUndoGesture();
        }

        private Vector2 ToLocalPoint(Vector2 worldPointerPosition)
        {
            return this.WorldToLocal(worldPointerPosition);
        }

        private static string BuildSummary(BaseNodeData data)
        {
            return data switch
            {
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

        private static string GetKindLabel(BaseNodeData data)
        {
            return data switch
            {
                FunctionNodeData => DialogueEditorLocalization.Text("Function"),
                SceneNodeData => DialogueEditorLocalization.Text("Scene"),
                DebugNodeData => DialogueEditorLocalization.Text("Debug"),
                _ => DialogueEditorLocalization.Text("Exec")
            };
        }

        private static bool IsInsideButton(VisualElement element)
        {
            while (element != null)
            {
                if (element is Button)
                {
                    return true;
                }

                element = element.parent;
            }

            return false;
        }

        private static string GetNodeClass(BaseNodeData data)
        {
            return data switch
            {
                FunctionNodeData => "dialogue-node--function",
                SceneNodeData => "dialogue-node--scene",
                DebugNodeData => "dialogue-node--debug",
                _ => "dialogue-node--executable"
            };
        }
    }
}

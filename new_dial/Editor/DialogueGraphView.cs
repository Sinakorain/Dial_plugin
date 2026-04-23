using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor
{
    internal enum DialoguePaletteItemType
    {
        TextNode,
        Comment
    }

    public class DialogueGraphView : GraphView
    {
        private const float AnchorInset = 18f;
        private const float PlacementGhostWidth = 270f;
        private const float PlacementGhostHeight = 160f;

        private readonly Dictionary<string, DialogueTextNodeView> _textNodeViews = new();
        private readonly Dictionary<string, DialogueCommentNodeView> _commentNodeViews = new();
        private readonly GridBackground _gridBackground;
        private readonly DialogueEdgeLayer _edgeLayer;
        private readonly VisualElement _placementGhost;
        private readonly Label _placementGhostTitleLabel;
        private readonly Label _placementGhostHintLabel;
        private readonly Label _emptyStateLabel;

        private DialogueGraphData _graph;
        private bool _isReloading;
        private Vector2 _lastPointerPosition;
        private DialoguePaletteItemType? _placementNodeType;
        private DialogueTextNodeView _activeLinkSource;
        private DialogueTextNodeView _activeLinkTarget;
        private Vector2 _activeLinkStartWorld;
        private Vector2 _activeLinkPointerWorld;

        public DialogueGraphView()
        {
            AddToClassList("dialogue-graph-view");
            style.flexGrow = 1f;

            _gridBackground = new GridBackground();
            _gridBackground.AddToClassList("dialogue-graph-grid");
            Insert(0, _gridBackground);

            _edgeLayer = new DialogueEdgeLayer
            {
                pickingMode = PickingMode.Ignore,
                GeometryProvider = GetEdgeGeometries
            };
            _edgeLayer.AddToClassList("dialogue-edge-layer");
            _edgeLayer.StretchToParentSize();
            Insert(1, _edgeLayer);

            _placementGhost = new VisualElement();
            _placementGhost.AddToClassList("dialogue-placement-ghost");
            _placementGhost.pickingMode = PickingMode.Ignore;
            _placementGhost.style.display = DisplayStyle.None;
            _placementGhostTitleLabel = new Label();
            _placementGhostTitleLabel.AddToClassList("dialogue-placement-ghost__title");
            _placementGhostHintLabel = new Label("Release on the graph to create this node.");
            _placementGhostHintLabel.AddToClassList("dialogue-placement-ghost__hint");
            _placementGhost.Add(_placementGhostTitleLabel);
            _placementGhost.Add(_placementGhostHintLabel);
            Add(_placementGhost);

            _emptyStateLabel = new Label("Select a dialogue or create a new one to start building nodes.");
            _emptyStateLabel.AddToClassList("dialogue-graph-empty-state");
            _emptyStateLabel.pickingMode = PickingMode.Ignore;
            Add(_emptyStateLabel);

            this.StretchToParentSize();
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            graphViewChanged = OnGraphViewChanged;
            RegisterCallback<MouseMoveEvent>(OnMouseMove, TrickleDown.TrickleDown);
            RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
            RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
        }

        public Action<BaseNodeData> SelectionChangedAction { get; set; }
        public Action GraphChangedAction { get; set; }

        public DialogueGraphData Graph => _graph;

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
        }

        public void LoadGraph(DialogueGraphData graph)
        {
            _isReloading = true;
            try
            {
                _graph = graph;
                DeleteElements(graphElements.ToList());
                _textNodeViews.Clear();
                _commentNodeViews.Clear();
                CancelLinkDrag();
                CancelNodePlacement();

                if (_graph == null)
                {
                    UpdateEmptyState();
                    RefreshEdgeLayer();
                    return;
                }

                foreach (var node in _graph.Nodes)
                {
                    switch (node)
                    {
                        case DialogueTextNodeData textNode:
                            CreateTextNodeView(textNode);
                            break;
                        case CommentNodeData commentNode:
                            CreateCommentNodeView(commentNode);
                            break;
                    }
                }

                RefreshNodeVisuals();
                UpdateEmptyState();
            }
            finally
            {
                _isReloading = false;
            }
        }

        public void CreateTextNode(Vector2 position)
        {
            if (_graph == null)
            {
                return;
            }

            var node = new DialogueTextNodeData
            {
                Title = "Text Node",
                Position = position
            };

            _graph.Nodes.Add(node);
            var view = CreateTextNodeView(node);
            if (_graph.Nodes.OfType<DialogueTextNodeData>().Count() == 1)
            {
                DialogueGraphUtility.EnsureSingleStartNode(_graph, node.Id);
            }

            RefreshNodeVisuals();
            SelectNode(view, node);
            MarkChanged();
        }

        public void CreateCommentNode(Vector2 position)
        {
            if (_graph == null)
            {
                return;
            }

            var node = new CommentNodeData
            {
                Title = "Comment",
                Position = position,
                Area = new Rect(position.x, position.y, 320f, 180f),
                Comment = "Notes"
            };

            _graph.Nodes.Add(node);
            var view = CreateCommentNodeView(node);
            RefreshNodeVisuals();
            SelectNode(view, node);
            MarkChanged();
        }

        internal bool BeginNodePlacement(DialoguePaletteItemType itemType, Vector2 worldPointerPosition)
        {
            if (_graph == null)
            {
                return false;
            }

            _placementNodeType = itemType;
            _placementGhostTitleLabel.text = itemType == DialoguePaletteItemType.TextNode ? "Text Node" : "Comment";
            UpdateNodePlacement(worldPointerPosition);
            return true;
        }

        internal void UpdateNodePlacement(Vector2 worldPointerPosition)
        {
            if (_placementNodeType == null)
            {
                return;
            }

            _lastPointerPosition = worldPointerPosition;
            var localPoint = this.WorldToLocal(worldPointerPosition);
            _placementGhost.style.left = localPoint.x - (PlacementGhostWidth * 0.5f);
            _placementGhost.style.top = localPoint.y - (PlacementGhostHeight * 0.5f);
            _placementGhost.style.width = PlacementGhostWidth;
            _placementGhost.style.height = PlacementGhostHeight;
            _placementGhost.style.display = worldBound.Contains(worldPointerPosition) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        internal bool CommitNodePlacement(Vector2 worldPointerPosition)
        {
            if (_placementNodeType == null)
            {
                return false;
            }

            var itemType = _placementNodeType.Value;
            var canPlace = worldBound.Contains(worldPointerPosition);
            CancelNodePlacement();

            if (!canPlace || _graph == null)
            {
                return false;
            }

            var canvasPosition = GetCanvasPosition(worldPointerPosition);
            switch (itemType)
            {
                case DialoguePaletteItemType.TextNode:
                    CreateTextNode(canvasPosition);
                    break;
                case DialoguePaletteItemType.Comment:
                    CreateCommentNode(canvasPosition);
                    break;
                default:
                    return false;
            }

            return true;
        }

        internal void CancelNodePlacement()
        {
            _placementNodeType = null;
            _placementGhost.style.display = DisplayStyle.None;
        }

        public void DeleteNode(BaseNodeData node)
        {
            if (_graph == null || node == null)
            {
                return;
            }

            DialogueGraphUtility.DeleteNode(_graph, node.Id);
            LoadGraph(_graph);
            SelectionChangedAction?.Invoke(null);
            MarkChanged();
        }

        public void RefreshNodeVisuals()
        {
            foreach (var view in _textNodeViews.Values)
            {
                view.RefreshFromData();
            }

            foreach (var view in _commentNodeViews.Values)
            {
                view.RefreshFromData();
            }

            RefreshEdgeLayer();
        }

        public void RefreshLinksForNode(string nodeId)
        {
            if (_graph == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            RefreshNodeVisuals();
            MarkChanged();
        }

        public Vector2 GetCanvasCenter()
        {
            var scale = viewTransform.scale.x == 0f ? 1f : viewTransform.scale.x;
            var panOffset = new Vector2(viewTransform.position.x, viewTransform.position.y);
            return (layout.center - panOffset) / scale;
        }

        public void FrameAndSelectNode(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            GraphElement element = null;
            BaseNodeData node = null;

            if (_textNodeViews.TryGetValue(nodeId, out var textNodeView))
            {
                element = textNodeView;
                node = textNodeView.Data;
            }
            else if (_commentNodeViews.TryGetValue(nodeId, out var commentNodeView))
            {
                element = commentNodeView;
                node = commentNodeView.Data;
            }

            if (element == null)
            {
                return;
            }

            SelectNode(element, node);

            var scale = viewTransform.scale.x == 0f ? 1f : viewTransform.scale.x;
            var targetCenter = element.GetPosition().center;
            var viewportCenter = layout.center;
            var desiredPan = viewportCenter - (targetCenter * scale);
            UpdateViewTransform(desiredPan, viewTransform.scale);
            RefreshEdgeLayer();
        }

        internal NodeLinkData CreateLink(string fromNodeId, string toNodeId, bool markChanged = true)
        {
            if (_graph == null ||
                string.IsNullOrWhiteSpace(fromNodeId) ||
                string.IsNullOrWhiteSpace(toNodeId) ||
                fromNodeId == toNodeId)
            {
                return null;
            }

            var link = new NodeLinkData
            {
                FromNodeId = fromNodeId,
                ToNodeId = toNodeId,
                Order = DialogueGraphUtility.GetOutgoingLinks(_graph, fromNodeId).Count
            };

            _graph.Links.Add(link);
            DialogueGraphUtility.NormalizeLinkOrder(_graph, fromNodeId);

            if (markChanged)
            {
                RefreshNodeVisuals();
                MarkChanged();
            }

            return link;
        }

        internal bool DeleteLink(NodeLinkData link, bool markChanged = true, bool reloadGraph = false)
        {
            if (_graph == null || link == null)
            {
                return false;
            }

            var removed = _graph.Links.RemoveAll(existing => existing != null && existing.Id == link.Id) > 0;
            if (!removed)
            {
                return false;
            }

            DialogueGraphUtility.NormalizeLinkOrder(_graph, link.FromNodeId);

            if (reloadGraph)
            {
                LoadGraph(_graph);
            }
            else
            {
                RefreshNodeVisuals();
            }

            if (markChanged)
            {
                MarkChanged();
            }

            return true;
        }

        internal void NotifySelected(BaseNodeData node)
        {
            SelectionChangedAction?.Invoke(node);
        }

        internal void NotifyNodeMoved()
        {
            RefreshNodeVisuals();
            MarkChanged();
        }

        internal void MarkChanged()
        {
            GraphChangedAction?.Invoke();
        }

        internal IEnumerable<NodeLinkData> GetOutgoingLinks(string nodeId)
        {
            return DialogueGraphUtility.GetOutgoingLinks(_graph, nodeId);
        }

        internal int GetRenderedLinkCount()
        {
            if (_graph == null)
            {
                return 0;
            }

            return _graph.Links.Count(link =>
                link != null &&
                !string.IsNullOrWhiteSpace(link.ToNodeId) &&
                _textNodeViews.ContainsKey(link.FromNodeId) &&
                _textNodeViews.ContainsKey(link.ToNodeId));
        }

        internal void BeginLinkDrag(DialogueTextNodeView sourceView, Vector2 worldPointerPosition)
        {
            if (sourceView == null || _graph == null)
            {
                return;
            }

            _activeLinkSource = sourceView;
            _activeLinkTarget = null;
            _activeLinkStartWorld = sourceView.GetBottomAnchorWorld(worldPointerPosition.x);
            _activeLinkPointerWorld = worldPointerPosition;
            RefreshEdgeLayer();
        }

        private DialogueTextNodeView CreateTextNodeView(DialogueTextNodeData node)
        {
            var view = new DialogueTextNodeView(node, this);
            _textNodeViews[node.Id] = view;
            AddElement(view);
            return view;
        }

        private DialogueCommentNodeView CreateCommentNodeView(CommentNodeData node)
        {
            var view = new DialogueCommentNodeView(node, this);
            _commentNodeViews[node.Id] = view;
            AddElement(view);
            return view;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_graph == null || _isReloading)
            {
                return change;
            }

            var changed = false;

            if (change.elementsToRemove != null)
            {
                foreach (var element in change.elementsToRemove)
                {
                    switch (element)
                    {
                        case DialogueTextNodeView textNodeView:
                            DialogueGraphUtility.DeleteNode(_graph, textNodeView.Data.Id);
                            changed = true;
                            break;
                        case DialogueCommentNodeView commentNodeView:
                            DialogueGraphUtility.DeleteNode(_graph, commentNodeView.Data.Id);
                            changed = true;
                            break;
                    }
                }
            }

            if (changed)
            {
                RefreshNodeVisuals();
                MarkChanged();
            }

            return change;
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            _lastPointerPosition = this.LocalToWorld(evt.localMousePosition);
            if (_activeLinkSource == null)
            {
                return;
            }

            _activeLinkPointerWorld = this.LocalToWorld(evt.localMousePosition);
            _activeLinkTarget = ResolveLinkTarget(_activeLinkPointerWorld, _activeLinkSource);
            RefreshEdgeLayer();
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            _lastPointerPosition = this.LocalToWorld(evt.localMousePosition);
            if (_activeLinkSource != null)
            {
                return;
            }

            if (evt.target == this || evt.target is GridBackground)
            {
                SelectionChangedAction?.Invoke(null);
            }
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            _lastPointerPosition = this.LocalToWorld(evt.localMousePosition);
            if (_activeLinkSource == null || evt.button != 0)
            {
                return;
            }

            var pointerWorld = this.LocalToWorld(evt.localMousePosition);
            var targetView = ResolveLinkTarget(pointerWorld, _activeLinkSource);
            var created = false;
            if (targetView != null)
            {
                created = CreateLink(_activeLinkSource.Data.Id, targetView.Data.Id) != null;
            }

            CancelLinkDrag();
            if (created)
            {
                evt.StopImmediatePropagation();
            }
        }

        private Vector2 GetCanvasPosition(Vector2 worldPosition)
        {
            return contentViewContainer.WorldToLocal(worldPosition);
        }

        private DialogueTextNodeView ResolveLinkTarget(Vector2 worldPointerPosition, DialogueTextNodeView sourceView)
        {
            if (panel == null)
            {
                return null;
            }

            var picked = panel.Pick(worldPointerPosition);
            while (picked != null && picked is not DialogueTextNodeView)
            {
                picked = picked.parent;
            }

            if (picked is not DialogueTextNodeView targetView ||
                targetView == sourceView ||
                !targetView.IsPointerInTopHalf(worldPointerPosition))
            {
                return null;
            }

            return targetView;
        }

        private IEnumerable<DialogueEdgeGeometry> GetEdgeGeometries()
        {
            foreach (var link in _graph?.Links ?? Enumerable.Empty<NodeLinkData>())
            {
                if (link == null ||
                    string.IsNullOrWhiteSpace(link.ToNodeId) ||
                    !_textNodeViews.TryGetValue(link.FromNodeId, out var sourceView) ||
                    !_textNodeViews.TryGetValue(link.ToNodeId, out var targetView))
                {
                    continue;
                }

                var sourceAnchorWorld = sourceView.GetBottomAnchorWorld(targetView.worldBound.center.x);
                var targetAnchorWorld = targetView.GetTopAnchorWorld(sourceView.worldBound.center.x);
                yield return new DialogueEdgeGeometry(
                    _edgeLayer.WorldToLocal(sourceAnchorWorld),
                    _edgeLayer.WorldToLocal(targetAnchorWorld),
                    new Color(0.88f, 0.9f, 0.96f, 0.92f),
                    2.3f);
            }

            if (_activeLinkSource != null)
            {
                var previewEndWorld = _activeLinkTarget != null
                    ? _activeLinkTarget.GetTopAnchorWorld(_activeLinkSource.worldBound.center.x)
                    : _activeLinkPointerWorld;

                yield return new DialogueEdgeGeometry(
                    _edgeLayer.WorldToLocal(_activeLinkStartWorld),
                    _edgeLayer.WorldToLocal(previewEndWorld),
                    new Color(0.55f, 0.76f, 1f, 0.98f),
                    2.8f);
            }
        }

        private void CancelLinkDrag()
        {
            _activeLinkSource = null;
            _activeLinkTarget = null;
            _activeLinkStartWorld = Vector2.zero;
            _activeLinkPointerWorld = Vector2.zero;
            RefreshEdgeLayer();
        }

        private void SelectNode(GraphElement element, BaseNodeData node)
        {
            ClearSelection();
            AddToSelection(element);
            SelectionChangedAction?.Invoke(node);
        }

        private void RefreshEdgeLayer()
        {
            _edgeLayer.MarkDirtyRepaint();
        }

        private void UpdateEmptyState()
        {
            _emptyStateLabel.style.display = _graph == null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private readonly struct DialogueEdgeGeometry
        {
            public DialogueEdgeGeometry(Vector2 start, Vector2 end, Color color, float thickness)
            {
                Start = start;
                End = end;
                Color = color;
                Thickness = thickness;
            }

            public Vector2 Start { get; }
            public Vector2 End { get; }
            public Color Color { get; }
            public float Thickness { get; }
        }

        private sealed class DialogueEdgeLayer : ImmediateModeElement
        {
            public Func<IEnumerable<DialogueEdgeGeometry>> GeometryProvider { get; set; }

            protected override void ImmediateRepaint()
            {
                if (GeometryProvider == null)
                {
                    return;
                }

                foreach (var geometry in GeometryProvider())
                {
                    var tangent = Mathf.Max(70f, Mathf.Abs(geometry.End.y - geometry.Start.y) * 0.55f);
                    var startTangent = geometry.Start + Vector2.up * tangent;
                    var endTangent = geometry.End + Vector2.down * tangent;
                    Handles.DrawBezier(geometry.Start, geometry.End, startTangent, endTangent, geometry.Color, null, geometry.Thickness);
                }
            }
        }
    }

    public class DialogueTextNodeView : Node
    {
        private const float LinkAnchorInset = 18f;

        private readonly DialogueGraphView _graphView;
        private readonly Label _bodyPreviewLabel;
        private readonly Label _metaLabel;
        private readonly Label _startBadge;

        public DialogueTextNodeView(DialogueTextNodeData data, DialogueGraphView graphView)
        {
            Data = data;
            _graphView = graphView;
            viewDataKey = data.Id;
            capabilities |= Capabilities.Deletable | Capabilities.Movable | Capabilities.Selectable;

            AddToClassList("dialogue-node");
            mainContainer.AddToClassList("dialogue-node__surface");
            topContainer.AddToClassList("dialogue-node__top-container");
            titleContainer.AddToClassList("dialogue-node__title-row");
            extensionContainer.AddToClassList("dialogue-node__content");
            inputContainer.style.display = DisplayStyle.None;
            outputContainer.style.display = DisplayStyle.None;

            _startBadge = new Label("START");
            _startBadge.AddToClassList("dialogue-node__start-badge");
            titleButtonContainer.Insert(0, _startBadge);

            var deleteButton = new Button(() => _graphView.DeleteNode(Data))
            {
                text = "X"
            };
            deleteButton.AddToClassList("dialogue-node__delete-button");
            titleButtonContainer.Add(deleteButton);

            _bodyPreviewLabel = new Label();
            _bodyPreviewLabel.AddToClassList("dialogue-node__body-preview");
            _bodyPreviewLabel.style.whiteSpace = WhiteSpace.Normal;
            extensionContainer.Add(_bodyPreviewLabel);

            _metaLabel = new Label();
            _metaLabel.AddToClassList("dialogue-node__meta");
            extensionContainer.Add(_metaLabel);

            RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
            RefreshFromData();
        }

        public DialogueTextNodeData Data { get; }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            Data.Position = newPos.position;
            _graphView.NotifyNodeMoved();
        }

        public void RefreshFromData()
        {
            var outgoingCount = _graphView
                .GetOutgoingLinks(Data.Id)
                .Count(link => !string.IsNullOrWhiteSpace(link.ToNodeId));

            title = string.IsNullOrWhiteSpace(Data.Title) ? "Untitled" : Data.Title;
            _startBadge.style.display = Data.IsStartNode ? DisplayStyle.Flex : DisplayStyle.None;
            _bodyPreviewLabel.text = BuildPreviewText(Data.BodyText);
            _metaLabel.text = outgoingCount == 0
                ? "Drag from the lower half to create a connection."
                : Data.UseOutputsAsChoices
                    ? $"{outgoingCount} choice link{(outgoingCount == 1 ? string.Empty : "s")}"
                    : $"{outgoingCount} outgoing link{(outgoingCount == 1 ? string.Empty : "s")}";

            EnableInClassList("dialogue-node--start", Data.IsStartNode);
            base.SetPosition(new Rect(
                Data.Position,
                GetPosition().size == Vector2.zero ? new Vector2(280f, 170f) : GetPosition().size));
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
            if (evt.button != 0 || evt.target is Button)
            {
                return;
            }

            var worldPointerPosition = this.LocalToWorld(evt.localMousePosition);
            _graphView.NotifySelected(Data);

            if (IsPointerInBottomHalf(worldPointerPosition))
            {
                _graphView.BeginLinkDrag(this, worldPointerPosition);
                evt.StopImmediatePropagation();
            }
        }

        private Vector2 ToLocalPoint(Vector2 worldPointerPosition)
        {
            return this.WorldToLocal(worldPointerPosition);
        }

        private static string BuildPreviewText(string bodyText)
        {
            if (string.IsNullOrWhiteSpace(bodyText))
            {
                return "Empty node text";
            }

            var normalized = bodyText.Replace("\r", " ").Replace("\n", " ").Trim();
            return normalized.Length <= 120 ? normalized : $"{normalized.Substring(0, 117)}...";
        }
    }

    public class DialogueCommentNodeView : Node
    {
        private readonly DialogueGraphView _graphView;
        private readonly Label _commentLabel;

        public DialogueCommentNodeView(CommentNodeData data, DialogueGraphView graphView)
        {
            Data = data;
            _graphView = graphView;
            viewDataKey = data.Id;
            capabilities |= Capabilities.Deletable | Capabilities.Movable | Capabilities.Selectable | Capabilities.Resizable;

            AddToClassList("dialogue-comment-node");
            mainContainer.AddToClassList("dialogue-comment-node__surface");
            extensionContainer.AddToClassList("dialogue-comment-node__content");
            inputContainer.style.display = DisplayStyle.None;
            outputContainer.style.display = DisplayStyle.None;

            var deleteButton = new Button(() => _graphView.DeleteNode(Data))
            {
                text = "X"
            };
            deleteButton.AddToClassList("dialogue-node__delete-button");
            titleButtonContainer.Add(deleteButton);

            _commentLabel = new Label();
            _commentLabel.AddToClassList("dialogue-comment-node__label");
            _commentLabel.style.whiteSpace = WhiteSpace.Normal;
            extensionContainer.Add(_commentLabel);

            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.target is not Button)
                {
                    _graphView.NotifySelected(Data);
                }
            }, TrickleDown.TrickleDown);

            RefreshFromData();
        }

        public CommentNodeData Data { get; }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            Data.Position = newPos.position;
            Data.Area = newPos;
            _graphView.NotifyNodeMoved();
        }

        public void RefreshFromData()
        {
            title = string.IsNullOrWhiteSpace(Data.Title) ? "Comment" : Data.Title;
            _commentLabel.text = string.IsNullOrWhiteSpace(Data.Comment) ? "Empty note" : Data.Comment;
            base.SetPosition(Data.Area.width <= 0f || Data.Area.height <= 0f
                ? new Rect(Data.Position.x, Data.Position.y, 320f, 180f)
                : Data.Area);
            RefreshExpandedState();
        }
    }
}

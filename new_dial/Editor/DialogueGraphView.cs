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
        private const float KeyboardPanSpeed = 900f;
        private const float FrameCenterTolerancePixels = 42f;
        internal static readonly Vector2 TextNodeInitialSize = new(280f, 170f);
        internal static readonly Vector2 CommentNodeInitialSize = new(420f, 260f);

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
        private bool _hasCanvasFocus;
        private bool _moveUpPressed;
        private bool _moveDownPressed;
        private bool _moveLeftPressed;
        private bool _moveRightPressed;
        private double _lastPanTickTime;
        private string _pendingFrameNodeId;
        private int _pendingFrameAttempts;
        private static DialogueClipboardData _clipboard;
        internal bool IsApplyingCommentGroupMove { get; private set; }

        public DialogueGraphView()
        {
            AddToClassList("dialogue-graph-view");
            style.flexGrow = 1f;
            focusable = true;
            tabIndex = 0;

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
            RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            RegisterCallback<KeyUpEvent>(OnKeyUp, TrickleDown.TrickleDown);
            RegisterCallback<FocusInEvent>(_ => SetCanvasFocusState(true));
            RegisterCallback<BlurEvent>(_ => SetCanvasFocusState(false));

            _lastPanTickTime = EditorApplication.timeSinceStartup;
            schedule.Execute(OnKeyboardPanTick).Every(16);
        }

        public Action<BaseNodeData> SelectionChangedAction { get; set; }
        public Action GraphChangedAction { get; set; }
        public Action<bool> CanvasFocusChangedAction { get; set; }

        public DialogueGraphData Graph => _graph;
        public bool HasCanvasFocus => _hasCanvasFocus;

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
            FocusCanvas();
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
                Area = new Rect(position, CommentNodeInitialSize),
                Comment = "Group related dialogue nodes here.",
                Tint = new Color(0.23f, 0.34f, 0.56f, 0.26f)
            };

            _graph.Nodes.Add(node);
            var view = CreateCommentNodeView(node);
            RefreshNodeVisuals();
            SelectNode(view, node);
            FocusCanvas();
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
            var size = GetPlacementSize(_placementNodeType.Value);
            var topLeft = GetPlacementTopLeft(_placementNodeType.Value, localPoint);
            _placementGhost.style.left = topLeft.x;
            _placementGhost.style.top = topLeft.y;
            _placementGhost.style.width = size.x;
            _placementGhost.style.height = size.y;
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

            var canvasPointerPosition = WorldToCanvasPosition(worldPointerPosition);
            var placementPosition = GetPlacementTopLeft(itemType, canvasPointerPosition);
            switch (itemType)
            {
                case DialoguePaletteItemType.TextNode:
                    CreateTextNode(placementPosition);
                    break;
                case DialoguePaletteItemType.Comment:
                    CreateCommentNode(placementPosition);
                    break;
                default:
                    return false;
            }

            FocusCanvas();
            return true;
        }

        private static Vector2 GetPlacementSize(DialoguePaletteItemType itemType)
        {
            return itemType == DialoguePaletteItemType.Comment
                ? CommentNodeInitialSize
                : TextNodeInitialSize;
        }

        private static Vector2 GetPlacementTopLeft(DialoguePaletteItemType itemType, Vector2 pointerPosition)
        {
            var size = GetPlacementSize(itemType);
            return pointerPosition - (size * 0.5f);
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

            if (node is CommentNodeData commentNode)
            {
                DeleteCommentGroup(commentNode);
            }
            else
            {
                DialogueGraphUtility.DeleteNode(_graph, node.Id);
            }

            LoadGraph(_graph);
            SelectionChangedAction?.Invoke(null);
            MarkChanged();
        }

        public bool CopySelectionToClipboard()
        {
            if (_graph == null)
            {
                return false;
            }

            var selectedNodes = GetExpandedSelectedNodeData();

            if (selectedNodes.Count == 0)
            {
                return false;
            }

            var nodeIds = new HashSet<string>(selectedNodes.Select(node => node.Id));
            var minPosition = new Vector2(
                selectedNodes.Min(node => node.Position.x),
                selectedNodes.Min(node => node.Position.y));

            _clipboard = new DialogueClipboardData
            {
                Nodes = selectedNodes.Select(node => node.Clone()).ToList(),
                Links = _graph.Links
                    .Where(link => link != null && nodeIds.Contains(link.FromNodeId) && nodeIds.Contains(link.ToNodeId))
                    .Select(link => link.Clone())
                    .ToList(),
                ReferencePosition = minPosition
            };

            return true;
        }

        public bool CutSelectionToClipboard()
        {
            if (!CopySelectionToClipboard())
            {
                return false;
            }

            DeleteSelectedNodes();
            return true;
        }

        public bool PasteClipboard()
        {
            if (_graph == null || _clipboard == null || _clipboard.Nodes.Count == 0)
            {
                return false;
            }

            var targetPosition = HasCanvasFocus
                ? WorldToCanvasPosition(_lastPointerPosition)
                : GetCanvasCenter();
            var offset = targetPosition - _clipboard.ReferencePosition;
            var idMap = new Dictionary<string, string>();
            var pastedNodes = new List<BaseNodeData>();

            foreach (var sourceNode in _clipboard.Nodes)
            {
                var clone = sourceNode.Clone();
                var originalId = clone.Id;
                clone.Id = GuidUtility.NewGuid();
                clone.Position += offset;

                if (clone is CommentNodeData commentNode)
                {
                    commentNode.Area = new Rect(commentNode.Area.position + offset, commentNode.Area.size);
                }
                else if (clone is DialogueTextNodeData textNode)
                {
                    textNode.IsStartNode = false;
                }

                idMap[originalId] = clone.Id;
                pastedNodes.Add(clone);
                _graph.Nodes.Add(clone);
            }

            foreach (var sourceLink in _clipboard.Links)
            {
                if (!idMap.TryGetValue(sourceLink.FromNodeId, out var fromId) ||
                    !idMap.TryGetValue(sourceLink.ToNodeId, out var toId))
                {
                    continue;
                }

                var clonedLink = sourceLink.Clone();
                clonedLink.Id = GuidUtility.NewGuid();
                clonedLink.FromNodeId = fromId;
                clonedLink.ToNodeId = toId;
                _graph.Links.Add(clonedLink);
            }

            LoadGraph(_graph);
            ClearSelection();
            foreach (var pastedNode in pastedNodes)
            {
                if (pastedNode is DialogueTextNodeData textNode &&
                    _textNodeViews.TryGetValue(textNode.Id, out var textView))
                {
                    AddToSelection(textView);
                }
                else if (pastedNode is CommentNodeData commentNode &&
                         _commentNodeViews.TryGetValue(commentNode.Id, out var commentView))
                {
                    AddToSelection(commentView);
                }
            }

            SelectionChangedAction?.Invoke(pastedNodes.LastOrDefault());
            FocusCanvas();
            MarkChanged();
            return true;
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
            var scale = GetCurrentGraphScale();
            var panOffset = GetCurrentGraphPan();
            return (layout.center - panOffset) / scale;
        }

        public void FocusCanvas()
        {
            Focus();
            SetCanvasFocusState(true);
        }

        internal void ReleaseCanvasFocus()
        {
            SetCanvasFocusState(false);
            Blur();
        }

        public void FrameAndSelectNode(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            _pendingFrameNodeId = nodeId;
            _pendingFrameAttempts = 0;
            AttemptPendingFrame();
        }

        internal bool TryFrameAndSelectNode(string nodeId)
        {
            if (!TryResolveFrameTarget(nodeId, out var element, out var node))
            {
                return false;
            }

            SelectNode(element, node);
            FocusCanvas();
            FrameSelection();

            if (!IsElementFramed(element))
            {
                CenterElementInViewport(element);
            }

            RefreshEdgeLayer();
            return IsElementFramed(element);
        }

        private void AttemptPendingFrame()
        {
            if (string.IsNullOrWhiteSpace(_pendingFrameNodeId))
            {
                return;
            }

            if (TryFrameAndSelectNode(_pendingFrameNodeId))
            {
                _pendingFrameNodeId = null;
                _pendingFrameAttempts = 0;
                return;
            }

            _pendingFrameAttempts++;
            if (_pendingFrameAttempts >= 8)
            {
                _pendingFrameNodeId = null;
                _pendingFrameAttempts = 0;
                return;
            }

            schedule.Execute(AttemptPendingFrame).ExecuteLater(16);
        }

        private bool TryResolveFrameTarget(string nodeId, out GraphElement element, out BaseNodeData node)
        {
            element = null;
            node = null;

            if (string.IsNullOrWhiteSpace(nodeId) ||
                panel == null ||
                worldBound.width <= 1f ||
                worldBound.height <= 1f ||
                layout.width <= 1f ||
                layout.height <= 1f)
            {
                return false;
            }

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
                return false;
            }

            return element.worldBound.width > 1f && element.worldBound.height > 1f;
        }

        private bool IsElementFramed(GraphElement element)
        {
            if (element == null ||
                worldBound.width <= 1f ||
                worldBound.height <= 1f ||
                element.worldBound.width <= 1f ||
                element.worldBound.height <= 1f)
            {
                return false;
            }

            var viewportCenter = worldBound.center;
            var elementCenter = element.worldBound.center;
            return Mathf.Abs(elementCenter.x - viewportCenter.x) <= FrameCenterTolerancePixels &&
                   Mathf.Abs(elementCenter.y - viewportCenter.y) <= FrameCenterTolerancePixels;
        }

        private void CenterElementInViewport(GraphElement element)
        {
            if (element == null ||
                worldBound.width <= 1f ||
                worldBound.height <= 1f ||
                element.worldBound.width <= 1f ||
                element.worldBound.height <= 1f)
            {
                return;
            }

            var delta = worldBound.center - element.worldBound.center;
            var currentPan = GetCurrentGraphPan();
            UpdateViewTransform(
                new Vector3(currentPan.x + delta.x, currentPan.y + delta.y, 0f),
                GetCurrentGraphScaleVector());
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

        internal void SelectCommentGroup(CommentNodeData commentNode)
        {
            if (commentNode == null || !_commentNodeViews.TryGetValue(commentNode.Id, out var commentView))
            {
                return;
            }

            var groupedElements = GetCommentGroupElements(commentNode).ToList();
            if (groupedElements.Count == 0)
            {
                groupedElements.Add(commentView);
            }

            ClearSelection();
            foreach (var element in groupedElements)
            {
                AddToSelection(element);
            }

            RestoreCommentNodeLayering();
            SelectionChangedAction?.Invoke(commentNode);
        }

        internal void NotifyNodeMoved()
        {
            RefreshEdgeLayer();
            RestoreCommentNodeLayering();
            MarkChanged();
        }

        internal void MoveCommentGroup(CommentNodeData rootComment, Vector2 delta, HashSet<string> ignoredNodeIds = null)
        {
            MoveCommentGroup(rootComment, delta, GetCommentArea(rootComment), ignoredNodeIds);
        }

        internal void MoveCommentGroup(CommentNodeData rootComment, Vector2 delta, Rect rootCommentArea, HashSet<string> ignoredNodeIds = null)
        {
            if (_graph == null || rootComment == null || delta == Vector2.zero)
            {
                return;
            }

            IsApplyingCommentGroupMove = true;
            try
            {
                var movedIds = new HashSet<string> { rootComment.Id };
                foreach (var element in GetCommentGroupElements(rootComment, rootCommentArea))
                {
                    switch (element)
                    {
                        case DialogueTextNodeView textNodeView
                            when movedIds.Add(textNodeView.Data.Id) &&
                                 (ignoredNodeIds == null || !ignoredNodeIds.Contains(textNodeView.Data.Id)):
                            textNodeView.Data.Position += delta;
                            textNodeView.RefreshFromData();
                            break;
                        case DialogueCommentNodeView commentNodeView
                            when movedIds.Add(commentNodeView.Data.Id) &&
                                 (ignoredNodeIds == null || !ignoredNodeIds.Contains(commentNodeView.Data.Id)):
                            commentNodeView.Data.Position += delta;
                            commentNodeView.Data.Area = new Rect(
                                commentNodeView.Data.Area.position + delta,
                                commentNodeView.Data.Area.size);
                            commentNodeView.RefreshFromData();
                            break;
                    }
                }
            }
            finally
            {
                IsApplyingCommentGroupMove = false;
            }
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

            FocusCanvas();
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
            RestoreCommentNodeLayering();
            return view;
        }

        internal void RestoreCommentNodeLayering()
        {
            foreach (var commentView in _commentNodeViews.Values)
            {
                commentView.SendToBack();
            }

            _edgeLayer.BringToFront();

            foreach (var textNodeView in _textNodeViews.Values)
            {
                textNodeView.BringToFront();
            }
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
                RefreshEdgeLayer();
                RestoreCommentNodeLayering();
                MarkChanged();
            }

            return change;
        }

        private void DeleteCommentGroup(CommentNodeData rootComment)
        {
            var nodeIdsToDelete = GetCommentGroupElements(rootComment)
                .OfType<Node>()
                .Select(element => element switch
                {
                    DialogueTextNodeView textNodeView => textNodeView.Data.Id,
                    DialogueCommentNodeView commentNodeView => commentNodeView.Data.Id,
                    _ => null
                })
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (!nodeIdsToDelete.Contains(rootComment.Id))
            {
                nodeIdsToDelete.Add(rootComment.Id);
            }

            foreach (var nodeId in nodeIdsToDelete)
            {
                DialogueGraphUtility.DeleteNode(_graph, nodeId);
            }
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

            FocusCanvas();
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

        internal Vector2 WorldToCanvasPosition(Vector2 worldPosition)
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

        private IEnumerable<GraphElement> GetCommentGroupElements(CommentNodeData rootComment)
        {
            return GetCommentGroupElements(rootComment, GetCommentArea(rootComment));
        }

        private IEnumerable<GraphElement> GetCommentGroupElements(CommentNodeData rootComment, Rect rootCommentArea)
        {
            var groupedIds = new HashSet<string>();
            foreach (var element in GetCommentGroupElementsRecursive(rootComment, rootCommentArea, groupedIds))
            {
                yield return element;
            }
        }

        private IEnumerable<GraphElement> GetCommentGroupElementsRecursive(CommentNodeData commentNode, Rect commentArea, HashSet<string> groupedIds)
        {
            if (commentNode == null || !groupedIds.Add(commentNode.Id))
            {
                yield break;
            }

            if (_commentNodeViews.TryGetValue(commentNode.Id, out var commentView))
            {
                yield return commentView;
            }

            foreach (var nestedComment in _commentNodeViews.Values)
            {
                if (nestedComment.Data.Id == commentNode.Id || groupedIds.Contains(nestedComment.Data.Id))
                {
                    continue;
                }

                var nestedCommentArea = GetCommentArea(nestedComment.Data);
                if (!IsRectGroupedByComment(nestedCommentArea, commentArea))
                {
                    continue;
                }

                foreach (var element in GetCommentGroupElementsRecursive(nestedComment.Data, nestedCommentArea, groupedIds))
                {
                    yield return element;
                }
            }

            foreach (var textNode in _textNodeViews.Values)
            {
                if (groupedIds.Contains(textNode.Data.Id))
                {
                    continue;
                }

                if (!IsRectGroupedByComment(GetGraphElementRect(textNode), commentArea))
                {
                    continue;
                }

                groupedIds.Add(textNode.Data.Id);
                yield return textNode;
            }
        }

        private static Rect GetCommentArea(CommentNodeData commentNode)
        {
            return commentNode.Area.width <= 0f || commentNode.Area.height <= 0f
                ? new Rect(commentNode.Position, CommentNodeInitialSize)
                : commentNode.Area;
        }

        private static Rect GetGraphElementRect(GraphElement element)
        {
            var rect = element.GetPosition();
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return element.worldBound;
            }

            return rect;
        }

        private static bool IsRectGroupedByComment(Rect nodeRect, Rect commentArea)
        {
            return commentArea.Contains(nodeRect.center);
        }

        private void RefreshEdgeLayer()
        {
            _edgeLayer.MarkDirtyRepaint();
        }

        internal void SetMovementKeyState(KeyCode keyCode, bool isPressed)
        {
            switch (keyCode)
            {
                case KeyCode.W:
                    _moveUpPressed = isPressed;
                    break;
                case KeyCode.S:
                    _moveDownPressed = isPressed;
                    break;
                case KeyCode.A:
                    _moveLeftPressed = isPressed;
                    break;
                case KeyCode.D:
                    _moveRightPressed = isPressed;
                    break;
            }
        }

        internal void StepKeyboardPan(float deltaTimeSeconds)
        {
            if (!_hasCanvasFocus || deltaTimeSeconds <= 0f)
            {
                return;
            }

            var input = Vector2.zero;
            if (_moveUpPressed)
            {
                input.y += 1f;
            }

            if (_moveDownPressed)
            {
                input.y -= 1f;
            }

            if (_moveLeftPressed)
            {
                input.x += 1f;
            }

            if (_moveRightPressed)
            {
                input.x -= 1f;
            }

            if (input == Vector2.zero)
            {
                return;
            }

            var scale = GetCurrentGraphScale();
            var panDelta = input.normalized * (KeyboardPanSpeed * scale * deltaTimeSeconds);
            var currentPan = GetCurrentGraphPan();
            UpdateViewTransform(
                new Vector3(currentPan.x + panDelta.x, currentPan.y + panDelta.y, 0f),
                GetCurrentGraphScaleVector());
            RefreshEdgeLayer();
        }

        private float GetCurrentGraphScale()
        {
            var scale = contentViewContainer.resolvedStyle.scale.value;
            return Mathf.Approximately(scale.x, 0f) ? 1f : scale.x;
        }

        private Vector3 GetCurrentGraphScaleVector()
        {
            var scale = contentViewContainer.resolvedStyle.scale.value;
            return new Vector3(
                Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
                Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
                Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
        }

        private Vector2 GetCurrentGraphPan()
        {
            var translate = contentViewContainer.resolvedStyle.translate;
            return new Vector2(translate.x, translate.y);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!_hasCanvasFocus)
            {
                return;
            }

            if (evt.actionKey)
            {
                var handled = evt.keyCode switch
                {
                    KeyCode.C => CopySelectionToClipboard(),
                    KeyCode.X => CutSelectionToClipboard(),
                    KeyCode.V => PasteClipboard(),
                    _ => false
                };

                if (handled)
                {
                    evt.StopImmediatePropagation();
                }

                return;
            }

            if (!TryMapMovementKey(evt.keyCode))
            {
                return;
            }

            SetMovementKeyState(evt.keyCode, true);
            evt.StopImmediatePropagation();
        }

        private void OnKeyUp(KeyUpEvent evt)
        {
            if (!_hasCanvasFocus)
            {
                return;
            }

            if (!TryMapMovementKey(evt.keyCode))
            {
                return;
            }

            SetMovementKeyState(evt.keyCode, false);
            evt.StopImmediatePropagation();
        }

        private void OnKeyboardPanTick()
        {
            var now = EditorApplication.timeSinceStartup;
            var deltaTime = Mathf.Max(0f, (float)(now - _lastPanTickTime));
            _lastPanTickTime = now;
            StepKeyboardPan(deltaTime);
        }

        private void SetCanvasFocusState(bool focused)
        {
            if (_hasCanvasFocus == focused)
            {
                return;
            }

            _hasCanvasFocus = focused;
            if (!focused)
            {
                ResetMovementKeys();
            }

            EnableInClassList("is-focused", focused);
            CanvasFocusChangedAction?.Invoke(focused);
        }

        private void ResetMovementKeys()
        {
            _moveUpPressed = false;
            _moveDownPressed = false;
            _moveLeftPressed = false;
            _moveRightPressed = false;
        }

        private static bool TryMapMovementKey(KeyCode keyCode)
        {
            return keyCode == KeyCode.W ||
                   keyCode == KeyCode.A ||
                   keyCode == KeyCode.S ||
                   keyCode == KeyCode.D;
        }

        private void UpdateEmptyState()
        {
            if (_graph == null)
            {
                _emptyStateLabel.text = "Select a dialogue or create a new one to start building nodes.";
                _emptyStateLabel.style.display = DisplayStyle.Flex;
                return;
            }

            if (_graph.Nodes.Count == 0)
            {
                _emptyStateLabel.text = "This dialogue is empty. Add your first node from the palette to begin.";
                _emptyStateLabel.style.display = DisplayStyle.Flex;
                return;
            }

            _emptyStateLabel.style.display = DisplayStyle.None;
        }

        private void DeleteSelectedNodes()
        {
            var selectedNodes = GetExpandedSelectedNodeData();

            if (selectedNodes.Count == 0)
            {
                return;
            }

            foreach (var selectedNode in selectedNodes.OfType<CommentNodeData>())
            {
                DeleteCommentGroup(selectedNode);
            }

            foreach (var selectedNode in selectedNodes.Where(node => node is not CommentNodeData))
            {
                DialogueGraphUtility.DeleteNode(_graph, selectedNode.Id);
            }

            LoadGraph(_graph);
            SelectionChangedAction?.Invoke(null);
            MarkChanged();
        }

        private List<BaseNodeData> GetExpandedSelectedNodeData()
        {
            var result = new List<BaseNodeData>();
            var seenIds = new HashSet<string>();
            foreach (var element in selection.OfType<Node>())
            {
                if (element is DialogueTextNodeView textNodeView)
                {
                    if (seenIds.Add(textNodeView.Data.Id))
                    {
                        result.Add(textNodeView.Data);
                    }
                }
                else if (element is DialogueCommentNodeView commentNodeView)
                {
                    foreach (var groupedElement in GetCommentGroupElements(commentNodeView.Data))
                    {
                        switch (groupedElement)
                        {
                            case DialogueTextNodeView groupedTextNodeView when seenIds.Add(groupedTextNodeView.Data.Id):
                                result.Add(groupedTextNodeView.Data);
                                break;
                            case DialogueCommentNodeView groupedCommentNodeView when seenIds.Add(groupedCommentNodeView.Data.Id):
                                result.Add(groupedCommentNodeView.Data);
                                break;
                        }
                    }
                }
            }

            return result;
        }

        internal HashSet<string> GetDirectSelectedNodeIds(CommentNodeData rootComment = null, Rect? rootCommentArea = null)
        {
            var result = new HashSet<string>();
            foreach (var element in selection.OfType<Node>())
            {
                switch (element)
                {
                    case DialogueTextNodeView textNodeView:
                        result.Add(textNodeView.Data.Id);
                        break;
                    case DialogueCommentNodeView commentNodeView:
                        result.Add(commentNodeView.Data.Id);
                        break;
                }
            }

            if (rootComment == null || !result.Contains(rootComment.Id))
            {
                return result;
            }

            var groupArea = rootCommentArea ?? GetCommentArea(rootComment);
            foreach (var element in GetCommentGroupElements(rootComment, groupArea))
            {
                switch (element)
                {
                    case DialogueTextNodeView textNodeView:
                        result.Remove(textNodeView.Data.Id);
                        break;
                    case DialogueCommentNodeView commentNodeView when commentNodeView.Data.Id != rootComment.Id:
                        result.Remove(commentNodeView.Data.Id);
                        break;
                }
            }

            return result;
        }

        internal HashSet<string> GetSelectedNodeIds()
        {
            return new HashSet<string>(GetExpandedSelectedNodeData().Select(node => node.Id));
        }

        [Serializable]
        private sealed class DialogueClipboardData
        {
            public List<BaseNodeData> Nodes = new();
            public List<NodeLinkData> Links = new();
            public Vector2 ReferencePosition;
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
        private const float MinCommentWidth = 260f;
        private const float MinCommentHeight = 160f;
        private const float ResizeZoneSize = 44f;
        private const string ResizeHotClassName = "dialogue-comment-node--resize-hot";
        private const float FallbackHeaderHeight = 54f;
        private const float ClickMoveThreshold = 6f;

        private readonly DialogueGraphView _graphView;
        private readonly VisualElement _headerBand;
        private readonly Label _titleLabel;
        private readonly Label _commentLabel;
        private readonly VisualElement _resizeHandle;
        private bool _isResizing;
        private bool _pendingCommentSelect;
        private bool _commentDragExceededThreshold;
        private Rect _resizeStartArea;
        private Vector2 _resizeStartCanvasPointer;
        private Vector2 _commentPressStartLocalPointer;

        public DialogueCommentNodeView(CommentNodeData data, DialogueGraphView graphView)
        {
            Data = data;
            _graphView = graphView;
            viewDataKey = data.Id;
            capabilities |= Capabilities.Deletable | Capabilities.Movable | Capabilities.Selectable;

            AddToClassList("dialogue-comment-node");
            style.borderTopWidth = 1f;
            style.borderRightWidth = 1f;
            style.borderBottomWidth = 1f;
            style.borderLeftWidth = 1f;
            mainContainer.AddToClassList("dialogue-comment-node__surface");
            topContainer.AddToClassList("dialogue-comment-node__header");
            extensionContainer.AddToClassList("dialogue-comment-node__content");
            mainContainer.style.flexGrow = 1f;
            mainContainer.style.flexShrink = 0f;
            extensionContainer.style.flexGrow = 1f;
            extensionContainer.style.flexShrink = 1f;
            inputContainer.style.display = DisplayStyle.None;
            outputContainer.style.display = DisplayStyle.None;
            titleContainer.style.display = DisplayStyle.None;
            titleButtonContainer.style.display = DisplayStyle.None;

            _headerBand = new VisualElement();
            _headerBand.AddToClassList("dialogue-comment-node__header-band");
            topContainer.Add(_headerBand);

            _titleLabel = new Label();
            _titleLabel.AddToClassList("dialogue-comment-node__title");
            _headerBand.Add(_titleLabel);

            _commentLabel = new Label();
            _commentLabel.AddToClassList("dialogue-comment-node__label");
            _commentLabel.style.whiteSpace = WhiteSpace.Normal;
            extensionContainer.Add(_commentLabel);

            _resizeHandle = new VisualElement();
            _resizeHandle.AddToClassList("dialogue-comment-node__resize-handle");
            _resizeHandle.pickingMode = PickingMode.Ignore;
            mainContainer.Add(_resizeHandle);

            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 || evt.target is Button)
                {
                    return;
                }

                if (IsPointerInResizeZone(evt.localMousePosition))
                {
                    BeginResize(evt.localMousePosition);
                    evt.StopImmediatePropagation();
                    return;
                }

                if (evt.target is not Button)
                {
                    _graphView.NotifySelected(Data);
                    _pendingCommentSelect = true;
                    _commentDragExceededThreshold = false;
                    _commentPressStartLocalPointer = evt.localMousePosition;
                }
            }, TrickleDown.TrickleDown);

            RegisterCallback<MouseMoveEvent>(evt =>
            {
                var isResizeHot = IsPointerInResizeZone(evt.localMousePosition);
                EnableInClassList(ResizeHotClassName, isResizeHot || _isResizing);

                if (_pendingCommentSelect && !_commentDragExceededThreshold)
                {
                    _commentDragExceededThreshold =
                        Vector2.Distance(evt.localMousePosition, _commentPressStartLocalPointer) > ClickMoveThreshold;
                }

                if (!_isResizing)
                {
                    return;
                }

                ContinueResize(evt.localMousePosition);
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);

            RegisterCallback<MouseUpEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                if (_isResizing)
                {
                    CancelResize();
                    evt.StopImmediatePropagation();
                    return;
                }

                if (_pendingCommentSelect && !_commentDragExceededThreshold)
                {
                    _pendingCommentSelect = false;
                    schedule.Execute(() => _graphView.SelectCommentGroup(Data));
                    evt.StopImmediatePropagation();
                    return;
                }

                ResetPendingCommentSelect();
            }, TrickleDown.TrickleDown);

            RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (!_isResizing)
                {
                    EnableInClassList(ResizeHotClassName, false);
                }
            });

            RegisterCallback<MouseCaptureOutEvent>(_ =>
            {
                CancelResize();
                ResetPendingCommentSelect();
            });
            RegisterCallback<GeometryChangedEvent>(_ => SyncVisualSize(GetPosition().size));

            RefreshFromData();
        }

        public CommentNodeData Data { get; }

        public override void SetPosition(Rect newPos)
        {
            var previousArea = Data.Area.width <= 0f || Data.Area.height <= 0f
                ? new Rect(Data.Position, DialogueGraphView.CommentNodeInitialSize)
                : Data.Area;
            var previousPos = previousArea.position;
            base.SetPosition(newPos);
            SyncVisualSize(newPos.size);
            Data.Position = newPos.position;
            Data.Area = newPos;
            if (!_isResizing && !_graphView.IsApplyingCommentGroupMove)
            {
                var delta = newPos.position - previousPos;
                if (delta != Vector2.zero)
                {
                    _graphView.MoveCommentGroup(Data, delta, previousArea, _graphView.GetDirectSelectedNodeIds(Data, previousArea));
                }
            }
            _graphView.NotifyNodeMoved();
        }

        public void RefreshFromData()
        {
            var titleText = string.IsNullOrWhiteSpace(Data.Title) ? "Comment" : Data.Title;
            title = titleText;
            _titleLabel.text = titleText;
            _commentLabel.text = string.IsNullOrWhiteSpace(Data.Comment) ? "Add a description for this comment area." : Data.Comment;
            ApplyTint();
            var area = Data.Area.width <= 0f || Data.Area.height <= 0f
                ? new Rect(Data.Position, DialogueGraphView.CommentNodeInitialSize)
                : Data.Area;
            base.SetPosition(area);
            SyncVisualSize(area.size);
            RefreshExpandedState();
        }

        private void ApplyTint()
        {
            var tint = Data.Tint;
            var headerTint = new Color(
                Mathf.Clamp01(tint.r * 1.08f),
                Mathf.Clamp01(tint.g * 1.08f),
                Mathf.Clamp01(tint.b * 1.08f),
                Mathf.Clamp01(Mathf.Max(tint.a, 0.42f)));

            mainContainer.style.backgroundColor = tint;
            _headerBand.style.backgroundColor = headerTint;
            mainContainer.style.borderTopColor = headerTint;
            mainContainer.style.borderRightColor = headerTint;
            mainContainer.style.borderBottomColor = headerTint;
            mainContainer.style.borderLeftColor = headerTint;
            style.borderTopColor = headerTint;
            style.borderRightColor = headerTint;
            style.borderBottomColor = headerTint;
            style.borderLeftColor = headerTint;
        }

        private bool IsPointerInResizeZone(Vector2 localPointerPosition)
        {
            var currentRect = GetPosition();
            var width = currentRect.width <= 0f ? DialogueGraphView.CommentNodeInitialSize.x : currentRect.width;
            var height = currentRect.height <= 0f ? DialogueGraphView.CommentNodeInitialSize.y : currentRect.height;
            return localPointerPosition.x >= width - ResizeZoneSize &&
                   localPointerPosition.y >= height - ResizeZoneSize;
        }

        private void BeginResize(Vector2 localPointerPosition)
        {
            ResetPendingCommentSelect();
            _isResizing = true;
            _resizeStartArea = Data.Area.width <= 0f || Data.Area.height <= 0f
                ? new Rect(Data.Position, DialogueGraphView.CommentNodeInitialSize)
                : Data.Area;
            _resizeStartCanvasPointer = _graphView.WorldToCanvasPosition(this.LocalToWorld(localPointerPosition));
            this.CaptureMouse();
        }

        private void ContinueResize(Vector2 localPointerPosition)
        {
            var currentCanvasPointer = _graphView.WorldToCanvasPosition(this.LocalToWorld(localPointerPosition));
            var delta = currentCanvasPointer - _resizeStartCanvasPointer;
            var resizedArea = new Rect(
                _resizeStartArea.position,
                new Vector2(
                    Mathf.Max(MinCommentWidth, _resizeStartArea.width + delta.x),
                    Mathf.Max(MinCommentHeight, _resizeStartArea.height + delta.y)));

            base.SetPosition(resizedArea);
            SyncVisualSize(resizedArea.size);
            Data.Position = resizedArea.position;
            Data.Area = resizedArea;
            _graphView.NotifyNodeMoved();
        }

        private void SyncVisualSize(Vector2 size)
        {
            var width = Mathf.Max(MinCommentWidth, size.x <= 0f ? DialogueGraphView.CommentNodeInitialSize.x : size.x);
            var height = Mathf.Max(MinCommentHeight, size.y <= 0f ? DialogueGraphView.CommentNodeInitialSize.y : size.y);
            var headerHeight = Mathf.Max(FallbackHeaderHeight, _headerBand.resolvedStyle.height);
            var contentHeight = Mathf.Max(0f, height - headerHeight);

            style.width = width;
            style.height = height;
            mainContainer.style.width = width;
            mainContainer.style.height = height;
            topContainer.style.width = width;
            extensionContainer.style.width = width;
            extensionContainer.style.height = contentHeight;

            mainContainer.MarkDirtyRepaint();
            topContainer.MarkDirtyRepaint();
            extensionContainer.MarkDirtyRepaint();
            _resizeHandle.MarkDirtyRepaint();
        }

        private void CancelResize()
        {
            _isResizing = false;
            EnableInClassList(ResizeHotClassName, false);
            if (this.HasMouseCapture())
            {
                this.ReleaseMouse();
            }
        }

        private void ResetPendingCommentSelect()
        {
            _pendingCommentSelect = false;
            _commentDragExceededThreshold = false;
        }
    }
}

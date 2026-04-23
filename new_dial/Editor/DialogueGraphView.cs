using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor
{
    public class DialogueGraphView : GraphView
    {
        private readonly Dictionary<string, DialogueTextNodeView> _textNodeViews = new();
        private readonly Dictionary<string, DialogueCommentNodeView> _commentNodeViews = new();
        private DialogueGraphData _graph;
        private bool _isReloading;

        public DialogueGraphView()
        {
            style.flexGrow = 1f;
            Insert(0, new GridBackground());
            this.StretchToParentSize();
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            graphViewChanged = OnGraphViewChanged;
            RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        public Action<BaseNodeData> SelectionChangedAction { get; set; }
        public Action GraphChangedAction { get; set; }

        public DialogueGraphData Graph => _graph;

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList().Where(port =>
                port != startPort &&
                port.direction != startPort.direction &&
                port.node != startPort.node).ToList();
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

                if (_graph == null)
                {
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

                foreach (var link in _graph.Links.Where(link => !string.IsNullOrWhiteSpace(link.ToNodeId)))
                {
                    if (!_textNodeViews.TryGetValue(link.FromNodeId, out var fromNode))
                    {
                        continue;
                    }

                    if (!_textNodeViews.TryGetValue(link.ToNodeId, out var toNode))
                    {
                        continue;
                    }

                    if (!fromNode.TryGetOutputPort(link.Id, out var outputPort))
                    {
                        continue;
                    }

                    var edge = new Edge
                    {
                        output = outputPort,
                        input = toNode.InputPort
                    };

                    edge.output.Connect(edge);
                    edge.input.Connect(edge);
                    AddElement(edge);
                }

                RefreshNodeVisuals();
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
            CreateTextNodeView(node);
            if (_graph.Nodes.OfType<DialogueTextNodeData>().Count() == 1)
            {
                DialogueGraphUtility.EnsureSingleStartNode(_graph, node.Id);
            }

            RefreshNodeVisuals();
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
            CreateCommentNodeView(node);
            MarkChanged();
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
        }

        public void RefreshLinksForNode(string nodeId)
        {
            if (_graph == null)
            {
                return;
            }

            if (_textNodeViews.TryGetValue(nodeId, out var view))
            {
                view.RebuildOutputPorts();
            }

            LoadGraph(_graph);
            MarkChanged();
        }

        public Vector2 GetCanvasCenter()
        {
            var scale = viewTransform.scale.x == 0f ? 1f : viewTransform.scale.x;
            var panOffset = new Vector2(viewTransform.position.x, viewTransform.position.y);
            return (layout.center - panOffset) / scale;
        }

        private void CreateTextNodeView(DialogueTextNodeData node)
        {
            var view = new DialogueTextNodeView(node, this);
            _textNodeViews[node.Id] = view;
            AddElement(view);
        }

        private void CreateCommentNodeView(CommentNodeData node)
        {
            var view = new DialogueCommentNodeView(node, this);
            _commentNodeViews[node.Id] = view;
            AddElement(view);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_graph == null || _isReloading)
            {
                return change;
            }

            if (change.edgesToCreate != null)
            {
                foreach (var edge in change.edgesToCreate)
                {
                    var linkData = edge.output?.userData as NodeLinkData;
                    if (linkData == null)
                    {
                        continue;
                    }

                    if (edge.input?.node is DialogueTextNodeView inputNode)
                    {
                        linkData.ToNodeId = inputNode.Data.Id;
                    }
                }
            }

            if (change.elementsToRemove != null)
            {
                foreach (var element in change.elementsToRemove)
                {
                    switch (element)
                    {
                        case Edge edge when edge.output?.userData is NodeLinkData linkData:
                            linkData.ToNodeId = string.Empty;
                            break;
                        case DialogueTextNodeView textNodeView:
                            DialogueGraphUtility.DeleteNode(_graph, textNodeView.Data.Id);
                            break;
                        case DialogueCommentNodeView commentNodeView:
                            DialogueGraphUtility.DeleteNode(_graph, commentNodeView.Data.Id);
                            break;
                    }
                }
            }

            RefreshNodeVisuals();
            MarkChanged();
            return change;
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.target == this || evt.target is GridBackground)
            {
                SelectionChangedAction?.Invoke(null);
            }
        }

        internal NodeLinkData CreateOutput(DialogueTextNodeData node)
        {
            var link = new NodeLinkData
            {
                FromNodeId = node.Id,
                Order = DialogueGraphUtility.GetOutgoingLinks(_graph, node.Id).Count
            };

            _graph.Links.Add(link);
            MarkChanged();
            return link;
        }

        internal void RemoveOutput(NodeLinkData link)
        {
            if (_graph == null || link == null)
            {
                return;
            }

            _graph.Links.RemoveAll(existing => existing.Id == link.Id);
            DialogueGraphUtility.NormalizeLinkOrder(_graph, link.FromNodeId);
            LoadGraph(_graph);
            MarkChanged();
        }

        internal void NotifySelected(BaseNodeData node)
        {
            SelectionChangedAction?.Invoke(node);
        }

        internal void NotifyNodeMoved()
        {
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
    }

    public class DialogueTextNodeView : Node
    {
        private readonly DialogueGraphView _graphView;
        private readonly Dictionary<string, Port> _outputPorts = new();

        public DialogueTextNodeView(DialogueTextNodeData data, DialogueGraphView graphView)
        {
            Data = data;
            _graphView = graphView;
            viewDataKey = data.Id;
            capabilities |= Capabilities.Deletable | Capabilities.Movable | Capabilities.Selectable;

            InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = "Input";
            inputContainer.Add(InputPort);

            titleButtonContainer.Add(new Button(() =>
            {
                _graphView.CreateOutput(Data);
                RebuildOutputPorts();
                _graphView.LoadGraph(_graphView.Graph);
            })
            {
                text = "+"
            });

            titleButtonContainer.Add(new Button(() =>
            {
                _graphView.DeleteNode(Data);
            })
            {
                text = "X"
            });

            RegisterCallback<MouseDownEvent>(_ => _graphView.NotifySelected(Data));
            RefreshFromData();
            RebuildOutputPorts();
        }

        public DialogueTextNodeData Data { get; }

        public Port InputPort { get; }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            Data.Position = newPos.position;
            _graphView.NotifyNodeMoved();
        }

        public void RefreshFromData()
        {
            title = Data.IsStartNode ? $"{Data.Title} [Start]" : Data.Title;
            base.SetPosition(new Rect(Data.Position, GetPosition().size == Vector2.zero ? new Vector2(260f, 180f) : GetPosition().size));
        }

        public void RebuildOutputPorts()
        {
            outputContainer.Clear();
            _outputPorts.Clear();

            foreach (var link in _graphView.GetOutgoingLinks(Data.Id))
            {
                var outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                outputPort.portName = $"Output {link.Order + 1}";
                outputPort.tooltip = string.IsNullOrWhiteSpace(link.ChoiceText) ? "Choice text is empty." : link.ChoiceText;
                outputPort.userData = link;
                outputContainer.Add(outputPort);
                _outputPorts[link.Id] = outputPort;
            }

            RefreshPorts();
            RefreshExpandedState();
        }

        public bool TryGetOutputPort(string linkId, out Port port)
        {
            return _outputPorts.TryGetValue(linkId, out port);
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

            titleButtonContainer.Add(new Button(() =>
            {
                _graphView.DeleteNode(Data);
            })
            {
                text = "X"
            });

            _commentLabel = new Label();
            _commentLabel.style.whiteSpace = WhiteSpace.Normal;
            mainContainer.Add(_commentLabel);

            RegisterCallback<MouseDownEvent>(_ => _graphView.NotifySelected(Data));
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
            title = Data.Title;
            _commentLabel.text = Data.Comment;
            base.SetPosition(Data.Area.width <= 0f || Data.Area.height <= 0f
                ? new Rect(Data.Position.x, Data.Position.y, 320f, 180f)
                : Data.Area);
        }
    }
}

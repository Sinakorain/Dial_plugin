using System.Linq;
using NUnit.Framework;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor.Tests
{
    public class DialogueGraphViewTests
    {
        [Test]
        public void LoadGraph_RebuildsRenderableLinkCountForConnectedLinks()
        {
            var graph = new DialogueGraphData();
            var start = new DialogueTextNodeData { Title = "Start" };
            var next = new DialogueTextNodeData { Title = "Next" };

            graph.Nodes.Add(start);
            graph.Nodes.Add(next);
            graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = next.Id,
                Order = 0
            });

            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            Assert.That(view.GetRenderedLinkCount(), Is.EqualTo(1));
        }

        [Test]
        public void CreateLink_AppendsOutgoingLinkWithNextOrder()
        {
            var graph = new DialogueGraphData();
            var start = new DialogueTextNodeData { Title = "Start" };
            var middle = new DialogueTextNodeData { Title = "Middle" };
            var end = new DialogueTextNodeData { Title = "End" };

            graph.Nodes.Add(start);
            graph.Nodes.Add(middle);
            graph.Nodes.Add(end);
            graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = middle.Id,
                Order = 0
            });

            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            var link = view.CreateLink(start.Id, end.Id, false);

            Assert.That(link, Is.Not.Null);
            Assert.That(graph.Links, Has.Count.EqualTo(2));
            Assert.That(graph.Links.Single(existing => existing.Id == link.Id).Order, Is.EqualTo(1));
            Assert.That(graph.Links.Single(existing => existing.Id == link.Id).ToNodeId, Is.EqualTo(end.Id));
        }

        [Test]
        public void DeleteLink_RemovesLinkAndNormalizesRemainingOrder()
        {
            var graph = new DialogueGraphData();
            var start = new DialogueTextNodeData { Title = "Start" };
            var left = new DialogueTextNodeData { Title = "Left" };
            var middle = new DialogueTextNodeData { Title = "Middle" };
            var right = new DialogueTextNodeData { Title = "Right" };

            graph.Nodes.Add(start);
            graph.Nodes.Add(left);
            graph.Nodes.Add(middle);
            graph.Nodes.Add(right);

            var first = new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = left.Id,
                Order = 0
            };
            var second = new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = middle.Id,
                Order = 1
            };
            var third = new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = right.Id,
                Order = 2
            };

            graph.Links.Add(first);
            graph.Links.Add(second);
            graph.Links.Add(third);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            var removed = view.DeleteLink(second, false, true);
            var remaining = DialogueGraphUtility.GetOutgoingLinks(graph, start.Id);

            Assert.That(removed, Is.True);
            Assert.That(remaining[0].Id, Is.EqualTo(first.Id));
            Assert.That(remaining[1].Id, Is.EqualTo(third.Id));
            Assert.That(remaining[0].Order, Is.EqualTo(0));
            Assert.That(remaining[1].Order, Is.EqualTo(1));
            Assert.That(view.GetRenderedLinkCount(), Is.EqualTo(2));
        }

        [Test]
        public void CreateTextNode_HidesEmptyStateWarning()
        {
            var view = new DialogueGraphView();
            view.LoadGraph(new DialogueGraphData());

            Assert.That(GetEmptyStateLabel(view).style.display, Is.EqualTo(DisplayStyle.Flex));

            view.CreateTextNode(new Vector2(100f, 100f));

            Assert.That(GetEmptyStateLabel(view).style.display, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void CreateCommentNode_HidesEmptyStateWarning()
        {
            var view = new DialogueGraphView();
            view.LoadGraph(new DialogueGraphData());

            Assert.That(GetEmptyStateLabel(view).style.display, Is.EqualTo(DisplayStyle.Flex));

            view.CreateCommentNode(new Vector2(100f, 100f));

            Assert.That(GetEmptyStateLabel(view).style.display, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void DeleteLastNode_ShowsEmptyStateWarningAgain()
        {
            var graph = new DialogueGraphData();
            var node = new DialogueTextNodeData
            {
                Title = "Only Node",
                Position = new Vector2(100f, 100f)
            };

            graph.Nodes.Add(node);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            Assert.That(GetEmptyStateLabel(view).style.display, Is.EqualTo(DisplayStyle.None));

            view.DeleteNode(node);

            Assert.That(graph.Nodes, Is.Empty);
            Assert.That(GetEmptyStateLabel(view).style.display, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void MovingCommentNode_UsesPreviousAreaToMoveContainedTextNodes()
        {
            var graph = new DialogueGraphData();
            var comment = new CommentNodeData
            {
                Title = "Group",
                Position = new Vector2(100f, 100f),
                Area = new Rect(100f, 100f, 420f, 260f)
            };
            var textNode = new DialogueTextNodeData
            {
                Title = "Inside",
                Position = new Vector2(105f, 105f)
            };

            graph.Nodes.Add(comment);
            graph.Nodes.Add(textNode);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            var commentView = view.graphElements
                .OfType<DialogueCommentNodeView>()
                .Single(nodeView => nodeView.Data.Id == comment.Id);
            var moveDelta = new Vector2(220f, 90f);
            var previousTextPosition = textNode.Position;

            commentView.SetPosition(new Rect(comment.Area.position + moveDelta, comment.Area.size));

            Assert.That(comment.Position, Is.EqualTo(new Vector2(320f, 190f)));
            Assert.That(comment.Area.position, Is.EqualTo(new Vector2(320f, 190f)));
            Assert.That(textNode.Position, Is.EqualTo(previousTextPosition + moveDelta));
        }

        [Test]
        public void MovingCommentNode_UsesPreviousAreaToMoveNestedCommentGroups()
        {
            var graph = new DialogueGraphData();
            var rootComment = new CommentNodeData
            {
                Title = "Root",
                Position = new Vector2(100f, 100f),
                Area = new Rect(100f, 100f, 520f, 320f)
            };
            var nestedComment = new CommentNodeData
            {
                Title = "Nested",
                Position = new Vector2(110f, 110f),
                Area = new Rect(110f, 110f, 260f, 160f)
            };
            var textNode = new DialogueTextNodeData
            {
                Title = "Nested Text",
                Position = new Vector2(115f, 115f)
            };

            graph.Nodes.Add(rootComment);
            graph.Nodes.Add(nestedComment);
            graph.Nodes.Add(textNode);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            var rootCommentView = view.graphElements
                .OfType<DialogueCommentNodeView>()
                .Single(nodeView => nodeView.Data.Id == rootComment.Id);
            var moveDelta = new Vector2(250f, 100f);
            var previousNestedPosition = nestedComment.Position;
            var previousNestedAreaPosition = nestedComment.Area.position;
            var previousTextPosition = textNode.Position;

            rootCommentView.SetPosition(new Rect(rootComment.Area.position + moveDelta, rootComment.Area.size));

            Assert.That(rootComment.Position, Is.EqualTo(new Vector2(350f, 200f)));
            Assert.That(nestedComment.Position, Is.EqualTo(previousNestedPosition + moveDelta));
            Assert.That(nestedComment.Area.position, Is.EqualTo(previousNestedAreaPosition + moveDelta));
            Assert.That(textNode.Position, Is.EqualTo(previousTextPosition + moveDelta));
        }

        [Test]
        public void SelectingCommentGroup_DoesNotPreventMovingContainedTextNodes()
        {
            var graph = new DialogueGraphData();
            var comment = new CommentNodeData
            {
                Title = "Group",
                Position = new Vector2(100f, 100f),
                Area = new Rect(100f, 100f, 420f, 260f)
            };
            var textNode = new DialogueTextNodeData
            {
                Title = "Inside",
                Position = new Vector2(105f, 105f)
            };

            graph.Nodes.Add(comment);
            graph.Nodes.Add(textNode);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            view.SelectCommentGroup(comment);

            var commentView = view.graphElements
                .OfType<DialogueCommentNodeView>()
                .Single(nodeView => nodeView.Data.Id == comment.Id);
            var moveDelta = new Vector2(160f, 80f);
            var previousTextPosition = textNode.Position;

            commentView.SetPosition(new Rect(comment.Area.position + moveDelta, comment.Area.size));

            Assert.That(comment.Position, Is.EqualTo(new Vector2(260f, 180f)));
            Assert.That(textNode.Position, Is.EqualTo(previousTextPosition + moveDelta));
        }

        [Test]
        public void SelectCommentGroup_AddsContainedNodesToSelection()
        {
            var graph = new DialogueGraphData();
            var rootComment = new CommentNodeData
            {
                Title = "Root",
                Position = new Vector2(100f, 100f),
                Area = new Rect(100f, 100f, 520f, 320f)
            };
            var nestedComment = new CommentNodeData
            {
                Title = "Nested",
                Position = new Vector2(110f, 110f),
                Area = new Rect(110f, 110f, 260f, 160f)
            };
            var textNode = new DialogueTextNodeData
            {
                Title = "Inside",
                Position = new Vector2(115f, 115f)
            };

            graph.Nodes.Add(rootComment);
            graph.Nodes.Add(nestedComment);
            graph.Nodes.Add(textNode);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            view.SelectCommentGroup(rootComment);

            var selectedIds = view.selection
                .OfType<Node>()
                .Select(element => element switch
                {
                    DialogueTextNodeView textNodeView => textNodeView.Data.Id,
                    DialogueCommentNodeView commentNodeView => commentNodeView.Data.Id,
                    _ => null
                })
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();

            Assert.That(selectedIds, Is.EquivalentTo(new[]
            {
                rootComment.Id,
                nestedComment.Id,
                textNode.Id
            }));
        }

        [Test]
        public void GetDirectSelectedNodeIds_OmitsAutoSelectedCommentGroupChildren()
        {
            var graph = new DialogueGraphData();
            var rootComment = new CommentNodeData
            {
                Title = "Root",
                Position = new Vector2(100f, 100f),
                Area = new Rect(100f, 100f, 520f, 320f)
            };
            var nestedComment = new CommentNodeData
            {
                Title = "Nested",
                Position = new Vector2(110f, 110f),
                Area = new Rect(110f, 110f, 260f, 160f)
            };
            var groupedTextNode = new DialogueTextNodeData
            {
                Title = "Grouped",
                Position = new Vector2(115f, 115f)
            };
            var externalTextNode = new DialogueTextNodeData
            {
                Title = "External",
                Position = new Vector2(800f, 800f)
            };

            graph.Nodes.Add(rootComment);
            graph.Nodes.Add(nestedComment);
            graph.Nodes.Add(groupedTextNode);
            graph.Nodes.Add(externalTextNode);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            view.SelectCommentGroup(rootComment);

            var externalTextView = view.graphElements
                .OfType<DialogueTextNodeView>()
                .Single(nodeView => nodeView.Data.Id == externalTextNode.Id);
            view.AddToSelection(externalTextView);

            var directSelectedIds = view.GetDirectSelectedNodeIds(rootComment, rootComment.Area);

            Assert.That(directSelectedIds, Is.EquivalentTo(new[]
            {
                rootComment.Id,
                externalTextNode.Id
            }));
        }

        [Test]
        public void SelectingCommentGroup_DoesNotPreventMovingNestedCommentGroups()
        {
            var graph = new DialogueGraphData();
            var rootComment = new CommentNodeData
            {
                Title = "Root",
                Position = new Vector2(100f, 100f),
                Area = new Rect(100f, 100f, 520f, 320f)
            };
            var nestedComment = new CommentNodeData
            {
                Title = "Nested",
                Position = new Vector2(110f, 110f),
                Area = new Rect(110f, 110f, 260f, 160f)
            };
            var textNode = new DialogueTextNodeData
            {
                Title = "Nested Text",
                Position = new Vector2(115f, 115f)
            };

            graph.Nodes.Add(rootComment);
            graph.Nodes.Add(nestedComment);
            graph.Nodes.Add(textNode);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            view.SelectCommentGroup(rootComment);

            var rootCommentView = view.graphElements
                .OfType<DialogueCommentNodeView>()
                .Single(nodeView => nodeView.Data.Id == rootComment.Id);
            var moveDelta = new Vector2(180f, 95f);
            var previousNestedPosition = nestedComment.Position;
            var previousNestedAreaPosition = nestedComment.Area.position;
            var previousTextPosition = textNode.Position;

            rootCommentView.SetPosition(new Rect(rootComment.Area.position + moveDelta, rootComment.Area.size));

            Assert.That(rootComment.Position, Is.EqualTo(new Vector2(280f, 195f)));
            Assert.That(nestedComment.Position, Is.EqualTo(previousNestedPosition + moveDelta));
            Assert.That(nestedComment.Area.position, Is.EqualTo(previousNestedAreaPosition + moveDelta));
            Assert.That(textNode.Position, Is.EqualTo(previousTextPosition + moveDelta));
        }

        [Test]
        public void GetOwningCommentNode_PrefersMostSpecificComment()
        {
            var graph = new DialogueGraphData();
            var rootComment = new CommentNodeData
            {
                Title = "Root",
                Position = new Vector2(0f, 0f),
                Area = new Rect(0f, 0f, 900f, 900f)
            };
            var nestedComment = new CommentNodeData
            {
                Title = "Nested",
                Position = new Vector2(100f, 100f),
                Area = new Rect(100f, 100f, 400f, 400f)
            };
            var textNode = new DialogueTextNodeData
            {
                Title = "Nested Text",
                Position = new Vector2(150f, 150f)
            };

            graph.Nodes.Add(rootComment);
            graph.Nodes.Add(nestedComment);
            graph.Nodes.Add(textNode);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            Assert.That(view.GetOwningCommentNode(textNode), Is.EqualTo(nestedComment));
        }

        [Test]
        public void GetParentCommentNode_ReturnsDirectContainingComment()
        {
            var graph = new DialogueGraphData();
            var rootComment = new CommentNodeData
            {
                Title = "Root",
                Position = new Vector2(0f, 0f),
                Area = new Rect(0f, 0f, 900f, 900f)
            };
            var nestedComment = new CommentNodeData
            {
                Title = "Nested",
                Position = new Vector2(100f, 100f),
                Area = new Rect(100f, 100f, 400f, 400f)
            };

            graph.Nodes.Add(rootComment);
            graph.Nodes.Add(nestedComment);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            Assert.That(view.GetParentCommentNode(nestedComment), Is.EqualTo(rootComment));
        }

        [Test]
        public void GetOwningCommentNode_UsesAreaCenterDistanceAndGraphOrderTieBreakers()
        {
            var nearestCenterGraph = new DialogueGraphData();
            var leftComment = new CommentNodeData
            {
                Title = "Left",
                Position = new Vector2(0f, 0f),
                Area = new Rect(0f, 0f, 700f, 700f)
            };
            var rightComment = new CommentNodeData
            {
                Title = "Right",
                Position = new Vector2(200f, 0f),
                Area = new Rect(200f, 0f, 700f, 700f)
            };
            var nearestCenterText = new DialogueTextNodeData
            {
                Title = "Nearest Center",
                Position = new Vector2(160f, 150f)
            };

            nearestCenterGraph.Nodes.Add(leftComment);
            nearestCenterGraph.Nodes.Add(rightComment);
            nearestCenterGraph.Nodes.Add(nearestCenterText);

            var nearestCenterView = new DialogueGraphView();
            nearestCenterView.LoadGraph(nearestCenterGraph);

            Assert.That(nearestCenterView.GetOwningCommentNode(nearestCenterText), Is.EqualTo(leftComment));

            var graphOrderGraph = new DialogueGraphData();
            var firstComment = new CommentNodeData
            {
                Title = "First",
                Position = new Vector2(50f, 50f),
                Area = new Rect(50f, 50f, 650f, 650f)
            };
            var secondComment = new CommentNodeData
            {
                Title = "Second",
                Position = new Vector2(50f, 50f),
                Area = new Rect(50f, 50f, 650f, 650f)
            };
            var graphOrderText = new DialogueTextNodeData
            {
                Title = "Graph Order",
                Position = new Vector2(180f, 150f)
            };

            graphOrderGraph.Nodes.Add(firstComment);
            graphOrderGraph.Nodes.Add(secondComment);
            graphOrderGraph.Nodes.Add(graphOrderText);

            var graphOrderView = new DialogueGraphView();
            graphOrderView.LoadGraph(graphOrderGraph);

            Assert.That(graphOrderView.GetOwningCommentNode(graphOrderText), Is.EqualTo(firstComment));
        }

        [Test]
        public void CutSelectionToClipboard_RemovesEntireNestedCommentHierarchy()
        {
            var graph = new DialogueGraphData();
            var rootComment = new CommentNodeData
            {
                Title = "Root",
                Position = new Vector2(0f, 0f),
                Area = new Rect(0f, 0f, 900f, 900f)
            };
            var nestedComment = new CommentNodeData
            {
                Title = "Nested",
                Position = new Vector2(100f, 100f),
                Area = new Rect(100f, 100f, 400f, 400f)
            };
            var rootText = new DialogueTextNodeData
            {
                Title = "Root Text",
                Position = new Vector2(520f, 150f)
            };
            var nestedText = new DialogueTextNodeData
            {
                Title = "Nested Text",
                Position = new Vector2(150f, 150f)
            };
            var externalText = new DialogueTextNodeData
            {
                Title = "External",
                Position = new Vector2(1200f, 1200f)
            };

            graph.Nodes.Add(rootComment);
            graph.Nodes.Add(nestedComment);
            graph.Nodes.Add(rootText);
            graph.Nodes.Add(nestedText);
            graph.Nodes.Add(externalText);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            view.SelectCommentGroup(rootComment);

            Assert.That(view.CutSelectionToClipboard(), Is.True);
            Assert.That(graph.Nodes.Select(node => node.Id), Is.EquivalentTo(new[]
            {
                externalText.Id
            }));
        }

        [Test]
        public void StepKeyboardPan_MovesOnlyWhileCanvasIsFocused()
        {
            var view = new DialogueGraphView();
            view.LoadGraph(new DialogueGraphData());

            view.SetMovementKeyState(KeyCode.W, true);
            view.StepKeyboardPan(1f);
            Assert.That(view.viewTransform.position.y, Is.EqualTo(0f));

            view.FocusCanvas();
            view.StepKeyboardPan(1f);

            Assert.That(view.HasCanvasFocus, Is.True);
            Assert.That(view.viewTransform.position.y, Is.GreaterThan(0f));
        }

        [Test]
        public void ReleaseCanvasFocus_StopsFurtherKeyboardPan()
        {
            var view = new DialogueGraphView();
            view.LoadGraph(new DialogueGraphData());
            view.FocusCanvas();
            view.SetMovementKeyState(KeyCode.D, true);
            view.ReleaseCanvasFocus();

            view.StepKeyboardPan(1f);

            Assert.That(view.HasCanvasFocus, Is.False);
            Assert.That(view.viewTransform.position.x, Is.EqualTo(0f));
        }

        private static Label GetEmptyStateLabel(DialogueGraphView view)
        {
            return view.Q<Label>(className: "dialogue-graph-empty-state");
        }
    }
}

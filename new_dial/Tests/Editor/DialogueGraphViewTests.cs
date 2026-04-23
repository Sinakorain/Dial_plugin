using System.Linq;
using NUnit.Framework;
using UnityEngine;

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
    }
}

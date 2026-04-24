using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor.Tests
{
    public class DialogueGraphViewTests
    {
        [SetUp]
        public void SetUp()
        {
            DialogueEditorLanguageSettings.CurrentLanguage = DialogueEditorLanguage.English;
        }

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
        public void CreateExecutableNodes_AddsRenderableNodeTypes()
        {
            var graph = new DialogueGraphData();
            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            view.CreateFunctionNode(new Vector2(100f, 100f));
            view.CreateSceneNode(new Vector2(420f, 100f));
            view.CreateDebugNode(new Vector2(740f, 100f));

            Assert.That(graph.Nodes.OfType<FunctionNodeData>(), Has.Count.EqualTo(1));
            Assert.That(graph.Nodes.OfType<SceneNodeData>(), Has.Count.EqualTo(1));
            Assert.That(graph.Nodes.OfType<DebugNodeData>(), Has.Count.EqualTo(1));
            Assert.That(view.graphElements.OfType<DialogueExecutableNodeView>(), Has.Count.EqualTo(3));
        }

        [Test]
        public void LoadGraph_RendersLinksBetweenExecutableNodes()
        {
            var graph = new DialogueGraphData();
            var start = new DialogueTextNodeData { Title = "Start" };
            var function = new FunctionNodeData { Title = "Function" };
            var scene = new SceneNodeData { Title = "Scene" };
            graph.Nodes.Add(start);
            graph.Nodes.Add(function);
            graph.Nodes.Add(scene);
            graph.Links.Add(new NodeLinkData { FromNodeId = start.Id, ToNodeId = function.Id, Order = 0 });
            graph.Links.Add(new NodeLinkData { FromNodeId = function.Id, ToNodeId = scene.Id, Order = 0 });

            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            Assert.That(view.GetRenderedLinkCount(), Is.EqualTo(2));
        }

        [Test]
        public void SelectRuntimeNode_SelectsTextNodeAndNotifiesInspector()
        {
            var graph = new DialogueGraphData();
            var node = new DialogueTextNodeData { Title = "Selectable" };
            graph.Nodes.Add(node);

            var view = new DialogueGraphView();
            BaseNodeData selected = null;
            view.SelectionChangedAction = nodeData => selected = nodeData;
            view.LoadGraph(graph);
            var nodeView = view.graphElements.OfType<DialogueTextNodeView>().Single();

            view.SelectRuntimeNode(nodeView);

            Assert.That(view.selection, Does.Contain(nodeView));
            Assert.That(selected, Is.SameAs(node));
        }

        [Test]
        public void SelectRuntimeNode_SelectsExecutableNodeAndNotifiesInspector()
        {
            var graph = new DialogueGraphData();
            var node = new FunctionNodeData { Title = "Selectable Function" };
            graph.Nodes.Add(node);

            var view = new DialogueGraphView();
            BaseNodeData selected = null;
            view.SelectionChangedAction = nodeData => selected = nodeData;
            view.LoadGraph(graph);
            var nodeView = view.graphElements.OfType<DialogueExecutableNodeView>().Single();

            view.SelectRuntimeNode(nodeView);

            Assert.That(view.selection, Does.Contain(nodeView));
            Assert.That(selected, Is.SameAs(node));
        }

        [Test]
        public void BeginLinkDrag_AfterSelectingNode_PreservesSelection()
        {
            var graph = new DialogueGraphData();
            var node = new DialogueTextNodeData { Title = "Source" };
            graph.Nodes.Add(node);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            var nodeView = view.graphElements.OfType<DialogueTextNodeView>().Single();

            view.SelectRuntimeNode(nodeView);
            view.BeginLinkDrag(nodeView, new Vector2(100f, 100f));

            Assert.That(view.selection, Does.Contain(nodeView));
            Assert.That(view.IsLinkDragActiveForTests, Is.True);
        }

        [Test]
        public void RefreshNodeVisuals_UsesCurrentLanguageForNodeSummaries()
        {
            var graph = new DialogueGraphData();
            var node = new FunctionNodeData { Title = string.Empty };
            graph.Nodes.Add(node);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            var nodeView = view.graphElements.OfType<DialogueExecutableNodeView>().Single();

            DialogueEditorLanguageSettings.CurrentLanguage = DialogueEditorLanguage.Russian;
            view.RefreshNodeVisuals();

            Assert.That(nodeView.Q<Label>(className: "dialogue-node__body-preview")?.text, Is.EqualTo("Функция не выбрана"));
        }

        [Test]
        public void TextNodeVisuals_SanitizeRichTextBodyPreview()
        {
            var graph = new DialogueGraphData();
            var node = new DialogueTextNodeData
            {
                Title = "Rich",
                BodyText = "Hello <b>friend</b> <unknown>tag</unknown>"
            };
            graph.Nodes.Add(node);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            var nodeView = view.graphElements.OfType<DialogueTextNodeView>().Single();

            var preview = nodeView.Q<VisualElement>(className: "dialogue-node__body-preview");
            Assert.That(preview, Is.Not.Null);
            Assert.That(string.Concat(preview.Query<Label>(className: "dialogue-rich-text-run").ToList().Select(label => label.text)),
                Is.EqualTo("Hello friend <unknown>tag</unknown>"));
            Assert.That(preview.Query<Label>(className: "dialogue-rich-text-run").ToList()
                .Any(label => label.text == "friend" && label.style.unityFontStyleAndWeight.value == FontStyle.Bold), Is.True);
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
        public void StepKeyboardPan_UsesConstantScreenSpeedAcrossZoomLevels()
        {
            var firstView = new DialogueGraphView();
            firstView.LoadGraph(new DialogueGraphData());
            firstView.FocusCanvas();
            firstView.SetMovementKeyState(KeyCode.W, true);

            var secondView = new DialogueGraphView();
            secondView.LoadGraph(new DialogueGraphData());
            secondView.FocusCanvas();
            secondView.UpdateViewTransform(Vector3.zero, Vector3.one * 0.5f);
            secondView.SetMovementKeyState(KeyCode.W, true);

            firstView.StepKeyboardPan(0.01f);
            secondView.StepKeyboardPan(0.01f);

            Assert.That(secondView.viewTransform.position.y, Is.EqualTo(firstView.viewTransform.position.y).Within(0.01f));
        }

        [Test]
        public void StepKeyboardPan_ClampsLargeFrameDelta()
        {
            var view = new DialogueGraphView();
            view.LoadGraph(new DialogueGraphData());
            view.FocusCanvas();
            view.SetMovementKeyState(KeyCode.W, true);

            view.StepKeyboardPan(1f);

            Assert.That(view.viewTransform.position.y, Is.EqualTo(45f).Within(0.01f));
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

        [Test]
        public void CreateTextNode_UndoRedo_RestoresNodeCount()
        {
            var graph = new DialogueGraphData();
            var view = CreateUndoEnabledView(graph, out var database);

            view.CreateTextNode(new Vector2(100f, 100f));

            Assert.That(GetResolvedGraph(database).Nodes.OfType<DialogueTextNodeData>(), Has.Count.EqualTo(1));

            Undo.PerformUndo();
            Assert.That(GetResolvedGraph(database).Nodes, Is.Empty);

            Undo.PerformRedo();
            Assert.That(GetResolvedGraph(database).Nodes.OfType<DialogueTextNodeData>(), Has.Count.EqualTo(1));
        }

        [Test]
        public void CreateAndDeleteLink_UndoRedo_RestoresLinkCount()
        {
            var graph = new DialogueGraphData();
            var start = new DialogueTextNodeData { Title = "Start" };
            var end = new DialogueTextNodeData { Title = "End" };
            graph.Nodes.Add(start);
            graph.Nodes.Add(end);

            var view = CreateUndoEnabledView(graph, out var database);
            var link = view.CreateLink(start.Id, end.Id);

            Assert.That(GetResolvedGraph(database).Links, Has.Count.EqualTo(1));

            view.DeleteLink(link);
            Assert.That(GetResolvedGraph(database).Links, Is.Empty);

            Undo.PerformUndo();
            Assert.That(GetResolvedGraph(database).Links, Has.Count.EqualTo(1));

            Undo.PerformUndo();
            Assert.That(GetResolvedGraph(database).Links, Is.Empty);

            Undo.PerformRedo();
            Assert.That(GetResolvedGraph(database).Links, Has.Count.EqualTo(1));
        }

        [Test]
        public void MovingTextNode_UndoRedo_RestoresPosition()
        {
            var graph = new DialogueGraphData();
            var node = new DialogueTextNodeData
            {
                Title = "Mover",
                Position = new Vector2(100f, 100f)
            };
            graph.Nodes.Add(node);

            var view = CreateUndoEnabledView(graph, out var database);
            var textView = view.graphElements
                .OfType<DialogueTextNodeView>()
                .Single(nodeView => nodeView.Data.Id == node.Id);

            textView.SetPosition(new Rect(new Vector2(320f, 220f), DialogueGraphView.TextNodeInitialSize));
            view.EndUndoGesture();

            Assert.That(GetResolvedNode<DialogueTextNodeData>(database, node.Id).Position, Is.EqualTo(new Vector2(320f, 220f)));

            Undo.PerformUndo();
            Assert.That(GetResolvedNode<DialogueTextNodeData>(database, node.Id).Position, Is.EqualTo(new Vector2(100f, 100f)));

            Undo.PerformRedo();
            Assert.That(GetResolvedNode<DialogueTextNodeData>(database, node.Id).Position, Is.EqualTo(new Vector2(320f, 220f)));
        }

        [Test]
        public void MovingCommentGroup_UndoRedo_RestoresContainedNodePositions()
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
                Position = new Vector2(130f, 130f)
            };

            graph.Nodes.Add(comment);
            graph.Nodes.Add(textNode);

            var view = CreateUndoEnabledView(graph, out var database);
            var commentView = view.graphElements
                .OfType<DialogueCommentNodeView>()
                .Single(nodeView => nodeView.Data.Id == comment.Id);

            commentView.SetPosition(new Rect(new Vector2(260f, 220f), comment.Area.size));
            view.EndUndoGesture();

            Assert.That(GetResolvedNode<CommentNodeData>(database, comment.Id).Area.position, Is.EqualTo(new Vector2(260f, 220f)));
            Assert.That(GetResolvedNode<DialogueTextNodeData>(database, textNode.Id).Position, Is.EqualTo(new Vector2(290f, 250f)));

            Undo.PerformUndo();
            Assert.That(GetResolvedNode<CommentNodeData>(database, comment.Id).Area.position, Is.EqualTo(new Vector2(100f, 100f)));
            Assert.That(GetResolvedNode<DialogueTextNodeData>(database, textNode.Id).Position, Is.EqualTo(new Vector2(130f, 130f)));
        }

        [Test]
        public void ResizeCommentNode_UndoRedo_RestoresArea()
        {
            var graph = new DialogueGraphData();
            var comment = new CommentNodeData
            {
                Title = "Resizable",
                Position = new Vector2(100f, 100f),
                Area = new Rect(100f, 100f, 420f, 260f)
            };
            graph.Nodes.Add(comment);

            var view = CreateUndoEnabledView(graph, out var database);
            var commentView = view.graphElements
                .OfType<DialogueCommentNodeView>()
                .Single(nodeView => nodeView.Data.Id == comment.Id);

            commentView.ResizeTo(new Rect(comment.Area.position, new Vector2(620f, 410f)));
            view.EndUndoGesture();

            Assert.That(GetResolvedNode<CommentNodeData>(database, comment.Id).Area.size, Is.EqualTo(new Vector2(620f, 410f)));

            Undo.PerformUndo();
            Assert.That(GetResolvedNode<CommentNodeData>(database, comment.Id).Area.size, Is.EqualTo(new Vector2(420f, 260f)));

            Undo.PerformRedo();
            Assert.That(GetResolvedNode<CommentNodeData>(database, comment.Id).Area.size, Is.EqualTo(new Vector2(620f, 410f)));
        }

        [Test]
        public void CutSelectionToClipboard_UndoRedo_RestoresRemovedHierarchy()
        {
            var graph = new DialogueGraphData();
            var rootComment = new CommentNodeData
            {
                Title = "Root",
                Position = new Vector2(0f, 0f),
                Area = new Rect(0f, 0f, 900f, 900f)
            };
            var nestedText = new DialogueTextNodeData
            {
                Title = "Nested Text",
                Position = new Vector2(120f, 120f)
            };
            var externalText = new DialogueTextNodeData
            {
                Title = "External",
                Position = new Vector2(1200f, 1200f)
            };

            graph.Nodes.Add(rootComment);
            graph.Nodes.Add(nestedText);
            graph.Nodes.Add(externalText);

            var view = CreateUndoEnabledView(graph, out var database);
            view.SelectCommentGroup(rootComment);

            Assert.That(view.CutSelectionToClipboard(), Is.True);
            Assert.That(GetResolvedGraph(database).Nodes.Select(node => node.Id), Is.EquivalentTo(new[] { externalText.Id }));

            Undo.PerformUndo();
            Assert.That(GetResolvedGraph(database).Nodes.Select(node => node.Id), Is.EquivalentTo(new[]
            {
                rootComment.Id,
                nestedText.Id,
                externalText.Id
            }));

            Undo.PerformRedo();
            Assert.That(GetResolvedGraph(database).Nodes.Select(node => node.Id), Is.EquivalentTo(new[] { externalText.Id }));
        }

        private static Label GetEmptyStateLabel(DialogueGraphView view)
        {
            return view.Q<Label>(className: "dialogue-graph-empty-state");
        }

        private static DialogueGraphView CreateUndoEnabledView(DialogueGraphData graph, out DialogueDatabaseAsset database)
        {
            database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            var npc = new NpcEntry { Name = "NPC" };
            var dialogue = new DialogueEntry
            {
                Name = "Dialogue",
                Graph = graph
            };

            npc.Dialogues.Add(dialogue);
            database.Npcs.Add(npc);

            var activeUndoGroup = -1;
            var view = new DialogueGraphView
            {
                ApplyUndoableChangeAction = (actionName, mutate) =>
                {
                    Undo.IncrementCurrentGroup();
                    var group = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName(actionName);
                    Undo.RegisterCompleteObjectUndo(database, actionName);
                    mutate();
                    Undo.CollapseUndoOperations(group);
                },
                BeginUndoGestureAction = actionName =>
                {
                    if (activeUndoGroup != -1)
                    {
                        return;
                    }

                    Undo.IncrementCurrentGroup();
                    activeUndoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName(actionName);
                    Undo.RegisterCompleteObjectUndo(database, actionName);
                },
                EndUndoGestureAction = () =>
                {
                    if (activeUndoGroup == -1)
                    {
                        return;
                    }

                    var group = activeUndoGroup;
                    activeUndoGroup = -1;
                    Undo.CollapseUndoOperations(group);
                }
            };

            view.LoadGraph(dialogue.Graph);
            return view;
        }

        private static DialogueGraphData GetResolvedGraph(DialogueDatabaseAsset database)
        {
            return database.Npcs[0].Dialogues[0].Graph;
        }

        private static TNode GetResolvedNode<TNode>(DialogueDatabaseAsset database, string nodeId) where TNode : BaseNodeData
        {
            return GetResolvedGraph(database).Nodes.OfType<TNode>().Single(node => node.Id == nodeId);
        }
    }
}

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
        public void DeleteLinkAtCanvasPosition_RemovesHitLink()
        {
            var graph = new DialogueGraphData();
            var start = new DialogueTextNodeData
            {
                Title = "Start",
                Position = new Vector2(100f, 140f)
            };
            var end = new DialogueTextNodeData
            {
                Title = "End",
                Position = new Vector2(460f, 420f)
            };
            graph.Nodes.Add(start);
            graph.Nodes.Add(end);
            graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = end.Id,
                Order = 0
            });

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            var geometry = view.GetEdgeGeometriesForTests().Single();
            var canvasHitPosition = geometry.Start + view.EdgeLayerCanvasBoundsForTests.position;

            var deleted = view.DeleteLinkAtCanvasPosition(canvasHitPosition, registerUndo: false);

            Assert.That(deleted, Is.True);
            Assert.That(graph.Links, Is.Empty);
            Assert.That(view.GetRenderedLinkCount(), Is.EqualTo(0));
        }

        [Test]
        public void DeleteLinkAtCanvasPosition_IgnoresMiss()
        {
            var graph = new DialogueGraphData();
            var start = new DialogueTextNodeData
            {
                Title = "Start",
                Position = new Vector2(100f, 140f)
            };
            var end = new DialogueTextNodeData
            {
                Title = "End",
                Position = new Vector2(460f, 420f)
            };
            graph.Nodes.Add(start);
            graph.Nodes.Add(end);
            graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = end.Id,
                Order = 0
            });

            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            var deleted = view.DeleteLinkAtCanvasPosition(new Vector2(-500f, -500f), registerUndo: false);

            Assert.That(deleted, Is.False);
            Assert.That(graph.Links, Has.Count.EqualTo(1));
        }

        [Test]
        public void HoverLinkAtCanvasPosition_HighlightsLinkGeometry()
        {
            var graph = CreateSingleLinkGraph(out var link);
            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            var defaultGeometry = view.GetEdgeGeometriesForTests().Single();
            var canvasHitPosition = defaultGeometry.Start + view.EdgeLayerCanvasBoundsForTests.position;

            var changed = view.UpdateHoveredLinkForTests(canvasHitPosition);
            var hoveredGeometry = view.GetEdgeGeometriesForTests().Single();

            Assert.That(changed, Is.True);
            Assert.That(view.HoveredLinkIdForTests, Is.EqualTo(link.Id));
            Assert.That(hoveredGeometry.Thickness, Is.GreaterThan(defaultGeometry.Thickness));
            Assert.That(hoveredGeometry.Color, Is.Not.EqualTo(defaultGeometry.Color));
        }

        [Test]
        public void HoverLinkAtCanvasPosition_MissClearsHover()
        {
            var graph = CreateSingleLinkGraph(out _);
            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            var geometry = view.GetEdgeGeometriesForTests().Single();
            var canvasHitPosition = geometry.Start + view.EdgeLayerCanvasBoundsForTests.position;

            view.UpdateHoveredLinkForTests(canvasHitPosition);
            var changed = view.UpdateHoveredLinkForTests(new Vector2(-500f, -500f));
            var restoredGeometry = view.GetEdgeGeometriesForTests().Single();

            Assert.That(changed, Is.True);
            Assert.That(view.HoveredLinkIdForTests, Is.Null);
            Assert.That(restoredGeometry.Thickness, Is.EqualTo(geometry.Thickness));
            Assert.That(restoredGeometry.Color, Is.EqualTo(geometry.Color));
        }

        [Test]
        public void HoverLinkAtCanvasPosition_IsSuppressedDuringActiveLinkDrag()
        {
            var graph = CreateSingleLinkGraph(out _);
            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            var geometry = view.GetEdgeGeometriesForTests().Single();
            var canvasHitPosition = geometry.Start + view.EdgeLayerCanvasBoundsForTests.position;
            var sourceView = view.graphElements
                .OfType<DialogueTextNodeView>()
                .First();

            view.BeginLinkDrag(sourceView, sourceView.worldBound.center);
            var changed = view.UpdateHoveredLinkForTests(canvasHitPosition);

            Assert.That(changed, Is.False);
            Assert.That(view.HoveredLinkIdForTests, Is.Null);
        }

        [Test]
        public void DeleteHoveredLink_ClearsHoverState()
        {
            var graph = CreateSingleLinkGraph(out _);
            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            var geometry = view.GetEdgeGeometriesForTests().Single();
            var canvasHitPosition = geometry.Start + view.EdgeLayerCanvasBoundsForTests.position;

            view.UpdateHoveredLinkForTests(canvasHitPosition);
            var deleted = view.DeleteLinkAtCanvasPosition(canvasHitPosition, registerUndo: false);

            Assert.That(deleted, Is.True);
            Assert.That(view.HoveredLinkIdForTests, Is.Null);
            Assert.That(graph.Links, Is.Empty);
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
        public void CreateNodes_AssignsGlobalNumberedDefaultTitles()
        {
            var graph = new DialogueGraphData();
            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            view.CreateTextNode(new Vector2(100f, 100f));
            view.CreateFunctionNode(new Vector2(420f, 100f));
            view.CreateSceneNode(new Vector2(740f, 100f));
            view.CreateDebugNode(new Vector2(1060f, 100f));
            view.CreateCommentNode(new Vector2(1380f, 100f));
            view.CreateChoiceNode(new Vector2(1700f, 100f));

            Assert.That(graph.Nodes[0].Title, Is.EqualTo("Text Node_1"));
            Assert.That(graph.Nodes[1].Title, Is.EqualTo("Function_2"));
            Assert.That(graph.Nodes[2].Title, Is.EqualTo("Scene_3"));
            Assert.That(graph.Nodes[3].Title, Is.EqualTo("Debug_4"));
            Assert.That(graph.Nodes[4].Title, Is.EqualTo("Comment_5"));
            Assert.That(graph.Nodes[5].Title, Is.EqualTo("Answer_6"));
        }

        [Test]
        public void CreateChoiceNode_AddsRenderableAnswerNode()
        {
            var graph = new DialogueGraphData();
            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            view.CreateChoiceNode(new Vector2(100f, 100f));

            Assert.That(graph.Nodes.OfType<DialogueChoiceNodeData>(), Has.Count.EqualTo(1));
            Assert.That(view.graphElements.OfType<DialogueExecutableNodeView>(), Has.Count.EqualTo(1));
        }

        [Test]
        public void CreateChoiceBranch_CreatesOnlyPlayableAnswerNode()
        {
            var graph = new DialogueGraphData();
            var start = new DialogueTextNodeData { Title = "Question" };
            graph.Nodes.Add(start);
            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            view.CreateChoiceBranch(start);

            var answer = graph.Nodes.OfType<DialogueChoiceNodeData>().Single();
            Assert.That(start.UseOutputsAsChoices, Is.True);
            Assert.That(answer.Title, Is.EqualTo("Answer_2"));
            Assert.That(graph.Nodes.OfType<DialogueTextNodeData>(), Has.Count.EqualTo(1));
            Assert.That(graph.Links, Has.Count.EqualTo(1));
            Assert.That(graph.Links[0].FromNodeId, Is.EqualTo(start.Id));
            Assert.That(graph.Links[0].ToNodeId, Is.EqualTo(answer.Id));
        }

        [Test]
        public void AnswerNodeVisuals_EditButtonTextAndBodyInline()
        {
            var graph = new DialogueGraphData();
            var answer = new DialogueChoiceNodeData
            {
                Title = "Answer",
                ChoiceText = "Ask",
                BodyText = "What happened?"
            };
            graph.Nodes.Add(answer);
            var view = new DialogueGraphView();
            view.SpeakerNameResolver = _ => "NPC 1";
            view.LoadGraph(graph);

            var nodeView = view.graphElements.OfType<DialogueExecutableNodeView>().Single();
            var titleField = nodeView.Q<TextField>("runtime-node-header-title-field");
            var buttonField = nodeView.Q<TextField>("answer-node-inline-button-text-field");
            var bodyField = nodeView.Q<TextField>("answer-node-inline-body-field");
            var metaLabel = nodeView.Q<Label>(className: "dialogue-node__meta");
            Assert.That(titleField, Is.Not.Null);
            Assert.That(buttonField, Is.Not.Null);
            Assert.That(bodyField, Is.Not.Null);
            Assert.That(metaLabel, Is.Not.Null);
            Assert.That(titleField.value, Is.EqualTo("Answer"));
            Assert.That(buttonField.value, Is.EqualTo("Ask"));
            Assert.That(bodyField.value, Is.EqualTo("What happened?"));
            Assert.That(metaLabel.text, Is.EqualTo("NPC 1 | No next"));

            titleField.value = "Answer Header";
            buttonField.value = "Ask about work";
            bodyField.value = "The mill needs help.";
            Assert.That(answer.Title, Is.EqualTo("Answer Header"));
            Assert.That(answer.ChoiceText, Is.EqualTo("Ask about work"));
            Assert.That(answer.BodyText, Is.EqualTo("The mill needs help."));
            Assert.That(nodeView.GetPosition().width, Is.EqualTo(DialogueGraphView.TextNodeInitialSize.x));
        }

        [Test]
        public void AnswerNodeVisuals_LongButtonTextSoftWrapsWithoutStoredNewlines()
        {
            var graph = new DialogueGraphData();
            var longButtonText = new string('a', 90);
            var answer = new DialogueChoiceNodeData
            {
                Title = "Answer",
                ChoiceText = longButtonText,
                BodyText = "Body"
            };
            graph.Nodes.Add(answer);
            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            var nodeView = view.graphElements.OfType<DialogueExecutableNodeView>().Single();
            var buttonField = nodeView.Q<TextField>("answer-node-inline-button-text-field");

            Assert.That(buttonField, Is.Not.Null);
            Assert.That(buttonField.multiline, Is.True);
            Assert.That(buttonField.style.whiteSpace.value, Is.EqualTo(WhiteSpace.Normal));
            Assert.That(buttonField.value, Is.EqualTo(longButtonText));
            Assert.That(answer.ChoiceText, Is.EqualTo(longButtonText));
            Assert.That(answer.ChoiceText, Does.Not.Contain("\n"));
            Assert.That(nodeView.GetPosition().width, Is.EqualTo(DialogueGraphView.TextNodeInitialSize.x));
            Assert.That(nodeView.GetPosition().height, Is.GreaterThanOrEqualTo(DialogueGraphView.TextNodeInitialSize.y));
        }

        [Test]
        public void CreateNodeFromPaletteShortcut_OffsetsFromOccupiedFallbackPoint()
        {
            var graph = new DialogueGraphData();
            var existingNode = new DialogueTextNodeData
            {
                Title = "Existing",
                Position = new Vector2(-140f, -85f)
            };
            graph.Nodes.Add(existingNode);
            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            view.CreateNodeFromPaletteShortcut(DialoguePaletteItemType.TextNode);

            var createdNode = graph.Nodes.OfType<DialogueTextNodeData>().Last();
            Assert.That(createdNode.Id, Is.Not.EqualTo(existingNode.Id));
            Assert.That(createdNode.Position, Is.Not.EqualTo(existingNode.Position));
            Assert.That(
                new Rect(createdNode.Position, DialogueGraphView.TextNodeInitialSize)
                    .Overlaps(new Rect(existingNode.Position, DialogueGraphView.TextNodeInitialSize)),
                Is.False);
        }

        [Test]
        public void PaletteShortcutPlacement_ClampsNodeIntoVisibleViewport()
        {
            var clamped = DialogueGraphView.ClampPaletteShortcutPlacementToViewport(
                new Vector2(900f, 700f),
                DialogueGraphView.TextNodeInitialSize,
                new Rect(0f, 0f, 1000f, 800f));

            Assert.That(clamped, Is.EqualTo(new Vector2(720f, 630f)));
        }

        [Test]
        public void PaletteShortcutPlacement_FindsVisibleOffsetAroundOccupiedPosition()
        {
            var size = DialogueGraphView.TextNodeInitialSize;
            var occupied = new[]
            {
                new Rect(new Vector2(360f, 300f), size)
            };

            var placement = DialogueGraphView.FindVisibleNonOverlappingPaletteShortcutPlacement(
                new Vector2(360f, 300f),
                size,
                new Rect(0f, 0f, 1000f, 800f),
                occupied);

            var placementRect = new Rect(placement, size);
            Assert.That(placementRect.Overlaps(occupied[0]), Is.False);
            Assert.That(placementRect.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(placementRect.yMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(placementRect.xMax, Is.LessThanOrEqualTo(1000f));
            Assert.That(placementRect.yMax, Is.LessThanOrEqualTo(800f));
        }

        [Test]
        public void PaletteShortcutPlacement_NearViewportEdgeOffsetsInVisibleDirection()
        {
            var size = DialogueGraphView.TextNodeInitialSize;
            var preferred = new Vector2(720f, 630f);
            var occupied = new[]
            {
                new Rect(preferred, size)
            };

            var placement = DialogueGraphView.FindVisibleNonOverlappingPaletteShortcutPlacement(
                preferred,
                size,
                new Rect(0f, 0f, 1000f, 800f),
                occupied);

            var placementRect = new Rect(placement, size);
            Assert.That(placementRect.Overlaps(occupied[0]), Is.False);
            Assert.That(placementRect.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(placementRect.yMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(placementRect.xMax, Is.LessThanOrEqualTo(1000f));
            Assert.That(placementRect.yMax, Is.LessThanOrEqualTo(800f));
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
        public void RuntimeNodeSingleClickRequestsInspector()
        {
            var graph = new DialogueGraphData();
            var node = new DialogueTextNodeData { Title = "Selectable" };
            graph.Nodes.Add(node);

            var view = new DialogueGraphView();
            BaseNodeData requested = null;
            view.NodeInspectorRequestedAction = nodeData => requested = nodeData;
            view.LoadGraph(graph);
            var nodeView = view.graphElements.OfType<DialogueTextNodeView>().Single();
            var titleField = nodeView.Q<TextField>("text-node-header-title-field");

            SendMouseDown(titleField, 1);
            Assert.That(requested, Is.SameAs(node));

            requested = null;
            SendMouseDown(nodeView, 1);
            Assert.That(requested, Is.SameAs(node));
        }

        [Test]
        public void ExecutableNodeSingleClickRequestsInspector()
        {
            var graph = new DialogueGraphData();
            var nodes = new BaseNodeData[]
            {
                new DialogueChoiceNodeData { Title = "Answer", ChoiceText = "Hello" },
                new FunctionNodeData { Title = "Function" },
                new SceneNodeData { Title = "Scene" },
                new DebugNodeData { Title = "Debug" }
            };
            graph.Nodes.AddRange(nodes);

            var view = new DialogueGraphView();
            BaseNodeData requested = null;
            view.NodeInspectorRequestedAction = nodeData => requested = nodeData;
            view.LoadGraph(graph);

            foreach (var node in nodes)
            {
                requested = null;
                var nodeView = view.graphElements
                    .OfType<DialogueExecutableNodeView>()
                    .Single(element => element.Data == node);

                SendMouseDown(nodeView, 1);

                Assert.That(requested, Is.SameAs(node));
            }
        }

        [Test]
        public void ExecutableInlineFieldsSingleClickRequestInspector()
        {
            var graph = new DialogueGraphData();
            var node = new DialogueChoiceNodeData { Title = "Answer", ChoiceText = "Hello", BodyText = "Body" };
            graph.Nodes.Add(node);

            var view = new DialogueGraphView();
            BaseNodeData requested = null;
            view.NodeInspectorRequestedAction = nodeData => requested = nodeData;
            view.LoadGraph(graph);
            var nodeView = view.graphElements.OfType<DialogueExecutableNodeView>().Single();
            var titleField = nodeView.Q<TextField>("runtime-node-header-title-field");
            var choiceField = nodeView.Q<TextField>("answer-node-inline-button-text-field");
            var bodyField = nodeView.Q<TextField>("answer-node-inline-body-field");

            SendMouseDown(titleField, 1);
            Assert.That(requested, Is.SameAs(node));

            requested = null;
            SendMouseDown(choiceField, 1);
            Assert.That(requested, Is.SameAs(node));

            requested = null;
            SendMouseDown(bodyField, 1);
            Assert.That(requested, Is.SameAs(node));
        }

        [Test]
        public void CommentNodeSingleClickRequestsInspector()
        {
            var graph = new DialogueGraphData();
            var node = new CommentNodeData { Title = "Comment" };
            graph.Nodes.Add(node);

            var view = new DialogueGraphView();
            BaseNodeData requested = null;
            view.NodeInspectorRequestedAction = nodeData => requested = nodeData;
            view.LoadGraph(graph);
            var nodeView = view.graphElements.OfType<DialogueCommentNodeView>().Single();
            var titleField = nodeView.Q<TextField>("comment-node-header-title-field");

            SendMouseDown(titleField, 1);
            Assert.That(requested, Is.SameAs(node));

            requested = null;
            SendMouseDown(nodeView, 1);
            Assert.That(requested, Is.SameAs(node));
        }

        [Test]
        public void InlineTitleAndTextFields_DoNotStartLinkDrag()
        {
            var graph = new DialogueGraphData();
            var node = new DialogueTextNodeData { Title = "Source", BodyText = "Body" };
            graph.Nodes.Add(node);

            var view = new DialogueGraphView();
            BaseNodeData requested = null;
            view.NodeInspectorRequestedAction = nodeData => requested = nodeData;
            view.LoadGraph(graph);
            var nodeView = view.graphElements.OfType<DialogueTextNodeView>().Single();
            var titleField = nodeView.Q<TextField>("text-node-header-title-field");
            var bodyField = nodeView.Q<TextField>("text-node-inline-body-field");

            SendMouseDown(titleField, 1);
            Assert.That(view.IsLinkDragActiveForTests, Is.False);
            Assert.That(requested, Is.SameAs(node));

            requested = null;
            SendMouseDown(bodyField, 1);
            Assert.That(view.IsLinkDragActiveForTests, Is.False);
            Assert.That(requested, Is.SameAs(node));
        }

        [Test]
        public void InlineTextFieldWASD_DoesNotPanCanvas()
        {
            var graph = new DialogueGraphData();
            var node = new DialogueTextNodeData { Title = "Source", BodyText = "Body" };
            graph.Nodes.Add(node);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            view.FocusCanvas();
            var nodeView = view.graphElements.OfType<DialogueTextNodeView>().Single();
            var bodyField = nodeView.Q<TextField>("text-node-inline-body-field");

            SendKeyDown(bodyField, KeyCode.W);
            view.StepKeyboardPan(1f);

            Assert.That(view.viewTransform.position.y, Is.EqualTo(0f));
        }

        [Test]
        public void WasdKeyDown_OnFocusedCanvas_StartsGraphPan()
        {
            var view = new DialogueGraphView();
            view.LoadGraph(new DialogueGraphData());
            view.FocusCanvas();
            view.UpdateViewTransform(Vector3.zero, Vector3.one * 0.5f);
            var initialScale = view.viewTransform.scale;

            SendKeyDown(view, KeyCode.W);
            view.StepKeyboardPan(1f);

            Assert.That(view.viewTransform.position.y, Is.GreaterThan(0f));
            Assert.That(view.viewTransform.scale, Is.EqualTo(initialScale));
        }

        [Test]
        public void WasdRepeatedKeyDown_DoesNotChangeZoom()
        {
            var view = new DialogueGraphView();
            view.LoadGraph(new DialogueGraphData());
            view.FocusCanvas();
            view.UpdateViewTransform(Vector3.zero, Vector3.one * 1.75f);
            var initialScale = view.viewTransform.scale;

            SendKeyDown(view, KeyCode.W);
            SendKeyDown(view, KeyCode.W);
            view.StepKeyboardPan(1f);
            view.StepKeyboardPan(1f);

            Assert.That(view.viewTransform.position.y, Is.GreaterThan(0f));
            Assert.That(view.viewTransform.scale, Is.EqualTo(initialScale));
        }

        [Test]
        public void WasdLeft_OnZoomedOutCanvas_PansWithoutChangingZoom()
        {
            var view = new DialogueGraphView();
            view.LoadGraph(new DialogueGraphData());
            view.FocusCanvas();
            view.UpdateViewTransform(Vector3.zero, Vector3.one * 0.5f);
            var initialScale = view.viewTransform.scale;

            var keyState = SendKeyDownWithState(view, KeyCode.A);
            view.StepKeyboardPan(1f);

            Assert.That(keyState.DefaultPrevented, Is.True);
            Assert.That(keyState.ImmediatePropagationStopped, Is.True);
            Assert.That(view.viewTransform.position.x, Is.GreaterThan(0f));
            Assert.That(view.viewTransform.scale, Is.EqualTo(initialScale));
        }

        [Test]
        public void WasdLeftDown_OnZoomedOutCanvas_PansDiagonallyWithoutChangingZoom()
        {
            var view = new DialogueGraphView();
            view.LoadGraph(new DialogueGraphData());
            view.FocusCanvas();
            view.UpdateViewTransform(Vector3.zero, Vector3.one * 0.5f);
            var initialScale = view.viewTransform.scale;

            var leftKeyState = SendKeyDownWithState(view, KeyCode.A);
            var downKeyState = SendKeyDownWithState(view, KeyCode.S);
            view.StepKeyboardPan(1f);
            view.StepKeyboardPan(1f);

            Assert.That(leftKeyState.DefaultPrevented, Is.True);
            Assert.That(downKeyState.DefaultPrevented, Is.True);
            Assert.That(view.viewTransform.position.x, Is.GreaterThan(0f));
            Assert.That(view.viewTransform.position.y, Is.LessThan(0f));
            Assert.That(view.viewTransform.scale, Is.EqualTo(initialScale));
        }

        [Test]
        public void WasdLeftRepeatedKeyDown_OnZoomedOutCanvas_DoesNotChangeZoom()
        {
            var view = new DialogueGraphView();
            view.LoadGraph(new DialogueGraphData());
            view.FocusCanvas();
            view.UpdateViewTransform(Vector3.zero, Vector3.one * 0.5f);
            var initialScale = view.viewTransform.scale;

            var firstKeyState = SendKeyDownWithState(view, KeyCode.A);
            var secondKeyState = SendKeyDownWithState(view, KeyCode.A);
            view.StepKeyboardPan(1f);
            view.StepKeyboardPan(1f);

            Assert.That(firstKeyState.DefaultPrevented, Is.True);
            Assert.That(secondKeyState.DefaultPrevented, Is.True);
            Assert.That(view.viewTransform.position.x, Is.GreaterThan(0f));
            Assert.That(view.viewTransform.scale, Is.EqualTo(initialScale));
        }

        [Test]
        public void WasdLeftKeyUp_AfterZoomedOutPan_StopsFurtherMovement()
        {
            var view = new DialogueGraphView();
            view.LoadGraph(new DialogueGraphData());
            view.FocusCanvas();
            view.UpdateViewTransform(Vector3.zero, Vector3.one * 0.5f);
            var initialScale = view.viewTransform.scale;

            SendKeyDown(view, KeyCode.A);
            view.StepKeyboardPan(1f);
            var panAfterKeyDown = view.viewTransform.position.x;

            var keyState = SendKeyUpWithState(view, KeyCode.A);
            view.StepKeyboardPan(1f);

            Assert.That(keyState.DefaultPrevented, Is.True);
            Assert.That(keyState.ImmediatePropagationStopped, Is.True);
            Assert.That(panAfterKeyDown, Is.GreaterThan(0f));
            Assert.That(view.viewTransform.position.x, Is.EqualTo(panAfterKeyDown).Within(0.01f));
            Assert.That(view.viewTransform.scale, Is.EqualTo(initialScale));
        }

        [TestCase(EventModifiers.Alt)]
        [TestCase(EventModifiers.Command)]
        [TestCase(EventModifiers.Control)]
        [TestCase(EventModifiers.Shift)]
        public void WasdWithModifiers_ClearsMovementStateConsumesEventAndDoesNotPan(EventModifiers modifiers)
        {
            var view = new DialogueGraphView();
            view.LoadGraph(new DialogueGraphData());
            view.FocusCanvas();
            view.SetMovementKeyState(KeyCode.W, true);

            var keyState = SendKeyDownWithState(view, KeyCode.W, modifiers);
            view.StepKeyboardPan(1f);

            Assert.That(keyState.DefaultPrevented, Is.True);
            Assert.That(keyState.ImmediatePropagationStopped, Is.True);
            Assert.That(view.viewTransform.position.y, Is.EqualTo(0f));
        }

        [Test]
        public void WasdKeyDown_AfterInlineFieldFocus_ReturnsWhenCanvasFocuses()
        {
            var graph = new DialogueGraphData();
            var node = new DialogueTextNodeData { Title = "Source", BodyText = "Body" };
            graph.Nodes.Add(node);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            view.FocusCanvas();
            var nodeView = view.graphElements.OfType<DialogueTextNodeView>().Single();
            var bodyField = nodeView.Q<TextField>("text-node-inline-body-field");

            SendKeyDown(bodyField, KeyCode.W);
            view.StepKeyboardPan(1f);
            Assert.That(view.viewTransform.position.y, Is.EqualTo(0f));

            view.FocusCanvas();
            SendKeyDown(view, KeyCode.W);
            view.StepKeyboardPan(1f);

            Assert.That(view.viewTransform.position.y, Is.GreaterThan(0f));
        }

        [Test]
        public void WasdKeyUp_FromInlineFieldClearsMovementState()
        {
            var graph = new DialogueGraphData();
            var node = new DialogueTextNodeData { Title = "Source", BodyText = "Body" };
            graph.Nodes.Add(node);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            view.FocusCanvas();
            view.SetMovementKeyState(KeyCode.W, true);
            var nodeView = view.graphElements.OfType<DialogueTextNodeView>().Single();
            var bodyField = nodeView.Q<TextField>("text-node-inline-body-field");

            SendKeyUp(bodyField, KeyCode.W);
            view.StepKeyboardPan(1f);

            Assert.That(view.viewTransform.position.y, Is.EqualTo(0f));
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
        public void TextNodeVisuals_ShowEditableHeaderTitleAndInlineBody()
        {
            var graph = new DialogueGraphData();
            var node = new DialogueTextNodeData
            {
                Title = "Rich",
                BodyText = "Hello <b>friend</b> <color=#00ff00>green</color> <mark=#FFE06680>clue</mark>"
            };
            graph.Nodes.Add(node);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            var nodeView = view.graphElements.OfType<DialogueTextNodeView>().Single();

            var bodyTitleField = nodeView.Q<TextField>("text-node-inline-title-field");
            var titleField = nodeView.Q<TextField>("text-node-header-title-field");
            var bodyField = nodeView.Q<TextField>("text-node-inline-body-field");
            Assert.That(bodyTitleField, Is.Null);
            Assert.That(titleField, Is.Not.Null);
            Assert.That(titleField.ClassListContains("dialogue-node__header-title-field"), Is.True);
            Assert.That(bodyField, Is.Not.Null);
            Assert.That(titleField.value, Is.EqualTo("Rich"));
            Assert.That(bodyField.value, Is.EqualTo("Hello <b>friend</b> <color=#00ff00>green</color> <mark=#FFE06680>clue</mark>"));

            titleField.value = "Updated";
            bodyField.value = "Updated <b>body</b>";
            Assert.That(node.Title, Is.EqualTo("Updated"));
            Assert.That(node.BodyText, Is.EqualTo("Updated <b>body</b>"));
        }

        [Test]
        public void TextNodeVisuals_AnswerLinkWithoutChoiceModeShowsAnswerSummary()
        {
            var graph = new DialogueGraphData();
            var node = new DialogueTextNodeData
            {
                Title = "Question"
            };
            var answer = new DialogueChoiceNodeData
            {
                ChoiceText = "Ask"
            };
            graph.Nodes.Add(node);
            graph.Nodes.Add(answer);
            graph.Links.Add(new NodeLinkData { FromNodeId = node.Id, ToNodeId = answer.Id, Order = 0 });

            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            var nodeView = view.graphElements.OfType<DialogueTextNodeView>().Single();
            var metaLabel = nodeView.Q<Label>(className: "dialogue-node__meta");

            Assert.That(metaLabel, Is.Not.Null);
            Assert.That(metaLabel.text, Is.EqualTo("1 answer"));
        }

        [Test]
        public void RuntimeNodeHeaderTitleEdits_AllExecutableTypes()
        {
            var graph = new DialogueGraphData();
            var nodes = new BaseNodeData[]
            {
                new FunctionNodeData { Title = "Function" },
                new SceneNodeData { Title = "Scene" },
                new DebugNodeData { Title = "Debug" }
            };
            graph.Nodes.AddRange(nodes);
            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            foreach (var node in nodes)
            {
                var nodeView = view.graphElements
                    .OfType<DialogueExecutableNodeView>()
                    .Single(element => element.Data.Id == node.Id);
                var titleField = nodeView.Q<TextField>("runtime-node-header-title-field");

                Assert.That(titleField, Is.Not.Null);
                Assert.That(titleField.value, Is.EqualTo(node.Title));

                titleField.value = $"{node.Title} Updated";

                Assert.That(node.Title, Does.EndWith(" Updated"));
            }
        }

        [Test]
        public void CommentNodeVisuals_EditHeaderTitleInline()
        {
            var graph = new DialogueGraphData();
            var comment = new CommentNodeData
            {
                Title = "Group",
                Position = new Vector2(100f, 100f),
                Area = new Rect(100f, 100f, 420f, 260f)
            };
            graph.Nodes.Add(comment);
            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            var commentView = view.graphElements.OfType<DialogueCommentNodeView>().Single();
            var titleField = commentView.Q<TextField>("comment-node-header-title-field");

            Assert.That(titleField, Is.Not.Null);
            Assert.That(titleField.value, Is.EqualTo("Group"));

            titleField.value = "Updated Group";

            Assert.That(comment.Title, Is.EqualTo("Updated Group"));
        }

        [Test]
        public void TextNodeVisuals_LongInlineBodyGrowsDownWithoutChangingWidth()
        {
            var graph = new DialogueGraphData();
            var longWord = new string('a', 80);
            var node = new DialogueTextNodeData
            {
                Title = "Long",
                BodyText = longWord
            };
            graph.Nodes.Add(node);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            var nodeView = view.graphElements.OfType<DialogueTextNodeView>().Single();

            var bodyField = nodeView.Q<TextField>("text-node-inline-body-field");
            Assert.That(bodyField, Is.Not.Null);
            Assert.That(bodyField.multiline, Is.True);
            Assert.That(bodyField.style.whiteSpace.value, Is.EqualTo(WhiteSpace.Normal));
            Assert.That(bodyField.value, Is.EqualTo(longWord));
            Assert.That(nodeView.GetPosition().width, Is.EqualTo(DialogueGraphView.TextNodeInitialSize.x));
            Assert.That(nodeView.GetPosition().height, Is.GreaterThanOrEqualTo(DialogueGraphView.TextNodeInitialSize.y));
            Assert.That(node.BodyText, Is.EqualTo(longWord));
            Assert.That(node.BodyText, Does.Not.Contain("\n"));
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
        public void MovingCommentNode_DoesNotAttachNodesEnteredDuringDrag()
        {
            var graph = new DialogueGraphData();
            var comment = new CommentNodeData
            {
                Title = "Group",
                Position = new Vector2(100f, 100f),
                Area = new Rect(100f, 100f, 420f, 260f)
            };
            var insideNode = new DialogueTextNodeData
            {
                Title = "Inside",
                Position = new Vector2(130f, 130f)
            };
            var enteredDuringDragNode = new DialogueTextNodeData
            {
                Title = "Entered During Drag",
                Position = new Vector2(470f, 130f)
            };

            graph.Nodes.Add(comment);
            graph.Nodes.Add(insideNode);
            graph.Nodes.Add(enteredDuringDragNode);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            var commentView = view.graphElements
                .OfType<DialogueCommentNodeView>()
                .Single(nodeView => nodeView.Data.Id == comment.Id);
            var firstMoveDelta = new Vector2(120f, 0f);
            var secondMoveDelta = new Vector2(50f, 0f);
            var previousInsidePosition = insideNode.Position;
            var previousEnteredDuringDragPosition = enteredDuringDragNode.Position;

            view.BeginCommentDrag(comment);
            commentView.SetPosition(new Rect(comment.Area.position + firstMoveDelta, comment.Area.size));
            commentView.SetPosition(new Rect(comment.Area.position + secondMoveDelta, comment.Area.size));
            view.EndCommentDrag(comment);

            Assert.That(insideNode.Position, Is.EqualTo(previousInsidePosition + firstMoveDelta + secondMoveDelta));
            Assert.That(enteredDuringDragNode.Position, Is.EqualTo(previousEnteredDuringDragPosition));
        }

        [Test]
        public void DeleteCommentOnly_KeepsContainedNodes()
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

            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            view.DeleteCommentOnly(comment);

            Assert.That(graph.Nodes.Select(node => node.Id), Is.EquivalentTo(new[]
            {
                textNode.Id
            }));
        }

        [Test]
        public void DeleteCommentGroup_RemovesContainedNodes()
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
            var outsideNode = new DialogueTextNodeData
            {
                Title = "Outside",
                Position = new Vector2(620f, 130f)
            };

            graph.Nodes.Add(comment);
            graph.Nodes.Add(textNode);
            graph.Nodes.Add(outsideNode);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            view.DeleteCommentGroup(comment);

            Assert.That(graph.Nodes.Select(node => node.Id), Is.EquivalentTo(new[]
            {
                outsideNode.Id
            }));
        }

        [Test]
        public void DeleteHotkey_EmptySingleComment_DeletesCommentOnlyWithoutPrompt()
        {
            var graph = new DialogueGraphData();
            var comment = new CommentNodeData
            {
                Title = "Empty",
                Position = new Vector2(100f, 100f),
                Area = new Rect(100f, 100f, 420f, 260f)
            };
            graph.Nodes.Add(comment);

            var promptCalls = 0;
            var view = new DialogueGraphView
            {
                CommentDeletePrompt = _ =>
                {
                    promptCalls++;
                    return DialogueCommentDeleteChoice.Cancel;
                }
            };
            view.LoadGraph(graph);
            view.SelectCommentGroup(comment);

            Assert.That(view.DeleteSelectionFromHotkey(), Is.True);

            Assert.That(promptCalls, Is.EqualTo(0));
            Assert.That(graph.Nodes, Is.Empty);
        }

        [Test]
        public void DeleteHotkey_CommentWithContents_CanDeleteCommentOnly()
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

            var view = new DialogueGraphView
            {
                CommentDeletePrompt = _ => DialogueCommentDeleteChoice.DeleteCommentOnly
            };
            view.LoadGraph(graph);
            view.SelectCommentGroup(comment);

            Assert.That(view.DeleteSelectionFromHotkey(), Is.True);

            Assert.That(graph.Nodes.Select(node => node.Id), Is.EquivalentTo(new[]
            {
                textNode.Id
            }));
        }

        [Test]
        public void DeleteHotkey_CommandDeleteWithCommentContents_ShowsPrompt()
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

            var promptCalls = 0;
            var view = new DialogueGraphView
            {
                CommentDeletePrompt = _ =>
                {
                    promptCalls++;
                    return DialogueCommentDeleteChoice.DeleteCommentOnly;
                }
            };
            view.LoadGraph(graph);
            view.FocusCanvas();
            view.SelectCommentGroup(comment);

            SendKeyDown(view, KeyCode.Delete, EventModifiers.Command);

            Assert.That(promptCalls, Is.EqualTo(1));
            Assert.That(graph.Nodes.Select(node => node.Id), Is.EquivalentTo(new[]
            {
                textNode.Id
            }));
        }

        [Test]
        public void DeleteHotkey_ControlBackspaceWithCommentContents_ShowsPrompt()
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

            var promptCalls = 0;
            var view = new DialogueGraphView
            {
                CommentDeletePrompt = _ =>
                {
                    promptCalls++;
                    return DialogueCommentDeleteChoice.DeleteCommentOnly;
                }
            };
            view.LoadGraph(graph);
            view.FocusCanvas();
            view.SelectCommentGroup(comment);

            SendKeyDown(view, KeyCode.Backspace, EventModifiers.Control);

            Assert.That(promptCalls, Is.EqualTo(1));
            Assert.That(graph.Nodes.Select(node => node.Id), Is.EquivalentTo(new[]
            {
                textNode.Id
            }));
        }

        [Test]
        public void DeleteHotkey_CommentWithContents_CanDeleteGroup()
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

            var view = new DialogueGraphView
            {
                CommentDeletePrompt = _ => DialogueCommentDeleteChoice.DeleteCommentWithContents
            };
            view.LoadGraph(graph);
            view.SelectCommentGroup(comment);

            Assert.That(view.DeleteSelectionFromHotkey(), Is.True);

            Assert.That(graph.Nodes, Is.Empty);
        }

        [Test]
        public void DeleteHotkey_CommentWithContents_CancelKeepsGraph()
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

            var view = new DialogueGraphView
            {
                CommentDeletePrompt = _ => DialogueCommentDeleteChoice.Cancel
            };
            view.LoadGraph(graph);
            view.SelectCommentGroup(comment);

            Assert.That(view.DeleteSelectionFromHotkey(), Is.True);

            Assert.That(graph.Nodes.Select(node => node.Id), Is.EquivalentTo(new[]
            {
                comment.Id,
                textNode.Id
            }));
        }

        [Test]
        public void DeleteHotkey_MultiSelection_KeepsBulkDeleteBehavior()
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
            var outsideNode = new DialogueTextNodeData
            {
                Title = "Outside",
                Position = new Vector2(620f, 130f)
            };
            graph.Nodes.Add(comment);
            graph.Nodes.Add(textNode);
            graph.Nodes.Add(outsideNode);

            var promptCalls = 0;
            var view = new DialogueGraphView
            {
                CommentDeletePrompt = _ =>
                {
                    promptCalls++;
                    return DialogueCommentDeleteChoice.Cancel;
                }
            };
            view.LoadGraph(graph);
            view.SelectCommentGroup(comment);
            var outsideView = view.graphElements
                .OfType<DialogueTextNodeView>()
                .Single(nodeView => nodeView.Data.Id == outsideNode.Id);
            view.AddToSelection(outsideView);

            Assert.That(view.DeleteSelectionFromHotkey(), Is.True);

            Assert.That(promptCalls, Is.EqualTo(0));
            Assert.That(graph.Nodes, Is.Empty);
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
        public void Zoom_DoesNotChangeNodeDataPositions()
        {
            var graph = new DialogueGraphData();
            var textNode = new DialogueTextNodeData
            {
                Title = "Text",
                Position = new Vector2(120f, 180f)
            };
            var functionNode = new FunctionNodeData
            {
                Title = "Function",
                Position = new Vector2(420f, 360f)
            };
            var sceneNode = new SceneNodeData
            {
                Title = "Scene",
                Position = new Vector2(680f, 500f)
            };
            graph.Nodes.Add(textNode);
            graph.Nodes.Add(functionNode);
            graph.Nodes.Add(sceneNode);
            var originalTextPosition = textNode.Position;
            var originalFunctionPosition = functionNode.Position;
            var originalScenePosition = sceneNode.Position;

            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            view.UpdateViewTransform(new Vector3(70f, -35f, 0f), Vector3.one * 0.5f);
            view.UpdateViewTransform(new Vector3(-20f, 50f, 0f), Vector3.one * 2f);
            view.UpdateViewTransform(Vector3.zero, Vector3.one);

            Assert.That(textNode.Position, Is.EqualTo(originalTextPosition));
            Assert.That(functionNode.Position, Is.EqualTo(originalFunctionPosition));
            Assert.That(sceneNode.Position, Is.EqualTo(originalScenePosition));
        }

        [Test]
        public void Zoom_DoesNotChangeCommentAreas()
        {
            var graph = new DialogueGraphData();
            var comment = new CommentNodeData
            {
                Title = "Comment",
                Position = new Vector2(90f, 110f),
                Area = new Rect(90f, 110f, 500f, 260f)
            };
            graph.Nodes.Add(comment);
            var originalPosition = comment.Position;
            var originalArea = comment.Area;

            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            view.UpdateViewTransform(new Vector3(100f, 40f, 0f), Vector3.one * 0.5f);
            view.UpdateViewTransform(new Vector3(-120f, -60f, 0f), Vector3.one * 1.75f);
            view.UpdateViewTransform(Vector3.zero, Vector3.one);

            Assert.That(comment.Position, Is.EqualTo(originalPosition));
            Assert.That(comment.Area, Is.EqualTo(originalArea));
        }

        [Test]
        public void Zoom_PreservesCanvasDistanceBetweenNodes()
        {
            var graph = new DialogueGraphData();
            var first = new DialogueTextNodeData
            {
                Title = "First",
                Position = new Vector2(100f, 140f)
            };
            var second = new DialogueTextNodeData
            {
                Title = "Second",
                Position = new Vector2(460f, 420f)
            };
            graph.Nodes.Add(first);
            graph.Nodes.Add(second);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            var firstView = view.graphElements
                .OfType<DialogueTextNodeView>()
                .Single(element => element.Data.Id == first.Id);
            var secondView = view.graphElements
                .OfType<DialogueTextNodeView>()
                .Single(element => element.Data.Id == second.Id);
            var originalCanvasDistance = secondView.GetPosition().center - firstView.GetPosition().center;

            view.UpdateViewTransform(new Vector3(80f, -45f, 0f), Vector3.one * 0.5f);
            var zoomedOutCanvasDistance = secondView.GetPosition().center - firstView.GetPosition().center;
            view.UpdateViewTransform(new Vector3(-30f, 95f, 0f), Vector3.one * 2f);
            var zoomedInCanvasDistance = secondView.GetPosition().center - firstView.GetPosition().center;

            Assert.That(zoomedOutCanvasDistance, Is.EqualTo(originalCanvasDistance));
            Assert.That(zoomedInCanvasDistance, Is.EqualTo(originalCanvasDistance));
        }

        [Test]
        public void EdgeLayer_IsChildOfContentViewContainer()
        {
            var view = new DialogueGraphView();

            Assert.That(view.IsEdgeLayerInContentViewContainerForTests, Is.True);
        }

        [Test]
        public void Zoom_PreservesEdgeCanvasAnchorsRelativeToNodeRects()
        {
            var graph = new DialogueGraphData();
            var source = new DialogueTextNodeData
            {
                Title = "Source",
                Position = new Vector2(100f, 140f)
            };
            var target = new DialogueTextNodeData
            {
                Title = "Target",
                Position = new Vector2(460f, 420f)
            };
            graph.Nodes.Add(source);
            graph.Nodes.Add(target);
            graph.Links.Add(new NodeLinkData
            {
                FromNodeId = source.Id,
                ToNodeId = target.Id,
                Order = 0
            });

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            var expectedSourceAnchor = GetExpectedBottomAnchor(
                new Rect(source.Position, DialogueGraphView.TextNodeInitialSize),
                new Rect(target.Position, DialogueGraphView.TextNodeInitialSize).center.x);
            var expectedTargetAnchor = GetExpectedTopAnchor(
                new Rect(target.Position, DialogueGraphView.TextNodeInitialSize),
                new Rect(source.Position, DialogueGraphView.TextNodeInitialSize).center.x);

            var initialGeometry = view.GetEdgeGeometriesForTests().Single();
            var initialStart = initialGeometry.Start + view.EdgeLayerCanvasBoundsForTests.position;
            var initialEnd = initialGeometry.End + view.EdgeLayerCanvasBoundsForTests.position;

            view.UpdateViewTransform(new Vector3(80f, -45f, 0f), Vector3.one * 0.5f);
            var zoomedGeometry = view.GetEdgeGeometriesForTests().Single();
            var zoomedStart = zoomedGeometry.Start + view.EdgeLayerCanvasBoundsForTests.position;
            var zoomedEnd = zoomedGeometry.End + view.EdgeLayerCanvasBoundsForTests.position;

            Assert.That(initialStart, Is.EqualTo(expectedSourceAnchor));
            Assert.That(initialEnd, Is.EqualTo(expectedTargetAnchor));
            Assert.That(initialGeometry.Thickness, Is.GreaterThan(3f));
            Assert.That(zoomedStart, Is.EqualTo(expectedSourceAnchor));
            Assert.That(zoomedEnd, Is.EqualTo(expectedTargetAnchor));
        }

        [Test]
        public void LinkDragPreview_UsesCanvasPointerAfterZoom()
        {
            var graph = new DialogueGraphData();
            var source = new DialogueTextNodeData
            {
                Title = "Source",
                Position = new Vector2(100f, 140f)
            };
            graph.Nodes.Add(source);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            view.UpdateViewTransform(new Vector3(80f, -45f, 0f), Vector3.one * 0.5f);
            var sourceView = view.graphElements
                .OfType<DialogueTextNodeView>()
                .Single(element => element.Data.Id == source.Id);
            var pointerCanvasPosition = new Vector2(640f, 520f);
            var pointerWorldPosition = view.contentViewContainer.LocalToWorld(pointerCanvasPosition);

            view.BeginLinkDrag(sourceView, pointerWorldPosition);

            var previewGeometry = view.GetEdgeGeometriesForTests().Single();
            var previewEnd = previewGeometry.End + view.EdgeLayerCanvasBoundsForTests.position;
            Assert.That(previewGeometry.Thickness, Is.GreaterThan(3f));
            Assert.That(previewEnd.x, Is.EqualTo(pointerCanvasPosition.x).Within(0.01f));
            Assert.That(previewEnd.y, Is.EqualTo(pointerCanvasPosition.y).Within(0.01f));
        }

        [Test]
        public void EdgeGeometry_NearVerticalLinkUsesStraightControls()
        {
            var graph = new DialogueGraphData();
            var source = new DialogueTextNodeData
            {
                Title = "Source",
                Position = new Vector2(100f, 100f)
            };
            var target = new DialogueTextNodeData
            {
                Title = "Target",
                Position = new Vector2(112f, 420f)
            };
            graph.Nodes.Add(source);
            graph.Nodes.Add(target);
            graph.Links.Add(new NodeLinkData { FromNodeId = source.Id, ToNodeId = target.Id, Order = 0 });

            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            var geometry = view.GetEdgeGeometriesForTests().Single();

            Assert.That(Mathf.Abs(geometry.End.x - geometry.Start.x), Is.LessThanOrEqualTo(36f));
            Assert.That(geometry.ControlA.x, Is.EqualTo(geometry.Start.x).Within(0.01f));
            Assert.That(geometry.ControlB.x, Is.EqualTo(geometry.End.x).Within(0.01f));
            Assert.That(geometry.ControlA.y, Is.InRange(geometry.Start.y, geometry.End.y));
            Assert.That(geometry.ControlB.y, Is.InRange(geometry.Start.y, geometry.End.y));
        }

        [Test]
        public void EdgeGeometry_DiagonalLinkKeepsControlsBetweenEndpoints()
        {
            var graph = new DialogueGraphData();
            var source = new DialogueTextNodeData
            {
                Title = "Source",
                Position = new Vector2(100f, 100f)
            };
            var target = new DialogueTextNodeData
            {
                Title = "Target",
                Position = new Vector2(780f, 460f)
            };
            graph.Nodes.Add(source);
            graph.Nodes.Add(target);
            graph.Links.Add(new NodeLinkData { FromNodeId = source.Id, ToNodeId = target.Id, Order = 0 });

            var view = new DialogueGraphView();
            view.LoadGraph(graph);

            var geometry = view.GetEdgeGeometriesForTests().Single();

            Assert.That(geometry.ControlA.x, Is.InRange(geometry.Start.x, geometry.End.x));
            Assert.That(geometry.ControlB.x, Is.InRange(geometry.Start.x, geometry.End.x));
            Assert.That(geometry.ControlA.y, Is.InRange(geometry.Start.y, geometry.End.y));
            Assert.That(geometry.ControlB.y, Is.InRange(geometry.Start.y, geometry.End.y));
            Assert.That(geometry.ControlA.x, Is.LessThan(geometry.ControlB.x));
            Assert.That(geometry.ControlA.y, Is.LessThan(geometry.ControlB.y));
        }

        [Test]
        public void LinkHitTesting_UsesSmoothedEdgeControls()
        {
            var graph = new DialogueGraphData();
            var source = new DialogueTextNodeData
            {
                Title = "Source",
                Position = new Vector2(100f, 100f)
            };
            var target = new DialogueTextNodeData
            {
                Title = "Target",
                Position = new Vector2(780f, 460f)
            };
            var link = new NodeLinkData { FromNodeId = source.Id, ToNodeId = target.Id, Order = 0 };
            graph.Nodes.Add(source);
            graph.Nodes.Add(target);
            graph.Links.Add(link);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            var geometry = view.GetEdgeGeometriesForTests().Single();
            var sample = EvaluateBezierForTests(geometry.Start, geometry.ControlA, geometry.ControlB, geometry.End, 0.25f) +
                         view.EdgeLayerCanvasBoundsForTests.position;

            Assert.That(view.UpdateHoveredLinkForTests(sample), Is.True);
            Assert.That(view.HoveredLinkIdForTests, Is.EqualTo(link.Id));
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
        public void StepKeyboardPan_CompensatesDraggedSelectedNode()
        {
            var graph = new DialogueGraphData();
            var node = new DialogueTextNodeData
            {
                Title = "Dragged",
                Position = new Vector2(100f, 100f)
            };
            graph.Nodes.Add(node);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            view.FocusCanvas();
            view.UpdateViewTransform(Vector3.zero, Vector3.one * 0.5f);
            var nodeView = view.graphElements
                .OfType<DialogueTextNodeView>()
                .Single(element => element.Data.Id == node.Id);
            view.SelectRuntimeNode(nodeView);
            view.BeginSelectionPointerDrag(Vector2.zero);
            view.SetMovementKeyState(KeyCode.W, true);

            view.StepKeyboardPan(1f);

            Assert.That(view.viewTransform.position.y, Is.EqualTo(45f).Within(0.01f));
            Assert.That(node.Position, Is.EqualTo(new Vector2(100f, 10f)));
        }

        [Test]
        public void ContinueSelectionPointerDrag_AfterKeyboardPanDuringDrag_UsesDragStartBaseline()
        {
            var graph = new DialogueGraphData();
            var node = new DialogueTextNodeData
            {
                Title = "Dragged",
                Position = new Vector2(100f, 100f)
            };
            graph.Nodes.Add(node);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            view.FocusCanvas();
            view.UpdateViewTransform(Vector3.zero, Vector3.one * 0.5f);
            var nodeView = view.graphElements
                .OfType<DialogueTextNodeView>()
                .Single(element => element.Data.Id == node.Id);
            view.SelectRuntimeNode(nodeView);
            view.BeginSelectionPointerDrag(new Vector2(10f, 10f));
            view.SetMovementKeyState(KeyCode.W, true);

            view.StepKeyboardPan(1f);
            view.ContinueSelectionPointerDrag(new Vector2(20f, 10f));

            Assert.That(node.Position, Is.EqualTo(new Vector2(120f, 10f)));
        }

        [Test]
        public void ContinueSelectionPointerDrag_AfterRepeatedKeyboardPan_DoesNotDoubleApplyPanOffset()
        {
            var graph = new DialogueGraphData();
            var node = new DialogueTextNodeData
            {
                Title = "Dragged",
                Position = new Vector2(100f, 100f)
            };
            graph.Nodes.Add(node);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            view.FocusCanvas();
            view.UpdateViewTransform(Vector3.zero, Vector3.one * 0.5f);
            var nodeView = view.graphElements
                .OfType<DialogueTextNodeView>()
                .Single(element => element.Data.Id == node.Id);
            view.SelectRuntimeNode(nodeView);
            view.BeginSelectionPointerDrag(new Vector2(10f, 10f));
            view.SetMovementKeyState(KeyCode.W, true);

            view.StepKeyboardPan(1f);
            view.StepKeyboardPan(1f);
            view.ContinueSelectionPointerDrag(new Vector2(10f, 10f));
            view.ContinueSelectionPointerDrag(new Vector2(10f, 10f));

            Assert.That(node.Position, Is.EqualTo(new Vector2(100f, -80f)));
        }

        [Test]
        public void StepKeyboardPan_DraggingSelectedCommentGroup_MovesChildOnlyOnce()
        {
            var graph = new DialogueGraphData();
            var comment = new CommentNodeData
            {
                Title = "Group",
                Position = new Vector2(80f, 80f),
                Area = new Rect(80f, 80f, 360f, 240f)
            };
            var child = new DialogueTextNodeData
            {
                Title = "Child",
                Position = new Vector2(120f, 120f)
            };
            graph.Nodes.Add(comment);
            graph.Nodes.Add(child);

            var view = new DialogueGraphView();
            view.LoadGraph(graph);
            view.FocusCanvas();
            view.UpdateViewTransform(Vector3.zero, Vector3.one * 0.5f);
            view.SelectCommentGroup(comment);
            view.BeginCommentDrag(comment);
            view.BeginSelectionPointerDrag(Vector2.zero);
            view.SetMovementKeyState(KeyCode.W, true);

            view.StepKeyboardPan(1f);

            Assert.That(comment.Position, Is.EqualTo(new Vector2(80f, -10f)));
            Assert.That(child.Position, Is.EqualTo(new Vector2(120f, 30f)));
        }

        [Test]
        public void ReleaseCanvasFocus_StopsFurtherKeyboardPan()
        {
            var view = new DialogueGraphView();
            view.LoadGraph(new DialogueGraphData());
            view.FocusCanvas();
            view.UpdateViewTransform(Vector3.zero, Vector3.one * 0.75f);
            var initialScale = view.viewTransform.scale;
            view.SetMovementKeyState(KeyCode.D, true);
            view.ReleaseCanvasFocus();

            view.StepKeyboardPan(1f);

            Assert.That(view.HasCanvasFocus, Is.False);
            Assert.That(view.viewTransform.position.x, Is.EqualTo(0f));
            Assert.That(view.viewTransform.scale, Is.EqualTo(initialScale));
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

        private static DialogueGraphData CreateSingleLinkGraph(out NodeLinkData link)
        {
            var graph = new DialogueGraphData();
            var start = new DialogueTextNodeData
            {
                Title = "Start",
                Position = new Vector2(100f, 140f)
            };
            var end = new DialogueTextNodeData
            {
                Title = "End",
                Position = new Vector2(460f, 420f)
            };

            link = new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = end.Id,
                Order = 0
            };

            graph.Nodes.Add(start);
            graph.Nodes.Add(end);
            graph.Links.Add(link);
            return graph;
        }

        private static Vector2 GetExpectedBottomAnchor(Rect sourceRect, float targetCenterX)
        {
            var x = Mathf.Clamp(targetCenterX, sourceRect.xMin + 18f, sourceRect.xMax - 18f);
            return new Vector2(x, sourceRect.yMax - 2f);
        }

        private static Vector2 GetExpectedTopAnchor(Rect targetRect, float sourceCenterX)
        {
            var x = Mathf.Clamp(sourceCenterX, targetRect.xMin + 18f, targetRect.xMax - 18f);
            return new Vector2(x, targetRect.yMin + 2f);
        }

        private static Vector2 EvaluateBezierForTests(Vector2 start, Vector2 controlA, Vector2 controlB, Vector2 end, float t)
        {
            var oneMinusT = 1f - t;
            return (oneMinusT * oneMinusT * oneMinusT * start) +
                   (3f * oneMinusT * oneMinusT * t * controlA) +
                   (3f * oneMinusT * t * t * controlB) +
                   (t * t * t * end);
        }

        private static DialogueGraphData GetResolvedGraph(DialogueDatabaseAsset database)
        {
            return database.Npcs[0].Dialogues[0].Graph;
        }

        private static void SendKeyDown(VisualElement target, KeyCode keyCode, EventModifiers modifiers = EventModifiers.None)
        {
            SendKeyDownWithState(target, keyCode, modifiers);
        }

        private static KeyEventState SendKeyDownWithState(VisualElement target, KeyCode keyCode, EventModifiers modifiers = EventModifiers.None)
        {
            var keyEvent = new Event
            {
                type = EventType.KeyDown,
                keyCode = keyCode,
                modifiers = modifiers
            };

            using (var keyDown = KeyDownEvent.GetPooled(keyEvent))
            {
                keyDown.target = target;
                target.SendEvent(keyDown);
                return KeyEventState.From(keyDown);
            }
        }

        private static void SendKeyUp(VisualElement target, KeyCode keyCode, EventModifiers modifiers = EventModifiers.None)
        {
            SendKeyUpWithState(target, keyCode, modifiers);
        }

        private static KeyEventState SendKeyUpWithState(VisualElement target, KeyCode keyCode, EventModifiers modifiers = EventModifiers.None)
        {
            var keyEvent = new Event
            {
                type = EventType.KeyUp,
                keyCode = keyCode,
                modifiers = modifiers
            };

            using (var keyUp = KeyUpEvent.GetPooled(keyEvent))
            {
                keyUp.target = target;
                target.SendEvent(keyUp);
                return KeyEventState.From(keyUp);
            }
        }

        private readonly struct KeyEventState
        {
            private KeyEventState(bool defaultPrevented, bool immediatePropagationStopped)
            {
                DefaultPrevented = defaultPrevented;
                ImmediatePropagationStopped = immediatePropagationStopped;
            }

            public bool DefaultPrevented { get; }
            public bool ImmediatePropagationStopped { get; }

            public static KeyEventState From(EventBase evt)
            {
#pragma warning disable 618
                return new KeyEventState(evt.isDefaultPrevented, evt.isImmediatePropagationStopped);
#pragma warning restore 618
            }
        }

        private static void SendMouseDown(VisualElement target, int clickCount)
        {
            var mouseEvent = new Event
            {
                type = EventType.MouseDown,
                button = 0,
                clickCount = clickCount,
                mousePosition = new Vector2(8f, 8f)
            };

            using (var mouseDown = MouseDownEvent.GetPooled(mouseEvent))
            {
                mouseDown.target = target;
                target.SendEvent(mouseDown);
            }
        }

        private static TNode GetResolvedNode<TNode>(DialogueDatabaseAsset database, string nodeId) where TNode : BaseNodeData
        {
            return GetResolvedGraph(database).Nodes.OfType<TNode>().Single(node => node.Id == nodeId);
        }
    }
}

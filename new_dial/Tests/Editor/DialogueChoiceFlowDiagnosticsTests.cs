using System.Linq;
using NUnit.Framework;

namespace NewDial.DialogueEditor.Tests
{
    public class DialogueChoiceFlowDiagnosticsTests
    {
        [Test]
        public void Analyze_ReportsNoOutgoingLinksForChoiceNode()
        {
            var dialogue = new DialogueEntry();
            var node = new DialogueTextNodeData
            {
                IsStartNode = true,
                UseOutputsAsChoices = true
            };
            dialogue.Graph.Nodes.Add(node);

            var diagnostics = DialogueChoiceFlowDiagnostics.Analyze(dialogue, node);

            Assert.That(diagnostics.Any(diagnostic =>
                diagnostic.Severity == DialogueChoiceFlowSeverity.Error &&
                diagnostic.Message == "Choice node has no outgoing links."), Is.True);
        }

        [Test]
        public void Analyze_ReportsFallbackAndBrokenChoiceLabels()
        {
            var dialogue = new DialogueEntry();
            var choiceNode = new DialogueTextNodeData
            {
                IsStartNode = true,
                UseOutputsAsChoices = true
            };
            var titledTarget = new DialogueTextNodeData { Title = "Target Title" };
            var untitledTarget = new DialogueTextNodeData { Title = string.Empty };
            dialogue.Graph.Nodes.Add(choiceNode);
            dialogue.Graph.Nodes.Add(titledTarget);
            dialogue.Graph.Nodes.Add(untitledTarget);
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = choiceNode.Id,
                ToNodeId = titledTarget.Id,
                Order = 0,
                ChoiceText = string.Empty
            });
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = choiceNode.Id,
                ToNodeId = untitledTarget.Id,
                Order = 1,
                ChoiceText = string.Empty
            });

            var diagnostics = DialogueChoiceFlowDiagnostics.Analyze(dialogue, choiceNode);

            Assert.That(diagnostics.Any(diagnostic =>
                diagnostic.Severity == DialogueChoiceFlowSeverity.Warning &&
                diagnostic.Message == "Choice text is empty; target title will be used."), Is.True);
            Assert.That(diagnostics.Any(diagnostic =>
                diagnostic.Severity == DialogueChoiceFlowSeverity.Error &&
                diagnostic.Message == "Choice text and target title are both empty."), Is.True);
        }

        [Test]
        public void Analyze_ReportsInvalidTargetsAndOrderConflicts()
        {
            var dialogue = new DialogueEntry();
            var choiceNode = new DialogueTextNodeData
            {
                IsStartNode = true,
                UseOutputsAsChoices = true
            };
            dialogue.Graph.Nodes.Add(choiceNode);
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = choiceNode.Id,
                ToNodeId = "missing-target",
                Order = -1,
                ChoiceText = "Broken"
            });
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = choiceNode.Id,
                ToNodeId = "also-missing",
                Order = -1,
                ChoiceText = "Also Broken"
            });

            var diagnostics = DialogueChoiceFlowDiagnostics.Analyze(dialogue, choiceNode);

            Assert.That(diagnostics.Any(diagnostic => diagnostic.Message == "Choice target is missing or invalid."), Is.True);
            Assert.That(diagnostics.Any(diagnostic => diagnostic.Message == "Choice order is negative."), Is.True);
            Assert.That(diagnostics.Any(diagnostic => diagnostic.Message == "Choice order conflicts with another choice."), Is.True);
        }

        [Test]
        public void Analyze_ReportsUnreachableChoiceTargets()
        {
            var dialogue = new DialogueEntry();
            var start = new DialogueTextNodeData { IsStartNode = true };
            var choiceNode = new DialogueTextNodeData { UseOutputsAsChoices = true };
            var target = new DialogueTextNodeData { Title = "Target" };
            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(choiceNode);
            dialogue.Graph.Nodes.Add(target);
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = choiceNode.Id,
                ToNodeId = target.Id,
                Order = 0,
                ChoiceText = "Go"
            });

            var diagnostics = DialogueChoiceFlowDiagnostics.Analyze(dialogue, choiceNode);

            Assert.That(diagnostics.Any(diagnostic =>
                diagnostic.Message == "Choice target is not reachable from the dialogue start."), Is.True);
        }

        [Test]
        public void Analyze_DoesNotThrowForNullCollections()
        {
            var dialogue = new DialogueEntry
            {
                Graph = new DialogueGraphData
                {
                    Nodes = null,
                    Links = null
                }
            };
            var node = new DialogueTextNodeData { UseOutputsAsChoices = true };

            Assert.DoesNotThrow(() => DialogueChoiceFlowDiagnostics.Analyze(dialogue, node));
        }
    }
}

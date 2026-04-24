using NUnit.Framework;
using System.Collections.Generic;

namespace NewDial.DialogueEditor.Tests
{
    public class DialoguePreviewSessionTests
    {
        [SetUp]
        public void SetUp()
        {
            DialogueEditorLanguageSettings.CurrentLanguage = DialogueEditorLanguage.English;
        }

        [Test]
        public void Choose_AppendsTranscriptAndBackRestoresChoiceState()
        {
            var dialogue = new DialogueEntry
            {
                Name = "Choice Preview"
            };

            var start = new DialogueTextNodeData
            {
                Title = "Question",
                BodyText = "Where do we go?",
                IsStartNode = true,
                UseOutputsAsChoices = true
            };

            var left = new DialogueTextNodeData
            {
                Title = "Left",
                BodyText = "We went left."
            };

            var right = new DialogueTextNodeData
            {
                Title = "Right",
                BodyText = "We went right."
            };

            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(left);
            dialogue.Graph.Nodes.Add(right);
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = left.Id,
                Order = 0,
                ChoiceText = "Go left"
            });
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = right.Id,
                Order = 1,
                ChoiceText = "Go right"
            });

            var session = new DialoguePreviewSession(dialogue);

            Assert.That(session.Transcript.Count, Is.EqualTo(1));
            Assert.That(session.CurrentChoices.Count, Is.EqualTo(2));

            Assert.That(session.Choose(1), Is.True);
            Assert.That(session.CurrentNode, Is.EqualTo(right));
            Assert.That(session.Transcript.Count, Is.EqualTo(2));
            Assert.That(session.Transcript[1].Kind, Is.EqualTo(DialoguePreviewTranscriptEntryKind.Choice));
            Assert.That(session.Transcript[1].Title, Is.EqualTo("Go right"));
            Assert.That(session.Transcript[1].Body, Is.EqualTo("We went right."));
            Assert.That(session.Transcript[1].NodeId, Is.EqualTo(right.Id));

            Assert.That(session.Back(), Is.True);
            Assert.That(session.CurrentNode, Is.EqualTo(start));
            Assert.That(session.CurrentChoices.Count, Is.EqualTo(2));
            Assert.That(session.Transcript.Count, Is.EqualTo(1));
        }

        [Test]
        public void Advance_ToEndAndBackRestoresPreviousNode()
        {
            var dialogue = new DialogueEntry
            {
                Name = "Linear Preview"
            };

            var start = new DialogueTextNodeData
            {
                Title = "Start",
                BodyText = "Start",
                IsStartNode = true
            };

            var end = new DialogueTextNodeData
            {
                Title = "End",
                BodyText = "End"
            };

            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(end);
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = end.Id,
                Order = 0
            });

            var session = new DialoguePreviewSession(dialogue);

            Assert.That(session.Advance(), Is.True);
            Assert.That(session.CurrentNode, Is.EqualTo(end));
            Assert.That(session.Advance(), Is.True);
            Assert.That(session.CurrentNode, Is.Null);
            Assert.That(session.IsEnded, Is.True);

            Assert.That(session.Back(), Is.True);
            Assert.That(session.CurrentNode, Is.EqualTo(end));
            Assert.That(session.Transcript.Count, Is.EqualTo(2));
        }

        [Test]
        public void StartCondition_UsesTestVariables()
        {
            var dialogue = new DialogueEntry
            {
                StartCondition = new ConditionData
                {
                    Type = ConditionType.VariableCheck,
                    Key = "has_key",
                    Operator = "==",
                    Value = "true"
                }
            };
            var start = new DialogueTextNodeData { IsStartNode = true };
            dialogue.Graph.Nodes.Add(start);

            var blockedSession = new DialoguePreviewSession(dialogue);
            Assert.That(blockedSession.CurrentNode, Is.Null);
            Assert.That(blockedSession.CurrentReason, Does.Contain("Dialogue start blocked by condition"));

            var allowedSession = new DialoguePreviewSession(dialogue, new Dictionary<string, string>
            {
                ["has_key"] = "true"
            });
            Assert.That(allowedSession.CurrentNode, Is.EqualTo(start));
        }

        [Test]
        public void ChoiceConditions_ReportBlockedChoices()
        {
            var dialogue = new DialogueEntry();
            var start = new DialogueTextNodeData
            {
                IsStartNode = true,
                UseOutputsAsChoices = true
            };
            var target = new DialogueTextNodeData
            {
                Title = "Locked",
                Condition = new ConditionData
                {
                    Type = ConditionType.VariableCheck,
                    Key = "door_open",
                    Operator = "==",
                    Value = "true"
                }
            };
            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(target);
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = target.Id,
                ChoiceText = "Open door"
            });

            var session = new DialoguePreviewSession(dialogue, new Dictionary<string, string>
            {
                ["door_open"] = "false"
            });

            Assert.That(session.CurrentChoices, Is.Empty);
            Assert.That(session.BlockedChoices.Count, Is.EqualTo(1));
            Assert.That(session.BlockedChoices[0].Reason, Does.Contain("Choice unavailable because condition is not met"));
            Assert.That(session.CurrentReason, Is.EqualTo("No choices are available with the current test variables."));
        }

        [Test]
        public void GenericChoiceFallback_IsExplained()
        {
            var dialogue = new DialogueEntry();
            var start = new DialogueTextNodeData
            {
                IsStartNode = true,
                UseOutputsAsChoices = true
            };
            var target = new DialogueTextNodeData { Title = string.Empty };
            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(target);
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = target.Id
            });

            var session = new DialoguePreviewSession(dialogue);

            Assert.That(session.CurrentChoices.Count, Is.EqualTo(1));
            Assert.That(session.GetChoiceExplanation(session.CurrentChoices[0]), Is.EqualTo("Generic fallback label is being used."));
        }
    }
}

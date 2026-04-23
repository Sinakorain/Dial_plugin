using NUnit.Framework;

namespace NewDial.DialogueEditor.Tests
{
    public class DialoguePreviewSessionTests
    {
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
    }
}

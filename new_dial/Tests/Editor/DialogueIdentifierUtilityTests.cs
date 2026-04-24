using NUnit.Framework;
using UnityEngine;

namespace NewDial.DialogueEditor.Tests
{
    public class DialogueIdentifierUtilityTests
    {
        [SetUp]
        public void SetUp()
        {
            DialogueEditorLanguageSettings.CurrentLanguage = DialogueEditorLanguage.English;
        }

        [Test]
        public void RenameNodeId_UpdatesIncomingAndOutgoingLinks()
        {
            var graph = new DialogueGraphData();
            var source = new DialogueTextNodeData { Id = "source" };
            var target = new DialogueTextNodeData { Id = "target" };
            var next = new DialogueTextNodeData { Id = "next" };
            graph.Nodes.Add(source);
            graph.Nodes.Add(target);
            graph.Nodes.Add(next);
            graph.Links.Add(new NodeLinkData { FromNodeId = source.Id, ToNodeId = target.Id, Order = 0 });
            graph.Links.Add(new NodeLinkData { FromNodeId = target.Id, ToNodeId = next.Id, Order = 0 });

            Assert.That(DialogueIdentifierUtility.RenameNodeId(graph, target, "renamed"), Is.True);

            Assert.That(target.Id, Is.EqualTo("renamed"));
            Assert.That(graph.Links[0].ToNodeId, Is.EqualTo("renamed"));
            Assert.That(graph.Links[1].FromNodeId, Is.EqualTo("renamed"));
        }

        [Test]
        public void DuplicateAndEmptyIds_AreDetected()
        {
            var database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            var firstNpc = new NpcEntry { Id = "npc" };
            var secondNpc = new NpcEntry { Id = "npc" };
            var firstDialogue = new DialogueEntry { Id = "dialogue" };
            var secondDialogue = new DialogueEntry { Id = "dialogue" };
            firstNpc.Dialogues.Add(firstDialogue);
            secondNpc.Dialogues.Add(secondDialogue);
            database.Npcs.Add(firstNpc);
            database.Npcs.Add(secondNpc);

            var graph = firstDialogue.Graph;
            graph.Nodes.Add(new DialogueTextNodeData { Id = "node" });
            graph.Nodes.Add(new DialogueTextNodeData { Id = "node" });
            graph.Nodes.Add(new DialogueTextNodeData { Id = string.Empty });

            Assert.That(DialogueIdentifierUtility.HasDuplicateNpcId(database), Is.True);
            Assert.That(DialogueIdentifierUtility.HasDuplicateDialogueId(database), Is.True);
            Assert.That(DialogueIdentifierUtility.HasDuplicateNodeId(graph), Is.True);
            Assert.That(DialogueIdentifierUtility.HasEmptyNodeId(graph), Is.True);

            secondNpc.Id = string.Empty;
            secondDialogue.Id = string.Empty;

            Assert.That(DialogueIdentifierUtility.HasEmptyNpcId(database), Is.True);
            Assert.That(DialogueIdentifierUtility.HasEmptyDialogueId(database), Is.True);
        }

        [Test]
        public void Validation_IgnoresEmptyIdsWhenCheckingDuplicates()
        {
            var database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            database.Npcs.Add(new NpcEntry { Id = string.Empty });
            database.Npcs.Add(new NpcEntry { Id = string.Empty });

            var graph = new DialogueGraphData();
            graph.Nodes.Add(new DialogueTextNodeData { Id = string.Empty });
            graph.Nodes.Add(new DialogueTextNodeData { Id = string.Empty });

            Assert.That(DialogueIdentifierUtility.HasDuplicateNpcId(database), Is.False);
            Assert.That(DialogueIdentifierUtility.HasDuplicateNodeId(graph), Is.False);
        }

        [Test]
        public void Validation_DoesNotThrowForNullCollections()
        {
            var database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            database.Npcs = null;
            var npc = new NpcEntry { Dialogues = null };
            var dialogue = new DialogueEntry();
            var graph = new DialogueGraphData
            {
                Nodes = null,
                Links = null
            };
            var node = new DialogueTextNodeData();

            Assert.DoesNotThrow(() => DialogueIdentifierUtility.GetIssues(database, npc));
            Assert.DoesNotThrow(() => DialogueIdentifierUtility.GetIssues(database, dialogue));
            Assert.DoesNotThrow(() => DialogueIdentifierUtility.GetIssues(graph, node));
            Assert.DoesNotThrow(() => DialogueIdentifierUtility.HasEmptyNpcId(database));
            Assert.DoesNotThrow(() => DialogueIdentifierUtility.HasDuplicateNpcId(database));
            Assert.DoesNotThrow(() => DialogueIdentifierUtility.HasEmptyDialogueId(database));
            Assert.DoesNotThrow(() => DialogueIdentifierUtility.HasDuplicateDialogueId(database));
            Assert.DoesNotThrow(() => DialogueIdentifierUtility.HasEmptyNodeId(graph));
            Assert.DoesNotThrow(() => DialogueIdentifierUtility.HasDuplicateNodeId(graph));
            Assert.DoesNotThrow(() => DialogueIdentifierUtility.RenameNodeId(graph, node, "renamed"));
        }
    }
}

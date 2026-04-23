using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace NewDial.DialogueEditor.Tests
{
    public class DialoguePlayerTests
    {
        [Test]
        public void Next_FollowsFirstValidTargetByOrder()
        {
            var dialogue = new DialogueEntry
            {
                Name = "Traversal Test"
            };

            var start = new DialogueTextNodeData
            {
                Title = "Start",
                BodyText = "Start",
                IsStartNode = true
            };

            var blocked = new DialogueTextNodeData
            {
                Title = "Blocked",
                BodyText = "Blocked",
                Condition = new ConditionData
                {
                    Type = ConditionType.VariableCheck,
                    Key = "allow_blocked",
                    Operator = "==",
                    Value = "true"
                }
            };

            var valid = new DialogueTextNodeData
            {
                Title = "Valid",
                BodyText = "Valid"
            };

            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(blocked);
            dialogue.Graph.Nodes.Add(valid);

            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = blocked.Id,
                Order = 0
            });

            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = valid.Id,
                Order = 1
            });

            var player = new DialoguePlayer(
                new DefaultDialogueConditionEvaluator(),
                new DictionaryDialogueVariableStore(new Dictionary<string, string>()));

            Assert.That(player.Start(dialogue), Is.True);
            Assert.That(player.Next(), Is.True);
            Assert.That(player.CurrentNode, Is.EqualTo(valid));
        }

        [Test]
        public void Choose_UsesChoiceModeOutputs()
        {
            var dialogue = new DialogueEntry
            {
                Name = "Choice Test"
            };

            var start = new DialogueTextNodeData
            {
                Title = "Question",
                BodyText = "Choose a branch.",
                IsStartNode = true,
                UseOutputsAsChoices = true
            };

            var left = new DialogueTextNodeData
            {
                Title = "Left",
                BodyText = "Left branch"
            };

            var right = new DialogueTextNodeData
            {
                Title = "Right",
                BodyText = "Right branch"
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

            var player = new DialoguePlayer();
            Assert.That(player.Start(dialogue), Is.True);
            Assert.That(player.CurrentChoices.Count, Is.EqualTo(2));

            Assert.That(player.Choose(1), Is.True);
            Assert.That(player.CurrentNode, Is.EqualTo(right));
        }

        [Test]
        public void GraphUtility_DeleteNode_RemovesConnectedLinks()
        {
            var graph = new DialogueGraphData();

            var a = new DialogueTextNodeData { Title = "A" };
            var b = new DialogueTextNodeData { Title = "B" };
            graph.Nodes.Add(a);
            graph.Nodes.Add(b);
            graph.Links.Add(new NodeLinkData
            {
                FromNodeId = a.Id,
                ToNodeId = b.Id,
                Order = 0
            });

            DialogueGraphUtility.DeleteNode(graph, a.Id);

            Assert.That(graph.Nodes, Has.Count.EqualTo(1));
            Assert.That(graph.Links, Is.Empty);
        }

        [Test]
        public void AutosaveStore_RoundTripsDatabaseState()
        {
            var root = Path.Combine(Path.GetTempPath(), $"newdial-tests-{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(root);

            try
            {
                var database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
                var npc = new NpcEntry { Name = "Merchant" };
                var dialogue = new DialogueEntry { Name = "Trade" };
                var node = new DialogueTextNodeData
                {
                    Title = "Start",
                    BodyText = "Interested in wares?",
                    IsStartNode = true,
                    Position = new Vector2(12f, 34f)
                };

                dialogue.Graph.Nodes.Add(node);
                npc.Dialogues.Add(dialogue);
                database.Npcs.Add(npc);

                DialogueEditorAutosaveStore.SaveSnapshot(database, "autosave-test", root);

                database.Npcs.Clear();
                Assert.That(database.Npcs, Is.Empty);

                var restored = DialogueEditorAutosaveStore.TryLoadSnapshot(database, "autosave-test", root);
                Assert.That(restored, Is.True);
                Assert.That(database.Npcs, Has.Count.EqualTo(1));
                Assert.That(database.Npcs[0].Dialogues[0].Graph.Nodes, Has.Count.EqualTo(1));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }
    }
}

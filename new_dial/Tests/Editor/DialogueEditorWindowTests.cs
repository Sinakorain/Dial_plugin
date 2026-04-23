using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor.Tests
{
    public class DialogueEditorWindowTests
    {
        [Test]
        public void UndoRedo_RestoresSelectedNodeAndInspectorState()
        {
            var database = CreateDatabase("SelectionUndo");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var titleField = window.rootVisualElement.Q<TextField>("node-title-field");
                Assert.That(titleField, Is.Not.Null);

                titleField.value = "Changed Title";

                Assert.That(window.HasUnsavedChangesForTests, Is.True);
                Assert.That(((DialogueTextNodeData)database.Npcs[0].Dialogues[0].Graph.Nodes[0]).Title, Is.EqualTo("Changed Title"));

                Undo.PerformUndo();

                Assert.That(window.SelectedNodeIdForTests, Is.EqualTo(node.Id));
                Assert.That(window.HasUnsavedChangesForTests, Is.False);
                Assert.That(window.rootVisualElement.Q<TextField>("node-title-field")?.value, Is.EqualTo("Original Title"));

                Undo.PerformRedo();

                Assert.That(window.SelectedNodeIdForTests, Is.EqualTo(node.Id));
                Assert.That(window.HasUnsavedChangesForTests, Is.True);
                Assert.That(window.rootVisualElement.Q<TextField>("node-title-field")?.value, Is.EqualTo("Changed Title"));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void UndoToBaseline_ClearsAutosaveSnapshotAndDirtyState()
        {
            var database = CreateDatabase("AutosaveUndo");
            var snapshotPath = DialogueEditorAutosaveStore.GetSnapshotPath(DialogueEditorAutosaveStore.GetStorageKey(database));
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                if (File.Exists(snapshotPath))
                {
                    File.Delete(snapshotPath);
                }

                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var bodyField = window.rootVisualElement.Q<TextField>("node-body-field");
                Assert.That(bodyField, Is.Not.Null);

                bodyField.value = "Updated body text";

                Assert.That(window.HasUnsavedChangesForTests, Is.True);
                Assert.That(File.Exists(snapshotPath), Is.True);

                Undo.PerformUndo();

                Assert.That(window.HasUnsavedChangesForTests, Is.False);
                Assert.That(File.Exists(snapshotPath), Is.False);
            }
            finally
            {
                if (File.Exists(snapshotPath))
                {
                    File.Delete(snapshotPath);
                }

                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void UndoRedo_RestoresLinkChoiceTextAndOrder()
        {
            var database = CreateDatabaseWithLinks("LinkUndo");
            var dialogue = database.Npcs[0].Dialogues[0];
            var startNode = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var originalFirstLinkId = DialogueGraphUtility.GetOutgoingLinks(dialogue.Graph, startNode.Id)[0].Id;
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                Assert.That(window.FocusDialogueNode(dialogue, startNode.Id), Is.True);

                var choiceField = window.rootVisualElement.Query<TextField>("link-choice-field").ToList()[0];
                choiceField.value = "Updated Choice";

                Assert.That(DialogueGraphUtility.GetOutgoingLinks(database.Npcs[0].Dialogues[0].Graph, startNode.Id)[0].ChoiceText, Is.EqualTo("Updated Choice"));

                Undo.PerformUndo();

                Assert.That(window.rootVisualElement.Query<TextField>("link-choice-field").ToList()[0].value, Is.EqualTo("Go Left"));
                Assert.That(DialogueGraphUtility.GetOutgoingLinks(database.Npcs[0].Dialogues[0].Graph, startNode.Id)[0].ChoiceText, Is.EqualTo("Go Left"));

                var orderField = window.rootVisualElement.Query<IntegerField>("link-order-field").ToList()[0];
                orderField.value = 5;

                var reorderedFirstLinkId = DialogueGraphUtility.GetOutgoingLinks(database.Npcs[0].Dialogues[0].Graph, startNode.Id)[0].Id;
                Assert.That(reorderedFirstLinkId, Is.Not.EqualTo(originalFirstLinkId));

                Undo.PerformUndo();

                Assert.That(DialogueGraphUtility.GetOutgoingLinks(database.Npcs[0].Dialogues[0].Graph, startNode.Id)[0].Id, Is.EqualTo(originalFirstLinkId));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void UndoRedo_RestoresStartToggleValue()
        {
            var database = CreateDatabase("StartToggleUndo");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var startToggle = window.rootVisualElement.Q<Toggle>("node-start-toggle");
                Assert.That(startToggle, Is.Not.Null);

                startToggle.value = false;
                Assert.That(((DialogueTextNodeData)database.Npcs[0].Dialogues[0].Graph.Nodes[0]).IsStartNode, Is.False);

                Undo.PerformUndo();
                Assert.That(((DialogueTextNodeData)database.Npcs[0].Dialogues[0].Graph.Nodes[0]).IsStartNode, Is.True);

                Undo.PerformRedo();
                Assert.That(((DialogueTextNodeData)database.Npcs[0].Dialogues[0].Graph.Nodes[0]).IsStartNode, Is.False);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        private static DialogueDatabaseAsset CreateDatabase(string prefix)
        {
            var database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            database.name = $"{prefix}-{Guid.NewGuid():N}";

            var npc = new NpcEntry
            {
                Name = "NPC"
            };

            var dialogue = new DialogueEntry
            {
                Name = "Dialogue"
            };

            dialogue.Graph.Nodes.Add(new DialogueTextNodeData
            {
                Title = "Original Title",
                BodyText = "Original body",
                IsStartNode = true,
                Position = new Vector2(120f, 120f)
            });

            npc.Dialogues.Add(dialogue);
            database.Npcs.Add(npc);
            return database;
        }

        private static DialogueDatabaseAsset CreateDatabaseWithLinks(string prefix)
        {
            var database = CreateDatabase(prefix);
            var dialogue = database.Npcs[0].Dialogues[0];
            var startNode = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var leftNode = new DialogueTextNodeData
            {
                Title = "Left",
                Position = new Vector2(360f, 120f)
            };
            var rightNode = new DialogueTextNodeData
            {
                Title = "Right",
                Position = new Vector2(360f, 320f)
            };

            dialogue.Graph.Nodes.Add(leftNode);
            dialogue.Graph.Nodes.Add(rightNode);
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = startNode.Id,
                ToNodeId = leftNode.Id,
                Order = 0,
                ChoiceText = "Go Left"
            });
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = startNode.Id,
                ToNodeId = rightNode.Id,
                Order = 1,
                ChoiceText = "Go Right"
            });

            return database;
        }
    }
}

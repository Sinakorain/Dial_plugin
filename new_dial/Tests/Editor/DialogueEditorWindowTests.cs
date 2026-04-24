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
        [SetUp]
        public void SetUp()
        {
            DialogueEditorLanguageSettings.CurrentLanguage = DialogueEditorLanguage.English;
        }

        [Test]
        public void LanguageSettings_DefaultsToEnglish()
        {
            DialogueEditorLanguageSettings.ResetForTests();

            Assert.That(DialogueEditorLanguageSettings.CurrentLanguage, Is.EqualTo(DialogueEditorLanguage.English));
            Assert.That(DialogueEditorLocalization.Text("Palette"), Is.EqualTo("Palette"));
        }

        [Test]
        public void LanguageSwitcher_RefreshesToolbarPaletteAndInspectorLabels()
        {
            var database = CreateDatabase("LanguageSwitch");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var languageField = window.rootVisualElement.Q<PopupField<string>>("editor-language-field");
                Assert.That(languageField, Is.Not.Null);
                Assert.That(HasLabel(window, "Palette"), Is.True);

                languageField.value = "RU";

                Assert.That(DialogueEditorLanguageSettings.CurrentLanguage, Is.EqualTo(DialogueEditorLanguage.Russian));
                Assert.That(HasLabel(window, "Палитра"), Is.True);
                Assert.That(HasLabel(window, "Текстовый узел"), Is.True);
                Assert.That(HasLabel(window, "Ключ озвучки"), Is.True);

                languageField = window.rootVisualElement.Q<PopupField<string>>("editor-language-field");
                languageField.value = "EN";

                Assert.That(DialogueEditorLanguageSettings.CurrentLanguage, Is.EqualTo(DialogueEditorLanguage.English));
                Assert.That(HasLabel(window, "Palette"), Is.True);
            }
            finally
            {
                DialogueEditorLanguageSettings.CurrentLanguage = DialogueEditorLanguage.English;
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void LeftDock_UsesFixedProjectAreaAndUnscrolledCompactPalette()
        {
            DialogueEditorLanguageSettings.CurrentLanguage = DialogueEditorLanguage.Russian;
            var database = CreateDatabase("LeftDockLayout");
            var npc = database.Npcs[0];
            for (var index = 0; index < 8; index++)
            {
                npc.Dialogues.Add(new DialogueEntry { Name = $"Dialogue {index + 2}" });
            }

            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);

                Assert.That(window.rootVisualElement.Q<VisualElement>("project-panel"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<ScrollView>(className: "dialogue-editor__project-scroll"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<VisualElement>("palette-panel"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<ScrollView>(className: "dialogue-editor__palette-scroll"), Is.Null);
                Assert.That(window.rootVisualElement.Query<VisualElement>(className: "dialogue-editor__palette-item").ToList(), Has.Count.EqualTo(5));
                Assert.That(HasLabel(window, "Текстовый узел"), Is.True);
                Assert.That(HasLabel(window, "Комментарий"), Is.True);
                Assert.That(HasLabel(window, "Функция"), Is.True);
                Assert.That(HasLabel(window, "Сцена"), Is.True);
                Assert.That(HasLabel(window, "Отладка"), Is.True);
            }
            finally
            {
                DialogueEditorLanguageSettings.CurrentLanguage = DialogueEditorLanguage.English;
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

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

        [Test]
        public void NodeInspector_ShowsEditableIdField()
        {
            var database = CreateDatabase("NodeIdInspector");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var idField = window.rootVisualElement.Q<TextField>("node-id-field");
                Assert.That(idField, Is.Not.Null);
                Assert.That(idField.value, Is.EqualTo(node.Id));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void NodeInspector_EditsVoiceKeyAndMarksDatabaseDirty()
        {
            var database = CreateDatabase("VoiceKeyInspector");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            node.VoiceKey = "npc.original.line";
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var voiceKeyField = window.rootVisualElement.Q<TextField>("node-voice-key-field");
                Assert.That(voiceKeyField, Is.Not.Null);
                Assert.That(voiceKeyField.value, Is.EqualTo("npc.original.line"));

                voiceKeyField.value = "innkeeper.greeting.hello";

                Assert.That(node.VoiceKey, Is.EqualTo("innkeeper.greeting.hello"));
                Assert.That(window.HasUnsavedChangesForTests, Is.True);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void NodeInspector_EditsSpeakerAndMarksDatabaseDirty()
        {
            var database = CreateDatabase("SpeakerInspector");
            var dialogue = database.Npcs[0].Dialogues[0];
            dialogue.Speakers.Add(new DialogueSpeakerEntry { Id = "npc", Name = "NPC" });
            dialogue.Speakers.Add(new DialogueSpeakerEntry { Id = "hero", Name = "Hero" });
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var speakerField = window.rootVisualElement.Q<PopupField<string>>("node-speaker-field");
                Assert.That(speakerField, Is.Not.Null);
                Assert.That(speakerField.value, Is.EqualTo("NPC"));

                speakerField.value = "Hero";

                Assert.That(node.SpeakerId, Is.EqualTo("hero"));
                Assert.That(window.HasUnsavedChangesForTests, Is.True);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void DialogueInspector_ManagesSpeakerRosterAndClearsRemovedReferences()
        {
            var database = CreateDatabase("SpeakerRoster");
            var dialogue = database.Npcs[0].Dialogues[0];
            dialogue.Speakers.Add(new DialogueSpeakerEntry { Id = "npc", Name = "NPC" });
            dialogue.Speakers.Add(new DialogueSpeakerEntry { Id = "hero", Name = "Hero" });
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            node.SpeakerId = "hero";
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                window.SaveBaselineForTests();

                var nameFields = window.rootVisualElement.Query<TextField>("dialogue-speaker-name-field").ToList();
                Assert.That(nameFields, Has.Count.EqualTo(2));
                nameFields[0].value = "Innkeeper";
                Assert.That(dialogue.Speakers[0].Name, Is.EqualTo("Innkeeper"));

                window.AddSpeaker(dialogue);
                Assert.That(dialogue.Speakers, Has.Count.EqualTo(3));
                Assert.That(dialogue.Speakers[2].Name, Is.EqualTo("Speaker 3"));

                window.RemoveSpeaker(dialogue, dialogue.Speakers[1]);
                Assert.That(dialogue.Speakers.Select(speaker => speaker.Id), Does.Not.Contain("hero"));
                Assert.That(node.SpeakerId, Is.Empty);
                Assert.That(window.HasUnsavedChangesForTests, Is.True);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void Autosave_PreservesSpeakersAndNodeSpeakerId()
        {
            var database = CreateDatabase("SpeakerAutosave");
            var dialogue = database.Npcs[0].Dialogues[0];
            dialogue.Speakers.Add(new DialogueSpeakerEntry { Id = "npc", Name = "NPC" });
            dialogue.Speakers.Add(new DialogueSpeakerEntry { Id = "hero", Name = "Hero" });
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            node.SpeakerId = "hero";
            node.VoiceKey = "hero.line";
            var restored = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            var tempRoot = Path.Combine(Path.GetTempPath(), $"NewDialSpeakerAutosave-{Guid.NewGuid():N}");

            try
            {
                DialogueEditorAutosaveStore.SaveSnapshot(database, "speaker-test", tempRoot);

                Assert.That(DialogueEditorAutosaveStore.TryLoadSnapshot(restored, "speaker-test", tempRoot), Is.True);
                var restoredDialogue = restored.Npcs[0].Dialogues[0];
                var restoredNode = (DialogueTextNodeData)restoredDialogue.Graph.Nodes[0];

                Assert.That(restoredDialogue.Speakers.Select(speaker => speaker.Name), Is.EqualTo(new[] { "NPC", "Hero" }));
                Assert.That(restoredNode.SpeakerId, Is.EqualTo("hero"));
                Assert.That(restoredNode.VoiceKey, Is.EqualTo("hero.line"));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public void EditingNodeId_UpdatesLinksAndKeepsSelection()
        {
            var database = CreateDatabaseWithLinks("NodeIdRename");
            var dialogue = database.Npcs[0].Dialogues[0];
            var startNode = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var leftNode = (DialogueTextNodeData)dialogue.Graph.Nodes[1];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.SuppressIdentifierWarningsForTests = true;
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, leftNode.Id), Is.True);

                var idField = window.rootVisualElement.Q<TextField>("node-id-field");
                idField.value = "renamed-left";

                Assert.That(leftNode.Id, Is.EqualTo("renamed-left"));
                Assert.That(window.SelectedNodeIdForTests, Is.EqualTo("renamed-left"));
                Assert.That(DialogueGraphUtility.GetOutgoingLinks(dialogue.Graph, startNode.Id)[0].ToNodeId, Is.EqualTo("renamed-left"));
                Assert.That(window.rootVisualElement.Q<TextField>("node-id-field")?.value, Is.EqualTo("renamed-left"));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void UndoRedo_RestoresNodeIdAndLinks()
        {
            var database = CreateDatabaseWithLinks("NodeIdUndo");
            var dialogue = database.Npcs[0].Dialogues[0];
            var startNode = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var leftNode = (DialogueTextNodeData)dialogue.Graph.Nodes[1];
            var originalId = leftNode.Id;
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.SuppressIdentifierWarningsForTests = true;
                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                Assert.That(window.FocusDialogueNode(dialogue, leftNode.Id), Is.True);

                var idField = window.rootVisualElement.Q<TextField>("node-id-field");
                idField.value = "renamed-left";

                Assert.That(leftNode.Id, Is.EqualTo("renamed-left"));
                Assert.That(DialogueGraphUtility.GetOutgoingLinks(dialogue.Graph, startNode.Id)[0].ToNodeId, Is.EqualTo("renamed-left"));

                Undo.PerformUndo();

                leftNode = (DialogueTextNodeData)dialogue.Graph.Nodes[1];
                Assert.That(leftNode.Id, Is.EqualTo(originalId));
                Assert.That(DialogueGraphUtility.GetOutgoingLinks(dialogue.Graph, startNode.Id)[0].ToNodeId, Is.EqualTo(originalId));
                Assert.That(window.rootVisualElement.Q<TextField>("node-id-field")?.value, Is.EqualTo(originalId));

                Undo.PerformRedo();

                leftNode = (DialogueTextNodeData)dialogue.Graph.Nodes[1];
                Assert.That(leftNode.Id, Is.EqualTo("renamed-left"));
                Assert.That(DialogueGraphUtility.GetOutgoingLinks(dialogue.Graph, startNode.Id)[0].ToNodeId, Is.EqualTo("renamed-left"));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void ProjectPanel_ShowsEditableNpcAndDialogueIdFields()
        {
            var database = CreateDatabase("ProjectIdFields");
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);

                var npcIdField = window.rootVisualElement.Q<TextField>("npc-id-field");
                var dialogueIdField = window.rootVisualElement.Q<TextField>("dialogue-id-field");

                Assert.That(npcIdField, Is.Not.Null);
                Assert.That(dialogueIdField, Is.Not.Null);
                Assert.That(npcIdField.value, Is.EqualTo(database.Npcs[0].Id));
                Assert.That(dialogueIdField.value, Is.EqualTo(database.Npcs[0].Dialogues[0].Id));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void ProjectPanel_ShowsDuplicateNpcAndDialogueWarnings()
        {
            var database = CreateDatabase("ProjectIdWarnings");
            var firstNpc = database.Npcs[0];
            var secondNpc = new NpcEntry { Name = "Second NPC" };
            secondNpc.Dialogues.Add(new DialogueEntry { Name = "Second Dialogue" });
            database.Npcs.Add(secondNpc);
            firstNpc.Dialogues.Add(new DialogueEntry { Name = "Duplicate Dialogue Candidate" });
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.SuppressIdentifierWarningsForTests = true;
                window.InitializeForTests(database);

                var npcFields = window.rootVisualElement.Query<TextField>("npc-id-field").ToList();
                Assert.That(npcFields, Has.Count.EqualTo(2));
                npcFields[1].value = firstNpc.Id;

                Assert.That(HasIdentifierIssue(window, "Duplicate NPC Id"), Is.True);

                var dialogueFields = window.rootVisualElement.Query<TextField>("dialogue-id-field").ToList();
                Assert.That(dialogueFields, Has.Count.GreaterThanOrEqualTo(2));
                dialogueFields[1].value = firstNpc.Dialogues[0].Id;

                Assert.That(HasIdentifierIssue(window, "Duplicate Dialogue Id"), Is.True);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void NodeInspector_ShowsEmptyAndDuplicateIdWarnings()
        {
            var database = CreateDatabase("NodeIdWarnings");
            var dialogue = database.Npcs[0].Dialogues[0];
            var firstNode = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var secondNode = new DialogueTextNodeData
            {
                Title = "Second",
                Position = new Vector2(360f, 120f)
            };
            dialogue.Graph.Nodes.Add(secondNode);
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.SuppressIdentifierWarningsForTests = true;
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, secondNode.Id), Is.True);

                var idField = window.rootVisualElement.Q<TextField>("node-id-field");
                idField.value = string.Empty;
                Assert.That(HasIdentifierIssue(window, "Id is empty"), Is.True);

                idField = window.rootVisualElement.Q<TextField>("node-id-field");
                idField.value = firstNode.Id;
                Assert.That(HasIdentifierIssue(window, "Duplicate Node Id"), Is.True);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void NodeInspector_ShowsChoiceFlowDiagnostics()
        {
            var database = CreateDatabase("ChoiceFlowWarnings");
            var dialogue = database.Npcs[0].Dialogues[0];
            var choiceNode = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            choiceNode.UseOutputsAsChoices = true;
            var untitledTarget = new DialogueTextNodeData
            {
                Title = string.Empty,
                Position = new Vector2(360f, 120f)
            };
            dialogue.Graph.Nodes.Add(untitledTarget);
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = choiceNode.Id,
                ToNodeId = untitledTarget.Id,
                Order = 0,
                ChoiceText = string.Empty
            });
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, choiceNode.Id), Is.True);

                Assert.That(HasChoiceDiagnostic(window, "Choice text and target title are both empty."), Is.True);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void ConditionEditor_HidesIrrelevantFieldsForNone()
        {
            var database = CreateDatabase("ConditionNone");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            node.Condition.Type = ConditionType.None;
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                Assert.That(window.rootVisualElement.Q<TextField>("condition-key-field"), Is.Null);
                Assert.That(window.rootVisualElement.Q<PopupField<string>>("condition-operator-field"), Is.Null);
                Assert.That(window.rootVisualElement.Q<TextField>("condition-value-field"), Is.Null);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void ConditionEditor_ShowsGuidedFieldsForVariableCondition()
        {
            var database = CreateDatabase("ConditionGuided");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            node.Condition.Type = ConditionType.VariableCheck;
            node.Condition.Operator = "==";
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                Assert.That(window.rootVisualElement.Q<TextField>("condition-key-field"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<PopupField<string>>("condition-operator-field"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<TextField>("condition-value-field"), Is.Not.Null);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void ConditionEditor_ShowsProviderKeySuggestions()
        {
            var provider = new TestConditionMetadataProvider();
            DialogueConditionMetadataRegistry.RegisterProvider(provider);
            var database = CreateDatabase("ConditionSuggestions");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            node.Condition.Type = ConditionType.VariableCheck;
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                Assert.That(window.rootVisualElement.Q<PopupField<string>>("condition-key-suggestion-field"), Is.Not.Null);
            }
            finally
            {
                DialogueConditionMetadataRegistry.UnregisterProvider(provider);
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void FunctionInspector_ShowsFallbackFreeFormFields()
        {
            var database = CreateDatabase("FunctionFallback");
            var dialogue = database.Npcs[0].Dialogues[0];
            var functionNode = new FunctionNodeData
            {
                Title = "Function",
                FunctionId = "custom.function",
                Position = new Vector2(360f, 120f)
            };
            dialogue.Graph.Nodes.Add(functionNode);
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, functionNode.Id), Is.True);

                Assert.That(window.rootVisualElement.Q<TextField>("function-id-field"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<TextField>("argument-name-field"), Is.Null);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void ExecutableInspector_ShowsValidationErrors()
        {
            var registry = new TestExecutionRegistry();
            DialogueExecutionRegistry.Register(registry);
            var database = CreateDatabase("ExecutableValidation");
            var dialogue = database.Npcs[0].Dialogues[0];
            var functionNode = new FunctionNodeData
            {
                Title = "Function",
                FunctionId = "session.set_int",
                Arguments = new System.Collections.Generic.List<DialogueArgumentEntry>
                {
                    new() { Name = "value", Value = DialogueArgumentValue.FromString("wrong") }
                }
            };
            dialogue.Graph.Nodes.Add(functionNode);
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, functionNode.Id), Is.True);

                Assert.That(HasChoiceDiagnostic(window, "Required argument 'key' is missing."), Is.True);
                Assert.That(HasChoiceDiagnostic(window, "Argument 'value' expects Int but is String."), Is.True);
            }
            finally
            {
                DialogueExecutionRegistry.Unregister(registry);
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void SceneInspector_DefaultKnownSceneWritesSceneKey()
        {
            var registry = new TestExecutionRegistry();
            DialogueExecutionRegistry.Register(registry);
            var database = CreateDatabase("SceneDefaultSelection");
            var dialogue = database.Npcs[0].Dialogues[0];
            var sceneNode = new SceneNodeData
            {
                Title = "Scene",
                Position = new Vector2(360f, 120f)
            };
            dialogue.Graph.Nodes.Add(sceneNode);
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, sceneNode.Id), Is.True);

                Assert.That(sceneNode.SceneKey, Is.EqualTo("Battle_Arena"));
                Assert.That(window.rootVisualElement.Q<PopupField<string>>("scene-descriptor-field")?.value, Is.EqualTo("Combat: Battle Arena"));
                Assert.That(window.rootVisualElement.Q<TextField>("scene-key-field")?.value, Is.EqualTo("Battle_Arena"));
            }
            finally
            {
                DialogueExecutionRegistry.Unregister(registry);
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

        private static bool HasIdentifierIssue(DialogueEditorWindow window, string issueText)
        {
            return window.rootVisualElement.Query<Label>(className: "dialogue-editor__id-issue")
                .ToList()
                .Any(label => label.text == issueText);
        }

        private static bool HasChoiceDiagnostic(DialogueEditorWindow window, string diagnosticText)
        {
            return window.rootVisualElement.Query<Label>(className: "dialogue-editor__choice-diagnostic")
                .ToList()
                .Any(label => label.text == diagnosticText);
        }

        private static bool HasLabel(DialogueEditorWindow window, string text)
        {
            return window.rootVisualElement.Query<Label>()
                .ToList()
                .Any(label => label.text == text);
        }

        private sealed class TestConditionMetadataProvider : IDialogueConditionMetadataProvider
        {
            public System.Collections.Generic.IEnumerable<DialogueConditionKeySuggestion> GetKeySuggestions(ConditionType type)
            {
                if (type == ConditionType.VariableCheck)
                {
                    yield return new DialogueConditionKeySuggestion("door_open", "Door Open", "Flags");
                }
            }
        }

        private sealed class TestExecutionRegistry : IDialogueExecutionRegistry
        {
            public System.Collections.Generic.IEnumerable<DialogueFunctionDescriptor> GetFunctions()
            {
                yield return new DialogueFunctionDescriptor(
                    "session.set_int",
                    "Set Int",
                    "Session",
                    parameters: new[]
                    {
                        new DialogueParameterDescriptor("key", DialogueArgumentType.String, required: true),
                        new DialogueParameterDescriptor("value", DialogueArgumentType.Int, required: true)
                    });
            }

            public System.Collections.Generic.IEnumerable<DialogueSceneDescriptor> GetScenes()
            {
                yield return new DialogueSceneDescriptor("Battle_Arena", "Battle Arena", "Combat");
            }
        }
    }
}

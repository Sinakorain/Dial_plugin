using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor.Tests
{
    public class DialogueLocalizationTableTests
    {
        [SetUp]
        public void SetUp()
        {
            DialogueEditorLanguageSettings.CurrentLanguage = DialogueEditorLanguage.English;
            DialogueContentLanguageSettings.ResetForTests();
        }

        [Test]
        public void Parser_DetectsHeadersQuotedCsvMultilineTextAndSkipsLoading()
        {
            var csv =
                "Keys,Russian,English,Spanish (Latin Americas)\n" +
                "\"Conversation/Farm.Plot.Dialogue_0001/Entry/1/Dialogue Text\",\"Привет\",\"Hello\nfriend\",\"Loading...\"\n";

            var table = DialogueLocalizationTableParser.Parse(csv);

            Assert.That(table.Rows, Has.Count.EqualTo(1));
            var row = table.Rows[0];
            Assert.That(row.ConversationId, Is.EqualTo("Farm.Plot.Dialogue_0001"));
            Assert.That(row.EntryIndex, Is.EqualTo(1));
            Assert.That(row.TextByLanguage["ru"], Is.EqualTo("Привет"));
            Assert.That(row.TextByLanguage["en"], Is.EqualTo("Hello\nfriend"));
            Assert.That(row.TextByLanguage.ContainsKey("es-419"), Is.False);
        }

        [Test]
        public void Import_EmptyDialogueCreatesLinearTextNodes()
        {
            var table = DialogueLocalizationTableParser.Parse(
                "Keys\tRussian\tEnglish\n" +
                "Conversation/Farm.Plot.Dialogue_0001/Entry/1/Dialogue Text\tОдин\tOne\n" +
                "Conversation/Farm.Plot.Dialogue_0001/Entry/2/Dialogue Text\tДва\tTwo\n");
            var dialogue = new DialogueEntry();
            dialogue.Graph.Nodes.Clear();

            var report = DialogueLocalizationImportUtility.ApplyConversationToDialogue(
                dialogue,
                table.GetConversation("Farm.Plot.Dialogue_0001"));

            Assert.That(report.Created, Is.EqualTo(2));
            Assert.That(dialogue.Graph.Nodes.OfType<DialogueTextNodeData>().Count(), Is.EqualTo(2));
            Assert.That(dialogue.Graph.Links, Has.Count.EqualTo(1));
            var first = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var second = (DialogueTextNodeData)dialogue.Graph.Nodes[1];
            Assert.That(first.IsStartNode, Is.True);
            Assert.That(first.Title, Is.EqualTo("Entry 1"));
            Assert.That(first.Position, Is.EqualTo(Vector2.zero));
            Assert.That(second.Position.x, Is.EqualTo(first.Position.x));
            Assert.That(second.Position.y, Is.GreaterThan(first.Position.y));
            Assert.That(first.BodyText, Is.EqualTo("Один"));
            Assert.That(DialogueTextLocalizationUtility.GetBodyText(first, "en"), Is.EqualTo("One"));
            Assert.That(dialogue.Graph.Links[0].FromNodeId, Is.EqualTo(first.Id));
            Assert.That(dialogue.Graph.Links[0].ToNodeId, Is.EqualTo(second.Id));
        }

        [Test]
        public void Import_ExistingDialogueUpdatesOnlyLocalizedTextData()
        {
            var dialogue = new DialogueEntry();
            var start = new DialogueTextNodeData
            {
                Id = "start",
                Title = "Start",
                LocalizationKey = "Conversation/Farm.Plot.Dialogue_0001/Entry/1/Dialogue Text",
                BodyText = "Old",
                IsStartNode = true,
                UseOutputsAsChoices = true,
                Position = new Vector2(12f, 34f),
                SpeakerId = "speaker"
            };
            var function = new FunctionNodeData
            {
                Id = "function",
                FunctionId = "Quest.Complete"
            };
            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(function);
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                Id = "link",
                FromNodeId = start.Id,
                ToNodeId = function.Id,
                ChoiceText = "Continue",
                Order = 7
            });
            var table = DialogueLocalizationTableParser.Parse(
                "Keys\tRussian\tEnglish\n" +
                "Conversation/Farm.Plot.Dialogue_0001/Entry/1/Dialogue Text\tНовый\tNew\n" +
                "Conversation/Farm.Plot.Dialogue_0001/Entry/2/Dialogue Text\tПропуск\tMissing\n");

            var report = DialogueLocalizationImportUtility.ApplyConversationToDialogue(
                dialogue,
                table.GetConversation("Farm.Plot.Dialogue_0001"));

            Assert.That(report.Updated, Is.EqualTo(1));
            Assert.That(report.Missing, Is.EqualTo(1));
            Assert.That(dialogue.Graph.Nodes, Has.Count.EqualTo(2));
            Assert.That(dialogue.Graph.Links, Has.Count.EqualTo(1));
            Assert.That(start.BodyText, Is.EqualTo("Новый"));
            Assert.That(DialogueTextLocalizationUtility.GetBodyText(start, "en"), Is.EqualTo("New"));
            Assert.That(start.Title, Is.EqualTo("Start"));
            Assert.That(start.IsStartNode, Is.True);
            Assert.That(start.UseOutputsAsChoices, Is.True);
            Assert.That(start.Position, Is.EqualTo(new Vector2(12f, 34f)));
            Assert.That(start.SpeakerId, Is.EqualTo("speaker"));
            Assert.That(((FunctionNodeData)dialogue.Graph.Nodes[1]).FunctionId, Is.EqualTo("Quest.Complete"));
            Assert.That(dialogue.Graph.Links[0].ChoiceText, Is.EqualTo("Continue"));
            Assert.That(dialogue.Graph.Links[0].Order, Is.EqualTo(7));
        }

        [Test]
        public void Import_BatchSelectedConversationsCreatesOnlyRequestedDialogues()
        {
            var table = DialogueLocalizationTableParser.Parse(
                "Keys\tRussian\tEnglish\n" +
                "Conversation/First/Entry/1/Dialogue Text\tПервый\tFirst\n" +
                "Conversation/Second/Entry/1/Dialogue Text\tВторой\tSecond\n");
            var database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            var npc = new NpcEntry();
            database.Npcs.Add(npc);

            var report = DialogueLocalizationImportUtility.ApplyConversationsToDatabase(
                database,
                npc,
                new[] { table.GetConversation("Second") },
                out var selectedNpc,
                out var selectedDialogue);

            Assert.That(report.ConversationsImported, Is.EqualTo(1));
            Assert.That(report.DialoguesCreated, Is.EqualTo(1));
            Assert.That(npc.Dialogues, Has.Count.EqualTo(1));
            Assert.That(npc.Dialogues[0].Id, Is.EqualTo("Second"));
            Assert.That(selectedNpc, Is.SameAs(npc));
            Assert.That(selectedDialogue, Is.SameAs(npc.Dialogues[0]));
            Assert.That(DialogueTextLocalizationUtility.GetBodyText((DialogueTextNodeData)selectedDialogue.Graph.Nodes[0], "en"), Is.EqualTo("Second"));
        }

        [Test]
        public void Import_BatchAllUpdatesMatchingDialogueIdAndCreatesMissingDialogues()
        {
            var database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            var npc = new NpcEntry();
            var existingDialogue = new DialogueEntry
            {
                Id = "Existing",
                Name = "Existing"
            };
            existingDialogue.Graph.Nodes.Add(new DialogueTextNodeData
            {
                LocalizationKey = "Conversation/Existing/Entry/1/Dialogue Text",
                BodyText = "Старый"
            });
            npc.Dialogues.Add(existingDialogue);
            database.Npcs.Add(npc);

            var table = DialogueLocalizationTableParser.Parse(
                "Keys\tRussian\tEnglish\n" +
                "Conversation/Existing/Entry/1/Dialogue Text\tНовый\tUpdated\n" +
                "Conversation/NewOne/Entry/1/Dialogue Text\tНовый диалог\tNew dialogue\n");

            var report = DialogueLocalizationImportUtility.ApplyConversationsToDatabase(
                database,
                npc,
                table.GetConversations(),
                out _,
                out _);

            Assert.That(report.ConversationsImported, Is.EqualTo(2));
            Assert.That(report.DialoguesCreated, Is.EqualTo(1));
            Assert.That(report.DialoguesUpdated, Is.EqualTo(1));
            Assert.That(report.Created, Is.EqualTo(1));
            Assert.That(report.Updated, Is.EqualTo(1));
            Assert.That(npc.Dialogues.Count(dialogue => dialogue.Id == "Existing"), Is.EqualTo(1));
            Assert.That(npc.Dialogues, Has.Count.EqualTo(2));
            Assert.That(((DialogueTextNodeData)existingDialogue.Graph.Nodes[0]).BodyText, Is.EqualTo("Новый"));
            Assert.That(DialogueTextLocalizationUtility.GetBodyText((DialogueTextNodeData)existingDialogue.Graph.Nodes[0], "en"), Is.EqualTo("Updated"));
            Assert.That(npc.Dialogues.Any(dialogue => dialogue.Id == "NewOne"), Is.True);
        }

        [Test]
        public void Export_RoundTripsLocalizedRowsAsTsv()
        {
            var dialogue = new DialogueEntry();
            var node = new DialogueTextNodeData
            {
                LocalizationKey = "Conversation/Farm.Plot.Dialogue_0001/Entry/1/Dialogue Text",
                BodyText = "Привет"
            };
            DialogueTextLocalizationUtility.SetBodyText(node, "en", "Hello");
            dialogue.Graph.Nodes.Add(node);

            var report = DialogueLocalizationImportUtility.ExportDialogue(dialogue, string.Empty, out var tsv);
            var parsed = DialogueLocalizationTableParser.Parse(tsv);

            Assert.That(report.Exported, Is.EqualTo(1));
            Assert.That(parsed.Rows, Has.Count.EqualTo(1));
            Assert.That(parsed.Rows[0].TextByLanguage["ru"], Is.EqualTo("Привет"));
            Assert.That(parsed.Rows[0].TextByLanguage["en"], Is.EqualTo("Hello"));
            Assert.That(tsv.Split('\n')[0].TrimEnd('\r'), Is.EqualTo("Keys\tRussian\tEnglish"));
        }

        [Test]
        public void EditorContentLanguage_DefaultsToOnlyBodyTextLanguageBeforeImport()
        {
            DialogueContentLanguageSettings.CurrentLanguageCode = "en";
            var database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            database.name = "DefaultContentLanguage";
            var npc = new NpcEntry();
            var dialogue = new DialogueEntry();
            var node = new DialogueTextNodeData
            {
                BodyText = "Привет",
                IsStartNode = true
            };
            dialogue.Graph.Nodes.Add(node);
            npc.Dialogues.Add(dialogue);
            database.Npcs.Add(npc);
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);

                var contentLanguageField = window.rootVisualElement.Q<PopupField<string>>("content-language-field");
                Assert.That(contentLanguageField, Is.Not.Null);
                Assert.That(contentLanguageField.choices, Is.EqualTo(new[] { "ru" }));
                Assert.That(contentLanguageField.value, Is.EqualTo("ru"));
                Assert.That(DialogueContentLanguageSettings.CurrentLanguageCode, Is.EqualTo("ru"));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void EditorContentLanguage_EditsSelectedLocalizationWithoutChangingUiLanguage()
        {
            var database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            database.name = "LocalizationLanguage";
            var npc = new NpcEntry();
            var dialogue = new DialogueEntry();
            var node = new DialogueTextNodeData
            {
                BodyText = "Привет",
                IsStartNode = true
            };
            DialogueTextLocalizationUtility.SetBodyText(node, "en", "Hello");
            dialogue.Graph.Nodes.Add(node);
            npc.Dialogues.Add(dialogue);
            database.Npcs.Add(npc);
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var contentLanguageField = window.rootVisualElement.Q<PopupField<string>>("content-language-field");
                Assert.That(contentLanguageField, Is.Not.Null);
                Assert.That(contentLanguageField.choices, Is.EqualTo(new[] { "ru", "en" }));
                contentLanguageField.value = "en";

                var bodyField = window.rootVisualElement.Q<TextField>("node-body-field");
                Assert.That(bodyField.value, Is.EqualTo("Hello"));
                bodyField.value = "Hello there";

                Assert.That(node.BodyText, Is.EqualTo("Привет"));
                Assert.That(DialogueTextLocalizationUtility.GetBodyText(node, "en"), Is.EqualTo("Hello there"));
                Assert.That(DialogueEditorLanguageSettings.CurrentLanguage, Is.EqualTo(DialogueEditorLanguage.English));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void EditorContentLanguage_RefreshAfterLocalizationImportShowsImportedLanguagesInRussianUi()
        {
            DialogueEditorLanguageSettings.CurrentLanguage = DialogueEditorLanguage.Russian;
            var database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            database.name = "LocalizationLanguageRussianUi";
            var npc = new NpcEntry();
            var dialogue = new DialogueEntry();
            dialogue.Graph.Nodes.Add(new DialogueTextNodeData
            {
                BodyText = "Привет",
                IsStartNode = true
            });
            npc.Dialogues.Add(dialogue);
            database.Npcs.Add(npc);
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);

                var contentLanguageField = window.rootVisualElement.Q<PopupField<string>>("content-language-field");
                Assert.That(contentLanguageField, Is.Not.Null);
                Assert.That(contentLanguageField.choices, Is.EqualTo(new[] { "ru" }));

                var table = DialogueLocalizationTableParser.Parse(
                    "Keys\tRussian\tEnglish\n" +
                    "Conversation/Imported/Entry/1/Dialogue Text\tИмпорт\tImported\n");
                DialogueLocalizationImportUtility.ApplyConversationsToDatabase(
                    database,
                    npc,
                    table.GetConversations(),
                    out var importedNpc,
                    out var importedDialogue);

                window.RefreshAfterLocalizationImport(importedNpc, importedDialogue);

                contentLanguageField = window.rootVisualElement.Q<PopupField<string>>("content-language-field");
                Assert.That(contentLanguageField, Is.Not.Null);
                Assert.That(contentLanguageField.choices, Is.EqualTo(new[] { "ru", "en" }));
            }
            finally
            {
                DialogueEditorLanguageSettings.CurrentLanguage = DialogueEditorLanguage.English;
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }
    }
}

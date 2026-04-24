using UnityEditor;
using UnityEngine;

namespace NewDial.DialogueEditor.Samples
{
    public static class BasicAdventureSampleCreator
    {
        [MenuItem("Tools/New Dial/Create Basic Adventure Sample")]
        public static void CreateSample()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Basic Adventure Sample",
                "BasicAdventureDialogueDatabase",
                "asset",
                "Choose where to save the sample dialogue database.");

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var asset = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            asset.Npcs.Add(CreateInnkeeperNpc());

            AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static NpcEntry CreateInnkeeperNpc()
        {
            var npc = new NpcEntry
            {
                Name = "Innkeeper"
            };

            npc.Dialogues.Add(CreateGreetingDialogue());
            npc.Dialogues.Add(CreateAfterHoursDialogue());
            return npc;
        }

        private static DialogueEntry CreateGreetingDialogue()
        {
            var dialogue = new DialogueEntry
            {
                Name = "Greeting"
            };
            dialogue.Speakers.Add(new DialogueSpeakerEntry { Name = "Innkeeper" });

            var start = new DialogueTextNodeData
            {
                Title = "Greeting",
                BodyText = "Welcome to the Lantern Rest. What do you need?",
                IsStartNode = true,
                UseOutputsAsChoices = true,
                Position = new Vector2(100f, 100f)
            };

            var room = new DialogueTextNodeData
            {
                Title = "Ask About Room",
                BodyText = "A room is 5 gold, breakfast included.",
                Position = new Vector2(460f, 20f)
            };

            var rumor = new DialogueTextNodeData
            {
                Title = "Ask About Rumor",
                BodyText = "People say the old watchtower lights itself at midnight.",
                Position = new Vector2(460f, 180f)
            };

            var trusted = new DialogueTextNodeData
            {
                Title = "Show Guard Badge",
                BodyText = "If you are with the city watch, take the back room. No charge.",
                Position = new Vector2(460f, 340f),
                Condition = new ConditionData
                {
                    Type = ConditionType.VariableCheck,
                    Key = "trust_level",
                    Operator = ">=",
                    Value = "2"
                }
            };

            var note = new CommentNodeData
            {
                Title = "Design Note",
                Comment = "Trusted branch demonstrates a simple numeric condition.",
                Area = new Rect(760f, 240f, 340f, 160f),
                Position = new Vector2(760f, 240f)
            };

            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(room);
            dialogue.Graph.Nodes.Add(rumor);
            dialogue.Graph.Nodes.Add(trusted);
            dialogue.Graph.Nodes.Add(note);

            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = room.Id,
                Order = 0,
                ChoiceText = "I need a room."
            });

            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = rumor.Id,
                Order = 1,
                ChoiceText = "Heard any rumors?"
            });

            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = trusted.Id,
                Order = 2,
                ChoiceText = "I have official business."
            });

            return dialogue;
        }

        private static DialogueEntry CreateAfterHoursDialogue()
        {
            var dialogue = new DialogueEntry
            {
                Name = "After Hours",
                StartCondition = new ConditionData
                {
                    Type = ConditionType.VariableCheck,
                    Key = "tavern_open",
                    Operator = "==",
                    Value = "false"
                }
            };
            dialogue.Speakers.Add(new DialogueSpeakerEntry { Name = "Innkeeper" });

            var start = new DialogueTextNodeData
            {
                Title = "Closed",
                BodyText = "We are closed. Come back when the bell rings in the morning.",
                IsStartNode = true,
                Position = new Vector2(100f, 100f)
            };

            dialogue.Graph.Nodes.Add(start);
            return dialogue;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NewDial.DialogueEditor
{
    public static class DialogueEditorAutosaveStore
    {
        private const string AutosaveFolderName = "DialogueEditorAutosaves";

        public static string GetStorageKey(DialogueDatabaseAsset database)
        {
            if (database == null)
            {
                return "unsaved-dialogue-database";
            }

            var assetPath = AssetDatabase.GetAssetPath(database);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return database.name;
            }

            return AssetDatabase.AssetPathToGUID(assetPath);
        }

        public static string GetSnapshotPath(string storageKey, string rootFolderOverride = null)
        {
            var root = string.IsNullOrWhiteSpace(rootFolderOverride)
                ? Path.Combine(Directory.GetCurrentDirectory(), "Library", AutosaveFolderName)
                : rootFolderOverride;

            Directory.CreateDirectory(root);
            return Path.Combine(root, $"{SanitizeFileName(storageKey)}.json");
        }

        public static void SaveSnapshot(DialogueDatabaseAsset database, string storageKey, string rootFolderOverride = null)
        {
            if (database == null || string.IsNullOrWhiteSpace(storageKey))
            {
                return;
            }

            var json = CaptureSnapshotJson(database, true);
            File.WriteAllText(GetSnapshotPath(storageKey, rootFolderOverride), json);
        }

        internal static string CaptureSnapshotJson(DialogueDatabaseAsset database, bool prettyPrint = false)
        {
            if (database == null)
            {
                return string.Empty;
            }

            var snapshot = DialogueDatabaseSnapshot.FromDatabase(database);
            return JsonUtility.ToJson(snapshot, prettyPrint);
        }

        public static bool TryLoadSnapshot(DialogueDatabaseAsset database, string storageKey, string rootFolderOverride = null)
        {
            if (database == null || string.IsNullOrWhiteSpace(storageKey))
            {
                return false;
            }

            var path = GetSnapshotPath(storageKey, rootFolderOverride);
            if (!File.Exists(path))
            {
                return false;
            }

            var json = File.ReadAllText(path);
            var snapshot = JsonUtility.FromJson<DialogueDatabaseSnapshot>(json);
            if (snapshot == null)
            {
                return false;
            }

            snapshot.ApplyTo(database);
            return true;
        }

        public static void ClearSnapshot(string storageKey, string rootFolderOverride = null)
        {
            if (string.IsNullOrWhiteSpace(storageKey))
            {
                return;
            }

            var path = GetSnapshotPath(storageKey, rootFolderOverride);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static string SanitizeFileName(string value)
        {
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidChar, '_');
            }

            return value;
        }

        [Serializable]
        private class DialogueDatabaseSnapshot
        {
            public List<NpcSnapshot> Npcs = new();

            public static DialogueDatabaseSnapshot FromDatabase(DialogueDatabaseAsset database)
            {
                var snapshot = new DialogueDatabaseSnapshot();
                foreach (var npc in database.Npcs)
                {
                    if (npc != null)
                    {
                        snapshot.Npcs.Add(NpcSnapshot.FromNpc(npc));
                    }
                }

                return snapshot;
            }

            public void ApplyTo(DialogueDatabaseAsset database)
            {
                database.Npcs.Clear();
                foreach (var npc in Npcs)
                {
                    if (npc != null)
                    {
                        database.Npcs.Add(npc.ToNpc());
                    }
                }
            }
        }

        [Serializable]
        private class NpcSnapshot
        {
            public string Id;
            public string Name;
            public List<DialogueSnapshot> Dialogues = new();

            public static NpcSnapshot FromNpc(NpcEntry npc)
            {
                var snapshot = new NpcSnapshot
                {
                    Id = npc.Id,
                    Name = npc.Name
                };

                foreach (var dialogue in npc.Dialogues)
                {
                    if (dialogue != null)
                    {
                        snapshot.Dialogues.Add(DialogueSnapshot.FromDialogue(dialogue));
                    }
                }

                return snapshot;
            }

            public NpcEntry ToNpc()
            {
                var npc = new NpcEntry
                {
                    Id = Id,
                    Name = Name
                };

                foreach (var dialogue in Dialogues)
                {
                    if (dialogue != null)
                    {
                        npc.Dialogues.Add(dialogue.ToDialogue());
                    }
                }

                return npc;
            }
        }

        [Serializable]
        private class DialogueSnapshot
        {
            public string Id;
            public string Name;
            public List<DialogueSpeakerEntry> Speakers = new();
            public ConditionData StartCondition;
            public GraphSnapshot Graph;

            public static DialogueSnapshot FromDialogue(DialogueEntry dialogue)
            {
                return new DialogueSnapshot
                {
                    Id = dialogue.Id,
                    Name = dialogue.Name,
                    Speakers = dialogue.Speakers?.Where(speaker => speaker != null).Select(speaker => speaker.Clone()).ToList() ?? new List<DialogueSpeakerEntry>(),
                    StartCondition = dialogue.StartCondition?.Clone() ?? new ConditionData(),
                    Graph = GraphSnapshot.FromGraph(dialogue.Graph)
                };
            }

            public DialogueEntry ToDialogue()
            {
                return new DialogueEntry
                {
                    Id = Id,
                    Name = Name,
                    Speakers = Speakers?.Where(speaker => speaker != null).Select(speaker => speaker.Clone()).ToList() ?? new List<DialogueSpeakerEntry>(),
                    StartCondition = StartCondition?.Clone() ?? new ConditionData(),
                    Graph = Graph?.ToGraph() ?? new DialogueGraphData()
                };
            }
        }

        [Serializable]
        private class GraphSnapshot
        {
            public List<NodeSnapshot> Nodes = new();
            public List<NodeLinkData> Links = new();

            public static GraphSnapshot FromGraph(DialogueGraphData graph)
            {
                var snapshot = new GraphSnapshot();

                if (graph == null)
                {
                    return snapshot;
                }

                foreach (var node in graph.Nodes)
                {
                    if (node != null)
                    {
                        snapshot.Nodes.Add(NodeSnapshot.FromNode(node));
                    }
                }

                foreach (var link in graph.Links)
                {
                    if (link != null)
                    {
                        snapshot.Links.Add(link.Clone());
                    }
                }

                return snapshot;
            }

            public DialogueGraphData ToGraph()
            {
                var graph = new DialogueGraphData();
                foreach (var node in Nodes)
                {
                    if (node != null)
                    {
                        graph.Nodes.Add(node.ToNode());
                    }
                }

                foreach (var link in Links)
                {
                    if (link != null)
                    {
                        graph.Links.Add(link.Clone());
                    }
                }

                return graph;
            }
        }

        [Serializable]
        private class NodeSnapshot
        {
            public string NodeType;
            public string Id;
            public string Title;
            public Vector2 Position;
            public ConditionData Condition;
            public string BodyText;
            public string VoiceKey;
            public string SpeakerId;
            public bool IsStartNode;
            public bool UseOutputsAsChoices;
            public Rect Area;
            public string Comment;
            public Color Tint;
            public string SceneKey;
            public DialogueSceneLoadMode LoadMode;
            public string EntryPointId;
            public string TransitionId;
            public bool CloseDialogueBeforeExecute;
            public bool WaitForCompletion;
            public string FunctionId;
            public DialogueExecutionFailurePolicy FailurePolicy;
            public string MessageTemplate;
            public DialogueDebugLogLevel LogLevel;
            public bool IncludeArguments;
            public List<DialogueArgumentEntry> Arguments = new();
            public List<DialogueArgumentEntry> Parameters = new();

            public static NodeSnapshot FromNode(BaseNodeData node)
            {
                var snapshot = new NodeSnapshot
                {
                    Id = node.Id,
                    Title = node.Title,
                    Position = node.Position,
                    Condition = node.Condition?.Clone() ?? new ConditionData()
                };

                switch (node)
                {
                    case DialogueTextNodeData textNode:
                        snapshot.NodeType = nameof(DialogueTextNodeData);
                        snapshot.BodyText = textNode.BodyText;
                        snapshot.VoiceKey = textNode.VoiceKey;
                        snapshot.SpeakerId = textNode.SpeakerId;
                        snapshot.IsStartNode = textNode.IsStartNode;
                        snapshot.UseOutputsAsChoices = textNode.UseOutputsAsChoices;
                        break;
                    case CommentNodeData commentNode:
                        snapshot.NodeType = nameof(CommentNodeData);
                        snapshot.Area = commentNode.Area;
                        snapshot.Comment = commentNode.Comment;
                        snapshot.Tint = commentNode.Tint;
                        break;
                    case FunctionNodeData functionNode:
                        snapshot.NodeType = nameof(FunctionNodeData);
                        snapshot.FunctionId = functionNode.FunctionId;
                        snapshot.CloseDialogueBeforeExecute = functionNode.CloseDialogueBeforeExecute;
                        snapshot.WaitForCompletion = functionNode.WaitForCompletion;
                        snapshot.FailurePolicy = functionNode.FailurePolicy;
                        snapshot.Arguments = CloneArguments(functionNode.Arguments);
                        break;
                    case SceneNodeData sceneNode:
                        snapshot.NodeType = nameof(SceneNodeData);
                        snapshot.SceneKey = sceneNode.SceneKey;
                        snapshot.LoadMode = sceneNode.LoadMode;
                        snapshot.EntryPointId = sceneNode.EntryPointId;
                        snapshot.TransitionId = sceneNode.TransitionId;
                        snapshot.CloseDialogueBeforeExecute = sceneNode.CloseDialogueBeforeExecute;
                        snapshot.WaitForCompletion = sceneNode.WaitForCompletion;
                        snapshot.Parameters = CloneArguments(sceneNode.Parameters);
                        break;
                    case DebugNodeData debugNode:
                        snapshot.NodeType = nameof(DebugNodeData);
                        snapshot.MessageTemplate = debugNode.MessageTemplate;
                        snapshot.LogLevel = debugNode.LogLevel;
                        snapshot.IncludeArguments = debugNode.IncludeArguments;
                        snapshot.FailurePolicy = debugNode.FailurePolicy;
                        snapshot.Arguments = CloneArguments(debugNode.Arguments);
                        break;
                }

                return snapshot;
            }

            public BaseNodeData ToNode()
            {
                if (NodeType == nameof(CommentNodeData))
                {
                    return new CommentNodeData
                    {
                        Id = Id,
                        Title = Title,
                        Position = Position,
                        Condition = Condition?.Clone() ?? new ConditionData(),
                        Area = Area,
                        Comment = Comment,
                        Tint = Tint
                    };
                }

                if (NodeType == nameof(FunctionNodeData))
                {
                    return new FunctionNodeData
                    {
                        Id = Id,
                        Title = Title,
                        Position = Position,
                        Condition = Condition?.Clone() ?? new ConditionData(),
                        FunctionId = FunctionId,
                        CloseDialogueBeforeExecute = CloseDialogueBeforeExecute,
                        WaitForCompletion = WaitForCompletion,
                        FailurePolicy = FailurePolicy,
                        Arguments = CloneArguments(Arguments)
                    };
                }

                if (NodeType == nameof(SceneNodeData))
                {
                    return new SceneNodeData
                    {
                        Id = Id,
                        Title = Title,
                        Position = Position,
                        Condition = Condition?.Clone() ?? new ConditionData(),
                        SceneKey = SceneKey,
                        LoadMode = LoadMode,
                        EntryPointId = EntryPointId,
                        TransitionId = TransitionId,
                        CloseDialogueBeforeExecute = CloseDialogueBeforeExecute,
                        WaitForCompletion = WaitForCompletion,
                        Parameters = CloneArguments(Parameters)
                    };
                }

                if (NodeType == nameof(DebugNodeData))
                {
                    return new DebugNodeData
                    {
                        Id = Id,
                        Title = Title,
                        Position = Position,
                        Condition = Condition?.Clone() ?? new ConditionData(),
                        MessageTemplate = MessageTemplate,
                        LogLevel = LogLevel,
                        IncludeArguments = IncludeArguments,
                        FailurePolicy = FailurePolicy,
                        Arguments = CloneArguments(Arguments)
                    };
                }

                return new DialogueTextNodeData
                {
                    Id = Id,
                    Title = Title,
                    Position = Position,
                    Condition = Condition?.Clone() ?? new ConditionData(),
                    BodyText = BodyText,
                    VoiceKey = VoiceKey,
                    SpeakerId = SpeakerId,
                    IsStartNode = IsStartNode,
                    UseOutputsAsChoices = UseOutputsAsChoices
                };
            }

            private static List<DialogueArgumentEntry> CloneArguments(IEnumerable<DialogueArgumentEntry> arguments)
            {
                return arguments?
                    .Where(argument => argument != null)
                    .Select(argument => argument.Clone())
                    .ToList() ?? new List<DialogueArgumentEntry>();
            }
        }
    }
}

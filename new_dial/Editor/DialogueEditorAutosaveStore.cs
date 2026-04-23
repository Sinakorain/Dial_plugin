using System;
using System.Collections.Generic;
using System.IO;
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

            var snapshot = DialogueDatabaseSnapshot.FromDatabase(database);
            var json = JsonUtility.ToJson(snapshot, true);
            File.WriteAllText(GetSnapshotPath(storageKey, rootFolderOverride), json);
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
            public ConditionData StartCondition;
            public GraphSnapshot Graph;

            public static DialogueSnapshot FromDialogue(DialogueEntry dialogue)
            {
                return new DialogueSnapshot
                {
                    Id = dialogue.Id,
                    Name = dialogue.Name,
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
            public bool IsStartNode;
            public bool UseOutputsAsChoices;
            public Rect Area;
            public string Comment;

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
                        snapshot.IsStartNode = textNode.IsStartNode;
                        snapshot.UseOutputsAsChoices = textNode.UseOutputsAsChoices;
                        break;
                    case CommentNodeData commentNode:
                        snapshot.NodeType = nameof(CommentNodeData);
                        snapshot.Area = commentNode.Area;
                        snapshot.Comment = commentNode.Comment;
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
                        Comment = Comment
                    };
                }

                return new DialogueTextNodeData
                {
                    Id = Id,
                    Title = Title,
                    Position = Position,
                    Condition = Condition?.Clone() ?? new ConditionData(),
                    BodyText = BodyText,
                    IsStartNode = IsStartNode,
                    UseOutputsAsChoices = UseOutputsAsChoices
                };
            }
        }
    }
}

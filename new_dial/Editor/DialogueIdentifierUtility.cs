using System.Collections.Generic;
using System.Linq;

namespace NewDial.DialogueEditor
{
    internal static class DialogueIdentifierUtility
    {
        public static IReadOnlyList<string> GetIssues(DialogueDatabaseAsset database, NpcEntry npc)
        {
            var issues = new List<string>();
            if (npc == null)
            {
                return issues;
            }

            AddEmptyIssue(issues, npc.Id);
            if (database?.Npcs != null &&
                !string.IsNullOrWhiteSpace(npc.Id) &&
                database.Npcs.Count(candidate => candidate != null && candidate.Id == npc.Id) > 1)
            {
                issues.Add("Duplicate NPC Id");
            }

            return issues;
        }

        public static IReadOnlyList<string> GetIssues(DialogueDatabaseAsset database, DialogueEntry dialogue)
        {
            var issues = new List<string>();
            if (dialogue == null)
            {
                return issues;
            }

            AddEmptyIssue(issues, dialogue.Id);
            if (!string.IsNullOrWhiteSpace(dialogue.Id) && CountDialoguesWithId(database, dialogue.Id) > 1)
            {
                issues.Add("Duplicate Dialogue Id");
            }

            return issues;
        }

        public static IReadOnlyList<string> GetIssues(DialogueGraphData graph, BaseNodeData node)
        {
            var issues = new List<string>();
            if (node == null)
            {
                return issues;
            }

            AddEmptyIssue(issues, node.Id);
            if (graph?.Nodes != null &&
                !string.IsNullOrWhiteSpace(node.Id) &&
                graph.Nodes.Count(candidate => candidate != null && candidate.Id == node.Id) > 1)
            {
                issues.Add("Duplicate Node Id");
            }

            return issues;
        }

        public static bool HasEmptyNpcId(DialogueDatabaseAsset database)
        {
            return database?.Npcs != null && database.Npcs.Any(npc => npc != null && string.IsNullOrWhiteSpace(npc.Id));
        }

        public static bool HasDuplicateNpcId(DialogueDatabaseAsset database)
        {
            return HasDuplicate(database?.Npcs?.Where(npc => npc != null).Select(npc => npc.Id));
        }

        public static bool HasEmptyDialogueId(DialogueDatabaseAsset database)
        {
            return database?.Npcs != null &&
                   database.Npcs.Where(npc => npc != null)
                       .SelectMany(npc => npc.Dialogues ?? Enumerable.Empty<DialogueEntry>())
                       .Any(dialogue => dialogue != null && string.IsNullOrWhiteSpace(dialogue.Id));
        }

        public static bool HasDuplicateDialogueId(DialogueDatabaseAsset database)
        {
            return HasDuplicate(database?.Npcs?
                .Where(npc => npc != null)
                .SelectMany(npc => npc.Dialogues ?? Enumerable.Empty<DialogueEntry>())
                .Where(dialogue => dialogue != null)
                .Select(dialogue => dialogue.Id));
        }

        public static bool HasEmptyNodeId(DialogueGraphData graph)
        {
            return graph?.Nodes != null && graph.Nodes.Any(node => node != null && string.IsNullOrWhiteSpace(node.Id));
        }

        public static bool HasDuplicateNodeId(DialogueGraphData graph)
        {
            return HasDuplicate(graph?.Nodes?.Where(node => node != null).Select(node => node.Id));
        }

        public static bool RenameNodeId(DialogueGraphData graph, BaseNodeData node, string newId)
        {
            if (graph == null || node == null)
            {
                return false;
            }

            var oldId = node.Id;
            node.Id = newId ?? string.Empty;

            if (graph.Links == null)
            {
                return !string.Equals(oldId, node.Id, System.StringComparison.Ordinal);
            }

            if (!string.IsNullOrWhiteSpace(oldId))
            {
                foreach (var link in graph.Links.Where(link => link != null))
                {
                    if (link.FromNodeId == oldId)
                    {
                        link.FromNodeId = node.Id;
                    }

                    if (link.ToNodeId == oldId)
                    {
                        link.ToNodeId = node.Id;
                    }
                }
            }

            var sourceNodeIds = graph.Links
                .Where(link => link != null && !string.IsNullOrWhiteSpace(link.FromNodeId))
                .Select(link => link.FromNodeId)
                .Distinct()
                .ToList();

            foreach (var sourceNodeId in sourceNodeIds)
            {
                DialogueGraphUtility.NormalizeLinkOrder(graph, sourceNodeId);
            }

            return !string.Equals(oldId, node.Id, System.StringComparison.Ordinal);
        }

        private static void AddEmptyIssue(ICollection<string> issues, string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                issues.Add("Id is empty");
            }
        }

        private static int CountDialoguesWithId(DialogueDatabaseAsset database, string id)
        {
            if (database?.Npcs == null)
            {
                return 0;
            }

            return database.Npcs
                .Where(npc => npc != null)
                .SelectMany(npc => npc.Dialogues ?? Enumerable.Empty<DialogueEntry>())
                .Count(dialogue => dialogue != null && dialogue.Id == id);
        }

        private static bool HasDuplicate(IEnumerable<string> ids)
        {
            if (ids == null)
            {
                return false;
            }

            var seen = new HashSet<string>();
            foreach (var id in ids.Where(id => !string.IsNullOrWhiteSpace(id)))
            {
                if (!seen.Add(id))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

using System.Collections.Generic;
using System.Linq;

namespace NewDial.DialogueEditor
{
    public enum DialogueReferenceTargetKind
    {
        Npc,
        Dialogue,
        Node
    }

    public enum DialogueReferenceKind
    {
        Internal,
        External
    }

    public readonly struct DialogueWhereUsedContext
    {
        public DialogueWhereUsedContext(
            DialogueDatabaseAsset database,
            NpcEntry npc,
            DialogueEntry dialogue,
            BaseNodeData node,
            DialogueReferenceTargetKind targetKind)
        {
            Database = database;
            Npc = npc;
            Dialogue = dialogue;
            Node = node;
            TargetKind = targetKind;
        }

        public DialogueDatabaseAsset Database { get; }

        public NpcEntry Npc { get; }

        public DialogueEntry Dialogue { get; }

        public BaseNodeData Node { get; }

        public DialogueReferenceTargetKind TargetKind { get; }
    }

    public readonly struct DialogueWhereUsedResult
    {
        public DialogueWhereUsedResult(DialogueReferenceKind kind, string label, string detail = null)
        {
            Kind = kind;
            Label = label ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public DialogueReferenceKind Kind { get; }

        public string Label { get; }

        public string Detail { get; }
    }

    public interface IDialogueExternalReferenceResolver
    {
        IEnumerable<DialogueWhereUsedResult> FindExternalReferences(DialogueWhereUsedContext context);
    }

    public static class DialogueExternalReferenceResolverRegistry
    {
        private static readonly List<IDialogueExternalReferenceResolver> Resolvers = new();

        public static void RegisterResolver(IDialogueExternalReferenceResolver resolver)
        {
            if (resolver != null && !Resolvers.Contains(resolver))
            {
                Resolvers.Add(resolver);
            }
        }

        public static void UnregisterResolver(IDialogueExternalReferenceResolver resolver)
        {
            if (resolver != null)
            {
                Resolvers.Remove(resolver);
            }
        }

        internal static IReadOnlyList<DialogueWhereUsedResult> FindExternalReferences(DialogueWhereUsedContext context)
        {
            return Resolvers
                .SelectMany(resolver => resolver.FindExternalReferences(context) ?? Enumerable.Empty<DialogueWhereUsedResult>())
                .Where(result => !string.IsNullOrWhiteSpace(result.Label))
                .ToList();
        }
    }

    internal static class DialogueWhereUsedUtility
    {
        public static IReadOnlyList<DialogueWhereUsedResult> GetWhereUsed(
            DialogueDatabaseAsset database,
            NpcEntry npc,
            DialogueEntry dialogue = null,
            BaseNodeData node = null)
        {
            var targetKind = node != null
                ? DialogueReferenceTargetKind.Node
                : dialogue != null
                    ? DialogueReferenceTargetKind.Dialogue
                    : DialogueReferenceTargetKind.Npc;
            var context = new DialogueWhereUsedContext(database, npc, dialogue, node, targetKind);
            var results = new List<DialogueWhereUsedResult>();
            AddInternalReferences(results, context);
            results.AddRange(DialogueExternalReferenceResolverRegistry.FindExternalReferences(context));
            return results;
        }

        private static void AddInternalReferences(ICollection<DialogueWhereUsedResult> results, DialogueWhereUsedContext context)
        {
            switch (context.TargetKind)
            {
                case DialogueReferenceTargetKind.Npc:
                    var dialogueCount = context.Npc?.Dialogues?.Count(dialogue => dialogue != null) ?? 0;
                    results.Add(new DialogueWhereUsedResult(
                        DialogueReferenceKind.Internal,
                        "NPC owns dialogues in this database.",
                        $"{dialogueCount} dialogue(s)"));
                    break;
                case DialogueReferenceTargetKind.Dialogue:
                    results.Add(new DialogueWhereUsedResult(
                        DialogueReferenceKind.Internal,
                        "Dialogue belongs to NPC.",
                        context.Npc == null ? "Owner NPC missing" : $"{context.Npc.Name} ({context.Npc.Id})"));
                    break;
                case DialogueReferenceTargetKind.Node:
                    AddNodeReferences(results, context.Dialogue?.Graph, context.Node);
                    break;
            }
        }

        private static void AddNodeReferences(
            ICollection<DialogueWhereUsedResult> results,
            DialogueGraphData graph,
            BaseNodeData node)
        {
            if (graph == null || node == null || string.IsNullOrWhiteSpace(node.Id))
            {
                results.Add(new DialogueWhereUsedResult(
                    DialogueReferenceKind.Internal,
                    "Node graph references cannot be resolved.",
                    "Missing graph or node id"));
                return;
            }

            var incoming = graph.Links?
                .Where(link => link != null && link.ToNodeId == node.Id)
                .ToList() ?? new List<NodeLinkData>();
            var outgoing = graph.Links?
                .Where(link => link != null && link.FromNodeId == node.Id)
                .ToList() ?? new List<NodeLinkData>();

            if (incoming.Count == 0 && outgoing.Count == 0)
            {
                results.Add(new DialogueWhereUsedResult(
                    DialogueReferenceKind.Internal,
                    "Node has no internal graph links."));
                return;
            }

            foreach (var link in incoming)
            {
                var source = GetNode(graph, link.FromNodeId);
                results.Add(new DialogueWhereUsedResult(
                    DialogueReferenceKind.Internal,
                    "Incoming link",
                    source == null ? link.FromNodeId : $"{source.Title} ({source.Id})"));
            }

            foreach (var link in outgoing)
            {
                var target = GetNode(graph, link.ToNodeId);
                results.Add(new DialogueWhereUsedResult(
                    DialogueReferenceKind.Internal,
                    "Outgoing link",
                    target == null ? link.ToNodeId : $"{target.Title} ({target.Id})"));
            }
        }

        private static BaseNodeData GetNode(DialogueGraphData graph, string nodeId)
        {
            if (graph?.Nodes == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return null;
            }

            return graph.Nodes.FirstOrDefault(node => node != null && node.Id == nodeId);
        }
    }
}

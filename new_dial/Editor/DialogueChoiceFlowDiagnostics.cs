using System.Collections.Generic;
using System.Linq;

namespace NewDial.DialogueEditor
{
    internal enum DialogueChoiceFlowSeverity
    {
        Warning,
        Error
    }

    internal readonly struct DialogueChoiceFlowDiagnostic
    {
        public DialogueChoiceFlowDiagnostic(DialogueChoiceFlowSeverity severity, string message, NodeLinkData link = null)
        {
            Severity = severity;
            Message = message;
            Link = link;
        }

        public DialogueChoiceFlowSeverity Severity { get; }

        public string Message { get; }

        public NodeLinkData Link { get; }
    }

    internal static class DialogueChoiceFlowDiagnostics
    {
        public static IReadOnlyList<DialogueChoiceFlowDiagnostic> Analyze(DialogueEntry dialogue, DialogueTextNodeData node)
        {
            var diagnostics = new List<DialogueChoiceFlowDiagnostic>();
            var graph = dialogue?.Graph;
            if (graph == null || node == null || !node.UseOutputsAsChoices)
            {
                return diagnostics;
            }

            var links = graph.Links?
                .Where(link => link != null && link.FromNodeId == node.Id)
                .OrderBy(link => link.Order)
                .ThenBy(link => link.Id)
                .ToList() ?? new List<NodeLinkData>();

            if (links.Count == 0)
            {
                diagnostics.Add(new DialogueChoiceFlowDiagnostic(
                    DialogueChoiceFlowSeverity.Error,
                    DialogueEditorLocalization.Text("Choice node has no outgoing links.")));
                return diagnostics;
            }

            AddOrderingDiagnostics(diagnostics, links);

            var reachableNodeIds = GetReachableTextNodeIds(dialogue);
            foreach (var link in links)
            {
                var target = GetTextNode(graph, link.ToNodeId);
                if (target == null)
                {
                    diagnostics.Add(new DialogueChoiceFlowDiagnostic(
                        DialogueChoiceFlowSeverity.Error,
                        DialogueEditorLocalization.Text("Choice target is missing or invalid."),
                        link));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(link.ChoiceText))
                {
                    diagnostics.Add(new DialogueChoiceFlowDiagnostic(
                        string.IsNullOrWhiteSpace(target.Title)
                            ? DialogueChoiceFlowSeverity.Error
                            : DialogueChoiceFlowSeverity.Warning,
                        string.IsNullOrWhiteSpace(target.Title)
                            ? DialogueEditorLocalization.Text("Choice text and target title are both empty.")
                            : DialogueEditorLocalization.Text("Choice text is empty; target title will be used."),
                        link));
                }

                if (reachableNodeIds.Count > 0 && !reachableNodeIds.Contains(target.Id))
                {
                    diagnostics.Add(new DialogueChoiceFlowDiagnostic(
                        DialogueChoiceFlowSeverity.Warning,
                        DialogueEditorLocalization.Text("Choice target is not reachable from the dialogue start."),
                        link));
                }
            }

            return diagnostics;
        }

        private static void AddOrderingDiagnostics(
            ICollection<DialogueChoiceFlowDiagnostic> diagnostics,
            IReadOnlyList<NodeLinkData> links)
        {
            foreach (var link in links.Where(link => link.Order < 0))
            {
                diagnostics.Add(new DialogueChoiceFlowDiagnostic(
                    DialogueChoiceFlowSeverity.Warning,
                    DialogueEditorLocalization.Text("Choice order is negative."),
                    link));
            }

            foreach (var group in links.GroupBy(link => link.Order).Where(group => group.Count() > 1))
            {
                foreach (var link in group)
                {
                    diagnostics.Add(new DialogueChoiceFlowDiagnostic(
                        DialogueChoiceFlowSeverity.Warning,
                        DialogueEditorLocalization.Text("Choice order conflicts with another choice."),
                        link));
                }
            }
        }

        private static HashSet<string> GetReachableTextNodeIds(DialogueEntry dialogue)
        {
            var result = new HashSet<string>();
            var graph = dialogue?.Graph;
            if (graph?.Nodes == null)
            {
                return result;
            }

            var startNode = graph.Nodes.OfType<DialogueTextNodeData>().FirstOrDefault(node => node.IsStartNode) ??
                            graph.Nodes.OfType<DialogueTextNodeData>().FirstOrDefault();
            if (graph == null || startNode == null || string.IsNullOrWhiteSpace(startNode.Id))
            {
                return result;
            }

            var pending = new Queue<string>();
            pending.Enqueue(startNode.Id);

            while (pending.Count > 0)
            {
                var nodeId = pending.Dequeue();
                if (!result.Add(nodeId))
                {
                    continue;
                }

                var outgoingLinks = graph.Links?
                    .Where(link => link != null && link.FromNodeId == nodeId)
                    .OrderBy(link => link.Order)
                    .ThenBy(link => link.Id) ?? Enumerable.Empty<NodeLinkData>();
                foreach (var link in outgoingLinks)
                {
                    var target = GetTextNode(graph, link.ToNodeId);
                    if (target != null && !string.IsNullOrWhiteSpace(target.Id) && !result.Contains(target.Id))
                    {
                        pending.Enqueue(target.Id);
                    }
                }
            }

            return result;
        }

        private static DialogueTextNodeData GetTextNode(DialogueGraphData graph, string nodeId)
        {
            if (graph?.Nodes == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return null;
            }

            return graph.Nodes.FirstOrDefault(node => node != null && node.Id == nodeId) as DialogueTextNodeData;
        }
    }
}

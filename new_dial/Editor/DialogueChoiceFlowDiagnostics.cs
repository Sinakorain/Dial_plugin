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
            if (graph == null || node == null || !DialogueGraphUtility.UsesChoices(graph, node))
            {
                return diagnostics;
            }

            var links = DialogueGraphUtility.GetChoiceCandidateLinks(graph, node);

            if (links.Count == 0)
            {
                diagnostics.Add(new DialogueChoiceFlowDiagnostic(
                    DialogueChoiceFlowSeverity.Error,
                    DialogueEditorLocalization.Text("Text node has no answers.")));
                return diagnostics;
            }

            AddOrderingDiagnostics(diagnostics, links);

            var reachableNodeIds = GetReachableRuntimeNodeIds(dialogue);
            foreach (var link in links)
            {
                var target = GetNode(graph, link.ToNodeId);
                if (target == null || !DialogueGraphUtility.IsRuntimeNode(target))
                {
                    diagnostics.Add(new DialogueChoiceFlowDiagnostic(
                        DialogueChoiceFlowSeverity.Error,
                        DialogueEditorLocalization.Text("Choice target is missing or invalid."),
                        link));
                    continue;
                }

                if (target is DialogueChoiceNodeData choiceNode)
                {
                    AnalyzeChoiceNodeTarget(diagnostics, graph, reachableNodeIds, link, choiceNode);
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

                diagnostics.Add(new DialogueChoiceFlowDiagnostic(
                    DialogueChoiceFlowSeverity.Warning,
                    DialogueEditorLocalization.Text("Legacy answer link. Use Add Choice to create an answer node."),
                    link));
            }

            return diagnostics;
        }

        private static void AnalyzeChoiceNodeTarget(
            ICollection<DialogueChoiceFlowDiagnostic> diagnostics,
            DialogueGraphData graph,
            HashSet<string> reachableNodeIds,
            NodeLinkData parentLink,
            DialogueChoiceNodeData choiceNode)
        {
            if (string.IsNullOrWhiteSpace(choiceNode.ChoiceText))
            {
                diagnostics.Add(new DialogueChoiceFlowDiagnostic(
                    DialogueChoiceFlowSeverity.Error,
                    DialogueEditorLocalization.Text("Button text is empty."),
                    parentLink));
            }

            var links = graph.Links?
                .Where(link => link != null && link.FromNodeId == choiceNode.Id)
                .OrderBy(link => link.Order)
                .ThenBy(link => link.Id)
                .ToList() ?? new List<NodeLinkData>();

            if (reachableNodeIds.Count > 0 && !reachableNodeIds.Contains(choiceNode.Id))
            {
                diagnostics.Add(new DialogueChoiceFlowDiagnostic(
                    DialogueChoiceFlowSeverity.Warning,
                    DialogueEditorLocalization.Text("Choice target is not reachable from the dialogue start."),
                    parentLink));
            }

            AddOrderingDiagnostics(diagnostics, links);
            foreach (var link in links)
            {
                var target = GetNode(graph, link.ToNodeId);
                if (target == null || !DialogueGraphUtility.IsRuntimeNode(target))
                {
                    diagnostics.Add(new DialogueChoiceFlowDiagnostic(
                        DialogueChoiceFlowSeverity.Error,
                        DialogueEditorLocalization.Text("Choice target is missing or invalid."),
                        parentLink));
                    continue;
                }

                if (reachableNodeIds.Count > 0 && !reachableNodeIds.Contains(target.Id))
                {
                    diagnostics.Add(new DialogueChoiceFlowDiagnostic(
                        DialogueChoiceFlowSeverity.Warning,
                        DialogueEditorLocalization.Text("Choice target is not reachable from the dialogue start."),
                        parentLink));
                }
            }
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

        private static HashSet<string> GetReachableRuntimeNodeIds(DialogueEntry dialogue)
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
                    var target = GetNode(graph, link.ToNodeId);
                    if (target != null &&
                        DialogueGraphUtility.IsRuntimeNode(target) &&
                        !string.IsNullOrWhiteSpace(target.Id) &&
                        !result.Contains(target.Id))
                    {
                        pending.Enqueue(target.Id);
                    }
                }
            }

            return result;
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

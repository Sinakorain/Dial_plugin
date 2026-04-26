using System.Collections.Generic;
using System.Linq;

namespace NewDial.DialogueEditor
{
    public static class DialogueGraphUtility
    {
        public static DialogueTextNodeData FindStartNode(DialogueEntry dialogue)
        {
            if (dialogue?.Graph == null)
            {
                return null;
            }

            return dialogue.Graph.Nodes.OfType<DialogueTextNodeData>().FirstOrDefault(node => node.IsStartNode) ??
                   dialogue.Graph.Nodes.OfType<DialogueTextNodeData>().FirstOrDefault();
        }

        public static BaseNodeData GetNode(DialogueGraphData graph, string nodeId)
        {
            if (graph == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return null;
            }

            return graph.Nodes.FirstOrDefault(node => node != null && node.Id == nodeId);
        }

        public static DialogueTextNodeData GetTextNode(DialogueGraphData graph, string nodeId)
        {
            return GetNode(graph, nodeId) as DialogueTextNodeData;
        }

        public static DialogueChoiceNodeData GetChoiceNode(DialogueGraphData graph, string nodeId)
        {
            return GetNode(graph, nodeId) as DialogueChoiceNodeData;
        }

        public static bool IsExecutableNode(BaseNodeData node)
        {
            return node is FunctionNodeData or SceneNodeData or DebugNodeData;
        }

        public static bool IsRuntimeNode(BaseNodeData node)
        {
            return node is DialogueTextNodeData or DialogueChoiceNodeData || IsExecutableNode(node);
        }

        public static List<NodeLinkData> GetOutgoingLinks(DialogueGraphData graph, string nodeId)
        {
            if (graph == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return new List<NodeLinkData>();
            }

            return graph.Links
                .Where(link => link != null && link.FromNodeId == nodeId)
                .OrderBy(link => link.Order)
                .ThenBy(link => link.Id)
                .ToList();
        }

        public static bool HasAnswerOutputs(DialogueGraphData graph, DialogueTextNodeData node)
        {
            if (graph == null || node == null)
            {
                return false;
            }

            return GetOutgoingLinks(graph, node.Id)
                .Any(link => GetNode(graph, link.ToNodeId) is DialogueChoiceNodeData);
        }

        public static bool UsesChoices(DialogueGraphData graph, DialogueTextNodeData node)
        {
            return node != null && (node.UseOutputsAsChoices || HasAnswerOutputs(graph, node));
        }

        public static List<NodeLinkData> GetChoiceCandidateLinks(DialogueGraphData graph, DialogueTextNodeData node)
        {
            if (graph == null || node == null)
            {
                return new List<NodeLinkData>();
            }

            var links = GetOutgoingLinks(graph, node.Id);
            if (node.UseOutputsAsChoices)
            {
                return links;
            }

            return links
                .Where(link => GetNode(graph, link.ToNodeId) is DialogueChoiceNodeData)
                .ToList();
        }

        public static List<NodeLinkData> GetIncomingLinks(DialogueGraphData graph, string nodeId)
        {
            if (graph == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return new List<NodeLinkData>();
            }

            return graph.Links
                .Where(link => link != null && link.ToNodeId == nodeId)
                .OrderBy(link => link.Order)
                .ThenBy(link => link.Id)
                .ToList();
        }

        public static void EnsureSingleStartNode(DialogueGraphData graph, string startNodeId)
        {
            if (graph == null)
            {
                return;
            }

            foreach (var textNode in graph.Nodes.OfType<DialogueTextNodeData>())
            {
                textNode.IsStartNode = textNode.Id == startNodeId;
            }
        }

        public static void DeleteNode(DialogueGraphData graph, string nodeId)
        {
            if (graph == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            graph.Nodes.RemoveAll(node => node != null && node.Id == nodeId);
            graph.Links.RemoveAll(link => link != null && (link.FromNodeId == nodeId || link.ToNodeId == nodeId));
        }

        public static void NormalizeLinkOrder(DialogueGraphData graph, string nodeId)
        {
            if (graph == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            var ordered = GetOutgoingLinks(graph, nodeId);
            for (var index = 0; index < ordered.Count; index++)
            {
                ordered[index].Order = index;
            }
        }
    }
}

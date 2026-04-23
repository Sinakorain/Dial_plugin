using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewDial.DialogueEditor
{
    [Serializable]
    public class DialogueGraphData
    {
        [SerializeReference]
        public List<BaseNodeData> Nodes = new();

        public List<NodeLinkData> Links = new();

        public DialogueGraphData Clone()
        {
            var clone = new DialogueGraphData();
            foreach (var node in Nodes)
            {
                if (node != null)
                {
                    clone.Nodes.Add(node.Clone());
                }
            }

            foreach (var link in Links)
            {
                if (link != null)
                {
                    clone.Links.Add(link.Clone());
                }
            }

            return clone;
        }
    }
}

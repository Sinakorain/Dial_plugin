// Copyright (c) 2026 Danil Kashulin. All rights reserved.

using System;

namespace NewDial.DialogueEditor
{
    [Serializable]
    public class NodeLinkData
    {
        public string Id = GuidUtility.NewGuid();
        public string FromNodeId = string.Empty;
        public string ToNodeId = string.Empty;
        public int Order;
        public string ChoiceText = string.Empty;

        public NodeLinkData Clone()
        {
            return new NodeLinkData
            {
                Id = Id,
                FromNodeId = FromNodeId,
                ToNodeId = ToNodeId,
                Order = Order,
                ChoiceText = ChoiceText
            };
        }
    }
}

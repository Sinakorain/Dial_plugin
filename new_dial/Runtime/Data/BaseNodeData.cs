using System;
using UnityEngine;

namespace NewDial.DialogueEditor
{
    [Serializable]
    public abstract class BaseNodeData
    {
        public string Id = GuidUtility.NewGuid();
        public string Title = "Node";
        public Vector2 Position = Vector2.zero;
        public ConditionData Condition = new();

        public abstract BaseNodeData Clone();
    }
}

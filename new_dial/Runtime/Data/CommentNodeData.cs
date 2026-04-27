// Copyright (c) 2026 Danil Kashulin. All rights reserved.

using System;
using UnityEngine;

namespace NewDial.DialogueEditor
{
    [Serializable]
    public class CommentNodeData : BaseNodeData
    {
        public Rect Area = new(0f, 0f, 300f, 160f);
        public string Comment = string.Empty;
        public Color Tint = new(0.23f, 0.34f, 0.56f, 0.26f);

        public override BaseNodeData Clone()
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
    }
}

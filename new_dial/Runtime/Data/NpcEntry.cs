// Copyright (c) 2026 Danil Kashulin. All rights reserved.

using System;
using System.Collections.Generic;

namespace NewDial.DialogueEditor
{
    [Serializable]
    public class NpcEntry
    {
        public string Id = GuidUtility.NewGuid();
        public string Name = "NPC";
        public List<DialogueEntry> Dialogues = new();

        public NpcEntry Clone()
        {
            var clone = new NpcEntry
            {
                Id = Id,
                Name = Name
            };

            foreach (var dialogue in Dialogues)
            {
                if (dialogue != null)
                {
                    clone.Dialogues.Add(dialogue.Clone());
                }
            }

            return clone;
        }
    }
}

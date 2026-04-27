// Copyright (c) 2026 Danil Kashulin. All rights reserved.

using System.Collections.Generic;
using UnityEngine;

namespace NewDial.DialogueEditor
{
    [CreateAssetMenu(fileName = "DialogueDatabase", menuName = "New Dial/Dialogue Database")]
    public class DialogueDatabaseAsset : ScriptableObject
    {
        public List<DialogueVariableDefinition> Variables = new();
        public List<NpcEntry> Npcs = new();

        public void CopyFrom(DialogueDatabaseAsset other)
        {
            Variables.Clear();
            Npcs.Clear();
            if (other == null)
            {
                return;
            }

            foreach (var variable in other.Variables ?? new List<DialogueVariableDefinition>())
            {
                if (variable != null)
                {
                    Variables.Add(variable.Clone());
                }
            }

            foreach (var npc in other.Npcs)
            {
                if (npc != null)
                {
                    Npcs.Add(npc.Clone());
                }
            }
        }
    }
}

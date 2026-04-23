using System.Collections.Generic;
using UnityEngine;

namespace NewDial.DialogueEditor
{
    [CreateAssetMenu(fileName = "DialogueDatabase", menuName = "New Dial/Dialogue Database")]
    public class DialogueDatabaseAsset : ScriptableObject
    {
        public List<NpcEntry> Npcs = new();

        public void CopyFrom(DialogueDatabaseAsset other)
        {
            Npcs.Clear();
            if (other == null)
            {
                return;
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

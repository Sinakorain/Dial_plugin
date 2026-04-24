using System.Linq;

namespace NewDial.DialogueEditor
{
    public static class DialogueSpeakerUtility
    {
        public static DialogueSpeakerEntry ResolveSpeaker(DialogueEntry dialogue, DialogueTextNodeData node)
        {
            if (dialogue?.Speakers == null || dialogue.Speakers.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(node?.SpeakerId))
            {
                var explicitSpeaker = dialogue.Speakers.FirstOrDefault(speaker =>
                    speaker != null && speaker.Id == node.SpeakerId);
                if (explicitSpeaker != null)
                {
                    return explicitSpeaker;
                }
            }

            return dialogue.Speakers.FirstOrDefault(speaker => speaker != null);
        }

        public static string ResolveSpeakerName(DialogueEntry dialogue, DialogueTextNodeData node)
        {
            return ResolveSpeaker(dialogue, node)?.Name ?? string.Empty;
        }
    }
}

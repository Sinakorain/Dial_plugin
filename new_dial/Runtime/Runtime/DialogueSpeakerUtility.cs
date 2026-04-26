using System.Linq;

namespace NewDial.DialogueEditor
{
    public static class DialogueSpeakerUtility
    {
        public static DialogueSpeakerEntry ResolveSpeaker(DialogueEntry dialogue, BaseNodeData node)
        {
            if (dialogue?.Speakers == null || dialogue.Speakers.Count == 0)
            {
                return null;
            }

            var speakerId = GetSpeakerId(node);
            if (!string.IsNullOrWhiteSpace(speakerId))
            {
                var explicitSpeaker = dialogue.Speakers.FirstOrDefault(speaker =>
                    speaker != null && speaker.Id == speakerId);
                if (explicitSpeaker != null)
                {
                    return explicitSpeaker;
                }
            }

            return dialogue.Speakers.FirstOrDefault(speaker => speaker != null);
        }

        public static DialogueSpeakerEntry ResolveSpeaker(DialogueEntry dialogue, DialogueTextNodeData node)
        {
            return ResolveSpeaker(dialogue, (BaseNodeData)node);
        }

        public static string ResolveSpeakerName(DialogueEntry dialogue, DialogueTextNodeData node)
        {
            return ResolveSpeaker(dialogue, node)?.Name ?? string.Empty;
        }

        public static string ResolveSpeakerName(DialogueEntry dialogue, BaseNodeData node)
        {
            return ResolveSpeaker(dialogue, node)?.Name ?? string.Empty;
        }

        private static string GetSpeakerId(BaseNodeData node)
        {
            return node switch
            {
                DialogueTextNodeData textNode => textNode.SpeakerId,
                DialogueChoiceNodeData choiceNode => choiceNode.SpeakerId,
                _ => string.Empty
            };
        }
    }
}

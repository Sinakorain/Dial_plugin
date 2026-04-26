namespace NewDial.DialogueEditor
{
    public readonly struct DialogueChoice
    {
        public DialogueChoice(NodeLinkData link, DialogueTextNodeData target)
            : this(link, target, null)
        {
        }

        public DialogueChoice(NodeLinkData link, BaseNodeData targetNode, DialogueChoiceNodeData choiceNode = null)
        {
            Link = link;
            TargetNode = targetNode;
            ChoiceNode = choiceNode;
        }

        public NodeLinkData Link { get; }

        public BaseNodeData TargetNode { get; }

        public DialogueTextNodeData Target => TargetNode as DialogueTextNodeData;

        public DialogueChoiceNodeData ChoiceNode { get; }

        public string Text
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ChoiceNode?.ChoiceText))
                {
                    return ChoiceNode.ChoiceText;
                }

                if (!string.IsNullOrWhiteSpace(Link?.ChoiceText))
                {
                    return Link.ChoiceText;
                }

                return string.IsNullOrWhiteSpace(TargetNode?.Title) ? "Choice" : TargetNode.Title;
            }
        }
    }
}

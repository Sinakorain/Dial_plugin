namespace NewDial.DialogueEditor
{
    public readonly struct DialogueChoice
    {
        public DialogueChoice(NodeLinkData link, DialogueTextNodeData target)
        {
            Link = link;
            Target = target;
        }

        public NodeLinkData Link { get; }

        public DialogueTextNodeData Target { get; }

        public string Text => string.IsNullOrWhiteSpace(Link.ChoiceText) ? Target?.Title ?? "Choice" : Link.ChoiceText;
    }
}

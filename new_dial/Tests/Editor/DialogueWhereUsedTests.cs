using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace NewDial.DialogueEditor.Tests
{
    public class DialogueWhereUsedTests
    {
        [Test]
        public void GetWhereUsed_ReturnsNodeInternalReferences()
        {
            var database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            var npc = new NpcEntry();
            var dialogue = new DialogueEntry();
            var start = new DialogueTextNodeData { Title = "Start" };
            var target = new DialogueTextNodeData { Title = "Target" };
            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(target);
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = target.Id
            });
            npc.Dialogues.Add(dialogue);
            database.Npcs.Add(npc);

            var results = DialogueWhereUsedUtility.GetWhereUsed(database, npc, dialogue, target);

            Assert.That(results.Any(result =>
                result.Kind == DialogueReferenceKind.Internal &&
                result.Label == "Incoming link" &&
                result.Detail.Contains(start.Id)), Is.True);
        }

        [Test]
        public void ExternalResolverResults_AreIncluded()
        {
            var resolver = new TestExternalReferenceResolver();
            DialogueExternalReferenceResolverRegistry.RegisterResolver(resolver);

            try
            {
                var database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
                var npc = new NpcEntry();

                var results = DialogueWhereUsedUtility.GetWhereUsed(database, npc);

                Assert.That(results.Any(result =>
                    result.Kind == DialogueReferenceKind.External &&
                    result.Label == "Scene reference" &&
                    result.Detail == "IntroScene"), Is.True);
                Assert.That(resolver.LastContext.TargetKind, Is.EqualTo(DialogueReferenceTargetKind.Npc));
            }
            finally
            {
                DialogueExternalReferenceResolverRegistry.UnregisterResolver(resolver);
            }
        }

        private sealed class TestExternalReferenceResolver : IDialogueExternalReferenceResolver
        {
            public DialogueWhereUsedContext LastContext { get; private set; }

            public IEnumerable<DialogueWhereUsedResult> FindExternalReferences(DialogueWhereUsedContext context)
            {
                LastContext = context;
                yield return new DialogueWhereUsedResult(DialogueReferenceKind.External, "Scene reference", "IntroScene");
            }
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NewDial.DialogueEditor.Tests
{
    public class DialoguePlayerTests
    {
        [Test]
        public void Next_FollowsFirstValidTargetByOrder()
        {
            var dialogue = new DialogueEntry
            {
                Name = "Traversal Test"
            };

            var start = new DialogueTextNodeData
            {
                Title = "Start",
                BodyText = "Start",
                IsStartNode = true
            };

            var blocked = new DialogueTextNodeData
            {
                Title = "Blocked",
                BodyText = "Blocked",
                Condition = new ConditionData
                {
                    Type = ConditionType.VariableCheck,
                    Key = "allow_blocked",
                    Operator = "==",
                    Value = "true"
                }
            };

            var valid = new DialogueTextNodeData
            {
                Title = "Valid",
                BodyText = "Valid"
            };

            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(blocked);
            dialogue.Graph.Nodes.Add(valid);

            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = blocked.Id,
                Order = 0
            });

            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = valid.Id,
                Order = 1
            });

            var player = new DialoguePlayer(
                new DefaultDialogueConditionEvaluator(),
                new DictionaryDialogueVariableStore(new Dictionary<string, string>()));

            Assert.That(player.Start(dialogue), Is.True);
            Assert.That(player.Next(), Is.True);
            Assert.That(player.CurrentNode, Is.EqualTo(valid));
        }

        [Test]
        public void Choose_UsesChoiceModeOutputs()
        {
            var dialogue = new DialogueEntry
            {
                Name = "Choice Test"
            };

            var start = new DialogueTextNodeData
            {
                Title = "Question",
                BodyText = "Choose a branch.",
                IsStartNode = true,
                UseOutputsAsChoices = true
            };

            var left = new DialogueTextNodeData
            {
                Title = "Left",
                BodyText = "Left branch"
            };

            var right = new DialogueTextNodeData
            {
                Title = "Right",
                BodyText = "Right branch"
            };

            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(left);
            dialogue.Graph.Nodes.Add(right);

            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = left.Id,
                Order = 0,
                ChoiceText = "Go left"
            });

            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = right.Id,
                Order = 1,
                ChoiceText = "Go right"
            });

            var player = new DialoguePlayer();
            Assert.That(player.Start(dialogue), Is.True);
            Assert.That(player.CurrentChoices.Count, Is.EqualTo(2));

            Assert.That(player.Choose(1), Is.True);
            Assert.That(player.CurrentNode, Is.EqualTo(right));
        }

        [Test]
        public void GraphUtility_DeleteNode_RemovesConnectedLinks()
        {
            var graph = new DialogueGraphData();

            var a = new DialogueTextNodeData { Title = "A" };
            var b = new DialogueTextNodeData { Title = "B" };
            graph.Nodes.Add(a);
            graph.Nodes.Add(b);
            graph.Links.Add(new NodeLinkData
            {
                FromNodeId = a.Id,
                ToNodeId = b.Id,
                Order = 0
            });

            DialogueGraphUtility.DeleteNode(graph, a.Id);

            Assert.That(graph.Nodes, Has.Count.EqualTo(1));
            Assert.That(graph.Links, Is.Empty);
        }

        [Test]
        public void Next_ExecutesFunctionNodeAndAdvancesToNextTextNode()
        {
            var dialogue = new DialogueEntry { Name = "Function Test" };
            var start = new DialogueTextNodeData { Title = "Start", IsStartNode = true };
            var function = new FunctionNodeData { Title = "Do Thing", FunctionId = "test.function" };
            var end = new DialogueTextNodeData { Title = "End" };
            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(function);
            dialogue.Graph.Nodes.Add(end);
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = start.Id, ToNodeId = function.Id, Order = 0 });
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = function.Id, ToNodeId = end.Id, Order = 0, ChoiceText = "Ignored" });

            var executor = new RecordingFunctionExecutor(DialogueExecutionResult.Success());
            var player = new DialoguePlayer(functionExecutor: executor);

            Assert.That(player.Start(dialogue), Is.True);
            Assert.That(player.Next(), Is.True);
            Assert.That(executor.ExecutedFunctionIds, Is.EqualTo(new[] { "test.function" }));
            Assert.That(player.CurrentNode, Is.EqualTo(end));
        }

        [Test]
        public void Next_ExecutesSceneNodeAndEndsWhenNoOutgoingLink()
        {
            var dialogue = new DialogueEntry { Name = "Scene Test" };
            var start = new DialogueTextNodeData { Title = "Start", IsStartNode = true };
            var scene = new SceneNodeData { Title = "Load", SceneKey = "Intro" };
            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(scene);
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = start.Id, ToNodeId = scene.Id, Order = 0 });

            var player = new DialoguePlayer(sceneExecutor: new RecordingSceneExecutor(DialogueExecutionResult.Success()));

            Assert.That(player.Start(dialogue), Is.True);
            Assert.That(player.Next(), Is.False);
            Assert.That(player.CurrentNode, Is.Null);
        }

        [Test]
        public void Next_DebugNodeLogsAndContinues()
        {
            var dialogue = new DialogueEntry { Name = "Debug Test" };
            var start = new DialogueTextNodeData { Title = "Start", IsStartNode = true };
            var debug = new DebugNodeData
            {
                Title = "Debug",
                MessageTemplate = "Reached debug",
                IncludeArguments = true,
                Arguments = new List<DialogueArgumentEntry>
                {
                    new() { Name = "flag", Value = DialogueArgumentValue.FromBool(true) }
                }
            };
            var end = new DialogueTextNodeData { Title = "End" };
            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(debug);
            dialogue.Graph.Nodes.Add(end);
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = start.Id, ToNodeId = debug.Id, Order = 0 });
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = debug.Id, ToNodeId = end.Id, Order = 0 });

            LogAssert.Expect(LogType.Log, "Reached debug [flag=true]");
            var player = new DialoguePlayer();

            Assert.That(player.Start(dialogue), Is.True);
            Assert.That(player.Next(), Is.True);
            Assert.That(player.CurrentNode, Is.EqualTo(end));
        }

        [Test]
        public void FunctionFailure_StopDialogueEndsWithRuntimeError()
        {
            var dialogue = new DialogueEntry { Name = "Failure Test" };
            var start = new DialogueTextNodeData { Title = "Start", IsStartNode = true };
            var function = new FunctionNodeData
            {
                Title = "Fail",
                FunctionId = "fail",
                FailurePolicy = DialogueExecutionFailurePolicy.StopDialogue
            };
            var end = new DialogueTextNodeData { Title = "End" };
            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(function);
            dialogue.Graph.Nodes.Add(end);
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = start.Id, ToNodeId = function.Id, Order = 0 });
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = function.Id, ToNodeId = end.Id, Order = 0 });

            LogAssert.Expect(LogType.Warning, "failed");
            var player = new DialoguePlayer(functionExecutor: new RecordingFunctionExecutor(DialogueExecutionResult.Failed("failed")));

            Assert.That(player.Start(dialogue), Is.True);
            Assert.That(player.Next(), Is.False);
            Assert.That(player.CurrentNode, Is.Null);
            Assert.That(player.LastRuntimeError, Is.EqualTo("failed"));
        }

        [Test]
        public void FunctionFailure_LogAndContinueAdvancesToNextValidLink()
        {
            var dialogue = new DialogueEntry { Name = "Continue Test" };
            var start = new DialogueTextNodeData { Title = "Start", IsStartNode = true };
            var function = new FunctionNodeData
            {
                Title = "Fail",
                FunctionId = "fail",
                FailurePolicy = DialogueExecutionFailurePolicy.LogAndContinue
            };
            var end = new DialogueTextNodeData { Title = "End" };
            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(function);
            dialogue.Graph.Nodes.Add(end);
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = start.Id, ToNodeId = function.Id, Order = 0 });
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = function.Id, ToNodeId = end.Id, Order = 0 });

            LogAssert.Expect(LogType.Warning, "failed");
            var player = new DialoguePlayer(functionExecutor: new RecordingFunctionExecutor(DialogueExecutionResult.Failed("failed")));

            Assert.That(player.Start(dialogue), Is.True);
            Assert.That(player.Next(), Is.True);
            Assert.That(player.CurrentNode, Is.EqualTo(end));
        }

        [Test]
        public void PendingFunction_WaitsUntilCompletionThenContinues()
        {
            var dialogue = new DialogueEntry { Name = "Pending Test" };
            var start = new DialogueTextNodeData { Title = "Start", IsStartNode = true };
            var function = new FunctionNodeData
            {
                Title = "Wait",
                FunctionId = "wait",
                WaitForCompletion = true
            };
            var end = new DialogueTextNodeData { Title = "End" };
            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(function);
            dialogue.Graph.Nodes.Add(end);
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = start.Id, ToNodeId = function.Id, Order = 0 });
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = function.Id, ToNodeId = end.Id, Order = 0 });

            var player = new DialoguePlayer(functionExecutor: new RecordingFunctionExecutor(DialogueExecutionResult.Pending()));

            Assert.That(player.Start(dialogue), Is.True);
            Assert.That(player.Next(), Is.True);
            Assert.That(player.IsWaitingForExecution, Is.True);
            Assert.That(player.CurrentNode, Is.Null);

            Assert.That(player.CompletePendingExecution(DialogueExecutionResult.Success()), Is.True);
            Assert.That(player.IsWaitingForExecution, Is.False);
            Assert.That(player.CurrentNode, Is.EqualTo(end));
        }

        [Test]
        public void ExecutableNode_UsesOutgoingLinkOrderThenStableId()
        {
            var dialogue = new DialogueEntry { Name = "Order Test" };
            var start = new DialogueTextNodeData { Title = "Start", IsStartNode = true };
            var debug = new DebugNodeData { Title = "Debug", MessageTemplate = "ordering" };
            var first = new DialogueTextNodeData { Title = "First" };
            var second = new DialogueTextNodeData { Title = "Second" };
            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(debug);
            dialogue.Graph.Nodes.Add(first);
            dialogue.Graph.Nodes.Add(second);
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = start.Id, ToNodeId = debug.Id, Order = 0 });
            dialogue.Graph.Links.Add(new NodeLinkData { Id = "b-link", FromNodeId = debug.Id, ToNodeId = second.Id, Order = 0 });
            dialogue.Graph.Links.Add(new NodeLinkData { Id = "a-link", FromNodeId = debug.Id, ToNodeId = first.Id, Order = 0 });

            LogAssert.Expect(LogType.Log, "ordering");
            var player = new DialoguePlayer();

            Assert.That(player.Start(dialogue), Is.True);
            Assert.That(player.Next(), Is.True);
            Assert.That(player.CurrentNode, Is.EqualTo(first));
        }

        [Test]
        public void DialogueClone_PreservesSpeakers()
        {
            var dialogue = new DialogueEntry
            {
                Name = "Multi Speaker",
                Speakers = new List<DialogueSpeakerEntry>
                {
                    new() { Id = "speaker_a", Name = "Ada" },
                    new() { Id = "speaker_b", Name = "Byron" }
                }
            };

            var clone = dialogue.Clone();

            Assert.That(clone.Speakers.Select(speaker => speaker.Id), Is.EqualTo(new[] { "speaker_a", "speaker_b" }));
            Assert.That(clone.Speakers.Select(speaker => speaker.Name), Is.EqualTo(new[] { "Ada", "Byron" }));
            Assert.That(clone.Speakers[0], Is.Not.SameAs(dialogue.Speakers[0]));
        }

        [Test]
        public void ArgumentClone_PreservesPrimitiveValues()
        {
            var node = new FunctionNodeData
            {
                Arguments = new List<DialogueArgumentEntry>
                {
                    new() { Name = "name", Value = DialogueArgumentValue.FromString("Ada") },
                    new() { Name = "count", Value = DialogueArgumentValue.FromInt(3) },
                    new() { Name = "scale", Value = DialogueArgumentValue.FromFloat(1.5f) },
                    new() { Name = "enabled", Value = DialogueArgumentValue.FromBool(true) }
                }
            };

            var clone = (FunctionNodeData)node.Clone();

            Assert.That(clone.Arguments.Select(argument => argument.Value.GetDisplayValue()), Is.EqualTo(new[] { "Ada", "3", "1.5", "true" }));
        }

        [Test]
        public void TextNodeClone_PreservesVoiceKeyAndSpeakerId()
        {
            var node = new DialogueTextNodeData
            {
                Title = "Greeting",
                BodyText = "Hello there.",
                LocalizationKey = "Conversation/Farm.Plot.Dialogue_0001/Entry/1/Dialogue Text",
                LocalizedBodyText = new List<DialogueLocalizedTextEntry>
                {
                    new() { LanguageCode = "en", Text = "Hello there." }
                },
                VoiceKey = "innkeeper.greeting.hello",
                SpeakerId = "speaker_innkeeper",
                IsStartNode = true,
                UseOutputsAsChoices = true
            };

            var clone = (DialogueTextNodeData)node.Clone();

            Assert.That(clone.VoiceKey, Is.EqualTo("innkeeper.greeting.hello"));
            Assert.That(clone.SpeakerId, Is.EqualTo("speaker_innkeeper"));
            Assert.That(clone.BodyText, Is.EqualTo("Hello there."));
            Assert.That(clone.LocalizationKey, Is.EqualTo("Conversation/Farm.Plot.Dialogue_0001/Entry/1/Dialogue Text"));
            Assert.That(clone.LocalizedBodyText[0].LanguageCode, Is.EqualTo("en"));
            Assert.That(clone.LocalizedBodyText[0].Text, Is.EqualTo("Hello there."));
            Assert.That(clone.LocalizedBodyText[0], Is.Not.SameAs(node.LocalizedBodyText[0]));
            Assert.That(clone.IsStartNode, Is.True);
            Assert.That(clone.UseOutputsAsChoices, Is.True);
        }

        [Test]
        public void CurrentSpeakerName_ResolvesExplicitSpeakerAndDefaultFallback()
        {
            var dialogue = new DialogueEntry
            {
                Name = "Speaker Test",
                Speakers = new List<DialogueSpeakerEntry>
                {
                    new() { Id = "narrator", Name = "Narrator" },
                    new() { Id = "hero", Name = "Hero" }
                }
            };
            var start = new DialogueTextNodeData
            {
                Title = "Start",
                BodyText = "Default speaker line.",
                IsStartNode = true
            };
            var explicitSpeaker = new DialogueTextNodeData
            {
                Title = "Hero Line",
                BodyText = "Explicit speaker line.",
                SpeakerId = "hero"
            };
            var missingSpeaker = new DialogueTextNodeData
            {
                Title = "Fallback Line",
                BodyText = "Missing speaker falls back.",
                SpeakerId = "missing"
            };

            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(explicitSpeaker);
            dialogue.Graph.Nodes.Add(missingSpeaker);
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = start.Id, ToNodeId = explicitSpeaker.Id, Order = 0 });
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = explicitSpeaker.Id, ToNodeId = missingSpeaker.Id, Order = 0 });

            var player = new DialoguePlayer();

            Assert.That(player.Start(dialogue), Is.True);
            Assert.That(player.CurrentSpeakerName, Is.EqualTo("Narrator"));

            Assert.That(player.Next(), Is.True);
            Assert.That(player.CurrentSpeakerName, Is.EqualTo("Hero"));

            Assert.That(player.Next(), Is.True);
            Assert.That(player.CurrentSpeakerName, Is.EqualTo("Narrator"));
        }

        [Test]
        public void Validator_FindsMissingRequiredArgumentAndTypeMismatch()
        {
            var node = new FunctionNodeData
            {
                FunctionId = "test",
                Arguments = new List<DialogueArgumentEntry>
                {
                    new() { Name = "amount", Value = DialogueArgumentValue.FromString("wrong") }
                }
            };
            var registry = new TestExecutionRegistry(new[]
            {
                new DialogueFunctionDescriptor(
                    "test",
                    parameters: new[]
                    {
                        new DialogueParameterDescriptor("amount", DialogueArgumentType.Int, required: true),
                        new DialogueParameterDescriptor("flag", DialogueArgumentType.Bool, required: true)
                    })
            });

            var issues = DialogueExecutableValidator.ValidateNode(node, registry);

            Assert.That(issues.Any(issue => issue.Message.Contains("amount") && issue.Message.Contains("expects Int")), Is.True);
            Assert.That(issues.Any(issue => issue.Message.Contains("flag") && issue.Message.Contains("missing")), Is.True);
        }

        [Test]
        public void AutosaveStore_RoundTripsDatabaseState()
        {
            var root = Path.Combine(Path.GetTempPath(), $"newdial-tests-{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(root);

            try
            {
                var database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
                var npc = new NpcEntry { Name = "Merchant" };
                var dialogue = new DialogueEntry { Name = "Trade" };
                var node = new DialogueTextNodeData
                {
                    Title = "Start",
                    BodyText = "Interested in wares?",
                    IsStartNode = true,
                    Position = new Vector2(12f, 34f)
                };

                dialogue.Graph.Nodes.Add(node);
                npc.Dialogues.Add(dialogue);
                database.Npcs.Add(npc);

                DialogueEditorAutosaveStore.SaveSnapshot(database, "autosave-test", root);

                database.Npcs.Clear();
                Assert.That(database.Npcs, Is.Empty);

                var restored = DialogueEditorAutosaveStore.TryLoadSnapshot(database, "autosave-test", root);
                Assert.That(restored, Is.True);
                Assert.That(database.Npcs, Has.Count.EqualTo(1));
                Assert.That(database.Npcs[0].Dialogues[0].Graph.Nodes, Has.Count.EqualTo(1));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private sealed class RecordingFunctionExecutor : IDialogueFunctionExecutor
        {
            private readonly DialogueExecutionResult _result;

            public RecordingFunctionExecutor(DialogueExecutionResult result)
            {
                _result = result;
            }

            public List<string> ExecutedFunctionIds { get; } = new();

            public DialogueExecutionResult Execute(FunctionNodeData node, DialogueExecutionContext context)
            {
                ExecutedFunctionIds.Add(node.FunctionId);
                return _result;
            }
        }

        private sealed class RecordingSceneExecutor : IDialogueSceneExecutor
        {
            private readonly DialogueExecutionResult _result;

            public RecordingSceneExecutor(DialogueExecutionResult result)
            {
                _result = result;
            }

            public DialogueExecutionResult Execute(SceneNodeData node, DialogueExecutionContext context)
            {
                return _result;
            }
        }

        private sealed class TestExecutionRegistry : IDialogueExecutionRegistry
        {
            private readonly IEnumerable<DialogueFunctionDescriptor> _functions;

            public TestExecutionRegistry(IEnumerable<DialogueFunctionDescriptor> functions)
            {
                _functions = functions;
            }

            public IEnumerable<DialogueFunctionDescriptor> GetFunctions()
            {
                return _functions;
            }

            public IEnumerable<DialogueSceneDescriptor> GetScenes()
            {
                return Enumerable.Empty<DialogueSceneDescriptor>();
            }
        }
    }
}

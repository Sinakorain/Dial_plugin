using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace NewDial.DialogueEditor.Tests
{
    public class DialoguePreviewSessionTests
    {
        [SetUp]
        public void SetUp()
        {
            DialogueEditorLanguageSettings.CurrentLanguage = DialogueEditorLanguage.English;
        }

        [Test]
        public void Choose_AppendsTranscriptAndBackRestoresChoiceState()
        {
            var dialogue = new DialogueEntry
            {
                Name = "Choice Preview"
            };

            var start = new DialogueTextNodeData
            {
                Title = "Question",
                BodyText = "Where do we go?",
                IsStartNode = true,
                UseOutputsAsChoices = true
            };

            var left = new DialogueTextNodeData
            {
                Title = "Left",
                BodyText = "We went left."
            };

            var right = new DialogueTextNodeData
            {
                Title = "Right",
                BodyText = "We went right."
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

            var session = new DialoguePreviewSession(dialogue);

            Assert.That(session.Transcript.Count, Is.EqualTo(1));
            Assert.That(session.CurrentChoices.Count, Is.EqualTo(2));

            Assert.That(session.Choose(1), Is.True);
            Assert.That(session.CurrentNode, Is.EqualTo(right));
            Assert.That(session.Transcript.Count, Is.EqualTo(2));
            Assert.That(session.Transcript[1].Kind, Is.EqualTo(DialoguePreviewTranscriptEntryKind.Choice));
            Assert.That(session.Transcript[1].Title, Is.EqualTo("Go right"));
            Assert.That(session.Transcript[1].Body, Is.EqualTo("We went right."));
            Assert.That(session.Transcript[1].ChoiceText, Is.EqualTo("Go right"));
            Assert.That(session.Transcript[1].NodeId, Is.EqualTo(right.Id));

            Assert.That(session.Back(), Is.True);
            Assert.That(session.CurrentNode, Is.EqualTo(start));
            Assert.That(session.CurrentChoices.Count, Is.EqualTo(2));
            Assert.That(session.Transcript.Count, Is.EqualTo(1));
        }

        [Test]
        public void Choose_UsesAnswerNodeTextInPreview()
        {
            var dialogue = new DialogueEntry { Name = "Answer Preview" };
            var start = new DialogueTextNodeData
            {
                BodyText = "Question",
                IsStartNode = true
            };
            var answer = new DialogueChoiceNodeData
            {
                ChoiceText = "Ask about work",
                BodyText = "The mill needs help."
            };
            var target = new DialogueTextNodeData { BodyText = "Come back tomorrow." };
            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(answer);
            dialogue.Graph.Nodes.Add(target);
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = start.Id, ToNodeId = answer.Id, Order = 0 });
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = answer.Id, ToNodeId = target.Id, Order = 0 });

            var session = new DialoguePreviewSession(dialogue);

            Assert.That(session.CurrentChoices, Has.Count.EqualTo(1));
            Assert.That(session.CurrentChoices[0].Text, Is.EqualTo("Ask about work"));
            Assert.That(session.Choose(0), Is.True);
            Assert.That(session.CurrentLineNode, Is.EqualTo(answer));
            Assert.That(session.CurrentNode, Is.Null);
            Assert.That(session.Transcript[1].ChoiceText, Is.EqualTo("Ask about work"));
            Assert.That(session.Transcript[1].Body, Is.EqualTo("The mill needs help."));
            Assert.That(session.Advance(), Is.True);
            Assert.That(session.CurrentNode, Is.EqualTo(target));
        }

        [Test]
        public void Choose_BlockedManualAnswerLinkReportsBlockedChoice()
        {
            var dialogue = new DialogueEntry { Name = "Blocked Manual Answer Preview" };
            var start = new DialogueTextNodeData
            {
                BodyText = "Question",
                IsStartNode = true
            };
            var answer = new DialogueChoiceNodeData
            {
                ChoiceText = "Secret",
                Condition = new ConditionData
                {
                    Type = ConditionType.VariableCheck,
                    Key = "has_secret",
                    Operator = "==",
                    Value = "true"
                }
            };
            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(answer);
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = start.Id, ToNodeId = answer.Id, Order = 0 });

            var session = new DialoguePreviewSession(dialogue);

            Assert.That(session.CurrentChoices, Is.Empty);
            Assert.That(session.CanAdvance, Is.False);
            Assert.That(session.BlockedChoices, Has.Count.EqualTo(1));
            Assert.That(session.BlockedChoices[0].Label, Is.EqualTo("Secret"));
            Assert.That(session.CurrentReason, Is.EqualTo("No choices are available with the current test variables."));
        }

        [Test]
        public void Choose_WithSpeaker_KeepsChoiceTextSeparateFromNodeBody()
        {
            var dialogue = new DialogueEntry
            {
                Name = "Speaker Choice",
                Speakers = new List<DialogueSpeakerEntry>
                {
                    new() { Id = "liar", Name = "Liar" }
                }
            };

            var start = new DialogueTextNodeData
            {
                BodyText = "Question",
                IsStartNode = true,
                UseOutputsAsChoices = true
            };
            var answer = new DialogueTextNodeData
            {
                BodyText = "No, thanks.",
                SpeakerId = "liar"
            };
            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(answer);
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = answer.Id,
                Order = 0,
                ChoiceText = "No, thanks"
            });

            var session = new DialoguePreviewSession(dialogue);

            Assert.That(session.Choose(0), Is.True);
            Assert.That(session.Transcript[1].Title, Is.EqualTo("Liar"));
            Assert.That(session.Transcript[1].ChoiceText, Is.EqualTo("No, thanks"));
            Assert.That(session.Transcript[1].Body, Is.EqualTo("No, thanks."));
        }

        [Test]
        public void Advance_ToEndAndBackRestoresPreviousNode()
        {
            var dialogue = new DialogueEntry
            {
                Name = "Linear Preview"
            };

            var start = new DialogueTextNodeData
            {
                Title = "Start",
                BodyText = "Start",
                IsStartNode = true
            };

            var end = new DialogueTextNodeData
            {
                Title = "End",
                BodyText = "End"
            };

            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(end);
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = end.Id,
                Order = 0
            });

            var session = new DialoguePreviewSession(dialogue);

            Assert.That(session.Advance(), Is.True);
            Assert.That(session.CurrentNode, Is.EqualTo(end));
            Assert.That(session.Advance(), Is.True);
            Assert.That(session.CurrentNode, Is.Null);
            Assert.That(session.IsEnded, Is.True);

            Assert.That(session.Back(), Is.True);
            Assert.That(session.CurrentNode, Is.EqualTo(end));
            Assert.That(session.Transcript.Count, Is.EqualTo(2));
        }

        [Test]
        public void StartCondition_UsesTestVariables()
        {
            var dialogue = new DialogueEntry
            {
                StartCondition = new ConditionData
                {
                    Type = ConditionType.VariableCheck,
                    Key = "has_key",
                    Operator = "==",
                    Value = "true"
                }
            };
            var start = new DialogueTextNodeData { IsStartNode = true };
            dialogue.Graph.Nodes.Add(start);

            var blockedSession = new DialoguePreviewSession(dialogue);
            Assert.That(blockedSession.CurrentNode, Is.Null);
            Assert.That(blockedSession.CurrentReason, Does.Contain("Dialogue-level start condition blocked"));

            var allowedSession = new DialoguePreviewSession(dialogue, new Dictionary<string, string>
            {
                ["has_key"] = "true"
            });
            Assert.That(allowedSession.CurrentNode, Is.EqualTo(start));
        }

        [Test]
        public void ChoiceConditions_ReportBlockedChoices()
        {
            var dialogue = new DialogueEntry();
            var start = new DialogueTextNodeData
            {
                IsStartNode = true,
                UseOutputsAsChoices = true
            };
            var target = new DialogueTextNodeData
            {
                Title = "Locked",
                Condition = new ConditionData
                {
                    Type = ConditionType.VariableCheck,
                    Key = "door_open",
                    Operator = "==",
                    Value = "true"
                }
            };
            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(target);
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = target.Id,
                ChoiceText = "Open door"
            });

            var session = new DialoguePreviewSession(dialogue, new Dictionary<string, string>
            {
                ["door_open"] = "false"
            });

            Assert.That(session.CurrentChoices, Is.Empty);
            Assert.That(session.BlockedChoices.Count, Is.EqualTo(1));
            Assert.That(session.BlockedChoices[0].Reason, Does.Contain("Choice unavailable because condition is not met"));
            Assert.That(session.CurrentReason, Is.EqualTo("No choices are available with the current test variables."));
        }

        [Test]
        public void DatabaseDefaultVariables_GatePreviewChoices()
        {
            var database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            database.Variables.Add(new DialogueVariableDefinition
            {
                Key = "door_open",
                Type = DialogueArgumentType.Bool,
                DefaultValue = DialogueArgumentValue.FromBool(true)
            });
            var dialogue = new DialogueEntry();
            var start = new DialogueTextNodeData
            {
                IsStartNode = true,
                UseOutputsAsChoices = true
            };
            var target = new DialogueTextNodeData
            {
                Title = "Open",
                Condition = new ConditionData
                {
                    Type = ConditionType.VariableCheck,
                    Key = "door_open",
                    Operator = "Truthy"
                }
            };
            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(target);
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = start.Id, ToNodeId = target.Id });

            var session = new DialoguePreviewSession(dialogue, database);

            Assert.That(session.CurrentChoices.Count, Is.EqualTo(1));
        }

        [Test]
        public void TestVariables_OverrideDatabaseDefaults()
        {
            var database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            database.Variables.Add(new DialogueVariableDefinition
            {
                Key = "has_key",
                Type = DialogueArgumentType.Bool,
                DefaultValue = DialogueArgumentValue.FromBool(false)
            });
            var dialogue = new DialogueEntry
            {
                StartCondition = new ConditionData
                {
                    Type = ConditionType.VariableCheck,
                    Key = "has_key",
                    Operator = "Truthy"
                }
            };
            var start = new DialogueTextNodeData { IsStartNode = true };
            dialogue.Graph.Nodes.Add(start);

            var session = new DialoguePreviewSession(dialogue, database, new Dictionary<string, string>
            {
                ["has_key"] = "true"
            });

            Assert.That(session.CurrentNode, Is.EqualTo(start));
        }

        [Test]
        public void StartConditionReason_IncludesCurrentDatabaseVariableValue()
        {
            var database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            database.Variables.Add(new DialogueVariableDefinition
            {
                Key = "has_key",
                Type = DialogueArgumentType.Bool,
                DefaultValue = DialogueArgumentValue.FromBool(false)
            });
            var dialogue = new DialogueEntry
            {
                StartCondition = new ConditionData
                {
                    Type = ConditionType.VariableCheck,
                    Key = "has_key",
                    Operator = "==",
                    Value = "true"
                }
            };
            dialogue.Graph.Nodes.Add(new DialogueTextNodeData { IsStartNode = true });

            var session = new DialoguePreviewSession(dialogue, database);

            Assert.That(session.CurrentNode, Is.Null);
            Assert.That(session.CurrentReason, Does.Contain("Dialogue-level start condition blocked"));
            Assert.That(session.CurrentReason, Does.Contain("current value: false"));
        }

        [Test]
        public void BuiltInSetVariable_ChangesLaterPreviewAvailability()
        {
            var database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            database.Variables.Add(new DialogueVariableDefinition
            {
                Key = "unlocked",
                Type = DialogueArgumentType.Bool,
                DefaultValue = DialogueArgumentValue.FromBool(false)
            });
            var dialogue = new DialogueEntry();
            var start = new DialogueTextNodeData { IsStartNode = true };
            var setVariable = new FunctionNodeData
            {
                FunctionId = DialogueBuiltInFunctions.SetVariableFunctionId,
                Arguments = new List<DialogueArgumentEntry>
                {
                    new()
                    {
                        Name = DialogueBuiltInFunctions.VariableKeyArgument,
                        Value = DialogueArgumentValue.FromString("unlocked")
                    },
                    new()
                    {
                        Name = DialogueBuiltInFunctions.VariableValueArgument,
                        Value = DialogueArgumentValue.FromString("true")
                    }
                }
            };
            var target = new DialogueTextNodeData
            {
                Title = "Unlocked",
                Condition = new ConditionData
                {
                    Type = ConditionType.VariableCheck,
                    Key = "unlocked",
                    Operator = "Truthy"
                }
            };
            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(setVariable);
            dialogue.Graph.Nodes.Add(target);
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = start.Id, ToNodeId = setVariable.Id, Order = 0 });
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = setVariable.Id, ToNodeId = target.Id, Order = 0 });

            var session = new DialoguePreviewSession(dialogue, database);

            Assert.That(session.TryGetCurrentVariableValue("unlocked", out var currentValue), Is.True);
            Assert.That(currentValue, Is.EqualTo("false"));
            Assert.That(session.Advance(), Is.True);
            Assert.That(session.CurrentNode, Is.EqualTo(target));
            Assert.That(session.TryGetCurrentVariableValue("unlocked", out currentValue), Is.True);
            Assert.That(currentValue, Is.EqualTo("true"));
        }

        [Test]
        public void GenericChoiceFallback_IsExplained()
        {
            var dialogue = new DialogueEntry();
            var start = new DialogueTextNodeData
            {
                IsStartNode = true,
                UseOutputsAsChoices = true
            };
            var target = new DialogueTextNodeData { Title = string.Empty };
            dialogue.Graph.Nodes.Add(start);
            dialogue.Graph.Nodes.Add(target);
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = start.Id,
                ToNodeId = target.Id
            });

            var session = new DialoguePreviewSession(dialogue);

            Assert.That(session.CurrentChoices.Count, Is.EqualTo(1));
            Assert.That(session.GetChoiceExplanation(session.CurrentChoices[0]), Is.EqualTo("Generic fallback label is being used."));
        }
    }
}

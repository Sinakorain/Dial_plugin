using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NewDial.DialogueEditor
{
    public class DialoguePlayer
    {
        private readonly IDialogueConditionEvaluator _conditionEvaluator;
        private readonly IDialogueVariableStore _variableStore;
        private readonly IDialogueVariableState _variableState;
        private readonly IDialogueFunctionExecutor _functionExecutor;
        private readonly IDialogueSceneExecutor _sceneExecutor;
        private readonly List<DialogueChoice> _choices = new();

        private DialogueEntry _currentDialogue;
        private BaseNodeData _currentDataNode;
        private BaseNodeData _pendingExecutionNode;

        public DialoguePlayer(
            IDialogueConditionEvaluator conditionEvaluator = null,
            IDialogueVariableStore variableStore = null,
            IDialogueFunctionExecutor functionExecutor = null,
            IDialogueSceneExecutor sceneExecutor = null)
        {
            _conditionEvaluator = conditionEvaluator ?? new DefaultDialogueConditionEvaluator();
            _variableStore = variableStore;
            _variableState = variableStore as IDialogueVariableState;
            _functionExecutor = functionExecutor;
            _sceneExecutor = sceneExecutor;
        }

        public DialoguePlayer(
            DialogueDatabaseAsset database,
            IDialogueConditionEvaluator conditionEvaluator = null,
            IDialogueVariableState variableState = null,
            IDialogueFunctionExecutor functionExecutor = null,
            IDialogueSceneExecutor sceneExecutor = null)
            : this(
                conditionEvaluator,
                variableState ?? DialogueVariableState.FromDatabase(database),
                functionExecutor,
                sceneExecutor)
        {
        }

        public event Action<DialogueTextNodeData> NodeChanged;
        public event Action<BaseNodeData> LineChanged;
        public event Action DialogueEnded;
        public event Action<string> RuntimeError;

        public DialogueEntry CurrentDialogue => _currentDialogue;

        public DialogueTextNodeData CurrentNode { get; private set; }

        public BaseNodeData CurrentLineNode { get; private set; }

        public DialogueSpeakerEntry CurrentSpeaker { get; private set; }

        public string CurrentSpeakerName => CurrentSpeaker?.Name ?? string.Empty;

        public IReadOnlyList<DialogueChoice> CurrentChoices => _choices;

        public bool IsWaitingForExecution { get; private set; }

        public DialogueExecutionResult LastExecutionResult { get; private set; }

        public string LastRuntimeError { get; private set; } = string.Empty;

        public bool CanStart(DialogueEntry dialogue)
        {
            return dialogue != null && _conditionEvaluator.Evaluate(dialogue.StartCondition, _variableStore);
        }

        public bool Start(DialogueEntry dialogue)
        {
            _currentDialogue = dialogue;
            _currentDataNode = null;
            CurrentLineNode = null;
            _pendingExecutionNode = null;
            _choices.Clear();
            IsWaitingForExecution = false;
            LastExecutionResult = null;
            LastRuntimeError = string.Empty;

            if (dialogue == null || dialogue.Graph == null)
            {
                CurrentNode = null;
                CurrentLineNode = null;
                CurrentSpeaker = null;
                return false;
            }

            var startNode = DialogueGraphUtility.FindStartNode(dialogue);
            if (startNode == null)
            {
                CurrentNode = null;
                CurrentLineNode = null;
                CurrentSpeaker = null;
                return false;
            }

            return EnterNode(startNode);
        }

        public bool Next()
        {
            if (CurrentLineNode == null || IsWaitingForExecution)
            {
                return false;
            }

            if (CurrentLineNode is DialogueTextNodeData textNode &&
                DialogueGraphUtility.UsesChoices(_currentDialogue?.Graph, textNode) &&
                _choices.Count > 0)
            {
                return false;
            }

            return AdvanceFromNode(CurrentLineNode);
        }

        public bool Choose(int index)
        {
            if (CurrentLineNode is not DialogueTextNodeData textNode ||
                IsWaitingForExecution ||
                !DialogueGraphUtility.UsesChoices(_currentDialogue?.Graph, textNode) ||
                index < 0 ||
                index >= _choices.Count)
            {
                return false;
            }

            var choice = _choices[index];
            var destination = choice.ChoiceNode != null
                ? choice.ChoiceNode
                : choice.TargetNode;
            if (destination == null)
            {
                EndDialogue();
                return false;
            }

            return EnterNode(destination);
        }

        public bool CompletePendingExecution(DialogueExecutionResult result)
        {
            if (!IsWaitingForExecution || _pendingExecutionNode == null)
            {
                return false;
            }

            var node = _pendingExecutionNode;
            IsWaitingForExecution = false;
            _pendingExecutionNode = null;
            LastExecutionResult = result ?? DialogueExecutionResult.Failed("Pending executable node completed without a result.");
            return HandleExecutionResult(node, LastExecutionResult);
        }

        private bool CanEnterNode(BaseNodeData node)
        {
            return _conditionEvaluator.Evaluate(node?.Condition, _variableStore);
        }

        private bool EnterNode(BaseNodeData node)
        {
            if (node == null || !DialogueGraphUtility.IsRuntimeNode(node) || !CanEnterNode(node))
            {
                return false;
            }

            _currentDataNode = node;
            if (node is DialogueTextNodeData textNode)
            {
                SetCurrentTextNode(textNode);
                return true;
            }

            if (node is DialogueChoiceNodeData choiceNode)
            {
                SetCurrentChoiceNode(choiceNode);
                return true;
            }

            CurrentLineNode = null;
            CurrentNode = null;
            CurrentSpeaker = null;
            _choices.Clear();
            return ExecuteNode(node);
        }

        private void SetCurrentTextNode(DialogueTextNodeData node)
        {
            _currentDataNode = node;
            CurrentLineNode = node;
            CurrentNode = node;
            CurrentSpeaker = DialogueSpeakerUtility.ResolveSpeaker(_currentDialogue, node);
            RebuildChoices();
            NodeChanged?.Invoke(CurrentNode);
            LineChanged?.Invoke(CurrentLineNode);
        }

        private void SetCurrentChoiceNode(DialogueChoiceNodeData node)
        {
            _currentDataNode = node;
            CurrentLineNode = node;
            CurrentNode = null;
            CurrentSpeaker = DialogueSpeakerUtility.ResolveSpeaker(_currentDialogue, node);
            _choices.Clear();
            LineChanged?.Invoke(CurrentLineNode);
        }

        private void RebuildChoices()
        {
            _choices.Clear();
            if (CurrentNode == null || !DialogueGraphUtility.UsesChoices(_currentDialogue?.Graph, CurrentNode))
            {
                return;
            }

            foreach (var link in DialogueGraphUtility.GetChoiceCandidateLinks(_currentDialogue.Graph, CurrentNode))
            {
                var target = DialogueGraphUtility.GetNode(_currentDialogue.Graph, link.ToNodeId);
                if (target is DialogueChoiceNodeData choiceNode)
                {
                    if (!CanEnterNode(choiceNode))
                    {
                        continue;
                    }

                    _choices.Add(new DialogueChoice(link, choiceNode, choiceNode));
                    continue;
                }

                if (target != null && DialogueGraphUtility.IsRuntimeNode(target) && CanEnterNode(target))
                {
                    _choices.Add(new DialogueChoice(link, target));
                }
            }
        }

        private bool AdvanceFromNode(BaseNodeData sourceNode)
        {
            if (_currentDialogue?.Graph == null || sourceNode == null)
            {
                EndDialogue();
                return false;
            }

            foreach (var link in DialogueGraphUtility.GetOutgoingLinks(_currentDialogue.Graph, sourceNode.Id))
            {
                var target = DialogueGraphUtility.GetNode(_currentDialogue.Graph, link.ToNodeId);
                if (target != null && DialogueGraphUtility.IsRuntimeNode(target) && CanEnterNode(target))
                {
                    return EnterNode(target);
                }
            }

            EndDialogue();
            return false;
        }

        private bool ExecuteNode(BaseNodeData node)
        {
            var result = node switch
            {
                FunctionNodeData functionNode => ExecuteFunctionNode(functionNode),
                SceneNodeData sceneNode => ExecuteSceneNode(sceneNode),
                DebugNodeData debugNode => ExecuteDebugNode(debugNode),
                _ => DialogueExecutionResult.Failed($"Unsupported executable node type: {node.GetType().Name}")
            };

            LastExecutionResult = result;
            if (result.Status == DialogueExecutionStatus.Pending && WaitsForCompletion(node))
            {
                IsWaitingForExecution = true;
                _pendingExecutionNode = node;
                return true;
            }

            if (result.Status == DialogueExecutionStatus.Pending)
            {
                LastExecutionResult = DialogueExecutionResult.Success(result.Message);
            }

            return HandleExecutionResult(node, LastExecutionResult);
        }

        private DialogueExecutionResult ExecuteFunctionNode(FunctionNodeData node)
        {
            if (node.FunctionId == DialogueBuiltInFunctions.SetVariableFunctionId)
            {
                return ExecuteSetVariableNode(node);
            }

            if (_functionExecutor == null)
            {
                return DialogueExecutionResult.Failed($"Function node '{node.Title}' cannot execute because no function executor is registered.");
            }

            try
            {
                return _functionExecutor.Execute(node, CreateExecutionContext(node)) ??
                       DialogueExecutionResult.Failed($"Function '{node.FunctionId}' returned no execution result.");
            }
            catch (Exception exception)
            {
                return DialogueExecutionResult.Failed($"Function '{node.FunctionId}' threw an exception: {exception.Message}");
            }
        }

        private DialogueExecutionResult ExecuteSceneNode(SceneNodeData node)
        {
            if (_sceneExecutor == null)
            {
                return DialogueExecutionResult.Failed($"Scene node '{node.Title}' cannot execute because no scene executor is registered.");
            }

            try
            {
                return _sceneExecutor.Execute(node, CreateExecutionContext(node)) ??
                       DialogueExecutionResult.Failed($"Scene '{node.SceneKey}' returned no execution result.");
            }
            catch (Exception exception)
            {
                return DialogueExecutionResult.Failed($"Scene '{node.SceneKey}' threw an exception: {exception.Message}");
            }
        }

        private DialogueExecutionResult ExecuteDebugNode(DebugNodeData node)
        {
            var message = string.IsNullOrWhiteSpace(node.MessageTemplate)
                ? "Debug dialogue node executed."
                : node.MessageTemplate;

            if (node.IncludeArguments && node.Arguments != null && node.Arguments.Count > 0)
            {
                var arguments = node.Arguments
                    .Where(argument => argument != null && !string.IsNullOrWhiteSpace(argument.Name))
                    .Select(argument => $"{argument.Name}={argument.Value?.GetDisplayValue() ?? string.Empty}");
                message = $"{message} [{string.Join(", ", arguments)}]";
            }

            switch (node.LogLevel)
            {
                case DialogueDebugLogLevel.Warning:
                    Debug.LogWarning(message);
                    break;
                case DialogueDebugLogLevel.Error:
                    Debug.LogError(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }

            return DialogueExecutionResult.Success(message);
        }

        private DialogueExecutionResult ExecuteSetVariableNode(FunctionNodeData node)
        {
            if (_variableState == null)
            {
                return DialogueExecutionResult.Failed("Set Variable cannot execute because no mutable variable state is available.");
            }

            var key = GetArgumentDisplayValue(node.Arguments, DialogueBuiltInFunctions.VariableKeyArgument);
            var value = GetArgumentDisplayValue(node.Arguments, DialogueBuiltInFunctions.VariableValueArgument);
            if (!_variableState.TrySetValueFromString(key, value, out var error))
            {
                return DialogueExecutionResult.Failed(error);
            }

            return DialogueExecutionResult.Success($"Variable '{key}' set.");
        }

        private DialogueExecutionContext CreateExecutionContext(BaseNodeData node)
        {
            return new DialogueExecutionContext(_currentDialogue, _currentDialogue?.Graph, node, _variableStore, _variableState);
        }

        private static string GetArgumentDisplayValue(IEnumerable<DialogueArgumentEntry> arguments, string name)
        {
            return arguments?
                .FirstOrDefault(argument => argument != null && argument.Name == name)?
                .Value?
                .GetDisplayValue() ?? string.Empty;
        }

        private bool HandleExecutionResult(BaseNodeData node, DialogueExecutionResult result)
        {
            switch (result.Status)
            {
                case DialogueExecutionStatus.Success:
                    return AdvanceFromNode(node);
                case DialogueExecutionStatus.EndDialogue:
                    EndDialogue();
                    return false;
                case DialogueExecutionStatus.Pending:
                    return AdvanceFromNode(node);
                case DialogueExecutionStatus.Failed:
                    return HandleExecutionFailure(node, result);
                default:
                    EndDialogue();
                    return false;
            }
        }

        private bool HandleExecutionFailure(BaseNodeData node, DialogueExecutionResult result)
        {
            var message = string.IsNullOrWhiteSpace(result?.Message)
                ? $"Executable node '{node?.Title}' failed."
                : result.Message;

            LastRuntimeError = message;
            Debug.LogWarning(message);
            if (GetFailurePolicy(node) == DialogueExecutionFailurePolicy.LogAndContinue)
            {
                return AdvanceFromNode(node);
            }

            RuntimeError?.Invoke(message);
            EndDialogue();
            return false;
        }

        private static DialogueExecutionFailurePolicy GetFailurePolicy(BaseNodeData node)
        {
            return node switch
            {
                FunctionNodeData functionNode => functionNode.FailurePolicy,
                DebugNodeData debugNode => debugNode.FailurePolicy,
                _ => DialogueExecutionFailurePolicy.StopDialogue
            };
        }

        private static bool WaitsForCompletion(BaseNodeData node)
        {
            return node switch
            {
                FunctionNodeData functionNode => functionNode.WaitForCompletion,
                SceneNodeData sceneNode => sceneNode.WaitForCompletion,
                _ => false
            };
        }

        private void EndDialogue()
        {
            _currentDataNode = null;
            CurrentLineNode = null;
            CurrentNode = null;
            CurrentSpeaker = null;
            _choices.Clear();
            DialogueEnded?.Invoke();
        }
    }
}

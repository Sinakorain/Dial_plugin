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
            _functionExecutor = functionExecutor;
            _sceneExecutor = sceneExecutor;
        }

        public event Action<DialogueTextNodeData> NodeChanged;
        public event Action DialogueEnded;
        public event Action<string> RuntimeError;

        public DialogueEntry CurrentDialogue => _currentDialogue;

        public DialogueTextNodeData CurrentNode { get; private set; }

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
            _pendingExecutionNode = null;
            _choices.Clear();
            IsWaitingForExecution = false;
            LastExecutionResult = null;
            LastRuntimeError = string.Empty;

            if (dialogue == null || dialogue.Graph == null)
            {
                CurrentNode = null;
                return false;
            }

            var startNode = DialogueGraphUtility.FindStartNode(dialogue);
            if (startNode == null)
            {
                CurrentNode = null;
                return false;
            }

            return EnterNode(startNode);
        }

        public bool Next()
        {
            if (CurrentNode == null || IsWaitingForExecution)
            {
                return false;
            }

            if (CurrentNode.UseOutputsAsChoices && _choices.Count > 0)
            {
                return false;
            }

            return AdvanceFromNode(CurrentNode);
        }

        public bool Choose(int index)
        {
            if (CurrentNode == null || IsWaitingForExecution || !CurrentNode.UseOutputsAsChoices || index < 0 || index >= _choices.Count)
            {
                return false;
            }

            var choice = _choices[index];
            if (choice.Target == null)
            {
                EndDialogue();
                return false;
            }

            return EnterNode(choice.Target);
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

            CurrentNode = null;
            _choices.Clear();
            return ExecuteNode(node);
        }

        private void SetCurrentTextNode(DialogueTextNodeData node)
        {
            _currentDataNode = node;
            CurrentNode = node;
            RebuildChoices();
            NodeChanged?.Invoke(CurrentNode);
        }

        private void RebuildChoices()
        {
            _choices.Clear();
            if (CurrentNode == null || !CurrentNode.UseOutputsAsChoices)
            {
                return;
            }

            foreach (var link in DialogueGraphUtility.GetOutgoingLinks(_currentDialogue.Graph, CurrentNode.Id))
            {
                var target = DialogueGraphUtility.GetTextNode(_currentDialogue.Graph, link.ToNodeId);
                if (target != null && CanEnterNode(target))
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

        private DialogueExecutionContext CreateExecutionContext(BaseNodeData node)
        {
            return new DialogueExecutionContext(_currentDialogue, _currentDialogue?.Graph, node, _variableStore);
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
            CurrentNode = null;
            _choices.Clear();
            DialogueEnded?.Invoke();
        }
    }
}

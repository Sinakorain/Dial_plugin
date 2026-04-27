// Copyright (c) 2026 Danil Kashulin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor.Tests
{
    public class DialogueEditorWindowTests
    {
        private const string RichTextTextColorsPrefsKey = "NewDial.DialogueEditor.RichText.TextColors";

        [SetUp]
        public void SetUp()
        {
            DialogueEditorLanguageSettings.CurrentLanguage = DialogueEditorLanguage.English;
            EditorPrefs.DeleteKey(RichTextTextColorsPrefsKey);
            DialoguePaletteShortcutSettings.ResetForTests();
        }

        [Test]
        public void LanguageSettings_DefaultsToEnglish()
        {
            DialogueEditorLanguageSettings.ResetForTests();

            Assert.That(DialogueEditorLanguageSettings.CurrentLanguage, Is.EqualTo(DialogueEditorLanguage.English));
            Assert.That(DialogueEditorLocalization.Text("Palette"), Is.EqualTo("Palette"));
        }

        [Test]
        public void StartWindow_UsesCompactDialogueOnlyLauncher()
        {
            var window = ScriptableObject.CreateInstance<DialogueStartWindow>();

            try
            {
                window.InitializeForTests();

                Assert.That(window.minSize, Is.EqualTo(DialogueStartWindow.CompactMinSize));
                Assert.That(window.maxSize, Is.EqualTo(DialogueStartWindow.CompactMaxSize));
                Assert.That(window.rootVisualElement.Q<VisualElement>("dialogue-start-card"), Is.Not.Null);
                Assert.That(HasLabel(window.rootVisualElement, "Dialogue Editor"), Is.True);
                Assert.That(HasLabel(window.rootVisualElement, "Create a dialogue database or open an existing one."), Is.True);
                Assert.That(HasButton(window.rootVisualElement, "New File"), Is.True);
                Assert.That(HasButton(window.rootVisualElement, "Load File"), Is.True);
                Assert.That(HasButton(window.rootVisualElement, "Exit"), Is.True);
                var importExportFoldout = window.rootVisualElement.Q<Foldout>("dialogue-start-import-export-foldout");
                Assert.That(importExportFoldout, Is.Not.Null);
                Assert.That(importExportFoldout.value, Is.False);
                Assert.That(DialogueStartWindow.CompactMinSize.y, Is.GreaterThanOrEqualTo(340f));
                Assert.That(window.rootVisualElement.Q<IMGUIContainer>("localization-database-field"), Is.Null);
                Assert.That(window.rootVisualElement.Query<Label>().ToList()
                    .Any(label => label.text.Contains("Cutscene", StringComparison.OrdinalIgnoreCase) ||
                                  label.text.Contains("катсцен", StringComparison.OrdinalIgnoreCase)), Is.False);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void StartWindow_ResizeKeepsTopLeftWhenExpandingAndCollapsingImportExport()
        {
            var window = ScriptableObject.CreateInstance<DialogueStartWindow>();

            try
            {
                var compactPosition = new Rect(123f, 234f, DialogueStartWindow.CompactMinSize.x, DialogueStartWindow.CompactMinSize.y);
                var expandedPosition = window.CalculateWindowRectForTests(compactPosition, expanded: true);

                Assert.That(expandedPosition.x, Is.EqualTo(compactPosition.x));
                Assert.That(expandedPosition.y, Is.EqualTo(compactPosition.y));
                Assert.That(expandedPosition.width, Is.EqualTo(DialogueStartWindow.ExpandedMinSize.x));
                Assert.That(expandedPosition.height, Is.EqualTo(DialogueStartWindow.ExpandedMinSize.y));

                var collapsedPosition = window.CalculateWindowRectForTests(expandedPosition, expanded: false);

                Assert.That(collapsedPosition.x, Is.EqualTo(compactPosition.x));
                Assert.That(collapsedPosition.y, Is.EqualTo(compactPosition.y));
                Assert.That(collapsedPosition.width, Is.InRange(DialogueStartWindow.CompactMinSize.x, DialogueStartWindow.CompactMaxSize.x));
                Assert.That(collapsedPosition.height, Is.InRange(DialogueStartWindow.CompactMinSize.y, DialogueStartWindow.CompactMaxSize.y));
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void StartWindow_ResizeUsesLastValidRectWhenUnityReportsDefaultOrigin()
        {
            var window = ScriptableObject.CreateInstance<DialogueStartWindow>();

            try
            {
                var lastValidPosition = new Rect(222f, 333f, DialogueStartWindow.CompactMinSize.x, DialogueStartWindow.CompactMinSize.y);
                var unityDefaultPosition = new Rect(0f, 0f, DialogueStartWindow.CompactMinSize.x, DialogueStartWindow.CompactMinSize.y);
                var expandedPosition = window.CalculateWindowRectForTests(
                    unityDefaultPosition,
                    expanded: true,
                    lastValidPosition: lastValidPosition);

                Assert.That(expandedPosition.x, Is.EqualTo(lastValidPosition.x));
                Assert.That(expandedPosition.y, Is.EqualTo(lastValidPosition.y));
                Assert.That(expandedPosition.width, Is.EqualTo(DialogueStartWindow.ExpandedMinSize.x));
                Assert.That(expandedPosition.height, Is.EqualTo(DialogueStartWindow.ExpandedMinSize.y));
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void StartWindow_LocalizationContextExpandsImportExportPanel()
        {
            var database = CreateDatabase("StartLocalizationContext");
            var npc = database.Npcs[0];
            var dialogue = npc.Dialogues[0];
            var window = ScriptableObject.CreateInstance<DialogueStartWindow>();

            try
            {
                window.InitializeForTests(database: database, selectedNpc: npc, selectedDialogue: dialogue, localizationExpanded: true);

                Assert.That(window.minSize, Is.EqualTo(DialogueStartWindow.ExpandedMinSize));
                Assert.That(window.maxSize, Is.EqualTo(DialogueStartWindow.ExpandedMaxSize));
                Assert.That(window.maxSize.y, Is.EqualTo(560f));
                Assert.That(window.rootVisualElement.Q<Foldout>("dialogue-start-import-export-foldout")?.value, Is.True);
                Assert.That(window.rootVisualElement.Q<IMGUIContainer>("localization-database-field"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<TextField>("localization-export-prefix-field")?.value, Is.EqualTo(dialogue.Id));
                Assert.That(window.rootVisualElement.Query<Label>().ToList()
                    .Any(label => label.text.Contains(database.name, StringComparison.Ordinal)), Is.True);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void LocalizationWindow_DoesNotExposeSeparateMenuItem()
        {
            var hasLocalizationMenu = typeof(DialogueLocalizationWindow)
                .GetMethods(System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Static)
                .SelectMany(method => method.GetCustomAttributes(typeof(MenuItem), inherit: false).Cast<MenuItem>())
                .Any(menuItem => menuItem.menuItem == "Tools/New Dial/Localization Import Export");

            Assert.That(hasLocalizationMenu, Is.False);
        }

        [Test]
        public void LanguageSwitcher_RefreshesToolbarPaletteAndInspectorLabels()
        {
            var database = CreateDatabase("LanguageSwitch");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var languageField = window.rootVisualElement.Q<PopupField<string>>("editor-language-field");
                Assert.That(languageField, Is.Not.Null);
                Assert.That(HasLabel(window, "Palette"), Is.True);

                languageField.value = "RU";

                Assert.That(DialogueEditorLanguageSettings.CurrentLanguage, Is.EqualTo(DialogueEditorLanguage.Russian));
                Assert.That(HasLabel(window, "Палитра"), Is.True);
                Assert.That(HasLabel(window, "Текстовый узел"), Is.True);
                Assert.That(HasLabel(window, "Ключ озвучки"), Is.True);

                languageField = window.rootVisualElement.Q<PopupField<string>>("editor-language-field");
                languageField.value = "EN";

                Assert.That(DialogueEditorLanguageSettings.CurrentLanguage, Is.EqualTo(DialogueEditorLanguage.English));
                Assert.That(HasLabel(window, "Palette"), Is.True);
            }
            finally
            {
                DialogueEditorLanguageSettings.CurrentLanguage = DialogueEditorLanguage.English;
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void LeftDock_UsesFixedProjectAreaAndUnscrolledCompactPalette()
        {
            DialogueEditorLanguageSettings.CurrentLanguage = DialogueEditorLanguage.Russian;
            var database = CreateDatabase("LeftDockLayout");
            var npc = database.Npcs[0];
            for (var index = 0; index < 8; index++)
            {
                npc.Dialogues.Add(new DialogueEntry { Name = $"Dialogue {index + 2}" });
            }

            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);

                var projectPanel = window.rootVisualElement.Q<VisualElement>("project-panel");
                var palettePanel = window.rootVisualElement.Q<VisualElement>("palette-panel");
                var variablesPanel = window.rootVisualElement.Q<VisualElement>("variables-panel");

                Assert.That(projectPanel, Is.Not.Null);
                Assert.That(palettePanel, Is.Not.Null);
                Assert.That(variablesPanel, Is.Not.Null);
                var dockSections = projectPanel.parent.Children().ToList();
                Assert.That(dockSections.IndexOf(projectPanel), Is.LessThan(dockSections.IndexOf(palettePanel)));
                Assert.That(dockSections.IndexOf(palettePanel), Is.LessThan(dockSections.IndexOf(variablesPanel)));

                var projectFoldout = window.rootVisualElement.Q<Foldout>("project-foldout");
                var paletteFoldout = window.rootVisualElement.Q<Foldout>("palette-foldout");
                var variablesFoldout = window.rootVisualElement.Q<Foldout>("variables-foldout");
                Assert.That(projectFoldout, Is.Not.Null);
                Assert.That(paletteFoldout, Is.Not.Null);
                Assert.That(variablesFoldout, Is.Not.Null);
                Assert.That(projectFoldout.value, Is.True);
                Assert.That(paletteFoldout.value, Is.True);
                Assert.That(variablesFoldout.value, Is.True);
                paletteFoldout.value = false;
                Assert.That(paletteFoldout.value, Is.False);
                paletteFoldout.value = true;
                Assert.That(paletteFoldout.value, Is.True);

                Assert.That(window.rootVisualElement.Q<ScrollView>(className: "dialogue-editor__project-scroll"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<ScrollView>(className: "dialogue-editor__palette-scroll"), Is.Null);
                Assert.That(window.rootVisualElement.Query<VisualElement>(className: "dialogue-editor__palette-item").ToList(), Has.Count.EqualTo(5));
                Assert.That(HasLabel(window, "Текстовый узел"), Is.True);
                Assert.That(HasLabel(window, "Комментарий"), Is.True);
                Assert.That(HasLabel(window, "Функция"), Is.True);
                Assert.That(HasLabel(window, "Сцена"), Is.True);
                Assert.That(HasLabel(window, "Отладка"), Is.True);
            }
            finally
            {
                DialogueEditorLanguageSettings.CurrentLanguage = DialogueEditorLanguage.English;
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void PaletteShortcuts_DefaultsRenderInPalette()
        {
            var database = CreateDatabase("PaletteShortcutDefaults");
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);

                Assert.That(GetPaletteShortcutText(window, "textnode"), Is.EqualTo("Alt+1"));
                Assert.That(GetPaletteShortcutText(window, "comment"), Is.EqualTo("Alt+2"));
                Assert.That(GetPaletteShortcutText(window, "function"), Is.EqualTo("Alt+3"));
                Assert.That(GetPaletteShortcutText(window, "scene"), Is.EqualTo("Alt+4"));
                Assert.That(GetPaletteShortcutText(window, "debug"), Is.EqualTo("Alt+5"));
                Assert.That(GetPaletteShortcutText(window, "choice"), Is.EqualTo("Alt+6"));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void PaletteShortcut_WithCanvasFocus_CreatesNodeAtCanvasCenter()
        {
            var database = CreateDatabase("PaletteShortcutCreate");
            var dialogue = database.Npcs[0].Dialogues[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                var graphView = window.GraphViewForTests;
                graphView.FocusCanvas();

                var handled = graphView.TryHandlePaletteShortcut(new DialoguePaletteShortcut(KeyCode.Alpha1, alt: true));

                Assert.That(handled, Is.True);
                Assert.That(dialogue.Graph.Nodes.OfType<DialogueTextNodeData>(), Has.Count.EqualTo(2));
                Assert.That(dialogue.Graph.Nodes.OfType<DialogueTextNodeData>().Last().Title, Is.EqualTo("Text Node_2"));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void PaletteShortcut_ChoiceCreatesAnswerNode()
        {
            var database = CreateDatabase("PaletteShortcutChoice");
            var dialogue = database.Npcs[0].Dialogues[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                var graphView = window.GraphViewForTests;
                graphView.FocusCanvas();

                var handled = graphView.TryHandlePaletteShortcut(new DialoguePaletteShortcut(KeyCode.Alpha6, alt: true));

                Assert.That(handled, Is.True);
                Assert.That(dialogue.Graph.Nodes.OfType<DialogueChoiceNodeData>(), Has.Count.EqualTo(1));
                Assert.That(dialogue.Graph.Nodes.OfType<DialogueChoiceNodeData>().Single().Title, Is.EqualTo("Answer_2"));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void PaletteShortcut_InEmptyDatabaseCreatesNpcDialogueAndNode()
        {
            var database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            database.name = $"PaletteShortcutEmpty-{Guid.NewGuid():N}";
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                var graphView = window.GraphViewForTests;
                graphView.FocusCanvas();

                var handled = graphView.TryHandlePaletteShortcut(new DialoguePaletteShortcut(KeyCode.Alpha1, alt: true));

                Assert.That(handled, Is.True);
                Assert.That(database.Npcs, Has.Count.EqualTo(1));
                Assert.That(database.Npcs[0].Dialogues, Has.Count.EqualTo(1));
                Assert.That(database.Npcs[0].Dialogues[0].Graph.Nodes.OfType<DialogueTextNodeData>(), Has.Count.EqualTo(1));
                Assert.That(database.Npcs[0].Dialogues[0].Graph.Nodes[0].Title, Is.EqualTo("Text Node_1"));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void PaletteShortcut_WithoutCanvasFocus_DoesNotCreateNode()
        {
            var database = CreateDatabase("PaletteShortcutNoFocus");
            var dialogue = database.Npcs[0].Dialogues[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                var graphView = window.GraphViewForTests;
                graphView.ReleaseCanvasFocus();

                var handled = graphView.TryHandlePaletteShortcut(new DialoguePaletteShortcut(KeyCode.Alpha1, alt: true));

                Assert.That(handled, Is.False);
                Assert.That(dialogue.Graph.Nodes.OfType<DialogueTextNodeData>(), Has.Count.EqualTo(1));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void PaletteShortcut_UnmatchedWKeyKeepsKeyboardPan()
        {
            var database = CreateDatabase("PaletteShortcutPan");
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                var graphView = window.GraphViewForTests;
                graphView.FocusCanvas();

                SendKeyDown(graphView, KeyCode.W);
                graphView.StepKeyboardPan(1f);

                Assert.That(graphView.viewTransform.position.y, Is.EqualTo(45f).Within(0.01f));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void PaletteShortcutSettings_ReservePlainWasdAndAllowModifiedWasd()
        {
            Assert.That(DialoguePaletteShortcutSettings.IsBindable(new DialoguePaletteShortcut(KeyCode.W)), Is.False);
            Assert.That(DialoguePaletteShortcutSettings.IsBindable(new DialoguePaletteShortcut(KeyCode.A)), Is.False);
            Assert.That(DialoguePaletteShortcutSettings.IsBindable(new DialoguePaletteShortcut(KeyCode.S)), Is.False);
            Assert.That(DialoguePaletteShortcutSettings.IsBindable(new DialoguePaletteShortcut(KeyCode.D)), Is.False);
            Assert.That(DialoguePaletteShortcutSettings.IsBindable(new DialoguePaletteShortcut(KeyCode.W, alt: true)), Is.True);

            DialoguePaletteShortcutSettings.SetShortcut(
                DialoguePaletteItemType.Function,
                new DialoguePaletteShortcut(KeyCode.W, alt: true));

            Assert.That(
                DialoguePaletteShortcutSettings.FindMatchingItem(new DialoguePaletteShortcut(KeyCode.W)),
                Is.Null);
            Assert.That(
                DialoguePaletteShortcutSettings.FindMatchingItem(new DialoguePaletteShortcut(KeyCode.W, alt: true)),
                Is.EqualTo(DialoguePaletteItemType.Function));
        }

        [Test]
        public void PaletteShortcutSettings_LegacyPlainWasdBindingLoadsAsUnassigned()
        {
            EditorPrefs.SetString(
                DialoguePaletteShortcutSettings.EditorPrefsKey,
                "{\"Items\":[{\"ItemType\":\"Function\",\"KeyCode\":\"W\",\"Action\":false,\"Shift\":false,\"Alt\":false}]}");

            var shortcut = DialoguePaletteShortcutSettings.GetShortcut(DialoguePaletteItemType.Function);

            Assert.That(shortcut.IsAssigned, Is.False);
            Assert.That(DialoguePaletteShortcutSettings.FormatShortcut(shortcut), Is.EqualTo("Unassigned"));
            Assert.That(DialoguePaletteShortcutSettings.FindMatchingItem(new DialoguePaletteShortcut(KeyCode.W)), Is.Null);
        }

        [Test]
        public void PaletteShortcut_ModifiedWasdRebindCreatesNode()
        {
            var database = CreateDatabase("PaletteShortcutModifiedWasd");
            var dialogue = database.Npcs[0].Dialogues[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                var functionItem = window.rootVisualElement.Q<VisualElement>("palette-item-function");
                Assert.That(functionItem, Is.Not.Null);

                DoubleClick(functionItem);
                SendKeyDown(functionItem, KeyCode.W, EventModifiers.Alt);

                Assert.That(GetPaletteShortcutText(window, "function"), Is.EqualTo("Alt+W"));

                var graphView = window.GraphViewForTests;
                graphView.FocusCanvas();
                var handled = graphView.TryHandlePaletteShortcut(new DialoguePaletteShortcut(KeyCode.W, alt: true));

                Assert.That(handled, Is.True);
                Assert.That(dialogue.Graph.Nodes.OfType<FunctionNodeData>(), Has.Count.EqualTo(1));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void PaletteShortcut_RebindSavesAndRendersAfterWindowRebuild()
        {
            var database = CreateDatabase("PaletteShortcutPersist");
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                var functionItem = window.rootVisualElement.Q<VisualElement>("palette-item-function");
                Assert.That(functionItem, Is.Not.Null);

                DoubleClick(functionItem);
                Assert.That(GetPaletteShortcutText(window, "function"), Is.EqualTo("Press"));
                SendKeyDown(functionItem, KeyCode.F, EventModifiers.Alt | EventModifiers.Shift);

                Assert.That(GetPaletteShortcutText(window, "function"), Is.EqualTo("Shift+Alt+F"));

                window.Close();
                window = ScriptableObject.CreateInstance<DialogueEditorWindow>();
                window.InitializeForTests(database);

                Assert.That(GetPaletteShortcutText(window, "function"), Is.EqualTo("Shift+Alt+F"));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void PaletteShortcut_RebindClickDoesNotCreatePaletteNode()
        {
            var database = CreateDatabase("PaletteShortcutNoCreateOnRebind");
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                var graph = database.Npcs[0].Dialogues[0].Graph;
                var initialFunctionCount = graph.Nodes.OfType<FunctionNodeData>().Count();
                var functionItem = window.rootVisualElement.Q<VisualElement>("palette-item-function");
                Assert.That(functionItem, Is.Not.Null);

                Click(functionItem);
                DoubleClick(functionItem);

                Assert.That(GetPaletteShortcutText(window, "function"), Is.EqualTo("Press"));
                Assert.That(graph.Nodes.OfType<FunctionNodeData>().Count(), Is.EqualTo(initialFunctionCount));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void PaletteShortcut_DuplicateRebindTransfersBinding()
        {
            var database = CreateDatabase("PaletteShortcutDuplicate");
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                var debugItem = window.rootVisualElement.Q<VisualElement>("palette-item-debug");
                Assert.That(debugItem, Is.Not.Null);

                DoubleClick(debugItem);
                SendKeyDown(debugItem, KeyCode.Alpha1, EventModifiers.Alt);

                Assert.That(GetPaletteShortcutText(window, "debug"), Is.EqualTo("Alt+1"));
                Assert.That(GetPaletteShortcutText(window, "textnode"), Is.EqualTo("Unassigned"));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void PaletteShortcut_RebindEscapeCancelsAndDeleteClears()
        {
            var database = CreateDatabase("PaletteShortcutCancelClear");
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                var sceneItem = window.rootVisualElement.Q<VisualElement>("palette-item-scene");
                Assert.That(sceneItem, Is.Not.Null);

                DoubleClick(sceneItem);
                SendKeyDown(sceneItem, KeyCode.Escape);

                Assert.That(GetPaletteShortcutText(window, "scene"), Is.EqualTo("Alt+4"));

                DoubleClick(sceneItem);
                SendKeyDown(sceneItem, KeyCode.Delete);

                Assert.That(GetPaletteShortcutText(window, "scene"), Is.EqualTo("Unassigned"));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void UndoRedo_RestoresSelectedNodeAndInspectorState()
        {
            var database = CreateDatabase("SelectionUndo");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var titleField = window.rootVisualElement.Q<TextField>("node-title-field");
                Assert.That(titleField, Is.Not.Null);

                titleField.value = "Changed Title";

                Assert.That(window.HasUnsavedChangesForTests, Is.True);
                Assert.That(((DialogueTextNodeData)database.Npcs[0].Dialogues[0].Graph.Nodes[0]).Title, Is.EqualTo("Changed Title"));

                Undo.PerformUndo();

                Assert.That(window.SelectedNodeIdForTests, Is.EqualTo(node.Id));
                Assert.That(window.HasUnsavedChangesForTests, Is.False);
                Assert.That(window.rootVisualElement.Q<TextField>("node-title-field")?.value, Is.EqualTo("Original Title"));

                Undo.PerformRedo();

                Assert.That(window.SelectedNodeIdForTests, Is.EqualTo(node.Id));
                Assert.That(window.HasUnsavedChangesForTests, Is.True);
                Assert.That(window.rootVisualElement.Q<TextField>("node-title-field")?.value, Is.EqualTo("Changed Title"));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void UndoToBaseline_ClearsAutosaveSnapshotAndDirtyState()
        {
            var database = CreateDatabase("AutosaveUndo");
            var snapshotPath = DialogueEditorAutosaveStore.GetSnapshotPath(DialogueEditorAutosaveStore.GetStorageKey(database));
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                if (File.Exists(snapshotPath))
                {
                    File.Delete(snapshotPath);
                }

                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var bodyField = window.rootVisualElement.Q<TextField>("node-body-field");
                Assert.That(bodyField, Is.Not.Null);

                bodyField.value = "Updated body text";

                Assert.That(window.HasUnsavedChangesForTests, Is.True);
                Assert.That(File.Exists(snapshotPath), Is.True);

                Undo.PerformUndo();

                Assert.That(window.HasUnsavedChangesForTests, Is.False);
                Assert.That(File.Exists(snapshotPath), Is.False);
            }
            finally
            {
                if (File.Exists(snapshotPath))
                {
                    File.Delete(snapshotPath);
                }

                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void MassNodeDrag_DefersAutosaveUntilUndoGestureEnds()
        {
            var database = CreateDatabase("MassDragAutosave");
            var snapshotPath = DialogueEditorAutosaveStore.GetSnapshotPath(DialogueEditorAutosaveStore.GetStorageKey(database));
            var dialogue = database.Npcs[0].Dialogues[0];
            var graph = dialogue.Graph;
            var previousNode = (DialogueTextNodeData)graph.Nodes[0];
            for (var index = 1; index < 55; index++)
            {
                var node = new DialogueTextNodeData
                {
                    Title = $"Node {index + 1}",
                    Position = new Vector2((index % 10) * 300f, (index / 10) * 210f)
                };
                graph.Nodes.Add(node);
                graph.Links.Add(new NodeLinkData
                {
                    FromNodeId = previousNode.Id,
                    ToNodeId = node.Id,
                    Order = 0
                });
                previousNode = node;
            }

            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                if (File.Exists(snapshotPath))
                {
                    File.Delete(snapshotPath);
                }

                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                var graphView = window.GraphViewForTests;
                foreach (var nodeView in graphView.graphElements.OfType<DialogueTextNodeView>())
                {
                    graphView.AddToSelection(nodeView);
                }

                graphView.BeginSelectionPointerDrag(Vector2.zero);
                graphView.ContinueSelectionPointerDrag(new Vector2(48f, 24f));

                Assert.That(window.HasUnsavedChangesForTests, Is.False);
                Assert.That(File.Exists(snapshotPath), Is.False);

                graphView.EndSelectionPointerDrag();

                Assert.That(window.HasUnsavedChangesForTests, Is.True);
                Assert.That(File.Exists(snapshotPath), Is.False);

                graphView.EndUndoGesture();

                Assert.That(window.HasUnsavedChangesForTests, Is.True);
                Assert.That(File.Exists(snapshotPath), Is.True);

                Undo.PerformUndo();

                Assert.That(window.HasUnsavedChangesForTests, Is.False);
                Assert.That(File.Exists(snapshotPath), Is.False);
            }
            finally
            {
                if (File.Exists(snapshotPath))
                {
                    File.Delete(snapshotPath);
                }

                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void UndoRedo_RestoresLinkOrder()
        {
            var database = CreateDatabaseWithLinks("LinkUndo");
            var dialogue = database.Npcs[0].Dialogues[0];
            var startNode = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var originalFirstLinkId = DialogueGraphUtility.GetOutgoingLinks(dialogue.Graph, startNode.Id)[0].Id;
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                Assert.That(window.FocusDialogueNode(dialogue, startNode.Id), Is.True);

                var orderField = window.rootVisualElement.Query<IntegerField>("link-order-field").ToList()[0];
                orderField.value = 5;

                var reorderedFirstLinkId = DialogueGraphUtility.GetOutgoingLinks(database.Npcs[0].Dialogues[0].Graph, startNode.Id)[0].Id;
                Assert.That(reorderedFirstLinkId, Is.Not.EqualTo(originalFirstLinkId));

                Undo.PerformUndo();

                Assert.That(DialogueGraphUtility.GetOutgoingLinks(database.Npcs[0].Dialogues[0].Graph, startNode.Id)[0].Id, Is.EqualTo(originalFirstLinkId));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void NodeInspector_AddChoiceCreatesAnswerBranchAndSelectsAnswer()
        {
            var database = CreateDatabase("AddAnswerBranch");
            var dialogue = database.Npcs[0].Dialogues[0];
            var startNode = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                Assert.That(window.FocusDialogueNode(dialogue, startNode.Id), Is.True);

                var addButton = window.rootVisualElement.Q<Button>("node-add-choice-button");
                Assert.That(addButton, Is.Not.Null);
                Click(addButton);

                var choiceNode = dialogue.Graph.Nodes.OfType<DialogueChoiceNodeData>().Single();
                Assert.That(startNode.UseOutputsAsChoices, Is.True);
                Assert.That(dialogue.Graph.Nodes.OfType<DialogueTextNodeData>(), Has.Count.EqualTo(1));
                Assert.That(dialogue.Graph.Links, Has.Count.EqualTo(1));
                Assert.That(DialogueGraphUtility.GetOutgoingLinks(dialogue.Graph, startNode.Id).Single().ToNodeId, Is.EqualTo(choiceNode.Id));
                Assert.That(DialogueGraphUtility.GetOutgoingLinks(dialogue.Graph, choiceNode.Id), Is.Empty);

                var answerField = window.rootVisualElement.Q<TextField>("choice-button-text-field");
                Assert.That(answerField, Is.Not.Null);
                answerField.value = "Ask about work";
                Assert.That(choiceNode.ChoiceText, Is.EqualTo("Ask about work"));
                var bodyField = window.rootVisualElement.Q<TextField>("choice-body-field");
                Assert.That(bodyField, Is.Not.Null);
                bodyField.value = "There is plenty to do.";
                Assert.That(choiceNode.BodyText, Is.EqualTo("There is plenty to do."));
                Assert.That(window.HasUnsavedChangesForTests, Is.True);

                Assert.That(window.FocusDialogueNode(dialogue, startNode.Id), Is.True);
                Assert.That(window.rootVisualElement.Q<VisualElement>("node-answers-list"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<VisualElement>("answer-card"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<TextField>("link-choice-field"), Is.Null);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void NodeInspector_ShowsManualAnswerLinksWithoutChoiceMode()
        {
            var database = CreateDatabase("ManualAnswerLinkInspector");
            var dialogue = database.Npcs[0].Dialogues[0];
            var startNode = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var answer = new DialogueChoiceNodeData
            {
                ChoiceText = "Manual answer",
                Position = new Vector2(360f, 120f)
            };
            dialogue.Graph.Nodes.Add(answer);
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = startNode.Id, ToNodeId = answer.Id, Order = 0 });
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, startNode.Id), Is.True);

                Assert.That(startNode.UseOutputsAsChoices, Is.False);
                Assert.That(window.rootVisualElement.Q<VisualElement>("node-answers-list"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<VisualElement>("answer-card"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<TextField>("link-choice-field"), Is.Null);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void TextNodeToggles_UseAlignedLayoutClass()
        {
            var database = CreateDatabase("TextNodeToggleLayout");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                Assert.That(window.rootVisualElement.Q<Toggle>("node-start-toggle")
                    ?.ClassListContains("dialogue-editor__node-toggle"), Is.True);
                Assert.That(window.rootVisualElement.Q<Button>("node-add-choice-button"), Is.Not.Null);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void UndoRedo_RestoresStartToggleValue()
        {
            var database = CreateDatabase("StartToggleUndo");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var startToggle = window.rootVisualElement.Q<Toggle>("node-start-toggle");
                Assert.That(startToggle, Is.Not.Null);

                startToggle.value = false;
                Assert.That(((DialogueTextNodeData)database.Npcs[0].Dialogues[0].Graph.Nodes[0]).IsStartNode, Is.False);

                Undo.PerformUndo();
                Assert.That(((DialogueTextNodeData)database.Npcs[0].Dialogues[0].Graph.Nodes[0]).IsStartNode, Is.True);

                Undo.PerformRedo();
                Assert.That(((DialogueTextNodeData)database.Npcs[0].Dialogues[0].Graph.Nodes[0]).IsStartNode, Is.False);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void NodeInspector_ShowsEditableIdField()
        {
            var database = CreateDatabase("NodeIdInspector");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var idField = window.rootVisualElement.Q<TextField>("node-id-field");
                Assert.That(idField, Is.Not.Null);
                Assert.That(idField.value, Is.EqualTo(node.Id));

                var whereUsedFoldout = window.rootVisualElement.Q<Foldout>("where-used-foldout");
                Assert.That(whereUsedFoldout, Is.Not.Null);
                Assert.That(whereUsedFoldout.value, Is.False);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void NodeInspector_EditsVoiceKeyAndMarksDatabaseDirty()
        {
            var database = CreateDatabase("VoiceKeyInspector");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            node.VoiceKey = "npc.original.line";
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var voiceKeyField = window.rootVisualElement.Q<TextField>("node-voice-key-field");
                Assert.That(voiceKeyField, Is.Not.Null);
                Assert.That(voiceKeyField.value, Is.EqualTo("npc.original.line"));

                voiceKeyField.value = "innkeeper.greeting.hello";

                Assert.That(node.VoiceKey, Is.EqualTo("innkeeper.greeting.hello"));
                Assert.That(window.HasUnsavedChangesForTests, Is.True);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void NodeInspector_UpdatesRichTextPreviewWhenBodyChanges()
        {
            var database = CreateDatabase("RichTextPreview");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var bodyField = window.rootVisualElement.Q<TextField>("node-body-field");
                var preview = window.rootVisualElement.Q<VisualElement>("node-body-rich-preview");
                Assert.That(bodyField, Is.Not.Null);
                Assert.That(preview, Is.Not.Null);

                bodyField.value = "Hello <b>friend</b> <unknown>tag</unknown>";

                Assert.That(node.BodyText, Is.EqualTo("Hello <b>friend</b> <unknown>tag</unknown>"));
                var runs = preview.Query<Label>(className: "dialogue-rich-text-run").ToList();
                Assert.That(GetRichTextPreviewText(preview), Is.EqualTo("Hello friend <unknown>tag</unknown>"));
                Assert.That(runs, Has.Count.GreaterThanOrEqualTo(3));
                Assert.That(runs[0].text, Is.EqualTo("Hello\u00A0"));
                Assert.That(runs[2].text, Does.StartWith("\u00A0<unknown>"));
                Assert.That(runs
                    .Any(label => label.text == "friend" && label.style.unityFontStyleAndWeight.value == FontStyle.Bold), Is.True);
                Assert.That(window.HasUnsavedChangesForTests, Is.True);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void NodeInspector_BodyTextFieldUsesFullWidthVerticalLayout()
        {
            var database = CreateDatabase("BodyTextLayout");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var bodyField = window.rootVisualElement.Q<TextField>("node-body-field");
                Assert.That(bodyField, Is.Not.Null);
                Assert.That(bodyField.ClassListContains("dialogue-editor__body-field"), Is.True);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void NodeInspector_RichTextToolbarWrapsSelectedText()
        {
            var database = CreateDatabase("RichTextToolbarSelection");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            node.BodyText = "Hello world";
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var bodyField = window.rootVisualElement.Q<TextField>("node-body-field");
                var boldButton = window.rootVisualElement.Q<Button>("rich-text-bold-button");
                Assert.That(bodyField, Is.Not.Null);
                Assert.That(boldButton, Is.Not.Null);

                bodyField.cursorIndex = 6;
                bodyField.selectIndex = 11;
                Click(boldButton);

                Assert.That(node.BodyText, Is.EqualTo("Hello <b>world</b>"));
                Assert.That(window.HasUnsavedChangesForTests, Is.True);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void NodeInspector_RichTextToolbarInsertsTagsAtCursor()
        {
            var database = CreateDatabase("RichTextToolbarCursor");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            node.BodyText = "Hello";
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var bodyField = window.rootVisualElement.Q<TextField>("node-body-field");
                var italicButton = window.rootVisualElement.Q<Button>("rich-text-italic-button");
                Assert.That(bodyField, Is.Not.Null);
                Assert.That(italicButton, Is.Not.Null);

                bodyField.Focus();
                bodyField.cursorIndex = 5;
                bodyField.selectIndex = 5;
                Click(italicButton);

                Assert.That(node.BodyText, Is.EqualTo("Hello<i></i>"));
                Assert.That(bodyField.cursorIndex, Is.EqualTo("Hello<i>".Length));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void NodeInspector_RichTextColorListAddsPersistsAppliesAndUsesInlinePicker()
        {
            var database = CreateDatabase("RichTextCustomColor");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            node.BodyText = "Danger";
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var bodyField = window.rootVisualElement.Q<TextField>("node-body-field");
                var addButton = window.rootVisualElement.Q<Button>("rich-text-color-add-button");
                Assert.That(bodyField, Is.Not.Null);
                Assert.That(addButton, Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<VisualElement>("rich-text-highlight-list"), Is.Null);

                Click(addButton);
                var customColorField = window.rootVisualElement.Q<TextField>("rich-text-color-field-0");
                var applyButton = window.rootVisualElement.Q<Button>("rich-text-color-apply-0");
                var pickerButton = window.rootVisualElement.Q<Button>("rich-text-color-field-picker-toggle-0");
                Assert.That(customColorField, Is.Not.Null);
                Assert.That(applyButton, Is.Not.Null);
                Assert.That(pickerButton, Is.Not.Null);

                bodyField.cursorIndex = 0;
                bodyField.selectIndex = 6;
                customColorField.value = "ff6b6b";
                var swatchButton = window.rootVisualElement.Q<Button>("rich-text-color-field-swatch-0");
                Assert.That(swatchButton, Is.Not.Null);
                Assert.That(customColorField.value, Is.EqualTo("#FF6B6B"));

                customColorField.value = "#ff6b6b80";
                Click(applyButton);

                Assert.That(node.BodyText, Is.EqualTo("Danger"));
                Assert.That(customColorField.ClassListContains("dialogue-editor__rich-text-color-field--invalid"), Is.True);

                customColorField.value = "#ff6b6b";
                Assert.That(customColorField.ClassListContains("dialogue-editor__rich-text-color-field--invalid"), Is.False);

                Click(swatchButton);
                Assert.That(node.BodyText, Is.EqualTo("Danger"));
                Assert.That(swatchButton.ClassListContains("dialogue-editor__rich-text-color-icon--selected"), Is.True);

                customColorField.value = "#ffffff";
                Click(pickerButton);
                var picker = window.rootVisualElement.Q<VisualElement>("rich-text-color-field-picker-0");
                var wheel = window.rootVisualElement.Q<VisualElement>("rich-text-color-field-picker-wheel-0");
                var pickerHandle = window.rootVisualElement.Q<VisualElement>("rich-text-color-field-picker-handle-0");
                var brightnessTrack = window.rootVisualElement.Q<VisualElement>("rich-text-color-field-picker-brightness-0");
                var brightnessHandle = window.rootVisualElement.Q<VisualElement>("rich-text-color-field-picker-brightness-handle-0");
                Assert.That(picker, Is.Not.Null);
                Assert.That(wheel, Is.Not.Null);
                Assert.That(pickerHandle, Is.Not.Null);
                Assert.That(brightnessTrack, Is.Not.Null);
                Assert.That(brightnessHandle, Is.Not.Null);
                Assert.That(brightnessTrack.userData, Is.TypeOf<DialogueEditorWindow.RichTextColorPickerState>());

                ((DialogueEditorWindow.RichTextColorPickerState)brightnessTrack.userData).SetBrightnessForTests(0.5f);

                var pickedColor = customColorField.value;
                Assert.That(pickedColor, Does.Match("^#[0-9A-F]{6}$"));
                Assert.That(pickedColor, Is.Not.EqualTo("#FFFFFF"));
                Assert.That(node.BodyText, Is.EqualTo("Danger"));

                Click(applyButton);

                Assert.That(node.BodyText, Is.EqualTo($"<color={pickedColor}>Danger</color>"));

                window.Close();
                window = ScriptableObject.CreateInstance<DialogueEditorWindow>();
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                Assert.That(window.rootVisualElement.Q<Button>("rich-text-color-field-swatch-0"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<TextField>("rich-text-color-field-0")?.value, Is.EqualTo(pickedColor));
            }
            finally
            {
                EditorPrefs.DeleteKey(RichTextTextColorsPrefsKey);
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                if (window != null)
                {
                    window.Close();
                }
            }
        }

        [Test]
        public void NodeInspector_EditsSpeakerAndMarksDatabaseDirty()
        {
            var database = CreateDatabase("SpeakerInspector");
            var dialogue = database.Npcs[0].Dialogues[0];
            dialogue.Speakers.Add(new DialogueSpeakerEntry { Id = "npc", Name = "NPC" });
            dialogue.Speakers.Add(new DialogueSpeakerEntry { Id = "hero", Name = "Hero" });
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var speakerField = window.rootVisualElement.Q<PopupField<string>>("node-speaker-field");
                Assert.That(speakerField, Is.Not.Null);
                Assert.That(speakerField.value, Is.EqualTo("NPC"));

                speakerField.value = "Hero";

                Assert.That(node.SpeakerId, Is.EqualTo("hero"));
                Assert.That(window.HasUnsavedChangesForTests, Is.True);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void DialogueInspector_ManagesSpeakerRosterAndClearsRemovedReferences()
        {
            var database = CreateDatabase("SpeakerRoster");
            var dialogue = database.Npcs[0].Dialogues[0];
            dialogue.Speakers.Add(new DialogueSpeakerEntry { Id = "npc", Name = "NPC" });
            dialogue.Speakers.Add(new DialogueSpeakerEntry { Id = "hero", Name = "Hero" });
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            node.SpeakerId = "hero";
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                window.SaveBaselineForTests();

                var nameFields = window.rootVisualElement.Query<TextField>("dialogue-speaker-name-field").ToList();
                Assert.That(nameFields, Has.Count.EqualTo(2));
                Assert.That(nameFields[0].ClassListContains("dialogue-editor__speaker-name-field"), Is.True);
                Assert.That(window.rootVisualElement.Q<Button>("dialogue-speaker-remove-button")
                    ?.ClassListContains("dialogue-editor__speaker-remove-button"), Is.True);
                nameFields[0].value = "Innkeeper";
                Assert.That(dialogue.Speakers[0].Name, Is.EqualTo("Innkeeper"));

                window.AddSpeaker(dialogue);
                Assert.That(dialogue.Speakers, Has.Count.EqualTo(3));
                Assert.That(dialogue.Speakers[2].Name, Is.EqualTo("Speaker 3"));

                window.RemoveSpeaker(dialogue, dialogue.Speakers[1]);
                Assert.That(dialogue.Speakers.Select(speaker => speaker.Id), Does.Not.Contain("hero"));
                Assert.That(node.SpeakerId, Is.Empty);
                Assert.That(window.HasUnsavedChangesForTests, Is.True);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void DialogueInspector_ShowsDialogueStartGateAndClearButton()
        {
            var database = CreateDatabase("DialogueStartGate");
            var dialogue = database.Npcs[0].Dialogues[0];
            dialogue.StartCondition.Type = ConditionType.VariableCheck;
            dialogue.StartCondition.Key = "has_key";
            dialogue.StartCondition.Operator = "==";
            dialogue.StartCondition.Value = "true";
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            node.Condition.Type = ConditionType.None;
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                window.SaveBaselineForTests();

                Assert.That(HasLabel(window.rootVisualElement, "Dialogue Start Gate"), Is.True);
                Assert.That(window.rootVisualElement.Q<Toggle>("node-start-toggle"), Is.Null);

                var clearButton = window.rootVisualElement.Q<Button>("dialogue-start-gate-clear-button");
                Assert.That(clearButton, Is.Not.Null);
                Assert.That(clearButton.enabledSelf, Is.True);

                Click(clearButton);

                Assert.That(dialogue.StartCondition.Type, Is.EqualTo(ConditionType.None));
                Assert.That(dialogue.StartCondition.Key, Is.Empty);
                Assert.That(node.Condition.Type, Is.EqualTo(ConditionType.None));
                Assert.That(window.HasUnsavedChangesForTests, Is.True);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void Autosave_PreservesSpeakersAndNodeSpeakerId()
        {
            var database = CreateDatabase("SpeakerAutosave");
            var dialogue = database.Npcs[0].Dialogues[0];
            dialogue.Speakers.Add(new DialogueSpeakerEntry { Id = "npc", Name = "NPC" });
            dialogue.Speakers.Add(new DialogueSpeakerEntry { Id = "hero", Name = "Hero" });
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            node.SpeakerId = "hero";
            node.VoiceKey = "hero.line";
            node.LocalizationKey = "Conversation/Farm.Plot.Dialogue_0001/Entry/1/Dialogue Text";
            DialogueTextLocalizationUtility.SetBodyText(node, "en", "Hello");
            var restored = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            var tempRoot = Path.Combine(Path.GetTempPath(), $"NewDialSpeakerAutosave-{Guid.NewGuid():N}");

            try
            {
                DialogueEditorAutosaveStore.SaveSnapshot(database, "speaker-test", tempRoot);

                Assert.That(DialogueEditorAutosaveStore.TryLoadSnapshot(restored, "speaker-test", tempRoot), Is.True);
                var restoredDialogue = restored.Npcs[0].Dialogues[0];
                var restoredNode = (DialogueTextNodeData)restoredDialogue.Graph.Nodes[0];

                Assert.That(restoredDialogue.Speakers.Select(speaker => speaker.Name), Is.EqualTo(new[] { "NPC", "Hero" }));
                Assert.That(restoredNode.SpeakerId, Is.EqualTo("hero"));
                Assert.That(restoredNode.VoiceKey, Is.EqualTo("hero.line"));
                Assert.That(restoredNode.LocalizationKey, Is.EqualTo("Conversation/Farm.Plot.Dialogue_0001/Entry/1/Dialogue Text"));
                Assert.That(DialogueTextLocalizationUtility.GetBodyText(restoredNode, "en"), Is.EqualTo("Hello"));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public void EditingNodeId_UpdatesLinksAndKeepsSelection()
        {
            var database = CreateDatabaseWithLinks("NodeIdRename");
            var dialogue = database.Npcs[0].Dialogues[0];
            var startNode = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var leftNode = (DialogueTextNodeData)dialogue.Graph.Nodes[1];
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.SuppressIdentifierWarningsForTests = true;
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, leftNode.Id), Is.True);

                var idField = window.rootVisualElement.Q<TextField>("node-id-field");
                idField.value = "renamed-left";

                Assert.That(leftNode.Id, Is.EqualTo("renamed-left"));
                Assert.That(window.SelectedNodeIdForTests, Is.EqualTo("renamed-left"));
                Assert.That(DialogueGraphUtility.GetOutgoingLinks(dialogue.Graph, startNode.Id)[0].ToNodeId, Is.EqualTo("renamed-left"));
                Assert.That(window.rootVisualElement.Q<TextField>("node-id-field")?.value, Is.EqualTo("renamed-left"));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void UndoRedo_RestoresNodeIdAndLinks()
        {
            var database = CreateDatabaseWithLinks("NodeIdUndo");
            var dialogue = database.Npcs[0].Dialogues[0];
            var startNode = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var leftNode = (DialogueTextNodeData)dialogue.Graph.Nodes[1];
            var originalId = leftNode.Id;
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.SuppressIdentifierWarningsForTests = true;
                window.InitializeForTests(database);
                window.SaveBaselineForTests();
                Assert.That(window.FocusDialogueNode(dialogue, leftNode.Id), Is.True);

                var idField = window.rootVisualElement.Q<TextField>("node-id-field");
                idField.value = "renamed-left";

                Assert.That(leftNode.Id, Is.EqualTo("renamed-left"));
                Assert.That(DialogueGraphUtility.GetOutgoingLinks(dialogue.Graph, startNode.Id)[0].ToNodeId, Is.EqualTo("renamed-left"));

                Undo.PerformUndo();

                leftNode = (DialogueTextNodeData)dialogue.Graph.Nodes[1];
                Assert.That(leftNode.Id, Is.EqualTo(originalId));
                Assert.That(DialogueGraphUtility.GetOutgoingLinks(dialogue.Graph, startNode.Id)[0].ToNodeId, Is.EqualTo(originalId));
                Assert.That(window.rootVisualElement.Q<TextField>("node-id-field")?.value, Is.EqualTo(originalId));

                Undo.PerformRedo();

                leftNode = (DialogueTextNodeData)dialogue.Graph.Nodes[1];
                Assert.That(leftNode.Id, Is.EqualTo("renamed-left"));
                Assert.That(DialogueGraphUtility.GetOutgoingLinks(dialogue.Graph, startNode.Id)[0].ToNodeId, Is.EqualTo("renamed-left"));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void ProjectPanel_ShowsEditableNpcAndDialogueIdFields()
        {
            var database = CreateDatabase("ProjectIdFields");
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);

                var npcIdField = window.rootVisualElement.Q<TextField>("npc-id-field");
                var dialogueIdField = window.rootVisualElement.Q<TextField>("dialogue-id-field");

                Assert.That(npcIdField, Is.Not.Null);
                Assert.That(dialogueIdField, Is.Not.Null);
                Assert.That(npcIdField.value, Is.EqualTo(database.Npcs[0].Id));
                Assert.That(dialogueIdField.value, Is.EqualTo(database.Npcs[0].Dialogues[0].Id));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void ProjectPanel_UsesCompactMetadataCards()
        {
            var database = CreateDatabase("ProjectCompactMetadata");
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);

                var projectNpcIdField = window.rootVisualElement.Q<TextField>("npc-id-field");
                var projectDialogueIdField = window.rootVisualElement.Q<TextField>("dialogue-id-field");
                var compactIdCards = window.rootVisualElement
                    .Query(className: "dialogue-editor__id-card--compact")
                    .ToList();
                var compactWhereUsedCards = window.rootVisualElement
                    .Query(className: "dialogue-editor__where-used-card--compact")
                    .ToList();

                Assert.That(projectNpcIdField, Is.Not.Null);
                Assert.That(projectDialogueIdField, Is.Not.Null);
                Assert.That(compactIdCards, Has.Count.GreaterThanOrEqualTo(2));
                Assert.That(compactWhereUsedCards, Has.Count.GreaterThanOrEqualTo(2));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void ProjectPanel_ShowsDuplicateNpcAndDialogueWarnings()
        {
            var database = CreateDatabase("ProjectIdWarnings");
            var firstNpc = database.Npcs[0];
            var secondNpc = new NpcEntry { Name = "Second NPC" };
            secondNpc.Dialogues.Add(new DialogueEntry { Name = "Second Dialogue" });
            database.Npcs.Add(secondNpc);
            firstNpc.Dialogues.Add(new DialogueEntry { Name = "Duplicate Dialogue Candidate" });
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.SuppressIdentifierWarningsForTests = true;
                window.InitializeForTests(database);

                var npcFields = window.rootVisualElement.Query<TextField>("npc-id-field").ToList();
                Assert.That(npcFields, Has.Count.EqualTo(2));
                npcFields[1].value = firstNpc.Id;

                Assert.That(HasIdentifierIssue(window, "Duplicate NPC Id"), Is.True);

                var dialogueFields = window.rootVisualElement.Query<TextField>("dialogue-id-field").ToList();
                Assert.That(dialogueFields, Has.Count.GreaterThanOrEqualTo(2));
                dialogueFields[1].value = firstNpc.Dialogues[0].Id;

                Assert.That(HasIdentifierIssue(window, "Duplicate Dialogue Id"), Is.True);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void NodeInspector_ShowsEmptyAndDuplicateIdWarnings()
        {
            var database = CreateDatabase("NodeIdWarnings");
            var dialogue = database.Npcs[0].Dialogues[0];
            var firstNode = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var secondNode = new DialogueTextNodeData
            {
                Title = "Second",
                Position = new Vector2(360f, 120f)
            };
            dialogue.Graph.Nodes.Add(secondNode);
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.SuppressIdentifierWarningsForTests = true;
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, secondNode.Id), Is.True);

                var idField = window.rootVisualElement.Q<TextField>("node-id-field");
                idField.value = string.Empty;
                Assert.That(HasIdentifierIssue(window, "Id is empty"), Is.True);

                idField = window.rootVisualElement.Q<TextField>("node-id-field");
                idField.value = firstNode.Id;
                Assert.That(HasIdentifierIssue(window, "Duplicate Node Id"), Is.True);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void NodeInspector_ShowsChoiceFlowDiagnostics()
        {
            var database = CreateDatabase("ChoiceFlowWarnings");
            var dialogue = database.Npcs[0].Dialogues[0];
            var choiceNode = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            choiceNode.UseOutputsAsChoices = true;
            var untitledTarget = new DialogueTextNodeData
            {
                Title = string.Empty,
                Position = new Vector2(360f, 120f)
            };
            dialogue.Graph.Nodes.Add(untitledTarget);
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = choiceNode.Id,
                ToNodeId = untitledTarget.Id,
                Order = 0,
                ChoiceText = string.Empty
            });
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, choiceNode.Id), Is.True);

                Assert.That(HasChoiceDiagnostic(window, "Choice text and target title are both empty."), Is.True);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void ConditionEditor_HidesIrrelevantFieldsForNone()
        {
            var database = CreateDatabase("ConditionNone");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            node.Condition.Type = ConditionType.None;
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                Assert.That(window.rootVisualElement.Q<TextField>("condition-key-field"), Is.Null);
                Assert.That(window.rootVisualElement.Q<PopupField<string>>("condition-operator-field"), Is.Null);
                Assert.That(window.rootVisualElement.Q<TextField>("condition-value-field"), Is.Null);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void ConditionEditor_ShowsGuidedFieldsForVariableCondition()
        {
            var database = CreateDatabase("ConditionGuided");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            node.Condition.Type = ConditionType.VariableCheck;
            node.Condition.Operator = "==";
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                Assert.That(window.rootVisualElement.Q<TextField>("condition-key-field"), Is.Not.Null);
                var operatorField = window.rootVisualElement.Q<PopupField<string>>("condition-operator-field");
                Assert.That(operatorField, Is.Not.Null);
                Assert.That(operatorField.choices, Does.Contain(">="));
                Assert.That(window.rootVisualElement.Q<TextField>("condition-value-field"), Is.Not.Null);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void ConditionTypes_ArePluginGenericOnly()
        {
            Assert.That(Enum.GetNames(typeof(ConditionType)), Is.EqualTo(new[]
            {
                nameof(ConditionType.None),
                nameof(ConditionType.Custom),
                nameof(ConditionType.VariableCheck)
            }));
        }

        [Test]
        public void ConditionEditor_ShowsProviderKeySuggestions()
        {
            var provider = new TestConditionMetadataProvider();
            DialogueConditionMetadataRegistry.RegisterProvider(provider);
            var database = CreateDatabase("ConditionSuggestions");
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            node.Condition.Type = ConditionType.VariableCheck;
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                Assert.That(window.rootVisualElement.Q<PopupField<string>>("condition-key-suggestion-field"), Is.Not.Null);
            }
            finally
            {
                DialogueConditionMetadataRegistry.UnregisterProvider(provider);
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void VariablesPanel_AddsVariableAndMarksDirty()
        {
            var database = CreateDatabase("VariablesPanel");
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                window.SaveBaselineForTests();

                typeof(DialogueEditorWindow)
                    .GetMethod("AddVariable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(window, Array.Empty<object>());

                Assert.That(database.Variables, Has.Count.EqualTo(1));
                Assert.That(window.HasUnsavedChangesForTests, Is.True);
                Assert.That(window.rootVisualElement.Q<VisualElement>("variables-panel"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<TextField>("variable-key-field"), Is.Not.Null);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void ConditionEditor_IncludesDatabaseVariableSuggestionsAndTypedValue()
        {
            var database = CreateDatabase("ConditionDatabaseVariable");
            database.Variables.Add(new DialogueVariableDefinition
            {
                Key = "trust",
                DisplayName = "Trust",
                Type = DialogueArgumentType.Int,
                DefaultValue = DialogueArgumentValue.FromInt(1)
            });
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            node.Condition.Type = ConditionType.VariableCheck;
            node.Condition.Key = "trust";
            node.Condition.Operator = ">=";
            node.Condition.Value = "2";
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var suggestions = window.rootVisualElement.Q<PopupField<string>>("condition-key-suggestion-field");
                Assert.That(suggestions, Is.Not.Null);
                Assert.That(suggestions.choices.Any(choice => choice.Contains("Trust")), Is.True);
                Assert.That(window.rootVisualElement.Q<IntegerField>("condition-int-value-field"), Is.Not.Null);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void ConditionEditor_BoolVariableUsesExplicitBoolValue()
        {
            var database = CreateDatabase("ConditionBoolVariable");
            database.Variables.Add(new DialogueVariableDefinition
            {
                Key = "IsPiskaStole?",
                DisplayName = "variable_1",
                Type = DialogueArgumentType.Bool,
                DefaultValue = DialogueArgumentValue.FromBool(false)
            });
            var dialogue = database.Npcs[0].Dialogues[0];
            var node = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            node.Condition.Type = ConditionType.VariableCheck;
            node.Condition.Key = "IsPiskaStole?";
            node.Condition.Operator = "Truthy";
            node.Condition.Value = string.Empty;
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, node.Id), Is.True);

                var operatorField = window.rootVisualElement.Q<PopupField<string>>("condition-operator-field");
                Assert.That(operatorField, Is.Not.Null);
                Assert.That(operatorField.choices, Is.EqualTo(new[] { "==", "!=" }));
                Assert.That(node.Condition.Operator, Is.EqualTo("=="));
                Assert.That(node.Condition.Value, Is.EqualTo("true"));
                Assert.That(window.rootVisualElement.Q<Toggle>("condition-bool-value-field"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Query<Label>().ToList()
                    .Any(label => label.text == "The selected operator does not use an expected value."), Is.False);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void PreviewWindow_DatabaseBoolVariableAppearsAndCanOverrideStartGate()
        {
            var database = CreateDatabase("PreviewDatabaseBoolVariable");
            database.Variables.Add(new DialogueVariableDefinition
            {
                Key = "has_key",
                Type = DialogueArgumentType.Bool,
                DefaultValue = DialogueArgumentValue.FromBool(false)
            });
            var dialogue = database.Npcs[0].Dialogues[0];
            var startNode = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            dialogue.StartCondition.Type = ConditionType.VariableCheck;
            dialogue.StartCondition.Key = "has_key";
            dialogue.StartCondition.Operator = "==";
            dialogue.StartCondition.Value = "true";
            var owner = ScriptableObject.CreateInstance<DialogueEditorWindow>();
            var preview = ScriptableObject.CreateInstance<DialoguePreviewWindow>();

            try
            {
                owner.InitializeForTests(database);
                preview.InitializeForTests(dialogue, owner);

                Assert.That(preview.SessionForTests.CurrentNode, Is.Null);
                Assert.That(preview.SessionForTests.CurrentReason, Does.Contain("Dialogue-level start condition blocked"));
                Assert.That(preview.SessionForTests.CurrentReason, Does.Contain("current value: false"));
                Assert.That(preview.rootVisualElement.Query<TextField>("preview-variable-key-field").ToList()
                    .Any(field => field.value == "has_key"), Is.True);

                var boolField = preview.rootVisualElement.Q<Toggle>("preview-variable-bool-value-field");
                Assert.That(boolField, Is.Not.Null);
                Assert.That(boolField.value, Is.False);

                boolField.value = true;

                Assert.That(preview.SessionForTests.CurrentNode, Is.EqualTo(startNode));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                preview.Close();
                owner.Close();
            }
        }

        [Test]
        public void PreviewWindow_ShowsRuntimeVariableChanges()
        {
            var database = CreateDatabase("PreviewRuntimeVariableChange");
            database.Variables.Add(new DialogueVariableDefinition
            {
                Key = "unlocked",
                Type = DialogueArgumentType.Bool,
                DefaultValue = DialogueArgumentValue.FromBool(false)
            });
            var dialogue = database.Npcs[0].Dialogues[0];
            var startNode = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var functionNode = new FunctionNodeData
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
            var targetNode = new DialogueTextNodeData { Title = "Unlocked" };
            dialogue.Graph.Nodes.Add(functionNode);
            dialogue.Graph.Nodes.Add(targetNode);
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = startNode.Id, ToNodeId = functionNode.Id, Order = 0 });
            dialogue.Graph.Links.Add(new NodeLinkData { FromNodeId = functionNode.Id, ToNodeId = targetNode.Id, Order = 0 });
            var owner = ScriptableObject.CreateInstance<DialogueEditorWindow>();
            var preview = ScriptableObject.CreateInstance<DialoguePreviewWindow>();

            try
            {
                owner.InitializeForTests(database);
                preview.InitializeForTests(dialogue, owner);

                var boolField = preview.rootVisualElement.Q<Toggle>("preview-variable-bool-value-field");
                Assert.That(boolField, Is.Not.Null);
                Assert.That(boolField.value, Is.False);

                Assert.That(preview.SessionForTests.Advance(), Is.True);
                preview.RefreshForTests();

                boolField = preview.rootVisualElement.Q<Toggle>("preview-variable-bool-value-field");
                Assert.That(boolField, Is.Not.Null);
                Assert.That(boolField.value, Is.True);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                preview.Close();
                owner.Close();
            }
        }

        [Test]
        public void FunctionInspector_SetVariableUsesVariableDropdown()
        {
            var database = CreateDatabase("SetVariableInspector");
            database.Variables.Add(new DialogueVariableDefinition
            {
                Key = "door_open",
                DisplayName = "Door Open",
                Type = DialogueArgumentType.Bool,
                DefaultValue = DialogueArgumentValue.FromBool(false)
            });
            var dialogue = database.Npcs[0].Dialogues[0];
            var functionNode = new FunctionNodeData
            {
                FunctionId = DialogueBuiltInFunctions.SetVariableFunctionId
            };
            dialogue.Graph.Nodes.Add(functionNode);
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, functionNode.Id), Is.True);

                Assert.That(window.rootVisualElement.Q<PopupField<string>>("set-variable-key-field"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<Toggle>("set-variable-bool-value-field"), Is.Not.Null);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void CommentInspector_ShowsSeparateDeleteActions()
        {
            var database = CreateDatabase("CommentDeleteActions");
            var dialogue = database.Npcs[0].Dialogues[0];
            var commentNode = new CommentNodeData
            {
                Title = "Comment",
                Position = new Vector2(80f, 80f),
                Area = new Rect(80f, 80f, 360f, 240f)
            };
            dialogue.Graph.Nodes.Add(commentNode);
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, commentNode.Id), Is.True);

                Assert.That(window.rootVisualElement.Q<Button>("comment-delete-node-button")?.text, Is.EqualTo("Delete Node"));
                Assert.That(window.rootVisualElement.Q<Button>("comment-delete-group-button")?.text, Is.EqualTo("Delete Comment With Contents"));
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void FunctionInspector_ShowsFallbackFreeFormFields()
        {
            var database = CreateDatabase("FunctionFallback");
            var dialogue = database.Npcs[0].Dialogues[0];
            var functionNode = new FunctionNodeData
            {
                Title = "Function",
                FunctionId = "custom.function",
                Position = new Vector2(360f, 120f)
            };
            dialogue.Graph.Nodes.Add(functionNode);
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, functionNode.Id), Is.True);

                Assert.That(window.rootVisualElement.Q<TextField>("function-id-field"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<TextField>("argument-name-field"), Is.Null);
            }
            finally
            {
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void ExecutableInspector_ShowsValidationErrors()
        {
            var registry = new TestExecutionRegistry();
            DialogueExecutionRegistry.Register(registry);
            var database = CreateDatabase("ExecutableValidation");
            var dialogue = database.Npcs[0].Dialogues[0];
            var functionNode = new FunctionNodeData
            {
                Title = "Function",
                FunctionId = "session.set_int",
                Arguments = new System.Collections.Generic.List<DialogueArgumentEntry>
                {
                    new() { Name = "value", Value = DialogueArgumentValue.FromString("wrong") }
                }
            };
            dialogue.Graph.Nodes.Add(functionNode);
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, functionNode.Id), Is.True);

                Assert.That(HasChoiceDiagnostic(window, "Required argument 'key' is missing."), Is.True);
                Assert.That(HasChoiceDiagnostic(window, "Argument 'value' expects Int but is String."), Is.True);
            }
            finally
            {
                DialogueExecutionRegistry.Unregister(registry);
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        [Test]
        public void SceneInspector_DefaultKnownSceneWritesSceneKey()
        {
            var registry = new TestExecutionRegistry();
            DialogueExecutionRegistry.Register(registry);
            var database = CreateDatabase("SceneDefaultSelection");
            var dialogue = database.Npcs[0].Dialogues[0];
            var sceneNode = new SceneNodeData
            {
                Title = "Scene",
                Position = new Vector2(360f, 120f)
            };
            dialogue.Graph.Nodes.Add(sceneNode);
            var window = ScriptableObject.CreateInstance<DialogueEditorWindow>();

            try
            {
                window.InitializeForTests(database);
                Assert.That(window.FocusDialogueNode(dialogue, sceneNode.Id), Is.True);

                Assert.That(sceneNode.SceneKey, Is.EqualTo("Battle_Arena"));
                Assert.That(window.rootVisualElement.Q<PopupField<string>>("scene-descriptor-field")?.value, Is.EqualTo("Combat: Battle Arena"));
                Assert.That(window.rootVisualElement.Q<TextField>("scene-key-field")?.value, Is.EqualTo("Battle_Arena"));
            }
            finally
            {
                DialogueExecutionRegistry.Unregister(registry);
                DialogueEditorAutosaveStore.ClearSnapshot(DialogueEditorAutosaveStore.GetStorageKey(database));
                window.Close();
            }
        }

        private static DialogueDatabaseAsset CreateDatabase(string prefix)
        {
            var database = ScriptableObject.CreateInstance<DialogueDatabaseAsset>();
            database.name = $"{prefix}-{Guid.NewGuid():N}";

            var npc = new NpcEntry
            {
                Name = "NPC"
            };

            var dialogue = new DialogueEntry
            {
                Name = "Dialogue"
            };

            dialogue.Graph.Nodes.Add(new DialogueTextNodeData
            {
                Title = "Original Title",
                BodyText = "Original body",
                IsStartNode = true,
                Position = new Vector2(120f, 120f)
            });

            npc.Dialogues.Add(dialogue);
            database.Npcs.Add(npc);
            return database;
        }

        private static DialogueDatabaseAsset CreateDatabaseWithLinks(string prefix)
        {
            var database = CreateDatabase(prefix);
            var dialogue = database.Npcs[0].Dialogues[0];
            var startNode = (DialogueTextNodeData)dialogue.Graph.Nodes[0];
            var leftNode = new DialogueTextNodeData
            {
                Title = "Left",
                Position = new Vector2(360f, 120f)
            };
            var rightNode = new DialogueTextNodeData
            {
                Title = "Right",
                Position = new Vector2(360f, 320f)
            };

            dialogue.Graph.Nodes.Add(leftNode);
            dialogue.Graph.Nodes.Add(rightNode);
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = startNode.Id,
                ToNodeId = leftNode.Id,
                Order = 0,
                ChoiceText = "Go Left"
            });
            dialogue.Graph.Links.Add(new NodeLinkData
            {
                FromNodeId = startNode.Id,
                ToNodeId = rightNode.Id,
                Order = 1,
                ChoiceText = "Go Right"
            });

            return database;
        }

        private static bool HasIdentifierIssue(DialogueEditorWindow window, string issueText)
        {
            return window.rootVisualElement.Query<Label>(className: "dialogue-editor__id-issue")
                .ToList()
                .Any(label => label.text == issueText);
        }

        private static bool HasChoiceDiagnostic(DialogueEditorWindow window, string diagnosticText)
        {
            return window.rootVisualElement.Query<Label>(className: "dialogue-editor__choice-diagnostic")
                .ToList()
                .Any(label => label.text == diagnosticText);
        }

        private static bool HasLabel(DialogueEditorWindow window, string text)
        {
            return HasLabel(window.rootVisualElement, text);
        }

        private static bool HasLabel(VisualElement root, string text)
        {
            return root.Query<Label>()
                .ToList()
                .Any(label => label.text == text);
        }

        private static bool HasButton(VisualElement root, string text)
        {
            return root.Query<Button>()
                .ToList()
                .Any(button => button.text == text);
        }

        private static string GetPaletteShortcutText(DialogueEditorWindow window, string itemName)
        {
            return window.rootVisualElement.Q<Label>($"palette-shortcut-{itemName}")?.text;
        }

        private static string GetRichTextPreviewText(VisualElement element)
        {
            return string.Concat(element.Query<Label>(className: "dialogue-rich-text-run")
                .ToList()
                .Select(label => label.text.Replace('\u00A0', ' ')));
        }

        private static void Click(VisualElement button)
        {
            Click(button, 1);
        }

        private static void DoubleClick(VisualElement button)
        {
            Click(button, 2);
        }

        private static void Click(VisualElement button, int clickCount)
        {
            var mouseDownEvent = new Event { type = EventType.MouseDown, button = 0 };
            using (var mouseDown = MouseDownEvent.GetPooled(mouseDownEvent))
            {
                mouseDown.target = button;
                mouseDown.clickCount = clickCount;
                button.SendEvent(mouseDown);
            }

            var mouseUpEvent = new Event { type = EventType.MouseUp, button = 0 };
            using (var mouseUp = MouseUpEvent.GetPooled(mouseUpEvent))
            {
                mouseUp.target = button;
                button.SendEvent(mouseUp);
            }
        }

        private static void SendKeyDown(VisualElement target, KeyCode keyCode, EventModifiers modifiers = EventModifiers.None)
        {
            var keyEvent = new Event
            {
                type = EventType.KeyDown,
                keyCode = keyCode,
                modifiers = modifiers
            };
            using (var keyDown = KeyDownEvent.GetPooled(keyEvent))
            {
                keyDown.target = target;
                target.SendEvent(keyDown);
            }
        }

        private sealed class TestConditionMetadataProvider : IDialogueConditionMetadataProvider
        {
            public System.Collections.Generic.IEnumerable<DialogueConditionKeySuggestion> GetKeySuggestions(ConditionType type)
            {
                if (type == ConditionType.VariableCheck)
                {
                    yield return new DialogueConditionKeySuggestion("door_open", "Door Open", "Flags");
                }
            }
        }

        private sealed class TestExecutionRegistry : IDialogueExecutionRegistry
        {
            public System.Collections.Generic.IEnumerable<DialogueFunctionDescriptor> GetFunctions()
            {
                yield return new DialogueFunctionDescriptor(
                    "session.set_int",
                    "Set Int",
                    "Session",
                    parameters: new[]
                    {
                        new DialogueParameterDescriptor("key", DialogueArgumentType.String, required: true),
                        new DialogueParameterDescriptor("value", DialogueArgumentType.Int, required: true)
                    });
            }

            public System.Collections.Generic.IEnumerable<DialogueSceneDescriptor> GetScenes()
            {
                yield return new DialogueSceneDescriptor("Battle_Arena", "Battle Arena", "Combat");
            }
        }
    }
}

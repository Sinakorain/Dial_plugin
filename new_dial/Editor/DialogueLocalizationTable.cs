// Copyright (c) 2026 Danil Kashulin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NewDial.DialogueEditor
{
    internal sealed class DialogueLocalizationTableLanguage
    {
        public DialogueLocalizationTableLanguage(string header, string code, int columnIndex)
        {
            Header = header ?? string.Empty;
            Code = code ?? string.Empty;
            ColumnIndex = columnIndex;
        }

        public string Header { get; }
        public string Code { get; }
        public int ColumnIndex { get; }
    }

    internal sealed class DialogueLocalizationTableRow
    {
        public string Key { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public int EntryIndex { get; set; }
        public Dictionary<string, string> TextByLanguage { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool TryGetText(string languageCode, out string text)
        {
            return TextByLanguage.TryGetValue(DialogueTextLocalizationUtility.NormalizeLanguageCode(languageCode), out text) &&
                   !string.IsNullOrEmpty(text);
        }
    }

    internal sealed class DialogueLocalizationConversation
    {
        public DialogueLocalizationConversation(string id, IEnumerable<DialogueLocalizationTableRow> rows)
        {
            Id = id ?? string.Empty;
            Rows = (rows ?? Enumerable.Empty<DialogueLocalizationTableRow>())
                .OrderBy(row => row.EntryIndex)
                .ThenBy(row => row.Key)
                .ToList();
        }

        public string Id { get; }
        public IReadOnlyList<DialogueLocalizationTableRow> Rows { get; }
    }

    internal sealed class DialogueLocalizationTable
    {
        public List<DialogueLocalizationTableLanguage> Languages { get; } = new();
        public List<DialogueLocalizationTableRow> Rows { get; } = new();
        public List<string> SkippedRows { get; } = new();

        public IReadOnlyList<DialogueLocalizationConversation> GetConversations()
        {
            return Rows
                .GroupBy(row => row.ConversationId)
                .Select(group => new DialogueLocalizationConversation(group.Key, group))
                .OrderBy(group => group.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public DialogueLocalizationConversation GetConversation(string conversationId)
        {
            return GetConversations().FirstOrDefault(conversation =>
                string.Equals(conversation.Id, conversationId, StringComparison.OrdinalIgnoreCase));
        }
    }

    internal static class DialogueLocalizationTableParser
    {
        private static readonly Regex DialogueKeyRegex = new(
            "^Conversation/(.+)/Entry/([0-9]+)/Dialogue Text$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, string> LanguageHeaderMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Russian"] = "ru",
            ["English"] = "en",
            ["Spanish"] = "es",
            ["Spanish (Latin Americas)"] = "es-419",
            ["German"] = "de",
            ["French"] = "fr",
            ["Italian"] = "it"
        };

        private static readonly Dictionary<string, string> HeaderByLanguageCode = LanguageHeaderMap
            .GroupBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.OrdinalIgnoreCase);

        public static DialogueLocalizationTable Parse(string sourceText)
        {
            var table = new DialogueLocalizationTable();
            var records = ParseRecords(sourceText ?? string.Empty, DetectDelimiter(sourceText ?? string.Empty));
            var headerIndex = FindHeader(records, table.Languages);
            if (headerIndex < 0)
            {
                table.SkippedRows.Add("Header row with Keys and language columns was not found.");
                return table;
            }

            for (var index = headerIndex + 1; index < records.Count; index++)
            {
                var record = records[index];
                if (record.Count == 0)
                {
                    continue;
                }

                var key = CleanCell(GetCell(record, 0));
                if (string.IsNullOrWhiteSpace(key) || key.StartsWith("*", StringComparison.Ordinal))
                {
                    continue;
                }

                var match = DialogueKeyRegex.Match(key);
                if (!match.Success)
                {
                    table.SkippedRows.Add($"Row {index + 1}: unsupported key '{key}'.");
                    continue;
                }

                var row = new DialogueLocalizationTableRow
                {
                    Key = key,
                    ConversationId = match.Groups[1].Value,
                    EntryIndex = int.Parse(match.Groups[2].Value)
                };

                foreach (var language in table.Languages)
                {
                    var text = GetCell(record, language.ColumnIndex);
                    if (IsMissingTranslation(text))
                    {
                        continue;
                    }

                    row.TextByLanguage[language.Code] = text;
                }

                table.Rows.Add(row);
            }

            return table;
        }

        public static string ToTsv(DialogueLocalizationConversation conversation)
        {
            return ToTsv(conversation?.Rows ?? Array.Empty<DialogueLocalizationTableRow>());
        }

        public static string ToTsv(IEnumerable<DialogueLocalizationTableRow> rows)
        {
            var builder = new StringBuilder();
            var materializedRows = (rows ?? Enumerable.Empty<DialogueLocalizationTableRow>()).ToList();
            var languages = GetLanguagesForRows(materializedRows);
            AppendRecord(builder, new[] { "Keys" }.Concat(languages.Select(language => language.Header)));
            foreach (var row in materializedRows)
            {
                var values = new List<string> { row?.Key ?? string.Empty };
                foreach (var language in languages)
                {
                    values.Add(row != null && row.TextByLanguage.TryGetValue(language.Code, out var text) ? text : string.Empty);
                }

                AppendRecord(builder, values);
            }

            return builder.ToString();
        }

        public static string GetHeaderForLanguageCode(string languageCode)
        {
            var normalized = DialogueTextLocalizationUtility.NormalizeLanguageCode(languageCode);
            return HeaderByLanguageCode.TryGetValue(normalized, out var header) ? header : normalized;
        }

        public static bool IsMissingTranslation(string value)
        {
            return string.IsNullOrWhiteSpace(value) ||
                   string.Equals(value.Trim(), "Loading...", StringComparison.OrdinalIgnoreCase);
        }

        private static int FindHeader(IReadOnlyList<List<string>> records, ICollection<DialogueLocalizationTableLanguage> languages)
        {
            for (var rowIndex = 0; rowIndex < records.Count; rowIndex++)
            {
                var record = records[rowIndex];
                if (!string.Equals(CleanCell(GetCell(record, 0)), "Keys", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                for (var columnIndex = 1; columnIndex < record.Count; columnIndex++)
                {
                    var header = CleanCell(GetCell(record, columnIndex));
                    if (LanguageHeaderMap.TryGetValue(header, out var code))
                    {
                        languages.Add(new DialogueLocalizationTableLanguage(header, code, columnIndex));
                    }
                }

                return languages.Count > 0 ? rowIndex : -1;
            }

            return -1;
        }

        private static char DetectDelimiter(string sourceText)
        {
            var firstLineEnd = sourceText.IndexOfAny(new[] { '\r', '\n' });
            var firstLine = firstLineEnd >= 0 ? sourceText.Substring(0, firstLineEnd) : sourceText;
            return firstLine.Count(character => character == '\t') >= firstLine.Count(character => character == ',') ? '\t' : ',';
        }

        private static List<List<string>> ParseRecords(string sourceText, char delimiter)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;

            for (var index = 0; index < sourceText.Length; index++)
            {
                var character = sourceText[index];
                if (character == '"')
                {
                    if (inQuotes && index + 1 < sourceText.Length && sourceText[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (!inQuotes && character == delimiter)
                {
                    record.Add(field.ToString());
                    field.Clear();
                    continue;
                }

                if (!inQuotes && (character == '\r' || character == '\n'))
                {
                    record.Add(field.ToString());
                    field.Clear();
                    records.Add(record);
                    record = new List<string>();

                    if (character == '\r' && index + 1 < sourceText.Length && sourceText[index + 1] == '\n')
                    {
                        index++;
                    }

                    continue;
                }

                field.Append(character);
            }

            if (field.Length > 0 || record.Count > 0)
            {
                record.Add(field.ToString());
                records.Add(record);
            }

            return records;
        }

        private static string GetCell(IReadOnlyList<string> record, int index)
        {
            return record != null && index >= 0 && index < record.Count ? record[index] ?? string.Empty : string.Empty;
        }

        private static IReadOnlyList<DialogueLocalizationTableLanguage> GetLanguagesForRows(IReadOnlyList<DialogueLocalizationTableRow> rows)
        {
            var codes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows ?? Array.Empty<DialogueLocalizationTableRow>())
            {
                if (row == null)
                {
                    continue;
                }

                foreach (var code in row.TextByLanguage.Keys)
                {
                    var normalized = DialogueTextLocalizationUtility.NormalizeLanguageCode(code);
                    if (!string.IsNullOrWhiteSpace(normalized))
                    {
                        codes.Add(normalized);
                    }
                }
            }

            if (codes.Count == 0)
            {
                codes.Add(DialogueTextLocalizationUtility.DefaultLanguageCode);
            }

            var orderedCodes = codes
                .OrderBy(code => code == DialogueTextLocalizationUtility.DefaultLanguageCode ? 0 : 1)
                .ThenBy(code => code, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return orderedCodes
                .Select((code, index) => new DialogueLocalizationTableLanguage(GetHeaderForLanguageCode(code), code, index + 1))
                .ToList();
        }

        private static string CleanCell(string value)
        {
            return (value ?? string.Empty).Trim().TrimStart('\uFEFF');
        }

        private static void AppendRecord(StringBuilder builder, IEnumerable<string> values)
        {
            builder.AppendLine(string.Join("\t", values.Select(EscapeTsvValue)));
        }

        private static string EscapeTsvValue(string value)
        {
            value ??= string.Empty;
            return value.IndexOfAny(new[] { '\t', '\r', '\n', '"' }) < 0
                ? value
                : $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }

    internal sealed class DialogueLocalizationImportReport
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Missing { get; set; }
        public int Skipped { get; set; }
        public List<string> Messages { get; } = new();
    }

    internal sealed class DialogueLocalizationBatchImportReport
    {
        public int ConversationsImported { get; set; }
        public int DialoguesCreated { get; set; }
        public int DialoguesUpdated { get; set; }
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Missing { get; set; }
        public int Skipped { get; set; }
        public List<string> Messages { get; } = new();
    }

    internal sealed class DialogueLocalizationExportReport
    {
        public int Exported { get; set; }
        public int SkippedMissingKey { get; set; }
        public List<string> Messages { get; } = new();
    }

    internal static class DialogueLocalizationImportUtility
    {
        private const float NodeSpacingY = 220f;

        public static DialogueLocalizationBatchImportReport ApplyConversationsToDatabase(
            DialogueDatabaseAsset database,
            NpcEntry preferredNpc,
            IEnumerable<DialogueLocalizationConversation> conversations,
            out NpcEntry selectedNpc,
            out DialogueEntry selectedDialogue)
        {
            selectedNpc = null;
            selectedDialogue = null;

            var report = new DialogueLocalizationBatchImportReport();
            var materializedConversations = (conversations ?? Enumerable.Empty<DialogueLocalizationConversation>())
                .Where(conversation => conversation != null)
                .ToList();

            if (database == null || materializedConversations.Count == 0)
            {
                report.Skipped++;
                report.Messages.Add("Missing database or conversations.");
                return report;
            }

            database.Npcs ??= new List<NpcEntry>();
            var targetNpcForNewDialogues = EnsureTargetNpc(database, preferredNpc);

            foreach (var conversation in materializedConversations)
            {
                var targetDialogue = FindDialogueById(database, conversation.Id, out var ownerNpc);
                if (targetDialogue == null)
                {
                    ownerNpc = targetNpcForNewDialogues;
                    targetDialogue = CreateDialogue(ownerNpc, conversation.Id);
                    report.DialoguesCreated++;
                }
                else
                {
                    report.DialoguesUpdated++;
                }

                var importReport = ApplyConversationToDialogue(targetDialogue, conversation);
                report.ConversationsImported++;
                report.Created += importReport.Created;
                report.Updated += importReport.Updated;
                report.Missing += importReport.Missing;
                report.Skipped += importReport.Skipped;
                report.Messages.AddRange(importReport.Messages);

                selectedNpc = ownerNpc;
                selectedDialogue = targetDialogue;
            }

            return report;
        }

        public static DialogueLocalizationImportReport ApplyConversationToDialogue(
            DialogueEntry dialogue,
            DialogueLocalizationConversation conversation)
        {
            var report = new DialogueLocalizationImportReport();
            if (dialogue == null || conversation == null)
            {
                report.Skipped++;
                report.Messages.Add("Missing dialogue or conversation.");
                return report;
            }

            dialogue.Graph ??= new DialogueGraphData();
            dialogue.Graph.Nodes ??= new List<BaseNodeData>();
            dialogue.Graph.Links ??= new List<NodeLinkData>();

            if (dialogue.Graph.Nodes.Count == 0)
            {
                CreateLinearNodes(dialogue.Graph, conversation, report);
                return report;
            }

            var lineNodesByKey = dialogue.Graph.Nodes
                .Where(IsLocalizableLineNode)
                .Where(node => !string.IsNullOrWhiteSpace(GetLocalizationKey(node)))
                .GroupBy(GetLocalizationKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var row in conversation.Rows)
            {
                if (!lineNodesByKey.TryGetValue(row.Key, out var node))
                {
                    report.Missing++;
                    report.Messages.Add($"Missing node for {row.Key}");
                    continue;
                }

                ApplyRowToNode(node, row);
                report.Updated++;
            }

            return report;
        }

        public static DialogueLocalizationExportReport ExportDialogue(
            DialogueEntry dialogue,
            string conversationIdOrPrefix,
            out string tsv)
        {
            var report = new DialogueLocalizationExportReport();
            var rows = new List<DialogueLocalizationTableRow>();
            var lineNodes = dialogue?.Graph?.Nodes?.Where(IsLocalizableLineNode).ToList() ?? new List<BaseNodeData>();
            var generatedEntryIndex = 1;

            foreach (var node in lineNodes)
            {
                var key = GetLocalizationKey(node);
                if (string.IsNullOrWhiteSpace(key))
                {
                    if (string.IsNullOrWhiteSpace(conversationIdOrPrefix))
                    {
                        report.SkippedMissingKey++;
                        report.Messages.Add($"Skipped '{node.Title}' because LocalizationKey is empty.");
                        continue;
                    }

                    key = BuildLocalizationKey(conversationIdOrPrefix, generatedEntryIndex++);
                }

                rows.Add(CreateRowFromNode(node, key));
                report.Exported++;
            }

            tsv = DialogueLocalizationTableParser.ToTsv(rows);
            return report;
        }

        private static void CreateLinearNodes(
            DialogueGraphData graph,
            DialogueLocalizationConversation conversation,
            DialogueLocalizationImportReport report)
        {
            DialogueTextNodeData previous = null;
            var orderedRows = conversation.Rows.OrderBy(row => row.EntryIndex).ToList();
            for (var index = 0; index < orderedRows.Count; index++)
            {
                var row = orderedRows[index];
                var node = new DialogueTextNodeData
                {
                    Title = $"Entry {row.EntryIndex}",
                    LocalizationKey = row.Key,
                    IsStartNode = index == 0,
                    Position = new Vector2(0f, index * NodeSpacingY)
                };
                ApplyRowToNode(node, row);
                graph.Nodes.Add(node);

                if (previous != null)
                {
                    graph.Links.Add(new NodeLinkData
                    {
                        FromNodeId = previous.Id,
                        ToNodeId = node.Id,
                        Order = 0
                    });
                }

                previous = node;
                report.Created++;
            }
        }

        private static void ApplyRowToNode(BaseNodeData node, DialogueLocalizationTableRow row)
        {
            SetLocalizationKey(node, row.Key);
            var localizedBodyText = row.TextByLanguage
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new DialogueLocalizedTextEntry
                {
                    LanguageCode = DialogueTextLocalizationUtility.NormalizeLanguageCode(pair.Key),
                    Text = pair.Value ?? string.Empty
                })
                .ToList();
            SetLocalizedBodyText(node, localizedBodyText);

            if (row.TryGetText(DialogueTextLocalizationUtility.DefaultLanguageCode, out var defaultText))
            {
                DialogueTextLocalizationUtility.SetBodyText(node, DialogueTextLocalizationUtility.DefaultLanguageCode, defaultText);
            }
            else if (string.IsNullOrEmpty(DialogueTextLocalizationUtility.GetBodyText(node, DialogueTextLocalizationUtility.DefaultLanguageCode)))
            {
                DialogueTextLocalizationUtility.SetBodyText(
                    node,
                    DialogueTextLocalizationUtility.DefaultLanguageCode,
                    row.TextByLanguage.Values.FirstOrDefault(value => !string.IsNullOrEmpty(value)) ?? string.Empty);
            }
        }

        private static DialogueLocalizationTableRow CreateRowFromNode(BaseNodeData node, string key)
        {
            var row = new DialogueLocalizationTableRow
            {
                Key = key,
                ConversationId = TryParseConversationId(key, out var conversationId) ? conversationId : string.Empty,
                EntryIndex = TryParseEntryIndex(key, out var entryIndex) ? entryIndex : 0
            };

            foreach (var entry in GetLocalizedBodyText(node) ?? Enumerable.Empty<DialogueLocalizedTextEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.LanguageCode) || string.IsNullOrEmpty(entry.Text))
                {
                    continue;
                }

                row.TextByLanguage[DialogueTextLocalizationUtility.NormalizeLanguageCode(entry.LanguageCode)] = entry.Text;
            }

            if (!row.TextByLanguage.ContainsKey(DialogueTextLocalizationUtility.DefaultLanguageCode) &&
                !string.IsNullOrEmpty(DialogueTextLocalizationUtility.GetBodyText(node, DialogueTextLocalizationUtility.DefaultLanguageCode)))
            {
                row.TextByLanguage[DialogueTextLocalizationUtility.DefaultLanguageCode] =
                    DialogueTextLocalizationUtility.GetBodyText(node, DialogueTextLocalizationUtility.DefaultLanguageCode);
            }

            return row;
        }

        private static bool IsLocalizableLineNode(BaseNodeData node)
        {
            return node is DialogueTextNodeData or DialogueChoiceNodeData;
        }

        private static string GetLocalizationKey(BaseNodeData node)
        {
            return node switch
            {
                DialogueTextNodeData textNode => textNode.LocalizationKey,
                DialogueChoiceNodeData choiceNode => choiceNode.LocalizationKey,
                _ => string.Empty
            };
        }

        private static void SetLocalizationKey(BaseNodeData node, string key)
        {
            switch (node)
            {
                case DialogueTextNodeData textNode:
                    textNode.LocalizationKey = key ?? string.Empty;
                    break;
                case DialogueChoiceNodeData choiceNode:
                    choiceNode.LocalizationKey = key ?? string.Empty;
                    break;
            }
        }

        private static List<DialogueLocalizedTextEntry> GetLocalizedBodyText(BaseNodeData node)
        {
            return node switch
            {
                DialogueTextNodeData textNode => textNode.LocalizedBodyText,
                DialogueChoiceNodeData choiceNode => choiceNode.LocalizedBodyText,
                _ => null
            };
        }

        private static void SetLocalizedBodyText(BaseNodeData node, List<DialogueLocalizedTextEntry> entries)
        {
            switch (node)
            {
                case DialogueTextNodeData textNode:
                    textNode.LocalizedBodyText = entries ?? new List<DialogueLocalizedTextEntry>();
                    break;
                case DialogueChoiceNodeData choiceNode:
                    choiceNode.LocalizedBodyText = entries ?? new List<DialogueLocalizedTextEntry>();
                    break;
            }
        }

        private static string BuildLocalizationKey(string conversationIdOrPrefix, int entryIndex)
        {
            var prefix = (conversationIdOrPrefix ?? string.Empty).Trim().TrimEnd('/');
            if (!prefix.StartsWith("Conversation/", StringComparison.OrdinalIgnoreCase))
            {
                prefix = $"Conversation/{prefix}";
            }

            return $"{prefix}/Entry/{entryIndex}/Dialogue Text";
        }

        private static NpcEntry EnsureTargetNpc(DialogueDatabaseAsset database, NpcEntry preferredNpc)
        {
            database.Npcs ??= new List<NpcEntry>();
            if (preferredNpc != null && database.Npcs.Contains(preferredNpc))
            {
                preferredNpc.Dialogues ??= new List<DialogueEntry>();
                return preferredNpc;
            }

            var npc = database.Npcs.FirstOrDefault(npc => npc != null);
            if (npc != null)
            {
                npc.Dialogues ??= new List<DialogueEntry>();
                return npc;
            }

            npc = new NpcEntry { Name = "Imported NPC" };
            database.Npcs.Add(npc);
            return npc;
        }

        private static DialogueEntry CreateDialogue(NpcEntry npc, string conversationId)
        {
            npc.Dialogues ??= new List<DialogueEntry>();
            var dialogue = new DialogueEntry
            {
                Id = conversationId,
                Name = conversationId
            };
            npc.Dialogues.Add(dialogue);
            return dialogue;
        }

        private static DialogueEntry FindDialogueById(DialogueDatabaseAsset database, string dialogueId, out NpcEntry ownerNpc)
        {
            ownerNpc = null;
            if (database?.Npcs == null || string.IsNullOrWhiteSpace(dialogueId))
            {
                return null;
            }

            foreach (var npc in database.Npcs.Where(npc => npc != null))
            {
                var dialogue = npc.Dialogues?.FirstOrDefault(candidate =>
                    candidate != null &&
                    string.Equals(candidate.Id, dialogueId, StringComparison.OrdinalIgnoreCase));
                if (dialogue == null)
                {
                    continue;
                }

                ownerNpc = npc;
                return dialogue;
            }

            return null;
        }

        private static bool TryParseConversationId(string key, out string conversationId)
        {
            conversationId = string.Empty;
            var match = Regex.Match(key ?? string.Empty, "^Conversation/(.+)/Entry/[0-9]+/Dialogue Text$");
            if (!match.Success)
            {
                return false;
            }

            conversationId = match.Groups[1].Value;
            return true;
        }

        private static bool TryParseEntryIndex(string key, out int entryIndex)
        {
            entryIndex = 0;
            var match = Regex.Match(key ?? string.Empty, "^Conversation/.+/Entry/([0-9]+)/Dialogue Text$");
            return match.Success && int.TryParse(match.Groups[1].Value, out entryIndex);
        }
    }
}

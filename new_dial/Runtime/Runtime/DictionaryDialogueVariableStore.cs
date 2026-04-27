// Copyright (c) 2026 Danil Kashulin. All rights reserved.

using System.Collections.Generic;

namespace NewDial.DialogueEditor
{
    public class DictionaryDialogueVariableStore : IDialogueVariableStore
    {
        private readonly IDictionary<string, string> _values;

        public DictionaryDialogueVariableStore(IDictionary<string, string> values)
        {
            _values = values ?? new Dictionary<string, string>();
        }

        public bool TryGetValue(string key, out string value)
        {
            return _values.TryGetValue(key, out value);
        }
    }
}

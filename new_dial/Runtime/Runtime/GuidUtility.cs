// Copyright (c) 2026 Danil Kashulin. All rights reserved.

using System;

namespace NewDial.DialogueEditor
{
    public static class GuidUtility
    {
        public static string NewGuid()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}

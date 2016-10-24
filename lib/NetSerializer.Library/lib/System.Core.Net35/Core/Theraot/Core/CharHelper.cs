﻿//


namespace Theraot.Core
{
    public static class CharHelper
    {
        private static readonly string classicWhitespace =
            "\u0009" + // Tab
            "\u000A" + // NewLine
            "\u000B" + // Vertical Tab
            "\u000C" + // Form Feed
            "\u000D" + // Carriage return
            "\u0020";  // Space

        public static bool IsClassicWhitespace(char character)
        {
            return classicWhitespace.IndexOf(character) >= 0;
        }
    }
}
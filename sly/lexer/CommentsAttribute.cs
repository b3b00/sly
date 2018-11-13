﻿using System;

namespace sly.lexer
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class CommentAttribute : Attribute
    {
        public string MultiLineCommentEnd;

        public string MultiLineCommentStart;

        public string SingleLineCommentStart;

        public CommentAttribute(string singleLineStart, string MultiLineStart, string multiLineEnd)
        {
            SingleLineCommentStart = singleLineStart;
            MultiLineCommentStart = MultiLineStart;
            MultiLineCommentEnd = multiLineEnd;
        }
    }
}
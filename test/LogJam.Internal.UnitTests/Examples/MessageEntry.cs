﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MessageEntry.cs">
// Copyright (c) 2011-2015 https://github.com/logjam2.  
// </copyright>
// Licensed under the <a href="https://github.com/logjam2/logjam/blob/master/LICENSE.txt">Apache License, Version 2.0</a>;
// you may not use this file except in compliance with the License.
// --------------------------------------------------------------------------------------------------------------------


namespace LogJam.Internal.UnitTests.Examples
{
    using System;
    using System.IO;

    using LogJam.Format;


    /// <summary>
    /// A simple, logentry struct.
    /// </summary>
    [DefaultFormatter(typeof(MessageEntryFormatter))]
    public struct MessageEntry : ILogEntry
    {

        public readonly DateTime Timestamp;
        public readonly int? MessageId;
        public readonly string Text;

        public MessageEntry(string text)
        {
            Timestamp = DateTime.UtcNow;
            MessageId = null;
            Text = text;
        }

        public MessageEntry(int messageId, string text)
        {
            Timestamp = DateTime.UtcNow;
            MessageId = messageId;
            Text = text;
        }

        public override string ToString()
        {
            if (MessageId.HasValue)
            {
                return string.Format("{0:HH:mm:ss.fff}  [{1}] {2}", Timestamp, MessageId.Value, Text);
            }
            else
            {
                return string.Format("{0:HH:mm:ss.fff}  [?] {1}", Timestamp, Text);
            }
        }


        /// <summary>
        /// Default <see cref="EntryFormatter{TEntry}" /> for <see cref="MessageEntry" />.
        /// </summary>
        public class MessageEntryFormatter : EntryFormatter<MessageEntry>
        {

            public override void Format(ref MessageEntry entry, TextWriter textWriter)
            {
                textWriter.WriteLine(entry.ToString());
            }

        }

    }

}

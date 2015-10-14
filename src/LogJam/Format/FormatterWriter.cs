﻿// // --------------------------------------------------------------------------------------------------------------------
// <copyright file="FormatterWriter.cs">
// Copyright (c) 2011-2015 https://github.com/logjam2.  
// </copyright>
// Licensed under the <a href="https://github.com/logjam2/logjam/blob/master/LICENSE.txt">Apache License, Version 2.0</a>;
// you may not use this file except in compliance with the License.
// --------------------------------------------------------------------------------------------------------------------


namespace LogJam.Format
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Text;
    using System.Threading;

    using LogJam.Trace;
    using LogJam.Util.Text;


    /// <summary>
    /// Base class for writing formatted log output to a text target.  A <c>FormatterWriter</c> is primarily used by one or more
    /// <see cref="EntryFormatter{TEntry}"/>s to write formatted text.  <c>FormatterWriter</c> is the primary abstraction for writing
    /// to a text target.
    /// <para>
    /// Text targets can be colorized and are generally optimized for readability.  In contrast, binary targets are generally optimized for
    /// efficient and precise writing and parsing.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <c>FormatterWriter</c> is <u>not</u> threadsafe.  It assumes that writes are synchronized at a higher level (typically using pluggable
    /// <see cref="ISynchronizingLogWriter"/>s), so that the last entry is completely formatted/written before the next entry starts. 
    /// <see cref="BeginEntry"/> and <see cref="EndEntry"/> provide basic checks for this assertion.
    /// </remarks>
    public abstract class FormatterWriter : IDisposable
    {
        public const string DefaultFieldDelimiter = "  ";


        #region Fields

        /// <summary>
        /// TimeZone to use for formatting dates and times.
        /// </summary>
        private TimeZoneInfo _outputTimeZone = TimeZoneInfo.Local;

        /// <summary>
        /// SetupLog <see cref="Tracer"/>.
        /// </summary>
        private readonly Tracer _setupTracer;

        /// <summary>
        /// Delimiter between fields.
        /// </summary>
        private readonly string _fieldDelimiter;

        /// <summary>
        /// A text buffer that can be used by any formatting methods.
        /// </summary>
        protected readonly StringBuilder buffer;

        /// <summary>
        /// Set to <c>true</c> when text writing is positioned at the beginning of a line.
        /// </summary>
        protected bool atBeginningOfLine;

        /// <summary>
        /// The number of started and not completed entries.  Should be 0 or 1 at all times (this is a class invariant).
        /// </summary>
        private int _startedEntries;

        #endregion


        protected FormatterWriter(ITracerFactory setupTracerFactory, string fieldDelimiter = DefaultFieldDelimiter, int spacesPerIndentLevel = 4)
        {
            Contract.Requires<ArgumentNullException>(setupTracerFactory != null);
            Contract.Requires<ArgumentNullException>(fieldDelimiter != null);

            _setupTracer = setupTracerFactory.TracerFor(this);
            _fieldDelimiter = fieldDelimiter;
            SpacesPerIndent = spacesPerIndentLevel;
            buffer = new StringBuilder(255);
            _startedEntries = 0;
        }

        public abstract void Dispose();

        /// <summary>
        /// The delimiter used to separate fields - eg single space, 2 spaces (default value), tab, comma.
        /// </summary>
        public string FieldDelimiter { get { return _fieldDelimiter; } }

        /// <summary>
        /// Derived classes must return the line delimiter for the log target, eg "\r\n".
        /// </summary>
        public abstract string LineDelimiter { get; }

        /// <summary>
        /// When this <c>IsColorEnabled</c> returns <c>true</c>, <see cref="EntryFormatter{TEntry}"/>s
        /// should determine and pass in <see cref="ColorCategory"/> values for all text.  When
        /// <c>IsColorEnabled</c> returns <c>false</c>, any <see cref="ColorCategory"/> values
        /// are ignored.
        /// </summary>
        public abstract bool IsColorEnabled { get; }

        /// <summary>
        /// <c>true</c> to include the Date when formatting log entrys.
        /// </summary>
        public bool IncludeDate { get; set; }

        /// <summary>
        /// <c>true</c> to include the Timestamp when formatting log entrys.
        /// </summary>
        public bool IncludeTimestamp { get; set; }

        /// <summary>
        /// The number of spaces for each indent level.
        /// </summary>
        public int SpacesPerIndent { get; private set; }

        /// <summary>
        /// The current indent level.  May be increased or decreased as needed by the formatter.
        /// </summary>
        public int IndentLevel { get; set; }

        /// <summary>
        /// Specifies the TimeZone to use when formatting the Timestamp for a log entry.
        /// </summary>
        public TimeZoneInfo OutputTimeZone
        {
            get { return _outputTimeZone; }
            set
            {
                Contract.Requires<ArgumentNullException>(value != null);

                _outputTimeZone = value;
            }
        }

        /// <summary>
        /// Marks the start of a new entry.
        /// </summary>
        public virtual void BeginEntry(int indentLevel)
        {
            if (Interlocked.Increment(ref _startedEntries) != 1)
            {
                _setupTracer.Error("FormatterWriter invariant violated: Only one entry must be written at a time.");
            }
            IndentLevel = indentLevel;
        }

        public abstract void WriteField(string text, ColorCategory colorCategory = ColorCategory.None, int padWidth = 0);

        public abstract void WriteLines(string lines, ColorCategory colorCategory = ColorCategory.None, int relativeIndentLevel = 1);

        protected virtual void WriteLinePrefix(int indentLevel)
        {
            WriteText(LineDelimiter, ColorCategory.None);
            atBeginningOfLine = false;
        }

        protected abstract void WriteText(string s, ColorCategory colorCategory);

        protected abstract void WriteField(StringBuilder sb, ColorCategory colorCategory);

        protected abstract void WriteLine(StringBuilder sb, ColorCategory colorCategory, int relativeIndentLevel = 1);

        protected virtual void WriteEndLine()
        {
            WriteText(LineDelimiter, ColorCategory.None);
            atBeginningOfLine = true;
        }

        /// <summary>
        /// Ends an entry.
        /// </summary>
        public virtual void EndEntry()
        {
            if (! atBeginningOfLine)
            {
                WriteEndLine();
            }
            Interlocked.Decrement(ref _startedEntries);
        }

        public abstract void Flush();

        public virtual void WriteDate(DateTime dateTimeUtc, ColorCategory colorCategory = ColorCategory.Detail)
        {
            DateTime outputDateTime = TimeZoneInfo.ConvertTimeFromUtc(dateTimeUtc, _outputTimeZone);

            // Format date
            buffer.Clear();
            buffer.AppendPadZeroes(outputDateTime.Year, 4);
            buffer.Append('/');
            buffer.AppendPadZeroes(outputDateTime.Month, 2);
            buffer.Append('/');
            buffer.AppendPadZeroes(outputDateTime.Day, 2);

            WriteField(buffer, colorCategory);
        }

        public virtual void WriteTimestamp(DateTime timestampUtc, ColorCategory colorCategory = ColorCategory.Detail)
        {
            DateTime outputTimestamp = TimeZoneInfo.ConvertTimeFromUtc(timestampUtc, _outputTimeZone);

            // Format time
            buffer.Clear();
            buffer.AppendPadZeroes(outputTimestamp.Hour, 2);
            buffer.Append(':');
            buffer.AppendPadZeroes(outputTimestamp.Minute, 2);
            buffer.Append(':');
            buffer.AppendPadZeroes(outputTimestamp.Second, 2);
            buffer.Append('.');
            buffer.AppendPadZeroes(outputTimestamp.Millisecond, 3);

            WriteField(buffer, colorCategory);
        }

        public virtual void WriteAbbreviatedTypeName(string typeName, ColorCategory colorCategory = ColorCategory.Detail, int padWidth = 0)
        {
            Contract.Requires<ArgumentNullException>(typeName != null);

            // Count the dots
            int countDots = 0;
            for (int i = typeName.IndexOf('.'); i >= 0; i = typeName.IndexOf('.', i + 1))
            {
                countDots++;
            }
            if (countDots == 0)
            {
                WriteField(typeName, colorCategory, padWidth);
                return;
            }

            // Walk the string, abbreviating until just over half the segments are abbreviated
            int segmentsToAbbreviate = (countDots >> 1) + 1;
            buffer.Clear();
            int len = typeName.Length;
            buffer.Append(AsciiToLower(typeName[0])); // Always include the first char
            for (int i = 1, segmentsAbbreviated = 0; i < len; ++i)
            {
                char ch = typeName[i];
                if (ch == '.')
                {
                    buffer.Append(ch);
                    i++;
                    if (++segmentsAbbreviated >= segmentsToAbbreviate)
                    {   // No more abbreviating - take the rest straight
                        buffer.Append(typeName, i, len - i);
                        break;
                    }
                    else
                    {   // Keep abbreviating - always include the first char after a '.'
                        if (i < len)
                        {
                            buffer.Append(AsciiToLower(typeName[i]));
                        }
                    }
                }
                else if (! AsciiIsLower(ch))
                {
                    buffer.Append(AsciiToLower(ch));
                }
                // Else ch is lower case - omit it 
            }

            // Add padding if needed
            int spacesPadding = padWidth - buffer.Length;
            if (spacesPadding > 0)
            {
                buffer.Append(' ', spacesPadding);
            }

            WriteField(buffer, colorCategory);
        }

        protected bool AsciiIsLower(char ch)
        {
            return (ch >= 97) && (ch <= 122);
        }

        protected char AsciiToLower(char ch)
        {
            if (65 <= ch && ch <= 90)
                ch |= ' ';
            return ch;
        }

    }

}
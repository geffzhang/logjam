﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TextLogWriter.cs">
// Copyright (c) 2011-2015 https://github.com/logjam2.  
// </copyright>
// Licensed under the <a href="https://github.com/logjam2/logjam/blob/master/LICENSE.txt">Apache License, Version 2.0</a>;
// you may not use this file except in compliance with the License.
// --------------------------------------------------------------------------------------------------------------------


namespace LogJam.Writer
{
    using System;
    using System.Diagnostics.Contracts;

    using LogJam.Format;
    using LogJam.Trace;
    using LogJam.Util;


    /// <summary>
    /// A <see cref="ILogWriter"/>s that writes log entries to a target that receives formatted text.
    /// Text targets can be colorized and are generally optimized for readability.  In contrast, binary targets are 
    /// generally optimized for efficient and precise writing and parsing.
    /// </summary>
    public class TextLogWriter : BaseLogWriter
    {

        private readonly FormatterWriter _formatterWriter;

        /// <summary>
        /// Creates a new <see cref="TextLogWriter" />.
        /// </summary>
        /// <param name="setupTracerFactory">The <see cref="ITracerFactory" /> to use for logging setup operations.</param>
        public TextLogWriter(ITracerFactory setupTracerFactory, FormatterWriter formatterWriter)
            : base(setupTracerFactory)
        {
            Contract.Requires<ArgumentNullException>(setupTracerFactory != null);
            Contract.Requires<ArgumentNullException>(formatterWriter != null);

            _formatterWriter = formatterWriter;
        }

        /// <summary>
        /// Returns <c>true</c> when this logwriter and its entrywriters are ready to log.
        /// </summary>
        public virtual bool IsEnabled { get { return IsStarted; } }

        /// <summary>
        /// Adds the specified <typeparamref name="TEntry" /> to this <see cref="TextWriterLogWriter" />, using
        /// <paramref name="entryFormatter" /> to
        /// format entries of type <c>TEntry</c>.
        /// </summary>
        /// <typeparam name="TEntry"></typeparam>
        /// <param name="entryFormatter"></param>
        /// <returns>this, for chaining calls in fluent style.</returns>
        public TextLogWriter AddFormat<TEntry>(EntryFormatter<TEntry> entryFormatter = null)
            where TEntry : ILogEntry
        {
            if (entryFormatter == null)
            {   // Try creating the default entry formatter
                entryFormatter = DefaultFormatterAttribute.GetDefaultFormatterFor<TEntry>();
                if (entryFormatter == null)
                {
                    throw new ArgumentNullException(nameof(entryFormatter),
                                                    $"No [DefaultFormatter] attribute could be found for entry type {typeof(TEntry).FullName}, so {nameof(entryFormatter)} argument must be set.");
                }
            }

            AddEntryWriter(new InnerEntryWriter<TEntry>(this, entryFormatter));
            return this;
        }

        /// <summary>
        /// Adds the specified <typeparamref name="TEntry" /> to this <see cref="TextWriterLogWriter" />, using
        /// <paramref name="formatAction" /> to
        /// format entries of type <c>TEntry</c>.
        /// </summary>
        /// <typeparam name="TEntry"></typeparam>
        /// <param name="formatAction"></param>
        /// <returns>this, for chaining calls in fluent style.</returns>
        public TextLogWriter AddFormat<TEntry>(FormatAction<TEntry> formatAction)
            where TEntry : ILogEntry
        {
            Contract.Requires<ArgumentNullException>(formatAction != null);

            return AddFormat((EntryFormatter<TEntry>) formatAction);
        }

        protected override void InternalStart()
        {
            (_formatterWriter as IStartable).SafeStart(SetupTracerFactory);

            base.InternalStart();
        }

        protected override void InternalStop()
        {
            base.InternalStop();

            (_formatterWriter as IStartable).SafeStop(SetupTracerFactory);
        }

        protected override void Dispose(bool disposing)
        {
            _formatterWriter.SafeDispose(SetupTracerFactory);
        }

        /// <summary>
        /// Central method to format and write <paramref name="entry"/> to the <see cref="FormatterWriter"/>.
        /// </summary>
        /// <typeparam name="TEntry">The log entry type</typeparam>
        /// <param name="entry">The log entry</param>
        /// <param name="entryFormatter">The <see cref="EntryFormatter{TEntry}"/></param>
        protected virtual void WriteFormattedEntry<TEntry>(ref TEntry entry, EntryFormatter<TEntry> entryFormatter)
            where TEntry : ILogEntry
        {
            if (IsStarted)
            {
                entryFormatter.Format(ref entry, _formatterWriter);
            }
        }

        /// <summary>
        /// Provides log writing to the <see cref="TextLogWriter" /> for entry type <typeparamref name="TEntry" />.
        /// </summary>
        /// <typeparam name="TEntry"></typeparam>
        internal class InnerEntryWriter<TEntry> : IEntryWriter<TEntry>
            where TEntry : ILogEntry
        {

            private readonly TextLogWriter _parent;
            private readonly EntryFormatter<TEntry> _formatter;

            public InnerEntryWriter(TextLogWriter parent, EntryFormatter<TEntry> entryFormatter)
            {
                _parent = parent;
                _formatter = entryFormatter;
            }

            public void Write(ref TEntry entry)
            {
                _parent.WriteFormattedEntry(ref entry, _formatter);              
            }

            public bool IsEnabled { get { return _parent.IsEnabled; } }

            internal EntryFormatter<TEntry> Formatter { get { return _formatter; } }
            internal TextLogWriter Parent { get { return _parent; } }

        }

    }

}

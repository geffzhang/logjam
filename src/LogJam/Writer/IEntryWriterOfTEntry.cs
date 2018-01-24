﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IEntryWriterOfTEntry.cs">
// Copyright (c) 2011-2015 https://github.com/logjam2.  
// </copyright>
// Licensed under the <a href="https://github.com/logjam2/logjam/blob/master/LICENSE.txt">Apache License, Version 2.0</a>;
// you may not use this file except in compliance with the License.
// --------------------------------------------------------------------------------------------------------------------


namespace LogJam.Writer
{
	using System;
	using System.Diagnostics.Contracts;


	/// <summary>
	/// Supports writing strongly-typed log entries to a log target. An <see cref="ILogWriter"/> contains one or more
	/// <c>IEntryWriter&lt;TEntry&gt;</c> instances, each are uniquely identified by their <c>TEntry</c> type.
	/// </summary>
	/// <typeparam name="TEntry">The base entry type supported by the entry writer.</typeparam>
	/// <seealso cref="IEntryWriter"/>.
	[ContractClass(typeof(EntryWriterContract<>))]
	public interface IEntryWriter<TEntry> : IEntryWriter
		where TEntry : ILogEntry
	{

		/// <summary>
		/// Writes <paramref name="entry"/> to the log target.
		/// </summary>
		/// <param name="entry">The log entry to write.</param>
		void Write(ref TEntry entry);

	}


	[ContractClassFor(typeof(IEntryWriter<>))]
	internal abstract class EntryWriterContract<TEntry> : IEntryWriter<TEntry>
		where TEntry : ILogEntry
	{

		public bool IsEnabled { get { throw new NotImplementedException(); } }
		public Type LogEntryType { get { throw new NotImplementedException(); } }

		public void Write(ref TEntry entry)
		{
			throw new NotImplementedException();
		}

	}
}

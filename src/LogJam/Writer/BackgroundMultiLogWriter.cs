﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="BackgroundMultiLogWriter.cs">
// Copyright (c) 2011-2016 https://github.com/logjam2. 
// </copyright>
// Licensed under the <a href="https://github.com/logjam2/logjam/blob/master/LICENSE.txt">Apache License, Version 2.0</a>;
// you may not use this file except in compliance with the License.
// --------------------------------------------------------------------------------------------------------------------


namespace LogJam.Writer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Threading;

    using LogJam.Config;
    using LogJam.Config.Initializer;
    using LogJam.Internal;
    using LogJam.Trace;
    using LogJam.Util;


    /// <summary>
    /// Provides multiple synchronized <see cref="IEntryWriter{TEntry}" />s and <see cref="ILogWriter" />s that write to
    /// proxied <see cref="IEntryWriter{TEntry}" />s on a single background thread.
    /// This implementation minimizes the performance impact of writing to logs by allowing clients to "send and forget",
    /// until the queue is full. In normal cases all logged entries are guaranteed to be written to the background log
    /// writers, however abnormal termination of an application can result in queued entries not being written.
    /// </summary>
    internal sealed class BackgroundMultiLogWriter : Startable, IDisposable, ILogJamComponent
    {

        /// <summary>
        /// The default max length for the blocking queue for log writers.
        /// </summary>
        public const int DefaultMaxQueueLength = 512;

        private readonly ITracerFactory _setupTracerFactory;
        private readonly Tracer _tracer;

        // The set of log writers that have been proxied
        private readonly List<LogWriterProxy> _proxyLogWriters;
        // Queued actions that take precedence over _backgroundActionQueue.
        private readonly ConcurrentQueue<Action> _priorityActionQueue;
        // Queued actions to invoke on the background thread.
        private readonly ConcurrentQueue<Action> _backgroundActionQueue;

        private BackgroundThread _backgroundThread;

        internal BackgroundMultiLogWriter(ITracerFactory setupTracerFactory)
        {
            Contract.Requires<ArgumentNullException>(setupTracerFactory != null);

            _setupTracerFactory = setupTracerFactory;
            _tracer = setupTracerFactory.TracerFor(this);

            _proxyLogWriters = new List<LogWriterProxy>();
            _priorityActionQueue = new ConcurrentQueue<Action>();
            _backgroundActionQueue = new ConcurrentQueue<Action>();

            _backgroundThread = null;
        }

        internal BackgroundMultiLogWriter(ITracerFactory setupTracerFactory, params ILogWriter[] logWriters)
            : this(setupTracerFactory)
        {
            Contract.Requires<ArgumentNullException>(setupTracerFactory != null);
            Contract.Requires<ArgumentNullException>(logWriters != null);

            foreach (ILogWriter logWriter in logWriters)
            {
                CreateProxyFor(logWriter);
            }
        }

        public ITracerFactory SetupTracerFactory
        {
            get { return _setupTracerFactory; }
        }

        public IEnumerable<ILogWriter> ProxyLogWriters
        {
            get { return _proxyLogWriters; }
        }

        /// <summary>
        /// Finalizer, used to ensure that queued logs get flushed during finalization.
        /// </summary>
        ~BackgroundMultiLogWriter()
        {
            if (! IsDisposed)
            {
                _tracer.Error("In finalizer (~BackgroundMultiLogWriter) - forgot to Dispose()?");
                Dispose(false);
            }
        }

        public ILogWriter CreateProxyFor(ILogWriter innerLogWriter, int maxQueueLength = DefaultMaxQueueLength)
        {
            Contract.Requires<ArgumentNullException>(innerLogWriter != null);
            Contract.Requires<ArgumentException>(maxQueueLength > 0);

            lock (this)
            {
                EnsureNotDisposed();
                OperationNotSupportedAfterStarting("CreateProxyFor(ILogWriter)");
                // TODO: It would be nice to support modifications after starting...

                var logWriter = new LogWriterProxy(innerLogWriter, _priorityActionQueue, _backgroundActionQueue, _setupTracerFactory, maxQueueLength);
                _proxyLogWriters.Add(logWriter);

                return logWriter;
            }
        }

        //#region ILogWriter

        //public bool TryGetEntryWriter<TEntry>(out IEntryWriter<TEntry> entryWriter) where TEntry : ILogEntry
        //{
        //	lock (this)
        //	{
        //		var logWriters = new List<IEntryWriter<TEntry>>();
        //		_proxyEntryWriters.GetEntryWriters(logWriters);
        //		if (logWriters.Count == 1)
        //		{
        //			entryWriter = logWriters[0];
        //			return true;
        //		}
        //		else if (logWriters.Count == 0)
        //		{
        //			entryWriter = null;
        //			return false;
        //		}
        //		else
        //		{
        //			entryWriter = new FanOutEntryWriter<TEntry>(logWriters);
        //			return true;
        //		}
        //	}
        //}

        ///// <summary>
        ///// Returns <c>true</c> if this object is ready to receive log writes.
        ///// </summary>
        ///// <remarks>IsEnabled is synonymous with <see cref="IsStarted"/> for this class.</remarks>
        //public bool IsEnabled { get { return (_backgroundThread != null) && _backgroundThread.IsStarted; } }

        //public bool IsSynchronized { get { return true; } }

        //public IEnumerator<ILogWriter> GetEnumerator()
        //{
        //	return _proxyEntryWriters.GetEnumerator();
        //}

        //IEnumerator IEnumerable.GetEnumerator()
        //{
        //	return GetEnumerator();
        //}

        //#endregion

        #region IStartable

        protected override void InternalStart()
        {
            if (_backgroundThread == null)
            {
                Interlocked.CompareExchange(ref _backgroundThread, new BackgroundThread(_setupTracerFactory, _priorityActionQueue, _backgroundActionQueue), null);
            }

            _backgroundThread.SafeStart(_setupTracerFactory);
            _proxyLogWriters.SafeStart(_setupTracerFactory);
        }

        protected override void InternalStop()
        {
            var backgroundThread = _backgroundThread;
            if (backgroundThread != null)
            {
                _proxyLogWriters.SafeStop(_setupTracerFactory);
                backgroundThread.Stop();
            }
        }

        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            lock (this)
            {
                if (IsDisposed)
                {
                    return;
                }

                State = StartableState.Disposing;
            }

            try
            {
                var backgroundThread = _backgroundThread;

                if (backgroundThread != null)
                {
                    _proxyLogWriters.SafeStop(_setupTracerFactory);
                    _proxyLogWriters.SafeDispose(_setupTracerFactory);

                    backgroundThread.Stop();
                    //_backgroundThread = null;
                }
                State = StartableState.Disposed;
            }
            catch
            {
                State = StartableState.FailedToStop;
                throw;
            }
        }

        private void EnsureNotDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(this.ToString());
            }
        }

        private void OperationNotSupportedAfterStarting(string method)
        {
            var state = State;
            if ((state == StartableState.Starting) || (state == StartableState.Started))
            {
                throw new LogJamException(string.Format("{0} not supported when instance is starting or started.", method), this);
            }
        }

        /// <summary>
        /// Used only for test verification.
        /// </summary>
        internal bool IsBackgroundThreadRunning
        {
            get
            {
                var backgroundThread = _backgroundThread;
                return backgroundThread != null && backgroundThread.IsThreadRunning;
            }
        }


        /// <summary>
        /// Standard initializer to create a <see cref="BackgroundMultiLogWriter"/> if <see cref="ILogWriterConfig.BackgroundLogging"/> is <c>true</c>.
        /// </summary>
        /// <remarks>
        /// This initializer is included in <see cref="LogManagerConfig.Initializers"/> by default.
        /// </remarks>
        public sealed class Initializer : IExtendLogWriterPipeline
        {

            public ILogWriter InitializeLogWriter(ITracerFactory setupTracerFactory, ILogWriter logWriter, DependencyDictionary dependencyDictionary)
            {
                var logWriterConfig = dependencyDictionary.Get<ILogWriterConfig>();
                if (logWriterConfig.BackgroundLogging)
                {
                    // Add a background logging proxy
                    var logManager = dependencyDictionary.Get<LogManager>();

                    var backgroundMultiLogWriter = new BackgroundMultiLogWriter(setupTracerFactory);
                    logManager.AddBackgroundMultiLogWriter(backgroundMultiLogWriter);
                    var backgroundLogWriter = backgroundMultiLogWriter.CreateProxyFor(logWriter);
                    setupTracerFactory.TracerFor(this).Verbose("Adding background logging proxy in front of {0}", logWriter);

                    // Add an ISynchronizingLogWriter to the DependencyDictionary
                    // Since a single queue/background thread is used, it's fine that a single ISynchronizingLogWriter is shared for the whole BackgroundMultiLogWriter.
                    dependencyDictionary.AddIfNotDefined(typeof(ISynchronizingLogWriter), backgroundLogWriter);

                    return backgroundLogWriter;
                }
                else
                {
                    return logWriter;
                }
            }

        }


        /// <summary>
        /// The set of operations that are executed on the background thread.  All these methods must be valid <see cref="Action"/>s.
        /// </summary>
        private interface IBackgroundThreadLogWriterActions
        {

            void StartInnerWriter();

            void DequeAndWriteEntry();

            void StopInnerWriter();

            void DisposeInnerWriter();

        }


        /// <summary>
        /// A proxy <see cref="ILogWriter" /> that can be accessed in the foreground thread, but that queues Start() and
        /// Stop() and <see cref="ISynchronizingLogWriter.QueueSynchronized"/> operations to the background thread.
        /// </summary>
        private class LogWriterProxy : BaseLogWriter, ISynchronizingLogWriter
        {

            // The ILogWriter that is accessed only on the background thread
            private readonly ILogWriter _innerLogWriter;
            // References parent._priorityActionQueue
            private readonly ConcurrentQueue<Action> _priorityActionQueue;
            // References parent._backgroundActionQueue
            private readonly ConcurrentQueue<Action> _backgroundActionQueue;
            private readonly ITracerFactory _setupTracerFactory;
            private readonly SemaphoreSlim _slotsLeftInQueue;


            internal LogWriterProxy(ILogWriter innerLogWriter,
                                    ConcurrentQueue<Action> priorityActionQueue,
                                    ConcurrentQueue<Action> backgroundActionQueue,
                                    ITracerFactory setupTracerFactory,
                                    int maxQueueLength)
                : base(setupTracerFactory)
            {
                Contract.Requires<ArgumentNullException>(innerLogWriter != null);
                Contract.Requires<ArgumentNullException>(priorityActionQueue != null);
                Contract.Requires<ArgumentNullException>(backgroundActionQueue != null);
                Contract.Requires<ArgumentNullException>(setupTracerFactory != null);
                Contract.Requires<ArgumentException>(maxQueueLength > 0);

                _innerLogWriter = innerLogWriter;
                _priorityActionQueue = priorityActionQueue;
                _backgroundActionQueue = backgroundActionQueue;
                _setupTracerFactory = setupTracerFactory;
                _slotsLeftInQueue = new SemaphoreSlim(maxQueueLength);
            }

            internal ILogWriter InnerLogWriter
            {
                get { return _innerLogWriter; }
            }

            public override bool IsSynchronized
            {
                get { return true; }
            }

            public void QueueSynchronized(Action action, LogWriterActionPriority priority)
            {
                switch (priority)
                {
                    case LogWriterActionPriority.Delay:
                        // Queueing the action on the ThreadPool causes a delay
                        ThreadPool.QueueUserWorkItem(state =>
                                                     {
                                                         _backgroundActionQueue.Enqueue(action);
                                                     });
                        break;

                    case LogWriterActionPriority.Normal:
                        // Run action in normal queue order
                        _backgroundActionQueue.Enqueue(action);
                        break;

                    case LogWriterActionPriority.High:
                        // Run action before the next normally queued actions
                        _priorityActionQueue.Enqueue(action);
                        break;

                    default:
                        throw new ArgumentException("Priority " + priority + " is not an acceptable value.");
                }
            }

            private void QueueBackgroundAction(Action backgroundAction)
            {
                _backgroundActionQueue.Enqueue(backgroundAction);
            }

            private BlockingQueueEntryWriter<TEntry> CreateBlockingQueueEntryWriter<TEntry>(IEntryWriter<TEntry> innerEntryWriter)
                where TEntry : ILogEntry
            {
                var proxyEntryWriter = new BlockingQueueEntryWriter<TEntry>(innerEntryWriter, _backgroundActionQueue, _slotsLeftInQueue, _setupTracerFactory);
                return proxyEntryWriter;
            }

            protected override void InternalStart()
            {
                // Start the _innerLogWriter in the current thread; that way its EntryWriters are available to create proxies immediately.
                (_innerLogWriter as IStartable).SafeStart(SetupTracerFactory);

                // Add EntryWriters to proxy the inner EntryWriters
                foreach (var kvp in _innerLogWriter.EntryWriters)
                {
                    Type entryWriterEntryType = kvp.Key;
                    object innerEntryWriter = kvp.Value;
                    if (! EntryWriters.Any(existingKvp => existingKvp.Key == entryWriterEntryType))
                    {
                        // Create + add a new EntryWriter for the entry type
                        var entryTypeArgs = new Type[] { entryWriterEntryType };
                        object blockingQueueEntryWriter = this.InvokeGenericMethod(entryTypeArgs, "CreateBlockingQueueEntryWriter", innerEntryWriter);
                        this.InvokeGenericMethod(entryTypeArgs, "AddEntryWriter", blockingQueueEntryWriter);
                    }
                }

                base.InternalStart();
            }

            protected override void InternalStop()
            {
                base.InternalStop();

                IStartable startableLogWriter = _innerLogWriter as IStartable;
                if (startableLogWriter != null)
                {
                    QueueBackgroundAction(() => startableLogWriter.SafeStop(SetupTracerFactory));
                }
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);

                IDisposable disposableLogWriter = _innerLogWriter as IDisposable;
                if (disposableLogWriter != null)
                {
                    QueueBackgroundAction(() => disposableLogWriter.SafeDispose(SetupTracerFactory));
                }
            }

        }


        /// <summary>
        /// A proxy <see cref="IEntryWriter{TEntry}" /> implementation that queues elements so they can be written on a background
        /// thread.
        /// </summary>
        /// <typeparam name="TEntry"></typeparam>
        private class BlockingQueueEntryWriter<TEntry> : Startable, IQueueEntryWriter<TEntry>, IBackgroundThreadLogWriterActions, IDisposable
            where TEntry : ILogEntry
        {

            // Set to true when this is started.
            private bool _isEnabled;

            private readonly IEntryWriter<TEntry> _innerEntryWriter;
            private readonly ConcurrentQueue<TEntry> _queue;
            private readonly SemaphoreSlim _slotsLeftInQueue;

            private readonly ConcurrentQueue<Action> _backgroundActionQueue;
            private readonly ITracerFactory _setupTracerFactory;

            internal BlockingQueueEntryWriter(IEntryWriter<TEntry> innerEntryWriter,
                                              ConcurrentQueue<Action> backgroundActionQueue,
                                              SemaphoreSlim slotsLeftInQueue,
                                              ITracerFactory setupTracerFactory)
            {
                Contract.Requires<ArgumentNullException>(innerEntryWriter != null);
                Contract.Requires<ArgumentNullException>(backgroundActionQueue != null);
                Contract.Requires<ArgumentNullException>(slotsLeftInQueue != null);
                Contract.Requires<ArgumentNullException>(setupTracerFactory != null);

                _innerEntryWriter = innerEntryWriter;
                _queue = new ConcurrentQueue<TEntry>();
                _slotsLeftInQueue = slotsLeftInQueue;

                _backgroundActionQueue = backgroundActionQueue;
                _setupTracerFactory = setupTracerFactory;

                _isEnabled = _innerEntryWriter.IsEnabled;
                State = _isEnabled ? StartableState.Started : StartableState.Unstarted;
            }

            private void QueueBackgroundAction(Action backgroundAction)
            {
                _backgroundActionQueue.Enqueue(backgroundAction);
            }

            public void Write(ref TEntry entry)
            {
                if (! IsEnabled)
                {
                    return;
                }

                // Blocks if maxQueueLength is exceeded
                _slotsLeftInQueue.Wait();

                // TODO: Evaluate writing my own lockless ConcurrentQueue implementation, which could be slightly more performant for 
                // reads and writes by using ref parameters for value types, and a contiguous block of entries.
                _queue.Enqueue(entry);

                QueueBackgroundAction(DequeAndWriteEntry);
            }

            public bool IsEnabled => _isEnabled;

            public Type LogEntryType => typeof(TEntry);

            public bool IsEmpty => _queue.IsEmpty;

            public bool TryDequeue(out TEntry logEntry)
            {
                bool success = _queue.TryDequeue(out logEntry);
                if (success)
                {
                    _slotsLeftInQueue.Release();
                }
                return success;
            }

            protected override void InternalStart()
            {
                if (_innerEntryWriter is IStartable)
                {
                    QueueBackgroundAction(StartInnerWriter);
                }

                // The QueueEntryWriter is enabled as the start signal is sent;
                // In the case of the QueueEntryWriter, "IsEnabled" means "ready to accept entries", even the the background
                // logwriter has not started yet.
                // If we don't mark it as enabled right away, callers will see EntryWriter.IsEnabled = false,
                // which will turn away new entries.
                _isEnabled = true;
            }

            protected override void InternalStop()
            {
                _isEnabled = false;
                if (_innerEntryWriter is IStartable)
                {
                    // Blocks if maxQueueLength is exceeded
                    _slotsLeftInQueue.Wait();

                    QueueBackgroundAction(StopInnerWriter);
                }

                // Wait for stop to finish on the background thread
                var waitEvent = new ManualResetEventSlim();
                QueueBackgroundAction(() =>
                                      {
                                          waitEvent.Set();
                                      });
                waitEvent.Wait(1000); // REVIEW: Add support for cancellation token to the BackgroundMultiLogWriter?
            }

            public void Dispose()
            {
                lock (this)
                {
                    if (IsDisposed)
                    {
                        return;
                    }
                    State = StartableState.Disposing;
                }

                _isEnabled = false;
                if (_innerEntryWriter is IStartable)
                {
                    // Blocks if maxQueueLength is exceeded
                    _slotsLeftInQueue.Wait();

                    QueueBackgroundAction(StopInnerWriter);
                }

                if (_innerEntryWriter is IDisposable)
                {
                    // Blocks if maxQueueLength is exceeded
                    _slotsLeftInQueue.Wait();

                    QueueBackgroundAction(DisposeInnerWriter);
                }

                // No need to wait for dispose to finish on the background thread:
                // This class is private and is controlled by BackgroundMultiLogWriter.Dispose(), which doesn't return
                // until the thread exits.
                // Plus, if this class is called in the finalizer, there's no guarantee the thread hasn't already exited.
                State = StartableState.Disposed;
            }

            #region IBackgroundThreadLogWriterActions

            public void StartInnerWriter()
            {
                (_innerEntryWriter as IStartable).SafeStart(_setupTracerFactory);
            }

            public void DequeAndWriteEntry()
            {
                TEntry logEntry;
                bool success = TryDequeue(out logEntry);
                if (success)
                {
                    _innerEntryWriter.Write(ref logEntry);
                }
            }

            public void StopInnerWriter()
            {
                (_innerEntryWriter as IStartable).SafeStop(_setupTracerFactory);
                _slotsLeftInQueue.Release();
            }

            public void DisposeInnerWriter()
            {
                (_innerEntryWriter as IDisposable).SafeDispose(_setupTracerFactory);
                _slotsLeftInQueue.Release();
            }

            #endregion
        }


        /// <summary>
        /// Encapsulates the background thread for a <see cref="BackgroundMultiLogWriter" /> instance.
        /// </summary>
        internal sealed class BackgroundThread : IStartable
        {

            private readonly Tracer _tracer;
            // Queued actions that take precedence over _backgroundActionQueue.
            private readonly ConcurrentQueue<Action> _priorityActionQueue;
            // Queued actions to invoke on the background thread.
            private readonly ConcurrentQueue<Action> _backgroundActionQueue;
            private volatile StartableState _startableState;
            private Thread _thread;

            // REVIEW: Important that this object + ThreadProc has NO reference to the parent BackgroundMultiLogWriter.
            // If there were a reference from this, it would never finalize.

            public BackgroundThread(ITracerFactory setupTracerFactory, ConcurrentQueue<Action> priorityActionQueue, ConcurrentQueue<Action> backgroundActionQueue)
            {
                Contract.Requires<ArgumentNullException>(setupTracerFactory != null);
                Contract.Requires<ArgumentNullException>(priorityActionQueue != null);
                Contract.Requires<ArgumentNullException>(backgroundActionQueue != null);

                _tracer = setupTracerFactory.TracerFor(this);
                _priorityActionQueue = priorityActionQueue;
                _backgroundActionQueue = backgroundActionQueue;
                _startableState = StartableState.Unstarted;
            }

            #region IStartable

            public StartableState State
            {
                get { return _startableState; }
            }

            /// @inheritdoc
            [Obsolete("Obsoleted in IStartable")]
            public bool IsStarted
            {
                get { return _startableState == StartableState.Started; }
            }

            /// @inheritdoc
            public bool IsReadyToStart
            {
                get
                {
                    var state = _startableState;
                    return ((state == StartableState.Unstarted) || (state == StartableState.Stopped));
                }
            }

            public void Start()
            {
                lock (this)
                {
                    if (! IsReadyToStart)
                    {
                        return;
                    }

                    Debug.Assert(! IsThreadRunning);

                    _startableState = StartableState.Starting;
                    _thread = new Thread(BackgroundThreadProc)
                              {
                                  Name = "BackgroundMultiLogWriter.BackgroundThread",
                                  IsBackground = true
                              };
                    _thread.Start();
                }
            }

            public void Stop()
            {
                lock (this)
                {
                    var thread = _thread;
                    if (thread != null)
                    {
                        _startableState = StartableState.Stopping;
                        thread.Join();
                    }

                    _startableState = StartableState.Stopped;
                }
            }

            #endregion

            internal bool IsThreadRunning
            {
                get
                {
                    var thread = _thread;
                    if (thread == null)
                    {
                        return false;
                    }
                    else if (! thread.IsAlive)
                    {
                        _thread = null;
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }

            /// <summary>
            /// ThreadProc for the background thread. At any one time, there should be 0 or 1 background threads for each
            /// <see cref="BackgroundMultiLogWriter" />
            /// instance.
            /// </summary>
            private void BackgroundThreadProc()
            {
                _startableState = StartableState.Started;
                _tracer.Info("Started background thread.");

                SpinWait spinWait = new SpinWait();
                while (true)
                {
                    Action action;
                    if (_priorityActionQueue.TryDequeue(out action))
                    {
                        try
                        {
                            action();
                        }
                        catch (Exception excp)
                        {
                            _tracer.Error(excp, "Exception caught in background thread while executing priority Action.");
                        }

                        spinWait.Reset();
                    }
                    else if (_backgroundActionQueue.TryDequeue(out action))
                    {
                        try
                        {
                            action();
                        }
                        catch (Exception excp)
                        {
                            _tracer.Error(excp, "Exception caught in background thread.");
                        }

                        spinWait.Reset();
                    }
                    else if (spinWait.NextSpinWillYield && _startableState == StartableState.Stopping)
                    {
                        // No queued actions, and logwriter is stopping: Time to exit the background thread
                        break;
                    }
                    else
                    {
                        spinWait.SpinOnce();
                    }
                }

                _tracer.Info("Exiting background thread.");
                _startableState = StartableState.Stopped;
                _thread = null;
            }

        }

    }

}

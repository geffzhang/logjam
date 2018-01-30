﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="BaseLogJamManager.cs">
// Copyright (c) 2011-2016 https://github.com/logjam2. 
// </copyright>
// Licensed under the <a href="https://github.com/logjam2/logjam/blob/master/LICENSE.txt">Apache License, Version 2.0</a>;
// you may not use this file except in compliance with the License.
// --------------------------------------------------------------------------------------------------------------------


namespace LogJam
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using LogJam.Internal;
    using LogJam.Trace;
    using LogJam.Util;


    /// <summary>
    /// Common LogJam *Manager functionality.
    /// </summary>
    public abstract class BaseLogJamManager : IStartable, IDisposable
    {
        #region Instance fields

        private StartableState _startableState = StartableState.Unstarted;
        private Exception _startException;

        private readonly List<WeakReference> _disposeOnStop = new List<WeakReference>();
        private readonly List<IDisposable> _linkedDisposables = new List<IDisposable>();

        #endregion

        /// <summary>
        /// A special <see cref="ITracerFactory" /> that returns <see cref="Tracer" />s for managing configuration,
        /// setup, shutdown, and exceptions during logging.
        /// </summary>
        public abstract ITracerFactory SetupTracerFactory { get; }

        /// <summary>
        /// Returns the collection of <see cref="TraceEntry" />s logged through <see cref="SetupTracerFactory" />.
        /// </summary>
        public abstract IEnumerable<TraceEntry> SetupLog { get; }

        /// <summary>
        /// Returns <c>true</c> if <see cref="Stop"/> or <c>Dispose</c> has been called.
        /// </summary>
        public bool IsStopped { get { return (_startableState == StartableState.Stopped) || (_startableState == StartableState.Disposed); } }

        /// <summary>
        /// Returns <c>true</c> if there have been no setup traces that are more severe than informational.
        /// </summary>
        /// <remarks>
        /// This method will return <c>false</c> if any warn, error, or severe traces have been reported during configuration, startup, or shutdown.
        /// </remarks>
        public bool IsHealthy { get { return ! SetupLog.HasAnyExceeding(TraceLevel.Info); } }

        /// <summary>
        /// Returns the <see cref="StartableState"/> for this object.
        /// </summary>
        public StartableState State
        {
            get => _startableState;
            private set
            {
                if (value != _startableState)
                {
                    StateChanging?.Invoke(this, new StartableStateChangingEventArgs(_startableState, value));
                    _startableState = value;
                }
            }
        }

        /// <summary>
        /// An event that is rasied when <see cref="State"/> changes.
        /// </summary>
        public event EventHandler<StartableStateChangingEventArgs> StateChanging;

        /// <summary>
        /// Returns <c>true</c> if this object is in a state that can be <see cref="Start"/>ed.
        /// </summary>
        public bool IsReadyToStart
        {
            get
            {
                var state = _startableState;
                return (state == StartableState.Unstarted) || (state == StartableState.Stopped) || (state == StartableState.Started);
            }
        }

        /// <summary>
        /// Ensures that <see cref="Start" /> is automatically called once, but <see cref="Start" /> is not automatically called
        /// again if an exception occurred during the initial start, or if <see cref="Stop" /> was called.
        /// </summary>
        public void EnsureAutoStarted()
        {
            if (_startableState == StartableState.Unstarted)
            {
                try
                {
                    Start();
                }
                catch (Exception exception)
                {
                    SetupTracerFactory.TracerFor(this).Error(exception, "AutoStart failed: Exception occurred.");
                }
            }
        }

        /// <summary>
        /// Starts the manager whether or not <c>Start()</c> has already been called.
        /// </summary>
        /// <remarks>To avoid starting more than once, and avoid exceptions, use <see cref="EnsureAutoStarted" />.
        /// Note that <see cref="EnsureAutoStarted"/> is called automatically in many cases.
        /// <para>
        /// Exceptions thrown from <see cref="Start"/> are abnormal. Individual subcomponents can fail to start,
        /// but the manager start will still succeed. Subcomponent start failures are reported in the <see cref="SetupLog"/>.
        /// </para></remarks>
        /// <exception cref="ObjectDisposedException">If this object was previously disposed.</exception>
        /// <exception cref="LogJamStartException">If an exception occurred while starting.</exception>
        public void Start()
        {
            var tracer = SetupTracerFactory.TracerFor(this);

            // The lock block includes all the start logic, so that other threads calling Start()
            // won't enter it until the first thread completes Start()ing.
            lock (this)
            {
                var state = State;
                if (state >= StartableState.Disposing)
                {
                    throw new ObjectDisposedException(this + " cannot be started; it has been Dispose()ed.");
                }
                if (! IsReadyToStart)
                {
                    throw new LogJamStartException(this + " cannot be started; state is: " + _startableState, this);
                }

                try
                {
                    if (StartableState.Started == State)
                    {
                        tracer.Debug("Restarting " + this + "...");
                        State = StartableState.Restarting;
                    }
                    else
                    {
                        tracer.Debug("Starting " + this + "...");
                        State = StartableState.Starting;
                    }

                    InternalStart();
                    State = StartableState.Started;
                    _startException = null;
                    tracer.Info(this + " started.");
                }
                catch (Exception startException)
                {
                    _startException = startException;
                    tracer.Severe(startException, "Start failed: Exception occurred.");
                    State = StartableState.FailedToStart;
                    if (startException is LogJamStartException)
                    {
                        throw;
                    }
                    else
                    {
                        throw new LogJamStartException("Start failed", startException, this);
                    }
                }
            }
        }

        /// <summary>
        /// Stops this instance; disposes all registered disposables.
        /// </summary>
        public void Stop()
        {
            lock (this)
            {
                var state = State;
                if ((state >= StartableState.Stopping) || (state == StartableState.Unstarted))
                {
                    return;
                }

                State = StartableState.Stopping;
            }

            var tracer = SetupTracerFactory.TracerFor(this);
            tracer.Info("Stopping " + this + "...");

            try
            {
                InternalStop();
                State = StartableState.Stopped;
            }
            catch (Exception stopException)
            {
                tracer.Error(stopException, "Stop failed: Exception occurred.");
                State = StartableState.FailedToStop;
            }

            foreach (var disposableRef in _disposeOnStop.ToArray())
            {
                if (disposableRef.Target is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {} // No need to log this
                    catch (Exception exception)
                    {
                        tracer.Error(exception, "Exception while disposing " + disposable + ".");
                    }
                }
            }
            _disposeOnStop.Clear();

            tracer.Info(this + " stopped.");
        }

        /// <summary>
        /// Resets this instance, to start new as an unconfigured instance with no memory of the past.
        /// </summary>
        /// <param name="clearSetupLog">If <c>true</c>, the contents of the <see cref="SetupTracerFactory" /> are cleared.</param>
        public void Reset(bool clearSetupLog)
        {
            lock (this)
            {
                var tracer = SetupTracerFactory.TracerFor(this);
                tracer.Info("Resetting... ");

                Stop();

                if (clearSetupLog)
                {
                    if (SetupTracerFactory is SetupLog setupTracerFactory)
                    {
                        setupTracerFactory.Clear();
                    }
                }

                InternalReset();

                tracer.Info("Completed Reset. ");
            }
        }

        /// <summary>
        /// The start implementation, to be overridden by derived classes.
        /// </summary>
        protected virtual void InternalStart()
        {}

        /// <summary>
        /// The stop implementation, to be overridden by derived classes.
        /// </summary>
        protected virtual void InternalStop()
        {}

        /// <summary>
        /// The reset implementation, to be overridden by derived classes.
        /// </summary>
        protected abstract void InternalReset();

        /// <summary>
        /// Registers <paramref name="objectToDispose" /> for cleanup when <see cref="Stop" /> is called.
        /// </summary>
        /// <param name="objectToDispose"></param>
        public void DisposeOnStop(object objectToDispose)
        {
            if (objectToDispose is IDisposable)
            {
                _disposeOnStop.Add(new WeakReference(objectToDispose));
            }
        }

        /// <summary>
        /// Registers <paramref name="objectToDispose" /> to also be disposed when <see cref="Dispose()" /> is called.
        /// </summary>
        /// <param name="objectToDispose"></param>
        public void LinkDispose(object objectToDispose)
        {
            if (objectToDispose is IDisposable)
            {
                _linkedDisposables.Add((IDisposable) objectToDispose);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (! IsDisposed)
            {
                Stop();

                // Protect against recursion (eg linked disposables could create a cycle)
                State = StartableState.Disposing;

                if (disposing)
                {
                    // Dispose all the linked disposables
                    foreach (var disposable in _linkedDisposables.ToArray())
                    {
                        if (disposable != null)
                        {
                            try
                            {
                                disposable.Dispose();
                            }
                            catch (ObjectDisposedException)
                            {} // No need to log this
                            catch (Exception exception)
                            {
                                var tracer = SetupTracerFactory.TracerFor(this);
                                tracer.Error(exception, "Exception while disposing " + disposable + ".");
                            }
                        }
                    }
                    _linkedDisposables.Clear();
                }
                State = StartableState.Disposed;
            }
        }

        protected bool IsDisposed { get { return _startableState >= StartableState.Disposing; } }


        /// <summary>
        /// Override ToString() to provide more descriptive start/stop logging.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            // This makes Start/Stop logging friendlier, but subclasses are welcome to provide a better ToString()
            return GetType().GetCSharpName();
        }

        /// <summary>
        /// Returns the <see cref="ILogJamComponent.SetupTracerFactory" />, and ensures the same instance is shared by
        /// all of the passed in components.
        /// </summary>
        /// <param name="components"></param>
        /// <returns></returns>
        // TODO: The need for this is messy. Perhaps each component should manage its messages, and we just walk the tree of components to collect them?
        internal ITracerFactory GetSetupTracerFactoryForComponents(IEnumerable<ILogJamComponent> components)
        {
            ITracerFactory setupTracerFactory = null;

            foreach (var component in components)
            {
                var componentInstance = component.SetupTracerFactory;
                if (componentInstance != null)
                {
                    if (setupTracerFactory == null)
                    {
                        setupTracerFactory = componentInstance;
                    }
                    else if (! ReferenceEquals(setupTracerFactory, componentInstance))
                    {
                        throw new LogJamSetupException("Illegal to use different setup tracer factory instances within the same set of components.", this);
                    }
                }
            }
            return setupTracerFactory;
        }

    }

}

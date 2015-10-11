﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="BaseLogJamManager.cs">
// Copyright (c) 2011-2015 https://github.com/logjam2.  
// </copyright>
// Licensed under the <a href="https://github.com/logjam2/logjam/blob/master/LICENSE.txt">Apache License, Version 2.0</a>;
// you may not use this file except in compliance with the License.
// --------------------------------------------------------------------------------------------------------------------


namespace LogJam
{
    using System;
    using System.Collections.Generic;

    using LogJam.Internal;
    using LogJam.Trace;


    /// <summary>
    /// Common LogJam *Manager functionality.
    /// </summary>
    public abstract class BaseLogJamManager : IStartable, IDisposable
    {
        #region Instance fields

        private bool _isStarted;
        private Exception _startException;
        private bool _isStopped;
        private bool _isDisposed;

        private readonly List<WeakReference> _disposables = new List<WeakReference>();

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
        /// Ensures that <see cref="Start" /> is automatically called once, but <see cref="Start" /> is not automatically called
        /// again if an exception occurred during the initial start, or if <see cref="Stop" /> was called.
        /// </summary>
        /// <returns>
        /// <c>true</c> if this instance has been successfully started; returns <c>false</c> if the instance was
        /// <see cref="Stop" />ped
        /// or if the initial call to <see cref="Start" /> was not completely successful.
        /// </returns>
        public bool EnsureStarted()
        {
            if (_isStarted)
            {
                return true;
            }
            if (_isStopped)
            {
                return false;
            }
            if (_startException != null)
            {
                return false;
            }

            Start();
            return _isStarted;
        }

        /// <summary>
        /// Starts the manager whether or not <c>Start()</c> has already been called.
        /// </summary>
        /// <remarks>To avoid starting more than once, use <see cref="EnsureStarted" />.</remarks>
        public void Start()
        {
            var tracer = SetupTracerFactory.TracerFor(this);
            string className = GetType().Name;

            if (IsDisposed)
            {
                tracer.Error(className + " cannot be started; it has been Dispose()ed.");
                return;
            }

            tracer.Debug("Starting " + className + "...");

            lock (this)
            {
                try
                {
                    InternalStart();
                    _isStarted = true;
                    _isStopped = false;
                    tracer.Info(className + " started.");
                }
                catch (Exception startException)
                {
                    _startException = startException;
                    tracer.Error(startException, "Start failed: Exception occurred.");
                }
            }
        }

        /// <summary>
        /// Stops this instance; closes all disposables.
        /// </summary>
        public void Stop()
        {
            lock (this)
            {
                if (_isStopped)
                {
                    return;
                }
                _isStopped = true;
            }

            var tracer = SetupTracerFactory.TracerFor(this);
            string className = GetType().Name;
            tracer.Info("Stopping " + className + "...");

            InternalStop();

            foreach (var disposableRef in _disposables)
            {
                IDisposable disposable = disposableRef.Target as IDisposable;
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
                        tracer.Error(exception, "Exception while disposing " + className + ".");
                    }
                }
            }

            tracer.Info(className + " stopped.");
            _isStarted = false;
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
                    var setupTracerFactory = SetupTracerFactory as SetupLog;
                    if (setupTracerFactory != null)
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
        /// Returns <c>true</c> if <see cref="Start()" /> was called and succeeded.
        /// </summary>
        public bool IsStarted { get { return _isStarted; } }

        /// <summary>
        /// Returns <c>true</c> if <see cref="Stop" /> or <see cref="Dispose" /> has been called.
        /// </summary>
        public bool IsStopped { get { return _isStopped; } }

        /// <summary>
        /// Returns <c>true</c> if there have been no setup traces that are more severe than informational.
        /// </summary>
        /// <remarks>
        /// This method will return <c>false</c> if any warn, error, or severe traces have been reported during configuration,
        /// startup, or shutdown.
        /// </remarks>
        public bool IsHealthy { get { return ! SetupLog.HasAnyExceeding(TraceLevel.Info); } }

        /// <summary>
        /// Registers <paramref name="objectToDispose" /> for cleanup when <see cref="Stop" /> is called.
        /// </summary>
        /// <param name="objectToDispose"></param>
        public void DisposeOnStop(object objectToDispose)
        {
            if (objectToDispose is IDisposable)
            {
                _disposables.Add(new WeakReference(objectToDispose));
            }
        }

        public void Dispose()
        {
            if (! _isDisposed)
            {
                Dispose(true);
                GC.SuppressFinalize(this);
                _isDisposed = true;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (! _isDisposed)
            {
                Stop();
                _isDisposed = true;
            }
        }

        protected bool IsDisposed { get { return _isDisposed; } }

        /// <summary>
        /// Returns the <see cref="ILogJamComponent.SetupTracerFactory" />, and ensures the same instance is shared by
        /// all of the passed in components.
        /// </summary>
        /// <param name="components"></param>
        /// <returns></returns>
        // REVIEW: The need for this is messy.  Perhaps each component should manage its messages, and we just walk the tree of components to collect them?
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

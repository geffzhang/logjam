﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TaskHelper.cs">
// Copyright (c) 2011-2018 https://github.com/logjam2.  
// </copyright>
// Licensed under the <a href="https://github.com/logjam2/logjam/blob/master/LICENSE.txt">Apache License, Version 2.0</a>;
// you may not use this file except in compliance with the License.
// --------------------------------------------------------------------------------------------------------------------


using System.Threading.Tasks;

namespace LogJam.WebApi.Tasks
{

    /// <summary>
    /// Task/TPL helper functions.
    /// </summary>
    internal static class TaskHelper
    {

        private static readonly Task s_completed = Task.FromResult(true);

        public static Task Completed { get { return s_completed; } }

    }

}

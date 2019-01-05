// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EnumerableWrapper.cs">
// Copyright (c) 2011-2016 https://github.com/logjam2. 
// </copyright>
// Licensed under the <a href="https://github.com/logjam2/logjam/blob/master/LICENSE.txt">Apache License, Version 2.0</a>;
// you may not use this file except in compliance with the License.
// --------------------------------------------------------------------------------------------------------------------


namespace LogJam.Util
{
    using System.Collections;
    using System.Collections.Generic;

    using LogJam.Shared.Internal;


    /// <summary>
    /// Wraps an <see cref="IEnumerable{T}" />. Used to prevent access to the underlying collection via casting.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal sealed class EnumerableWrapper<T> : IEnumerable<T>
    {

        private readonly IEnumerable<T> _enumerable;

        internal EnumerableWrapper(IEnumerable<T> enumerable)
        {
            Arg.NotNull(enumerable, nameof(enumerable));

            _enumerable = enumerable;
        }

        #region Implementation of IEnumerable

        public IEnumerator<T> GetEnumerator()
        {
            return _enumerable.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _enumerable.GetEnumerator();
        }

        #endregion
    }
}

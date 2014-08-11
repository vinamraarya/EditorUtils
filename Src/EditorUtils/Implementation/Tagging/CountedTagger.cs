﻿using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using EditorUtils;

namespace EditorUtils.Implementation.Tagging
{
    /// <summary>
    /// It's possible, and very likely, for an ITagger<T> to be requested multiple times for the 
    /// same scenario via the ITaggerProvider.  This happens when extensions spin up custom 
    /// ITagAggregator instances or simple manually query for a new ITagger.  Having multiple taggers
    /// for the same data is often very unnecessary.  Produces a lot of duplicate work.  For example
    /// consider having multiple :hlsearch taggers for the same ITextView.  
    ///
    /// CountedTagger helps solve this by using a ref counted solution over the raw ITagger.  It allows
    /// for only one ITagger to be created for the same scenario
    /// </summary>
    internal sealed class CountedTagger<TTag> : ITagger<TTag>, IDisposable
        where TTag : ITag
    {
        private readonly ITagger<TTag> _tagger;
        private readonly object _key;
        private readonly PropertyCollection _propertyCollection;
        private int _count;

        internal ITagger<TTag> Tagger
        {
            get { return _tagger; }
        }

        internal CountedTagger(
            ITagger<TTag> tagger,
            object key,
            PropertyCollection propertyCollection)
        {
            _tagger = tagger;
            _key = key;
            _propertyCollection = propertyCollection;
            _count = 1;
        }

        internal void Dispose()
        {
            _count--;

            if (_count == 0)
            {
                var disposable = _tagger as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }
                _propertyCollection.RemoveProperty(_key);
            }
        }

        internal static ITagger<TTag> Create(
            object key, 
            PropertyCollection propertyCollection,
            Func<ITagger<TTag>> createFunc)
        {
            CountedTagger<TTag> countedTagger;
            if (propertyCollection.TryGetPropertySafe(key, out countedTagger))
            {
                countedTagger._count++;
                return countedTagger;
            }

            countedTagger = new CountedTagger<TTag>(createFunc(), key, propertyCollection);
            propertyCollection[key] = countedTagger;
            return countedTagger;
        }

        #region ITagger<TTag>

        IEnumerable<ITagSpan<TTag>> ITagger<TTag>.GetTags(NormalizedSnapshotSpanCollection col)
        {
            return _tagger.GetTags(col);
        }

        event EventHandler<SnapshotSpanEventArgs> ITagger<TTag>.TagsChanged
        {
            add { _tagger.TagsChanged += value; }
            remove { _tagger.TagsChanged -= value; }
        }

        #endregion

        #region IDisposable

        void IDisposable.Dispose()
        {
            Dispose();
        }

        #endregion
    }
}

﻿using System;
using Umbraco.Core.PropertyEditors;

namespace Umbraco.Core.Models.PublishedContent
{
    /// <inheritdoc />
    /// <summary>
    /// Represents a published property that has a unique invariant-neutral value
    /// and caches conversion results locally.
    /// </summary>
    /// <remarks>
    /// <para>Conversions results are stored within the property and will not
    /// be refreshed, so this class is not suitable for cached properties.</para>
    /// <para>Does not support variations: the ctor throws if the property type
    /// supports variations.</para>
    /// </remarks>
    internal class RawValueProperty : PublishedPropertyBase
    {
        private readonly object _sourceValue; //the value in the db
        private readonly Lazy<object> _objectValue;
        private readonly Lazy<object> _xpathValue;

        public override object GetSourceValue(int? languageId = null, string segment = null)
            => languageId == null & segment == null ? _sourceValue : null;

        public override bool HasValue(int? languageId = null, string segment = null)
        {
            var sourceValue = GetSourceValue(languageId, segment);
            return sourceValue is string s ? !string.IsNullOrWhiteSpace(s) : sourceValue != null;
        }

        public override object GetValue(int? languageId = null, string segment = null)
            => languageId == null & segment == null ? _objectValue.Value : null;

        public override object GetXPathValue(int? languageId = null, string segment = null)
            => languageId == null & segment == null ? _xpathValue.Value : null;

        public RawValueProperty(PublishedPropertyType propertyType, IPublishedElement content, object sourceValue, bool isPreviewing = false)
            : base(propertyType, PropertyCacheLevel.Unknown) // cache level is ignored
        {
            if (propertyType.Variations != ContentVariation.InvariantNeutral)
                throw new ArgumentException("Property types with variations are not supported here.", nameof(propertyType));

            _sourceValue = sourceValue;

            var interValue = new Lazy<object>(() => PropertyType.ConvertSourceToInter(content, _sourceValue, isPreviewing));
            _objectValue = new Lazy<object>(() => PropertyType.ConvertInterToObject(content, PropertyCacheLevel.Unknown, interValue.Value, isPreviewing));
            _xpathValue = new Lazy<object>(() => PropertyType.ConvertInterToXPath(content, PropertyCacheLevel.Unknown, interValue.Value, isPreviewing));
        }
    }
}

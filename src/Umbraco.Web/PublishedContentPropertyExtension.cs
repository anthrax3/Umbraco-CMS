﻿using Umbraco.Core;
using Umbraco.Core.Models.PublishedContent;

namespace Umbraco.Web
{
    /// <summary>
    /// Provides extension methods for <c>IPublishedProperty</c>.
    /// </summary>
    public static class PublishedPropertyExtension
    {
        #region Value<T>

        public static T Value<T>(this IPublishedProperty property, int? languageId = null, string segment = null)
        {
            return property.Value(false, default(T), languageId, segment);
        }

        public static T Value<T>(this IPublishedProperty property, T defaultValue, int? languageId = null, string segment = null)
        {
            return property.Value(true, defaultValue, languageId, segment);
        }

        internal static T Value<T>(this IPublishedProperty property, bool withDefaultValue, T defaultValue, int? languageId = null, string segment = null)
        {
            if (property.HasValue(languageId, segment) == false && withDefaultValue) return defaultValue;

            // else we use .Value so we give the converter a chance to handle the default value differently
            // eg for IEnumerable<T> it may return Enumerable<T>.Empty instead of null

            var value = property.GetValue(languageId, segment);

            // if value is null (strange but why not) it still is OK to call TryConvertTo
            // because it's an extension method (hence no NullRef) which will return a
            // failed attempt. So, no need to care for value being null here.

            // if already the requested type, return
            if (value is T) return (T)value;

            // if can convert to requested type, return
            var convert = value.TryConvertTo<T>();
            if (convert.Success) return convert.Result;

            // at that point, the code tried with the raw value
            // that makes no sense because it sort of is unpredictable,
            // you wouldn't know when the converters run or don't run.
            // so, it's commented out now.

            // try with the raw value
            //var source = property.ValueSource;
            //if (source is string) source = TextValueConverterHelper.ParseStringValueSource((string)source);
            //if (source is T) return (T)source;
            //convert = source.TryConvertTo<T>();
            //if (convert.Success) return convert.Result;

            return defaultValue;
        }

        #endregion
    }
}

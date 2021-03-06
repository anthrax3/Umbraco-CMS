﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.PropertyEditors.ValueConverters;

namespace Umbraco.Web.Models
{
    /// <summary>
    /// Provide an abstract base class for <c>IPublishedContent</c> implementations.
    /// </summary>
    /// <remarks>This base class does which (a) consitently resolves and caches the Url, (b) provides an implementation
    /// for this[alias], and (c) provides basic content set management.</remarks>
    [DebuggerDisplay("Content Id: {Id}, Name: {Name}")]
    public abstract class PublishedContentBase : IPublishedContent
    {
        #region Content

        private string _url;

        /// <summary>
        /// Gets the url of the content.
        /// </summary>
        /// <remarks>
        /// If this content is Content, the url that is returned is the one computed by the NiceUrlProvider, otherwise if
        /// this content is Media, the url returned is the value found in the 'umbracoFile' property.
        /// </remarks>
        public virtual string Url
        {
            get
            {
                // should be thread-safe although it won't prevent url from being resolved more than once
                if (_url != null)
                    return _url;

                switch (ItemType)
                {
                    case PublishedItemType.Content:
                        if (UmbracoContext.Current == null)
                            throw new InvalidOperationException(
                                "Cannot resolve a Url for a content item when UmbracoContext.Current is null.");
                        if (UmbracoContext.Current.UrlProvider == null)
                            throw new InvalidOperationException(
                                "Cannot resolve a Url for a content item when UmbracoContext.Current.UrlProvider is null.");
                        _url = UmbracoContext.Current.UrlProvider.GetUrl(Id);
                        break;
                    case PublishedItemType.Media:
                        var prop = GetProperty(Constants.Conventions.Media.File);
                        if (prop == null || prop.GetValue() == null)
                        {
                            _url = string.Empty;
                            return _url;
                        }

                        var propType = ContentType.GetPropertyType(Constants.Conventions.Media.File);

                        // fixme this is horrible we need url providers for media too
                        //This is a hack - since we now have 2 properties that support a URL: upload and cropper, we need to detect this since we always
                        // want to return the normal URL and the cropper stores data as json
                        switch (propType.EditorAlias)
                        {
                            case Constants.PropertyEditors.Aliases.UploadField:
                                _url = prop.GetValue().ToString();
                                break;
                            case Constants.PropertyEditors.Aliases.ImageCropper:
                                //get the url from the json format

                                var stronglyTyped = prop.GetValue() as ImageCropperValue;
                                if (stronglyTyped != null)
                                {
                                    _url = stronglyTyped.Src;
                                    break;
                                }
                                _url = prop.GetValue()?.ToString();
                                break;
                        }
                        break;
                    default:
                        throw new NotSupportedException();
                }

                return _url;
            }
        }

        public abstract PublishedItemType ItemType { get; }
        public abstract int Id { get; }
        public abstract Guid Key { get; }
        public abstract int TemplateId { get; }
        public abstract int SortOrder { get; }
        public abstract string Name { get; }
        public abstract string UrlName { get; }
        public abstract string DocumentTypeAlias { get; }
        public abstract int DocumentTypeId { get; }
        public abstract string WriterName { get; }
        public abstract string CreatorName { get; }
        public abstract int WriterId { get; }
        public abstract int CreatorId { get; }
        public abstract string Path { get; }
        public abstract DateTime CreateDate { get; }
        public abstract DateTime UpdateDate { get; }
        public abstract int Level { get; }

        public abstract bool IsDraft { get; }

        #endregion

        #region Tree

        /// <summary>
        /// Gets the parent of the content.
        /// </summary>
        public abstract IPublishedContent Parent { get; }

        /// <summary>
        /// Gets the children of the content.
        /// </summary>
        /// <remarks>Children are sorted by their sortOrder.</remarks>
        public abstract IEnumerable<IPublishedContent> Children { get; }

        #endregion

        #region ContentType

        public abstract PublishedContentType ContentType { get; }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the properties of the content.
        /// </summary>
        public abstract IEnumerable<IPublishedProperty> Properties { get; }

        /// <summary>
        /// Gets a property identified by its alias.
        /// </summary>
        /// <param name="alias">The property alias.</param>
        /// <returns>The property identified by the alias.</returns>
        /// <remarks>
        /// <para>If no property with the specified alias exists, returns <c>null</c>.</para>
        /// <para>The returned property may have no value (ie <c>HasValue</c> is <c>false</c>).</para>
        /// <para>The alias is case-insensitive.</para>
        /// </remarks>
        public abstract IPublishedProperty GetProperty(string alias);

        /// <summary>
        /// Gets a property identified by its alias.
        /// </summary>
        /// <param name="alias">The property alias.</param>
        /// <param name="recurse">A value indicating whether to navigate the tree upwards until a property with a value is found.</param>
        /// <returns>The property identified by the alias.</returns>
        /// <remarks>
        /// <para>Navigate the tree upwards and look for a property with that alias and with a value (ie <c>HasValue</c> is <c>true</c>).
        /// If found, return the property. If no property with that alias is found, having a value or not, return <c>null</c>. Otherwise
        /// return the first property that was found with the alias but had no value (ie <c>HasValue</c> is <c>false</c>).</para>
        /// <para>The alias is case-insensitive.</para>
        /// </remarks>
        public virtual IPublishedProperty GetProperty(string alias, bool recurse)
        {
            var property = GetProperty(alias);
            if (recurse == false) return property;

            IPublishedContent content = this;
            var firstNonNullProperty = property;
            while (content != null && (property == null || property.HasValue() == false))
            {
                content = content.Parent;
                property = content?.GetProperty(alias);
                if (firstNonNullProperty == null && property != null) firstNonNullProperty = property;
            }

            // if we find a content with the property with a value, return that property
            // if we find no content with the property, return null
            // if we find a content with the property without a value, return that property
            //   have to save that first property while we look further up, hence firstNonNullProperty

            return property != null && property.HasValue() ? property : firstNonNullProperty;
        }

        #endregion
    }
}

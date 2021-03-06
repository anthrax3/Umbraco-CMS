﻿using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.PropertyEditors;
using Umbraco.Tests.TestHelpers;
using Umbraco.Web;
using Umbraco.Web.PublishedCache;

namespace Umbraco.Tests.PublishedContent
{
    class SolidPublishedShapshot : IPublishedShapshot
    {
        public readonly SolidPublishedContentCache InnerContentCache = new SolidPublishedContentCache();
        public readonly SolidPublishedContentCache InnerMediaCache = new SolidPublishedContentCache();

        public IPublishedContentCache Content => InnerContentCache;

        public IPublishedMediaCache Media => InnerMediaCache;

        public IPublishedMemberCache Members => null;

        public IDomainCache Domains => null;

        public IDisposable ForcedPreview(bool forcedPreview, Action<bool> callback = null)
        {
            throw new NotImplementedException();
        }

        public void Resync()
        { }

        public ICacheProvider SnapshotCache => null;

        public ICacheProvider ElementsCache => null;
    }

    class SolidPublishedContentCache : PublishedCacheBase, IPublishedContentCache, IPublishedMediaCache
    {
        private readonly Dictionary<int, IPublishedContent> _content = new Dictionary<int, IPublishedContent>();

        public SolidPublishedContentCache()
            : base(false)
        { }

        public void Add(SolidPublishedContent content)
        {
            _content[content.Id] = content.CreateModel();
        }

        public void Clear()
        {
            _content.Clear();
        }

        public IPublishedContent GetByRoute(bool preview, string route, bool? hideTopLevelNode = null)
        {
            throw new NotImplementedException();
        }

        public IPublishedContent GetByRoute(string route, bool? hideTopLevelNode = null)
        {
            throw new NotImplementedException();
        }

        public string GetRouteById(bool preview, int contentId)
        {
            throw new NotImplementedException();
        }

        public string GetRouteById(int contentId)
        {
            throw new NotImplementedException();
        }

        public override IPublishedContent GetById(bool preview, int contentId)
        {
            return _content.ContainsKey(contentId) ? _content[contentId] : null;
        }

        public override IPublishedContent GetById(bool preview, Guid contentId)
        {
            throw new NotImplementedException();
        }

        public override bool HasById(bool preview, int contentId)
        {
            return _content.ContainsKey(contentId);
        }

        public override IEnumerable<IPublishedContent> GetAtRoot(bool preview)
        {
            return _content.Values.Where(x => x.Parent == null);
        }

        public override IPublishedContent GetSingleByXPath(bool preview, string xpath, Core.Xml.XPathVariable[] vars)
        {
            throw new NotImplementedException();
        }

        public override IPublishedContent GetSingleByXPath(bool preview, System.Xml.XPath.XPathExpression xpath, Core.Xml.XPathVariable[] vars)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<IPublishedContent> GetByXPath(bool preview, string xpath, Core.Xml.XPathVariable[] vars)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<IPublishedContent> GetByXPath(bool preview, System.Xml.XPath.XPathExpression xpath, Core.Xml.XPathVariable[] vars)
        {
            throw new NotImplementedException();
        }

        public override System.Xml.XPath.XPathNavigator CreateNavigator(bool preview)
        {
            throw new NotImplementedException();
        }

        public override System.Xml.XPath.XPathNavigator CreateNodeNavigator(int id, bool preview)
        {
            throw new NotImplementedException();
        }

        public override bool HasContent(bool preview)
        {
            return _content.Count > 0;
        }

        public override PublishedContentType GetContentType(int id)
        {
            throw new NotImplementedException();
        }

        public override PublishedContentType GetContentType(string alias)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<IPublishedContent> GetByContentType(PublishedContentType contentType)
        {
            throw new NotImplementedException();
        }
    }

    class SolidPublishedContent : IPublishedContent
    {
        #region Constructor

        public SolidPublishedContent(PublishedContentType contentType)
        {
            // initialize boring stuff
            TemplateId = 0;
            WriterName = CreatorName = string.Empty;
            WriterId = CreatorId = 0;
            CreateDate = UpdateDate = DateTime.Now;
            Version = Guid.Empty;
            IsDraft = false;

            ContentType = contentType;
            DocumentTypeAlias = contentType.Alias;
            DocumentTypeId = contentType.Id;
        }

        #endregion

        #region Content

        public int Id { get; set; }
        public Guid Key { get; set; }
        public int TemplateId { get; set; }
        public int SortOrder { get; set; }
        public string Name { get; set; }
        public string UrlName { get; set; }
        public string DocumentTypeAlias { get; private set; }
        public int DocumentTypeId { get; private set; }
        public string WriterName { get; set; }
        public string CreatorName { get; set; }
        public int WriterId { get; set; }
        public int CreatorId { get; set; }
        public string Path { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }
        public Guid Version { get; set; }
        public int Level { get; set; }
        public string Url { get; set; }

        public PublishedItemType ItemType { get { return PublishedItemType.Content; } }
        public bool IsDraft { get; set; }

        #endregion

        #region Tree

        public int ParentId { get; set; }
        public IEnumerable<int> ChildIds { get; set; }

        public IPublishedContent Parent { get; set; }
        public IEnumerable<IPublishedContent> Children { get; set; }

        #endregion

        #region ContentType

        public PublishedContentType ContentType { get; private set; }

        #endregion

        #region Properties

        public IEnumerable<IPublishedProperty> Properties { get; set; }

        public IPublishedProperty GetProperty(string alias)
        {
            return Properties.FirstOrDefault(p => p.Alias.InvariantEquals(alias));
        }

        public IPublishedProperty GetProperty(string alias, bool recurse)
        {
            var property = GetProperty(alias);
            if (recurse == false) return property;

            IPublishedContent content = this;
            while (content != null && (property == null || property.HasValue() == false))
            {
                content = content.Parent;
                property = content == null ? null : content.GetProperty(alias);
            }

            return property;
        }

        public object this[string alias]
        {
            get
            {
                var property = GetProperty(alias);
                return property == null || property.HasValue() == false ? null : property.GetValue();
            }
        }

        #endregion
    }

    class SolidPublishedProperty : IPublishedProperty
    {
        public string Alias { get; set; }
        public object SolidSourceValue { get; set; }
        public object SolidValue { get; set; }
        public bool SolidHasValue { get; set; }
        public object SolidXPathValue { get; set; }

        public object GetSourceValue(int? languageId = null, string segment = null) => SolidSourceValue;
        public object GetValue(int? languageId = null, string segment = null) => SolidValue;
        public object GetXPathValue(int? languageId = null, string segment = null) => SolidXPathValue;
        public bool HasValue(int? languageId = null, string segment = null) => SolidHasValue;
    }

    [PublishedModel("ContentType2")]
    internal class ContentType2 : PublishedContentModel
    {
        #region Plumbing

        public ContentType2(IPublishedContent content)
            : base(content)
        { }

        #endregion

        public int Prop1 => this.Value<int>("prop1");
    }

    [PublishedModel("ContentType2Sub")]
    internal class ContentType2Sub : ContentType2
    {
        #region Plumbing

        public ContentType2Sub(IPublishedContent content)
            : base(content)
        { }

        #endregion
    }

    class PublishedContentStrong1 : PublishedContentModel
    {
        public PublishedContentStrong1(IPublishedContent content)
            : base(content)
        { }

        public int StrongValue => this.Value<int>("strongValue");
    }

    class PublishedContentStrong1Sub : PublishedContentStrong1
    {
        public PublishedContentStrong1Sub(IPublishedContent content)
            : base(content)
        { }

        public int AnotherValue => this.Value<int>("anotherValue");
    }

    class PublishedContentStrong2 : PublishedContentModel
    {
        public PublishedContentStrong2(IPublishedContent content)
            : base(content)
        { }

        public int StrongValue => this.Value<int>("strongValue");
    }

    class AutoPublishedContentType : PublishedContentType
    {
        private static readonly PublishedPropertyType Default;

        static AutoPublishedContentType()
        {
            var dataTypeService = new TestObjects.TestDataTypeService(
                new DataType(new VoidEditor(Mock.Of<ILogger>())) { Id = 666 });

            var factory = new PublishedContentTypeFactory(Mock.Of<IPublishedModelFactory>(), new PropertyValueConverterCollection(Array.Empty<IPropertyValueConverter>()), dataTypeService);
            Default = factory.CreatePropertyType("*", 666);
        }

        public AutoPublishedContentType(int id, string alias, IEnumerable<PublishedPropertyType> propertyTypes)
            : base(id, alias, PublishedItemType.Content, Enumerable.Empty<string>(), propertyTypes, ContentVariation.InvariantNeutral)
        { }

        public AutoPublishedContentType(int id, string alias, IEnumerable<string> compositionAliases, IEnumerable<PublishedPropertyType> propertyTypes)
            : base(id, alias, PublishedItemType.Content, compositionAliases, propertyTypes, ContentVariation.InvariantNeutral)
        { }

        public override PublishedPropertyType GetPropertyType(string alias)
        {
            var propertyType = base.GetPropertyType(alias);
            return propertyType ?? Default;
        }
    }
}

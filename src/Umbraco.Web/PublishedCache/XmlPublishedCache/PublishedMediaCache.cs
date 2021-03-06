﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.XPath;
using Examine;
using Examine.LuceneEngine.Providers;
using Examine.LuceneEngine.SearchCriteria;
using Examine.Providers;
using Lucene.Net.Store;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Xml;
using Umbraco.Web.Models;
using Umbraco.Examine;
using umbraco;
using Umbraco.Core.Cache;
using Umbraco.Core.Services;
using Umbraco.Web.Composing;

namespace Umbraco.Web.PublishedCache.XmlPublishedCache
{
    /// <summary>
    /// An IPublishedMediaStore that first checks for the media in Examine, and then reverts to the database
    /// </summary>
    /// <remarks>
    /// NOTE: In the future if we want to properly cache all media this class can be extended or replaced when these classes/interfaces are exposed publicly.
    /// </remarks>
    internal class PublishedMediaCache : PublishedCacheBase, IPublishedMediaCache
    {
        private readonly IMediaService _mediaService;
        private readonly IUserService _userService;

        // by default these are null unless specified by the ctor dedicated to tests
        // when they are null the cache derives them from the ExamineManager, see
        // method GetExamineManagerSafe().
        //
        private readonly ILuceneSearcher _searchProvider;
        private readonly BaseIndexProvider _indexProvider;
        private readonly XmlStore _xmlStore;
        private readonly PublishedContentTypeCache _contentTypeCache;

        // must be specified by the ctor
        private readonly ICacheProvider _cacheProvider;

        public PublishedMediaCache(XmlStore xmlStore, IMediaService mediaService, IUserService userService, ICacheProvider cacheProvider, PublishedContentTypeCache contentTypeCache)
            : base(false)
        {
            if (mediaService == null) throw new ArgumentNullException(nameof(mediaService));
            if (userService == null) throw new ArgumentNullException(nameof(userService));
            _mediaService = mediaService;
            _userService = userService;

            _cacheProvider = cacheProvider;
            _xmlStore = xmlStore;
            _contentTypeCache = contentTypeCache;
        }

        /// <summary>
        /// Generally used for unit testing to use an explicit examine searcher
        /// </summary>
        /// <param name="mediaService"></param>
        /// <param name="userService"></param>
        /// <param name="searchProvider"></param>
        /// <param name="indexProvider"></param>
        /// <param name="cacheProvider"></param>
        /// <param name="contentTypeCache"></param>
        internal PublishedMediaCache(IMediaService mediaService, IUserService userService, ILuceneSearcher searchProvider, BaseIndexProvider indexProvider, ICacheProvider cacheProvider, PublishedContentTypeCache contentTypeCache)
            : base(false)
        {
            if (mediaService == null) throw new ArgumentNullException(nameof(mediaService));
            if (userService == null) throw new ArgumentNullException(nameof(userService));
            if (searchProvider == null) throw new ArgumentNullException(nameof(searchProvider));
            if (indexProvider == null) throw new ArgumentNullException(nameof(indexProvider));

            _mediaService = mediaService;
            _userService = userService;
            _searchProvider = searchProvider;
            _indexProvider = indexProvider;
            _cacheProvider = cacheProvider;
            _contentTypeCache = contentTypeCache;
        }

        static PublishedMediaCache()
        {
            InitializeCacheConfig();
        }

        public override IPublishedContent GetById(bool preview, int nodeId)
        {
            return GetUmbracoMedia(nodeId);
        }

        public override IPublishedContent GetById(bool preview, Guid nodeId)
        {
            throw new NotImplementedException();
        }

        public override bool HasById(bool preview, int contentId)
        {
            return GetUmbracoMedia(contentId) != null;
        }

        public override IEnumerable<IPublishedContent> GetAtRoot(bool preview)
        {
            //TODO: We should be able to look these ids first in Examine!

            var rootMedia = _mediaService.GetRootMedia();
            return rootMedia.Select(m => GetUmbracoMedia(m.Id));
        }

        public override IPublishedContent GetSingleByXPath(bool preview, string xpath, XPathVariable[] vars)
        {
            throw new NotImplementedException("PublishedMediaCache does not support XPath.");
            //var navigator = CreateNavigator(preview);
            //var iterator = navigator.Select(xpath, vars);
            //return GetSingleByXPath(iterator);
        }

        public override IPublishedContent GetSingleByXPath(bool preview, XPathExpression xpath, XPathVariable[] vars)
        {
            throw new NotImplementedException("PublishedMediaCache does not support XPath.");
            //var navigator = CreateNavigator(preview);
            //var iterator = navigator.Select(xpath, vars);
            //return GetSingleByXPath(iterator);
        }

        private IPublishedContent GetSingleByXPath(XPathNodeIterator iterator)
        {
            throw new NotImplementedException("PublishedMediaCache does not support XPath.");
            //if (iterator.MoveNext() == false) return null;
            //var idAttr = iterator.Current.GetAttribute("id", "");
            //int id;
            //return int.TryParse(idAttr, out id) ? GetUmbracoMedia(id) : null;
        }

        public override IEnumerable<IPublishedContent> GetByXPath(bool preview, string xpath, XPathVariable[] vars)
        {
            throw new NotImplementedException("PublishedMediaCache does not support XPath.");
            //var navigator = CreateNavigator(preview);
            //var iterator = navigator.Select(xpath, vars);
            //return GetByXPath(iterator);
        }

        public override IEnumerable<IPublishedContent> GetByXPath(bool preview, XPathExpression xpath, XPathVariable[] vars)
        {
            throw new NotImplementedException("PublishedMediaCache does not support XPath.");
            //var navigator = CreateNavigator(preview);
            //var iterator = navigator.Select(xpath, vars);
            //return GetByXPath(iterator);
        }

        private IEnumerable<IPublishedContent> GetByXPath(XPathNodeIterator iterator)
        {
            while (iterator.MoveNext())
            {
                var idAttr = iterator.Current.GetAttribute("id", "");
                int id;
                if (int.TryParse(idAttr, out id))
                    yield return GetUmbracoMedia(id);
            }
        }

        public override XPathNavigator CreateNavigator(bool preview)
        {
            throw new NotImplementedException("PublishedMediaCache does not support XPath.");
            //var doc = _xmlStore.GetMediaXml();
            //return doc.CreateNavigator();
        }

        public override XPathNavigator CreateNodeNavigator(int id, bool preview)
        {
            // preview is ignored for media cache

            // this code is mostly used when replacing old media.ToXml() code, and that code
            // stored the XML attached to the media itself - so for some time in memory - so
            // unless we implement some sort of cache here, we're probably degrading perfs.

            XPathNavigator navigator = null;
            var node = _xmlStore.GetMediaXmlNode(id);
            if (node != null)
            {
                navigator = node.CreateNavigator();
            }
            return navigator;
        }

        public override bool HasContent(bool preview) { throw new NotImplementedException(); }

        private static ExamineManager GetExamineManagerSafe()
        {
            try
            {
                return ExamineManager.Instance;
            }
            catch (TypeInitializationException)
            {
                return null;
            }
        }

        private BaseIndexProvider GetIndexProviderSafe()
        {
            if (_indexProvider != null)
                return _indexProvider;

            var eMgr = GetExamineManagerSafe();
            if (eMgr == null) return null;

            try
            {
                //by default use the InternalSearcher
                var indexer = eMgr.IndexProviderCollection[Constants.Examine.InternalIndexer];
                if (indexer.IndexerData.IncludeNodeTypes.Any() || indexer.IndexerData.ExcludeNodeTypes.Any())
                {
                    Current.Logger.Warn<PublishedMediaCache>("The InternalIndexer for examine is configured incorrectly, it should not list any include/exclude node types or field names, it should simply be configured as: " + "<IndexSet SetName=\"InternalIndexSet\" IndexPath=\"~/App_Data/TEMP/ExamineIndexes/Internal/\" />");
                }
                return indexer;
            }
            catch (Exception ex)
            {
                Current.Logger.Error<PublishedMediaCache>("Could not retrieve the InternalIndexer", ex);
                //something didn't work, continue returning null.
            }
            return null;
        }

        private ILuceneSearcher GetSearchProviderSafe()
        {
            if (_searchProvider != null)
                return _searchProvider;

            var eMgr = GetExamineManagerSafe();
            if (eMgr == null) return null;

            try
            {
                //by default use the InternalSearcher
                return eMgr.GetSearcher(Constants.Examine.InternalIndexer);
            }
            catch (FileNotFoundException)
            {
                //Currently examine is throwing FileNotFound exceptions when we have a loadbalanced filestore and a node is published in umbraco
                //See this thread: http://examine.cdodeplex.com/discussions/264341
                //Catch the exception here for the time being, and just fallback to GetMedia
                //TODO: Need to fix examine in LB scenarios!
            }
            catch (NullReferenceException)
            {
                //This will occur when the search provider cannot be initialized. In newer examine versions the initialization is lazy and therefore
                // the manager will return the singleton without throwing initialization errors, however if examine isn't configured correctly a null
                // reference error will occur because the examine settings are null.
            }
            catch (AlreadyClosedException)
            {
                //If the app domain is shutting down and the site is under heavy load the index reader will be closed and it really cannot
                //be re-opened since the app domain is shutting down. In this case we have no option but to try to load the data from the db.
            }
            return null;
        }

        private IPublishedContent GetUmbracoMedia(int id)
        {
            // this recreates an IPublishedContent and model each time
            // it is called, but at least it should NOT hit the database
            // nor Lucene each time, relying on the memory cache instead

            if (id <= 0) return null; // fail fast

            var cacheValues = GetCacheValues(id, GetUmbracoMediaCacheValues);

            return cacheValues == null ? null : CreateFromCacheValues(cacheValues);
        }

        private CacheValues GetUmbracoMediaCacheValues(int id)
        {
            var searchProvider = GetSearchProviderSafe();

            if (searchProvider != null)
            {
                try
                {
                    // first check in Examine as this is WAY faster
                    //
                    // the filter will create a query like this:
                    // +(+__NodeId:3113 -__Path:-1,-21,*) +__IndexType:media
                    //
                    // note that since the use of the wildcard, it automatically escapes it in Lucene.

                    var criteria = searchProvider.CreateSearchCriteria("media");
                    var filter = criteria.Id(id).Not().Field(BaseUmbracoIndexer.IndexPathFieldName, "-1,-21,".MultipleCharacterWildcard());

                    var result = searchProvider.Search(filter.Compile()).FirstOrDefault();
                    if (result != null) return ConvertFromSearchResult(result);
                }
                catch (Exception ex)
                {
                    if (ex is FileNotFoundException)
                    {
                        //Currently examine is throwing FileNotFound exceptions when we have a loadbalanced filestore and a node is published in umbraco
                        //See this thread: http://examine.cdodeplex.com/discussions/264341
                        //Catch the exception here for the time being, and just fallback to GetMedia
                        //TODO: Need to fix examine in LB scenarios!
                        Current.Logger.Error<PublishedMediaCache>("Could not load data from Examine index for media", ex);
                    }
                    else if (ex is AlreadyClosedException)
                    {
                        //If the app domain is shutting down and the site is under heavy load the index reader will be closed and it really cannot
                        //be re-opened since the app domain is shutting down. In this case we have no option but to try to load the data from the db.
                        Current.Logger.Error<PublishedMediaCache>("Could not load data from Examine index for media, the app domain is most likely in a shutdown state", ex);
                    }
                    else throw;
                }
            }

            // don't log a warning here, as it can flood the log in case of eg a media picker referencing a media
            // that has been deleted, hence is not in the Examine index anymore (for a good reason). try to get
            // the media from the service, first
            var media = _mediaService.GetById(id);
            if (media == null || media.Trashed) return null; // not found, ok

            // so, the media was not found in Examine's index *yet* it exists, which probably indicates that
            // the index is corrupted. Or not up-to-date. Log a warning, but only once, and only if seeing the
            // error more that a number of times.

            var miss = Interlocked.CompareExchange(ref _examineIndexMiss, 0, 0); // volatile read
            if (miss < ExamineIndexMissMax && Interlocked.Increment(ref _examineIndexMiss) == ExamineIndexMissMax)
                Current.Logger.Warn<PublishedMediaCache>("Failed ({0} times) to retrieve medias from Examine index and had to load"
                    + " them from DB. This may indicate that the Examine index is corrupted.",
                    () => ExamineIndexMissMax);

            return ConvertFromIMedia(media);
        }

        private const int ExamineIndexMissMax = 10;
        private int _examineIndexMiss;

        internal CacheValues ConvertFromXPathNodeIterator(XPathNodeIterator media, int id)
        {
            if (media?.Current != null)
            {
                return media.Current.Name.InvariantEquals("error")
                    ? null
                    : ConvertFromXPathNavigator(media.Current);
            }

            Current.Logger.Warn<PublishedMediaCache>(
                "Could not retrieve media {0} from Examine index or from legacy library.GetMedia method",
                () => id);

            return null;
        }

        internal CacheValues ConvertFromSearchResult(SearchResult searchResult)
        {
            // note: fixing fields in 7.x, removed by Shan for 8.0
            var values = new Dictionary<string, string>(searchResult.Fields);

            return new CacheValues
            {
                Values = values,
                FromExamine = true
            };
        }

        internal CacheValues ConvertFromXPathNavigator(XPathNavigator xpath, bool forceNav = false)
        {
            if (xpath == null) throw new ArgumentNullException(nameof(xpath));

            var values = new Dictionary<string, string> { { "nodeName", xpath.GetAttribute("nodeName", "") } };
            values["nodeTypeAlias"] = xpath.Name;

            var result = xpath.SelectChildren(XPathNodeType.Element);
            //add the attributes e.g. id, parentId etc
            if (result.Current != null && result.Current.HasAttributes)
            {
                if (result.Current.MoveToFirstAttribute())
                {
                    //checking for duplicate keys because of the 'nodeTypeAlias' might already be added above.
                    if (values.ContainsKey(result.Current.Name) == false)
                    {
                        values[result.Current.Name] = result.Current.Value;
                    }
                    while (result.Current.MoveToNextAttribute())
                    {
                        if (values.ContainsKey(result.Current.Name) == false)
                        {
                            values[result.Current.Name] = result.Current.Value;
                        }
                    }
                    result.Current.MoveToParent();
                }
            }
            // because, migration
            if (values.ContainsKey("key") == false)
                values["key"] = Guid.Empty.ToString();
            //add the user props
            while (result.MoveNext())
            {
                if (result.Current != null && result.Current.HasAttributes == false)
                {
                    var value = result.Current.Value;
                    if (string.IsNullOrEmpty(value))
                    {
                        if (result.Current.HasAttributes || result.Current.SelectChildren(XPathNodeType.Element).Count > 0)
                        {
                            value = result.Current.OuterXml;
                        }
                    }
                    values[result.Current.Name] = value;
                }
            }

            return new CacheValues
            {
                Values = values,
                XPath = forceNav ? xpath : null // outside of tests we do NOT want to cache the navigator!
            };
        }

        internal CacheValues ConvertFromIMedia(IMedia media)
        {
            var values = new Dictionary<string, string>();

            var creator = _userService.GetProfileById(media.CreatorId);
            var creatorName = creator == null ? "" : creator.Name;

            values["id"] = media.Id.ToString();
            values["key"] = media.Key.ToString();
            values["parentID"] = media.ParentId.ToString();
            values["level"] = media.Level.ToString();
            values["creatorID"] = media.CreatorId.ToString();
            values["creatorName"] = creatorName;
            values["writerID"] = media.CreatorId.ToString();
            values["writerName"] = creatorName;
            values["template"] = "0";
            values["urlName"] = "";
            values["sortOrder"] = media.SortOrder.ToString();
            values["createDate"] = media.CreateDate.ToString("yyyy-MM-dd HH:mm:ss");
            values["updateDate"] = media.UpdateDate.ToString("yyyy-MM-dd HH:mm:ss");
            values["nodeName"] = media.Name;
            values["path"] = media.Path;
            values["nodeType"] = media.ContentType.Id.ToString();
            values["nodeTypeAlias"] = media.ContentType.Alias;

            // add the user props
            foreach (var prop in media.Properties)
                values[prop.Alias] = prop.GetValue()?.ToString();

            return new CacheValues
            {
                Values = values
            };
        }

        /// <summary>
        /// We will need to first check if the document was loaded by Examine, if so we'll need to check if this property exists
        /// in the results, if it does not, then we'll have to revert to looking up in the db.
        /// </summary>
        /// <param name="dd"> </param>
        /// <param name="alias"></param>
        /// <returns></returns>
        private IPublishedProperty GetProperty(DictionaryPublishedContent dd, string alias)
        {
            //lets check if the alias does not exist on the document.
            //NOTE: Examine will not index empty values and we do not output empty XML Elements to the cache - either of these situations
            // would mean that the property is missing from the collection whether we are getting the value from Examine or from the library media cache.
            if (dd.Properties.All(x => x.Alias.InvariantEquals(alias) == false))
            {
                return null;
            }

            if (dd.LoadedFromExamine)
            {
                //We are going to check for a special field however, that is because in some cases we store a 'Raw'
                //value in the index such as for xml/html.
                var rawValue = dd.Properties.FirstOrDefault(x => x.Alias.InvariantEquals(BaseUmbracoIndexer.RawFieldPrefix + alias));
                return rawValue
                       ?? dd.Properties.FirstOrDefault(x => x.Alias.InvariantEquals(alias));
            }

            //if its not loaded from examine, then just return the property
            return dd.Properties.FirstOrDefault(x => x.Alias.InvariantEquals(alias));
        }

        /// <summary>
        /// A Helper methods to return the children for media whther it is based on examine or xml
        /// </summary>
        /// <param name="parentId"></param>
        /// <param name="xpath"></param>
        /// <returns></returns>
        private IEnumerable<IPublishedContent> GetChildrenMedia(int parentId, XPathNavigator xpath = null)
        {

            //if there is no navigator, try examine first, then re-look it up
            if (xpath == null)
            {
                var searchProvider = GetSearchProviderSafe();

                if (searchProvider != null)
                {
                    try
                    {
                        //first check in Examine as this is WAY faster
                        var criteria = searchProvider.CreateCriteria("media");

                        var filter = criteria.ParentId(parentId).Not().Field(BaseUmbracoIndexer.IndexPathFieldName, "-1,-21,".MultipleCharacterWildcard());
                        //the above filter will create a query like this, NOTE: That since the use of the wildcard, it automatically escapes it in Lucene.
                        //+(+parentId:3113 -__Path:-1,-21,*) +__IndexType:media

                        // sort with the Sort field (updated for 8.0)
                        var results = searchProvider.Find(
                            filter.And().OrderBy(new SortableField("sortOrder", SortType.Int)).Compile());

                        if (results.Any())
                        {
                            // var medias = results.Select(ConvertFromSearchResult);
                            var medias = results.Select(x =>
                            {
                                int nid;
                                if (int.TryParse(x["__NodeId"], out nid) == false && int.TryParse(x["NodeId"], out nid) == false)
                                    throw new Exception("Failed to extract NodeId from search result.");
                                var cacheValues = GetCacheValues(nid, id => ConvertFromSearchResult(x));
                                return CreateFromCacheValues(cacheValues);
                            });

                            return medias;
                        }

                        //if there's no result then return null. Previously we defaulted back to library.GetMedia below
                        //but this will always get called for when we are getting descendents since many items won't have
                        //children and then we are hitting the database again!
                        //So instead we're going to rely on Examine to have the correct results like it should.
                        return Enumerable.Empty<IPublishedContent>();
                    }
                    catch (FileNotFoundException)
                    {
                        //Currently examine is throwing FileNotFound exceptions when we have a loadbalanced filestore and a node is published in umbraco
                        //See this thread: http://examine.cdodeplex.com/discussions/264341
                        //Catch the exception here for the time being, and just fallback to GetMedia
                    }
                }

                //falling back to get media

                var media = library.GetMedia(parentId, true);
                if (media?.Current != null)
                {
                    xpath = media.Current;
                }
                else
                {
                    return Enumerable.Empty<IPublishedContent>();
                }
            }

            var mediaList = new List<IPublishedContent>();

            // this is so bad, really
            var item = xpath.Select("//*[@id='" + parentId + "']");
            if (item.Current == null)
                return Enumerable.Empty<IPublishedContent>();
            var items = item.Current.SelectChildren(XPathNodeType.Element);

            // and this does not work, because... meh
            //var q = "//* [@id='" + parentId + "']/* [@id]";
            //var items = xpath.Select(q);

            foreach (XPathNavigator itemm in items)
            {
                int id;
                if (int.TryParse(itemm.GetAttribute("id", ""), out id) == false)
                    continue; // wtf?
                var captured = itemm;
                var cacheValues = GetCacheValues(id, idd => ConvertFromXPathNavigator(captured));
                mediaList.Add(CreateFromCacheValues(cacheValues));
            }

            ////The xpath might be the whole xpath including the current ones ancestors so we need to select the current node
            //var item = xpath.Select("//*[@id='" + parentId + "']");
            //if (item.Current == null)
            //{
            //    return Enumerable.Empty<IPublishedContent>();
            //}
            //var children = item.Current.SelectChildren(XPathNodeType.Element);

            //foreach(XPathNavigator x in children)
            //{
            //    //NOTE: I'm not sure why this is here, it is from legacy code of ExamineBackedMedia, but
            //    // will leave it here as it must have done something!
            //    if (x.Name != "contents")
            //    {
            //        //make sure it's actually a node, not a property
            //        if (!string.IsNullOrEmpty(x.GetAttribute("path", "")) &&
            //            !string.IsNullOrEmpty(x.GetAttribute("id", "")))
            //        {
            //            mediaList.Add(ConvertFromXPathNavigator(x));
            //        }
            //    }
            //}

            return mediaList;
        }

        /// <summary>
        /// An IPublishedContent that is represented all by a dictionary.
        /// </summary>
        /// <remarks>
        /// This is a helper class and definitely not intended for public use, it expects that all of the values required
        /// to create an IPublishedContent exist in the dictionary by specific aliases.
        /// </remarks>
        internal class DictionaryPublishedContent : PublishedContentBase
        {
            // note: I'm not sure this class fully complies with IPublishedContent rules especially
            // I'm not sure that _properties contains all properties including those without a value,
            // neither that GetProperty will return a property without a value vs. null... @zpqrtbnk

            // List of properties that will appear in the XML and do not match
            // anything in the ContentType, so they must be ignored.
            private static readonly string[] IgnoredKeys = { "version", "isDoc" };

            public DictionaryPublishedContent(
                IDictionary<string, string> valueDictionary,
                Func<int, IPublishedContent> getParent,
                Func<int, XPathNavigator, IEnumerable<IPublishedContent>> getChildren,
                Func<DictionaryPublishedContent, string, IPublishedProperty> getProperty,
                ICacheProvider cacheProvider,
                PublishedContentTypeCache contentTypeCache,
                XPathNavigator nav,
                bool fromExamine)
            {
                if (valueDictionary == null) throw new ArgumentNullException(nameof(valueDictionary));
                if (getParent == null) throw new ArgumentNullException(nameof(getParent));
                if (getProperty == null) throw new ArgumentNullException(nameof(getProperty));

                _getParent = new Lazy<IPublishedContent>(() => getParent(ParentId));
                _getChildren = new Lazy<IEnumerable<IPublishedContent>>(() => getChildren(Id, nav));
                _getProperty = getProperty;
                _cacheProvider = cacheProvider;

                LoadedFromExamine = fromExamine;

                ValidateAndSetProperty(valueDictionary, val => _id = int.Parse(val), "id", "nodeId", "__NodeId"); //should validate the int!
                ValidateAndSetProperty(valueDictionary, val => _key = Guid.Parse(val), "key");
                //ValidateAndSetProperty(valueDictionary, val => _templateId = int.Parse(val), "template", "templateId");
                ValidateAndSetProperty(valueDictionary, val => _sortOrder = int.Parse(val), "sortOrder");
                ValidateAndSetProperty(valueDictionary, val => _name = val, "nodeName", "__nodeName");
                ValidateAndSetProperty(valueDictionary, val => _urlName = val, "urlName");
                ValidateAndSetProperty(valueDictionary, val => _documentTypeAlias = val, "nodeTypeAlias", LuceneIndexer.NodeTypeAliasFieldName);
                ValidateAndSetProperty(valueDictionary, val => _documentTypeId = int.Parse(val), "nodeType");
                //ValidateAndSetProperty(valueDictionary, val => _writerName = val, "writerName");
                ValidateAndSetProperty(valueDictionary, val => _creatorName = val, "creatorName", "writerName"); //this is a bit of a hack fix for: U4-1132
                //ValidateAndSetProperty(valueDictionary, val => _writerId = int.Parse(val), "writerID");
                ValidateAndSetProperty(valueDictionary, val => _creatorId = int.Parse(val), "creatorID", "writerID"); //this is a bit of a hack fix for: U4-1132
                ValidateAndSetProperty(valueDictionary, val => _path = val, "path", "__Path");
                ValidateAndSetProperty(valueDictionary, val => _createDate = ParseDateTimeValue(val), "createDate");
                ValidateAndSetProperty(valueDictionary, val => _updateDate = ParseDateTimeValue(val), "updateDate");
                ValidateAndSetProperty(valueDictionary, val => _level = int.Parse(val), "level");
                ValidateAndSetProperty(valueDictionary, val =>
                {
                    int pId;
                    ParentId = -1;
                    if (int.TryParse(val, out pId))
                    {
                        ParentId = pId;
                    }
                }, "parentID");

                _contentType = contentTypeCache.Get(PublishedItemType.Media, _documentTypeAlias);
                _properties = new Collection<IPublishedProperty>();

                //handle content type properties
                //make sure we create them even if there's no value
                foreach (var propertyType in _contentType.PropertyTypes)
                {
                    var alias = propertyType.Alias;
                    _keysAdded.Add(alias);
                    string value;
                    const bool isPreviewing = false; // false :: never preview a media
                    var property = valueDictionary.TryGetValue(alias, out value) == false || value == null
                        ? new XmlPublishedProperty(propertyType, this, isPreviewing)
                        : new XmlPublishedProperty(propertyType, this, isPreviewing, value);
                    _properties.Add(property);
                }

                //loop through remaining values that haven't been applied
                foreach (var i in valueDictionary.Where(x =>
                    _keysAdded.Contains(x.Key) == false // not already processed
                    && IgnoredKeys.Contains(x.Key) == false)) // not ignorable
                {
                    if (i.Key.InvariantStartsWith("__"))
                    {
                        // no type for that one, dunno how to convert, drop it
                        //IPublishedProperty property = new PropertyResult(i.Key, i.Value, PropertyResultType.CustomProperty);
                        //_properties.Add(property);
                    }
                    else
                    {
                        // this is a property that does not correspond to anything, ignore and log
                        Current.Logger.Warn<PublishedMediaCache>("Dropping property \"" + i.Key + "\" because it does not belong to the content type.");
                    }
                }
            }

            private DateTime ParseDateTimeValue(string val)
            {
                if (LoadedFromExamine == false)
                    return DateTime.Parse(val);

                //we need to parse the date time using Lucene converters
                var ticks = long.Parse(val);
                return new DateTime(ticks);
            }

            /// <summary>
            /// Flag to get/set if this was laoded from examine cache
            /// </summary>
            internal bool LoadedFromExamine { get; }

            //private readonly Func<DictionaryPublishedContent, IPublishedContent> _getParent;
            private readonly Lazy<IPublishedContent> _getParent;
            //private readonly Func<DictionaryPublishedContent, IEnumerable<IPublishedContent>> _getChildren;
            private readonly Lazy<IEnumerable<IPublishedContent>> _getChildren;
            private readonly Func<DictionaryPublishedContent, string, IPublishedProperty> _getProperty;
            private readonly ICacheProvider _cacheProvider;

            /// <summary>
            /// Returns 'Media' as the item type
            /// </summary>
            public override PublishedItemType ItemType => PublishedItemType.Media;

            public override IPublishedContent Parent => _getParent.Value;

            public int ParentId { get; private set; }

            public override int Id => _id;

            public override Guid Key => _key;

            public override int TemplateId => 0;

            public override int SortOrder => _sortOrder;

            public override string Name => _name;

            public override string UrlName => _urlName;

            public override string DocumentTypeAlias => _documentTypeAlias;

            public override int DocumentTypeId => _documentTypeId;

            public override string WriterName => _creatorName;

            public override string CreatorName => _creatorName;

            public override int WriterId => _creatorId;

            public override int CreatorId => _creatorId;

            public override string Path => _path;

            public override DateTime CreateDate => _createDate;

            public override DateTime UpdateDate => _updateDate;

            public override int Level => _level;

            public override bool IsDraft => false;

            public override IEnumerable<IPublishedProperty> Properties => _properties;

            public override IEnumerable<IPublishedContent> Children => _getChildren.Value;

            public override IPublishedProperty GetProperty(string alias)
            {
                return _getProperty(this, alias);
            }

            public override PublishedContentType ContentType => _contentType;

            // override to implement cache
            //   cache at context level, ie once for the whole request
            //   but cache is not shared by requests because we wouldn't know how to clear it
            public override IPublishedProperty GetProperty(string alias, bool recurse)
            {
                if (recurse == false) return GetProperty(alias);

                var key = $"XmlPublishedCache.PublishedMediaCache:RecursiveProperty-{Id}-{alias.ToLowerInvariant()}";
                var cacheProvider = _cacheProvider;
                return cacheProvider.GetCacheItem<IPublishedProperty>(key, () => base.GetProperty(alias, true));
            }

            private readonly List<string> _keysAdded = new List<string>();
            private int _id;
            private Guid _key;
            //private int _templateId;
            private int _sortOrder;
            private string _name;
            private string _urlName;
            private string _documentTypeAlias;
            private int _documentTypeId;
            //private string _writerName;
            private string _creatorName;
            //private int _writerId;
            private int _creatorId;
            private string _path;
            private DateTime _createDate;
            private DateTime _updateDate;
            //private Guid _version;
            private int _level;
            private readonly ICollection<IPublishedProperty> _properties;
            private readonly PublishedContentType _contentType;

            private void ValidateAndSetProperty(IDictionary<string, string> valueDictionary, Action<string> setProperty, params string[] potentialKeys)
            {
                var key = potentialKeys.FirstOrDefault(x => valueDictionary.ContainsKey(x) && valueDictionary[x] != null);
                if (key == null)
                {
                    throw new FormatException("The valueDictionary is not formatted correctly and is missing any of the  '" + string.Join(",", potentialKeys) + "' elements");
                }

                setProperty(valueDictionary[key]);
                _keysAdded.Add(key);
            }
        }

        internal void Resync()
        {
            // clear recursive properties cached by XmlPublishedContent.GetProperty
            // assume that nothing else is going to cache IPublishedProperty items (else would need to do ByKeySearch)
            // NOTE all properties cleared when clearing the content cache (see content cache)
            //_cacheProvider.ClearCacheObjectTypes<IPublishedProperty>();
            //_cacheProvider.ClearCacheByKeySearch("XmlPublishedCache.PublishedMediaCache:RecursiveProperty-");
        }

        #region Content types

        public override PublishedContentType GetContentType(int id)
        {
            return _contentTypeCache.Get(PublishedItemType.Media, id);
        }

        public override PublishedContentType GetContentType(string alias)
        {
            return _contentTypeCache.Get(PublishedItemType.Media, alias);
        }

        public override IEnumerable<IPublishedContent> GetByContentType(PublishedContentType contentType)
        {
            throw new NotImplementedException();
        }

        #endregion

        // REFACTORING

        // caching the basic atomic values - and the parent id
        // but NOT caching actual parent nor children and NOT even
        // the list of children ids - BUT caching the path

        internal class CacheValues
        {
            public IDictionary<string, string> Values { get; set; }
            public XPathNavigator XPath { get; set; }
            public bool FromExamine { get; set; }
        }

        public const string PublishedMediaCacheKey = "MediaCacheMeh.";
        private const int PublishedMediaCacheTimespanSeconds = 4 * 60; // 4 mins
        private static TimeSpan _publishedMediaCacheTimespan;
        private static bool _publishedMediaCacheEnabled;

        private static void InitializeCacheConfig()
        {
            var value = ConfigurationManager.AppSettings["Umbraco.PublishedMediaCache.Seconds"];
            int seconds;
            if (int.TryParse(value, out seconds) == false)
                seconds = PublishedMediaCacheTimespanSeconds;
            if (seconds > 0)
            {
                _publishedMediaCacheEnabled = true;
                _publishedMediaCacheTimespan = TimeSpan.FromSeconds(seconds);
            }
            else
            {
                _publishedMediaCacheEnabled = false;
            }
        }

        internal IPublishedContent CreateFromCacheValues(CacheValues cacheValues)
        {
            var content = new DictionaryPublishedContent(
                cacheValues.Values,
                parentId => parentId < 0 ? null : GetUmbracoMedia(parentId),
                GetChildrenMedia,
                GetProperty,
                _cacheProvider,
                _contentTypeCache,
                cacheValues.XPath, // though, outside of tests, that should be null
                cacheValues.FromExamine
            );
            return content.CreateModel();
        }

        private static CacheValues GetCacheValues(int id, Func<int, CacheValues> func)
        {
            if (_publishedMediaCacheEnabled == false)
                return func(id);

            var cache = Current.ApplicationCache.RuntimeCache;
            var key = PublishedMediaCacheKey + id;
            return (CacheValues)cache.GetCacheItem(key, () => func(id), _publishedMediaCacheTimespan);
        }

        internal static void ClearCache(int id)
        {
            var cache = Current.ApplicationCache.RuntimeCache;
            var sid = id.ToString();
            var key = PublishedMediaCacheKey + sid;

            // we do clear a lot of things... but the cache refresher is somewhat
            // convoluted and it's hard to tell what to clear exactly ;-(

            // clear the parent - NOT (why?)
            //var exist = (CacheValues) cache.GetCacheItem(key);
            //if (exist != null)
            //    cache.ClearCacheItem(PublishedMediaCacheKey + GetValuesValue(exist.Values, "parentID"));

            // clear the item
            cache.ClearCacheItem(key);

            // clear all children - in case we moved and their path has changed
            var fid = "/" + sid + "/";
            cache.ClearCacheObjectTypes<CacheValues>((k, v) =>
                GetValuesValue(v.Values, "path", "__Path").Contains(fid));
        }

        private static string GetValuesValue(IDictionary<string, string> d, params string[] keys)
        {
            string value = null;
            var ignored = keys.Any(x => d.TryGetValue(x, out value));
            return value ?? "";
        }
    }
}

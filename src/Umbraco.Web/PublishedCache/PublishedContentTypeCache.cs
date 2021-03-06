﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Services;

namespace Umbraco.Web.PublishedCache
{
    /// <summary>
    /// Represents a content type cache.
    /// </summary>
    /// <remarks>This cache is not snapshotted, so it refreshes any time things change.</remarks>
    public class PublishedContentTypeCache
    {
        private readonly Dictionary<string, PublishedContentType> _typesByAlias = new Dictionary<string, PublishedContentType>();
        private readonly Dictionary<int, PublishedContentType> _typesById = new Dictionary<int, PublishedContentType>();
        private readonly IContentTypeService _contentTypeService;
        private readonly IMediaTypeService _mediaTypeService;
        private readonly IMemberTypeService _memberTypeService;
        private readonly IPublishedContentTypeFactory _publishedContentTypeFactory;
        private readonly ILogger _logger;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        // default ctor
        internal PublishedContentTypeCache(IContentTypeService contentTypeService, IMediaTypeService mediaTypeService, IMemberTypeService memberTypeService, IPublishedContentTypeFactory publishedContentTypeFactory, ILogger logger)
        {
            _contentTypeService = contentTypeService;
            _mediaTypeService = mediaTypeService;
            _memberTypeService = memberTypeService;
            _logger = logger;
            _publishedContentTypeFactory = publishedContentTypeFactory;
        }

        // for unit tests ONLY
        internal PublishedContentTypeCache(ILogger logger, IPublishedContentTypeFactory publishedContentTypeFactory)
        {
            _logger = logger;
            _publishedContentTypeFactory = publishedContentTypeFactory;
        }

        // note: cache clearing is performed by XmlStore

        /// <summary>
        /// Clears all cached content types.
        /// </summary>
        public void ClearAll()
        {
            _logger.Debug<PublishedContentTypeCache>("Clear all.");

            try
            {
                _lock.EnterWriteLock();

                _typesByAlias.Clear();
                _typesById.Clear();
            }
            finally
            {
                if (_lock.IsWriteLockHeld)
                    _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Clears a cached content type.
        /// </summary>
        /// <param name="id">An identifier.</param>
        public void ClearContentType(int id)
        {
            _logger.Debug<PublishedContentTypeCache>("Clear content type w/id {0}.", () => id);

            try
            {
                _lock.EnterUpgradeableReadLock();

                if (_typesById.TryGetValue(id, out var type) == false)
                    return;

                try
                {
                    _lock.EnterWriteLock();

                    _typesByAlias.Remove(GetAliasKey(type));
                    _typesById.Remove(id);
                }
                finally
                {
                    if (_lock.IsWriteLockHeld)
                        _lock.ExitWriteLock();
                }
            }
            finally
            {
                if (_lock.IsUpgradeableReadLockHeld)
                    _lock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Clears all cached content types referencing a data type.
        /// </summary>
        /// <param name="id">A data type identifier.</param>
        public void ClearDataType(int id)
        {
            _logger.Debug<PublishedContentTypeCache>("Clear data type w/id {0}.", () => id);

            // there is no recursion to handle here because a PublishedContentType contains *all* its
            // properties ie both its own properties and those that were inherited (it's based upon an
            // IContentTypeComposition) and so every PublishedContentType having a property based upon
            // the cleared data type, be it local or inherited, will be cleared.

            try
            {
                _lock.EnterWriteLock();

                var toRemove = _typesById.Values.Where(x => x.PropertyTypes.Any(xx => xx.DataType.Id == id)).ToArray();
                foreach (var type in toRemove)
                {
                    _typesByAlias.Remove(GetAliasKey(type));
                    _typesById.Remove(type.Id);
                }
            }
            finally
            {
                if (_lock.IsWriteLockHeld)
                    _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Gets a published content type.
        /// </summary>
        /// <param name="itemType">An item type.</param>
        /// <param name="alias">An alias.</param>
        /// <returns>The published content type corresponding to the item type and alias.</returns>
        public PublishedContentType Get(PublishedItemType itemType, string alias)
        {
            var aliasKey = GetAliasKey(itemType, alias);

            try
            {
                _lock.EnterUpgradeableReadLock();

                if (_typesByAlias.TryGetValue(aliasKey, out var type))
                    return type;

                type = CreatePublishedContentType(itemType, alias);

                try
                {
                    _lock.EnterWriteLock();

                    return _typesByAlias[aliasKey] = _typesById[type.Id] = type;
                }
                finally
                {
                    if (_lock.IsWriteLockHeld)
                        _lock.ExitWriteLock();
                }
            }
            finally
            {
                if (_lock.IsUpgradeableReadLockHeld)
                    _lock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Gets a published content type.
        /// </summary>
        /// <param name="itemType">An item type.</param>
        /// <param name="id">An identifier.</param>
        /// <returns>The published content type corresponding to the item type and identifier.</returns>
        public PublishedContentType Get(PublishedItemType itemType, int id)
        {
            try
            {
                _lock.EnterUpgradeableReadLock();

                if (_typesById.TryGetValue(id, out var type))
                    return type;

                type = CreatePublishedContentType(itemType, id);

                try
                {
                    _lock.EnterWriteLock();

                    return _typesByAlias[GetAliasKey(type)] = _typesById[type.Id] = type;
                }
                finally
                {
                    if (_lock.IsWriteLockHeld)
                        _lock.ExitWriteLock();
                }
            }
            finally
            {
                if (_lock.IsUpgradeableReadLockHeld)
                    _lock.ExitUpgradeableReadLock();
            }
        }

        private PublishedContentType CreatePublishedContentType(PublishedItemType itemType, string alias)
        {
            if (GetPublishedContentTypeByAlias != null)
                return GetPublishedContentTypeByAlias(alias);

            IContentTypeComposition contentType;
            switch (itemType)
            {
                case PublishedItemType.Content:
                    contentType = _contentTypeService.Get(alias);
                    break;
                case PublishedItemType.Media:
                    contentType = _mediaTypeService.Get(alias);
                    break;
                case PublishedItemType.Member:
                    contentType = _memberTypeService.Get(alias);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(itemType));
            }

            if (contentType == null)
                throw new Exception($"ContentTypeService failed to find a {itemType.ToString().ToLower()} type with alias \"{alias}\".");

            return _publishedContentTypeFactory.CreateContentType(contentType);
        }

        private PublishedContentType CreatePublishedContentType(PublishedItemType itemType, int id)
        {
            if (GetPublishedContentTypeById != null)
                return GetPublishedContentTypeById(id);

            IContentTypeComposition contentType;
            switch (itemType)
            {
                case PublishedItemType.Content:
                    contentType = _contentTypeService.Get(id);
                    break;
                case PublishedItemType.Media:
                    contentType = _mediaTypeService.Get(id);
                    break;
                case PublishedItemType.Member:
                    contentType = _memberTypeService.Get(id);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(itemType));
            }

            if (contentType == null)
                throw new Exception($"ContentTypeService failed to find a {itemType.ToString().ToLower()} type with id {id}.");

            return _publishedContentTypeFactory.CreateContentType(contentType);
        }

        // for unit tests - changing the callback must reset the cache obviously
        private Func<string, PublishedContentType> _getPublishedContentTypeByAlias;
        internal Func<string, PublishedContentType> GetPublishedContentTypeByAlias
        {
            get => _getPublishedContentTypeByAlias;
            set
            {
                try
                {
                    _lock.EnterWriteLock();

                    _typesByAlias.Clear();
                    _typesById.Clear();
                    _getPublishedContentTypeByAlias = value;
                }
                finally
                {
                    if (_lock.IsWriteLockHeld)
                        _lock.ExitWriteLock();
                }
            }
        }

        // for unit tests - changing the callback must reset the cache obviously
        private Func<int, PublishedContentType> _getPublishedContentTypeById;
        internal Func<int, PublishedContentType> GetPublishedContentTypeById
        {
            get => _getPublishedContentTypeById;
            set
            {
                try
                {
                    _lock.EnterWriteLock();

                    _typesByAlias.Clear();
                    _typesById.Clear();
                    _getPublishedContentTypeById = value;
                }
                finally
                {
                    if (_lock.IsWriteLockHeld)
                        _lock.ExitWriteLock();
                }
            }
        }

        private static string GetAliasKey(PublishedItemType itemType, string alias)
        {
            string k;

            switch (itemType)
            {
                case PublishedItemType.Content:
                    k = "c";
                    break;
                case PublishedItemType.Media:
                    k = "m";
                    break;
                case PublishedItemType.Member:
                    k = "m";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(itemType));
            }

            return k + ":" + alias;
        }

        private static string GetAliasKey(PublishedContentType contentType)
        {
            return GetAliasKey(contentType.ItemType, contentType.Alias);
        }
    }
}

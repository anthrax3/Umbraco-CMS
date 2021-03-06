﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Routing;
using LightInject;
using Moq;
using NUnit.Framework;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.Events;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Persistence.Repositories.Implement;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.Services;
using Umbraco.Core.Services.Implement;
using Umbraco.Core.Sync;
using Umbraco.Tests.TestHelpers;
using Umbraco.Tests.Testing;
using Umbraco.Web;
using Umbraco.Web.Cache;
using Umbraco.Web.PublishedCache;
using Umbraco.Web.PublishedCache.NuCache;
using Umbraco.Web.Routing;
using Umbraco.Web.Security;

namespace Umbraco.Tests.Scoping
{
    [TestFixture]
    [UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, PublishedRepositoryEvents = true)]
    public class ScopedNuCacheTests : TestWithDatabaseBase
    {
        private CacheRefresherComponent _cacheRefresher;

        protected override void Compose()
        {
            base.Compose();

            // the cache refresher component needs to trigger to refresh caches
            // but then, it requires a lot of plumbing ;(
            // fixme - and we cannot inject a DistributedCache yet
            // so doing all this mess
            Container.RegisterSingleton<IServerMessenger, ScopedXmlTests.LocalServerMessenger>();
            Container.RegisterSingleton(f => Mock.Of<IServerRegistrar>());
            Container.RegisterCollectionBuilder<CacheRefresherCollectionBuilder>()
                .Add(f => f.TryGetInstance<TypeLoader>().GetCacheRefreshers());
        }

        public override void TearDown()
        {
            base.TearDown();

            _cacheRefresher?.Unbind();
            _cacheRefresher = null;

            _onPublishedAssertAction = null;
            ContentService.Published -= OnPublishedAssert;
        }

        private void OnPublishedAssert(IContentService sender, PublishEventArgs<IContent> args)
        {
            _onPublishedAssertAction?.Invoke();
        }

        private Action _onPublishedAssertAction;

        protected override IPublishedSnapshotService CreatePublishedSnapshotService()
        {
            var options = new PublishedSnapshotService.Options { IgnoreLocalDb = true };
            var publishedSnapshotAccessor = new UmbracoContextPublishedSnapshotAccessor(Umbraco.Web.Composing.Current.UmbracoContextAccessor);
            var runtimeStateMock = new Mock<IRuntimeState>();
            runtimeStateMock.Setup(x => x.Level).Returns(() => RuntimeLevel.Run);

            var contentTypeFactory = new PublishedContentTypeFactory(Mock.Of<IPublishedModelFactory>(), new PropertyValueConverterCollection(Array.Empty<IPropertyValueConverter>()), Mock.Of<IDataTypeService>());
            var documentRepository = Mock.Of<IDocumentRepository>();
            var mediaRepository = Mock.Of<IMediaRepository>();
            var memberRepository = Mock.Of<IMemberRepository>();

            return new PublishedSnapshotService(
                options,
                null,
                runtimeStateMock.Object,
                ServiceContext,
                contentTypeFactory,
                publishedSnapshotAccessor,
                Logger,
                ScopeProvider,
                documentRepository, mediaRepository, memberRepository);
        }

        protected UmbracoContext GetUmbracoContextNu(string url, int templateId = 1234, RouteData routeData = null, bool setSingleton = false, IUmbracoSettingsSection umbracoSettings = null, IEnumerable<IUrlProvider> urlProviders = null)
        {
            // ensure we have a PublishedSnapshotService
            var service = PublishedSnapshotService as PublishedSnapshotService;

            var httpContext = GetHttpContextFactory(url, routeData).HttpContext;

            var umbracoContext = new UmbracoContext(
                httpContext,
                service,
                new WebSecurity(httpContext, Current.Services.UserService),
                umbracoSettings ?? SettingsForTests.GetDefault(),
                urlProviders ?? Enumerable.Empty<IUrlProvider>());

            if (setSingleton)
                Umbraco.Web.Composing.Current.UmbracoContextAccessor.UmbracoContext = umbracoContext;

            return umbracoContext;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestScope(bool complete)
        {
            var umbracoContext = GetUmbracoContextNu("http://example.com/", setSingleton: true);

            // wire cache refresher
            _cacheRefresher = new CacheRefresherComponent(true);
            _cacheRefresher.Initialize(new DistributedCache());

            // create document type, document
            var contentType = new ContentType(-1) { Alias = "CustomDocument", Name = "Custom Document" };
            Current.Services.ContentTypeService.Save(contentType);
            var item = new Content("name", -1, contentType);

            // event handler
            var evented = 0;
            _onPublishedAssertAction = () =>
            {
                evented++;

                var e = umbracoContext.ContentCache.GetById(item.Id);

                // during events, due to LiveSnapshot, we see the changes
                Assert.IsNotNull(e);
                Assert.AreEqual("changed", e.Name);
            };

            using (var scope = ScopeProvider.CreateScope())
            {
                item.PublishValues();
                Current.Services.ContentService.SaveAndPublish(item);
                scope.Complete();
            }

            // been created
            var x = umbracoContext.ContentCache.GetById(item.Id);
            Assert.IsNotNull(x);
            Assert.AreEqual("name", x.Name);

            ContentService.Published += OnPublishedAssert;

            using (var scope = ScopeProvider.CreateScope())
            {
                item.Name = "changed";
                item.PublishValues();
                Current.Services.ContentService.SaveAndPublish(item);

                if (complete)
                    scope.Complete();
            }

            // only 1 event occuring because we are publishing twice for the same event for
            // the same object and the scope deduplicates the events (uses the latest)
            Assert.AreEqual(complete ? 1 : 0, evented);

            // after the scope,
            // if completed, we see the changes
            // else changes have been rolled back
            x = umbracoContext.ContentCache.GetById(item.Id);
            Assert.IsNotNull(x);
            Assert.AreEqual(complete ? "changed" : "name", x.Name);
        }
    }
}

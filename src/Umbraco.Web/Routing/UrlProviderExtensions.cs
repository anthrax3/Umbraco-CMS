﻿using System;
using System.Collections.Generic;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Core;
using Umbraco.Core.Logging;
using LightInject;
using Umbraco.Web.Composing;

namespace Umbraco.Web.Routing
{
    internal static class UrlProviderExtensions
    {
        // fixme inject
        private static ILocalizedTextService TextService => Current.Services.TextService;
        private static IContentService ContentService => Current.Services.ContentService;
        private static ILogger Logger => Current.Logger;

        /// <summary>
        /// Gets the URLs for the content item
        /// </summary>
        /// <param name="content"></param>
        /// <param name="umbracoContext"></param>
        /// <returns></returns>
        /// <remarks>
        /// Use this when displaying URLs, if there are errors genertaing the urls the urls themselves will
        /// contain the errors.
        /// </remarks>
        public static IEnumerable<string> GetContentUrls(this IContent content, UmbracoContext umbracoContext)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (umbracoContext == null) throw new ArgumentNullException(nameof(umbracoContext));

            var urls = new List<string>();

            if (content.Published == false)
            {
                urls.Add(TextService.Localize("content/itemNotPublished"));
                return urls;
            }

            string url;
            var urlProvider = umbracoContext.UrlProvider;
            try
            {
                url = urlProvider.GetUrl(content.Id);
            }
            catch (Exception e)
            {
                Logger.Error<UrlProvider>("GetUrl exception.", e);
                url = "#ex";
            }
            if (url == "#")
            {
                // document as a published version yet it's url is "#" => a parent must be
                // unpublished, walk up the tree until we find it, and report.
                var parent = content;
                do
                {
                    parent = parent.ParentId > 0 ? parent.Parent(ContentService) : null;
                }
                while (parent != null && parent.Published);

                urls.Add(parent == null
                    ? TextService.Localize("content/parentNotPublishedAnomaly") // oops - internal error
                    : TextService.Localize("content/parentNotPublished", new[] { parent.Name }));
            }
            else if (url == "#ex")
            {
                urls.Add(TextService.Localize("content/getUrlException"));
            }
            else
            {
                // test for collisions
                var uri = new Uri(url.TrimEnd('/'), UriKind.RelativeOrAbsolute);
                if (uri.IsAbsoluteUri == false) uri = uri.MakeAbsolute(UmbracoContext.Current.CleanedUmbracoUrl);
                uri = UriUtility.UriToUmbraco(uri);
                var r = Core.Composing.Current.Container.GetInstance<PublishedRouter>(); // fixme inject or ?
                var pcr = r.CreateRequest(UmbracoContext.Current, uri);
                r.TryRouteRequest(pcr);

                if (pcr.HasPublishedContent == false)
                {
                    urls.Add(TextService.Localize("content/routeError", new[] { "(error)" }));
                }
                else if (pcr.PublishedContent.Id != content.Id)
                {
                    var o = pcr.PublishedContent;
                    string s;
                    if (o == null)
                    {
                        s = "(unknown)";
                    }
                    else
                    {
                        var l = new List<string>();
                        while (o != null)
                        {
                            l.Add(o.Name);
                            o = o.Parent;
                        }
                        l.Reverse();
                        s = "/" + string.Join("/", l) + " (id=" + pcr.PublishedContent.Id + ")";

                    }
                    urls.Add(TextService.Localize("content/routeError", s));
                }
                else
                {
                    urls.Add(url);
                    urls.AddRange(urlProvider.GetOtherUrls(content.Id));
                }
            }
            return urls;
        }
    }
}

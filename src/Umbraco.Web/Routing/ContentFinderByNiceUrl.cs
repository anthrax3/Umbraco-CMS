﻿using Umbraco.Core.Logging;
using Umbraco.Core;
using Umbraco.Core.Models.PublishedContent;

namespace Umbraco.Web.Routing
{
    /// <summary>
    /// Provides an implementation of <see cref="IContentFinder"/> that handles page nice urls.
    /// </summary>
    /// <remarks>
    /// <para>Handles <c>/foo/bar</c> where <c>/foo/bar</c> is the nice url of a document.</para>
    /// </remarks>
    public class ContentFinderByNiceUrl : IContentFinder
    {
        protected ILogger Logger { get; }

        public ContentFinderByNiceUrl(ILogger logger)
        {
            Logger = logger;
        }

        /// <summary>
        /// Tries to find and assign an Umbraco document to a <c>PublishedContentRequest</c>.
        /// </summary>
        /// <param name="frequest">The <c>PublishedContentRequest</c>.</param>
        /// <returns>A value indicating whether an Umbraco document was found and assigned.</returns>
        public virtual bool TryFindContent(PublishedRequest frequest)
        {
            string route;
            if (frequest.HasDomain)
                route = frequest.Domain.ContentId + DomainHelper.PathRelativeToDomain(frequest.Domain.Uri, frequest.Uri.GetAbsolutePathDecoded());
            else
                route = frequest.Uri.GetAbsolutePathDecoded();

            var node = FindContent(frequest, route);
            return node != null;
        }

        /// <summary>
        /// Tries to find an Umbraco document for a <c>PublishedContentRequest</c> and a route.
        /// </summary>
        /// <param name="docreq">The document request.</param>
        /// <param name="route">The route.</param>
        /// <returns>The document node, or null.</returns>
        protected IPublishedContent FindContent(PublishedRequest docreq, string route)
        {
            Logger.Debug<ContentFinderByNiceUrl>("Test route \"{0}\"", () => route);

            var node = docreq.UmbracoContext.ContentCache.GetByRoute(route);
            if (node != null)
            {
                docreq.PublishedContent = node;
                Logger.Debug<ContentFinderByNiceUrl>("Got content, id={0}", () => node.Id);
            }
            else
            {
                Logger.Debug<ContentFinderByNiceUrl>("No match.");
            }

            return node;
        }
    }
}

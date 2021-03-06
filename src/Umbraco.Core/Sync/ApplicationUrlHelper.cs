﻿using System;
using System.Web;
using Umbraco.Core.Configuration;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.Composing;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;

namespace Umbraco.Core.Sync
{
    /// <summary>
    /// A helper used to determine the current server umbraco application url.
    /// </summary>
    public static class ApplicationUrlHelper
    {
        // because we cannot logger.Info<ApplicationUrlHelper> because type is static
        private static readonly Type TypeOfApplicationUrlHelper = typeof(ApplicationUrlHelper);

        /// <summary>
        /// Gets or sets a custom provider for the umbraco application url.
        /// </summary>
        /// <remarks>
        /// <para>Receives the current request as a parameter, and it may be null. Must return a properly
        /// formatted url with scheme and umbraco dir and no trailing slash eg "http://www.mysite.com/umbraco",
        /// or <c>null</c>. To be used in auto-load-balancing scenarios where the application url is not
        /// in config files but is determined programmatically.</para>
        /// <para>Must be assigned before resolution is frozen.</para>
        /// </remarks>
        // FIXME need another way to do it, eg an interface, injected!
        public static Func<HttpRequestBase, string> ApplicationUrlProvider { get; set; }

        internal static string GetApplicationUrl(ILogger logger, HttpRequestBase request = null, IUmbracoSettingsSection settings = null)
        {
            var umbracoApplicationUrl = TryGetApplicationUrl(settings ?? UmbracoConfig.For.UmbracoSettings(), logger);
            if (umbracoApplicationUrl != null)
                return umbracoApplicationUrl;

            umbracoApplicationUrl = ApplicationUrlProvider?.Invoke(request);
            if (string.IsNullOrWhiteSpace(umbracoApplicationUrl) == false)
            {
                umbracoApplicationUrl = umbracoApplicationUrl.TrimEnd('/');
                logger.Info(TypeOfApplicationUrlHelper, "ApplicationUrl: " + umbracoApplicationUrl + " (provider)");
                return umbracoApplicationUrl;
            }

            if (request == null) return null;

            umbracoApplicationUrl = GetApplicationUrlFromCurrentRequest(request);
            logger.Info(TypeOfApplicationUrlHelper, "ApplicationUrl: " + umbracoApplicationUrl + " (UmbracoModule request)");
            return umbracoApplicationUrl;
        }

        internal static string TryGetApplicationUrl(IUmbracoSettingsSection settings, ILogger logger)
        {
            // try umbracoSettings:settings/web.routing/@umbracoApplicationUrl
            // which is assumed to:
            // - end with SystemDirectories.Umbraco
            // - contain a scheme
            // - end or not with a slash, it will be taken care of
            // eg "http://www.mysite.com/umbraco"
            var url = settings.WebRouting.UmbracoApplicationUrl;
            if (url.IsNullOrWhiteSpace() == false)
            {
                var umbracoApplicationUrl = url.TrimEnd('/');
                logger.Info(TypeOfApplicationUrlHelper, "ApplicationUrl: " + umbracoApplicationUrl + " (using web.routing/@umbracoApplicationUrl)");
                return umbracoApplicationUrl;
            }

            // try umbracoSettings:settings/scheduledTasks/@baseUrl
            // which is assumed to:
            // - end with SystemDirectories.Umbraco
            // - NOT contain any scheme (because, legacy)
            // - end or not with a slash, it will be taken care of
            // eg "mysite.com/umbraco"
            url = settings.ScheduledTasks.BaseUrl;
            if (url.IsNullOrWhiteSpace() == false)
            {
                var ssl = GlobalSettings.UseSSL ? "s" : "";
                url = "http" + ssl + "://" + url;
                var umbracoApplicationUrl = url.TrimEnd('/');
                logger.Info(TypeOfApplicationUrlHelper, "ApplicationUrl: " + umbracoApplicationUrl + " (using scheduledTasks/@baseUrl)");
                return umbracoApplicationUrl;
            }

            // try the server registrar
            // which is assumed to return a url that:
            // - end with SystemDirectories.Umbraco
            // - contain a scheme
            // - end or not with a slash, it will be taken care of
            // eg "http://www.mysite.com/umbraco"
            url = Current.ServerRegistrar.GetCurrentServerUmbracoApplicationUrl();
            if (url.IsNullOrWhiteSpace() == false)
            {
                var umbracoApplicationUrl = url.TrimEnd('/');
                logger.Info(TypeOfApplicationUrlHelper, "ApplicationUrl: " + umbracoApplicationUrl + " (IServerRegistrar)");
                return umbracoApplicationUrl;
            }

            // else give up...
            return null;
        }

        private static string GetApplicationUrlFromCurrentRequest(HttpRequestBase request)
        {
            // if (HTTP and SSL not required) or (HTTPS and SSL required),
            //  use ports from request
            // otherwise,
            //  if non-standard ports used,
            //  user may need to set umbracoApplicationUrl manually per
            //  http://our.umbraco.org/documentation/Using-Umbraco/Config-files/umbracoSettings/#ScheduledTasks
            var port = (request.IsSecureConnection == false && GlobalSettings.UseSSL == false)
                        || (request.IsSecureConnection && GlobalSettings.UseSSL)
                ? ":" + request.ServerVariables["SERVER_PORT"]
                : "";

            var useSsl = GlobalSettings.UseSSL || port == "443";
            var ssl = useSsl ? "s" : ""; // force, whatever the first request
            var url = "http" + ssl + "://" + request.ServerVariables["SERVER_NAME"] + port + IOHelper.ResolveUrl(SystemDirectories.Umbraco);

            return url.TrimEnd('/');
        }
    }
}

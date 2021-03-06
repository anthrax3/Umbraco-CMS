﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Migrations.Install;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Web.Composing;
using Umbraco.Web.Install.InstallSteps;
using Umbraco.Web.Install.Models;

namespace Umbraco.Web.Install
{
    internal class InstallHelper
    {
        private readonly DatabaseBuilder _databaseBuilder;
        private readonly HttpContextBase _httpContext;
        private readonly ILogger _logger;
        private InstallationType? _installationType;

        internal InstallHelper(UmbracoContext umbracoContext, DatabaseBuilder databaseBuilder, ILogger logger)
        {
            _httpContext = umbracoContext.HttpContext;
            _logger = logger;
            _databaseBuilder = databaseBuilder;
        }

        /// <summary>
        /// Get the installer steps
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// The step order returned here is how they will appear on the front-end if they have views assigned
        /// </remarks>
        public IEnumerable<InstallSetupStep> GetAllSteps()
        {
            return new List<InstallSetupStep>
            {
                // fixme - should NOT use current everywhere here - inject!
                new NewInstallStep(_httpContext, Current.Services.UserService, _databaseBuilder),
                new UpgradeStep(_databaseBuilder),
                new FilePermissionsStep(),
                new MajorVersion7UpgradeReport(_databaseBuilder, Current.RuntimeState, Current.SqlContext, Current.ScopeProvider),
                new Version73FileCleanup(_httpContext, _logger),
                new ConfigureMachineKey(),
                new DatabaseConfigureStep(_databaseBuilder),
                new DatabaseInstallStep(_databaseBuilder, Current.RuntimeState, Current.Logger),
                new DatabaseUpgradeStep(_databaseBuilder, Current.RuntimeState, Current.Logger),
                new StarterKitDownloadStep(Current.Services.ContentService, this, Current.UmbracoContext.Security),
                new StarterKitInstallStep(_httpContext),
                new StarterKitCleanupStep(),
                new SetUmbracoVersionStep(_httpContext, _logger, this)
            };
        }

        /// <summary>
        /// Returns the steps that are used only for the current installation type
        /// </summary>
        /// <returns></returns>
        public IEnumerable<InstallSetupStep> GetStepsForCurrentInstallType()
        {
            return GetAllSteps().Where(x => x.InstallTypeTarget.HasFlag(GetInstallationType()));
        }

        public InstallationType GetInstallationType()
        {
            return _installationType ?? (_installationType = IsBrandNewInstall ? InstallationType.NewInstall : InstallationType.Upgrade).Value;
        }

        internal static void DeleteLegacyInstaller()
        {
            if (Directory.Exists(IOHelper.MapPath(SystemDirectories.Install)))
            {
                if (Directory.Exists(IOHelper.MapPath("~/app_data/temp/install_backup")))
                {
                    //this means the backup already exists with files but there's no files in it, so we'll delete the backup and re-run it
                    if (Directory.GetFiles(IOHelper.MapPath("~/app_data/temp/install_backup")).Any() == false)
                    {
                        Directory.Delete(IOHelper.MapPath("~/app_data/temp/install_backup"), true);
                        Directory.Move(IOHelper.MapPath(SystemDirectories.Install), IOHelper.MapPath("~/app_data/temp/install_backup"));
                    }
                }
                else
                {
                    Directory.Move(IOHelper.MapPath(SystemDirectories.Install), IOHelper.MapPath("~/app_data/temp/install_backup"));
                }
            }

            if (Directory.Exists(IOHelper.MapPath("~/Areas/UmbracoInstall")))
            {
                Directory.Delete(IOHelper.MapPath("~/Areas/UmbracoInstall"), true);
            }
        }

        internal void InstallStatus(bool isCompleted, string errorMsg)
        {
            try
            {
                var userAgent = _httpContext.Request.UserAgent;

                // Check for current install Id
                var installId = Guid.NewGuid();

                var installCookie = _httpContext.Request.GetPreviewCookieValue();
                if (string.IsNullOrEmpty(installCookie) == false)
                {
                    if (Guid.TryParse(installCookie, out installId))
                    {
                        // check that it's a valid Guid
                        if (installId == Guid.Empty)
                            installId = Guid.NewGuid();
                    }
                }
                _httpContext.Response.Cookies.Set(new HttpCookie("umb_installId", "1"));

                var dbProvider = string.Empty;
                if (IsBrandNewInstall == false)
                {
                    // we don't have DatabaseProvider anymore... doing it differently
                    //dbProvider = ApplicationContext.Current.DatabaseContext.DatabaseProvider.ToString();
                    dbProvider = GetDbProviderString(Current.SqlContext);
                }

                var check = new org.umbraco.update.CheckForUpgrade();
                check.Install(installId,
                    IsBrandNewInstall == false,
                    isCompleted,
                    DateTime.Now,
                    UmbracoVersion.Current.Major,
                    UmbracoVersion.Current.Minor,
                    UmbracoVersion.Current.Build,
                    UmbracoVersion.CurrentComment,
                    errorMsg,
                    userAgent,
                    dbProvider);
            }
            catch (Exception ex)
            {
                Current.Logger.Error<InstallHelper>("An error occurred in InstallStatus trying to check upgrades", ex);
            }
        }

        internal static string GetDbProviderString(ISqlContext sqlContext)
        {
            var dbProvider = string.Empty;

            // we don't have DatabaseProvider anymore...
            //dbProvider = ApplicationContext.Current.DatabaseContext.DatabaseProvider.ToString();
            //
            // doing it differently
            var syntax = sqlContext.SqlSyntax;
            if (syntax is SqlCeSyntaxProvider)
                dbProvider = "SqlServerCE";
            else if (syntax is MySqlSyntaxProvider)
                dbProvider = "MySql";
            else if (syntax is SqlServerSyntaxProvider)
                dbProvider = (syntax as SqlServerSyntaxProvider).ServerVersion.IsAzure ? "SqlAzure" : "SqlServer";

            return dbProvider;
        }

        /// <summary>
        /// Checks if this is a brand new install meaning that there is no configured version and there is no configured database connection
        /// </summary>
        private bool IsBrandNewInstall
        {
            get
            {
                var databaseSettings = ConfigurationManager.ConnectionStrings[Constants.System.UmbracoConnectionName];
                if (GlobalSettings.ConfigurationStatus.IsNullOrWhiteSpace()
                    && _databaseBuilder.IsConnectionStringConfigured(databaseSettings) == false)
                {
                    //no version or conn string configured, must be a brand new install
                    return true;
                }

                //now we have to check if this is really a new install, the db might be configured and might contain data

                if (_databaseBuilder.IsConnectionStringConfigured(databaseSettings) == false
                    || _databaseBuilder.IsDatabaseConfigured == false)
                {
                    return true;
                }

                return _databaseBuilder.HasSomeNonDefaultUser() == false;
            }
        }

        internal IEnumerable<Package> GetStarterKits()
        {
            var packages = new List<Package>();

            try
            {
                var requestUri = $"http://our.umbraco.org/webapi/StarterKit/Get/?umbracoVersion={UmbracoVersion.Current}";

                using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
                using (var httpClient = new HttpClient())
                using (var response = httpClient.SendAsync(request).Result)
                {
                    packages = response.Content.ReadAsAsync<IEnumerable<Package>>().Result.ToList();
                }
            }
            catch (AggregateException ex)
            {
                Current.Logger.Error<InstallHelper>("Could not download list of available starter kits", ex);
            }

            return packages;
        }
    }
}

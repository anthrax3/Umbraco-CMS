﻿using System;
using System.Threading;
using System.Web;
using Semver;
using Umbraco.Core.Configuration;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.Exceptions;
using Umbraco.Core.Logging;
using Umbraco.Core.Sync;

namespace Umbraco.Core
{
    /// <summary>
    /// Represents the state of the Umbraco runtime.
    /// </summary>
    internal class RuntimeState : IRuntimeState
    {
        private readonly ILogger _logger;
        private readonly Lazy<IServerRegistrar> _serverRegistrar;
        private readonly Lazy<MainDom> _mainDom;
        private RuntimeLevel _level;

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeState"/> class.
        /// </summary>
        /// <param name="logger">A logger.</param>
        /// <param name="serverRegistrar">A (lazy) server registrar.</param>
        /// <param name="mainDom">A (lazy) MainDom.</param>
        public RuntimeState(ILogger logger, Lazy<IServerRegistrar> serverRegistrar, Lazy<MainDom> mainDom)
        {
            _logger = logger;
            _serverRegistrar = serverRegistrar;
            _mainDom = mainDom;
        }

        private IServerRegistrar ServerRegistrar => _serverRegistrar.Value;

        /// <summary>
        /// Gets the application MainDom.
        /// </summary>
        /// <remarks>This is NOT exposed in the interface as MainDom is internal.</remarks>
        public MainDom MainDom => _mainDom.Value;

        /// <summary>
        /// Gets the version of the executing code.
        /// </summary>
        public Version Version => UmbracoVersion.Current;

        /// <summary>
        /// Gets the version comment of the executing code.
        /// </summary>
        public string VersionComment => UmbracoVersion.CurrentComment;

        /// <summary>
        /// Gets the semantic version of the executing code.
        /// </summary>
        public SemVersion SemanticVersion => UmbracoVersion.SemanticVersion;

        /// <summary>
        /// Gets a value indicating whether the application is running in debug mode.
        /// </summary>
        public bool Debug { get; } = GlobalSettings.DebugMode;

        /// <summary>
        /// Gets a value indicating whether the runtime is the current main domain.
        /// </summary>
        public bool IsMainDom => MainDom.IsMainDom;

        /// <summary>
        /// Get the server's current role.
        /// </summary>
        public ServerRole ServerRole => ServerRegistrar.GetCurrentServerRole();

        /// <summary>
        /// Gets the Umbraco application url.
        /// </summary>
        /// <remarks>This is eg "http://www.example.com".</remarks>
        public Uri ApplicationUrl { get; private set; }

        /// <summary>
        /// Gets the Umbraco application virtual path.
        /// </summary>
        /// <remarks>This is either "/" or eg "/virtual".</remarks>
        public string ApplicationVirtualPath { get; } = HttpRuntime.AppDomainAppVirtualPath;

        /// <summary>
        /// Gets the runtime level of execution.
        /// </summary>
        public RuntimeLevel Level
        {
            get => _level;
            internal set { _level = value; if (value == RuntimeLevel.Run) _runLevel.Set(); }
        }

        /// <summary>
        /// Ensures that the <see cref="ApplicationUrl"/> property has a value.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="settings"></param>
        internal void EnsureApplicationUrl(HttpRequestBase request = null, IUmbracoSettingsSection settings = null)
        {
            if (ApplicationUrl != null) return;
            ApplicationUrl = new Uri(ApplicationUrlHelper.GetApplicationUrl(_logger, request, settings));
        }

        private readonly ManualResetEventSlim _runLevel = new ManualResetEventSlim(false);

        /// <summary>
        /// Waits for the runtime level to become RuntimeLevel.Run.
        /// </summary>
        /// <param name="timeout">A timeout.</param>
        /// <returns>True if the runtime level became RuntimeLevel.Run before the timeout, otherwise false.</returns>
        internal bool WaitForRunLevel(TimeSpan timeout)
        {
            return _runLevel.WaitHandle.WaitOne(timeout);
        }

        /// <summary>
        /// Gets the exception that caused the boot to fail.
        /// </summary>
        public BootFailedException BootFailedException { get; internal set; }
    }
}

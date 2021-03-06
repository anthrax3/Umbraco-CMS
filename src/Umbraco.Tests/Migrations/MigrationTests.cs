﻿using System;
using System.Data;
using Semver;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Migrations;
using Umbraco.Core.Migrations.Upgrade;
using Umbraco.Core.Persistence;
using Umbraco.Core.Scoping;
using Umbraco.Core.Services;

namespace Umbraco.Tests.Migrations
{
    public class MigrationTests
    {
        public class TestUpgrader : Upgrader
        {
            private readonly MigrationPlan _plan;

            public TestUpgrader(IScopeProvider scopeProvider, IMigrationBuilder migrationBuilder, IKeyValueService keyValueService, PostMigrationCollection postMigrations, ILogger logger, MigrationPlan plan)
                : base(scopeProvider, migrationBuilder, keyValueService, postMigrations, logger)
            {
                _plan = plan;
            }

            protected override MigrationPlan GetPlan()
            {
                return _plan;
            }

            protected override (SemVersion, SemVersion) GetVersions()
            {
                return (new SemVersion(0), new SemVersion(0));
            }
        }


        public class TestScopeProvider : IScopeProvider
        {
            private readonly IScope _scope;

            public TestScopeProvider(IScope scope)
            {
                _scope = scope;
            }

            public IScope CreateScope(IsolationLevel isolationLevel = IsolationLevel.Unspecified, RepositoryCacheMode repositoryCacheMode = RepositoryCacheMode.Unspecified, IEventDispatcher eventDispatcher = null, bool? scopeFileSystems = null, bool callContext = false, bool autoComplete = false)
            {
                return _scope;
            }

            public IScope CreateDetachedScope(IsolationLevel isolationLevel = IsolationLevel.Unspecified, RepositoryCacheMode repositoryCacheMode = RepositoryCacheMode.Unspecified, IEventDispatcher eventDispatcher = null, bool? scopeFileSystems = null)
            {
                throw new NotImplementedException();
            }

            public void AttachScope(IScope scope, bool callContext = false)
            {
                throw new NotImplementedException();
            }

            public IScope DetachScope()
            {
                throw new NotImplementedException();
            }

            public IScopeContext Context { get; set; }
            public ISqlContext SqlContext { get; set;  }
        }
    }
}

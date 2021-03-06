﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;
using Umbraco.Core.Models.Entities;
using Umbraco.Core.Strings;

namespace Umbraco.Core.Models.Membership
{
    [Obsolete("This should not be used it exists for legacy reasons only, use user groups instead, it will be removed in future versions")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Serializable]
    [DataContract(IsReference = true)]
    internal class UserType : EntityBase, IUserType
    {
        private string _alias;
        private string _name;
        private IEnumerable<string> _permissions;
        private static readonly Lazy<PropertySelectors> Ps = new Lazy<PropertySelectors>();
        private class PropertySelectors
        {
            public readonly PropertyInfo NameSelector = ExpressionHelper.GetPropertyInfo<UserType, string>(x => x.Name);
            public readonly PropertyInfo AliasSelector = ExpressionHelper.GetPropertyInfo<UserType, string>(x => x.Alias);
            public readonly PropertyInfo PermissionsSelector = ExpressionHelper.GetPropertyInfo<UserType, IEnumerable<string>>(x => x.Permissions);
        }

        public UserType(string name, string alias)
        {
            Name = name;
            Alias = alias;
        }

        public UserType()
        {
        }

        [DataMember]
        public string Alias
        {
            get { return _alias; }
            set
            {
                SetPropertyValueAndDetectChanges(
                    value.ToCleanString(CleanStringType.Alias | CleanStringType.UmbracoCase),
                    ref _alias,
                    Ps.Value.AliasSelector);
            }
        }

        [DataMember]
        public string Name
        {
            get { return _name; }
            set { SetPropertyValueAndDetectChanges(value, ref _name, Ps.Value.NameSelector); }
        }

        /// <summary>
        /// The set of default permissions for the user type
        /// </summary>
        /// <remarks>
        /// By default each permission is simply a single char but we've made this an enumerable{string} to support a more flexible permissions structure in the future.
        /// </remarks>
        [DataMember]
        public IEnumerable<string> Permissions
        {
            get { return _permissions; }
            set
            {
                SetPropertyValueAndDetectChanges(value, ref _permissions, Ps.Value.PermissionsSelector,
                    //Custom comparer for enumerable
                    new DelegateEqualityComparer<IEnumerable<string>>(
                        (enum1, enum2) => enum1.UnsortedSequenceEqual(enum2),
                        enum1 => enum1.GetHashCode()));
            }
        }
    }
}

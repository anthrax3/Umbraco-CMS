﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.PropertyEditors;
using umbraco;
using ClientDependency.Core;
using Umbraco.Core.Services;
using Constants = Umbraco.Core.Constants;

namespace Umbraco.Web.PropertyEditors
{
    /// <summary>
    /// A property editor to allow the individual selection of pre-defined items.
    /// </summary>
    /// <remarks>
    /// Due to remaining backwards compatible, this stores the id of the drop down item in the database which is why it is marked
    /// as INT and we have logic in here to ensure it is formatted correctly including ensuring that the string value is published
    /// in cache and not the int ID.
    /// </remarks>
    [DataEditor(Constants.PropertyEditors.Aliases.DropDownList, "Dropdown list", "dropdown", ValueType = ValueTypes.String, Group = "lists", Icon = "icon-indent")]
    public class DropDownPropertyEditor : DropDownWithKeysPropertyEditor
    {
        /// <summary>
        /// The constructor will setup the property editor based on the attribute if one is found
        /// </summary>
        public DropDownPropertyEditor(ILogger logger, ILocalizedTextService textService)
            : base(logger, textService)
        { }

        /// <summary>
        /// We need to override the value editor so that we can ensure the string value is published in cache and not the integer ID value.
        /// </summary>
        /// <returns></returns>
        protected override IDataValueEditor CreateValueEditor() => new PublishValueValueEditor(Attribute, Logger);
    }
}

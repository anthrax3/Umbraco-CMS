﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Umbraco.Core.Models;
using Umbraco.Core.PropertyEditors;

namespace Umbraco.Web.PropertyEditors
{
    /// <summary>
    /// Represents the configuration editor for the tag value editor.
    /// </summary>
    public class TagConfigurationEditor : ConfigurationEditor<TagConfiguration>
    {
        public TagConfigurationEditor(ManifestValidatorCollection validators)
        {
            Fields.Add(new ConfigurationField(new ManifestValueValidator(validators) { ValidationName = "Required" })
            {
                Description = "Define a tag group",
                Key = "group",
                Name = "Tag group",
                PropertyName = nameof(TagConfiguration.Group),
                View = "requiredfield"
            });

            Fields.Add(new ConfigurationField(new ManifestValueValidator(validators) {ValidationName = "Required"})
            {
                Description = "Select whether to store the tags in cache as CSV (default) or as JSON. The only benefits of storage as JSON is that you are able to have commas in a tag value but this will require parsing the json in your views or using a property value converter",
                Key = "storageType",
                Name = "Storage Type",
                PropertyName = nameof(TagConfiguration.StorageType),
                View = "views/propertyeditors/tags/tags.prevalues.html"
            });
        }

        // fixme
        public override IDictionary<string, object> DefaultConfiguration => new Dictionary<string, object>
        {
            {"group", "default"},
            {"storageType", TagsStorageType.Csv.ToString()}
        };

        public override Dictionary<string, object> ToConfigurationEditor(TagConfiguration configuration)
        {
            var dictionary = base.ToConfigurationEditor(configuration);

            // the front-end editor expects the string value of the storage type
            if (!dictionary.TryGetValue("storageType", out var storageType))
                storageType = TagsStorageType.Csv;
            dictionary["storageType"] = storageType.ToString();

            return dictionary;
        }

        public override TagConfiguration FromConfigurationEditor(Dictionary<string, object> editorValues, TagConfiguration configuration)
        {
            // the front-end editor retuns the string value of the storage type
            // pure Json could do with
            // [JsonConverter(typeof(StringEnumConverter))]
            // but here we're only deserializing to object and it's too late

            editorValues["storageType"] = Enum.Parse(typeof(TagsStorageType), (string) editorValues["storageType"]);
            return base.FromConfigurationEditor(editorValues, configuration);
        }
    }
}
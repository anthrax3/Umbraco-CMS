﻿using Umbraco.Core.PropertyEditors;

namespace Umbraco.Web.PropertyEditors
{
    /// <summary>
    /// Represents the configuration for the rich text value editor.
    /// </summary>
    public class RichTextConfiguration
    {
        [ConfigurationField("editor", "Editor", "views/propertyeditors/rte/rte.prevalues.html", HideLabel = true)]
        public string Editor { get; set; }

        [ConfigurationField("hideLabel", "Hide Label", "boolean")]
        public bool HideLabel { get; set; }
    }
}
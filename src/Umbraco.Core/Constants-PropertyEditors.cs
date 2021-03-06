﻿using Umbraco.Core.PropertyEditors;

namespace Umbraco.Core
{
    public static partial class Constants
    {
        /// <summary>
        /// Defines property editors constants.
        /// </summary>
        public static class PropertyEditors
        {
            /// <summary>
            /// Used to prefix generic properties that are internal content properties
            /// </summary>
            public const string InternalGenericPropertiesPrefix = "_umb_";

            /// <summary>
            /// Defines Umbraco built-in property editor aliases.
            /// </summary>
            public static class Aliases
            {
                /// <summary>
                /// CheckBox List.
                /// </summary>
                public const string CheckBoxList = "Umbraco.CheckBoxList";

                /// <summary>
                /// Color Picker.
                /// </summary>
                public const string ColorPicker = "Umbraco.ColorPicker";

                /// <summary>
                /// Content Picker.
                /// </summary>
                public const string ContentPicker = "Umbraco.ContentPicker";

                /// <summary>
                /// Date.
                /// </summary>
                public const string Date = "Umbraco.Date";

                /// <summary>
                /// DateTime.
                /// </summary>
                public const string DateTime = "Umbraco.DateTime";

                /// <summary>
                /// DropDown List.
                /// </summary>
                public const string DropDownList = "Umbraco.DropDown";

                /// <summary>
                /// DropDown List, Publish Keys.
                /// </summary>
                public const string DropdownlistPublishKeys = "Umbraco.DropdownlistPublishingKeys";

                /// <summary>
                /// DropDown List Multiple.
                /// </summary>
                public const string DropDownListMultiple = "Umbraco.DropDownMultiple";

                /// <summary>
                /// DropDown List Multiple, Publish Keys.
                /// </summary>
                public const string DropdownlistMultiplePublishKeys = "Umbraco.DropdownlistMultiplePublishKeys";

                /// <summary>
                /// Folder Browser.
                /// </summary>
                public const string FolderBrowser = "Umbraco.FolderBrowser";

                /// <summary>
                /// Grid.
                /// </summary>
                public const string Grid = "Umbraco.Grid";

                /// <summary>
                /// Image Cropper.
                /// </summary>
                public const string ImageCropper = "Umbraco.ImageCropper";

                /// <summary>
                /// Integer.
                /// </summary>
                public const string Integer = "Umbraco.Integer";

                /// <summary>
                /// Decimal.
                /// </summary>
                public const string Decimal = "Umbraco.Decimal";

                /// <summary>
                /// ListView.
                /// </summary>
                public const string ListView = "Umbraco.ListView";

                /// <summary>
                /// Macro Container.
                /// </summary>
                public const string MacroContainer = "Umbraco.MacroContainer";

                /// <summary>
                /// Media Picker.
                /// </summary>
                public const string MediaPicker = "Umbraco.MediaPicker";

                /// <summary>
                /// Member Picker.
                /// </summary>
                public const string MemberPicker = "Umbraco.MemberPicker";

                /// <summary>
                /// Member Group Picker.
                /// </summary>
                public const string MemberGroupPicker = "Umbraco.MemberGroupPicker";

                /// <summary>
                /// MultiNode Tree Picker.
                /// </summary>
                public const string MultiNodeTreePicker = "Umbraco.MultiNodeTreePicker";

                /// <summary>
                /// Multiple TextString.
                /// </summary>
                public const string MultipleTextstring = "Umbraco.MultipleTextstring";

                /// <summary>
                /// NoEdit.
                /// </summary>
                public const string NoEdit = "Umbraco.NoEdit";

                /// <summary>
                /// Picker Relations.
                /// </summary>
                public const string PickerRelations = "Umbraco.PickerRelations";

                /// <summary>
                /// RadioButton list.
                /// </summary>
                public const string RadioButtonList = "Umbraco.RadioButtonList";

                /// <summary>
                /// Related Links.
                /// </summary>
                public const string RelatedLinks = "Umbraco.RelatedLinks";

                /// <summary>
                /// Slider.
                /// </summary>
                public const string Slider = "Umbraco.Slider";

                /// <summary>
                /// Tags.
                /// </summary>
                public const string Tags = "Umbraco.Tags";

                /// <summary>
                /// Textbox.
                /// </summary>
                public const string TextBox = "Umbraco.TextBox";

                /// <summary>
                /// Textbox Multiple.
                /// </summary>
                public const string TextArea = "Umbraco.TextArea";

                /// <summary>
                /// TinyMCE
                /// </summary>
                public const string TinyMce = "Umbraco.TinyMCEv3";

                /// <summary>
                /// Boolean.
                /// </summary>
                public const string Boolean = "Umbraco.TrueFalse";

                /// <summary>
                /// Markdown Editor.
                /// </summary>
                public const string MarkdownEditor = "Umbraco.MarkdownEditor";

                /// <summary>
                /// User Picker.
                /// </summary>
                public const string UserPicker = "Umbraco.UserPicker";

                /// <summary>
                /// Upload Field.
                /// </summary>
                public const string UploadField = "Umbraco.UploadField";

                /// <summary>
                /// XPatch Checkbox List.
                /// </summary>
                public const string XPathCheckBoxList = "Umbraco.XPathCheckBoxList";

                /// <summary>
                /// XPath DropDown List.
                /// </summary>
                public const string XPathDropDownList = "Umbraco.XPathDropDownList";

                /// <summary>
                /// Email Address.
                /// </summary>
                public const string EmailAddress = "Umbraco.EmailAddress";

                /// <summary>
                /// Nested Content.
                /// </summary>
                public const string NestedContent = "Umbraco.NestedContent";
            }

            /// <summary>
            /// Defines Umbraco build-in datatype configuration keys.
            /// </summary>
            public static class ConfigurationKeys
            {
                /// <summary>
                /// The value type of property data (i.e., string, integer, etc)
                /// </summary>
                /// <remarks>Must be a valid <see cref="ValueTypes"/> value.</remarks>
                public const string DataValueType = "umbracoDataValueType";
            }
        }
    }
}

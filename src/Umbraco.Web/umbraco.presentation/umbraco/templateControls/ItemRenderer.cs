using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Xml;
using Umbraco.Core.Macros;
using Umbraco.Web.Templates;
using Umbraco.Web.Composing;

namespace umbraco.presentation.templateControls
{
    public class ItemRenderer
    {
        public readonly static ItemRenderer Instance = new ItemRenderer();
        /// <summary>
        /// Initializes a new instance of the <see cref="ItemRenderer"/> class.
        /// </summary>
        protected ItemRenderer()
        { }

        /// <summary>
        /// Renders the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="writer">The writer.</param>
        public virtual void Render(Item item, HtmlTextWriter writer)
        {
            if (item.DebugMode)
            {
                writer.AddAttribute(HtmlTextWriterAttribute.Title, string.Format("Field Tag: '{0}'", item.Field));
                writer.AddAttribute("style", "border: 1px solid #fc6;");
                writer.RenderBeginTag(HtmlTextWriterTag.Div);
            }

            try
            {
                StringWriter renderOutputWriter = new StringWriter();
                HtmlTextWriter htmlWriter = new HtmlTextWriter(renderOutputWriter);
                foreach (Control control in item.Controls)
                {
                    try
                    {
                        control.RenderControl(htmlWriter);
                    }
                    catch (Exception renderException)
                    {
                        // TODO: Validate that the current control is within the scope of a form control
                        // Even controls that are inside this scope, can produce this error in async postback.
                        HttpContext.Current.Trace.Warn("ItemRenderer",
                            String.Format("Error rendering control {0} of {1}.", control.ClientID, item), renderException);
                    }
                }

                // parse macros and execute the XSLT transformation on the result if not empty
                string renderOutput = renderOutputWriter.ToString();
                string xsltTransformedOutput = renderOutput.Trim().Length == 0
                                               ? String.Empty
                                               : XsltTransform(item.Xslt, renderOutput, item.XsltDisableEscaping);
                // handle text before/after
                xsltTransformedOutput = AddBeforeAfterText(xsltTransformedOutput, helper.FindAttribute(item.LegacyAttributes, "insertTextBefore"), helper.FindAttribute(item.LegacyAttributes, "insertTextAfter"));
                string finalResult = xsltTransformedOutput.Trim().Length > 0 ? xsltTransformedOutput : GetEmptyText(item);

                //Don't parse urls if a content item is assigned since that is taken care
                // of with the value converters
                if (item.ContentItem == null)
                {
                    writer.Write(TemplateUtilities.ResolveUrlsFromTextString(finalResult));
                }
                else
                {
                    writer.Write(finalResult);
                }

            }
            catch (Exception renderException)
            {
                HttpContext.Current.Trace.Warn("ItemRenderer", String.Format("Error rendering {0}.", item), renderException);
            }
            finally
            {
                if (item.DebugMode)
                {
                    writer.RenderEndTag();
                }
            }
        }

        /// <summary>
        /// Renders the field contents.
        /// Checks via the NodeId attribute whether to fetch data from another page than the current one.
        /// </summary>
        /// <returns>A string of field contents (macros not parsed)</returns>
        protected virtual string GetFieldContents(Item item)
        {
            var tempElementContent = string.Empty;

            // if a nodeId is specified we should get the data from another page than the current one
            if (string.IsNullOrEmpty(item.NodeId) == false)
            {
                var tempNodeId = item.GetParsedNodeId();
                if (tempNodeId != null && tempNodeId.Value != 0)
                {
                    //moved the following from the catch block up as this will allow fallback options alt text etc to work
                    // stop using GetXml
                    //var cache = Umbraco.Web.UmbracoContext.Current.ContentCache.InnerCache as PublishedContentCache;
                    //if (cache == null) throw new InvalidOperationException("Unsupported IPublishedContentCache, only the Xml one is supported.");
                    //var xml = cache.GetXml(Umbraco.Web.UmbracoContext.Current, Umbraco.Web.UmbracoContext.Current.InPreviewMode);
                    //var itemPage = new page(xml.GetElementById(tempNodeId.ToString()));
                    var c = Umbraco.Web.UmbracoContext.Current.ContentCache.GetById(tempNodeId.Value);
                    var itemPage = new page(c);

                    tempElementContent =
                        new item(item.ContentItem, itemPage.Elements, item.LegacyAttributes).FieldContent;
                }
            }
            else
            {
                // gets the field content from the current page (via the PageElements collection)
                tempElementContent =
                    new item(item.ContentItem, item.PageElements, item.LegacyAttributes).FieldContent;
            }

            return tempElementContent;
        }

        /// <summary>
        /// Inits the specified item. To be called from the OnInit method of Item.
        /// </summary>
        /// <param name="item">The item.</param>
        public virtual void Init(Item item)
        { }

        /// <summary>
        /// Loads the specified item. To be called from the OnLoad method of Item.
        /// </summary>
        /// <param name="item">The item.</param>
        public virtual void Load(Item item)
        {
            using (Current.ProfilingLogger.DebugDuration<ItemRenderer>(string.Format("Item: {0}", item.Field)))
            {
                ParseMacros(item);
            }
        }

        /// <summary>
        /// Parses the macros inside the text, by creating child elements for each item.
        /// </summary>
        /// <param name="item">The item.</param>
        protected virtual void ParseMacros(Item item)
        {
            // do nothing if the macros have already been rendered
            if (item.Controls.Count > 0)
                return;

            var elementText = GetFieldContents(item);

            //Don't parse macros if there's a content item assigned since the content value
            // converters take care of that, just add the already parsed text
            if (item.ContentItem != null)
            {
                item.Controls.Add(new LiteralControl(elementText));
            }
            else
            {
                using (Current.ProfilingLogger.DebugDuration<ItemRenderer>("Parsing Macros"))
                {

                    MacroTagParser.ParseMacros(
                        elementText,

                        //callback for when a text block is parsed
                        textBlock => item.Controls.Add(new LiteralControl(textBlock)),

                        //callback for when a macro is parsed:
                        (macroAlias, attributes) =>
                        {
                            var macroControl = new Macro
                            {
                                Alias = macroAlias
                            };
                            foreach (var i in attributes.Where(i => macroControl.Attributes[i.Key] == null))
                            {
                                macroControl.Attributes.Add(i.Key, i.Value);
                            }
                            item.Controls.Add(macroControl);
                        });
                }
            }

        }

        /// <summary>
        /// Transforms the content using the XSLT attribute, if provided.
        /// </summary>
        /// <param name="xpath">The xpath expression.</param>
        /// <param name="itemData">The item's rendered content.</param>
        /// <param name="disableEscaping">if set to <c>true</c>, escaping is disabled.</param>
        /// <returns>The transformed content if the XSLT attribute is present, otherwise the original content.</returns>
        protected virtual string XsltTransform(string xpath, string itemData, bool disableEscaping)
        {
            if (!String.IsNullOrEmpty(xpath))
            {
                // XML-encode the expression and add the itemData parameter to it
                string xpathEscaped = xpath.Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
                string xpathExpression = string.Format(xpathEscaped, "$itemData");

                // prepare support for XSLT extensions
                StringBuilder namespaceList = new StringBuilder();
                StringBuilder namespaceDeclaractions = new StringBuilder();
                foreach (KeyValuePair<string, object> extension in Umbraco.Web.Macros.XsltMacroEngine.GetXsltExtensions())
                {
                    namespaceList.Append(extension.Key).Append(' ');
                    namespaceDeclaractions.AppendFormat("xmlns:{0}=\"urn:{0}\" ", extension.Key);
                }

                // add the XSLT expression into the full XSLT document, together with the needed parameters
                string xslt = string.Format(Umbraco.Web.umbraco.presentation.umbraco.templateControls.Resources.InlineXslt, xpathExpression, disableEscaping ? "yes" : "no",
                                                                  namespaceList, namespaceDeclaractions);

                // create the parameter
                Dictionary<string, object> parameters = new Dictionary<string, object>(1);
                parameters.Add("itemData", itemData);

                // apply the XSLT transformation
                using (var xslReader = new XmlTextReader(new StringReader(xslt)))
                {
                    var transform = Umbraco.Web.Macros.XsltMacroEngine.GetXsltTransform(xslReader, false);
                    return Umbraco.Web.Macros.XsltMacroEngine.ExecuteItemRenderer(Current.ProfilingLogger, transform, itemData);
                }
            }
            return itemData;
        }

        protected string AddBeforeAfterText(string text, string before, string after)
        {
            if (!String.IsNullOrEmpty(text))
            {
                if (!String.IsNullOrEmpty(before))
                    text = String.Format("{0}{1}", HttpContext.Current.Server.HtmlDecode(before), text);
                if (!String.IsNullOrEmpty(after))
                    text = String.Format("{0}{1}", text, HttpContext.Current.Server.HtmlDecode(after));
            }

            return text;
        }

        /// <summary>
        /// Gets the text to display if the field contents are empty.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>The text to display.</returns>
        protected virtual string GetEmptyText(Item item)
        {
            return item.TextIfEmpty;
        }

    }
}

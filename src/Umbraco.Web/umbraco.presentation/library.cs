﻿using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Newtonsoft.Json;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Configuration;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.Templates;
using Umbraco.Core.IO;
using Umbraco.Core.Xml;
using Umbraco.Web.Composing;
using Umbraco.Web.PublishedCache.XmlPublishedCache;

namespace umbraco
{
    /// <summary>
    /// Function library for umbraco. Includes various helper-methods and methods to
    /// save and load data from umbraco.
    ///
    /// Especially usefull in XSLT where any of these methods can be accesed using the umbraco.library name-space. Example:
    /// &lt;xsl:value-of select="umbraco.library:NiceUrl(@id)"/&gt;
    /// </summary>
    [Obsolete("v8.kill.kill")]
    public class library
    {
        /// <summary>
        /// Returns a new UmbracoHelper so that we can start moving the logic from some of these methods to it
        /// </summary>
        /// <returns></returns>
        private static UmbracoHelper GetUmbracoHelper()
        {
            return new UmbracoHelper(Current.UmbracoContext, Current.Services, Current.ApplicationCache);
        }

        #region Declarations

        /// <summary>
        /// Used by umbraco's publishing enginge, to determine if publishing is currently active
        /// </summary>
        public static bool IsPublishing = false;
        /// <summary>
        /// Used by umbraco's publishing enginge, to how many nodes is publish in the current publishing cycle
        /// </summary>
        public static int NodesPublished = 0;
        /// <summary>
        /// Used by umbraco's publishing enginge, to determine the start time of the current publishing cycle.
        /// </summary>
        public static DateTime PublishStart;
        private page _page;

        #endregion

        #region Constructors

        /// <summary>
        /// Empty constructor
        /// </summary>
        public library()
        {
        }

        public library(int id)
        {
            var content = GetSafeContentCache().GetById(id);
            _page = new page(content);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="library"/> class.
        /// </summary>
        /// <param name="Page">The page.</param>
        public library(page page)
        {
            _page = page;
        }

        #endregion

        #region Xslt Helper functions

        /// <summary>
        /// This will convert a json structure to xml for use in xslt
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static XPathNodeIterator JsonToXml(string json)
        {
            try
            {
                if (json.StartsWith("["))
                {
                    //we'll assume it's an array, in which case we need to add a root
                    json = "{\"arrayitem\":" + json + "}";
                }
                var xml = JsonConvert.DeserializeXmlNode(json, "json", false);
                return xml.CreateNavigator().Select("/json");
            }
            catch (Exception ex)
            {
                var xd = new XmlDocument();
                xd.LoadXml(string.Format("<error>Could not convert JSON to XML. Error: {0}</error>", ex));
                return xd.CreateNavigator().Select("/error");
            }
        }

        /// <summary>
        /// Returns a string with a friendly url from a node.
        /// IE.: Instead of having /482 (id) as an url, you can have
        /// /screenshots/developer/macros (spoken url)
        /// </summary>
        /// <param name="nodeID">Identifier for the node that should be returned</param>
        /// <returns>String with a friendly url from a node</returns>
        public static string NiceUrl(int nodeID)
        {
            return GetUmbracoHelper().NiceUrl(nodeID);
        }

        /// <summary>
        /// This method will always add the domain to the path if the hostnames are set up correctly.
        /// </summary>
        /// <param name="nodeId">Identifier for the node that should be returned</param>
        /// <returns>String with a friendly url with full domain from a node</returns>
        public static string NiceUrlWithDomain(int nodeId)
        {
            return GetUmbracoHelper().NiceUrlWithDomain(nodeId);
        }

        /// <summary>
        /// This method will always add the domain to the path.
        /// </summary>
        /// <param name="nodeId">Identifier for the node that should be returned</param>
        /// <param name="ignoreUmbracoHostNames">Ignores the umbraco hostnames and returns the url prefixed with the requested host (including scheme and port number)</param>
        /// <returns>String with a friendly url with full domain from a node</returns>
        internal static string NiceUrlWithDomain(int nodeId, bool ignoreUmbracoHostNames)
        {
            if (ignoreUmbracoHostNames)
                return HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority) + NiceUrl(nodeId);

            return NiceUrlWithDomain(nodeId);
        }

        public static string ResolveVirtualPath(string path)
        {
            return IOHelper.ResolveUrl(path);
        }


        /// <summary>
        /// Returns a string with the data from the given element of a node. Both elements (data-fields)
        /// and properties can be used - ie:
        /// getItem(1, nodeName) will return a string with the name of the node with id=1 even though
        /// nodeName is a property and not an element (data-field).
        /// </summary>
        /// <param name="nodeId">Identifier for the node that should be returned</param>
        /// <param name="alias">The element that should be returned</param>
        /// <returns>Returns a string with the data from the given element of a node</returns>
        public static string GetItem(int nodeId, string alias)
        {
            var doc = UmbracoContext.Current.ContentCache.GetById(nodeId);

            if (doc == null)
                return string.Empty;

            switch (alias)
            {
                case "id":
                    return doc.Id.ToString();
                case "parentID":
                    return doc.Parent.Id.ToString();
                case "level":
                    return doc.Level.ToString();
                case "writerID":
                    return doc.WriterId.ToString();
                case "template":
                    return doc.TemplateId.ToString();
                case "sortOrder":
                    return doc.SortOrder.ToString();
                case "createDate":
                    return doc.CreateDate.ToString("yyyy-MM-dd'T'HH:mm:ss");
                case "updateDate":
                    return doc.UpdateDate.ToString("yyyy-MM-dd'T'HH:mm:ss");
                case "nodeName":
                    return doc.Name;
                case "writerName":
                    return doc.WriterName;
                case "path":
                    return doc.Path;
                case "creatorName":
                    return doc.CreatorName;
            }

            // in 4.9.0 the method returned the raw XML from the cache, unparsed
            // starting with 5c20f4f (4.10?) the method returns prop.Value.ToString()
            //   where prop.Value is parsed for internal links + resolve urls - but not for macros
            //   comments say "fixing U4-917 and U4-821" which are not related
            // if we return DataValue.ToString() we're back to the original situation
            // if we return Value.ToString() we'll have macros parsed and that's nice
            //
            // so, use Value.ToString() here.
            var prop = doc.GetProperty(alias);
            return prop == null ? string.Empty : prop.GetValue().ToString();
        }

        /// <summary>
        /// Returns a string with the data from the given element of the current node. Both elements (data-fields)
        /// and properties can be used - ie:
        /// getItem(nodeName) will return a string with the name of the current node/page even though
        /// nodeName is a property and not an element (data-field).
        /// </summary>
        /// <param name="alias"></param>
        /// <returns></returns>
        public static string GetItem(string alias)
        {
            try
            {
                int currentID = int.Parse(HttpContext.Current.Items["pageID"].ToString());
                return GetItem(currentID, alias);
            }
            catch (Exception ItemException)
            {
                HttpContext.Current.Trace.Warn("library.GetItem", "Error reading '" + alias + "'", ItemException);
                return string.Empty;
            }
        }

        /// <summary>
        /// Get a media object as an xml object
        /// </summary>
        /// <param name="MediaId">The identifier of the media object to be returned</param>
        /// <param name="deep">If true, children of the media object is returned</param>
        /// <returns>An umbraco xml node of the media (same format as a document node)</returns>
        public static XPathNodeIterator GetMedia(int MediaId, bool deep)
        {
            try
            {
                if (UmbracoConfig.For.UmbracoSettings().Content.UmbracoLibraryCacheDuration > 0)
                {
                    var xml = Current.ApplicationCache.RuntimeCache.GetCacheItem<XElement>(
                        $"{CacheKeys.MediaCacheKey}_{MediaId}_{deep}",
                        timeout:        TimeSpan.FromSeconds(UmbracoConfig.For.UmbracoSettings().Content.UmbracoLibraryCacheDuration),
                        getCacheItem:   () => GetMediaDo(MediaId, deep).Item1);

                    if (xml != null)
                    {
                        //returning the root element of the Media item fixes the problem
                        return xml.CreateNavigator().Select("/");
                    }

                }
                else
                {
                    var xml = GetMediaDo(MediaId, deep).Item1;

                    //returning the root element of the Media item fixes the problem
                    return xml.CreateNavigator().Select("/");
                }
            }
            catch(Exception ex)
            {
                Current.Logger.Error<library>("An error occurred looking up media", ex);
            }

            Current.Logger.Debug<library>("No media result for id {0}", () => MediaId);

            var errorXml = new XElement("error", string.Format("No media is maching '{0}'", MediaId));
            return errorXml.CreateNavigator().Select("/");
        }

        private static Tuple<XElement, string> GetMediaDo(int mediaId, bool deep)
        {
            var media = Current.Services.MediaService.GetById(mediaId);
            if (media == null) return null;

            var serialized = EntityXmlSerializer.Serialize(
                Current.Services.MediaService,
                Current.Services.DataTypeService,
                Current.Services.UserService,
                Current.Services.LocalizationService,
                Current.UrlSegmentProviders,
                media,
                deep);
            return Tuple.Create(serialized, media.Path);
        }

        /// <summary>
        /// Get a member as an xml object
        /// </summary>
        /// <param name="MemberId">The identifier of the member object to be returned</param>
        /// <returns>An umbraco xml node of the member (same format as a document node), but with two additional attributes on the "node" element:
        /// "email" and "loginName".
        /// </returns>
        public static XPathNodeIterator GetMember(int MemberId)
        {
            try
            {
                if (UmbracoConfig.For.UmbracoSettings().Content.UmbracoLibraryCacheDuration > 0)
                {
                    var xml = Current.ApplicationCache.RuntimeCache.GetCacheItem<XElement>(
                        string.Format(
                            "{0}_{1}", CacheKeys.MemberLibraryCacheKey, MemberId),
                        timeout:        TimeSpan.FromSeconds(UmbracoConfig.For.UmbracoSettings().Content.UmbracoLibraryCacheDuration),
                        getCacheItem:   () => GetMemberDo(MemberId));

                    if (xml != null)
                    {
                        return xml.CreateNavigator().Select("/");
                    }
                }
                else
                {
                    return GetMemberDo(MemberId).CreateNavigator().Select("/");
                }
            }
            catch (Exception ex)
            {
                Current.Logger.Error<library>("An error occurred looking up member", ex);
            }

            Current.Logger.Debug<library>("No member result for id {0}", () => MemberId);

            var xd = new XmlDocument();
            xd.LoadXml(string.Format("<error>No member is maching '{0}'</error>", MemberId));
            return xd.CreateNavigator().Select("/");
        }

        private static XElement GetMemberDo(int MemberId)
        {
            var member = Current.Services.MemberService.GetById(MemberId);
            if (member == null) return null;

            var serialized = EntityXmlSerializer.Serialize(
                Current.Services.DataTypeService, Current.Services.LocalizationService, member);
            return serialized;
        }

        /// <summary>
        /// Whether or not the current user is logged in (as a member)
        /// </summary>
        /// <returns>True is the current user is logged in</returns>
        public static bool IsLoggedOn()
        {
            return GetUmbracoHelper().MemberIsLoggedOn();
        }

        public static XPathNodeIterator AllowedGroups(int documentId, string path)
        {
            XmlDocument xd = new XmlDocument();
            xd.LoadXml("<roles/>");
            foreach (string role in GetAccessingMembershipRoles(documentId, path))
                xd.DocumentElement.AppendChild(XmlHelper.AddTextNode(xd, "role", role));
            return xd.CreateNavigator().Select(".");
        }

        private static string[] GetAccessingMembershipRoles(int documentId, string path)
        {
            var entry = Current.Services.PublicAccessService.GetEntryForContent(path.EnsureEndsWith("," + documentId));
            if (entry == null) return new string[] { };

            var memberGroupRoleRules = entry.Rules.Where(x => x.RuleType == Constants.Conventions.PublicAccess.MemberRoleRuleType);
            return memberGroupRoleRules.Select(x => x.RuleValue).ToArray();

        }

        /// <summary>
        /// Check if a document object is protected by the "Protect Pages" functionality in umbraco
        /// </summary>
        /// <param name="DocumentId">The identifier of the document object to check</param>
        /// <param name="Path">The full path of the document object to check</param>
        /// <returns>True if the document object is protected</returns>
        public static bool IsProtected(int DocumentId, string Path)
        {
            return GetUmbracoHelper().IsProtected(DocumentId, Path);
        }

        /// <summary>
        /// Check if the current user has access to a document
        /// </summary>
        /// <param name="NodeId">The identifier of the document object to check</param>
        /// <param name="Path">The full path of the document object to check</param>
        /// <returns>True if the current user has access or if the current document isn't protected</returns>
        public static bool HasAccess(int NodeId, string Path)
        {
            return GetUmbracoHelper().MemberHasAccess(NodeId, Path);
        }


        /// <summary>
        /// Returns an MD5 hash of the string specified
        /// </summary>
        /// <param name="text">The text to create a hash from</param>
        /// <returns>Md5 hash of the string</returns>
        [Obsolete("Please use the CreateHash method instead. This may be removed in future versions")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static string md5(string text)
        {
            return text.ToMd5();
        }

        /// <summary>
        /// Generates a hash based on the text string passed in.  This method will detect the
        /// security requirements (is FIPS enabled) and return an appropriate hash.
        /// </summary>
        /// <param name="text">The text to create a hash from</param>
        /// <returns>hash of the string</returns>
        public static string CreateHash(string text)
        {
            return text.GenerateHash();
        }

        /// <summary>
        /// Compare two dates
        /// </summary>
        /// <param name="firstDate">The first date to compare</param>
        /// <param name="secondDate">The second date to compare</param>
        /// <returns>True if the first date is greater than the second date</returns>
        public static bool DateGreaterThan(string firstDate, string secondDate)
        {
            if (DateTime.Parse(firstDate) > DateTime.Parse(secondDate))
                return true;
            else
                return false;
        }

        /// <summary>
        /// Compare two dates
        /// </summary>
        /// <param name="firstDate">The first date to compare</param>
        /// <param name="secondDate">The second date to compare</param>
        /// <returns>True if the first date is greater than or equal the second date</returns>
        public static bool DateGreaterThanOrEqual(string firstDate, string secondDate)
        {
            if (DateTime.Parse(firstDate) >= DateTime.Parse(secondDate))
                return true;
            else
                return false;
        }

        /// <summary>
        /// Check if a date is greater than today
        /// </summary>
        /// <param name="firstDate">The date to check</param>
        /// <returns>True if the date is greater that today (ie. at least the day of tomorrow)</returns>
        public static bool DateGreaterThanToday(string firstDate)
        {
            DateTime first = DateTime.Parse(firstDate);
            first = new DateTime(first.Year, first.Month, first.Day);
            DateTime today = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
            TimeSpan TS = new TimeSpan(first.Ticks - today.Ticks);
            if (TS.Days > 0)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Check if a date is greater than or equal today
        /// </summary>
        /// <param name="firstDate">The date to check</param>
        /// <returns>True if the date is greater that or equal today (ie. at least today or the day of tomorrow)</returns>
        public static bool DateGreaterThanOrEqualToday(string firstDate)
        {
            DateTime first = DateTime.Parse(firstDate);
            first = new DateTime(first.Year, first.Month, first.Day);
            DateTime today = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
            TimeSpan TS = new TimeSpan(first.Ticks - today.Ticks);
            if (TS.Days >= 0)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Get the current date
        /// </summary>
        /// <returns>Current date i xml format (ToString("s"))</returns>
        public static string CurrentDate()
        {
            return DateTime.Now.ToString("s");
        }

        /// <summary>
        /// Add a value to a date
        /// </summary>
        /// <param name="Date">The Date to user</param>
        /// <param name="AddType">The type to add: "y": year, "m": month, "d": day, "h": hour, "min": minutes, "s": seconds</param>
        /// <param name="add">An integer value to add</param>
        /// <returns>A date in xml format (ToString("s"))</returns>
        public static string DateAdd(string Date, string AddType, int add)
        {
            return DateAddWithDateTimeObject(DateTime.Parse(Date), AddType, add);
        }

        /// <summary>
        /// Get the day of week from a date matching the current culture settings
        /// </summary>
        /// <param name="Date">The date to use</param>
        /// <returns>A string with the DayOfWeek matching the current contexts culture settings</returns>
        public static string GetWeekDay(string Date)
        {
            return DateTime.Parse(Date).ToString("dddd");
        }

        /// <summary>
        /// Add a value to a date. Similar to the other overload, but uses a datetime object instead of a string
        /// </summary>
        /// <param name="Date">The Date to user</param>
        /// <param name="AddType">The type to add: "y": year, "m": month, "d": day, "h": hour, "min": minutes, "s": seconds</param>
        /// <param name="add">An integer value to add</param>
        /// <returns>A date in xml format (ToString("s"))</returns>
        public static string DateAddWithDateTimeObject(DateTime Date, string AddType, int add)
        {
            switch (AddType.ToLower())
            {
                case "y":
                    Date = Date.AddYears(add);
                    break;
                case "m":
                    Date = Date.AddMonths(add);
                    break;
                case "d":
                    Date = Date.AddDays(add);
                    break;
                case "h":
                    Date = Date.AddHours(add);
                    break;
                case "min":
                    Date = Date.AddMinutes(add);
                    break;
                case "s":
                    Date = Date.AddSeconds(add);
                    break;
            }

            return Date.ToString("s");
        }

        /// <summary>
        /// Return the difference between 2 dates, in either minutes, seconds or years.
        /// </summary>
        /// <param name="firstDate">The first date.</param>
        /// <param name="secondDate">The second date.</param>
        /// <param name="diffType">format to return, can only be: s,m or y:  s = seconds, m = minutes, y = years.</param>
        /// <returns>A timespan as a integer</returns>
        public static int DateDiff(string firstDate, string secondDate, string diffType)
        {
            TimeSpan TS = DateTime.Parse(firstDate).Subtract(DateTime.Parse(secondDate));

            switch (diffType.ToLower())
            {
                case "m":
                    return Convert.ToInt32(TS.TotalMinutes);
                case "s":
                    return Convert.ToInt32(TS.TotalSeconds);
                case "y":
                    return Convert.ToInt32(TS.TotalDays / 365);
            }
            // return default
            return 0;
        }

        /// <summary>
        /// Formats a string to the specified formate.
        /// </summary>
        /// <param name="Date">The date.</param>
        /// <param name="Format">The format, compatible with regular .net date formats</param>
        /// <returns>A date in the new format as a string</returns>
        public static string FormatDateTime(string Date, string Format)
        {
            DateTime result;
            if (DateTime.TryParse(Date, out result))
                return result.ToString(Format);
            return string.Empty;
        }

        /// <summary>
        /// Converts a string to Long Date and returns it as a string
        /// </summary>
        /// <param name="Date">The date.</param>
        /// <param name="WithTime">if set to <c>true</c> the date will include time.</param>
        /// <param name="TimeSplitter">The splitter between date and time.</param>
        /// <returns>A Long Date as a string.</returns>
        public static string LongDate(string Date, bool WithTime, string TimeSplitter)
        {
            DateTime result;
            if (DateTime.TryParse(Date, out result))
            {
                if (WithTime)
                    return result.ToLongDateString() + TimeSplitter + result.ToLongTimeString();
                return result.ToLongDateString();
            }
            return string.Empty;
        }

        /// <summary>
        /// Checks whether the Culture with the specified name exixts in the standard .net cultureInfo.
        /// </summary>
        /// <param name="cultureName">Name of the culture.</param>
        /// <returns></returns>
        public static bool CultureExists(string cultureName)
        {
            CultureInfo[] ci = CultureInfo.GetCultures(CultureTypes.AllCultures);
            CultureInfo c = Array.Find(ci, delegate(CultureInfo culture) { return culture.Name == cultureName; });
            return c != null;
        }

        /// <summary>
        /// Converts a string to datetime in the longdate with day name format.
        /// </summary>
        /// <param name="Date">The date.</param>
        /// <param name="DaySplitter">String between day name and date</param>
        /// <param name="WithTime">if set to <c>true</c> the datetiem will include time.</param>
        /// <param name="TimeSplitter">String between date and time.</param>
        /// <param name="GlobalAlias">Culture name.</param>
        /// <returns>A datetime in the longdate formate with day name, as a string</returns>
        public static string LongDateWithDayName(string Date, string DaySplitter, bool WithTime, string TimeSplitter,
                                                 string GlobalAlias)
        {
            if (!CultureExists(GlobalAlias))
                return string.Empty;

            DateTime result;
            CultureInfo.GetCultureInfo(GlobalAlias);
            DateTimeFormatInfo dtInfo = CultureInfo.GetCultureInfo(GlobalAlias).DateTimeFormat;
            if (DateTime.TryParse(Date, dtInfo, DateTimeStyles.None, out result))
            {
                if (WithTime)
                    return
                        result.ToString(dtInfo.LongDatePattern) + TimeSplitter + result.ToString(dtInfo.LongTimePattern);
                return result.ToString(dtInfo.LongDatePattern);
            }
            return string.Empty;
        }

        /// <summary>
        /// Converts a string to a Long Date and returns it as a string
        /// </summary>
        /// <param name="Date">The date.</param>
        /// <returns>A Long Date as a string.</returns>
        public static string LongDate(string Date)
        {
            DateTime result;
            if (DateTime.TryParse(Date, out result))
                return result.ToLongDateString();
            return string.Empty;
        }

        /// <summary>
        /// Converts a string to a Short Date and returns it as a string
        /// </summary>
        /// <param name="Date">The date.</param>
        /// <returns>A Short Date as a string.</returns>
        public static string ShortDate(string Date)
        {
            DateTime result;
            if (DateTime.TryParse(Date, out result))
                return result.ToShortDateString();
            return string.Empty;
        }

        /// <summary>
        /// Converts a string to a Short Date, with a specific culture, and returns it as a string
        /// </summary>
        /// <param name="Date">The date.</param>
        /// <param name="GlobalAlias">Culture name</param>
        /// <returns>A short date with a specific culture, as a string</returns>
        public static string ShortDateWithGlobal(string Date, string GlobalAlias)
        {
            if (!CultureExists(GlobalAlias))
                return string.Empty;

            DateTime result;
            if (DateTime.TryParse(Date, out result))
            {
                DateTimeFormatInfo dtInfo = CultureInfo.GetCultureInfo(GlobalAlias).DateTimeFormat;
                return result.ToString(dtInfo.ShortDatePattern);
            }
            return string.Empty;
        }

        /// <summary>
        /// Converts a string to a Short Date with time, with a specific culture, and returns it as a string
        /// </summary>
        /// <param name="Date">The date.</param>
        /// <param name="GlobalAlias">Culture name</param>
        /// <returns>A short date withi time, with a specific culture, as a string</returns>
        public static string ShortDateWithTimeAndGlobal(string Date, string GlobalAlias)
        {
            if (!CultureExists(GlobalAlias))
                return string.Empty;

            DateTime result;
            if (DateTime.TryParse(Date, out result))
            {
                DateTimeFormatInfo dtInfo = CultureInfo.GetCultureInfo(GlobalAlias).DateTimeFormat;
                return result.ToString(dtInfo.ShortDatePattern) + " " +
                       result.ToString(dtInfo.ShortTimePattern);
            }
            return string.Empty;
        }

        /// <summary>
        /// Converts a datetime string to the ShortTime format.
        /// </summary>
        /// <param name="Date">The date.</param>
        /// <returns></returns>
        public static string ShortTime(string Date)
        {
            DateTime result;
            if (DateTime.TryParse(Date, out result))
                return result.ToShortTimeString();
            return string.Empty;
        }

        /// <summary>
        /// Converts a datetime string to the ShortDate format.
        /// </summary>
        /// <param name="Date">The date.</param>
        /// <param name="WithTime">if set to <c>true</c> the date will include time.</param>
        /// <param name="TimeSplitter">String dividing date and time</param>
        /// <returns></returns>
        public static string ShortDate(string Date, bool WithTime, string TimeSplitter)
        {
            DateTime result;
            if (DateTime.TryParse(Date, out result))
            {
                if (WithTime)
                    return result.ToShortDateString() + TimeSplitter + result.ToLongTimeString();
                return result.ToShortDateString();
            }
            return string.Empty;
        }

        /// <summary>
        /// Replaces text line breaks with html line breaks
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The text with text line breaks replaced with html linebreaks (<br/>)</returns>
        public static string ReplaceLineBreaks(string text)
        {
            return GetUmbracoHelper().ReplaceLineBreaksForHtml(text);
        }

        /// <summary>
        /// Renders the content of a macro. Uses the normal template umbraco macro markup as input.
        /// This only works properly with xslt macros.
        /// Python and .ascx based macros will not render properly, as viewstate is not included.
        /// </summary>
        /// <param name="Text">The macro markup to be rendered.</param>
        /// <param name="PageId">The page id.</param>
        /// <returns>The rendered macro as a string</returns>
        public static string RenderMacroContent(string Text, int PageId)
        {
            try
            {
                var p = new page(GetSafeContentCache().GetById(PageId));
                template t = new template(p.Template);
                Control c = t.parseStringBuilder(new StringBuilder(Text), p);

                StringWriter sw = new StringWriter();
                HtmlTextWriter hw = new HtmlTextWriter(sw);
                c.RenderControl(hw);

                return sw.ToString();
            }
            catch (Exception ee)
            {
                return string.Format("<!-- Error generating macroContent: '{0}' -->", ee);
            }
        }

        /// <summary>
        /// Renders a template.
        /// </summary>
        /// <param name="PageId">The page id.</param>
        /// <param name="TemplateId">The template id.</param>
        /// <returns>The rendered template as a string</returns>
        public static string RenderTemplate(int PageId, int TemplateId)
        {
            if (UmbracoConfig.For.UmbracoSettings().Templates.UseAspNetMasterPages)
            {
                using (var sw = new StringWriter())
                {
                    try
                    {
                        var altTemplate = TemplateId == -1 ? null : (int?)TemplateId;
                        var templateRenderer = new TemplateRenderer(Umbraco.Web.UmbracoContext.Current, PageId, altTemplate);
                        templateRenderer.Render(sw);
                    }
                    catch (Exception ee)
                    {
                        sw.Write("<!-- Error rendering template with id {0}: '{1}' -->", PageId, ee);
                    }

                    return sw.ToString();
                }
            }
            else
            {
                var p = new page(GetSafeContentCache().GetById(PageId));
                p.RenderPage(TemplateId);
                var c = p.PageContentControl;

                using (var sw = new StringWriter())
                using(var hw = new HtmlTextWriter(sw))
                {
                    c.RenderControl(hw);
                    return sw.ToString();
                }

            }
        }

        /// <summary>
        /// Renders the default template for a specific page.
        /// </summary>
        /// <param name="PageId">The page id.</param>
        /// <returns>The rendered template as a string.</returns>
        public static string RenderTemplate(int PageId)
        {
            return RenderTemplate(PageId, -1);
        }

        /// <summary>
        /// Registers the client script block.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="script">The script.</param>
        /// <param name="addScriptTags">if set to <c>true</c> [add script tags].</param>
        public static void RegisterClientScriptBlock(string key, string script, bool addScriptTags)
        {
            Page p = HttpContext.Current.CurrentHandler as Page;

            if (p != null)
                p.ClientScript.RegisterClientScriptBlock(p.GetType(), key, script, addScriptTags);
        }

        /// <summary>
        /// Registers the client script include.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="url">The URL.</param>
        public static void RegisterStyleSheetFile(string key, string url)
        {
            Page p = HttpContext.Current.CurrentHandler as Page;

            if (p != null)
            {
                System.Web.UI.HtmlControls.HtmlGenericControl include = new System.Web.UI.HtmlControls.HtmlGenericControl("link");
                include.ID = key;
                include.Attributes.Add("rel", "stylesheet");
                include.Attributes.Add("type", "text/css");
                include.Attributes.Add("href", url);

                if (p.Header != null)
                {
                    if (p.Header.FindControl(key) == null)
                    {
                        p.Header.Controls.Add(include);
                    }
                }
                else
                {
                    //This is a fallback in case there is no header
                    p.ClientScript.RegisterClientScriptBlock(p.GetType(), key, "<link rel='stylesheet' href='" + url + "' />");
                }
            }
        }

        /// <summary>
        /// Registers the client script include.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="url">The URL.</param>
        public static void RegisterJavaScriptFile(string key, string url)
        {
            Page p = HttpContext.Current.CurrentHandler as Page;

            if (p != null)
            {

                if (ClientDependency.Core.Controls.ClientDependencyLoader.Instance == null)
                {
                    System.Web.UI.HtmlControls.HtmlGenericControl include = new System.Web.UI.HtmlControls.HtmlGenericControl("script");
                    include.ID = key;
                    include.Attributes.Add("type", "text/javascript");
                    include.Attributes.Add("src", url);

                    if (p.Header != null)
                    {
                        if (p.Header.FindControl(key) == null)
                        {
                            p.Header.Controls.Add(include);
                        }
                    }
                    else
                    {
                        //This is a fallback in case there is no header
                        p.ClientScript.RegisterClientScriptInclude(p.GetType(), key, url);
                    }
                }
                else
                {
                    ClientDependency.Core.Controls.ClientDependencyLoader.Instance.RegisterDependency(url, ClientDependency.Core.ClientDependencyType.Javascript);
                }
            }
        }

        /// <summary>
        /// Adds a reference to the jQuery javascript file from the client/ui folder using "jQuery" as a key
        /// Recommended to use instead of RegisterJavaScriptFile for all nitros/packages that uses jQuery
        /// </summary>
        public static void AddJquery()
        {
            RegisterJavaScriptFile("jQuery", String.Format("{0}/ui/jquery.js", IOHelper.ResolveUrl(SystemDirectories.UmbracoClient)));
        }


        /// <summary>
        /// Strips all html from a string.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>Returns the string without any html tags.</returns>
        public static string StripHtml(string text)
        {
            string pattern = @"<(.|\n)*?>";
            return Regex.Replace(text, pattern, string.Empty);
        }

        /// <summary>
        /// Truncates a string if it's too long
        /// </summary>
        /// <param name="Text">The text to eventually truncate</param>
        /// <param name="MaxLength">The maximum number of characters (length)</param>
        /// <param name="AddString">String to append if text is truncated (ie "...")</param>
        /// <returns>A truncated string if text if longer than MaxLength appended with the addString parameters. If text is shorter
        /// then MaxLength then the full - non-truncated - string is returned</returns>
        public static string TruncateString(string Text, int MaxLength, string AddString)
        {
            if (Text.Length > MaxLength)
                return Text.Substring(0, MaxLength - AddString.Length) + AddString;
            else
                return Text;
        }

        /// <summary>
        /// Split a string into xml elements
        /// </summary>
        /// <param name="StringToSplit">The full text to spil</param>
        /// <param name="Separator">The separator</param>
        /// <returns>An XPathNodeIterator containing the substrings in the format of <values><value></value></values></returns>
        public static XPathNodeIterator Split(string StringToSplit, string Separator)
        {
            string[] values = StringToSplit.Split(Convert.ToChar(Separator));
            XmlDocument xd = new XmlDocument();
            xd.LoadXml("<values/>");
            foreach (string id in values)
            {
                XmlNode node = XmlHelper.AddTextNode(xd, "value", id);
                xd.DocumentElement.AppendChild(node);
            }
            XPathNavigator xp = xd.CreateNavigator();
            return xp.Select("/values");
        }

        /// <summary>
        /// Removes the starting and ending paragraph tags in a string.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>Returns the string without starting and endning paragraph tags</returns>
        public static string RemoveFirstParagraphTag(string text)
        {
            if (String.IsNullOrEmpty(text))
                return "";

            if (text.Length > 5)
            {
                if (text.ToUpper().Substring(0, 3) == "<P>")
                    text = text.Substring(3, text.Length - 3);
                if (text.ToUpper().Substring(text.Length - 4, 4) == "</P>")
                    text = text.Substring(0, text.Length - 4);
            }
            return text;
        }

        /// <summary>
        /// Replaces a specified value with a new one.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="oldValue">The old value.</param>
        /// <param name="newValue">The new value.</param>
        /// <returns></returns>
        public static string Replace(string text, string oldValue, string newValue)
        {
            return text.Replace(oldValue, newValue);
        }

        /// <summary>
        /// Returns the Last index of the specified value
        /// </summary>
        /// <param name="Text">The text.</param>
        /// <param name="Value">The value.</param>
        /// <returns>Return the last index of a value as a integer </returns>
        public static int LastIndexOf(string Text, string Value)
        {
            return Text.LastIndexOf(Value);
        }

        /// <summary>
        /// Gets the dictionary item with the specified key.
        /// </summary>
        /// <param name="Key">The key.</param>
        /// <returns>A dictionary items value as a string.</returns>
        public static string GetDictionaryItem(string Key)
        {
            return GetUmbracoHelper().GetDictionaryValue(Key);
        }

        /// <summary>
        /// Gets the current page.
        /// </summary>
        /// <returns>An XpathNodeIterator containing the current page as Xml.</returns>
        public static XPathNodeIterator GetXmlNodeCurrent()
        {
            var pageId = "";

            try
            {
                var nav = UmbracoContext.Current.ContentCache.CreateNavigator();
                pageId = HttpContext.Current.Items["pageID"]?.ToString();

                if (pageId == null)
                    throw new NullReferenceException("pageID not found in the current HTTP context");

                nav.MoveToId(pageId);
                return nav.Select(".");
            }
            catch (Exception ex)
            {
                Current.Logger.Error<library>($"Could not retrieve current xml node for page Id {pageId}.", ex);
            }

            XmlDocument xd = new XmlDocument();
            xd.LoadXml("<error>No current node exists</error>");
            return xd.CreateNavigator().Select("/");
        }

        /// <summary>
        /// Gets the page with the specified id.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>Returns the node with the specified id as xml in the form of a XPathNodeIterator</returns>
        public static XPathNodeIterator GetXmlNodeById(string id)
        {
            var nav = GetSafeContentCache().CreateNavigator();

            if (nav.MoveToId(id))
                return nav.Select(".");

            var xd = new XmlDocument();
            xd.LoadXml(string.Format("<error>No published item exist with id {0}</error>", id));
            return xd.CreateNavigator().Select(".");
        }

        // legacy would access the raw XML from content.Instance ie a static thing
        // now that we use a PublishedSnapshotService, we need to have a "context" to handle a cache.
        // UmbracoContext does it for most cases but in some cases we might not have an
        // UmbracoContext. For backward compatibility, try to do something here...
        internal static PublishedContentCache GetSafeContentCache()
        {
            PublishedContentCache contentCache;

            if (UmbracoContext.Current != null)
            {
                contentCache = UmbracoContext.Current.ContentCache as PublishedContentCache;
            }
            else
            {
                var publishedSnapshot = Current.PublishedSnapshot
                    ?? Current.PublishedSnapshotService.CreatePublishedSnapshot(null);
                contentCache = publishedSnapshot.Content as PublishedContentCache;
            }

            if (contentCache == null)
                throw new InvalidOperationException("Unsupported IPublishedContentCache, only the Xml one is supported.");

            return contentCache;
        }

        /// <summary>
        /// Queries the umbraco Xml cache with the specified Xpath query
        /// </summary>
        /// <param name="xpathQuery">The XPath query</param>
        /// <returns>Returns nodes matching the xpath query as a XpathNodeIterator</returns>
        public static XPathNodeIterator GetXmlNodeByXPath(string xpathQuery)
        {
            return GetSafeContentCache().CreateNavigator().Select(xpathQuery);
        }

        /// <summary>
        /// Gets the entire umbraco xml cache.
        /// </summary>
        /// <returns>Returns the entire umbraco Xml cache as a XPathNodeIterator</returns>
        public static XPathNodeIterator GetXmlAll()
        {
            return GetSafeContentCache().CreateNavigator().Select("/root");
        }

        /// <summary>
        /// Fetches a xml file from the specified path on the server.
        /// The path can be relative ("/path/to/file.xml") or absolute ("c:\folder\file.xml")
        /// </summary>
        /// <param name="Path">The path.</param>
        /// <param name="Relative">if set to <c>true</c> the path is relative.</param>
        /// <returns>The xml file as a XpathNodeIterator</returns>
        public static XPathNodeIterator GetXmlDocument(string Path, bool Relative)
        {
            XmlDocument xmlDoc = new XmlDocument();
            try
            {
                if (Relative)
                    xmlDoc.Load(IOHelper.MapPath(Path));
                else
                    xmlDoc.Load(Path);
            }
            catch (Exception err)
            {
                xmlDoc.LoadXml(string.Format("<error path=\"{0}\" relative=\"{1}\">{2}</error>",
                                             HttpContext.Current.Server.HtmlEncode(Path), Relative, err));
            }
            XPathNavigator xp = xmlDoc.CreateNavigator();
            return xp.Select("/");
        }

        /// <summary>
        /// Fetches a xml file from the specified url.
        /// the Url can be a local url or even from a remote server.
        /// </summary>
        /// <param name="Url">The URL.</param>
        /// <returns>The xml file as a XpathNodeIterator</returns>
        public static XPathNodeIterator GetXmlDocumentByUrl(string Url)
        {
            XmlDocument xmlDoc = new XmlDocument();
            WebRequest request = WebRequest.Create(Url);
            try
            {
                WebResponse response = request.GetResponse();
                Stream responseStream = response.GetResponseStream();
                XmlTextReader reader = new XmlTextReader(responseStream);

                xmlDoc.Load(reader);

                response.Close();
                responseStream.Close();
            }
            catch (Exception err)
            {
                xmlDoc.LoadXml(string.Format("<error url=\"{0}\">{1}</error>",
                                             HttpContext.Current.Server.HtmlEncode(Url), err));
            }
            XPathNavigator xp = xmlDoc.CreateNavigator();
            return xp.Select("/");
        }

        /// <summary>
        /// Gets the XML document by URL Cached.
        /// </summary>
        /// <param name="Url">The URL.</param>
        /// <param name="CacheInSeconds">The cache in seconds (so 900 would be 15 minutes). This is independent of the global cache refreshing, as it doesn't gets flushed on publishing (like the macros do)</param>
        /// <returns></returns>
        public static XPathNodeIterator GetXmlDocumentByUrl(string Url, int CacheInSeconds)
        {

            object urlCache =
                            HttpContext.Current.Cache.Get("GetXmlDoc_" + Url);
            if (urlCache != null)
                return (XPathNodeIterator)urlCache;
            else
            {
                XPathNodeIterator result =
                    GetXmlDocumentByUrl(Url);

                HttpContext.Current.Cache.Insert("GetXmlDoc_" + Url,
                    result, null, DateTime.Now.Add(new TimeSpan(0, 0, CacheInSeconds)), TimeSpan.Zero, System.Web.Caching.CacheItemPriority.Low, null);
                return result;
            }

        }

        /// <summary>
        /// Returns the Xpath query for a node with the specified id
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>The Xpath query for the node with the specified id as a string</returns>
        public static string QueryForNode(string id)
        {
            var xpathQuery = string.Empty;
            var preview = UmbracoContext.Current != null && UmbracoContext.Current.InPreviewMode;
            var xml = GetSafeContentCache().GetXml(preview);
            var elt = xml.GetElementById(id);

            if (elt == null) return xpathQuery;

            var path = elt.Attributes["path"].Value.Split((",").ToCharArray());
            for (var i = 1; i < path.Length; i++)
            {
                if (i > 1)
                    xpathQuery += "/node [@id = " + path[i] + "]";
                else
                    xpathQuery += " [@id = " + path[i] + "]";
            }

            return xpathQuery;
        }

        /// <summary>
        /// Helper function to get a value from a comma separated string. Usefull to get
        /// a node identifier from a Page's path string
        /// </summary>
        /// <param name="path">The comma separated string</param>
        /// <param name="level">The index to be returned</param>
        /// <returns>A string with the value of the index</returns>
        public static string GetNodeFromLevel(string path, int level)
        {
            try
            {
                string[] newPath = path.Split(',');
                if (newPath.Length >= level)
                    return newPath[level].ToString();
                else
                    return string.Empty;
            }
            catch
            {
                return "<!-- error in GetNodeFromLevel -->";
            }
        }

        /// <summary>
        /// Sends an e-mail using the System.Net.Mail.MailMessage object
        /// </summary>
        /// <param name="fromMail">The sender of the e-mail</param>
        /// <param name="toMail">The recipient(s) of the e-mail, add multiple email addresses by using a semicolon between them</param>
        /// <param name="subject">E-mail subject</param>
        /// <param name="body">The complete content of the e-mail</param>
        /// <param name="isHtml">Set to true when using Html formatted mails</param>
        public static void SendMail(string fromMail, string toMail, string subject, string body, bool isHtml)
        {
            try
            {
                var mailSender = new EmailSender();
                using (var mail = new MailMessage())
                {
                    mail.From = new MailAddress(fromMail.Trim());
                    foreach (var mailAddress in toMail.Split(';'))
                        mail.To.Add(new MailAddress(mailAddress.Trim()));
                    mail.Subject = subject;
                    mail.IsBodyHtml = isHtml;
                    mail.Body = body;
                    mailSender.Send(mail);
                }
            }
            catch (Exception ee)
            {
                Current.Logger.Error<library>("umbraco.library.SendMail: Error sending mail.", ee);
            }
        }

        /// <summary>
        /// These random methods are from Eli Robillards blog - kudos for the work :-)
        /// http://weblogs.asp.net/erobillard/archive/2004/05/06/127374.aspx
        ///
        /// Get a Random object which is cached between requests.
        /// The advantage over creating the object locally is that the .Next
        /// call will always return a new value. If creating several times locally
        /// with a generated seed (like millisecond ticks), the same number can be
        /// returned.
        /// </summary>
        /// <returns>A Random object which is cached between calls.</returns>
        public static Random GetRandom(int seed)
        {
            Random r = (Random)HttpContext.Current.Cache.Get("RandomNumber");
            if (r == null)
            {
                if (seed == 0)
                    r = new Random();
                else
                    r = new Random(seed);
                HttpContext.Current.Cache.Insert("RandomNumber", r);
            }
            return r;
        }

        /// <summary>
        /// GetRandom with no parameters.
        /// </summary>
        /// <returns>A Random object which is cached between calls.</returns>
        public static Random GetRandom()
        {
            return GetRandom(0);
        }

        /// <summary>
        /// Get any value from the current Request collection. Please note that there also specialized methods for
        /// Querystring, Form, Servervariables and Cookie collections
        /// </summary>
        /// <param name="key">Name of the Request element to be returned</param>
        /// <returns>A string with the value of the Requested element</returns>
        public static string Request(string key)
        {
            if (HttpContext.Current.Request[key] != null)
                return HttpContext.Current.Request[key];
            else
                return string.Empty;
        }

        /// <summary>
        /// Changes the mime type of the current page.
        /// </summary>
        /// <param name="MimeType">The mime type (like text/xml)</param>
        public static void ChangeContentType(string MimeType)
        {
            if (!String.IsNullOrEmpty(MimeType))
            {
                HttpContext.Current.Response.ContentType = MimeType;
            }
        }

        /// <summary>
        /// Get any value from the current Items collection.
        /// </summary>
        /// <param name="key">Name of the Items element to be returned</param>
        /// <returns>A string with the value of the Items element</returns>
        public static string ContextKey(string key)
        {
            if (HttpContext.Current.Items[key] != null)
                return HttpContext.Current.Items[key].ToString();
            else
                return string.Empty;
        }

        /// <summary>
        /// Get any value from the current Http Items collection
        /// </summary>
        /// <param name="key">Name of the Item element to be returned</param>
        /// <returns>A string with the value of the Requested element</returns>
        public static string GetHttpItem(string key)
        {
            if (HttpContext.Current.Items[key] != null)
                return HttpContext.Current.Items[key].ToString();
            else
                return string.Empty;
        }

        /// <summary>
        /// Get any value from the current Form collection
        /// </summary>
        /// <param name="key">Name of the Form element to be returned</param>
        /// <returns>A string with the value of the form element</returns>
        public static string RequestForm(string key)
        {
            if (HttpContext.Current.Request.Form[key] != null)
                return HttpContext.Current.Request.Form[key];
            else
                return string.Empty;
        }

        /// <summary>
        /// Get any value from the current Querystring collection
        /// </summary>
        /// <param name="key">Name of the querystring element to be returned</param>
        /// <returns>A string with the value of the querystring element</returns>
        public static string RequestQueryString(string key)
        {
            if (HttpContext.Current.Request.QueryString[key] != null)
                return HttpContext.Current.Request.QueryString[key];
            else
                return string.Empty;
        }

        /// <summary>
        /// Get any value from the users cookie collection
        /// </summary>
        /// <param name="key">Name of the cookie to return</param>
        /// <returns>A string with the value of the cookie</returns>
        public static string RequestCookies(string key)
        {
            // zb-00004 #29956 : refactor cookies handling
            var value = HttpContext.Current.Request.GetCookieValue(key);
            return value ?? "";
        }

        /// <summary>
        /// Get any element from the server variables collection
        /// </summary>
        /// <param name="key">The key for the element to be returned</param>
        /// <returns>A string with the value of the requested element</returns>
        public static string RequestServerVariables(string key)
        {
            if (HttpContext.Current.Request.ServerVariables[key] != null)
                return HttpContext.Current.Request.ServerVariables[key];
            else
                return string.Empty;
        }

        /// <summary>
        /// Get any element from current user session
        /// </summary>
        /// <param name="key">The key for the element to be returned</param>
        /// <returns>A string with the value of the requested element</returns>
        public static string Session(string key)
        {
            if (HttpContext.Current.Session != null && HttpContext.Current.Session[key] != null)
                return HttpContext.Current.Session[key].ToString();
            else
                return string.Empty;
        }

        /// <summary>
        /// Returns the current ASP.NET session identifier
        /// </summary>
        /// <returns>The current ASP.NET session identifier</returns>
        public static string SessionId()
        {
            if (HttpContext.Current.Session != null)
                return HttpContext.Current.Session.SessionID;
            else
                return string.Empty;
        }

        /// <summary>
        /// URL-encodes a string
        /// </summary>
        /// <param name="Text">The string to be encoded</param>
        /// <returns>A URL-encoded string</returns>
        public static string UrlEncode(string Text)
        {
            return HttpUtility.UrlEncode(Text);
        }

        /// <summary>
        /// HTML-encodes a string
        /// </summary>
        /// <param name="Text">The string to be encoded</param>
        /// <returns>A HTML-encoded string</returns>
        public static string HtmlEncode(string Text)
        {
            return HttpUtility.HtmlEncode(Text);
        }

        public static IRelation[] GetRelatedNodes(int nodeId)
        {
            return Current.Services.RelationService.GetByParentOrChildId(nodeId).ToArray();
        }

        /// <summary>
        /// Gets the related nodes, of the node with the specified Id, as XML.
        /// </summary>
        /// <param name="NodeId">The node id.</param>
        /// <returns>The related nodes as a XpathNodeIterator in the format:
        ///     <code>
        ///         <relations>
        ///             <relation typeId="[typeId]" typeName="[typeName]" createDate="[createDate]" parentId="[parentId]" childId="[childId]"><node>[standard umbraco node Xml]</node></relation>
        ///         </relations>
        ///     </code>
        /// </returns>
        public static XPathNodeIterator GetRelatedNodesAsXml(int NodeId)
        {
            XmlDocument xd = new XmlDocument();
            xd.LoadXml("<relations/>");
            var rels = Current.Services.RelationService.GetByParentOrChildId(NodeId);

            const bool published = true; // work with published versions?

            foreach (var r in rels)
            {
                XmlElement n = xd.CreateElement("relation");
                n.AppendChild(XmlHelper.AddCDataNode(xd, "comment", r.Comment));
                n.Attributes.Append(XmlHelper.AddAttribute(xd, "typeId", r.RelationTypeId.ToString()));
                n.Attributes.Append(XmlHelper.AddAttribute(xd, "typeName", r.RelationType.Name));
                n.Attributes.Append(XmlHelper.AddAttribute(xd, "createDate", r.CreateDate.ToString(CultureInfo.InvariantCulture)));
                n.Attributes.Append(XmlHelper.AddAttribute(xd, "parentId", r.ParentId.ToString()));
                n.Attributes.Append(XmlHelper.AddAttribute(xd, "childId", r.ChildId.ToString()));

                // Append the node that isn't the one we're getting the related nodes from
                if (NodeId == r.ChildId)
                {
                    var parent = Current.Services.ContentService.GetById(r.ParentId);
                    if (parent != null)
                    {
                        var x = EntityXmlSerializer.Serialize(
                            Current.Services.ContentService,
                            Current.Services.DataTypeService,
                            Current.Services.UserService,
                            Current.Services.LocalizationService,
                            Current.UrlSegmentProviders, parent, published).GetXmlNode(xd);
                        n.AppendChild(x);
                    }
                }
                else
                {
                    var child = Current.Services.ContentService.GetById(r.ChildId);
                    if (child != null)
                    {
                        var x = EntityXmlSerializer.Serialize(
                            Current.Services.ContentService,
                            Current.Services.DataTypeService,
                            Current.Services.UserService,
                            Current.Services.LocalizationService,
                            Current.UrlSegmentProviders, child, published).GetXmlNode(xd);
                        n.AppendChild(x);
                    }
                }

                xd.DocumentElement.AppendChild(n);
            }
            XPathNavigator xp = xd.CreateNavigator();
            return xp.Select(".");
        }

        /// <summary>
        /// Returns the identifier of the current page
        /// </summary>
        /// <returns>The identifier of the current page</returns>
        public int PageId()
        {
            if (_page != null)
                return _page.PageID;
            else
                return -1;
        }

        /// <summary>
        /// Returns the title of the current page
        /// </summary>
        /// <returns>The title of the current page</returns>
        public string PageName()
        {
            if (_page != null)
                return _page.PageName;
            else
                return string.Empty;
        }

        /// <summary>
        /// Returns any element from the currentpage including generic properties
        /// </summary>
        /// <param name="key">The name of the page element to return</param>
        /// <returns>A string with the element value</returns>
        public string PageElement(string key)
        {
            if (_page != null)
            {
                if (_page.Elements[key] != null)
                    return _page.Elements[key].ToString();
                else
                    return string.Empty;
            }
            else
                return string.Empty;
        }




        #endregion

        #region Template Control Mapping Functions

        /// <summary>
        /// Creates an Umbraco item for the specified field of the specified node.
        /// This brings the <c>umbraco:Item</c> element functionality to XSLT documents,
        /// which enables Live Editing of XSLT generated content.
        /// </summary>
        /// <param name="nodeId">The ID of the node to create.</param>
        /// <param name="fieldName">Name of the field to create.</param>
        /// <returns>An Umbraco item.</returns>
        public string Item(int nodeId, string fieldName)
        {
            return Item(nodeId, fieldName, null);
        }

        /// <summary>
        /// Creates an Umbraco item for the specified field of the specified node.
        /// This brings the <c>umbraco:Item</c> element functionality to XSLT documents,
        /// which enables Live Editing of XSLT generated content.
        /// </summary>
        /// <param name="nodeId">The ID of the node to create.</param>
        /// <param name="fieldName">Name of the field to create.</param>
        /// <param name="displayValue">
        ///     Value that is displayed to the user, which can be different from the field value.
        ///     Ignored if <c>null</c>.
        ///     Inside an XSLT document, an XPath expression might be useful to generate this value,
        ///     analogous to the functionality of the <c>Xslt</c> property of an <c>umbraco:Item</c> element.
        /// </param>
        /// <returns>An Umbraco item.</returns>
        public string Item(int nodeId, string fieldName, string displayValue)
        {
            // require a field name
            if (String.IsNullOrEmpty(fieldName))
                throw new ArgumentNullException("fieldName");

            // encode the display value, if present, as an inline XSLT expression
            // escaping is disabled, since the user can choose to set
            // disable-output-escaping="yes" on the value-of element calling this function.
            string xslt = displayValue == null
                          ? String.Empty
                          : string.Format("xslt=\"'{0}'\" xsltdisableescaping=\"true\"",
                                          HttpUtility.HtmlEncode(displayValue).Replace("'", "&amp;apos;"));

            // return a placeholder, the actual item will be created later on
            // in the CreateControlsFromText method of macro
            return string.Format("[[[[umbraco:Item nodeId=\"{0}\" field=\"{1}\" {2}]]]]", nodeId, fieldName, xslt);
        }

        #endregion
    }
}

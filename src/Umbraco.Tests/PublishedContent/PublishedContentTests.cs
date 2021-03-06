﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NUnit.Framework;
using Umbraco.Core;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web;
using Umbraco.Web.PublishedCache;
using Umbraco.Core.Composing;
using LightInject;
using Moq;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Tests.TestHelpers;
using Umbraco.Tests.Testing;
using Umbraco.Web.PropertyEditors;

namespace Umbraco.Tests.PublishedContent
{
    /// <summary>
    /// Tests the methods on IPublishedContent using the DefaultPublishedContentStore
    /// </summary>
    [TestFixture]
    [UmbracoTest(PluginManager = UmbracoTestOptions.PluginManager.PerFixture)]
    public class PublishedContentTests : PublishedContentTestBase
    {
        protected override void Compose()
        {
            base.Compose();

            Container.RegisterSingleton<IPublishedModelFactory>(f => new PublishedModelFactory(f.GetInstance<TypeLoader>().GetTypes<PublishedContentModel>()));
            Container.RegisterSingleton<IPublishedContentTypeFactory, PublishedContentTypeFactory>();

            var logger = Mock.Of<ILogger>();
            var dataTypeService = new TestObjects.TestDataTypeService(
                new DataType(new VoidEditor(logger)) { Id = 1},
                new DataType(new TrueFalsePropertyEditor(logger)) { Id = 1001 },
                new DataType(new RichTextPropertyEditor(logger)) { Id = 1002 },
                new DataType(new IntegerPropertyEditor(logger)) { Id = 1003 },
                new DataType(new TextboxPropertyEditor(logger)) { Id = 1004 },
                new DataType(new MediaPickerPropertyEditor(logger)) { Id = 1005 });
            Container.RegisterSingleton<IDataTypeService>(f => dataTypeService);
        }

        protected override void Initialize()
        {
            base.Initialize();

            var factory = Container.GetInstance<IPublishedContentTypeFactory>() as PublishedContentTypeFactory;

            // need to specify a custom callback for unit tests
            // AutoPublishedContentTypes generates properties automatically
            // when they are requested, but we must declare those that we
            // explicitely want to be here...

            var propertyTypes = new[]
            {
                // AutoPublishedContentType will auto-generate other properties
                factory.CreatePropertyType("umbracoNaviHide", 1001),
                factory.CreatePropertyType("selectedNodes", 1),
                factory.CreatePropertyType("umbracoUrlAlias", 1),
                factory.CreatePropertyType("content", 1002),
                factory.CreatePropertyType("testRecursive", 1),
            };
            var compositionAliases = new[] { "MyCompositionAlias" };
            var type = new AutoPublishedContentType(0, "anything", compositionAliases, propertyTypes);
            ContentTypesCache.GetPublishedContentTypeByAlias = alias => type;
        }

        protected override TypeLoader CreatePluginManager(IServiceFactory f)
        {
            var pluginManager = base.CreatePluginManager(f);

            // this is so the model factory looks into the test assembly
            pluginManager.AssembliesToScan = pluginManager.AssembliesToScan
                .Union(new[] { typeof(PublishedContentTests).Assembly })
                .ToList();

            return pluginManager;
        }

        private readonly Guid _node1173Guid = Guid.NewGuid();

        protected override string GetXmlContent(int templateId)
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
<!DOCTYPE root[
<!ELEMENT Home ANY>
<!ATTLIST Home id ID #REQUIRED>
<!ELEMENT CustomDocument ANY>
<!ATTLIST CustomDocument id ID #REQUIRED>
]>
<root id=""-1"">
    <Home id=""1046"" parentID=""-1"" level=""1"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""1"" createDate=""2012-06-12T14:13:17"" updateDate=""2012-07-20T18:50:43"" nodeName=""Home"" urlName=""home"" writerName=""admin"" creatorName=""admin"" path=""-1,1046"" isDoc="""">
        <content><![CDATA[]]></content>
        <umbracoUrlAlias><![CDATA[this/is/my/alias, anotheralias]]></umbracoUrlAlias>
        <umbracoNaviHide>1</umbracoNaviHide>
        <testRecursive><![CDATA[This is the recursive val]]></testRecursive>
        <Home id=""1173"" parentID=""1046"" level=""2"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""1"" createDate=""2012-07-20T18:06:45"" updateDate=""2012-07-20T19:07:31"" nodeName=""Sub1"" urlName=""sub1"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173"" isDoc="""" key=""" + _node1173Guid + @""">
            <content><![CDATA[<div>This is some content</div>]]></content>
            <umbracoUrlAlias><![CDATA[page2/alias, 2ndpagealias]]></umbracoUrlAlias>
            <testRecursive><![CDATA[]]></testRecursive>
            <Home id=""1174"" parentID=""1173"" level=""3"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""1"" createDate=""2012-07-20T18:07:54"" updateDate=""2012-07-20T19:10:27"" nodeName=""Sub2"" urlName=""sub2"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173,1174"" isDoc="""">
                <content><![CDATA[]]></content>
                <umbracoUrlAlias><![CDATA[only/one/alias]]></umbracoUrlAlias>
                <creatorName><![CDATA[Custom data with same property name as the member name]]></creatorName>
                <testRecursive><![CDATA[]]></testRecursive>
            </Home>
            <CustomDocument id=""1177"" parentID=""1173"" level=""3"" writerID=""0"" creatorID=""0"" nodeType=""1234"" template=""" + templateId + @""" sortOrder=""2"" createDate=""2012-07-16T15:26:59"" updateDate=""2012-07-18T14:23:35"" nodeName=""custom sub 1"" urlName=""custom-sub-1"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173,1177"" isDoc="""" />
            <CustomDocument id=""1178"" parentID=""1173"" level=""3"" writerID=""0"" creatorID=""0"" nodeType=""1234"" template=""" + templateId + @""" sortOrder=""3"" createDate=""2012-07-16T15:26:59"" updateDate=""2012-07-16T14:23:35"" nodeName=""custom sub 2"" urlName=""custom-sub-2"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173,1178"" isDoc="""" />
            <Home id=""1176"" parentID=""1173"" level=""3"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""4"" createDate=""2012-07-20T18:08:08"" updateDate=""2012-07-20T19:10:52"" nodeName=""Sub 3"" urlName=""sub-3"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173,1176"" isDoc="""" key=""CDB83BBC-A83B-4BA6-93B8-AADEF67D3C09"">
                <content><![CDATA[]]></content>
                <umbracoNaviHide>1</umbracoNaviHide>
            </Home>
        </Home>
        <Home id=""1175"" parentID=""1046"" level=""2"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""2"" createDate=""2012-07-20T18:08:01"" updateDate=""2012-07-20T18:49:32"" nodeName=""Sub 2"" urlName=""sub-2"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1175"" isDoc=""""><content><![CDATA[]]></content>
        </Home>
        <CustomDocument id=""4444"" parentID=""1046"" level=""2"" writerID=""0"" creatorID=""0"" nodeType=""1234"" template=""" + templateId + @""" sortOrder=""3"" createDate=""2012-07-16T15:26:59"" updateDate=""2012-07-18T14:23:35"" nodeName=""Test"" urlName=""test-page"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,4444"" isDoc="""">
            <selectedNodes><![CDATA[1172,1176,1173]]></selectedNodes>
        </CustomDocument>
    </Home>
    <CustomDocument id=""1172"" parentID=""-1"" level=""1"" writerID=""0"" creatorID=""0"" nodeType=""1234"" template=""" + templateId + @""" sortOrder=""2"" createDate=""2012-07-16T15:26:59"" updateDate=""2012-07-18T14:23:35"" nodeName=""Test"" urlName=""test-page"" writerName=""admin"" creatorName=""admin"" path=""-1,1172"" isDoc="""" />
</root>";
        }

        internal IPublishedContent GetNode(int id)
        {
            var ctx = GetUmbracoContext("/test");
            var doc = ctx.ContentCache.GetById(id);
            Assert.IsNotNull(doc);
            return doc;
        }

        [Test]
        public void GetNodeByIds()
        {
            var ctx = GetUmbracoContext("/test");
            var contentById = ctx.ContentCache.GetById(1173);
            Assert.IsNotNull(contentById);
            var contentByGuid = ctx.ContentCache.GetById(_node1173Guid);
            Assert.IsNotNull(contentByGuid);
            Assert.AreEqual(contentById.Id, contentByGuid.Id);
            Assert.AreEqual(contentById.Key, contentByGuid.Key);

            contentById = ctx.ContentCache.GetById(666);
            Assert.IsNull(contentById);
            contentByGuid = ctx.ContentCache.GetById(Guid.NewGuid());
            Assert.IsNull(contentByGuid);
        }

        [Test]
        public void Is_Last_From_Where_Filter_Dynamic_Linq()
        {
            var doc = GetNode(1173);

            var items = doc.Children.Where(x => x.IsVisible()).ToIndexedArray();

            foreach (var item in items)
            {
                if (item.Content.Id != 1178)
                {
                    Assert.IsFalse(item.IsLast());
                }
                else
                {
                    Assert.IsTrue(item.IsLast());
                }
            }
        }

        [Test]
        public void Is_Last_From_Where_Filter()
        {
            var doc = GetNode(1173);

            var items = doc
                .Children
                .Where(x => x.IsVisible())
                .ToIndexedArray();

            Assert.AreEqual(3, items.Length);

            foreach (var d in items)
            {
                switch (d.Content.Id)
                {
                    case 1174:
                        Assert.IsTrue(d.IsFirst());
                        Assert.IsFalse(d.IsLast());
                        break;
                    case 1177:
                        Assert.IsFalse(d.IsFirst());
                        Assert.IsFalse(d.IsLast());
                        break;
                    case 1178:
                        Assert.IsFalse(d.IsFirst());
                        Assert.IsTrue(d.IsLast());
                        break;
                    default:
                        Assert.Fail("Invalid id.");
                        break;
                }
            }
        }

        [PublishedModel("Home")]
        internal class Home : PublishedContentModel
        {
            public Home(IPublishedContent content)
                : base(content)
            {}
        }

        [Test]
        [Ignore("Fails as long as PublishedContentModel is internal.")] // fixme
        public void Is_Last_From_Where_Filter2()
        {
            var doc = GetNode(1173);

            var items = doc.Children
                .Select(x => x.CreateModel()) // linq, returns IEnumerable<IPublishedContent>

                // only way around this is to make sure every IEnumerable<T> extension
                // explicitely returns a PublishedContentSet, not an IEnumerable<T>

                .OfType<Home>() // ours, return IEnumerable<Home> (actually a PublishedContentSet<Home>)
                .Where(x => x.IsVisible()) // so, here it's linq again :-(
                .ToIndexedArray(); // so, we need that one for the test to pass

            Assert.AreEqual(1, items.Length);

            foreach (var d in items)
            {
                switch (d.Content.Id)
                {
                    case 1174:
                        Assert.IsTrue(d.IsFirst());
                        Assert.IsTrue(d.IsLast());
                        break;
                    default:
                        Assert.Fail("Invalid id.");
                        break;
                }
            }
        }

        [Test]
        public void Is_Last_From_Take()
        {
            var doc = GetNode(1173);

            var items = doc.Children.Take(3).ToIndexedArray();

            foreach (var item in items)
            {
                if (item.Content.Id != 1178)
                {
                    Assert.IsFalse(item.IsLast());
                }
                else
                {
                    Assert.IsTrue(item.IsLast());
                }
            }
        }

        [Test]
        public void Is_Last_From_Skip()
        {
            var doc = GetNode(1173);

            foreach (var d in doc.Children.Skip(1).ToIndexedArray())
            {
                if (d.Content.Id != 1176)
                {
                    Assert.IsFalse(d.IsLast());
                }
                else
                {
                    Assert.IsTrue(d.IsLast());
                }
            }
        }

        [Test]
        public void Is_Last_From_Concat()
        {
            var doc = GetNode(1173);

            var items = doc.Children
                .Concat(new[] { GetNode(1175), GetNode(4444) })
                .ToIndexedArray();

            foreach (var item in items)
            {
                if (item.Content.Id != 4444)
                {
                    Assert.IsFalse(item.IsLast());
                }
                else
                {
                    Assert.IsTrue(item.IsLast());
                }
            }
        }

        [Test]
        public void Descendants_Ordered_Properly()
        {
            var doc = GetNode(1046);

            var expected = new[] {1046, 1173, 1174, 1177, 1178, 1176, 1175, 4444, 1172};
            var exindex = 0;

            // must respect the XPath descendants-or-self axis!
            foreach (var d in doc.DescendantsOrSelf())
                Assert.AreEqual(expected[exindex++], d.Id);
        }

        [Test]
        public void GetPropertyValueRecursiveTest()
        {
            var doc = GetNode(1174);
            var rVal = doc.Value("testRecursive", true);
            var nullVal = doc.Value("DoNotFindThis", true);
            Assert.AreEqual("This is the recursive val", rVal);
            Assert.AreEqual(null, nullVal);
        }

        [Test]
        public void Get_Property_Value_Uses_Converter()
        {
            var doc = GetNode(1173);

            var propVal = doc.Value("content");
            Assert.IsInstanceOf(typeof(IHtmlString), propVal);
            Assert.AreEqual("<div>This is some content</div>", propVal.ToString());

            var propVal2 = doc.Value<IHtmlString>("content");
            Assert.IsInstanceOf(typeof(IHtmlString), propVal2);
            Assert.AreEqual("<div>This is some content</div>", propVal2.ToString());

            var propVal3 = doc.Value("Content");
            Assert.IsInstanceOf(typeof(IHtmlString), propVal3);
            Assert.AreEqual("<div>This is some content</div>", propVal3.ToString());
        }

        [Test]
        public void Complex_Linq()
        {
            var doc = GetNode(1173);

            var result = doc.Ancestors().OrderBy(x => x.Level)
                .Single()
                .Descendants()
                .FirstOrDefault(x => x.Value<string>("selectedNodes", "").Split(',').Contains("1173"));

            Assert.IsNotNull(result);
        }

        [Test]
        public void Children_GroupBy_DocumentTypeAlias()
        {
            var doc = GetNode(1046);

            var found1 = doc.Children.GroupBy(x => x.DocumentTypeAlias).ToArray();

            Assert.AreEqual(2, found1.Length);
            Assert.AreEqual(2, found1.Single(x => x.Key.ToString() == "Home").Count());
            Assert.AreEqual(1, found1.Single(x => x.Key.ToString() == "CustomDocument").Count());
        }

        [Test]
        public void Children_Where_DocumentTypeAlias()
        {
            var doc = GetNode(1046);

            var found1 = doc.Children.Where(x => x.DocumentTypeAlias == "CustomDocument");
            var found2 = doc.Children.Where(x => x.DocumentTypeAlias == "Home");

            Assert.AreEqual(1, found1.Count());
            Assert.AreEqual(2, found2.Count());
        }

        [Test]
        public void Children_Order_By_Update_Date()
        {
            var doc = GetNode(1173);

            var ordered = doc.Children.OrderBy(x => x.UpdateDate);

            var correctOrder = new[] { 1178, 1177, 1174, 1176 };
            for (var i = 0; i < correctOrder.Length; i++)
            {
                Assert.AreEqual(correctOrder[i], ordered.ElementAt(i).Id);
            }

        }

        [Test]
        public void FirstChild()
        {
            var doc = GetNode(1173); // has child nodes
            Assert.IsNotNull(doc.FirstChild());
            Assert.IsNotNull(doc.FirstChild(x => true));
            Assert.IsNotNull(doc.FirstChild<IPublishedContent>());

            doc = GetNode(1175); // does not have child nodes
            Assert.IsNull(doc.FirstChild());
            Assert.IsNull(doc.FirstChild(x => true));
            Assert.IsNull(doc.FirstChild<IPublishedContent>());
        }

        [Test]
        public void IsComposedOf()
        {
            var doc = GetNode(1173);

            var isComposedOf = doc.IsComposedOf("MyCompositionAlias");

            Assert.IsTrue(isComposedOf);
        }

        [Test]
        public void HasProperty()
        {
            var doc = GetNode(1173);

            var hasProp = doc.HasProperty(Constants.Conventions.Content.UrlAlias);

            Assert.IsTrue(hasProp);
        }

        [Test]
        public void HasValue()
        {
            var doc = GetNode(1173);

            var hasValue = doc.HasValue(Constants.Conventions.Content.UrlAlias);
            var noValue = doc.HasValue("blahblahblah");

            Assert.IsTrue(hasValue);
            Assert.IsFalse(noValue);
        }

        [Test]
        public void Ancestors_Where_Visible()
        {
            var doc = GetNode(1174);

            var whereVisible = doc.Ancestors().Where(x => x.IsVisible());
            Assert.AreEqual(1, whereVisible.Count());

        }

        [Test]
        public void Visible()
        {
            var hidden = GetNode(1046);
            var visible = GetNode(1173);

            Assert.IsFalse(hidden.IsVisible());
            Assert.IsTrue(visible.IsVisible());
        }

        [Test]
        public void Ancestor_Or_Self()
        {
            var doc = GetNode(1173);

            var result = doc.AncestorOrSelf();

            Assert.IsNotNull(result);

            // ancestor-or-self has to be self!
            Assert.AreEqual(1173, result.Id);
        }

        [Test]
        public void U4_4559()
        {
            var doc = GetNode(1174);
            var result = doc.AncestorOrSelf(1);
            Assert.IsNotNull(result);
            Assert.AreEqual(1046, result.Id);
        }

        [Test]
        public void Ancestors_Or_Self()
        {
            var doc = GetNode(1174);

            var result = doc.AncestorsOrSelf().ToArray();

            Assert.IsNotNull(result);

            Assert.AreEqual(3, result.Length);
            Assert.IsTrue(result.Select(x => ((dynamic)x).Id).ContainsAll(new dynamic[] { 1174, 1173, 1046 }));
        }

        [Test]
        public void Ancestors()
        {
            var doc = GetNode(1174);

            var result = doc.Ancestors().ToArray();

            Assert.IsNotNull(result);

            Assert.AreEqual(2, result.Length);
            Assert.IsTrue(result.Select(x => ((dynamic)x).Id).ContainsAll(new dynamic[] { 1173, 1046 }));
        }

        [Test]
        public void Descendants_Or_Self()
        {
            var doc = GetNode(1046);

            var result = doc.DescendantsOrSelf().ToArray();

            Assert.IsNotNull(result);

            Assert.AreEqual(8, result.Length);
            Assert.IsTrue(result.Select(x => ((dynamic)x).Id).ContainsAll(new dynamic[] { 1046, 1173, 1174, 1176, 1175 }));
        }

        [Test]
        public void Descendants()
        {
            var doc = GetNode(1046);

            var result = doc.Descendants().ToArray();

            Assert.IsNotNull(result);

            Assert.AreEqual(7, result.Length);
            Assert.IsTrue(result.Select(x => ((dynamic)x).Id).ContainsAll(new dynamic[] { 1173, 1174, 1176, 1175, 4444 }));
        }

        [Test]
        public void Up()
        {
            var doc = GetNode(1173);

            var result = doc.Up();

            Assert.IsNotNull(result);

            Assert.AreEqual(1046, result.Id);
        }

        [Test]
        public void Down()
        {
            var doc = GetNode(1173);

            var result = doc.Down();

            Assert.IsNotNull(result);

            Assert.AreEqual(1174, result.Id);
        }

        [Test]
        public void FragmentProperty()
        {
            var factory = Container.GetInstance<IPublishedContentTypeFactory>() as PublishedContentTypeFactory;

            var pt = factory.CreatePropertyType("detached", 1003);
            var ct = factory.CreateContentType(0, "alias", new[] { pt });
            var prop = new PublishedElementPropertyBase(pt, null, false, PropertyCacheLevel.None, 5548);
            Assert.IsInstanceOf<int>(prop.GetValue());
            Assert.AreEqual(5548, prop.GetValue());
        }

        public void Fragment1()
        {
            var type = ContentTypesCache.Get(PublishedItemType.Content, "detachedSomething");
            var values = new Dictionary<string, object>();
            var f = new PublishedElement(type, Guid.NewGuid(), values, false);
        }

        [Test]
        public void Fragment2()
        {
            var factory = Container.GetInstance<IPublishedContentTypeFactory>() as PublishedContentTypeFactory;

            var pt1 = factory.CreatePropertyType("legend", 1004);
            var pt2 = factory.CreatePropertyType("image", 1005);
            var pt3 = factory.CreatePropertyType("size", 1003);
            const string val1 = "boom bam";
            const int val2 = 0;
            const int val3 = 666;

            var guid = Guid.NewGuid();

            var ct = factory.CreateContentType(0, "alias", new[] { pt1, pt2, pt3 });

            var c = new ImageWithLegendModel(ct, guid, new Dictionary<string, object>
            {
                { "legend", val1 },
                { "image", val2 },
                { "size", val3 },
            }, false);

            Assert.AreEqual(val1, c.Legend);
            Assert.AreEqual(val3, c.Size);
        }

        class ImageWithLegendModel : PublishedElement
        {
            public ImageWithLegendModel(PublishedContentType contentType, Guid fragmentKey, Dictionary<string, object> values, bool previewing)
                : base(contentType, fragmentKey, values, previewing)
            { }


            public string Legend => this.Value<string>("legend");

            public IPublishedContent Image => this.Value<IPublishedContent>("image");

            public int Size => this.Value<int>("size");
        }
    }
}

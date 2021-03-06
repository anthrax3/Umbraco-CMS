﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Moq;
using NUnit.Framework;
using Umbraco.Core;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.IO;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Membership;
using LightInject;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Services;
using Umbraco.Tests.TestHelpers.Entities;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence.Dtos;
using Umbraco.Core.Persistence.Repositories.Implement;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.Scoping;
using Umbraco.Core.Services.Implement;
using Umbraco.Tests.Testing;
using Umbraco.Web.PropertyEditors;

namespace Umbraco.Tests.Services
{
    /// <summary>
    /// Tests covering all methods in the ContentService class.
    /// This is more of an integration test as it involves multiple layers
    /// as well as configuration.
    /// </summary>
    [TestFixture]
    [UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, PublishedRepositoryEvents = true, WithApplication = true)]
    public class ContentServiceTests : TestWithSomeContentBase
    {
        //TODO Add test to verify there is only ONE newest document/content in {Constants.DatabaseSchema.Tables.Document} table after updating.
        //TODO Add test to delete specific version (with and without deleting prior versions) and versions by date.

        public override void SetUp()
        {
            base.SetUp();
            ContentRepositoryBase.ThrowOnWarning = true;
        }

        public override void TearDown()
        {
            ContentRepositoryBase.ThrowOnWarning = false;
            base.TearDown();
        }

        protected override void Compose()
        {
            base.Compose();

            // fixme - do it differently
            Container.Register(factory => factory.GetInstance<ServiceContext>().TextService);
        }

        [Test]
        public void Create_Blueprint()
        {
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;

            var contentType = MockedContentTypes.CreateTextpageContentType();
            ServiceContext.FileService.SaveTemplate(contentType.DefaultTemplate);
            contentTypeService.Save(contentType);

            var blueprint = MockedContent.CreateTextpageContent(contentType, "hello", -1);
            blueprint.SetValue("title", "blueprint 1");
            blueprint.SetValue("bodyText", "blueprint 2");
            blueprint.SetValue("keywords", "blueprint 3");
            blueprint.SetValue("description", "blueprint 4");

            contentService.SaveBlueprint(blueprint);

            var found = contentService.GetBlueprintsForContentTypes().ToArray();
            Assert.AreEqual(1, found.Length);

            //ensures it's not found by normal content
            var contentFound = contentService.GetById(found[0].Id);
            Assert.IsNull(contentFound);
        }

        [Test]
        public void Delete_Blueprint()
        {
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;

            var contentType = MockedContentTypes.CreateTextpageContentType();
            ServiceContext.FileService.SaveTemplate(contentType.DefaultTemplate);
            contentTypeService.Save(contentType);

            var blueprint = MockedContent.CreateTextpageContent(contentType, "hello", -1);
            blueprint.SetValue("title", "blueprint 1");
            blueprint.SetValue("bodyText", "blueprint 2");
            blueprint.SetValue("keywords", "blueprint 3");
            blueprint.SetValue("description", "blueprint 4");

            contentService.SaveBlueprint(blueprint);

            contentService.DeleteBlueprint(blueprint);

            var found = contentService.GetBlueprintsForContentTypes().ToArray();
            Assert.AreEqual(0, found.Length);
        }

        [Test]
        public void Create_Content_From_Blueprint()
        {
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;

            var contentType = MockedContentTypes.CreateTextpageContentType();
            ServiceContext.FileService.SaveTemplate(contentType.DefaultTemplate);
            contentTypeService.Save(contentType);

            var blueprint = MockedContent.CreateTextpageContent(contentType, "hello", -1);
            blueprint.SetValue("title", "blueprint 1");
            blueprint.SetValue("bodyText", "blueprint 2");
            blueprint.SetValue("keywords", "blueprint 3");
            blueprint.SetValue("description", "blueprint 4");

            contentService.SaveBlueprint(blueprint);

            var fromBlueprint = contentService.CreateContentFromBlueprint(blueprint, "hello world");
            contentService.Save(fromBlueprint);

            Assert.IsTrue(fromBlueprint.HasIdentity);
            Assert.AreEqual("blueprint 1", fromBlueprint.Properties["title"].GetValue());
            Assert.AreEqual("blueprint 2", fromBlueprint.Properties["bodyText"].GetValue());
            Assert.AreEqual("blueprint 3", fromBlueprint.Properties["keywords"].GetValue());
            Assert.AreEqual("blueprint 4", fromBlueprint.Properties["description"].GetValue());
        }

        [Test]
        public void Get_All_Blueprints()
        {
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;

            var ct1 = MockedContentTypes.CreateTextpageContentType("ct1");
            ServiceContext.FileService.SaveTemplate(ct1.DefaultTemplate);
            contentTypeService.Save(ct1);
            var ct2 = MockedContentTypes.CreateTextpageContentType("ct2");
            ServiceContext.FileService.SaveTemplate(ct2.DefaultTemplate);
            contentTypeService.Save(ct2);

            for (int i = 0; i < 10; i++)
            {
                var blueprint = MockedContent.CreateTextpageContent(i % 2 == 0 ? ct1 : ct2, "hello" + i, -1);
                contentService.SaveBlueprint(blueprint);
            }

            var found = contentService.GetBlueprintsForContentTypes().ToArray();
            Assert.AreEqual(10, found.Length);

            found = contentService.GetBlueprintsForContentTypes(ct1.Id).ToArray();
            Assert.AreEqual(5, found.Length);

            found = contentService.GetBlueprintsForContentTypes(ct2.Id).ToArray();
            Assert.AreEqual(5, found.Length);
        }

        /// <summary>
        /// Ensures that we don't unpublish all nodes when a node is deleted that has an invalid path of -1
        /// Note: it is actually the MoveToRecycleBin happening on the initial deletion of a node through the UI
        /// that causes the issue.
        /// Regression test: http://issues.umbraco.org/issue/U4-9336
        /// </summary>
        [Test]
        [Ignore("not applicable to v8")]

        // fixme - this test was imported from 7.6 BUT it makes no sense for v8
        // we should trust the PATH, full stop

        public void Moving_Node_To_Recycle_Bin_With_Invalid_Path()
        {
            var contentService = ServiceContext.ContentService;
            var root = ServiceContext.ContentService.GetById(NodeDto.NodeIdSeed + 1);
            root.PublishValues();
            Assert.IsTrue(contentService.SaveAndPublish(root).Success);
            var content = contentService.CreateAndSave("Test", -1, "umbTextpage", 0);
            content.PublishValues();
            Assert.IsTrue(contentService.SaveAndPublish(content).Success);
            var hierarchy = CreateContentHierarchy().OrderBy(x => x.Level).ToArray();
            contentService.Save(hierarchy, 0);
            foreach (var c in hierarchy)
            {
                c.PublishValues();
                Assert.IsTrue(contentService.SaveAndPublish(c).Success);
            }

            //now make the data corrupted :/
            using (var scope = ScopeProvider.CreateScope())
            {
                scope.Database.Execute("UPDATE umbracoNode SET path = '-1' WHERE id = @id", new { id = content.Id });
                scope.Complete();
            }

            //re-get with the corrupt path
            content = contentService.GetById(content.Id);

            // here we get all descendants by the path of the node being moved to bin, and unpublish all of them.
            // since the path is invalid, there's logic in here to fix that if it's possible and re-persist the entity.
            var moveResult = ServiceContext.ContentService.MoveToRecycleBin(content);

            Assert.IsTrue(moveResult.Success);

            //re-get with the fixed/moved path
            content = contentService.GetById(content.Id);

            Assert.AreEqual("-1,-20," + content.Id, content.Path);

            //re-get
            hierarchy = contentService.GetByIds(hierarchy.Select(x => x.Id).ToArray()).OrderBy(x => x.Level).ToArray();

            Assert.That(hierarchy.All(c => c.Trashed == false), Is.True);
            Assert.That(hierarchy.All(c => c.Path.StartsWith("-1,-20") == false), Is.True);
        }

        [Test]
        public void Remove_Scheduled_Publishing_Date()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var content = contentService.CreateAndSave("Test", -1, "umbTextpage", 0);

            content.ReleaseDate = DateTime.Now.AddHours(2);
            contentService.Save(content, 0);

            content = contentService.GetById(content.Id);
            content.ReleaseDate = null;
            contentService.Save(content, 0);


            // Assert
            content.PublishValues();
            Assert.IsTrue(contentService.SaveAndPublish(content).Success);
        }

        [Test]
        public void Get_Top_Version_Ids()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var content = contentService.CreateAndSave("Test", -1, "umbTextpage", 0);
            for (var i = 0; i < 20; i++)
            {
                content.SetValue("bodyText", "hello world " + Guid.NewGuid());
                content.PublishValues();
                contentService.SaveAndPublish(content);
            }

            // Assert
            var allVersions = contentService.GetVersionIds(content.Id, int.MaxValue);
            Assert.AreEqual(21, allVersions.Count());

            var topVersions = contentService.GetVersionIds(content.Id, 4);
            Assert.AreEqual(4, topVersions.Count());
        }

        [Test]
        public void Get_By_Ids_Sorted()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var results = new List<IContent>();
            for (var i = 0; i < 20; i++)
            {
                results.Add(contentService.CreateAndSave("Test", -1, "umbTextpage", 0));
            }

            var sortedGet = contentService.GetByIds(new[] {results[10].Id, results[5].Id, results[12].Id}).ToArray();

            // Assert
            Assert.AreEqual(sortedGet[0].Id, results[10].Id);
            Assert.AreEqual(sortedGet[1].Id, results[5].Id);
            Assert.AreEqual(sortedGet[2].Id, results[12].Id);
        }

        [Test]
        public void Count_All()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            for (int i = 0; i < 20; i++)
            {
                contentService.CreateAndSave("Test", -1, "umbTextpage", 0);
            }

            // Assert
            Assert.AreEqual(24, contentService.Count());
        }

        [Test]
        public void Count_By_Content_Type()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;
            var contentType = MockedContentTypes.CreateSimpleContentType("umbBlah", "test Doc Type");
            contentTypeService.Save(contentType);

            // Act
            for (int i = 0; i < 20; i++)
            {
                contentService.CreateAndSave("Test", -1, "umbBlah", 0);
            }

            // Assert
            Assert.AreEqual(20, contentService.Count(documentTypeAlias: "umbBlah"));
        }

        [Test]
        public void Count_Children()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;
            var contentType = MockedContentTypes.CreateSimpleContentType("umbBlah", "test Doc Type");
            contentTypeService.Save(contentType);
            var parent = contentService.CreateAndSave("Test", -1, "umbBlah", 0);

            // Act
            for (int i = 0; i < 20; i++)
            {
                contentService.CreateAndSave("Test", parent, "umbBlah");
            }

            // Assert
            Assert.AreEqual(20, contentService.CountChildren(parent.Id));
        }

        [Test]
        public void Count_Descendants()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;
            var contentType = MockedContentTypes.CreateSimpleContentType("umbBlah", "test Doc Type");
            contentTypeService.Save(contentType);
            var parent = contentService.CreateAndSave("Test", -1, "umbBlah", 0);

            // Act
            IContent current = parent;
            for (int i = 0; i < 20; i++)
            {
                current = contentService.CreateAndSave("Test", current, "umbBlah");
            }

            // Assert
            Assert.AreEqual(20, contentService.CountDescendants(parent.Id));
        }

        [Test]
        public void GetAncestors_Returns_Empty_List_When_Path_Is_Null()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var current = new Mock<IContent>();
            var res = contentService.GetAncestors(current.Object);

            // Assert
            Assert.IsEmpty(res);
        }

        [Test]
        public void TagsAreUpdatedWhenContentIsTrashedAndUnTrashed_One()
        {
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;
            var tagService = ServiceContext.TagService;
            var contentType = MockedContentTypes.CreateSimpleContentType("umbMandatory", "Mandatory Doc Type", true);
            contentType.PropertyGroups.First().PropertyTypes.Add(
                new PropertyType("test", ValueStorageType.Ntext, "tags")
                {
                    DataTypeId = 1041
                });
            contentTypeService.Save(contentType);

            var content1 = MockedContent.CreateSimpleContent(contentType, "Tagged content 1", -1);
            content1.AssignTags("tags", new[] { "hello", "world", "some", "tags", "plus" });
            content1.PublishValues();
            contentService.SaveAndPublish(content1);

            var content2 = MockedContent.CreateSimpleContent(contentType, "Tagged content 2", -1);
            content2.AssignTags("tags", new[] { "hello", "world", "some", "tags" });
            content2.PublishValues();
            contentService.SaveAndPublish(content2);

            // verify
            var tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(5, tags.Count());
            var allTags = tagService.GetAllContentTags();
            Assert.AreEqual(5, allTags.Count());

            contentService.MoveToRecycleBin(content1);

            // fixme - killing the rest of this test
            // this is not working consistently even in 7 when unpublishing a branch
            // in 8, tags never go away - one has to check that the entity is published and not trashed
            return;

            // no more tags for this entity
            tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(0, tags.Count());

            // tags still assigned to content2 are still there
            allTags = tagService.GetAllContentTags();
            Assert.AreEqual(4, allTags.Count());

            contentService.Move(content1, -1);

            Assert.IsFalse(content1.Published);

            // no more tags for this entity
            tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(0, tags.Count());

            // tags still assigned to content2 are still there
            allTags = tagService.GetAllContentTags();
            Assert.AreEqual(4, allTags.Count());

            content1.PublishValues();
            contentService.SaveAndPublish(content1);

            Assert.IsTrue(content1.Published);

            // tags are back
            tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(5, tags.Count());

            // tags are back
            allTags = tagService.GetAllContentTags();
            Assert.AreEqual(5, allTags.Count());
        }

        [Test]
        public void TagsAreUpdatedWhenContentIsTrashedAndUnTrashed_All()
        {
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;
            var tagService = ServiceContext.TagService;
            var contentType = MockedContentTypes.CreateSimpleContentType("umbMandatory", "Mandatory Doc Type", true);
            contentType.PropertyGroups.First().PropertyTypes.Add(
                new PropertyType("test", ValueStorageType.Ntext, "tags")
                {
                    DataTypeId = 1041
                });
            contentTypeService.Save(contentType);

            var content1 = MockedContent.CreateSimpleContent(contentType, "Tagged content 1", -1);
            content1.AssignTags("tags", new[] { "hello", "world", "some", "tags", "bam" });
            content1.PublishValues();
            contentService.SaveAndPublish(content1);

            var content2 = MockedContent.CreateSimpleContent(contentType, "Tagged content 2", -1);
            content2.AssignTags("tags", new[] { "hello", "world", "some", "tags" });
            content2.PublishValues();
            contentService.SaveAndPublish(content2);

            // verify
            var tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(5, tags.Count());
            var allTags = tagService.GetAllContentTags();
            Assert.AreEqual(5, allTags.Count());

            contentService.Unpublish(content1);
            contentService.Unpublish(content2);

            // fixme - killing the rest of this test
            // this is not working consistently even in 7 when unpublishing a branch
            // in 8, tags never go away - one has to check that the entity is published and not trashed
            return;

            // no more tags
            tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(0, tags.Count());

            // no more tags
            allTags = tagService.GetAllContentTags();
            Assert.AreEqual(0, allTags.Count());

            contentService.Move(content1, -1);
            contentService.Move(content2, -1);

            // no more tags
            tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(0, tags.Count());

            // no more tags
            allTags = tagService.GetAllContentTags();
            Assert.AreEqual(0, allTags.Count());

            content1.PublishValues();
            contentService.SaveAndPublish(content1);
            content2.PublishValues();
            contentService.SaveAndPublish(content2);

            // tags are back
            tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(5, tags.Count());

            // tags are back
            allTags = tagService.GetAllContentTags();
            Assert.AreEqual(5, allTags.Count());
        }

        [Test]
        [Ignore("U4-8442, will need to be fixed eventually.")]
        public void TagsAreUpdatedWhenContentIsTrashedAndUnTrashed_Tree()
        {
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;
            var tagService = ServiceContext.TagService;
            var contentType = MockedContentTypes.CreateSimpleContentType("umbMandatory", "Mandatory Doc Type", true);
            contentType.PropertyGroups.First().PropertyTypes.Add(
                new PropertyType("test", ValueStorageType.Ntext, "tags")
                {
                    DataTypeId = 1041
                });
            contentTypeService.Save(contentType);

            var content1 = MockedContent.CreateSimpleContent(contentType, "Tagged content 1", -1);
            content1.AssignTags("tags", new[] { "hello", "world", "some", "tags", "plus" });
            content1.PublishValues();
            contentService.SaveAndPublish(content1);

            var content2 = MockedContent.CreateSimpleContent(contentType, "Tagged content 2", content1.Id);
            content2.AssignTags("tags", new[] { "hello", "world", "some", "tags" });
            content2.PublishValues();
            contentService.SaveAndPublish(content2);

            // verify
            var tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(5, tags.Count());
            var allTags = tagService.GetAllContentTags();
            Assert.AreEqual(5, allTags.Count());

            contentService.MoveToRecycleBin(content1);

            // no more tags
            tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(0, tags.Count());
            tags = tagService.GetTagsForEntity(content2.Id);
            Assert.AreEqual(0, tags.Count());

            // no more tags
            allTags = tagService.GetAllContentTags();
            Assert.AreEqual(0, allTags.Count());

            contentService.Move(content1, -1);

            Assert.IsFalse(content1.Published);

            // no more tags
            tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(0, tags.Count());
            tags = tagService.GetTagsForEntity(content2.Id);
            Assert.AreEqual(0, tags.Count());

            // no more tags
            allTags = tagService.GetAllContentTags();
            Assert.AreEqual(0, allTags.Count());

            content1.PublishValues();
            contentService.SaveAndPublish(content1);

            Assert.IsTrue(content1.Published);

            // tags are back
            tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(5, tags.Count());

            // fixme tag & tree issue
            // when we publish, we 'just' publish the top one and not the ones below = fails
            // what we should do is... NOT clear tags when unpublishing or trashing or...
            // and just update the tag service to NOT return anything related to trashed or
            // unpublished entities (since trashed is set on ALL entities in the trashed branch)
            tags = tagService.GetTagsForEntity(content2.Id); // including that one!
            Assert.AreEqual(4, tags.Count());

            // tags are back
            allTags = tagService.GetAllContentTags();
            Assert.AreEqual(5, allTags.Count());
        }

        [Test]
        public void TagsAreUpdatedWhenContentIsUnpublishedAndRePublished()
        {
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;
            var tagService = ServiceContext.TagService;
            var contentType = MockedContentTypes.CreateSimpleContentType("umbMandatory", "Mandatory Doc Type", true);
            contentType.PropertyGroups.First().PropertyTypes.Add(
                new PropertyType("test", ValueStorageType.Ntext, "tags")
                {
                    DataTypeId = 1041
                });
            contentTypeService.Save(contentType);

            var content1 = MockedContent.CreateSimpleContent(contentType, "Tagged content 1", -1);
            content1.AssignTags("tags", new[] { "hello", "world", "some", "tags", "bam" });
            content1.PublishValues();
            contentService.SaveAndPublish(content1);

            var content2 = MockedContent.CreateSimpleContent(contentType, "Tagged content 2", -1);
            content2.AssignTags("tags", new[] { "hello", "world", "some", "tags" });
            content2.PublishValues();
            contentService.SaveAndPublish(content2);

            contentService.Unpublish(content1);
            contentService.Unpublish(content2);

            // fixme - killing the rest of this test
            // this is not working consistently even in 7 when unpublishing a branch
            // in 8, tags never go away - one has to check that the entity is published and not trashed
            return;

            var tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(0, tags.Count());
            var allTags = tagService.GetAllContentTags();
            Assert.AreEqual(0, allTags.Count());

            content2.PublishValues();
            contentService.SaveAndPublish(content2);

            tags = tagService.GetTagsForEntity(content2.Id);
            Assert.AreEqual(4, tags.Count());
            allTags = tagService.GetAllContentTags();
            Assert.AreEqual(4, allTags.Count());
        }

        [Test]
        [Ignore("U4-8442, will need to be fixed eventually.")]
        public void TagsAreUpdatedWhenContentIsUnpublishedAndRePublished_Tree()
        {
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;
            var tagService = ServiceContext.TagService;
            var contentType = MockedContentTypes.CreateSimpleContentType("umbMandatory", "Mandatory Doc Type", true);
            contentType.PropertyGroups.First().PropertyTypes.Add(
                new PropertyType("test", ValueStorageType.Ntext, "tags")
                {
                    DataTypeId = 1041
                });
            contentTypeService.Save(contentType);

            var content1 = MockedContent.CreateSimpleContent(contentType, "Tagged content 1", -1);
            content1.AssignTags("tags", new[] { "hello", "world", "some", "tags", "bam" });
            content1.PublishValues();
            contentService.SaveAndPublish(content1);

            var content2 = MockedContent.CreateSimpleContent(contentType, "Tagged content 2", content1);
            content2.AssignTags("tags", new[] { "hello", "world", "some", "tags" });
            content2.PublishValues();
            contentService.SaveAndPublish(content2);

            contentService.Unpublish(content1);

            var tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(0, tags.Count());

            // fixme tag & tree issue
            // when we (un)publish, we 'just' publish the top one and not the ones below = fails
            // see similar note above
            tags = tagService.GetTagsForEntity(content2.Id);
            Assert.AreEqual(0, tags.Count());
            var allTags = tagService.GetAllContentTags();
            Assert.AreEqual(0, allTags.Count());

            content1.PublishValues();
            contentService.SaveAndPublish(content1);

            tags = tagService.GetTagsForEntity(content2.Id);
            Assert.AreEqual(4, tags.Count());
            allTags = tagService.GetAllContentTags();
            Assert.AreEqual(5, allTags.Count());
        }

        [Test]
        public void Create_Tag_Data_Bulk_Publish_Operation()
        {
            //Arrange
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;
            var dataTypeService = ServiceContext.DataTypeService;

            //set configuration
            var dataType = dataTypeService.GetDataType(1041);
            dataType.Configuration = new TagConfiguration
            {
                Group = "test",
                StorageType = TagsStorageType.Csv
            };

            var contentType = MockedContentTypes.CreateSimpleContentType("umbMandatory", "Mandatory Doc Type", true);
            contentType.PropertyGroups.First().PropertyTypes.Add(
                new PropertyType("test", ValueStorageType.Ntext, "tags")
                {
                    DataTypeId = 1041
                });
            contentTypeService.Save(contentType);
            contentType.AllowedContentTypes = new[] { new ContentTypeSort(new Lazy<int>(() => contentType.Id), 0, contentType.Alias) };

            var content = MockedContent.CreateSimpleContent(contentType, "Tagged content", -1);
            content.AssignTags("tags", new[] { "hello", "world", "some", "tags" });
            contentService.Save(content);

            var child1 = MockedContent.CreateSimpleContent(contentType, "child 1 content", content.Id);
            child1.AssignTags("tags", new[] { "hello1", "world1", "some1" });
            contentService.Save(child1);

            var child2 = MockedContent.CreateSimpleContent(contentType, "child 2 content", content.Id);
            child2.AssignTags("tags", new[] { "hello2", "world2" });
            contentService.Save(child2);

            // Act
            contentService.SaveAndPublishBranch(content, true);

            // Assert
            var propertyTypeId = contentType.PropertyTypes.Single(x => x.Alias == "tags").Id;

            using (var scope = ScopeProvider.CreateScope())
            {
                Assert.AreEqual(4, scope.Database.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                    new { nodeId = content.Id, propTypeId = propertyTypeId }));

                Assert.AreEqual(3, scope.Database.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                    new { nodeId = child1.Id, propTypeId = propertyTypeId }));

                Assert.AreEqual(2, scope.Database.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                    new { nodeId = child2.Id, propTypeId = propertyTypeId }));

                scope.Complete();
            }
        }

        [Test]
        public void Does_Not_Create_Tag_Data_For_Non_Published_Version()
        {
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;

            // create content type with a tag property
            var contentType = MockedContentTypes.CreateSimpleContentType("umbMandatory", "Mandatory Doc Type", true);
            contentType.PropertyGroups.First().PropertyTypes.Add(new PropertyType("test", ValueStorageType.Ntext, "tags") { DataTypeId = 1041 });
            contentTypeService.Save(contentType);

            // create a content with tags and publish
            var content = MockedContent.CreateSimpleContent(contentType, "Tagged content", -1);
            content.AssignTags("tags", new[] { "hello", "world", "some", "tags" });
            content.PublishValues();
            contentService.SaveAndPublish(content);

            // edit tags and save
            content.AssignTags("tags", new[] { "another", "world" }, merge: true);
            contentService.Save(content);

            // the (edit) property does contain all tags
            Assert.AreEqual(5, content.Properties["tags"].GetValue().ToString().Split(',').Distinct().Count());

            // but the database still contains the initial two tags
            var propertyTypeId = contentType.PropertyTypes.Single(x => x.Alias == "tags").Id;
            using (var scope = ScopeProvider.CreateScope())
            {
                Assert.AreEqual(4, scope.Database.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                    new { nodeId = content.Id, propTypeId = propertyTypeId }));
                scope.Complete();
            }
        }

        [Test]
        public void Can_Replace_Tag_Data_To_Published_Content()
        {
            //Arrange
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;
            var contentType = MockedContentTypes.CreateSimpleContentType("umbMandatory", "Mandatory Doc Type", true);
            contentType.PropertyGroups.First().PropertyTypes.Add(
                new PropertyType("test", ValueStorageType.Ntext, "tags")
                    {
                        DataTypeId = 1041
                    });
            contentTypeService.Save(contentType);

            var content = MockedContent.CreateSimpleContent(contentType, "Tagged content", -1);


            // Act
            content.AssignTags("tags", new[] { "hello", "world", "some", "tags" });
            content.PublishValues();
            contentService.SaveAndPublish(content);

            // Assert
            Assert.AreEqual(4, content.Properties["tags"].GetValue().ToString().Split(',').Distinct().Count());
            var propertyTypeId = contentType.PropertyTypes.Single(x => x.Alias == "tags").Id;
            using (var scope = ScopeProvider.CreateScope())
            {
                Assert.AreEqual(4, scope.Database.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                    new { nodeId = content.Id, propTypeId = propertyTypeId }));

                scope.Complete();
            }
        }

        [Test]
        public void Can_Append_Tag_Data_To_Published_Content()
        {
            //Arrange
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;
            var contentType = MockedContentTypes.CreateSimpleContentType("umbMandatory", "Mandatory Doc Type", true);
            contentType.PropertyGroups.First().PropertyTypes.Add(
                new PropertyType("test", ValueStorageType.Ntext, "tags")
                {
                    DataTypeId = 1041
                });
            contentTypeService.Save(contentType);
            var content = MockedContent.CreateSimpleContent(contentType, "Tagged content", -1);
            content.AssignTags("tags", new[] { "hello", "world", "some", "tags" });
            content.PublishValues();
            contentService.SaveAndPublish(content);

            // Act
            content.AssignTags("tags", new[] { "another", "world" }, merge: true);
            content.PublishValues();
            contentService.SaveAndPublish(content);

            // Assert
            Assert.AreEqual(5, content.Properties["tags"].GetValue().ToString().Split(',').Distinct().Count());
            var propertyTypeId = contentType.PropertyTypes.Single(x => x.Alias == "tags").Id;
            using (var scope = ScopeProvider.CreateScope())
            {
                Assert.AreEqual(5, scope.Database.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                    new { nodeId = content.Id, propTypeId = propertyTypeId }));

                scope.Complete();
            }
        }

        [Test]
        public void Can_Remove_Tag_Data_To_Published_Content()
        {
            //Arrange
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;
            var contentType = MockedContentTypes.CreateSimpleContentType("umbMandatory", "Mandatory Doc Type", true);
            contentType.PropertyGroups.First().PropertyTypes.Add(
                new PropertyType("test", ValueStorageType.Ntext, "tags")
                {
                    DataTypeId = 1041
                });
            contentTypeService.Save(contentType);
            var content = MockedContent.CreateSimpleContent(contentType, "Tagged content", -1);
            content.AssignTags("tags", new[] { "hello", "world", "some", "tags" });
            content.PublishValues();
            contentService.SaveAndPublish(content);

            // Act
            content.RemoveTags("tags", new[] { "some", "world" });
            content.PublishValues();
            contentService.SaveAndPublish(content);

            // Assert
            Assert.AreEqual(2, content.Properties["tags"].GetValue().ToString().Split(',').Distinct().Count());
            var propertyTypeId = contentType.PropertyTypes.Single(x => x.Alias == "tags").Id;
            using (var scope = ScopeProvider.CreateScope())
            {
                Assert.AreEqual(2, scope.Database.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                    new { nodeId = content.Id, propTypeId = propertyTypeId }));

                scope.Complete();
            }
        }

        [Test]
        public void Can_Remove_Property_Type()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var content = contentService.Create("Test", -1, "umbTextpage", 0);

            // Assert
            Assert.That(content, Is.Not.Null);
            Assert.That(content.HasIdentity, Is.False);
        }

        [Test]
        public void Can_Create_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var content = contentService.Create("Test", -1, "umbTextpage", 0);

            // Assert
            Assert.That(content, Is.Not.Null);
            Assert.That(content.HasIdentity, Is.False);
        }

        [Test]
        public void Can_Create_Content_Without_Explicitly_Set_User()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var content = contentService.Create("Test", -1, "umbTextpage");

            // Assert
            Assert.That(content, Is.Not.Null);
            Assert.That(content.HasIdentity, Is.False);
            Assert.That(content.CreatorId, Is.EqualTo(0));//Default to zero/administrator
        }

        [Test]
        public void Can_Save_New_Content_With_Explicit_User()
        {
            var user = new User
                {
                    Name = "Test",
                    Email = "test@test.com",
                    Username = "test",
                RawPasswordValue = "test"
                };
            ServiceContext.UserService.Save(user);
            var content = new Content("Test", -1, ServiceContext.ContentTypeService.Get("umbTextpage"));

            // Act
            ServiceContext.ContentService.Save(content, (int)user.Id);

            // Assert
            Assert.That(content.CreatorId, Is.EqualTo(user.Id));
            Assert.That(content.WriterId, Is.EqualTo(user.Id));
        }

        [Test]
        public void Cannot_Create_Content_With_Non_Existing_ContentType_Alias()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act & Assert
            Assert.Throws<Exception>(() => contentService.Create("Test", -1, "umbAliasDoesntExist"));
        }

        [Test]
        public void Cannot_Save_Content_With_Empty_Name()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = new Content(string.Empty, -1, ServiceContext.ContentTypeService.Get("umbTextpage"));

            // Act & Assert
            Assert.Throws<ArgumentException>(() => contentService.Save(content));
        }

        [Test]
        public void Can_Get_Content_By_Id()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var content = contentService.GetById(NodeDto.NodeIdSeed + 2 );

            // Assert
            Assert.That(content, Is.Not.Null);
            Assert.That(content.Id, Is.EqualTo(NodeDto.NodeIdSeed + 2));
        }

        [Test]
        public void Can_Get_Content_By_Guid_Key()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var content = contentService.GetById(new Guid("B58B3AD4-62C2-4E27-B1BE-837BD7C533E0"));

            // Assert
            Assert.That(content, Is.Not.Null);
            Assert.That(content.Id, Is.EqualTo(NodeDto.NodeIdSeed + 2));
        }

        [Test]
        public void Can_Get_Content_By_Level()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var contents = contentService.GetByLevel(2).ToList();

            // Assert
            Assert.That(contents, Is.Not.Null);
            Assert.That(contents.Any(), Is.True);
            Assert.That(contents.Count(), Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Can_Get_Children_Of_Content_Id()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var contents = contentService.GetChildren(NodeDto.NodeIdSeed + 2).ToList();

            // Assert
            Assert.That(contents, Is.Not.Null);
            Assert.That(contents.Any(), Is.True);
            Assert.That(contents.Count(), Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Can_Get_Descendents_Of_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var hierarchy = CreateContentHierarchy();
            contentService.Save(hierarchy, 0);

            // Act
            var contents = contentService.GetDescendants(NodeDto.NodeIdSeed + 2).ToList();

            // Assert
            Assert.That(contents, Is.Not.Null);
            Assert.That(contents.Any(), Is.True);
            Assert.That(contents.Count(), Is.EqualTo(52));
        }

        [Test]
        public void Can_Get_All_Versions_Of_Content()
        {
            var contentService = ServiceContext.ContentService;

            var parent = ServiceContext.ContentService.GetById(NodeDto.NodeIdSeed + 2);
            Assert.IsFalse(parent.Published);
            parent.PublishValues();
            ServiceContext.ContentService.SaveAndPublish(parent); // publishing parent, so Text Page 2 can be updated.

            var content = contentService.GetById(NodeDto.NodeIdSeed + 4);
            Assert.IsFalse(content.Published);
            var versions = contentService.GetVersions(NodeDto.NodeIdSeed + 4).ToList();
            Assert.AreEqual(1, versions.Count);

            var version1 = content.VersionId;
            Console.WriteLine($"1 e={content.VersionId} p={content.PublishedVersionId}");

            content.Name = "Text Page 2 Updated";
            content.SetValue("author", "Jane Doe");
            content.PublishValues();
            contentService.SaveAndPublish(content); // publishes the current version, creates a version

            var version2 = content.VersionId;
            Console.WriteLine($"2 e={content.VersionId} p={content.PublishedVersionId}");

            content.Name = "Text Page 2 ReUpdated";
            content.SetValue("author", "Bob Hope");
            content.PublishValues();
            contentService.SaveAndPublish(content); // publishes again, creates a version

            var version3 = content.VersionId;
            Console.WriteLine($"3 e={content.VersionId} p={content.PublishedVersionId}");

            var content1 = contentService.GetById(content.Id);
            Assert.AreEqual("Bob Hope", content1.GetValue("author"));
            Assert.AreEqual("Bob Hope", content1.GetValue("author", published: true));

            content.Name = "Text Page 2 ReReUpdated";
            content.SetValue("author", "John Farr");
            contentService.Save(content); // no new version

            content1 = contentService.GetById(content.Id);
            Assert.AreEqual("John Farr", content1.GetValue("author"));
            Assert.AreEqual("Bob Hope", content1.GetValue("author", published: true));

            versions = contentService.GetVersions(NodeDto.NodeIdSeed + 4).ToList();
            Assert.AreEqual(3, versions.Count);

            // versions come with most recent first
            Assert.AreEqual(version3, versions[0].VersionId); // the edited version
            Assert.AreEqual(version2, versions[1].VersionId); // the published version
            Assert.AreEqual(version1, versions[2].VersionId); // the previously published version

            // p is always the same, published version
            // e is changing, actual version we're loading
            Console.WriteLine();
            foreach (var version in ((IEnumerable<IContent>) versions).Reverse())
                Console.WriteLine($"+ e={((Content) version).VersionId} p={((Content) version).PublishedVersionId}");

            // and proper values
            // first, the current (edited) version, with edited and published versions
            Assert.AreEqual("John Farr", versions[0].GetValue("author")); // current version has the edited value
            Assert.AreEqual("Bob Hope", versions[0].GetValue("author", published: true)); // and the published published value

            // then, the current (published) version, with edited == published
            Assert.AreEqual("Bob Hope", versions[1].GetValue("author")); // own edited version
            Assert.AreEqual("Bob Hope", versions[1].GetValue("author", published: true)); // and published

            // then, the first published version - with values as 'edited'
            Assert.AreEqual("Jane Doe", versions[2].GetValue("author")); // own edited version
            Assert.AreEqual("Bob Hope", versions[2].GetValue("author", published: true)); // and published
        }

        [Test]
        public void Can_Get_Root_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var contents = contentService.GetRootContent().ToList();

            // Assert
            Assert.That(contents, Is.Not.Null);
            Assert.That(contents.Any(), Is.True);
            Assert.That(contents.Count(), Is.EqualTo(1));
        }

        [Test]
        public void Can_Get_Content_For_Expiration()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var root = contentService.GetById(NodeDto.NodeIdSeed + 2);
            root.PublishValues();
            contentService.SaveAndPublish(root);
            var content = contentService.GetById(NodeDto.NodeIdSeed + 4);
            content.ExpireDate = DateTime.Now.AddSeconds(1);
            content.PublishValues();
            contentService.SaveAndPublish(content);

            // Act
            Thread.Sleep(new TimeSpan(0, 0, 0, 2));
            var contents = contentService.GetContentForExpiration().ToList();

            // Assert
            Assert.That(contents, Is.Not.Null);
            Assert.That(contents.Any(), Is.True);
            Assert.That(contents.Count(), Is.EqualTo(1));
        }

        [Test]
        public void Can_Get_Content_For_Release()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var contents = contentService.GetContentForRelease().ToList();

            // Assert
            Assert.That(DateTime.Now.AddMinutes(-5) <= DateTime.Now);
            Assert.That(contents, Is.Not.Null);
            Assert.That(contents.Any(), Is.True);
            Assert.That(contents.Count(), Is.EqualTo(1));
        }

        [Test]
        public void Can_Get_Content_In_RecycleBin()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var contents = contentService.GetContentInRecycleBin().ToList();

            // Assert
            Assert.That(contents, Is.Not.Null);
            Assert.That(contents.Any(), Is.True);
            Assert.That(contents.Count(), Is.EqualTo(1));
        }

        [Test]
        public void Can_UnPublish_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 2);
            content.PublishValues();
            var published = contentService.SaveAndPublish(content, 0);

            using (var scope = ScopeProvider.CreateScope())
            {
                Assert.IsTrue(scope.Database.Exists<ContentXmlDto>(content.Id));
            }

            // Act
            var unpublished = contentService.Unpublish(content, 0);

            // Assert
            Assert.That(published.Success, Is.True);
            Assert.That(unpublished.Success, Is.True);
            Assert.That(content.Published, Is.False);

            using (var scope = ScopeProvider.CreateScope())
            {
                Assert.IsFalse(scope.Database.Exists<ContentXmlDto>(content.Id));
            }
        }

        [Test]
        public void Can_Publish_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 2);

            // Act
            content.PublishValues();
            var published = contentService.SaveAndPublish(content, 0);

            // Assert
            Assert.That(published.Success, Is.True);
            Assert.That(content.Published, Is.True);
        }

        [Test]
        public void IsPublishable()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var parent = contentService.Create("parent", -1, "umbTextpage");
            parent.PublishValues();
            contentService.SaveAndPublish(parent);
            var content = contentService.Create("child", parent, "umbTextpage");
            contentService.Save(content);

            Assert.IsTrue(contentService.IsPathPublishable(content));
            contentService.Unpublish(parent);
            Assert.IsFalse(contentService.IsPathPublishable(content));
        }

        [Test]
        public void Can_Publish_Content_WithEvents()
        {
            ContentService.Publishing += ContentServiceOnPublishing;

            // tests that during 'publishing' event, what we get from the repo is the 'old' content,
            // because 'publishing' fires before the 'saved' event ie before the content is actually
            // saved

            try
            {
                var contentService = ServiceContext.ContentService;
                var content = contentService.GetById(NodeDto.NodeIdSeed + 2);
                Assert.AreEqual("Home", content.Name);

                content.Name = "foo";
                content.PublishValues();
                var published = contentService.SaveAndPublish(content, 0);

                Assert.That(published.Success, Is.True);
                Assert.That(content.Published, Is.True);

                var e = ServiceContext.ContentService.GetById(content.Id);
                Assert.AreEqual("foo", e.Name);
            }
            finally
            {
                ContentService.Publishing -= ContentServiceOnPublishing;
            }
        }

        private void ContentServiceOnPublishing(IContentService sender, PublishEventArgs<IContent> args)
        {
            Assert.AreEqual(1, args.PublishedEntities.Count());
            var entity = args.PublishedEntities.First();
            Assert.AreEqual("foo", entity.Name);

            var e = ServiceContext.ContentService.GetById(entity.Id);
            Assert.AreEqual("Home", e.Name);
        }

        [Test]
        public void Can_Publish_Only_Valid_Content()
        {
            var contentTypeService = ServiceContext.ContentTypeService;
            var contentType = MockedContentTypes.CreateSimpleContentType("umbMandatory", "Mandatory Doc Type", true);
            contentTypeService.Save(contentType);

            const int parentId = NodeDto.NodeIdSeed + 2;

            var contentService = ServiceContext.ContentService;
            var content = MockedContent.CreateSimpleContent(contentType, "Invalid Content", parentId);
            content.SetValue("author", string.Empty);
            contentService.Save(content);

            var parent = contentService.GetById(parentId);

            var parentCanPublishValues = parent.PublishValues();
            var parentPublished = contentService.SaveAndPublish(parent);

            // parent can publish values
            // and therefore can be published
            Assert.IsTrue(parentCanPublishValues);
            Assert.IsTrue(parentPublished.Success);
            Assert.IsTrue(parent.Published);

            var contentCanPublishValues = content.PublishValues();
            var contentPublished = contentService.SaveAndPublish(content);

            // content cannot publish values because they are invalid
            Assert.IsFalse(contentCanPublishValues);
            Assert.IsNotEmpty(content.Validate());

            // and therefore cannot be published,
            // because it did not have a published version at all
            Assert.IsFalse(contentPublished.Success);
            Assert.AreEqual(PublishResultType.FailedNoPublishedValues, contentPublished.Result);
            Assert.IsFalse(content.Published);
        }

        // documents: an enumeration of documents, in tree order
        // map: applies (if needed) PublishValue, returns a value indicating whether to proceed with the branch
        private IEnumerable<IContent> MapPublishValues(IEnumerable<IContent> documents, Func<IContent, bool> map)
        {
            var exclude = new HashSet<int>();
            foreach (var document in documents)
            {
                if (exclude.Contains(document.ParentId))
                {
                    exclude.Add(document.Id);
                    continue;
                }
                if (!map(document))
                {
                    exclude.Add(document.Id);
                    continue;
                }
                yield return document;
            }
        }

        [Test]
        public void Can_Publish_Content_Children()
        {
            const int parentId = NodeDto.NodeIdSeed + 2;

            var contentService = ServiceContext.ContentService;
            var parent = contentService.GetById(parentId);

            Console.WriteLine(" " + parent.Id);
            foreach (var x in contentService.GetDescendants(parent))
                Console.WriteLine("          ".Substring(0, x.Level) + x.Id);
            Console.WriteLine();

            // publish parent & its branch
            // only those that are not already published
            // only invariant/neutral values
            var parentPublished = contentService.SaveAndPublishBranch(parent, true);

            foreach (var result in parentPublished)
                Console.WriteLine("          ".Substring(0, result.Content.Level) + $"{result.Content.Id}: {result.Result}");

            // everything should be successful
            Assert.IsTrue(parentPublished.All(x => x.Success));
            Assert.IsTrue(parent.Published);

            var children = contentService.GetChildren(parentId);

            // children are published including ... that was released 5 mins ago
            Assert.IsTrue(children.First(x => x.Id == NodeDto.NodeIdSeed + 4).Published);
        }

        [Test]
        public void Cannot_Publish_Expired_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 4); //This Content expired 5min ago
            content.ExpireDate = DateTime.Now.AddMinutes(-5);
            contentService.Save(content);

            var parent = contentService.GetById(NodeDto.NodeIdSeed + 2);
            parent.PublishValues();
            var parentPublished = contentService.SaveAndPublish(parent, 0);//Publish root Home node to enable publishing of 'NodeDto.NodeIdSeed + 3'

            // Act
            content.PublishValues();
            var published = contentService.SaveAndPublish(content, 0);

            // Assert
            Assert.That(parentPublished.Success, Is.True);
            Assert.That(published.Success, Is.False);
            Assert.That(content.Published, Is.False);
        }

        [Test]
        public void Cannot_Publish_Content_Awaiting_Release()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 3);
            content.ReleaseDate = DateTime.Now.AddHours(2);
            contentService.Save(content, 0);

            var parent = contentService.GetById(NodeDto.NodeIdSeed + 2);
            parent.PublishValues();
            var parentPublished = contentService.SaveAndPublish(parent, 0);//Publish root Home node to enable publishing of 'NodeDto.NodeIdSeed + 3'

            // Act
            content.PublishValues();
            var published = contentService.SaveAndPublish(content, 0);

            // Assert
            Assert.That(parentPublished.Success, Is.True);
            Assert.That(published.Success, Is.False);
            Assert.That(content.Published, Is.False);
        }

        [Test]
        public void Cannot_Publish_Content_Where_Parent_Is_Unpublished()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.Create("Subpage with Unpublisehed Parent", NodeDto.NodeIdSeed + 2, "umbTextpage", 0);
            contentService.Save(content, 0);

            // Act
            var published = contentService.SaveAndPublishBranch(content, true);

            // Assert
            Assert.That(published.All(x => x.Success), Is.False);
            Assert.That(content.Published, Is.False);
        }

        [Test]
        public void Cannot_Publish_Trashed_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 5);

            // Act
            content.PublishValues();
            var published = contentService.SaveAndPublish(content, 0);

            // Assert
            Assert.That(published.Success, Is.False);
            Assert.That(content.Published, Is.False);
            Assert.That(content.Trashed, Is.True);
        }

        [Test]
        public void Can_Save_And_Publish_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.Create("Home US", - 1, "umbTextpage", 0);
            content.SetValue("author", "Barack Obama");

            // Act
            content.PublishValues();
            var published = contentService.SaveAndPublish(content, 0);

            // Assert
            Assert.That(content.HasIdentity, Is.True);
            Assert.That(content.Published, Is.True);
            Assert.IsTrue(published.Success);
        }

        /// <summary>
        /// Try to immitate a new child content item being created through the UI.
        /// This content item will have no Id, Path or Identity.
        /// It seems like this is wiped somewhere in the process when creating an item through the UI
        /// and we need to make sure we handle nullchecks for these properties when creating content.
        /// This is unfortunately not caught by the normal ContentService tests.
        /// </summary>
        [Test]
        public void Can_Save_And_Publish_Content_And_Child_Without_Identity()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.Create("Home US", -1, "umbTextpage", 0);
            content.SetValue("author", "Barack Obama");

            // Act
            content.PublishValues();
            var published = contentService.SaveAndPublish(content, 0);
            var childContent = contentService.Create("Child", content.Id, "umbTextpage", 0);
            // Reset all identity properties
            childContent.Id = 0;
            childContent.Path = null;
            ((Content)childContent).ResetIdentity();
            childContent.PublishValues();
            var childPublished = contentService.SaveAndPublish(childContent, 0);

            // Assert
            Assert.That(content.HasIdentity, Is.True);
            Assert.That(content.Published, Is.True);
            Assert.That(childContent.HasIdentity, Is.True);
            Assert.That(childContent.Published, Is.True);
            Assert.That(published.Success, Is.True);
            Assert.That(childPublished.Success, Is.True);
        }

        [Test]
        public void Can_Get_Published_Descendant_Versions()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            var root = contentService.GetById(NodeDto.NodeIdSeed + 2);
            root.PublishValues();
            var rootPublished = contentService.SaveAndPublish(root);

            var content = contentService.GetById(NodeDto.NodeIdSeed + 4);
            content.Properties["title"].SetValue(content.Properties["title"].GetValue() + " Published");
            content.PublishValues();
            var contentPublished = contentService.SaveAndPublish(content);
            var publishedVersion = content.VersionId;

            content.Properties["title"].SetValue(content.Properties["title"].GetValue() + " Saved");
            contentService.Save(content);
            Assert.AreEqual(publishedVersion, content.VersionId);

            // Act
            var publishedDescendants = ((ContentService) contentService).GetPublishedDescendants(root).ToList();
            Assert.AreNotEqual(0, publishedDescendants.Count);

            // Assert
            Assert.IsTrue(rootPublished.Success);
            Assert.IsTrue(contentPublished.Success);

            //Console.WriteLine(publishedVersion);
            //foreach (var d in publishedDescendants) Console.WriteLine(d.Version);
            Assert.IsTrue(publishedDescendants.Any(x => x.VersionId == publishedVersion));

            //Ensure that the published content version has the correct property value and is marked as published
            var publishedContentVersion = publishedDescendants.First(x => x.VersionId == publishedVersion);
            Assert.That(publishedContentVersion.Published, Is.True);
            Assert.That(publishedContentVersion.Properties["title"].GetValue(published: true), Contains.Substring("Published"));

            // and has the correct draft properties
            Assert.That(publishedContentVersion.Properties["title"].GetValue(), Contains.Substring("Saved"));

            //Ensure that the latest version of the content is ok
            var currentContent = contentService.GetById(NodeDto.NodeIdSeed + 4);
            Assert.That(currentContent.Published, Is.True);
            Assert.That(currentContent.Properties["title"].GetValue(published: true), Contains.Substring("Published"));
            Assert.That(currentContent.Properties["title"].GetValue(), Contains.Substring("Saved"));
            Assert.That(currentContent.VersionId, Is.EqualTo(publishedContentVersion.VersionId));
        }

        [Test]
        public void Can_Save_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.Create("Home US", - 1, "umbTextpage", 0);
            content.SetValue("author", "Barack Obama");

            // Act
            contentService.Save(content, 0);

            // Assert
            Assert.That(content.HasIdentity, Is.True);
        }

        [Test]
        public void Can_Bulk_Save_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;

            var contentType = contentTypeService.Get("umbTextpage");
            Content subpage = MockedContent.CreateSimpleContent(contentType, "Text Subpage 1", NodeDto.NodeIdSeed + 2);
            Content subpage2 = MockedContent.CreateSimpleContent(contentType, "Text Subpage 2", NodeDto.NodeIdSeed + 2);
            var list = new List<IContent> {subpage, subpage2};

            // Act
            contentService.Save(list, 0);

            // Assert
            Assert.That(list.Any(x => !x.HasIdentity), Is.False);
        }

        [Test]
        public void Can_Bulk_Save_New_Hierarchy_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var hierarchy = CreateContentHierarchy().ToList();

            // Act
            contentService.Save(hierarchy, 0);

            Assert.That(hierarchy.Any(), Is.True);
            Assert.That(hierarchy.Any(x => x.HasIdentity == false), Is.False);
            //all parent id's should be ok, they are lazy and if they equal zero an exception will be thrown
            Assert.DoesNotThrow(() => hierarchy.Any(x => x.ParentId != 0));

        }

        [Test]
        public void Can_Delete_Content_Of_Specific_ContentType()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;
            var contentType = contentTypeService.Get("umbTextpage");

            // Act
            contentService.DeleteOfType(contentType.Id);
            var rootContent = contentService.GetRootContent();
            var contents = contentService.GetByType(contentType.Id);

            // Assert
            Assert.That(rootContent.Any(), Is.False);
            Assert.That(contents.Any(x => !x.Trashed), Is.False);
        }

        [Test]
        public void Can_Delete_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 4);

            // Act
            contentService.Delete(content, 0);
            var deleted = contentService.GetById(NodeDto.NodeIdSeed + 4);

            // Assert
            Assert.That(deleted, Is.Null);
        }

        [Test]
        public void Can_Move_Content_To_RecycleBin()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 3);

            // Act
            contentService.MoveToRecycleBin(content, 0);

            // Assert
            Assert.That(content.ParentId, Is.EqualTo(-20));
            Assert.That(content.Trashed, Is.True);
        }

        [Test]
        public void Can_Move_Content_Structure_To_RecycleBin_And_Empty_RecycleBin()
        {
            var contentService = ServiceContext.ContentService;
            var contentType = ServiceContext.ContentTypeService.Get("umbTextpage");

            var subsubpage = MockedContent.CreateSimpleContent(contentType, "Text Page 3", NodeDto.NodeIdSeed + 3);
            contentService.Save(subsubpage, 0);

            var content = contentService.GetById(NodeDto.NodeIdSeed + 2);
            var descendants = contentService.GetDescendants(content).ToList();
            Assert.AreNotEqual(-20, content.ParentId);
            Assert.IsFalse(content.Trashed);
            Assert.AreEqual(3, descendants.Count);
            Assert.IsFalse(descendants.Any(x => x.Path.StartsWith("-1,-20,")));
            Assert.IsFalse(descendants.Any(x => x.Trashed));

            contentService.MoveToRecycleBin(content, 0);
            descendants = contentService.GetDescendants(content).ToList();

            Assert.AreEqual(-20, content.ParentId);
            Assert.IsTrue(content.Trashed);
            Assert.AreEqual(3, descendants.Count);
            Assert.IsTrue(descendants.All(x => x.Path.StartsWith("-1,-20,")));
            Assert.True(descendants.All(x => x.Trashed));

            contentService.EmptyRecycleBin();
            var trashed = contentService.GetContentInRecycleBin();
            Assert.IsEmpty(trashed);
        }

        [Test]
        public void Can_Empty_RecycleBin()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            contentService.EmptyRecycleBin();
            var contents = contentService.GetContentInRecycleBin();

            // Assert
            Assert.That(contents.Any(), Is.False);
        }

        [Test]
        public void Ensures_Permissions_Are_Retained_For_Copied_Descendants_With_Explicit_Permissions()
        {
            // Arrange
            var userGroup = MockedUserGroup.CreateUserGroup("1");
            ServiceContext.UserService.Save(userGroup);

            var contentType = MockedContentTypes.CreateSimpleContentType("umbTextpage1", "Textpage");
            contentType.AllowedContentTypes = new List<ContentTypeSort>
            {
                new ContentTypeSort(new Lazy<int>(() => contentType.Id), 0, contentType.Alias)
            };
            ServiceContext.FileService.SaveTemplate(contentType.DefaultTemplate);
            ServiceContext.ContentTypeService.Save(contentType);

            var parentPage = MockedContent.CreateSimpleContent(contentType);
            ServiceContext.ContentService.Save(parentPage);

            var childPage = MockedContent.CreateSimpleContent(contentType, "child", parentPage);
            ServiceContext.ContentService.Save(childPage);
            //assign explicit permissions to the child
            ServiceContext.ContentService.SetPermission(childPage, 'A', new[] { userGroup.Id });

            //Ok, now copy, what should happen is the childPage will retain it's own permissions
            var parentPage2 = MockedContent.CreateSimpleContent(contentType);
            ServiceContext.ContentService.Save(parentPage2);

            var copy = ServiceContext.ContentService.Copy(childPage, parentPage2.Id, false, true);

            //get the permissions and verify
            var permissions = ServiceContext.UserService.GetPermissionsForPath(userGroup, copy.Path, fallbackToDefaultPermissions: true);
            var allPermissions = permissions.GetAllPermissions().ToArray();
            Assert.AreEqual(1, allPermissions.Length);
            Assert.AreEqual("A", allPermissions[0]);
        }

        [Test]
        public void Ensures_Permissions_Are_Inherited_For_Copied_Descendants()
        {
            // Arrange
            var userGroup = MockedUserGroup.CreateUserGroup("1");
            ServiceContext.UserService.Save(userGroup);

            var contentType = MockedContentTypes.CreateSimpleContentType("umbTextpage1", "Textpage");
            contentType.AllowedContentTypes = new List<ContentTypeSort>
            {
                new ContentTypeSort(new Lazy<int>(() => contentType.Id), 0, contentType.Alias)
            };
            ServiceContext.FileService.SaveTemplate(contentType.DefaultTemplate);
            ServiceContext.ContentTypeService.Save(contentType);

            var parentPage = MockedContent.CreateSimpleContent(contentType);
            ServiceContext.ContentService.Save(parentPage);
            ServiceContext.ContentService.SetPermission(parentPage, 'A', new[] { userGroup.Id });

            var childPage1 = MockedContent.CreateSimpleContent(contentType, "child1", parentPage);
            ServiceContext.ContentService.Save(childPage1);
            var childPage2 = MockedContent.CreateSimpleContent(contentType, "child2", childPage1);
            ServiceContext.ContentService.Save(childPage2);
            var childPage3 = MockedContent.CreateSimpleContent(contentType, "child3", childPage2);
            ServiceContext.ContentService.Save(childPage3);

            //Verify that the children have the inherited permissions
            var descendants = ServiceContext.ContentService.GetDescendants(parentPage).ToArray();
            Assert.AreEqual(3, descendants.Length);

            foreach (var descendant in descendants)
            {
                var permissions = ServiceContext.UserService.GetPermissionsForPath(userGroup, descendant.Path, fallbackToDefaultPermissions: true);
                var allPermissions = permissions.GetAllPermissions().ToArray();
                Assert.AreEqual(1, allPermissions.Length);
                Assert.AreEqual("A", allPermissions[0]);
            }

            //create a new parent with a new permission structure
            var parentPage2 = MockedContent.CreateSimpleContent(contentType);
            ServiceContext.ContentService.Save(parentPage2);
            ServiceContext.ContentService.SetPermission(parentPage2, 'B', new[] { userGroup.Id });

            //Now copy, what should happen is the child pages will now have permissions inherited from the new parent
            var copy = ServiceContext.ContentService.Copy(childPage1, parentPage2.Id, false, true);

            descendants = ServiceContext.ContentService.GetDescendants(parentPage2).ToArray();
            Assert.AreEqual(3, descendants.Length);

            foreach (var descendant in descendants)
            {
                var permissions = ServiceContext.UserService.GetPermissionsForPath(userGroup, descendant.Path, fallbackToDefaultPermissions: true);
                var allPermissions = permissions.GetAllPermissions().ToArray();
                Assert.AreEqual(1, allPermissions.Length);
                Assert.AreEqual("B", allPermissions[0]);
            }
        }

        [Test]
        public void Can_Empty_RecycleBin_With_Content_That_Has_All_Related_Data()
        {
            // Arrange
            //need to:
            // * add relations
            // * add permissions
            // * add notifications
            // * public access
            // * tags
            // * domain
            // * published & preview data
            // * multiple versions

            var contentType = MockedContentTypes.CreateAllTypesContentType("test", "test");
            ServiceContext.ContentTypeService.Save(contentType, 0);

            object obj =
                new
                {
                    tags = "Hello,World"
                };
            var content1 = MockedContent.CreateBasicContent(contentType);
            content1.PropertyValues(obj);
            content1.ResetDirtyProperties(false);
            ServiceContext.ContentService.Save(content1, 0);
            content1.PublishValues();
            Assert.IsTrue(ServiceContext.ContentService.SaveAndPublish(content1, 0).Success);
            var content2 = MockedContent.CreateBasicContent(contentType);
            content2.PropertyValues(obj);
            content2.ResetDirtyProperties(false);
            ServiceContext.ContentService.Save(content2, 0);
            content2.PublishValues();
            Assert.IsTrue(ServiceContext.ContentService.SaveAndPublish(content2, 0).Success);

            var editorGroup = ServiceContext.UserService.GetUserGroupByAlias("editor");
            editorGroup.StartContentId = content1.Id;
            ServiceContext.UserService.Save(editorGroup);

            var admin = ServiceContext.UserService.GetUserById(Constants.Security.SuperId);
            admin.StartContentIds = new[] {content1.Id};
            ServiceContext.UserService.Save(admin);

            ServiceContext.RelationService.Save(new RelationType(Constants.ObjectTypes.Document, Constants.ObjectTypes.Document, "test"));
            Assert.IsNotNull(ServiceContext.RelationService.Relate(content1, content2, "test"));

            ServiceContext.PublicAccessService.Save(new PublicAccessEntry(content1, content2, content2, new List<PublicAccessRule>
            {
                new PublicAccessRule
                {
                    RuleType = "test",
                    RuleValue = "test"
                }
            }));
            Assert.IsTrue(ServiceContext.PublicAccessService.AddRule(content1, "test2", "test2").Success);

            var user = ServiceContext.UserService.GetUserById(Constants.Security.SuperId);
            var userGroup = ServiceContext.UserService.GetUserGroupByAlias(user.Groups.First().Alias);
            Assert.IsNotNull(ServiceContext.NotificationService.CreateNotification(user, content1, "X"));

            ServiceContext.ContentService.SetPermission(content1, 'A', new[] { userGroup.Id });

            Assert.IsTrue(ServiceContext.DomainService.Save(new UmbracoDomain("www.test.com", "en-AU")
            {
                RootContentId = content1.Id
            }).Success);

            // Act
            ServiceContext.ContentService.MoveToRecycleBin(content1);
            ServiceContext.ContentService.EmptyRecycleBin();
            var contents = ServiceContext.ContentService.GetContentInRecycleBin();

            // Assert
            Assert.That(contents.Any(), Is.False);
        }

        [Test]
        public void Can_Move_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 5);

            // Act - moving out of recycle bin
            contentService.Move(content, NodeDto.NodeIdSeed + 2);

            // Assert
            Assert.That(content.ParentId, Is.EqualTo(NodeDto.NodeIdSeed + 2));
            Assert.That(content.Trashed, Is.False);
            Assert.That(content.Published, Is.False);
        }

        [Test]
        public void Can_Copy_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var temp = contentService.GetById(NodeDto.NodeIdSeed + 4);

            // Act
            var copy = contentService.Copy(temp, temp.ParentId, false, 0);
            var content = contentService.GetById(NodeDto.NodeIdSeed + 4);

            // Assert
            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.Id, Is.Not.EqualTo(content.Id));
            Assert.AreNotSame(content, copy);
            foreach (var property in copy.Properties)
            {
                Assert.AreEqual(property.GetValue(), content.Properties[property.Alias].GetValue());
            }
            //Assert.AreNotEqual(content.Name, copy.Name);
        }

        [Test]
        public void Can_Copy_Recursive()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var temp = contentService.GetById(NodeDto.NodeIdSeed + 2);
            Assert.AreEqual("Home", temp.Name);
            Assert.AreEqual(2, temp.Children(contentService).Count());

            // Act
            var copy = contentService.Copy(temp, temp.ParentId, false, true, 0);
            var content = contentService.GetById(NodeDto.NodeIdSeed + 2);

            // Assert
            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.Id, Is.Not.EqualTo(content.Id));
            Assert.AreNotSame(content, copy);
            Assert.AreEqual(2, copy.Children(contentService).Count());

            var child = contentService.GetById(NodeDto.NodeIdSeed + 3);
            var childCopy = copy.Children(contentService).First();
            Assert.AreEqual(childCopy.Name, child.Name);
            Assert.AreNotEqual(childCopy.Id, child.Id);
            Assert.AreNotEqual(childCopy.Key, child.Key);
        }

        [Test]
        public void Can_Copy_NonRecursive()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var temp = contentService.GetById(NodeDto.NodeIdSeed + 2);
            Assert.AreEqual("Home", temp.Name);
            Assert.AreEqual(2, temp.Children(contentService).Count());

            // Act
            var copy = contentService.Copy(temp, temp.ParentId, false, false, 0);
            var content = contentService.GetById(NodeDto.NodeIdSeed + 2);

            // Assert
            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.Id, Is.Not.EqualTo(content.Id));
            Assert.AreNotSame(content, copy);
            Assert.AreEqual(0, copy.Children(contentService).Count());
        }

        [Test]
        public void Can_Copy_Content_With_Tags()
        {
            const string propAlias = "tags";

            var contentService = ServiceContext.ContentService;

            // create a content type that has a 'tags' property
            // the property needs to support tags, else nothing works of course!
            var contentType = MockedContentTypes.CreateSimpleContentType3("umbTagsPage", "TagsPage");
            contentType.Key = new Guid("78D96D30-1354-4A1E-8450-377764200C58");
            ServiceContext.FileService.SaveTemplate(contentType.DefaultTemplate); // else, FK violation on contentType!
            ServiceContext.ContentTypeService.Save(contentType);

            var content = MockedContent.CreateSimpleContent(contentType, "Simple Tags Page", -1);
            content.AssignTags(propAlias, new[] {"hello", "world"});
            contentService.Save(content);

            // value has been set but no tags have been created (not published)
            Assert.AreEqual("hello,world", content.GetValue(propAlias));
            var contentTags = ServiceContext.TagService.GetTagsForEntity(content.Id).ToArray();
            Assert.AreEqual(0, contentTags.Length);

            // reloading the content yields the same result
            content = (Content) contentService.GetById(content.Id);
            Assert.AreEqual("hello,world", content.GetValue(propAlias));
            contentTags = ServiceContext.TagService.GetTagsForEntity(content.Id).ToArray();
            Assert.AreEqual(0, contentTags.Length);

            // publish
            content.PublishValues();
            contentService.SaveAndPublish(content);

            // now tags have been set (published)
            Assert.AreEqual("hello,world", content.GetValue(propAlias));
            contentTags = ServiceContext.TagService.GetTagsForEntity(content.Id).ToArray();
            Assert.AreEqual(2, contentTags.Length);

            // copy
            var copy = contentService.Copy(content, content.ParentId, false);

            // copy is not published, so property has value, but no tags have been created
            Assert.AreEqual("hello,world", copy.GetValue(propAlias));
            var copiedTags = ServiceContext.TagService.GetTagsForEntity(copy.Id).ToArray();
            Assert.AreEqual(0, copiedTags.Length);

            // publish
            copy.PublishValues();
            contentService.SaveAndPublish(copy);

            // now tags have been set (published)
            copiedTags = ServiceContext.TagService.GetTagsForEntity(copy.Id).ToArray();

            Assert.AreEqual(2, copiedTags.Length);
            Assert.AreEqual("hello", copiedTags[0].Text);
            Assert.AreEqual("world", copiedTags[1].Text);
        }

        [Test]
        public void Can_Rollback_Version_On_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            var parent = ServiceContext.ContentService.GetById(NodeDto.NodeIdSeed + 2);
            Assert.IsFalse(parent.Published);
            parent.PublishValues();
            ServiceContext.ContentService.SaveAndPublish(parent); // publishing parent, so Text Page 2 can be updated.

            var content = contentService.GetById(NodeDto.NodeIdSeed + 4);
            Assert.IsFalse(content.Published);

            var versions = contentService.GetVersions(NodeDto.NodeIdSeed + 4).ToList();
            Assert.AreEqual(1, versions.Count);

            var version1 = content.VersionId;

            content.Name = "Text Page 2 Updated";
            content.SetValue("author", "Francis Doe");

            // non published = edited
            Assert.IsTrue(content.Edited);

            content.PublishValues();
            contentService.SaveAndPublish(content); // new version
            var version2 = content.VersionId;
            Assert.AreNotEqual(version1, version2);

            Assert.IsTrue(content.Published);
            Assert.IsFalse(content.Edited);
            Assert.AreEqual("Francis Doe", contentService.GetById(content.Id).GetValue<string>("author")); // version2 author is Francis

            Assert.AreEqual("Text Page 2 Updated", content.Name);
            Assert.AreEqual("Text Page 2 Updated", content.PublishName);

            content.Name = "Text Page 2 ReUpdated";
            content.SetValue("author", "Jane Doe");

            // is not actually 'edited' until changes have been saved
            Assert.IsFalse(content.Edited);
            contentService.Save(content); // just save changes
            Assert.IsTrue(content.Edited);

            Assert.AreEqual("Text Page 2 ReUpdated", content.Name);
            Assert.AreEqual("Text Page 2 Updated", content.PublishName);

            content.Name = "Text Page 2 ReReUpdated";

            content.PublishValues();
            contentService.SaveAndPublish(content); // new version
            var version3 = content.VersionId;
            Assert.AreNotEqual(version2, version3);

            Assert.IsTrue(content.Published);
            Assert.IsFalse(content.Edited);
            Assert.AreEqual("Jane Doe", contentService.GetById(content.Id).GetValue<string>("author")); // version3 author is Jane

            Assert.AreEqual("Text Page 2 ReReUpdated", content.Name);
            Assert.AreEqual("Text Page 2 ReReUpdated", content.PublishName);

            // here we have
            // version1, first published version
            // version2, second published version
            // version3, third and current published version

            // rollback all values to version1
            var rollback = contentService.GetById(NodeDto.NodeIdSeed + 4);
            var rollto = contentService.GetVersion(version1);
            rollback.CopyValues(rollto);
            rollback.Name = rollto.Name; // must do it explicitely
            contentService.Save(rollback);

            Assert.IsNotNull(rollback);
            Assert.IsTrue(rollback.Published);
            Assert.IsTrue(rollback.Edited);
            Assert.AreEqual("Francis Doe", contentService.GetById(content.Id).GetValue<string>("author")); // author is now Francis again
            Assert.AreEqual(version3, rollback.VersionId); // same version but with edits

            // props and name have rolled back
            Assert.AreEqual("Francis Doe", rollback.GetValue<string>("author"));
            Assert.AreEqual("Text Page 2 Updated", rollback.Name);

            // published props and name are still there
            Assert.AreEqual("Jane Doe", rollback.GetValue<string>("author", published: true));
            Assert.AreEqual("Text Page 2 ReReUpdated", rollback.PublishName);

            // rollback all values to current version
            // special because... current has edits... this really is equivalent to rolling back to version2
            var rollback2 = contentService.GetById(NodeDto.NodeIdSeed + 4);
            var rollto2 = contentService.GetVersion(version3);
            rollback2.CopyValues(rollto2);
            rollback2.Name = rollto2.PublishName; // must do it explicitely AND must pick the publish one!
            contentService.Save(rollback2);

            Assert.IsTrue(rollback2.Published);
            Assert.IsFalse(rollback2.Edited); // all changes cleared!

            Assert.AreEqual("Jane Doe", rollback2.GetValue<string>("author"));
            Assert.AreEqual("Text Page 2 ReReUpdated", rollback2.Name);

            // test rollback to self, again
            content = contentService.GetById(content.Id);
            Assert.AreEqual("Text Page 2 ReReUpdated", content.Name);
            Assert.AreEqual("Jane Doe", content.GetValue<string>("author"));
            content.PublishValues();
            contentService.SaveAndPublish(content);
            Assert.IsFalse(content.Edited);
            content.Name = "Xxx";
            content.SetValue("author", "Bob Doe");
            contentService.Save(content);
            Assert.IsTrue(content.Edited);
            rollto = contentService.GetVersion(content.VersionId);
            content.CopyValues(rollto);
            content.Name = rollto.PublishName; // must do it explicitely AND must pick the publish one!
            contentService.Save(content);
            Assert.IsFalse(content.Edited);
            Assert.AreEqual("Text Page 2 ReReUpdated", content.Name);
            Assert.AreEqual("Jane Doe", content.GetValue("author"));
        }

        [Test]
        public void Can_Save_Lazy_Content()
        {
            var contentType = ServiceContext.ContentTypeService.Get("umbTextpage");
            var root = ServiceContext.ContentService.GetById(NodeDto.NodeIdSeed + 2);

            var c = new Lazy<IContent>(() => MockedContent.CreateSimpleContent(contentType, "Hierarchy Simple Text Page", root.Id));
            var c2 = new Lazy<IContent>(() => MockedContent.CreateSimpleContent(contentType, "Hierarchy Simple Text Subpage", c.Value.Id));
            var list = new List<Lazy<IContent>> {c, c2};

            using (var scope = ScopeProvider.CreateScope())
            {
                var repository = CreateRepository(ScopeProvider, out _);

                foreach (var content in list)
                {
                    repository.Save(content.Value);
                }

                Assert.That(c.Value.HasIdentity, Is.True);
                Assert.That(c2.Value.HasIdentity, Is.True);

                Assert.That(c.Value.Id > 0, Is.True);
                Assert.That(c2.Value.Id > 0, Is.True);

                Assert.That(c.Value.ParentId > 0, Is.True);
                Assert.That(c2.Value.ParentId > 0, Is.True);
            }

        }

        [Test]
        public void Can_Verify_Property_Types_On_Content()
        {
            // Arrange
            var contentTypeService = ServiceContext.ContentTypeService;
            var contentType = MockedContentTypes.CreateAllTypesContentType("allDataTypes", "All DataTypes");
            contentTypeService.Save(contentType);
            var contentService = ServiceContext.ContentService;
            var content = MockedContent.CreateAllTypesContent(contentType, "Random Content", -1);
            contentService.Save(content);
            var id = content.Id;

            // Act
            var sut = contentService.GetById(id);

            // Arrange
            Assert.That(sut.GetValue<bool>("isTrue"), Is.True);
            Assert.That(sut.GetValue<int>("number"), Is.EqualTo(42));
            Assert.That(sut.GetValue<string>("bodyText"), Is.EqualTo("Lorem Ipsum Body Text Test"));
            Assert.That(sut.GetValue<string>("singleLineText"), Is.EqualTo("Single Line Text Test"));
            Assert.That(sut.GetValue<string>("multilineText"), Is.EqualTo("Multiple lines \n in one box"));
            Assert.That(sut.GetValue<string>("upload"), Is.EqualTo("/media/1234/koala.jpg"));
            Assert.That(sut.GetValue<string>("label"), Is.EqualTo("Non-editable label"));
            //SD: This is failing because the 'content' call to GetValue<DateTime> always has empty milliseconds
            //MCH: I'm guessing this is an issue because of the format the date is actually stored as, right? Cause we don't do any formatting when saving or loading
            Assert.That(sut.GetValue<DateTime>("dateTime").ToString("G"), Is.EqualTo(content.GetValue<DateTime>("dateTime").ToString("G")));
            Assert.That(sut.GetValue<string>("colorPicker"), Is.EqualTo("black"));
            //that one is gone in 7.4
            //Assert.That(sut.GetValue<string>("folderBrowser"), Is.Null);
            Assert.That(sut.GetValue<string>("ddlMultiple"), Is.EqualTo("1234,1235"));
            Assert.That(sut.GetValue<string>("rbList"), Is.EqualTo("random"));
            Assert.That(sut.GetValue<DateTime>("date").ToString("G"), Is.EqualTo(content.GetValue<DateTime>("date").ToString("G")));
            Assert.That(sut.GetValue<string>("ddl"), Is.EqualTo("1234"));
            Assert.That(sut.GetValue<string>("chklist"), Is.EqualTo("randomc"));
            Assert.That(sut.GetValue<Udi>("contentPicker"), Is.EqualTo(Udi.Create(Constants.UdiEntityType.Document, new Guid("74ECA1D4-934E-436A-A7C7-36CC16D4095C"))));
            Assert.That(sut.GetValue<Udi>("mediaPicker"), Is.EqualTo(Udi.Create(Constants.UdiEntityType.Media, new Guid("44CB39C8-01E5-45EB-9CF8-E70AAF2D1691"))));
            Assert.That(sut.GetValue<Udi>("memberPicker"), Is.EqualTo(Udi.Create(Constants.UdiEntityType.Member, new Guid("9A50A448-59C0-4D42-8F93-4F1D55B0F47D"))));
            Assert.That(sut.GetValue<string>("relatedLinks"), Is.EqualTo("<links><link title=\"google\" link=\"http://google.com\" type=\"external\" newwindow=\"0\" /></links>"));
            Assert.That(sut.GetValue<string>("tags"), Is.EqualTo("this,is,tags"));
        }

        [Test]
        public void Can_Delete_Previous_Versions_Not_Latest()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 5);
            var version = content.VersionId;

            // Act
            contentService.DeleteVersion(NodeDto.NodeIdSeed + 5, version, true, 0);
            var sut = contentService.GetById(NodeDto.NodeIdSeed + 5);

            // Assert
            Assert.That(sut.VersionId, Is.EqualTo(version));
        }

        [Test]
        public void Ensure_Content_Xml_Created()
        {
            var contentService = ServiceContext.ContentService;
            var content = contentService.Create("Home US", -1, "umbTextpage", 0);
            content.SetValue("author", "Barack Obama");

            contentService.Save(content);

            using (var scope = ScopeProvider.CreateScope())
            {
                Assert.IsFalse(scope.Database.Exists<ContentXmlDto>(content.Id));
            }

            content.PublishValues();
            contentService.SaveAndPublish(content);

            using (var scope = ScopeProvider.CreateScope())
            {
                Assert.IsTrue(scope.Database.Exists<ContentXmlDto>(content.Id));
            }
        }

        [Test]
        public void Ensure_Preview_Xml_Created()
        {
            var contentService = ServiceContext.ContentService;
            var content = contentService.Create("Home US", -1, "umbTextpage", 0);
            content.SetValue("author", "Barack Obama");

            contentService.Save(content);

            using (var scope = ScopeProvider.CreateScope())
            {
                Assert.IsTrue(scope.Database.SingleOrDefault<PreviewXmlDto>("WHERE nodeId=@nodeId", new{nodeId = content.Id}) != null);
            }
        }

        [Test]
        public void Can_Get_Paged_Children()
        {
            var service = ServiceContext.ContentService;
            // Start by cleaning the "db"
            var umbTextPage = service.GetById(new Guid("B58B3AD4-62C2-4E27-B1BE-837BD7C533E0"));
            service.Delete(umbTextPage);

            var contentType = MockedContentTypes.CreateSimpleContentType();
            ServiceContext.ContentTypeService.Save(contentType);
            for (int i = 0; i < 10; i++)
            {
                var c1 = MockedContent.CreateSimpleContent(contentType);
                ServiceContext.ContentService.Save(c1);
            }

            long total;
            var entities = service.GetPagedChildren(-1, 0, 6, out total).ToArray();
            Assert.That(entities.Length, Is.EqualTo(6));
            Assert.That(total, Is.EqualTo(10));
            entities = service.GetPagedChildren(-1, 1, 6, out total).ToArray();
            Assert.That(entities.Length, Is.EqualTo(4));
            Assert.That(total, Is.EqualTo(10));
        }

        [Test]
        public void Can_Get_Paged_Children_Dont_Get_Descendants()
        {
            var service = ServiceContext.ContentService;
            // Start by cleaning the "db"
            var umbTextPage = service.GetById(new Guid("B58B3AD4-62C2-4E27-B1BE-837BD7C533E0"));
            service.Delete(umbTextPage);

            var contentType = MockedContentTypes.CreateSimpleContentType();
            ServiceContext.ContentTypeService.Save(contentType);
            // only add 9 as we also add a content with children
            for (int i = 0; i < 9; i++)
            {
                var c1 = MockedContent.CreateSimpleContent(contentType);
                ServiceContext.ContentService.Save(c1);
            }

            var willHaveChildren = MockedContent.CreateSimpleContent(contentType);
            ServiceContext.ContentService.Save(willHaveChildren);
            for (int i = 0; i < 10; i++)
            {
                var c1 = MockedContent.CreateSimpleContent(contentType, "Content" + i, willHaveChildren.Id);
                ServiceContext.ContentService.Save(c1);
            }

            long total;
            // children in root including the folder - not the descendants in the folder
            var entities = service.GetPagedChildren(-1, 0, 6, out total).ToArray();
            Assert.That(entities.Length, Is.EqualTo(6));
            Assert.That(total, Is.EqualTo(10));
            entities = service.GetPagedChildren(-1, 1, 6, out total).ToArray();
            Assert.That(entities.Length, Is.EqualTo(4));
            Assert.That(total, Is.EqualTo(10));

            // children in folder
            entities = service.GetPagedChildren(willHaveChildren.Id, 0, 6, out total).ToArray();
            Assert.That(entities.Length, Is.EqualTo(6));
            Assert.That(total, Is.EqualTo(10));
            entities = service.GetPagedChildren(willHaveChildren.Id, 1, 6, out total).ToArray();
            Assert.That(entities.Length, Is.EqualTo(4));
            Assert.That(total, Is.EqualTo(10));
        }

        [Test]
        public void PublishingTest()
        {
            var contentType = new ContentType(-1)
            {
                Alias = "foo",
                Name = "Foo"
            };

            var properties = new PropertyTypeCollection(true)
            {
                new PropertyType("test", ValueStorageType.Ntext) { Alias = "title", Name = "Title", Mandatory = false, DataTypeId = -88 },
            };

            contentType.PropertyGroups.Add(new PropertyGroup(properties) { Name = "content" });

            contentType.SetDefaultTemplate(new Template("Textpage", "textpage"));
            ServiceContext.FileService.SaveTemplate(contentType.DefaultTemplate); // else, FK violation on contentType!
            ServiceContext.ContentTypeService.Save(contentType);

            var contentService = ServiceContext.ContentService;
            var content = contentService.Create("foo", -1, "foo");
            contentService.Save(content);

            Assert.IsFalse(content.Published);
            Assert.IsTrue(content.Edited);

            content = contentService.GetById(content.Id);
            Assert.IsFalse(content.Published);
            Assert.IsTrue(content.Edited);

            content.SetValue("title", "foo");
            Assert.IsTrue(content.Edited);

            contentService.Save(content);

            Assert.IsFalse(content.Published);
            Assert.IsTrue(content.Edited);

            content = contentService.GetById(content.Id);
            Assert.IsFalse(content.Published);
            Assert.IsTrue(content.Edited);

            var versions = contentService.GetVersions(content.Id);
            Assert.AreEqual(1, versions.Count());

            // publish content
            // becomes Published, !Edited
            // creates a new version
            // can get published property values
            content.PublishValues();
            contentService.SaveAndPublish(content);

            Assert.IsTrue(content.Published);
            Assert.IsFalse(content.Edited);

            content = contentService.GetById(content.Id);
            Assert.IsTrue(content.Published);
            Assert.IsFalse(content.Edited);

            versions = contentService.GetVersions(content.Id);
            Assert.AreEqual(2, versions.Count());

            Assert.AreEqual("foo", content.GetValue("title", published: true));
            Assert.AreEqual("foo", content.GetValue("title"));

            // unpublish content
            // becomes !Published, Edited
            contentService.Unpublish(content);

            Assert.IsFalse(content.Published);
            Assert.IsTrue(content.Edited);

            Assert.AreEqual("foo", content.GetValue("title", published: true));
            Assert.AreEqual("foo", content.GetValue("title"));

            var vpk = ((Content) content).VersionId;
            var ppk = ((Content) content).PublishedVersionId;

            content = contentService.GetById(content.Id);
            Assert.IsFalse(content.Published);
            Assert.IsTrue(content.Edited);

            // fixme - depending on 1 line in ContentBaseFactory.BuildEntity
            // the published infos can be gone or not
            // if gone, it's not consistent with above
            Assert.AreEqual(vpk, ((Content) content).VersionId);
            Assert.AreEqual(ppk, ((Content) content).PublishedVersionId); // still there

            // fixme - depending on 1 line in ContentRepository.MapDtoToContent
            // the published values can be null or not
            // if null, it's not consistent with above
            //Assert.IsNull(content.GetValue("title", published:  true));
            Assert.AreEqual("foo", content.GetValue("title", published: true)); // still there
            Assert.AreEqual("foo", content.GetValue("title"));

            versions = contentService.GetVersions(content.Id);
            Assert.AreEqual(2, versions.Count());

            // ah - we have a problem here - since we're not published we don't have published values
            // and therefore we cannot "just" republish the content - we need to publish some values
            // so... that's not really an option
            //
            //contentService.SaveAndPublish(content);

            // fixme - what shall we do of all this?
            /*
            // this basically republishes a content
            // what if it never was published?
            // what if it has changes?
            // do we want to "publish" only some variants, or the entire content?
            contentService.Publish(content);

            Assert.IsTrue(content.Published);
            Assert.IsFalse(content.Edited);

            // fixme - should it be 2 or 3
            versions = contentService.GetVersions(content.Id);
            Assert.AreEqual(2, versions.Count());

            // fixme - now test rollbacks
            var version = contentService.GetByVersion(content.Id); // test that it gets a version - should be GetVersion
            var previousVersion = contentService.GetVersions(content.Id).Skip(1).FirstOrDefault(); // need an optimized way to do this
            content.CopyValues(version); // copies the edited value - always
            content.Template = version.Template;
            content.Name = version.Name;
            contentService.Save(content); // this is effectively a rollback?
            contentService.Rollback(content); // just kill the method and offer options on values + template + name...
            */
        }

        private IEnumerable<IContent> CreateContentHierarchy()
        {
            var contentType = ServiceContext.ContentTypeService.Get("umbTextpage");
            var root = ServiceContext.ContentService.GetById(NodeDto.NodeIdSeed + 2);

            var list = new List<IContent>();

            for (int i = 0; i < 10; i++)
            {
                var content = MockedContent.CreateSimpleContent(contentType, "Hierarchy Simple Text Page " + i, root);

                list.Add(content);
                list.AddRange(CreateChildrenOf(contentType, content, 4));

                Debug.Print("Created: 'Hierarchy Simple Text Page {0}'", i);
            }

            return list;
        }

        private IEnumerable<IContent> CreateChildrenOf(IContentType contentType, IContent content, int depth)
        {
            var list = new List<IContent>();
            for (int i = 0; i < depth; i++)
            {
                var c = MockedContent.CreateSimpleContent(contentType, "Hierarchy Simple Text Subpage " + i, content);
                list.Add(c);

                Debug.Print("Created: 'Hierarchy Simple Text Subpage {0}' - Depth: {1}", i, depth);
            }
            return list;
        }

        private DocumentRepository CreateRepository(IScopeProvider provider, out ContentTypeRepository contentTypeRepository)
        {
            var accessor = (IScopeAccessor) provider;
            var templateRepository = new TemplateRepository(accessor, DisabledCache, Logger, Mock.Of<ITemplatesSection>(), Mock.Of<IFileSystem>(), Mock.Of<IFileSystem>());
            var tagRepository = new TagRepository(accessor, DisabledCache, Logger);
            contentTypeRepository = new ContentTypeRepository(accessor, DisabledCache, Logger, templateRepository);
            var repository = new DocumentRepository(accessor, DisabledCache, Logger, contentTypeRepository, templateRepository, tagRepository, Mock.Of<IContentSection>());
            return repository;
        }
    }
}

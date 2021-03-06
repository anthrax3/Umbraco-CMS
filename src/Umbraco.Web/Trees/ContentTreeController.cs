﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Formatting;
using System.Web.Http;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Entities;
using Umbraco.Core.Services;
using Umbraco.Web.Composing;
using Umbraco.Web.Models.Trees;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi.Filters;
using Umbraco.Web._Legacy.Actions;
using Umbraco.Web.Models.ContentEditing;
using Umbraco.Web.Search;
using Constants = Umbraco.Core.Constants;

namespace Umbraco.Web.Trees
{
    //We will not allow the tree to render unless the user has access to any of the sections that the tree gets rendered
    // this is not ideal but until we change permissions to be tree based (not section) there's not much else we can do here.
    [UmbracoApplicationAuthorize(
        Constants.Applications.Content,
        Constants.Applications.Media,
        Constants.Applications.Users,
        Constants.Applications.Settings,
        Constants.Applications.Developer,
        Constants.Applications.Members)]
    [Tree(Constants.Applications.Content, Constants.Trees.Content)]
    [PluginController("UmbracoTrees")]
    [CoreTree]
    [SearchableTree("searchResultFormatter", "configureContentResult")]
    public class ContentTreeController : ContentTreeControllerBase, ISearchableTree
    {
        private readonly UmbracoTreeSearcher _treeSearcher = new UmbracoTreeSearcher();

        protected override int RecycleBinId => Constants.System.RecycleBinContent;

        protected override bool RecycleBinSmells => Services.ContentService.RecycleBinSmells();

        private int[] _userStartNodes;
        protected override int[] UserStartNodes
            => _userStartNodes ?? (_userStartNodes = Security.CurrentUser.CalculateContentStartNodeIds(Services.EntityService));

        /// <inheritdoc />
        protected override TreeNode GetSingleTreeNode(IEntitySlim entity, string parentId, FormDataCollection queryStrings)
        {
            var allowedUserOptions = GetAllowedUserMenuItemsForNode(entity);
            if (CanUserAccessNode(entity, allowedUserOptions))
            {

                //Special check to see if it ia a container, if so then we'll hide children.
                var isContainer = entity.IsContainer;   // && (queryStrings.Get("isDialog") != "true");

                var node = CreateTreeNode(
                    entity,
                    Constants.ObjectTypes.Document,
                    parentId,
                    queryStrings,
                    entity.HasChildren && !isContainer);

                // entity is either a container, or a document
                if (isContainer)
                {
                    node.AdditionalData.Add("isContainer", true);
                    node.SetContainerStyle();
                }
                else
                {
                    var documentEntity = (IDocumentEntitySlim) entity;
                    if (!documentEntity.Published)
                        node.SetNotPublishedStyle();
                    if (documentEntity.Edited)
                        node.SetHasUnpublishedVersionStyle();
                    node.AdditionalData.Add("contentType", documentEntity.ContentTypeAlias);
                }

                if (Services.PublicAccessService.IsProtected(entity.Path))
                    node.SetProtectedStyle();

                return node;
            }

            return null;
        }

        protected override MenuItemCollection PerformGetMenuForNode(string id, FormDataCollection queryStrings)
        {
            if (id == Constants.System.Root.ToInvariantString())
            {
                var menu = new MenuItemCollection();

                // if the user's start node is not the root then the only menu item to display is refresh
                if (UserStartNodes.Contains(Constants.System.Root) == false)
                {
                    menu.Items.Add<RefreshNode, ActionRefresh>(
                        Services.TextService.Localize(string.Concat("actions/", ActionRefresh.Instance.Alias)),
                        true);
                    return menu;
                }

                //set the default to create
                menu.DefaultMenuAlias = ActionNew.Instance.Alias;

                // we need to get the default permissions as you can't set permissions on the very root node
                var permission = Services.UserService.GetPermissions(Security.CurrentUser, Constants.System.Root).First();
                var nodeActions = global::Umbraco.Web._Legacy.Actions.Action.FromEntityPermission(permission)
                    .Select(x => new MenuItem(x));

                //these two are the standard items
                menu.Items.Add<ActionNew>(Services.TextService.Localize("actions", ActionNew.Instance.Alias));
                menu.Items.Add<ActionSort>(Services.TextService.Localize("actions", ActionSort.Instance.Alias), true).ConvertLegacyMenuItem(null, "content", "content");

                //filter the standard items
                FilterUserAllowedMenuItems(menu, nodeActions);

                if (menu.Items.Any())
                {
                    menu.Items.Last().SeperatorBefore = true;
                }

                // add default actions for *all* users
                menu.Items.Add<ActionRePublish>(Services.TextService.Localize("actions", ActionRePublish.Instance.Alias)).ConvertLegacyMenuItem(null, "content", "content");
                menu.Items.Add<RefreshNode, ActionRefresh>(Services.TextService.Localize("actions", ActionRefresh.Instance.Alias), true);

                return menu;
            }


            //return a normal node menu:
            int iid;
            if (int.TryParse(id, out iid) == false)
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }
            var item = Services.EntityService.Get(iid, UmbracoObjectTypes.Document);
            if (item == null)
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }

            //if the user has no path access for this node, all they can do is refresh
            if (Security.CurrentUser.HasPathAccess(item, Services.EntityService, RecycleBinId) == false)
            {
                var menu = new MenuItemCollection();
                menu.Items.Add<RefreshNode, ActionRefresh>(
                    Services.TextService.Localize(string.Concat("actions/", ActionRefresh.Instance.Alias)),
                    true);
                return menu;
            }

            var nodeMenu = GetAllNodeMenuItems(item);
            var allowedMenuItems = GetAllowedUserMenuItemsForNode(item);

            FilterUserAllowedMenuItems(nodeMenu, allowedMenuItems);

            //if the media item is in the recycle bin, don't have a default menu, just show the regular menu
            if (item.Path.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Contains(RecycleBinId.ToInvariantString()))
            {
                nodeMenu.DefaultMenuAlias = null;
                nodeMenu.Items.Insert(2, new MenuItem(ActionRestore.Instance, Services.TextService.Localize("actions", ActionRestore.Instance.Alias)));
            }
            else
            {
                //set the default to create
                nodeMenu.DefaultMenuAlias = ActionNew.Instance.Alias;
            }


            return nodeMenu;
        }

        protected override UmbracoObjectTypes UmbracoObjectType
        {
            get { return UmbracoObjectTypes.Document; }
        }

        /// <summary>
        /// Returns true or false if the current user has access to the node based on the user's allowed start node (path) access
        /// </summary>
        /// <param name="id"></param>
        /// <param name="queryStrings"></param>
        /// <returns></returns>
        protected override bool HasPathAccess(string id, FormDataCollection queryStrings)
        {
            var entity = GetEntityFromId(id);
            return HasPathAccess(entity, queryStrings);
        }

        /// <summary>
        /// Returns a collection of all menu items that can be on a content node
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        protected MenuItemCollection GetAllNodeMenuItems(IUmbracoEntity item)
        {
            var menu = new MenuItemCollection();
            AddActionNode<ActionNew>(item, menu);
            AddActionNode<ActionDelete>(item, menu);

            AddActionNode<ActionCreateBlueprintFromContent>(item, menu);

            //need to ensure some of these are converted to the legacy system - until we upgrade them all to be angularized.
            AddActionNode<ActionMove>(item, menu, true);
            AddActionNode<ActionCopy>(item, menu);
            AddActionNode<ActionChangeDocType>(item, menu, convert: true);

            AddActionNode<ActionSort>(item, menu, true, true);

            AddActionNode<ActionRollback>(item, menu, convert: true);
            AddActionNode<ActionAudit>(item, menu, convert: true);
            AddActionNode<ActionPublish>(item, menu, true, true);
            AddActionNode<ActionToPublish>(item, menu, convert: true);
            AddActionNode<ActionAssignDomain>(item, menu, convert: true);
            AddActionNode<ActionRights>(item, menu, convert: true);
            AddActionNode<ActionProtect>(item, menu, true, true);

            AddActionNode<ActionNotify>(item, menu, true, true);
            AddActionNode<ActionSendToTranslate>(item, menu, convert: true);

            AddActionNode<RefreshNode, ActionRefresh>(item, menu, true);

            return menu;
        }

        private void AddActionNode<TAction>(IUmbracoEntity item, MenuItemCollection menu, bool hasSeparator = false, bool convert = false)
            where TAction : IAction
        {
            var menuItem = menu.Items.Add<TAction>(Services.TextService.Localize("actions", Current.Actions.GetAction<TAction>().Alias), hasSeparator);
            if (convert) menuItem.ConvertLegacyMenuItem(item, "content", "content");
        }

        private void AddActionNode<TItem, TAction>(IUmbracoEntity item, MenuItemCollection menu, bool hasSeparator = false, bool convert = false)
            where TItem : MenuItem, new()
            where TAction : IAction
        {
            var menuItem = menu.Items.Add<TItem, TAction>(Services.TextService.Localize("actions", Current.Actions.GetAction<TAction>().Alias), hasSeparator);
            if (convert) menuItem.ConvertLegacyMenuItem(item, "content", "content");
        }

        public IEnumerable<SearchResultItem> Search(string query, int pageSize, long pageIndex, out long totalFound, string searchFrom = null)
        {
            return _treeSearcher.ExamineSearch(Umbraco, query, UmbracoEntityTypes.Document, pageSize, pageIndex, out totalFound, searchFrom);
        }
    }
}

﻿using FluentCMS.Web.Api.Filters;
using System.IO;

namespace FluentCMS.Web.Api.Controllers;

public class PageController(
    ISiteService siteService,
    IPageService pageService,
    IPluginDefinitionService pluginDefinitionService,
    IPluginService pluginService,
    ILayoutService layoutService,
    IRoleService roleService,
    IUserRoleService userRoleService,
    ISetupService setupService,
    ISettingsService settingsService,
    IApiExecutionContext apiExecutionContext,
    IMapper mapper) : BaseGlobalController
{

    public const string AREA = "Page Management";
    public const string READ = $"Read";
    public const string UPDATE = $"Update";
    public const string CREATE = "Create";
    public const string DELETE = $"Delete";

    [HttpGet("{siteUrl}")]
    [DecodeQueryParam]
    [Policy(AREA, READ)]
    public async Task<IApiPagingResult<PageDetailResponse>> GetAll([FromRoute] string siteUrl, CancellationToken cancellationToken = default)
    {
        var site = await siteService.GetByUrl(siteUrl, cancellationToken);
        var entities = await pageService.GetBySiteId(site.Id, cancellationToken);
        var layouts = await layoutService.GetAll(cancellationToken);

        List<PageDetailResponse> entitiesResponse = [];

        foreach (var entity in entities)
        {
            var layout = layouts.Where(x => x.Id == entity.LayoutId).FirstOrDefault();

            var mappedEntity = mapper.Map<PageDetailResponse>(entity);
            mappedEntity.Layout = mapper.Map<LayoutDetailResponse>(layout);
            entitiesResponse.Add(mappedEntity);
        }

        return OkPaged(entitiesResponse);
    }

    [HttpGet("{id}")]
    [Policy(AREA, READ)]
    public async Task<IApiResult<PageDetailResponse>> GetById([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await pageService.GetById(id, cancellationToken);
        var entityResponse = mapper.Map<PageDetailResponse>(entity);

        return Ok(entityResponse);
    }

    [HttpGet]
    [DecodeQueryParam]
    [Policy(AREA, READ)]
    public async Task<IApiResult<PageFullDetailResponse>> GetByUrl([FromQuery] string url, CancellationToken cancellationToken = default)
    {
        var uri = new Uri(url);
        var domain = uri.Authority;
        var path = uri.AbsolutePath;

        if (!await setupService.IsInitialized(cancellationToken))
            return Ok(GetSetupPage());

        return await GetPageResponse(domain, path, cancellationToken);
    }

    [HttpPost]
    [Policy(AREA, CREATE)]
    public async Task<IApiResult<PageDetailResponse>> Create(PageCreateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = mapper.Map<Page>(request);
        var newEntity = await pageService.Create(entity, cancellationToken);

        var pageResponse = mapper.Map<PageDetailResponse>(newEntity);
        return Ok(pageResponse);
    }

    [HttpPut]
    [Policy(AREA, UPDATE)]
    public async Task<IApiResult<PageDetailResponse>> Update(PageUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = mapper.Map<Page>(request);
        var updatedEntity = await pageService.Update(entity, cancellationToken);

        var entityResponse = mapper.Map<PageDetailResponse>(updatedEntity);
        return Ok(entityResponse);
    }

    [HttpDelete("{id}")]
    [Policy(AREA, DELETE)]
    public async Task<IApiResult<bool>> Delete([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        await pageService.Delete(id, cancellationToken);

        return Ok(true);
    }

    #region Private Methods

    private static string GetFullPath(Dictionary<Guid, Page> allPages, Page page)
    {
        var result = new List<string>();
        var currentPage = page;
        while (currentPage != null)
        {
            result.Add(currentPage.Path);
            currentPage = currentPage.ParentId.HasValue ? allPages[currentPage.ParentId.Value] : default!;
        }
        result.Reverse();

        return string.Join("", result);
    }

    private async Task<IApiResult<PageFullDetailResponse>> GetPageResponse(string domain, string path, CancellationToken cancellationToken = default)
    {
        var site = await siteService.GetByUrl(domain, cancellationToken);
        var pages = (await pageService.GetBySiteId(site.Id, cancellationToken)).ToDictionary(x => x.Id, x => x);
        var pluginDefinitions = (await pluginDefinitionService.GetAll(cancellationToken)).ToDictionary(x => x.Id);

        var pagesByPath = new Dictionary<string, Page>();

        foreach (var _page in pages.Values)
        {
            pagesByPath.Add(GetFullPath(pages, _page), _page);
        }

        if (!pagesByPath.TryGetValue(path, out Page? page))
            throw new AppException(ExceptionCodes.PageNotFound);

        var layoutId = page.LayoutId ?? site.LayoutId;
        var editLayoutId = page.EditLayoutId ?? site.EditLayoutId;
        var detailLayoutId = page.DetailLayoutId ?? site.DetailLayoutId;

        var layout = await layoutService.GetById(layoutId, cancellationToken);
        var editLayout = await layoutService.GetById(editLayoutId, cancellationToken);
        var detailLayout = await layoutService.GetById(detailLayoutId, cancellationToken);

        var plugins = await pluginService.GetByPageId(page.Id, cancellationToken);

        var pageResponse = mapper.Map<PageFullDetailResponse>(page);
        pageResponse.Site = mapper.Map<SiteDetailResponse>(site);
        // set the site settings
        pageResponse.Site.Settings = (await settingsService.GetById(site.Id, cancellationToken)).Values;
        pageResponse.Layout = mapper.Map<LayoutDetailResponse>(layout);
        pageResponse.EditLayout = mapper.Map<LayoutDetailResponse>(editLayout);
        pageResponse.DetailLayout = mapper.Map<LayoutDetailResponse>(detailLayout);
        pageResponse.FullPath = path;
        pageResponse.Sections = [];

        // set all available roles in the site
        var allRoles = await roleService.GetAllForSite(site.Id, cancellationToken) ?? [];
        pageResponse.Site.AllRoles = mapper.Map<List<RoleDetailResponse>>(allRoles);

        // set admin roles property for the site
        // TODO: read from IPermissionService
        pageResponse.Site.AdminRoles = pageResponse.Site.AllRoles.Where(x => x.Type == RoleTypes.Administrators).ToList();

        // set contributor roles property for the site
        // TODO: read from IPermissionService
        pageResponse.Site.ContributorRoles = pageResponse.Site.AllRoles.Where(x => x.Type == RoleTypes.Administrators).ToList();

        // setting current user details and permissions
        pageResponse.User = mapper.Map<UserRoleDetailResponse>(apiExecutionContext);
        var userRoleIds = await userRoleService.GetUserRoleIds(apiExecutionContext.UserId, site.Id, cancellationToken);
        pageResponse.User.Roles = pageResponse.Site.AllRoles.Where(x => userRoleIds.Contains(x.Id)).ToList();

        // setting the plugin details
        var pluginSettingsDict = (await settingsService.GetByIds(plugins.Select(x => x.Id), cancellationToken)).ToDictionary(x => x.Id, x => x.Values);
        foreach (var plugin in plugins)
        {
            if (!pageResponse.Sections.ContainsKey(plugin.Section))
                pageResponse.Sections.Add(plugin.Section, []);

            var pluginResponse = mapper.Map<PluginDetailResponse>(plugin);

            // set settings for the plugin if exists in the settings dictionary
            if (pluginSettingsDict.TryGetValue(plugin.Id, out Dictionary<string, string>? value))
                pluginResponse.Settings = value;

            pluginResponse.Definition = mapper.Map<PluginDefinitionDetailResponse>(pluginDefinitions[plugin.DefinitionId]);
            pageResponse.Sections[plugin.Section].Add(pluginResponse);
        }

        return Ok(pageResponse);
    }

    private static PageFullDetailResponse GetSetupPage()
    {
        var page = new PageFullDetailResponse
        {
            Title = "Setup",
            Locked = true,
            Layout = new LayoutDetailResponse
            {
                Body = System.IO.File.ReadAllText(Path.Combine(ServiceConstants.DefaultTemplateFolder, "AuthLayout.body.html")),
                Head = System.IO.File.ReadAllText(Path.Combine(ServiceConstants.DefaultTemplateFolder, "AuthLayout.head.html"))
            },
            Site = new(),
            Sections = new Dictionary<string, List<PluginDetailResponse>>
            {
                ["Main"] =
                  [
                      new()
                      {
                          Locked = true,
                          Section = "Main",
                          Definition = new PluginDefinitionDetailResponse
                          {
                              Name = "Setup",
                              Description = "Setup View Plugin",
                              Assembly = "FluentCMS.Web.Plugins.Admin.dll",
                              Locked = true,
                              Types =
                              [
                                  new PluginDefinitionType
                                  {
                                      IsDefault = true,
                                      Name = "Setup",
                                      Type = "SetupViewPlugin"
                                  }
                              ]
                          }
                      }
                  ]
            }
        };
        return page;
    }

    #endregion
}

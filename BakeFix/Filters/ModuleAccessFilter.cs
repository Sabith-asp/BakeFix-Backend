using BakeFix.Repositories;
using BakeFix.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BakeFix.Filters
{
    /// <summary>
    /// Decorate a controller class or action with this attribute to require
    /// that the caller's organization has the named module enabled.
    /// SuperAdmins bypass the check entirely.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireModuleAttribute : Attribute
    {
        public string ModuleName { get; }
        public RequireModuleAttribute(string moduleName) => ModuleName = moduleName;
    }

    public class ModuleAccessFilter : IAsyncActionFilter
    {
        private readonly ITenantContext _tenant;
        private readonly IOrganizationRepository _orgRepo;

        public ModuleAccessFilter(ITenantContext tenant, IOrganizationRepository orgRepo)
        {
            _tenant = tenant;
            _orgRepo = orgRepo;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var moduleAttr = context.ActionDescriptor.EndpointMetadata
                .OfType<RequireModuleAttribute>()
                .FirstOrDefault();

            if (moduleAttr is not null && !_tenant.IsSuperAdmin)
            {
                var enabledModules = await _orgRepo.GetEnabledModulesAsync(_tenant.RequiredOrgId);

                if (!enabledModules.Contains(moduleAttr.ModuleName, StringComparer.OrdinalIgnoreCase))
                {
                    context.Result = new ObjectResult(new
                    {
                        message = $"Module '{moduleAttr.ModuleName}' is not enabled for your organization."
                    })
                    { StatusCode = 403 };
                    return;
                }
            }

            await next();
        }
    }
}

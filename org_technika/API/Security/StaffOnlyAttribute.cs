using System.Net;
using System.Security.Claims;
using System.Web.Http;
using System.Web.Http.Controllers;

namespace API.Security
{
    public sealed class StaffOnlyAttribute : AuthorizeAttribute
    {
        protected override bool IsAuthorized(HttpActionContext actionContext)
        {
            ClaimsPrincipal principal = actionContext.RequestContext.Principal as ClaimsPrincipal;
            if (principal == null || principal.Identity == null || !principal.Identity.IsAuthenticated) return false;
            string role = (principal.FindFirst("role") ?? principal.FindFirst(ClaimTypes.Role))?.Value;
            return role == "Администратор" || role == "Приемщик" || role == "Мастер";
        }

        protected override void HandleUnauthorizedRequest(HttpActionContext actionContext)
        {
            ClaimsPrincipal principal = actionContext.RequestContext.Principal as ClaimsPrincipal;
            actionContext.Response = new System.Net.Http.HttpResponseMessage(principal != null && principal.Identity.IsAuthenticated ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized);
        }
    }
}

using System.Net;
using System.Security.Claims;
using System.Security.Principal;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Net.Http;

namespace API.Security
{
    public sealed class AdminOnlyAttribute : AuthorizeAttribute
    {
        protected override bool IsAuthorized(HttpActionContext actionContext)
        {
            IPrincipal principal = actionContext.RequestContext.Principal;
            if (principal == null || principal.Identity == null || !principal.Identity.IsAuthenticated)
                return false;

            ClaimsIdentity identity = principal.Identity as ClaimsIdentity;
            if (identity == null)
                return principal.IsInRole("Администратор");

            return identity.HasClaim("role", "Администратор") ||
                   identity.HasClaim(ClaimTypes.Role, "Администратор") ||
                   identity.HasClaim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", "Администратор") ||
                   identity.HasClaim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role", "Администратор");
        }

        protected override void HandleUnauthorizedRequest(HttpActionContext actionContext)
        {
            IPrincipal principal = actionContext.RequestContext.Principal;
            actionContext.Response = new HttpResponseMessage(
                principal != null && principal.Identity.IsAuthenticated
                    ? HttpStatusCode.Forbidden
                    : HttpStatusCode.Unauthorized);
        }
    }
}

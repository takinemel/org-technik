using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Jwt;
using Owin;
using System;
using System.Configuration;
using System.Web.Http;

[assembly: OwinStartup(typeof(API.Startup))]

namespace API
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            string signingKeyText = ConfigurationManager.AppSettings["Jwt:SigningKey"];
            string issuer = ConfigurationManager.AppSettings["Jwt:Issuer"];
            string audience = ConfigurationManager.AppSettings["Jwt:Audience"];

            if (string.IsNullOrWhiteSpace(signingKeyText) ||
                signingKeyText.StartsWith("REPLACE_", StringComparison.Ordinal) ||
                System.Text.Encoding.UTF8.GetByteCount(signingKeyText) < 32)
            {
                throw new ConfigurationErrorsException(
                    "Jwt:SigningKey должен быть заменён случайным секретом длиной не менее 32 байт.");
            }

            if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            {
                throw new ConfigurationErrorsException(
                    "В Web.config должны быть заданы Jwt:Issuer и Jwt:Audience.");
            }

            byte[] keyBytes = System.Text.Encoding.UTF8.GetBytes(signingKeyText);

            app.UseJwtBearerAuthentication(new JwtBearerAuthenticationOptions
            {
                AuthenticationMode = AuthenticationMode.Active,
                AllowedAudiences = new[] { audience },
                IssuerSecurityKeyProviders = new[]
                {
                    new SymmetricKeyIssuerSecurityKeyProvider(issuer, keyBytes)
                }
            });

            HttpConfiguration config = GlobalConfiguration.Configuration;
        }
    }
}

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace org_technika
{
    public sealed class ApiClient
    {
        private readonly HttpClient _httpClient = new HttpClient();
        public ApiClient()
        {
            string url = ConfigurationManager.AppSettings["ApiBaseUrl"];
            if (string.IsNullOrWhiteSpace(url)) url = "http://localhost:62236/";
            _httpClient.BaseAddress = new Uri(url.EndsWith("/") ? url : url + "/");
            _httpClient.Timeout = TimeSpan.FromSeconds(20);
        }

        public async Task<JToken> GetAsync(string path, string token)
        {
            using (HttpRequestMessage request = CreateRequest(HttpMethod.Get, path, token)) return await SendAsync(request);
        }
        public async Task<JToken> SendJsonAsync(HttpMethod method, string path, object body, string token)
        {
            using (HttpRequestMessage request = CreateRequest(method, path, token))
            {
                request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
                return await SendAsync(request);
            }
        }
        private HttpRequestMessage CreateRequest(HttpMethod method, string path, string token)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, path);
            if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return request;
        }
        private async Task<JToken> SendAsync(HttpRequestMessage request)
        {
            using (HttpResponseMessage response = await _httpClient.SendAsync(request))
            {
                string text = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    string message = text;
                    try { message = JObject.Parse(text)["message"]?.ToString() ?? text; } catch { }
                    throw new InvalidOperationException(string.Format("API {0}: {1}", (int)response.StatusCode, message));
                }
                return string.IsNullOrWhiteSpace(text) ? JValue.CreateNull() : JToken.Parse(text);
            }
        }
        public Task<JToken> LoginAsync(string login, string password)
        {
            return SendJsonAsync(HttpMethod.Post, "api/auth/login", new { Login = login, Password = password }, null);
        }
    }
}

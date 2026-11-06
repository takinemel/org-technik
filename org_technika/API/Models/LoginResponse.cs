using System;

namespace API.Models
{
    public class LoginResponse
    {
        public string AccessToken { get; set; }

        public string TokenType { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public int UserId { get; set; }

        public string Login { get; set; }

        public string Role { get; set; }
    }
}

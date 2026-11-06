using System;

namespace org_technika
{
    public sealed class SessionState
    {
        public string Token { get; set; }
        public string Login { get; set; }
        public string Role { get; set; }
        public int UserId { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public bool IsAuthenticated { get { return !string.IsNullOrWhiteSpace(Token) && ExpiresAtUtc > DateTime.UtcNow; } }
    }
}


using System;

namespace NexoSupportToolKit.Services
{
    public static class AdminAuthService
    {
        private const string AdminUser = "admin";
        private const string AdminPassword = "NEXOSUPPORT-ADMIN-2026";

        public static bool IsConfigured()
        {
            return true;
        }

        public static bool ValidateLogin(string username, string password)
        {
            return string.Equals(
                       username?.Trim(),
                       AdminUser,
                       StringComparison.OrdinalIgnoreCase)
                   &&
                   string.Equals(
                       password,
                       AdminPassword,
                       StringComparison.Ordinal);
        }
    }
}


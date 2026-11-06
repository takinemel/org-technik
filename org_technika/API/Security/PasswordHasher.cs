using System;
using System.Security.Cryptography;

namespace API.Security
{
    public static class PasswordHasher
    {
        private const string Algorithm = "PBKDF2-SHA256";
        private const int Iterations = 600000;
        private const int SaltSizeBytes = 16;
        private const int HashSizeBytes = 32;

        public static string HashPassword(string password)
        {
            if (password == null)
            {
                throw new ArgumentNullException("password");
            }

            byte[] salt = new byte[SaltSizeBytes];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] hash = DeriveHash(password, salt, Iterations);

            return string.Join("$",
                Algorithm,
                Iterations.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Convert.ToBase64String(salt),
                Convert.ToBase64String(hash));
        }

        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            string[] fields = storedHash.Split('$');
            if (fields.Length != 4 || fields[0] != Algorithm)
            {
                return false;
            }

            int iterations;
            if (!int.TryParse(fields[1], out iterations) || iterations < 100000 || iterations > 2000000)
            {
                return false;
            }

            try
            {
                byte[] salt = Convert.FromBase64String(fields[2]);
                byte[] expectedHash = Convert.FromBase64String(fields[3]);

                if (salt.Length != SaltSizeBytes || expectedHash.Length != HashSizeBytes)
                {
                    return false;
                }

                byte[] actualHash = DeriveHash(password, salt, iterations);
                return FixedTimeEquals(actualHash, expectedHash);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static byte[] DeriveHash(string password, byte[] salt, int iterations)
        {
            using (Rfc2898DeriveBytes deriveBytes = new Rfc2898DeriveBytes(
                password, salt, iterations, HashAlgorithmName.SHA256))
            {
                return deriveBytes.GetBytes(HashSizeBytes);
            }
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            int difference = 0;
            for (int i = 0; i < left.Length; i++)
            {
                difference |= left[i] ^ right[i];
            }

            return difference == 0;
        }
    }
}

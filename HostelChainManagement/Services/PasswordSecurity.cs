using System;
using System.Linq;
using System.Security.Cryptography;

namespace QuanLyChuoiNhaTro.Services
{
    public static class PasswordSecurity
    {
        private const string Scheme = "PBKDF2-SHA256";
        private const int Iterations = 210000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        public static void ValidateNewPassword(string password)
        {
            if (string.IsNullOrEmpty(password) ||
                password.Length < 8 ||
                password.Length > 72)
            {
                throw new InvalidOperationException(
                    "Mật khẩu phải có từ 8 đến 72 ký tự.");
            }

            if (!password.Any(c => c >= 'A' && c <= 'Z') ||
                !password.Any(c => c >= 'a' && c <= 'z') ||
                !password.Any(c => c >= '0' && c <= '9'))
            {
                throw new InvalidOperationException(
                    "Mật khẩu phải có chữ hoa, chữ thường và số.");
            }

            if (password.Any(c => c < 32 || c > 126))
            {
                throw new InvalidOperationException(
                    "Mật khẩu chỉ được dùng ký tự ASCII không dấu.");
            }
        }

        public static string HashPassword(string password)
        {
            ValidateNewPassword(password);

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            return Scheme + "$"
                + Iterations + "$"
                + Convert.ToBase64String(salt) + "$"
                + Convert.ToBase64String(hash);
        }

        public static bool VerifyPassword(
            string password,
            string storedPassword)
        {
            if (string.IsNullOrEmpty(storedPassword))
                return false;

            if (!storedPassword.StartsWith(
                    Scheme + "$",
                    StringComparison.Ordinal))
            {
                return string.Equals(
                    password,
                    storedPassword,
                    StringComparison.Ordinal);
            }

            string[] parts = storedPassword.Split('$');

            if (parts.Length != 4 ||
                parts[0] != Scheme ||
                !int.TryParse(parts[1], out int iterations) ||
                iterations < 100000 ||
                iterations > 600000)
            {
                return false;
            }

            try
            {
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] expected = Convert.FromBase64String(parts[3]);

                if (salt.Length != SaltSize ||
                    expected.Length != HashSize)
                {
                    return false;
                }

                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    expected.Length);

                return CryptographicOperations.FixedTimeEquals(
                    actual,
                    expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
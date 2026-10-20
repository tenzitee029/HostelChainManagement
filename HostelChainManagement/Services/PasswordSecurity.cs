using System;
using System.Linq;

namespace QuanLyChuoiNhaTro.Services
{
    public static class PasswordSecurity
    {
        public static void ValidateNewPassword(string password)
        {
            if (string.IsNullOrEmpty(password)
                || password.Length < 8
                || password.Length > 72)
            {
                throw new InvalidOperationException(
                    "Mật khẩu phải có từ 8 đến 72 ký tự.");
            }

            if (!password.Any(c => c >= 'A' && c <= 'Z')
                || !password.Any(c => c >= 'a' && c <= 'z')
                || !password.Any(c => c >= '0' && c <= '9'))
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

        public static bool VerifyPassword(
            string password,
            string storedPassword)
        {
            if (string.IsNullOrEmpty(password)
                || string.IsNullOrEmpty(storedPassword))
            {
                return false;
            }

            // So sánh trực tiếp, phân biệt chữ hoa và chữ thường.
            // Không Trim mật khẩu.
            return string.Equals(
                password,
                storedPassword,
                StringComparison.Ordinal);
        }
    }
}
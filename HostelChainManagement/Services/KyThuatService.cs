using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Models;
using QuanLyChuoiNhaTro.Services.Technician;

namespace QuanLyChuoiNhaTro.Services;

public static class KyThuatService
{
    private static string UserId()
    {
        var user = UserSession.CurrentUser;

        if (user == null
            || user.VaiTro != "Nhân viên kỹ thuật")
        {
            throw new InvalidOperationException(
                "Chức năng chỉ dành cho Nhân viên kỹ thuật.");
        }

        return user.MaNguoiDung;
    }

    // Kiểm tra đúng nhân viên, chức vụ và tài khoản đang hoạt động.
    private const string Scope = @"
        nv.MaNguoiDung = @User
        AND nv.ChucVu = N'Kỹ thuật'
        AND nv.TrangThaiLamViec = N'Đang làm'
        AND EXISTS
        (
            SELECT 1
            FROM dbo.NguoiDung u
            WHERE u.MaNguoiDung = @User
              AND u.VaiTro = N'Nhân viên kỹ thuật'
              AND u.TrangThai = N'Hoạt động'
        )";

    public static Task<DataTable> GetJobsAsync()
    {
        return DanhSachCongViecService.GetJobsAsync();
    }

    public static Task SaveAsync(
        string id,
        string expectedStatus,
        string expectedProgress,
        long expectedVersion,
        string progress,
        string note,
        string? photo,
        IReadOnlyList<VatTuKyThuat> materials)
    {
        return XuLyCongViecService.SaveAsync(
            id,
            expectedStatus,
            expectedProgress,
            expectedVersion,
            progress,
            note,
            photo,
            materials);
    }

    public static async Task ChangePasswordAsync(
        string oldPassword,
        string newPassword)
    {
        if (newPassword.Length < 8
            || newPassword.Length > 255
            || newPassword.Any(c => c > 127)
            || newPassword == oldPassword)
        {
            throw new InvalidOperationException(
                "Mật khẩu mới cần 8–255 ký tự ASCII "
                + "và khác mật khẩu cũ.");
        }

        using var connection = Database.CreateConnection();
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();

        command.Transaction = transaction;

        command.Parameters.Add(
            "@User", SqlDbType.VarChar, 20).Value = UserId();

        command.CommandText = @"
            SELECT MatKhau
            FROM dbo.NguoiDung WITH (UPDLOCK, HOLDLOCK)
            WHERE MaNguoiDung = @User
              AND VaiTro = N'Nhân viên kỹ thuật'
              AND TrangThai = N'Hoạt động';";

        string? currentPassword =
            await command.ExecuteScalarAsync() as string;

        if (!string.Equals(
            currentPassword,
            oldPassword,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Mật khẩu hiện tại không chính xác "
                + "hoặc tài khoản không hoạt động.");
        }

        command.CommandText = @"
            UPDATE dbo.NguoiDung
            SET MatKhau = @Password
            WHERE MaNguoiDung = @User;";

        command.Parameters.Add(
            "@Password",
            SqlDbType.VarChar,
            255).Value = newPassword;

        await command.ExecuteNonQueryAsync();
        transaction.Commit();
    }
}
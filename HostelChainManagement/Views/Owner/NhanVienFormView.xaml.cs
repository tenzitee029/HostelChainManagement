
using System;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Owner
{
    public partial class NhanVienFormView : UserControl
    {
        private readonly bool _dangSua;

        public event Action? HuyRequested;
        public event Action? LuuThanhCong;

        public NhanVienFormView(DataRowView? nhanVien = null)
        {
            InitializeComponent();

            _dangSua = nhanVien != null;

            if (!_dangSua)
            {
                txtTieuDe.Text = "Thêm nhân viên";
                txtMaNhanVien.Text = "Hệ thống tự động cấp";
                return;
            }

            txtTieuDe.Text = "Cập nhật nhân viên";

            txtMaNhanVien.Text =
                LayGiaTri(nhanVien!, "MaNhanVien");

            txtHoTen.Text =
                LayGiaTri(nhanVien!, "HoTen");

            txtSoDienThoai.Text =
                LayGiaTri(nhanVien!, "SoDienThoai");

            txtCCCD.Text =
                LayGiaTri(nhanVien!, "CCCD");

            string chucVu =
                LayGiaTri(nhanVien!, "ChucVu");

            var item = cmbVaiTro.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(x =>
                    string.Equals(
                        x.Content?.ToString(),
                        chucVu,
                        StringComparison.Ordinal));

            if (item != null)
            {
                cmbVaiTro.SelectedItem = item;
            }
            else
            {
                cmbVaiTro.SelectedIndex = 0;
            }
        }

        private static string LayGiaTri(
            DataRowView row,
            string tenCot)
        {
            if (!row.DataView.Table.Columns.Contains(tenCot))
                return "";

            if (row[tenCot] == DBNull.Value)
                return "";

            return Convert.ToString(row[tenCot]) ?? "";
        }

        private void Huy_Click(
            object sender,
            RoutedEventArgs e)
        {
            HuyRequested?.Invoke();
        }

        private async void XacNhan_Click(
            object sender,
            RoutedEventArgs e)
        {
            txtThongBao.Visibility = Visibility.Collapsed;

            string hoTen = txtHoTen.Text.Trim();
            string soDienThoai = txtSoDienThoai.Text.Trim();
            string cccd = txtCCCD.Text.Trim();

            string chucVu =
                (cmbVaiTro.SelectedItem as ComboBoxItem)?
                .Content?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(hoTen))
            {
                HienThiLoi("Vui lòng nhập họ tên nhân viên.");
                txtHoTen.Focus();
                return;
            }

            if (soDienThoai.Length != 10
                && soDienThoai.Length != 11)
            {
                HienThiLoi(
                    "Số điện thoại phải có 10 hoặc 11 chữ số.");
                txtSoDienThoai.Focus();
                return;
            }

            if (soDienThoai.Any(c => c < '0' || c > '9'))
            {
                HienThiLoi(
                    "Số điện thoại chỉ được chứa chữ số.");
                txtSoDienThoai.Focus();
                return;
            }

            if (cccd.Length != 12
                || cccd.Any(c => c < '0' || c > '9'))
            {
                HienThiLoi(
                    "CCCD phải gồm đúng 12 chữ số.");
                txtCCCD.Focus();
                return;
            }

            if (chucVu != "Quản lý" && chucVu != "Kỹ thuật")
            {
                HienThiLoi(
                    "Vui lòng chọn vai trò chuyên môn.");
                cmbVaiTro.Focus();
                return;
            }

            btnXacNhan.IsEnabled = false;

            try
            {
                string? maNhanVien = _dangSua
                    ? txtMaNhanVien.Text.Trim()
                    : null;

                var taiKhoan =
                    await ChuTroService.LuuNhanVienAsync(
                        maNhanVien,
                        hoTen,
                        soDienThoai,
                        cccd,
                        chucVu);

                if (taiKhoan != null)
                {
                    MessageBox.Show(
                        "Thêm mới nhân viên thành công."
                        + Environment.NewLine
                        + Environment.NewLine
                        + "Tên đăng nhập: "
                        + taiKhoan.TenDangNhap
                        + Environment.NewLine
                        + "Mật khẩu tạm: "
                        + taiKhoan.MatKhau
                        + Environment.NewLine
                        + Environment.NewLine
                        + "Hãy ghi lại thông tin đăng nhập này "
                        + "và bàn giao riêng cho nhân viên. "
                        + "Yêu cầu nhân viên đổi mật khẩu sau khi đăng nhập.",
                        "Tài khoản nhân viên mới",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "Cập nhật thông tin nhân viên thành công.",
                        "Thông báo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                LuuThanhCong?.Invoke();
            }
            catch (InvalidOperationException ex)
            {
                HienThiLoi(ex.Message);
            }
            catch (SqlException ex) when (
                ex.Number == 2601 || ex.Number == 2627)
            {
                HienThiLoi(
                    "Tên đăng nhập, số điện thoại hoặc CCCD đã tồn tại trong hệ thống.");
            }
            catch (SqlException)
            {
                HienThiLoi(
                    "Không thể lưu dữ liệu nhân viên vào SQL Server. "
                    + "Vui lòng kiểm tra kết nối và cấu trúc database.");
            }
            catch (Exception)
            {
                HienThiLoi(
                    "Đã xảy ra lỗi khi lưu thông tin nhân viên.");
            }
            finally
            {
                btnXacNhan.IsEnabled = true;
            }
        }

        private void HienThiLoi(string noiDung)
        {
            txtThongBao.Text = noiDung;
            txtThongBao.Visibility = Visibility.Visible;
        }
    }
}

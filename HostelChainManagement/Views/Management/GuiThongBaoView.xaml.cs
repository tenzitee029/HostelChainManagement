using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Management;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class GuiThongBaoView : UserControl
    {
        private DataTable? _danhSachPhong;
        private bool _dangTai;
        private bool _dangGui;
        private bool _sanSang;

        public GuiThongBaoView()
        {
            InitializeComponent();
            txtMaThongBao.Text = TaoMa();
            DatTrangThai();
        }

        private static string TaoMa()
        {
            return "TB" + Guid.NewGuid().ToString("N")
                .Substring(0, 18).ToUpperInvariant();
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_dangTai || _sanSang)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền gửi thông báo.";
                return;
            }

            _dangTai = true;
            DatTrangThai();
            txtThongBao.Text = "Đang tải dãy trọ và phòng...";

            try
            {
                var (day, phong) =
                    await GuiThongBaoService.TaiDuLieuAsync(
                        user.MaNguoiDung);

                if (!IsLoaded)
                    return;

                _danhSachPhong = phong;
                cboDayTro.ItemsSource = day.DefaultView;

                if (day.Rows.Count == 0)
                {
                    txtThongBao.Text = "Bạn chưa được phân công dãy trọ.";
                    return;
                }

                cboDayTro.SelectedIndex = 0;
                _sanSang = true;
                txtThongBao.Text = "";
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không tải được dữ liệu: " + ex.Message;
            }
            finally
            {
                _dangTai = false;
                DatTrangThai();
            }
        }

        private void DayTro_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            if (_danhSachPhong == null || cboPhong == null)
                return;

            string maDay =
                cboDayTro.SelectedValue?.ToString() ?? "";

            var ketQua = _danhSachPhong.Clone();

            foreach (DataRow dong in _danhSachPhong.Rows)
            {
                if (Convert.ToString(dong["MaDayTro"]) == maDay)
                    ketQua.ImportRow(dong);
            }

            cboPhong.ItemsSource = ketQua.DefaultView;
            cboPhong.SelectedIndex = -1;
        }

        private void PhamVi_Changed(
            object sender, RoutedEventArgs e)
        {
            if (cboPhong == null || rbPhong == null)
                return;

            cboPhong.IsEnabled = rbPhong.IsChecked == true;
        }

        private async void Gui_Click(
            object sender, RoutedEventArgs e)
        {
            if (_dangGui || !_sanSang)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền gửi thông báo.";
                return;
            }

            string tieuDe = txtTieuDe.Text.Trim();
            string noiDung = txtNoiDung.Text.Trim();

            string maDay =
                cboDayTro.SelectedValue?.ToString() ?? "";

            bool theoPhong = rbPhong.IsChecked == true;

            string maPhong =
                cboPhong.SelectedValue?.ToString() ?? "";

            if (tieuDe.Length == 0 || tieuDe.Length > 200)
            {
                txtThongBao.Text = "Tiêu đề phải có từ 1 đến 200 ký tự.";
                return;
            }

            if (noiDung.Length == 0)
            {
                txtThongBao.Text = "Vui lòng nhập nội dung thông báo.";
                return;
            }

            if (maDay.Length == 0 || (theoPhong && maPhong.Length == 0))
            {
                txtThongBao.Text =
                    "Vui lòng chọn dãy trọ và phòng nếu gửi theo phòng.";
                return;
            }

            string phamVi = theoPhong
                ? Convert.ToString(
                    (cboPhong.SelectedItem as DataRowView)?["TenHienThi"])
                    ?? maPhong
                : "Toàn bộ dãy";

            string tenDay = Convert.ToString(
                (cboDayTro.SelectedItem as DataRowView)?["TenDayTro"])
                ?? maDay;

            var xacNhan = MessageBox.Show(
                $"Gửi thông báo “{tieuDe}”?\n"
                + $"Dãy: {tenDay}\nPhạm vi: {phamVi}\n\n"
                + "Người nhận là khách có hợp đồng đang hiệu lực.",
                "Xác nhận gửi",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (xacNhan != MessageBoxResult.Yes)
                return;

            _dangGui = true;
            DatTrangThai();
            txtThongBao.Text = "Đang gửi thông báo...";

            int soNguoiNhan = 0;

            try
            {
                soNguoiNhan = await GuiThongBaoService.GuiAsync(
                    () => txtMaThongBao.Text,
                    user.MaNguoiDung,
                    maDay,
                    maPhong,
                    theoPhong,
                    tieuDe,
                    noiDung);
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                txtThongBao.Text =
                    "Dữ liệu đang được cập nhật. Vui lòng thử lại.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không gửi được thông báo: " + ex.Message;
            }
            finally
            {
                _dangGui = false;
                DatTrangThai();
            }

            if (soNguoiNhan > 0 &&
                txtThongBao.Text == "Đang gửi thông báo...")
            {
                MessageBox.Show(
                    $"Đã lưu thông báo cho {soNguoiNhan} tài khoản khách thuê.",
                    "Gửi thành công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                txtMaThongBao.Text = TaoMa();
                txtTieuDe.Clear();
                txtNoiDung.Clear();
                txtThongBao.Text = "";

                txtTieuDe.Focus();
            }
        }

        private void DatTrangThai()
        {
            bool choPhep =
                _sanSang && !_dangTai && !_dangGui;

            pnlThongTin.IsEnabled = choPhep;
            btnGui.IsEnabled = choPhep;
            btnGui.Content = _dangGui
                ? "Đang gửi..."
                : "Gửi thông báo";

            if (cboPhong != null)
                cboPhong.IsEnabled = rbPhong.IsChecked == true;
        }
    }
}
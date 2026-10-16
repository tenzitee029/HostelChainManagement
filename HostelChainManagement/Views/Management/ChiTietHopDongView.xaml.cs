using System;
using System.Windows;
using System.Windows.Controls;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Management;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class ChiTietHopDongView : UserControl
    {
        private readonly string _maHopDong;
        private bool _dangTai;
        private bool _daTai;

        public event Action? QuayLai;

        public ChiTietHopDongView(string maHopDong)
        {
            InitializeComponent();

            _maHopDong = maHopDong;
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_dangTai || _daTai)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền xem hợp đồng.";
                return;
            }

            _dangTai = true;
            txtThongBao.Text = "Đang tải chi tiết hợp đồng...";
            pnlChiTiet.Visibility = Visibility.Collapsed;

            try
            {
                var duLieu =
                    await ChiTietHopDongService.LayChiTietAsync(
                        _maHopDong,
                        user.MaNguoiDung);

                if (duLieu == null)
                {
                    txtThongBao.Text =
                        "Hợp đồng không tồn tại hoặc bạn không có quyền xem.";
                    return;
                }

                var hopDong = duLieu.HopDong;
                var thanhVien = duLieu.ThanhVien;

                if (!IsLoaded)
                    return;

                pnlChiTiet.DataContext = hopDong.DefaultView[0];
                dgThanhVien.ItemsSource = thanhVien.DefaultView;

                txtThanhVienTrong.Visibility =
                    thanhVien.Rows.Count == 0
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                pnlChiTiet.Visibility = Visibility.Visible;
                txtThongBao.Text = "";
                _daTai = true;
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không tải được chi tiết hợp đồng: " + ex.Message;
            }
            finally
            {
                _dangTai = false;
            }
        }

        private void QuayLai_Click(
            object sender, RoutedEventArgs e)
        {
            QuayLai?.Invoke();
        }
    }
}
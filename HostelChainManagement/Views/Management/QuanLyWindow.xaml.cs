using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Views.Account;
using QuanLyChuoiNhaTro.Views.Auth;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class QuanLyWindow : Window
    {
        public QuanLyWindow()
        {
            InitializeComponent();

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                throw new InvalidOperationException(
                    "Bạn không có quyền mở màn hình quản lý.");
            }

            txtUser.Text = user.HoTen;
            ShowOverview();
        }

        private void ShowView(
            UserControl view,
            string title,
            Button selectedButton)
        {
            Button[] buttons =
            {
                btnOverview,
                btnRooms,
                btnTenants,
                btnContracts,
                btnInvoices,
                btnRepairs,
                btnNotifications,
                btnReports,
                btnProfile,
                btnPassword
            };

            foreach (var button in buttons)
            {
                button.Background = Brushes.Transparent;
                button.Foreground = new SolidColorBrush(
                    Color.FromRgb(203, 213, 225));
            }

            selectedButton.Background = new SolidColorBrush(
                Color.FromRgb(37, 99, 235));

            selectedButton.Foreground = Brushes.White;

            txtBreadcrumb.Text = $"Hệ thống / {title}";
            contentArea.Content = view;
        }

        // Tổng quan.
        private void ShowOverview()
        {
            ShowView(
                new TongQuanQuanLyView(),
                "Tổng quan",
                btnOverview);
        }

        private void Overview_Click(
            object sender, RoutedEventArgs e)
        {
            ShowOverview();
        }

        // Phòng trọ.
        private void Rooms_Click(
            object sender, RoutedEventArgs e)
        {
            ShowView(
                new PhongTroView(),
                "Quản lý phòng",
                btnRooms);
        }

        // Khách thuê.
        private void Tenants_Click(
            object sender, RoutedEventArgs e)
        {
            HienThiKhachThue();
        }

        private void HienThiKhachThue()
        {
            var view = new QuanLyKhachThueView();

            view.MoForm += HienThiFormKhachThue;

            ShowView(view, "Khách thuê", btnTenants);
        }

        private void HienThiFormKhachThue(DataRowView? khach)
        {
            var form = new KhachThueFormView(khach);

            form.QuayLai += HienThiKhachThue;
            form.DaLuu += HienThiKhachThue;

            ShowView(
                form,
                khach == null
                    ? "Khách thuê / Thêm khách thuê"
                    : "Khách thuê / Cập nhật khách thuê",
                btnTenants);
        }

        // Hợp đồng.
        private void Contracts_Click(
            object sender, RoutedEventArgs e)
        {
            HienThiHopDong();
        }

        private void HienThiHopDong()
        {
            var view = new QuanLyHopDongView();

            view.MoLapHopDong += HienThiLapHopDong;
            view.MoGiaHan += HienThiGiaHanHopDong;
            view.MoThanhLy += HienThiThanhLyHopDong;

            ShowView(view, "Hợp đồng", btnContracts);
        }

        private void HienThiLapHopDong()
        {
            var form = new LapHopDongView();

            form.QuayLai += HienThiHopDong;
            form.DaLuu += HienThiHopDong;

            ShowView(
                form,
                "Hợp đồng / Lập hợp đồng thuê",
                btnContracts);
        }

        private void HienThiGiaHanHopDong(string maHopDong)
        {
            var form = new GiaHanHopDongView(maHopDong);

            form.QuayLai += HienThiHopDong;
            form.DaLuu += HienThiHopDong;

            ShowView(
                form,
                "Hợp đồng / Gia hạn hợp đồng",
                btnContracts);
        }

        private void HienThiThanhLyHopDong(string maHopDong)
        {
            var form = new ThanhLyHopDongView(maHopDong);

            form.QuayLai += HienThiHopDong;
            form.DaLuu += HienThiHopDong;

            ShowView(
                form,
                "Hợp đồng / Thanh lý hợp đồng",
                btnContracts);
        }

        // Hóa đơn.
        private void Invoices_Click(
            object sender, RoutedEventArgs e)
        {
            HienThiHoaDon();
        }

        private void HienThiHoaDon()
        {
            var view = new QuanLyHoaDonView();

            view.MoTaoHoaDon += HienThiTaoHoaDon;
            view.MoThanhToan += HienThiXacNhanThanhToan;

            ShowView(view, "Hóa đơn", btnInvoices);
        }

        private void HienThiTaoHoaDon()
        {
            var form = new TaoHoaDonView();

            form.QuayLai += HienThiHoaDon;
            form.DaLuu += HienThiHoaDon;

            ShowView(
                form,
                "Hóa đơn / Tạo hóa đơn hàng tháng",
                btnInvoices);
        }

        private void HienThiXacNhanThanhToan(string maHoaDon)
        {
            var form = new XacNhanThanhToanView(maHoaDon);

            form.QuayLai += HienThiHoaDon;
            form.DaLuu += HienThiHoaDon;

            ShowView(
                form,
                "Hóa đơn / Xác nhận thanh toán",
                btnInvoices);
        }

        // Danh sách yêu cầu sửa chữa.
        private void Repairs_Click(
            object sender, RoutedEventArgs e)
        {
            HienThiSuaChua();
        }

        private void HienThiSuaChua()
        {
            var view = new QuanLySuaChuaView();

            view.MoChiTiet += HienThiChiTietYeuCau;

            ShowView(view, "Sửa chữa", btnRepairs);
        }

        // Chi tiết yêu cầu.
        private void HienThiChiTietYeuCau(string maSuCo)
        {
            var form = new ChiTietYeuCauView(maSuCo);

            form.QuayLai += HienThiSuaChua;
            form.MoPhanCong += HienThiPhanCongKyThuat;

            ShowView(
                form,
                "Sửa chữa / Chi tiết yêu cầu",
                btnRepairs);
        }

        // Phân công kỹ thuật viên.
        private void HienThiPhanCongKyThuat(string maSuCo)
        {
            var form = new PhanCongKyThuatView(maSuCo);

            form.QuayLai += () =>
                HienThiChiTietYeuCau(maSuCo);

            form.DaLuu += HienThiSuaChua;

            ShowView(
                form,
                "Sửa chữa / Phân công nhân viên kỹ thuật",
                btnRepairs);
        }

        // Thông báo.
        private void Notifications_Click(
            object sender, RoutedEventArgs e)
        {
            ShowView(
                new GuiThongBaoView(),
                "Thông báo / Gửi thông báo tới khách thuê",
                btnNotifications);
        }

        // Hồ sơ cá nhân.
        private void Profile_Click(
            object sender, RoutedEventArgs e)
        {
            var view = new HoSoView();

            view.ProfileUpdated += (_, _) =>
            {
                txtUser.Text =
                    UserSession.CurrentUser?.HoTen ?? "";
            };

            ShowView(view, "Hồ sơ cá nhân", btnProfile);
        }

        // Đổi mật khẩu.
        private void Password_Click(
            object sender, RoutedEventArgs e)
        {
            ShowView(
                new DoiMatKhauView(),
                "Đổi mật khẩu",
                btnPassword);
        }

        // Đăng xuất.
        private void Logout_Click(
            object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show(
                this,
                "Bạn có chắc chắn muốn đăng xuất?",
                "Xác nhận",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
                return;

            UserSession.SignOut();

            var login = new LoginWindow();
            Application.Current.MainWindow = login;

            login.Show();
            Close();
        }
    }
}
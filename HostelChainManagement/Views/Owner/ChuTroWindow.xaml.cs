
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Views.Account;
using QuanLyChuoiNhaTro.Views.Auth;

namespace QuanLyChuoiNhaTro.Views.Owner
{
    public partial class ChuTroWindow : Window
    {
        private XemHopDongHoaDonView? _xemHopDongHoaDonView;
        private XemBaoCaoView? _xemBaoCaoView;

        public ChuTroWindow()
        {
            InitializeComponent();

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Chủ nhà trọ")
            {
                throw new InvalidOperationException(
                    "Bạn không có quyền mở giao diện Chủ nhà trọ.");
            }

            txtUser.Text = string.IsNullOrWhiteSpace(user.HoTen)
                ? "—"
                : user.HoTen;

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
                btnDayTro,
                btnNhanVien,
                btnPhanCong,
                btnHopDong,
                btnBaoCao,
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

            txtBreadcrumb.Text = "Hệ thống / " + title;

            contentArea.Content = view;
        }

        private void ShowOverview()
        {
            var view = new TongQuanChuTroView();

            view.NavigateRequested += OnOverviewNavigate;

            ShowView(
                view,
                "Tổng quan",
                btnOverview);
        }

        private void ShowDayTro()
        {
            var view = new QuanLyDayTroView();

            view.MoForm += ShowDayTroForm;

            ShowView(
                view,
                "Quản lý các dãy trọ",
                btnDayTro);
        }

        private void ShowDayTroForm(DataRowView? dayTro)
        {
            var form = new DayTroFormView(dayTro);

            form.LuuThanhCong += ShowDayTro;

            ShowView(
                form,
                dayTro == null
                    ? "Dãy trọ / Thêm dãy trọ"
                    : "Dãy trọ / Cập nhật dãy trọ",
                btnDayTro);
        }

        private void ShowNhanVien()
        {
            var view = new QuanLyNhanVienView();

            view.MoForm += ShowNhanVienForm;

            ShowView(
                view,
                "Quản lý nhân viên",
                btnNhanVien);
        }

        private void ShowNhanVienForm(DataRowView? nhanVien)
        {
            var form = new NhanVienFormView(nhanVien);

            form.HuyRequested += ShowNhanVien;
            form.LuuThanhCong += ShowNhanVien;

            ShowView(
                form,
                nhanVien == null
                    ? "Nhân viên / Thêm nhân viên"
                    : "Nhân viên / Cập nhật nhân viên",
                btnNhanVien);
        }

        private void ShowPhanCong()
        {
            ShowView(
                new PhanCongQuanLyView(),
                "Phân công quản lý",
                btnPhanCong);
        }

        private void ShowHopDongHoaDon()
        {
            if (_xemHopDongHoaDonView == null)
            {
                _xemHopDongHoaDonView =
                    new XemHopDongHoaDonView();

                _xemHopDongHoaDonView
                    .XemChiTietHopDongRequested +=
                    ShowChiTietHopDong;

                _xemHopDongHoaDonView
                    .XemChiTietHoaDonRequested +=
                    ShowChiTietHoaDon;
            }

            ShowView(
                _xemHopDongHoaDonView,
                "Xem hợp đồng, hóa đơn",
                btnHopDong);
        }

        private void ShowChiTietHopDong(string maHopDong)
        {
            var view = new ChiTietHopDongView(maHopDong);

            view.QuayLaiRequested += ShowHopDongHoaDon;

            ShowView(
                view,
                "Hợp đồng & hóa đơn / Chi tiết hợp đồng",
                btnHopDong);
        }

        private void ShowChiTietHoaDon(string maHoaDon)
        {
            var view = new ChiTietHoaDonView(maHoaDon);

            view.QuayLaiRequested += ShowHopDongHoaDon;

            ShowView(
                view,
                "Hợp đồng & hóa đơn / Chi tiết hóa đơn",
                btnHopDong);
        }

        private void ShowBaoCao()
        {
            if (_xemBaoCaoView == null)
            {
                _xemBaoCaoView = new XemBaoCaoView();

                if (_xemBaoCaoView.FindName("txtTongCongNo")
                    is TextBlock tongCongNo)
                {
                    tongCongNo.Cursor = Cursors.Hand;

                    tongCongNo.Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(37, 99, 235));

                    tongCongNo.TextDecorations =
                        TextDecorations.Underline;

                    tongCongNo.ToolTip =
                        "Bấm để mở danh sách nhắc nợ quá hạn";

                    tongCongNo.MouseLeftButtonUp +=
                        (_, _) => ShowNhacNo();
                }
            }

            ShowView(
                _xemBaoCaoView,
                "Xem báo cáo",
                btnBaoCao);
        }

        private void ShowNhacNo()
        {
            var view = new NhacNoView();

            view.QuayLaiRequested += ShowBaoCao;

            ShowView(
                view,
                "Báo cáo / Nhắc nợ",
                btnBaoCao);
        }

        private void Overview_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowOverview();
        }

        private void DayTro_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowDayTro();
        }

        private void NhanVien_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowNhanVien();
        }

        private void PhanCong_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowPhanCong();
        }

        private void HopDong_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowHopDongHoaDon();
        }

        private void BaoCao_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowBaoCao();
        }

        private void OnOverviewNavigate(string destination)
        {
            switch (destination)
            {
                case "DayTro":
                    ShowDayTro();
                    break;

                case "NhanVien":
                    ShowNhanVien();
                    break;

                case "PhanCong":
                    ShowPhanCong();
                    break;

                case "HopDong":
                    ShowHopDongHoaDon();
                    break;

                case "BaoCao":
                    ShowBaoCao();
                    break;
            }
        }

        private void Profile_Click(
            object sender,
            RoutedEventArgs e)
        {
            var view = new HoSoView();

            view.ProfileUpdated += (_, _) =>
            {
                txtUser.Text =
                    UserSession.CurrentUser?.HoTen ?? "—";
            };

            ShowView(
                view,
                "Hồ sơ cá nhân",
                btnProfile);
        }

        private void Password_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowView(
                new DoiMatKhauView(),
                "Đổi mật khẩu",
                btnPassword);
        }

        private void Logout_Click(
            object sender,
            RoutedEventArgs e)
        {
            var answer = MessageBox.Show(
                this,
                "Bạn có chắc chắn muốn đăng xuất?",
                "Xác nhận đăng xuất",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
                return;

            UserSession.SignOut();

            var loginWindow = new LoginWindow();

            Application.Current.MainWindow = loginWindow;

            loginWindow.Show();

            Close();
        }
    }
}

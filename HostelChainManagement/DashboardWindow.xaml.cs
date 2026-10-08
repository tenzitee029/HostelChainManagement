using QuanLyChuoiNhaTro;
using QuanLyChuoiNhaTro.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace QuanLyChuoiNhaTro
{
    public partial class DashboardWindow : Window
    {
        public DashboardWindow()
        {
            InitializeComponent();

            var user = UserSession.CurrentUser
                ?? throw new InvalidOperationException(
                    "Bạn chưa đăng nhập.");

            txtUser.Text = $"{user.HoTen} — {user.VaiTro}";
            txtTitle.Text = $"Trang chính — {user.VaiTro}";

            lstFunctions.ItemsSource = user.VaiTro switch
            {
                "Admin" => new[]
                {
                    "Quản lý tài khoản",
                    "Bảo trì hệ thống"
                },

                "Chủ nhà trọ" => new[]
                {
                    "Quản lý dãy trọ",
                    "Quản lý nhân viên",
                    "Phân công quản lý dãy trọ",
                    "Xem hợp đồng và hóa đơn",
                    "Xem báo cáo và nhắc nợ"
                },

                "Nhân viên quản lý" => new[]
                {
                    "Quản lý phòng",
                    "Quản lý khách thuê",
                    "Lập hợp đồng thuê",
                    "Gia hạn / Thanh lý hợp đồng",
                    "Tạo hóa đơn hàng tháng",
                    "Xác nhận thanh toán",
                    "Lập báo cáo",
                    "Tiếp nhận yêu cầu sửa chữa",
                    "Phân công nhân viên kỹ thuật",
                    "Gửi thông báo"
                },

                "Nhân viên kỹ thuật" => new[]
                {
                    "Danh sách yêu cầu sửa chữa được phân công",
                    "Cập nhật tiến độ / Xác nhận hoàn thành",
                    "Gửi thông báo"
                },

                "Khách thuê" => new[]
                {
                    "Phòng đang thuê",
                    "Hợp đồng thuê",
                    "Hóa đơn hàng tháng",
                    "Thanh toán online",
                    "Gửi yêu cầu sửa chữa",
                    "Gửi yêu cầu trả phòng"
                },

                _ => Array.Empty<string>()
            };
        }

        private void Logout_Click(
            object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show(
                "Bạn có chắc chắn muốn đăng xuất khỏi hệ thống?",
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

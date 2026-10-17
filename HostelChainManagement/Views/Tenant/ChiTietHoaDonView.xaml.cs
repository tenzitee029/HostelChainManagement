using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Tenant;

namespace QuanLyChuoiNhaTro.Views.Tenant
{
    public partial class ChiTietHoaDonView : UserControl
    {
        private readonly string _maHoaDon;
        private bool _loading;

        public event Action? BackRequested;

        public ChiTietHoaDonView(string maHoaDon)
        {
            InitializeComponent();

            _maHoaDon = maHoaDon;
        }

        private async void View_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            if (_loading)
                return;

            _loading = true;

            var session = UserSession.CurrentUser;

            detailsPanel.Visibility = Visibility.Collapsed;
            detailsPanel.DataContext = null;

            dgKhoanThu.ItemsSource = null;
            btnPayOnline.Visibility = Visibility.Collapsed;

            txtStatus.Text = "Đang tải chi tiết hóa đơn...";

            try
            {
                var data =
                    await HoaDonKhachThueService.GetChiTietAsync(
                        _maHoaDon);

                if (!IsLoaded
                    || !ReferenceEquals(
                        session,
                        UserSession.CurrentUser))
                {
                    return;
                }

                detailsPanel.DataContext = data.ThongTin;

                dgKhoanThu.ItemsSource =
                    data.KhoanThu.DefaultView;

                txtEmptyItems.Visibility =
                    data.KhoanThu.Rows.Count == 0
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                btnPayOnline.Visibility =
                    (string)data.ThongTin["TrangThai"]
                        == "Chưa thanh toán"
                            ? Visibility.Visible
                            : Visibility.Collapsed;

                detailsPanel.Visibility = Visibility.Visible;

                txtStatus.Text = "";
            }
            catch (SqlException)
            {
                txtStatus.Text =
                    "Không tải được chi tiết hóa đơn. " +
                    "Vui lòng kiểm tra kết nối SQL Server " +
                    "và cấu trúc CSDL.";
            }
            catch (Exception ex)
            {
                txtStatus.Text = ex.Message;
            }
            finally
            {
                _loading = false;
            }
        }

        private void Back_Click(
            object sender,
            RoutedEventArgs e)
        {
            BackRequested?.Invoke();
        }
    }
}
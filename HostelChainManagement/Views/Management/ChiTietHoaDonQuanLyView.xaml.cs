using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class ChiTietHoaDonQuanLyView : UserControl
    {
        private readonly string _maHoaDon;

        private bool _loading;
        private bool _canPay;

        public event Action? BackRequested;
        public event Action<string>? PaymentRequested;

        public ChiTietHoaDonQuanLyView(string maHoaDon)
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
            _canPay = false;

            var session = UserSession.CurrentUser;

            detailsPanel.Visibility = Visibility.Collapsed;
            detailsPanel.DataContext = null;

            dgKhoanThu.ItemsSource = null;

            btnXacNhanThanhToan.Visibility =
                Visibility.Collapsed;

            txtStatus.Text = "Đang tải chi tiết hóa đơn...";

            try
            {
                var data =
                    await ChiTietHoaDonQuanLyService.GetAsync(
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

                _canPay =
                    Convert.ToDecimal(
                        data.ThongTin["ConPhaiTra"]) > 0;

                btnXacNhanThanhToan.Visibility = _canPay
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                detailsPanel.Visibility = Visibility.Visible;

                txtStatus.Text = "";
            }
            catch (SqlException)
            {
                txtStatus.Text =
                    "Không tải được chi tiết hóa đơn. "
                    + "Vui lòng kiểm tra SQL Server.";
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

        private void Payment_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_loading || !_canPay)
                return;

            if (UserSession.CurrentUser?.VaiTro
                != "Nhân viên quản lý")
            {
                return;
            }

            PaymentRequested?.Invoke(_maHoaDon);
        }
    }
}
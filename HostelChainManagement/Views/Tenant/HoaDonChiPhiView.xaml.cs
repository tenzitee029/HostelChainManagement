using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Tenant
{
    public partial class HoaDonChiPhiView : UserControl
    {
        private bool _loading;

        public event Action<string>? InvoiceSelected;

        public HoaDonChiPhiView()
        {
            InitializeComponent();
        }

        private async void View_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await LoadAsync();
        }

        private async void Refresh_Click(
            object sender,
            RoutedEventArgs e)
        {
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_loading)
                return;

            _loading = true;

            var session = UserSession.CurrentUser;

            btnRefresh.IsEnabled = false;
            btnDetail.IsEnabled = false;

            dgHoaDon.ItemsSource = null;
            txtStatus.Text = "Đang tải hóa đơn...";

            try
            {
                var table =
                    await HoaDonKhachThueService.GetDanhSachAsync();

                if (!IsLoaded
                    || !ReferenceEquals(
                        session,
                        UserSession.CurrentUser))
                {
                    return;
                }

                dgHoaDon.ItemsSource = table.DefaultView;

                txtStatus.Text = table.Rows.Count == 0
                    ? "Chưa có hóa đơn nào được phát hành cho phòng của bạn"
                    : "";
            }
            catch (SqlException)
            {
                txtStatus.Text =
                    "Không tải được hóa đơn. " +
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
                btnRefresh.IsEnabled = true;
            }
        }

        private void Invoice_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            btnDetail.IsEnabled =
                dgHoaDon.SelectedItem is DataRowView;
        }

        private void Detail_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenSelected();
        }

        private void Invoice_DoubleClick(
            object sender,
            MouseButtonEventArgs e)
        {
            // Chỉ mở khi nhấp đúp vào dòng dữ liệu.
            if (e.OriginalSource is DependencyObject source
                && ItemsControl.ContainerFromElement(
                    dgHoaDon,
                    source) is DataGridRow)
            {
                OpenSelected();
            }
        }

        private void OpenSelected()
        {
            if (dgHoaDon.SelectedItem is DataRowView row)
            {
                InvoiceSelected?.Invoke(
                    (string)row["MaHoaDon"]);
            }
        }
    }
}
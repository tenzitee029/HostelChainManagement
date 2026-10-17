using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services.Owner;

namespace QuanLyChuoiNhaTro.Views.Owner
{
    public partial class QuanLyDayTroView : UserControl
    {
        public event Action<DataRowView?>? MoForm;

        private bool _dangTai;

        public QuanLyDayTroView()
        {
            InitializeComponent();

            Loaded += QuanLyDayTroView_Loaded;
        }

        private async void QuanLyDayTroView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= QuanLyDayTroView_Loaded;

            await TaiDanhSachDayTroAsync();
        }

        private async Task TaiDanhSachDayTroAsync()
        {
            if (_dangTai)
                return;

            _dangTai = true;

            dgvDayTro.ItemsSource = null;

            txtEmptyState.Text = "Đang tải danh sách dãy trọ...";
            txtEmptyState.Visibility = Visibility.Visible;

            try
            {
                DataTable table =
                    await QuanLyDayTroService.GetDanhSachDayTroAsync();

                dgvDayTro.ItemsSource = table.DefaultView;

                if (table.Rows.Count == 0)
                {
                    txtEmptyState.Text =
                        "Chưa có dãy trọ thuộc tài khoản này.";

                    txtEmptyState.Visibility = Visibility.Visible;
                }
                else
                {
                    txtEmptyState.Visibility = Visibility.Collapsed;
                }
            }
            catch (InvalidOperationException ex)
            {
                dgvDayTro.ItemsSource = null;

                txtEmptyState.Text = ex.Message;
                txtEmptyState.Visibility = Visibility.Visible;
            }
            catch (SqlException)
            {
                dgvDayTro.ItemsSource = null;

                txtEmptyState.Text =
                    "Không thể tải danh sách dãy trọ. " +
                    "Vui lòng kiểm tra kết nối SQL Server.";

                txtEmptyState.Visibility = Visibility.Visible;
            }
            catch (Exception)
            {
                dgvDayTro.ItemsSource = null;

                txtEmptyState.Text =
                    "Đã xảy ra lỗi khi tải danh sách dãy trọ.";

                txtEmptyState.Visibility = Visibility.Visible;
            }
            finally
            {
                _dangTai = false;
            }
        }

        private void ThemDayTro_Click(
            object sender,
            RoutedEventArgs e)
        {
            MoForm?.Invoke(null);
        }

        private void SuaDayTro_Click(
            object sender,
            RoutedEventArgs e)
        {
            DataRowView? dayTro = null;

            if (sender is Button button)
            {
                dayTro = button.DataContext as DataRowView;
            }

            dayTro ??= dgvDayTro.SelectedItem as DataRowView;

            if (dayTro == null)
            {
                MessageBox.Show(
                    "Vui lòng chọn dãy trọ cần cập nhật.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            MoForm?.Invoke(dayTro);
        }
    }
}
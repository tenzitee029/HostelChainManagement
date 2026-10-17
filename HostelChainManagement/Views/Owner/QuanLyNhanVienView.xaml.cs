using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services.Owner;

namespace QuanLyChuoiNhaTro.Views.Owner
{
    public partial class QuanLyNhanVienView : UserControl
    {
        public event Action<DataRowView?>? MoForm;

        private bool _dangTai;

        public QuanLyNhanVienView()
        {
            InitializeComponent();

            Loaded += QuanLyNhanVienView_Loaded;
        }

        private async void QuanLyNhanVienView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= QuanLyNhanVienView_Loaded;

            await TaiDanhSachNhanVienAsync();
        }

        private async Task TaiDanhSachNhanVienAsync()
        {
            if (_dangTai)
                return;

            _dangTai = true;

            dgvNhanVien.ItemsSource = null;

            txtEmptyState.Text = "Đang tải danh sách nhân viên...";
            txtEmptyState.Visibility = Visibility.Visible;
            txtThongBao.Visibility = Visibility.Collapsed;

            try
            {
                DataTable table =
                    await QuanLyNhanVienService.GetDanhSachNhanVienAsync();

                dgvNhanVien.ItemsSource = table.DefaultView;

                if (table.Rows.Count == 0)
                {
                    txtEmptyState.Text =
                        "Chưa có nhân viên thuộc các dãy trọ của bạn.";

                    txtEmptyState.Visibility = Visibility.Visible;
                }
                else
                {
                    txtEmptyState.Visibility = Visibility.Collapsed;
                }
            }
            catch (InvalidOperationException ex)
            {
                dgvNhanVien.ItemsSource = null;

                txtEmptyState.Text = ex.Message;
                txtEmptyState.Visibility = Visibility.Visible;
            }
            catch (SqlException)
            {
                dgvNhanVien.ItemsSource = null;

                txtEmptyState.Text =
                    "Không thể tải danh sách nhân viên. "
                    + "Vui lòng kiểm tra kết nối SQL Server.";

                txtEmptyState.Visibility = Visibility.Visible;
            }
            catch (Exception)
            {
                dgvNhanVien.ItemsSource = null;

                txtEmptyState.Text =
                    "Đã xảy ra lỗi khi tải danh sách nhân viên.";

                txtEmptyState.Visibility = Visibility.Visible;
            }
            finally
            {
                _dangTai = false;
            }
        }

        private void ThemNhanVien_Click(
            object sender,
            RoutedEventArgs e)
        {
            MoForm?.Invoke(null);
        }

        private void SuaNhanVien_Click(
            object sender,
            RoutedEventArgs e)
        {
            DataRowView? nhanVien = null;

            if (sender is Button button)
            {
                nhanVien = button.DataContext as DataRowView;
            }

            nhanVien ??= dgvNhanVien.SelectedItem as DataRowView;

            if (nhanVien == null)
            {
                MessageBox.Show(
                    "Vui lòng chọn nhân viên cần cập nhật.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            MoForm?.Invoke(nhanVien);
        }
    }
}
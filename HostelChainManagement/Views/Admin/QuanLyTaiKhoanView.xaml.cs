using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Admin
{
    public partial class QuanLyTaiKhoanView : UserControl
    {
        private DataTable? _accounts;
        private bool _busy;

        public bool IsBusy => _busy;
        public event Action? CreateRequested;

        public QuanLyTaiKhoanView()
        {
            InitializeComponent();

            cboRoleFilter.ItemsSource =
                new[] { "Tất cả vai trò" }
                    .Concat(AdminService.Roles)
                    .ToArray();

            cboRoleFilter.SelectedIndex = 0;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            editorPanel.IsEnabled = !busy;
        }

        private async Task RunAsync(
            Func<Task> action,
            string? success = null)
        {
            if (_busy)
                return;

            SetBusy(true);
            txtStatus.Text = "Đang xử lý...";

            try
            {
                await action();

                if (success != null)
                    txtStatus.Text = success;
                else if (txtStatus.Text == "Đang xử lý...")
                    txtStatus.Text = "";
            }
            catch (SqlException ex)
            {
                txtStatus.Text = ex.Number == 50001
                    ? ex.Message
                    : "Không thực hiện được thao tác. "
                        + "Vui lòng kiểm tra SQL Server.";
            }
            catch (Exception ex)
            {
                txtStatus.Text = ex.Message;
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            await RunAsync(LoadAccountsAsync);
        }

        private async Task LoadAccountsAsync()
        {
            _accounts =
                await AdminService.GetAccountsAsync();

            if (IsLoaded)
                ApplyAccountFilter();
        }

        private void ApplyAccountFilter()
        {
            if (_accounts == null
                || txtSearch == null
                || cboStatus == null)
            {
                return;
            }

            string role =
                cboRoleFilter.SelectedItem as string
                ?? "Tất cả vai trò";

            string status =
                (cboStatus.SelectedItem as ComboBoxItem)?
                    .Content.ToString()
                ?? "Tất cả trạng thái";

            var rows = _accounts.DefaultView.Cast<DataRowView>()
                .Where(row =>
                    (row["TenDangNhap"].ToString() ?? "")
                        .Contains(
                            txtSearch.Text.Trim(),
                            StringComparison.OrdinalIgnoreCase)
                    && (role == "Tất cả vai trò"
                        || row["VaiTro"].ToString() == role)
                    && (status == "Tất cả trạng thái"
                        || row["TrangThai"].ToString() == status))
                .ToList();

            accountsGrid.ItemsSource = rows;

            txtNoAccounts.Visibility = rows.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void AccountFilter_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            ApplyAccountFilter();
        }

        private void AccountSearch_Changed(
            object sender, TextChangedEventArgs e)
        {
            ApplyAccountFilter();
        }

        private void NewAccount_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_busy)
                CreateRequested?.Invoke();
        }

        private async void ToggleStatus_Click(
            object sender, RoutedEventArgs e)
        {
            if (_busy
                || (sender as Button)?.DataContext
                    is not DataRowView row)
            {
                return;
            }

            string id = row["MaNguoiDung"].ToString()!;
            string old = row["TrangThai"].ToString()!;

            string next =
                old == "Hoạt động" ? "Khóa" : "Hoạt động";

            string action =
                next == "Khóa" ? "Khóa" : "Mở khóa";

            if (MessageBox.Show(
                Window.GetWindow(this),
                $"{action} tài khoản {row["TenDangNhap"]}?",
                "Xác nhận",
                MessageBoxButton.YesNo)
                != MessageBoxResult.Yes)
            {
                return;
            }

            await RunAsync(async () =>
            {
                await AdminService.SetStatusAsync(
                    id, old, next);

                await LoadAccountsAsync();
            }, "Cập nhật trạng thái tài khoản thành công.");
        }

        private async void ChangeRole_Click(
            object sender, RoutedEventArgs e)
        {
            if (_busy
                || accountsGrid.SelectedItem
                    is not DataRowView row)
            {
                return;
            }

            string id = row["MaNguoiDung"].ToString()!;
            string old = row["VaiTro"].ToString()!;

            await RunAsync(async () =>
            {
                var info = AdminProfileDialog.Show(
                    Window.GetWindow(this),
                    await AdminService.GetDaysAsync(),
                    old,
                    false);

                if (info == null)
                    return;

                await AdminService.SetRoleAsync(
                    id, old, info.Role, info.Day);

                await LoadAccountsAsync();
                txtStatus.Text = "Đã cập nhật vai trò.";
            });
        }
    }
}
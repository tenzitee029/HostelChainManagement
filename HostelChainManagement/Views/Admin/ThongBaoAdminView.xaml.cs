using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Admin
{
    public partial class ThongBaoAdminView : UserControl
    {
        private bool _loading;

        public bool IsBusy => _loading;

        public ThongBaoAdminView()
        {
            InitializeComponent();
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }

        private async void Reload_Click(
            object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_loading)
                return;

            _loading = true;
            editorPanel.IsEnabled = false;
            txtStatus.Text = "Đang tải thông báo...";

            var session = UserSession.CurrentUser;

            try
            {
                var table =
                    await AdminService.GetNotificationsAsync();

                if (!IsLoaded
                    || !ReferenceEquals(
                        session, UserSession.CurrentUser))
                {
                    return;
                }

                notificationsGrid.ItemsSource =
                    table.DefaultView;

                txtNotification.Clear();

                txtStatus.Text = table.Rows.Count == 0
                    ? "Bạn chưa có thông báo."
                    : "";
            }
            catch (SqlException)
            {
                txtStatus.Text =
                    "Không tải được thông báo. "
                    + "Vui lòng kiểm tra SQL Server.";
            }
            catch (Exception ex)
            {
                txtStatus.Text = ex.Message;
            }
            finally
            {
                _loading = false;
                editorPanel.IsEnabled = true;
            }
        }

        private void Notification_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            if (notificationsGrid.SelectedItem
                is DataRowView row)
            {
                txtNotification.Text =
                    row["TieuDe"] + "\n\n" + row["NoiDung"];
            }
        }
    }
}
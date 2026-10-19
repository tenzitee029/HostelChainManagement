using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Admin;

namespace QuanLyChuoiNhaTro.Views.Admin
{
    public partial class NhatKyHeThongView : UserControl
    {
        private DataTable? _logs;
        private bool _busy;

        public bool IsBusy => _busy;

        public NhatKyHeThongView()
        {
            InitializeComponent();
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
                {
                    txtStatus.Text = success;
                }
                else if (txtStatus.Text == "Đang xử lý...")
                {
                    txtStatus.Text = "";
                }
            }
            catch (SqlException ex)
            {
                txtStatus.Text = ex.Number == 50001
                    ? ex.Message
                    : "Không đọc được nhật ký. "
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
            object sender,
            RoutedEventArgs e)
        {
            await RunAsync(async () =>
            {
                var session = UserSession.CurrentUser;

                var logs =
                    await NhatKyHeThongService.GetLogsAsync();

                if (!IsLoaded
                    || !ReferenceEquals(
                        session,
                        UserSession.CurrentUser))
                {
                    return;
                }

                _logs = logs;
                ApplyLogFilter();
            });
        }

        private string SelectedLogType()
        {
            return (cboLogType.SelectedItem as ComboBoxItem)?
                .Content.ToString() ?? "Tất cả";
        }

        private static DataRowView[] FilterRows(
            DataTable table,
            string type)
        {
            return table.DefaultView
                .Cast<DataRowView>()
                .Where(row =>
                    type == "Tất cả"
                    || row["LoaiNhatKy"].ToString() == type)
                .ToArray();
        }

        private void ApplyLogFilter()
        {
            if (_logs == null
                || cboLogType == null
                || logsGrid == null
                || txtNoLogs == null)
            {
                return;
            }

            var rows = FilterRows(
                _logs,
                SelectedLogType());

            logsGrid.ItemsSource = rows;

            txtNoLogs.Visibility = rows.Length == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void LogFilter_Changed(
            object sender,
            SelectionChangedEventArgs e)
        {
            ApplyLogFilter();
        }

        private async Task ExportAsync(bool excel)
        {
            if (_busy)
                return;

            var dialog = new SaveFileDialog
            {
                Filter = excel
                    ? "Excel Workbook|*.xlsx"
                    : "Text file|*.txt",

                FileName =
                    "NhatKy_"
                    + DateTime.Now.ToString("yyyyMMdd_HHmmss"),

                AddExtension = true
            };

            var owner = Window.GetWindow(this);

            bool? accepted = owner != null
                ? dialog.ShowDialog(owner)
                : dialog.ShowDialog();

            if (accepted != true)
                return;

            // Xuất theo bộ lọc đang chọn.
            string type = SelectedLogType();

            await RunAsync(async () =>
            {
                var session = UserSession.CurrentUser;

                var logs =
                    await NhatKyHeThongService.GetLogsAsync();

                if (!IsLoaded
                    || !ReferenceEquals(
                        session,
                        UserSession.CurrentUser))
                {
                    txtStatus.Text =
                        "Đã hủy xuất vì giao diện hoặc phiên đăng nhập thay đổi.";

                    return;
                }

                _logs = logs;
                ApplyLogFilter();

                // Clone giữ đủ cột, kể cả khi không có dòng dữ liệu.
                var filtered = logs.Clone();

                foreach (var row in FilterRows(logs, type))
                {
                    filtered.ImportRow(row.Row);
                }

                string temporary = Path.Combine(
                    Path.GetDirectoryName(dialog.FileName)!,
                    Guid.NewGuid().ToString("N") + ".tmp");

                try
                {
                    NhatKyHeThongService.ExportLogs(
                        filtered,
                        temporary,
                        excel);

                    File.Move(
                        temporary,
                        dialog.FileName,
                        true);
                }
                finally
                {
                    if (File.Exists(temporary))
                    {
                        File.Delete(temporary);
                    }
                }

                txtStatus.Text =
                    "Đã xuất nhật ký: " + dialog.FileName;
            });
        }

        private async void ExportText_Click(
            object sender,
            RoutedEventArgs e)
        {
            await ExportAsync(false);
        }

        private async void ExportExcel_Click(
            object sender,
            RoutedEventArgs e)
        {
            await ExportAsync(true);
        }
    }
}
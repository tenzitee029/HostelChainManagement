using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
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
                txtStatus.Text = success ?? "";
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
            object sender, RoutedEventArgs e)
        {
            await RunAsync(async () =>
            {
                _logs = await NhatKyHeThongService.GetLogsAsync();

                if (IsLoaded)
                    ApplyLogFilter();
            });
        }

        private void ApplyLogFilter()
        {
            if (_logs == null || cboLogType == null)
                return;

            string type =
                (cboLogType.SelectedItem as ComboBoxItem)?
                    .Content.ToString() ?? "Tất cả";

            var rows = _logs.DefaultView.Cast<DataRowView>()
                .Where(row =>
                    type == "Tất cả"
                    || row["LoaiNhatKy"].ToString() == type)
                .ToList();

            logsGrid.ItemsSource = rows;

            txtNoLogs.Visibility = rows.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void LogFilter_Changed(
            object sender, SelectionChangedEventArgs e)
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

            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
                return;

            await RunAsync(async () =>
            {
                _logs = await NhatKyHeThongService.GetLogsAsync();
                ApplyLogFilter();

                var rows =
                    ((IEnumerable<DataRowView>)logsGrid.ItemsSource)
                    .Select(row => row.Row)
                    .ToArray();

                var filtered = rows.Length > 0
                    ? rows.CopyToDataTable()
                    : _logs.Clone();

                string temporary = Path.Combine(
                    Path.GetDirectoryName(dialog.FileName)!,
                    Guid.NewGuid().ToString("N") + ".tmp");

                try
                {
                    NhatKyHeThongService.ExportLogs(
                        filtered, temporary, excel);

                    File.Move(temporary, dialog.FileName, true);
                }
                finally
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
            }, "Đã xuất nhật ký: " + dialog.FileName);
        }

        private async void ExportText_Click(
            object sender, RoutedEventArgs e)
        {
            await ExportAsync(false);
        }

        private async void ExportExcel_Click(
            object sender, RoutedEventArgs e)
        {
            await ExportAsync(true);
        }
    }
}
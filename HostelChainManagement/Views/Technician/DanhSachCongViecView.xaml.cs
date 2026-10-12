using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Technician
{
    public partial class DanhSachCongViecView : UserControl
    {
        private DataTable? _jobs;
        private bool _loading;

        public bool IsBusy => _loading;

        public event Action<string>? WorkSelected;

        public DanhSachCongViecView()
        {
            InitializeComponent();
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }

        private async void Refresh_Click(
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
            btnDetail.IsEnabled = false;

            string? day = cboDay.SelectedItem as string;
            var session = UserSession.CurrentUser;

            txtStatus.Text = "Đang tải công việc...";

            try
            {
                var table = await KyThuatService.GetJobsAsync();

                if (!IsLoaded
                    || !ReferenceEquals(
                        session, UserSession.CurrentUser))
                {
                    return;
                }

                _jobs = table;

                if (!_jobs.Columns.Contains("TenNhanVien"))
                    _jobs.Columns.Add("TenNhanVien", typeof(string));

                foreach (DataRow row in _jobs.Rows)
                    row["TenNhanVien"] = session?.HoTen ?? "";

                cboDay.ItemsSource = new[] { "Chọn dãy trọ" }
                    .Concat(
                        _jobs.AsEnumerable()
                            .Select(
                                row => row.Field<string>("TenDayTro")!)
                            .Distinct())
                    .ToList();

                cboDay.SelectedItem =
                    day != null && cboDay.Items.Contains(day)
                        ? day
                        : "Chọn dãy trọ";

                ApplyFilter();
                txtStatus.Text = "";
            }
            catch (SqlException)
            {
                txtStatus.Text =
                    "Không tải được công việc. "
                    + "Vui lòng kiểm tra kết nối SQL Server "
                    + "và cấu trúc CSDL.";
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

        private void ApplyFilter()
        {
            if (_jobs == null
                || txtRoom == null
                || dpDate == null)
            {
                return;
            }

            string day =
                cboDay.SelectedItem as string ?? "Chọn dãy trọ";

            var rows = _jobs.DefaultView.Cast<DataRowView>()
                .Where(row =>
                    (day == "Chọn dãy trọ"
                        || row["TenDayTro"].ToString() == day)
                    && (row["SoPhong"].ToString() ?? "")
                        .Contains(
                            txtRoom.Text.Trim(),
                            StringComparison.OrdinalIgnoreCase)
                    && (!dpDate.SelectedDate.HasValue
                        || ((DateTime)row["NgayPhanCong"]).Date
                            == dpDate.SelectedDate.Value.Date))
                .ToList();

            jobsGrid.ItemsSource = rows;
            btnDetail.IsEnabled = false;

            txtEmpty.Visibility = rows.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            txtEmpty.Text = _jobs.Rows.Count == 0
                ? "Hiện tại bạn không có yêu cầu sửa chữa nào cần xử lý"
                : "Không có công việc phù hợp với bộ lọc";
        }

        private void Filter_Changed(
            object sender, SelectionChangedEventArgs e)
            => ApplyFilter();

        private void Room_Changed(
            object sender, TextChangedEventArgs e)
            => ApplyFilter();

        private void Date_Changed(
            object? sender, SelectionChangedEventArgs e)
            => ApplyFilter();

        private void Jobs_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            btnDetail.IsEnabled =
                jobsGrid.SelectedItem is DataRowView;
        }

        private void Detail_Click(
            object sender, RoutedEventArgs e)
        {
            OpenSelected();
        }

        private void Jobs_DoubleClick(
            object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source
                && ItemsControl.ContainerFromElement(
                    jobsGrid, source) is DataGridRow)
            {
                OpenSelected();
            }
        }

        private void OpenSelected()
        {
            if (!_loading
                && jobsGrid.SelectedItem is DataRowView row)
            {
                WorkSelected?.Invoke(
                    row["MaChiTietSuaChua"].ToString()!);
            }
        }
    }
}
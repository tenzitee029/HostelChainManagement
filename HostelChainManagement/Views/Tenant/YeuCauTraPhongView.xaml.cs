using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Tenant
{
    public partial class YeuCauTraPhongView : UserControl
    {
        private bool _busy;
        private bool _invalidDate;

        private DuLieuTraPhong? _data;

        public YeuCauTraPhongView()
        {
            InitializeComponent();
        }

        private async void View_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            if (_busy)
                return;

            SetBusy(true);

            try
            {
                await ReloadAsync();
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void Refresh_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_busy)
                return;

            SetBusy(true);

            try
            {
                await ReloadAsync();
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task ReloadAsync(string? success = null)
        {
            var session = UserSession.CurrentUser;

            string? selected =
                (cboHopDong.SelectedItem as DataRowView)?
                ["MaHopDong"] as string;

            _data = null;

            cboHopDong.ItemsSource = null;
            dgYeuCau.ItemsSource = null;

            contractPanel.DataContext = null;
            requestPanel.Visibility = Visibility.Collapsed;

            txtStatus.Text = success ?? "Đang tải dữ liệu...";

            try
            {
                var data = await YeuCauTraPhongService.GetAsync();

                if (!IsLoaded
                    || !ReferenceEquals(
                        session,
                        UserSession.CurrentUser))
                {
                    return;
                }

                _data = data;

                dpNgayTra.DisplayDateStart =
                    data.NgayHienTai.AddDays(1);

                // Gợi ý ngày sau 30 ngày; khách có thể đổi.
                dpNgayTra.SelectedDate =
                    data.NgayHienTai.AddDays(30);

                _invalidDate = false;

                cboHopDong.ItemsSource =
                    data.HopDong.DefaultView;

                cboHopDong.SelectedValuePath = "MaHopDong";

                if (selected != null)
                {
                    cboHopDong.SelectedValue = selected;
                }

                if (cboHopDong.SelectedIndex < 0
                    && data.HopDong.Rows.Count > 0)
                {
                    cboHopDong.SelectedIndex = 0;
                }

                dgYeuCau.ItemsSource =
                    data.YeuCau.DefaultView;

                txtEmpty.Visibility =
                    data.YeuCau.Rows.Count == 0
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                txtStatus.Text = success
                    ?? (data.HopDong.Rows.Count == 0
                        ? "Bạn chưa có hợp đồng đang thuê " +
                          "còn hiệu lực để gửi yêu cầu."
                        : "");
            }
            catch (Exception ex)
            {
                // Nếu thao tác ghi đã thành công nhưng tải lại lỗi,
                // vẫn giữ thông báo thành công để tránh gửi lại.
                string prefix = success == null
                    ? ""
                    : success
                      + "\nChưa tải lại được dữ liệu. ";

                txtStatus.Text = prefix
                    + (ex is SqlException
                        ? "Không tải được dữ liệu. " +
                          "Vui lòng kiểm tra SQL Server."
                        : ex.Message);
            }
        }

        private void Contract_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            contractPanel.DataContext =
                cboHopDong.SelectedItem;

            UpdateButtons();
        }

        private void Request_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            requestPanel.DataContext =
                dgYeuCau.SelectedItem;

            requestPanel.Visibility =
                dgYeuCau.SelectedItem is DataRowView
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            UpdateButtons();
        }

        private void SetBusy(bool value)
        {
            _busy = value;

            btnRefresh.IsEnabled = !value;

            UpdateButtons();
        }

        private void UpdateButtons()
        {
            // Có thể phát sinh sự kiện trong lúc InitializeComponent.
            if (btnSend == null || btnCancel == null)
                return;

            var contract =
                cboHopDong.SelectedItem as DataRowView;

            formPanel.IsEnabled =
                !_busy && contract != null;

            bool pending = false;

            if (_data != null && contract != null)
            {
                foreach (DataRow row in _data.YeuCau.Rows)
                {
                    bool sameContract =
                        (string)row["MaHopDong"]
                        == (string)contract["MaHopDong"];

                    bool activeRequest =
                        (string)row["TrangThai"] is
                            "Chờ xử lý" or "Đã duyệt";

                    if (sameContract && activeRequest)
                    {
                        pending = true;
                        break;
                    }
                }
            }

            txtPending.Text = pending
                ? "Hợp đồng đã có yêu cầu chờ xử lý hoặc đã duyệt. " +
                  "Chọn phiếu bên dưới để xem hoặc hủy nếu đủ điều kiện."
                : "";

            btnSend.IsEnabled =
                !_busy
                && contract != null
                && !pending;

            var request =
                dgYeuCau.SelectedItem as DataRowView;

            btnCancel.IsEnabled =
                !_busy
                && _data != null
                && request != null
                && (string)request["TrangThai"] == "Chờ xử lý"
                && (string)request["TrangThaiHopDong"] == "Hiệu lực"
                && request["NgayThanhLy"] == DBNull.Value
                && (DateTime)request["NgayDuKienTra"]
                    > _data.NgayHienTai;
        }

        private void Date_ValidationError(
            object sender,
            DatePickerDateValidationErrorEventArgs e)
        {
            e.ThrowException = false;

            _invalidDate = true;

            txtStatus.Text =
                "Ngày dự kiến trả phòng không hợp lệ. " +
                "Vui lòng chọn lại bằng lịch.";
        }

        private void Date_Changed(
            object sender,
            SelectionChangedEventArgs e)
        {
            _invalidDate = false;
        }

        private async void Send_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_busy
                || cboHopDong.SelectedItem
                    is not DataRowView contract)
            {
                return;
            }

            if (_invalidDate
                || dpNgayTra.SelectedDate is not DateTime date
                || _data == null
                || date <= _data.NgayHienTai)
            {
                txtStatus.Text =
                    "Vui lòng chọn ngày dự kiến trả phòng sau hôm nay.";

                return;
            }

            var answer = MessageBox.Show(
                Window.GetWindow(this),
                "Xác nhận gửi yêu cầu trả phòng?",
                "Xác nhận",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
                return;

            SetBusy(true);

            txtStatus.Text = "Đang gửi yêu cầu...";

            try
            {
                string id =
                    await YeuCauTraPhongService.GuiAsync(
                        (string)contract["MaHopDong"],
                        date,
                        txtLyDo.Text,
                        txtHoanCoc.Text);

                if (!IsLoaded)
                    return;

                txtLyDo.Clear();
                txtHoanCoc.Clear();

                await ReloadAsync(
                    "Yêu cầu trả phòng của bạn đã được gửi. " +
                    "Nhân viên quản lý sẽ liên hệ kiểm tra phòng " +
                    "trước ngày bàn giao." +
                    "\nMã yêu cầu: " + id);
            }
            catch (SqlException)
            {
                txtStatus.Text =
                    "Không gửi được yêu cầu. " +
                    "Vui lòng kiểm tra kết nối SQL Server.";
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

        private async void Cancel_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_busy
                || dgYeuCau.SelectedItem is not DataRowView row)
            {
                return;
            }

            var answer = MessageBox.Show(
                Window.GetWindow(this),
                "Bạn có chắc muốn hủy yêu cầu trả phòng đã chọn?",
                "Xác nhận",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
                return;

            SetBusy(true);

            txtStatus.Text = "Đang hủy yêu cầu...";

            try
            {
                await YeuCauTraPhongService.HuyAsync(
                    (string)row["MaYeuCau"]);

                if (!IsLoaded)
                    return;

                await ReloadAsync(
                    "Đã hủy yêu cầu trả phòng. " +
                    "Hợp đồng tiếp tục giữ trạng thái hiện tại.");
            }
            catch (SqlException)
            {
                txtStatus.Text =
                    "Không hủy được yêu cầu. " +
                    "Vui lòng kiểm tra kết nối SQL Server.";
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
    }
}
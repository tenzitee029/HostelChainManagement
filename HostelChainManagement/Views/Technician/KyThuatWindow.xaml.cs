using System.Data;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using QuanLyChuoiNhaTro.Models;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Views.Account;
using QuanLyChuoiNhaTro.Views.Auth;

namespace QuanLyChuoiNhaTro.Views.Technician;

public partial class KyThuatWindow : Window
{
    private DataTable? _jobs;
    private DataRowView? _selected;

    private bool _busy;

    private byte[]? _photoBytes;
    private string? _photoExtension;

    private static readonly string PhotoFolder =
        Environment.GetEnvironmentVariable(
            "QUANLYNHATRO_PHOTO_DIR")
        ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "QuanLyChuoiNhaTro",
            "AnhNghiemThu");

    public KyThuatWindow()
    {
        InitializeComponent();

        var user = UserSession.CurrentUser;

        if (user == null
            || user.VaiTro != "Nhân viên kỹ thuật")
        {
            throw new InvalidOperationException(
                "Bạn không có quyền mở màn hình kỹ thuật.");
        }

        txtUser.Text = user.HoTen;
    }

    private string Value(string name)
    {
        return _selected?[name]?.ToString() ?? "";
    }

    private string Display(string name)
    {
        string value = Value(name);

        return string.IsNullOrWhiteSpace(value)
            ? "—"
            : value;
    }

    private bool Editable =>
        _selected != null
        && (Value("TrangThai")
            is "Được phân công" or "Đang xử lý")
        && (Value("TrangThaiSuCo")
            is "Đã phân công" or "Đang xử lý");

    private async Task RunAsync(
        Func<Task> action,
        string? success = null)
    {
        if (_busy)
            return;

        _busy = true;

        contentPanel.IsEnabled =
            navPanel.IsEnabled =
            btnNotifications.IsEnabled = false;

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
                : "Không truy cập được CSDL. "
                    + "Kiểm tra SQL Server và cấu hình kết nối.";
        }
        catch (Exception ex)
        {
            txtStatus.Text = ex.Message;
        }
        finally
        {
            _busy = false;

            contentPanel.IsEnabled =
                navPanel.IsEnabled =
                btnNotifications.IsEnabled = true;
        }
    }

    private void ShowPage(
        UIElement page,
        string breadcrumb,
        Button? menu = null)
    {
        foreach (var panel in new UIElement[]
        {
            listPanel,
            detailPanel,
            completePanel,
            notificationPanel,
            profilePanel,
            passwordPanel
        })
        {
            panel.Visibility = Visibility.Collapsed;
        }

        page.Visibility = Visibility.Visible;

        txtBreadcrumb.Text =
            "Hệ thống / " + breadcrumb;

        foreach (var button in new[]
        {
            btnJobs,
            btnProfile,
            btnPassword
        })
        {
            button.Background = Brushes.Transparent;

            button.Foreground = new SolidColorBrush(
                Color.FromRgb(203, 213, 225));
        }

        if (menu != null)
        {
            menu.Background = new SolidColorBrush(
                Color.FromRgb(37, 99, 235));

            menu.Foreground = Brushes.White;
        }

        txtStatus.Text = "";
    }

    private async void Window_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await RunAsync(LoadJobsAsync);
    }

    private async Task LoadJobsAsync()
    {
        string? day =
            cboDay.SelectedItem as string;

        _jobs =
            await KyThuatService.GetJobsAsync();

        // Bổ sung cột hiển thị tên kỹ thuật viên.
        // Danh sách được service giới hạn cho người đang đăng nhập.
        _jobs.Columns.Add(
            "TenNhanVien",
            typeof(string));

        foreach (DataRow row in _jobs.Rows)
        {
            row["TenNhanVien"] =
                UserSession.CurrentUser?.HoTen ?? "";
        }

        cboDay.ItemsSource =
            new[] { "Chọn dãy trọ" }
                .Concat(
                    _jobs.AsEnumerable()
                        .Select(
                            row => row.Field<string>(
                                "TenDayTro")!)
                        .Distinct())
                .ToList();

        cboDay.SelectedItem =
            day != null && cboDay.Items.Contains(day)
                ? day
                : "Chọn dãy trọ";

        ApplyFilter();
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
            cboDay.SelectedItem as string
            ?? "Chọn dãy trọ";

        var rows = _jobs.DefaultView
            .Cast<DataRowView>()
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

        txtEmpty.Visibility =
            rows.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

        txtEmpty.Text = _jobs.Rows.Count == 0
            ? "Hiện tại bạn không có yêu cầu sửa chữa nào cần xử lý"
            : "Không có công việc phù hợp với bộ lọc";
    }

    private void Filter_Changed(
        object sender,
        SelectionChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void Room_Changed(
        object sender,
        TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void Date_Changed(
        object? sender,
        SelectionChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void Jobs_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!_busy
            && jobsGrid.SelectedItem is DataRowView row)
        {
            ShowDetail(row);
        }
    }

    private void Jobs_DoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!_busy
            && jobsGrid.SelectedItem is DataRowView row)
        {
            ShowDetail(row);
        }
    }

    private void ShowDetail(DataRowView row)
    {
        _selected = row;

        txtRequestId.Text = Display("MaSuCo");
        txtDetailRoom.Text = Display("SoPhong");

        // CSDL hiện tại chưa có cột vị trí hư hỏng riêng.
        txtLocation.Text = "—";

        txtPriority.Text = Display("MucDoUuTien");
        txtProgress.Text = Display("TienDo");
        txtJobStatus.Text = Display("TrangThai");
        txtPhone.Text = Display("SoDienThoai");

        txtTechnician.Text =
            UserSession.CurrentUser?.HoTen ?? "—";

        txtDescription.Text =
            Display("NoiDungSuCo");

        txtNote.Text =
            Value("NoiDungXuLy");

        cboProgress.SelectedItem =
            cboProgress.Items
                .Cast<ComboBoxItem>()
                .FirstOrDefault(
                    item =>
                        item.Content.ToString()
                        == Value("TienDo"))
            ?? cboProgress.Items[0];

        progressEditor.IsEnabled =
            btnSaveProgress.IsEnabled = Editable;

        imgAttachment.Source =
            LoadImage(Value("TepDinhKem"));

        bool hasAttachment =
            !string.IsNullOrWhiteSpace(
                Value("TepDinhKem"));

        btnAttachment.Content =
            !hasAttachment
                ? "—"
                : imgAttachment.Source == null
                    ? "Mở ảnh / video đính kèm"
                    : "Mở ảnh";

        btnAttachment.IsEnabled = hasAttachment;

        ShowPage(
            detailPanel,
            "Danh sách công việc / Chi tiết công việc",
            btnJobs);
    }

    private static BitmapImage? LoadImage(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var image = new BitmapImage();

            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 900;

            image.UriSource = new Uri(
                Path.GetFullPath(path));

            image.EndInit();
            image.Freeze();

            return image;
        }
        catch
        {
            // Tệp video hoặc đường dẫn không đọc được:
            // vẫn có thể mở bằng nút đính kèm.
            return null;
        }
    }

    private async Task ReloadSelectedAsync(string id)
    {
        await LoadJobsAsync();

        var row = _jobs!.DefaultView
            .Cast<DataRowView>()
            .FirstOrDefault(
                item =>
                    item["MaChiTietSuaChua"].ToString()
                    == id);

        if (row != null)
        {
            ShowDetail(row);
        }
        else
        {
            _selected = null;

            ShowPage(
                listPanel,
                "Danh sách công việc",
                btnJobs);
        }
    }

    private async void Jobs_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_busy)
            return;

        _selected = null;

        ShowPage(
            listPanel,
            "Danh sách công việc",
            btnJobs);

        await RunAsync(LoadJobsAsync);
    }

    private async void SaveProgress_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!Editable || _busy)
            return;

        string id = Value("MaChiTietSuaChua");
        string status = Value("TrangThai");
        string oldProgress = Value("TienDo");

        long version = Convert.ToInt64(
            _selected!["HistoryVersion"]);

        string progress =
            (cboProgress.SelectedItem as ComboBoxItem)?
                .Content.ToString()
            ?? "";

        string note = txtNote.Text;

        await RunAsync(
            async () =>
            {
                await KyThuatService.SaveAsync(
                    id,
                    status,
                    oldProgress,
                    version,
                    progress,
                    note,
                    null,
                    Array.Empty<VatTuKyThuat>());

                await ReloadSelectedAsync(id);
            },
            "Đã lưu tiến độ và gửi thông báo "
                + "cho khách thuê, quản lý.");
    }

    private async void GoComplete_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_selected == null || _busy)
            return;

        string id = Value("MaChiTietSuaChua");

        await RunAsync(async () =>
        {
            var materials =
                await KyThuatService.GetMaterialsAsync(id);

            txtCompleteId.Text =
                Display("MaSuCo");

            txtCompleteRoom.Text =
                Display("SoPhong");

            txtResolution.Text =
                Value("NoiDungXuLy");

            txtMaterialName.Clear();
            txtQuantity.Text = "1";
            txtCost.Text = "0";

            if (materials.Rows.Count == 1)
            {
                var material = materials.Rows[0];

                txtMaterialName.Text =
                    material["TenVatTu"].ToString();

                txtQuantity.Text =
                    Convert.ToDecimal(material["SoLuong"])
                        .ToString(CultureInfo.CurrentCulture);

                txtCost.Text =
                    Convert.ToDecimal(material["DonGia"])
                        .ToString(CultureInfo.CurrentCulture);
            }
            else if (materials.Rows.Count > 1)
            {
                // Hiển thị dữ liệu nghiệm thu cũ có nhiều vật tư.
                txtMaterialName.Text = string.Join(
                    "; ",
                    materials.AsEnumerable()
                        .Select(
                            row => row.Field<string>("TenVatTu")));

                txtQuantity.Text = "1";

                txtCost.Text =
                    Convert.ToDecimal(
                        _selected!["ChiPhiVatTu"])
                    .ToString(CultureInfo.CurrentCulture);
            }

            _photoBytes = null;
            _photoExtension = null;

            txtPhoto.Text =
                string.IsNullOrWhiteSpace(Value("AnhSauSua"))
                    ? "Chưa chọn tệp"
                    : Path.GetFileName(Value("AnhSauSua"));

            completionEditor.IsEnabled =
                btnComplete.IsEnabled = Editable;

            ShowPage(
                completePanel,
                "Danh sách công việc / Xác nhận hoàn thành sửa chữa",
                btnJobs);
        });
    }

    private void ChoosePhoto_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn ảnh nghiệm thu",
            Filter = "Ảnh JPG/PNG|*.jpg;*.jpeg;*.png"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            if (new FileInfo(dialog.FileName).Length
                > 10 * 1024 * 1024)
            {
                throw new InvalidOperationException(
                    "Ảnh nghiệm thu tối đa 10 MB.");
            }

            byte[] bytes =
                File.ReadAllBytes(dialog.FileName);

            using var stream = new MemoryStream(bytes);

            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            if (decoder is not (
                PngBitmapDecoder or JpegBitmapDecoder))
            {
                throw new InvalidOperationException(
                    "Vui lòng chọn ảnh JPG hoặc PNG hợp lệ.");
            }

            _photoBytes = bytes;

            _photoExtension =
                decoder is PngBitmapDecoder
                    ? ".png"
                    : ".jpg";

            txtPhoto.Text =
                Path.GetFileName(dialog.FileName);
        }
        catch (Exception ex)
        {
            txtStatus.Text = ex.Message;
        }
    }

    private async void Complete_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!Editable || _busy)
            return;

        if (string.IsNullOrWhiteSpace(txtResolution.Text)
            || _photoBytes == null)
        {
            txtStatus.Text =
                "Vui lòng nhập ghi chú cách khắc phục "
                + "và chọn ảnh nghiệm thu.";
            return;
        }

        var materials = new List<VatTuKyThuat>();

        if (!decimal.TryParse(
                txtQuantity.Text,
                out decimal quantity)
            || !decimal.TryParse(
                txtCost.Text,
                out decimal cost))
        {
            txtStatus.Text =
                "Số lượng và chi phí phải là số hợp lệ.";
            return;
        }

        if (!string.IsNullOrWhiteSpace(txtMaterialName.Text))
        {
            materials.Add(new VatTuKyThuat
            {
                TenVatTu = txtMaterialName.Text.Trim(),
                SoLuong = quantity,
                DonGia = cost
            });
        }
        else if (cost != 0)
        {
            txtStatus.Text =
                "Vui lòng nhập tên linh kiện "
                + "khi có chi phí vật tư.";
            return;
        }

        var answer = MessageBox.Show(
            this,
            "Xác nhận hoàn tất công việc sửa chữa?",
            "Xác nhận",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
            return;

        string id = Value("MaChiTietSuaChua");
        string status = Value("TrangThai");
        string oldProgress = Value("TienDo");

        long version = Convert.ToInt64(
            _selected!["HistoryVersion"]);

        string note = txtResolution.Text;
        byte[] photo = _photoBytes;
        string extension = _photoExtension!;

        await RunAsync(
            async () =>
            {
                Directory.CreateDirectory(PhotoFolder);

                string saved = Path.Combine(
                    PhotoFolder,
                    Guid.NewGuid().ToString("N") + extension);

                File.WriteAllBytes(saved, photo);

                try
                {
                    await KyThuatService.SaveAsync(
                        id,
                        status,
                        oldProgress,
                        version,
                        "Hoàn thành",
                        note,
                        saved,
                        materials);
                }
                catch
                {
                    File.Delete(saved);
                    throw;
                }

                _photoBytes = null;

                await ReloadSelectedAsync(id);
            },
            "Đã xác nhận hoàn tất và gửi thông báo "
                + "cho khách thuê, quản lý.");
    }

    private void OpenAttachment_Click(
        object sender,
        RoutedEventArgs e)
    {
        string path = Value("TepDinhKem");

        try
        {
            bool web =
                Uri.TryCreate(
                    path,
                    UriKind.Absolute,
                    out var uri)
                && uri.Scheme is "http" or "https";

            bool media =
                File.Exists(path)
                && new[]
                {
                    ".jpg",
                    ".jpeg",
                    ".png",
                    ".gif",
                    ".bmp",
                    ".mp4",
                    ".webm",
                    ".mov"
                }.Contains(
                    Path.GetExtension(path)
                        .ToLowerInvariant());

            if (!web && !media)
            {
                throw new InvalidOperationException(
                    "Không tìm thấy ảnh/video đính kèm "
                    + "hoặc loại tệp không được hỗ trợ.");
            }

            Process.Start(new ProcessStartInfo(
                web ? path : Path.GetFullPath(path))
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            txtStatus.Text = ex.Message;
        }
    }

    private async void Notifications_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_busy)
            return;

        ShowPage(
            notificationPanel,
            "Thông báo");

        await RunAsync(async () =>
        {
            var notifications =
                await KyThuatService.GetNotificationsAsync();

            notificationsGrid.ItemsSource =
                notifications.DefaultView;

            txtNotification.Clear();
        });
    }

    private async void Notification_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (notificationsGrid.SelectedItem
            is not DataRowView row)
        {
            return;
        }

        txtNotification.Text =
            row["TieuDe"] + "\n\n" + row["NoiDung"];

        if ((bool)row["DaDoc"])
            return;

        try
        {
            await KyThuatService.MarkReadAsync(
                row["MaThongBao"].ToString()!);

            row["DaDoc"] = true;
        }
        catch (Exception ex)
        {
            txtStatus.Text =
                "Không đánh dấu đã đọc: " + ex.Message;
        }
    }

    private void Profile_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_busy)
            return;

        var view = new HoSoView();

        view.ProfileUpdated += (_, _) =>
        {
            txtUser.Text =
                UserSession.CurrentUser?.HoTen ?? "";
        };

        profilePanel.Content = view;

        ShowPage(
            profilePanel,
            "Hồ sơ cá nhân",
            btnProfile);
    }

    private void Password_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!_busy)
        {
            ShowPage(
                passwordPanel,
                "Đổi mật khẩu",
                btnPassword);
        }
    }

    private async void ChangePassword_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (newPassword.Password
            != confirmPassword.Password)
        {
            txtStatus.Text =
                "Hai mật khẩu mới không trùng khớp.";
            return;
        }

        string old = oldPassword.Password;
        string next = newPassword.Password;

        await RunAsync(
            async () =>
            {
                await KyThuatService.ChangePasswordAsync(
                    old,
                    next);

                oldPassword.Clear();
                newPassword.Clear();
                confirmPassword.Clear();
            },
            "Đổi mật khẩu thành công.");
    }

    private void Logout_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_busy)
            return;

        var answer = MessageBox.Show(
            this,
            "Bạn có chắc chắn muốn đăng xuất?",
            "Xác nhận",
            MessageBoxButton.YesNo);

        if (answer != MessageBoxResult.Yes)
            return;

        UserSession.SignOut();

        var login = new LoginWindow();

        Application.Current.MainWindow = login;
        login.Show();

        Close();
    }
}
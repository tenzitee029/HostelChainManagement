using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Models;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Technician
{
    public partial class ChiTietCongViecView : UserControl
    {
        private readonly string _id;
        private DataRowView? _selected;
        private bool _busy;

        public bool IsBusy => _busy;

        public event Action? ListRequested;
        public event Action<string>? CompleteRequested;

        public ChiTietCongViecView(string id)
        {
            InitializeComponent();
            _id = id;
        }

        private string Value(string name)
            => _selected?[name]?.ToString() ?? "";

        private string Display(string name)
            => string.IsNullOrWhiteSpace(Value(name))
                ? "—" : Value(name);

        private bool Editable =>
            _selected != null
            && (Value("TrangThai")
                is "Được phân công" or "Đang xử lý")
            && (Value("TrangThaiSuCo")
                is "Đã phân công" or "Đang xử lý");

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
                    : "Không thực hiện được thao tác. "
                        + "Vui lòng kiểm tra kết nối SQL Server.";
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
            await RunAsync(LoadJobAsync);
        }

        private async Task LoadJobAsync()
        {
            var table = await KyThuatService.GetJobsAsync();

            if (!IsLoaded)
                return;

            _selected = table.DefaultView.Cast<DataRowView>()
                .FirstOrDefault(
                    row => row["MaChiTietSuaChua"].ToString() == _id)
                ?? throw new InvalidOperationException(
                    "Công việc không còn được phân công cho bạn.");

            DisplayJob();
        }

        private void DisplayJob()
        {
            txtRequestId.Text = Display("MaSuCo");
            txtDetailRoom.Text = Display("SoPhong");

            // CSDL hiện tại không có cột vị trí riêng.
            txtLocation.Text = "—";

            txtPriority.Text = Display("MucDoUuTien");
            txtProgress.Text = Display("TienDo");
            txtJobStatus.Text = Display("TrangThai");
            txtPhone.Text = Display("SoDienThoai");

            txtTechnician.Text =
                UserSession.CurrentUser?.HoTen ?? "—";

            txtDescription.Text = Display("NoiDungSuCo");
            txtNote.Text = Value("NoiDungXuLy");

            cboProgress.SelectedItem = cboProgress.Items
                .Cast<ComboBoxItem>()
                .FirstOrDefault(
                    item => item.Content.ToString() == Value("TienDo"))
                ?? cboProgress.Items[0];

            progressEditor.IsEnabled =
                btnSaveProgress.IsEnabled = Editable;

            imgAttachment.Source =
                LoadImage(Value("TepDinhKem"));

            bool hasAttachment =
                !string.IsNullOrWhiteSpace(Value("TepDinhKem"));

            btnAttachment.IsEnabled = hasAttachment;

            btnAttachment.Content = !hasAttachment
                ? "—"
                : imgAttachment.Source == null
                    ? "Mở ảnh / video đính kèm"
                    : "Mở ảnh";
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
                image.UriSource = new Uri(Path.GetFullPath(path));
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private async void SaveProgress_Click(
            object sender, RoutedEventArgs e)
        {
            if (!Editable || _busy)
                return;

            string id = Value("MaChiTietSuaChua");
            string status = Value("TrangThai");
            string previous = Value("TienDo");

            long version =
                Convert.ToInt64(_selected!["HistoryVersion"]);

            string progress =
                (cboProgress.SelectedItem as ComboBoxItem)?
                    .Content.ToString() ?? "";

            string note = txtNote.Text;

            await RunAsync(async () =>
            {
                await KyThuatService.SaveAsync(
                    id, status, previous, version,
                    progress, note, null,
                    Array.Empty<VatTuKyThuat>());

                await LoadJobAsync();
            }, "Đã lưu tiến độ và gửi thông báo cho khách thuê, quản lý.");
        }

        private void Jobs_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_busy)
                ListRequested?.Invoke();
        }

        private void GoComplete_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_busy && _selected != null)
                CompleteRequested?.Invoke(_id);
        }

        private void OpenAttachment_Click(
            object sender, RoutedEventArgs e)
        {
            string path = Value("TepDinhKem");

            try
            {
                bool web =
                    Uri.TryCreate(
                        path, UriKind.Absolute, out var uri)
                    && uri.Scheme is "http" or "https";

                bool media =
                    File.Exists(path)
                    && new[]
                    {
                        ".jpg", ".jpeg", ".png", ".gif",
                        ".bmp", ".mp4", ".webm", ".mov"
                    }.Contains(
                        Path.GetExtension(path).ToLowerInvariant());

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
    }
}
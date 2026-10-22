using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using QuanLyChuoiNhaTro.Models;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Technician;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace QuanLyChuoiNhaTro.Views.Technician
{
    public partial class XacNhanHoanThanhView : UserControl
    {
        private const decimal MaxMoney =
            999999999999999999m;

        private readonly string _id;

        private readonly ObservableCollection<VatTuKyThuat>
            _materials = new();

        private DataRowView? _selected;

        private byte[]? _photoBytes;
        private string? _photoExtension;

        private bool _busy;
        private bool _loaded;

        public bool IsBusy => _busy;

        public event Action<string>? Completed;

        private static readonly string PhotoFolder =
            Environment.GetEnvironmentVariable(
                "QUANLYNHATRO_PHOTO_DIR")
            ?? Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "QuanLyChuoiNhaTro",
                "AnhNghiemThu");

        public XacNhanHoanThanhView(string id)
        {
            InitializeComponent();

            _id = id;

            dgMaterials.ItemsSource = _materials;

            UpdateTotal();
        }

        private string Value(string name)
        {
            return _selected?[name]?.ToString() ?? "";
        }

        private bool Editable =>
            _selected != null
            && Value("TrangThai")
                is "Được phân công" or "Đang xử lý"
            && Value("TrangThaiSuCo")
                is "Đã phân công" or "Đang xử lý";

        private void SetBusy(bool busy)
        {
            _busy = busy;

            editorPanel.IsEnabled = !busy;

            completionEditor.IsEnabled =
                !busy && Editable;

            btnComplete.IsEnabled =
                !busy && Editable;
        }

        private void UpdateTotal()
        {
            decimal total = _materials.Sum(x => x.ThanhTien);

            txtTotalMaterials.Text =
                "Tổng tiền linh kiện: "
                + total.ToString(
                    "N0",
                    CultureInfo.GetCultureInfo("vi-VN"))
                + " VNĐ";
        }

        private async Task RunAsync(Func<Task> action)
        {
            if (_busy)
                return;

            SetBusy(true);
            txtStatus.Text = "Đang xử lý...";

            try
            {
                await action();
            }
            catch (SqlException ex)
            {
                if (IsLoaded)
                {
                    txtStatus.Text = ex.Number == 50001
                        ? ex.Message
                        : "Không thực hiện được thao tác. "
                          + "Vui lòng kiểm tra SQL Server.";
                }
            }
            catch (Exception ex)
            {
                if (IsLoaded)
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
            if (_loaded)
                return;

            var session = UserSession.CurrentUser;

            await RunAsync(async () =>
            {
                var table =
                    await XacNhanHoanThanhService.GetJobsAsync();

                if (!IsLoaded
                    || !ReferenceEquals(
                        session,
                        UserSession.CurrentUser))
                {
                    return;
                }

                _selected = table.DefaultView
                    .Cast<DataRowView>()
                    .FirstOrDefault(
                        row => Convert.ToString(
                            row["MaChiTietSuaChua"]) == _id)
                    ?? throw new InvalidOperationException(
                        "Công việc không còn được phân công cho bạn.");

                var materials =
                    await XacNhanHoanThanhService.GetMaterialsAsync(_id);

                if (!IsLoaded
                    || !ReferenceEquals(
                        session,
                        UserSession.CurrentUser))
                {
                    return;
                }

                txtCompleteId.Text = Value("MaSuCo");
                txtCompleteRoom.Text = Value("SoPhong");
                txtResolution.Text = Value("NoiDungXuLy");

                _materials.Clear();

                foreach (DataRow row in materials.Rows)
                {
                    _materials.Add(new VatTuKyThuat
                    {
                        TenVatTu =
                            Convert.ToString(row["TenVatTu"]) ?? "",

                        SoLuong =
                            Convert.ToDecimal(row["SoLuong"]),

                        DonGia =
                            Convert.ToDecimal(row["DonGia"])
                    });
                }

                txtMaterialName.Clear();
                txtQuantity.Text = "1";
                txtCost.Text = "0";

                _photoBytes = null;
                _photoExtension = null;

                txtPhoto.Text =
                    string.IsNullOrWhiteSpace(Value("AnhSauSua"))
                        ? "Chưa chọn tệp"
                        : Path.GetFileName(Value("AnhSauSua"));

                UpdateTotal();

                _loaded = true;
                txtStatus.Text = "";
            });
        }

        private void AddMaterial_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_busy || !Editable)
                return;

            string name = txtMaterialName.Text.Trim();

            if (name.Length == 0 || name.Length > 100)
            {
                txtStatus.Text =
                    "Nhập tên linh kiện từ 1 đến 100 ký tự.";
                return;
            }

            string quantityText =
                txtQuantity.Text.Trim().Replace(',', '.');

            if (!decimal.TryParse(
                    quantityText,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out decimal quantity)
                || quantity <= 0
                || quantity > 99999999.99m
                || decimal.Round(quantity, 2) != quantity)
            {
                txtStatus.Text =
                    "Số lượng phải lớn hơn 0, tối đa 2 số lẻ.";
                return;
            }

            if (!decimal.TryParse(
                    txtCost.Text.Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out decimal price)
                || price < 0
                || price > MaxMoney)
            {
                txtStatus.Text =
                    "Đơn giá phải là số nguyên không âm. "
                    + "Ví dụ: 50000.";
                return;
            }

            var material = new VatTuKyThuat
            {
                TenVatTu = name,
                SoLuong = quantity,
                DonGia = price
            };

            decimal currentTotal =
                _materials.Sum(x => x.ThanhTien);

            if (material.ThanhTien > MaxMoney
                || currentTotal + material.ThanhTien > MaxMoney)
            {
                txtStatus.Text =
                    "Tổng tiền linh kiện vượt giới hạn database.";
                return;
            }

            _materials.Add(material);

            UpdateTotal();

            txtMaterialName.Clear();
            txtQuantity.Text = "1";
            txtCost.Text = "0";

            txtStatus.Text =
                "Đã thêm linh kiện vào danh sách.";

            txtMaterialName.Focus();
        }

        private void RemoveMaterial_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_busy || !Editable)
                return;

            if (dgMaterials.SelectedItem is not VatTuKyThuat item)
            {
                txtStatus.Text = "Chọn dòng linh kiện cần xóa.";
                return;
            }

            _materials.Remove(item);

            UpdateTotal();

            txtStatus.Text = "Đã xóa dòng linh kiện.";
        }

        private void ChoosePhoto_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_busy || !Editable)
                return;

            var dialog = new OpenFileDialog
            {
                Title = "Chọn ảnh nghiệm thu",
                Filter = "Ảnh JPG/PNG|*.jpg;*.jpeg;*.png"
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
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
            if (_busy || !Editable || _selected == null)
                return;

            // Tránh bỏ sót linh kiện đang nhập nhưng chưa thêm.
            if (!string.IsNullOrWhiteSpace(txtMaterialName.Text)
                || txtQuantity.Text.Trim() != "1"
                || txtCost.Text.Trim() != "0")
            {
                txtStatus.Text =
                    "Bạn còn dòng linh kiện đang nhập. "
                    + "Hãy bấm Thêm linh kiện hoặc xóa nội dung dòng nhập.";

                return;
            }

            // Cách khắc phục vẫn bắt buộc.
            if (string.IsNullOrWhiteSpace(txtResolution.Text))
            {
                txtStatus.Text = "Vui lòng nhập cách khắc phục.";
                return;
            }

            byte[]? photo = _photoBytes;
            string? extension = _photoExtension;

            // Chỉ kiểm tra ảnh khi người dùng đã chọn ảnh.
            if (photo != null && string.IsNullOrWhiteSpace(extension))
            {
                txtStatus.Text =
                    "Ảnh đã chọn không hợp lệ. Vui lòng chọn lại.";

                return;
            }

            decimal total = _materials.Sum(x => x.ThanhTien);

            if (MessageBox.Show(
                Window.GetWindow(this),
                "Xác nhận hoàn thành công việc?\n"
                + $"Số dòng linh kiện: {_materials.Count}\n"
                + $"Tổng tiền: {total:N0} VNĐ\n\n"
                + "Chi phí sẽ được đưa vào hóa đơn của khách thuê.",
                "Xác nhận",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question)
                != MessageBoxResult.Yes)
            {
                return;
            }

            string id = Value("MaChiTietSuaChua");
            string status = Value("TrangThai");
            string previous = Value("TienDo");

            long version =
                Convert.ToInt64(_selected["HistoryVersion"]);

            string note = txtResolution.Text.Trim();

            var materials = _materials.ToList();
            var session = UserSession.CurrentUser;

            await RunAsync(async () =>
            {
                // Giữ ảnh hiện có nếu không chọn ảnh mới.
                string? photoPath =
                    string.IsNullOrWhiteSpace(Value("AnhSauSua"))
                        ? null
                        : Value("AnhSauSua");

                string? saved = null;

                try
                {
                    // Không chọn ảnh thì bỏ qua việc tạo thư mục,
                    // ghi tệp và vẫn lưu kết quả hoàn thành.
                    if (photo != null)
                    {
                        Directory.CreateDirectory(PhotoFolder);

                        saved = Path.Combine(
                            PhotoFolder,
                            Guid.NewGuid().ToString("N") + extension);

                        await File.WriteAllBytesAsync(saved, photo);

                        photoPath = saved;
                    }

                    await XacNhanHoanThanhService.SaveAsync(
                        id,
                        status,
                        previous,
                        version,
                        "Hoàn thành",
                        note,
                        photoPath,
                        materials);
                }
                catch
                {
                    // Chỉ dọn ảnh mới tạo nếu lưu thất bại.
                    // Không xóa ảnh cũ của công việc.
                    if (saved != null)
                    {
                        try
                        {
                            File.Delete(saved);
                        }
                        catch
                        {
                            // Giữ lại lỗi lưu ban đầu.
                        }
                    }

                    throw;
                }

                _photoBytes = null;
                _photoExtension = null;

                if (IsLoaded
                    && ReferenceEquals(
                        session,
                        UserSession.CurrentUser))
                {
                    txtStatus.Text =
                        "Đã hoàn thành và lưu danh sách linh kiện.";

                    Completed?.Invoke(id);
                }
            });
        }
    }
}
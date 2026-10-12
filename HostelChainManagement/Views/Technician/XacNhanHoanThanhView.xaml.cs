using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using QuanLyChuoiNhaTro.Models;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Technician
{
    public partial class XacNhanHoanThanhView : UserControl
    {
        private readonly string _id;
        private DataRowView? _selected;
        private byte[]? _photoBytes;
        private string? _photoExtension;
        private bool _busy;

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
        }

        private string Value(string name)
            => _selected?[name]?.ToString() ?? "";

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
                var table = await KyThuatService.GetJobsAsync();

                if (!IsLoaded)
                    return;

                _selected = table.DefaultView.Cast<DataRowView>()
                    .FirstOrDefault(
                        row => row["MaChiTietSuaChua"].ToString() == _id)
                    ?? throw new InvalidOperationException(
                        "Công việc không còn được phân công cho bạn.");

                var materials =
                    await KyThuatService.GetMaterialsAsync(_id);

                if (!IsLoaded)
                    return;

                txtCompleteId.Text = Value("MaSuCo");
                txtCompleteRoom.Text = Value("SoPhong");
                txtResolution.Text = Value("NoiDungXuLy");

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
                    txtMaterialName.Text = string.Join(
                        "; ",
                        materials.AsEnumerable().Select(
                            row => row.Field<string>("TenVatTu")));

                    txtQuantity.Text = "1";

                    txtCost.Text =
                        Convert.ToDecimal(_selected["ChiPhiVatTu"])
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
            });
        }

        private void ChoosePhoto_Click(
            object sender, RoutedEventArgs e)
        {
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

                byte[] bytes = File.ReadAllBytes(dialog.FileName);

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
                    decoder is PngBitmapDecoder ? ".png" : ".jpg";

                txtPhoto.Text =
                    Path.GetFileName(dialog.FileName);
            }
            catch (Exception ex)
            {
                txtStatus.Text = ex.Message;
            }
        }

        private async void Complete_Click(
            object sender, RoutedEventArgs e)
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

            if (!decimal.TryParse(
                    txtQuantity.Text, out decimal quantity)
                || !decimal.TryParse(
                    txtCost.Text, out decimal cost))
            {
                txtStatus.Text =
                    "Số lượng và chi phí phải là số hợp lệ.";
                return;
            }

            var materials = new List<VatTuKyThuat>();

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
                    "Vui lòng nhập tên linh kiện khi có chi phí vật tư.";
                return;
            }

            if (MessageBox.Show(
                Window.GetWindow(this),
                "Xác nhận hoàn tất công việc sửa chữa?",
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
                Convert.ToInt64(_selected!["HistoryVersion"]);

            string note = txtResolution.Text;
            byte[] photo = _photoBytes;
            string extension = _photoExtension!;

            await RunAsync(async () =>
            {
                Directory.CreateDirectory(PhotoFolder);

                string saved = Path.Combine(
                    PhotoFolder,
                    Guid.NewGuid().ToString("N") + extension);

                File.WriteAllBytes(saved, photo);

                try
                {
                    await KyThuatService.SaveAsync(
                        id, status, previous, version,
                        "Hoàn thành", note, saved, materials);
                }
                catch
                {
                    File.Delete(saved);
                    throw;
                }

                _photoBytes = null;

                if (IsLoaded)
                    Completed?.Invoke(id);
            }, "Đã xác nhận hoàn tất và gửi thông báo.");
        }
    }
}
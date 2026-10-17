using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using QuanLyChuoiNhaTro.Models;
using QuanLyChuoiNhaTro.Services.Account;

namespace QuanLyChuoiNhaTro.Views.Account
{
    public partial class HoSoView : UserControl
    {
        private HoSoCaNhan? _savedProfile;
        private byte[]? _avatar;
        private bool _busy;

        // Window có thể nghe sự kiện để cập nhật tên ở thanh trên.
        public event EventHandler? ProfileUpdated;

        public HoSoView()
        {
            InitializeComponent();
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_busy)
                return;

            SetBusy(true);
            txtStatus.Text = "Đang tải hồ sơ...";

            try
            {
                var profile = await HoSoService.GetAsync();

                if (!IsLoaded)
                    return;

                _savedProfile = profile;
                DisplayProfile(profile);
                txtStatus.Text = "";
            }
            catch (SqlException)
            {
                txtStatus.Text =
                    "Không tải được hồ sơ. Kiểm tra SQL Server " +
                    "và cột AnhDaiDien.";
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

        private void DisplayProfile(HoSoCaNhan profile)
        {
            txtTenDangNhap.Text = profile.TenDangNhap;
            txtHoTen.Text = profile.HoTen;
            txtCCCD.Text = profile.CCCD;
            txtEmail.Text = profile.Email;
            txtPhone.Text = profile.SoDienThoai;
            txtAddress.Text = profile.DiaChi;

            _avatar = profile.AnhDaiDien;
            ShowAvatar(_avatar);
        }

        private void ShowAvatar(byte[]? bytes)
        {
            imgAvatar.Source = null;
            txtNoAvatar.Visibility = Visibility.Visible;

            btnDeleteAvatar.IsEnabled =
                bytes != null && bytes.Length > 0;

            if (bytes == null || bytes.Length == 0)
                return;

            using var stream = new MemoryStream(bytes);

            var bitmap = new BitmapImage();

            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 280;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            imgAvatar.Source = bitmap;
            txtNoAvatar.Visibility = Visibility.Collapsed;
        }

        private void ChooseAvatar_Click(
            object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Chọn ảnh đại diện",
                Filter = "Ảnh PNG/JPG|*.png;*.jpg;*.jpeg",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var file = new FileInfo(dialog.FileName);

                if (file.Length > 2 * 1024 * 1024)
                {
                    txtStatus.Text =
                        "Ảnh đại diện không được vượt quá 2 MB.";
                    return;
                }

                byte[] bytes = File.ReadAllBytes(dialog.FileName);

                // Kiểm tra nội dung thực sự là PNG hoặc JPEG.
                using (var stream = new MemoryStream(bytes))
                {
                    var decoder = BitmapDecoder.Create(
                        stream,
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.OnLoad);

                    if (decoder is not PngBitmapDecoder
                        && decoder is not JpegBitmapDecoder)
                    {
                        txtStatus.Text =
                            "Vui lòng chọn ảnh PNG hoặc JPG.";
                        return;
                    }
                }

                ShowAvatar(bytes);
                _avatar = bytes;
                txtStatus.Text = "";
            }
            catch (Exception)
            {
                txtStatus.Text =
                    "Không đọc được ảnh. Vui lòng chọn ảnh khác.";
            }
        }

        private void DeleteAvatar_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_busy || _savedProfile == null)
                return;

            _avatar = null;

            ShowAvatar(null);

            txtStatus.Text =
                "Đã bỏ ảnh đại diện trên form. " +
                "Nhấn Cập nhật để lưu thay đổi.";
        }

        private async void Save_Click(
            object sender, RoutedEventArgs e)
        {
            if (_busy || _savedProfile == null)
                return;

            var profile = new HoSoCaNhan
            {
                TenDangNhap = txtTenDangNhap.Text,
                HoTen = txtHoTen.Text,
                CCCD = _savedProfile.CCCD,
                Email = txtEmail.Text,
                SoDienThoai = txtPhone.Text,
                DiaChi = txtAddress.Text,
                AnhDaiDien = _avatar
            };

            SetBusy(true);
            txtStatus.Text = "Đang cập nhật...";

            try
            {
                await HoSoService.UpdateAsync(profile);

                _savedProfile = profile;

                if (!IsLoaded)
                    return;

                DisplayProfile(profile);
                txtStatus.Text = "Cập nhật hồ sơ thành công.";

                ProfileUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch (InvalidOperationException ex)
            {
                txtStatus.Text = ex.Message;
            }
            catch (SqlException)
            {
                txtStatus.Text =
                    "Không lưu được hồ sơ. Kiểm tra kết nối SQL Server.";
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

        private void Cancel_Click(
            object sender, RoutedEventArgs e)
        {
            if (_savedProfile == null)
                return;

            DisplayProfile(_savedProfile);
            txtStatus.Text = "";
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            editorPanel.IsEnabled =
                !busy && _savedProfile != null;
        }
    }
}
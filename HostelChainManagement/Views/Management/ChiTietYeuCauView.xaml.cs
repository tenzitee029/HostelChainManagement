using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Management;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class ChiTietYeuCauView : UserControl
    {
        private void PhanCong_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_dangXuLy && _daTai &&
                _trangThai == "Đã tiếp nhận")
            {
                MoPhanCong?.Invoke(_maSuCo);
            }
        }

        public event Action<string>? MoPhanCong;

        private readonly string _maSuCo;

        private string _trangThai = "";
        private string _tep = "";
        private bool _dangXuLy;
        private bool _daTai;

        public event Action? QuayLai;

        public ChiTietYeuCauView(string maSuCo)
        {
            InitializeComponent();

            _maSuCo = maSuCo;
            txtMaYeuCau.Text = maSuCo;
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (!_daTai)
                await TaiThongTinAsync();
        }

        private async Task TaiThongTinAsync()
        {
            if (_dangXuLy)
                return;

            var user = UserSession.CurrentUser;

            _daTai = false;
            _trangThai = "";
            _tep = "";

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền xem yêu cầu.";
                CapNhatNut();
                return;
            }

            _dangXuLy = true;
            CapNhatNut();
            txtThongBao.Text = "Đang tải yêu cầu...";

            try
            {
                var duLieu =
                    await ChiTietYeuCauService.LayThongTinAsync(
                        _maSuCo,
                        user.MaNguoiDung);

                if (duLieu == null)
                {
                    txtTrangThai.Text = "Không tải được";
                    txtThongBao.Text =
                        "Yêu cầu không tồn tại hoặc bạn không còn quyền xem.";
                    return;
                }

                if (!IsLoaded)
                    return;

                _trangThai =
                    Convert.ToString(duLieu["TrangThai"]) ?? "";

                txtTrangThai.Text = _trangThai;

                txtNgayGui.Text =
                    Convert.ToDateTime(duLieu["NgayGui"])
                        .ToString("dd/MM/yyyy HH:mm");

                txtNguoiGui.Text =
                    Convert.ToString(duLieu["HoTen"]);

                txtPhong.Text =
                    Convert.ToString(duLieu["TenPhong"]);

                txtNoiDung.Text =
                    Convert.ToString(duLieu["NoiDungSuCo"]);

                var thongTin = new StringBuilder();

                thongTin.AppendLine(
                    "Mức độ ưu tiên: " + duLieu["MucDoUuTien"]);

                string mongMuon =
                    Convert.ToString(duLieu["ThoiGianMongMuon"]) ?? "";

                if (mongMuon.Length > 0)
                {
                    thongTin.AppendLine(
                        "Thời gian mong muốn: " + mongMuon);
                }

                if (duLieu["NgayTiepNhan"] != DBNull.Value)
                {
                    thongTin.AppendLine(
                        "Ngày tiếp nhận: "
                        + Convert.ToDateTime(duLieu["NgayTiepNhan"])
                            .ToString("dd/MM/yyyy HH:mm"));
                }

                string lyDo =
                    Convert.ToString(duLieu["LyDoTuChoi"]) ?? "";

                if (lyDo.Length > 0)
                    thongTin.AppendLine("Lý do từ chối: " + lyDo);

                txtThongTinThem.Text = thongTin.ToString();

                _tep =
                    Convert.ToString(duLieu["TepDinhKem"]) ?? "";

                HienThiTep();

                _daTai = true;
                txtThongBao.Text = "";
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không tải được yêu cầu: " + ex.Message;
            }
            finally
            {
                _dangXuLy = false;
                CapNhatNut();
            }
        }

        private bool TepHopLe()
        {
            if (string.IsNullOrWhiteSpace(_tep))
                return false;

            try
            {
                string duoi =
                    Path.GetExtension(_tep).ToLowerInvariant();

                bool dungLoai =
                    duoi == ".png" || duoi == ".jpg" || duoi == ".jpeg"
                    || duoi == ".bmp"
                    || duoi == ".mp4" || duoi == ".avi"
                    || duoi == ".mov" || duoi == ".wmv";

                return dungLoai && File.Exists(_tep);
            }
            catch
            {
                return false;
            }
        }

        private void HienThiTep()
        {
            imgDinhKem.Source = null;
            imgDinhKem.Visibility = Visibility.Collapsed;

            txtKhongCoTep.Visibility = Visibility.Visible;
            txtDuongDan.Text = _tep;

            if (string.IsNullOrWhiteSpace(_tep))
            {
                txtKhongCoTep.Text = "Không có tệp đính kèm.";
                return;
            }

            if (!TepHopLe())
            {
                txtKhongCoTep.Text =
                    "Tệp không có trên máy này hoặc định dạng chưa được hỗ trợ.";
                return;
            }

            string duoi =
                Path.GetExtension(_tep).ToLowerInvariant();

            if (duoi == ".mp4" || duoi == ".avi"
                || duoi == ".mov" || duoi == ".wmv")
            {
                txtKhongCoTep.Text =
                    "Video đính kèm — nhấn Mở tệp để xem.";
                return;
            }

            try
            {
                using var stream = File.OpenRead(_tep);

                var anh = new BitmapImage();

                anh.BeginInit();
                anh.CacheOption = BitmapCacheOption.OnLoad;
                anh.StreamSource = stream;
                anh.DecodePixelWidth = 1000;
                anh.EndInit();
                anh.Freeze();

                imgDinhKem.Source = anh;
                imgDinhKem.Visibility = Visibility.Visible;
                txtKhongCoTep.Visibility = Visibility.Collapsed;
            }
            catch
            {
                txtKhongCoTep.Text = "Không đọc được nội dung ảnh.";
            }
        }

        private void MoTep_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_daTai || !TepHopLe())
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.GetFullPath(_tep),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không mở được tệp: " + ex.Message;
            }
        }

        private async void TiepNhan_Click(
            object sender, RoutedEventArgs e)
        {
            await CapNhatYeuCauAsync(false);
        }

        private async void TuChoi_Click(
            object sender, RoutedEventArgs e)
        {
            if (_dangXuLy || !_daTai ||
                _trangThai != "Chờ tiếp nhận")
            {
                return;
            }

            if (pnlLyDo.Visibility != Visibility.Visible)
            {
                pnlLyDo.Visibility = Visibility.Visible;

                txtThongBao.Text =
                    "Nhập lý do, sau đó nhấn Từ chối tiếp nhận lần nữa.";

                txtLyDo.Focus();
                return;
            }

            await CapNhatYeuCauAsync(true);
        }

        private async Task CapNhatYeuCauAsync(bool tuChoi)
        {
            if (_dangXuLy || !_daTai ||
                _trangThai != "Chờ tiếp nhận")
            {
                return;
            }

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text =
                    "Bạn không có quyền xử lý yêu cầu.";
                return;
            }

            string lyDo = txtLyDo.Text.Trim();

            if (tuChoi && (lyDo.Length == 0 || lyDo.Length > 255))
            {
                txtThongBao.Text =
                    "Lý do từ chối phải có từ 1 đến 255 ký tự.";
                return;
            }

            string cauHoi = tuChoi
                ? $"Từ chối yêu cầu {_maSuCo}?\nLý do: {lyDo}"
                : $"Tiếp nhận yêu cầu {_maSuCo}?";

            if (MessageBox.Show(
                cauHoi,
                "Xác nhận xử lý",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            _dangXuLy = true;
            CapNhatNut();

            txtThongBao.Text = "Đang cập nhật yêu cầu...";

            bool thanhCong = false;

            try
            {
                int soDong =
                    await ChiTietYeuCauService.CapNhatAsync(
                        _maSuCo,
                        user.MaNguoiDung,
                        tuChoi,
                        lyDo);

                thanhCong = soDong == 1;

                if (thanhCong)
                {
                    pnlLyDo.Visibility = Visibility.Collapsed;
                    txtLyDo.Clear();

                    if (tuChoi)
                    {
                        MessageBox.Show(
                            "Đã từ chối yêu cầu.",
                            "Thông báo");

                        await TaiThongTinAsync();
                    }
                    else
                    {
                        // SQL đã lưu tiếp nhận thành công.
                        _trangThai = "Đã tiếp nhận";
                        txtTrangThai.Text = _trangThai;
                        txtThongBao.Text = "";

                        CapNhatNut();

                        // Chuyển ngay sang màn hình phân công.
                        MoPhanCong?.Invoke(_maSuCo);
                    }
                }
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không cập nhật được: " + ex.Message;
            }
            finally
            {
                _dangXuLy = false;
                CapNhatNut();
            }

            if (thanhCong)
            {
                MessageBox.Show(
                    tuChoi
                        ? "Đã từ chối yêu cầu."
                        : "Đã tiếp nhận yêu cầu.",
                    "Thông báo");

                pnlLyDo.Visibility = Visibility.Collapsed;
                txtLyDo.Clear();

                await TaiThongTinAsync();
            }
        }

        private void CapNhatNut()
        {
            bool coQuyen =
                UserSession.CurrentUser?.VaiTro == "Nhân viên quản lý";

            bool choPhep =
                coQuyen && _daTai && !_dangXuLy;

            bool choTiepNhan =
                choPhep && _trangThai == "Chờ tiếp nhận";

            btnTiepNhan.IsEnabled = choTiepNhan;
            btnTuChoi.IsEnabled = choTiepNhan;
            txtLyDo.IsEnabled = choTiepNhan;

            btnQuayLai.IsEnabled = !_dangXuLy;

            btnMoTep.IsEnabled =
                choPhep && TepHopLe();
        }

        private void QuayLai_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_dangXuLy)
                QuayLai?.Invoke();
        }
    }
}
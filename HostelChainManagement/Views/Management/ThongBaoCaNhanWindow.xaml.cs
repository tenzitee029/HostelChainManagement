using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using QuanLyChuoiNhaTro.Services;
using QuanLyChuoiNhaTro.Services.Management;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class ThongBaoCaNhanWindow : UserControl
    {
        public event Action? DaDocThongBao;

        private readonly string _maNguoiDung;
        private bool _dangTai;
        private bool _dangDoc;
        private bool _daDong;

        public ThongBaoCaNhanWindow()
        {
            InitializeComponent();

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                throw new InvalidOperationException(
                    "Bạn không có quyền mở thông báo quản lý.");
            }

            _maNguoiDung = user.MaNguoiDung;

            Loaded += Window_Loaded;
            Unloaded += (_, _) => _daDong = true;

            btnTaiLai.Click += TaiLai_Click;
            lstThongBao.SelectionChanged += ChonThongBao_Changed;
        }

        private bool DungTaiKhoan()
        {
            var user = UserSession.CurrentUser;

            return user != null
                && user.MaNguoiDung == _maNguoiDung
                && user.VaiTro == "Nhân viên quản lý";
        }

        private async void Window_Loaded(
            object sender, RoutedEventArgs e)
        {
            _daDong = false;
            await TaiDanhSachAsync();
        }

        private async void TaiLai_Click(
            object sender, RoutedEventArgs e)
        {
            await TaiDanhSachAsync();
        }

        private async Task TaiDanhSachAsync()
        {
            if (_dangTai || _dangDoc || !DungTaiKhoan())
                return;

            _dangTai = true;
            btnTaiLai.IsEnabled = false;
            lstThongBao.IsEnabled = false;

            lstThongBao.ItemsSource = null;
            txtTieuDe.Text = "Chọn một thông báo";
            txtThongTin.Text = "";
            txtNoiDung.Clear();
            txtTrangThai.Text = "Đang tải thông báo...";

            try
            {
                var table =
                    await ThongBaoCaNhanService.LayDanhSachAsync(
                        _maNguoiDung);

                if (_daDong || !DungTaiKhoan())
                    return;

                lstThongBao.ItemsSource = table.DefaultView;

                int chuaDoc = 0;

                foreach (DataRow dong in table.Rows)
                {
                    if (!Convert.ToBoolean(dong["DaDoc"]))
                        chuaDoc++;
                }

                txtTrangThai.Text = table.Rows.Count == 0
                    ? "Bạn chưa có thông báo."
                    : $"{table.Rows.Count} thông báo — "
                      + $"{chuaDoc} chưa đọc.";
            }
            catch (Exception ex)
            {
                if (!_daDong)
                {
                    txtTrangThai.Text =
                        "Không tải được thông báo: " + ex.Message;
                }
            }
            finally
            {
                _dangTai = false;

                if (!_daDong)
                {
                    btnTaiLai.IsEnabled = true;
                    lstThongBao.IsEnabled = true;
                }
            }
        }

        private async void ChonThongBao_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            if (_dangTai || _dangDoc || !DungTaiKhoan() ||
                lstThongBao.SelectedItem is not DataRowView dong)
            {
                return;
            }

            string maThongBao =
                Convert.ToString(dong["MaThongBao"]) ?? "";

            _dangDoc = true;
            btnTaiLai.IsEnabled = false;
            lstThongBao.IsEnabled = false;

            txtTieuDe.Text = "Đang mở thông báo...";
            txtThongTin.Text = "";
            txtNoiDung.Clear();

            try
            {
                using var service = new ThongBaoCaNhanService();
                await service.MoKetNoiAsync();

                var chiTiet = await service.LayChiTietAsync(
                    maThongBao,
                    _maNguoiDung);

                if (chiTiet == null)
                {
                    txtTieuDe.Text = "Không mở được thông báo";
                    txtTrangThai.Text =
                        "Thông báo không còn tồn tại hoặc bạn không có quyền.";
                    return;
                }

                if (_daDong || !DungTaiKhoan())
                    return;

                txtTieuDe.Text = chiTiet.TieuDe;
                txtNoiDung.Text = chiTiet.NoiDung;
                txtThongTin.Text =
                    $"Người gửi: {chiTiet.NguoiGui}\n"
                    + $"Ngày gửi: {chiTiet.NgayGui:dd/MM/yyyy HH:mm}";

                await service.DanhDauDaDocAsync(
                    maThongBao,
                    _maNguoiDung);

                if (_daDong)
                    return;

                dong["DaDoc"] = true;
                DaDocThongBao?.Invoke();
                txtTrangThai.Text = "Đã mở và đánh dấu thông báo đã đọc.";
            }
            catch (Exception ex)
            {
                if (!_daDong)
                {
                    txtTrangThai.Text =
                        "Có lỗi khi mở hoặc đánh dấu đã đọc: " + ex.Message;
                }
            }
            finally
            {
                _dangDoc = false;

                if (!_daDong)
                {
                    btnTaiLai.IsEnabled = true;
                    lstThongBao.IsEnabled = true;
                }
            }
        }
    }
}
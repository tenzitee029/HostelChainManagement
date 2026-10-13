using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

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
                using var connection = Database.CreateConnection();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    SELECT
                        tb.MaThongBao,
                        tb.TieuDe,
                        tb.NgayGui,
                        n.HoTen AS NguoiGui,
                        nn.DaDoc
                    FROM dbo.ThongBaoNguoiNhan nn
                    JOIN dbo.ThongBao tb
                      ON tb.MaThongBao = nn.MaThongBao
                    JOIN dbo.NguoiDung n
                      ON n.MaNguoiDung = tb.MaNguoiGui
                    WHERE nn.MaNguoiNhan = @MaNguoiDung
                    ORDER BY tb.NgayGui DESC, tb.MaThongBao;";

                command.Parameters.Add(
                    "@MaNguoiDung", SqlDbType.VarChar, 20)
                    .Value = _maNguoiDung;

                await connection.OpenAsync();

                using var reader =
                    await command.ExecuteReaderAsync();

                var table = new DataTable();
                table.Load(reader);

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
                using var connection = Database.CreateConnection();
                await connection.OpenAsync();

                string tieuDe;
                string noiDung;
                string nguoiGui;
                DateTime ngayGui;

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT tb.TieuDe, tb.NoiDung,
                               tb.NgayGui, n.HoTen AS NguoiGui
                        FROM dbo.ThongBao tb
                        JOIN dbo.ThongBaoNguoiNhan nn
                          ON nn.MaThongBao = tb.MaThongBao
                        JOIN dbo.NguoiDung n
                          ON n.MaNguoiDung = tb.MaNguoiGui
                        WHERE tb.MaThongBao = @MaThongBao
                          AND nn.MaNguoiNhan = @MaNguoiDung;";

                    command.Parameters.Add(
                        "@MaThongBao", SqlDbType.VarChar, 20)
                        .Value = maThongBao;

                    command.Parameters.Add(
                        "@MaNguoiDung", SqlDbType.VarChar, 20)
                        .Value = _maNguoiDung;

                    using var reader =
                        await command.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        txtTieuDe.Text = "Không mở được thông báo";
                        txtTrangThai.Text =
                            "Thông báo không còn tồn tại hoặc bạn không có quyền.";
                        return;
                    }

                    tieuDe = Convert.ToString(reader["TieuDe"]) ?? "";
                    noiDung = Convert.ToString(reader["NoiDung"]) ?? "";
                    nguoiGui = Convert.ToString(reader["NguoiGui"]) ?? "";
                    ngayGui = Convert.ToDateTime(reader["NgayGui"]);
                }

                if (_daDong || !DungTaiKhoan())
                    return;

                txtTieuDe.Text = tieuDe;
                txtNoiDung.Text = noiDung;
                txtThongTin.Text =
                    $"Người gửi: {nguoiGui}\n"
                    + $"Ngày gửi: {ngayGui:dd/MM/yyyy HH:mm}";

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        UPDATE dbo.ThongBaoNguoiNhan
                        SET DaDoc = 1,
                            NgayDoc = GETDATE()
                        WHERE MaThongBao = @MaThongBao
                          AND MaNguoiNhan = @MaNguoiDung
                          AND DaDoc = 0;";

                    command.Parameters.Add(
                        "@MaThongBao", SqlDbType.VarChar, 20)
                        .Value = maThongBao;

                    command.Parameters.Add(
                        "@MaNguoiDung", SqlDbType.VarChar, 20)
                        .Value = _maNguoiDung;

                    await command.ExecuteNonQueryAsync();
                }

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
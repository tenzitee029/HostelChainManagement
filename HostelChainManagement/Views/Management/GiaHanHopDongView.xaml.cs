using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Data;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class GiaHanHopDongView : UserControl
    {
        private readonly string _maHopDong;
        private string _maPhong = "";
        private string _maKhach = "";
        private DateTime _ngayKetThucCu;

        private bool _dangTai;
        private bool _dangLuu;
        private bool _sanSang;

        public event Action? QuayLai;
        public event Action? DaLuu;

        public GiaHanHopDongView(string maHopDong)
        {
            InitializeComponent();

            _maHopDong = maHopDong;
            txtMaHopDong.Text = maHopDong;
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_dangTai || _sanSang)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền gia hạn hợp đồng.";
                return;
            }

            _dangTai = true;
            DatTrangThai();
            txtThongBao.Text = "Đang tải hợp đồng...";

            try
            {
                using var connection = Database.CreateConnection();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    SELECT
                        h.MaPhong,
                        h.MaKhachThue,
                        h.NgayBatDau,
                        h.NgayKetThuc,
                        h.TrangThai,
                        n.HoTen,
                        d.TenDayTro + N' - Phòng ' + p.SoPhong AS TenPhong,
                        CASE
                            WHEN h.TrangThai = N'Hiệu lực'
                             AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                             AND h.NgayKetThuc >= CAST(GETDATE() AS DATE)
                                THEN 1 ELSE 0
                        END AS ChoPhepGiaHan
                    FROM dbo.HopDongThue h
                    JOIN dbo.KhachThue k
                      ON k.MaKhachThue = h.MaKhachThue
                    JOIN dbo.NguoiDung n
                      ON n.MaNguoiDung = k.MaNguoiDung
                    JOIN dbo.PhongTro p ON p.MaPhong = h.MaPhong
                    JOIN dbo.DayTro d ON d.MaDayTro = p.MaDayTro
                    WHERE h.MaHopDong = @MaHopDong
                      AND EXISTS (
                          SELECT 1 FROM dbo.NhanVien nv
                          WHERE nv.MaDayTro = p.MaDayTro
                            AND nv.MaNguoiDung = @QuanLy
                            AND nv.ChucVu = N'Quản lý'
                            AND nv.TrangThaiLamViec = N'Đang làm'
                      );";

                command.Parameters.Add(
                    "@MaHopDong", SqlDbType.VarChar, 20)
                    .Value = _maHopDong;

                command.Parameters.Add(
                    "@QuanLy", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;

                await connection.OpenAsync();

                using var reader = await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    txtThongBao.Text =
                        "Hợp đồng không tồn tại hoặc bạn không có quyền quản lý.";
                    return;
                }

                if (!IsLoaded)
                    return;

                _maPhong = Convert.ToString(reader["MaPhong"]) ?? "";
                _maKhach = Convert.ToString(reader["MaKhachThue"]) ?? "";

                DateTime batDau = Convert.ToDateTime(reader["NgayBatDau"]);
                _ngayKetThucCu =
                    Convert.ToDateTime(reader["NgayKetThuc"]).Date;

                txtTrangThai.Text = Convert.ToString(reader["TrangThai"]);
                txtKhachThue.Text = Convert.ToString(reader["HoTen"]);
                txtPhong.Text = Convert.ToString(reader["TenPhong"]);
                txtNgayBatDau.Text = batDau.ToString("dd/MM/yyyy");
                txtNgayKetThuc.Text = _ngayKetThucCu.ToString("dd/MM/yyyy");

                if (Convert.ToInt32(reader["ChoPhepGiaHan"]) != 1)
                {
                    txtThongBao.Text =
                        "Chỉ gia hạn hợp đồng có trạng thái Hiệu lực "
                        + "và đang trong thời gian thuê.";
                    return;
                }

                if (_ngayKetThucCu == DateTime.MaxValue.Date)
                {
                    txtThongBao.Text =
                        "Ngày kết thúc đã đạt giới hạn, không thể gia hạn thêm.";
                    return;
                }

                dpKetThucMoi.DisplayDateStart =
                    _ngayKetThucCu.AddDays(1);

                _sanSang = true;
                txtThongBao.Text = "";
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không tải được hợp đồng: " + ex.Message;
            }
            finally
            {
                _dangTai = false;
                DatTrangThai();
            }
        }

        private async void Luu_Click(
            object sender, RoutedEventArgs e)
        {
            if (_dangLuu || !_sanSang)
                return;

            var user = UserSession.CurrentUser;

            if (user == null || user.VaiTro != "Nhân viên quản lý")
            {
                txtThongBao.Text = "Bạn không có quyền gia hạn hợp đồng.";
                return;
            }

            if (!dpKetThucMoi.SelectedDate.HasValue)
            {
                txtThongBao.Text = "Vui lòng chọn ngày kết thúc mới.";
                return;
            }

            DateTime ngayMoi = dpKetThucMoi.SelectedDate.Value.Date;

            if (ngayMoi <= _ngayKetThucCu)
            {
                txtThongBao.Text =
                    "Ngày kết thúc mới phải sau ngày kết thúc hiện tại.";
                return;
            }

            var xacNhan = MessageBox.Show(
                $"Gia hạn hợp đồng {_maHopDong} đến "
                + $"{ngayMoi:dd/MM/yyyy}?",
                "Xác nhận gia hạn",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (xacNhan != MessageBoxResult.Yes)
                return;

            _dangLuu = true;
            DatTrangThai();
            txtThongBao.Text = "Đang gia hạn hợp đồng...";

            bool thanhCong = false;

            try
            {
                using var connection = Database.CreateConnection();
                await connection.OpenAsync();

                using var transaction =
                    connection.BeginTransaction(IsolationLevel.Serializable);

                using var command = connection.CreateCommand();
                command.Transaction = transaction;

                command.CommandText = @"
                    DECLARE @Khoa VARCHAR(20);
                    DECLARE @BatDau DATE;

                    -- Cùng thứ tự khóa với chức năng lập hợp đồng:
                    -- phòng trước, khách thuê sau.
                    SELECT @Khoa = p.MaPhong
                    FROM dbo.PhongTro p WITH (UPDLOCK, HOLDLOCK)
                    WHERE p.MaPhong = @MaPhong;

                    SET @Khoa = NULL;

                    SELECT @Khoa = k.MaKhachThue
                    FROM dbo.KhachThue k WITH (UPDLOCK, HOLDLOCK)
                    WHERE k.MaKhachThue = @MaKhach;

                    SELECT @BatDau = h.NgayBatDau
                    FROM dbo.HopDongThue h WITH (UPDLOCK, HOLDLOCK)
                    WHERE h.MaHopDong = @MaHopDong
                      AND h.MaPhong = @MaPhong
                      AND h.MaKhachThue = @MaKhach
                      AND h.NgayKetThuc = @NgayCu
                      AND h.TrangThai = N'Hiệu lực'
                      AND h.NgayBatDau <= CAST(GETDATE() AS DATE)
                      AND h.NgayKetThuc >= CAST(GETDATE() AS DATE);

                    IF @BatDau IS NULL
                        THROW 50001,
                            N'Hợp đồng đã thay đổi hoặc không còn đủ điều kiện gia hạn. Hãy mở lại form.', 1;

                    IF @NgayMoi <= @NgayCu
                        THROW 50002,
                            N'Ngày kết thúc mới phải sau ngày kết thúc hiện tại.', 1;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM dbo.PhongTro p
                        JOIN dbo.NhanVien nv ON nv.MaDayTro = p.MaDayTro
                        WHERE p.MaPhong = @MaPhong
                          AND nv.MaNguoiDung = @QuanLy
                          AND nv.ChucVu = N'Quản lý'
                          AND nv.TrangThaiLamViec = N'Đang làm'
                    )
                        THROW 50003,
                            N'Bạn không còn quyền quản lý hợp đồng này.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM dbo.HopDongThue h WITH (UPDLOCK, HOLDLOCK)
                        WHERE h.MaHopDong <> @MaHopDong
                          AND h.TrangThai IN
                              (N'Hiệu lực', N'Yêu cầu trả phòng')
                          AND (
                              h.MaPhong = @MaPhong
                              OR h.MaKhachThue = @MaKhach
                          )
                          AND h.NgayBatDau <= @NgayMoi
                          AND h.NgayKetThuc >= @BatDau
                    )
                        THROW 50004,
                            N'Thời gian gia hạn trùng hợp đồng khác của phòng hoặc khách thuê.', 1;

                    UPDATE dbo.HopDongThue
                    SET NgayKetThuc = @NgayMoi
                    WHERE MaHopDong = @MaHopDong;";

                command.Parameters.Add(
                    "@MaHopDong", SqlDbType.VarChar, 20)
                    .Value = _maHopDong;
                command.Parameters.Add(
                    "@MaPhong", SqlDbType.VarChar, 20)
                    .Value = _maPhong;
                command.Parameters.Add(
                    "@MaKhach", SqlDbType.VarChar, 20)
                    .Value = _maKhach;
                command.Parameters.Add(
                    "@QuanLy", SqlDbType.VarChar, 20)
                    .Value = user.MaNguoiDung;
                command.Parameters.Add(
                    "@NgayCu", SqlDbType.Date)
                    .Value = _ngayKetThucCu;
                command.Parameters.Add(
                    "@NgayMoi", SqlDbType.Date)
                    .Value = ngayMoi;

                await command.ExecuteNonQueryAsync();
                transaction.Commit();

                thanhCong = true;
                _sanSang = false;
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                txtThongBao.Text =
                    "Dữ liệu đang được người khác cập nhật. Vui lòng thử lại.";
            }
            catch (Exception ex)
            {
                txtThongBao.Text =
                    "Không gia hạn được hợp đồng: " + ex.Message;
            }
            finally
            {
                _dangLuu = false;
                DatTrangThai();
            }

            if (thanhCong)
            {
                MessageBox.Show(
                    $"Gia hạn thành công đến ngày {ngayMoi:dd/MM/yyyy}.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DaLuu?.Invoke();
            }
        }

        private void DatTrangThai()
        {
            bool choPhep = _sanSang && !_dangTai && !_dangLuu;

            dpKetThucMoi.IsEnabled = choPhep;
            btnLuu.IsEnabled = choPhep;
            btnQuayLai.IsEnabled = !_dangLuu;

            btnLuu.Content = _dangLuu
                ? "Đang lưu..."
                : "Xác nhận gia hạn";
        }

        private void QuayLai_Click(
            object sender, RoutedEventArgs e)
        {
            if (!_dangLuu)
                QuayLai?.Invoke();
        }
    }
}
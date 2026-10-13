namespace QuanLyChuoiNhaTro.Models
{
    public sealed class HoSoCaNhan
    {
        public string TenDangNhap { get; set; } = "";
        public string HoTen { get; set; } = "";
        public string CCCD { get; set; } = "";
        public string Email { get; set; } = "";
        public string SoDienThoai { get; set; } = "";
        public string DiaChi { get; set; } = "";

        public byte[]? AnhDaiDien { get; set; }
    }
}
using System;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services.Owner;

namespace QuanLyChuoiNhaTro.Views.Owner
{
    public partial class DayTroFormView : UserControl
    {
        private readonly bool _dangSua;

        public event Action? LuuThanhCong;

        public DayTroFormView(DataRowView? dayTro = null)
        {
            InitializeComponent();

            _dangSua = dayTro != null;

            if (!_dangSua)
            {
                txtTieuDe.Text = "Thêm dãy trọ";
                txtMaDay.Text = "—";
                return;
            }

            txtTieuDe.Text = "Cập nhật dãy trọ";

            txtMaDay.Text = LayGiaTri(dayTro!, "MaDayTro");
            txtTenDay.Text = LayGiaTri(dayTro!, "TenDayTro");
            txtDiaChi.Text = LayGiaTri(dayTro!, "DiaChi");
            txtMoTa.Text = LayGiaTri(dayTro!, "MoTa");
            txtQuyDinh.Text = LayGiaTri(dayTro!, "QuyDinh");

            string trangThai = LayGiaTri(dayTro!, "TrangThai");

            var item = cmbTrangThai.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(x =>
                    string.Equals(
                        x.Content?.ToString(),
                        trangThai,
                        StringComparison.Ordinal));

            cmbTrangThai.SelectedItem = item;

            if (item == null)
            {
                cmbTrangThai.SelectedIndex = 0;
            }
        }

        private static string LayGiaTri(
            DataRowView row,
            string tenCot)
        {
            if (!row.DataView.Table.Columns.Contains(tenCot))
                return "";

            if (row[tenCot] == DBNull.Value)
                return "";

            return Convert.ToString(row[tenCot]) ?? "";
        }

        private async void Luu_Click(
            object sender,
            RoutedEventArgs e)
        {
            txtThongBao.Visibility = Visibility.Collapsed;

            string tenDay = txtTenDay.Text.Trim();
            string diaChi = txtDiaChi.Text.Trim();

            string trangThai =
                (cmbTrangThai.SelectedItem as ComboBoxItem)?
                .Content?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(tenDay))
            {
                HienThiLoi("Vui lòng nhập tên dãy trọ.");
                txtTenDay.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(diaChi))
            {
                HienThiLoi("Vui lòng nhập địa chỉ dãy trọ.");
                txtDiaChi.Focus();
                return;
            }

            if (cmbTrangThai.SelectedIndex <= 0)
            {
                HienThiLoi("Vui lòng chọn trạng thái dãy trọ.");
                cmbTrangThai.Focus();
                return;
            }

            btnLuu.IsEnabled = false;

            try
            {
                string? maDay = _dangSua
                    ? txtMaDay.Text.Trim()
                    : null;

                string maDaLuu = await DayTroFormService.LuuDayTroAsync(
                    maDay,
                    tenDay,
                    diaChi,
                    txtMoTa.Text,
                    txtQuyDinh.Text,
                    trangThai);

                txtMaDay.Text = maDaLuu;

                MessageBox.Show(
                    "Lưu thông tin dãy trọ thành công",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                LuuThanhCong?.Invoke();
            }
            catch (InvalidOperationException ex)
            {
                HienThiLoi(ex.Message);
            }
            catch (SqlException ex) when (
                ex.Number == 2627 || ex.Number == 2601)
            {
                HienThiLoi(
                    "Mã dãy hoặc thông tin dãy trọ đã tồn tại.");
            }
            catch (SqlException)
            {
                HienThiLoi(
                    "Không thể lưu dữ liệu vào SQL Server. Vui lòng kiểm tra kết nối và thử lại.");
            }
            catch (Exception)
            {
                HienThiLoi(
                    "Đã xảy ra lỗi khi lưu thông tin dãy trọ.");
            }
            finally
            {
                btnLuu.IsEnabled = true;
            }
        }

        private void HienThiLoi(string noiDung)
        {
            txtThongBao.Text = noiDung;
            txtThongBao.Visibility = Visibility.Visible;
        }
    }
}
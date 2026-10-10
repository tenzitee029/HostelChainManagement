using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Tenant
{
    public partial class PhongCuaToiView : UserControl
    {
        private bool _loading;

        public PhongCuaToiView()
        {
            InitializeComponent();
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_loading)
                return;

            _loading = true;
            ClearRoom();
            txtStatus.Text = "Đang tải thông tin phòng...";

            try
            {
                var room =
                    await KhachThueService.GetPhongCuaToiAsync();

                // View có thể đã bị thay thế khi người dùng chọn menu.
                if (!IsLoaded)
                    return;

                if (room == null)
                {
                    txtStatus.Text =
                        "Tài khoản chưa có hợp đồng thuê đang hiệu lực.";
                    return;
                }

                txtSoPhong.Text = room.SoPhong;
                txtTenDay.Text = room.TenDayTro;
                txtDiaChi.Text = room.DiaChi;
                txtDienTich.Text = $"{room.DienTich:N2} m²";
                txtGiaThue.Text = $"{room.GiaThue:N0} VNĐ";

                dgThietBi.ItemsSource = room.ThietBi.DefaultView;
                dgThanhVien.ItemsSource = room.ThanhVien.DefaultView;

                txtEmptyEquipment.Visibility =
                    room.ThietBi.Rows.Count == 0
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                txtEmptyMembers.Visibility =
                    room.ThanhVien.Rows.Count == 0
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                txtStatus.Text = "";
            }
            catch (SqlException)
            {
                txtStatus.Text =
                    "Không tải được thông tin phòng. " +
                    "Vui lòng kiểm tra kết nối SQL Server.";
            }
            catch (Exception ex)
            {
                txtStatus.Text = ex.Message;
            }
            finally
            {
                _loading = false;
            }
        }

        private void ClearRoom()
        {
            txtSoPhong.Text = "—";
            txtTenDay.Text = "—";
            txtDiaChi.Text = "—";
            txtDienTich.Text = "—";
            txtGiaThue.Text = "—";

            dgThietBi.ItemsSource = null;
            dgThanhVien.ItemsSource = null;

            txtEmptyEquipment.Visibility = Visibility.Visible;
            txtEmptyMembers.Visibility = Visibility.Visible;
        }
    }
}
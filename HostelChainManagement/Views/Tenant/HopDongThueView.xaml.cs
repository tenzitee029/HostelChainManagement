using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Tenant
{
    public partial class HopDongThueView : UserControl
    {
        private bool _loading;

        public HopDongThueView()
        {
            InitializeComponent();
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_loading)
                return;

            _loading = true;

            cboHopDong.IsEnabled = false;
            cboHopDong.ItemsSource = null;
            contractDetails.DataContext = null;
            contractDetails.Visibility = Visibility.Collapsed;

            txtStatus.Text = "Đang tải hợp đồng...";

            try
            {
                var table =
                    await KhachThueService.GetHopDongCuaToiAsync();

                if (!IsLoaded)
                    return;

                if (table.Rows.Count == 0)
                {
                    txtStatus.Text =
                        "Tài khoản chưa có hợp đồng thuê.";
                    return;
                }

                cboHopDong.ItemsSource = table.DefaultView;
                cboHopDong.IsEnabled = true;
                cboHopDong.SelectedIndex = 0;

                contractDetails.Visibility = Visibility.Visible;
                txtStatus.Text = "";
            }
            catch (SqlException)
            {
                txtStatus.Text =
                    "Không tải được hợp đồng. " +
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

        private void Contract_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            contractDetails.DataContext =
                cboHopDong.SelectedItem as DataRowView;
        }
    }
}
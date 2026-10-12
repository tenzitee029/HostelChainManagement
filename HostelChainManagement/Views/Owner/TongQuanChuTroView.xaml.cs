
using System;
using System.Windows;
using System.Windows.Controls;

namespace QuanLyChuoiNhaTro.Views.Owner
{
    public partial class TongQuanChuTroView : UserControl
    {
        public event Action<string>? NavigateRequested;

        public TongQuanChuTroView()
        {
            InitializeComponent();
        }

        private void DayTro_Click(object sender, RoutedEventArgs e)
        {
            NavigateRequested?.Invoke("DayTro");
        }

        private void NhanVien_Click(object sender, RoutedEventArgs e)
        {
            NavigateRequested?.Invoke("NhanVien");
        }

        private void PhanCong_Click(object sender, RoutedEventArgs e)
        {
            NavigateRequested?.Invoke("PhanCong");
        }

        private void HopDong_Click(object sender, RoutedEventArgs e)
        {
            NavigateRequested?.Invoke("HopDong");
        }
    }
}

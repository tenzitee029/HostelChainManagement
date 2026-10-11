using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QuanLyChuoiNhaTro.Services;

namespace QuanLyChuoiNhaTro.Views.Management
{
    public partial class TongQuanQuanLyView : UserControl
    {
        private bool _loading;

        public TongQuanQuanLyView()
        {
            InitializeComponent();

            itemsOperations.ItemsSource = new[]
            {
                "Quản lý phòng",
                "Quản lý khách thuê",
                "Lập / Gia hạn / Thanh lý hợp đồng",
                "Tạo hóa đơn hàng tháng",
                "Xác nhận thanh toán",
                "Tiếp nhận / Phân công sửa chữa",
                "Gửi thông báo",
                "Lập báo cáo tháng / năm"
            };

            DisplayData(null);
        }

        private async void View_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_loading)
                return;

            _loading = true;
            DisplayData(null);
            txtStatus.Text = "Đang tải dữ liệu...";

            try
            {
                var data = await QuanLyService.GetTongQuanAsync();

                if (!IsLoaded)
                    return;

                DisplayData(data);
                txtStatus.Text = "";
            }
            catch (SqlException)
            {
                txtStatus.Text =
                    "Không tải được dữ liệu. Kiểm tra kết nối SQL Server.";
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

        private void DisplayData(TongQuanQuanLy? data)
        {
            itemsMetrics.ItemsSource = new[]
            {
                Metric("P", "Tổng phòng", data?.TongPhong,
                    "Phòng thuộc dãy phụ trách", "#DBEAFE"),

                Metric("T", "Đang thuê", data?.DangThue,
                    "Phòng có trạng thái đang thuê", "#DBEAFE"),

                Metric("+", "Phòng trống", data?.PhongTrong,
                    "Phòng có trạng thái trống", "#DCFCE7"),

                Metric("SC", "Đang sửa chữa", data?.DangSuaChua,
                    "Phòng đang sửa chữa", "#FEF3C7"),

                Metric("HĐ", "Chưa thanh toán", data?.ChuaThanhToan,
                    "Hóa đơn chưa thanh toán hoặc còn nợ", "#FEE2E2"),

                Metric("YC", "Yêu cầu sửa chữa", data?.YeuCauSuaChua,
                    "Yêu cầu đang chờ hoặc đang xử lý", "#CFFAFE")
            };

            itemsSummary.ItemsSource = new[]
            {
                Summary(
                    "Phòng trọ",
                    data == null
                        ? "Trống / Đang thuê / Đang sửa chữa"
                        : $"{data.PhongTrong} trống / " +
                          $"{data.DangThue} đang thuê / " +
                          $"{data.DangSuaChua} đang sửa chữa",
                    data?.TongPhong),

                Summary(
                    "Hợp đồng thuê",
                    "Hợp đồng đang hiệu lực trong dãy phụ trách",
                    data?.HopDongHieuLuc),

                Summary(
                    "Hóa đơn",
                    data == null
                        ? "Theo dõi trạng thái thanh toán"
                        : $"{data.ChuaThanhToan} hóa đơn chưa thanh toán hoặc còn nợ",
                    data?.TongHoaDon),

                Summary(
                    "Yêu cầu sửa chữa",
                    "Yêu cầu đang chờ hoặc đang xử lý",
                    data?.YeuCauSuaChua)
            };
        }

        private static QuanLyMetric Metric(
            string code,
            string title,
            int? value,
            string note,
            string color)
        {
            return new QuanLyMetric
            {
                Code = code,
                Title = title,
                Value = value?.ToString() ?? "—",
                Note = note,
                Color = color
            };
        }

        private static QuanLySummary Summary(
            string title,
            string note,
            int? value)
        {
            return new QuanLySummary
            {
                Title = title,
                Note = note,
                Value = value?.ToString() ?? "—"
            };
        }
    }

    public sealed class QuanLyMetric
    {
        public string Code { get; init; } = "";
        public string Title { get; init; } = "";
        public string Value { get; init; } = "";
        public string Note { get; init; } = "";
        public string Color { get; init; } = "";
    }

    public sealed class QuanLySummary
    {
        public string Title { get; init; } = "";
        public string Note { get; init; } = "";
        public string Value { get; init; } = "";
    }
}
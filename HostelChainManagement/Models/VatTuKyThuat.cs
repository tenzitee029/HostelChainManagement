using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuanLyChuoiNhaTro.Models;

public sealed class VatTuKyThuat : INotifyPropertyChanged
{
    private string _tenVatTu = "";
    private decimal _soLuong = 1;
    private decimal _donGia;

    public string TenVatTu
    {
        get => _tenVatTu;
        set
        {
            _tenVatTu = value;
            Changed();
        }
    }

    public decimal SoLuong
    {
        get => _soLuong;
        set
        {
            _soLuong = value;
            Changed();
            Changed(nameof(ThanhTien));
        }
    }

    public decimal DonGia
    {
        get => _donGia;
        set
        {
            _donGia = value;
            Changed();
            Changed(nameof(ThanhTien));
        }
    }

    public decimal ThanhTien => decimal.Round(
        SoLuong * DonGia,
        0,
        MidpointRounding.AwayFromZero);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Changed(
        [CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(name));
    }
}
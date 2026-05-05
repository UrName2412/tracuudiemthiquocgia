using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TraCuuDiemThiQuocGia.Models;
using TraCuuDiemThiQuocGia.Services;

namespace TraCuuDiemThiQuocGia.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private string _soBaoDanh = string.Empty;
    private ThiSinh? _thiSinh;
    private bool _isLoading = false;
    private string _errorMessage = string.Empty;
    private bool _hasError = false;

    public string SoBaoDanh
    {
        get => _soBaoDanh;
        set { _soBaoDanh = value; OnPropertyChanged(); }
    }

    public ThiSinh? ThiSinh
    {
        get => _thiSinh;
        set
        {
            _thiSinh = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSearchVisible));
            OnPropertyChanged(nameof(IsResultVisible));
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; OnPropertyChanged(); }
    }

    public bool HasError
    {
        get => _hasError;
        set { _hasError = value; OnPropertyChanged(); }
    }

    public bool IsSearchVisible => ThiSinh == null;
    public bool IsResultVisible => ThiSinh != null;

    public ICommand TraCuuCommand { get; }
    public ICommand ResetCommand { get; }

    public MainViewModel()
    {
        TraCuuCommand = new Command(async () => await OnTraCuu());
        ResetCommand = new Command(OnReset);
    }

    private async Task OnTraCuu()
    {
        HasError = false;
        ErrorMessage = string.Empty;

        if (!int.TryParse(SoBaoDanh, out int sbd) || sbd < 1 || sbd > 1000)
        {
            HasError = true;
            ErrorMessage = "Vui lòng nhập số báo danh hợp lệ từ 1 đến 1000.";
            return;
        }

        IsLoading = true;

        var (ketQua, loi) = await DatabaseRouter.TraCuuAsync(sbd);

        IsLoading = false;

        if (ketQua != null)
        {
            ThiSinh = ketQua;
        }
        else
        {
            HasError = true;
            ErrorMessage = string.IsNullOrEmpty(loi)
                ? $"Không tìm thấy SBD {sbd} trong database."
                : loi;
        }
    }

    private void OnReset()
    {
        ThiSinh = null;
        SoBaoDanh = string.Empty;
        HasError = false;
        ErrorMessage = string.Empty;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
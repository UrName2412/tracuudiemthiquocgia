using System.Net.Http.Headers;
using System.Text.Json;
using TraCuuDiemThiQuocGia.Models;

namespace TraCuuDiemThiQuocGia.Services;

public class DatabaseRouter
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static string? _supabaseMienBacUrl;
    private static string? _supabaseMienBacKey;
    private static string? _supabaseMienNamUrl;
    private static string? _supabaseMienNamKey;

    private const string TABLE_NAME = "ThiSinh";

    static DatabaseRouter()
    {
        LoadConfiguration();
    }

    private static void LoadConfiguration()
    {
        try
        {
            // Trong MAUI, cách tốt nhất để đọc file cấu hình tĩnh là dùng FileSystem.OpenAppPackageFileAsync
            // Tuy nhiên hàm static constructor không hỗ trợ async, nên chúng ta cần tạo một Task đồng bộ
            var json = Task.Run(async () =>
            {
                try
                {
                    using var stream = await FileSystem.OpenAppPackageFileAsync("appsettings.json");
                    using var reader = new StreamReader(stream);
                    return await reader.ReadToEndAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ FileSystem.OpenAppPackageFileAsync failed: {ex.Message}");
                    return null;
                }
            }).Result;

            if (string.IsNullOrEmpty(json))
            {
                System.Diagnostics.Debug.WriteLine($"✗ Could not read appsettings.json from AppPackage");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"✓ Found and read appsettings.json");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("Supabase", out var supabaseElement))
            {
                if (supabaseElement.TryGetProperty("MienBac", out var mienBac))
                {
                    if (mienBac.TryGetProperty("Url", out var url))
                    {
                        _supabaseMienBacUrl = url.GetString();
                        System.Diagnostics.Debug.WriteLine($"✓ Loaded MienBac URL");
                    }
                    if (mienBac.TryGetProperty("AnonKey", out var key))
                    {
                        _supabaseMienBacKey = key.GetString();
                        System.Diagnostics.Debug.WriteLine($"✓ Loaded MienBac Key");
                    }
                }

                if (supabaseElement.TryGetProperty("MienNam", out var mienNam))
                {
                    if (mienNam.TryGetProperty("Url", out var url))
                    {
                        _supabaseMienNamUrl = url.GetString();
                        System.Diagnostics.Debug.WriteLine($"✓ Loaded MienNam URL");
                    }
                    if (mienNam.TryGetProperty("AnonKey", out var key))
                    {
                        _supabaseMienNamKey = key.GetString();
                        System.Diagnostics.Debug.WriteLine($"✓ Loaded MienNam Key");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"✗ Error parsing configuration: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"  {ex.StackTrace}");
        }
    }

    public static async Task<(ThiSinh? ketQua, string? loi)> TraCuuAsync(int sbd)
    {
        try
        {
            if (sbd >= 1 && sbd <= 500)
                return await TraCuuTuSupabaseAsync(_supabaseMienBacUrl, _supabaseMienBacKey, sbd, "Miền Bắc");

            if (sbd >= 501 && sbd <= 1000)
                return await TraCuuTuSupabaseAsync(_supabaseMienNamUrl, _supabaseMienNamKey, sbd, "Miền Nam");

            return (null, "Số báo danh phải từ 1 đến 1000.");
        }
        catch (TaskCanceledException)
        {
            return (null, "Không kết nối được Supabase. Vui lòng kiểm tra mạng.");
        }
        catch (Exception ex)
        {
            return (null, $"Khu vực đang bảo trì.");
        }
    }

    private static async Task<(ThiSinh? ketQua, string? loi)> TraCuuTuSupabaseAsync(string? baseUrl, string? apiKey, int sbd, string khuVuc)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
            return (null, $"Chưa cấu hình Supabase cho {khuVuc}. Vui lòng điền URL và anon key trong appsettings.json.");

        var requestUrl = $"{baseUrl.TrimEnd('/')}/{TABLE_NAME}?MaSoThiSinh=eq.{sbd}&select=MaSoThiSinh,HoTen,NgaySinh,DiemToan,DiemVan,DiemAnh&limit=1";
        
        System.Diagnostics.Debug.WriteLine($"\n--- GỌI API SUPABASE ({khuVuc}) ---");
        System.Diagnostics.Debug.WriteLine($"URL: {requestUrl}");

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("apikey", apiKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        
        System.Diagnostics.Debug.WriteLine($"Status Code: {response.StatusCode}");
        System.Diagnostics.Debug.WriteLine($"Response JSON: {json}");
        System.Diagnostics.Debug.WriteLine($"----------------------------------\n");

        if (!response.IsSuccessStatusCode)
        {
            return response.StatusCode == System.Net.HttpStatusCode.NotFound
                ? (null, $"Không tìm thấy số báo danh {sbd}.")
                : (null, $"Khu vực đang bảo trì.");
        }

        var data = JsonSerializer.Deserialize<List<ThiSinhSupabaseJson>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var item = data?.FirstOrDefault();
        if (item is null)
            return (null, $"Không tìm thấy SBD {sbd} trong dữ liệu {khuVuc}.");

        var ts = new ThiSinh
        {
            SoBaoDanh = item.MaSoThiSinh,
            HoTen = item.HoTen ?? string.Empty,
            NgaySinh = item.NgaySinh,
            KhuVuc = khuVuc,
            DiemToan = item.DiemToan,
            DiemVan = item.DiemVan,
            DiemAnh = item.DiemAnh
        };

        return (ts, null);
    }

    private sealed class ThiSinhSupabaseJson
    {
        public int MaSoThiSinh { get; set; }
        public string? HoTen { get; set; }
        public DateTime NgaySinh { get; set; }
        public double DiemToan { get; set; }
        public double DiemVan { get; set; }
        public double DiemAnh { get; set; }
    }
}
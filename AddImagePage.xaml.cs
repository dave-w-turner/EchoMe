namespace EchoMe;

public partial class AddImagePage : ContentPage
{
    public AddImagePage()
    {
        InitializeComponent();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnPickFileClicked(object? sender, EventArgs e)
    {
        try
        {
            var results = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
            {
                SelectionLimit = 1
            });

            var result = results?.FirstOrDefault();

            if (result != null)
            {
                await Shell.Current.GoToAsync($"{nameof(CropPhotoPage)}?ImagePath={Uri.EscapeDataString(result.FullPath)}");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error ⚠️", $"Could not load photo: {ex.Message}", "OK");
        }
    }

    private async void OnCapturePhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            if (MediaPicker.Default.IsCaptureSupported)
            {
                var photo = await MediaPicker.Default.CapturePhotoAsync();

                if (photo != null)
                {
                    string localPath = Path.Combine(FileSystem.CacheDirectory, photo.FileName);

                    using var stream = await photo.OpenReadAsync();
                    using var localStream = File.Create(localPath);
                    await stream.CopyToAsync(localStream);

                    await Shell.Current.GoToAsync($"{nameof(CropPhotoPage)}?ImagePath={Uri.EscapeDataString(localPath)}");
                }
            }
            else
            {
                await DisplayAlertAsync("No Camera 🚫", "Camera functionality is not supported on this platform device.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error ⚠️", $"Camera access failed: {ex.Message}", "OK");
        }
    }
}

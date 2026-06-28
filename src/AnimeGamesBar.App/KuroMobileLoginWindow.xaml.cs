using AnimeGamesBar.App.Services.Kuro;
using AnimeGamesBar.App.Services.Skland;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AnimeGamesBar.App;

public sealed partial class KuroMobileLoginWindow : Window
{
    private readonly TaskCompletionSource<SklandCredential?> _completion = new();
    private readonly KuroMobileLoginClient _client;
    private readonly SklandCredential _currentCredential;
    private readonly string _deviceId;

    public KuroMobileLoginWindow(KuroMobileLoginClient client, SklandCredential currentCredential)
    {
        InitializeComponent();

        _client = client;
        _currentCredential = currentCredential;
        _deviceId = KuroMobileLoginClient.EnsureDeviceId(currentCredential.DeviceId);

        Closed += OnClosed;
    }

    public async Task<SklandCredential?> WaitForCredentialAsync(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() => _completion.TrySetCanceled(cancellationToken));
        return await _completion.Task;
    }

    private async void SendCodeButton_OnClick(object sender, RoutedEventArgs e)
    {
        var mobile = MobileBox.Text.Trim();
        if (!IsValidMobile(mobile))
        {
            SetStatus("请输入 11 位手机号。", InfoBarSeverity.Warning);
            return;
        }

        await RunWithBusyStateAsync(async () =>
        {
            var result = await _client.SendSmsCodeAsync(mobile, _deviceId, CancellationToken.None);
            if (result.GeeTestRequired)
            {
                SetStatus("库街区要求极验风控验证，当前短信登录无法继续。请改用网页登录或手动填写库街区 Token。", InfoBarSeverity.Warning);
                return;
            }

            SetStatus("验证码已发送，请查收短信。", InfoBarSeverity.Success);
            CodeBox.Focus(FocusState.Programmatic);
        });
    }

    private async void FinishButton_OnClick(object sender, RoutedEventArgs e)
    {
        var mobile = MobileBox.Text.Trim();
        var code = CodeBox.Text.Trim();
        if (!IsValidMobile(mobile))
        {
            SetStatus("请输入 11 位手机号。", InfoBarSeverity.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            SetStatus("请输入短信验证码。", InfoBarSeverity.Warning);
            return;
        }

        await RunWithBusyStateAsync(async () =>
        {
            var credential = await _client.LoginBySmsCodeAsync(
                _currentCredential,
                mobile,
                code,
                _deviceId,
                CancellationToken.None);

            _completion.TrySetResult(credential);
            Close();
        });
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        _completion.TrySetResult(null);
        Close();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _completion.TrySetResult(null);
    }

    private async Task RunWithBusyStateAsync(Func<Task> action)
    {
        SetBusy(true);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        SendCodeButton.IsEnabled = !isBusy;
        FinishButton.IsEnabled = !isBusy;
    }

    private void SetStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }

    private static bool IsValidMobile(string value)
    {
        return value.Length == 11 && value.All(char.IsDigit);
    }
}

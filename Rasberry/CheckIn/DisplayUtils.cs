using System;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Notifications;


namespace CheckIn
{
    public static class DisplayUtils
    {
        public static void ShowToast(string mess, bool showicon = true, string title = "Money Lover", Uri uri = null,
               bool autoHide = true)
        {
            if (string.IsNullOrEmpty(mess))
                return;
            try
            {
                CoreApplication.MainView?.CoreWindow?.Dispatcher?.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    try
                    {
                        var toast = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
                        toast.GetElementsByTagName("text")[0].AppendChild(toast.CreateTextNode(title));
                        toast.GetElementsByTagName("text")[1].AppendChild(toast.CreateTextNode(mess));

                        var toastNode = toast.SelectSingleNode("/toast");
                        var audio = toast.CreateElement("audio");
                        audio.SetAttribute("silent", "true");
                        toastNode.AppendChild(audio);

                        var t = new ToastNotification(toast);
                        if (autoHide) t.ExpirationTime = DateTimeOffset.UtcNow.AddSeconds(10);
                        ToastNotificationManager.CreateToastNotifier().Show(t);
                    }
                    finally { }
                });
            }
            finally { }
        }
    }
}

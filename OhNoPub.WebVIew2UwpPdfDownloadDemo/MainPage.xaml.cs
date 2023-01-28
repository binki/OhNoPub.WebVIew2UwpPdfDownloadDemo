using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace OhNoPub.WebVIew2UwpPdfDownloadDemo
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        static int downloadIndex;

        public MainPage()
        {
            this.InitializeComponent();

            WebView2.CoreWebView2Initialized += (sender, e) =>
            {
                WebView2.CoreWebView2.DownloadStarting += async (sender, e) =>
                {
                    // Allow our event handler to do asynchronous things.
                    using var deferral = e.GetDeferral();

                    // Generate a process-unique file name.
                    var currentDownloadIndex = Interlocked.Increment(ref downloadIndex);
                    // Use the provided file name's extension, but ignore other parts of the file name.
                    var downloadFileName = $"download-{currentDownloadIndex}{Path.GetExtension(e.DownloadOperation.ResultFilePath)}";

                    // Use a folder which our app has access to and which will, eventually, automatically get cleaned up
                    // in the event of app termination.
                    var folder = ApplicationData.Current.TemporaryFolder;

                    // Inform WebView2 of the path. Otherwise, we won't be able to access the resulting file from the app.
                    e.ResultFilePath = Path.Combine(folder.Path, downloadFileName);

                    // Do ensure that we remove any existing file. I don't know how to get a per-app-instance/window temporary
                    // folder (maybe the folder we got already is this, but I wouldn't count on it) (if this is a stale temporary
                    // folder, there might be abandoned downloads) (if there are multiple instances of our app with the same temporary
                    // directory, then we could actually run into concurrency issues here; though since this is at least user scoped, it is
                    // unlikely that the user is going to be able to actively use multiple instances of our app concurrently enough that
                    // this is an issue).
                    var maybeExistingFile = await folder.TryGetItemAsync(downloadFileName);
                    if (maybeExistingFile is not null) await maybeExistingFile.DeleteAsync();

                    // Hide the downloading GUI. This prevents the user from interacting from the download,
                    // but also gives us the responsibility of showing progress and reporting failures to the user.
                    // TODO: Add GUI stuff to show progress (unless the user just knows that if it doesn't show up in a few seconds they are plumb out of luck).
                    e.Handled = true;
                    e.DownloadOperation.StateChanged += async (sender, e) =>
                    {
                        if (sender.State == Microsoft.Web.WebView2.Core.CoreWebView2DownloadState.Completed)
                        {
                            var file = await folder.GetFileAsync(downloadFileName);
                            // Read the file into memory and display it. This lets us delete the temporarily file immediately so that
                            // we do not have to worry about cleaning it up later.
                            //
                            // If you expect the files to be large, you actaully want to avoid using the data URI and instead
                            // should feed the actual file path to the WebView2. However, then you will need to detect
                            // when the user is done with the file so that it can be deleted to avoid using up too much space.
                            using (var stream = await file.OpenStreamForReadAsync())
                            {
                                var p = await file.GetBasicPropertiesAsync();
                                var buffer = new byte[p.Size];
                                var position = 0;
                                while (position < buffer.Length) position += await stream.ReadAsync(buffer, position, buffer.Length - position);
                                WebView2.CoreWebView2.Navigate($"data:application/pdf;base64,{Convert.ToBase64String(buffer)}");
                            }

                            // Clean the file since we don't need it anymore.
                            await file.DeleteAsync();
                        }
                    };
                    deferral.Complete();
                };
            };
        }
    }
}

using System;
using System.Threading.Tasks;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

public class VoiceNote
{
    private const string voiceFile = "audio.m4a";
    private string appFile;
    private MediaCapture voiceCap;
    private InMemoryRandomAccessStream buffer;

    public static bool Recording;

    private async Task<bool> init()
    {
        if (buffer != null)
        {
            buffer.Dispose();
        }
        buffer = new InMemoryRandomAccessStream();
        if (voiceCap != null)
        {
            voiceCap.Dispose();
        }
        try
        {
            MediaCaptureInitializationSettings capSettings = new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Audio
            };
            voiceCap = new MediaCapture();
            await voiceCap.InitializeAsync(capSettings);
            voiceCap.RecordLimitationExceeded += (MediaCapture sender) =>
            {
                StopRecording();
                throw new Exception("Recording has become too long, please try again.");
            };
            voiceCap.Failed += (MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs) =>
            {
                Recording = false;
                throw new Exception(string.Format("Code: {0}. {1}", errorEventArgs.Code, errorEventArgs.Message));
            };
        }
        catch (Exception ex)
        {
            if (ex.InnerException != null && ex.InnerException.GetType() == typeof(UnauthorizedAccessException))
            {
                throw ex.InnerException;
            }
            throw;
        }
        return true;
    }

    public async void RecordVoice() //Starts the voice recording
    {
        await init();
        await voiceCap.StartRecordToStreamAsync(MediaEncodingProfile.CreateM4a(AudioEncodingQuality.Auto), buffer);
        if (Recording) throw new InvalidOperationException("Please finish current recording.");
        Recording = true;
    }

    public async void StopRecording() //Stops the voice recording
    {
        await voiceCap.StopRecordAsync();
        Recording = false;
    }

    public async Task PlayVoice(CoreDispatcher dispatcher) //Plays the voice recording back
    {
        MediaElement playVoice = new MediaElement();
        IRandomAccessStream voice = buffer.CloneStream();
        if (voice == null) throw new ArgumentNullException("BUFFER");
        StorageFolder appFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
        if (!string.IsNullOrEmpty(appFile))
        {
            StorageFile sf = await appFolder.GetFileAsync(appFile);
            await sf.DeleteAsync();
        }
        await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
        {
            StorageFile file = await appFolder.CreateFileAsync(voiceFile, CreationCollisionOption.GenerateUniqueName);
            appFile = file.Name;
            using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                await RandomAccessStream.CopyAndCloseAsync(voice.GetInputStreamAt(0), fileStream.GetOutputStreamAt(0));
                await voice.FlushAsync();
                voice.Dispose();
            }
            IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
            playVoice.SetSource(stream, file.FileType);
            playVoice.Play();
        });
    }
}
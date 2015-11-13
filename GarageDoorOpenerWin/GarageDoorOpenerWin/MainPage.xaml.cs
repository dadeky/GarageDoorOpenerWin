using System;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=391641

namespace GarageDoorOpenerWin
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private StreamSocket chatSocket;
        private DataWriter chatWriter;
        private HostName hn;
        //private string remoteServiceName = "\\\\?\\BTHENUM#{00001101-0000-1000-8000-00805f9b34fb}_LOCALMFG&001d#6&2914267d&0&F40669919D20_C00000000#{b142fc3e-fa4e-460b-8abc-072b628b3c70}";
        private string remoteServiceName = "\\\\?\\BTHENUM#{00001101-0000-1000-8000-00805f9b34fb}_LOCALMFG&001d#6&2914267d&0&98D3314016E7_C00000000#{b142fc3e-fa4e-460b-8abc-072b628b3c70}";
        public MainPage()
        {
            this.InitializeComponent();
            chatSocket = null;
            chatWriter = null;
            hn = new HostName("98:D3:31:40:16:E7");
            //hn = new HostName("f4:06:69:91:9d:20");
            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

        void App_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            Disconnect();
        }

        private async void OpenDoor(object sender, RoutedEventArgs e)
        {
            //write to bluetooth stream {c:ope}
            try
            {
                string str = "{c:ope}";
                chatWriter.WriteString(str);
                await chatWriter.StoreAsync();
                msgBlock.Text = "Open command sent";
            }
            catch (Exception ex)
            {
                msgBlock.Text = ex.Message;
            }
        }

        private async void StopDoor(object sender, RoutedEventArgs e)
        {
            //write to bluetooth stream {c:sto}
            try
            {
                string str = "{c:sto}";
                chatWriter.WriteString(str);
                await chatWriter.StoreAsync();
                msgBlock.Text = "Stop command sent";
            }
            catch (Exception ex)
            {
                msgBlock.Text = ex.Message;
            }
            //await ConnectBt();
        }

        private async void CloseDoor(object sender, RoutedEventArgs e)
        {
            //write to bluetooth stream {c:clo}
            try
            {
                string str = "{c:clo}";
                chatWriter.WriteString(str);
                await chatWriter.StoreAsync();
                msgBlock.Text = "Close command sent";
            }
            catch (Exception ex)
            {
                msgBlock.Text = ex.Message;
            }
        }

        private async Task SendSettingsRequest()
        {
            //write to bluetooth stream {g}
            try
            {
                string str = "{g}";
                chatWriter.WriteString(str);
                await chatWriter.StoreAsync();
            }
            catch (Exception ex)
            {
                msgBlock.Text = ex.Message;
            }
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            await ConnectBt();
        }

        private async Task ConnectBt()
        {
            try
            {
                msgBlock.Text = "Connecting ...";
                lock (this)
                {
                    chatSocket = new StreamSocket();
                }
                await chatSocket.ConnectAsync(hn, remoteServiceName);
                chatWriter = new DataWriter(chatSocket.OutputStream);
                msgBlock.Text = "Connected to: " + hn;
                DataReader chatReader = new DataReader(chatSocket.InputStream);
                chatReader.InputStreamOptions = InputStreamOptions.Partial;
                await SendSettingsRequest();
                ReceiveStringLoop(chatReader);
            }
            catch (Exception ex)
            {
                msgBlock.Text = "Error connecting: " + ex.Message;
            }
        }

        private void Disconnect()
        {
            if (chatWriter != null)
            {
                chatWriter.DetachStream();
                chatWriter = null;
            }

            lock (this)
            {
                if (chatSocket != null)
                {
                    chatSocket.Dispose();
                    chatSocket = null;
                }
            }
        }

        private async void ReceiveStringLoop(DataReader chatReader)
        {
            try
            {
                string jsonString = "";
                string s = "";
                bool stringComplete;
                while (true)
                {
                    stringComplete = false;
                    if (chatReader.UnconsumedBufferLength == 0)
                    {
                        await chatReader.LoadAsync(256);
                    }

                    while (chatReader.UnconsumedBufferLength > 0)
                    {
                        s = chatReader.ReadString(1);
                        if (s == "\r" || s == "\n")
                        {
                            stringComplete = true;
                            break;
                        }
                        else
                        {

                            jsonString += s;
                        }
                    }

                    JsonObject jsonObject;
                    if (JsonObject.TryParse(jsonString, out jsonObject))
                    {
                        //reading the current sensor
                        //{"sensor":{"current":"512"}}
                        jsonString = "";
                        if (jsonObject.ContainsKey("sensor"))
                        {
                            JsonObject sensorJsonObject = jsonObject.GetNamedObject("sensor");
                            if (sensorJsonObject.ContainsKey("current"))
                            {
                                string curr = sensorJsonObject.GetNamedString("current", "");
                                int current = Int32.Parse(curr);
                                TensionProgressText.Text = "Tension: " + curr + "/1023";
                                ElectricCurrentProgressBar.Value = current;
                            }
                        }

                        //reading the arduino settings
                        //{"ard_settings":{"speedUp":"100","speedDown":"200","maxCurrentUp":"300","maxCurrentDown":"400"}}
                        if (jsonObject.ContainsKey("ard_settings"))
                        {
                            JsonObject settingsJsonObject = jsonObject.GetNamedObject("ard_settings");
                            if (settingsJsonObject.ContainsKey("maxCurrentDown"))
                            {
                                string mcd = settingsJsonObject.GetNamedString("maxCurrentDown", "");
                                int maxCurrentDown = Int32.Parse(mcd);
                                ElectricalCurrentSlider.Value = maxCurrentDown;
                                TensionText.Text = "Tension threshold: " + mcd + "/1023";
                            }
                        }
                    }
                    else
                    {
                        if (stringComplete)
                        {
                            jsonString = "";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lock (this)
                {
                    if (chatSocket == null)
                    {
                        // Do not print anything here -  the user closed the socket.
                    }
                    else
                    {
                        msgBlock.Text = ex.Message;
                        //Disconnect();
                    }
                }
            }
        }

        private async void ChangeDownCurrent(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            double curr = ElectricalCurrentSlider.Value;
            string current = Convert.ToString(curr);
            try
            {
                string sendString = "{s:mcd:" + current + "}";
                chatWriter.WriteString(sendString);
                await chatWriter.StoreAsync();
                msgBlock.Text = "Down current: " + current;
                TensionText.Text = "Tension threshold: " + current + "/1023";
            }
            catch (Exception ex)
            {
                msgBlock.Text = "Error sending data: " + ex.Message;
            }
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await ConnectBt();
        }
    }

}

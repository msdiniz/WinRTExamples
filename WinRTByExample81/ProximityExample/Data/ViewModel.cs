﻿namespace ProximityExample.Data
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Text;
    using System.Threading.Tasks;

    using Windows.Networking.Proximity;
    using Windows.Networking.Sockets;
    using Windows.UI.Core;

    using ViewModelHelper;

    public class ViewModel : BaseViewModel
    {
        private const string DiscoveryText = "WinRT By Example Proximity";

        private readonly Action<Action> routeToUiThread = action => action();

        private readonly CoreDispatcher dispatcher;

        private readonly bool isDesignTime;

        private ProximityDevice proximityDevice;

        public ViewModel()
        {
            this.Peers = new ObservableCollection<FoundPeer>();

            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                this.isDesignTime = true;
                this.errorMessage = "Design-time error message.";
                this.Peers.Add(new FoundPeer { Name = "Design Peer"} );
                this.Peers.Add(new FoundPeer { Name = "Another Design Peer", Text = "Some Discovery Text"} );
                this.selectedPeer = this.Peers[0];
                this.connectedPeer = "Design-time Connected Peer";
                this.lastMessage = "Something from a peer.";
            }
            else
            {
                this.proximityDevice = ProximityDevice.GetDefault();
                if (this.proximityDevice != null)
                {
                    this.proximityDevice.DeviceArrived += this.ProximityDeviceDeviceArrived;
                    this.proximityDevice.DeviceDeparted += this.ProximityDeviceDeviceDeparted;
                }
                this.dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
                this.routeToUiThread = action => this.dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action());
                this.supportsTriggeredConnect = (PeerFinder.SupportedDiscoveryTypes & PeerDiscoveryTypes.Triggered) > 0;
                this.supportsBrowseConnect = (PeerFinder.SupportedDiscoveryTypes & PeerDiscoveryTypes.Browse) > 0;
            }

            Connect = new ActionCommand(async obj => await this.ConnectCommand(), obj => this.SelectedPeer != null && !this.IsConnecting);
            SendMessage = new ActionCommand(obj => this.SendMessageCommand(), obj => this.peerConnectionSocket != null);
            Browse = new ActionCommand(async obj => await this.BrowseCommand(), obj => this.IsAdvertising && !this.IsBrowsing && !this.IsConnecting);
            StartAdvertising = new ActionCommand(obj => this.StartAdvertisingCommand(), obj => this.SupportsPeer && !this.IsAdvertising);
            StopAdvertising = new ActionCommand(obj => this.StopAdvertisingCommand(), obj => this.IsAdvertising && !this.IsConnecting);
        }

        private bool isAdvertising, isBrowsing, isConnecting, supportsTriggeredConnect, supportsBrowseConnect;

        private string errorMessage, connectedPeer, lastMessage, messageToSend;

        private PeerSocket peerConnectionSocket;

        private FoundPeer selectedPeer; 

        public ActionCommand StartAdvertising { get; private set; }

        public ActionCommand StopAdvertising { get; private set; }

        public ActionCommand Browse { get; private set; }

        public ActionCommand Connect { get; private set; }

        public ActionCommand SendMessage { get; private set; }

        public ObservableCollection<FoundPeer> Peers { get; private set; }

        public bool HasProximityDevice
        {
            get
            {
                return this.proximityDevice != null;
            }
        }
        
        public bool IsAdvertising
        {
            get
            {
                return this.isAdvertising;
            }
            set
            {
                if (this.isAdvertising == value)
                {
                    return;
                }

                if (value)
                {
                    this.StartPeerFinder();
                }
                else
                {
                    this.StopPeerFinder();
                }

                this.isAdvertising = value;
                this.OnPropertyChanged();
                this.StartAdvertising.OnCanExecuteChanged();
                this.StopAdvertising.OnCanExecuteChanged();
                this.Browse.OnCanExecuteChanged();
            }
        }

        public bool IsBrowsing
        {
            get
            {
                return this.isBrowsing;
            }

            set
            {
                this.isBrowsing = value;
                this.OnPropertyChanged();
                this.Browse.OnCanExecuteChanged();
            }
        }

        public bool IsConnecting
        {
            get
            {
                return this.isConnecting;
            }

            set
            {
                this.isConnecting = value;
                this.OnPropertyChanged();
                this.Connect.OnCanExecuteChanged();
                this.Browse.OnCanExecuteChanged();
                this.StopAdvertising.OnCanExecuteChanged();
            }
        }

        public bool SupportsTriggeredConnect
        {
            get
            {
                return this.supportsTriggeredConnect;
            }

            set
            {
                this.supportsTriggeredConnect = value;
                this.OnPropertyChanged();                
                this.OnPropertyChanged("SupportsPeer");
            }
        }

        public bool SupportsBrowseConnect
        {
            get
            {
                return this.supportsBrowseConnect;
            }

            set
            {
                this.supportsBrowseConnect = value;
                this.OnPropertyChanged();             
                this.OnPropertyChanged("SupportsPeer");
            }
        }

        public string ErrorMessage
        {
            get
            {
                return this.errorMessage;
            }

            set
            {
                this.errorMessage = value;
                this.OnPropertyChanged();
            }
        }

        public string ConnectedPeer
        {
            get
            {
                return this.connectedPeer;
            }

            set
            {
                this.connectedPeer = value;
                this.OnPropertyChanged();
            }
        }

        public string LastMessage
        {
            get
            {
                return this.lastMessage;
            }

            set
            {
                this.lastMessage = value;
                this.OnPropertyChanged();
            }
        }

        public string MessageToSend 
        {
            get
            {
                return this.messageToSend;
            }

            set
            {
                this.messageToSend = value;
                this.OnPropertyChanged();             
            }
        }

        public bool SupportsPeer
        {
            get
            {
                return this.supportsTriggeredConnect || this.supportsBrowseConnect;
            }
        }

        public FoundPeer SelectedPeer
        {
            get
            {
                return this.selectedPeer;
            }

            set
            {
                this.selectedPeer = value;
                this.OnPropertyChanged();
                this.Connect.OnCanExecuteChanged();
            }
        }

        public override bool IsDesignTime
        {
            get
            {
                return this.isDesignTime;
            }
        }

        public override Action<Action> RouteToUiThread
        {
            get
            {
                return this.routeToUiThread;
            }
        }

        private void StartAdvertisingCommand()
        {
            if (this.IsAdvertising)
            {
                this.IsAdvertising = false;
            }

            this.IsAdvertising = true;            
        }

        private void StopAdvertisingCommand()
        {
            if (this.IsAdvertising)
            {
                this.IsAdvertising = false;                
            }
        }

        private void StartPeerFinder()
        {
            this.ErrorMessage = string.Empty;

            try
            {
                PeerFinder.TriggeredConnectionStateChanged += this.PeerFinderTriggeredConnectionStateChanged;
                PeerFinder.ConnectionRequested += this.PeerFinderConnectionRequested;
                
                PeerFinder.Role = PeerRole.Peer;
                PeerFinder.DiscoveryData = Encoding.UTF8.GetBytes(DiscoveryText).AsBuffer();
                
                PeerFinder.Start();
            }
            catch (Exception ex)
            {
                this.ErrorMessage = ex.Message;
            }
        }

        private void StopPeerFinder()
        {
            this.ErrorMessage = string.Empty;

            try
            {
                if (this.peerConnectionSocket != null)
                {
                    this.DisposeSocket();
                }

                this.SelectedPeer = null;
                this.ConnectedPeer = null; 
                this.Peers.Clear();

                PeerFinder.TriggeredConnectionStateChanged -= this.PeerFinderTriggeredConnectionStateChanged;
                PeerFinder.ConnectionRequested -= this.PeerFinderConnectionRequested;
                PeerFinder.Stop();
            }
            catch (Exception ex)
            {
                this.ErrorMessage = ex.Message;
            }
        }

        private async Task BrowseCommand()
        {
            this.IsBrowsing = true;
            this.ErrorMessage = string.Empty;

            this.SelectedPeer = null;
            this.Peers.Clear();

            try
            {
                var peers = await PeerFinder.FindAllPeersAsync();

                if (peers != null && peers.Count > 0)
                {
                    foreach (var peer in peers)
                    {
                        this.Peers.Add(new FoundPeer(peer));
                    }                    
                }
            }
            catch (Exception ex)
            {
                this.ErrorMessage = ex.Message;
            }

            this.IsBrowsing = false;
        }

        private async Task ConnectCommand()
        {
            this.IsConnecting = true;

            try
            {
                this.ConnectedPeer = "Connecting...";
                var socket = await PeerFinder.ConnectAsync(this.SelectedPeer.Information);
                this.InitializeSocket(socket);
                this.ConnectedPeer = this.SelectedPeer.Name; 
            }
            catch (Exception ex)
            {
                this.ErrorMessage = ex.Message;
                this.ConnectedPeer = "Connection Failed.";
            }

            this.IsConnecting = false;
        }

        private async void SendMessageCommand()
        {
            if (string.IsNullOrWhiteSpace(this.MessageToSend) || this.peerConnectionSocket == null)
            {
                return;
            }

            var message = this.MessageToSend;
            this.MessageToSend = string.Empty;
            
            await this.peerConnectionSocket.WriteMessage(message);
        }

        private void InitializeSocket(StreamSocket socket)
        {
            if (this.peerConnectionSocket != null)
            {
                this.DisposeSocket();
            }

            try
            {
                this.peerConnectionSocket = new PeerSocket(socket);
                this.peerConnectionSocket.ErrorRaisedEvent += this.PeerConnectionSocketErrorRaisedEvent;
                this.peerConnectionSocket.MessageRaisedEvent += this.PeerConnectionSocketMessageRaisedEvent;
                this.RouteToUiThread(() => this.SendMessage.OnCanExecuteChanged());
                this.peerConnectionSocket.ReadLoop();
            }
            catch (Exception ex)
            {
                this.RouteToUiThread(() => this.ErrorMessage = ex.Message);
            }
        }

        private void DisposeSocket()
        {
            if (this.peerConnectionSocket == null)
            {
                return;
            }

            this.peerConnectionSocket.ErrorRaisedEvent -= this.PeerConnectionSocketErrorRaisedEvent;
            this.peerConnectionSocket.MessageRaisedEvent -= this.PeerConnectionSocketMessageRaisedEvent;
            this.peerConnectionSocket.Dispose();
            this.peerConnectionSocket = null;
            this.RouteToUiThread(
                () =>
                    {
                        this.ConnectedPeer = null;
                        this.SendMessage.OnCanExecuteChanged();
                    });
        }

        private async void PeerFinderConnectionRequested(object sender, ConnectionRequestedEventArgs args)
        {
            if (this.isConnecting)
            {
                return; 
            }

            try
            {
                this.RouteToUiThread(
                    () =>
                        {
                            this.IsConnecting = true;
                            this.ConnectedPeer = string.Format(
                                "Incoming request from {0}...",
                                args.PeerInformation.DisplayName);
                        });                

                var socket = await PeerFinder.ConnectAsync(args.PeerInformation);
                this.InitializeSocket(socket);
                this.RouteToUiThread(
                    () =>
                        {
                            this.IsConnecting = false;
                            var peer =
                                this.Peers.FirstOrDefault(p => p.Information.Id == args.PeerInformation.Id);
                            if (peer == null)
                            {
                                peer = new FoundPeer(args.PeerInformation);
                                this.Peers.Add(peer);
                            }
                            this.SelectedPeer = peer;
                            this.ConnectedPeer = peer.Name;                            
                        });
            }
            catch (Exception ex)
            {
                this.RouteToUiThread(() => this.ErrorMessage = ex.Message);
            }
        }

        private void PeerConnectionSocketMessageRaisedEvent(object sender, string e)
        {
            this.RouteToUiThread(() => this.LastMessage = e);
        }

        private void PeerConnectionSocketErrorRaisedEvent(object sender, string e)
        {
            this.RouteToUiThread(() => this.ErrorMessage = e);
            this.DisposeSocket();
        }

        private void ProximityDeviceDeviceDeparted(ProximityDevice sender)
        {
            this.RouteToUiThread(
                () =>
                    {
                        this.LastMessage = string.Format("Device left range.");
                    });
        }

        private void ProximityDeviceDeviceArrived(ProximityDevice sender)
        {
            this.RouteToUiThread(
                () =>
                {
                    this.LastMessage = string.Format("Device entered range.");
                });
        }

        private void PeerFinderTriggeredConnectionStateChanged(object sender, TriggeredConnectionStateChangedEventArgs args)
        {
            switch (args.State)
            {
                case TriggeredConnectState.Connecting:
                case TriggeredConnectState.PeerFound:
                    this.RouteToUiThread(
                        () =>
                            {
                                this.IsConnecting = true;
                                this.ConnectedPeer = "Tap complete, opening socket.";
                            });
                    break;
                
                case TriggeredConnectState.Completed:
                    this.RouteToUiThread(
                        () =>
                            {
                                this.IsConnecting = false;                                
                            });
                    this.InitializeSocket(args.Socket);                            
                    break;
               
                default:
                    if (args.State != TriggeredConnectState.Listening)
                    {
                        this.RouteToUiThread(
                            () =>
                                {
                                    this.ErrorMessage = string.Format("Issue connecting to tap socket: {0}", args.State);
                                });
                    }
                    break;
            }
        }

        public void Dispose()
        {
            if (this.proximityDevice != null)
            {
                this.proximityDevice.DeviceDeparted -= this.ProximityDeviceDeviceDeparted;
                this.proximityDevice.DeviceArrived -= this.ProximityDeviceDeviceArrived;
                this.proximityDevice = null;
            }

            this.DisposeSocket();

            if (this.isAdvertising)
            {
                PeerFinder.Stop();
            }
        }
    }
}

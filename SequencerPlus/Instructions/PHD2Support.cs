using Newtonsoft.Json.Linq;
using NINA.Core.Locale;
using NINA.Core.Utility.Notification;
using NINA.Core.Utility;
using NINA.Profile;
using NINA.WPF.Base.Mediator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;

namespace NINA.Plugin.SequencerPlus {
    public class PHD2Support {
        private static object lockobj = new object();

        private bool _connected;

        public bool Connected {
            get => _connected;
            private set {
                lock (lockobj) {
                    _connected = value;
                }
            }
        }

        static IGuiderMediator GuiderMediator { get; set; }
        static IProfileService ProfileService { get; set; }

        private static CancellationTokenSource _clientCTS;

        private static TcpState GetState(TcpClient tcpClient) {
            var foo = IPGlobalProperties.GetIPGlobalProperties()
              .GetActiveTcpConnections()
              .SingleOrDefault(x => x.LocalEndPoint.Equals(tcpClient.Client.LocalEndPoint));
            return foo != null ? foo.State : TcpState.Unknown;
        }

        private static TaskCompletionSource<bool> _tcs;
        private static IPAddress phd2Ip;

        private static bool PHD2Listening = false;

        private static async Task RunListener() {

            if (!GuiderMediator.GetInfo().Connected) return;

            if (PHD2Listening) return;

            PHD2Listening = true;

            var jls = new JsonLoadSettings() { LineInfoHandling = LineInfoHandling.Ignore, CommentHandling = CommentHandling.Ignore };
            _tcs = new TaskCompletionSource<bool>();
            _clientCTS?.Dispose();
            _clientCTS = new CancellationTokenSource();

            var serverPort = ProfileService.ActiveProfile.GuiderSettings.PHD2ServerPort;
            var serverHost = ProfileService.ActiveProfile.GuiderSettings.PHD2ServerUrl;

            IPHostEntry hostEntry;
            try {
                hostEntry = DnsHelper.GetIPHostEntryByName(serverHost);
                phd2Ip = hostEntry.AddressList.First();
            } catch (Exception ex) {
                Logger.Error($"Failed to resolve PHD2 server {serverHost}: {ex.Message}");
                Notification.ShowError(string.Format(Loc.Instance["LblPhd2ServerHostNotResolved"], serverHost));
                return;
            }

            try {
                using var client = new TcpClient(AddressFamily.InterNetwork) {
                    NoDelay = true,
                };

                await client.ConnectAsync(phd2Ip, ProfileService.ActiveProfile.GuiderSettings.PHD2ServerPort);
                _tcs.TrySetResult(true);

                using NetworkStream s = client.GetStream();

                while (true) {
                    var state = GetState(client);
                    if (state == TcpState.CloseWait) {
                        throw new Exception(Loc.Instance["LblPhd2ServerConnectionLost"]);
                    }

                    var message = string.Empty;
                    while (s.DataAvailable) {
                        byte[] response = new byte[1024];
                        await s.ReadAsync(response, _clientCTS.Token);
                        message += Encoding.ASCII.GetString(response);
                    }

                    var lines = message.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                    foreach (string line in lines) {
                        if (!string.IsNullOrEmpty(line) && line.StartsWith('{')) {
                            JObject o = JObject.Parse(line, jls);
                            JToken t = o.GetValue("Event");
                            string phdevent = "";
                            if (t != null) {
                                phdevent = t.ToString();
                                Logger.Trace($"PHD2 event received - {o}");
                                ProcessEvent(phdevent, o);
                            }
                        }
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(500), _clientCTS.Token);
                }
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                Logger.Error(ex);
                Notification.ShowError("PHD2 Error: " + ex.Message);
                throw;
            } finally {
                _tcs.TrySetResult(false);
                PHD2Listening = false;
            }
        }

        private static string Phd2AppState = null;

        private static void ProcessEvent(string phdevent, JObject message) {
            Logger.Info("PHD2: " + phdevent);
            if (phdevent == "CalibrationFailed") {
                Logger.Warning("Reason: " + message.GetValue("Reason").ToString());
            } else if (phdevent == "AppState") {
                Phd2AppState = message.GetValue("State").ToString();
                Logger.Info("PHD2 AppState: " + Phd2AppState);
            }
        }


    }
}
